using System;
using UnityEngine;
using UnityEngine.UI;

namespace DongshanClub {
 public partial class CampusGame {
  HubPropHover notebookHover,stampHover;
  Sprite propShadow;
  Image HubShadow(float x,float y,float w,float h){
   if(propShadow==null){
    const int width=128,height=64;var tex=new Texture2D(width,height,TextureFormat.RGBA32,false);var pixels=new Color[width*height];
    for(int yy=0;yy<height;yy++)for(int xx=0;xx<width;xx++){
     float dx=(xx+.5f-width/2f)/(width/2f),dy=(yy+.5f-height/2f)/(height/2f);
     float a=Mathf.Pow(Mathf.Clamp01(1-dx*dx-dy*dy),2);pixels[yy*width+xx]=new Color(0,0,0,a);
    }
    tex.SetPixels(pixels);tex.Apply(false,true);propShadow=Sprite.Create(tex,new Rect(0,0,width,height),new Vector2(.5f,.5f),100);
   }
   var im=Panel(stage,x,y,w,h,new Color(0,0,0,.24f),"PropShadow");im.sprite=propShadow;im.raycastTarget=false;return im;
  }
  HubPropHover PropHit(string name,string sprite,float x,float y,float w,float h,Action click){
   var hit=Picture(stage,sprite,x,y,w,h);hit.name="Hotspot:"+name;hit.color=new Color(1,1,1,.001f);hit.raycastTarget=true;hit.alphaHitTestMinimumThreshold=.08f;
   var b=hit.gameObject.AddComponent<Button>();b.transition=Selectable.Transition.None;b.onClick.AddListener(()=>{ResetHubHovers();ClickSound();click();});
   return hit.gameObject.AddComponent<HubPropHover>();
  }
  void BuildHubProps(){
   HubShadow(710,625,520,98);
   var closed=Picture(stage,"hub-book-closed",622,288,646,446);closed.name="BookCoverClosed";
   var opened=Picture(stage,"hub-book-open",622,288,646,446);opened.name="BookCoverOpen";
   notebookHover=PropHit("笔记本","hub-book-closed",622,288,646,446,()=>InspectChapter("xiaoye"));
   notebookHover.closedCover=closed;notebookHover.openCover=opened;notebookHover.Initialize();
   var shadow=HubShadow(1241,570,177,56);
   var stamp=Picture(stage,"hub-stamp",1200,380,247,260);stamp.name="FloatingStamp";
   stampHover=PropHit("印章","hub-stamp",1200,380,247,260,()=>InspectChapter("president"));
   stampHover.floatingObject=stamp.rectTransform;stampHover.shadow=shadow;stampHover.Initialize();
  }
  void ResetHubHovers(){if(notebookHover!=null)notebookHover.ResetHover();if(stampHover!=null)stampHover.ResetHover();}
 }
}
