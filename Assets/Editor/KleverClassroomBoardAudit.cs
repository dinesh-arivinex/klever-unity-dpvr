using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace Klever.VR.Editor
{
 public static class ClassroomBoardAudit
 {
  public static void Run()
  {
   EditorSceneManager.OpenScene("Assets/Scenes/KlassMeta.unity",OpenSceneMode.Single);
   var room=UnityEngine.Object.FindObjectsOfType<ChangeClassRoom>(true).Single();
   foreach(var camera in UnityEngine.Object.FindObjectsOfType<Camera>(true)) camera.gameObject.SetActive(false);
   var eye=new GameObject("Board audit eye").AddComponent<Camera>(); eye.tag="MainCamera"; eye.transform.position=new Vector3(0,1.6f,0); eye.fieldOfView=68; eye.aspect=16f/9;
   foreach(var existing in UnityEngine.Object.FindObjectsOfType<Canvas>(true).Where(c=>c.name=="Controller Panel")) existing.gameObject.SetActive(false);
   var panel=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Controller Instruction Prefab/Controller Panel.prefab"));
#if KLEVER_DPN
   var dpvr=panel.AddComponent<Klever.DPVR.DpvrControllerGuide>(); dpvr.Build();
#endif
   panel.name="Controller Panel";
   ClassroomBranding.Configure(room);
   var corners=new Vector3[4];
   for(int index=0;index<3;index++)
   {
    var selected=new[]{room.boardA,room.boardB,room.boardC}[index];
    room.AssignClassRoom("classEnvironment "+(index+1));
    var originalRotation=selected.transform.rotation;
    if(new[] {room.boardA,room.boardB,room.boardC}.Count(b=>b.activeSelf)!=1) throw new Exception("Previous classroom board is still active.");
    var surface=ClassroomBoardLayout.Surface(room); if(surface==null) throw new Exception("Board missing in classroom "+index);
    ClassroomBoardLayout.Fit((RectTransform)panel.transform,surface,eye.transform.position);
    var scale=panel.transform.localScale;
    for(int repeat=0;repeat<20;repeat++) ClassroomBoardLayout.Fit((RectTransform)panel.transform,surface,eye.transform.position);
    if(Vector3.Distance(scale,panel.transform.localScale)>0.000001f) throw new Exception("Guide scale drifts while fitting.");
    if(Vector3.Dot(panel.transform.up,Vector3.up)<0.999f)throw new Exception("Guide inherits board roll.");
    if(Quaternion.Angle(selected.transform.rotation,originalRotation)>0.001f)throw new Exception("Guide fitting changed the classroom board geometry.");
    var frame=ClassroomBoardLayout.Measure(surface,eye.transform.position);

    foreach(var rect in panel.GetComponentsInChildren<RectTransform>()) {
    rect.GetWorldCorners(corners);
    foreach(var corner in corners){var direction=corner-eye.transform.position; float t=Vector3.Dot(frame.Center-eye.transform.position,frame.Forward)/Vector3.Dot(direction,frame.Forward); var projected=eye.transform.position+direction*t; var offset=projected-frame.Center;
     if(Mathf.Abs(Vector3.Dot(offset,frame.Right))>frame.Width*0.5f-0.02f || Mathf.Abs(Vector3.Dot(offset,frame.Up))>frame.Height*0.5f-0.02f)throw new Exception("Instruction canvas escapes classroom board "+index);
    }
    }
    Canvas.ForceUpdateCanvases();
    foreach(var text in panel.GetComponentsInChildren<TMP_Text>()) {text.ForceMeshUpdate(); if(text.isTextOverflowing) throw new Exception("Instruction text overflows: "+text.name);}
#if KLEVER_DPN
    room.webManager.FitSimulationToBoard(room);
    var viewport = (RectTransform)room.webManager._webViewPrefab.transform;
    var initialScale = viewport.localScale;
    room.webManager.FitSimulationToBoard(room);
    if (Vector3.Distance(initialScale, viewport.localScale) > 0.000001f) throw new Exception("WebView scale drifts when fitted again.");
    foreach (var mask in viewport.GetComponentsInParent<Mask>(true))
     if (mask.enabled) throw new Exception("Active UI Mask clips moved board WebView: " + mask.name);
    foreach (var rectMask in viewport.GetComponentsInParent<RectMask2D>(true))
     if (rectMask.enabled) throw new Exception("Active RectMask2D clips moved board WebView: " + rectMask.name);
    var webRaw = viewport.GetComponentInChildren<RawImage>(true);
    if (webRaw == null || webRaw.maskable || !webRaw.raycastTarget)
     throw new Exception("Board WebView RawImage must be unmasked and raycastable.");
    var next = (RectTransform)room.webManager.buttonNext.transform;
    var nextCanvas = next.GetComponentInParent<Canvas>(true);
    var nextButton = next.GetComponent<Button>();
    var nextImage = next.GetComponent<Image>();
    if (nextCanvas == null || nextCanvas.renderMode != RenderMode.WorldSpace || nextButton == null || !nextButton.enabled || !nextButton.interactable || nextImage == null || !nextImage.enabled || !nextImage.raycastTarget)
     throw new Exception("Board Simulation Proceed Button is missing an active WorldSpace Canvas/Button/Image.");
    var clock = room.webManager.timer != null ? room.webManager.timer.transform.parent as RectTransform : null;
    foreach (var control in clock != null ? new[] { viewport, next, clock } : new[] { viewport, next })
    {
    control.GetWorldCorners(corners);
    foreach (var corner in corners)
    {
     var offset = corner - frame.Center;
     if (Mathf.Abs(Vector3.Dot(offset, frame.Right)) >= frame.Width * 0.5f ||
         Mathf.Abs(Vector3.Dot(offset, frame.Up)) >= frame.Height * 0.5f ||
         Mathf.Abs(Vector3.Dot(offset, frame.Forward) + frame.Depth + 0.008f) > 0.002f)
      throw new Exception("2D simulation control escapes the face of classroom board " + index);
    }
    }
    var viewSpan = MeasureProjectedSpan(viewport, frame);
    var nextSpan = MeasureProjectedSpan(next, frame);
    var clockSpan = clock != null ? MeasureProjectedSpan(clock, frame) : default;
    if (viewSpan.height < frame.Height * 0.78f || viewSpan.width < frame.Width * 0.65f)
     throw new Exception("2D simulation screen is too small on classroom board " + index + ": " + viewSpan);
    if (nextSpan.height > frame.Height * 0.08f || nextSpan.width > frame.Width * 0.22f ||
        (clock != null && (clockSpan.height > frame.Height * 0.08f || clockSpan.width > frame.Width * 0.28f)))
     throw new Exception("2D simulation timer or Next button is too large on classroom board " + index);
    if (nextSpan.top >= viewSpan.bottom - 0.005f || (clock != null && clockSpan.top >= viewSpan.bottom - 0.005f))
     throw new Exception("2D simulation footer controls overlap viewport on classroom board " + index);
    viewport.GetWorldCorners(corners);
    var uvBottomLeft = Klever.DPVR.DpvrBoardWebPointer.NormalizedPointFromWorld(viewport, corners[0]);
    var uvTopRight = Klever.DPVR.DpvrBoardWebPointer.NormalizedPointFromWorld(viewport, corners[2]);
    var uvCenter = Klever.DPVR.DpvrBoardWebPointer.NormalizedPointFromWorld(viewport, (corners[0] + corners[2]) * 0.5f);
    if (Vector2.Distance(uvBottomLeft, new Vector2(0f, 1f)) > 0.002f ||
        Vector2.Distance(uvTopRight, new Vector2(1f, 0f)) > 0.002f ||
        Vector2.Distance(uvCenter, new Vector2(0.5f, 0.5f)) > 0.002f)
     throw new Exception("DpvrBoardWebPointer normalized coordinate mapping failed on classroom board " + index);
    Debug.Log("[DPVR-SIMULATION-AUDIT] room=" + (index + 1) + " unmasked board WebView, compact footer, enlarged viewport and ray UV mapping PASS");
#endif
    var brand=ClassroomBranding.Configure(room); brand.Refresh();
    if(brand.Badge.GetComponentsInChildren<Graphic>(true).Any(g=>g.raycastTarget) || brand.Badge.GetComponentsInChildren<Collider>(true).Length>0) throw new Exception("Branding blocks interactions.");
    if(!brand.Badge.gameObject.activeSelf)throw new Exception("Logo hidden in visible classroom.");
    ((RectTransform)brand.Badge.transform).GetWorldCorners(corners);
    foreach (var corner in corners)
    {
     var direction = corner - eye.transform.position;
     float t = Vector3.Dot(frame.Center - eye.transform.position, frame.Forward) / Vector3.Dot(direction, frame.Forward);
     var offset = eye.transform.position + direction * t - frame.Center;
#if KLEVER_DPN
     if (Vector3.Dot(offset, frame.Up) > -frame.Height * 0.5f - 0.02f)
      throw new Exception("Logo overlaps the board in classroom " + index);
#else
     if (Mathf.Abs(Vector3.Dot(offset, frame.Right)) > frame.Width * 0.5f - 0.01f ||
         Mathf.Abs(Vector3.Dot(offset, frame.Up)) > frame.Height * 0.5f - 0.01f)
      throw new Exception("Logo outside board corner.");
#endif
    }
    foreach(var mode in new[]{"video","image360","simulation"})if(ClassroomBranding.ShouldShow(room,surface,mode))throw new Exception("Logo leaks into immersive content.");
    room.classRooms[index].SetActive(false);brand.Refresh();if(brand.Badge.gameObject.activeSelf)throw new Exception("Logo remains when classroom is hidden.");
    room.classRooms[index].SetActive(true);surface.gameObject.SetActive(false);brand.Refresh();if(brand.Badge.gameObject.activeSelf)throw new Exception("Logo remains when board is hidden.");
    surface.gameObject.SetActive(true);brand.Refresh();
    if(Resources.Load<Texture2D>("KleverClassroomMark")==null) throw new Exception("Classroom logo asset missing.");
    eye.transform.LookAt(frame.Center,Vector3.up);
    var prefix=Environment.GetEnvironmentVariable("KLEVER_BOARD_PREVIEW");
    if(!string.IsNullOrEmpty(prefix)) Render(eye,prefix+"_ROOM"+(index+1)+".png");
    Debug.Log("[BOARD-LAYOUT-AUDIT] room="+(index+1)+" contained, text fits, scale stable, branding non-interactive PASS");
   }
   EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  }
  private static (float width, float height, float bottom, float top) MeasureProjectedSpan(RectTransform rect, ClassroomBoardLayout.Frame frame)
  {
   var corners = new Vector3[4];
   rect.GetWorldCorners(corners);
   float minRight = float.PositiveInfinity, maxRight = float.NegativeInfinity;
   float minUp = float.PositiveInfinity, maxUp = float.NegativeInfinity;
   foreach (var corner in corners)
   {
    var offset = corner - frame.Center;
    float right = Vector3.Dot(offset, frame.Right);
    float up = Vector3.Dot(offset, frame.Up);
    if (right < minRight) minRight = right;
    if (right > maxRight) maxRight = right;
    if (up < minUp) minUp = up;
    if (up > maxUp) maxUp = up;
   }
   return (maxRight - minRight, maxUp - minUp, minUp, maxUp);
  }
  private static void Render(Camera eye,string path)
  {
   var target=new RenderTexture(1600,900,24); eye.targetTexture=target; eye.Render(); RenderTexture.active=target;
   var texture=new Texture2D(1600,900,TextureFormat.RGB24,false); texture.ReadPixels(new Rect(0,0,1600,900),0,0); texture.Apply(); File.WriteAllBytes(path,texture.EncodeToPNG());
   RenderTexture.active=null; eye.targetTexture=null; UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(target);
  }
 }
}