#include "station_rating_animation.h"
// Native synopsis viewport: complete text, measured by the original font renderer.
#include "system_infos.h"
#include "game_infos.h"
#include "station_game_infos.h"
#include "station_info_layout.h"
#include "station_root_label.h"
#include "station_synopsis_scroll.h"
#include "station_synopsis_selection.h"
#include "station_metadata_lookup.h"
#include "station_game_panel_layout.h"
#include "native_players_evidence.h"
#include "station_header_layout.h"
#include "station_game_meta_row.h"
#include "station_platform_button_label.h"
static bool stationConsoleAvailable(const char*);
static void drawStationLottie(int,unsigned,StationInfoRect);
static unsigned infoStarsStarted;
static void*infoRatingUnknown;
static void drawStationConsole(void*,const char*,const StationInfoRect&);
#include "station_game_lookup.h"
struct Vec3{float x,y,z;};struct Vec2{float x,y;};
static void setLongText(void*t,const char*value){
 alignas(8) B s[24]={};strAssign(s,value);fn<void(*)(void*,const void*)>(0x2d2c70)(t,s);
 if(s[0]&1)fn<V>(0x39d820)(at<void*>(s,16));
}
static void*createInfoText(void*p,unsigned color){
 void*t=fn<void*(*)(U)>(0x39d9c0)(0x130);alignas(8) B empty[24]={};
 fn<void(*)(void*,void*,const void*,const void*,unsigned,int,Vec3,Vec2,unsigned)>(0x2d2a04)(t,at<void*>(p,0x10),empty,(B*)p+0x928+0xe8,color,0,Vec3{0,0,0},Vec2{0,0},0);
 fn<void(*)(void*,void*)>(0x2772c8)(p,t);return t;
}
static void*infoTitle;static void*infoDescription;
static void*infoListCountText;static StationInfoRect infoListCountViewport{};
static void*infoPlayersText;
static void*infoActionCountText;static void*infoActionPlayersText;
static char infoActionCount[48]={},infoActionPlayers[96]={};
static StationPrimaryMetaLayout infoPrimaryMeta{};
static const StationGameDetails*infoDetails;
static StationGamePanelLayout infoDetailsLayout{};static StationInfoRect infoTitleViewport{};
static bool infoGameDetailsVisible;
static StationInfoRect infoPlayersViewport{},infoHeaderStars{};static bool infoSinglePlayer;static float infoMetadataIcon;
static void fitInfoText(void*t,StationInfoRect r,float scale,int align){
 if(r.w<=0||r.h<=0)return;
 for(int i=0;i<5;i++){
  place(t,r.x,r.y,r.w,0,scale,align);fn<void(*)(void*,int)>(0x2d38e8)(t,3);
  float measured=at<float>(t,0x58)*scale;
  if(measured<=r.h||scale<=.30f){fn<void(*)(void*,float,float,float)>(0x277194)(t,r.x,r.y+(r.h-measured>0?(r.h-measured)*.5f:0),0);break;}
  scale*=r.h/measured*.98f;if(scale<.30f)scale=.30f;
 }
}
// Exact native glyph advances, including trailing spaces. TextComponent auto-width
// calls Font::sizeText and writes dimensions at +0x54/+0x58 (verified libmain ABI).
static float nativeInfoTextWidth(void*t,const char*text,float scale){
 if(!text||!*text){setLongText(t,"");return 0;}
 setLongText(t,text);fn<void(*)(void*,float,float)>(0x2771d8)(t,0.f,0.f);
 return at<float>(t,0x54)*scale;
}
static float fitGameTitleOneLine(void*t,const char*heading,StationInfoRect r,float scale=stationGameMetadataScale){
 char text[1024]={};U n=strlen(heading);if(n>1018)n=1018;
 while(n&&((B)heading[n]&0xC0)==0x80)n--;
 memcpy(text,heading,n);bool shortened=heading[n]!=0;
 float measured=nativeInfoTextWidth(t,text,scale);
 if(measured>r.w||shortened){
  // Binary search only at UTF-8 character boundaries; bounded layout work.
  unsigned ends[1024]={},characters=0;
  for(unsigned i=1;i<=n;i++)if(i==n||((B)text[i]&0xC0)!=0x80)ends[++characters]=i;
  char candidate[1024]={};unsigned low=0,high=characters,best=0;
  while(low<=high){
   unsigned mid=low+(high-low)/2,bytes=ends[mid];memcpy(candidate,text,bytes);memcpy(candidate+bytes,"\xE2\x80\xA6",4);
   float size=nativeInfoTextWidth(t,candidate,scale);
   if(size<=r.w){best=mid;low=mid+1;}else{if(!mid)break;high=mid-1;}
  }
  unsigned bytes=ends[best];memcpy(text+bytes,"\xE2\x80\xA6",4);
  measured=nativeInfoTextWidth(t,text,scale);
 }
 float line=at<float>(t,0x58)*scale;
 // Preserve auto-width: the next group starts after the displayed text itself.
 place(t,r.x,r.y+(r.h-line)*.5f,0,0,scale,0);
 fn<void(*)(void*,int)>(0x2d38e8)(t,3);
 return measured;
}
static int platformActionAlignment=1;
static char platformActionLabel[1024]="ABRIR";static StationInfoRect platformActionRect{};
static void preparePlatformActionLabel(void*p,const char*key,const char*title){
 float w=at<float>(p,0x54),h=at<float>(p,0x58),bh=h*.094f;
 auto button=folderMode?stationCollectionAction(w*.035f,h*.851f,coverSlot(p,0).w,bh,false):stationCompactActionRect({w*.035f,h*.851f,coverSlot(p,0).w,bh});
 float inset=actionLabelInset(bh);
 platformActionRect={button.x+inset,button.y,button.w-inset-bh*.16f,bh};
 void*t=(B*)p+0xe00;char collectionLabel[1024]={};
 const char*label=stationButtonFullName(key,title);
 if(folderMode){
  memcpy(collectionLabel,"ABRIR ",6);U n=strlen(title);if(n>1011)n=1011;
  while(n&&((B)title[n]&0xc0)==0x80)n--;memcpy(collectionLabel+6,title,n);label=collectionLabel;
 }else if(nativeInfoTextWidth(t,label,.92f)>platformActionRect.w)label=stationButtonShortName(key,label);
 fitGameTitleOneLine(t,label,platformActionRect,.92f);
 const char*displayed=strData((B*)t+0xd0);U n=strlen(displayed);if(n>sizeof(platformActionLabel)-5)n=sizeof(platformActionLabel)-5;
 while(n&&((B)displayed[n]&0xC0)==0x80)n--;
 memcpy(platformActionLabel,displayed,n);platformActionLabel[n]=0;
 platformActionAlignment=folderMode?1:0;
 if(!folderMode){float three=nativeInfoTextWidth(t,"   ",.92f);float measured=nativeInfoTextWidth(t,platformActionLabel,.92f);platformActionRect=stationMainButtonText(platformActionRect,measured,three);}
 fitInfoText(t,platformActionRect,.92f,platformActionAlignment);
}
static void layoutGameStatusText(void*t){
 static void*owner;static float oldW,oldH;alignas(8) static B previous[24]={};
 float w=at<float>(gui,0x54),h=at<float>(gui,0x58);auto r=stationStatusRect(w,h);
 const char*text=strData((B*)t+0xd0);
 // Relayout only on content/viewport changes or when the native header resets its bounds.
 if(!gameStatusNeedsLayout&&owner==t&&oldW==w&&oldH==h&&strcmp(strData(previous),text)==0)return;
 owner=t;oldW=w;oldH=h;gameStatusNeedsLayout=false;strAssign(previous,text);fitInfoText(t,r,.75f,0);
}
static const char*infoConsoleKey;static StationInfoLayout infoGameLayout;
static StationInfoRect infoViewport{},infoTrack{};
static StationSynopsisScroll infoScroll{};
alignas(8) static B infoSourceText[24]={};
static bool setSynopsisText(void*text,const char*raw,bool force){
 if(!raw)raw="";
 bool changed=force||strcmp(strData(infoSourceText),raw)!=0;
 if(!changed)return false;
 strAssign(infoSourceText,raw);
 // Keep every UTF-8 byte; old form-feed page separators become line breaks.
 alignas(8) B normalized[24]={};strAssign(normalized,raw);
 char*s=(char*)strData(normalized);for(char*p=s;*p;p++)if(*p=='\f')*p='\n';
 fn<void(*)(void*,const void*)>(0x2d2c70)(text,normalized);
 if(normalized[0]&1)fn<V>(0x39d820)(at<void*>(normalized,16));
 return true;
}
static void updateSystemInfo(void*p){
 static void*owner;static int oldIndex=-2,oldRevision;
 static bool wasSystems,wasFolder;static U oldMappedIndex=~(U)0,oldVisibleCount=~(U)0;static float oldW,oldH;
 float w=at<float>(p,0x54),h=at<float>(p,0x58);
 bool newOwner=owner!=p;
 if(newOwner){owner=p;infoTitle=createInfoText(p,0xF3FFF6ff);infoDescription=createInfoText(p,0xD1DED5ff);infoListCountText=createInfoText(p,0xD1DED5ff);infoPlayersText=createInfoText(p,0xD1DED5ff);infoActionCountText=createInfoText(p,0xE5F1FFff);infoActionPlayersText=createInfoText(p,0xE5F1FFff);infoRatingUnknown=createInfoText(p,0xD1DED5ff);setLongText(infoRatingUnknown,"SEM NOTA");oldIndex=-2;}
 int cursor=at<int>(p,0xf0),rev=at<int>(systemsMode?(folderMode?(void*)folderFacade:(void*)facade):fn<void*(*)()>(0x1887dc)(),0xa8);
 U*visible=at<U*>(p,0xf8),*end=at<U*>(p,0x100);
 U visibleCount=visible&&end&&end>=visible?(U)(end-visible):0;
 bool valid=visible&&end&&cursor>=0&&(U)cursor<(U)(end-visible);
 U index=valid?visible[cursor]:~(U)0;
 bool selectionChanged=newOwner||oldIndex!=cursor||oldMappedIndex!=index||wasSystems!=systemsMode||wasFolder!=folderMode;
 bool changed=selectionChanged||oldVisibleCount!=visibleCount||oldRevision!=rev||oldW!=w||oldH!=h;
 if(!changed)return;
 if(selectionChanged)infoStarsStarted=fn<unsigned(*)()>(0x39e240)();
 oldVisibleCount=visibleCount;oldIndex=cursor;oldMappedIndex=index;wasSystems=systemsMode;wasFolder=folderMode;oldRevision=rev;oldW=w;oldH=h;
 infoConsoleKey=nullptr;infoDetails=nullptr;infoGameDetailsVisible=false;infoDetailsLayout={};infoTitleViewport={};infoPlayersViewport={};infoListCountViewport={};infoHeaderStars={};
 fn<void(*)(void*,bool)>(0x277228)(infoTitle,valid);fn<void(*)(void*,bool)>(0x277228)(infoDescription,valid);
 if(!valid){infoViewport={};infoTrack={};stationSynopsisMeasure(infoScroll,0,0,true);return;}
 const char*heading="";const char*body="";const char*playerEvidenceId=nullptr;char folderText[6144]={};
 if(folderMode){
  if(index>=(U)folderCount){infoViewport={};infoTrack={};stationSynopsisMeasure(infoScroll,0,0,true);return;}
  heading=strData(folderItems+index*0xe8+0x18);describeFolder(index,folderText,sizeof(folderText));body=folderText;
  preparePlatformActionLabel(p,folderPlatform,heading);
 }else if(systemsMode){
  if(index>=(U)systemCount){infoViewport={};infoTrack={};stationSynopsisMeasure(infoScroll,0,0,true);return;}
  const char*key=strData(items+index*0xe8+0x60);const SystemInfo*info=nullptr;
  for(int i=0;i<NINFOS;i++)if(presentationKeyEqual(systemInfos[i].key,key)){info=&systemInfos[i];break;}
  heading=info?info->title:key;body=info?info->description:"";
  const char*buttonName=strData(items+index*0xe8+0x18);preparePlatformActionLabel(p,key,*buttonName?buttonName:heading);
 }else{
  void*catalog=fn<void*(*)()>(0x1887dc)();B*item=at<B*>(catalog,0x88)+index*0xe8;
  if(item>=at<B*>(catalog,0x90)){infoViewport={};infoTrack={};stationSynopsisMeasure(infoScroll,0,0,true);return;}
  const char*key=strData(item+0x60),*id=strData(item);heading=strData(item+0x18);infoConsoleKey=key;infoDetails=findStationGameDetailsForGame(id,key,heading);playerEvidenceId=id;
  const char*serverDescription=strData(item+0x30);
  const GameInfo*gameInfo=findStationGameInfo(key,id);
  if(!gameInfo&&!starts(id,"station_"))for(int i=0;i<NGAMEINFOS;i++)if(strcmp(gameInfos[i].system,key)==0&&strcmp(gameInfos[i].id,id)==0){gameInfo=&gameInfos[i];break;}
  body=stationSynopsisChoose(serverDescription,heading,gameInfo?gameInfo->name:nullptr,gameInfo?gameInfo->pages:nullptr,gameInfo&&stationSynopsisNeedsOverride(id,serverDescription),"Sinopse ainda não localizada para esta edição.").text;
 }
 setLongText(infoTitle,heading);bool bodyChanged=setSynopsisText(infoDescription,body,newOwner);
 float titleY=systemsMode?.873f:.851f,left=contentLeft(p),width=w*.965f-left;
 infoGameLayout=stationInfoLayout(w,h,left,!systemsMode&&infoConsoleKey&&stationConsoleAvailable(infoConsoleKey),systemsMode);
 infoViewport=infoGameLayout.text;infoTrack={};
 place(infoTitle,left,h*titleY,width,h*.045f,systemsMode?1.05f:.96f,0);
 infoGameDetailsVisible=!systemsMode&&!folderMode;
 if(infoGameDetailsVisible){
  char players[96]={};stationVerifiedPlayers(playerEvidenceId,players,sizeof(players));
  infoSinglePlayer=strcmp(players,"1")==0;
  char countLabel[48]={},digits[24]={};U remainingCount=visibleCount;int length=0;
  do{digits[length++]=(char)('0'+remainingCount%10);remainingCount/=10;}while(remainingCount&&length<23);
  for(int i=0;i<length;i++)countLabel[i]=digits[length-i-1];
  memcpy(infoActionCount,countLabel,strlen(countLabel)+1);memcpy(infoActionPlayers,players,strlen(players)+1);
  // Title alone above the synopsis; counts remain in the primary action.
  infoTitleViewport={left,infoViewport.y,w*.965f-left,h*.076f};
  fitGameTitleOneLine(infoTitle,heading,infoTitleViewport);
  float advance=h*.093f;infoViewport.y+=advance;infoViewport.h-=advance;
  if(infoGameLayout.photo){infoGameLayout.console.y+=advance;infoGameLayout.console.h-=advance;}
  infoGameLayout.text=infoViewport;
  infoPlayersViewport={};infoListCountViewport={};
  infoGameDetailsVisible=infoTitleViewport.w>0&&infoViewport.h>0;
 }

 fn<void(*)(void*,bool)>(0x277228)(infoListCountText,false);
 fn<void(*)(void*,bool)>(0x277228)(infoPlayersText,false);
 // Height zero enables the original TextComponent's auto-height/wrapped layout.
 constexpr float scale=.82f;
 place(infoDescription,infoViewport.x,infoViewport.y,infoViewport.w,0,scale,0);
 fn<void(*)(void*,int)>(0x2d38e8)(infoDescription,3);
 float contentHeight=at<float>(infoDescription,0x58)*scale;
 if(contentHeight>infoViewport.h){
  float gutter=w*.014f;if(gutter<20.f)gutter=20.f;
  if(gutter<infoViewport.w*.2f){
   infoTrack={infoViewport.x+infoViewport.w-gutter,infoViewport.y,gutter,infoViewport.h};
   place(infoDescription,infoViewport.x,infoViewport.y,infoViewport.w-gutter,0,scale,0);
   fn<void(*)(void*,int)>(0x2d38e8)(infoDescription,3);
   contentHeight=at<float>(infoDescription,0x58)*scale;
  }
 }
 stationSynopsisMeasure(infoScroll,infoViewport.h,contentHeight,selectionChanged||bodyChanged);
 __android_log_print(4,"TurboCarousel","SYNOPSIS mode=%s content=%.1f viewport=%.1f scroll=%d bytes=%lu",folderMode?"collections":systemsMode?"platforms":"games",contentHeight,infoViewport.h,contentHeight>infoViewport.h,strlen(body));
}
static bool isSystemInfoText(void*t){return t==infoActionCountText||t==infoActionPlayersText||t==infoRatingUnknown||t==infoTitle||t==infoDescription||t==infoPlayersText||t==infoListCountText;}
static void clipSynopsis(const StationInfoRect&r){
 Vec3 local[4]={{r.x,r.y,0},{r.x+r.w,r.y,0},{r.x,r.y+r.h,0},{r.x+r.w,r.y+r.h,0}};
 Vec3 point=fn<Vec3(*)(const void*,const void*)>(0x2e1274)(&formationMatrix,local);
 float l=point.x,t=point.y,right=l,bottom=t;
 for(int i=1;i<4;i++){point=fn<Vec3(*)(const void*,const void*)>(0x2e1274)(&formationMatrix,local+i);if(point.x<l)l=point.x;if(point.y<t)t=point.y;if(point.x>right)right=point.x;if(point.y>bottom)bottom=point.y;}
 Int2 pos{(int)l,(int)t};if(pos.x>l)pos.x--;if(pos.y>t)pos.y--;
 int x2=(int)right,y2=(int)bottom;if(x2<right)x2++;if(y2<bottom)y2++;
 Int2 size{x2-pos.x,y2-pos.y};fn<void(*)(const Int2*,const Int2*)>(0x2e2800)(&pos,&size);
}
static void drawDetailsPerson(float x,float y,float size,unsigned color){
 ActionIconMesh m(color);
 static const float circle[][2]={{.5f,0},{.854f,.146f},{1,.5f},{.854f,.854f},{.5f,1},{.146f,.854f},{0,.5f},{.146f,.146f}};
 float head=size*.27f,center=x+size*.5f;
 for(int i=0;i<8;i++){auto a=circle[i];auto b=circle[(i+1)%8];m.quad(center,y+head*.5f,center-head*.5f+a[0]*head,y+a[1]*head,center-head*.5f+b[0]*head,y+b[1]*head,center-head*.5f+b[0]*head,y+b[1]*head);}
 float t=size*.065f;
 m.line(x+size*.20f,y+size*.85f,x+size*.23f,y+size*.53f,t);m.line(x+size*.23f,y+size*.53f,center,y+size*.40f,t);m.line(center,y+size*.40f,x+size*.77f,y+size*.53f,t);m.line(x+size*.77f,y+size*.53f,x+size*.80f,y+size*.85f,t);m.line(x+size*.20f,y+size*.85f,x+size*.80f,y+size*.85f,t);m.flush();
}
static void drawDetailsStar(const StationInfoRect&r,unsigned color){
 static const float points[][2]={{.5f,0},{.612f,.345f},{.976f,.345f},{.682f,.559f},{.794f,.905f},{.5f,.691f},{.206f,.905f},{.318f,.559f},{.024f,.345f},{.388f,.345f}};
 ActionIconMesh m(color);
 for(int i=0;i<10;i++){auto a=points[i];auto b=points[(i+1)%10];m.quad(r.x+r.w*.5f,r.y+r.h*.5f,r.x+a[0]*r.w,r.y+a[1]*r.h,r.x+b[0]*r.w,r.y+b[1]*r.h,r.x+b[0]*r.w,r.y+b[1]*r.h);}
 m.flush();
}
// The existing primary action label owns its text; fit stars beside it without changing the action.
static StationInfoRect infoPlayStars{};
static void layoutPrimaryGameLabel(void*t){
 updateSystemInfo(gui);if(!infoGameDetailsVisible){infoPlayStars={};return;}
 float w=at<float>(gui,0x54),h=at<float>(gui,0x58);auto r=stationBottomGameAction(w,h,0);
 static void*owner;static float oldW,oldH;alignas(8) static B previous[24]={};
 static char count[48]={},players[96]={};
 const char*label=strData((B*)t+0xd0);
 if(owner==t&&oldW==w&&oldH==h&&strcmp(strData(previous),label)==0&&strcmp(count,infoActionCount)==0&&strcmp(players,infoActionPlayers)==0&&!gameStatusNeedsLayout)return;
 owner=t;oldW=w;oldH=h;gameStatusNeedsLayout=false;strAssign(previous,label);
 memcpy(count,infoActionCount,strlen(infoActionCount)+1);memcpy(players,infoActionPlayers,strlen(infoActionPlayers)+1);
 constexpr float metadataScale=.56f;
 float labelWidth=nativeInfoTextWidth(t,strData(previous),gameActionTextScale);
 float countWidth=nativeInfoTextWidth(infoActionCountText,count,metadataScale);
 float playersWidth=nativeInfoTextWidth(infoActionPlayersText,players,metadataScale);
 float threeSpaces=nativeInfoTextWidth(t,"   ",gameActionTextScale);
 // Icon badge ends at .79h. Text follows by three real font spaces.
 infoPrimaryMeta=stationPrimaryMetaLayout(r,r.h*.79f+threeSpaces,labelWidth,countWidth,playersWidth,threeSpaces);
 auto layout=infoPrimaryMeta;
 fitGameTitleOneLine(t,strData(previous),{layout.label.x,layout.label.y,layout.label.w+.1f,layout.label.h},gameActionTextScale*layout.scale);
 fitGameTitleOneLine(infoActionCountText,count,{layout.count.x,layout.count.y,layout.count.w+.1f,layout.count.h},metadataScale*layout.scale);
 fitGameTitleOneLine(infoActionPlayersText,players,{layout.players.x,layout.players.y,layout.players.w+.1f,layout.players.h},metadataScale*layout.scale);
 infoPlayStars={layout.stars.x,layout.stars.y,layout.stars.w,layout.stars.h};
 strAssign(previous,strData((B*)t+0xd0));
}
// Draw after the native primary button fill, once per visible frame.
static void drawPlayButtonStars(void*p){
 if(systemsMode||folderMode||modal(p)||at<int>(p,0x370)<2||!infoGameDetailsVisible||infoPlayStars.w<=0)return;
 fn<void(*)(const void*)>(0x2e5640)(&formationMatrix);fn<void(*)(unsigned)>(0x2e52e8)(0);
 auto folder=infoPrimaryMeta.folder;drawActionIcon(folder.x,folder.y,folder.h,6,0x62F49Bff);
 auto person=infoPrimaryMeta.person;
 if(infoSinglePlayer)drawDetailsPerson(person.x,person.y,person.h,0x62F49Bff);
 else{drawDetailsPerson(person.x+person.w*.22f,person.y,person.h*.88f,0xA6DAB9ff);drawDetailsPerson(person.x,person.y+person.h*.18f,person.h*.76f,0x62F49Bff);}
 fn<void(*)(void*,void*)>(0x2d2dc4)(infoActionCountText,&formationMatrix);
 fn<void(*)(void*,void*)>(0x2d2dc4)(infoActionPlayersText,&formationMatrix);
 fn<void(*)(const void*)>(0x2e5640)(&formationMatrix);fn<void(*)(unsigned)>(0x2e52e8)(0);
 auto row=infoPlayStars;float size=row.h,step=size*1.15f;
 if(!infoDetails||infoDetails->ratingThousandths<0){static void*labelOwner;static float x=-1,y=-1,width=-1,height=-1;if(labelOwner!=infoRatingUnknown||row.x!=x||row.y!=y||row.w!=width||row.h!=height){x=row.x;y=row.y;width=row.w;height=row.h;labelOwner=infoRatingUnknown;fitInfoText(infoRatingUnknown,row,.38f,0);}fn<void(*)(void*,void*)>(0x2d2dc4)(infoRatingUnknown,&formationMatrix);return;}
 float rating=infoDetails&&infoDetails->ratingThousandths>=0?infoDetails->ratingThousandths/200.f:-1;
 unsigned frame=stationRatingLoopFrame(fn<unsigned(*)()>(0x39e240)()-infoStarsStarted);
 for(int i=0;i<5;i++){
  StationInfoRect star{row.x+i*step,row.y+(row.h-size)*.5f,size,size};
  drawDetailsStar(star,0x82948966u);
  float fill=rating-i;if(fill>1)fill=1;
  if(fill>0){
   clipSynopsis({star.x,star.y,star.w*fill,star.h});
   // Original Favourite art is centered in half its canvas; fit the star to this glyph.
   // The fixed fill beneath the animation keeps fractional ratings readable in every frame.
   drawDetailsStar(star,0xffbe32ffu);
   drawStationLottie(1,frame,{star.x-star.w*.5f,star.y-star.h*.5f,star.w*2.f,star.h*2.f});fn<void(*)()>(0x2e2aac)();
  }
 }
}
static void drawSynopsisScrollbar(){
 if(infoTrack.w<=0||infoScroll.contentHeight<=infoScroll.viewportHeight)return;
 auto thumb=stationSynopsisThumb(infoScroll,infoTrack);
 float line=infoTrack.w*.16f;if(line<2)line=2;
 float x=infoTrack.x+(infoTrack.w-line)*.5f;
 rect(x,infoTrack.y,line,infoTrack.h,0x8BC7A038u);
 float thumbWidth=line*1.8f;
 rect(infoTrack.x+(infoTrack.w-thumbWidth)*.5f,thumb.y,thumbWidth,thumb.h,infoScroll.dragging?0xB4FFD0ffu:0x53DD8Cddu);
}
static void drawSystemInfoLayer(void*p){
 updateSystemInfo(p);drawSystemInfoPanel(p);
 if(infoTitle&&!systemsMode&&infoTitleViewport.w>0){if(infoTitleViewport.w>0)clipSynopsis(infoTitleViewport);fn<void(*)(void*,void*)>(0x2d2dc4)(infoTitle,&formationMatrix);if(infoTitleViewport.w>0)fn<void(*)()>(0x2e2aac)();}
 if(infoDescription&&infoViewport.w>0&&infoViewport.h>0){
  fn<void(*)(void*,float,float,float)>(0x277194)(infoDescription,infoViewport.x,infoViewport.y-infoScroll.offset,0);
  clipSynopsis(infoViewport);
  fn<void(*)(void*,void*)>(0x2d2dc4)(infoDescription,&formationMatrix);
  fn<void(*)()>(0x2e2aac)();
 }
 fn<void(*)(const void*)>(0x2e5640)(&formationMatrix);
 if(!modal(p)){drawSynopsisScrollbar();}
 fn<void(*)(const void*)>(0x2e5640)(&formationMatrix);
 if(!systemsMode&&!folderMode&&infoConsoleKey&&infoGameLayout.photo)drawStationConsole(p,infoConsoleKey,infoGameLayout.console);
}
static bool touchSynopsis(void*p,const void*event){
 bool enabled=!modal(p)&&!at<B>(p,0x16d0)&&!at<B>(p,0x1d99)&&at<int>(p,0x370)>=2&&infoViewport.w>0&&infoViewport.h>0;
 if(at<int>((void*)event,0)==0&&hitCover(at<float>((void*)event,0x10),at<float>((void*)event,0x14))>=0)return false;
 return stationSynopsisTouch(infoScroll,at<int>((void*)event,0),at<U>((void*)event,8),at<float>((void*)event,0x10),at<float>((void*)event,0x14),infoViewport,infoTrack,enabled);
}
