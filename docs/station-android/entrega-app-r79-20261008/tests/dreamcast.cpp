#include <cstring>
#include <cstdio>
#include <cstdlib>
#include <string>
#include "station_dreamcast_descriptions.h"
#include "station_synopsis_selection.h"
static unsigned checks;
static void check(bool ok){++checks;if(!ok){std::fprintf(stderr,"FAIL %u\n",checks);std::exit(1);}}
int main(){
 for(const auto&r:stationDreamcastDescriptions){
  auto p=stationDreamcastDescription("Dreamcast",r.name);check(p==&r);check(std::strlen(p->description)>0);
  check(!stationDreamcastDescription("PlayStation",r.name));
  check(!stationDreamcastDescription("dreamcast",r.name));
  std::string upper=r.name;for(char&c:upper)if(c>='a'&&c<='z')c-=32;
  check(stationDreamcastDescription("Dreamcast",upper.c_str())==p);
  upper=" \t"+upper+"\r\n";check(stationDreamcastDescription("Dreamcast",upper.c_str())==p);
  std::string similar=std::string(r.name)+" (unreviewed edition)";check(!stationDreamcastDescription("Dreamcast",similar.c_str()));
  const char*newText="Descrição atual recebida do servidor.";
  check(stationSynopsisChoose(newText,r.name,r.name,r.description,false,"missing").text==newText);
  check(stationSynopsisChoose("",r.name,r.name,r.description,false,"missing").text==r.description);
 }
 auto first=stationDreamcastDescription("Dreamcast","102 Dalmatians - Puppies to the Rescue");check(first);check(std::strstr(first->description,"Cruella")!=nullptr);
 check(!stationDreamcastDescription(nullptr,"test"));check(!stationDreamcastDescription("Dreamcast",nullptr));
 std::string huge(1200,'x');check(!stationDreamcastDescription("Dreamcast",huge.c_str()));
 char tiny[2];check(!stationDreamcastTitleKey("abc",tiny,2));
 check(!stationDreamcastDescription("Dreamcast","102 Dalmatians"));
 std::printf("PASS %u Dreamcast lookup/precedence checks; no live catalog enumeration\n",checks);
}
