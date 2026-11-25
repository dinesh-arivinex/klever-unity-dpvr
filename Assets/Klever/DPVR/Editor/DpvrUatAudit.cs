#if KLEVER_DPN
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Klever.DPVR.Editor
{
    public static class DpvrUatAudit
    {
        [Serializable] private class Item { public string path; public Vector3 position; public Vector3 scale; public Vector3 size; public Vector3 center; public Vector3[] corners; }
        [Serializable] private class Layout { public Item[] guides; public Item[] boards; }
        [Serializable] private class SourceFixture { public string source; public string expectedKey; }
        [Serializable] private class Questions { public GamificationManager.QuestionData[] questions; }
        private static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;

        public static void Inspect()
        {
            var fixturePath = Environment.GetEnvironmentVariable("KLEVER_DPVR_UAT_FIXTURE");
            var fixture = DpvrContentCompatibility.Parse(File.ReadAllText(fixturePath));
            if (DpvrContentCompatibility.Assessments(fixture)?.Count != 1 ||
                DpvrContentCompatibility.VideoUrl(fixture["content"][0]["videoDetails"][0]) != "https://example.test/clip.mp4")
                throw new InvalidOperationException("Observed UAT payload schema regression failed.");
            var legacy = DpvrContentCompatibility.Parse("{\"assessments\":[{\"typeOfGame\":\"MCQ\"}],\"video\":{\"VideoId\":{\"videoURL\":\"https://example.test/legacy.mp4\"}}}");
            if (DpvrContentCompatibility.Assessments(legacy)?.Count != 1 ||
                DpvrContentCompatibility.VideoUrl(legacy["video"]) != "https://example.test/legacy.mp4")
                throw new InvalidOperationException("Legacy payload schema regression failed.");
            Debug.Log("[DPVR-UAT-AUDIT] Current and legacy video/assessment schemas PASS.");
            var questions = JsonUtility.FromJson<Questions>("{\"questions\":" + DpvrContentCompatibility.Assessments(fixture).ToString() + "}");
            if (questions.questions.Length != 1 || questions.questions[0].typeOfGame != "MCQ" ||
                questions.questions[0].options.Count != 4 || questions.questions[0].options.Count(o => o.isCorrect) != 1)
                throw new InvalidOperationException("UAT MCQ deserialization failed.");
            var sourceFixture = JsonUtility.FromJson<SourceFixture>(File.ReadAllText(Environment.GetEnvironmentVariable("KLEVER_DPVR_SOURCE_FIXTURE")));
            if (DpvrVideoRenditions.SourceKey(sourceFixture.source) != sourceFixture.expectedKey ||
                DpvrVideoRenditions.SourceKey("https://example.test/clip.mp4?token=one") !=
                DpvrVideoRenditions.SourceKey("https://example.test/clip.mp4?token=two"))
                throw new InvalidOperationException("Video rendition source identity failed.");
            var rendition = DpvrVideoRenditions.Resolve(sourceFixture.source);
            if (rendition.PathType != RenderHeads.Media.AVProVideo.MediaPathType.RelativeToStreamingAssetsFolder ||
                !File.Exists(Path.Combine(Application.streamingAssetsPath, rendition.Path)))
                throw new InvalidOperationException("UAT video did not resolve to its packaged rendition.");
            var unknown = DpvrVideoRenditions.Resolve("https://example.test/unmapped.mp4");
            if (unknown.PathType != RenderHeads.Media.AVProVideo.MediaPathType.AbsolutePathOrURL)
                throw new InvalidOperationException("Unmapped videos lost their original source.");
            Debug.Log("[DPVR-UAT-AUDIT] Four-option MCQ and packaged video identity/fallback PASS.");
            EditorSceneManager.OpenScene("Assets/Scenes/KlassMeta.unity", OpenSceneMode.Single);
            DpvrControllerGuide.ConfigureAll();
            var room = UnityEngine.Object.FindObjectsOfType<ChangeClassRoom>(true).Single();
            var surface = room.boardA.GetComponent<Renderer>();
            if (surface == null) throw new InvalidOperationException("Classroom A board surface missing.");
            foreach (var panel in UnityEngine.Object.FindObjectsOfType<DpvrControllerGuide>(true))
            {
                panel.FitToBoard(surface, DpvrRuntimeBootstrap.LegacyStartEyePosition);
                var panelCorners = new Vector3[4]; panel.GetComponent<RectTransform>().GetWorldCorners(panelCorners);
                foreach (var corner in panelCorners)
                    if (corner.x < surface.bounds.min.x + 0.07f || corner.x > surface.bounds.max.x - 0.07f ||
                        corner.y < surface.bounds.min.y + 0.07f || corner.y > surface.bounds.max.y - 0.07f ||
                        corner.z >= surface.bounds.min.z)
                        throw new InvalidOperationException("Guide outside or behind the green board.");
            }
            Debug.Log("[DPVR-UAT-AUDIT] All guide corners inside and in front of the green board PASS.");
            var guides = UnityEngine.Object.FindObjectsOfType<Canvas>(true).Where(c => c.name == "Controller Panel")
                .Select(c => { var corners = new Vector3[4]; c.GetComponent<RectTransform>().GetWorldCorners(corners);
                    return new Item {path = PathOf(c.transform), position = c.transform.position, scale = c.transform.lossyScale, corners = corners}; }).ToArray();
            var boards = UnityEngine.Object.FindObjectsOfType<Renderer>(true)
                .Where(r => PathOf(r.transform).Contains("BoardScreen") || r.name == "BoardBorder")
                .Select(r => new Item {path = PathOf(r.transform), position = r.transform.position, scale = r.transform.lossyScale,
                    size = r.bounds.size, center = r.bounds.center}).ToArray();
            var output = Environment.GetEnvironmentVariable("KLEVER_DPVR_LAYOUT_OUTPUT");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllText(output, JsonUtility.ToJson(new Layout {guides = guides, boards = boards}, true));
            Debug.Log("[DPVR-UAT-AUDIT] Layout written: " + output);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        public static void RunAndBuild()
        {
            DpvrLessonAudit.Run();
            Inspect();
            DpvrMcqAudit.Run();
            DpvrSessionAudit.Run();
            DpvrLegacyBuildPipeline.BuildTestAndroid();
        }
    }
}
#endif
