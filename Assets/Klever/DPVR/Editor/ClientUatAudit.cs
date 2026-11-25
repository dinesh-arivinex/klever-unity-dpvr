#if KLEVER_DPN
using System;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace Klever.DPVR.Editor
{
    public static class ClientUatAudit
    {
        public static void RunAndBuild()
        {
            Klever.VR.Editor.UpdateAudit.Run();
            Klever.VR.Editor.ReliabilityAudit.Run();
            Klever.VR.Editor.ClassroomBoardAudit.Run();
            Klever.VR.Editor.MqttPauseAudit.Run();
            DpvrSessionAudit.Run();
            DpvrLessonAudit.Run();
            var manager = new GameObject("UAT interaction manager").AddComponent<UnityEngine.XR.Interaction.Toolkit.XRInteractionManager>();
            var root = new GameObject("Offset imported model");
            var mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mesh.transform.SetParent(root.transform, false);
            mesh.transform.localPosition = new Vector3(10, -5, 7);
            mesh.transform.localScale = new Vector3(3, 4, 5);
            root.transform.localScale = new Vector3(0.5f, 2f, 1.5f);
            var interaction = DpvrLessonInteraction.ConfigureRoot(root, manager);
            var anchor = new Vector3(1000, 0.65f, 1000);
            DpvrLessonInteraction.PlaceOnTable(root, anchor);
            var bounds = root.GetComponent<BoxCollider>().bounds;
            if (Mathf.Abs(bounds.center.x - anchor.x) > 0.001f || Mathf.Abs(bounds.center.z - anchor.z) > 0.001f || Mathf.Abs(bounds.min.y - anchor.y - 0.02f) > 0.001f || Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)) > 0.651f)
                throw new Exception("Imported pivot/scale placement missed the tabletop.");
            var constrained = DpvrLessonInteraction.ConstrainCenter(anchor + new Vector3(100, -100, 100), anchor, 0.3f);
            if (Vector2.Distance(new Vector2(constrained.x, constrained.z), new Vector2(anchor.x, anchor.z)) > 0.451f || constrained.y < anchor.y + 0.319f) throw new Exception("Model escaped the child interaction area.");
            var enabled = new GameObject("Enabled input" );
            var disabled = new GameObject("Previously disabled input"); disabled.SetActive(false);
            var pause = new GameObject("UAT pause").AddComponent<LessonPause>();
            pause.Apply(true, new[] { enabled, null, disabled });
            if (Time.timeScale != 0 || !AudioListener.pause || enabled.activeSelf) throw new Exception("Pause did not freeze input/time/audio.");
            if (DpvrSessionPresentation.Instance.CurrentStage != DpvrSessionPresentation.Stage.Paused || !DpvrSessionPresentation.Instance.IsVisible) throw new Exception("Pause did not show visible student feedback.");
            pause.Apply(false, null);
            if (Time.timeScale != 1 || AudioListener.pause || !enabled.activeSelf || disabled.activeSelf) throw new Exception("Resume did not restore the exact input state.");
            var outbox = new Klass.Net.LearningEventOutbox("editor-uat", Guid.NewGuid().ToString("N"));
            var node = new SimpleJSON.JSONObject(); node["experienceId"] = "audit"; node["clientSessionId"] = "audit"; node["sequence"] = "1";
            outbox.Add(node); int sent = 0; outbox.SendPending(_ => sent++);
            outbox.Acknowledge(node); outbox.SendPending(_ => sent++);
            if (sent != 1) throw new Exception("Durable outbox acknowledgement failed.");
            var nullChoice = new SimpleJSON.JSONObject(); nullChoice["selectedOptionId"] = (string)null; nullChoice["selectedText"] = "Actual learner choice";
            bool reproduced = false; try { nullChoice.ToString(); } catch (NullReferenceException) { reproduced = true; }
            if (!reproduced) throw new Exception("Optional-ID serialization fixture did not reproduce the bundled JSON failure.");
            var answerNode = new SimpleJSON.JSONObject(); answerNode["experienceId"] = "audit"; answerNode["clientSessionId"] = "audit"; answerNode["sequence"] = "2"; answerNode["response"] = nullChoice;
            outbox.Add(answerNode);
            outbox.SendPending(record => { if (record["response"]["selectedOptionId"].Value != string.Empty || record["response"]["selectedText"].Value != "Actual learner choice") throw new Exception("Optional-ID normalization changed the actual choice."); sent++; });
            if (sent != 2) throw new Exception("An answer with a missing optional ID was lost.");
            outbox.Acknowledge(answerNode);
            var handler = new GameObject("UAT command ordering").AddComponent<DeviceSynHandler>();
            handler.isOpenGame = true;
            var control = typeof(DeviceSynHandler).GetMethod("HandleSessionControl", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var newer = new SimpleJSON.JSONObject(); newer["ts"] = "2000"; newer["isStart"] = true; newer["isPaused"] = true;
            control.Invoke(handler, new object[] { newer });
            var older = new SimpleJSON.JSONObject(); older["ts"] = "1000"; older["isStart"] = true; older["isPaused"] = false;
            control.Invoke(handler, new object[] { older });
            if (Time.timeScale != 0) throw new Exception("An old retained command resumed a paused lesson.");
            var resume = new SimpleJSON.JSONObject(); resume["ts"] = "3000"; resume["isStart"] = true; resume["isPaused"] = false;
            control.Invoke(handler, new object[] { resume });
            if (Time.timeScale != 1) throw new Exception("A fresh Resume command was ignored.");
            Debug.Log("[CLIENT-UAT-AUDIT] PASS: offset model bounds/table placement, movement constraints, null-safe pause/resume and database-acknowledged outbox.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            DpvrLegacyBuildPipeline.BuildTestAndroid();
        }
        public static void InspectScene()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/KlassMeta.unity", OpenSceneMode.Single);
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
                if (t.gameObject.scene.IsValid() && (t.name == "Teacher Table" || t.name == "Table" || t.name.Contains("InstantCube") || t.name == "CollectionPointCloneScene"))
                {
                    Debug.Log("[UAT-GEOMETRY] " + t.name + " position=" + t.position + " active=" + t.gameObject.activeInHierarchy);
                    foreach (var r in t.GetComponentsInChildren<MeshRenderer>(true))
                        Debug.Log("[UAT-GEOMETRY] renderer=" + r.name + " bounds=" + r.bounds);
                }
            foreach (var a in Resources.FindObjectsOfTypeAll<AssetDownloader>())
                if (a.gameObject.scene.IsValid()) Debug.Log("[UAT-GEOMETRY] authored anchor=" + a.instantiationPoint.name + " world=" + a.instantiationPoint.position);
        }
    }
}
#endif
