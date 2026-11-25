#if KLEVER_DPN
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;

namespace Klever.DPVR
{
    // Local adapter for the update modal. Uses the same XRI selection edges as
    // the physical keypad; no alternate input polling or global rig changes.
    [DefaultExecutionOrder(21000)]
    public sealed class DpvrUpdatePopupInput : MonoBehaviour
    {
        private Canvas surface;
        private Button primary, later;
        private bool configured;
        private XRRayInteractor ray;
        private RectTransform marker;
        private Image markerImage;
        private Material markerMaterial;

        public static void Configure(Canvas canvas, Button action, Button dismiss, XRInteractionManager manager = null)
        {
            var input = canvas.GetComponent<DpvrUpdatePopupInput>();
            if (input == null) input = canvas.gameObject.AddComponent<DpvrUpdatePopupInput>();
            input.surface = canvas; input.primary = action; input.later = dismiss;
            input.TryConfigure(manager);
        }

        private void TryConfigure(XRInteractionManager manager = null)
        {
            if (configured || surface == null || primary == null || later == null) return;
            if (manager == null) manager = FindObjectOfType<XRInteractionManager>();
            if (manager == null) return; // The platform rig may still be starting.
            var rect = (RectTransform)surface.transform;
            var backplate = surface.gameObject.AddComponent<BoxCollider>();
            backplate.isTrigger = true;
            backplate.center = new Vector3(rect.rect.center.x, rect.rect.center.y, 0);
            backplate.size = new Vector3(rect.rect.width, rect.rect.height, 2);
            Bind(primary, manager); Bind(later, manager);

            var point = new GameObject("Update controller hit marker", typeof(RectTransform), typeof(Image));
            point.layer = 5; point.transform.SetParent(surface.transform, false);
            marker = (RectTransform)point.transform; marker.sizeDelta = new Vector2(14, 14);
            marker.localRotation = Quaternion.Euler(0, 0, 45);
            markerImage = point.GetComponent<Image>(); markerImage.raycastTarget = false;
            markerMaterial = new Material(surface.GetComponent<Image>().material);
            markerMaterial.renderQueue = 5001;
            markerImage.material = markerMaterial;
            var border = point.AddComponent<Outline>();
            border.effectColor = new Color(0.04f, 0.06f, 0.07f);
            border.effectDistance = new Vector2(3, -3);
            point.SetActive(false);
            configured = true;
        }

        private static PointerEventData Pointer()
        {
            return EventSystem.current == null ? null : new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        }

        private static void Bind(Button button, XRInteractionManager manager)
        {
            bool active = button.gameObject.activeSelf;
            button.gameObject.SetActive(false);
            // Keep the canvas background as a UGUI blocker. Button clicks use
            // only XRI, so the same trigger cannot also invoke a UGUI click.
            button.GetComponent<Image>().raycastTarget = false;
            var highlight = button.gameObject.AddComponent<Outline>();
            highlight.effectColor = new Color(0.75f, 1f, 0.9f, 1f);
            highlight.effectDistance = new Vector2(4, -4);
            highlight.enabled = false;
            var rect = (RectTransform)button.transform;
            var collider = button.gameObject.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.center = new Vector3(rect.rect.center.x, rect.rect.center.y, -3);
            collider.size = new Vector3(rect.rect.width, rect.rect.height, 4);
            var target = button.gameObject.AddComponent<XRSimpleInteractable>();
            target.interactionManager = manager;
            target.colliders.Clear(); target.colliders.Add(collider);
            target.hoverEntered.AddListener(_ => { var data = Pointer(); if (data != null) button.OnPointerEnter(data); });
            target.hoverExited.AddListener(_ => { var data = Pointer(); if (data != null) button.OnPointerExit(data); });
            target.selectEntered.AddListener(_ => {
                if (!button.IsActive() || !button.IsInteractable()) return;
                var data = Pointer(); if (data != null) button.OnPointerDown(data);
                button.onClick.Invoke();
            });
            target.selectExited.AddListener(_ => { var data = Pointer(); if (data != null) button.OnPointerUp(data); });
            button.gameObject.SetActive(active);
        }

        private void OnEnable() { TryConfigure(); }
        private void LateUpdate()
        {
            if (!configured) { TryConfigure(); return; }
            if (ray == null || !ray.isActiveAndEnabled) ray = FindObjectOfType<XRRayInteractor>();
            bool hit = ray != null && ray.isActiveAndEnabled && ray.TryGetCurrent3DRaycastHit(out var result) &&
                result.collider != null && (result.collider.transform == transform || result.collider.transform.IsChildOf(transform));
            foreach (var btn in new[] { primary, later })
                btn.GetComponent<Outline>().enabled = false;
            marker.gameObject.SetActive(hit);
            if (!hit) return;
            // Re-read after the definite-hit check for C# definite assignment.
            ray.TryGetCurrent3DRaycastHit(out var current);
            var point = transform.InverseTransformPoint(current.point);
            marker.localPosition = new Vector3(point.x, point.y, -8);
            var button = current.collider.GetComponent<Button>();
            var state = Klever.VR.Updates.VrUpdateClient.Instance?.State;
            bool busy = state == Klever.VR.Updates.VrUpdateClient.UpdateState.Downloading ||
                        state == Klever.VR.Updates.VrUpdateClient.UpdateState.Verifying ||
                        state == Klever.VR.Updates.VrUpdateClient.UpdateState.Installing;
            marker.localRotation = Quaternion.Euler(0, 0, busy ? Time.unscaledTime * 240f : 45f);
            markerImage.color = Color.white;
            if (button != null && button.IsInteractable())
            {
                var outline = button.GetComponent<Outline>();
                if (outline != null) outline.enabled = true;
            }
        }
        private void OnDestroy()
        {
            if (markerMaterial == null) return;
            if (Application.isPlaying) Destroy(markerMaterial); else DestroyImmediate(markerMaterial);
        }
    }
}
#endif
