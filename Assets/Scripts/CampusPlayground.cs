using System;
using UnityEngine;
using UnityEngine.UI;

namespace DongshanClub {
 public partial class CampusGame {
  ClubPlayground playground;
  Text playgroundObjective,playgroundCaption,playgroundPrompt,playgroundSteps;
  RectTransform playgroundPause,playgroundFinish;
  bool playgroundFromTitle,playgroundWasPaused,playgroundWasFinished;

  void OpenPlayground(){
   if(playground!=null)return;
   StopWorkshop();
   StopVideo();StopTyping();StopPortraitReaction();auto=false;SetHidden(false);Clear(overlay);Clear(feedback);
   modal=true;inMemory=false;playgroundFromTitle=title;playgroundWasPaused=false;playgroundWasFinished=false;
   var world=new GameObject("ClubPlayableDemo");playground=world.AddComponent<ClubPlayground>();playground.Testing=qa;
   playground.Build();
   var screen=R("PlayableDemo",overlay,0,0,1600,900).gameObject.AddComponent<RawImage>();screen.texture=playground.View;
   Panel(overlay,30,24,1540,148,new Color32(18,38,36,242),"PlaygroundHeader");
   T(overlay,"基础课 · 先让它跑起来",53,40,1120,36,24,gold);
   playgroundObjective=T(overlay,playground.Objective,53,85,1170,45,28,cream);
   playgroundSteps=T(overlay,"开关  →  缺口  →  压板  →  终点",53,137,1180,28,18,muted);
   B(overlay,"暂停 / 继续",1310,42,235,48,()=>{if(playground!=null&&!playground.Finished)playground.SetPaused(!playground.Paused);});
   B(overlay,"退出试玩",1310,105,235,45,ClosePlayground,true);
   Panel(overlay,30,685,1050,189,new Color32(18,38,36,244),"PlaygroundTalk");
   T(overlay,"旁边还坐着几个同学",52,700,1000,30,18,gold);
   playgroundCaption=T(overlay,playground.Caption,52,740,1000,118,24,cream);
   Panel(overlay,1100,685,470,189,new Color32(18,38,36,244),"PlaygroundControls");
   T(overlay,"WASD / 方向键  移动　空格  跳跃\nE  近距离交互　R  重来　Esc  暂停",1120,703,430,65,20,cream);
   B(overlay,"给点提示",1120,797,202,52,()=>{if(playground!=null)playground.Hint();},true);
   B(overlay,"放回起点",1340,797,207,52,()=>{if(playground!=null&&!playground.Finished){playground.SetPaused(false);playground.Respawn(false);}});
   var prompt=Panel(overlay,430,597,740,56,new Color32(18,38,36,238),"PlaygroundPrompt");
   playgroundPrompt=T(prompt.transform,"",10,0,720,56,23,cream,TextAnchor.MiddleCenter);
   playgroundPause=Panel(overlay,490,245,620,380,new Color32(239,233,213,252),"PlaygroundPause").rectTransform;
   T(playgroundPause,"先歇一会儿",30,24,560,54,35,ink);
   T(playgroundPause,"关卡停在这里，角色和箱子都不会动。",30,95,560,58,23,ink);
   B(playgroundPause,"继续玩",30,169,560,60,()=>playground.SetPaused(false));
   B(playgroundPause,"重来这一段",30,246,265,60,()=>{playground.SetPaused(false);playground.Respawn(false);},true);
   B(playgroundPause,"回去",315,246,275,60,ClosePlayground,true);playgroundPause.gameObject.SetActive(false);
   playgroundFinish=Panel(overlay,455,231,690,399,new Color32(239,233,213,252),"PlaygroundFinish").rectTransform;
   T(playgroundFinish,"还真走通了。",30,25,630,58,39,ink);
   T(playgroundFinish,"门能开，箱子能推，人也能跳过去。\n至于这个世界长什么样——下次再说。",30,110,630,100,26,ink);
   B(playgroundFinish,playgroundFromTitle?"回到桌面":"回去听他们接着聊",30,245,630,65,ClosePlayground);
   T(playgroundFinish,"没有成绩，也不用刷满分。",30,336,630,35,20,ink);playgroundFinish.gameObject.SetActive(false);
   playground.Changed=RefreshPlayground;RefreshPlayground();
  }
  void RefreshPlayground(){
   if(playground==null)return;
   playgroundObjective.text=playground.Objective;playgroundCaption.text=playground.Caption;playgroundPrompt.text=playground.Prompt;
   playgroundPrompt.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(playground.Prompt)&&!playground.Finished&&!playground.Paused);
   playgroundSteps.text=(playground.GateOpened?"开关 ✓":"开关")+"  →  "+(playground.Player.position.z>16?"缺口 ✓":"缺口")+"  →  "+(playground.BoxOnPlate?"压板 ✓":"压板")+"  →  "+(playground.Finished?"终点 ✓":"终点");
   if(playgroundWasPaused!=playground.Paused){playgroundWasPaused=playground.Paused;playgroundPause.gameObject.SetActive(playground.Paused&&!playground.Finished);}
   if(playgroundWasFinished!=playground.Finished){playgroundWasFinished=playground.Finished;playgroundFinish.gameObject.SetActive(playground.Finished);}
  }
  void StopPlayground(){
   if(playground==null)return;
   var old=playground;playground=null;old.Changed=null;
   old.gameObject.SetActive(false);Destroy(old.gameObject);
   playgroundObjective=null;playgroundCaption=null;playgroundPrompt=null;playgroundSteps=null;playgroundPause=null;playgroundFinish=null;
  }
  void ClosePlayground(){
   if(playground==null)return;bool finished=playground.Finished,fromTitle=playgroundFromTitle;
   StopPlayground();Clear(overlay);modal=false;inMemory=false;
   if(fromTitle){ShowTitle();return;}
   if(finished&&Current.kind=="lesson"){activityDone=true;Done("小叶子把这一版走通了。部长在旁边继续改他的程序。");}
  }
 }
}
