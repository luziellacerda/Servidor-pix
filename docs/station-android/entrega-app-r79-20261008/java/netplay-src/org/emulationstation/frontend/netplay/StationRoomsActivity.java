package org.emulationstation.frontend.netplay;
import android.app.*;
import android.os.*;
import android.content.*;
import org.emulationstation.frontend.auth.StationTaskNavigation;
import org.emulationstation.frontend.auth.StationTaskPolicy;
import android.content.res.ColorStateList;
import android.graphics.Typeface;
import android.graphics.drawable.*;
import android.view.*;
import android.widget.*;
import org.emulationstation.frontend.station.*;
import org.json.*;
import java.net.*;
import java.util.*;
import java.util.concurrent.*;

/** Real signed Station room snapshots. No public lobby, simulated peers or polling on the UI thread. */
public final class StationRoomsActivity extends Activity {
    private final ExecutorService commands=Executors.newSingleThreadExecutor();
    private final ExecutorService events=Executors.newSingleThreadExecutor();
    private final StationRoomState state=new StationRoomState();
    private final StationRoomFeedback feedback=new StationRoomFeedback();
    private final Set<StationApi.Cancellation> requests=Collections.synchronizedSet(new HashSet<>());
    private volatile boolean active;private boolean busy,launching;private volatile int epoch,page;
    private Intent pendingNavigation;
    private final StationGameSession.LaunchReturn nativeReturn=new StationGameSession.LaunchReturn();
    private String selectedItem,launchKey="";private boolean returningFromGame,returningRecovery;private volatile StationOnlineClient client;private StationOnlineGame prepared;
    private TextView status,title,subtitle,connection,peerTitle,roomTitle,chatTitle;private EditText nickname,chat;
    private ScrollView peerScroll,roomScroll,chatScroll;private LinearLayout leftPanel,middlePanel,rightPanel;private boolean narrow;private ImageView heroCover;private StationLobbyIcon heroSymbol;private TextView gamePlatform;private android.graphics.Bitmap heroBitmap;private int paintedMessages;private String paintedRoom="";
    private EditText playerSearch;private StationRoomArtwork artwork;private boolean creating;private Button choose;private TextView createMessage;
    private int section;private String conversationPeer="",conversationName="",lastMessageId="";
    private LinearLayout createPanel,chatActions;private StationCreateGameCard createCard, ownRoomCard;private String ownRoomCoverItem="";private TextView createGame,chatHint,createInfo;private Button navRooms,navPeople,navCreate,navChat,inviteNavigation;
    private final java.util.Map<String,String> drafts=new java.util.HashMap<>();
    private final java.util.Set<String> seenMessages=new java.util.LinkedHashSet<>();private String pendingPeer="",pendingName="";private TextView inboxNotice;
    private StationPlayerSheet playerSheet;private Dialog codeDialog;
    private final StationRoomRoster roster = new StationRoomRoster();
    private LinearLayout confirmationPlayers;private Button confirmStart;private TextView roomReadiness;
    private String inspectedPeer="",inspectedName="",peerFeedback="";
    private LinearLayout peers,rooms,messages;private Button create,retry,ready,start,leave,send;
    private final ScheduledExecutorService multiplayerEvents=Executors.newSingleThreadScheduledExecutor();
    private ScheduledFuture<?> multiplayerPolling;
    private Future<?> polling;private long painted=-1;private int paintedPage=-1;
    private StationHyperspaceView hyperspace;private StationGameRatingView headerRating;private TextView headerPlayers;private JSONObject headerMetadata;private String heroRequestId="";private int heroEpoch=-1;private String profileRequestId="";private int profileEpoch=-1;
    private final int green=0xff56dea2,white=0xffedf2f4,muted=0xff9aaab2;
    @Override public void onCreate(Bundle saved){super.onCreate(saved);nativeReturn.restore(saved==null?null:saved.getBundle("station.nativeReturn"));StationTaskNavigation.arrived(this);selectedItem=getIntent().getStringExtra("station.itemId");returningFromGame=saved!=null&&saved.getBoolean("returning");returningRecovery=saved!=null&&saved.getBoolean("returningRecovery");artwork=new StationRoomArtwork(this);build();}
    @Override protected void onNewIntent(Intent intent){
        super.onNewIntent(intent);setIntent(intent);StationTaskNavigation.arrived(this);
        pendingNavigation=intent;if(active)applyPendingNavigation(state.get());
    }
    private void applyPendingNavigation(JSONObject snapshot){
        Intent incoming=pendingNavigation;if(incoming==null||snapshot==null||launching||returningFromGame)return;
        pendingNavigation=null;
        String item=incoming.getStringExtra("station.itemId");
        if(item!=null&&StationTaskPolicy.canSelectItem(true,snapshot.optJSONObject("room")!=null,launching,returningFromGame)&&!item.equals(selectedItem)){
            selectedItem=item;prepared=null;roomLayout(false);loadHero();
        }
        String peer=incoming.getStringExtra("station.peerId");
        if(peer!=null&&!peer.isEmpty()){openConversation(peer,roster.resolveName(snapshot,peer));}
    }
    private void stopMultiplayerPoll(){if(multiplayerPolling!=null){multiplayerPolling.cancel(true);multiplayerPolling=null;}}
    @Override public void onSaveInstanceState(Bundle out){out.putBoolean("returning",returningFromGame);out.putBoolean("returningRecovery",returningRecovery);out.putBundle("station.nativeReturn",nativeReturn.save());super.onSaveInstanceState(out);}
    @Override protected void onActivityResult(int requestCode,int resultCode,Intent data){
        super.onActivityResult(requestCode,resultCode,data);
        // RESULT_CANCELED also covers a native-process crash before Service ATTACH.
        // The exact recorded launch is cleaned locally; result codes never authorize Leave.
        if(nativeReturn.completed(requestCode))android.util.Log.i("StationRooms","game stage=native-activity-returned");
    }
    private int dp(float x){return(int)(getResources().getDisplayMetrics().density*x+.5f);}
    private GradientDrawable bg(int color,int rim){GradientDrawable d=new GradientDrawable();d.setColor(color);d.setCornerRadius(dp(14));d.setStroke(dp(1),rim);return d;}
    private LinearLayout vertical(){LinearLayout v=new LinearLayout(this);v.setOrientation(1);return v;}
    private LinearLayout row(){LinearLayout v=new LinearLayout(this);v.setGravity(Gravity.CENTER_VERTICAL);return v;}
    private TextView label(String value,int size,int color){TextView t=new TextView(this);t.setText(value);t.setTextSize(size);t.setTextColor(color);t.setFontFeatureSettings("kern");t.setIncludeFontPadding(false);return t;}
    private TextView text(LinearLayout parent,String value,int size,int color){TextView t=label(value,size,color);t.setPadding(0,dp(5),0,dp(5));parent.addView(t,new LinearLayout.LayoutParams(-1,-2));return t;}
    private void bold(TextView t){t.setTypeface(Typeface.create("sans-serif-medium",Typeface.NORMAL));}
    private Button button(LinearLayout parent,String value,Runnable action,boolean primary){
        StationActionButton b=new StationActionButton(this,value,primary);b.compactAppearance();LinearLayout.LayoutParams lp=new LinearLayout.LayoutParams(-1,dp(48));parent.addView(b,lp);b.setOnClickListener(v->action.run());return b;
    }
    private Button navButton(LinearLayout parent,String value,Runnable action){
        StationActionButton b=new StationActionButton(this,value,false);b.compactAppearance();b.setTextSize(12);styleNavigation(b,false);
        LinearLayout.LayoutParams lp=new LinearLayout.LayoutParams(-1,dp(48));parent.addView(b,lp);b.setOnClickListener(v->action.run());return b;
    }
    private Button inline(LinearLayout parent,String value,int width,Runnable action,boolean primary){
        LinearLayout holder=vertical();LinearLayout.LayoutParams lp=new LinearLayout.LayoutParams(dp(Math.max(48,width)),-2);lp.setMargins(dp(8),0,0,0);parent.addView(holder,lp);return button(holder,value,action,primary);
    }
    private TextView pill(String value,int color){TextView t=label(value,10,color);bold(t);t.setSingleLine(true);t.setGravity(Gravity.CENTER);t.setPadding(dp(9),dp(5),dp(9),dp(5));t.setBackground(bg(0xff14241c,0xff294634));return t;}
    private LinearLayout card(LinearLayout parent){LinearLayout c=vertical();c.setPadding(dp(12),dp(9),dp(12),dp(9));c.setBackground(bg(0xff152128,0xff2c3c42));LinearLayout.LayoutParams lp=new LinearLayout.LayoutParams(-1,-2);lp.setMargins(0,0,0,dp(8));parent.addView(c,lp);return c;}
    private LinearLayout panel(LinearLayout parent,float weight,boolean narrow){LinearLayout v=vertical();v.setPadding(dp(12),dp(12),dp(12),dp(10));v.setBackground(bg(0xb3121c21,0x7026343c));LinearLayout.LayoutParams lp=narrow?new LinearLayout.LayoutParams(-1,dp(280)):new LinearLayout.LayoutParams(0,-1,weight);lp.setMargins(0,0,narrow?0:dp(10),narrow?dp(10):0);parent.addView(v,lp);return v;}
    private ScrollView scroll(LinearLayout parent,LinearLayout content){ScrollView s=new ScrollView(this);s.setFillViewport(true);s.setClipToPadding(false);s.setPadding(0,dp(10),0,0);s.setVerticalScrollBarEnabled(false);s.addView(content,new ScrollView.LayoutParams(-1,-2));parent.addView(s,new LinearLayout.LayoutParams(-1,0,1));return s;}
    private void empty(LinearLayout parent,String icon,String heading,String detail){
        LinearLayout box=vertical();box.setGravity(Gravity.CENTER);box.setPadding(dp(10),dp(10),dp(10),dp(10));parent.addView(box,new LinearLayout.LayoutParams(-1,-1));
        View mark=parent==rooms||parent==messages?new StationLottieIllustration(this,parent==messages?"dialog":"online-robot"):new StationLobbyIcon(this,1);box.addView(mark,new LinearLayout.LayoutParams(dp(parent==peers?72:112),dp(parent==peers?72:112)));
        TextView h=text(box,heading,14,white);h.setGravity(Gravity.CENTER);h.setPadding(0,dp(13),0,dp(6));bold(h);
        TextView d=text(box,detail,12,muted);d.setGravity(Gravity.CENTER);d.setLineSpacing(dp(2),1);
    }
    private void field(EditText field,String hint){field.setTextSize(13);field.setTextColor(white);field.setHintTextColor(muted);field.setPadding(dp(12),dp(8),dp(12),dp(8));field.setBackground(bg(0xff08130d,0xff2b4935));field.setHint(hint);}
    private void setConnection(String value,boolean online){connection.setText((online?"●  ":"○  ")+value);connection.setTextColor(online?green:muted);}
    private void composer(boolean enabled){chat.setEnabled(enabled);send.setEnabled(enabled);chat.setHint(enabled?"Escreva para sua sala…":"Entre em uma sala");}
    private void loadingLobby(){
        closeSheets();
        setConnection("Conectando",false);peerTitle.setText("JOGADORES");roomTitle.setText("SALAS DISPONÍVEIS");peers.removeAllViews();rooms.removeAllViews();messages.removeAllViews();
        empty(peers,"…","Buscando jogadores","A lista aparece assim que a conexão for confirmada.");empty(rooms,"…","Abrindo o lobby","Preparando suas salas e seus convites.");empty(messages,"↗","Conversa da sala","Entre em uma partida para conversar com outro jogador.");composer(false);roomLayout(false);
    }
    private void unavailableLobby(){
        setConnection("Indisponível",false);peers.removeAllViews();rooms.removeAllViews();
        empty(peers,"○","Conexão pendente","Os jogadores aparecem quando o serviço estiver disponível.");empty(rooms,"!","Salas indisponíveis","Não foi possível conectar agora. Use Reconectar para tentar novamente.");composer(false);
    }
    private void build(){
        getWindow().setStatusBarColor(0xff0b1115);getWindow().setNavigationBarColor(0xff0b1115);
        getWindow().getDecorView().setSystemUiVisibility(View.SYSTEM_UI_FLAG_FULLSCREEN|View.SYSTEM_UI_FLAG_HIDE_NAVIGATION|View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY);
        getWindow().setSoftInputMode(WindowManager.LayoutParams.SOFT_INPUT_ADJUST_RESIZE);
        narrow=getResources().getConfiguration().screenWidthDp<760;
        FrameLayout stage=new FrameLayout(this);hyperspace=new StationHyperspaceView(this);
        stage.addView(hyperspace,new FrameLayout.LayoutParams(-1,-1));
        LinearLayout root=row();stage.addView(root,new FrameLayout.LayoutParams(-1,-1));setContentView(stage);
        LinearLayout sidebar=vertical();sidebar.setPadding(dp(12),dp(10),dp(12),dp(8));sidebar.setBackgroundColor(0xf010191d);
        root.addView(sidebar,new LinearLayout.LayoutParams(dp(narrow?156:184),-1));
        
        connection=text(sidebar,"○  Conectando",11,muted);connection.setPadding(0,dp(4),0,dp(6));
        ScrollView sideScroll=new ScrollView(this);sideScroll.setVerticalScrollBarEnabled(false);LinearLayout navItems=vertical();sideScroll.addView(navItems,new ScrollView.LayoutParams(-1,-2));sidebar.addView(sideScroll,new LinearLayout.LayoutParams(-1,0,1));
        navRooms=navButton(navItems,"Salas",()->navigate(0));navigationIcon(navRooms,0);
        navPeople=navButton(navItems,"Pessoas",()->navigate(1));navigationIcon(navPeople,1);
        navCreate=navButton(navItems,"Criar sala",()->navigate(2));navigationIcon(navCreate,2);
        navChat=navButton(navItems,"Conversas",()->{if(!pendingPeer.isEmpty())openConversation(pendingPeer,pendingName);else navigate(3);});navigationIcon(navChat,3);
        for(Button n:new Button[]{navRooms,navPeople,navCreate,navChat}){n.setGravity(Gravity.CENTER_VERTICAL|Gravity.LEFT);n.setTextSize(12);n.setPadding(dp(10),0,dp(6),0);}
        
        TextView signature=text(navItems,"LZ GAMES · COMUNIDADE",8,muted);signature.setLetterSpacing(.08f);signature.setPadding(dp(10),dp(4),0,dp(14));
        inviteNavigation=navButton(navItems,"Usar convite",()->enterCodeDialog());navigationIcon(inviteNavigation,4);
        retry=navButton(navItems,"Reconectar",()->connect());navigationIcon(retry,5);
        Button back=navButton(sidebar,"Voltar",()->exitRooms());navigationIcon(back,6);
        LinearLayout main=vertical();main.setPadding(dp(18),dp(10),dp(16),dp(8));root.addView(main,new LinearLayout.LayoutParams(0,-1,1));
        LinearLayout header=row();main.addView(header,new LinearLayout.LayoutParams(-1,dp(52)));
        FrameLayout art=new FrameLayout(this);header.addView(art,new LinearLayout.LayoutParams(dp(32),dp(40)));heroSymbol=new StationLobbyIcon(this,2);art.addView(heroSymbol,new FrameLayout.LayoutParams(-1,-1));heroCover=new ImageView(this);heroCover.setScaleType(ImageView.ScaleType.FIT_CENTER);art.addView(heroCover,new FrameLayout.LayoutParams(-1,-1));
        LinearLayout names=vertical();LinearLayout.LayoutParams np=new LinearLayout.LayoutParams(0,-2,1);np.setMargins(dp(12),0,dp(12),0);header.addView(names,np);
        HorizontalScrollView headingScroll=new HorizontalScrollView(this);headingScroll.setHorizontalScrollBarEnabled(false);names.addView(headingScroll,new LinearLayout.LayoutParams(-1,dp(29)));LinearLayout headline=row();headingScroll.addView(headline,new HorizontalScrollView.LayoutParams(-2,-1));
        title=label("Jogar online",19,white);bold(title);title.setSingleLine(true);headline.addView(title,new LinearLayout.LayoutParams(-2,-1));
        headerRating=new StationGameRatingView(this);LinearLayout.LayoutParams rp=new LinearLayout.LayoutParams(dp(90),dp(24));rp.setMargins(dp(12),0,dp(10),0);headline.addView(headerRating,rp);
        headerPlayers=label("—",11,muted);headerPlayers.setGravity(Gravity.CENTER_VERTICAL);headerPlayers.setSingleLine(true);headerPlayers.setCompoundDrawablesWithIntrinsicBounds(new StationSocialIcon(1,dp(17),green),null,null,null);headerPlayers.setCompoundDrawablePadding(dp(5));headline.addView(headerPlayers,new LinearLayout.LayoutParams(-2,-1));
        gamePlatform=label("",11,green);gamePlatform.setSingleLine(true);gamePlatform.setGravity(Gravity.CENTER_VERTICAL);gamePlatform.setPadding(dp(12),0,0,0);headline.addView(gamePlatform,new LinearLayout.LayoutParams(-2,-1));
        subtitle=text(names,selectedItem==null?"Escolha uma sala ou converse com alguém":"Preparando seu jogo…",11,muted);subtitle.setSingleLine(true);subtitle.setEllipsize(android.text.TextUtils.TruncateAt.END);
        nickname=new EditText(this);field(nickname,"Seu nome online");nickname.setSingleLine(true);nickname.setFilters(new android.text.InputFilter[]{new android.text.InputFilter.LengthFilter(24)});nickname.setText(getSharedPreferences("station-online-profile",0).getString("nickname","Jogador"));header.addView(nickname,new LinearLayout.LayoutParams(dp(narrow?95:135),dp(40)));
        nickname.setContentDescription("Seu nome online. Use Reconectar para atualizar.");
        LinearLayout columns=row();LinearLayout.LayoutParams cp=new LinearLayout.LayoutParams(-1,0,1);cp.topMargin=dp(10);main.addView(columns,cp);
        leftPanel=panel(columns,.44f,false);middlePanel=panel(columns,.44f,false);createPanel=panel(columns,.44f,false);createPanel.setPadding(dp(6),0,dp(6),0);rightPanel=panel(columns,.56f,false);
        peerTitle=text(leftPanel,"PESSOAS ONLINE",12,green);bold(peerTitle);
        playerSearch=new EditText(this);field(playerSearch,"Buscar nesta página");playerSearch.setSingleLine(true);playerSearch.setFilters(new android.text.InputFilter[]{new android.text.InputFilter.LengthFilter(48)});leftPanel.addView(playerSearch,new LinearLayout.LayoutParams(-1,dp(40)));
        peers=vertical();peerScroll=scroll(leftPanel,peers);peerScroll.setVerticalScrollBarEnabled(true);
        playerSearch.addTextChangedListener(new android.text.TextWatcher(){public void beforeTextChanged(CharSequence s,int a,int c,int n){}public void afterTextChanged(android.text.Editable e){}public void onTextChanged(CharSequence s,int a,int b,int c){if(state.get()!=null)renderPlayers(state.get());}});
        roomTitle=text(middlePanel,"VER SALAS",12,green);roomTitle.setVisibility(View.GONE);middlePanel.setPadding(dp(6),0,dp(6),0);rooms=vertical();roomScroll=scroll(middlePanel,rooms);roomScroll.setPadding(0,0,0,0);roomScroll.setVerticalScrollBarEnabled(true);
        ScrollView createScroll=new ScrollView(this);createScroll.setFillViewport(true);createScroll.setVerticalScrollBarEnabled(true);createPanel.addView(createScroll,new LinearLayout.LayoutParams(-1,-1));
        createCard=new StationCreateGameCard(this);createScroll.addView(createCard,new ScrollView.LayoutParams(-1,-2));
        LinearLayout createActions=createCard.actions;
        bold(text(createActions,"NOVA PARTIDA",10,green));
        createGame=text(createActions,"Jogo não selecionado",20,white);bold(createGame);
        createInfo=text(createActions,StationGamePlayerInfo.pending().detail,12,muted);createInfo.setLineSpacing(dp(2),1);
        text(createActions,"A quantidade de jogadores depende do jogo e do modo aprovado. Todos precisam da mesma edição instalada.",12,muted);
        choose=button(createActions,"Escolher jogo",()->chooseGame(),false);navigationIcon(choose,0);compactCreateAction(choose);
        create=button(createActions,"Criar sala",()->createRoom(),true);navigationIcon(create,2);compactCreateAction(create);create.setEnabled(false);createMessage=text(createActions,"",12,muted);createMessage.setVisibility(View.GONE);createMessage.setAccessibilityLiveRegion(View.ACCESSIBILITY_LIVE_REGION_POLITE);
        chatTitle=text(rightPanel,"Suas conversas",17,white);bold(chatTitle);chatHint=text(rightPanel,"Selecione uma pessoa ou entre em uma sala",11,muted);
        inboxNotice=text(rightPanel,"",12,green);inboxNotice.setVisibility(View.GONE);inboxNotice.setPadding(0,dp(8),0,dp(8));inboxNotice.setOnClickListener(v->{if(!pendingPeer.isEmpty())openConversation(pendingPeer,pendingName);});
        chatActions=row();rightPanel.addView(chatActions,new LinearLayout.LayoutParams(-1,-2));messages=vertical();chatScroll=scroll(rightPanel,messages);chatScroll.setVerticalScrollBarEnabled(true);
        LinearLayout chatbar=row();rightPanel.addView(chatbar,new LinearLayout.LayoutParams(-1,-2));chat=new EditText(this);field(chat,"Sua mensagem");chat.setMaxLines(3);chat.setInputType(android.text.InputType.TYPE_CLASS_TEXT|android.text.InputType.TYPE_TEXT_FLAG_CAP_SENTENCES);chat.setFilters(new android.text.InputFilter[]{new android.text.InputFilter.LengthFilter(500)});chat.setContentDescription("Escrever mensagem");chatbar.addView(chat,new LinearLayout.LayoutParams(0,dp(46),1));send=inline(chatbar,"",48,()->sendChat(),true);navigationIcon(send,StationSocialIcon.SEND);send.setGravity(Gravity.CENTER);send.setContentDescription("Enviar mensagem");
        status=text(main,"Conectando com sua sessão…",11,muted);status.setMaxLines(2);status.setAccessibilityLiveRegion(View.ACCESSIBILITY_LIVE_REGION_POLITE);loadingLobby();navigate(0);
        String incoming=getIntent().getStringExtra("station.peerId");if(incoming!=null){conversationPeer=incoming;conversationName="Jogador";section=3;roomLayout(false);}
    }
    private void compactCreateAction(Button button){
        ((StationActionButton)button).compactAppearance();
        LinearLayout.LayoutParams lp=(LinearLayout.LayoutParams)button.getLayoutParams();lp.height=dp(48);lp.topMargin=0;lp.bottomMargin=0;button.setLayoutParams(lp);
    }
    private void clearHeroCover(){
        heroCover.setImageDrawable(null);if(createCard!=null)createCard.setCover(null);if(ownRoomCard!=null)ownRoomCard.setCover(null);
        heroBitmap=null; // Artwork cache owns shared bitmaps; never recycle while another row displays one.
    }
    private void bindHeroCover(){
        heroCover.setImageBitmap(heroBitmap);
        if(createCard!=null)createCard.setCover(heroRequestId.equals(selectedItem)?heroBitmap:null);
        if(ownRoomCard!=null)ownRoomCard.setCover(heroRequestId.equals(ownRoomCoverItem)?heroBitmap:null);
    }
    private LinearLayout roomCoverCard(LinearLayout parent,String item){
        LinearLayout shell=card(parent);shell.setPadding(dp(8),dp(2),dp(8),dp(2));
        StationCreateGameCard display=new StationCreateGameCard(this,true);shell.addView(display,new LinearLayout.LayoutParams(-1,-2));
        display.setGameName(client.name(item));artwork.load(item,512,display::setCover);return display.actions;
    }
    private LinearLayout actionLine(LinearLayout parent){LinearLayout line=row();parent.addView(line,new LinearLayout.LayoutParams(-1,dp(48)));return line;}
    private Button roomAction(LinearLayout line,String value,int icon,int tone,Runnable action){
        StationActionButton b=new StationActionButton(this,value,tone,icon);
        LinearLayout.LayoutParams lp=new LinearLayout.LayoutParams(0,dp(48),1);lp.leftMargin=line.getChildCount()==0?0:dp(8);
        line.addView(b,lp);b.setOnClickListener(v->action.run());b.setEnabled(!busy&&!launching);return b;
    }
    private void navigationIcon(Button button,int kind){((StationActionButton)button).setActionIcon(kind);}
    @Override protected void onResume(){super.onResume();if(hyperspace!=null)hyperspace.setResumed(true);if(headerRating!=null)headerRating.setResumed(true);}
    @Override protected void onPause(){if(hyperspace!=null)hyperspace.setResumed(false);if(headerRating!=null)headerRating.setResumed(false);super.onPause();}
    private void navigate(int next){section=next;roomLayout(room()!=null);if(client!=null&&state.get()!=null){painted=-1;paint(state.get());}}
    private static final class GameChoice {final StationCatalog.Item item;GameChoice(StationCatalog.Item i){item=i;}@Override public String toString(){return item.name+"  ·  "+item.platform;}}
    private final class GameChoiceAdapter extends ArrayAdapter<GameChoice>{
        GameChoiceAdapter(java.util.List<GameChoice> choices){super(StationRoomsActivity.this,android.R.layout.simple_list_item_1,choices);}
        @Override public View getView(int position,View recycled,android.view.ViewGroup parent){
            LinearLayout line;ImageView cover;TextView name,platform;
            if(recycled instanceof LinearLayout){line=(LinearLayout)recycled;cover=(ImageView)line.getChildAt(0);LinearLayout labels=(LinearLayout)line.getChildAt(1);name=(TextView)labels.getChildAt(0);platform=(TextView)labels.getChildAt(1);}
            else{line=row();line.setPadding(dp(6),dp(3),dp(6),dp(3));cover=new ImageView(StationRoomsActivity.this);cover.setScaleType(ImageView.ScaleType.FIT_CENTER);line.addView(cover,new LinearLayout.LayoutParams(dp(38),dp(50)));LinearLayout labels=vertical();LinearLayout.LayoutParams lp=new LinearLayout.LayoutParams(0,-2,1);lp.leftMargin=dp(10);line.addView(labels,lp);name=text(labels,"",13,white);name.setMaxLines(2);name.setEllipsize(android.text.TextUtils.TruncateAt.END);platform=text(labels,"",10,muted);}
            GameChoice choice=getItem(position);if(choice==null)return line;
            final String id=choice.item.itemId;cover.setTag(id);cover.setImageDrawable(null);cover.setContentDescription("Capa de "+choice.item.name);name.setText(choice.item.name);platform.setText(choice.item.platform);
            artwork.load(id,128,bitmap->{if(id.equals(cover.getTag()))cover.setImageBitmap(bitmap);});
            return line;
        }
    }
    private void chooseGame(){
        if(client==null||state.get()==null||busy)return;
        java.util.ArrayList<StationCatalog.Item> items=new java.util.ArrayList<>();java.util.ArrayList<String> names=new java.util.ArrayList<>();
        StationCoordinator.Library lib=client.app.coordinator.current();if(lib==null)return;
        for(StationCatalog.Item item:lib.catalog.items)try{String platform=StationOnlineGame.platform(item.platform);JSONArray engines=state.get().optJSONArray("engines");for(int i=0;engines!=null&&i<engines.length();i++)if(platform.equals(engines.getJSONObject(i).optString("platform"))){items.add(item);names.add(item.name+"  ·  "+item.platform);break;}}catch(Exception unsupported){}
        if(items.isEmpty()){status.setText("Nenhum motor liberado para os jogos deste catálogo.");return;}
        LinearLayout panel=codePanel("Escolher jogo","Busque uma edição do catálogo. Baixe o jogo antes de criar sua sala.");EditText search=new EditText(this);field(search,"Buscar jogo");search.setSingleLine(true);panel.addView(search);
        ListView list=new ListView(this);java.util.ArrayList<GameChoice> choices=new java.util.ArrayList<>();for(StationCatalog.Item item:items)choices.add(new GameChoice(item));ArrayAdapter<GameChoice> adapter=new GameChoiceAdapter(choices);list.setAdapter(adapter);panel.addView(list,new LinearLayout.LayoutParams(-1,dp(170)));
        search.addTextChangedListener(new android.text.TextWatcher(){public void beforeTextChanged(CharSequence s,int a,int c,int n){}public void afterTextChanged(android.text.Editable e){}public void onTextChanged(CharSequence s,int a,int b,int c){adapter.getFilter().filter(s);}});
        list.setOnItemClickListener((parent,view,index,id)->{GameChoice chosen=adapter.getItem(index);if(chosen==null)return;selectedItem=chosen.item.itemId;createGame.setText(chosen.item.name);subtitle.setText(chosen.item.name);codeDialog.dismiss();createCard.setGameName(chosen.item.name);showCreateMessage("",false);loadHero();});displayCodePanel();
    }
    private void confirmWithCover(String heading,String message,String positive,Runnable accepted){
        confirmWithCover(heading,message,positive,accepted,currentCoverItem());
    }
    private void confirmWithCover(String heading,String message,String positive,Runnable accepted,String item){
        LinearLayout content=codePanel(heading,message,item);
        Button yes=button(content,positive,()->{codeDialog.dismiss();accepted.run();},true);compactCreateAction(yes);
        Button no=button(content,"Cancelar",()->codeDialog.dismiss(),false);compactCreateAction(no);displayCodePanel();
    }


