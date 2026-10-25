using System;
using System.Collections.Generic;
using UnityEngine;

namespace DongshanClub {
 // A disposable physics scene. Story state stays with CampusGame; this scene owns its camera and materials.
 public class ClubPlayground : MonoBehaviour {
  const int WorldLayer=30;
  public Action Changed;
  public bool Testing,Paused,Finished,GateOpened,BoxOnPlate;
  public int Falls,Interactions;
  public string Objective="先去蓝色开关旁边，按 E 开门。";
  public string Caption="部长：能玩了。先走过去，开个门。\n小叶子：这次真能走？";
  public string Prompt="";
  public RenderTexture View;
  public Rigidbody Player {get;private set;}
  public Rigidbody Box {get;private set;}
  public Vector3 Checkpoint {get;private set;}
  public Vector3 SwitchPosition {get{return new Vector3(-2.5f,0,5);}}
  public Vector3 PlatePosition {get{return new Vector3(1,0,25.5f);}}
  public bool Grounded {get;private set;}
  public bool HasJumped {get;private set;}
  public bool SawGap {get;private set;}
  public float DistanceMoved {get;private set;}
  readonly List<Material> materials=new List<Material>();
  Camera viewCamera;Transform entryDoor,exitDoor,plate,avatar;Collider entryCollider,exitCollider;
  Vector3 previousPosition,boxStart=new Vector3(1,.7f,21),cameraVelocity;
  Vector3 pausedPlayerVelocity,pausedBoxVelocity;
  Vector2 testMove;bool testJump,testInteract;
  float jumpBuffer,groundGrace,entryLift,exitLift,stageClock;
  bool crossedGap,plateAnnounced,firstFall;int stage;
  Material switchMaterial,plateMaterial,goalMaterial;

