package org.emulationstation.frontend.station;
public final class HttpRouteProbe {
 static int checks;
 static void ok(boolean value){checks++;if(!value)throw new AssertionError("check "+checks);}
 public static void main(String[] args)throws Exception {
  boolean fixed=args.length>0&&args[0].equals("fixed");String route="/v1/station/online/multiplayer/command";
  ok(StationHttp.allowed("POST",route)==fixed);
  for(String method:new String[]{"GET","PUT","DELETE","PATCH","HEAD","post"})ok(!StationHttp.allowed(method,route));
  for(String suffix:new String[]{"/","?x=1","/relay","/../command","%00","#fragment"})ok(!StationHttp.allowed("POST",route+suffix));
  ok(!StationHttp.allowed("POST","https://other.invalid"+route));
  ok(StationHttp.allowed("POST","/v1/station/online/command"));ok(StationHttp.allowed("POST","/v1/station/online/events"));
  ok(StationHttp.allowed("GET","/v1/station/catalog?metadata=1"));
  ok(!StationHttp.allowed("GET","/v1/station/covers/../../x"));
  StationHttp http=new StationHttp();StationApi.Cancellation cancel=new StationApi.Cancellation();cancel.cancel();
  for(int size:new int[]{0,1,8192}){
   try{http.exchange("POST",route,new byte[size],null,cancel);throw new AssertionError("request escaped cancellation");}
   catch(java.io.InterruptedIOException expected){ok(fixed);}
   catch(java.io.IOException expected){ok(!fixed&&expected.getMessage().equals("Unsupported Station request"));}
  }
  try{http.exchange("POST",route,new byte[8193],null,cancel);throw new AssertionError("oversize");}
  catch(java.io.IOException expected){ok(expected.getMessage().equals("Unsupported Station request"));}
  System.out.println("PASS "+checks+" actual HTTP route checks; fixed="+fixed+"; no request sent");
 }
}
