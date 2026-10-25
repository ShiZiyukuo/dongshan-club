using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace DongshanClub {
 public partial class CampusGame {
  Coroutine portraitReaction;
  Image reactionImage;
  Sprite reactionIdle;
  Vector2 reactionPosition;
  IEnumerator InviteEntrance(RectTransform actor){
   for(float t=0;t<.35f;t+=Time.unscaledDeltaTime){
    if(actor==null)yield break;
    float u=Mathf.Clamp01(t/.35f);actor.localScale=Vector3.one*Mathf.Lerp(.97f,1,1-Mathf.Pow(1-u,3));yield return null;
   }
   if(actor!=null)actor.localScale=Vector3.one;
  }
  void StopPortraitReaction(){
   if(portraitReaction!=null)StopCoroutine(portraitReaction);
   if(reactionImage!=null){reactionImage.sprite=reactionIdle;reactionImage.rectTransform.anchoredPosition=reactionPosition;}
   portraitReaction=null;reactionImage=null;
  }
  IEnumerator PortraitReaction(string key,Image actor){
   string pose=key=="founder"?"founder-explain":"xiaoye-sleep";
   if(actor==null||!art.ContainsKey(pose))yield break;
   reactionImage=actor;reactionIdle=art[key];reactionPosition=actor.rectTransform.anchoredPosition;
   actor.sprite=art[pose];
   float duration=key=="founder"?1.5f:2f;
   for(float t=0;t<duration;t+=Time.unscaledDeltaTime){
    if(actor==null)yield break;
    // Founder perks up; Xiaoye settles and breathes. No horizontal translation.
    float offset=key=="founder"?Mathf.Sin(Mathf.Clamp01(t/.36f)*Mathf.PI)*7:
      -5*Mathf.Sin(Mathf.PI*t/duration)+Mathf.Sin(t*5)*1.2f;
    actor.rectTransform.anchoredPosition=reactionPosition+new Vector2(0,offset);
    yield return null;
   }
   if(actor!=null){actor.sprite=reactionIdle;actor.rectTransform.anchoredPosition=reactionPosition;}
   reactionImage=null;portraitReaction=null;
  }
 }
}
