using UnityEngine;
namespace DongshanClub {
 public partial class CampusGame {
  // New code-generated placeholders; no private photograph is embedded.
  Texture2D PublicPlaceholder(string key) {
   const int w=128,h=192; var texture=new Texture2D(w,h,TextureFormat.RGBA32,false);
   int seed=0;foreach(char c in key)seed=(seed*31+c)&0x7fffffff;
   var color=new Color32((byte)(70+seed%80),(byte)(90+seed%70),(byte)(100+seed%60),255);
   var pixels=new Color32[w*h];
   for(int y=0;y<h;y++)for(int x=0;x<w;x++){
    float dx=(x-w*.5f)/(w*.22f),dy=(y-h*.69f)/(h*.15f);
    bool head=dx*dx+dy*dy<1;bool body=y>h*.14f&&y<h*.48f&&Mathf.Abs(x-w*.5f)<w*.30f;
    pixels[y*w+x]=head||body?color:new Color32(0,0,0,0);
   }
   texture.SetPixels32(pixels);texture.Apply();texture.name="PublicPlaceholder:"+key;return texture;
  }
 }
}
