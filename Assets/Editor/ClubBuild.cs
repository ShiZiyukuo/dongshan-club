using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using DongshanClub;
public static class ClubBuild {
 [MenuItem("东山社团/生成启动场景")]
 public static void CreateScene(){
  Directory.CreateDirectory("Assets/Scenes");var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  new GameObject("ClubBootstrap").AddComponent<ClubBootstrap>();EditorSceneManager.SaveScene(scene,"Assets/Scenes/Club.unity");
  EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/Club.unity",true)};AssetDatabase.SaveAssets();
 }
 [MenuItem("东山社团/打包Windows试玩版")]
 public static void BuildWindows(){
  ValidateStory();CreateScene();
  if(AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/UIKeep.mat")==null){var shader=Shader.Find("UI/Default");if(shader!=null)AssetDatabase.CreateAsset(new Material(shader),"Assets/Resources/UIKeep.mat");}
  if(AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/PlaygroundKeep.mat")==null){var shader=Shader.Find("Standard");if(shader!=null)AssetDatabase.CreateAsset(new Material(shader),"Assets/Resources/PlaygroundKeep.mat");}
  PlayerSettings.companyName="DongshanClub";PlayerSettings.productName="这破班谁爱管谁管";PlayerSettings.bundleVersion="0.8.0";
  PlayerSettings.defaultScreenWidth=1600;PlayerSettings.defaultScreenHeight=900;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
  PlayerSettings.resizableWindow=true;PlayerSettings.runInBackground=true;
  PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone,ScriptingImplementation.Mono2x);
  PlayerSettings.colorSpace=ColorSpace.Gamma;PlayerSettings.stripEngineCode=false;PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.Standalone,ApiCompatibilityLevel.NET_Standard_2_0);
  foreach(var guid in AssetDatabase.FindAssets("t:Texture2D",new[]{"Assets/Resources/Art"})){
   var p=AssetDatabase.GUIDToAssetPath(guid);var importer=AssetImporter.GetAtPath(p) as TextureImporter;if(importer==null)continue;
   importer.textureType=TextureImporterType.Default;importer.isReadable=true;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
  }
  Directory.CreateDirectory("Builds/Windows-v0.8.0");
  var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/Club.unity"},locationPathName="Builds/Windows-v0.8.0/SXUGame DevClub.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
  Debug.Log("CLUB_BUILD_RESULT "+result.summary.result+" errors="+result.summary.totalErrors);
  if(result.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Build failed: "+result.summary.result);
 }
 public static void ValidateStory(){
  var cast=JsonUtility.FromJson<CastBook>(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/cast.json").text);
  foreach(var member in cast.members)if(AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/Art/"+member.key+".png")==null)throw new Exception("Missing cast art "+member.key);
  foreach(var key in new[]{"story","story-president"}){
   var story=JsonUtility.FromJson<Story>(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/"+key+".json").text);
   if(story.chapters.Length<1||story.nodes.Length<100)throw new Exception("Story incomplete "+key);
   if(story.nodes.Select(n=>n.id).Distinct().Count()!=story.nodes.Length)throw new Exception("Duplicate IDs "+key);
   foreach(var n in story.nodes){
    if(n.chapter<0||n.chapter>=story.chapters.Length||string.IsNullOrWhiteSpace(n.text))throw new Exception("Invalid node "+n.id);
    if(n.kind=="choice"&&(n.choices==null||n.replies==null||n.choices.Length!=n.replies.Length))throw new Exception("Invalid choice "+n.id);
    if(n.who!="小叶子的文档"&&!string.IsNullOrEmpty(n.who)&&string.IsNullOrEmpty(n.actor))throw new Exception("Unassigned speaker "+n.id);
   }
   Debug.Log("CLUB_STORY_VALIDATED "+key+" nodes="+story.nodes.Length+" chapters="+story.chapters.Length);
  }
  foreach(var key in new[]{"chapter-table","president","founder-explain","xiaoye-sleep","xiaoye-invite"})if(AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/Art/"+key+".png")==null)throw new Exception("Missing art "+key);
  foreach(var key in new[]{"renpy","fish","neural"})if(!File.Exists("Assets/StreamingAssets/Media/"+key+".mp4"))throw new Exception("Missing recording "+key);
 }
}
