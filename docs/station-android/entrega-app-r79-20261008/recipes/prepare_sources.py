"""Prepare narrowly scoped R79 overlays; keep frozen R77/R78 sources intact."""
from pathlib import Path
import json, hashlib, xml.etree.ElementTree as ET, re
ROOT=Path(__file__).resolve().parent.parent
VERSIONS=ROOT.parent
def sha(p): return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def write(rel,text):
    p=ROOT/rel;p.parent.mkdir(parents=True,exist_ok=True);p.write_text(text,encoding='utf8',newline='\n')
def replace(text,old,new):
    assert text.count(old)==1,old[:100]
    return text.replace(old,new)
http=(VERSIONS/'station-collection-videos-r67-20261007/client/src/java/org/emulationstation/frontend/station/StationHttp.java').read_text('utf8')
http=replace(http,'int maximum=path.equals(', 'int maximum=path.equals("/v1/station/online/multiplayer/command")?8192:path.equals(')
http=replace(http,'|| path.equals("/v1/station/online/command") || path.equals("/v1/station/online/events");','|| path.equals("/v1/station/online/command") || path.equals("/v1/station/online/events")\n            || path.equals("/v1/station/online/multiplayer/command");')
write('java/client/src/java/org/emulationstation/frontend/station/StationHttp.java',http)
rooms=(VERSIONS/'station-multiplayer-r77-20261008/java/netplay-src/org/emulationstation/frontend/netplay/StationRoomsActivity.java').read_text('utf8')
rooms=replace(rooms,'private String heroRequestId="";private int heroEpoch=-1;','private String heroRequestId="";private int heroEpoch=-1;private String profileRequestId="";private int profileEpoch=-1;')
rooms=replace(rooms,'private void connect(){if(!active)return;epoch++;cancelAll();','private void connect(){if(!active)return;epoch++;stopMultiplayerPoll();cancelAll();')
rooms=replace(rooms,'client=new StationOnlineClient(this);client.page=page;','client=new StationOnlineClient(this);client.page=page;\n                runOnUiThread(()->{if(active&&epoch==generation){roomLayout(room()!=null);loadHero();}});')
start=rooms.index('                client.discoverMultiplayer(cancel);current=client.compose(null);deliver(current,generation);')
end=rooms.index('                long heartbeat=',start)
rooms=rooms[:start]+'''                // Present the verified social snapshot before probing optional v3 support.
                deliver(current,generation);beginMultiplayerDiscovery(client,generation);
'''+rooms[end:]
anchor='    private void deliver(JSONObject snapshot,int generation)'
pos=rooms.index(anchor)
rooms=rooms[:pos]+'''    private void beginMultiplayerDiscovery(final StationOnlineClient connectedClient,final int generation){
        if(!active||epoch!=generation||client!=connectedClient)return;
        multiplayerPolling=multiplayerEvents.schedule(()->{
            StationApi.Cancellation probe=new StationApi.Cancellation();requests.add(probe);
            String notice="As novas salas aguardam ativação no servidor. A capa e os dados do jogo continuam disponíveis.";
            try{
                connectedClient.discoverMultiplayer(probe);probe.check();
                if(!active||epoch!=generation||client!=connectedClient)return;
                deliver(connectedClient.compose(null),generation);
                if(connectedClient.multiplayerEnabled)notice="";
            }catch(Exception error){
                if(probe.cancelled()||!active||epoch!=generation)return;
                recordFailure(error);notice="Não foi possível consultar esta modalidade online. Seus dados e sua capa continuam disponíveis. Use Reconectar para tentar novamente.";
            }finally{requests.remove(probe);}
            final String message=notice;
            runOnUiThread(()->{
                if(!active||epoch!=generation||client!=connectedClient)return;
                if(!busy&&!creating)showCreateMessage(message,!message.isEmpty());
                loadHero();
                if(connectedClient.multiplayerEnabled){
                    multiplayerPolling=multiplayerEvents.scheduleWithFixedDelay(()->{
                        if(!active||epoch!=generation||client!=connectedClient)return;
                        StationApi.Cancellation poll=new StationApi.Cancellation();requests.add(poll);
                        try{deliver(connectedClient.pollMultiplayer(poll),generation);}
                        catch(Exception error){if(!poll.cancelled())recordFailure(error);}
                        finally{requests.remove(poll);}
                    },1,1,TimeUnit.SECONDS);
                }
            });
        },0,TimeUnit.MILLISECONDS);
    }
'''+rooms[pos:]
rooms=replace(rooms,'if(client==null||state.get()==null||target==null||target.isEmpty()||(target.equals(heroRequestId)&&heroEpoch==epoch))return;','if(client==null||target==null||target.isEmpty())return;\n        if(target.equals(heroRequestId)&&heroEpoch==epoch){loadHeroProfile(target);return;}')
begin=rooms.index('                StationApi.Cancellation check=new StationApi.Cancellation();requests.add(check);',rooms.index('    private void loadHero(){'))
end=rooms.index('            }catch(Exception unavailable){android.util.Log.i("StationOnline","Selected game metadata unavailable");}',begin)
profile=rooms[begin:end]
rooms=rooms[:begin]+rooms[end:]
pos=rooms.index('    private static boolean contains(JSONArray')
rooms=rooms[:pos]+'''    private void loadHeroProfile(final String target){
        if(client==null||!client.multiplayerEnabled||state.get()==null||(target.equals(profileRequestId)&&profileEpoch==epoch))return;
        profileRequestId=target;profileEpoch=epoch;final int generation=epoch;
        commands.execute(()->{
'''+profile+'''        });
    }
'''+rooms[pos:]
# Loading local metadata and fetching a capability may complete in either order.
rooms=replace(rooms,'subtitle.setText(name);gamePlatform.setText(platform);headerPlayers.setText("Jogadores a confirmar");headerPlayers.setContentDescription("Quantidade de jogadores online ainda não confirmada");headerRating.setRating(details.rating);','headerRating.setRating(details.rating);')
rooms=replace(rooms,'String name=item.name,platform=item.platform;','String name=item.name,platform=item.platform;\n                runOnUiThread(()->{if(active&&generation==epoch&&target.equals(heroRequestId)){subtitle.setText(name);gamePlatform.setText(platform);}});')
rooms=replace(rooms,'            }catch(Exception unavailable){android.util.Log.i("StationOnline","Selected game metadata unavailable");}\n        });\n    }','            }catch(Exception unavailable){android.util.Log.i("StationOnline","Selected game metadata unavailable");}\n        });\n        loadHeroProfile(target);\n    }')
write('java/netplay-src/org/emulationstation/frontend/netplay/StationRoomsActivity.java',rooms)

