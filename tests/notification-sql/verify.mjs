import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { PGlite } from '@electric-sql/pglite';

// PostgreSQL/WASM in memory; never connects to a database or uses credentials.
// Fixtures model the columns used by the NEW migration, including direct-mode
// NULL digests from migration 015. This is not a production PG16 migration test.
const db = new PGlite();
const root = new URL('../../', import.meta.url);
try {
  await db.exec(`
    CREATE SCHEMA suite;
    CREATE ROLE "turborama-suite"; CREATE ROLE "turborama-suite-admin";
    GRANT USAGE ON SCHEMA suite TO "turborama-suite","turborama-suite-admin";
    CREATE TABLE suite.schema_migrations(version text PRIMARY KEY);
    CREATE TABLE suite.suite_licenses(license_id varchar(64) PRIMARY KEY,product_id text,status text,
      activation_consumed boolean,enrollment_state text,license_term text,expires_at timestamptz,
      maximum_active_devices int,revocation_generation bigint);
    CREATE TABLE suite.suite_devices(license_id varchar(64),device_id char(64),public_key_spki text,status text,
      PRIMARY KEY(license_id,device_id));
    CREATE TABLE suite.suite_license_enrollments(license_id varchar(64),device_id char(64),public_key_spki text);
    CREATE TABLE suite.suite_sessions(license_id varchar(64),device_id char(64),session_id char(64),
      status text,revocation_generation bigint,authorized_until timestamptz);
    CREATE TABLE suite.suite_content_entitlements(license_id varchar(64),scope text,status text,
      source_system text,source_purchase_id varchar(64),source_item_key text,product_id text);
    CREATE TABLE suite.suite_license_deliveries(license_id varchar(64),source_system text,source_purchase_id varchar(64),
      source_item_key text,product_id text,provisioning_state text,financial_state text);
    CREATE TABLE suite.suite_content_grants(license_id varchar(64),device_id char(64),state text,
      revocation_generation bigint,created_at timestamptz,catalog_identity char(64),item_id char(32),
      artifact_id char(32),artifact_version int,manifest_identity char(64),sha256 char(64));
    CREATE TABLE suite.suite_content_items(catalog_identity char(64),item_id char(32),display_name varchar(512));
    GRANT SELECT ON suite.suite_licenses,suite.suite_devices,suite.suite_license_deliveries TO "turborama-suite-admin";
    INSERT INTO suite.suite_licenses VALUES('TS-SQL-TEST','TURBORAMA_SUITE','ACTIVE',true,'BOUND','LIFETIME',NULL,1,0);
    INSERT INTO suite.suite_devices VALUES('TS-SQL-TEST',repeat('d',64),'fixture-public-key','ACTIVE');
    INSERT INTO suite.suite_license_enrollments SELECT license_id,device_id,public_key_spki FROM suite.suite_devices;
    INSERT INTO suite.suite_sessions VALUES('TS-SQL-TEST',repeat('d',64),repeat('e',64),'ACTIVE',0,clock_timestamp()+interval '1 hour');
    INSERT INTO suite.suite_content_entitlements VALUES('TS-SQL-TEST','FULL_CATALOG','ACTIVE','TURBOBOX_V1','fixture-purchase','fixture-item','TURBORAMA_SUITE');
    INSERT INTO suite.suite_license_deliveries VALUES('TS-SQL-TEST','TURBOBOX_V1','fixture-purchase','fixture-item','TURBORAMA_SUITE','PROVISIONED','PAID');
    INSERT INTO suite.suite_content_grants VALUES('TS-SQL-TEST',repeat('d',64),'COMPLETED',0,clock_timestamp(),repeat('b',64),repeat('a',32),repeat('a',32),1,repeat('b',64),NULL);
    INSERT INTO suite.suite_content_items VALUES(repeat('b',64),repeat('a',32),'Conteúdo autorizado de teste');
  `);
  const migration = await readFile(new URL('migrations/suite/026_suite_extraction_notifications.up.sql',root),'utf8');
  await db.exec(migration);
  const api = await readFile(new URL('src/TurboRamaSuiteOnlineServer/ExtractionNotificationEndpoints.cs',root),'utf8');
  const filter = api.match(/private const string ContextFilter = """([\s\S]*?)""";/)[1];
  const insertParts = api.match(/var insert = new NpgsqlCommand\("""([\s\S]*?)""" \+ ContextFilter \+ "([^"]+)"/);
  const insert = insertParts[1] + filter + insertParts[2];
  const inputs = ['TS-SQL-TEST','d'.repeat(64),'e'.repeat(64),'a'.repeat(32),'a'.repeat(32),1,'b'.repeat(64),'c'.repeat(64)];
  const read = 'SELECT * FROM suite.suite_extraction_notice_context WHERE ' + filter;
  assert.equal((await db.query(read,inputs)).rows.length,1,'Direct mode NULL digest must accept authenticated artifact context');
  for (const [change,restore] of [
    ["UPDATE suite.suite_devices SET status='SUSPENDED'","UPDATE suite.suite_devices SET status='ACTIVE'"],
    ["UPDATE suite.suite_licenses SET status='REVOKED'","UPDATE suite.suite_licenses SET status='ACTIVE'"],
    ["UPDATE suite.suite_licenses SET revocation_generation=1","UPDATE suite.suite_licenses SET revocation_generation=0"],
    ["UPDATE suite.suite_content_entitlements SET status='SUSPENDED'","UPDATE suite.suite_content_entitlements SET status='ACTIVE'"],
    ["UPDATE suite.suite_license_deliveries SET financial_state='SUSPENDED'","UPDATE suite.suite_license_deliveries SET financial_state='PAID'"],
    ["UPDATE suite.suite_sessions SET authorized_until=clock_timestamp()-interval '1 hour'","UPDATE suite.suite_sessions SET authorized_until=clock_timestamp()+interval '1 hour'"],
    ["UPDATE suite.suite_content_grants SET state='ISSUED'","UPDATE suite.suite_content_grants SET state='COMPLETED'"],
    ["UPDATE suite.suite_content_grants SET sha256=repeat('f',64)","UPDATE suite.suite_content_grants SET sha256=NULL"]
  ]) {
    await db.exec(change);
    assert.equal((await db.query(read,inputs)).rows.length,0,change);
    await db.exec(restore);
  }
  assert.equal((await db.query(read,['TS-OTHER-ACCOUNT',...inputs.slice(1)])).rows.length,0,'Account isolation');
  const event='f'.repeat(64);
  const params=[...inputs,event,'emulators',Math.floor(Date.now()/1000)-10,3,'fixture-public-key'];
  await db.exec('SET ROLE "turborama-suite"');
  assert.equal((await db.query(insert,params)).affectedRows,1,'API role insert');
  assert.equal((await db.query(insert,params)).affectedRows,0,'Idempotent duplicate');
  await assert.rejects(()=>db.query('DELETE FROM suite.suite_extraction_notification_outbox'),/permission denied/);
  await db.exec('RESET ROLE');
  const admin = await readFile(new URL('src/TurboRamaSuiteAdminServer/ExtractionNotificationAdminEndpoints.cs',root),'utf8');
  const sql = [...admin.matchAll(/"""([\s\S]*?)"""/g)].map(match=>match[1]);
  assert.equal(sql.length,5,'Review test extraction when SQL blocks change');
  const [stale,lease,render,dispatch,complete]=sql;
  const firstToken='11111111-1111-1111-1111-111111111111';
  const secondToken='22222222-2222-2222-2222-222222222222';
  await db.exec('SET ROLE "turborama-suite-admin"');
  assert.equal((await db.query(lease,[firstToken])).rows[0].source_purchase_id,'fixture-purchase');
  assert.equal((await db.query(lease,[secondToken])).rows.length,0,'Busy lease');
  await db.exec("UPDATE suite.suite_extraction_notification_outbox SET lease_until=clock_timestamp()-interval '1 second'");
  assert.equal((await db.query(lease,[secondToken])).rows.length,1,'Expired PRE-dispatch lease retries');
  assert.equal((await db.query(complete,[event,firstToken,'SKIPPED','TEST',['LEASED','SKIPPED']])).affectedRows,0,'Old lease must not acknowledge new owner');
  assert.equal((await db.query(render,[event,secondToken])).rows[0].template_variant,3,'Variant persisted');
  assert.equal((await db.query(dispatch,[event,secondToken])).affectedRows,1,'Dispatch boundary');
  assert.equal((await db.query(complete,[event,secondToken,'PENDING','TEST',['LEASED','PENDING']])).affectedRows,0,'Dispatched job cannot become retry');
  await db.exec("UPDATE suite.suite_extraction_notification_outbox SET lease_until=clock_timestamp()-interval '1 second'");
  await db.exec(stale);
  assert.equal((await db.query(lease,[firstToken])).rows.length,0,'Ambiguous dispatch must never be recycled');
  assert.equal((await db.query('SELECT status FROM suite.suite_extraction_notification_outbox')).rows[0].status,'UNCERTAIN');
  assert.equal((await db.query(complete,[event,secondToken,'QUEUED','',['DISPATCHING','UNCERTAIN','QUEUED']])).affectedRows,1,'Late confirmed acknowledgement');
  await db.exec('RESET ROLE');
  assert.equal((await db.query('SELECT count(*)::int AS count FROM suite.suite_extraction_notification_outbox')).rows[0].count,1);
  console.log('PASS: actual migration/view/INSERT and worker SQL; direct-mode NULL hash, denied states, roles, deduplication and lease fencing.');
  console.log((await db.query('SELECT version()')).rows[0].version);
} catch (error) {
  console.error('FAIL:', error.message, error.code ?? '', error.detail ?? '');
  process.exitCode = 1;
} finally { await db.close(); }
