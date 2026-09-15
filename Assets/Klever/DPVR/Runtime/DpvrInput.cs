using UnityEngine.InputSystem;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace Klever.DPVR
{
    /// <summary>
    /// Shared DPVR input actions. Multiple bindings are intentional: P1 MAX firmware
    /// versions can expose the Simple Controller either as a generic XRController or
    /// through the vendor DpvrController layout.
    /// </summary>
    public static class DpvrInput
    {
        private static readonly InputAction SelectAction = CreateSelectAction();

        public static bool SelectWasPressedThisFrame
        {
            get
            {
                EnsureEnabled();
                return SelectAction.WasPressedThisFrame();
            }
        }

        public static InputAction CreateButtonAction(string name)
        {
            var action = new InputAction(name, InputActionType.Button);
            action.AddBinding("<XRController>{RightHand}/triggerPressed");
            action.AddBinding("<XRController>{RightHand}/primaryButton");
            action.AddBinding("<DpvrController>{RightHand}/select");
            return action;
        }

        public static InputAction CreateAxisAction(string name)
        {
            var action = new InputAction(name, InputActionType.Value, expectedControlType: "Axis");
            action.AddBinding("<XRController>{RightHand}/trigger");
            action.AddBinding("<DpvrController>{RightHand}/select");
            return action;
        }

        private static InputAction CreateSelectAction()
        {
            var action = CreateButtonAction("DPVR Select");
            action.Enable();
            return action;
        }

        private static void EnsureEnabled()
        {
            if (!SelectAction.enabled)
                SelectAction.Enable();
        }
    }

    public static class DpvrInteractionUtility
    {
        public static void SetGrabEnabled(GameObject target, bool enabled)
        {
            if (target == null)
                return;

            XRGrabInteractable grab = target.GetComponent<XRGrabInteractable>();
            if (grab != null)
                grab.enabled = enabled;
        }
    }
}
