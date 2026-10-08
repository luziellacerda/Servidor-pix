package org.emulationstation.frontend.netplay;

public final class StationRoomLeaveIntentTest {
    private static int checks;
    private static void check(boolean ok,String name){checks++;if(!ok)throw new AssertionError(name);}
    public static void main(String[] args){
        String room="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",other="bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",protocol="station-stream.v3";
        StationRoomLeaveIntent intent=StationRoomLeaveIntent.human(room,7,protocol);
        check(StationRoomLeaveIntent.parse(intent.encoded).encoded.equals(intent.encoded),"durable roundtrip");
        check(intent.decide(room,7,protocol)==StationRoomLeaveIntent.Decision.LEAVE,"exact target eligible");
        for(long generation:new long[]{1,6,8,Long.MAX_VALUE}){
            check(intent.decide(room,generation,protocol)==StationRoomLeaveIntent.Decision.HOLD_DIFFERENT_GENERATION,"different generation never leaves");
            check(intent.afterResult(intent.encoded,true,room,generation,protocol).equals(intent.encoded),"same room conflict remains pending");
        }
        check(intent.decide(room,7,"station-stream.v2")==StationRoomLeaveIntent.Decision.HOLD_DIFFERENT_GENERATION,"protocol cannot cross");
        check(intent.decide(other,7,protocol)==StationRoomLeaveIntent.Decision.ACKNOWLEDGED_ABSENT,"new room proves old membership absent without leave");
        check(intent.afterResult(intent.encoded,true,other,7,protocol).isEmpty(),"signed other membership retires only old intent");
        check(intent.afterResult(intent.encoded,true,null,0,"").isEmpty(),"signed absence acknowledges completion");
        check(intent.afterResult(intent.encoded,false,null,0,"").equals(intent.encoded),"network failure retains intention");
        check(intent.afterResult(intent.encoded,true,room,7,protocol).equals(intent.encoded),"sending request is not acknowledgement");
        StationRoomLeaveIntent newer=StationRoomLeaveIntent.human(other,20,protocol);
        check(intent.afterResult(newer.encoded,true,null,0,"").equals(newer.encoded),"late completion preserves newer human intent");
        check(intent.afterResult("",true,null,0,"").isEmpty(),"no human intent cannot be invented by lifecycle");
        for(int retry=0;retry<100;retry++){
            check(intent.afterResult(intent.encoded,false,null,0,"").equals(intent.encoded),"repeated transport failures retain target");
            check(StationRoomLeaveIntent.parse(intent.encoded).requestId.equals(intent.requestId),"retry preserves request id");
        }
        check(intent.afterResult(intent.encoded,true,null,0,"").isEmpty(),"lost successful reply reconciles on later signed snapshot");
        for(String invalid:new String[]{"","station-stream.v2|"+room+"|7|"+intent.requestId,protocol+"|"+room+"|0|"+intent.requestId,protocol+"|"+room+"|-1|"+intent.requestId,protocol+"|"+room+"|9223372036854775808|"+intent.requestId,intent.encoded+"|extra","anything"})
            check(StationRoomLeaveIntent.parse(invalid)==null,"malformed intent cannot trigger departure");
        check(StationRoomLeaveIntent.parse(null)==null,"no stored human intention");
        System.out.println("{\"passed\":true,\"checks\":"+checks+",\"scope\":\"pure exact human-departure identity and acknowledgement; no Android/network gameplay\"}");
    }
}
