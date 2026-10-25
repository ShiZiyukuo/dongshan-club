using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DongshanClub {
 public class HubPropHover:MonoBehaviour,IPointerEnterHandler,IPointerExitHandler {
  public Image closedCover,openCover,shadow;
  public RectTransform floatingObject;
  Vector2 restingPosition;
  Vector3 restingShadowScale;
  float amount,target;
  public void Initialize(){
   if(floatingObject!=null)restingPosition=floatingObject.anchoredPosition;
   if(shadow!=null)restingShadowScale=shadow.rectTransform.localScale;
   ResetHover();
  }
  public void SetHovered(bool hovered){target=hovered?1:0;}
  public void OnPointerEnter(PointerEventData e){SetHovered(true);}
  public void OnPointerExit(PointerEventData e){SetHovered(false);}
  public void ResetHover(){amount=target=0;Apply();}
  void OnDisable(){ResetHover();}
  void Update(){amount=Mathf.MoveTowards(amount,target,Time.unscaledDeltaTime/0.2f);Apply();}
  void Apply(){
   float eased=amount*amount*(3-2*amount);
   if(closedCover!=null){var c=closedCover.color;c.a=1-eased;closedCover.color=c;}
   if(openCover!=null){var c=openCover.color;c.a=eased;openCover.color=c;}
   if(floatingObject!=null)floatingObject.anchoredPosition=restingPosition+new Vector2(0,13*eased);
   if(shadow!=null){var c=shadow.color;c.a=.24f-.07f*eased;shadow.color=c;shadow.rectTransform.localScale=restingShadowScale*(1-.07f*eased);}
  }
 }
 public class HubHoverRelay:MonoBehaviour,IPointerEnterHandler,IPointerExitHandler {
  public HubPropHover target;
  public void OnPointerEnter(PointerEventData e){if(target!=null)target.SetHovered(true);}
  public void OnPointerExit(PointerEventData e){if(target!=null)target.SetHovered(false);}
 }
}