    @Override protected void onStart(){super.onStart();artwork.start();StationPresence.foreground(this,false);active=true;if(returningFromGame)StationTaskNavigation.gameReturned(this);connect();loadHero();}
    @Override protected void onStop(){stopMultiplayerPoll();if(hyperspace!=null)hyperspace.setResumed(false);if(headerRating!=null)headerRating.setResumed(false);active=false;epoch++;artwork.stop();cancelAll();closeSheets();super.onStop();}
    @Override protected void onDestroy(){commands.shutdownNow();events.shutdownNow();clearHeroCover();artwork.close();multiplayerEvents.shutdownNow();super.onDestroy();}
    private void cancelAll(){if(polling!=null)polling.cancel(true);synchronized(requests){for(StationApi.Cancellation c:requests)c.cancel();requests.clear();}}
    private void connect(){if(!active)return;epoch++;stopMultiplayerPoll();cancelAll();busy=false;creating=false;choose.setEnabled(true);create.setText("Criar sala");feedback.begin();int generation=epoch;state.reset();roster.reset();painted=-1;prepared=null;launchKey="";launching=false;loadingLobby();
        String nick=nickname.getText().toString().trim();if(nick.isEmpty()){status.setText("Escolha seu apelido para entrar.");return;}getPreferences(0).edit().putString("nickname",nick).apply();getSharedPreferences("station-online-profile",0).edit().putString("nickname",nick).apply();
        status.setText("Consultando jogadores e salas…");create.setEnabled(false);polling=events.submit(()->{
            StationApi.Cancellation cancel=new StationApi.Cancellation();requests.add(cancel);
            try{
                client=new StationOnlineClient(this);client.page=page;
                runOnUiThread(()->{if(active&&epoch==generation){roomLayout(room()!=null);loadHero();}});
                if(returningFromGame&&!returningRecovery){try{client.call(StationOnlineClient.command("leave"),false,cancel);}catch(StationApi.Failure e){if(!e.code.equals("STATION_ONLINE_ENTER_REQUIRED"))throw e;}returningFromGame=false;}
                JSONObject current=client.call(StationOnlineClient.command("enter").put("nickname",nick),false,cancel);
                if(returningFromGame&&returningRecovery){JSONObject lost=current.optJSONObject("room");if(lost!=null&&"relay-wss-v2".equals(lost.optString("transport")))current=client.call(StationOnlineClient.command("recovery-failed").put("roomId",lost.getString("roomId")).put("generation",lost.getLong("generation")),false,cancel);returningFromGame=false;returningRecovery=false;}
                // Present the verified social snapshot before probing optional v3 support.
                deliver(current,generation);beginMultiplayerDiscovery(client,generation);
                long heartbeat=SystemClock.elapsedRealtime();
                while(active&&epoch==generation&&!Thread.currentThread().isInterrupted()){
                    JSONObject latest=state.get();if(latest!=null)current=latest;
                    if(SystemClock.elapsedRealtime()-heartbeat>=20000){current=client.call(StationOnlineClient.command("heartbeat"),false,cancel);heartbeat=SystemClock.elapsedRealtime();deliver(current,generation);}
                    try{current=client.events(current,page,cancel);deliver(current,generation);}
                    catch(StationApi.Failure e){if(!e.code.equals("STATION_ONLINE_POLL_EXISTS"))throw e;Thread.sleep(1000);cancel.check();}
                }
            }catch(Exception e){if(!cancel.cancelled()){runOnUiThread(()->{if(active&&epoch==generation)setConnection("Reconectar",false);});failure(e,generation);}}finally{requests.remove(cancel);}
        });
    }
    private void beginMultiplayerDiscovery(final StationOnlineClient connectedClient,final int generation){
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
    private void deliver(JSONObject snapshot,int generation)throws Exception {if(!active||generation!=epoch)return;if(!state.accept(snapshot))return;runOnUiThread(()->{if(active&&generation==epoch){roster.observe(snapshot);paint(snapshot);applyPendingNavigation(snapshot);}});}
    private static void recordFailure(Throwable error){
        StringBuilder detail=new StringBuilder();Throwable cause=error;
        for(int depth=0;cause!=null&&depth<3;depth++,cause=cause.getCause()){
            detail.append(" type=").append(cause.getClass().getName());
            if(cause instanceof StationApi.Failure){StationApi.Failure api=(StationApi.Failure)cause;detail.append(" http=").append(api.status);if(api.code!=null&&api.code.matches("[A-Z0-9_]{1,80}"))detail.append(" code=").append(api.code);}
            StackTraceElement[] stack=cause.getStackTrace();for(int i=0;i<Math.min(stack.length,8);i++)detail.append(" at=").append(stack[i].getClassName()).append('.').append(stack[i].getMethodName()).append(':').append(stack[i].getLineNumber());
        }
        android.util.Log.w("StationRooms",detail.toString());
    }
    private void failure(Throwable error,int generation){recordFailure(error);runOnUiThread(()->{if(active&&generation==epoch){if(creating){creating=false;choose.setEnabled(true);create.setText("Criar sala");showCreateMessage(StationOnlineClient.message(error),true);}feedback.fail(StationOnlineClient.message(error));status.setText(feedback.text(state.get(),false));if(state.get()==null)unavailableLobby();busy=false;retry.setEnabled(true);create.setEnabled(state.get()!=null&&room()==null&&!launching);peerFeedback=StationOnlineClient.message(error);if(state.get()!=null)renderConversation(state.get());refreshPlayerSheet();}});}
    interface Work {JSONObject run(StationApi.Cancellation cancel)throws Exception;}
    private void action(Work work){action(work,null);}
    private void action(Work work,Runnable completed){if(busy||!active||client==null||state.get()==null)return;busy=true;feedback.begin();create.setEnabled(false);refreshPlayerSheet();int generation=epoch;StationApi.Cancellation cancel=new StationApi.Cancellation();requests.add(cancel);
        commands.execute(()->{try{JSONObject response=work.run(cancel);deliver(response,generation);runOnUiThread(()->{if(active&&generation==epoch){busy=false;status.setText(feedback.text(state.get(),false));updateStartState();create.setEnabled(room()==null&&!launching);if(completed!=null)completed.run();painted=-1;if(state.get()!=null)paint(state.get());refreshPlayerSheet();}});}catch(Exception e){if(!cancel.cancelled())failure(e,generation);}finally{requests.remove(cancel);}});
    }
    private JSONObject room(){JSONObject snapshot=state.get();return snapshot==null?null:snapshot.optJSONObject("room");}
    private void showCreateMessage(String message,boolean error){
        createMessage.setText(message);createMessage.setTextColor(error?0xfff4b0a2:green);createMessage.setVisibility(message.isEmpty()?View.GONE:View.VISIBLE);
    }
    private static StationGamePlayerInfo profileInfo(StationMultiplayerProfile p){return StationGamePlayerInfo.fromVerifiedProfile(true,p.maximumPlayers,p.allowed,p.mode,p.controllerProfile);}
    private static StationGamePlayerInfo roomGameInfo(JSONObject r){
        if(r==null||!"station-stream.v3".equals(r.optString("recoveryProtocol")))return StationGamePlayerInfo.pending();
        JSONArray a=r.optJSONArray("allowedPlayerCounts");int[] counts=new int[a==null?0:a.length()];for(int i=0;i<counts.length;i++)counts[i]=a.optInt(i);
        return StationGamePlayerInfo.fromVerifiedProfile(true,r.optInt("maximumPlayers"),counts,r.optString("mode"),r.optString("controllerProfile"));
    }
    private static StationGamePlayerInfo.RoomInfo roomInfo(JSONObject r,String self){
        StationGamePlayerInfo info=roomGameInfo(r);JSONArray a=r==null?null:r.optJSONArray("roster");StationGamePlayerInfo.Participant[] people=new StationGamePlayerInfo.Participant[a==null?0:a.length()];
        for(int i=0;i<people.length;i++){JSONObject p=a.optJSONObject(i);if(p!=null)people[i]=new StationGamePlayerInfo.Participant(p.optInt("slot"),p.optString("peerId"),p.optString("nickname"),p.optBoolean("ready"),self.equals(p.optString("peerId")));}
        return info.forRoom(r==null?0:r.optInt("capacity"),r!=null&&"waiting".equals(r.optString("state")),people);
    }
    private String roomExplanation(JSONObject r){
        StationGamePlayerInfo.RoomInfo info=roomInfo(r,state.get()==null?"":state.get().optString("selfId"));
        StringBuilder text=new StringBuilder(info.detail);if(!info.confirmed)return text.toString();
        text.append("\n\n");for(StationGamePlayerInfo.Position p:info.positions){text.append(p.label).append(" · ").append(p.name).append(p.self?" (você)":"").append("\n");}
        if("battle-single".equals(r.optString("mode"))&&"snes-multitap-port2-v1".equals(r.optString("controllerProfile")))text.append("\nNo jogo, escolha BATTLE GAME → Single Match e marque MAN nos controles dos participantes. O modo Normal é individual.");
        text.append("\nTodos precisam da mesma edição do jogo. Cada participante controla apenas sua posição e pode usar sua própria rede.");return text.toString();
    }
    private void createRoom(){
        if(busy||launching||!active||client==null||state.get()==null)return;
        if(selectedItem==null){showCreateMessage("Escolha um jogo antes de criar a sala.",true);return;}
        if(room()!=null){showCreateMessage("Saia da sala atual antes de criar outra.",true);return;}
        final String item=selectedItem;final java.util.List<StationMultiplayerProfile> modes=new ArrayList<>();
        action(cancel->{modes.addAll(StationOnlineGame.profiles(this,client,item,cancel));if(modes.isEmpty())throw StationMultiplayerProfile.unavailable();return client.compose(null);},()->{
            if(!item.equals(selectedItem)||!active)return;
            if(modes.size()==1){prepareRoom(modes.get(0).profileId);return;}
            String[] labels=new String[modes.size()];for(int i=0;i<labels.length;i++)labels[i]=profileInfo(modes.get(i)).summary;
            new AlertDialog.Builder(this).setTitle("Modo da partida").setItems(labels,(d,i)->prepareRoom(modes.get(i).profileId)).setNegativeButton("Cancelar",null).show();
        });
    }
    private void prepareRoom(String profileId){
        if(busy||launching||!active||client==null||state.get()==null)return;
        if(selectedItem==null){showCreateMessage("Escolha um jogo antes de criar a sala.",true);return;}
        if(room()!=null){showCreateMessage("Saia da sala atual antes de criar outra.",true);return;}
        final String requested=selectedItem;creating=true;choose.setEnabled(false);create.setText("Conferindo…");prepared=null;
        showCreateMessage("Conferindo a edição, o modo e os jogadores permitidos…",false);
        action(cancel->{prepared=StationOnlineGame.prepare(this,client,requested,state.get(),cancel,profileId);return client.compose(null);},()->{
            creating=false;choose.setEnabled(true);create.setText("Criar sala");
            StationOnlineGame game=prepared;if(game==null||!requested.equals(selectedItem))return;
            int[] counts=game.multiplayerProfile.allowed;
            if(counts.length==1){createVerifiedRoom(game,counts[0]);return;}
            String[] choices=new String[counts.length];for(int i=0;i<counts.length;i++)choices[i]=counts[i]+" vagas";
            final int[] chosen={counts.length-1};
            new AlertDialog.Builder(this).setTitle(profileInfo(game.multiplayerProfile).modeLabel+" · vagas").setSingleChoiceItems(choices,chosen[0],(d,index)->chosen[0]=index)
                .setNegativeButton("Cancelar",null).setPositiveButton("Criar sala",(d,index)->createVerifiedRoom(game,counts[chosen[0]])).show();
        });
    }
    private void createVerifiedRoom(StationOnlineGame game,int capacity){
        if(busy||launching||!active||!game.itemId.equals(selectedItem)||!game.multiplayerProfile.permits(capacity))return;
        creating=true;choose.setEnabled(false);create.setText("Criando…");showCreateMessage("Confirmando sua sala…",false);
        action(cancel->{JSONObject response=client.call(game.fields(StationOnlineClient.command("create")).put("capacity",capacity),false,cancel);
            if(!StationRoomCreation.confirmed(response,game.itemId))throw new StationOnlineGame.Unavailable("A criação da sala não foi confirmada. Reconecte para conferir.");
            game.verifyRoom(response.getJSONObject("room"));prepared=game;return response;
        },()->{creating=false;choose.setEnabled(true);create.setText("Criar sala");conversationPeer="";showCreateMessage("",false);navigate(0);});
    }
    private void join(String roomId,String itemId){
        if(busy||launching)return;JSONObject snapshot=state.get(),mine=snapshot==null?null:snapshot.optJSONObject("room");
        JSONObject target=findRoom(snapshot,roomId);if(target==null){status.setText("A sala mudou. Atualize a lista antes de entrar.");return;}
        if(mine==null){confirmWithCover("Entrar nesta sala?",roomExplanation(target),"Aceitar e entrar",()->joinConfirmed(roomId,itemId,null,target.optLong("generation"),target.optString("profileId")),itemId);return;}
        if(roomId.equals(mine.optString("roomId"))){status.setText("Você já está nesta sala. Marque Estou pronto para confirmar sua participação.");return;}
        if(!StationPlayerModel.soloWaitingRoom(snapshot)){status.setText("Saia da sala atual antes de entrar em outra.");return;}
        final String previous=mine.optString("roomId");
        confirmWithCover("Entrar na outra sala?","Você está sozinho na sua sala. Ela será encerrada para você entrar na sala de outro jogador. Estou pronto confirma a participação; não muda de sala.\n\n"+roomExplanation(target),"Sair e entrar",()->joinConfirmed(roomId,itemId,previous,target.optLong("generation"),target.optString("profileId")),itemId);
    }
    private void joinConfirmed(String roomId,String itemId,String previous,long confirmedGeneration,String confirmedProfile){
        status.setText("Conferindo se os jogos são idênticos…");final int generation=epoch;
        action(cancel->{
            // Prepare before leaving: a missing local game must preserve the current room.
            StationOnlineGame game=StationOnlineGame.prepare(this,client,itemId,state.get(),cancel,confirmedProfile);
            JSONObject target=client.multiplayerRoom(roomId);if(target==null||target.optLong("generation")!=confirmedGeneration||!confirmedProfile.equals(target.optString("profileId")))throw new StationOnlineGame.Unavailable("A sala mudou. Confira novamente as vagas e o modo.");game.verifyRoom(target);
            JSONObject current=state.get(),mine=current==null?null:current.optJSONObject("room");
            if(mine!=null){
                if(previous==null||!previous.equals(mine.optString("roomId"))||!StationPlayerModel.soloWaitingRoom(current))
                    throw new StationOnlineGame.Unavailable("Sua sala mudou. Atualize a lista antes de entrar em outra.");
                JSONObject left=client.call(StationOnlineClient.command("leave").put("roomId",previous),false,cancel);
                prepared=null;deliver(left,generation);
                if(left.optJSONObject("room")!=null)throw new java.io.IOException("Room leave not acknowledged");
            }
            prepared=game;JSONObject joined=client.call(game.fields(StationOnlineClient.command("join").put("roomId",roomId).put("generation",confirmedGeneration)),false,cancel);
            JSONObject actual=joined.optJSONObject("room");
            if(actual==null||!roomId.equals(actual.optString("roomId"))||!StationPlayerModel.member(actual,joined.optString("selfId")))
                throw new java.io.IOException("Room join not acknowledged");
            game.verifyRoom(actual);return joined;
        },()->{if(playerSheet!=null)playerSheet.dismiss();});
    }
    private void command(String action,String key,Object value){
        final JSONObject displayed=room();final String target=displayed==null?"":displayed.optString("roomId");final long generation=displayed==null?0:displayed.optLong("generation");
        action(cancel->{JSONObject c=StationOnlineClient.command(action);if(key!=null)c.put(key,value);if(!target.isEmpty()){c.put("roomId",target);if("station-stream.v3".equals(displayed.optString("recoveryProtocol")))c.put("generation",generation);}return client.call(c,false,cancel);});
    }
    private void sendChat(){
        String value=chat.getText().toString().trim(),peer=conversationPeer;JSONObject mine=room();if(value.isEmpty()||busy)return;
        if(peer.isEmpty()&&mine==null){status.setText("Escolha uma pessoa ou entre em uma sala.");return;}
        if(!peer.isEmpty()&&!StationSocial.supports(state.get(),"direct-chat-v1")){status.setText("Conversa privada aguardando atualização do servidor.");return;}
        final String targetRoom=mine==null?"":mine.optString("roomId");
        action(cancel->{JSONObject request=StationOnlineClient.command(peer.isEmpty()?"chat":"direct-chat").put("text",value);if(peer.isEmpty())request.put("roomId",targetRoom);else request.put("peerId",peer);return client.call(request,false,cancel);},()->{if(conversationPeer.equals(peer)&&chat.getText().toString().trim().equals(value))chat.setText("");drafts.remove(peer);renderConversation(state.get());});
    }
    private String nickname(JSONObject snapshot,String id){return roster.resolveName(snapshot,id);}
    /** Room membership comes from the signed room, never from the visible people page. */
    private void renderParticipants(LinearLayout parent,JSONObject snapshot){
        parent.removeAllViews();LinearLayout line=row();line.setGravity(Gravity.TOP);parent.addView(line,new LinearLayout.LayoutParams(-1,-2));
        java.util.List<StationRoomRoster.Row> members=roster.rows(snapshot);
        for(int i=0;i<members.size();i++){
            if(i>0&&i%2==0){line=row();line.setGravity(Gravity.TOP);parent.addView(line,new LinearLayout.LayoutParams(-1,-2));}
            StationRoomRoster.Row member=members.get(i);LinearLayout column=vertical();
            LinearLayout.LayoutParams cp=new LinearLayout.LayoutParams(0,-2,1);cp.leftMargin=i%2==0?0:dp(12);line.addView(column,cp);
            TextView role=text(column,"P"+member.position+" · "+(member.occupied?(member.host?"ANFITRIÃO":"CONVIDADO")+(member.self?" · VOCÊ":""):"VAGA DISPONÍVEL"),9,muted);role.setLetterSpacing(.04f);
            TextView name=text(column,member.name,16,white);bold(name);name.setMinLines(2);name.setMaxLines(2);name.setEllipsize(android.text.TextUtils.TruncateAt.END);name.setPadding(0,0,0,dp(3));
            name.setContentDescription(member.occupied?"Participante: "+member.name:"Aguardando outro jogador");
            String readiness=!member.occupied?"Aguardando convite":member.ready?"Pronto":"Ainda não confirmou";
            TextView badge=text(column,readiness,10,member.ready?green:muted);badge.setSingleLine(true);badge.setEllipsize(android.text.TextUtils.TruncateAt.END);
            badge.setCompoundDrawablesWithIntrinsicBounds(new StationSocialIcon(member.ready?StationSocialIcon.READY:StationSocialIcon.PEOPLE,dp(13),member.ready?green:muted),null,null,null);badge.setCompoundDrawablePadding(dp(4));
        }
    }

    private void paint(JSONObject snapshot){
        JSONObject latest=state.get();if(latest==null||snapshot.optLong("revision")<latest.optLong("revision")||snapshot.optInt("page")!=page)return;
        if(snapshot.optLong("revision")==painted&&snapshot.optInt("page")==paintedPage)return;painted=snapshot.optLong("revision");paintedPage=page;
        roster.observe(snapshot);if(inviteNavigation!=null)inviteNavigation.setVisibility(client!=null&&client.multiplayerEnabled?View.GONE:View.VISIBLE);
        create.setEnabled(!busy&&!launching&&room()==null);setConnection("Online",true);status.setText(feedback.text(snapshot,busy));
        loadHero();
        int ry=roomScroll.getScrollY(),py=peerScroll.getScrollY();if(ownRoomCard!=null)ownRoomCard.setCover(null);ownRoomCard=null;ownRoomCoverItem="";rooms.removeAllViews();start=null;roomReadiness=null;renderPlayers(snapshot);refreshPlayerSheet();
        JSONObject mine=snapshot.optJSONObject("room");String self=snapshot.optString("selfId");roomTitle.setText("SALAS  ·  "+snapshot.optInt("totalRooms"));
        JSONArray inv=snapshot.optJSONArray("invites");if(inv!=null)for(int i=0;i<inv.length();i++){JSONObject v=inv.optJSONObject(i);if(v==null)continue;LinearLayout c=roomCoverCard(rooms,v.optString("itemId"));bold(text(c,"CONVITE RECEBIDO",10,green));text(c,nickname(snapshot,v.optString("fromPeerId"))+"  ·  "+client.name(v.optString("itemId")),14,white);button(c,"Aceitar e entrar",()->{conversationPeer="";join(v.optString("roomId"),v.optString("itemId"));},true);button(c,"Agora não",()->command("dismiss-invite","text",v.optString("inviteId")),false);}
        JSONArray requests=snapshot.optJSONArray("joinRequests");if(requests!=null)for(int i=0;i<requests.length();i++){JSONObject r=requests.optJSONObject(i);if(r==null)continue;LinearLayout c=card(rooms);bold(text(c,"PEDIDO PARA JOGAR",10,green));text(c,nickname(snapshot,r.optString("fromPeerId")),15,white);button(c,"Aceitar pedido",()->socialAction("accept-request",null,r.optString("requestId")),true);button(c,"Recusar",()->socialAction("dismiss-request",null,r.optString("requestId")),false);}
        if(mine!=null){
            ownRoomCoverItem=mine.optString("itemId");ownRoomCard=new StationCreateGameCard(this,true);
            boolean more=(inv!=null&&inv.length()>0)||(requests!=null&&requests.length()>0)||snapshot.optInt("page")>0||!snapshot.isNull("nextPage");
            JSONArray availableRooms=snapshot.optJSONArray("rooms");for(int ri=0;availableRooms!=null&&ri<availableRooms.length();ri++){JSONObject other=availableRooms.optJSONObject(ri);if(other!=null&&!mine.optString("roomId").equals(other.optString("roomId")))more=true;}
            rooms.addView(ownRoomCard,new LinearLayout.LayoutParams(-1,more?-2:-1));ownRoomCard.setGameName(client.name(ownRoomCoverItem));bindHeroCover();
            LinearLayout current=ownRoomCard.actions;
            LinearLayout heading=row();current.addView(heading,new LinearLayout.LayoutParams(-1,dp(48)));
            TextView caption=label("SUA SALA",10,green);bold(caption);caption.setLetterSpacing(.12f);heading.addView(caption,new LinearLayout.LayoutParams(0,-2,1));
            StationActionButton exit=new StationActionButton(this,"Sair",StationActionButton.DANGER,StationSocialIcon.EXIT);
            heading.addView(exit,new LinearLayout.LayoutParams(dp(88),dp(48)));exit.setContentDescription("Sair da sala atual");exit.setEnabled(!busy&&!launching);exit.setOnClickListener(v->command("leave",null,null));
            TextView game=text(current,client.name(mine.optString("itemId")),19,white);bold(game);game.setMaxLines(2);game.setEllipsize(android.text.TextUtils.TruncateAt.END);
            text(current,roomInfo(mine,self).summary,12,green);
            LinearLayout participants=vertical();current.addView(participants,new LinearLayout.LayoutParams(-1,-2));renderParticipants(participants,snapshot);
            JSONArray rd=mine.optJSONArray("ready");
            roomReadiness=text(current,"",11,muted);roomReadiness.setMinHeight(dp(26));roomReadiness.setAccessibilityLiveRegion(View.ACCESSIBILITY_LIVE_REGION_POLITE);
            if("waiting".equals(mine.optString("state"))){
                LinearLayout primary=actionLine(current);
                boolean isReady=contains(rd,self);
                ready=roomAction(primary,isReady?"Desmarcar":"Estou pronto",isReady?StationSocialIcon.NOT_READY:StationSocialIcon.READY,isReady?StationActionButton.SECONDARY:StationActionButton.PRIMARY,()->command("ready","value",!contains(rd,self)));
                ready.setContentDescription(isReady?"Desmarcar sua confirmação":"Confirmar que você está pronto para jogar");
                if(self.equals(mine.optString("hostId")))start=roomAction(primary,"Iniciar",StationSocialIcon.PLAY,StationActionButton.PRIMARY,()->startDialog());
            }
            LinearLayout secondary=actionLine(current);
            roomAction(secondary,"Conversa",StationSocialIcon.CHAT,StationActionButton.SECONDARY,()->openConversation("",""));
            if(!"station-stream.v3".equals(mine.optString("recoveryProtocol"))&&StationPlayerModel.shareable(snapshot))roomAction(secondary,"Convidar",StationSocialIcon.CODE,StationActionButton.SECONDARY,()->showRoomCode());
            if(StationLaunchPolicy.eligible(mine,self.equals(mine.optString("hostId")))){if(!launching&&StationLaunchPolicy.key(mine).equals(launchKey))roomAction(actionLine(current),"Abrir partida",StationSocialIcon.PLAY,StationActionButton.PRIMARY,()->{launchKey="";launch(mine,snapshot);});else launch(mine,snapshot);}

        }
        JSONArray all=snapshot.optJSONArray("rooms");int count=0;
        for(int i=0;all!=null&&i<all.length();i++){JSONObject r=all.optJSONObject(i);if(r==null||mine!=null&&mine.optString("roomId").equals(r.optString("roomId")))continue;count++;LinearLayout c=roomCoverCard(rooms,r.optString("itemId"));bold(text(c,client.name(r.optString("itemId")),15,white));text(c,nickname(snapshot,r.optString("hostId"))+"  ·  "+roomInfo(r,self).summary,12,muted);StationGamePlayerInfo.RoomInfo seats=roomInfo(r,self);boolean available="station-stream.v3".equals(r.optString("recoveryProtocol"))?seats.confirmed&&seats.acceptingPlayers&&seats.vacancies>0:"waiting".equals(r.optString("state"))&&r.optInt("players")<r.optInt("capacity",r.optInt("maximumPlayers",2));if(available){boolean social=!"station-stream.v3".equals(r.optString("recoveryProtocol"))&&StationSocial.supports(snapshot,"join-request-v1");Button b=button(c,StationSocial.requested(snapshot,r.optString("roomId"))?"Pedido enviado":social?"Pedir para jogar":"Entrar na sala",()->{if(social)action(cancel->client.call(StationOnlineClient.command("request-join").put("roomId",r.getString("roomId")),false,cancel));else join(r.optString("roomId"),r.optString("itemId"));},true);b.setEnabled(!busy&&!StationSocial.requested(snapshot,r.optString("roomId")));}else text(c,"Partida em andamento ou sala completa",12,muted);}
        if(count==0&&mine==null&&(inv==null||inv.length()==0))empty(rooms,"+","Ainda não há salas aqui","Crie a primeira sala ou procure pessoas online para combinar uma partida.");
        if(snapshot.optInt("page")>0)button(rooms,"← Página anterior",()->page(-1),false);if(!snapshot.isNull("nextPage"))button(rooms,"Mais salas →",()->page(1),false);
        JSONArray direct=snapshot.optJSONArray("directMessages");if(direct!=null)for(int i=0;i<direct.length();i++){JSONObject m=direct.optJSONObject(i);if(m==null||!self.equals(m.optString("toPeerId")))continue;String mid=m.optString("messageId");if(!seenMessages.contains(mid)&&!conversationPeer.equals(m.optString("fromPeerId"))){pendingPeer=m.optString("fromPeerId");pendingName=m.optString("nickname","Jogador");}seenMessages.add(mid);while(seenMessages.size()>128)seenMessages.remove(seenMessages.iterator().next());}
        renderConversation(snapshot);updateStartState();roomLayout(mine!=null);peerScroll.post(()->peerScroll.scrollTo(0,py));roomScroll.post(()->roomScroll.scrollTo(0,ry));
    }
    private void socialAction(String action,String peer,String text){action(cancel->{JSONObject c=StationOnlineClient.command(action);if(peer!=null)c.put("peerId",peer);if(text!=null)c.put("text",text);return client.call(c,false,cancel);});}
    private void openConversation(String id,String name){
        drafts.put(conversationPeer,chat.getText().toString());conversationPeer=id;conversationName=name;lastMessageId="";if(id.equals(pendingPeer)){pendingPeer="";pendingName="";}chat.setText(drafts.containsKey(id)?drafts.get(id):"");if(playerSheet!=null)playerSheet.dismiss();section=3;if(state.get()!=null)renderConversation(state.get());roomLayout(room()!=null);
    }
    private void renderConversation(JSONObject snapshot){
        inboxNotice.setText(pendingName+" • nova mensagem  ›");inboxNotice.setVisibility(pendingPeer.isEmpty()?View.GONE:View.VISIBLE);navChat.setText(pendingPeer.isEmpty()?"Conversas":"Nova mensagem");
        int cy=chatScroll.getScrollY();boolean atBottom=messages.getHeight()-chatScroll.getHeight()-cy<dp(64);messages.removeAllViews();chatActions.removeAllViews();JSONObject mine=snapshot.optJSONObject("room");String self=snapshot.optString("selfId");JSONArray list=null;
        if(!conversationPeer.isEmpty()){
            JSONObject peer=StationPlayerModel.peer(snapshot,conversationPeer);if(peer!=null)conversationName=peer.optString("nickname","Jogador");chatTitle.setText(conversationName);chatHint.setText(peer==null?"Presença não confirmada nesta página":"in-room".equals(peer.optString("status"))?"Em uma sala":"Online no aplicativo");
            inline(chatActions,"Perfil / convite",145,()->showPlayer(conversationPeer,conversationName),false);
            boolean enabled=StationSocial.supports(snapshot,"direct-chat-v1");composer(enabled&&!busy);chat.setHint(enabled?"Mensagem privada…":"Conversa privada aguardando ativação");list=StationSocial.messages(snapshot,conversationPeer);if(section==3)for(int i=0;i<list.length();i++){JSONObject m=list.optJSONObject(i);if(m!=null&&self.equals(m.optString("toPeerId")))StationPresence.markRead(m.optString("messageId"));}
            if(!enabled)empty(messages,"…","Conversa privada em preparação","As salas e seus convites já estão disponíveis. Esta conversa depende da atualização do serviço.");
            else if(list.length()==0)empty(messages,"…","Comece a conversa","Combine um jogo com "+conversationName+". As mensagens ficam disponíveis enquanto a presença estiver ativa.");
        }else if(mine!=null){chatTitle.setText("Conversa da sala");chatHint.setText(client.name(mine.optString("itemId")));list=mine.optJSONArray("messages");composer(!busy);if(list==null||list.length()==0)empty(messages,"…","Diga olá","Combine a partida com os participantes da sala.");}
        else{chatTitle.setText("Suas conversas");chatHint.setText("Pessoas online · convites · partidas");composer(false);empty(messages,"…","Jogue junto","Abra Pessoas online e toque em um nome para conversar ou convidar.");}
        for(int i=0;list!=null&&i<list.length();i++){JSONObject m=list.optJSONObject(i);if(m==null)continue;boolean own=self.equals(m.optString("fromPeerId"));LinearLayout bubble=card(messages);LinearLayout.LayoutParams lp=(LinearLayout.LayoutParams)bubble.getLayoutParams();lp.setMargins(own?dp(30):0,dp(3),own?0:dp(30),dp(7));bubble.setLayoutParams(lp);bubble.setBackground(bg(own?0xff174b3f:0xff202d35,0x00202d35));bold(text(bubble,own?"Você":m.optString("nickname","Jogador"),11,own?green:0xffa4c8e1));TextView value=text(bubble,m.optString("text"),14,white);value.setTextIsSelectable(true);value.setLineSpacing(dp(2),1);String utc=m.optString("utc");String time="";try{time=(m.has("utcMs")?java.time.Instant.ofEpochMilli(m.getLong("utcMs")).atZone(java.time.ZoneId.systemDefault()):java.time.OffsetDateTime.parse(utc).atZoneSameInstant(java.time.ZoneId.systemDefault())).format(java.time.format.DateTimeFormatter.ofPattern("HH:mm"));}catch(Exception ignored){}TextView stamp=text(bubble,time,10,muted);stamp.setGravity(Gravity.RIGHT);}
        String last=StationSocial.last(list);boolean changed=!last.equals(lastMessageId);lastMessageId=last;if(changed&&atBottom)chatScroll.post(()->chatScroll.fullScroll(View.FOCUS_DOWN));else chatScroll.post(()->chatScroll.scrollTo(0,cy));
    }

    /** Forty-eight dp touch targets without nested cards, avatars or per-row buttons. */
    private void renderPlayers(JSONObject snapshot){
        peers.removeAllViews();JSONArray list=snapshot.optJSONArray("peers");String self=snapshot.optString("selfId");
        String filter=playerSearch.getText().toString().trim().toLowerCase(java.util.Locale.ROOT);int shown=0;
        peerTitle.setText("ONLINE  ·  "+snapshot.optInt("totalPeers"));
        if(list!=null)for(int i=0;i<list.length();i++){
            JSONObject p=list.optJSONObject(i);if(p==null)continue;String id=p.optString("peerId"),name=p.optString("nickname","Jogador");
            if(!name.toLowerCase(java.util.Locale.ROOT).contains(filter))continue;shown++;
            boolean own=self.equals(id),available="online".equals(p.optString("status")),inRoom="in-room".equals(p.optString("status"));
            LinearLayout line=row();line.setPadding(dp(4),0,dp(5),0);
            android.graphics.drawable.GradientDrawable base=bg(0x00000000,0x00000000);
            line.setBackground(new RippleDrawable(ColorStateList.valueOf(0x3261ee8a),base,null));
            line.setFocusable(true);line.setClickable(true);line.setContentDescription(name+(own?", você":available?", disponível":inRoom?", em uma sala":", status não informado")+", abrir perfil");
            TextView dot=label("●",9,available?green:inRoom?0xffd7b877:muted);dot.setGravity(Gravity.CENTER);line.addView(dot,new LinearLayout.LayoutParams(dp(20),-1));
            TextView n=label(name,14,white);n.setSingleLine(true);n.setEllipsize(android.text.TextUtils.TruncateAt.END);line.addView(n,new LinearLayout.LayoutParams(0,-2,1));
            TextView tail=label(own?"você":"›",own?10:22,muted);tail.setGravity(Gravity.RIGHT|Gravity.CENTER_VERTICAL);line.addView(tail,new LinearLayout.LayoutParams(dp(38),-1));
            peers.addView(line,new LinearLayout.LayoutParams(-1,dp(48)));line.setOnClickListener(v->{if(!own)openConversation(id,name);else showPlayer(id,name);});
            View separator=new View(this);separator.setBackgroundColor(0xff203128);peers.addView(separator,new LinearLayout.LayoutParams(-1,dp(1)));
        }
        if(shown==0){TextView empty=text(peers,filter.isEmpty()?"Nenhum jogador nesta página.":"Nenhum nome encontrado nesta página.",12,muted);empty.setPadding(dp(8),dp(18),dp(8),dp(18));}
        if(snapshot.optInt("page")>0)button(peers,"← Página anterior",()->page(-1),false);
        if(!snapshot.isNull("nextPage"))button(peers,"Próxima página →",()->page(1),false);
    }
    private void closeSheets(){if(playerSheet!=null){playerSheet.dismiss();playerSheet=null;}if(codeDialog!=null){codeDialog.dismiss();codeDialog=null;}inspectedPeer="";inspectedName="";peerFeedback="";}
    private void showPlayer(String id,String name){
        if(playerSheet!=null)playerSheet.dismiss();inspectedPeer=id;inspectedName=name;peerFeedback="";
        playerSheet=new StationPlayerSheet(this,new StationPlayerSheet.Actions(){
            public void invite(){invitePlayer(id);}
            public void join(){joinPlayer(id);}
            public void code(){showRoomCode();}
            public void block(){confirmBlock(id,name);}
        });
        playerSheet.setOnDismissListener(d->{if(playerSheet==d){playerSheet=null;inspectedPeer="";}});
        refreshPlayerSheet();playerSheet.show();
    }
    private void refreshPlayerSheet(){
        if(playerSheet==null||client==null||inspectedPeer.isEmpty())return;
        StationPlayerModel model=StationPlayerModel.read(state.get(),inspectedPeer,inspectedName,selectedItem);
        String item=model.ownedRoom==null?selectedItem:model.ownedRoom.optString("itemId");
        playerSheet.render(model,model.roomItemId.isEmpty()?"":client.name(model.roomItemId),item==null?"Jogo não selecionado":client.name(item),busy,peerFeedback,StationSocial.supports(state.get(),"join-request-v1"));String coverItem=model.roomItemId.isEmpty()?selectedItem:model.roomItemId;StationPlayerSheet sheet=playerSheet;
        if(coverItem==null||coverItem.isEmpty()){sheet.coverItem="";sheet.setCover(null);}
        else if(!coverItem.equals(sheet.coverItem)){sheet.coverItem=coverItem;sheet.setCover(null);artwork.load(coverItem,256,bitmap->{if(sheet==playerSheet&&coverItem.equals(sheet.coverItem))sheet.setCover(bitmap);});}
    }
    private void invitePlayer(String id){
        if(client!=null&&client.multiplayerEnabled){status.setText("Converse com o jogador e peça para entrar pela lista de salas. O convite desta modalidade ainda está em preparação.");return;}
        JSONObject snapshot=state.get();StationPlayerModel model=StationPlayerModel.read(snapshot,id,inspectedName,selectedItem);
        if(!model.canInvite||busy){peerFeedback=model.reason;refreshPlayerSheet();return;}
        peerFeedback="Preparando seu convite…";final int generation=epoch;
        action(cancel->{
            JSONObject current=state.get();StationPlayerModel latest=StationPlayerModel.read(current,id,model.name,selectedItem);
            if(!latest.canInvite)throw new StationOnlineGame.Unavailable(latest.reason);
            JSONObject own=current.optJSONObject("room");
            if(own==null){prepared=StationOnlineGame.prepare(this,client,selectedItem,current,cancel);current=client.call(prepared.fields(StationOnlineClient.command("create")),false,cancel);deliver(current,generation);own=current.optJSONObject("room");}
            if(own==null)throw new java.io.IOException("Missing created room");
            return client.call(StationOnlineClient.command("invite").put("roomId",own.getString("roomId")).put("peerId",id),false,cancel);
        },()->{peerFeedback="Convite enviado. O jogador tem 60 segundos para aceitar.";status.setText("Convite enviado para "+model.name+".");});
    }
    private void joinPlayer(String id){
        StationPlayerModel model=StationPlayerModel.read(state.get(),id,inspectedName,selectedItem);
        if(!model.canJoin||busy){peerFeedback=model.reason;refreshPlayerSheet();return;}
        if(StationSocial.supports(state.get(),"join-request-v1")){action(cancel->client.call(StationOnlineClient.command("request-join").put("roomId",model.joinRoomId),false,cancel),()->{peerFeedback="Pedido enviado. Aguarde o convite do anfitrião.";refreshPlayerSheet();});}
        else join(model.joinRoomId,model.joinItemId);
    }
    private void confirmBlock(String id,String name){
        confirmWithCover("Bloquear "+name+"?","Oculta este jogador enquanto estiver online e encerra uma sala compartilhada.","Bloquear",()->{if(playerSheet!=null)playerSheet.dismiss();command("block","peerId",id);});
    }
    private String currentCoverItem(){JSONObject own=room();return section==2||own==null?selectedItem:own.optString("itemId",selectedItem);}
    private LinearLayout codePanel(String heading,String detail){return codePanel(heading,detail,currentCoverItem());}
    private LinearLayout codePanel(String heading,String detail,String item){
        if(codeDialog!=null)codeDialog.dismiss();confirmationPlayers=null;confirmStart=null;codeDialog=new Dialog(this);codeDialog.requestWindowFeature(Window.FEATURE_NO_TITLE);
        StationFlowPanel flow=new StationFlowPanel(this);flow.setBackground(bg(0xff0b1711,0xff315441));LinearLayout outer=flow.content;
        if(item!=null){String game=client==null?"jogo selecionado":client.name(item);artwork.load(item,1024,bitmap->flow.setCover(bitmap,game));}
        TextView brand=text(outer,"LZ GAMES  /  CONVITE",10,green);brand.setLetterSpacing(.1f);bold(brand);
        TextView title=text(outer,heading,22,white);bold(title);text(outer,detail,12,muted);
        codeDialog.setContentView(flow);return outer;
    }
    private void displayCodePanel(){
        codeDialog.show();Window w=codeDialog.getWindow();if(w==null)return;
        w.setBackgroundDrawableResource(android.R.color.transparent);w.addFlags(WindowManager.LayoutParams.FLAG_DIM_BEHIND);
        WindowManager.LayoutParams p=w.getAttributes();p.width=Math.min(dp(740),getResources().getDisplayMetrics().widthPixels-dp(24));p.height=Math.min(dp(440),getResources().getDisplayMetrics().heightPixels-dp(24));p.dimAmount=.72f;p.gravity=Gravity.CENTER;w.setAttributes(p);w.getDecorView().setSystemUiVisibility(getWindow().getDecorView().getSystemUiVisibility());
        w.setSoftInputMode(WindowManager.LayoutParams.SOFT_INPUT_ADJUST_RESIZE);
    }
    private void showRoomCode(){
        if(room()!=null&&"station-stream.v3".equals(room().optString("recoveryProtocol"))){status.setText("Entre nesta partida pela lista de salas. O código desta modalidade ainda está em preparação.");return;}
        JSONObject snapshot=state.get();if(!StationPlayerModel.shareable(snapshot)){peerFeedback="O código está disponível quando sua sala tem uma vaga e aguarda jogadores.";refreshPlayerSheet();status.setText(peerFeedback);return;}
        JSONObject mine=snapshot.optJSONObject("room");final String code;
        try{code=mine.has("inviteCode")?StationInvitationCode.display(mine.optString("inviteCode")):StationInvitationCode.encode(snapshot.optString("instance"),mine.optString("roomId"),mine.optString("itemId"));}catch(IllegalArgumentException e){status.setText(e.getMessage());return;}
        LinearLayout panel=codePanel("Código da sua sala",client.name(mine.optString("itemId"))+"\nCompartilhe com outro jogador. "+(code.startsWith("TS1:")?"Ele cola o convite em Código de convite.":"Ele digita ou cola os 8 caracteres em Código de convite.")+" A senha da partida é automática após atualizar o app.");
        TextView value=text(panel,code,code.startsWith("TS1:")?12:26,white);value.setTypeface(Typeface.MONOSPACE);value.setTextIsSelectable(true);value.setPadding(dp(12),dp(12),dp(12),dp(12));value.setBackground(bg(0xff060f0a,0xff294634));
        text(panel,"Válido enquanto esta sala existir e tiver uma vaga. O outro jogador precisa da mesma edição instalada.",11,muted);
        Button copy=button(panel,"Copiar código",()->{android.content.ClipboardManager clipboard=(android.content.ClipboardManager)getSystemService(CLIPBOARD_SERVICE);if(clipboard!=null){clipboard.setPrimaryClip(ClipData.newPlainText("Convite TurboStations",code));Toast.makeText(this,"Código copiado",Toast.LENGTH_SHORT).show();}},true);
        compactCreateAction(copy);compactCreateAction(button(panel,"Fechar",()->codeDialog.dismiss(),false));displayCodePanel();
    }
    private void enterCodeDialog(){
        if(busy)return;LinearLayout panel=codePanel("Entrar por código","Digite ou cole o convite de 8 caracteres. Convites antigos também são aceitos.");
        EditText input=new EditText(this);field(input,"7KPM-4XRT");input.setSingleLine(true);input.setTextSize(12);input.setFilters(new android.text.InputFilter[]{new android.text.InputFilter.LengthFilter(160)});input.setInputType(android.text.InputType.TYPE_CLASS_TEXT|android.text.InputType.TYPE_TEXT_FLAG_NO_SUGGESTIONS);panel.addView(input,new LinearLayout.LayoutParams(-1,dp(48)));
        TextView error=text(panel,"",12,0xffefad98);error.setVisibility(View.GONE);error.setAccessibilityLiveRegion(View.ACCESSIBILITY_LIVE_REGION_POLITE);
        Button joinCode=button(panel,"Entrar na sala",()->{
            JSONObject snapshot=state.get();
            try{
                if(busy)throw new IllegalArgumentException("Aguarde a ação atual terminar.");
                if(snapshot==null||!active)throw new IllegalArgumentException("Conecte-se ao lobby antes de entrar.");
                if(snapshot.optJSONObject("room")!=null)throw new IllegalArgumentException("Saia da sala atual antes de usar outro código.");
                StationInvitationCode parsed=StationInvitationCode.parse(input.getText().toString());
                if(!parsed.shortCode.isEmpty()){
                    if(!supportsShortInvite(snapshot))throw new IllegalArgumentException("Os convites curtos aguardam a atualização do servidor.");
                    codeDialog.dismiss();joinCode(parsed.shortCode);
                }else{
                    if(!parsed.instance.equals(snapshot.optString("instance")))throw new IllegalArgumentException("Este código expirou após a atualização das salas. Peça um novo convite.");
                    codeDialog.dismiss();join(parsed.roomId,parsed.itemId);
                }
            }catch(IllegalArgumentException e){error.setText(e.getMessage());error.setVisibility(View.VISIBLE);}
        },true);
        compactCreateAction(joinCode);compactCreateAction(button(panel,"Cancelar",()->codeDialog.dismiss(),false));displayCodePanel();
    }

    private static boolean supportsShortInvite(JSONObject snapshot){
        JSONArray caps=snapshot.optJSONArray("roomCapabilities");
        for(int i=0;caps!=null&&i<caps.length();i++)if("short-invite-v1".equals(caps.optString(i)))return true;
        return false;
    }
    private void joinCode(String code){
        status.setText("Localizando a sala…");final int generation=epoch;
        action(cancel->{
            if(room()!=null)throw new StationOnlineGame.Unavailable("Saia da sala atual antes de usar outro convite.");
            JSONObject resolved=client.call(StationOnlineClient.command("resolve-code").put("inviteCode",code),false,cancel);
            JSONObject target=resolved.getJSONObject("resolvedRoom");
            String roomId=target.getString("roomId"),itemId=target.getString("itemId");
            // Validate the signed locator before preparing local files or joining.
            StationInvitationCode.encode(resolved.getString("instance"),roomId,itemId);
            runOnUiThread(()->{if(active&&epoch==generation){selectedItem=itemId;subtitle.setText(client.name(itemId));loadHero();}});
            StationOnlineGame game=StationOnlineGame.prepare(this,client,itemId,resolved,cancel);
            JSONObject joined=client.call(game.fields(StationOnlineClient.command("join").put("roomId",roomId)),false,cancel);
            JSONObject actual=joined.optJSONObject("room");
            if(actual==null||!roomId.equals(actual.optString("roomId"))||!StationPlayerModel.member(actual,joined.optString("selfId")))throw new java.io.IOException("Room join not acknowledged");
            game.verifyRoom(actual);prepared=game;return joined;
        },()->{if(playerSheet!=null)playerSheet.dismiss();conversationPeer="";navigate(0);});
    }

    private void styleNavigation(Button button,boolean selected){
        ((StationActionButton)button).setNavigationSelected(selected);
    }
    private void roomLayout(boolean joined){
        navRooms.setText(joined?"Sua sala":"Salas");
        leftPanel.setVisibility(section==1?View.VISIBLE:View.GONE);middlePanel.setVisibility(section==0?View.VISIBLE:View.GONE);createPanel.setVisibility(section==2?View.VISIBLE:View.GONE);
        rightPanel.setVisibility(section==3?View.VISIBLE:View.GONE);
        styleNavigation(navRooms,section==0);styleNavigation(navPeople,section==1);styleNavigation(navCreate,section==2);styleNavigation(navChat,section==3);
        if(createGame!=null){createGame.setText(selectedItem==null?"Jogo não selecionado":client==null?"Carregando jogo…":client.name(selectedItem));createCard.setGameName(createGame.getText().toString());}
    }
    private void loadHero(){
        JSONObject ownRoom=room();final String target=section==2||ownRoom==null?selectedItem:ownRoom.optString("itemId",selectedItem);
        if(client==null||target==null||target.isEmpty())return;
        if(target.equals(heroRequestId)&&heroEpoch==epoch){loadHeroProfile(target);return;}
        heroRequestId=target;heroEpoch=epoch;final int generation=epoch;
        headerRating.setRating(-1);headerPlayers.setText("Jogadores a confirmar");if(createInfo!=null)createInfo.setText(StationGamePlayerInfo.pending().detail);gamePlatform.setText("");clearHeroCover();heroSymbol.setVisibility(View.VISIBLE);
        artwork.load(target,1024,bitmap->{if(active&&generation==epoch&&target.equals(heroRequestId)&&!isDestroyed()){heroBitmap=bitmap;bindHeroCover();heroSymbol.setVisibility(bitmap==null?View.VISIBLE:View.GONE);}});
        commands.execute(()->{
            try{
                StationAndroid app=StationAndroid.get(this);StationCoordinator.Library library=app.coordinator.current();StationCatalog.Item item=library==null?null:library.catalog.find(target);if(item==null)return;
                String name=item.name,platform=item.platform;
                runOnUiThread(()->{if(active&&generation==epoch&&target.equals(heroRequestId)){subtitle.setText(name);gamePlatform.setText(platform);}});
                if(headerMetadata==null)try(java.io.InputStream in=getAssets().open("station-ui-r43/game-metadata.json")){headerMetadata=new JSONObject(new String(StationOnlineGame.read(in,512*1024),java.nio.charset.StandardCharsets.UTF_8));}
                final StationHeaderMetadata details=StationHeaderMetadata.lookup(headerMetadata,item.itemId,item.players);
                runOnUiThread(()->{if(active&&generation==epoch&&target.equals(heroRequestId)){headerRating.setRating(details.rating);}});
            }catch(Exception unavailable){android.util.Log.i("StationOnline","Selected game metadata unavailable");}
        });
        loadHeroProfile(target);
    }
    private void loadHeroProfile(final String target){
        if(client==null||!client.multiplayerEnabled||state.get()==null||(target.equals(profileRequestId)&&profileEpoch==epoch))return;
        profileRequestId=target;profileEpoch=epoch;final int generation=epoch;
        commands.execute(()->{
                StationApi.Cancellation check=new StationApi.Cancellation();requests.add(check);
                try{
                    java.util.List<StationMultiplayerProfile> profiles=StationOnlineGame.profiles(this,client,target,check);StringBuilder brief=new StringBuilder(),detail=new StringBuilder();java.util.SortedSet<Integer> counts=new java.util.TreeSet<>();
                    for(StationMultiplayerProfile p:profiles){StationGamePlayerInfo info=profileInfo(p);if(detail.length()>0)detail.append("\n");for(int count:p.allowed)counts.add(count);detail.append(info.detail);}
                    brief.setLength(0);for(int count:counts){if(brief.length()>0)brief.append(", ");brief.append(count);}if(brief.length()>0)brief.append(" jogadores").append(profiles.size()>1?" · "+profiles.size()+" modos":"");
                    final String b=brief.length()==0?StationGamePlayerInfo.pending().capacityLabel:brief.toString(),d=detail.length()==0?StationGamePlayerInfo.pending().detail:detail.toString();
                    runOnUiThread(()->{if(active&&generation==epoch&&target.equals(heroRequestId)){headerPlayers.setText(b);headerPlayers.setContentDescription(b);if(createInfo!=null)createInfo.setText(d);}});
                }catch(Exception unavailable){runOnUiThread(()->{if(active&&generation==epoch&&target.equals(heroRequestId)&&createInfo!=null)createInfo.setText(StationGamePlayerInfo.pending().detail);});}
                finally{requests.remove(check);}
        });
    }
    private static boolean contains(JSONArray a,String id){if(a!=null)for(int i=0;i<a.length();i++)if(id.equals(a.optString(i)))return true;return false;}
    private static JSONObject findRoom(JSONObject snapshot,String rid){JSONArray a=snapshot.optJSONArray("rooms");if(a!=null)for(int i=0;i<a.length();i++){JSONObject r=a.optJSONObject(i);if(r!=null&&rid.equals(r.optString("roomId")))return r;}return null;}
    private void page(int delta){if(busy||client==null)return;page=Math.max(0,Math.min(40,page+delta));client.page=page;action(cancel->client.call(StationOnlineClient.command("heartbeat"),false,cancel));}
    private static String roomConfirmationKey(JSONObject snapshot){JSONObject room=snapshot==null?null:snapshot.optJSONObject("room");return room==null?"":snapshot.optString("instance")+"/"+room.optString("roomId")+"/"+room.optString("itemId")+"/"+room.optLong("generation")+"/"+room.optString("profileId")+"/"+room.optString("profileSha256")+"/"+room.optInt("capacity")+"/"+String.valueOf(room.optJSONArray("roster"));}
    private void updateStartState(){
        JSONObject snapshot=state.get();String reason=StationRoomStartState.reason(snapshot);
        boolean allowed=!busy&&!launching&&reason.isEmpty();
        if(start!=null){start.setEnabled(allowed);start.setContentDescription("Iniciar partida"+(reason.isEmpty()?"":": "+reason));}
        if(roomReadiness!=null)roomReadiness.setText(busy?"Confirmando sua solicitação…":launching?"Preparando a partida…":reason.isEmpty()?"Todos estão prontos. Pode iniciar.":reason);
        if(confirmationPlayers!=null&&codeDialog!=null&&codeDialog.isShowing()){
            if(!roomConfirmationKey(snapshot).equals(confirmationPlayers.getTag())){codeDialog.dismiss();status.setText("A sala mudou. Confira o jogo e os participantes antes de iniciar.");}
            else{renderParticipants(confirmationPlayers,snapshot);if(confirmStart!=null)confirmStart.setEnabled(allowed);}
        }
    }
    private void startDialog(){
        JSONObject snapshot=state.get();String reason=StationRoomStartState.reason(snapshot);
        if(busy||!reason.isEmpty()){feedback.fail(reason.isEmpty()?"Aguarde a solicitação atual terminar.":reason);status.setText(feedback.text(state.get(),busy));return;}

        JSONArray transports=snapshot==null?null:snapshot.optJSONArray("transports");
        if(!contains(transports,StationRoomStartState.transport(snapshot))){status.setText("A conexão pela internet aguarda a atualização do servidor Station. As salas e os jogos locais continuam disponíveis.");return;}
        String confirmedItem=snapshot.optJSONObject("room").optString("itemId");
        LinearLayout content=codePanel("Tudo pronto para jogar",client.name(confirmedItem),confirmedItem);
        final String confirmedRoom=roomConfirmationKey(snapshot);
        confirmationPlayers=vertical();confirmationPlayers.setTag(confirmedRoom);content.addView(confirmationPlayers,new LinearLayout.LayoutParams(-1,-2));renderParticipants(confirmationPlayers,snapshot);
        text(content,roomExplanation(snapshot.optJSONObject("room")),12,muted);
        LinearLayout actions=actionLine(content);
        roomAction(actions,"Voltar",StationSocialIcon.BACK,StationActionButton.SECONDARY,()->codeDialog.dismiss());
        confirmStart=roomAction(actions,"Iniciar",StationSocialIcon.PLAY,StationActionButton.PRIMARY,()->{
            codeDialog.dismiss();action(cancel->{JSONObject latest=state.get();String changed=StationRoomStartState.reason(latest);if(!changed.isEmpty())throw new StationOnlineGame.Unavailable(changed);if(!confirmedRoom.equals(roomConfirmationKey(latest)))throw new StationOnlineGame.Unavailable("A sala mudou. Confira os participantes antes de iniciar.");return client.call(StationOnlineClient.command("start").put("roomId",latest.getJSONObject("room").getString("roomId")).put("generation",latest.getJSONObject("room").optLong("generation")).put("transport",StationRoomStartState.transport(latest)),false,cancel);});
        });
        codeDialog.setOnDismissListener(d->{confirmationPlayers=null;confirmStart=null;});displayCodePanel();updateStartState();
    }
    private void launch(JSONObject room,JSONObject snapshot){String key=StationLaunchPolicy.key(room);if(launching||nativeReturn.pending()||key.equals(launchKey))return;launchKey=key;launching=true;int generation=epoch;StationApi.Cancellation cancel=new StationApi.Cancellation();requests.add(cancel);status.setText("Conectando a partida online…");android.util.Log.i("StationRooms","launch stage=prepare room="+room.optString("roomId")+" generation="+room.optLong("generation")+" host="+snapshot.optString("selfId").equals(room.optString("hostId")));
        commands.execute(()->{try{StationOnlineGame game=prepared;if(game==null)game=StationOnlineGame.prepare(this,client,room.getString("itemId"),snapshot,cancel);JSONObject gameRoom=room;
            if("station-stream.v3".equals(room.optString("recoveryProtocol"))){
                game.verifyRoom(room);JSONArray links=room.getJSONArray("links"),launchLinks=new JSONArray();String self=snapshot.getString("selfId");boolean host=self.equals(room.getString("hostId"));
                for(int li=0;li<links.length();li++){JSONObject link=links.getJSONObject(li);if(!host&&!self.equals(link.getString("guestPeerId")))continue;
                    JSONObject result=client.multiplayer(game.fields(StationOnlineClient.command("ticket")).put("roomId",room.getString("roomId")).put("generation",room.getLong("generation")).put("linkId",link.getString("linkId")),cancel);
                    JSONObject actual=result.getJSONObject("room");game.verifyRoom(actual);if(actual.getLong("generation")!=room.getLong("generation"))throw new java.io.IOException("Generation changed");
                    JSONObject ticket=result.getJSONObject("ticket");StationMultiplayerSession.checkTicket(ticket,link.getString("linkId"));
                    launchLinks.put(new JSONObject(link.toString()).put("ticket",ticket));
                }
                gameRoom=new JSONObject(room.toString()).put("launchLinks",launchLinks);
            }else if("relay-wss-v1".equals(room.optString("transport"))||"relay-wss-v2".equals(room.optString("transport"))){JSONObject ticketRequest=StationOnlineClient.command("relay-ticket").put("roomId",room.getString("roomId"));if("relay-wss-v2".equals(room.optString("transport")))game.fields(ticketRequest).put("generation",room.getLong("generation"));JSONObject connection=client.call(ticketRequest,false,cancel);gameRoom=connection.getJSONObject("room");android.util.Log.i("StationRooms","launch stage=relay-ticket-received");}
            game.verifyRoom(gameRoom);String file=StationRetroLaunch.prepare(this,game,gameRoom,snapshot,cancel);runOnUiThread(()->{if(active&&generation==epoch){returningFromGame=true;returningRecovery="relay-wss-v2".equals(room.optString("transport"))||"relay-wss-v3".equals(room.optString("transport"));Intent nativeIntent=null;try{nativeIntent=StationGameSession.attach(this,room,new Intent(this,StationRetroActivity.class).putExtra("station.launch",file));android.util.Log.i("StationRooms","launch stage=activity");startActivityForResult(nativeIntent,nativeReturn.prepare(nativeIntent,room.optString("roomId"),("station-stream.v2".equals(room.optString("recoveryProtocol"))||"station-stream.v3".equals(room.optString("recoveryProtocol")))?room.optLong("generation",-1):0));}catch(RuntimeException e){StationGameSession.discardLaunch(nativeIntent,room.optString("roomId"),("station-stream.v2".equals(room.optString("recoveryProtocol"))||"station-stream.v3".equals(room.optString("recoveryProtocol")))?room.optLong("generation",-1):0);nativeReturn.abort(nativeIntent);returningFromGame=false;StationRetroLaunch.discard(this,file);launchFailure(e,generation);}}else StationRetroLaunch.discard(this,file);});}catch(Exception e){launchFailure(e,generation);}finally{requests.remove(cancel);}});
    }
    private void launchFailure(Throwable error,int generation){
        recordFailure(error);
        runOnUiThread(()->{if(active&&generation==epoch){launching=false;busy=false;feedback.fail(StationOnlineClient.message(error));painted=-1;JSONObject current=state.get();if(current!=null)paint(current);}});
    }
    private void exitRooms(){
        // Return never depends on network latency. Foreground presence completes explicit leave.
        JSONObject leaving=room();
        if(leaving!=null){
            android.content.SharedPreferences profile=getSharedPreferences("station-online-profile",0);
            if("station-stream.v3".equals(leaving.optString("recoveryProtocol"))){
                StationRoomLeaveIntent intent=StationRoomLeaveIntent.human(leaving.optString("roomId"),leaving.optLong("generation"),leaving.optString("recoveryProtocol"));
                synchronized(StationRoomLeaveIntent.class){if(!profile.edit().putString(StationRoomLeaveIntent.KEY,intent.encoded).remove("leaveOnCatalog").commit()){status.setText("Não foi possível registrar a saída. Tente novamente.");return;}}
            }else profile.edit().putBoolean("leaveOnCatalog",true).apply();
        }
        finish();
    }
    @Override public void onBackPressed(){if(section==3){navigate(1);return;}exitRooms();}
}
