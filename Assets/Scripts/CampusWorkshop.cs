using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DongshanClub {
 public partial class CampusGame {
  ClubWorkshop workshop;
  Text workshopTitle,workshopGoal,workshopMessage,workshopMode,workshopInventory,workshopHover,workshopSaved,workshopFinishTitle,workshopFinishText;
  Text workshopModeButton,workshopNextButton,workshopRunStatus,workshopSpeedLabel,workshopFooter;
  Button workshopUndo,workshopReset,workshopDraft,workshopLoad,workshopPublish,workshopNext,workshopResume,workshopFree,workshopSpeed;
  readonly Dictionary<WorkshopTool,Button> workshopTools=new Dictionary<WorkshopTool,Button>();
  RectTransform workshopPausePanel,workshopFinishPanel;bool workshopFromTitle,workshopHadCompletion,workshopStoryCompleted;
  string WorkshopPath {get{return Path.Combine(qa?qaDir:Application.persistentDataPath,"workshop-level.json");}}

  void OpenWorkshop(bool published=false){
   if(workshop!=null)return;StopPlayground();StopVideo();StopPortraitReaction();StopTyping();auto=false;SetHidden(false);
   Clear(overlay);if(toast!=null)toast.gameObject.SetActive(false);CancelInvoke("HideToast");
   modal=true;inMemory=false;workshopFromTitle=title;workshopHadCompletion=false;workshopStoryCompleted=false;
   var obj=new GameObject("ClubWorkshop");workshop=obj.AddComponent<ClubWorkshop>();workshop.Testing=qa;workshop.Build(WorkshopPath,published);
   Panel(overlay,0,0,1600,900,new Color32(37,31,27,255),"WorkshopBackdrop");
   Panel(overlay,30,22,1540,113,new Color32(63,46,36,255));
   MotifLine(overlay,30,22,4,113,vermilion);
   LedgerRule(overlay,52,76,1060);
   workshopTitle=T(overlay,"",52,34,1040,36,29,gold);
   workshopGoal=T(overlay,"",52,83,1080,44,21,cream);
   var mode=B(overlay,"",1188,40,350,64,()=>{if(workshop!=null)workshop.SetPlaying(!workshop.Playing);},true);workshopModeButton=mode.GetComponentInChildren<Text>();
   var screen=R("WorkshopView",overlay,30,154,1120,550).gameObject.AddComponent<RawImage>();screen.texture=workshop.View;
   var hit=screen.gameObject.AddComponent<WorkshopBoardHit>();hit.world=workshop;hit.canvas=canvas;hit.onEdit=ClickSound;
   workshopRunStatus=T(overlay,"",48,164,810,33,20,gold);
   workshopSpeed=B(overlay,"",915,163,215,39,()=>workshop.ToggleSlow(),true);workshopSpeedLabel=workshopSpeed.GetComponentInChildren<Text>();
   Panel(overlay,1170,154,400,550,new Color32(63,46,36,255),"WorkshopTools");
   workshopMode=T(overlay,"",1190,169,360,40,25,gold);
   workshopInventory=T(overlay,"",1190,217,360,67,18,cream);
   workshopTools.Clear();
   var labels=new[]{"地板","墙","箱子","压板","门","移动","拿掉","转向 ↑"};
   for(int i=0;i<labels.Length;i++){
    WorkshopTool tool=(WorkshopTool)i;int col=i%2,row=i/2;
    workshopTools[tool]=B(overlay,labels[i],1190+col*184,302+row*57,174,46,()=>{if(workshop!=null)workshop.SelectTool(tool);});
   }
   workshopUndo=B(overlay,"撤回一步",1190,540,174,45,()=>workshop.Undo(),true);
   workshopReset=B(overlay,"重新摆",1374,540,174,45,()=>workshop.ResetLayout());
   B(overlay,"给点提示",1190,598,174,45,()=>workshop.Hint(),true);
   B(overlay,"暂停 / 退出",1374,598,174,45,()=>workshop.SetPaused(true));
   workshopHover=T(overlay,"",1190,665,360,31,17,muted);
   Panel(overlay,30,722,1540,156,new Color32(63,46,36,255));
   workshopMessage=T(overlay,"",52,739,1010,110,23,cream);
   workshopSaved=T(overlay,"",1090,737,450,36,18,gold);
   workshopDraft=B(overlay,"存草稿",1090,787,138,48,()=>workshop.SaveDraft(),true);
   workshopLoad=B(overlay,"读草稿",1240,787,138,48,()=>workshop.LoadDraft());
   workshopPublish=B(overlay,"留到展示",1390,787,148,48,()=>WorkshopPublish(),true);
   workshopFooter=T(overlay,"",52,850,1005,25,16,muted);
   workshopPausePanel=Panel(overlay,440,230,720,425,cream,"WorkshopPause").rectTransform;
   T(workshopPausePanel,"先放一会儿",30,24,660,56,36,ink);
   T(workshopPausePanel,"角色和箱子暂停了。自己的关卡退出时会存成草稿。",30,101,660,85,24,ink);
   B(workshopPausePanel,"继续",30,200,660,59,()=>workshop.SetPaused(false));
   B(workshopPausePanel,"直接试第三关",30,281,320,55,()=>{workshop.SetPaused(false);workshop.SkipToBuilding();},true);
   B(workshopPausePanel,"回去",370,281,320,55,CloseWorkshop,true);
   B(workshopPausePanel,"新一轮体验",30,354,320,44,()=>workshop.RestartTutorial());
   workshopResume=B(workshopPausePanel,"继续上次草稿",370,354,320,44,()=>{workshop.SetPaused(false);if(workshop.Stage<2)workshop.SkipToBuilding();if(workshop.Playing)workshop.SetPlaying(false);workshop.LoadDraft();});
   workshopPausePanel.gameObject.SetActive(false);
   workshopFinishPanel=Panel(overlay,430,215,740,441,cream,"WorkshopComplete").rectTransform;
   workshopFinishTitle=T(workshopFinishPanel,"",30,24,680,61,38,ink);
   workshopFinishText=T(workshopFinishPanel,"",30,107,680,120,25,ink);
   workshopNext=B(workshopFinishPanel,"",30,260,680,61,()=>{
    if(workshop.Stage<2){workshopStoryCompleted=true;workshop.NextStage();}else if(workshop.ViewingPublished)workshop.SetPlaying(false);else WorkshopPublish();
   });workshopNextButton=workshopNext.GetComponentInChildren<Text>();
   workshopFree=B(workshopFinishPanel,"自己出一关",30,260,325,61,()=>workshop.StartFreeBuild(),true);workshopFree.gameObject.SetActive(false);
   B(workshopFinishPanel,"再改改",30,345,325,55,()=>workshop.SetPlaying(false),true);
   B(workshopFinishPanel,"回去",375,345,335,55,CloseWorkshop,true);
   workshopFinishPanel.gameObject.SetActive(false);
   workshop.Changed=RefreshWorkshop;RefreshWorkshop();
   if(!string.IsNullOrEmpty(workshop.SaveError))workshop.Tell(workshop.SaveError);
   if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(null);
  }
  void RefreshWorkshop(){
   if(workshop==null)return;
   workshopTitle.text="东山试作坊 · "+workshop.StageName;workshopGoal.text=workshop.Goal;
   workshopMode.text=workshop.Completed?"试跑通过了":workshop.RunFailed?"回去改改吧":workshop.Playing?(workshop.Automatic?"小机器人试跑中":"试玩中"):"正在改地图";
   workshopModeButton.text=workshop.ViewingPublished?"回到草稿   Tab":workshop.Playing?(workshop.Automatic?"停下改改   Tab":"回到编辑   Tab"):(workshop.Automatic?"开始试跑   Tab":"试试这一版   Tab");
   workshopRunStatus.text=workshop.RunStatus;workshopSpeed.gameObject.SetActive(workshop.Automatic);workshopSpeedLabel.text=workshop.SlowMotion?"恢复正常速度":"慢点跑，看清楚";
   workshopFooter.text=workshop.Automatic?"Tab 编辑 / 试跑　Q / E 旋转箭头　R 重试　Ctrl+Z 撤回　右键拿掉":"Tab 编辑 / 试玩　WASD / 方向键移动　R 重试　Ctrl+Z 撤回　右键拿掉";
   workshopInventory.text=workshop.Inventory();workshopMessage.text=workshop.Message;workshopHover.text=workshop.Hover;
   foreach(var pair in workshopTools){
    pair.Value.interactable=!workshop.Playing&&!workshop.Paused&&(workshop.Stage>0||pair.Key==WorkshopTool.Floor||pair.Key==WorkshopTool.Move||pair.Key==WorkshopTool.Erase)&&(pair.Key!=WorkshopTool.Arrow||workshop.Automatic);
    pair.Value.GetComponent<Image>().color=pair.Key==workshop.Tool?new Color32(153,64,45,255):new Color32(91,55,42,255);
   }
   workshopTools[WorkshopTool.Arrow].GetComponentInChildren<Text>().text="转向 "+ClubWorkshop.DirectionLabel(workshop.ArrowFacing);
   workshopUndo.interactable=!workshop.Playing&&!workshop.Paused&&workshop.UndoCount>0;
   workshopReset.interactable=!workshop.Playing&&!workshop.Paused;
   workshopDraft.interactable=workshop.Stage>=2&&!workshop.ViewingPublished&&!workshop.Paused;
   workshopLoad.interactable=workshop.Stage>=2&&!workshop.Playing&&workshop.HasDraft&&!workshop.Paused;
   workshopPublish.interactable=workshop.Stage>=2&&!workshop.ViewingPublished&&workshop.Verified&&!workshop.Paused;
   workshopResume.interactable=workshop.HasDraft;
   workshopSaved.text=workshop.ViewingPublished?"正在试玩展示版，草稿单独保留。":workshop.Stage<2?"每轮从头体验，旧草稿可从暂停里打开。":workshop.Verified?"这一版已走通，可以留到展示。":workshop.Dirty?"有新的改动，退出时会存草稿。":workshop.DraftMatchesLayout?"草稿已保存，走通后可以展示。":"这是新地图；旧草稿需手动读回。";
   workshopPausePanel.gameObject.SetActive(workshop.Paused);
   workshopFinishPanel.gameObject.SetActive(workshop.Completed&&!workshop.Paused);
   bool canCreate=workshop.Completed&&workshop.Stage==2&&!workshop.ViewingPublished&&workshop.Automatic;
   workshopFree.gameObject.SetActive(canCreate);
   workshopNext.GetComponent<RectTransform>().anchoredPosition=new Vector2(canCreate?375:30,-260);
   workshopNext.GetComponent<RectTransform>().sizeDelta=new Vector2(canCreate?335:680,61);
   workshopNextButton.rectTransform.sizeDelta=new Vector2((canCreate?335:680)-24,61);
   if(workshop.Completed){
    workshopHadCompletion=true;workshopStoryCompleted=true;
    workshopFinishTitle.text=workshop.Stage==0?"这块地板，是你补的。":workshop.Stage==1?"过了，就算。":workshop.Automatic?"这回，它自己跑通了。":"这一关，能玩了。";
    workshopFinishText.text=canCreate?"它按你改过的路线跑通了。\n可以试试别的走法，也可以自己出一关。":workshop.ViewingPublished?"展示版走通了。想继续改的话，\n可以回到单独保存的草稿。":workshop.Stage==0?"地板补上，这回能走稳了。\n再让箱子搭把手，试试压住机关。":workshop.Stage==1?"过了！箱子也算出了一份力。\n下一关换你改地图，让机器人自己试。":"要不要把这一版留在展示电脑里？\n回到故事结尾，还能打开它给别人玩。";
    workshopNextButton.text=workshop.ViewingPublished?"回到我的草稿":workshop.Stage==0?"试试机关  →":workshop.Stage==1?"让它自己试  →":"留到展示里";
   }
  }
  void WorkshopPublish(){
   if(workshop==null||!workshop.Publish())return;
   workshopStoryCompleted=true;
   // Return to the editor while keeping the tested layout valid and the invitation visible in the dialogue.
   workshop.SetPlaying(false);workshop.Tell("同学：收到了，结尾的展示电脑里也能打开。\n小叶子：行，那这关先这样。 ");
  }
  void StopWorkshop(){
   if(workshop==null)return;var old=workshop;workshop=null;old.Changed=null;old.gameObject.SetActive(false);Destroy(old.gameObject);workshopTools.Clear();
  }
  void CloseWorkshop(){
   if(workshop==null)return;
   if(workshop.Stage>=2&&workshop.Dirty&&!workshop.SaveDraft()){workshop.SetPaused(false);return;}
   bool fromTitle=workshopFromTitle,completed=workshopStoryCompleted||workshopHadCompletion;
   StopWorkshop();Clear(overlay);modal=false;inMemory=false;
   if(fromTitle){ShowTitle();return;}
   if(completed&&Current.kind=="lesson")Done("小叶子把工程改了几处。部长凑过去，接着讲刚才那个机关。 ");
  }
  bool HasWorkshopShowcase(){
   try{if(!File.Exists(WorkshopPath))return false;var data=JsonUtility.FromJson<WorkshopFile>(File.ReadAllText(WorkshopPath));return data!=null&&data.version==1&&ClubWorkshop.Valid(data.published);}catch{return false;}
  }
 }

 public class WorkshopBoardHit : MonoBehaviour,IPointerClickHandler {
  public ClubWorkshop world;public Canvas canvas;
  public Action onEdit;
  int CellAt(Vector2 screenPoint){
   if(world==null)return -1;Vector2 local;var rect=(RectTransform)transform;
   if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect,screenPoint,canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,out local))return -1;
   return world.CellFromViewport(new Vector2(local.x/rect.rect.width,1+local.y/rect.rect.height));
  }
  public void OnPointerClick(PointerEventData data){if(world!=null&&!world.Paused&&world.ApplyCell(CellAt(data.position),data.button==PointerEventData.InputButton.Right))onEdit?.Invoke();}
  void Update(){if(world!=null)world.HoverCell(CellAt(Input.mousePosition));}
 }
}
