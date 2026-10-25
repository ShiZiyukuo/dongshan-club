using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace DongshanClub {
 public partial class CampusGame {
  IEnumerator RunQA(){
   Directory.CreateDirectory(qaDir);var report=new List<string>();int errors=0,total=0,interactions=0,movies=0;var checkedCast=new HashSet<string>();
   PlayerPrefs.SetInt("dc_qa_completed",0);PlayerPrefs.SetInt("dc_qa_president_completed",0);
   if(!Environment.GetCommandLineArgs().Contains("--playground-qa")){
    yield return RunWorkshopQA(report);errors+=workshopQaErrors;
    if(Environment.GetCommandLineArgs().Contains("--workshop-qa")){
     report.Add("Errors: "+errors);File.WriteAllLines(Path.Combine(qaDir,"runtime-report.txt"),report.ToArray());
     Debug.Log("CAMPUS_WORKSHOP_QA_COMPLETE errors="+errors);Application.Quit(errors==0?0:2);yield break;
    }
   }
   yield return RunPlaygroundQA(report);errors+=playgroundQaErrors;
   if(Environment.GetCommandLineArgs().Contains("--playground-qa")){
    report.Add("Errors: "+errors);File.WriteAllLines(Path.Combine(qaDir,"runtime-report.txt"),report.ToArray());
    Debug.Log("CAMPUS_PLAYGROUND_QA_COMPLETE errors="+errors);Application.Quit(errors==0?0:2);yield break;
   }
   ShowTitle();yield return null;
   bool intro=stage.GetComponentsInChildren<Text>().Any(t=>t.text==OpeningWords);
   bool entries=stage.GetComponentsInChildren<Button>().Any(b=>b.name=="Hotspot:笔记本")&&stage.GetComponentsInChildren<Button>().Any(b=>b.name=="Hotspot:印章");
   if(!intro||!entries)errors++;report.Add("Hub exact opening words: "+intro+"; two physical entries: "+entries);
   bool regionalTitle=stage.GetComponentsInChildren<Text>().Any(t=>t.text=="东山有戏")&&stage.GetComponentsInChildren<Text>().Any(t=>t.text=="这关一起过");
   bool localTheme=stage.GetComponentsInChildren<Image>().Any(im=>im.name=="DongshanSeal")&&stage.GetComponentsInChildren<Image>().Count(im=>im.name=="LatticeLine")>10;
   if(!regionalTitle||!localTheme)errors++;report.Add("New title and regional UI="+regionalTitle+"/"+localTheme);
   yield return Shot("01-hub");
   bool privatePosters=!stage.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("招新海报"))&&!stage.GetComponentsInChildren<Image>().Any(im=>im.sprite==art["recruitment-main"]||im.sprite==art["recruitment-tech"]);
   bool noWhiteFrame=notebookHover.GetComponent<Button>().transition==Selectable.Transition.None&&stampHover.GetComponent<Button>().transition==Selectable.Transition.None;
   ResetHubHovers();notebookHover.OnPointerEnter(null);yield return new WaitForSecondsRealtime(.3f);
   bool coverOpened=notebookHover.openCover.color.a>.99f&&notebookHover.closedCover.color.a<.01f;yield return Shot("hub-book-hover");
   notebookHover.OnPointerExit(null);yield return new WaitForSecondsRealtime(.3f);
   bool coverClosed=notebookHover.openCover.color.a<.01f&&notebookHover.closedCover.color.a>.99f;
   var stampRest=stampHover.floatingObject.anchoredPosition;stampHover.OnPointerEnter(null);yield return new WaitForSecondsRealtime(.3f);
   bool stampRaised=stampHover.floatingObject.anchoredPosition.y>stampRest.y+10;yield return Shot("hub-stamp-hover");
   stampHover.OnPointerExit(null);yield return new WaitForSecondsRealtime(.3f);
   bool stampReturned=Vector2.Distance(stampRest,stampHover.floatingObject.anchoredPosition)<.01f;
   if(!privatePosters||!noWhiteFrame||!coverOpened||!coverClosed||!stampRaised||!stampReturned)errors++;
   report.Add("Hub posters inside card only="+privatePosters+" no white highlight="+noWhiteFrame+" book open/close="+coverOpened+"/"+coverClosed+" stamp rise/return="+stampRaised+"/"+stampReturned);
   if(!OpeningWords.Contains("焦虑和迷茫")||OpeningWords.Contains("焦虑的迷茫"))errors++;
   stage.GetComponentsInChildren<Button>().First(b=>b.name=="Hotspot:活动卡").onClick.Invoke();yield return null;
   bool posterOne=recruitmentOriginal!=null&&recruitmentOriginal.width==128&&recruitmentOriginal.height==192;
   yield return Shot("recruitment-main-fit");
   overlay.GetComponentsInChildren<Button>().First(b=>b.name=="Button:看底部二维码").onClick.Invoke();yield return null;
   bool zoomed=recruitmentZoom>=2.5f&&recruitmentScroll.verticalNormalizedPosition<.01f&&recruitmentScroll.horizontalNormalizedPosition>.99f;yield return Shot("recruitment-main-qr");
   overlay.GetComponentsInChildren<Button>().First(b=>b.name=="Button:下一张").onClick.Invoke();yield return null;
   bool posterTwo=recruitmentOriginal!=null&&recruitmentOriginal.width==128&&recruitmentOriginal.height==192;
   bool group=overlay.GetComponentsInChildren<Text>().Any(t=>t.text=="公开版不提供群号");yield return Shot("recruitment-tech-fit");
   overlay.GetComponentsInChildren<Button>().First(b=>b.name=="Button:看底部二维码").onClick.Invoke();yield return null;zoomed=zoomed&&recruitmentScroll.horizontalNormalizedPosition<.01f;yield return Shot("recruitment-tech-qr");
   CloseModal();bool released=recruitmentOriginal==null&&!modal;if(!posterOne||!posterTwo||!zoomed||!group||!released)errors++;
   report.Add("Recruitment original posters="+posterOne+"/"+posterTwo+" zoomBottom="+zoomed+" group="+group+" closeReleased="+released);
   foreach(var key in new[]{"president","xiaoye"}){
    InspectChapter(key);yield return null;yield return Shot(key+"-entry");
    NewGame();int phones=0;
    for(int i=0;i<story.nodes.Length;i++){
     ShowNode(i);yield return null;revealed=true;dialogue.text=DisplayLine(Current);
     Canvas.ForceUpdateCanvases();
     if(dialogue.preferredHeight>dialogue.rectTransform.rect.height+4){errors++;report.Add("Dialogue overflow "+key+" "+Current.id+" height="+dialogue.preferredHeight);}
     if(Current.id=="n80"||Current.id=="n120"||Current.id=="p200")yield return Shot(key+"-regional-"+Current.id);
     var n=Current;total++;
     if(!string.IsNullOrEmpty(activeActor)){
      if(portrait==null||portrait.sprite!=art[n.kind=="invite"?"xiaoye-invite":activeActor]){errors++;report.Add("Wrong portrait "+key+" "+n.id);}
      if(checkedCast.Add(activeActor))yield return Shot("cast-"+activeActor);
     }
     if(string.IsNullOrEmpty(n.who)&&n.kind!="choice"&&speaker.text!="旁白")errors++;
     if(n.chat!=null){
      phones++;if(phoneRows==null||phoneScroll==null)errors++;
      if(phoneRows.Any(r=>r.who==ActiveName)&&phoneRows.Any(r=>!r.system&&r.who!=ActiveName)&&phones%5==0)yield return Shot(key+"-phone-"+phones);
     }
     if(n.kind=="paper"||n.kind=="snack"||n.kind=="attachments"){
      if(dragCallback==null){errors++;}else{yield return Shot(key+"-"+n.kind);dragCallback();if(!activityDone)errors++;interactions++;}
     }
     if(n.kind=="lesson"){
      OpenWorkshop();yield return null;bool storyModal=modal&&workshop!=null;
      CloseWorkshop();bool restored=!modal&&workshop==null&&!activityDone&&Current.id==n.id;
      if(!storyModal||!restored)errors++;report.Add("Lesson workshop entry and unfinished exit restored="+(storyModal&&restored));
      activityDone=true;
     }
     if(n.kind=="notice"||n.kind=="stamp"){
      if(presidentComplete==null)errors++;else{
       if(n.kind=="notice"){
        stage.GetComponentsInChildren<Button>().First(b=>b.name=="Button:仍发 18:30").onClick.Invoke();if(activityDone)errors++;
       }else stage.GetComponentsInChildren<Button>().First(b=>b.name=="Button:转右一点").onClick.Invoke();
       presidentComplete();if(!activityDone)errors++;yield return Shot("president-"+n.kind);interactions++;
      }
     }
     if(n.kind=="evidence"){
      stage.GetComponentsInChildren<Button>().First(b=>b.name=="Button:操作说明").onClick.Invoke();CloseModal();
      stage.GetComponentsInChildren<Button>().First(b=>b.name=="Button:先按WASD试试").onClick.Invoke();if(!activityDone)errors++;interactions++;
     }
     if(n.kind=="memory"){
      OpenMemory();yield return null;var bs=overlay.GetComponentsInChildren<Button>();
      bs.First(b=>b.name=="Button:按 E / 交互").onClick.Invoke();if(solved)errors++;
      bs.First(b=>b.name=="Button:走进浅蓝色区域").onClick.Invoke();bs.First(b=>b.name=="Button:按 E / 交互").onClick.Invoke();if(!solved)errors++;
      yield return Shot("xiaoye-memory");bs.First(b=>b.name=="Button:回到刚才").onClick.Invoke();if(!activityDone||modal)errors++;interactions++;
     }
     if(n.kind=="media"){
      OpenVideo(n.media);yield return null;
      bool notice=modal&&moviePlayer==null&&overlay.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("私人录像未纳入公开仓库"));
      if(!notice)errors++;report.Add("Public media notice "+n.media+": "+notice);
      CloseModal();movies++;
     }
     if(i==15&&portrait!=null){ToggleHidden();bool retained=portrait.gameObject.activeInHierarchy&&background.gameObject.activeInHierarchy&&!stage.gameObject.activeSelf;if(!retained)errors++;report.Add(key+" hidden portrait retained: "+retained);yield return Shot(key+"-hidden");ToggleHidden();}
     if(n.kind=="invite"){
      yield return new WaitForSecondsRealtime(.4f);yield return Shot(key+"-invitation");
      ToggleHidden();bool kept=portrait!=null&&portrait.gameObject.activeInHierarchy;ToggleHidden();
      Poke("xiaoye");bool accepted=won&&stage.GetComponentsInChildren<Text>().Any(t=>t.text=="行，那就一起。");
      if(!kept||!accepted)errors++;report.Add(key+" shared invitation visibleHidden="+kept+" accepted="+accepted);
     }else if(n.kind=="choice"){
      int before=index;Choose(0);if(index!=before+1||!pickedReplies.ContainsKey(n.id))errors++;
     }else if(n.kind!="ending"){
      int before=index;Advance();if(index!=before+1){errors++;report.Add("Blocked "+key+" "+n.id+" kind="+n.kind);}
     }
    }
    ShowNode(Math.Min(25,story.nodes.Length-1));Save(2,false);string expected=Current.id;ShowNode(0);if(!Load(2)||Current.id!=expected)errors++;
    report.Add(key+" nodes="+story.nodes.Length+" chapters="+story.chapters.Length+" phone="+phones+" save/load checked");
    ShowCredits();yield return Shot(key+"-ending");
    stage.GetComponentsInChildren<Button>().First(b=>b.name=="Button:怎么加入").onClick.Invoke();yield return null;
    bool endingPoster=recruitmentOriginal!=null&&modal;if(!endingPoster)errors++;report.Add(key+" ending recruitment poster: "+endingPoster);CloseModal();
    ShowTitle();yield return null;
   }
   // A real switch between both stories must leave each story's file untouched.
   SelectStory("xiaoye");ShowNode(50);Save(1,false);string xPath=SavePath(1),xContents=File.ReadAllText(xPath),xId=Current.id;
   SelectStory("president");ShowNode(31);Save(1,false);string pPath=SavePath(1),pId=Current.id;
   bool separated=xPath!=pPath&&File.ReadAllText(xPath)==xContents;
   SelectStory("xiaoye");bool xRestore=Load(1)&&Current.id==xId;SelectStory("president");bool pRestore=Load(1)&&Current.id==pId;
   if(!separated||!xRestore||!pRestore)errors++;report.Add("Branch save isolation="+separated+" xiaoye="+xRestore+" president="+pRestore);
   SelectStory("xiaoye");File.WriteAllText(SavePath(3),JsonUtility.ToJson(new SaveState{index=229,date="legacy"}));
   bool legacy=Load(3)&&Current.id=="n229";if(!legacy)errors++;report.Add("Legacy index mapped to unchanged node ID: "+legacy);
   foreach(var key in new[]{"founder","xiaoye"}){
    ShowNode(Array.FindIndex(story.nodes,n=>n.actor==key&&string.IsNullOrEmpty(n.chat)));yield return null;lastPoke=-10;var origin=portraitRect.anchoredPosition;Poke(key);yield return new WaitForSecondsRealtime(.24f);
    string pose=key=="founder"?"founder-explain":"xiaoye-sleep";bool changed=portrait.sprite==art[pose]&&Mathf.Approximately(portraitRect.anchoredPosition.x,origin.x);yield return Shot("reaction-"+key);
    yield return new WaitForSecondsRealtime(2.1f);bool restored=portrait.sprite==art[key]&&Vector2.Distance(portraitRect.anchoredPosition,origin)<.01f;if(!changed||!restored)errors++;report.Add("Pose "+key+" changed="+changed+" restored="+restored);
   }
   SelectStory("president");var own=story.nodes.First(n=>n.chat!=null&&n.who=="社长");ShowNode(Array.IndexOf(story.nodes,own));yield return null;
   bool sent=stage.GetComponentsInChildren<Image>().Any(im=>im.name=="Bubble:sent");if(!sent)errors++;report.Add("President own messages are outgoing: "+sent);
   ShowTitle();yield return Shot("02-hub-completed");
   report.Add("Story nodes rendered: "+total);report.Add("Cast checked: "+checkedCast.Count);report.Add("Interactive checks: "+interactions);report.Add("Recordings checked: "+movies);report.Add("Errors: "+errors);
   File.WriteAllLines(Path.Combine(qaDir,"runtime-report.txt"),report.ToArray());Debug.Log("CAMPUS_QA_COMPLETE errors="+errors);Application.Quit(errors==0?0:2);
  }
 }
}
