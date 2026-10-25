using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DongshanClub {
 public partial class ClubWorkshop {
  public bool Automatic {get{return Layout!=null&&Layout.mode>0;}}
  public bool RunFailed {get;private set;}
  public bool ChallengeCleared {get;private set;}
  public bool SlowMotion {get;private set;}
  public int ArrowFacing {get;private set;}
  public int RunSteps {get;private set;}
  public string RunStatus {get{return !Automatic?"WASD / 方向键移动":!Playing?"编辑：摆好箭头，再看它自己跑":Completed?"它自己跑到出口了":RunFailed?"试跑停住了 · Tab 回去改改":Paused?"已暂停":(SlowMotion?"慢慢看":"正在试跑")+" · 已走 "+RunSteps+" 格";}}
  int ArrowLimit {get{return Layout.mode==1?6:12;}}
  float RobotSpeed {get{return SlowMotion?1.25f:2.6f;}}
  int robotX=1,robotZ=1,robotDirection=1,targetX=2,targetZ=1;
  float stalledFor,runElapsed;Vector3 previousRobotPosition;
  readonly Dictionary<int,int> robotVisits=new Dictionary<int,int>();
  static readonly Vector2Int[] RobotDirections={Vector2Int.up,Vector2Int.right,Vector2Int.down,Vector2Int.left};
  public static string DirectionLabel(int direction){return new[]{"↑","→","↓","←"}[(direction+4)%4];}
  public void RotateArrow(int amount){ArrowFacing=(ArrowFacing+amount+4)%4;Tell("下一块转向地砖："+DirectionLabel(ArrowFacing)+"。也可以点已有箭头转一格。 ");}
  public void ToggleSlow(){SlowMotion=!SlowMotion;Changed?.Invoke();}
  bool PlaceArrow(int cell){
   if(!Automatic)return false;
   if(Layout.floor[cell]==0){Tell("箭头要放在地板上。先铺路，再告诉它往哪走。 ");return false;}
   if(Layout.arrows[cell]==0&&Layout.arrows.Count(v=>v>0)>=ArrowLimit){Tell("转向地砖用完了。挪一块过来，或者换条路线。 ");return false;}
   Remember();Layout.arrows[cell]=Layout.arrows[cell]>0?Layout.arrows[cell]%4+1:ArrowFacing+1;
   Edited("它踩到这块地砖，就会朝 "+DirectionLabel(Layout.arrows[cell]-1)+" 走。点试跑看看。 ");return true;
  }
  void BuildRobotDetails(){
   if(!Automatic)return;
   Player.GetComponent<Renderer>().sharedMaterial=blueMat;
   var head=Cube("Robot antenna",Player.position+Vector3.up*.83f,new Vector3(.05f,.35f,.05f),wallMat,false);head.transform.SetParent(Player.transform,true);
   var light=Shape("Robot antenna light",PrimitiveType.Sphere,Player.position+Vector3.up*1.04f,Vector3.one*.18f,goldMat,Player.transform,false);
   foreach(int i in Enumerable.Range(0,48)){
    if(Layout.arrows[i]==0)continue;int direction=Layout.arrows[i]-1;
    var arrow=new GameObject("Turn arrow "+i);arrow.transform.SetParent(boardRoot.transform,false);arrow.transform.position=World(i)+Vector3.up*.075f;
    arrow.transform.rotation=Quaternion.Euler(0,direction*90,0);
    AddArrowBar(arrow.transform,new Vector3(0,0,-.11f),new Vector3(.13f,.035f,.68f),0);
    AddArrowBar(arrow.transform,new Vector3(-.15f,0,.13f),new Vector3(.11f,.035f,.47f),42);
    AddArrowBar(arrow.transform,new Vector3(.15f,0,.13f),new Vector3(.11f,.035f,.47f),-42);
   }
  }
  void AddArrowBar(Transform parent,Vector3 local,Vector3 size,float angle){
   var bar=Shape("Arrow stroke",PrimitiveType.Cube,Vector3.zero,size,goldMat,parent,false);
   bar.transform.localPosition=local;bar.transform.localRotation=Quaternion.Euler(0,angle,0);
  }
  void ResetRobotRun(){
   RunFailed=false;RunSteps=0;stalledFor=0;runElapsed=0;robotX=1;robotZ=1;robotDirection=1;targetX=2;targetZ=1;
   robotVisits.Clear();if(Player!=null){previousRobotPosition=Player.position;if(Automatic)Player.rotation=Quaternion.LookRotation(Vector3.right);}
  }
  Vector2 RobotMovement(float dt){
   runElapsed+=dt;
   Vector3 delta=World(targetX,targetZ)-Player.position;delta.y=0;
   if(delta.magnitude<.11f){
    robotX=targetX;robotZ=targetZ;RunSteps++;
    if(robotX<0||robotX>=8||robotZ<0||robotZ>=6){FailRobot("它走出地图了。\n同学：得早点给它一个转弯的提示。 ");return Vector2.zero;}
    int cell=Index(robotX,robotZ);if(Layout.arrows[cell]>0)robotDirection=Layout.arrows[cell]-1;
    int state=cell*4+robotDirection;int visits;robotVisits.TryGetValue(state,out visits);robotVisits[state]=visits+1;
    if(visits>=2){FailRobot("小叶子：这儿刚才是不是来过？\n同学：它绕圈了。改一个箭头试试。 ");return Vector2.zero;}
    var step=RobotDirections[robotDirection];targetX=robotX+step.x;targetZ=robotZ+step.y;
    delta=World(targetX,targetZ)-Player.position;delta.y=0;
   }
   var moved=Player.position-previousRobotPosition;moved.y=0;previousRobotPosition=Player.position;
   if(moved.magnitude<.006f&&Player.position.y>.2f)stalledFor+=dt;else stalledFor=0;
   if(stalledFor>1.25f){
    bool boxAhead=boxes.Any(b=>Vector3.Distance(b.position,Player.position)<1.5f);
    FailRobot(boxAhead?"箱子推不动了。\n小叶子：它也不知道换个方向。\n同学：停下来，改改箭头或者挪一下箱子。":"它顶着墙不走了。\n部长：你别替它按方向键。\n同学：回编辑里放个转向地砖吧。 ");return Vector2.zero;
   }
   if(runElapsed>65){FailRobot("它还没找到出口。先停一会儿，检查一下这条路线。 ");return Vector2.zero;}
   return new Vector2(delta.x,delta.z).normalized;
  }
  void FailRobot(string message){RunFailed=true;Freeze(true);if(cursor!=null){cursor.position=World(robotX,robotZ);cursor.gameObject.SetActive(true);}Tell(message);}
  public bool StartFreeBuild(){
   if(!ChallengeCleared&&!(Stage==2&&Completed)){Tell("先让它跑通第三关，再自己出一关。 ");return false;}
   if(Dirty&&!SaveDraft())return false;
   Stage=3;ViewingPublished=false;Playing=false;Paused=false;Completed=false;Tool=WorkshopTool.Floor;undo.Clear();movingCell=-1;
   Layout=Preset(3);sessionBaseline=Signature(Layout);Rebuild();Tell("同学：刚才那关跑通了。现在自己出一关呗。\n它还是从蓝色起点向右走，箭头怎么摆，你来。 ");return true;
  }
 }
}
