using System;
using UnityEngine;
using UnityEngine.UI;

namespace DongshanClub {
 public partial class CampusGame {
  Action presidentComplete;
  void PresidentActivity(Beat n){
   presidentComplete=null;
   if(n.kind=="stamp"){StampActivity();return;}
   bool notice=n.kind=="notice";
   var p=ActivityPanel(notice?"通知的时间对不上":"这次要交哪一份",notice?"对照已经确认的信息，再决定发哪个版本。":"查看要求和文件，旧稿可以先放到一旁。");
   B(p,notice?"早期安排":"老师的要求",25,145,210,54,()=>Doc(notice?"早期安排":"附件要求",notice?"拟定开始时间：18:30。\n此版本尚未完成最后确认。":"提交最新版申请材料与附件。\n同名旧稿不需要重复提交。"));
   B(p,notice?"最新确认":"文件列表",255,145,210,54,()=>Doc(notice?"老师的回复":"文件列表",notice?"确认开始时间：19:00。\n地点和参与方式沿用已确认的安排。":"申请材料-最终确认.pdf\n申请材料-旧稿.pdf\n附件清单.pdf"));
   B(p,notice?"通知草稿":"核对记录",485,145,210,54,()=>Doc(notice?"待发布的草稿":"核对记录",notice?"开始时间仍写着18:30。\n其他信息已经核对。":"内容已补齐，附件中多放了一份旧稿。\n将旧稿移开，再提交确认版本。"));
   if(notice){
    B(p,"仍发 18:30",25,242,300,60,()=>Toast("最新确认里是19:00。再核对一下，不着急。"));
    presidentComplete=()=>Done("她把时间改为19:00，重新发出了通知。");
    B(p,"改为 19:00，再发通知",350,242,345,60,()=>presidentComplete(),true);
   }else MakeDrag(p,"申请材料-旧稿",new Vector2(25,240),new Vector2(450,240),()=>Done("旧稿被移到一旁。这次提交的文件确认好了。"));
  }
  void StampActivity(){
   var p=ActivityPanel("留一张活动卡", "微调方向后盖一下，歪一点也可以。没有完美评分。");
   var card=Panel(p,218,145,270,143,new Color32(255,251,233,255),"ActivityCard");
   T(card.transform,"游戏制作社团",20,14,230,31,19,ink,TextAnchor.MiddleCenter);
   var preview=RoundPanel(card.transform,91,63,88,53,new Color32(142,54,51,70),"StampPreview");
   Panel(preview.transform,15,23,24,6,cream);Panel(preview.transform,24,14,6,24,cream);
   RoundPanel(preview.transform,59,14,9,9,cream);RoundPanel(preview.transform,70,27,9,9,cream);
   float angle=-7;
   preview.rectTransform.localEulerAngles=new Vector3(0,0,angle);
   Action<float> rotate=a=>{if(activityDone)return;angle=Mathf.Clamp(angle+a,-22,22);preview.rectTransform.localEulerAngles=new Vector3(0,0,angle);};
   B(p,"转左一点",25,145,160,57,()=>rotate(-5));B(p,"转右一点",515,145,180,57,()=>rotate(5));
   presidentComplete=()=>{if(activityDone)return;preview.color=new Color32(142,54,51,235);PlayerPrefs.SetFloat(Prefix+"stampAngle",angle);Done("图案留在卡片上。她把卡片放到一边晾着。");};
   B(p,"盖在这张卡上",25,309,670,56,()=>presidentComplete(),true);
  }
 }
}
