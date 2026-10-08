#include <cstdio>
#include <cstring>
#include "collection_presentation.h"
static int checks=0,failures=0;
#define CHECK(c) do{++checks;if(!(c)){++failures;std::printf("FAIL line %d: %s\n",__LINE__,#c);}}while(0)
int main(){
 const char*broken[]={"# 0 - ART OF FIGTHERS COLE\ufffd\ufffdO #","# 1 - METAL SLUG COLE\ufffd\ufffdO #",
  "# 2 - SAMURAI SHODOWN COLE\ufffd\ufffdO #","# 3 - THE KING OF FIGTHERS COLE\ufffd\ufffdO #","# 4 - FATAL FURY COLE\ufffd\ufffdO #"};
 const char*correct[]={"# 0 - ART OF FIGTHERS COLEÇÃO #","# 1 - METAL SLUG COLEÇÃO #",
  "# 2 - SAMURAI SHODOWN COLEÇÃO #","# 3 - THE KING OF FIGTHERS COLEÇÃO #","# 4 - FATAL FURY COLEÇÃO #"};
 char text[6144];const char*examples[]={"Jogo A","Jogo B"};
 for(int i=0;i<5;++i){
  CHECK(std::strcmp(stationCollectionEditorialPath("Neo Geo",broken[i]),correct[i])==0);
  CHECK(stationCollectionEditorialPath("Super Nintendo",broken[i])==broken[i]);
  CHECK(stationCollectionEditorialPath("Neo Geo CD",broken[i])==broken[i]);
  CHECK(stationCollectionEditorialPath("Neo Geo",correct[i])==correct[i]);
  const char*editorial=collectionEditorial("Neo Geo",correct[i]);CHECK(editorial!=nullptr);
  CHECK(collectionEditorial("Neo Geo",broken[i])==editorial);
  collectionSynopsis(text,sizeof(text),broken[i],"Neo Geo",4,0,examples,2,broken[i]);
  CHECK(std::strstr(text,editorial)!=nullptr);CHECK(std::strstr(text,broken[i])!=nullptr);
  CHECK(std::strstr(text,"Jogo A; Jogo B")!=nullptr);CHECK(std::strstr(text,"4 jogos de Neo Geo")!=nullptr);
 }
 // Editorial selection never matches an unknown sibling, substring or child.
 const char*unknown="# 8 - UNKNOWN COLE\ufffd\ufffdO #";
 CHECK(stationCollectionEditorialPath("Neo Geo",unknown)==unknown);
 CHECK(collectionEditorial("Neo Geo",unknown)==nullptr);
 CHECK(collectionEditorial("Neo Geo","# 1 - METAL SLUG COLE\ufffd\ufffdO #/child")==nullptr);
 CHECK(collectionEditorial("Neo Geo","METAL SLUG")==nullptr);
 collectionSynopsis(text,sizeof(text),"Coleção futura","Neo Geo",3,0,examples,2,unknown);
 CHECK(std::strstr(text,"Esta seleção reúne")!=nullptr);CHECK(std::strstr(text,"3 jogos de Neo Geo")!=nullptr);
 for(const auto&row:collectionEditorials)CHECK(collectionEditorial(row.platform,row.path)==row.text);
 std::printf("%s %d editorial path checks; 5 exact aliases; no catalog renames\n",failures?"FAIL":"PASS",checks);
 return failures?1:0;
}
