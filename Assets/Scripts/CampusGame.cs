using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DongshanClub {
 [Serializable] public class Story { public string title,version; public Chapter[] chapters; public Beat[] nodes; }
 [Serializable] public class Chapter { public string title,subtitle; public int start; }
 [Serializable] public class Beat { public string id,scene,kind,who,text,media,actor,chat,contact,chatTime; public int chapter; public string[] choices,replies; }
 [Serializable] public class ChoicePick { public string node; public int value; }
 [Serializable] public class CastBook { public CastMember[] members; }
 [Serializable] public class CastMember { public string key,name,note; public string[] aliases,pokes; }
 [Serializable] public class SaveState { public int index; public string date,nodeId,chapterKey; public bool memory,solved,lesson; public int[] visited; public ChoicePick[] picks; }

 public partial class CampusGame : MonoBehaviour {
  public static CampusGame Instance;
  readonly Color ink=new Color32(53,43,35,255),cream=new Color32(246,237,215,255),gold=new Color32(222,179,107,255),muted=new Color32(204,186,157,255);
  Story story; Font font; Canvas canvas; Camera renderCamera; RectTransform root,characters,stage,overlay,feedback; Image background,portrait;
  Text dialogue,speaker,chapterLabel,location,progress,toastLabel; RectTransform toast,portraitRect;
  GameObject activity,choices; Button next; Coroutine typing; bool revealed,title=true,modal,hidden,activityDone,qa,auto;
  int index,pokes; float lastPoke=-10,autoClock; bool memory,solved,lesson; readonly HashSet<int> visited=new HashSet<int>();
  readonly List<string> history=new List<string>(); readonly Dictionary<string,Sprite> art=new Dictionary<string,Sprite>();
  AudioSource music,sfx; float volume=.35f; string qaDir; bool won; bool inMemory; Action dragCallback;
  Action memoryInteract;
  CastBook cast; string activeActor=""; readonly Dictionary<string,int> pickedReplies=new Dictionary<string,int>();
  string Prefix {get{return (qa?"dc_qa_":"dc_")+(chapterKey=="president"?"president_":"");}}
  Beat Current {get{return story.nodes[Mathf.Clamp(index,0,story.nodes.Length-1)];}}
  public static void Boot() { if(Instance==null)new GameObject("DongshanClub").AddComponent<CampusGame>(); }
  void Awake(){
   Instance=this; Application.targetFrameRate=60; Screen.sleepTimeout=SleepTimeout.NeverSleep;
   qa=Environment.GetCommandLineArgs().Contains("--qa");
   var q=Environment.GetCommandLineArgs().FirstOrDefault(a=>a.StartsWith("--qa-dir=")); qaDir=q==null?Path.Combine(Application.persistentDataPath,"QA"):q.Substring(9);
   SelectStory("xiaoye");
   cast=JsonUtility.FromJson<CastBook>(Resources.Load<TextAsset>("cast").text);
   font=Resources.Load<Font>("Fonts/Campus"); if(font==null)font=Font.CreateDynamicFontFromOSFont("Microsoft YaHei",28);
   foreach(var key in new[]{"classroom","cafeteria","xiaoye","founder","renpy-poster","fish-poster","founder-explain","xiaoye-sleep","xiaoye-invite","chapter-table","chapter-table-clean-v043","hub-book-closed","hub-book-open","hub-stamp","recruitment-main","recruitment-tech"}.Concat(cast.members.Select(m=>m.key)).Distinct()){
    var t=Resources.Load<Texture2D>("Art/"+(key=="cafeteria"?"cafeteria-v2":key)); if(t==null)t=PublicPlaceholder(key); art[key]=Sprite.Create(t,new Rect(0,0,t.width,t.height),new Vector2(.5f,.5f),100);
   }
   volume=PlayerPrefs.GetFloat(Prefix+"volume",.35f); won=PlayerPrefs.GetInt(Prefix+"completed",0)==1;
   BuildRoot(); BuildAudio(); ShowTitle();StartCoroutine(ApplyWindowTitle());if(qa)StartCoroutine(RunQA());
  }
  RectTransform R(string name,Transform parent,float x,float y,float w,float h){
   var g=new GameObject(name,typeof(RectTransform));var r=g.GetComponent<RectTransform>();r.SetParent(parent,false);
   r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;
  }
  Image Panel(Transform p,float x,float y,float w,float h,Color color,string name="Panel"){
   var r=R(name,p,x,y,w,h);var im=r.gameObject.AddComponent<Image>();im.color=color;return im;
  }
  Text T(Transform p,string value,float x,float y,float w,float h,int size,Color color,TextAnchor align=TextAnchor.UpperLeft){
   var r=R("Text",p,x,y,w,h);var t=r.gameObject.AddComponent<Text>();t.font=font;t.text=value;t.fontSize=size;t.color=color;t.alignment=align;
   t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Overflow;t.raycastTarget=false;t.supportRichText=false;t.lineSpacing=1.05f;return t;
  }
  Button B(Transform p,string value,float x,float y,float w,float h,Action action,bool light=false){
   var im=Panel(p,x,y,w,h,light?cream:new Color32(91,55,42,250),"Button:"+value);var b=im.gameObject.AddComponent<Button>();
   if(light){var outline=im.gameObject.AddComponent<Outline>();outline.effectColor=new Color(.12f,.23f,.18f,.22f);outline.effectDistance=new Vector2(1,-1);}
   var colors=b.colors; colors.normalColor=Color.white;colors.highlightedColor=new Color(1.15f,1.12f,1.02f);colors.pressedColor=new Color(.82f,.73f,.62f);b.colors=colors;
   T(im.transform,value,12,0,w-24,h,Mathf.Min(23,(int)(h*.43f)),light?ink:cream,TextAnchor.MiddleCenter);
   b.onClick.AddListener(()=>{ClickSound();action();});return b;
  }
  Image Picture(Transform p,string key,float x,float y,float w,float h,bool preserve=true){
   var im=Panel(p,x,y,w,h,Color.white,"Art:"+key);if(art.ContainsKey(key))im.sprite=art[key];else im.color=ink;im.preserveAspect=preserve;im.raycastTarget=false;return im;
  }
  void Clear(Transform t){foreach(Transform child in t){child.gameObject.SetActive(false);Destroy(child.gameObject);}}
  void BuildRoot(){
   var go=new GameObject("Canvas",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
   var sc=go.GetComponent<CanvasScaler>();sc.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;sc.referenceResolution=new Vector2(1600,900);sc.matchWidthOrHeight=.5f;root=go.GetComponent<RectTransform>();
   if(FindObjectOfType<EventSystem>()==null)new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));
   var cam=new GameObject("Camera",typeof(Camera)).GetComponent<Camera>();cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=ink;cam.cullingMask=~(1<<30);renderCamera=cam;
   background=Picture(root,"classroom",0,0,1600,900,false); Panel(root,0,0,1600,900,new Color(0.025f,.055f,.045f,.24f));
   characters=R("Characters",root,0,0,1600,900);stage=R("Stage",root,0,0,1600,900);overlay=R("Overlay",root,0,0,1600,900);feedback=R("Feedback",root,0,0,1600,900);
  }
  void SetBG(string key){if(art.ContainsKey(key))background.sprite=art[key];}
  void ShowOldTitle(){
   StopVideo();StopTyping();Clear(stage);Clear(characters);Clear(overlay);Clear(feedback);SetHidden(false);modal=false;title=true;auto=false;SetBG("classroom");
   Panel(stage,0,0,820,900,new Color32(17,40,35,245));Panel(stage,54,58,64,4,gold);
   T(stage,"DONGSHAN / GAME MAKING CLUB",55,81,680,35,19,muted);
   T(stage,"东山有戏",60,165,740,135,108,cream);T(stage,"这关一起过",60,332,740,135,86,gold);
   T(stage,"山西大学 · 东山校区",65,579,640,38,23,cream);
   T(stage,"一个不咸不淡的社团故事。",65,623,650,35,22,muted);
   Picture(stage,"xiaoye",770,80,810,1050);Panel(stage,830,780,660,61,new Color32(246,240,221,235));
   T(stage,won?"这把空椅子，给下一个来的人。":"“下次再看。”",852,791,610,40,24,ink);
   B(stage,"开始故事  →",65,701,225,62,NewGame,true);
   B(stage,"继续",305,701,125,62,()=>Load(0));B(stage,"设置",445,701,125,62,Settings);B(stage,"关于",585,701,125,62,About);
   T(stage,"约15分钟 · 点击人物会有反应 · 戴耳机也可以",65,795,700,45,19,muted);
   T(stage,Application.version+" / 团结引擎",65,851,500,28,16,muted);
   B(stage,"退出",1390,22,110,43,()=>Application.Quit());
   if(won){T(stage,"一周目已完成",1200,78,300,40,20,cream,TextAnchor.MiddleRight);Panel(stage,1420,665,70,70,new Color32(39,64,53,255));Panel(stage,1415,735,80,9,gold);Panel(stage,1420,744,8,35,ink);Panel(stage,1482,744,8,35,ink);}
  }
  void NewGame(){memory=false;solved=false;lesson=false;visited.Clear();history.Clear();pickedReplies.Clear();pokes=0;ShowNode(0);}
  void ShowNode(int target){
   StopWorkshop();
   StopPlayground();
   StopVideo();StopTyping();index=Mathf.Clamp(target,0,story.nodes.Length-1);title=false;modal=false;SetHidden(false);inMemory=false;autoClock=0;
   Clear(stage);Clear(characters);Clear(overlay);Clear(feedback);CancelInvoke("HideToast");activity=null;choices=null;phoneRows=null;phoneScroll=null;dragCallback=null;activityDone=false;
   var n=Current;visited.Add(n.chapter);SetBG(n.scene);
   Panel(stage,0,0,1600,110,new Color32(48,37,30,242));Panel(stage,60,104,1480,2,new Color32(180,133,80,190));
   T(stage,GameTitle,60,26,530,36,26,cream);
   chapterLabel=T(stage,story.chapters[n.chapter].title,610,30,490,34,22,gold);
   B(stage,"章节",1120,25,100,46,ChapterMenu);B(stage,"存档",1235,25,100,46,()=>SaveMenu(false));B(stage,"菜单",1350,25,160,46,Pause);
   location=T(stage,story.chapters[n.chapter].subtitle,70,132,900,35,19,cream);
   var end=n.chapter+1<story.chapters.Length?story.chapters[n.chapter+1].start:story.nodes.Length;
   progress=T(stage,(n.chapter+1).ToString("00")+" / "+story.chapters.Length.ToString("00")+"    ·    "+(index-story.chapters[n.chapter].start+1)+" / "+(end-story.chapters[n.chapter].start),1200,132,310,32,18,cream,TextAnchor.MiddleRight);
   for(int i=0;i<story.chapters.Length;i++)Panel(stage,70+i*(1460f/story.chapters.Length),187,1460f/story.chapters.Length-14,3,i<=n.chapter?gold:new Color32(198,205,183,95));
   RenderCast(n);
   bool phoneMode=!string.IsNullOrEmpty(n.chat);float boxX=phoneMode?855:70,boxW=n.kind=="invite"?700:phoneMode?675:1460;
   var box=Panel(stage,boxX,660,boxW,174,new Color32(248,239,218,250),"Dialogue");Panel(box.transform,0,0,boxW,3,vermilion);
   Lattice(box.transform,boxW-48,14,28,new Color32(153,106,67,85));
   var bn=box.gameObject.AddComponent<Button>();bn.onClick.AddListener(Advance);
   speaker=T(box.transform,SpeakerLabel(n),30,17,boxW-90,35,23,vermilion);
   dialogue=T(box.transform,"",30,57,boxW-60,108,phoneMode?23:27,ink);dialogue.lineSpacing=1.05f;
   next=B(stage,"继续  ▷",1385,840,145,43,Advance,true);
   B(stage,"记录",70,845,90,38,History);B(stage,chapterKey=="xiaoye"?"回访":"篇章",170,845,90,38,()=>{if(chapterKey=="xiaoye")MemoryMenu();else ChapterMenu();});
   B(stage,"自动",270,845,90,38,()=>{auto=!auto;Toast(auto?"自动阅读已开启":"自动阅读已关闭");});
   T(stage,"空格继续 · H隐藏界面 · 点击立绘试试看",390,851,800,30,17,cream);
   toast=R("Reaction",feedback,390,835,960,60);Panel(toast,0,0,960,60,cream);toastLabel=T(toast,"",18,8,924,45,21,ink);toast.gameObject.SetActive(false);
   string line=DisplayLine(n);
   history.Add((n.kind=="choice"||string.IsNullOrEmpty(n.who)?"旁白：":n.who+"：")+n.text);if(history.Count>400)history.RemoveAt(0);
   revealed=false;typing=StartCoroutine(Type(line));
   SetActivity(n);Save(0,false);
  }
  IEnumerator Type(string line){
   if(qa){dialogue.text=line;revealed=true;yield break;}
   dialogue.text="";for(int i=1;i<=line.Length;i++){dialogue.text=line.Substring(0,i);yield return new WaitForSecondsRealtime(.022f);}revealed=true;typing=null;
  }
  void StopTyping(){if(typing!=null){StopCoroutine(typing);typing=null;}}
  public void Advance(){
   if(title||modal||hidden||inMemory)return;
   if(!revealed){StopTyping();dialogue.text=DisplayLine(Current);revealed=true;return;}
   if(Current.kind=="choice"){Toast("先选一句回应吧。");return;}
   if(!activityDone&&IsRequired(Current.kind)){Toast("先试试画面左边的互动。没有时间限制。");return;}
   if(Current.kind=="ending"){ShowCredits();return;}if(Current.kind=="invite"){CompleteInvitation();return;}if(index<story.nodes.Length-1)ShowNode(index+1);
  }
  bool IsRequired(string kind){return kind=="lesson"||kind=="attachments"||kind=="notice"||kind=="stamp"||kind=="paper"||kind=="snack"||kind=="evidence"||kind=="memory";}
  void SetHidden(bool value){hidden=value;stage.gameObject.SetActive(!value);feedback.gameObject.SetActive(!value);}
  void ToggleHidden(){SetHidden(!hidden);}
  void Update(){
   if(workshop!=null){if(Input.GetKeyDown(KeyCode.Escape))workshop.SetPaused(!workshop.Paused);return;}
   if(playground!=null){if(Input.GetKeyDown(KeyCode.Escape)&&!playground.Finished)playground.SetPaused(!playground.Paused);return;}
   if(modal&&inMemory&&Input.GetKeyDown(KeyCode.E))memoryInteract?.Invoke();
   if(Input.GetKeyDown(KeyCode.Escape)){if(hidden)SetHidden(false);else if(modal)CloseModal();else if(!title)Pause();}
   if((Input.GetKeyDown(KeyCode.H)||Input.GetMouseButtonDown(1))&&!title&&!modal)ToggleHidden();
   if(Input.GetKeyDown(KeyCode.Space)||Input.GetKeyDown(KeyCode.Return))Advance();
   if(Input.GetKeyDown(KeyCode.L)&&!title&&!modal)History();
   if(auto&&!title&&!modal&&revealed&&!IsRequired(Current.kind)&&Current.kind!="choice"&&Current.kind!="ending"&&Current.kind!="invite"){
    autoClock+=Time.unscaledDeltaTime;if(autoClock>Mathf.Max(2.8f,Current.text.Length*.10f)){autoClock=0;Advance();}
   }
  }
  public void Poke(string key){
   if(!modal&&!title&&Current.kind=="invite"){CompleteInvitation();return;}
   if(modal||title||portrait==null||portraitRect==null||Time.unscaledTime-lastPoke<.65f)return;lastPoke=Time.unscaledTime;
   var person=cast.members.FirstOrDefault(m=>m.key==key);if(person==null||person.pokes==null||person.pokes.Length==0)return;
   if(key=="founder"||key=="xiaoye"){
    StopPortraitReaction();
    Toast(key=="founder"?"等一下，我给你讲个简单的。":"……我只是闭眼想一下。");
    portraitReaction=StartCoroutine(PortraitReaction(key,portrait));
   }else {Toast(person.pokes[pokes++%person.pokes.Length]);StartCoroutine(Nudge(portraitRect));}
   ClickSound();
  }
  IEnumerator Nudge(RectTransform r){var start=r.anchoredPosition;for(float t=0;t<.32f;t+=Time.unscaledDeltaTime){if(r==null)yield break;r.anchoredPosition=start+new Vector2(Mathf.Sin(t*32)*9*(1-t/.32f),0);yield return null;}if(r!=null)r.anchoredPosition=start;}
  public void Toast(string text){if(toast==null||hidden)return;toastLabel.text=text;toast.gameObject.SetActive(true);CancelInvoke("HideToast");Invoke("HideToast",3.4f);}
  void HideToast(){if(toast!=null)toast.gameObject.SetActive(false);}
  RectTransform ActivityPanel(string label,string hint){
   var p=Panel(stage,90,230,720,390,new Color32(239,234,215,247),"Activity");activity=p.gameObject;
   T(p.transform,label,26,22,665,48,29,ink);T(p.transform,hint,26,75,665,65,20,new Color32(116,87,64,255));return p.rectTransform;
  }
  void Done(string text){activityDone=true;Toast(text);Save(0,false);}
  void MakeDrag(RectTransform panel,string label,Vector2 from,Vector2 to,Action onSuccess){
   var slot=Panel(panel,to.x,to.y,240,102,new Color32(200,211,188,255),"DropTarget");
   T(slot.transform,"放到这里",15,0,210,100,23,ink,TextAnchor.MiddleCenter);
   var card=Panel(panel,from.x,from.y,235,98,ink,"Draggable");T(card.transform,label,15,0,205,96,24,cream,TextAnchor.MiddleCenter);
   var d=card.gameObject.AddComponent<CampusDrag>();d.canvas=canvas;d.target=slot.rectTransform;d.onDrop=()=>{if(activityDone)return;slot.color=gold;onSuccess();};dragCallback=()=>d.CompleteForTest();
  }
  void SetActivity(Beat n){
   if(n.kind=="invite"){
    B(stage,"行啊，打个招呼",90,493,570,67,CompleteInvitation,true);
    T(stage,"也可以点一下他伸出的手。",95,576,565,45,22,cream);return;
   }
   if(n.kind=="attachments"||n.kind=="notice"||n.kind=="stamp"){PresidentActivity(n);return;}
   if(!string.IsNullOrEmpty(n.chat)){RenderPhone(n);if(n.kind=="choice")PhoneChoices(n);return;}
   if(n.kind=="choice"){
    var p=ActivityPanel("回一句", "不用想太久，只是接个话。");choices=p.gameObject;
    for(int i=0;i<n.choices.Length;i++){int c=i;B(p,n.choices[i],28,140+i*67,660,54,()=>Choose(c));}return;
   }
   if(n.kind=="paper"){
    var p=ActivityPanel("社团申请材料", "拖过去看两眼，补上那两段。");
    MakeDrag(p,"申请表",new Vector2(30,183),new Vector2(420,183),()=>Done("他补了几行。技术部部长看了看：行。"));return;
   }
   if(n.kind=="snack"){
    var p=ActivityPanel("第一次见面会", "把糖袋拖给旁边的同学。那边还在聊自己的游戏。");
    MakeDrag(p,"还有一袋糖",new Vector2(30,183),new Vector2(420,183),()=>Done("谢谢。他自己也拿了一颗。"));return;
   }
   if(n.kind=="media"){MediaPreview(n.media);return;}
   if(n.kind=="lesson"){
    lesson=true;
    var p=ActivityPanel("先让它跑起来", "部长打开了一个没做完的小关卡。这次可以自己补地板、摆机关，再试着走通。");
    T(p,"这次可以切进编辑，补地板、摆箱子，\n再自己搭一关留给别人玩。",30,153,650,90,25,ink);
    B(p,"我来改改  →",30,292,325,60,()=>OpenWorkshop(),true);
    B(p,"先看别人玩，接着聊",375,292,315,60,()=>{activityDone=true;Advance();});return;
   }
   if(n.kind=="demo"||n.kind=="lesson"){
    if(n.kind=="lesson")lesson=true;
    var p=ActivityPanel(n.kind=="demo"?"动作程序 · 演示示意":"基础课 · 交互演示",n.kind=="demo"?"攻击、闪避，还没做成完整游戏。":"灰色方块前，有一小块浅蓝色区域。");
    var arena=Panel(p,30,145,660,125,new Color32(73,91,84,255));
    Panel(arena.transform,40,67,550,3,muted);Panel(arena.transform,340,32,90,49,new Color32(118,145,156,255));
    var actor=Panel(arena.transform,130,20,35,65,gold);T(arena.transform,"程序功能示意",460,80,180,30,16,cream);
    B(p,n.kind=="demo"?"试试攻击":"看一下演示",30,292,305,60,()=>{StartCoroutine(Nudge(actor.rectTransform));Toast(n.kind=="demo"?"角色挥了一下。这段程序，技术部部长能研究很久。":"方块变绿了。他还没看懂过程。稍后可以回访。");});
    B(p,"继续听",355,292,335,60,()=>{activityDone=true;Advance();},true);return;
   }
   if(n.kind=="memory"){
    memory=true;var p=ActivityPanel("那张旧存档", "他想起了浅蓝色区域。回去试试，进度会留在这里。");
    B(p,"回访：基础课的灰色方块  →",30,158,660,72,OpenMemory,true);
    T(p,"如果已经知道怎么做，第一次回访也能直接成功。",30,266,660,70,20,ink);return;
   }
   if(n.kind=="evidence") {BuildEvidence();return;}
   if(n.kind=="door"){
    var p=ActivityPanel("隔壁教室", "门上贴着一张纸。");var door=Panel(p,210,133,285,230,new Color32(99,112,96,255));
    T(door.transform,"暂未开放",45,54,195,70,28,cream,TextAnchor.MiddleCenter);B(p,"敲一下",30,292,145,58,()=>Toast("没有回应。里面还没做。"));return;
   }
   if(n.kind=="message"){RenderPhone(n);return;}
   if(n.kind=="ending")activityDone=true;
  }
  void Choose(int c){if(Current.kind!="choice")return;var n=Current;pickedReplies[n.id]=c;string reply=n.replies[c];ShowNode(index+1);if(string.IsNullOrEmpty(n.chat))Toast(reply);}
  void BuildEvidence(){
   var p=ActivityPanel("角色为什么不动？", "先查看桌上的材料，再决定下一步。答错也可以继续试。");
   B(p,"比赛要求",25,142,210,54,()=>Doc("提交要求","作品需要能独立运行。\n\n这份版本已经能打开，问题发生在进入游戏之后。"));
   B(p,"操作说明",255,142,210,54,()=>Doc("操作说明","WASD 移动\nE 交互\n\n操作提示还藏在单独的说明里。"));
   B(p,"试玩记录",485,142,210,54,()=>Doc("试玩记录","已进入游戏。\n按方向键没有反应。\n\n试玩者没有使用WASD。"));
   B(p,"先按WASD试试",25,235,670,57,()=>{Done("角色动了。问题不是程序坏了，是操作没说清楚。");});
   B(p,"先请技术部部长检查程序",25,307,670,50,()=>Toast("技术部部长：你按的哪个？\n再看看操作说明和试玩记录。"));
  }
  RectTransform Modal(string heading,int height=610){
   StopVideo();auto=false;Clear(overlay);modal=true;Panel(overlay,0,0,1600,900,new Color(0,0,0,.66f));
   var p=Panel(overlay,310,(900-height)/2,980,height,cream,"Modal");
   MotifLine(p.transform,0,0,980,5,vermilion);LedgerRule(p.transform,32,90,916);
   Lattice(p.transform,943,height-36,24,new Color32(153,106,67,110));T(p.transform,heading,32,23,780,53,34,ink);
   B(p.transform,"关闭 ×",810,22,140,48,CloseModal);return p.rectTransform;
  }
  void CloseModal(){if(workshop!=null){CloseWorkshop();return;}if(playground!=null){ClosePlayground();return;}StopRecruitment();StopVideo();Clear(overlay);modal=false;inMemory=false;memoryInteract=null;}
  void Doc(string heading,string text){var p=Modal(heading,430);T(p,text,38,118,900,250,28,ink);}
  void Pause(){var p=Modal("先歇一会儿",540);B(p,"继续",35,112,435,66,CloseModal,true);B(p,"存档 / 读档",505,112,435,66,()=>SaveMenu(false));B(p,"章节与回访",35,202,435,66,ChapterMenu);B(p,"设置",505,202,435,66,Settings);B(p,"回到标题",35,292,905,66,()=>{Save(0,false);ShowTitle();});T(p,"进度会自动保存。随时回来就好。",35,400,900,60,23,ink);}
  string SavePath(int slot){return Path.Combine(Application.persistentDataPath,(qa?"qa-":"")+(chapterKey=="president"?"president-":"")+"slot-"+slot+".json");}
  void Save(int slot,bool tell=true){
   if(title)return;var s=new SaveState{index=index,nodeId=Current.id,chapterKey=chapterKey,date=DateTime.Now.ToString("MM-dd HH:mm"),memory=memory,solved=solved,lesson=lesson,visited=visited.ToArray(),picks=pickedReplies.Select(p=>new ChoicePick{node=p.Key,value=p.Value}).ToArray()};
   try{Directory.CreateDirectory(Application.persistentDataPath);var f=SavePath(slot);File.WriteAllText(f+".tmp",JsonUtility.ToJson(s));if(File.Exists(f))File.Delete(f);File.Move(f+".tmp",f);if(tell)Toast("已经记下这里。");}catch(Exception e){Debug.LogException(e);if(tell)Doc("暂时没存好","保存进度失败，请检查存储空间。游戏可以继续。\n"+e.Message);}
  }
  bool Load(int slot){
   try{if(!File.Exists(SavePath(slot))){if(title)Doc("这里还没有存档","先开始一段故事吧。");else Toast("这里还没有存档。");return false;}
    var s=JsonUtility.FromJson<SaveState>(File.ReadAllText(SavePath(slot)));int savedIndex=ResolveSavedIndex(s);if(savedIndex<0)throw new Exception("存档不属于当前篇章或位置无效");
    memory=s.memory;solved=s.solved;lesson=s.lesson;visited.Clear();if(s.visited!=null)foreach(int c in s.visited)visited.Add(c);pickedReplies.Clear();if(s.picks!=null)foreach(var p in s.picks)pickedReplies[p.node]=p.value;history.Clear();ShowNode(savedIndex);return true;
   }catch(Exception e){Doc("这份存档暂时打不开","可以返回标题重新开始，其他存档不受影响。\n"+e.Message);return false;}
  }
  void SaveMenu(bool unused){var p=Modal("记下这里 / 从这里回来",650);for(int i=0;i<4;i++){
    int slot=i;string label=i==0?"自动存档":"手动存档 "+i;string detail="空白";
    try{if(File.Exists(SavePath(i))){var s=JsonUtility.FromJson<SaveState>(File.ReadAllText(SavePath(i)));detail=story.chapters[story.nodes[ResolveSavedIndex(s)].chapter].title+"  ·  "+s.date;}}catch{detail="存档暂时不可用";}
    T(p,label,35,115+i*110,560,32,24,ink);T(p,detail,35,151+i*110,560,36,18,new Color32(94,113,97,255));
    if(i>0&&!title)B(p,"保存",610,117+i*110,140,57,()=>{Save(slot);SaveMenu(false);});
    B(p,"读取",775,117+i*110,165,57,()=>Load(slot),true);
   }}
  void ChapterMenu(){var p=Modal("那些不咸不淡的日子",690);for(int i=0;i<story.chapters.Length;i++){int c=i;bool unlocked=visited.Contains(c);B(p,(i+1).ToString("00")+"  "+(unlocked?story.chapters[i].title:"还没走到这里"),35+(i%2)*465,108+(i/2)*102,440,79,()=>{if(unlocked)ShowNode(story.chapters[c].start);},unlocked);}T(p,"仅能回看本次已到过的章节。",35,543,880,35,20,ink);if(chapterKey=="xiaoye")B(p,"基础课回访",35,594,905,57,()=>{if(lesson)OpenMemory();else Doc("还没到那节课","先往后走走吧。");});}
  void MemoryMenu(){if(!lesson){Toast("先到基础课看看。之后可以从这里回访。");return;}var p=Modal("旧存档",430);T(p,memory?"茶话会里的演示，让他想起了那块浅蓝色区域。":"那节没怎么听懂的基础课，还留在这里。",35,117,905,95,26,ink);B(p,"打开：基础课 · 灰色方块",35,266,905,69,OpenMemory,true);}
  void OpenMemory(){
   var p=Modal("基础课 · 灰色方块",600);inMemory=true;
   T(p,memory?"先进入浅蓝色范围，再按E。他似乎终于听懂了。":"灰色方块旁，有一块浅蓝色区域。没有任何惩罚，试试看。",35,99,905,66,23,ink);
   var floor=Panel(p,35,190,905,177,new Color32(62,82,76,255));
   var zone=Panel(floor.transform,550,52,150,95,new Color32(99,157,171,255));
   var cube=Panel(floor.transform,745,32,90,90,new Color32(153,159,149,255));
   var actor=Panel(floor.transform,110,55,45,74,gold);bool entered=false;bool triggered=false;
   B(p,"走进浅蓝色区域",35,410,440,65,()=>{entered=true;actor.rectTransform.anchoredPosition=new Vector2(600,-55);Toast("提示出现了：E。");},true);
   memoryInteract=()=>{if(!entered){Toast("没有反应。是不是站得还不够近？");return;}if(triggered)return;triggered=true;cube.color=new Color32(130,193,111,255);solved=true;PlayerPrefs.SetInt(Prefix+"memorySolved",1);Toast("原来是这么回事。技术部部长：对，先让它有反应。\n点“回到刚才”继续。");};
   B(p,"按 E / 交互",505,410,435,65,memoryInteract);
   B(p,"回到刚才",35,511,905,55,()=>{if(Current.kind=="memory"&&!solved){Toast("还差一下：进入范围，再按E。");return;}CloseModal();if(Current.kind=="memory")Done("他把那份旧工程的链接，重新翻了出来。");});
  }
  void History(){var p=Modal("刚才说到哪了",700);var scrollGo=R("Scroll",p,30,110,920,535);var img=scrollGo.gameObject.AddComponent<Image>();img.color=new Color32(228,230,211,255);scrollGo.gameObject.AddComponent<Mask>().showMaskGraphic=true;
   var content=R("Content",scrollGo,20,0,865,Mathf.Max(535,history.Count*95));var text=string.Join("\n\n",history.ToArray());var txt=T(content,text,8,15,845,32000,23,ink);txt.verticalOverflow=VerticalWrapMode.Overflow;Canvas.ForceUpdateCanvases();float ht=txt.preferredHeight+45;content.sizeDelta=new Vector2(865,ht);txt.rectTransform.sizeDelta=new Vector2(845,ht);
   var sc=scrollGo.gameObject.AddComponent<ScrollRect>();sc.content=content;sc.viewport=scrollGo;sc.horizontal=false;sc.vertical=true;sc.movementType=ScrollRect.MovementType.Clamped;sc.verticalNormalizedPosition=0;
  }
  void Settings(){var p=Modal("声音和阅读",550);T(p,"背景音乐",35,125,290,45,26,ink);
   B(p,"小一点",360,118,170,57,()=>{volume=Mathf.Clamp01(volume-.1f);ApplyVolume();Settings();});T(p,Mathf.RoundToInt(volume*100)+"%",545,130,120,40,25,ink,TextAnchor.MiddleCenter);B(p,"大一点",685,118,240,57,()=>{volume=Mathf.Clamp01(volume+.1f);ApplyVolume();Settings();});
   B(p,"关闭 / 开启音乐",35,215,890,65,()=>{volume=volume>.01f?0:.35f;ApplyVolume();Settings();});
   T(p,"鼠标：点击对白继续；点击人物听一句闲话。\n拖动纸张、糖袋；作品录像可播放、暂停或跳过。\n空格/回车：继续　Esc：菜单　H/右键：隐藏界面\n隐藏时保留背景与立绘；回访不覆盖主线进度。",35,317,890,185,22,ink);
  }
  void About(){var p=Modal("关于这段故事",650);T(p,GameTitle+"\n几个人，一桌电脑，几顿没聊完的饭。\n发生在山西大学东山校区的社团故事。\n\n剧情依据本人提供的经历创作，对话经过戏剧化处理。\n小叶子、部长与社长参考用户提供的照片；其余为原创外形。\n食堂按本人提供的实拍重绘；教室为绘画示意。\nRen’Py与DeepSeek大肥鱼使用本人提供的真实录像。\n美术经AI辅助制作；程序由团结引擎运行。\n背景音乐与提示音由本项目合成。\n\n社团：数媒交互与引擎技术协会\nQQ群：公开版不提供群号\n活动卡和结尾都可以查看招新海报。",35,112,905,485,22,ink);}
  void ShowOldCredits(){
   won=true;PlayerPrefs.SetInt(Prefix+"completed",1);PlayerPrefs.Save();StopVideo();StopTyping();SetHidden(false);Clear(stage);Clear(characters);Clear(overlay);Clear(feedback);title=false;modal=false;SetBG("classroom");
   Panel(stage,0,0,1600,900,new Color32(17,40,35,237));Panel(stage,120,100,70,4,gold);
   T(stage,GameTitle,120,155,1330,100,62,cream);T(stage,"一个不咸不淡的社团故事。",125,282,1250,55,30,gold);
   T(stage,"技术部部长还在研究他的3D战斗。\n有人翻开新画稿，有人把空椅子搬到桌边。\n\n你也可以来看看，大家正在做什么。",125,377,1100,200,31,cream);
   Panel(stage,1250,505,120,100,new Color32(99,125,99,255));Panel(stage,1250,605,10,95,muted);Panel(stage,1360,605,10,95,muted);Panel(stage,1240,480,140,12,gold);
   B(stage,"看看作品背后",125,695,310,72,()=>{var p=Modal("作品背后",490);T(p,"从申请材料、第一次见面会，到联谊和比赛，\n这段故事停在社团第二年，刚换完届的时候。\n\n如果你就在迎新摊位前，可以直接和身边的人聊聊。\n不需要先会程序，画画、写故事、试玩和提意见也算。",35,117,905,290,27,ink);},true);
   B(stage,"回到标题",465,695,260,72,ShowTitle);B(stage,"制作说明",755,695,260,72,About);
   T(stage,"感谢你把这段日常，玩到了结尾。",125,820,1200,40,22,muted);
  }
  void ApplyVolume(){music.volume=volume*.23f;PlayerPrefs.SetFloat(Prefix+"volume",volume);PlayerPrefs.Save();}
  void BuildAudio(){
   music=gameObject.AddComponent<AudioSource>();sfx=gameObject.AddComponent<AudioSource>();music.loop=true;
   const int rate=22050;const int seconds=24;float[] data=new float[rate*seconds];double[][] chords={new[]{130.81,164.81,196.00},new[]{110.00,130.81,164.81},new[]{87.31,110.00,130.81},new[]{98.00,123.47,146.83}};
   for(int i=0;i<data.Length;i++){double t=(double)i/rate;int k=(int)(t/6)%4;double envelope=Math.Pow(Math.Sin(Math.PI*(t%6)/6),2);double v=0;foreach(double hz in chords[k])v+=Math.Sin(t*hz*2*Math.PI)*.11+Math.Sin(t*hz*4*Math.PI)*.025;data[i]=(float)(v*envelope);}
   var clip=AudioClip.Create("EveningLoop",data.Length,1,rate,false);clip.SetData(data,0);music.clip=clip;ApplyVolume();music.Play();
  }
  void ClickSound(){if(sfx==null)return;const int count=1200;float[] samples=new float[count];for(int i=0;i<count;i++)samples[i]=(float)(Math.Sin(i*.15)*Math.Exp(-i/180.0)*.12);var c=AudioClip.Create("Click",count,1,22050,false);c.SetData(samples,0);sfx.PlayOneShot(c,.22f);Destroy(c,.3f);}

  IEnumerator Shot(string name){
   yield return null;
   var rt=new RenderTexture(1600,900,24);var previous=RenderTexture.active;
   renderCamera.targetTexture=rt;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=renderCamera;canvas.planeDistance=1;
   Canvas.ForceUpdateCanvases();renderCamera.Render();RenderTexture.active=rt;
   var image=new Texture2D(1600,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();
   File.WriteAllBytes(Path.Combine(qaDir,name+".png"),image.EncodeToPNG());
   renderCamera.targetTexture=null;RenderTexture.active=previous;canvas.renderMode=RenderMode.ScreenSpaceOverlay;rt.Release();Destroy(rt);Destroy(image);
   yield return null;
  }
  IEnumerator RunLegacyQA(){
   Directory.CreateDirectory(qaDir);won=false;ShowTitle();yield return new WaitForSecondsRealtime(1);var report=new List<string>();
   report.Add("Assets loaded: "+string.Join(",",art.Keys));yield return Shot("01-title");NewGame();yield return null;
   int errors=0,interactions=0,moviesChecked=0,phoneChecked=0;var castChecked=new HashSet<string>();bool capturedPaper=false,capturedMemory=false,capturedEvidence=false,capturedFounder=false;
   for(int i=0;i<story.nodes.Length;i++){
   ShowNode(i);yield return null;revealed=true;dialogue.text=DisplayLine(Current);
    if(activeActor!=DisplayActorKey(Current)){errors++;report.Add("Wrong on-screen actor: "+Current.id);}
    if(!string.IsNullOrEmpty(activeActor)){
     if(portrait==null||portrait.sprite!=art[activeActor]){errors++;report.Add("Wrong portrait: "+Current.id);}
     if(!castChecked.Contains(activeActor)){castChecked.Add(activeActor);yield return Shot("cast-"+activeActor);}
    }
    if(string.IsNullOrEmpty(Current.who)&&speaker.text!="旁白"){errors++;report.Add("Narration attributed to speaker: "+Current.id);}
    if(!string.IsNullOrEmpty(Current.chat)){
     if(phoneRows==null||phoneScroll==null){errors++;report.Add("Missing phone view: "+Current.id);}else phoneChecked++;
     if(i==28||i==30||i==240||i==254)yield return Shot("phone-"+i);
     if(i==30){bool conversation=phoneRows.Any(r=>r.who=="小叶子")&&phoneRows.Any(r=>r.who=="技术部部长");report.Add("Phone has both conversation sides: "+conversation);if(!conversation)errors++;}
     if(i==240){int chosen=pickedReplies["n29"];Save(2,false);pickedReplies.Clear();Load(2);if(!pickedReplies.ContainsKey("n29")||pickedReplies["n29"]!=chosen)errors++;report.Add("Phone reply save/load: "+pickedReplies.ContainsKey("n29"));}
     if(i==254&&phoneScroll!=null){phoneScroll.verticalNormalizedPosition=1;yield return null;yield return Shot("phone-history-scroll");phoneScroll.verticalNormalizedPosition=0;}
    }
    if(Current.kind=="demo"&&!capturedFounder){yield return Shot("02b-founder");capturedFounder=true;}
    if(Current.kind=="paper"||Current.kind=="snack"){
     if(!capturedPaper){yield return Shot("02-drag-paper");capturedPaper=true;}
     if(dragCallback==null){errors++;report.Add("Missing drag handler: "+Current.id);}else {dragCallback();if(!activityDone){errors++;report.Add("Drag failed: "+Current.id);}interactions++;}
    }
    if(Current.kind=="choice") {for(int c=0;c<Current.choices.Length;c++){if(Current.replies==null||c>=Current.replies.Length)errors++;}interactions++;}
    if(Current.kind=="media"){
     var mediaKey=Current.media;OpenVideo(mediaKey);float deadline=Time.realtimeSinceStartup+18;
     while(moviePlayer!=null&&movieError==null&&(!moviePlayer.isPrepared||moviePlayer.frame<1)&&Time.realtimeSinceStartup<deadline)yield return null;
     bool ready=moviePlayer!=null&&moviePlayer.isPrepared&&moviePlayer.frame>=1&&movieError==null;
     report.Add("Recording decoded "+mediaKey+": "+ready+(ready?" frame="+moviePlayer.frame:" error="+movieError));
     if(!ready)errors++;else{
      double before=moviePlayer.time;moviePlayer.Pause();yield return null;if(moviePlayer.isPlaying)errors++;
      moviePlayer.Play();yield return null;yield return Shot(mediaKey=="renpy"?"03a-renpy-recording":"03b-deepseek-fish-recording");
     }
     CloseModal();yield return null;if(moviePlayer!=null||movieTexture!=null)errors++;moviesChecked++;interactions++;
    }
    if(Current.kind=="memory"){
     OpenMemory();yield return null;var buttons=overlay.GetComponentsInChildren<Button>();
     buttons.First(b=>b.name=="Button:按 E / 交互").onClick.Invoke();if(solved)errors++;
     buttons.First(b=>b.name=="Button:走进浅蓝色区域").onClick.Invoke();
     buttons.First(b=>b.name=="Button:按 E / 交互").onClick.Invoke();if(!solved)errors++;
     yield return Shot("03-memory-solved");buttons.First(b=>b.name=="Button:回到刚才").onClick.Invoke();if(!activityDone||modal)errors++;interactions++;capturedMemory=true;
    }
    if(Current.kind=="evidence"){
     yield return Shot("04-investigation");stage.GetComponentsInChildren<Button>().First(b=>b.name=="Button:操作说明").onClick.Invoke();if(!modal)errors++;CloseModal();
     stage.GetComponentsInChildren<Button>().First(b=>b.name=="Button:先按WASD试试").onClick.Invoke();if(!activityDone)errors++;interactions++;capturedEvidence=true;
    }
    if(i==15){
     int expected=index;Save(3,false);ShowNode(0);if(!Load(3)||index!=expected)errors++;report.Add("Save/load round trip: "+(index==expected));ShowNode(i);
     ToggleHidden();bool keptPortrait=portrait!=null&&portrait.gameObject.activeInHierarchy&&characters.gameObject.activeInHierarchy&&!stage.gameObject.activeSelf&&background.gameObject.activeInHierarchy&&index==i;
     report.Add("Hidden UI keeps portrait and background: "+keptPortrait);if(!keptPortrait)errors++;
     yield return Shot("02c-hidden-keeps-portrait");ToggleHidden();if(hidden||!stage.gameObject.activeSelf)errors++;
    }
    if(i==story.chapters[2].start){yield return Shot("02d-real-cafeteria");}
    if(i%45==0){Poke(activeActor);yield return null;}
    if(Current.kind!="ending"){
     int expectedNext=i+1;
     if(Current.kind=="choice")stage.GetComponentsInChildren<Button>().First(b=>b.name=="Button:"+Current.choices[0]).onClick.Invoke();else Advance();
     if(index!=expectedNext){errors++;report.Add("Progression failed at "+i+" kind="+story.nodes[i].kind);}
    }
   }
   if(!capturedMemory||!capturedEvidence||visited.Count!=8||moviesChecked!=2||castChecked.Count!=9||phoneChecked!=35)errors++;
   foreach(var key in new[]{"founder","xiaoye"}){
    ShowNode(Array.FindIndex(story.nodes,n=>n.actor==key&&string.IsNullOrEmpty(n.chat)));yield return null;
    var origin=portraitRect.anchoredPosition;lastPoke=-10;Poke(key);yield return new WaitForSecondsRealtime(.24f);
    string pose=key=="founder"?"founder-explain":"xiaoye-sleep";
    bool changed=art.ContainsKey(pose)&&portrait.sprite==art[pose]&&Mathf.Approximately(portraitRect.anchoredPosition.x,origin.x);
    if(!changed)errors++;yield return Shot("reaction-"+key);
    ToggleHidden();bool visible=portrait.gameObject.activeInHierarchy;ToggleHidden();if(!visible)errors++;
    yield return new WaitForSecondsRealtime(2.1f);
    bool restored=portrait.sprite==art[key]&&Vector2.Distance(portraitRect.anchoredPosition,origin)<.01f;
    if(!restored)errors++;
    lastPoke=-10;Poke(key);ShowNode(index);yield return null;
    bool cancelled=portrait.sprite==art[key]&&portraitReaction==null;if(!cancelled)errors++;
    report.Add("Pose interaction "+key+": changed="+changed+" restored="+restored+" hiddenVisible="+visible+" cancelled="+cancelled);
   }
   ShowNode(30);yield return null;
   var wallpaper=stage.GetComponentsInChildren<RawImage>().FirstOrDefault(im=>im.name=="PhoneWallpaper");
   bool wallpaperReady=wallpaper!=null&&wallpaper.texture!=null&&Mathf.Approximately(wallpaper.color.a,.40f);
   report.Add("User phone wallpaper opacity 0.40: "+wallpaperReady);if(!wallpaperReady)errors++;
   ShowCredits();yield return Shot("05-ending");ShowTitle();yield return Shot("06-title-completed");
   report.Add("Story nodes rendered: "+story.nodes.Length);report.Add("Chapters visited: "+visited.Count);report.Add("Cast portraits checked: "+castChecked.Count);report.Add("Phone nodes checked: "+phoneChecked);report.Add("Interactive sequences checked: "+interactions);report.Add("Recordings checked: "+moviesChecked);report.Add("Cross-save interaction: "+solved);report.Add("Errors: "+errors);
   File.WriteAllLines(Path.Combine(qaDir,"runtime-report.txt"),report.ToArray());Debug.Log("CAMPUS_QA_COMPLETE errors="+errors);Application.Quit(errors==0?0:2);
  }
 }
 public class PortraitHit:MonoBehaviour,IPointerClickHandler { public CampusGame game;public string actorKey;public void OnPointerClick(PointerEventData e){game.Poke(actorKey);} }
 public class CampusDrag:MonoBehaviour,IBeginDragHandler,IDragHandler,IEndDragHandler {
  public Canvas canvas;public RectTransform target;public Action onDrop;Vector2 start;bool finished;
  RectTransform R {get{return (RectTransform)transform;}}
  public void OnBeginDrag(PointerEventData e){if(finished)return;start=R.anchoredPosition;}
  public void OnDrag(PointerEventData e){if(finished)return;R.anchoredPosition+=e.delta/canvas.scaleFactor;}
  public void OnEndDrag(PointerEventData e){if(finished)return;if(RectTransformUtility.RectangleContainsScreenPoint(target,e.position,null)){finished=true;onDrop?.Invoke();gameObject.SetActive(false);}else R.anchoredPosition=start;}
  public void CompleteForTest(){
   if(finished)return;
   var e=new PointerEventData(EventSystem.current);e.position=new Vector2(-1000,-1000);OnBeginDrag(e);OnEndDrag(e);
   var destination=RectTransformUtility.WorldToScreenPoint(null,target.TransformPoint(target.rect.center));
   var origin=RectTransformUtility.WorldToScreenPoint(null,R.TransformPoint(R.rect.center));
   e.position=destination;e.delta=destination-origin;OnBeginDrag(e);OnDrag(e);OnEndDrag(e);
  }
 }
}
