using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace DongshanClub {
 public partial class CampusGame {
  VideoPlayer moviePlayer;
  RenderTexture movieTexture;
  bool movieMuted=true;
  string movieError;

  void MediaPreview(string key){
   var fish=key=="fish";
   bool neural=key=="neural";
   var p=ActivityPanel(neural?"部长的神经网络动画模型":fish?"Unity · DeepSeek大肥鱼":"Ren’Py · 我的作品", "原始录像未公开。可以查看说明，或继续故事。");
   Panel(p,30,135,660,143,new Color32(25,35,34,255));
   if(neural){T(p,"神经网络动画模型",53,158,610,46,31,cream);T(p,"技术部部长制作 · 20秒录像",53,221,610,35,20,gold);}
   else Picture(p,key+"-poster",30,135,660,143);
   B(p,"查看公开版说明",30,296,325,61,()=>OpenVideo(key),true);
   B(p,"继续故事",375,296,315,61,()=>Advance());
  }
  string MoviePath(string key){return Path.Combine(Application.streamingAssetsPath,"Media",key+".mp4");}
  void OpenVideo(string key){
   var p=Modal("公开版 · 录像说明",500);
   T(p,"私人录像未纳入公开仓库。此处保留剧情与返回入口。",35,140,905,150,25,ink);
   B(p,"返回故事",35,340,905,65,CloseModal,true);
  }
  void StopVideo(){
   if(moviePlayer!=null){moviePlayer.Stop();Destroy(moviePlayer);moviePlayer=null;}
   if(movieTexture!=null){movieTexture.Release();Destroy(movieTexture);movieTexture=null;}
   if(music!=null)music.volume=volume*.23f;
  }
 }
}
