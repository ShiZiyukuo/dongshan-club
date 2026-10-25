using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace DongshanClub {
 public partial class CampusGame {
  int playgroundQaErrors;
  void PlaygroundCheck(List<string> report,string label,bool passed){
   report.Add("3D playground "+label+": "+passed);if(!passed){playgroundQaErrors++;Debug.LogError("PLAYGROUND_QA_FAILED "+label);}
  }
  IEnumerator PlaygroundWalk(Vector2 target,float seconds=7,float tolerance=.18f){
   float deadline=Time.realtimeSinceStartup+seconds;
   while(playground!=null&&!playground.Finished&&Time.realtimeSinceStartup<deadline){
    var at=playground.Player.position;var delta=target-new Vector2(at.x,at.z);
    if(delta.magnitude<tolerance)break;
    playground.TestInput(delta.normalized);yield return new WaitForFixedUpdate();
   }
   if(playground!=null)playground.TestInput(Vector2.zero);
   yield return new WaitForSecondsRealtime(.22f);
  }
  IEnumerator RunPlaygroundQA(List<string> report){
   playgroundQaErrors=0;ShowTitle();yield return null;
   var titleButton=stage.GetComponentsInChildren<Button>().FirstOrDefault(b=>b.name=="Button:修一关，再留一关");
   PlaygroundCheck(report,"direct entry exists",titleButton!=null);if(titleButton==null)yield break;
   OpenPlayground();yield return new WaitForSecondsRealtime(.35f);
   PlaygroundCheck(report,"real camera and physics",playground!=null&&playground.View.IsCreated()&&playground.Player.GetComponent<CapsuleCollider>()!=null);
   if(playground==null)yield break;
   yield return Shot("playground-01-start");
   playground.Interact();PlaygroundCheck(report,"E out of range rejected",!playground.GateOpened);
   yield return PlaygroundWalk(new Vector2(-2.5f,3.8f));
   PlaygroundCheck(report,"WASD movement",Vector3.Distance(playground.Player.position,playground.SwitchPosition+Vector3.up)<1.9f&&playground.DistanceMoved>3);
   playground.SetPaused(true);var pausedAt=playground.Player.position;var pausedBox=playground.Box.position;
   playground.TestInput(Vector2.right);yield return new WaitForSecondsRealtime(.3f);
   PlaygroundCheck(report,"pause freezes both bodies",Vector3.Distance(pausedAt,playground.Player.position)<.001f&&Vector3.Distance(pausedBox,playground.Box.position)<.001f);
   playground.TestInput(Vector2.zero);playground.SetPaused(false);
   playground.Interact();yield return new WaitForSecondsRealtime(.7f);
   PlaygroundCheck(report,"nearby switch opens physical gate",playground.GateOpened);
   report.Add("3D playground at switch="+playground.Player.position);
   yield return Shot("playground-02-switch");
   yield return PlaygroundWalk(new Vector2(0,3.5f));
   yield return PlaygroundWalk(new Vector2(0,11.05f));
   report.Add("3D playground jump start="+playground.Player.position+" grounded="+playground.Grounded);
   float launchZ=playground.Player.position.z;playground.TestInput(Vector2.up,true);
   yield return PlaygroundWalk(new Vector2(0,17),4);
   report.Add("3D playground jump launch="+launchZ+" landing="+playground.Player.position+" jumped="+playground.HasJumped+" falls="+playground.Falls);
   PlaygroundCheck(report,"jump traverses gap",launchZ>10.5f&&playground.HasJumped&&playground.Player.position.z>16&&playground.Falls==0);
   yield return new WaitForSecondsRealtime(.3f);
   PlaygroundCheck(report,"landing checkpoint",playground.Checkpoint.z>16);
   yield return Shot("playground-03-gap-crossed");
   int oldFalls=playground.Falls;float fallDeadline=Time.realtimeSinceStartup+4;
   while(playground.Falls==oldFalls&&Time.realtimeSinceStartup<fallDeadline){playground.TestInput(Vector2.right);yield return new WaitForFixedUpdate();}
   playground.TestInput(Vector2.zero);yield return new WaitForSecondsRealtime(.3f);
   PlaygroundCheck(report,"fall returns to nearby checkpoint",playground.Falls==oldFalls+1&&Vector3.Distance(playground.Player.position,playground.Checkpoint)<.5f);
   yield return PlaygroundWalk(new Vector2(1,19.2f));
   float beforePush=playground.Box.position.z;float pushDeadline=Time.realtimeSinceStartup+7;
   while(!playground.BoxOnPlate&&Time.realtimeSinceStartup<pushDeadline){
    float dx=1-playground.Player.position.x;playground.TestInput(new Vector2(Mathf.Clamp(dx*2,-.4f,.4f),1));yield return new WaitForFixedUpdate();
   }
   playground.TestInput(Vector2.zero);yield return new WaitForSecondsRealtime(.45f);
   PlaygroundCheck(report,"physical box pushes and holds plate",playground.BoxOnPlate&&playground.Box.position.z>beforePush+3);
   report.Add("3D playground push positions player="+playground.Player.position+" box="+playground.Box.position);
   yield return Shot("playground-04-box-plate");
   yield return PlaygroundWalk(new Vector2(-2,playground.Player.position.z));
   yield return PlaygroundWalk(new Vector2(-2,32));
   yield return PlaygroundWalk(new Vector2(0,34),5);
   PlaygroundCheck(report,"complete via traversal",playground.Finished&&playground.DistanceMoved>30);
   yield return Shot("playground-05-finish");
   var oldWorld=playground;var oldView=playground.View;ClosePlayground();yield return null;
   PlaygroundCheck(report,"exit frees world and render target",playground==null&&oldWorld==null&&oldView==null&&!modal&&title);
   // Re-entry must create a new level without inheriting a box or completed gate.
   OpenPlayground();yield return new WaitForSecondsRealtime(.15f);
   PlaygroundCheck(report,"replay resets physics state",playground!=null&&!playground.GateOpened&&!playground.Finished&&playground.Falls==0);
   CloseModal();yield return null;PlaygroundCheck(report,"close routes to title",playground==null&&title&&!modal);
  }
 }
}
