#if KLEVER_DPN
using System;
using System.IO;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Klever.DPVR.Editor
{
    public static class DpvrSessionAudit
    {
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/KlassMeta.unity", OpenSceneMode.Single);
            var manager = UnityEngine.Object.FindObjectsOfType<GamificationManager>(true).Single();
            var game = UnityEngine.Object.FindObjectsOfType<GameManager>(true).Single();
            var handler = game.deviceSynHandler;
            var loading = UnityEngine.Object.FindObjectsOfType<LoadingBar>(true).First(l => l.deviceSynHandler == handler);
            GameManager.Instance = game;
            if (!manager.disableObjects.Any(o => o == null))
                throw new InvalidOperationException("Completion regression no longer contains missing optional objects.");
            foreach (var camera in UnityEngine.Object.FindObjectsOfType<Camera>(true)) camera.gameObject.SetActive(false);
            var eye = new GameObject("Session audit eye").AddComponent<Camera>();
            eye.tag = "MainCamera";
            eye.transform.position = DpvrRuntimeBootstrap.LegacyStartEyePosition;
            eye.aspect = 1024f / 1152f;
            eye.fieldOfView = 90;
            eye.clearFlags = CameraClearFlags.SolidColor;
            eye.backgroundColor = Color.black;
            loading.ToggleObject(false);
            loading.SynchDevice();
            var feedback = DpvrSessionPresentation.Instance;
            if (feedback == null || !feedback.IsVisible || feedback.CurrentStage != DpvrSessionPresentation.Stage.Loading)
                throw new InvalidOperationException("No independent loading feedback while classroom is hidden.");
            Render(eye, "LOADING");
            DpvrSessionPresentation.NarrationReady();
            if (!feedback.IsVisible) throw new InvalidOperationException("Loading feedback disappeared before a rendered classroom was confirmed.");
            for (var parent = manager.transform; parent != null; parent = parent.parent) parent.gameObject.SetActive(true);
            // Actual completion preparation, including the scene's null cleanup
            // entries. Do not call score/network coroutines or answer a question.
            manager.PrepareCompletionScreen();
            if (!DeviceSynHandler.isGameOver || !handler.isCompletedDevice || handler.CurrentPlayState != "completed" ||
                !manager.thankyouCanvas.activeSelf || !feedback.IsVisible ||
                feedback.CurrentStage != DpvrSessionPresentation.Stage.Completed)
                throw new InvalidOperationException("Completion state/UI failed before score submission.");
            foreach (var optional in manager.disableObjects)
                if (optional != null && optional.activeSelf) throw new InvalidOperationException("Completion did not hide lesson content.");
            handler.PublishDownloadProgress(0.25f);
            if (handler.CurrentPlayState != "completed") throw new InvalidOperationException("Progress overwrote completed state.");
            DpvrSessionPresentation.NarrationReady();
            if (!feedback.IsVisible) throw new InvalidOperationException("Late narration hid completion feedback.");
            manager.allOptions.Add(new System.Collections.Generic.List<AnswerData>());
            manager.allOptions.Add(new System.Collections.Generic.List<AnswerData>());
            manager.mcqScore = 2;
            manager.PrepareCompletionScreen();
            if (feedback.Message.Contains("Score:") || !feedback.Message.Contains("Experience completed successfully") || feedback.GetComponentsInChildren<Graphic>(true).Any(g => g.raycastTarget))
                throw new InvalidOperationException("Completion score or non-blocking graphics failed.");
            Render(eye, "COMPLETION");
            DpvrSessionPresentation.ShowLoading();
            if (feedback.CurrentStage != DpvrSessionPresentation.Stage.Loading || feedback.Message.Contains("completed"))
                throw new InvalidOperationException("New lesson retained completion feedback.");
            DpvrSessionPresentation.Hide();
            Debug.Log("[DPVR-SESSION-AUDIT] Null-safe production completion, completed state priority, loading/ready/completed/new-lesson feedback and non-blocking UI PASS. No scores submitted.");
            GameManager.Instance = null;
            DeviceSynHandler.isGameOver = false;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        private static void Render(Camera eye, string kind)
        {
            var prefix = Environment.GetEnvironmentVariable("KLEVER_DPVR_SESSION_PREVIEW");
            if (string.IsNullOrEmpty(prefix)) return;
            DpvrSessionPresentation.Instance.FollowView();
            Canvas.ForceUpdateCanvases();
            var target = new RenderTexture(1024, 1152, 24);
            eye.targetTexture = target;
            eye.Render();
            RenderTexture.active = target;
            var texture = new Texture2D(1024, 1152, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, 1024, 1152), 0, 0);
            texture.Apply();
            File.WriteAllBytes(prefix + "_" + kind + ".png", texture.EncodeToPNG());
            RenderTexture.active = null;
            eye.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
#endif
