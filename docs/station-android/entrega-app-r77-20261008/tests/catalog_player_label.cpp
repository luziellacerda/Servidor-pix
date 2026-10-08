#include "station_catalog_player_label.h"
#include <cassert>
#include <cstdio>
#include <string>
int main(){int checks=0;char out[32];
 auto test=[&](const std::string&s,bool accepted){bool actual=stationPlayerEvidenceLabel(s.data(),s.size(),out,sizeof(out));assert(actual==accepted);assert(std::string(out)==(accepted?s:"—"));checks+=2;};
 test("1",true);test("",false);test("—",false);test("2",false);test("4",false);test("1-4",false);test("4 players",false);test("2 sim",false);test("0 sim.",false);test("1 sim.",false);test("17 sim.",false);test("04 sim.",false);test("99 alt.",false);test("4 SIM.",false);test("4 alt. ",false);test(std::string("4 sim.\0",7),false);
 for(int n=2;n<=16;n++){test(std::to_string(n)+" sim.",true);test(std::to_string(n)+" alt.",true);}
 char small[3]={};assert(!stationPlayerEvidenceLabel("1",1,small,1)&&small[0]==0);++checks;
 assert(!stationPlayerEvidenceLabel(nullptr,6,out,sizeof(out))&&std::string(out)=="—");++checks;
 assert(!stationPlayerEvidenceLabel("1",1,nullptr,0));++checks;
 printf("PASS %d native player label checks\n",checks);
}
