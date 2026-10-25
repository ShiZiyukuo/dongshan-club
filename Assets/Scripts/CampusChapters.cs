using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace DongshanClub {
 public partial class CampusGame {
  string chapterKey="xiaoye";
  string ActiveName {get{return chapterKey=="president"?"社长":"小叶子";}}
  string ChapterName {get{return chapterKey=="president"?"社长篇 · 先放我这儿吧":"小叶子篇 · 下次再看";}}
  const string OpeningWords="过去的这一年，其实不管是协会还是我，其实都经历过焦虑和迷茫......";
  void SelectStory(string key){
   chapterKey=key;
   var resource=Resources.Load<TextAsset>(key=="president"?"story-president":"story");
   story=JsonUtility.FromJson<Story>(resource.text);
   memory=false;solved=false;lesson=false;visited.Clear();history.Clear();pickedReplies.Clear();index=0;
   won=PlayerPrefs.GetInt(Prefix+"completed",0)==1;
  }
  int ResolveSavedIndex(SaveState s){
   if((string.IsNullOrEmpty(s.chapterKey)?"xiaoye":s.chapterKey)!=chapterKey)return -1;
   string id=!string.IsNullOrEmpty(s.nodeId)?s.nodeId:chapterKey=="xiaoye"?"n"+s.index:"";
   return Array.FindIndex(story.nodes,n=>n.id==id);
  }
  bool Finished(string key){return PlayerPrefs.GetInt((qa?"dc_qa_":"dc_")+(key=="president"?"president_":"")+"completed",0)==1;}
  void ShowTitle(){
   StopWorkshop();
   StopPlayground();
   StopPortraitReaction();StopVideo();StopTyping();Clear(stage);Clear(characters);Clear(overlay);Clear(feedback);
   SetHidden(false);title=true;modal=false;auto=false;SetBG("chapter-table-clean-v043");
   BuildHubProps();
   Panel(stage,0,0,690,900,new Color32(47,36,29,244));
   T(stage,"SXU / GAME DEV CLUB",62,60,570,36,18,muted);
   Lattice(stage,604,55,32,new Color32(222,179,107,90));
   LedgerRule(stage,65,119,550);
   T(stage,"东山有戏",57,155,485,98,76,cream);
   T(stage,"这关一起过",65,267,500,76,50,gold);
   TitleSeal(stage,565,175);
   LedgerRule(stage,65,349,550);
   T(stage,"山西大学 · 东山校区",65,373,560,38,23,gold);
   T(stage,OpeningWords,65,442,554,142,27,cream);
   T(stage,"饭可以慢慢吃，游戏也能一点点做。\n翻翻笔记，或拿起印章，听听这一年。",65,621,550,87,23,muted);
   if(Finished("president")){
    var mark=RoundPanel(stage,1288,660,82,46,new Color32(146,52,48,220),"SavedStamp");
    mark.rectTransform.localEulerAngles=new Vector3(0,0,PlayerPrefs.GetFloat((qa?"dc_qa_":"dc_")+"president_stampAngle",-7));
    Panel(mark.transform,12,20,22,5,cream);Panel(mark.transform,21,11,5,23,cream);
    RoundPanel(mark.transform,55,11,8,8,cream);RoundPanel(mark.transform,66,23,8,8,cream);
   }
   HubHotspot("活动卡",1195,601,305,134,()=>ShowRecruitment(0));
   HubLabel("笔记本",Finished("xiaoye")?"小叶子篇 · 已读完":"小叶子篇 · 下次再看",747,742,370,()=>InspectChapter("xiaoye"));
   HubLabel("印章",Finished("president")?"社长篇 · 已读完":"社长篇 · 先放我这儿吧",1162,742,370,()=>InspectChapter("president"));
   B(stage,"修一关，再留一关",65,714,520,50,()=>OpenWorkshop(),true);
   B(stage,"设置",65,780,160,52,Settings);B(stage,"关于",245,780,160,52,About);
   B(stage,"退出",425,780,160,52,()=>Application.Quit());
   T(stage,Application.version+" / 团结引擎 · 每篇约15分钟",65,850,580,30,17,muted);
  }
  void HubHotspot(string label,float x,float y,float w,float h,Action click){
   var im=Panel(stage,x,y,w,h,new Color(1,1,1,.001f),"Hotspot:"+label);
   var b=im.gameObject.AddComponent<Button>();b.transition=Selectable.Transition.None;b.onClick.AddListener(()=>{ResetHubHovers();ClickSound();click();});
  }
  void HubLabel(string item,string detail,float x,float y,float w,Action action){
   var p=RoundPanel(stage,x,y,w,113,new Color32(60,43,33,245),"Entry:"+item);
   MotifLine(p.transform,0,10,4,93,vermilion);
   Lattice(p.transform,w-37,16,21,new Color32(222,179,107,90));
   T(p.transform,item,20,12,w-64,38,29,gold);T(p.transform,detail,20,61,w-40,34,19,cream);
   p.gameObject.AddComponent<Button>().onClick.AddListener(()=>{ClickSound();action();});
   var relay=p.gameObject.AddComponent<HubHoverRelay>();relay.target=item=="笔记本"?notebookHover:stampHover;
  }
  void InspectChapter(string key){
   ResetHubHovers();
   SelectStory(key);
   var p=Modal(key=="president"?"印章":"笔记本",590);
   T(p,ChapterName,35,112,900,50,31,ink);
   T(p,key=="president"?"小小的游戏手柄图案，旁边有几张印歪的活动卡。\n\n有些活动，她只赶上了后半场。":"封面内侧留着一句：先记着，之后再改。\n\n从点名、听不懂的基础课，到自己做出一点东西。",35,185,900,174,26,ink);
   B(p,"开始这段故事",35,391,435,65,NewGame,true);
   bool hasSave=File.Exists(SavePath(0));
   if(hasSave)B(p,"继续这段故事",505,391,435,65,()=>Load(0));
   else T(p,"还没有这篇的存档",505,407,435,45,22,new Color32(116,87,64,255),TextAnchor.MiddleCenter);
   B(p,"放回桌上",35,480,905,55,ShowTitle);
  }
  void ShowCredits(){
   won=true;PlayerPrefs.SetInt(Prefix+"completed",1);PlayerPrefs.Save();StopPortraitReaction();StopVideo();StopTyping();SetHidden(false);
   Clear(stage);Clear(characters);Clear(overlay);Clear(feedback);title=false;modal=false;auto=false;SetBG("chapter-table");
   Panel(stage,0,0,1600,900,new Color32(43,32,26,242));
   Lattice(stage,1450,75,58,new Color32(222,179,107,95));
   LedgerRule(stage,105,207,1290);
   T(stage,ChapterName,100,105,1320,80,46,gold);
   T(stage,"行，那就一起。",105,242,1250,90,38,cream);
   T(stage,"想做什么，坐下来聊两句。没想好也行，先玩。\n桌上有大家的半成品，旁边还有把空椅子。",105,384,1220,130,29,muted);
   B(stage,"看看大家做的东西",105,644,410,72,ShowWorks,true);
   B(stage,"怎么加入",545,644,410,72,()=>ShowRecruitment(0));
   B(stage,chapterKey=="president"?"放下印章":"合上笔记本",985,644,410,72,ShowTitle);
   T(stage,"两篇独立，随时可以回到桌面，拿起另一件物品。",105,791,1260,45,22,muted);
  }
  void ShowWorks(){
   var p=Modal("大家做过的东西",610);
   T(p,"这里都是同学们录下来的东西。\n部长这段是他做的神经网络动画模型。",35,115,905,100,26,ink);
   B(p,"看看 Ren’Py 录像",35,260,435,70,()=>OpenVideo("renpy"),true);
   B(p,"看看大肥鱼录像",505,260,435,70,()=>OpenVideo("fish"),true);
   B(p,"部长的神经网络动画模型",35,365,905,65,()=>OpenVideo("neural"));
   if(HasWorkshopShowcase())B(p,"玩你留的那一关",35,465,905,58,()=>OpenWorkshop(true),true);
   else B(p,"旧版3D基础课 · 走一遍",35,465,905,58,OpenPlayground,true);
   T(p,"录像可随时退出；不要求播放完毕。",35,548,905,30,18,ink);
  }
  void CompleteInvitation(){
   if(title||modal)return;
   ClickSound();ShowCredits();
  }
 }
}
