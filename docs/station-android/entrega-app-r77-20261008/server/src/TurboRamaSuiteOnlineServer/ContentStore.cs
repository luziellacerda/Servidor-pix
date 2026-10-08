using System.Data;
using System.Security.Cryptography;
using Npgsql;
using NpgsqlTypes;

namespace TurboRamaSuiteOnlineServer;

public sealed record ContentCatalogItemRecord(
    string ItemId,
    string Availability,
    ContentArtifactDescriptor? Descriptor,
    string? DescriptorHash,
    string? ReasonCode);

public sealed record ContentCatalogPageRecord(
    string CatalogIdentity,
    long CatalogSequence,
    IReadOnlyList<ContentCatalogItemRecord> Items,
    string? NextCursor);

public sealed record ContentGrantDraft(
    string GrantId,
    string TokenDigest,
    long ExpiresAt,
    string CorrelationId);

public sealed record IssuedContentGrantRecord(
    string GrantId,
    long ExpiresAt,
    long RangeStart);

public sealed record ClaimedContentGrantRecord(
    string GrantId,
    string CatalogIdentity,
    string ItemId,
    string ArtifactId,
    int ArtifactVersion,
    string ManifestIdentity,
    string DescriptorHash,
    long RangeStart,
    string SafeFileName,
    string FileExtension,
    string ExtractPolicy,
    string ContentType,
    string? SourceETag,
    string? SourceLastModified,
    byte[] UpstreamUrlCiphertext,
    byte[] UpstreamUrlNonce,
    byte[] UpstreamUrlTag,
    int KeyVersion);

public interface IContentControlStore
{
    Task<bool> IsProductionCatalogReadyAsync(CancellationToken cancellationToken);

    Task<ContentCatalogPageRecord> ReadCatalogPageAsync(
        ChallengeRecord challenge,
        CatalogPageContext context,
        long now,
        CancellationToken cancellationToken);

    Task<IssuedContentGrantRecord> CreateDownloadGrantAsync(
        ChallengeRecord challenge,
        DownloadAuthorizationContext context,
        ContentGrantDraft draft,
        long now,
        CancellationToken cancellationToken);
}

public interface IContentGatewayStore
{
    Task<bool> IsProductionGatewayReadyAsync(
        int activeKeyVersion,
        string keySetFingerprint,
        string allowlistFingerprint,
        int[] supportedKeyVersions,
        CancellationToken cancellationToken);

    Task<ClaimedContentGrantRecord> ClaimDownloadGrantAsync(
        string grantId,
        string tokenDigest,
        long requestedRangeStart,
        long now,
        CancellationToken cancellationToken);

    Task CompleteDownloadGrantAsync(
        string grantId,
        bool succeeded,
        string? failureCode,
        CancellationToken cancellationToken);

    Task<bool> IsDownloadGrantAuthorizationCurrentAsync(
        string grantId,
        long now,
        CancellationToken cancellationToken);
}

