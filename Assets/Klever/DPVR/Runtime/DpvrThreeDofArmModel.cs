using UnityEngine;
using UnityEngine.InputSystem;

namespace Klever.DPVR
{
    /// <summary>
    /// Provides a stable virtual controller position for the rotational-only P1 MAX
    /// controller. The controller rotation remains device-driven; only translation is
    /// synthesized from a shoulder/elbow model.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed class DpvrThreeDofArmModel : MonoBehaviour
    {
        [SerializeField] private Vector3 shoulderOffset = new Vector3(0.18f, -0.20f, 0.05f);
        [SerializeField] private float forearmLength = 0.42f;
        [SerializeField, Range(0f, 30f)] private float smoothing = 18f;

        private readonly InputAction rotationAction = new InputAction(
            "DPVR Controller Rotation",
            InputActionType.PassThrough,
            "<XRController>{RightHand}/deviceRotation",
            expectedControlType: "Quaternion");

        private Transform head;
        private bool hasPose;

        public void Configure(Transform headTransform)
        {
            head = headTransform;
        }

        private void OnEnable()
        {
            rotationAction.Enable();
        }

        private void OnDisable()
        {
            rotationAction.Disable();
        }

        private void LateUpdate()
        {
            if (head == null)
                return;

            Quaternion controllerRotation = rotationAction.ReadValue<Quaternion>();
            if (controllerRotation == default)
                controllerRotation = transform.rotation;

            Quaternion headYaw = Quaternion.Euler(0f, head.eulerAngles.y, 0f);
            Vector3 shoulder = head.position + headYaw * shoulderOffset;
            Vector3 aimDirection = controllerRotation * Vector3.forward;
            Vector3 targetPosition = shoulder + aimDirection.normalized * forearmLength;

            if (!hasPose || smoothing <= 0f)
            {
                transform.SetPositionAndRotation(targetPosition, controllerRotation);
                hasPose = true;
                return;
            }

            float blend = 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime);
            transform.position = Vector3.Lerp(transform.position, targetPosition, blend);
            transform.rotation = Quaternion.Slerp(transform.rotation, controllerRotation, blend);
        }

        private void OnDestroy()
        {
            rotationAction.Dispose();
        }
    }
}
