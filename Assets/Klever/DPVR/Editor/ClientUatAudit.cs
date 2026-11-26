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
            RunAuditsOnly();
            DpvrLegacyBuildPipeline.BuildTestAndroid();
        }

        public static void RunAuditsOnly()
        {
            Klever.VR.Editor.UpdateAudit.Run();
            Klever.VR.Editor.ReliabilityAudit.Run();
            Klever.VR.Editor.ClassroomBoardAudit.Run();
            Klever.VR.Editor.MqttPauseAudit.Run();
            DpvrSessionAudit.Run();
            DpvrLessonAudit.Run();
            if (VideoManager.DpvrPlaybackComplete(false, 35, 98, false, 0, 0) ||
                VideoManager.DpvrPlaybackComplete(false, 97.99, 98, false, 0, 0) ||
                !VideoManager.DpvrPlaybackComplete(true, 98, 98, false, 0, 0) ||
                VideoManager.DpvrPlaybackComplete(false, 29.99, 98, true, 10, 30) ||
                !VideoManager.DpvrPlaybackComplete(false, 30, 98, true, 10, 30))
                throw new Exception("Video must advance only at natural end or the authored YouTube cutoff.");
            Debug.Log("[DPVR-MEDIA-AUDIT] Full video and authored segment completion rules PASS.");
            AuditFillInEntry();
            AuditMatchItEntry();
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
        }

        private static void AuditFillInEntry()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/KlassMeta.unity", OpenSceneMode.Single);
            var game = UnityEngine.Object.FindObjectOfType<GamificationManager>(true);
            var quiz = game.FillIt.GetComponent<QuizManagers>();
            game.FillIt.SetActive(false);
            game.apiManager.assessmentsString = "[" +
                "{\"typeOfGame\":\"FillInTheBlanks\",\"question\":\"Gravity\",\"answer\":[{\"text\":\"Earth\"}]}," +
                "{\"typeOfGame\":\"FillInTheBlanks\",\"question\":\"Triangle\",\"answer\":[{\"text\":\"Three\"}]}," +
                "{\"typeOfGame\":\"FillInTheBlanks\",\"question\":\"Freezing\",\"answer\":[{\"text\":\"Zero\"}]}]";
            game.StartAfterLoadingCanvas();
            var rows = quiz.fillItReferenceManager.Questions;
            if (!game.FillIt.activeSelf || quiz.totQuestionFillItCount != 3 || !rows[2].activeSelf || rows[3].activeSelf ||
                quiz.questionsFillIt[2].GetComponent<TMPro.TextMeshProUGUI>().text != "Freezing")
                throw new Exception("Classroom fill-in assessment did not render the three authored questions.");
            for (var parent = game.FillIt.transform.parent; parent != null; parent = parent.parent)
                parent.gameObject.SetActive(true);
            var slots = game.FillIt.GetComponentsInChildren<ColliderTriggerFill>();
            if (slots.Length != 3) throw new Exception("The three authored drop slots are not active.");
            quiz.nextButton.enabled = false;
            var first = slots[0];
            var answers = new[] { first.Answer1, first.Answer2, first.Answer3 };
            foreach (var slot in slots)
            {
                var card = Array.Find(answers, answer => answer.GetComponentInChildren<TMPro.TextMeshProUGUI>().text ==
                    slot.textfromRes.text);
                var collider = card == null ? null : card.GetComponent<BoxCollider>();
                if (!slot.TryDropDpvr(collider) || slot.TryDropDpvr(collider) || collider.enabled ||
                    Vector3.Distance(card.transform.parent.parent.position, slot.PositionGetter.transform.position) > 0.001f)
                    throw new Exception("Drop failed to snap or accepted a duplicate.");
            }
            if (!quiz.nextButton.enabled || quiz.answerPlacedCount != 3 || game.mcqScore != 3)
                throw new Exception("Fill-in drop scoring or Next activation failed.");
            Debug.Log("[DPVR-MEDIA-AUDIT] Production classroom fill-in entry and visible row count PASS.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        private static void AuditMatchItEntry()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/KlassMeta.unity", OpenSceneMode.Single);
            var game = UnityEngine.Object.FindObjectOfType<GamificationManager>(true);
            var quiz = game.MatchIt.GetComponent<QuizManagers>();
            game.MatchIt.SetActive(false);
            game.apiManager.assessmentsString = "[{\"typeOfGame\":\"MatchingFollowing\",\"question\":\"Match\",\"options\":[" +
                "{\"left\":{\"text\":\"Q1\"},\"right\":{\"text\":\"A1\"}}," +
                "{\"left\":{\"text\":\"Q2\"},\"right\":{\"text\":\"A2\"}}," +
                "{\"left\":{\"text\":\"Q3\"},\"right\":{\"text\":\"A3\"}}," +
                "{\"left\":{\"text\":\"Q4\"},\"right\":{\"text\":\"A4\"}}," +
                "{\"left\":{\"text\":\"Q5\"},\"right\":{\"text\":\"A5\"}}]}]";
            game.StartAfterLoadingCanvas();
            if (!game.MatchIt.activeSelf || quiz.totQuestionMatchItCount != 5 || quiz.colliderTriggerMatch == null)
                throw new Exception("Classroom MatchIt assessment did not initialize all five pairs.");
            for (var parent = game.MatchIt.transform.parent; parent != null; parent = parent.parent)
                parent.gameObject.SetActive(true);
            var match = quiz.colliderTriggerMatch;
            var cards = new[] { match.Answer1Pos, match.Answer2Pos, match.Answer3Pos, match.Answer4Pos, match.Answer5Pos };
            var firstBefore = cards[0].transform.position;
            var secondBefore = cards[1].transform.position;
            if (match.TrySwapDpvr(cards[0], cards[0]) || !match.TrySwapDpvr(cards[0], cards[1]) ||
                Vector3.Distance(cards[0].transform.position, secondBefore) > 0.001f ||
                Vector3.Distance(cards[1].transform.position, firstBefore) > 0.001f)
                throw new Exception("MatchIt DPVR card swap failed.");

            for (int i = 0; i < 5; i++)
            {
                string expected = quiz.answerPlacedTextMatchIt[i].GetComponent<TMPro.TextMeshProUGUI>().text;
                if (quiz.answersMatchIt[i].GetComponent<TMPro.TextMeshProUGUI>().text == expected) continue;
                int sourceIndex = Array.FindIndex(quiz.answersMatchIt,
                    obj => obj != null && obj.GetComponent<TMPro.TextMeshProUGUI>().text == expected);
                var currentCard = quiz.answersMatchIt[i].GetComponentInParent<ColliderTriggerMatch>(true).gameObject;
                var targetCard = quiz.answersMatchIt[sourceIndex].GetComponentInParent<ColliderTriggerMatch>(true).gameObject;
                if (!match.TrySwapDpvr(currentCard, targetCard))
                    throw new Exception("MatchIt DPVR alignment swap failed at slot " + i + ".");
            }
            game.mcqScore = 0;
            quiz.MatchSubmit();
            if (!quiz.IsMatchSubmitted || game.mcqScore != 5 || match.TrySwapDpvr(cards[0], cards[1]))
                throw new Exception("MatchIt DPVR submit scoring or post-submit lock failed.");
            Debug.Log("[DPVR-MEDIA-AUDIT] Classroom MatchIt drag-swap and scoring PASS.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
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