public sealed class PostgresContentStore : IContentControlStore, IContentGatewayStore,
    IAsyncDisposable
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly bool _ownsDataSource;

    public PostgresContentStore(NpgsqlDataSource dataSource) => _dataSource = dataSource;

    public PostgresContentStore(string connectionString)
    {
        _dataSource = NpgsqlDataSource.Create(connectionString);
        _ownsDataSource = true;
    }

    public ValueTask DisposeAsync() => _ownsDataSource
        ? _dataSource.DisposeAsync()
        : ValueTask.CompletedTask;

    public async Task<bool> IsProductionCatalogReadyAsync(
        CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand("""
            WITH eligible_delivery AS(
              SELECT d.source_system,d.source_purchase_id,d.source_item_key,
                     d.product_id,d.license_id
              FROM suite.suite_license_deliveries d
              JOIN suite.suite_licenses l ON l.license_id=d.license_id
              WHERE d.source_system='TURBOBOX_V1'
                AND d.source_product_sku='SUITE_LIFETIME_1_DEVICE'
                AND d.product_id=$1 AND d.provisioning_state='PROVISIONED'
                AND l.provisioning_origin='COMMERCE' AND l.product_id=$1
                AND l.status='ACTIVE'
                AND (d.financial_state='PAID' OR
                  (d.financial_state='SUSPENDED'
                   AND d.administrative_resume_source_version IS NOT NULL
                   AND d.administrative_resume_source_version=d.last_source_version
                   AND d.administrative_resume_actor IS NOT NULL
                   AND d.administrative_resume_reason IS NOT NULL
                   AND d.administrative_resume_request_id IS NOT NULL))),
            active_entitlement AS(
              SELECT e.license_id,e.scope,e.source_system,e.source_purchase_id,
                     e.source_item_key,e.product_id
              FROM suite.suite_content_entitlements e
              WHERE e.product_id=$1 AND e.scope='FULL_CATALOG' AND e.status='ACTIVE')
            SELECT EXISTS(
              SELECT 1
              FROM suite.suite_content_catalog_state cs
              JOIN suite.suite_content_snapshots s
                ON s.catalog_identity=cs.active_catalog_identity
              WHERE cs.product_id=$1 AND s.status='PUBLISHED' AND s.item_count=$2
                AND s.origin_active_key_version IS NOT NULL
                AND s.origin_key_set_fingerprint IS NOT NULL
                AND s.origin_allowlist_fingerprint IS NOT NULL
                AND (SELECT count(*) FROM suite.suite_content_items i
                     WHERE i.catalog_identity=s.catalog_identity)=$2
                AND (SELECT count(*) FROM suite.suite_content_items ready
                     WHERE ready.catalog_identity=s.catalog_identity
                       AND ready.status='READY')=s.ready_item_count
                AND (SELECT count(*) FROM suite.suite_content_items maintenance
                     WHERE maintenance.catalog_identity=s.catalog_identity
                       AND maintenance.status='MAINTENANCE')=s.maintenance_item_count)
              AND EXISTS(SELECT 1 FROM eligible_delivery)
              AND (SELECT count(*) FROM eligible_delivery)=
                  (SELECT count(*) FROM active_entitlement)
              AND NOT EXISTS(
                SELECT 1 FROM active_entitlement entitlement
                LEFT JOIN eligible_delivery delivery
                  ON delivery.license_id=entitlement.license_id
                 AND delivery.source_system=entitlement.source_system
                 AND delivery.source_purchase_id=entitlement.source_purchase_id
                 AND delivery.source_item_key=entitlement.source_item_key
                 AND delivery.product_id=entitlement.product_id
                WHERE delivery.license_id IS NULL)
              AND NOT EXISTS(
                SELECT 1 FROM eligible_delivery delivery
                WHERE (SELECT count(*) FROM active_entitlement entitlement
                  WHERE entitlement.license_id=delivery.license_id
                    AND entitlement.source_system=delivery.source_system
                    AND entitlement.source_purchase_id=delivery.source_purchase_id
                    AND entitlement.source_item_key=delivery.source_item_key
                    AND entitlement.product_id=delivery.product_id)<>1)
              AND NOT EXISTS(
                SELECT 1 FROM active_entitlement entitlement
                JOIN suite.suite_licenses license
                  ON license.license_id=entitlement.license_id
                WHERE license.provisioning_origin='LEGACY_ADMIN')
              AND NOT EXISTS(
                SELECT entitlement.license_id,entitlement.scope
                FROM active_entitlement entitlement
                GROUP BY entitlement.license_id,entitlement.scope
                HAVING count(*)>1)
            """);
        command.Parameters.AddWithValue(Protocol.ProductId);
        command.Parameters.AddWithValue(ContentProtocol.ExpectedProductionItemCount);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    public async Task<bool> IsProductionGatewayReadyAsync(
        int activeKeyVersion,
        string keySetFingerprint,
        string allowlistFingerprint,
        int[] supportedKeyVersions,
        CancellationToken cancellationToken)
    {
        if (activeKeyVersion < 1 || !ContentProtocol.IsSha256(keySetFingerprint) ||
            !ContentProtocol.IsSha256(allowlistFingerprint) ||
            supportedKeyVersions.Length == 0 ||
            supportedKeyVersions.Any(version => version < 1))
            return false;
        await using var command = _dataSource.CreateCommand("""
            SELECT EXISTS(
              SELECT 1
              FROM suite.suite_content_catalog_state cs
              JOIN suite.suite_content_snapshots s
                ON s.catalog_identity=cs.active_catalog_identity
              WHERE cs.product_id=$1 AND s.status='PUBLISHED' AND s.item_count=$2
                AND s.origin_active_key_version=$3
                AND s.origin_key_set_fingerprint=$4
                AND s.origin_allowlist_fingerprint=$5
                AND (SELECT count(*) FROM suite.suite_content_items i
                     WHERE i.catalog_identity=s.catalog_identity)=$2
                AND (SELECT count(*) FROM suite.suite_content_items ready
                     WHERE ready.catalog_identity=s.catalog_identity
                       AND ready.status='READY')=s.ready_item_count
                AND (SELECT count(*) FROM suite.suite_content_items maintenance
                     WHERE maintenance.catalog_identity=s.catalog_identity
                       AND maintenance.status='MAINTENANCE')=s.maintenance_item_count
                AND (SELECT count(*) FROM suite.suite_content_artifact_origins o
                     WHERE o.catalog_identity=s.catalog_identity)=
                    s.ready_item_count
                AND NOT EXISTS(
                  SELECT 1 FROM suite.suite_content_artifact_origins o
                  JOIN suite.suite_content_items i
                    ON i.catalog_identity=o.catalog_identity AND i.item_id=o.item_id
                  WHERE o.catalog_identity=s.catalog_identity AND i.status<>'READY')
                AND NOT EXISTS(
                  SELECT 1 FROM suite.suite_content_artifact_origins o
                  WHERE o.catalog_identity=s.catalog_identity
                    AND (NOT(o.key_version=ANY($6)) OR
                         octet_length(o.upstream_url_ciphertext) NOT BETWEEN 1 AND 4096 OR
                         octet_length(o.upstream_url_nonce)<>12 OR
                         octet_length(o.upstream_url_tag)<>16)))
            """);
        command.Parameters.AddWithValue(Protocol.ProductId);
        command.Parameters.AddWithValue(ContentProtocol.ExpectedProductionItemCount);
        command.Parameters.AddWithValue(activeKeyVersion);
        command.Parameters.AddWithValue(keySetFingerprint);
        command.Parameters.AddWithValue(allowlistFingerprint);
        command.Parameters.AddWithValue(supportedKeyVersions);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    public Task<ContentCatalogPageRecord> ReadCatalogPageAsync(
        ChallengeRecord challenge,
        CatalogPageContext context,
        long now,
        CancellationToken cancellationToken) => ExecuteSerializableAsync(async (connection, transaction, token) =>
    {
        _ = await LockAuthorizationAsync(connection, transaction, context.LicenseId,
            context.DeviceId, context.SessionId, now, token);

        string catalogIdentity;
        long catalogSequence;
        await using (var snapshot = new NpgsqlCommand("""
            SELECT s.catalog_identity,s.catalog_sequence
            FROM suite.suite_content_catalog_state cs
            JOIN suite.suite_content_snapshots s
              ON s.catalog_identity=cs.active_catalog_identity
            WHERE cs.product_id=$1 AND s.status='PUBLISHED'
              AND s.item_count=$2
              AND (SELECT count(*) FROM suite.suite_content_items item
                   WHERE item.catalog_identity=s.catalog_identity)=$2
              AND (SELECT count(*) FROM suite.suite_content_items ready
                   WHERE ready.catalog_identity=s.catalog_identity
                     AND ready.status='READY')=s.ready_item_count
              AND (SELECT count(*) FROM suite.suite_content_items maintenance
                   WHERE maintenance.catalog_identity=s.catalog_identity
                     AND maintenance.status='MAINTENANCE')=s.maintenance_item_count
            """, connection, transaction))
        {
            snapshot.Parameters.AddWithValue(Protocol.ProductId);
            snapshot.Parameters.AddWithValue(ContentProtocol.ExpectedProductionItemCount);
            await using var reader = await snapshot.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token))
                throw new SuiteException(503, "CONTENT_CATALOG_UNAVAILABLE",
                    "The content catalog is unavailable.");
            catalogIdentity = reader.GetString(0);
            catalogSequence = reader.GetInt64(1);
        }

        var afterItemId = ContentProtocol.DecodeCursor(context.Cursor);
        var responseItemLimit = Math.Min(context.PageSize,
            ContentProtocol.MaximumCatalogResponseItems);
        var rows = new List<ContentCatalogItemRecord>(responseItemLimit + 1);
        await using (var command = new NpgsqlCommand("""
            SELECT item_id,status,artifact_id,artifact_version,
                   safe_file_name,file_extension,extract_policy,manifest_identity,
                   descriptor_hash,maintenance_reason
            FROM suite.suite_content_items
            WHERE catalog_identity=$1 AND status IN('READY','MAINTENANCE') AND item_id>$2
            ORDER BY item_id
            LIMIT $3
            """, connection, transaction))
        {
            command.Parameters.AddWithValue(catalogIdentity);
            command.Parameters.AddWithValue(afterItemId);
            command.Parameters.AddWithValue(responseItemLimit + 1);
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
            {
                var availability = reader.GetString(1);
                ContentArtifactDescriptor? descriptor = null;
                string? descriptorHash = null;
                string? reasonCode = null;
                if (availability == ContentProtocol.ReadyAvailability)
                {
                    descriptor = new ContentArtifactDescriptor(
                        reader.GetString(2), reader.GetInt32(3), reader.GetString(4),
                        reader.GetString(5), reader.GetString(6), reader.GetString(7));
                    descriptorHash = reader.GetString(8);
                }
                else if (availability == ContentProtocol.MaintenanceAvailability &&
                         !reader.IsDBNull(9))
                    reasonCode = ContentProtocol.MaintenanceReasonCode;
                var item = new ContentCatalogItemRecord(reader.GetString(0), availability,
                    descriptor, descriptorHash, reasonCode);
                ValidateStoredItem(item);
                rows.Add(item);
            }
        }

        string? nextCursor = null;
        if (rows.Count > responseItemLimit)
        {
            rows.RemoveAt(rows.Count - 1);
            nextCursor = ContentProtocol.EncodeCursor(rows[^1].ItemId);
        }

        await ConsumeChallengeAsync(connection, transaction, challenge, now, token);
        await transaction.CommitAsync(token);
        return new ContentCatalogPageRecord(catalogIdentity, catalogSequence, rows, nextCursor);
    }, cancellationToken);

    public Task<IssuedContentGrantRecord> CreateDownloadGrantAsync(
        ChallengeRecord challenge,
        DownloadAuthorizationContext context,
        ContentGrantDraft draft,
        long now,
        CancellationToken cancellationToken) => ExecuteSerializableAsync(async (connection, transaction, token) =>
    {
        var authorization = await LockAuthorizationAsync(connection, transaction,
            context.LicenseId, context.DeviceId, context.SessionId, now, token);

        await using (var quota = new NpgsqlCommand(
                         "SELECT suite.enforce_suite_content_grant_quota($1,$2,$3)",
                         connection, transaction))
        {
            quota.Parameters.AddWithValue(context.LicenseId);
            quota.Parameters.AddWithValue(context.DeviceId);
            quota.Parameters.AddWithValue(context.SessionId);
            try { await quota.ExecuteNonQueryAsync(token); }
            catch (PostgresException exception) when (
                exception.MessageText == "SUITE_CONTENT_GRANT_QUOTA_EXCEEDED")
            {
                throw new SuiteException(429, "CONTENT_QUOTA_EXCEEDED",
                    "Content request quota was exceeded.");
            }
        }

        ContentCatalogItemRecord item;
        string? sourceETag;
        string? sourceLastModified;
        await using (var command = new NpgsqlCommand("""
            SELECT i.item_id,i.artifact_id,i.artifact_version,
                   i.safe_file_name,i.file_extension,i.extract_policy,i.manifest_identity,
                   i.descriptor_hash,i.source_etag,i.source_last_modified
            FROM suite.suite_content_catalog_state cs
            JOIN suite.suite_content_snapshots s
              ON s.catalog_identity=cs.active_catalog_identity AND s.status='PUBLISHED'
            JOIN suite.suite_content_items i
              ON i.catalog_identity=s.catalog_identity AND i.item_id=$3 AND i.status='READY'
            WHERE cs.product_id=$1 AND s.catalog_identity=$2
              AND s.item_count=$4
              AND (SELECT count(*) FROM suite.suite_content_items item
                   WHERE item.catalog_identity=s.catalog_identity)=$4
              AND (SELECT count(*) FROM suite.suite_content_items ready
                   WHERE ready.catalog_identity=s.catalog_identity
                     AND ready.status='READY')=s.ready_item_count
              AND (SELECT count(*) FROM suite.suite_content_items maintenance
                   WHERE maintenance.catalog_identity=s.catalog_identity
                     AND maintenance.status='MAINTENANCE')=s.maintenance_item_count
            """, connection, transaction))
        {
            command.Parameters.AddWithValue(Protocol.ProductId);
            command.Parameters.AddWithValue(context.CatalogIdentity);
            command.Parameters.AddWithValue(context.ItemId);
            command.Parameters.AddWithValue(ContentProtocol.ExpectedProductionItemCount);
            await using var reader = await command.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token))
                throw new SuiteException(404, "CONTENT_NOT_AVAILABLE",
                    "The requested content is not available.");
            var descriptor = new ContentArtifactDescriptor(
                reader.GetString(1), reader.GetInt32(2), reader.GetString(3),
                reader.GetString(4), reader.GetString(5), reader.GetString(6));
            item = new ContentCatalogItemRecord(reader.GetString(0),
                ContentProtocol.ReadyAvailability, descriptor, reader.GetString(7), null);
            sourceETag = reader.IsDBNull(8) ? null : reader.GetString(8);
            sourceLastModified = reader.IsDBNull(9) ? null : reader.GetString(9);
        }

        ValidateStoredItem(item);
        if (item.ItemId != context.ItemId ||
            item.Descriptor!.ArtifactId != context.ArtifactId ||
            item.Descriptor.ArtifactVersion != context.ArtifactVersion ||
            item.Descriptor.ManifestIdentity != context.ManifestIdentity ||
            !Protocol.FixedEquals(item.DescriptorHash!, context.DescriptorHash) ||
            (context.Offset > 0 &&
             context.SourceETag != (sourceETag ?? string.Empty) &&
             context.SourceLastModified != (sourceLastModified ?? string.Empty)))
            throw new SuiteException(409, "CONTENT_DESCRIPTOR_MISMATCH",
                "The authorized content descriptor is no longer current.");

        var expiresAt = Math.Min(draft.ExpiresAt, authorization.AuthorizedUntil);
        if (expiresAt <= now)
            throw new SuiteException(409, "SESSION_INVALID", "Session is not current.");

        await using (var insert = new NpgsqlCommand("""
            INSERT INTO suite.suite_content_grants(
              grant_id,token_digest,license_id,device_id,session_id,
              revocation_generation,authorized_until,catalog_identity,item_id,
              artifact_id,artifact_version,manifest_identity,descriptor_hash,
              range_start,content_length,sha256,source_etag,source_last_modified,
              state,correlation_id,expires_at)
            VALUES($1,$2,$3,$4,$5,$6,to_timestamp($7),$8,$9,$10,$11,$12,$13,
                   $14,$15,$16,$17,$18,'ISSUED',$19,to_timestamp($20))
            """, connection, transaction))
        {
            insert.Parameters.AddWithValue(draft.GrantId);
            insert.Parameters.AddWithValue(draft.TokenDigest);
            insert.Parameters.AddWithValue(context.LicenseId);
            insert.Parameters.AddWithValue(context.DeviceId);
            insert.Parameters.AddWithValue(context.SessionId);
            insert.Parameters.AddWithValue(authorization.RevocationGeneration);
            insert.Parameters.AddWithValue(authorization.AuthorizedUntil);
            insert.Parameters.AddWithValue(context.CatalogIdentity);
            insert.Parameters.AddWithValue(context.ItemId);
            insert.Parameters.AddWithValue(context.ArtifactId);
            insert.Parameters.AddWithValue(context.ArtifactVersion);
            insert.Parameters.AddWithValue(context.ManifestIdentity);
            insert.Parameters.AddWithValue(context.DescriptorHash);
            insert.Parameters.AddWithValue(context.Offset);
            insert.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Bigint,
                Value = DBNull.Value });
            insert.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Char,
                Value = DBNull.Value });
            insert.Parameters.AddWithValue((object?)sourceETag ?? DBNull.Value);
            insert.Parameters.AddWithValue((object?)sourceLastModified ?? DBNull.Value);
            insert.Parameters.AddWithValue(draft.CorrelationId);
            insert.Parameters.AddWithValue(expiresAt);
            await insert.ExecuteNonQueryAsync(token);
        }

        await ConsumeChallengeAsync(connection, transaction, challenge, now, token);
        await transaction.CommitAsync(token);
        return new IssuedContentGrantRecord(draft.GrantId, expiresAt, context.Offset);
    }, cancellationToken);

    public Task<ClaimedContentGrantRecord> ClaimDownloadGrantAsync(
        string grantId,
        string tokenDigest,
        long requestedRangeStart,
        long now,
        CancellationToken cancellationToken) => ExecuteSerializableAsync(async (connection, transaction, token) =>
    {
        ContentGrantRow row;
        await using (var command = new NpgsqlCommand("""
            SELECT g.token_digest,g.license_id,g.device_id,g.session_id,
                   g.revocation_generation,g.catalog_identity,g.item_id,
                   g.artifact_id,g.artifact_version,
                   g.manifest_identity,g.descriptor_hash,g.range_start,
                   g.state,extract(epoch from g.expires_at)::bigint,
                   extract(epoch from g.authorized_until)::bigint,
                   i.safe_file_name,i.file_extension,i.extract_policy,i.content_type,
                   i.source_etag,i.source_last_modified,o.upstream_url_ciphertext,
                   o.upstream_url_nonce,o.upstream_url_tag,o.key_version
            FROM suite.suite_content_grants g
            JOIN suite.suite_content_items i
              ON i.catalog_identity=g.catalog_identity AND i.item_id=g.item_id
             AND i.artifact_id=g.artifact_id
             AND i.artifact_version=g.artifact_version
             AND i.manifest_identity=g.manifest_identity
             AND i.descriptor_hash=g.descriptor_hash
             AND i.status='READY'
            JOIN suite.suite_content_artifact_origins o
              ON o.catalog_identity=i.catalog_identity AND o.item_id=i.item_id
            WHERE g.grant_id=$1
            FOR UPDATE OF g
            """, connection, transaction))
        {
            command.Parameters.AddWithValue(grantId);
            await using var reader = await command.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token)) NotAvailable();
            row = new ContentGrantRow(
                reader.GetString(0), reader.GetString(1), reader.GetString(2),
                reader.GetString(3), reader.GetInt64(4), reader.GetString(5),
                reader.GetString(6), reader.GetString(7), reader.GetInt32(8),
                reader.GetString(9), reader.GetString(10), reader.GetInt64(11),
                reader.GetString(12), reader.GetInt64(13), reader.GetInt64(14),
                reader.GetString(15), reader.GetString(16), reader.GetString(17),
                reader.GetString(18), reader.IsDBNull(19) ? null : reader.GetString(19),
                reader.IsDBNull(20) ? null : reader.GetString(20),
                (byte[])reader[21], (byte[])reader[22], (byte[])reader[23],
                reader.GetInt32(24));
        }

        if (!Protocol.FixedEquals(row.TokenDigest, tokenDigest) || row.State != "ISSUED" ||
            row.ExpiresAt <= now || row.AuthorizedUntil <= now ||
            row.RangeStart != requestedRangeStart)
            NotAvailable();

        var authorization = await LockAuthorizationAsync(connection, transaction,
            row.LicenseId, row.DeviceId, row.SessionId, now, token);
        if (authorization.RevocationGeneration != row.RevocationGeneration)
            NotAvailable();

        await using (var update = new NpgsqlCommand("""
            UPDATE suite.suite_content_grants
            SET state='CLAIMED',claimed_at=clock_timestamp(),
                last_authorized_at=clock_timestamp()
            WHERE grant_id=$1 AND state='ISSUED'
            """, connection, transaction))
        {
            update.Parameters.AddWithValue(grantId);
            if (await update.ExecuteNonQueryAsync(token) != 1) NotAvailable();
        }

        await transaction.CommitAsync(token);
        return new ClaimedContentGrantRecord(
            grantId, row.CatalogIdentity, row.ItemId, row.ArtifactId,
            row.ArtifactVersion, row.ManifestIdentity, row.DescriptorHash,
            row.RangeStart, row.SafeFileName,
            row.FileExtension, row.ExtractPolicy, row.ContentType, row.SourceETag,
            row.SourceLastModified, row.Ciphertext, row.Nonce, row.Tag,
            row.KeyVersion);
    }, cancellationToken);

    public async Task CompleteDownloadGrantAsync(
        string grantId,
        bool succeeded,
        string? failureCode,
        CancellationToken cancellationToken)
    {
        if (!succeeded && (string.IsNullOrWhiteSpace(failureCode) ||
                           failureCode.Length > 64 ||
                           failureCode.Any(character => !(char.IsAsciiLetterOrDigit(character) ||
                                                           character is '_'))))
            throw new ArgumentException("Failure code is invalid.", nameof(failureCode));
        await using var command = _dataSource.CreateCommand("""
            UPDATE suite.suite_content_grants
            SET state=$2,completed_at=clock_timestamp(),failure_code=$3
            WHERE grant_id=$1 AND state='CLAIMED'
            """);
        command.Parameters.AddWithValue(grantId);
        command.Parameters.AddWithValue(succeeded ? "COMPLETED" : "FAILED");
        command.Parameters.AddWithValue(succeeded ? DBNull.Value : failureCode!);
        _ = await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> IsDownloadGrantAuthorizationCurrentAsync(
        string grantId,
        long now,
        CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand("""
            UPDATE suite.suite_content_grants g
            SET last_authorized_at=clock_timestamp()
            WHERE g.grant_id=$1 AND g.state='CLAIMED' AND EXISTS(
              SELECT 1
              FROM suite.suite_licenses l
              JOIN suite.suite_devices d
                ON d.license_id=l.license_id AND d.device_id=g.device_id
              JOIN suite.suite_sessions s
                ON s.license_id=g.license_id AND s.device_id=g.device_id
               AND s.session_id=g.session_id
              JOIN suite.suite_content_entitlements e
                ON e.license_id=g.license_id AND e.scope='FULL_CATALOG'
              WHERE l.license_id=g.license_id AND l.product_id=$2 AND l.status='ACTIVE'
                AND l.license_term='LIFETIME' AND l.expires_at IS NULL
                AND l.maximum_active_devices=1 AND d.status='ACTIVE'
                AND s.status='ACTIVE' AND s.revoked_at IS NULL
                AND s.authorized_until>to_timestamp($3)
                AND s.revocation_generation=l.revocation_generation
                AND g.revocation_generation=l.revocation_generation
                AND e.status='ACTIVE'
                AND (l.provisioning_origin<>'COMMERCE' OR EXISTS(
                  SELECT 1 FROM suite.suite_license_deliveries ld
                  WHERE ld.license_id=l.license_id
                    AND ld.provisioning_state='PROVISIONED'
                    AND (ld.financial_state='PAID' OR
                      (ld.financial_state='SUSPENDED'
                       AND ld.administrative_resume_source_version IS NOT NULL
                       AND ld.administrative_resume_source_version=ld.last_source_version
                       AND ld.administrative_resume_actor IS NOT NULL
                       AND ld.administrative_resume_reason IS NOT NULL
                       AND ld.administrative_resume_request_id IS NOT NULL))))
            )
            RETURNING true
            """);
        command.Parameters.AddWithValue(grantId);
        command.Parameters.AddWithValue(Protocol.ProductId);
        command.Parameters.AddWithValue(now);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    private async Task<T> ExecuteSerializableAsync<T>(
        Func<NpgsqlConnection, NpgsqlTransaction, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
                await using var transaction = await connection.BeginTransactionAsync(
                    IsolationLevel.Serializable, cancellationToken);
                return await operation(connection, transaction, cancellationToken);
            }
            catch (PostgresException exception) when (
                exception.SqlState is PostgresErrorCodes.SerializationFailure or
                    PostgresErrorCodes.DeadlockDetected && attempt < 3)
            {
                await Task.Delay(RandomNumberGenerator.GetInt32(15, 75) * attempt,
                    cancellationToken);
            }
            catch (PostgresException exception) when (
                exception.SqlState is PostgresErrorCodes.SerializationFailure or
                    PostgresErrorCodes.DeadlockDetected)
            {
                throw new SuiteException(409, "TRANSACTION_CONFLICT",
                    "The operation could not be completed safely.", exception);
            }
        }
        throw new SuiteException(409, "TRANSACTION_CONFLICT",
            "The operation could not be completed safely.");
    }

    private static async Task<ContentAuthorizationRecord> LockAuthorizationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string licenseId,
        string deviceId,
        string sessionId,
        long now,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT l.revocation_generation,
                   extract(epoch from s.authorized_until)::bigint
            FROM suite.suite_licenses l
            JOIN suite.suite_devices d
              ON d.license_id=l.license_id AND d.device_id=$2
            JOIN suite.suite_sessions s
              ON s.license_id=l.license_id AND s.device_id=d.device_id AND s.session_id=$3
            JOIN suite.suite_content_entitlements e
              ON e.license_id=l.license_id AND e.scope='FULL_CATALOG'
            WHERE l.license_id=$1 AND l.product_id=$4 AND l.status='ACTIVE'
              AND l.license_term='LIFETIME' AND l.expires_at IS NULL
              AND l.maximum_active_devices=1 AND d.status='ACTIVE'
              AND s.status='ACTIVE' AND s.authorized_until>to_timestamp($5)
              AND s.revocation_generation=l.revocation_generation
              AND s.revoked_at IS NULL AND e.status='ACTIVE'
              AND (l.provisioning_origin<>'COMMERCE' OR EXISTS(
                SELECT 1 FROM suite.suite_license_deliveries ld
                WHERE ld.license_id=l.license_id
                  AND ld.provisioning_state='PROVISIONED'
                  AND (ld.financial_state='PAID' OR
                    (ld.financial_state='SUSPENDED'
                     AND ld.administrative_resume_source_version IS NOT NULL
                     AND ld.administrative_resume_source_version=ld.last_source_version
                     AND ld.administrative_resume_actor IS NOT NULL
                     AND ld.administrative_resume_reason IS NOT NULL
                     AND ld.administrative_resume_request_id IS NOT NULL))))
            """, connection, transaction);
        command.Parameters.AddWithValue(licenseId);
        command.Parameters.AddWithValue(deviceId);
        command.Parameters.AddWithValue(sessionId);
        command.Parameters.AddWithValue(Protocol.ProductId);
        command.Parameters.AddWithValue(now);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new SuiteException(403, "CONTENT_DENIED",
                "Content access is not authorized.");
        return new ContentAuthorizationRecord(reader.GetInt64(0), reader.GetInt64(1));
    }

    private static async Task ConsumeChallengeAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ChallengeRecord challenge,
        long now,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            UPDATE suite.suite_challenges c
            SET consumed_at=clock_timestamp()
            FROM suite.suite_licenses l
            WHERE c.challenge_id=$1 AND c.product_id=$2 AND c.license_id=$3
              AND c.device_id=$4 AND c.session_id=$5 AND c.action=$6
              AND c.context_hash=$7 AND c.consumed_at IS NULL
              AND c.invalidated_at IS NULL AND c.expires_at>to_timestamp($8)
              AND l.license_id=c.license_id
              AND l.revocation_generation=c.revocation_generation
            """, connection, transaction);
        command.Parameters.AddWithValue(challenge.ChallengeId);
        command.Parameters.AddWithValue(challenge.ProductId);
        command.Parameters.AddWithValue(challenge.LicenseId);
        command.Parameters.AddWithValue(challenge.DeviceId);
        command.Parameters.AddWithValue(challenge.SessionId);
        command.Parameters.AddWithValue(challenge.Action);
        command.Parameters.AddWithValue(challenge.ContextHash);
        command.Parameters.AddWithValue(now);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new SuiteException(409, "CHALLENGE_INVALID",
                "Challenge is invalid or expired.");
    }

    private static void ValidateStoredItem(ContentCatalogItemRecord item)
    {
        if (item.Availability == ContentProtocol.MaintenanceAvailability &&
            item.Descriptor is null && item.DescriptorHash is null &&
            item.ReasonCode == ContentProtocol.MaintenanceReasonCode)
            return;
        if (item.Availability != ContentProtocol.ReadyAvailability ||
            item.Descriptor is null || item.DescriptorHash is null ||
            item.ReasonCode is not null)
            throw new SuiteException(503, "CONTENT_METADATA_INVALID",
                "The content catalog metadata is invalid.");
        ContentProtocol.Validate(item.Descriptor);
        var expected = ContentProtocol.DescriptorHash(item.ItemId, item.Descriptor);
        if (!Protocol.FixedEquals(expected, item.DescriptorHash))
            throw new SuiteException(503, "CONTENT_METADATA_INVALID",
                "The content catalog metadata is invalid.");
    }

    private static void NotAvailable() => throw new SuiteException(404,
        "CONTENT_NOT_AVAILABLE", "The requested content is not available.");

    private sealed record ContentAuthorizationRecord(
        long RevocationGeneration,
        long AuthorizedUntil);

    private sealed record ContentGrantRow(
        string TokenDigest,
        string LicenseId,
        string DeviceId,
        string SessionId,
        long RevocationGeneration,
        string CatalogIdentity,
        string ItemId,
        string ArtifactId,
        int ArtifactVersion,
        string ManifestIdentity,
        string DescriptorHash,
        long RangeStart,
        string State,
        long ExpiresAt,
        long AuthorizedUntil,
        string SafeFileName,
        string FileExtension,
        string ExtractPolicy,
        string ContentType,
        string? SourceETag,
        string? SourceLastModified,
        byte[] Ciphertext,
        byte[] Nonce,
        byte[] Tag,
        int KeyVersion);
}
