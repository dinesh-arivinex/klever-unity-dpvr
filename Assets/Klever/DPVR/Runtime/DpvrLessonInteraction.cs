#if KLEVER_DPN
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;

namespace Klever.DPVR
{
    // Adapt only the lesson model roots, not UI, quiz answers or scenery.
    public sealed class DpvrLessonInteraction : MonoBehaviour
    {
        private XRGrabInteractable grab;
        private InputAction touch;
        private Vector3 baseScale;
        private float scaleFactor = 1f;
        private XRRayInteractor selectingRay;
        private BoxCollider box;
        private Vector3 tableAnchor, grabStart, aimStart;
        private float grabDistance;
        private bool placed;

        public void BeginModel()
        {
            if (grab != null && grab.interactionManager != null) grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)grab);
            placed = false;
            RestoreRay();
        }

        public static void PlaceOnTable(GameObject root, Vector3 anchor)
        {
            var interaction = root.GetComponent<DpvrLessonInteraction>();
            if (interaction == null) return;
            interaction.BeginModel();
            interaction.box = root.GetComponent<BoxCollider>();
            // Rebind after import. The fitted root box owns selection; imported
            // mesh colliders must not intercept the ray without a registered owner.
            interaction.grab.interactionManager.UnregisterInteractable((IXRInteractable)interaction.grab);
            interaction.grab.colliders.Clear();
            interaction.grab.colliders.Add(interaction.box);
            foreach (var collider in root.GetComponentsInChildren<Collider>(true))
                if (collider != interaction.box && collider.GetComponentInParent<XRBaseInteractable>() == interaction.grab &&
                    collider.GetComponentInParent<Canvas>() == null && collider.GetComponentInParent<UnityEngine.UI.Button>() == null)
                    collider.enabled = false;
            if (interaction.grab.isActiveAndEnabled) interaction.grab.interactionManager.RegisterInteractable((IXRInteractable)interaction.grab);
            FitLocalBounds(root.transform, interaction.box);
            var size = interaction.box.bounds.size;
            float extent = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            if (extent > 0.001f) root.transform.localScale *= 0.65f / extent;
            Physics.SyncTransforms();
            var bounds = interaction.box.bounds;
            root.transform.position += new Vector3(anchor.x - bounds.center.x, anchor.y + 0.02f - bounds.min.y, anchor.z - bounds.center.z);
            interaction.tableAnchor = anchor;
            interaction.baseScale = root.transform.localScale;
            interaction.scaleFactor = 1f;
            interaction.placed = true;
            Physics.SyncTransforms();
            Debug.Log("[DPVR-MODEL] Placed above teacher table at " + anchor);
        }

        public static Vector3 ConstrainCenter(Vector3 center, Vector3 anchor, float halfHeight)
        {
            var delta = Vector2.ClampMagnitude(new Vector2(center.x - anchor.x, center.z - anchor.z), 0.45f);
            return new Vector3(anchor.x + delta.x, Mathf.Clamp(center.y, anchor.y + halfHeight + 0.02f, anchor.y + halfHeight + 0.4f), anchor.z + delta.y);
        }

        public static void ConfigureAll(XRInteractionManager manager)
        {
            foreach (var annotations in Resources.FindObjectsOfTypeAll<AnnotationManager>())
                if (annotations.gameObject.scene.IsValid() && annotations.GrabObj != null)
                    ConfigureRoot(annotations.GrabObj, manager);
            // Imported wrappers exist during preload and are later parented under
            // GrabObj. Giving them a second grab owner creates competing colliders.
        }

        public static DpvrLessonInteraction ConfigureRoot(GameObject root, XRInteractionManager manager)
        {
            var existing = root.GetComponent<DpvrLessonInteraction>();
            if (existing != null) return existing;
            // Preserve any separately authored grab interaction.
            if (root.GetComponent<XRGrabInteractable>() != null) return null;
            var box = root.GetComponent<BoxCollider>();
            if (box == null)
            {
                box = root.AddComponent<BoxCollider>();
                FitLocalBounds(root.transform, box);
            }
            box.enabled = true; box.isTrigger = false; root.layer = 0;
            var body = root.GetComponent<Rigidbody>();
            if (body == null) body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            var interaction = root.AddComponent<DpvrLessonInteraction>();
            interaction.baseScale = root.transform.localScale;
            interaction.box = box;
            interaction.grab = root.AddComponent<XRGrabInteractable>();
            interaction.grab.interactionManager = manager;
            manager.UnregisterInteractable((IXRInteractable)interaction.grab);
            interaction.grab.colliders.Clear();
            interaction.grab.colliders.Add(box);
            foreach (var collider in root.GetComponentsInChildren<Collider>(true))
                if (collider != box && collider.GetComponentInParent<XRBaseInteractable>() == interaction.grab)
                    interaction.grab.colliders.Add(collider);
            interaction.grab.useDynamicAttach = false;
            interaction.grab.attachEaseInTime = 0.2f;
            interaction.grab.movementType = XRBaseInteractable.MovementType.Kinematic;
            interaction.grab.throwOnDetach = false;
            interaction.grab.trackRotation = false;
            interaction.grab.trackPosition = false;
            interaction.grab.smoothPosition = true;
            interaction.grab.selectEntered.AddListener(interaction.OnGrab);
            interaction.grab.selectExited.AddListener(interaction.OnRelease);
            if (interaction.grab.isActiveAndEnabled) manager.RegisterInteractable((IXRInteractable)interaction.grab);
            interaction.touch = new InputAction("DPVR held model touchpad", InputActionType.Value,
                "<XRController>{RightHand}/primary2DAxis", expectedControlType: "Vector2");
            Debug.Log("[DPVR-MODEL] Lesson model grab configured.");
            return interaction;
        }

        public static void FitLocalBounds(Transform root, BoxCollider box)
        {
            Bounds bounds = default;
            bool found = false;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!(renderer is MeshRenderer || renderer is SkinnedMeshRenderer) ||
                    renderer.CompareTag("notAChild") || renderer.name == "Quad" ||
                    renderer.GetComponent<TMPro.TMP_Text>() != null || renderer.GetComponentInParent<Canvas>() != null)
                    continue;
                var world = renderer.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    var offset = Vector3.Scale(world.extents, new Vector3((corner & 1) == 0 ? -1 : 1,
                        (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    var point = root.InverseTransformPoint(world.center + offset);
                    if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
                    else bounds.Encapsulate(point);
                }
            }
            if (found) { box.center = bounds.center; box.size = Vector3.Max(bounds.size, Vector3.one * 0.01f); }
        }

        private void OnGrab(SelectEnterEventArgs args)
        {
            touch.Enable();
            selectingRay = args.interactorObject as XRRayInteractor;
            if (selectingRay != null)
            {
                // Hold at the initial distance and preserve the initial object offset.
                // Never pull a distant object to the controller or follow its roll.
                var origin = selectingRay.rayOriginTransform != null ? selectingRay.rayOriginTransform : selectingRay.transform;
                grabDistance = Mathf.Clamp(Vector3.Distance(origin.position, box.bounds.center), 0.5f, 8f);
                aimStart = origin.position + origin.forward * grabDistance;
                grabStart = transform.position;
            }
            Debug.Log("[DPVR-MODEL] Grab started.");
        }

        private void OnRelease(SelectExitEventArgs args)
        {
            RestoreRay();
            Debug.Log("[DPVR-MODEL] Grab released.");
        }

        private void RestoreRay()
        {
            touch?.Disable();
            selectingRay = null;
        }

        public static float NextScale(float current, float axis, float seconds) =>
            Mathf.Clamp(current * Mathf.Exp(axis * seconds * 0.6f), 0.65f, 1.4f);

        private void Update()
        {
            if (LessonPause.IsPaused || Time.timeScale == 0 || grab == null || !grab.isSelected || touch == null) return;
            if (selectingRay != null && placed)
            {
                var origin = selectingRay.rayOriginTransform != null ? selectingRay.rayOriginTransform : selectingRay.transform;
                var desired = grabStart + (origin.position + origin.forward * grabDistance - aimStart);
                var centerOffset = box.bounds.center - transform.position;
                desired = ConstrainCenter(desired + centerOffset, tableAnchor, box.bounds.extents.y) - centerOffset;
                transform.position = Vector3.MoveTowards(transform.position, desired, 0.7f * Time.deltaTime);
            }
            var centerBefore = box.bounds.center;
            var axis = touch.ReadValue<Vector2>();
            if (Mathf.Abs(axis.x) > 0.25f && Mathf.Abs(axis.x) >= Mathf.Abs(axis.y))
                transform.RotateAround(centerBefore, Vector3.up, axis.x * 45f * Time.deltaTime);
            else if (Mathf.Abs(axis.y) > 0.25f)
            {
                scaleFactor = NextScale(scaleFactor, axis.y, Time.deltaTime);
                transform.localScale = baseScale * scaleFactor;
                Physics.SyncTransforms();
                transform.position += centerBefore - box.bounds.center;
            }
        }

        private void LateUpdate()
        {
            if (!placed || box == null) return;
            Physics.SyncTransforms();
            var bounds = box.bounds;
            transform.position += ConstrainCenter(bounds.center, tableAnchor, bounds.extents.y) - bounds.center;
        }
        private void OnDisable() => RestoreRay();
        private void OnDestroy()
        {
            RestoreRay(); touch?.Dispose();
        }
    }
}
#endif
