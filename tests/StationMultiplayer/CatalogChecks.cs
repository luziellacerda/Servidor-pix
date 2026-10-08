using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using TurboRamaSuiteOnlineServer;

internal static class CatalogChecks
{
    internal static void Run(Action<bool,string> check)
    {
        var directory=Path.Combine(Path.GetTempPath(),"station-content-contract-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try {
            var index=Path.Combine(directory,"index.json");
            var game=Path.Combine(directory,"game.bin");
            var cover=Path.Combine(directory,"cover.png");
            File.WriteAllBytes(game,[1,2,3,4]);File.WriteAllBytes(cover,[9,8,7]);
            var document=JsonSerializer.SerializeToNode(new {revision=1,items=new[]{new {
                itemId="synthetic-content",name="Synthetic",platform="snes",revision=1,
                coverId="synthetic-cover",filePath=game,coverPath=cover,
                metadata=new {description="Synopsis",developer="",publisher="",genre="",players="",releaseDate=""},
                artifact=new {fileName="game.bin",sizeBytes=4,sha256=new string('a',64),format="raw",launchPath="game.bin",expandedSizeBytes=4,fileCount=1}
            }}})!;
            void Save()=>File.WriteAllText(index,document.ToJsonString());
            string Wire(object value)=>JsonSerializer.Serialize(value,StrictJson.Options);
            Save();var initial=StationLibrary.TryLoad(index,verifyContent:false)!;
            var entry=initial.Catalog.Single();
            check(entry.ContentSha256 is null,"absent identity stays unknown");
            check(Wire(entry.PublicValue(false))==Wire(new {itemId=entry.ItemId,name=entry.Name,platform=entry.Platform,revision=entry.Revision,coverId=entry.CoverId,folderPath=entry.FolderPath}),"legacy catalog bytes without metadata preserved");
            check(Wire(entry.PublicValue(true))==Wire(new {itemId=entry.ItemId,name=entry.Name,platform=entry.Platform,revision=entry.Revision,coverId=entry.CoverId,metadata=entry.Metadata,folderPath=entry.FolderPath}),"legacy catalog bytes with metadata preserved");
            var payloadHash=new string('b',64);
            document["revision"]=2;document["items"]![0]!["contentSha256"]=payloadHash;Save();
            var monitor=new StationLibraryMonitor(index,StationLibrary.TryLoad(index,verifyContent:false)!,false);
            var current=monitor.Current.Catalog.Single();
            check(current.ContentSha256==payloadHash,"publisher payload identity loaded");
            check(monitor.Current.TryResolveArtifact(entry.ItemId,out var artifact)&&artifact.Descriptor.Sha256!=payloadHash,"payload and container identities remain separate");
            foreach(var withMetadata in new[]{false,true}) {
                var serialized=Wire(current.PublicValue(withMetadata));
                using var parsed=JsonDocument.Parse(serialized);
                check(parsed.RootElement.GetProperty("contentSha256").GetString()==payloadHash,"known identity serialized independently of metadata flag");
                check(parsed.RootElement.TryGetProperty("metadata",out _)==withMetadata,"metadata opt-in preserved");
                check(!serialized.Contains(directory,StringComparison.Ordinal)&&!serialized.Contains("filePath",StringComparison.Ordinal)&&!serialized.Contains("coverPath",StringComparison.Ordinal),"private paths remain private");
            }
            using var key=RSA.Create(2048);using var signer=new StationResponseSigner(key.ExportRSAPrivateKeyPem());
            using var envelope=JsonDocument.Parse(Wire(signer.Sign(new {items=new[]{current.PublicValue(true)}})));
            var signedBytes=StationProtocol.Decode(envelope.RootElement.GetProperty("payload").GetString()!,8192);
            var signature=StationProtocol.Decode(envelope.RootElement.GetProperty("signature").GetString()!,512);
            check(key.VerifyData(signedBytes,signature,HashAlgorithmName.SHA256,RSASignaturePadding.Pss),"catalog identity covered by existing response signature");
            using var signedJson=JsonDocument.Parse(signedBytes);
            check(signedJson.RootElement.GetProperty("items")[0].GetProperty("contentSha256").GetString()==payloadHash,"signed identity matches publisher value");
            signedBytes[10]^=1;
            check(!key.VerifyData(signedBytes,signature,HashAlgorithmName.SHA256,RSASignaturePadding.Pss),"tampered catalog rejected");
            foreach(JsonNode? bad in new JsonNode?[]{null,JsonValue.Create("invalid"),JsonValue.Create(new string('B',64)),JsonValue.Create(123)}) {
                document["revision"]=3;document["items"]![0]!["contentSha256"]=bad?.DeepClone();Save();
                bool rejected=false;
                try {monitor.Reload();}catch(InvalidOperationException){rejected=true;}
                check(rejected&&monitor.Current.Revision==2&&monitor.Current.Catalog.Single().ContentSha256==payloadHash,"invalid publisher hash retains last valid catalog");
            }
            document["revision"]=3;document["items"]![0]!.AsObject().Remove("contentSha256");Save();
            check(monitor.Reload()&&monitor.Current.Revision==3&&monitor.Current.Catalog.Single().ContentSha256 is null,"later catalog can withdraw an identity without inventing one");
            File.Delete(game);
            check(JsonSerializer.SerializeToElement(current.PublicValue(true),StrictJson.Options).GetProperty("contentSha256").GetString()==payloadHash,"catalog serialization performs no ROM read");
        } finally {Directory.Delete(directory,true);}
    }
}
