using System.Security.Cryptography;
using System.Text.Json;
using TurboRamaSuiteOnlineServer;
if (args.Length == 1 || (args.Length == 2 && args[1] == "--trusted-import")) {
    var library=StationLibrary.TryLoad(args[0],verifyContent:args.Length==1) ?? throw new Exception("Missing index");
    Console.WriteLine($"VALIDATED revision={library.Revision} visible={library.ItemCount} compatibility={library.CompatibilityItemCount}");
    return;
}
var directory = Path.Combine(Path.GetTempPath(), "station-auto-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
try
{
    var game=Path.Combine(directory,"Synthetic.z64"); File.WriteAllBytes(game,[0x80,0x37,0x12,0x40,1,2,3,4]);
    var cover=Path.Combine(directory,"cover.png");File.WriteAllBytes(cover,[137,80,78,71,13,10,26,10]);
    var index=Path.Combine(directory,"index.json");
    void Write(long revision, long itemRevision, string description) => File.WriteAllText(index,JsonSerializer.Serialize(new {
        revision,items=new[]{new{itemId="station_synthetic",name="Synthetic",platform="n64",revision=itemRevision,
        coverId="cover_synthetic",folderPath=new[]{"Selecionados","Traduções"},filePath=game,coverPath=cover,metadata=new{description},artifact=new{
        fileName="Synthetic.z64",sizeBytes=8,sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(game))).ToLowerInvariant(),
        format="raw",launchPath="Synthetic.z64",expandedSizeBytes=8,fileCount=1}}}},StrictJson.Options));
    int checks=0;
    void Check(bool condition) {checks++;if(!condition)throw new Exception("Station automatic catalog check failed");}
    Write(4,4,"Before");var initial=StationLibrary.TryLoad(index)!;
    using var monitor=new StationLibraryMonitor(index,initial);Check(!monitor.Reload());
    Write(5,4,"Server synopsis");Check(monitor.Reload());Check(monitor.Current.Revision==5);Check(monitor.Current.Catalog[0].FolderPath.SequenceEqual(new[]{"Selecionados","Traduções"}));
    Check(monitor.Current.Catalog[0].Metadata?.Description=="Server synopsis");
    Check(monitor.Current.Catalog[0].Revision==4);
    Write(6,5,"Changed edition");Check(monitor.Reload());
    Check(monitor.TryResolveGrant("station_synthetic",game,4,out var artifact));Check(artifact.Entry.Revision==4);
    File.WriteAllText(index,"{invalid");try{monitor.Reload();throw new Exception("Bad index accepted");}catch(JsonException){}
    Check(monitor.Current.Revision==6);
    Write(5,5,"Rollback forbidden");try{monitor.Reload();throw new Exception("Older index accepted");}catch(InvalidOperationException){}
    Check(monitor.Current.Revision==6);
    File.WriteAllBytes(game,[1,2,3]);Check(!monitor.TryResolveGrant("station_synthetic",game,4,out _));
    File.WriteAllBytes(game,[0x80,0x37,0x12,0x40,1,2,3,4]);
    var folderCases=new object?[]{null,"wrong",new[]{".."},new[]{"."},new[]{"a/b"},new[]{"a\\b"},new[]{" "},new[]{"control\n"},new[]{new string('x',81)},Enumerable.Repeat("level",9).ToArray(),new object[]{42}};
    foreach(var invalid in folderCases){
        Write(8,5,"Folders");
        var tree=System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(index))!;
        tree["items"]![0]!["folderPath"]=JsonSerializer.SerializeToNode(invalid);
        File.WriteAllText(index,tree.ToJsonString());
        try{StationLibrary.TryLoad(index);throw new Exception("Invalid folder accepted");}catch(InvalidOperationException){Check(true);}
    }
    Write(9,6,"Verified by importer");
    using(var locked=new FileStream(game,FileMode.Open,FileAccess.Read,FileShare.None))
    {
        try{StationLibrary.TryLoad(index);throw new Exception("Strict load did not open the body");}
        catch(IOException){Check(true);}
        var trusted=StationLibrary.TryLoad(index,verifyContent:false)!;
        Check(trusted.ItemCount==1);
        using var imported=new StationLibraryMonitor(index,trusted,verifyContent:false);
        var tree=System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(index))!;
        tree["revision"]=10;tree["items"]![0]!["metadata"]!["description"]="Imported metadata updated";
        File.WriteAllText(index,tree.ToJsonString());
        Check(imported.Reload());Check(imported.Current.Revision==10);
        Check(imported.Current.Catalog[0].Metadata?.Description=="Imported metadata updated");
    }
    var importedFile=StationLibrary.TryLoad(index,verifyContent:false)!;
    File.WriteAllBytes(game,[1,2,3]);Check(!importedFile.TryResolveArtifact("station_synthetic",out _));
    try{StationLibrary.TryLoad(index,verifyContent:false);throw new Exception("Stale file size accepted");}
    catch(InvalidOperationException){Check(true);}
    File.WriteAllBytes(game,[0x80,0x37,0x12,0x40,1,2,3,4]);
    Write(11,7,"Large catalog");
    var large=System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(index))!;
    var template=large["items"]![0]!;
    var entries=new System.Text.Json.Nodes.JsonArray();
    for(var i=0;i<4690;i++) {
        var entry=template.DeepClone();
        entry["itemId"]=$"station_large_{i:D5}";
        entry["coverId"]=$"cover_large_{i:D5}";
        entries.Add(entry);
    }
    large["items"]=entries;
    File.WriteAllText(index,large.ToJsonString());
    var expanded=StationLibrary.TryLoad(index,verifyContent:false)!;
    Check(expanded.ItemCount==4690);
    Check(StationLibrary.CatalogMaximumItems(null)==4096);
    Check(expanded.Catalog.Take(StationLibrary.CatalogMaximumItems(null)).Count()==4096);
    Check(expanded.Catalog.Take(StationLibrary.CatalogMaximumItems("20000")).Count()==4690);
    Check(expanded.Catalog[4096].ItemId=="station_large_04096");
    foreach(var invalid in new[]{"", "0", "20001", "-1", " 5000", "5000,5000", "abc", "2147483648"}) {
        try { StationLibrary.CatalogMaximumItems(invalid);throw new Exception("Invalid catalog limit accepted"); }
        catch(SuiteException e) when(e.StatusCode==400 && e.Code=="STATION_INVALID_CATALOG_LIMIT") {Check(true);}
    }
    for(var i=entries.Count;i<=StationLibrary.MaximumItems;i++) {
        var entry=template.DeepClone();entry["itemId"]=$"station_large_{i:D5}";entry["coverId"]=$"cover_large_{i:D5}";entries.Add(entry);
    }
    File.WriteAllText(index,large.ToJsonString());
    try {StationLibrary.TryLoad(index,verifyContent:false);throw new Exception("Oversized library accepted");}
    catch(InvalidOperationException) {Check(true);}
    Console.WriteLine($"PASS {checks} Station snapshot, metadata, active grant and last-good checks");
}
finally {Directory.Delete(directory,true);}
