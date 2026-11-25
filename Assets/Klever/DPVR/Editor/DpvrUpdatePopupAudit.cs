#if KLEVER_DPN
using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;

namespace Klever.DPVR.Editor
{
    public static class DpvrUpdatePopupAudit
    {
        private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
        public static void Run()
        {
            var root = new GameObject("Isolated popup input audit");
            try
            {
                var manager = root.AddComponent<XRInteractionManager>();
                var surface = new GameObject("Modal", typeof(RectTransform), typeof(Canvas), typeof(Image));
                surface.layer = 5; surface.transform.SetParent(root.transform, false);
                surface.transform.position = new Vector3(500, 500, 500);
                surface.transform.localScale = Vector3.one * 0.001f;
                ((RectTransform)surface.transform).sizeDelta = new Vector2(1000, 420);
                var canvas = surface.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
                var first = MakeButton(surface.transform, "First", -190);
                var second = MakeButton(surface.transform, "Second", 220);
                int firstClicks = 0, secondClicks = 0;
                first.onClick.AddListener(() => firstClicks++);
                second.onClick.AddListener(() => secondClicks++);
                DpvrUpdatePopupInput.Configure(canvas, first, second, manager);
                DpvrUpdatePopupInput.Configure(canvas, first, second, manager);
                Require(surface.GetComponentsInChildren<BoxCollider>(true).Length == 3, "Popup repeated setup duplicates targets.");
                var controller = new GameObject("Audit controller", typeof(ActionBasedController));
                controller.transform.SetParent(root.transform, false);
                var ray = controller.AddComponent<XRRayInteractor>(); ray.interactionManager = manager;
                ray.SendMessage("Awake"); manager.RegisterInteractor((IXRInteractor)ray);
                foreach (var button in new[] { first, second })
                {
                    var target = button.GetComponent<XRSimpleInteractable>();
                    target.SendMessage("Awake");
                    // Awake may fill collider lists, so verify the trigger remains explicitly owned.
                    Require(target.colliders.Count == 1 && target.colliders[0] == button.GetComponent<BoxCollider>(), "Popup trigger target ownership changed.");
                    manager.RegisterInteractable((IXRInteractable)target);
                    Require(!button.GetComponent<Image>().raycastTarget, "Popup enables duplicate UGUI button click path.");
                }
                var behind = new GameObject("Underlying lesson control", typeof(BoxCollider));
                behind.layer = 5; behind.transform.SetParent(root.transform, false);
                behind.transform.position = surface.transform.position + Vector3.forward;
                behind.GetComponent<BoxCollider>().size = new Vector3(1, 0.5f, 0.05f);
                Physics.SyncTransforms();
                foreach (var button in new[] { first, second })
                {
                    var origin = button.transform.position - Vector3.forward * 1.8f;
                    Require(Physics.Raycast(origin, Vector3.forward, out var hit, 5, 1 << 5, QueryTriggerInteraction.Collide) && hit.collider.gameObject == button.gameObject, "Ray passes through popup button or hits its backplate first.");
                    Require(manager.TryGetInteractableForCollider(hit.collider, out var found) && found == button.GetComponent<XRSimpleInteractable>(), "Popup hit is not routed to the button.");
                    var target = (IXRSelectInteractable)button.GetComponent<XRSimpleInteractable>();
                    manager.SelectEnter((IXRSelectInteractor)ray, target);
                    manager.SelectExit((IXRSelectInteractor)ray, target);
                }
                Require(firstClicks == 1 && secondClicks == 1, "Popup press invokes zero or duplicate callbacks.");
                first.interactable = false;
                manager.SelectEnter((IXRSelectInteractor)ray, (IXRSelectInteractable)first.GetComponent<XRSimpleInteractable>());
                manager.SelectExit((IXRSelectInteractor)ray, (IXRSelectInteractable)first.GetComponent<XRSimpleInteractable>());
                Require(firstClicks == 1, "Busy/disabled update action is clickable.");
                var middle = surface.transform.position + Vector3.up * 0.1f - Vector3.forward * 1.8f;
                Require(Physics.Raycast(middle, Vector3.forward, out var background, 5, 1 << 5, QueryTriggerInteraction.Collide) && background.collider.gameObject == surface, "Modal background allows clicks through to lesson controls.");
                surface.SetActive(false); Physics.SyncTransforms();
                Require(Physics.Raycast(middle, Vector3.forward, out var afterHide, 5, 1 << 5, QueryTriggerInteraction.Collide) && afterHide.collider.gameObject == behind, "Hidden popup continues blocking lesson controls.");
                Debug.Log("[DPVR-UPDATE-INPUT-AUDIT] PASS: both ray targets, single callbacks, disabled action, modal blocking and release after hide; no installer invoked.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        private static Button MakeButton(Transform parent, string name, float x)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.layer = 5; go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform; rect.sizeDelta = new Vector2(350, 70); rect.anchoredPosition = new Vector2(x, -125);
            return go.GetComponent<Button>();
        }
    }
}
#endif
