#pragma once
// Presentation only. Exact ID/platform lookup must happen before this selector.
// No title search, file access, allocation, catalogue mutation or network calls.
enum class StationSynopsisReason { Published, Blank, Placeholder, TitleOnly, ReviewedOverride };
struct StationSynopsisChoice {const char*text;StationSynopsisReason reason;bool usedExactFallback;};

static inline unsigned stationSynopsisSpaceBytes(const char*p){
 if(!p||!*p)return 0;
 unsigned char a=(unsigned char)p[0];
 if(a==' '||(a>='\t'&&a<='\r'))return 1;
 if(a==0xc2&&(unsigned char)p[1]==0xa0)return 2; // non-breaking space
 if(a==0xe2&&(unsigned char)p[1]==0x80){
  unsigned char b=(unsigned char)p[2];
  if((b>=0x80&&b<=0x8b)||b==0xaf)return 3; // Unicode spaces, zero-width space
 }
 if(a==0xe2&&(unsigned char)p[1]==0x81&&(unsigned char)p[2]==0x9f)return 3;
 if(a==0xe3&&(unsigned char)p[1]==0x80&&(unsigned char)p[2]==0x80)return 3;
 return 0;
}
static inline const char*stationSynopsisSkipSpaces(const char*p){
 if(!p)return "";while(unsigned n=stationSynopsisSpaceBytes(p))p+=n;return p;
}
static inline bool stationSynopsisBlank(const char*text){return !*stationSynopsisSkipSpaces(text);}
static inline unsigned stationSynopsisNextComparable(const char*&p){
 if(stationSynopsisSpaceBytes(p)){p=stationSynopsisSkipSpaces(p);return *p?' ':0;}
 unsigned char c=(unsigned char)*p;if(!c)return 0;++p;
 if(c>='A'&&c<='Z')return c+32;
 // Fold only Latin-1 letters used in the exact Portuguese placeholder list.
 if(c==0xc3&&*p){unsigned char next=(unsigned char)*p;
  if((next>=0x80&&next<=0x96)||(next>=0x98&&next<=0x9e)){++p;return 0x100+next+32;}
  if(next>=0xa0&&next<=0xbe){++p;return 0x100+next;}
 }
 return c;
}
static inline bool stationSynopsisSame(const char*a,const char*b,bool terminalPeriod=false){
 const char*x=stationSynopsisSkipSpaces(a),*y=stationSynopsisSkipSpaces(b);
 for(;;){unsigned l=stationSynopsisNextComparable(x),r=stationSynopsisNextComparable(y);
  if(terminalPeriod){if(l=='.'&&!*stationSynopsisSkipSpaces(x))l=0;if(r=='.'&&!*stationSynopsisSkipSpaces(y))r=0;}
  if(l!=r)return false;if(!l)return true;
 }
}
static inline bool stationSynopsisPlaceholder(const char*text){
 // Whole-value matches only. A sentence mentioning one of these phrases is prose.
 static const char*const known[]={
  "Sinopse ainda não localizada para esta edição", "Sinopse ainda não disponível nesta edição",
  "Sinopse ainda não disponível", "Sinopse não disponível", "Sem sinopse",
  "Descrição não disponível", "Nenhuma descrição disponível", "Sem descrição",
  "No description available", "No synopsis available", "Description unavailable", "Synopsis unavailable"
 };
 for(const char*value:known)if(stationSynopsisSame(text,value,true))return true;
 return false;
}
static inline StationSynopsisChoice stationSynopsisChoose(const char*published,const char*displayTitle,
 const char*exactTitle,const char*exactFallback,bool reviewedOverride,const char*pending){
 StationSynopsisReason reason=StationSynopsisReason::Published;
 if(stationSynopsisBlank(published))reason=StationSynopsisReason::Blank;
 else if(stationSynopsisPlaceholder(published))reason=StationSynopsisReason::Placeholder;
 else if(reviewedOverride)reason=StationSynopsisReason::ReviewedOverride;
 else if((!stationSynopsisBlank(displayTitle)&&stationSynopsisSame(published,displayTitle))||
         (!stationSynopsisBlank(exactTitle)&&stationSynopsisSame(published,exactTitle)))reason=StationSynopsisReason::TitleOnly;
 if(reason==StationSynopsisReason::Published)return {published,reason,false};
 if(!stationSynopsisBlank(exactFallback)&&!stationSynopsisPlaceholder(exactFallback))return {exactFallback,reason,true};
 return {pending?pending:"Sinopse ainda não localizada para esta edição.",reason,false};
}
