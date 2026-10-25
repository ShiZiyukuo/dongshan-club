using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace DongshanClub {
 public partial class CampusGame {
  const string ClubGroup="公开版不提供群号";
  int recruitmentIndex;
  float recruitmentZoom=1;
  Texture2D recruitmentOriginal;
  RectTransform recruitmentContent,recruitmentViewport;
  RawImage recruitmentImage;
  ScrollRect recruitmentScroll;
  Text recruitmentZoomLabel;
  void StopRecruitment(){
   if(recruitmentOriginal!=null)Destroy(recruitmentOriginal);
   recruitmentOriginal=null;recruitmentImage=null;recruitmentContent=null;recruitmentViewport=null;recruitmentScroll=null;
  }
  void ShowRecruitment(int which){
   ResetHubHovers();
   StopRecruitment();recruitmentIndex=Mathf.Clamp(which,0,1);recruitmentZoom=1;
   var p=Modal("社团招新海报",810);
   T(p,"数媒交互与引擎技术协会",35,86,905,42,25,ink);
   string key=recruitmentIndex==0?"recruitment-main":"recruitment-tech";
   recruitmentOriginal=PublicPlaceholder(key);
   recruitmentViewport=R("PosterViewport",p,35,142,650,555);
   var clip=recruitmentViewport.gameObject.AddComponent<Image>();clip.color=new Color32(213,216,204,255);recruitmentViewport.gameObject.AddComponent<Mask>().showMaskGraphic=true;
   recruitmentContent=R("PosterContent",recruitmentViewport,0,0,650,555);
   recruitmentImage=R("PosterOriginal",recruitmentContent,0,0,650,555).gameObject.AddComponent<RawImage>();recruitmentImage.texture=recruitmentOriginal;recruitmentImage.raycastTarget=false;
   recruitmentScroll=recruitmentViewport.gameObject.AddComponent<ScrollRect>();recruitmentScroll.viewport=recruitmentViewport;recruitmentScroll.content=recruitmentContent;
   recruitmentScroll.horizontal=true;recruitmentScroll.vertical=true;recruitmentScroll.scrollSensitivity=32;recruitmentScroll.movementType=ScrollRect.MovementType.Clamped;
   T(p,recruitmentIndex==0?"综合招新海报":"技术方向招新海报",715,147,230,65,24,ink);
   T(p,"公开示例",715,230,230,35,20,ink);
   T(p,ClubGroup,715,271,230,45,30,ink);
   var copied=T(p,"",715,379,230,40,18,ink);
   B(p,"复制说明",715,326,230,50,()=>{GUIUtility.systemCopyBuffer=ClubGroup;copied.text="说明已复制";},true);
   B(p,"放大 ＋",715,432,230,50,()=>ResizeRecruitment(recruitmentZoom+.5f));
   B(p,"缩小 －",715,495,230,50,()=>ResizeRecruitment(recruitmentZoom-.5f));
   B(p,"查看底部",715,558,230,50,()=>{ResizeRecruitment(Mathf.Max(2.5f,recruitmentZoom));Canvas.ForceUpdateCanvases();recruitmentScroll.normalizedPosition=new Vector2(recruitmentIndex==0?1:0,0);});
   recruitmentZoomLabel=T(p,"",715,631,230,38,20,ink);
   B(p,"上一张",35,720,235,53,()=>ShowRecruitment((recruitmentIndex+1)%2));
   B(p,"下一张",290,720,235,53,()=>ShowRecruitment((recruitmentIndex+1)%2));
   B(p,"看全图",550,720,395,53,()=>ResizeRecruitment(1),true);
   T(p,"放大后可拖动海报查看。公开版使用程序占位图，不含原海报或二维码。",35,700,650,18,14,ink);
   ResizeRecruitment(1);
  }
  void ResizeRecruitment(float zoom){
   if(recruitmentOriginal==null||recruitmentImage==null)return;
   recruitmentZoom=Mathf.Clamp(zoom,1,4);
   float fit=Mathf.Min(650f/recruitmentOriginal.width,555f/recruitmentOriginal.height);
   float w=recruitmentOriginal.width*fit*recruitmentZoom,h=recruitmentOriginal.height*fit*recruitmentZoom;
   float cw=Mathf.Max(650,w),ch=Mathf.Max(555,h);
   recruitmentContent.sizeDelta=new Vector2(cw,ch);
   recruitmentImage.rectTransform.sizeDelta=new Vector2(w,h);recruitmentImage.rectTransform.anchoredPosition=new Vector2((cw-w)/2,-(ch-h)/2);
   recruitmentZoomLabel.text=(recruitmentIndex+1)+" / 2　·　"+Mathf.RoundToInt(recruitmentZoom*100)+"%";
   Canvas.ForceUpdateCanvases();recruitmentScroll.normalizedPosition=new Vector2(.5f,.5f);
  }
 }
}
