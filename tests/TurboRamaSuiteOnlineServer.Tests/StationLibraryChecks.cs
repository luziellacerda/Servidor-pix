using System.Security.Cryptography;
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
            File.WriteAllBytes(cover, [0x89, 0x50, 0x4E, 0x47]);
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
                        coverPath = cover
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
            var blob = library.ReadCover("cover-alpha-01") ??
                throw new Exception("Station cover missing.");
            if (blob.ContentType != "image/png" || blob.Bytes.Length != 4)
                throw new Exception("Station cover mismatch.");
            if (StationLibrary.TryLoad(Path.Combine(root, "missing.json")) is not null)
                throw new Exception("Missing library index must stay unloaded.");

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
