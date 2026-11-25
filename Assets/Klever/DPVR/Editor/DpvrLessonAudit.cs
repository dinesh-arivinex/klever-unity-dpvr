#if KLEVER_DPN
using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace Klever.DPVR.Editor
{
    public static class DpvrLessonAudit
    {
        public static void Run()
        {
            DpvrKeypadAudit.Run();
            EditorSceneManager.OpenScene("Assets/Scenes/KlassMeta.unity", OpenSceneMode.Single);
            var manager = new GameObject("Lesson audit manager").AddComponent<XRInteractionManager>();
            var annotations = UnityEngine.Object.FindObjectsOfType<AnnotationManager>(true).Single();
            var originalTransform = annotations.GrabObj.transform.position;
            DpvrLessonInteraction.ConfigureAll(manager);
            var grab = annotations.GrabObj.GetComponent<XRGrabInteractable>();
            var rootCollider = annotations.GrabObj.GetComponent<BoxCollider>();
            if (grab == null || !grab.colliders.Contains(rootCollider) || grab.throwOnDetach || grab.trackRotation ||
                !annotations.GrabObj.GetComponent<Rigidbody>().isKinematic || annotations.GrabObj.GetComponent<Rigidbody>().useGravity ||
                annotations.GrabObj.transform.position != originalTransform)
                throw new InvalidOperationException("Lesson grab configuration failed.");
            manager.RegisterInteractable((IXRInteractable)grab);
            if (!manager.TryGetInteractableForCollider(rootCollider, out var mapped) || mapped != (IXRInteractable)grab)
                throw new InvalidOperationException("Lesson root collider not mapped to grab interaction.");
            DpvrLessonInteraction.ConfigureAll(manager);
            if (annotations.GrabObj.GetComponents<XRGrabInteractable>().Length != 1)
                throw new InvalidOperationException("Repeated setup added duplicate grabs.");
            // Downloaded roots can have offset meshes and scaled/rotated parents.
            var synthetic = new GameObject("Audit downloaded model");
            synthetic.transform.SetPositionAndRotation(new Vector3(4, 2, -3), Quaternion.Euler(10, 40, 0));
            synthetic.transform.localScale = new Vector3(0.5f, 2f, 1.5f);
            var mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mesh.transform.SetParent(synthetic.transform, false);
            mesh.transform.localPosition = new Vector3(2, 1, -1);
            DpvrLessonInteraction.ConfigureRoot(synthetic, manager);
            var fitted = synthetic.GetComponent<BoxCollider>();
            var rendererBounds = mesh.GetComponent<Renderer>().bounds;
            for (int corner = 0; corner < 8; corner++)
            {
                var point = synthetic.transform.InverseTransformPoint(rendererBounds.center + Vector3.Scale(rendererBounds.extents,
                    new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1)));
                var local = new Bounds(fitted.center, fitted.size + Vector3.one * 0.001f);
                if (!local.Contains(point)) throw new InvalidOperationException("Downloaded model collider misses mesh bounds.");
            }
            if (DpvrLessonInteraction.NextScale(1, 1, 100) != 1.4f || DpvrLessonInteraction.NextScale(1, -1, 100) != 0.65f ||
                !Mathf.Approximately(DpvrLessonInteraction.NextScale(1, 0, 1), 1f))
                throw new InvalidOperationException("Model scale clamps failed.");
            AuditSelectionLifecycle(manager, synthetic.GetComponent<XRGrabInteractable>());
            var video = UnityEngine.Object.FindObjectsOfType<VideoManager>(true).Single();
            video.mediaPlayer.AudioVolume = 0f;
            video.mediaPlayer.AudioMuted = true;
            video.RestoreDpvrVideoAudio();
            if (!Mathf.Approximately(video.mediaPlayer.AudioVolume, 1f) || video.mediaPlayer.AudioMuted)
                throw new InvalidOperationException("Per-clip video audio reset failed.");
            var panels = UnityEngine.Object.FindObjectsOfType<Canvas>(true).Where(c => c.name == "Controller Panel").ToArray();
            if (panels.Length == 0) throw new InvalidOperationException("Controller instruction panels not found.");
            DpvrControllerGuide.ConfigureAll();
            DpvrControllerGuide.ConfigureAll();
            foreach (var panel in panels)
                if (panel.GetComponents<DpvrControllerGuide>().Length != 1 ||
                    panel.GetComponentsInChildren<TMP_Text>(true).Any(t => EnabledBelow(t.transform, panel.transform) && t.text.Contains("Grip Button")))
                    throw new InvalidOperationException("Quest guide remained enabled or duplicate DPVR guide added.");

            // Render an isolated guide for layout review. All scene changes are discarded.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Controller Instruction Prefab/Controller Panel.prefab");
            var guide = UnityEngine.Object.Instantiate(prefab);
            guide.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            guide.transform.localScale = Vector3.one;
            guide.AddComponent<DpvrControllerGuide>().Build();
            var camera = new GameObject("Guide preview camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true; camera.orthographicSize = 540; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            var texture = new RenderTexture(1920, 1080, 24);
            camera.targetTexture = texture;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = texture;
            var pixels = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); pixels.Apply();
            var preview = Environment.GetEnvironmentVariable("KLEVER_DPVR_GUIDE_PREVIEW") ?? "Builds/DPVR/dpvr-controller-guide.png";
            Directory.CreateDirectory(Path.GetDirectoryName(preview));
            File.WriteAllBytes(preview, pixels.EncodeToPNG());
            RenderTexture.active = null; camera.targetTexture = null; texture.Release();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Debug.Log("[DPVR-LESSON-AUDIT] PASS: lesson grab ownership, idempotent setup, transformed mesh bounds, scale limits, clip audio reset and " + panels.Length + " DPVR guide panels. Preview=" + preview);
        }

        public static void RunAndBuild() { Run(); DpvrLegacyBuildPipeline.BuildTestAndroid(); }

        private static void AuditSelectionLifecycle(XRInteractionManager manager, XRGrabInteractable grab)
        {
            // Unity does not run MonoBehaviour Awake in this read-only Editor scene.
            // Initialize the real XRI components, then exercise manager selection
            // events rather than invoking our handlers directly.
            grab.SendMessage("Awake");
            var controller = new GameObject("Audit ray controller");
            controller.AddComponent<ActionBasedController>();
            var ray = controller.AddComponent<XRRayInteractor>();
            ray.interactionManager = manager;
            ray.SendMessage("Awake");
            manager.RegisterInteractor((IXRInteractor)ray);
            manager.RegisterInteractable((IXRInteractable)grab);
            var attach = ray.attachTransform;
            var originalPosition = attach.localPosition;
            var originalRotation = attach.localRotation;
            var uiObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var button = uiObject.AddComponent<XRSimpleInteractable>();
            button.colliders.Clear(); button.colliders.Add(uiObject.GetComponent<Collider>());
            button.interactionManager = manager;
            manager.RegisterInteractable((IXRInteractable)button);
            int clicks = 0;
            button.selectEntered.AddListener(_ => clicks++);
            for (int cycle = 0; cycle < 3; cycle++)
            {
                manager.SelectEnter((IXRSelectInteractor)ray, (IXRSelectInteractable)grab);
                if (!grab.isSelected || ray.attachTransform != attach || grab.trackPosition)
                    throw new InvalidOperationException("Model grab did not use the standard XRI attach object.");
                manager.SelectExit((IXRSelectInteractor)ray, (IXRSelectInteractable)grab);
                if (grab.isSelected || Vector3.Distance(attach.localPosition, originalPosition) > 0.0001f ||
                    Quaternion.Angle(attach.localRotation, originalRotation) > 0.001f)
                    throw new InvalidOperationException("Model release did not restore the pointer attachment.");
                manager.SelectEnter((IXRSelectInteractor)ray, (IXRSelectInteractable)button);
                manager.SelectExit((IXRSelectInteractor)ray, (IXRSelectInteractable)button);
            }
            if (clicks != 3) throw new InvalidOperationException("Post-grab button selections failed.");
            Debug.Log("[DPVR-LESSON-AUDIT] Three real XRI grab/release/button cycles PASS.");
        }

        private static bool EnabledBelow(Transform text, Transform panel)
        {
            for (var current = text; current != null && current != panel; current = current.parent)
                if (!current.gameObject.activeSelf) return false;
            return true;
        }
    }
}
#endif