  public void Build(){
   gameObject.layer=WorldLayer;
   var cameraObject=new GameObject("PlaygroundCamera");cameraObject.transform.SetParent(transform,false);
   viewCamera=cameraObject.AddComponent<Camera>();viewCamera.cullingMask=1<<WorldLayer;
   viewCamera.enabled=false;
   viewCamera.clearFlags=CameraClearFlags.SolidColor;viewCamera.backgroundColor=new Color32(30,47,53,255);
   viewCamera.orthographic=true;viewCamera.orthographicSize=9.1f;viewCamera.nearClipPlane=.1f;viewCamera.farClipPlane=100;
   View=new RenderTexture(1600,900,24,RenderTextureFormat.ARGB32);View.antiAliasing=2;View.Create();viewCamera.targetTexture=View;
   var lightObject=new GameObject("PlaygroundLight");lightObject.transform.SetParent(transform,false);
   var light=lightObject.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.1f;light.cullingMask=1<<WorldLayer;
   lightObject.transform.rotation=Quaternion.Euler(52,-30,0);
   var floor=Mat(new Color32(120,146,135,255));var edge=Mat(new Color32(58,82,80,255));
   var blue=Mat(new Color32(78,192,209,255));var yellow=Mat(new Color32(239,188,87,255));
   var coral=Mat(new Color32(196,112,97,255));var cream=Mat(new Color32(238,229,199,255));
   Cube("Start platform",new Vector3(0,-.35f,6),new Vector3(10,.7f,12),floor);
   Cube("Landing platform",new Vector3(0,-.35f,24),new Vector3(10,.7f,18),floor);
   Cube("Finish platform",new Vector3(0,-.35f,35.5f),new Vector3(10,.7f,5),floor);
   for(int z=1;z<38;z+=2){if(z>11&&z<15)continue;
    Cube("Edge marker",new Vector3(-4.86f,.025f,z),new Vector3(.17f,.05f,.8f),cream,false);
    Cube("Edge marker",new Vector3(4.86f,.025f,z),new Vector3(.17f,.05f,.8f),cream,false);
   }
   for(int z=2;z<37;z+=3){if(z>11&&z<16)continue;Cube("Path marker",new Vector3(0,.015f,z),new Vector3(.15f,.03f,.65f),edge,false);}
   entryDoor=Cube("First gate",new Vector3(0,1.5f,8),new Vector3(10,3,.45f),coral).transform;entryCollider=entryDoor.GetComponent<Collider>();
   exitDoor=Cube("Plate gate",new Vector3(0,1.5f,30),new Vector3(10,3,.45f),coral).transform;exitCollider=exitDoor.GetComponent<Collider>();
   foreach(float z in new[]{8f,30f})foreach(float x in new[]{-4.7f,4.7f})Cube("Gate post",new Vector3(x,2,z),new Vector3(.55f,4,.8f),edge);
   Cube("Switch plinth",SwitchPosition+Vector3.up*.45f,new Vector3(.65f,.9f,.65f),edge);
   switchMaterial=Mat(new Color32(78,192,209,255));
   Cube("Switch",SwitchPosition+Vector3.up*.99f,new Vector3(.72f,.2f,.72f),switchMaterial,false);
   Cube("Switch area",SwitchPosition+Vector3.up*.012f,new Vector3(3,.025f,3),blue,false);
   plateMaterial=Mat(new Color32(78,192,209,255));
   plate=Cube("Weight plate",PlatePosition+Vector3.up*.035f,new Vector3(2.6f,.07f,2.6f),plateMaterial,false).transform;
   Cube("Jump edge",new Vector3(0,.025f,11.65f),new Vector3(9.5f,.05f,.2f),yellow,false);
   Cube("Landing edge",new Vector3(0,.025f,15.2f),new Vector3(9.5f,.05f,.2f),yellow,false);
   // The unfinished area is visibly marked; it never becomes an invisible trap.
   for(int i=0;i<7;i++)Cube("Gap stripe",new Vector3(-4.2f+i*1.4f,-1.4f,13.5f),new Vector3(.65f,.05f,2.6f),edge,false);
   goalMaterial=Mat(new Color32(239,188,87,255));
   Cube("Goal",new Vector3(0,.035f,35),new Vector3(3,.07f,3),goalMaterial,false);
   for(int i=0;i<2;i++)Cube("Goal post",new Vector3(i==0?-1.7f:1.7f,1.5f,35),new Vector3(.2f,3,.2f),yellow,false);
   Cube("Goal lintel",new Vector3(0,3,35),new Vector3(3.6f,.2f,.2f),yellow,false);
   var crate=Cube("Pushable box",boxStart,new Vector3(1.3f,1.3f,1.3f),cream);
   Box=crate.AddComponent<Rigidbody>();Box.mass=1.6f;Box.drag=4;Box.interpolation=RigidbodyInterpolation.Interpolate;
   Box.constraints=RigidbodyConstraints.FreezeRotation;Box.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
   var boxSurface=new PhysicMaterial("Box slide"){dynamicFriction=.12f,staticFriction=.12f,frictionCombine=PhysicMaterialCombine.Minimum};
   crate.GetComponent<Collider>().sharedMaterial=boxSurface;ownedPhysicsMaterial=boxSurface;
   var person=GameObject.CreatePrimitive(PrimitiveType.Capsule);person.name="Playable capsule";person.layer=WorldLayer;person.transform.SetParent(transform,false);
   person.transform.position=new Vector3(0,1,1);person.transform.localScale=new Vector3(.72f,.85f,.72f);person.GetComponent<Renderer>().sharedMaterial=yellow;
   Player=person.AddComponent<Rigidbody>();Player.mass=3;Player.constraints=RigidbodyConstraints.FreezeRotation;Player.interpolation=RigidbodyInterpolation.Interpolate;
   Player.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
   person.GetComponent<Collider>().sharedMaterial=boxSurface;avatar=person.transform;
   // A face provides heading feedback even with placeholder geometry.
   foreach(float x in new[]{-.16f,.16f}){
    var eye=GameObject.CreatePrimitive(PrimitiveType.Sphere);eye.name="Eye";eye.layer=WorldLayer;eye.transform.SetParent(person.transform,false);
    eye.transform.localPosition=new Vector3(x,.42f,.43f);eye.transform.localScale=Vector3.one*.13f;
    eye.GetComponent<Renderer>().sharedMaterial=edge;Destroy(eye.GetComponent<Collider>());
   }
   Checkpoint=Player.position;previousPosition=Player.position;
   viewCamera.transform.position=Player.position+new Vector3(0,14,-12);
   viewCamera.transform.rotation=Quaternion.LookRotation(new Vector3(0,-14,16));
   Changed?.Invoke();
  }
  PhysicMaterial ownedPhysicsMaterial;
  Material Mat(Color color){
   var shader=Shader.Find("Standard");if(shader==null)shader=Shader.Find("Unlit/Color");
   var mat=new Material(shader){color=color};if(mat.HasProperty("_Glossiness"))mat.SetFloat("_Glossiness",.1f);
   materials.Add(mat);return mat;
  }
  GameObject Cube(string label,Vector3 at,Vector3 size,Material material,bool solid=true){
   var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=label;obj.layer=WorldLayer;obj.transform.SetParent(transform,false);
   obj.transform.position=at;obj.transform.localScale=size;obj.GetComponent<Renderer>().sharedMaterial=material;
   if(!solid){var collider=obj.GetComponent<Collider>();collider.enabled=false;Destroy(collider);}return obj;
  }
  void Update(){
   if(Testing||Paused||Finished)return;
   if(Input.GetKeyDown(KeyCode.Space))jumpBuffer=.16f;
   if(Input.GetKeyDown(KeyCode.E))Interact();
   if(Input.GetKeyDown(KeyCode.R))Respawn(false);
  }
  public void TestInput(Vector2 move,bool jump=false,bool interact=false){testMove=move;testJump|=jump;testInteract|=interact;}
  void FixedUpdate(){
   if(Player==null||Paused||Finished)return;
   float dt=Time.fixedDeltaTime;stageClock+=dt;
   var move=Testing?testMove:new Vector2(Input.GetAxisRaw("Horizontal"),Input.GetAxisRaw("Vertical"));
   move=Vector2.ClampMagnitude(move,1);
   if(testJump){jumpBuffer=.16f;testJump=false;}if(testInteract){Interact();testInteract=false;}
   // Ignore the capsule itself when checking for a floor.
   Grounded=false;
   foreach(var hit in Physics.SphereCastAll(Player.position,.26f,Vector3.down,.68f,1<<WorldLayer,QueryTriggerInteraction.Ignore)){
    if(hit.rigidbody!=Player&&hit.normal.y>.55f){Grounded=true;break;}
   }
   groundGrace=Grounded?.12f:Mathf.Max(0,groundGrace-dt);jumpBuffer=Mathf.Max(0,jumpBuffer-dt);
   Vector3 velocity=Player.velocity;
   Vector2 planar=Vector2.MoveTowards(new Vector2(velocity.x,velocity.z),move*5.4f,(Grounded?32:26)*dt);
   velocity.x=planar.x;velocity.z=planar.y;
   if(jumpBuffer>0&&groundGrace>0){velocity.y=9.8f;groundGrace=0;jumpBuffer=0;Grounded=false;HasJumped=true;}
   Player.velocity=velocity;Player.AddForce(Vector3.down*12f,ForceMode.Acceleration);
   if(move.sqrMagnitude>.02f)avatar.rotation=Quaternion.Slerp(avatar.rotation,Quaternion.LookRotation(new Vector3(move.x,0,move.y)),12*dt);
   if(Player.position.y< -5){Respawn(true);return;}
   if(Box.position.y< -5){Box.position=boxStart;Box.velocity=Vector3.zero;Say("小叶子：箱子也掉了。\n部长：我给它加了个自动回来。",5);}
   DistanceMoved+=Vector3.Distance(previousPosition,Player.position);previousPosition=Player.position;
   entryLift=Mathf.MoveTowards(entryLift,GateOpened?1:0,dt*1.6f);
   entryDoor.position=new Vector3(0,1.5f-entryLift*4,8);entryCollider.enabled=entryLift<.78f;
   var delta=Box.position-PlatePosition;
   BoxOnPlate=Mathf.Abs(delta.x)<1.08f&&Mathf.Abs(delta.z)<1.08f&&Box.position.y<1.1f;
   var playerDelta=Player.position-PlatePosition;
   bool personOnPlate=Mathf.Abs(playerDelta.x)<1.22f&&Mathf.Abs(playerDelta.z)<1.22f&&Player.position.y<1.5f;
   bool holdOpen=BoxOnPlate||personOnPlate||(exitLift>.8f&&Mathf.Abs(Player.position.z-30)<1.2f);
   exitLift=Mathf.MoveTowards(exitLift,holdOpen?1:0,dt*1.8f);
   exitDoor.position=new Vector3(0,1.5f-exitLift*4,30);exitCollider.enabled=exitLift<.78f;
   plateMaterial.color=holdOpen?new Color32(118,213,137,255):new Color32(78,192,209,255);
   plate.position=PlatePosition+Vector3.up*(holdOpen?.015f:.035f);
   if(GateOpened&&Player.position.z>9.2f&&stage<1){stage=1;stageClock=0;Objective="前面地板没铺。跑起来，空格跳过去。";Say("小叶子：这儿怎么空了一块？\n部长：还没铺。先跳过去。",6);}
   if(Player.position.z>10)SawGap=true;
   if(!crossedGap&&Player.position.z>16&&Grounded){
    crossedGap=true;stage=2;stageClock=0;Checkpoint=new Vector3(0,1,17.3f);Objective="把白箱子推到蓝色压板上，让门一直开着。";
    Say("部长：压板上有东西，门就开。\n小叶子：那我走了谁站着？\n旁边同学：你试试那个箱子。",8);
   }
   if(BoxOnPlate&&!plateAnnounced){plateAnnounced=true;stage=3;stageClock=0;Objective="门开了！绕过箱子，走到黄色终点。";Say("小叶子：总算不用我站着了。\n部长：对，就是这个意思。",5);}
   if(!BoxOnPlate&&plateAnnounced){Objective="箱子离开压板了，把它推回来就行。";plateAnnounced=false;}
   float switchDistance=FlatDistance(Player.position,SwitchPosition);
   Prompt=!GateOpened&&switchDistance<1.9f?"E  打开门":Grounded&&Player.position.z>9.5f&&Player.position.z<12?"空格  跳过缺口":personOnPlate&&!BoxOnPlate?"门开了，但离开压板就会关。":"";
   if(Player.position.z>33.3f&&BoxOnPlate){Complete();}
   else if(stageClock>25&&stage==0){Say("旁边同学：开关在左边，那个蓝色的。走近了再按 E。",7);stageClock=0;}
   else if(stageClock>35&&stage==2){Say("旁边同学：绕到箱子后面，往蓝色板子上推。\n小叶子：行，不用写代码就行。",7);stageClock=0;}
   Changed?.Invoke();
  }
  float FlatDistance(Vector3 a,Vector3 b){a.y=0;b.y=0;return Vector3.Distance(a,b);}
  public void Interact(){
   Interactions++;
   if(GateOpened){Say("部长：开关不用一直按，门已经开了。",3);return;}
   if(FlatDistance(Player.position,SwitchPosition)>1.9f){Say("小叶子：按了，没反应。\n部长：得先走到开关旁边。",4);return;}
   GateOpened=true;switchMaterial.color=new Color32(118,213,137,255);Objective="门开了，往前走。";
   Say("小叶子：哦，得站这么近。\n部长：对。这个就叫范围。",5);
  }
  public void Hint(){
   if(!GateOpened)Say("走到左边蓝色开关的旁边，再按 E。门会一直开着。",7);
   else if(!crossedGap)Say("沿着路往前走，到黄色边线前按空格起跳。掉下去会自动回来。",7);
   else if(!BoxOnPlate)Say("绕到白箱子后面，用身体把它推上蓝色压板。R 可以把角色和箱子放回最近的起点。",8);
   else Say("绕开箱子，穿过前面的门，走到黄色框里。",5);
  }
  public void Respawn(bool fell){
   if(fell)Falls++;
   Player.position=Checkpoint;Player.velocity=Vector3.zero;Player.angularVelocity=Vector3.zero;previousPosition=Checkpoint;
   jumpBuffer=0;groundGrace=0;testMove=Vector2.zero;
   if(!fell||Box.position.y< -2){Box.position=boxStart;Box.velocity=Vector3.zero;}
   if(fell&&!firstFall){firstFall=true;Say("小叶子：掉下去了。\n部长：没事，回来就行。\n小叶子：你这个功能倒是挺全。",6);}
   else Say(fell?"回到刚才的平台了。再试一次就行。":"角色和箱子放回来了。开过的门不用重开。",4);
   Changed?.Invoke();
  }
  void Say(string text,float seconds){Caption=text;Changed?.Invoke();}
  public void SetPaused(bool value){
   if(Paused==value||Player==null)return;Paused=value;
   if(value){pausedPlayerVelocity=Player.velocity;pausedBoxVelocity=Box.velocity;Player.isKinematic=true;Box.isKinematic=true;}
   else {Player.isKinematic=false;Box.isKinematic=false;Player.velocity=pausedPlayerVelocity;Box.velocity=pausedBoxVelocity;}
   Changed?.Invoke();
  }
  void Complete(){
   Finished=true;Player.velocity=Vector3.zero;Box.velocity=Vector3.zero;Player.isKinematic=true;Box.isKinematic=true;
   Objective="这一版，至少能从头走到尾了。";
   Caption="小叶子：这就没了？\n部长：嗯，先做到这儿。\n旁边同学：那下次我画个能看的箱子。";
   goalMaterial.color=new Color32(118,213,137,255);Changed?.Invoke();
  }
  void LateUpdate(){
   if(viewCamera==null||Player==null)return;
   Vector3 target=new Vector3(Player.position.x*.35f,14,Mathf.Max(-10,Player.position.z-12));
   viewCamera.transform.position=Vector3.SmoothDamp(viewCamera.transform.position,target,ref cameraVelocity,.18f);
   viewCamera.Render();
  }
  void OnApplicationFocus(bool focused){if(!focused&&!Testing&&!Finished)SetPaused(true);}
  void OnDestroy(){
   if(viewCamera!=null)viewCamera.targetTexture=null;
   if(View!=null){View.Release();Destroy(View);View=null;}
   foreach(var mat in materials)if(mat!=null)Destroy(mat);
   if(ownedPhysicsMaterial!=null)Destroy(ownedPhysicsMaterial);
  }
 }
}
