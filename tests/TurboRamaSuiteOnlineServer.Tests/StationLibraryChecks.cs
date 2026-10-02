using System.Security.Cryptography;
using System.IO.Compression;
using System.Text.Json;
using TurboRamaSuiteOnlineServer;

internal static class StationLibraryChecks
{
    public static void Run()
    {
        var root = Path.Combine(Path.GetTempPath(), "station-lib-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var game = Path.Combine(root, "item.bin");
            var cover = Path.Combine(root, "cover.png");
            File.WriteAllBytes(game, [1, 2, 3, 4]);
            File.WriteAllBytes(cover, Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGNgYGBgAAAABQABpfZFQAAAAABJRU5ErkJggg=="));
            var index = Path.Combine(root, "index.json");
            File.WriteAllText(index, JsonSerializer.Serialize(new
            {
                revision = 3,
                items = new[]
                {
                    new
                    {
                        itemId = "item-alpha-01",
                        name = "Alpha",
                        platform = "PS2",
                        coverId = "cover-alpha-01",
                        filePath = game,
                        coverPath = cover,
                        artifact = new
                        {
                            fileName = "item.bin", sizeBytes = 4,
                            sha256 = Convert.ToHexString(SHA256.HashData([1, 2, 3, 4])).ToLowerInvariant(),
                            format = "raw", launchPath = "item.bin",
                            expandedSizeBytes = 4, fileCount = 1
                        }
                    }
                }
            }));
            var library = StationLibrary.TryLoad(index) ??
                throw new Exception("Station library failed to load.");
            if (library.Revision != 3 || library.ItemCount != 1 ||
                library.Catalog[0].ItemId != "item-alpha-01" ||
                library.Catalog[0].CoverId != "cover-alpha-01")
                throw new Exception("Station catalog fields mismatch.");
            if (library.Catalog[0].GetType().GetProperty("FilePath") is not null)
                throw new Exception("Station catalog leaked a file path property.");
            var json = JsonSerializer.Serialize(library.Catalog[0]);
            if (json.Contains(game, StringComparison.Ordinal) ||
                json.Contains("filePath", StringComparison.Ordinal))
                throw new Exception("Station catalog serialized a file path.");
            if (!library.TryResolve("item-alpha-01", out var resolved, out _) ||
                resolved != game)
                throw new Exception("Station item resolve failed.");
            if (!library.TryResolveArtifact("item-alpha-01", out var artifact) ||
                artifact.Descriptor.FileName != "item.bin" ||
                artifact.Descriptor.SizeBytes != 4 || artifact.Entry.Revision != 3)
                throw new Exception("Station artifact descriptor failed.");
            File.WriteAllBytes(game, [1, 2, 3, 4, 5]);
            if (library.TryResolveArtifact("item-alpha-01", out _))
                throw new Exception("Changed artifact was authorized.");
            File.WriteAllBytes(game, [1, 2, 3, 4]);
            File.SetLastWriteTimeUtc(game, DateTime.UtcNow.AddSeconds(5));
            if (library.TryResolveArtifact("item-alpha-01", out _))
                throw new Exception("Rewritten artifact kept the old index identity.");
            var blob = library.ReadCover("cover-alpha-01") ??
                throw new Exception("Station cover missing.");
            if (blob.ContentType != "image/png" || blob.Bytes.Length != 70 ||
                Convert.ToHexString(SHA256.HashData(blob.Bytes)).ToLowerInvariant() !=
                "c2153f77e11087fcb078ae38527fa83bef29791e3700e30cc87fec4405a66d0f")
                throw new Exception("Station cover mismatch.");
            if (StationLibrary.TryLoad(Path.Combine(root, "missing.json")) is not null)
                throw new Exception("Missing library index must stay unloaded.");

            var archivePath = Path.Combine(root, "multi.zip");
            using (var zip = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                using (var first = new BinaryWriter(zip.CreateEntry("disc/game.cue").Open()))
                    first.Write(new byte[] { 1, 2 });
                using (var second = new BinaryWriter(zip.CreateEntry("disc/game.bin").Open()))
                    second.Write(new byte[] { 3, 4, 5 });
            }
            var zipHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(archivePath)))
                .ToLowerInvariant();
            void WriteZipIndex(string hash, string launch) => File.WriteAllText(index,
                JsonSerializer.Serialize(new
                {
                    revision = 4,
                    items = new[] { new
                    {
                        itemId = "item-zip-01", name = "Multi", platform = "ps2",
                        coverId = "cover-zip-01", filePath = archivePath,
                        coverPath = cover,
                        artifact = new
                        {
                            fileName = "multi.zip", sizeBytes = new FileInfo(archivePath).Length,
                            sha256 = hash, format = "zip", launchPath = launch,
                            expandedSizeBytes = 5, fileCount = 2
                        }
                    } }
                }));
            WriteZipIndex(zipHash, "disc/game.cue");
            if (!StationLibrary.TryLoad(index)!.TryResolveArtifact("item-zip-01", out _))
                throw new Exception("Valid archive descriptor failed.");
            WriteZipIndex(new string('0', 64), "disc/game.cue");
            try { _ = StationLibrary.TryLoad(index); throw new Exception("Wrong SHA was accepted."); }
            catch (InvalidOperationException error) when (error.Message == "Station artifact index is stale.") { }
            WriteZipIndex(zipHash, "disc/missing.cue");
            try { _ = StationLibrary.TryLoad(index); throw new Exception("Missing launch path was accepted."); }
            catch (InvalidOperationException error) when (error.Message == "Station ZIP metadata is invalid.") { }
            using (var zip = ZipFile.Open(archivePath, ZipArchiveMode.Update))
            {
                using (var duplicate = zip.CreateEntry("disc/game.cue").Open()) duplicate.WriteByte(2);
            }
            var duplicateHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(archivePath)))
                .ToLowerInvariant();
            WriteZipIndex(duplicateHash, "disc/game.cue");
            try { _ = StationLibrary.TryLoad(index); throw new Exception("Duplicate ZIP member was accepted."); }
            catch (InvalidOperationException error) when (error.Message == "Station ZIP metadata is invalid.") { }
            File.WriteAllText(index, JsonSerializer.Serialize(new
            {
                revision = 5,
                items = new[]
                {
                    new { itemId = "shared-01", name = "A", platform = "snes",
                        revision = 1, coverId = "shared-cover", filePath = game,
                        coverPath = cover },
                    new { itemId = "shared-02", name = "B", platform = "snes",
                        revision = 2, coverId = "shared-cover", filePath = game,
                        coverPath = cover }
                }
            }));
            try { _ = StationLibrary.TryLoad(index); throw new Exception("Ambiguous cover revision accepted."); }
            catch (InvalidOperationException error) when
                (error.Message == "Station cover ID has conflicting revisions.") { }

            var keyPath = Path.Combine(root, "download.key");
            var key = RandomNumberGenerator.GetBytes(32);
            File.WriteAllBytes(keyPath, key);
            if (OperatingSystem.IsLinux())
                File.SetUnixFileMode(keyPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            var other = RandomNumberGenerator.GetBytes(32);
            using (var cipher = StationGrantCipher.Load(keyPath, other))
            {
                var sealedPath = cipher.Seal("lic\ndev\nitem\ngrant", game);
                var opened = cipher.Open("lic\ndev\nitem\ngrant", sealedPath.Nonce,
                    sealedPath.Ciphertext, sealedPath.Tag);
                if (opened != game)
                    throw new Exception("Station grant cipher round-trip failed.");
            }
            try
            {
                using var _ = StationGrantCipher.Load(keyPath, key);
                throw new Exception("Station download key accepted a reused secret.");
            }
            catch (InvalidOperationException) { }
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }
}
