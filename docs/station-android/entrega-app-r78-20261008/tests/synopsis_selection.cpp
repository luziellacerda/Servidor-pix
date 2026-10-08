#include <cstdio>
#include <cstring>
#include <initializer_list>
#include "station_synopsis_selection.h"
#include "station_synopsis_pages.hpp"
struct GameInfo {const char*system;const char*id;const char*name;const char*pages;const char*source;int pageCount;};
#include "station_game_infos.h"
#include "station_game_lookup.h"
#include "production_baseline_selector.h"
#include "generated_synopsis_cases.h"
static int checks=0,failures=0,baselineBad=0;
#define CHECK(c) do{++checks;if(!(c)){++failures;std::printf("FAIL line %d: %s\n",__LINE__,#c);}}while(0)
static const char*pending="Sinopse ainda não localizada para esta edição.";
int main(){
 const char*fallback="Uma aventura de ação com desafios e exploração.";
 const char*blank[]={nullptr,""," ","\r\n\t\v\f","\u00a0", "\u2000\u2001\u2002\u2003\u2004\u2005\u2006\u2007\u2008\u2009\u200a\u200b\u202f\u205f\u3000", " \n\u00a0\u3000\t"};
 for(const char*value:blank){auto c=stationSynopsisChoose(value,"Título","Título",fallback,false,pending);
  CHECK(c.text==fallback&&c.usedExactFallback&&c.reason==StationSynopsisReason::Blank);
  CHECK(stationSynopsisChoose(value,"Título","Título",nullptr,false,pending).text==pending);
 }
 const char*placeholders[]={"Sinopse ainda não localizada para esta edição."," SINOPSE NÃO DISPONÍVEL. ",
  "Sinopse ainda não disponível nesta edição", "Sinopse ainda não disponível", "Sem sinopse",
  "Descrição não disponível", "Nenhuma descrição disponível", "Sem descrição", "No description available.",
  "No\fDESCRIPTION  AVAILABLE", "No synopsis available", "Description unavailable", "Synopsis unavailable"};
 for(const char*value:placeholders){auto c=stationSynopsisChoose(value,"Título","Título",fallback,false,pending);
  CHECK(c.text==fallback&&c.usedExactFallback&&c.reason==StationSynopsisReason::Placeholder);
  CHECK(stationSynopsisChoose(value,"Título","Título",nullptr,false,pending).text==pending);
 }
 const char*prose[]={"Corra!", "Lute.", "Tetris é um quebra-cabeça.", "  Uma aventura curta.  ",
  "Sem descrição do manual, a história é apresentada no jogo.", "A frase No description available aparece no menu.",
  "Pontuação: 10.", "Old Towers recebeu uma sinopse nova.", "Título — jogo de ação", " \u00a0Ação\u3000 ",
  "Sinopse não disponível nesta tradução; o jogo base apresenta fases de plataforma.", "No description available!"};
 for(const char*value:prose){auto c=stationSynopsisChoose(value,"Título","Título",fallback,false,pending);
  CHECK(c.text==value&&!c.usedExactFallback&&c.reason==StationSynopsisReason::Published);
 }
 CHECK(stationSynopsisChoose(" TÍTULO ","Título",nullptr,fallback,false,pending).text==fallback);
 CHECK(stationSynopsisChoose("Título\fcom    espaços","Outro","Título com espaços",fallback,false,pending).text==fallback);
 CHECK(stationSynopsisChoose("Título.","Título",nullptr,fallback,false,pending).reason==StationSynopsisReason::Published);
 CHECK(stationSynopsisChoose("Old Towers","Old Towers (USA)",nullptr,fallback,true,pending).reason==StationSynopsisReason::ReviewedOverride);
 CHECK(stationSynopsisChoose("No description available","Título",nullptr," ",false,pending).text==pending);
 CHECK(stationSynopsisChoose("No description available","Título",nullptr,"Sem sinopse",false,pending).text==pending);
 CHECK(!stationSynopsisSame("Títulos","Título"));CHECK(!stationSynopsisSame("Título","Titulos"));
 CHECK(!stationSynopsisSame("ação","acão"));CHECK(stationSynopsisSame("AÇÃO","ação"));
 CHECK(NSTATIONGAMEINFOS==2467);
 for(int i=0;i<NSTATIONGAMEINFOS;++i){const auto&record=stationGameInfos[i];
  CHECK(i==0||std::strcmp(stationGameInfos[i-1].id,record.id)<0);
  CHECK(!stationSynopsisBlank(record.pages));CHECK(!stationSynopsisPlaceholder(record.pages));
  const auto*exact=findStationGameInfo(record.system,record.id);CHECK(exact==&record);
  CHECK(findStationGameInfo("Wrong platform",record.id)==nullptr);
  for(const char*value:{"", " \n\t", "No description available.",record.name}){
   auto c=stationSynopsisChoose(value,record.name,exact->name,exact->pages,false,pending);
   CHECK(c.text==record.pages&&c.usedExactFallback);
  }
  const char*published="Prosa nova publicada com revisão própria.";
  CHECK(stationSynopsisChoose(published,record.name,exact->name,exact->pages,false,pending).text==published);
 }
 // The old expression is extracted verbatim from frozen R77 by the recipe.
 const GameInfo*sample=&stationGameInfos[0];const char*id=sample->id;
 for(const char*text:{" \n\t", "No description available.", sample->name}){
  if(productionBaselineSynopsis(id,text,sample)==text)++baselineBad;
  CHECK(stationSynopsisChoose(text,sample->name,sample->name,sample->pages,false,pending).text==sample->pages);
 }
 CHECK(baselineBad==3);
 int exactReplacements=0,pagedReplacements=0;
 for(const auto&row:publishedSynopsisCases){
  const auto*exact=findStationGameInfo(row.platform,row.id);CHECK(exact!=nullptr);
  CHECK(std::strcmp(exact->pages,row.expected)==0);
  std::string native=station::synopsisPages(row.published);
  auto choice=stationSynopsisChoose(native.c_str(),exact->name,exact->name,exact->pages,
    exact&&stationSynopsisNeedsOverride(row.id,native.c_str()),pending);
  bool differs=std::strcmp(row.published,exact->pages)!=0;
  if(differs){CHECK(choice.usedExactFallback&&choice.text==exact->pages);++exactReplacements;
   if(native.find('\f')!=std::string::npos)++pagedReplacements;
  }else CHECK(choice.text==native.c_str()&&!choice.usedExactFallback);
  std::string updated="Texto atualizado pelo servidor: ";updated+=row.published;
  std::string updatedNative=station::synopsisPages(updated);
  CHECK(!stationSynopsisNeedsOverride(row.id,updatedNative.c_str()));
  CHECK(stationSynopsisChoose(updatedNative.c_str(),exact->name,exact->name,exact->pages,false,pending).text==updatedNative.c_str());
 }
 CHECK(exactReplacements>=50);CHECK(pagedReplacements>=14);
 CHECK(findStationGameInfo("Super Nintendo","unpublished_id")==nullptr);
 CHECK(stationSynopsisChoose("Título","Título",nullptr,nullptr,false,pending).text==pending);
 std::printf("%s %d synopsis checks; %d baseline insufficient-text wins reproduced; %d exact IDs; %d exact replacements including %d paged\n",failures?"FAIL":"PASS",checks,baselineBad,NSTATIONGAMEINFOS,exactReplacements,pagedReplacements);
 return failures?1:0;
}
