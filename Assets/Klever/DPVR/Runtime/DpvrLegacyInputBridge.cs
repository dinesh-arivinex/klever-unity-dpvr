#if KLEVER_DPN
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;

namespace Klever.DPVR
{
    [StructLayout(LayoutKind.Sequential)]
    [UnityEngine.Scripting.Preserve]
    public struct DpvrLegacyControllerState : IInputStateTypeInfo
    {
        public FourCC format => new FourCC('D', 'P', 'N', 'C');
        [InputControl(name = "isTracked", layout = "Button", format = "BIT", bit = 0)]
        public byte tracked;
        [InputControl(layout = "Integer")] public int trackingState;
        [InputControl(layout = "Vector3")] public Vector3 devicePosition;
        [InputControl(layout = "Quaternion")] public Quaternion deviceRotation;
        [InputControl(name = "triggerPressed", layout = "Button", bit = 0)]
        [InputControl(name = "primaryButton", layout = "Button", bit = 0)]
        public uint buttons;
        [InputControl(layout = "Axis")] public float trigger;
        [InputControl(layout = "Vector2")] public Vector2 primary2DAxis;
        [InputControl(layout = "Vector2")] public Vector2 uiScroll;
    }

    [InputControlLayout(stateType = typeof(DpvrLegacyControllerState), displayName = "DPVR Legacy Right Controller")]
    [UnityEngine.Scripting.Preserve]
    public sealed class DpvrLegacyController : XRController { }

    // Exposes the proprietary SDK controller through existing generic XRI bindings.
    // It deliberately does not claim physical position or haptic support.
    public sealed class DpvrLegacyInputBridge : MonoBehaviour
    {
        private DpvrLegacyController device;
        private dpn.DpnConnectionState lastConnection = (dpn.DpnConnectionState)(-100);
        private bool wasTouching;
        private Vector2 previousTouch;

        private void OnEnable()
        {
            InputSystem.RegisterLayout<DpvrLegacyController>();
            device = InputSystem.AddDevice<DpvrLegacyController>();
            InputSystem.SetDeviceUsage(device, UnityEngine.InputSystem.CommonUsages.RightHand);
        }

        private void Update()
        {
            if (device == null) return;
            var connection = dpn.DpnDaydreamController.State;
            bool connected = connection == dpn.DpnConnectionState.Connected;
            bool select = connected && (dpn.DpnDaydreamController.TriggerButton || dpn.DpnDaydreamController.ClickButton);
            bool touching = connected && dpn.DpnDaydreamController.IsTouching;
            Vector2 touch = touching ? dpn.DpnDaydreamController.TouchPos : Vector2.zero;
            bool centered = dpn.DpnManager.touchPosOrig == dpn.TouchPosOrig.CENTER;
            Vector2 axis = touching ? (centered ? touch : touch * 2f - Vector2.one) : Vector2.zero;
            axis.y = -axis.y;
            Vector2 scroll = touching && wasTouching && !dpn.DpnDaydreamController.TouchDown
                ? (touch - previousTouch) * (centered ? -10f : -20f) : Vector2.zero;
            scroll = Vector2.ClampMagnitude(scroll, 2f);
            previousTouch = touch;
            wasTouching = touching;
            InputSystem.QueueStateEvent(device, new DpvrLegacyControllerState {
                tracked = connected ? (byte)1 : (byte)0,
                trackingState = connected ? (int)InputTrackingState.Rotation : 0,
                deviceRotation = connected ? dpn.DpnDaydreamController.Orientation : Quaternion.identity,
                devicePosition = Vector3.zero,
                buttons = select ? 1u : 0u,
                trigger = select ? 1f : 0f,
                primary2DAxis = axis,
                uiScroll = scroll
            });
            if (connection != lastConnection)
            {
                Debug.Log("[DPVR-DPN] Controller connection=" + connection);
                lastConnection = connection;
            }
        }

        private void OnDisable()
        {
            if (device != null && device.added) InputSystem.RemoveDevice(device);
            device = null;
        }
    }
}
#endif
