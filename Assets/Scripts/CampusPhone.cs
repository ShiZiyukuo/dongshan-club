using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace DongshanClub {
 public partial class CampusGame {
  class ChatRow { public string who,text; public bool system; public ChatRow(string person,string content,bool note=false){who=person;text=content;system=note;} }
  Sprite roundedUI; ScrollRect phoneScroll; List<ChatRow> phoneRows;

  Image RoundPanel(Transform parent,float x,float y,float w,float h,Color color,string name="RoundPanel"){
   if(roundedUI==null){
    const int size=64;const float radius=19f;var tex=new Texture2D(size,size,TextureFormat.RGBA32,false);var pixels=new Color[size*size];
    for(int yy=0;yy<size;yy++)for(int xx=0;xx<size;xx++){
     float dx=Mathf.Max(Mathf.Abs(xx-31.5f)-(32-radius),0),dy=Mathf.Max(Mathf.Abs(yy-31.5f)-(32-radius),0);
     pixels[yy*size+xx]=new Color(1,1,1,Mathf.Clamp01(radius+.5f-Mathf.Sqrt(dx*dx+dy*dy)));
    }
    tex.SetPixels(pixels);tex.Apply(false,true);roundedUI=Sprite.Create(tex,new Rect(0,0,size,size),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(20,20,20,20));
   }
   var im=Panel(parent,x,y,w,h,color,name);im.sprite=roundedUI;im.type=Image.Type.Sliced;return im;
  }
  void PhoneAvatar(Transform parent,string person,float x,float y){
   var key=ActorKey(person);var bg=RoundPanel(parent,x,y,39,39,new Color32(214,223,205,255),"Avatar:"+person);
   bg.gameObject.AddComponent<Mask>().showMaskGraphic=true;
   if(!string.IsNullOrEmpty(key)&&art.ContainsKey(key)){
    var raw=R("Face",bg.transform,0,0,39,39).gameObject.AddComponent<RawImage>();raw.texture=art[key].texture;
    raw.uvRect=key=="xiaoye"?new Rect(.22f,.62f,.53f,.35f):new Rect(.22f,.63f,.56f,.35f);raw.raycastTarget=false;
   }else T(bg.transform,person.Length>0?person.Substring(0,1):"群",0,0,39,39,19,ink,TextAnchor.MiddleCenter);
  }
  List<ChatRow> BuildChatRows(Beat current){
   var rows=new List<ChatRow>();
   for(int i=0;i<=index;i++){
    var n=story.nodes[i];if(n.chat!=current.chat)continue;
    if(n.kind=="choice"){
     int chosen;if(!pickedReplies.TryGetValue(n.id,out chosen)||chosen<0||chosen>=n.choices.Length)continue;
     rows.Add(new ChatRow(ActiveName,n.choices[chosen]));
     foreach(Match reply in Regex.Matches(n.replies[chosen],@"(技术部部长|你)：(.*?)(?=(?:技术部部长|你)：|$)"))
      rows.Add(new ChatRow(reply.Groups[1].Value=="你"?ActiveName:reply.Groups[1].Value,reply.Groups[2].Value.Trim()));
    }else if(string.IsNullOrEmpty(n.who)) rows.Add(new ChatRow("",n.text,true));
    else foreach(var paragraph in n.text.Split(new[]{'\n'},StringSplitOptions.RemoveEmptyEntries))rows.Add(new ChatRow(n.who,paragraph));
   }
   return rows;
  }
  void RenderPhone(Beat n){
   string contact=string.IsNullOrEmpty(n.contact)?n.who:n.contact;
   var frame=RoundPanel(stage,185,188,470,650,new Color32(44,35,31,255),"PhoneFrame");activity=frame.gameObject;
   var screen=RoundPanel(frame.transform,9,9,452,632,new Color32(247,240,224,255),"PhoneScreen");
   T(screen.transform,DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)).ToString("HH:mm"),23,6,110,28,14,ink);
   RoundPanel(screen.transform,171,0,110,17,new Color32(44,35,31,255));
   for(int i=0;i<4;i++)Panel(screen.transform,348+i*5,22-(i*3),3,5+i*3,ink);
   RoundPanel(screen.transform,380,11,28,13,ink);RoundPanel(screen.transform,382,13,21,9,new Color32(148,181,133,255));Panel(screen.transform,409,15,2,5,ink);
   T(screen.transform,"‹",19,40,30,42,29,ink);T(screen.transform,contact,57,42,320,35,23,ink);
   T(screen.transform,contact=="社团群"?"一起做点东西":"消息",58,78,310,24,14,new Color32(117,99,81,255));
   T(screen.transform,"...",393,40,44,37,22,ink);Panel(screen.transform,15,106,422,1,new Color32(211,219,204,255));
   var wallpaper=Resources.Load<Texture2D>("Art/phone-wallpaper");
   if(wallpaper!=null){
    var bg=R("PhoneWallpaper",screen.transform,14,116,424,421).gameObject.AddComponent<RawImage>();
    bg.texture=wallpaper;bg.color=new Color(1,1,1,.40f);bg.raycastTarget=false;
    float fraction=(424f/421)/(wallpaper.width/(float)wallpaper.height);
    bg.uvRect=fraction<1?new Rect((1-fraction)/2,0,fraction,1):new Rect(0,(1-1/fraction)/2,1,1/fraction);
   }
   var viewport=R("ChatViewport",screen.transform,14,116,424,421);var clip=viewport.gameObject.AddComponent<Image>();clip.color=Color.white;viewport.gameObject.AddComponent<Mask>().showMaskGraphic=false;
   var content=R("Messages",viewport,0,0,424,421);
   RoundPanel(content,169,5,86,27,new Color32(241,242,233,235),"ChatTimeBacking");
   T(content,string.IsNullOrEmpty(n.chatTime)?"刚刚":n.chatTime,0,5,424,30,14,new Color32(130,142,129,255),TextAnchor.MiddleCenter);
   phoneRows=BuildChatRows(n);float top=45;
   foreach(var row in phoneRows){
    if(row.system){
     var note=T(content,row.text,28,top,368,90,15,new Color32(119,135,120,255),TextAnchor.UpperCenter);
     float height=Mathf.Max(30,note.preferredHeight+13);note.rectTransform.sizeDelta=new Vector2(368,height);top+=height+14;continue;
    }
    bool sent=row.who==ActiveName;const float maxWidth=310;
    PhoneAvatar(content,row.who,sent?377:8,top+2);
    if(!sent){RoundPanel(content,53,top-1,Mathf.Min(290,row.who.Length*15+14),25,new Color32(241,242,233,235),"SenderBacking");T(content,row.who,58,top,285,24,14,new Color32(106,77,55,255));}
    float textTop=sent?top:top+25;
    var bubble=RoundPanel(content,57,textTop,maxWidth,60,sent?new Color32(230,206,164,255):new Color32(255,254,248,255),sent?"Bubble:sent":"Bubble:received");
    var body=T(bubble.transform,row.text,14,9,maxWidth-28,200,20,ink);
    float width=Mathf.Clamp(body.preferredWidth+29,58,maxWidth);body.rectTransform.sizeDelta=new Vector2(width-28,200);
    float bh=Mathf.Max(47,body.preferredHeight+21);bubble.rectTransform.anchoredPosition=new Vector2(sent?364-width:57,-textTop);bubble.rectTransform.sizeDelta=new Vector2(width,bh);body.rectTransform.sizeDelta=new Vector2(width-28,bh-18);
    top=textTop+bh+19;
   }
   content.sizeDelta=new Vector2(424,Mathf.Max(421,top+12));
   phoneScroll=viewport.gameObject.AddComponent<ScrollRect>();phoneScroll.viewport=viewport;phoneScroll.content=content;phoneScroll.horizontal=false;phoneScroll.vertical=true;phoneScroll.movementType=ScrollRect.MovementType.Clamped;phoneScroll.scrollSensitivity=24;
   var compose=RoundPanel(screen.transform,14,552,424,53,new Color32(230,218,196,255));
   T(compose.transform,n.kind=="choice"?"从右侧选一句回复…":"上滑看前文 · 点继续",16,10,278,36,17,new Color32(99,121,102,255));
   var send=RoundPanel(compose.transform,315,6,101,41,new Color32(144,58,43,255));T(send.transform,"继续",0,0,101,41,18,cream,TextAnchor.MiddleCenter);
   send.gameObject.AddComponent<Button>().onClick.AddListener(()=>{ClickSound();if(n.kind=="choice")Toast("从右边选一句回复吧。");else Advance();});
   RoundPanel(screen.transform,158,619,136,4,new Color32(61,81,65,255));
   Canvas.ForceUpdateCanvases();phoneScroll.verticalNormalizedPosition=0;
  }
  void PhoneChoices(Beat n){
   var p=RoundPanel(stage,855,430,675,207,new Color32(242,238,218,249),"PhoneChoices");choices=p.gameObject;
   T(p.transform,"回一句",22,15,620,42,25,ink);
   for(int i=0;i<n.choices.Length;i++){int pick=i;B(p.transform,n.choices[i],22+i*215,78,201,59,()=>Choose(pick));}
   T(p.transform,"不用想太久，只是接个话。",22,158,625,36,18,new Color32(104,124,105,255));
  }
 }
}
