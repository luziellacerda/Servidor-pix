#pragma once
// Presentation only: no URLs, IO, threads, catalogue mutation or invented metadata.
struct CollectionText {
 char* data;unsigned long capacity,used;
 CollectionText(char* value,unsigned long size):data(value),capacity(size),used(0){if(capacity)data[0]=0;}
 void append(const char* text){
  if(!text||!capacity)return;
  while(*text){
   unsigned char c=(unsigned char)*text;unsigned n=c<0x80?1:c<0xe0?2:c<0xf0?3:4;
   if(used+n>=capacity)return;
   for(unsigned i=1;i<n;i++)if(!text[i]||((unsigned char)text[i]&0xc0)!=0x80)return;
   for(unsigned i=0;i<n;i++)data[used++]=*text++;
   data[used]=0;
  }
 }
 void number(unsigned long n){char buffer[24],reverse[24];int i=0,j=0;do{reverse[i++]=(char)('0'+n%10);n/=10;}while(n);while(i)buffer[j++]=reverse[--i];buffer[j]=0;append(buffer);}
};
static const char*collectionLeafName(const char*path){const char*leaf=path;for(const char*p=path;*p;p++)if(*p=='/')leaf=p+1;return leaf;}
static bool collectionHasPath(const char*parent,const char*path,bool all,bool direct){
 if(all)return true;
 while(*parent&&*path&&*parent==*path){parent++;path++;}
 return !*parent&&(!*path||(!direct&&*path=='/'));
}
#include "collection_editorial.h"
#include "system_infos.h"
static bool collectionKeyEqual(const char*a,const char*b){
 while(*a&&*b){char x=*a++,y=*b++;if(x>='A'&&x<='Z')x+=32;if(y>='A'&&y<='Z')y+=32;if(x!=y)return false;}
 while(*a==' ')a++;while(*b==' ')b++;return !*a&&!*b;
}
#include "station_collection_editorial_aliases.h"
static const char*collectionEditorial(const char*platform,const char*path){
 path=stationCollectionEditorialPath(platform,path);
 for(const auto&e:collectionEditorials)if(collectionKeyEqual(platform,e.platform)&&collectionKeyEqual(path,e.path))return e.text;
 return nullptr;
}
static void collectionSynopsis(char*output,unsigned long size,const char*name,const char*platform,unsigned long count,int kind,const char*const*examples,unsigned exampleCount,const char*path=nullptr){
 CollectionText text(output,size);const char*editorial=kind==2?nullptr:collectionEditorial(platform,path?path:name);
 if(kind==2){
  text.append("Toda a biblioteca de ");text.append(platform);text.append(" em uma única seleção: ");text.number(count);text.append(count==1?" jogo disponível.":" jogos disponíveis.");
  text.append(" Aqui estão os títulos publicados na raiz e nas coleções da plataforma, reunidos para facilitar a descoberta e o retorno aos seus favoritos.\n\n");
  bool found=false;for(const auto&info:systemInfos)if(collectionKeyEqual(platform,info.key)){text.append(info.description);found=true;break;}
  if(!found)text.append("Explore as capas e as fichas para conhecer a proposta de cada jogo. Personagens, objetivos e estilos variam de um título para outro, permitindo escolher a próxima experiência pelo que desperta sua curiosidade.");
 }else{
  text.append(name);text.append(" — ");text.number(count);text.append(count==1?" jogo de ":" jogos de ");text.append(platform);text.append(".\n\n");
  if(editorial)text.append(editorial);
  else{
   text.append("Esta seleção reúne os títulos organizados em ");text.append(name);text.append(". Percorrer suas capas é uma maneira de reconhecer jogos familiares e encontrar outras propostas dentro da mesma coleção. Cada ficha apresenta a experiência e a edição daquele título, preservando as diferenças entre os jogos.\n\n");
   text.append(kind==1?"A lista apresenta os jogos diretamente nesta pasta. ":"A lista reúne esta seleção e suas subcoleções, quando houver. ");
   text.append("Vale comparar as sinopses, observar os objetivos e escolher o próximo jogo pelo tipo de desafio que procura. A seleção acompanha os títulos publicados no catálogo: novos itens aparecem na mesma organização, sem perder o nome da coleção.");
  }
 }
 if(exampleCount&&examples){text.append("\n\nNesta seleção: ");for(unsigned i=0;i<exampleCount;i++){if(i)text.append("; ");text.append(examples[i]);}text.append(".");}
 text.append("\n\nAbra a lista para pesquisar nesta seleção, consultar os detalhes, baixar um título ou continuar um jogo já instalado.");
}
