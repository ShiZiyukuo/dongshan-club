using UnityEngine;

namespace DongshanClub {
 public partial class CampusGame {
  const string GameTitle="东山有戏：这关一起过";
  readonly Color vermilion=new Color32(162,62,45,255);
  // Keep the original product identifier so existing saves and preferences stay accessible.
  // Only the title of this process's own Windows window changes.
  #if UNITY_STANDALONE_WIN && !UNITY_EDITOR
  [System.Runtime.InteropServices.DllImport("user32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode)]
  static extern bool SetWindowText(System.IntPtr handle,string text);
  #endif
  System.Collections.IEnumerator ApplyWindowTitle(){
   #if UNITY_STANDALONE_WIN && !UNITY_EDITOR
   for(int attempt=0;attempt<8;attempt++){
    yield return new WaitForSecondsRealtime(.2f);
    using(var process=System.Diagnostics.Process.GetCurrentProcess()){
     var handle=process.MainWindowHandle;
     if(handle!=System.IntPtr.Zero&&SetWindowText(handle,GameTitle))yield break;
    }
   }
   #else
   yield break;
   #endif
  }
  // Geometric window lattice, kept away from text and all pointer hit targets.
  void Lattice(Transform parent,float x,float y,float size,Color color){
   float t=2;
   MotifLine(parent,x,y,size,t,color);MotifLine(parent,x,y,t,size,color);
   MotifLine(parent,x+size-t,y,t,size,color);MotifLine(parent,x,y+size-t,size,t,color);
   MotifLine(parent,x+size*.28f,y,size*.44f,t,color);
   MotifLine(parent,x+size*.28f,y,t,size*.28f,color);
   MotifLine(parent,x+size*.28f,y+size*.28f,size*.44f,t,color);
   MotifLine(parent,x+size*.72f-t,y+size*.28f,t,size*.44f,color);
   MotifLine(parent,x+size*.28f,y+size*.72f-t,size*.44f,t,color);
   MotifLine(parent,x+size*.28f,y+size*.72f-t,t,size*.28f,color);
  }
  void MotifLine(Transform p,float x,float y,float w,float h,Color c){Panel(p,x,y,w,h,c,"LatticeLine").raycastTarget=false;}
  void LedgerRule(Transform p,float x,float y,float width){
   MotifLine(p,x,y,width,1,new Color32(161,120,77,100));
   for(int i=0;i<3;i++)MotifLine(p,x+i*7,y-2,3,5,gold);
  }
  void TitleSeal(Transform p,float x,float y){
   var seal=Panel(p,x,y,62,94,vermilion,"DongshanSeal");seal.raycastTarget=false;
   MotifLine(seal.transform,5,5,52,1,cream);MotifLine(seal.transform,5,88,52,1,cream);
   T(seal.transform,"东\n山",0,9,62,78,28,cream,UnityEngine.TextAnchor.MiddleCenter);
   seal.rectTransform.localEulerAngles=new Vector3(0,0,-4);
  }
 }
}
