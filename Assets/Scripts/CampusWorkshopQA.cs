using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DongshanClub {
 public partial class CampusGame {
  int workshopQaErrors;
  void WorkshopCheck(List<string> report,string label,bool passed){report.Add("Workshop "+label+": "+passed);if(!passed){workshopQaErrors++;Debug.LogError("WORKSHOP_QA_FAILED "+label);}}
  IEnumerator WorkshopWalk(int x,int z,float seconds=6){
   var target=ClubWorkshop.World(x,z);float deadline=Time.realtimeSinceStartup+seconds;
   while(workshop!=null&&!workshop.Completed&&Time.realtimeSinceStartup<deadline){
    var delta=target-workshop.Player.position;var move=new Vector2(delta.x,delta.z);
    if(move.magnitude<.13f)break;workshop.TestInput(move.normalized);yield return new WaitForFixedUpdate();
   }
   if(workshop!=null)workshop.TestInput(Vector2.zero);yield return new WaitForSecondsRealtime(.18f);
  }
  void WorkshopClick(int x,int z,bool right=false){
   var screen=overlay.GetComponentsInChildren<RawImage>().First(v=>v.name=="WorkshopView");var rect=screen.rectTransform;
   Vector3 uv=workshop.BoardCamera.WorldToViewportPoint(ClubWorkshop.World(x,z));
   Vector3 local=new Vector3(uv.x*rect.rect.width,(uv.y-1)*rect.rect.height,0);
   var position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(local));
   var pointer=new PointerEventData(EventSystem.current){position=position,button=right?PointerEventData.InputButton.Right:PointerEventData.InputButton.Left};
   ExecuteEvents.Execute<IPointerClickHandler>(screen.gameObject,pointer,ExecuteEvents.pointerClickHandler);
  }
  IEnumerator WorkshopPush(){
   yield return WorkshopWalk(2,1);float deadline=Time.realtimeSinceStartup+4;
   while(!workshop.BoxOnPlate&&Time.realtimeSinceStartup<deadline){workshop.TestInput(Vector2.up);yield return new WaitForFixedUpdate();}
   workshop.TestInput(Vector2.zero);yield return new WaitForSecondsRealtime(.35f);
  }
  void WorkshopArrow(int x,int z,int direction){
   workshop.SelectTool(WorkshopTool.Arrow);while(workshop.ArrowFacing!=direction)workshop.RotateArrow(1);WorkshopClick(x,z);
  }
  IEnumerator WorkshopWaitRobot(float seconds){
   float until=Time.realtimeSinceStartup+seconds;
   while(workshop!=null&&!workshop.Completed&&!workshop.RunFailed&&Time.realtimeSinceStartup<until)yield return new WaitForFixedUpdate();
   yield return new WaitForSecondsRealtime(.15f);
  }
  IEnumerator RunWorkshopQA(List<string> report){
   var previousDraft=ClubWorkshop.Preset(3);previousDraft.mode=0;previousDraft.floor[ClubWorkshop.Index(0,0)]=1;
   File.WriteAllText(WorkshopPath,JsonUtility.ToJson(new WorkshopFile{progress=2,draft=previousDraft},true));
   workshopQaErrors=0;ShowTitle();yield return null;
   var button=stage.GetComponentsInChildren<Button>().FirstOrDefault(b=>b.name=="Button:修一关，再留一关");
   WorkshopCheck(report,"title entry",button!=null);if(button==null)yield break;
   button.onClick.Invoke();yield return new WaitForSecondsRealtime(.3f);
   WorkshopCheck(report,"new visitor starts fresh despite previous draft",workshop!=null&&workshop.Playing&&workshop.Stage==0&&workshop.Player!=null&&workshop.HasDraft);
   WorkshopCheck(report,"draft-only save loads without needing published map",workshop!=null&&workshop.HasDraft&&!workshop.HasPublished);
   if(workshop==null)yield break;
   string original=ClubWorkshop.Signature(workshop.Layout);int falls=workshop.Falls;float fallDeadline=Time.realtimeSinceStartup+5;
   while(workshop.Falls==falls&&Time.realtimeSinceStartup<fallDeadline){workshop.TestInput(Vector2.right);yield return new WaitForFixedUpdate();}
   workshop.TestInput(Vector2.zero);yield return new WaitForSecondsRealtime(.2f);
   WorkshopCheck(report,"missing floor causes safe retry without editing layout",workshop.Falls==falls+1&&ClubWorkshop.Signature(workshop.Layout)==original);
   workshop.SetPlaying(false);workshop.SelectTool(WorkshopTool.Floor);WorkshopClick(4,1);yield return null;
   WorkshopCheck(report,"pointer places actual floor",workshop.Layout.floor[ClubWorkshop.Index(4,1)]==1);
   workshop.Undo();WorkshopCheck(report,"undo placement",workshop.Layout.floor[ClubWorkshop.Index(4,1)]==0);
   WorkshopClick(4,1);workshop.SelectTool(WorkshopTool.Move);WorkshopClick(4,1);WorkshopClick(4,2);
   WorkshopCheck(report,"move floor",workshop.Layout.floor[ClubWorkshop.Index(4,1)]==0&&workshop.Layout.floor[ClubWorkshop.Index(4,2)]==1);
   workshop.Undo();WorkshopClick(4,1,true);WorkshopCheck(report,"right click removes added floor",workshop.Layout.floor[ClubWorkshop.Index(4,1)]==0);
   workshop.Undo();workshop.SelectTool(WorkshopTool.Floor);yield return Shot("workshop-01-repaired");
   workshop.SetPlaying(true);yield return WorkshopWalk(3,1);yield return WorkshopWalk(6,1);yield return WorkshopWalk(6,4);
   WorkshopCheck(report,"repaired floor can be walked to goal",workshop.Completed&&workshop.Verified);
   yield return Shot("workshop-02-first-complete");
   if(!workshop.Completed){report.Add("Workshop stopped after unreachable repair stage at "+workshop.Player.position);CloseWorkshop();yield break;}
   workshop.NextStage();yield return null;
   WorkshopCheck(report,"second stage edit mode",workshop.Stage==1&&!workshop.Playing);
   workshop.SelectTool(WorkshopTool.Box);bool noFloor=workshop.ApplyCell(ClubWorkshop.Index(0,0)),protectedStart=workshop.ApplyCell(ClubWorkshop.StartCell);
   WorkshopCheck(report,"objects need floor and endpoints stay fixed",!noFloor&&!protectedStart);
   WorkshopClick(4,2,true);WorkshopCheck(report,"existing wall can be removed",workshop.Layout.items[ClubWorkshop.Index(4,2)]==0);
   workshop.Undo();
   WorkshopClick(2,2);string authored=ClubWorkshop.Signature(workshop.Layout);workshop.SetPlaying(true);yield return WorkshopPush();
   WorkshopCheck(report,"physics box presses linked gate",workshop.BoxOnPlate&&workshop.DoorOpen);
   var bodyAt=workshop.Player.position;workshop.SetPaused(true);workshop.TestInput(Vector2.right);yield return new WaitForSecondsRealtime(.25f);
   WorkshopCheck(report,"pause freezes player",Vector3.Distance(bodyAt,workshop.Player.position)<.001f);workshop.TestInput(Vector2.zero);workshop.SetPaused(false);
   workshop.RestartTest();yield return new WaitForSecondsRealtime(.2f);
   var box=workshop.GetComponentsInChildren<Rigidbody>().FirstOrDefault(b=>b.name.StartsWith("Box "));
   WorkshopCheck(report,"restart restores authored box and keeps map",box!=null&&Vector2.Distance(new Vector2(box.position.x,box.position.z),new Vector2(ClubWorkshop.World(2,2).x,ClubWorkshop.World(2,2).z))<.05f&&ClubWorkshop.Signature(workshop.Layout)==authored);
   yield return WorkshopPush();yield return WorkshopWalk(3,2);yield return WorkshopWalk(3,3);yield return WorkshopWalk(5,3);yield return WorkshopWalk(6,3);yield return WorkshopWalk(6,4);
   WorkshopCheck(report,"pressure plate level completes",workshop.Completed);
   yield return Shot("workshop-03-plate-complete");
   if(!workshop.Completed){report.Add("Workshop stopped at plate stage at "+workshop.Player.position);CloseWorkshop();yield break;}
   workshop.NextStage();yield return null;
   WorkshopCheck(report,"third stage starts fresh as automatic challenge",workshop.Stage==2&&workshop.Playing&&workshop.Automatic&&ClubWorkshop.Signature(workshop.Layout)==ClubWorkshop.Signature(ClubWorkshop.Preset(2)));
   string initial=ClubWorkshop.Signature(workshop.Layout);yield return WorkshopWaitRobot(9);
   WorkshopCheck(report,"initial robot gets stuck and stops",workshop.RunFailed&&!workshop.Completed&&ClubWorkshop.Signature(workshop.Layout)==initial);
   yield return Shot("robot-01-stuck");WorkshopCheck(report,"sandbox waits for challenge completion",!workshop.StartFreeBuild());
   workshop.SetPlaying(false);WorkshopClick(4,1,true);WorkshopArrow(6,1,0);
   string shortcut=ClubWorkshop.Signature(workshop.Layout);WorkshopCheck(report,"arrow placement changes authored route",workshop.Layout.arrows[ClubWorkshop.Index(6,1)]==1);
   WorkshopCheck(report,"untested route cannot publish",!workshop.Publish());
   workshop.SetPlaying(true);workshop.TestInput(Vector2.left);yield return WorkshopWaitRobot(12);
   WorkshopCheck(report,"robot finishes bypass without directional input",workshop.Completed&&workshop.Verified&&!workshop.BoxOnPlate&&workshop.ChallengeCleared);
   yield return Shot("robot-02-bypass-win");
   if(!workshop.Completed){report.Add("Robot stopped at "+workshop.Player.position+" "+workshop.Message);CloseWorkshop();yield break;}
   workshop.SetPlaying(false);workshop.ResetLayout();WorkshopClick(3,2,true);
   WorkshopArrow(2,1,0);WorkshopClick(2,1);WorkshopCheck(report,"clicking arrow rotates direction",workshop.Layout.arrows[ClubWorkshop.Index(2,1)]==2);workshop.Undo();
   workshop.SelectTool(WorkshopTool.Move);WorkshopClick(2,1);WorkshopClick(1,2);
   WorkshopCheck(report,"arrow can move and undo",workshop.Layout.arrows[ClubWorkshop.Index(1,2)]==1&&workshop.Layout.arrows[ClubWorkshop.Index(2,1)]==0);workshop.Undo();
   WorkshopArrow(2,2,1);WorkshopArrow(3,2,2);WorkshopArrow(3,1,3);
   workshop.SetPlaying(true);yield return WorkshopWaitRobot(16);
   WorkshopCheck(report,"loop is detected with useful feedback",workshop.RunFailed&&workshop.Message.Contains("绕圈"));yield return Shot("robot-03-loop");
   workshop.SetPlaying(false);workshop.ResetLayout();WorkshopClick(2,1,true);workshop.SetPlaying(true);yield return WorkshopWaitRobot(6);
   WorkshopCheck(report,"missing floor stops robot safely",workshop.RunFailed&&workshop.Falls>0);
   workshop.SetPlaying(false);workshop.ResetLayout();
   WorkshopArrow(3,1,0);WorkshopArrow(3,2,3);WorkshopArrow(2,2,0);WorkshopArrow(2,4,1);
   workshop.SelectTool(WorkshopTool.Floor);WorkshopClick(5,4);
   string pressure=ClubWorkshop.Signature(workshop.Layout);yield return Shot("robot-pressure-edit");workshop.SetPlaying(true);
   yield return new WaitForSecondsRealtime(.3f);var pausedAt=workshop.Player.position;workshop.SetPaused(true);yield return new WaitForSecondsRealtime(.25f);
   WorkshopCheck(report,"robot pause freezes movement",Vector3.Distance(pausedAt,workshop.Player.position)<.001f);workshop.SetPaused(false);
   yield return WorkshopWaitRobot(18);
   WorkshopCheck(report,"robot pushes box and completes pressure route",workshop.Completed&&workshop.BoxOnPlate&&workshop.DoorOpen&&ClubWorkshop.Signature(workshop.Layout)==pressure);
   yield return Shot("robot-04-pressure-win");
   if(!workshop.Completed){report.Add("Pressure route stopped at "+workshop.Player.position+" "+workshop.Message);CloseWorkshop();yield break;}
   workshopFree.onClick.Invoke();yield return null;
   WorkshopCheck(report,"sandbox unlocks after challenge",workshop.Stage==3&&!workshop.Playing&&workshop.Layout.mode==2);
   workshop.SelectTool(WorkshopTool.Floor);foreach(int x in new[]{3,4,5,6})WorkshopClick(x,1);WorkshopClick(6,2);WorkshopArrow(6,1,0);
   string custom=ClubWorkshop.Signature(workshop.Layout);
   WorkshopCheck(report,"draft saves before verification",workshop.SaveDraft()&&File.Exists(WorkshopPath));
   WorkshopCheck(report,"untested sandbox cannot publish",!workshop.Publish());yield return Shot("robot-05-sandbox-edit");
   workshop.SetPlaying(true);yield return WorkshopWaitRobot(12);
   WorkshopCheck(report,"sandbox robot reaches exit automatically",workshop.Completed&&workshop.Verified);
   bool published=workshop.Publish();WorkshopCheck(report,"verified automatic map saves for showcase",published&&HasWorkshopShowcase());
   workshop.SetPlaying(false);workshop.SelectTool(WorkshopTool.Floor);WorkshopClick(0,0);
   WorkshopCheck(report,"editing invalidates verification",!workshop.Verified&&!workshop.Publish());
   workshop.Undo();WorkshopCheck(report,"undo restores tested automatic layout",workshop.Verified&&ClubWorkshop.Signature(workshop.Layout)==custom);
   WorkshopClick(0,0);workshop.SaveDraft();string revisedDraft=ClubWorkshop.Signature(workshop.Layout);
   var oldWorld=workshop;var oldTexture=workshop.View;CloseWorkshop();yield return null;
   WorkshopCheck(report,"exit releases scene and texture",workshop==null&&oldWorld==null&&oldTexture==null&&title&&!modal);
   OpenWorkshop();yield return null;WorkshopCheck(report,"next visitor starts first tutorial",workshop.Stage==0&&workshop.Playing);
   workshop.SkipToBuilding();WorkshopCheck(report,"next visitor receives original challenge",workshop.Stage==2&&workshop.Automatic&&ClubWorkshop.Signature(workshop.Layout)==ClubWorkshop.Signature(ClubWorkshop.Preset(2))&&!workshop.Dirty);
   CloseWorkshop();yield return null;var beforeReplay=JsonUtility.FromJson<WorkshopFile>(File.ReadAllText(WorkshopPath));
   WorkshopCheck(report,"untouched challenge preserves old draft",ClubWorkshop.Signature(beforeReplay.draft)==revisedDraft);
   OpenWorkshop();yield return null;workshop.SetPaused(true);workshopResume.onClick.Invoke();
   WorkshopCheck(report,"draft restores arrow directions and sandbox mode",workshop.Stage==3&&workshop.Automatic&&ClubWorkshop.Signature(workshop.Layout)==revisedDraft);CloseWorkshop();yield return null;
   OpenWorkshop(true);yield return null;yield return WorkshopWaitRobot(12);
   WorkshopCheck(report,"saved showcase really runs itself",workshop.Completed&&workshop.ViewingPublished&&ClubWorkshop.Signature(workshop.Layout)==custom);
   yield return Shot("robot-06-saved-showcase");CloseWorkshop();yield return null;
   var saved=JsonUtility.FromJson<WorkshopFile>(File.ReadAllText(WorkshopPath));
   WorkshopCheck(report,"showcase playback preserves newer draft",ClubWorkshop.Signature(saved.draft)==revisedDraft&&ClubWorkshop.Signature(saved.published)==custom);
   string currentSave=File.ReadAllText(WorkshopPath);var legacy=ClubWorkshop.Preset(3);legacy.mode=0;legacy.arrows=null;
   File.WriteAllText(WorkshopPath,JsonUtility.ToJson(new WorkshopFile{draft=legacy,published=legacy},true));
   OpenWorkshop(true);yield return null;WorkshopCheck(report,"legacy manual showcase stays playable",!workshop.Automatic&&workshop.Playing);
   yield return WorkshopWalk(2,1);WorkshopCheck(report,"legacy manual movement works",Vector3.Distance(workshop.Player.position,ClubWorkshop.World(2,1)+Vector3.up*.65f)<.5f);
   CloseWorkshop();yield return null;File.WriteAllText(WorkshopPath,currentSave);
   ShowCredits();ShowWorks();yield return null;var showcase=overlay.GetComponentsInChildren<Button>().FirstOrDefault(b=>b.name=="Button:玩你留的那一关");
   WorkshopCheck(report,"ending offers player automatic map",showcase!=null);
   if(showcase!=null){showcase.onClick.Invoke();yield return null;WorkshopCheck(report,"ending starts automatic showcase",workshop!=null&&workshop.Automatic&&workshop.Playing&&ClubWorkshop.Signature(workshop.Layout)==custom);CloseWorkshop();}
   ShowTitle();
  }
 }
}
