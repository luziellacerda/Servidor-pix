package org.emulationstation.frontend.station;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
import org.json.*;
public final class StationFoldersTest {
    private static int count;
    static void check(boolean v){count++;if(!v)throw new AssertionError(count);}
    static JSONObject row()throws Exception{return new JSONObject().put("itemId","station_game001").put("name","Jogo de teste").put("platform","snes").put("revision",1).put("coverId","cover_test001");}
    static StationCatalog parse(JSONObject r)throws Exception{return StationCatalog.fromVerifiedPayload(new JSONObject().put("revision",2).put("items",new JSONArray().put(r)));}
    static void reject(Object path)throws Exception{try{parse(row().put("folderPath",path));throw new AssertionError("accepted "+path);}catch(IOException expected){count++;}}
    public static void main(String[] args)throws Exception{
        StationCatalog flat=parse(row());check(flat.items.get(0).folderPath.isEmpty());check(!new JSONObject(new String(flat.localPayload(),StandardCharsets.UTF_8)).getJSONArray("items").getJSONObject(0).has("folderPath"));
        StationCatalog folders=parse(row().put("folderPath",new JSONArray().put("Selecionados").put("Traduções")));StationCatalog.Item item=folders.items.get(0);
        check(item.itemId.equals(flat.items.get(0).itemId));check(item.revision==1);check(item.coverId.equals(flat.items.get(0).coverId));check(StationCatalog.folderKey(item).equals("Selecionados/Traduções"));
        StationCatalog restored=StationCatalog.fromVerifiedPayload(new JSONObject(new String(folders.localPayload(),StandardCharsets.UTF_8)));check(restored.items.get(0).folderPath.equals(item.folderPath));
        try{item.folderPath.add("bad");throw new AssertionError();}catch(UnsupportedOperationException expected){count++;}
        check(parse(row().put("folderPath",new JSONArray())).items.get(0).folderPath.isEmpty());
        for(Object bad:new Object[]{JSONObject.NULL,"RPG",true,10,new JSONObject()})reject(bad);
        for(String bad:new String[]{""," ",".","..","RPG/PTBR","RPG\\PTBR","bad\nname","bad\u0000name","x".repeat(81),"\ud800"})reject(new JSONArray().put(bad));
        reject(new JSONArray().put(2));reject(new JSONArray().put(JSONObject.NULL));
        JSONArray depth=new JSONArray();for(int i=0;i<8;i++)depth.put("Pasta "+i);check(parse(row().put("folderPath",depth)).items.get(0).folderPath.size()==8);depth.put("Pasta 9");reject(depth);
        check(parse(row().put("folderPath",new JSONArray().put("日本語 🎮"))).items.get(0).folderPath.get(0).equals("日本語 🎮"));
        System.out.println("PASS "+count+" folder metadata checks; identity/cache/backward compatibility preserved");
    }
}
