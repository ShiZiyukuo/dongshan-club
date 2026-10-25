using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace DongshanClub {
 public partial class CampusGame {
  string ActorKey(string name){var person=cast.members.FirstOrDefault(m=>m.aliases.Contains(name??""));return person==null?"":person.key;}
  string DisplayActorKey(Beat n){
   if(!string.IsNullOrEmpty(n.chat))return n.contact=="社团群"?(n.who==ActiveName?"":n.actor??""):ActorKey(n.contact);
   if(!string.IsNullOrEmpty(n.actor))return n.actor;
   if(!string.IsNullOrEmpty(n.who))return "";
   for(int i=index-1;i>=0;i--){var old=story.nodes[i];if(old.chapter!=n.chapter||old.scene!=n.scene||!string.IsNullOrEmpty(old.chat))break;if(!string.IsNullOrEmpty(old.actor))return old.actor;}
   return "";
  }
  void RenderCast(Beat n){
   StopPortraitReaction();
   portrait=null;portraitRect=null;activeActor=DisplayActorKey(n);if(string.IsNullOrEmpty(activeActor)||!art.ContainsKey(activeActor))return;
   bool ye=activeActor=="xiaoye",boss=activeActor=="founder";
   portrait=Picture(characters,activeActor,ye?860:boss?960:925,ye?194:180,ye?650:boss?530:600,ye?975:boss?840:900);
   portraitRect=portrait.rectTransform;portrait.raycastTarget=true;portrait.alphaHitTestMinimumThreshold=.12f;
   if(n.kind=="invite"){portrait.sprite=art["xiaoye-invite"];StartCoroutine(InviteEntrance(portraitRect));}
   bool narrative=string.IsNullOrEmpty(n.who)||n.kind=="choice";
   portrait.color=narrative?new Color(.79f,.83f,.8f,.91f):Color.white;
   var hit=portrait.gameObject.AddComponent<PortraitHit>();hit.game=this;hit.actorKey=activeActor;
   var member=cast.members.First(m=>m.key==activeActor);
   string tag=!string.IsNullOrEmpty(n.chat)?"手机那头 · "+member.name:narrative?member.name+" · 场景叙述":n.who+" / "+member.note;
   T(stage,tag,1040,606,470,33,17,cream,TextAnchor.MiddleRight);
  }
  string SpeakerLabel(Beat n){
   if(n.kind=="choice")return "接一句话";
   if(string.IsNullOrEmpty(n.who))return "旁白";
   return n.who+(!string.IsNullOrEmpty(n.chat)?" · 消息":"");
  }
  string DisplayLine(Beat n){
   if(string.IsNullOrEmpty(n.chat))return n.text;
   if(n.kind=="choice")return "选一句回过去。聊天不会决定什么大事。";
   if(string.IsNullOrEmpty(n.who))return n.text;
   if(n.who==ActiveName)return ActiveName+"回了一句。";
   return n.kind=="message"?"手机振了一下。":"对面回了一句。";
  }
 }
}
