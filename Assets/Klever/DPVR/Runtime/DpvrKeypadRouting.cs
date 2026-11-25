#if KLEVER_DPN
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace Klever.DPVR
{
    // Only the registration keypad is adapted. Keep its authored callbacks and
    // geometry; each collider belongs to exactly one functional button.
    public sealed class DpvrKeypadRouting : MonoBehaviour
    {
        private bool configured;

        public static void ConfigureAll(XRInteractionManager manager)
        {
            foreach (var owner in Resources.FindObjectsOfTypeAll<DeviceIDManager>())
            {
                if (!owner.gameObject.scene.IsValid()) continue;
                var routing = owner.GetComponent<DpvrKeypadRouting>();
                if (routing == null) routing = owner.gameObject.AddComponent<DpvrKeypadRouting>();
                routing.Configure(owner, manager);
            }
        }

        public static bool IsKey(XRSimpleInteractable candidate, DeviceIDManager owner)
        {
            var action = candidate.selectEntered;
            for (int i = 0; i < action.GetPersistentEventCount(); i++)
                if (action.GetPersistentTarget(i) == owner &&
                    (action.GetPersistentMethodName(i) == nameof(DeviceIDManager.OnButtonClick) ||
                     action.GetPersistentMethodName(i) == nameof(DeviceIDManager.OnBackspaceClick) ||
                     action.GetPersistentMethodName(i) == nameof(DeviceIDManager.OnSubmitClick)))
                    return true;
            return false;
        }

        private void Configure(DeviceIDManager owner, XRInteractionManager manager)
        {
            if (configured) return;
            var keys = new HashSet<XRBaseInteractable>();
            var owned = new HashSet<Collider>();
            var decorative = new HashSet<XRSimpleInteractable>();
            var sceneObjects = new List<XRBaseInteractable>();
            foreach (var candidate in Resources.FindObjectsOfTypeAll<XRBaseInteractable>())
            {
                if (candidate.gameObject.scene != owner.gameObject.scene) continue;
                sceneObjects.Add(candidate);
                if (!(candidate is XRSimpleInteractable key) || !IsKey(key, owner)) continue;
                // Refuse to swallow another functional interaction under a key.
                foreach (var child in key.GetComponentsInChildren<XRSimpleInteractable>(true))
                    if (child != key && HasCallbacks(child))
                    {
                        Debug.LogError("[DPVR-KEYPAD] Nested functional button requires explicit routing; no repair applied.");
                        return;
                    }
                keys.Add(key);
                foreach (var collider in key.GetComponentsInChildren<Collider>(true)) owned.Add(collider);
                foreach (var child in key.GetComponentsInChildren<XRSimpleInteractable>(true))
                    if (child != key) decorative.Add(child);
            }
            if (keys.Count == 0) return; // Scene may not contain the registration keypad.

            var affected = new HashSet<XRBaseInteractable>(keys);
            foreach (var item in sceneObjects)
                foreach (var collider in item.colliders)
                    if (owned.Contains(collider)) { affected.Add(item); break; }
            foreach (var item in decorative) affected.Add(item);
            // Unregister before editing lists: the manager's collider map must be
            // rebuilt, including associations retained by an ancestor container.
            foreach (var item in affected)
                if (item.interactionManager != null)
                    item.interactionManager.UnregisterInteractable((IXRInteractable)item);
            foreach (var item in decorative) item.enabled = false;
            foreach (var item in affected)
            {
                item.colliders.RemoveAll(c => owned.Contains(c));
                if (keys.Contains(item))
                    foreach (var collider in item.GetComponentsInChildren<Collider>(true))
                        item.colliders.Add(collider); // Includes the root trigger excluded by XRI auto-discovery.
                item.interactionManager = manager;
            }
            foreach (var item in affected)
                if (item.isActiveAndEnabled) manager.RegisterInteractable((IXRInteractable)item);
            configured = true;
            Debug.Log("[DPVR-KEYPAD] Configured buttons=" + keys.Count + " colliders=" + owned.Count +
                      " disabledDecorativeInteractables=" + decorative.Count);
        }

        private static bool HasCallbacks(XRSimpleInteractable item) =>
            item.selectEntered.GetPersistentEventCount() + item.selectExited.GetPersistentEventCount() +
            item.firstSelectEntered.GetPersistentEventCount() + item.lastSelectExited.GetPersistentEventCount() +
            item.hoverEntered.GetPersistentEventCount() + item.hoverExited.GetPersistentEventCount() +
            item.firstHoverEntered.GetPersistentEventCount() + item.lastHoverExited.GetPersistentEventCount() +
            item.activated.GetPersistentEventCount() + item.deactivated.GetPersistentEventCount() > 0;
    }
}
#endif
