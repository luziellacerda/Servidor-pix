using System.Security.Cryptography;
using System.Text.Json;
using TurboRamaSuiteOnlineServer;

var root = Path.GetFullPath(args[0]); Directory.CreateDirectory(root);
var clock = new TestTime(); var registry = new StationCarouselRegistry(root, clock); int checks = 0;
void Check(bool value, string label) { checks++; if (!value) throw new Exception(label); }
byte[] Clip(byte seed) { var b = Enumerable.Repeat(seed, 64).ToArray(); "ftyp"u8.CopyTo(b.AsSpan(4)); return b; }
string Hash(byte[] b) => Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant();
object Entry(byte[] bytes, string asset = "turbo-system-videos/720-snes.mp4") => new
    { asset, sha256 = Hash(bytes), sizeBytes = bytes.Length, contentType = "video/mp4", width = 720, height = 720, fps = 30, audioTracks = 0 };
async Task Index(long revision, params object[] items)
{
    await File.WriteAllTextAsync(Path.Combine(root,"index.json"), JsonSerializer.Serialize(new { schemaVersion = 1, revision, items })); clock.Advance();
}
var first=Clip(1);var second=Clip(2);
await File.WriteAllBytesAsync(Path.Combine(root, Hash(first)+".mp4"), first);
Check(await registry.ReadAsync(default) is null,"missing index not ready");
await Index(1,Entry(first));var a=await registry.ReadAsync(default);Check(a?.Revision==1&&a.Items.Count==1,"initial registry validates");
foreach(var asset in new[]{"../../x", "https://host/x", "turbo-system-videos/720-../x.mp4", "turbo-system-videos/720-UPPER.mp4"})
{await Index(2,Entry(first,asset));Check((await registry.ReadAsync(default))?.Revision==1,"reject unsafe asset");}
await Index(2,Entry(first),Entry(first));Check((await registry.ReadAsync(default))?.Revision==1,"reject duplicates");
await Index(2,Entry(second));Check((await registry.ReadAsync(default))?.Revision==1,"missing new file retains old registry");
await File.WriteAllBytesAsync(Path.Combine(root,Hash(second)+".mp4"),first);
clock.Advance();Check((await registry.ReadAsync(default))?.Revision==1,"hash mismatch retains old registry");
await File.WriteAllBytesAsync(Path.Combine(root,Hash(second)+".mp4"),second);
clock.Advance();Check((await registry.ReadAsync(default))?.Revision==2,"new valid revision accepted without process restart");
await Index(1,Entry(first));Check((await registry.ReadAsync(default))?.Revision==2,"rollback refused");
await File.WriteAllTextAsync(Path.Combine(root,"index.json"),"{}");clock.Advance();Check((await registry.ReadAsync(default))?.Revision==2,"missing fields retain old registry");
var malformed=Clip(3);malformed[4]=0;await File.WriteAllBytesAsync(Path.Combine(root,Hash(malformed)+".mp4"),malformed);
await Index(3,Entry(malformed));Check((await registry.ReadAsync(default))?.Revision==2,"invalid mp4 signature refused");
foreach(var h in new[]{"../x",new string('a',63),new string('A',64),"http://host/file"})Check(!StationCarouselRegistry.SafeHash(h),"unsafe file identifier refused");
await Index(3,Entry(first));Check((await registry.ReadAsync(default))?.Revision==3,"valid recovery after invalid publication");
var serialized=JsonSerializer.Serialize((await registry.ReadAsync(default))!.Items,StrictJson.Options);Check(serialized.Contains("\"asset\"")&&serialized.Contains("\"sizeBytes\""),"wire casing matches Android");
if(args.Length>1){var actual=await new StationCarouselRegistry(args[1]).ReadAsync(default);Check(actual?.Items.Count==58,"all 58 real carousel files validate");}
Console.WriteLine($"PASS {checks} server media checks");
sealed class TestTime : TimeProvider {
    private DateTimeOffset now=DateTimeOffset.Parse("2026-10-09T00:00:00Z");
    public override DateTimeOffset GetUtcNow()=>now;
    public void Advance()=>now=now.AddSeconds(61);
}
