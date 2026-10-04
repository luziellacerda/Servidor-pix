using System.Text.Json.Nodes;
using TurboRamaSuiteOnlineServer;
var work=Path.Combine(AppContext.BaseDirectory,"fixtures-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(work);var count=0;
void Check(bool ok){count++;if(!ok)throw new Exception("Check "+count);}
JsonObject Row()=>new(){["itemId"]="station_fixture001",["coverId"]="cover_fixture001",["name"]="Jogo fixture",["platform"]="snes",["revision"]=1,["filePath"]=Path.Combine(work,"game.rom"),["coverPath"]=Path.Combine(work,"cover.png")};
StationLibrary Load(JsonObject row){var path=Path.Combine(work,Guid.NewGuid().ToString("N")+".json");File.WriteAllText(path,new JsonObject{["revision"]=2,["items"]=new JsonArray(row)}.ToJsonString());return StationLibrary.TryLoad(path)!;}
void Reject(JsonNode? path){var row=Row();row["folderPath"]=path;try{Load(row);throw new Exception("Invalid folder accepted");}catch(InvalidOperationException){count++;}}
var flat=Load(Row());Check(flat.Catalog[0].FolderPath.Count==0);
var row=Row();row["folderPath"]=new JsonArray("Selecionados","Traduções");var item=Load(row).Catalog[0];Check(item.FolderPath.SequenceEqual(new[]{"Selecionados","Traduções"}));Check(item.ItemId==flat.Catalog[0].ItemId);Check(item.CoverId==flat.Catalog[0].CoverId);Check(item.Revision==1);
foreach(var bad in new[]{""," ",".","..","a/b","a\\b","bad\nname",new string('x',81)})Reject(new JsonArray(bad));
Reject(null);Reject(JsonValue.Create("RPG"));Reject(new JsonArray(1));Reject(new JsonArray((JsonNode?)null));
Reject(new JsonArray("1","2","3","4","5","6","7","8","9"));
row=Row();row["folderPath"]=new JsonArray("日本語 🎮");Check(Load(row).Catalog[0].FolderPath[0]=="日本語 🎮");
row=Row();row["folderPath"]=new JsonArray();Check(Load(row).Catalog[0].FolderPath.Count==0);
Console.WriteLine($"PASS {count} server folder checks; default, nested, Unicode, invalid input, preserved IDs/covers/revision");
