using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace Klever.DPVR.Editor
{
    // Read-only scene audit. No scene/prefab is saved and no registration is sent.
    public static class DpvrKeypadAudit
    {
        [Serializable] private class Hit { public string path; public float distance; public bool trigger; }
        [Serializable] private class Key { public string name; public Vector3 center; public Vector3 size; public int callbacks; public string callbackArgument; public Hit[] hits; public Hit[] wristHits; }
        [Serializable] private class Routing { public int buttons; public int colliders; public int incorrectOwnersBefore; public int incorrectOwnersAfter; public int surfaceRays; public int digitDispatches; public int deleteDispatches; public int unrelatedInteractablesChecked; }
        [Serializable] private class Report { public string camera; public Vector3 origin; public Vector3 authoredCameraPosition; public Key[] keys; public Routing routing; }
        private static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;

        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/KlassMeta.unity", OpenSceneMode.Single);
            var cameras = UnityEngine.Object.FindObjectsOfType<Camera>(true);
            var camera = cameras.FirstOrDefault(c => c.name == "CenterEyeAnchor") ?? cameras.FirstOrDefault(c => c.CompareTag("MainCamera"));
            if (camera == null) throw new InvalidOperationException("Scene camera not found for keypad audit.");
            var origin = DpvrRuntimeBootstrap.LegacyStartEyePosition;
            foreach (var key in UnityEngine.Object.FindObjectsOfType<XRSimpleInteractable>(true))
                if (key.name.Length == 1 && char.IsDigit(key.name[0]) && PathOf(key.transform).Contains("NumPad"))
                    for (var parent = key.transform; parent != null; parent = parent.parent)
                        parent.gameObject.SetActive(true);
            foreach (var t in UnityEngine.Object.FindObjectsOfType<Transform>(true))
                if (t.GetComponent<XROrigin>() != null || t.name == "OVRCameraRig" || t.name == "OVRInteraction" || t.name.Contains("[BuildingBlock] Camera Rig") || t.name.Contains("[BuildingBlock] Hand Tracking") || t.name.Contains("[BuildingBlock] Controller Tracking"))
                    t.gameObject.SetActive(false);
            Physics.SyncTransforms();
            var routing = AuditRouting(origin);
            var keys = UnityEngine.Object.FindObjectsOfType<XRSimpleInteractable>(true)
                .Where(k => k.name.Length == 1 && char.IsDigit(k.name[0]) && PathOf(k.transform).Contains("NumPad"))
                .OrderBy(k => k.name).Select(k => {
                    var collider = k.GetComponent<BoxCollider>();
                    var center = collider.bounds.center;
                    var delta = center - origin;
                    var shoulder = origin + new Vector3(0.18f, -0.20f, 0.05f);
                    var direction = (center - shoulder).normalized;
                    var wrist = shoulder + direction * 0.42f;
                    var calls = new SerializedObject(k).FindProperty("m_SelectEntered.m_PersistentCalls.m_Calls");
                    return new Key {
                        name = k.name, center = center, size = collider.bounds.size,
                        callbacks = k.selectEntered.GetPersistentEventCount(),
                        callbackArgument = calls.arraySize == 1 ? calls.GetArrayElementAtIndex(0).FindPropertyRelative("m_Arguments.m_StringArgument").stringValue : null,
                        hits = Physics.RaycastAll(origin, delta.normalized, delta.magnitude + 0.1f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide)
                            .OrderBy(h => h.distance).Take(8).Select(h => new Hit {path = PathOf(h.collider.transform), distance = h.distance, trigger = h.collider.isTrigger}).ToArray(),
                        wristHits = Physics.RaycastAll(wrist, direction, Vector3.Distance(wrist, center) + 0.1f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide)
                            .OrderBy(h => h.distance).Take(8).Select(h => new Hit {path = PathOf(h.collider.transform), distance = h.distance, trigger = h.collider.isTrigger}).ToArray()
                    };
                }).ToArray();
            var report = new Report {camera = PathOf(camera.transform), authoredCameraPosition = camera.transform.position, origin = origin, keys = keys, routing = routing};
            var output = Environment.GetEnvironmentVariable("KLEVER_DPVR_AUDIT_OUTPUT");
            if (string.IsNullOrEmpty(output)) output = "Builds/DPVR/keypad-scene-audit.json";
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllText(output, JsonUtility.ToJson(report, true));
            Debug.Log("[DPVR-AUDIT] Read-only keypad geometry audit: " + output + " keys=" + keys.Length);
            foreach (var key in keys)
                if (key.callbacks != 1 || key.callbackArgument != key.name || key.hits.Length == 0 || !key.hits[0].path.EndsWith("/" + key.name) || key.wristHits.Length == 0 || !key.wristHits[0].path.EndsWith("/" + key.name))
                    throw new InvalidOperationException("Keypad callback/eye/wrist ray audit failed for key " + key.name + "; inspect " + output);
            if (keys.Length != 10) throw new InvalidOperationException("Expected ten keypad digits.");
            Debug.Log("[DPVR-AUDIT] PASS: all ten digit callbacks and unobstructed eye/wrist rays.");
            // Discard temporary activation changes; do not save the audited scene.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        private static Routing AuditRouting(Vector3 origin)
        {
            var owner = UnityEngine.Object.FindObjectsOfType<DeviceIDManager>(true).Single();
            var buttons = UnityEngine.Object.FindObjectsOfType<XRSimpleInteractable>(true)
                .Where(k => DpvrKeypadRouting.IsKey(k, owner)).ToArray();
            if (buttons.Length != 12) throw new InvalidOperationException("Expected ten digits, Delete and Submit.");
            foreach (var button in buttons)
                for (var parent = button.transform; parent != null; parent = parent.parent) parent.gameObject.SetActive(true);
            Physics.SyncTransforms();
            var manager = new GameObject("Read-only keypad audit manager").AddComponent<XRInteractionManager>();
            var all = UnityEngine.Object.FindObjectsOfType<XRBaseInteractable>(true);
            // Mirror XRI 2.5.4 Awake when examining objects outside Play mode.
            foreach (var item in all)
            {
                if (item.colliders.Count == 0)
                    item.colliders.AddRange(item.GetComponentsInChildren<Collider>().Where(c => !c.isTrigger));
                item.interactionManager = manager;
                if (item.isActiveAndEnabled) manager.RegisterInteractable((IXRInteractable)item);
            }
            var expected = buttons.SelectMany(b => b.GetComponentsInChildren<Collider>(true)
                .Select(c => new { collider = c, button = b })).ToArray();
            var owned = new System.Collections.Generic.HashSet<Collider>(expected.Select(e => e.collider));
            var unrelated = all.Where(i => !buttons.Contains(i) && !i.colliders.Any(owned.Contains) &&
                !buttons.Any(b => i.transform.IsChildOf(b.transform))).ToDictionary(i => i, i => i.colliders.ToArray());
            var report = new Routing { buttons = buttons.Length, colliders = expected.Length,
                incorrectOwnersBefore = expected.Count(e => !manager.TryGetInteractableForCollider(e.collider, out var hit) || hit != (IXRInteractable)e.button) };
            DpvrKeypadRouting.ConfigureAll(manager);
            report.incorrectOwnersAfter = expected.Count(e => !manager.TryGetInteractableForCollider(e.collider, out var hit) || hit != (IXRInteractable)e.button);
            if (report.incorrectOwnersAfter != 0) throw new InvalidOperationException("Keypad collider ownership is not unique.");
            foreach (var item in unrelated)
            {
                if (!item.Value.SequenceEqual(item.Key.colliders)) throw new InvalidOperationException("Unrelated collider list changed.");
                report.unrelatedInteractablesChecked++;
            }
            // Check nine points over every button, not only a ray through its centre.
            foreach (var button in buttons)
            {
                var box = button.GetComponent<BoxCollider>();
                if (box == null) throw new InvalidOperationException("Keypad root collider missing.");
                foreach (float x in new[] { -0.3f, 0f, 0.3f })
                    foreach (float y in new[] { -0.3f, 0f, 0.3f })
                    {
                        var target = box.transform.TransformPoint(box.center + Vector3.Scale(box.size, new Vector3(x, y, 0)));
                        var direction = (target - (origin + new Vector3(0.18f, -0.20f, 0.05f))).normalized;
                        var wrist = origin + new Vector3(0.18f, -0.20f, 0.05f) + direction * 0.42f;
                        if (!Physics.Raycast(wrist, direction, out var hit, 20f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide) ||
                            !manager.TryGetInteractableForCollider(hit.collider, out var mapped) || mapped != (IXRInteractable)button)
                            throw new InvalidOperationException("Keypad surface ray mapped to wrong action: " + button.name);
                        report.surfaceRays++;
                    }
            }
            var digits = buttons.Where(b => b.name.Length == 1 && char.IsDigit(b.name[0])).OrderBy(b => b.name).ToArray();
            var delete = buttons.Single(b => Enumerable.Range(0, b.selectEntered.GetPersistentEventCount()).Any(i =>
                b.selectEntered.GetPersistentMethodName(i) == nameof(DeviceIDManager.OnBackspaceClick)));
            var tested = digits.Concat(new[] { delete }).ToArray();
            var savedText = owner.inputText.text;
            try
            {
                foreach (var button in tested) button.selectEntered.SetPersistentListenerState(0, UnityEngine.Events.UnityEventCallState.EditorAndRuntime);
                // Exercise the authored local digit/Delete callbacks repeatedly.
                // Submit is inspected above but NEVER invoked; no API request is sent.
                for (int repeat = 0; repeat < 10; repeat++)
                    foreach (var digit in digits)
                    {
                        owner.inputText.text = "";
                        digit.selectEntered.Invoke(new SelectEnterEventArgs());
                        if (owner.inputText.text != digit.name) throw new InvalidOperationException("Digit dispatch failed.");
                        report.digitDispatches++;
                        delete.selectEntered.Invoke(new SelectEnterEventArgs());
                        if (owner.inputText.text != "") throw new InvalidOperationException("Delete dispatch failed.");
                        report.deleteDispatches++;
                    }
            }
            finally
            {
                owner.inputText.text = savedText;
                foreach (var button in tested) button.selectEntered.SetPersistentListenerState(0, UnityEngine.Events.UnityEventCallState.RuntimeOnly);
            }
            // Reconfiguration must be harmless when the bootstrap revisits a scene.
            DpvrKeypadRouting.ConfigureAll(manager);
            if (expected.Any(e => !manager.TryGetInteractableForCollider(e.collider, out var mapped) || mapped != (IXRInteractable)e.button))
                throw new InvalidOperationException("Repeated configuration changed keypad ownership.");
            Debug.Log("[DPVR-AUDIT] Routing PASS: buttons=" + report.buttons + " ownershipErrorsBefore=" + report.incorrectOwnersBefore +
                      " after=" + report.incorrectOwnersAfter + " surfaceRays=" + report.surfaceRays +
                      " digitDispatches=" + report.digitDispatches + " deleteDispatches=" + report.deleteDispatches +
                      " unrelated=" + report.unrelatedInteractablesChecked);
            return report;
        }

        public static void RunAndBuild()
        {
            Run();
            DpvrLegacyBuildPipeline.BuildTestAndroid();
        }
    }
}
