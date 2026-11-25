using System.Linq;
using UnityEngine;
using UnityEditor.SceneManagement;
public static class KleverBoardProbe
{
 public static void Run(){
 EditorSceneManager.OpenScene("Assets/Scenes/KlassMeta.unity",OpenSceneMode.Single);
 var room=Object.FindObjectsOfType<ChangeClassRoom>(true).Single();
 foreach(var board in new[]{room.boardA,room.boardB,room.boardC}) {
 Debug.Log("[BOARD-PROBE] root="+board.name+" pos="+board.transform.position+" rotation="+board.transform.eulerAngles+" scale="+board.transform.lossyScale);
 foreach(var r in board.GetComponentsInChildren<Renderer>(true))Debug.Log("[BOARD-PROBE] renderer="+r.name+" bounds="+r.bounds+" local="+r.localBounds+" rotation="+r.transform.eulerAngles+" scale="+r.transform.lossyScale);
 }
 foreach(var c in Object.FindObjectsOfType<Canvas>(true).Where(c=>c.name=="Controller Panel"))Debug.Log("[BOARD-PROBE] panel="+c.transform.position+" scale="+c.transform.lossyScale+" rotation="+c.transform.eulerAngles+" rect="+((RectTransform)c.transform).rect);
 }
}