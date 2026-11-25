#if KLEVER_DPN
using System;
using System.IO;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Klever.DPVR.Editor
{
    public static class DpvrMcqAudit
    {
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/KlassMeta.unity", OpenSceneMode.Single);
            var game = UnityEngine.Object.FindObjectsOfType<GamificationManager>(true).Single();
            var room = UnityEngine.Object.FindObjectsOfType<ChangeClassRoom>(true).Single();
            var quiz = game.mcqGame.GetComponent<QuizManagers>();
            var buttons = quiz.answerButtons.ToArray();
            var next = quiz.nextButton;
            if (game.crossbow != null || game.crossbowHandMode != null || game.ballVisualize != null || game.ballVisualizeHandMode != null)
                throw new InvalidOperationException("MCQ regression fixture no longer reproduces the missing optional visuals.");
            var fixture = DpvrContentCompatibility.Parse(File.ReadAllText(Environment.GetEnvironmentVariable("KLEVER_DPVR_UAT_FIXTURE")));
            game.apiManager.assessmentsString = DpvrContentCompatibility.Assessments(fixture).ToString();
            foreach (var camera in UnityEngine.Object.FindObjectsOfType<Camera>(true)) camera.gameObject.SetActive(false);
            var eye = new GameObject("MCQ audit eye").AddComponent<Camera>();
            eye.tag = "MainCamera";
            eye.transform.position = DpvrRuntimeBootstrap.LegacyStartEyePosition;
            eye.fieldOfView = 90;
            eye.aspect = 1024f / 1152f;
            eye.nearClipPlane = 0.05f;
            room.Gamification.SetPositionAndRotation(room.classRoomAGamPos.position, room.classRoomAGamPos.rotation);
            for (var parent = game.transform; parent != null; parent = parent.parent) parent.gameObject.SetActive(true);
            for (var parent = quiz.transform.parent; parent != null; parent = parent.parent) parent.gameObject.SetActive(true);
            game.mcqGame.SetActive(false);
            // Exercise the production parser and entry switch. Never answer,
            // advance, submit a score, connect a service, or save the scene.
            game.StartAfterLoadingCanvas();
            Canvas.ForceUpdateCanvases();
            if (!quiz.gameObject.activeInHierarchy || string.IsNullOrEmpty(quiz.questionText.text) ||
                buttons.Length != 4 || !buttons.SequenceEqual(quiz.answerButtons) || next != quiz.nextButton)
                throw new InvalidOperationException("MCQ entry failed or changed widget references.");
            foreach (var button in buttons)
            {
                if (!button.gameObject.activeInHierarchy) throw new InvalidOperationException("MCQ answer inactive.");
                AssertVisible(button.GetComponent<RectTransform>(), eye);
            }
            AssertVisible(next.GetComponent<RectTransform>(), eye);
            var output = Environment.GetEnvironmentVariable("KLEVER_DPVR_MCQ_PREVIEW");
            if (!string.IsNullOrEmpty(output))
            {
                var target = new RenderTexture(1024, 1152, 24);
                eye.targetTexture = target;
                eye.Render();
                RenderTexture.active = target;
                var texture = new Texture2D(1024, 1152, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, 1024, 1152), 0, 0);
                texture.Apply();
                File.WriteAllBytes(output, texture.EncodeToPNG());
                RenderTexture.active = null;
                eye.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(target);
            }
            Debug.Log("[DPVR-MCQ-AUDIT] Production MCQ entry with missing optional visuals, four answers, Next and preserved widget references/layout PASS. No answers or scores submitted.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        private static void AssertVisible(RectTransform rect, Camera eye)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            foreach (var point in corners)
            {
                var view = eye.WorldToViewportPoint(point);
                if (view.z <= eye.nearClipPlane || view.x < 0.05f || view.x > 0.95f || view.y < 0.05f || view.y > 0.95f)
                    throw new InvalidOperationException("MCQ widget outside view: " + rect.name + " " + view);
            }
        }
    }
}
#endif
