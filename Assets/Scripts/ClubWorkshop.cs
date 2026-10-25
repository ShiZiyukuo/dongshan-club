using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DongshanClub {
 [Serializable] public class WorkshopLevel {
  public int version=1;
  public int[] floor=new int[48],items=new int[48];
  public int mode;
  public int[] arrows=new int[48];
  public WorkshopLevel Copy(){return new WorkshopLevel{mode=mode,floor=(int[])floor.Clone(),items=(int[])items.Clone(),arrows=arrows==null?new int[48]:(int[])arrows.Clone()};}
 }
 [Serializable] public class WorkshopFile {
  public int version=1,progress;
  public WorkshopLevel draft,published;
 }
 public enum WorkshopTool { Floor,Wall,Box,Plate,Door,Move,Erase,Arrow }

 // Layout data is authoritative. Each test creates fresh physics objects from that layout.
 public partial class ClubWorkshop : MonoBehaviour {
  public const int Width=8,Height=6;const int Layer=30;const float Cell=1.6f;
  public Action Changed;
  public bool Testing,Playing,Paused,Completed;
  public bool ViewingPublished {get;private set;}
  public int Stage {get;private set;}
  public int Falls {get;private set;}
  public WorkshopLevel Layout {get;private set;}
  public WorkshopTool Tool=WorkshopTool.Floor;
  public string Message="",Hover="",SaveError="";
  public RenderTexture View;
  public Camera BoardCamera {get;private set;}
  public Rigidbody Player {get;private set;}
  public bool DoorOpen {get;private set;}
  public bool BoxOnPlate {get;private set;}
  public int UndoCount {get{return undo.Count;}}
  public bool Verified {get{return verifiedStage==Stage&&verifiedSignature==Signature(Layout);}}
  public bool HasPublished {get{return stored.published!=null;}}
  public bool HasDraft {get{return stored.draft!=null;}}
  public string SavePath {get;private set;}
  public bool Dirty {get{return Stage>=2&&!ViewingPublished&&Signature(Layout)!=Signature(stored.draft)&&Signature(Layout)!=sessionBaseline;}}
  public bool DraftMatchesLayout {get{return HasDraft&&Signature(Layout)==Signature(stored.draft);}}
  public string StageName {get{return ViewingPublished?"展示 / 留下来的那一关":Stage==0?"01 / 补一块地板":Stage==1?"02 / 让门开着":Stage==2?"03 / 你别教它，让它试":"额外 / 自己出一关";}}
  public string Goal {get{return Stage==0?"走到金色出口。路不通？Tab 切到编辑，补上地板。":Stage==1?"让角色走到出口。用箱子压住机关，或另想个办法。":Automatic?"小机器人只会往前走。摆转向地砖，让它自己走到金色出口。":"这是以前保存的手动关卡。WASD移动到出口即可。";}}
  readonly List<WorkshopLevel> undo=new List<WorkshopLevel>();
  readonly List<Material> materials=new List<Material>();
  readonly List<Rigidbody> boxes=new List<Rigidbody>();
  readonly List<Transform> doors=new List<Transform>();
  readonly List<Renderer> plateRenderers=new List<Renderer>();
  readonly Dictionary<Rigidbody,Vector3> pausedVelocities=new Dictionary<Rigidbody,Vector3>();
  WorkshopFile stored=new WorkshopFile();GameObject boardRoot;Transform cursor;
  Material floorMat,wallMat,boxMat,blueMat,goldMat,redMat,greenMat,darkMat,cursorMat;
  PhysicMaterial slide;Vector2 testMove;int movingCell=-1;bool movingFloor,movingArrow,firstDrop;
  string verifiedSignature="",sessionBaseline="";int verifiedStage=-1;float gateLift;
  public static int Index(int x,int z){return z*Width+x;}
  public static Vector3 World(int x,int z){return new Vector3((x-3.5f)*Cell,0,(z-2.5f)*Cell);}
  public static Vector3 World(int index){return World(index%Width,index/Width);}
  public static int StartCell {get{return Index(1,1);}}
  public static int GoalCell {get{return Index(6,4);}}
  public static string Signature(WorkshopLevel level){return level==null?"":level.mode+":"+string.Join("",level.floor)+":"+string.Join("",level.items)+":"+string.Join("",level.arrows??new int[48]);}

  public void Build(string savePath,bool published=false){
   SavePath=savePath;ReadStore();
   floorMat=Mat(new Color32(159,179,161,255));wallMat=Mat(new Color32(84,110,104,255));boxMat=Mat(new Color32(242,222,166,255));
   blueMat=Mat(new Color32(77,188,206,255));goldMat=Mat(new Color32(245,184,78,255));redMat=Mat(new Color32(190,101,89,255));
   greenMat=Mat(new Color32(112,206,136,255));darkMat=Mat(new Color32(39,60,63,255));cursorMat=Mat(new Color32(250,206,107,255));
   slide=new PhysicMaterial("Workshop sliding"){dynamicFriction=.08f,staticFriction=.08f,frictionCombine=PhysicMaterialCombine.Minimum};
   var cam=new GameObject("WorkshopCamera");cam.transform.SetParent(transform,false);BoardCamera=cam.AddComponent<Camera>();
   BoardCamera.enabled=false;BoardCamera.cullingMask=1<<Layer;BoardCamera.clearFlags=CameraClearFlags.SolidColor;
   BoardCamera.backgroundColor=new Color32(27,44,48,255);BoardCamera.orthographic=true;BoardCamera.orthographicSize=4.9f;
   BoardCamera.nearClipPlane=.1f;BoardCamera.farClipPlane=60;cam.transform.position=new Vector3(0,13,-9);
   cam.transform.LookAt(new Vector3(0,0,.5f));
   View=new RenderTexture(1120,550,24,RenderTextureFormat.ARGB32){antiAliasing=2};View.Create();BoardCamera.targetTexture=View;
   var sun=new GameObject("WorkshopSun");sun.transform.SetParent(transform,false);var light=sun.AddComponent<Light>();
   light.type=LightType.Directional;light.intensity=1.1f;light.cullingMask=1<<Layer;light.shadows=LightShadows.Soft;light.shadowStrength=.4f;
   sun.transform.rotation=Quaternion.Euler(55,-35,0);
   if(published&&HasPublished){Layout=stored.published.Copy();Stage=Layout.mode==1?2:3;ViewingPublished=true;verifiedSignature=Signature(Layout);verifiedStage=Stage;Playing=true;Message="这是之前留的那一关。先看它跑一遍。";}
   else {Stage=0;Layout=Preset(0);Playing=true;Message="部长：能玩了，你试试。\n小叶子：行，先走走看。";}
   sessionBaseline=Signature(Layout);
   Rebuild();Changed?.Invoke();
  }
  Material Mat(Color color){var mat=new Material(Shader.Find("Standard")){color=color};mat.SetFloat("_Glossiness",.08f);materials.Add(mat);return mat;}
  GameObject Shape(string label,PrimitiveType type,Vector3 position,Vector3 size,Material material,Transform parent,bool solid=true){
   var obj=GameObject.CreatePrimitive(type);obj.name=label;obj.layer=Layer;obj.transform.SetParent(parent,false);obj.transform.position=position;
   obj.transform.localScale=size;obj.GetComponent<Renderer>().sharedMaterial=material;
   if(!solid){obj.GetComponent<Collider>().enabled=false;Destroy(obj.GetComponent<Collider>());}else obj.GetComponent<Collider>().sharedMaterial=slide;
   return obj;
  }
  GameObject Cube(string label,Vector3 position,Vector3 size,Material material,bool solid=true){return Shape(label,PrimitiveType.Cube,position,size,material,boardRoot.transform,solid);}
  void Rebuild(){
   if(boardRoot!=null){boardRoot.SetActive(false);Destroy(boardRoot);}boxes.Clear();doors.Clear();plateRenderers.Clear();pausedVelocities.Clear();Player=null;
   boardRoot=new GameObject("WorkshopBoard");boardRoot.transform.SetParent(transform,false);gateLift=0;DoorOpen=false;BoxOnPlate=false;
   for(int i=0;i<48;i++){
    Vector3 at=World(i);
    Cube("Grid "+i,at+Vector3.down*.35f,new Vector3(1.52f,.05f,1.52f),darkMat,false);
    if(Layout.floor[i]==0)continue;
    Cube("Floor "+i,at+Vector3.down*.18f,new Vector3(1.54f,.36f,1.54f),floorMat);
    int item=Layout.items[i];
    if(item==1)Cube("Wall "+i,at+Vector3.up*.7f,new Vector3(1.52f,1.4f,1.52f),wallMat);
    if(item==2){
     var obj=Cube("Box "+i,at+Vector3.up*.55f,Vector3.one*1.05f,boxMat);
     var body=obj.AddComponent<Rigidbody>();body.mass=1.6f;body.drag=5;body.constraints=RigidbodyConstraints.FreezeRotation;
     body.interpolation=RigidbodyInterpolation.Interpolate;body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;body.isKinematic=!Playing;boxes.Add(body);
     Cube("Box stripe "+i,at+Vector3.up*1.082f,new Vector3(.18f,.016f,.98f),goldMat,false).transform.SetParent(obj.transform,true);
    }
    if(item==3){var plate=Cube("Plate "+i,at+Vector3.up*.025f,new Vector3(1.35f,.05f,1.35f),blueMat,false);plateRenderers.Add(plate.GetComponent<Renderer>());}
    if(item==4)doors.Add(Cube("Door "+i,at+Vector3.up*.8f,new Vector3(1.53f,1.6f,1.53f),redMat).transform);
   }
   Cube("Start",World(StartCell)+Vector3.up*.026f,new Vector3(1.25f,.05f,1.25f),blueMat,false);
   Cube("Exit",World(GoalCell)+Vector3.up*.026f,new Vector3(1.25f,.05f,1.25f),goldMat,false);
   for(int side=0;side<2;side++)Cube("Exit post",World(GoalCell)+new Vector3(side==0?-.58f:.58f,.36f,.45f),new Vector3(.1f,.72f,.1f),goldMat,false);
   var person=Shape("WorkshopPlayer",PrimitiveType.Capsule,World(StartCell)+Vector3.up*.75f,new Vector3(.5f,.65f,.5f),goldMat,boardRoot.transform);
   Player=person.AddComponent<Rigidbody>();Player.mass=3;Player.constraints=RigidbodyConstraints.FreezeRotation;Player.interpolation=RigidbodyInterpolation.Interpolate;
   Player.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;Player.isKinematic=!Playing;
   foreach(float x in new[]{-.15f,.15f}){
    var eye=Shape("Eye",PrimitiveType.Sphere,Vector3.zero,Vector3.one*.07f,darkMat,person.transform,false);
    eye.transform.localPosition=new Vector3(x,.35f,.43f);eye.transform.localScale=Vector3.one*.14f;
   }
   var marker=new GameObject("Cursor");marker.transform.SetParent(boardRoot.transform,false);cursor=marker.transform;
   for(int side=0;side<4;side++){
    var bar=Shape("Cursor edge",PrimitiveType.Cube,Vector3.zero,side<2?new Vector3(1.5f,.04f,.045f):new Vector3(.045f,.04f,1.5f),cursorMat,cursor,false);
    bar.transform.localPosition=side==0?new Vector3(0,.07f,-.75f):side==1?new Vector3(0,.07f,.75f):side==2?new Vector3(-.75f,.07f,0):new Vector3(.75f,.07f,0);
   }
   cursor.gameObject.SetActive(false);Physics.SyncTransforms();
   BuildRobotDetails();ResetRobotRun();
  }
  public static WorkshopLevel Preset(int stage){
   var level=new WorkshopLevel();
   if(stage==0){
    for(int x=1;x<=6;x++)level.floor[Index(x,1)]=1;
    for(int z=1;z<=4;z++)level.floor[Index(6,z)]=1;
    level.floor[Index(4,1)]=0;
   }else if(stage==1){
    for(int z=1;z<=4;z++)for(int x=1;x<=6;x++)level.floor[Index(x,z)]=1;
    for(int z=1;z<=4;z++)level.items[Index(4,z)]=z==3?4:1;
    level.items[Index(2,3)]=3;
   }else if(stage==2){
    level.mode=1;
    for(int z=1;z<=4;z++)for(int x=1;x<=6;x++)level.floor[Index(x,z)]=1;
    level.floor[Index(5,4)]=0;
    for(int z=1;z<=4;z++)level.items[Index(4,z)]=z==4?4:1;
    level.items[Index(3,2)]=2;level.items[Index(3,3)]=3;
   }else {level.mode=2;foreach(int i in new[]{StartCell,Index(2,1),Index(1,2),GoalCell,Index(5,4),Index(6,3)})level.floor[i]=1;}
   return level;
  }
  public void SelectTool(WorkshopTool tool){
   if(Stage==0&&tool!=WorkshopTool.Floor&&tool!=WorkshopTool.Move&&tool!=WorkshopTool.Erase){Tell("先补好这块地板，下一关再试机关。 ");return;}
   if(tool==WorkshopTool.Arrow&&!Automatic){Tell("转向地砖在第三关的小机器人试跑里使用。 ");return;}
   Tool=tool;movingCell=-1;Tell(tool==WorkshopTool.Arrow?"点地板放箭头。Q / E 旋转方向；再点已有箭头也能转。":tool==WorkshopTool.Move?"先点要移动的东西，再点它的新位置。":"选好了。直接点地图上的格子就行。");
  }
  public string Inventory(){
   int floors=Layout.floor.Sum();int[] counts=new int[5];foreach(int item in Layout.items)counts[item]++;
   return "地板 "+floors+" / "+FloorLimit+"　箱子 "+counts[2]+" / 4\n门 / 压板各1组"+(Automatic?"　转向 "+Layout.arrows.Count(v=>v>0)+" / "+ArrowLimit:"");
  }
  int FloorLimit {get{return Stage==0?9:Stage==1?27:32;}}
  bool Locked(int i){return i==StartCell||i==GoalCell;}
  void Remember(){undo.Add(Layout.Copy());if(undo.Count>40)undo.RemoveAt(0);}
  public bool ApplyCell(int i,bool erase=false){
   if(Playing||Paused||i<0||i>=48)return false;
   var tool=erase?WorkshopTool.Erase:Tool;
   if(Locked(i)){Tell("蓝色起点和金色出口先留着。中间怎么搭，你来。 ");return false;}
   if(tool==WorkshopTool.Arrow)return PlaceArrow(i);
   if(tool==WorkshopTool.Move){
    if(movingCell<0){
     if(Layout.floor[i]==0){Tell("这里还没放东西。先点一块地板或一个物件。 ");return false;}
     movingCell=i;movingArrow=Layout.items[i]==0&&Layout.arrows[i]>0;movingFloor=Layout.items[i]==0&&!movingArrow;Tell("拿起来了。点新位置；换工具可以放下。 ");return false;
    }
    if(i==movingCell){movingCell=-1;Tell("放回原处了。 ");return false;}
    if(movingFloor?(Layout.floor[i]!=0):movingArrow?(Layout.floor[i]==0||Layout.arrows[i]>0):(Layout.floor[i]==0||Layout.items[i]!=0)){Tell(movingFloor?"地板要放在空格上。":"这里放不下，换一块地板试试。 ");return false;}
    if(Stage==0&&Preset(0).floor[movingCell]!=0){Tell("原来的路先留着。可以移动你补上的地板。 ");movingCell=-1;return false;}
    Remember();if(movingFloor){Layout.floor[movingCell]=0;Layout.floor[i]=1;}else if(movingArrow){Layout.arrows[i]=Layout.arrows[movingCell];Layout.arrows[movingCell]=0;}else{Layout.items[i]=Layout.items[movingCell];Layout.items[movingCell]=0;}
    movingCell=-1;Edited("挪好了。切去试玩，看看这个位置行不行。 ");return true;
   }
   if(tool==WorkshopTool.Erase){
    if(Layout.floor[i]==0){Tell("这里本来就是空的。 ");return false;}
    if(Stage==0&&Preset(0).floor[i]!=0){Tell("这轮先保留原来的路。补上的地板可以删，也可以撤回。 ");return false;}
    Remember();if(Layout.items[i]!=0)Layout.items[i]=0;else if(Layout.arrows[i]>0)Layout.arrows[i]=0;else Layout.floor[i]=0;
    Edited("拿掉了。想反悔可以点撤回。 ");return true;
   }
   if(tool==WorkshopTool.Floor){
    if(Layout.floor[i]!=0){Tell("这里已经有地板了。 ");return false;}
    if(Layout.floor.Sum()>=FloorLimit){Tell("地板用完了。删掉不用的，或者挪一块过来。 ");return false;}
    Remember();Layout.floor[i]=1;Edited(Stage==0?"小叶子：这就铺上了？\n同学：对，切回去试试。":"地板铺好了。 ");return true;
   }
   if(Layout.floor[i]==0){Tell("这里还没地板，先铺一块再放物件。 ");return false;}
   if(Layout.items[i]!=0){Tell("这格已经有东西了。可以先挪开，或者拿掉。 ");return false;}
   int value=(int)tool;int limit=value==1?12:value==2?4:1;
   if(Layout.items.Count(v=>v==value)>=limit){Tell(value>=3?"这一关先只连一组门和压板。可以移动已有的。":"这个物件用完了，可以挪动已有的。 ");return false;}
   Remember();Layout.items[i]=value;Edited(value==2?"箱子放好了。试玩时可以用身体把它推过去。":value>=3?"门和压板已经连好。有东西压着，门就开。":"墙放好了。留条能走的路就行。 ");return true;
  }
  void Edited(string message){Completed=false;Rebuild();Tell(message);}
  public bool Undo(){if(Playing||Paused||undo.Count==0)return false;Layout=undo[undo.Count-1];if(Stage>=2)Stage=Layout.mode==1?2:3;undo.RemoveAt(undo.Count-1);movingCell=-1;Edited("撤回了一步。 ");return true;}
  public void ResetLayout(){if(Playing||Paused)return;Remember();Layout=Preset(Stage);movingCell=-1;Edited("放回这轮最初的样子了。点撤回还能找回来。 ");}
  public void SetPlaying(bool play){
   if(Paused)return;
   bool returnToDraft=ViewingPublished&&!play;
   if(returnToDraft){ViewingPublished=false;Layout=HasDraft?stored.draft.Copy():Layout.Copy();Stage=Layout.mode==1?2:3;sessionBaseline=Signature(Layout);undo.Clear();}
   Playing=play;Completed=false;movingCell=-1;testMove=Vector2.zero;Rebuild();
   if(returnToDraft){Tell("回到你的草稿了。展示版另外留着，试玩它不会覆盖草稿。 ");return;}
   Tell(play?(Automatic?"同学：先别按方向键，让它自己走。\n小叶子：行，看看这次往哪儿去。":"现在可以走了。WASD / 方向键移动，R 重来，Tab 回到编辑。"):"已经放回你摆的那一版了。改好再点试跑，不用重新摆箱子。 ");
  }
  public void NextStage(){
   if(!Completed||Stage>=2)return;
   Stage++;stored.progress=Mathf.Max(stored.progress,Stage);Playing=Stage==2;Completed=false;undo.Clear();
   Layout=Preset(Stage);sessionBaseline=Signature(Layout);Rebuild();WriteStore();
   Tell(Stage==1?"部长：门得有人压住开关。\n小叶子：我走了谁压？\n同学：摆个箱子试试。":"部长：你别教它，让它自己试。\n小叶子：它知道转弯吗？\n同学：……好像不知道。 ");
  }
  public void SkipToBuilding(){
   Stage=2;ViewingPublished=false;Playing=true;Completed=false;Paused=false;movingCell=-1;undo.Clear();Layout=Preset(2);sessionBaseline=Signature(Layout);Rebuild();
   Tell("它只会往前走。先看看哪里卡住，再停下摆转向地砖。 ");
  }
  public void RestartTutorial(){
   if(Stage>=2&&Dirty&&!SaveDraft())return;
   Stage=0;ViewingPublished=false;Playing=true;Paused=false;Completed=false;Tool=WorkshopTool.Floor;movingCell=-1;undo.Clear();testMove=Vector2.zero;
   ChallengeCleared=false;Layout=Preset(0);sessionBaseline=Signature(Layout);Rebuild();Tell("新一轮从这里开始。第三关会重新给一张测试地图，旧作品单独留着。 ");
  }
  public bool SaveDraft(){
   if(ViewingPublished){Tell("这是展示版。切回编辑会打开你的草稿，两个版本各自留着。 ");return false;}
   if(Stage<2){Tell("这两轮先熟悉一下，第三关就能保存布置。 ");return false;}
   var previous=stored.draft;stored.draft=Layout.Copy();stored.progress=2;bool success=WriteStore();if(!success)stored.draft=previous;
   Tell(success?"草稿收好了，下次打开还能接着改。":"没能保存。当前布置还在，可以稍后再试。 ");return success;
  }
  public bool LoadDraft(){
   if(!HasDraft||Playing||Paused)return false;Remember();Layout=stored.draft.Copy();Stage=Layout.mode==1?2:3;ChallengeCleared|=Stage==3;movingCell=-1;Edited("上次保存的草稿读回来了。也可以撤回这次读取。 ");return true;
  }
  public bool Publish(){
   if(ViewingPublished){Tell("这一版已经在展示里了。切回编辑可以继续改草稿。 ");return false;}
   if(Stage<2||!Verified){Tell(Automatic?"先让小机器人自己跑到出口，再把这一版留到展示里。":"先走到出口，再把这一版留到展示里。 ");return false;}
   var previous=stored.published;var previousDraft=stored.draft;stored.published=Layout.Copy();stored.draft=Layout.Copy();stored.progress=2;
   if(!WriteStore()){stored.published=previous;stored.draft=previousDraft;Tell("这次没能存好。关卡还在，可以再试一次。 ");return false;}
   Tell("同学：收到了，摆摊的时候就放这关。\n小叶子：那我也算做了一关了。 ");return true;
  }
  public bool LoadPublished(){
   if(!HasPublished)return false;ViewingPublished=true;Layout=stored.published.Copy();Stage=Layout.mode==1?2:3;verifiedSignature=Signature(Layout);verifiedStage=Stage;
   Playing=true;Completed=false;Paused=false;movingCell=-1;undo.Clear();Rebuild();Tell("这是你留在展示里的那一关。试玩不会修改保存的布置。 ");return true;
  }
  void ReadStore(){
   if(!File.Exists(SavePath))return;
   try{var file=JsonUtility.FromJson<WorkshopFile>(File.ReadAllText(SavePath));
    // JsonUtility can deserialize a null class field as an empty object. A missing showcase must not discard a valid draft.
    if(EmptyRecord(file==null?null:file.draft)&&file!=null)file.draft=null;
    if(EmptyRecord(file==null?null:file.published)&&file!=null)file.published=null;
    if(file!=null){NormalizeLevel(file.draft);NormalizeLevel(file.published);}
    if(file==null||file.version!=1||(file.draft!=null&&!Valid(file.draft))||(file.published!=null&&!Valid(file.published)))throw new InvalidDataException("Invalid workshop file");
    stored=file;
   }catch(Exception){stored=new WorkshopFile();SaveError="上次的关卡暂时读不出来。旧文件保留着，这次可以重新搭。";}
  }
  public static bool Valid(WorkshopLevel level){
   if(level==null||level.version!=1||level.mode<0||level.mode>2||level.floor==null||level.items==null||level.floor.Length!=48||level.items.Length!=48)return false;
   if(level.floor[StartCell]!=1||level.floor[GoalCell]!=1||level.items[StartCell]!=0||level.items[GoalCell]!=0)return false;
   for(int i=0;i<48;i++)if(level.floor[i]<0||level.floor[i]>1||level.items[i]<0||level.items[i]>4||(level.floor[i]==0&&level.items[i]!=0))return false;
   if(level.arrows!=null&&level.arrows.Length!=0){if(level.arrows.Length!=48)return false;for(int i=0;i<48;i++)if(level.arrows[i]<0||level.arrows[i]>4||(level.arrows[i]>0&&(level.floor[i]==0||i==StartCell||i==GoalCell)))return false;}
   return level.floor.Sum()<=32&&level.items.Count(i=>i==1)<=12&&level.items.Count(i=>i==2)<=4&&level.items.Count(i=>i==3)<=1&&level.items.Count(i=>i==4)<=1;
  }
  static bool EmptyRecord(WorkshopLevel level){return level==null||((level.floor==null||level.floor.All(v=>v==0))&&(level.items==null||level.items.All(v=>v==0)));}
  static void NormalizeLevel(WorkshopLevel level){if(level!=null&&(level.arrows==null||level.arrows.Length==0))level.arrows=new int[48];}
  bool WriteStore(){
   try{Directory.CreateDirectory(Path.GetDirectoryName(SavePath));string temp=SavePath+".tmp";
    File.WriteAllText(temp,JsonUtility.ToJson(stored,true));
    if(File.Exists(SavePath))File.Replace(temp,SavePath,SavePath+".bak");else File.Move(temp,SavePath);
    SaveError="";return true;
   }catch(Exception){SaveError="保存没成功，可以检查存储空间后再试。";return false;}
  }
  public void HoverCell(int i){
   if(i<0||i>=48||Playing||Paused){cursor.gameObject.SetActive(false);if(Hover!=""){Hover="";Changed?.Invoke();}return;}
   cursor.position=World(i);cursor.gameObject.SetActive(true);
   string label=i==StartCell?"起点":i==GoalCell?"出口":Layout.floor[i]==0?"空地":new[]{"地板","墙","箱子","压板","门"}[Layout.items[i]];
   if(Layout.arrows[i]>0)label+=" / 转向"+DirectionLabel(Layout.arrows[i]-1);
   string text="第 "+(i%8+1)+" 列 · 第 "+(i/8+1)+" 排 / "+label;
   if(text!=Hover){Hover=text;Changed?.Invoke();}
  }
  public int CellFromViewport(Vector2 uv){
   if(uv.x<0||uv.x>1||uv.y<0||uv.y>1)return -1;
   var ray=BoardCamera.ViewportPointToRay(uv);var plane=new Plane(Vector3.up,Vector3.zero);float distance;
   if(!plane.Raycast(ray,out distance))return -1;var point=ray.GetPoint(distance);
   int x=Mathf.FloorToInt(point.x/Cell+4),z=Mathf.FloorToInt(point.z/Cell+3);
   return x>=0&&x<8&&z>=0&&z<6?Index(x,z):-1;
  }
  public void Hint(){
   Tell(Stage==0?"断开的地方在第5列、第2排。Tab切到编辑，选地板，再点那个空格。":Stage==1?"试试把箱子放在压板前一格，再从箱子后面把它推上去。也可以删墙或铺另一条路。":Stage==2?"它起步向右走。可以拿掉挡路的墙，在最右边放一个向上的箭头；也可以引它往上，利用箱子压住机关。":"先把路铺通，再在转弯的位置放箭头。它起步向右，踩到箭头才改方向。 ");
  }
  void Update(){
   if(Testing||Paused)return;
   if(Input.GetKeyDown(KeyCode.Tab))SetPlaying(!Playing);
   if(Playing&&Input.GetKeyDown(KeyCode.R))RestartTest();
   if(!Playing&&Input.GetKey(KeyCode.LeftControl)&&Input.GetKeyDown(KeyCode.Z))Undo();
   if(!Playing&&Tool==WorkshopTool.Arrow&&Input.GetKeyDown(KeyCode.Q))RotateArrow(-1);
   if(!Playing&&Tool==WorkshopTool.Arrow&&Input.GetKeyDown(KeyCode.E))RotateArrow(1);
  }
  public void TestInput(Vector2 move){testMove=Vector2.ClampMagnitude(move,1);}
  void FixedUpdate(){
   if(!Playing||Paused||Completed||RunFailed||Player==null)return;
   var move=Automatic?RobotMovement(Time.fixedDeltaTime):Testing?testMove:Vector2.ClampMagnitude(new Vector2(Input.GetAxisRaw("Horizontal"),Input.GetAxisRaw("Vertical")),1);
   if(RunFailed)return;
   var v=Player.velocity;var planar=Vector2.MoveTowards(new Vector2(v.x,v.z),move*(Automatic?RobotSpeed:3.8f),32*Time.fixedDeltaTime);
   Player.velocity=new Vector3(planar.x,v.y,planar.y);Player.AddForce(Vector3.down*6,ForceMode.Acceleration);
   if(move.sqrMagnitude>.01f)Player.rotation=Quaternion.Slerp(Player.rotation,Quaternion.LookRotation(new Vector3(move.x,0,move.y)),Time.fixedDeltaTime*14);
   if(Player.position.y< -3){Falls++;if(Automatic){FailRobot("小机器人掉下去了。\n同学：它真的不会自己绕。补路，或者让它早点转弯。 ");return;}RestartTest();if(!firstDrop){firstDrop=true;Tell("小叶子：地板真没了。\n同学：Tab切回去，补一块就行。 ");}return;}
   if(boxes.Any(b=>b.position.y< -3)){if(Automatic){FailRobot("箱子被推下去了。\n小叶子：它还挺实诚，一直推。\n同学：停下来换个摆法吧。 ");return;}RestartTest();Tell("箱子掉下去了。先放回原位，布置没变。 ");return;}
   bool pressed=false;BoxOnPlate=false;
   foreach(var plate in plateRenderers){
    var at=plate.transform.position;bool boxHere=boxes.Any(b=>Near(b.position,at,Automatic?.95f:.65f)&&b.position.y<1.2f);
    bool personHere=Near(Player.position,at,.67f)&&Player.position.y<1.4f;
    plate.sharedMaterial=boxHere||personHere?greenMat:blueMat;pressed|=boxHere||personHere;BoxOnPlate|=boxHere;
   }
   // Leave enough clearance if the door is closing while a player is already crossing it.
   bool crossing=gateLift>.8f&&doors.Any(d=>Near(Player.position,d.position,1.1f));
   gateLift=Mathf.MoveTowards(gateLift,pressed||crossing?1:0,Time.fixedDeltaTime*3);
   foreach(var door in doors){var pos=door.position;pos.y=.8f-gateLift*2.2f;door.position=pos;door.GetComponent<Collider>().enabled=gateLift<.95f;}
   DoorOpen=gateLift>.95f;
   if(Near(Player.position,World(GoalCell),.49f)&&Player.position.y<1.4f){
    Completed=true;verifiedSignature=Signature(Layout);verifiedStage=Stage;Freeze(true);
    if(Stage==2)ChallengeCleared=true;
    Tell(Stage==0?"小叶子：还真能走过去。\n同学：你刚才补的那块已经算进游戏里了。":Stage==1?(BoxOnPlate?"部长：对，就是这么用。\n小叶子：行，总算不用我站着了。":"部长：我本来想讲压板的。\n同学：但他过了。\n部长：……也行。"):Automatic?(BoxOnPlate?"部长：这回连机关也跑通了。\n小叶子：行，总算不用替它着急了。":"部长：我本来想讲压板的。\n同学：但它过了。\n部长：……也行。"):"同学：这关能走通。 ");
   }
   Changed?.Invoke();
  }
  static bool Near(Vector3 a,Vector3 b,float radius){return Mathf.Abs(a.x-b.x)<radius&&Mathf.Abs(a.z-b.z)<radius;}
  public void RestartTest(){if(!Playing||Paused)return;Completed=false;testMove=Vector2.zero;Rebuild();Tell("回到你摆的那一版了。R重来不会改动地板和箱子的原始位置。 ");}
  void Freeze(bool value){
   var bodies=boxes.Concat(Player==null?new Rigidbody[0]:new[]{Player});
   foreach(var body in bodies){if(value){pausedVelocities[body]=body.velocity;body.isKinematic=true;}else{body.isKinematic=false;Vector3 velocity;if(pausedVelocities.TryGetValue(body,out velocity))body.velocity=velocity;}}
  }
  public void SetPaused(bool value){if(value==Paused)return;Paused=value;if(Playing&&!Completed&&!RunFailed)Freeze(value);Changed?.Invoke();}
  void OnApplicationFocus(bool focused){if(!focused&&!Testing)SetPaused(true);}
  void LateUpdate(){if(BoardCamera!=null)BoardCamera.Render();}
  void OnApplicationQuit(){if(Stage>=2&&Dirty)SaveDraft();}
  public void Tell(string message){Message=message;Changed?.Invoke();}
  void OnDestroy(){
   if(BoardCamera!=null)BoardCamera.targetTexture=null;if(View!=null){View.Release();Destroy(View);}
   foreach(var material in materials)Destroy(material);if(slide!=null)Destroy(slide);
  }
 }
}