# This is a description-only fallback, never evidence of controllers or online seats.
source=Path(r'E:\ESTUDO APK\work\native-carousel\implementation\metadata-sources\catalog-xml\11-dreamcast.xml')
def key(s): return re.sub(r'[ \t\r\n]+',' ',s.strip(' \t\r\n')).translate(str.maketrans('ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'))
group={};empty=0
for g in ET.parse(source).getroot().iter('game'):
    name=(g.findtext('name')or'').strip();description=(g.findtext('desc')or'').strip()
    if not name or not description:empty+=1;continue
    group.setdefault(key(name),[]).append((name,description))
accepted=[];conflicts=[]
for k,rows in sorted(group.items()):
    if len({r[1] for r in rows})!=1:conflicts.append(k);continue
    accepted.append(dict(key=k,name=rows[0][0],description=rows[0][1]))
write('data/dreamcast-descriptions.json',json.dumps(accepted,ensure_ascii=False,indent=2)+'\n')
header=['#pragma once','// Local Dreamcast XML; descriptions only. Exact title with ASCII case/space normalization.','struct StationDreamcastDescription {const char* key;const char* name;const char* description;};','static const StationDreamcastDescription stationDreamcastDescriptions[]={']
for r in accepted:header.append('{'+','.join(json.dumps(r[k],ensure_ascii=False).replace('?',r'\?') for k in ['key','name','description'])+'},')
header+=['};',r'''
static bool stationDreamcastTitleKey(const char* text,char* out,unsigned size){
 if(!text||!out||size<2)return false;
 unsigned n=0;bool gap=false;
 for(const unsigned char*p=(const unsigned char*)text;*p;p++){
  unsigned char c=*p;
  if(c==' '||c=='\t'||c=='\r'||c=='\n'){if(n)gap=true;continue;}
  if(n+(gap?1u:0u)+1>=size)return false;
  if(gap){out[n++]=' ';gap=false;}
  out[n++]=(char)(c>='A'&&c<='Z'?c+('a'-'A'):c);
 }
 out[n]=0;return n>0;
}
static const StationDreamcastDescription* stationDreamcastDescription(const char* platform,const char* title){
 if(!platform||strcmp(platform,"Dreamcast")!=0)return nullptr;
 char key[1024];if(!stationDreamcastTitleKey(title,key,sizeof(key)))return nullptr;
 unsigned lo=0,hi=sizeof(stationDreamcastDescriptions)/sizeof(stationDreamcastDescriptions[0]);
 while(lo<hi){unsigned mid=lo+(hi-lo)/2;int order=strcmp(key,stationDreamcastDescriptions[mid].key);if(order==0)return &stationDreamcastDescriptions[mid];if(order<0)hi=mid;else lo=mid+1;}
 return nullptr;
}
''']
write('native/station_dreamcast_descriptions.h','\n'.join(header)+'\n')
native=(VERSIONS/'station-synopses-r78-20261008/native/native_info.h').read_text('utf8')
native=replace(native,'#include "station_game_infos.h"','#include "station_game_infos.h"\n#include "station_dreamcast_descriptions.h"')
native=replace(native,'  body=stationSynopsisChoose(serverDescription,heading,gameInfo?gameInfo->name:nullptr,gameInfo?gameInfo->pages:nullptr,gameInfo&&stationSynopsisNeedsOverride(id,serverDescription),"Sinopse ainda não localizada para esta edição.").text;',
'''  const StationDreamcastDescription*dc=gameInfo?nullptr:stationDreamcastDescription(key,heading);
  body=stationSynopsisChoose(serverDescription,heading,gameInfo?gameInfo->name:dc?dc->name:nullptr,gameInfo?gameInfo->pages:dc?dc->description:nullptr,gameInfo&&stationSynopsisNeedsOverride(id,serverDescription),"Sinopse ainda não localizada para esta edição.").text;''')
write('native/native_info.h',native)
write('evidence/dreamcast-source.json',json.dumps(dict(source=str(source),sourceSHA256=sha(source),sourceRecords=686,sourceWithoutDescription=empty,unambiguousTitles=len(accepted),conflictingTitles=conflicts,observedPhoneCount=243,liveCatalogEnumerated=False,all243Covered=False,scope='Description only; no fuzzy match, no region/punctuation removal, no capacity or rating claims'),ensure_ascii=False,indent=2)+'\n')
print(json.dumps(dict(java=2,dreamcastTitles=len(accepted),conflicts=len(conflicts))))
