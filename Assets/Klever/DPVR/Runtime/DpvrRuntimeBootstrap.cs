using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.Events;

namespace Klever.DPVR
{
    /// <summary>
    /// Creates the P1 Pro Edu OpenXR rig and adapts the existing Meta-authored scene at
    /// runtime. Keeping this in one platform bootstrap prevents DPVR-specific logic
    /// from leaking into lesson, media, networking, and assessment systems.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class DpvrRuntimeBootstrap : MonoBehaviour
    {
        private const string RuntimeRootName = "[Klever] DPVR OpenXR Runtime";
        private readonly List<InputAction> ownedActions = new List<InputAction>();
        private readonly HashSet<int> convertedObjects = new HashSet<int>();
        private XROrigin xrOrigin;
        private XRInteractionManager interactionManager;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
#if UNITY_ANDROID && KLEVER_DPVR
            if (FindObjectOfType<DpvrRuntimeBootstrap>() != null)
                return;

            var root = new GameObject(RuntimeRootName);
            DontDestroyOnLoad(root);
            root.AddComponent<DpvrRuntimeBootstrap>();
            root.AddComponent<DpvrPerformanceProfile>();
#endif
        }

        private void Awake()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void Start()
        {
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
            StartCoroutine(LogRuntimeBaseline());
            StartCoroutine(RefreshDynamicContent());
        }

        private static IEnumerator LogRuntimeBaseline()
        {
            // XR initialization completes before scene load in the normal build, but
            // one frame keeps diagnostics reliable after a cold boot or app resume.
            yield return null;

            XRGeneralSettings settings = XRGeneralSettings.Instance;
            XRLoader activeLoader = settings == null || settings.Manager == null
                ? null
                : settings.Manager.activeLoader;
            if (activeLoader == null)
            {
                Debug.LogError("[DPVR] No XR loader is active. Confirm the P1 Pro global firmware exposes its OpenXR runtime.");
            }
            else
            {
                Debug.Log("[DPVR] Active XR loader: " + activeLoader.name);
            }

            Debug.Log("[DPVR] OpenXR runtime: " + OpenXRRuntime.name + " " + OpenXRRuntime.version);
            Debug.Log("[DPVR] Unity device: " + SystemInfo.deviceModel + ", OS: " + SystemInfo.operatingSystem);

#if UNITY_ANDROID && !UNITY_EDITOR
            using (var build = new AndroidJavaClass("android.os.Build"))
            using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
            {
                string device = build.GetStatic<string>("DEVICE");
                string model = build.GetStatic<string>("MODEL");
                string display = build.GetStatic<string>("DISPLAY");
                string[] abis = build.GetStatic<string[]>("SUPPORTED_ABIS");
                int sdk = version.GetStatic<int>("SDK_INT");

                Debug.Log("[DPVR] Android baseline: device=" + device +
                          ", model=" + model +
                          ", sdk=" + sdk +
                          ", display=" + display +
                          ", abis=" + string.Join(",", abis));

                string normalizedDevice = (device ?? string.Empty).Replace(" ", string.Empty).ToLowerInvariant();
                string normalizedModel = (model ?? string.Empty).Replace(" ", string.Empty).ToLowerInvariant();
                if (!normalizedDevice.Contains("p1pro") && !normalizedModel.Contains("p1pro"))
                    Debug.LogWarning("[DPVR] This build targets P1 Pro Edu, but the connected device does not identify as P1Pro.");
                if (sdk < 27)
                    Debug.LogError("[DPVR] Android API " + sdk + " is below the supported P1 Pro baseline (API 27).");
            }
#endif
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            for (int i = 0; i < ownedActions.Count; i++)
                ownedActions[i].Dispose();
            ownedActions.Clear();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Camera sourceCamera = FindMetaCamera(scene);
            DisableMetaRigRoots(scene);

            if (xrOrigin == null)
                xrOrigin = CreateOpenXrRig(sourceCamera);

            ConfigureEventSystem();
            AdaptSceneContent();
        }

        private IEnumerator RefreshDynamicContent()
        {
            var delay = new WaitForSecondsRealtime(2f);
            while (true)
            {
                AdaptSceneContent();
                yield return delay;
            }
        }

        private XROrigin CreateOpenXrRig(Camera sourceCamera)
        {
            EnsureInteractionManager();

            var originObject = new GameObject("DPVR XR Origin");
            originObject.SetActive(false);
            originObject.transform.SetParent(transform, false);
            var origin = originObject.AddComponent<XROrigin>();

            var floorOffset = new GameObject("Camera Offset");
            floorOffset.transform.SetParent(originObject.transform, false);

            var cameraObject = new GameObject("DPVR Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(floorOffset.transform, false);
            var cameraComponent = cameraObject.AddComponent<Camera>();
            if (sourceCamera != null)
                cameraComponent.CopyFrom(sourceCamera);
            cameraComponent.stereoTargetEye = StereoTargetEyeMask.Both;
            cameraComponent.nearClipPlane = Mathf.Max(0.05f, cameraComponent.nearClipPlane);
            cameraObject.AddComponent<AudioListener>();

            var headDriver = cameraObject.AddComponent<TrackedPoseDriver>();
            headDriver.trackingType = TrackedPoseDriver.TrackingType.RotationOnly;
            headDriver.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            headDriver.positionInput = CreateActionProperty(
                "DPVR Head Position", InputActionType.PassThrough,
                "<XRHMD>/centerEyePosition", "Vector3");
            headDriver.rotationInput = CreateActionProperty(
                "DPVR Head Rotation", InputActionType.PassThrough,
                "<XRHMD>/centerEyeRotation", "Quaternion");
            headDriver.trackingStateInput = CreateActionProperty(
                "DPVR Head Tracking State", InputActionType.PassThrough,
                "<XRHMD>/trackingState", "Integer");

            origin.CameraFloorOffsetObject = floorOffset;
            origin.Camera = cameraComponent;
            origin.CameraYOffset = 1.6f;
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Device;

            CreateRightController(floorOffset.transform, cameraObject.transform);
            originObject.SetActive(true);
            return origin;
        }

        private void CreateRightController(Transform parent, Transform head)
        {
            var controllerObject = new GameObject("DPVR Right Controller");
            controllerObject.transform.SetParent(parent, false);
            controllerObject.transform.localPosition = new Vector3(0.25f, 1.25f, 0.35f);

            var controller = controllerObject.AddComponent<ActionBasedController>();
            controller.enableInputTracking = true;
            controller.enableInputActions = true;
            controller.rotationAction = CreateControllerActionProperty(
                "DPVR Controller Rotation", InputActionType.PassThrough,
                "deviceRotation", "Quaternion");
            controller.isTrackedAction = CreateControllerActionProperty(
                "DPVR Controller Tracked", InputActionType.Button,
                "isTracked", "Button");
            controller.trackingStateAction = CreateControllerActionProperty(
                "DPVR Controller Tracking State", InputActionType.PassThrough,
                "trackingState", "Integer");

            InputAction select = DpvrInput.CreateButtonAction("DPVR Controller Select");
            controller.selectAction = RegisterAction(select);
            controller.selectActionValue = RegisterAction(DpvrInput.CreateAxisAction("DPVR Controller Select Value"));
            controller.uiPressAction = controller.selectAction;
            controller.uiPressActionValue = controller.selectActionValue;

            var activate = new InputAction("DPVR Controller Activate", InputActionType.Button);
            activate.AddBinding("<XRController>{RightHand}/primaryButton");
            activate.AddBinding("<XRController>{RightHand}/secondaryButton");
            controller.activateAction = RegisterAction(activate);

            var rayInteractor = controllerObject.AddComponent<XRRayInteractor>();
            rayInteractor.interactionManager = interactionManager;
            rayInteractor.lineType = XRRayInteractor.LineType.StraightLine;
            rayInteractor.maxRaycastDistance = 20f;
            rayInteractor.enableUIInteraction = true;
            rayInteractor.hitClosestOnly = true;

            controllerObject.AddComponent<XRInteractorLineVisual>();
            var lineRenderer = controllerObject.GetComponent<LineRenderer>();
            if (lineRenderer != null)
            {
                lineRenderer.startWidth = 0.004f;
                lineRenderer.endWidth = 0.0015f;
                lineRenderer.useWorldSpace = true;
            }

            var armModel = controllerObject.AddComponent<DpvrThreeDofArmModel>();
            armModel.Configure(head);

            var cursorObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cursorObject.name = "DPVR Ray Cursor";
            cursorObject.transform.SetParent(transform, false);
            cursorObject.transform.localScale = Vector3.one * 0.025f;
            try
            {
                cursorObject.tag = "Cursor";
            }
            catch (UnityException)
            {
                Debug.LogWarning("[DPVR] The Cursor tag is missing; legacy archery/basketball trigger input will be unavailable.");
            }

            var cursorCollider = cursorObject.GetComponent<SphereCollider>();
            cursorCollider.isTrigger = true;
            var body = cursorObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            var cursor = cursorObject.AddComponent<DpvrRayCursor>();
            cursor.Configure(rayInteractor, cursorCollider);
        }

        private InputActionProperty CreateActionProperty(
            string name, InputActionType type, string binding, string expectedControlType)
        {
            var action = new InputAction(name, type, binding, expectedControlType: expectedControlType);
            return RegisterAction(action);
        }

        private InputActionProperty CreateControllerActionProperty(
            string name, InputActionType type, string controlPath, string expectedControlType)
        {
            var action = new InputAction(name, type, expectedControlType: expectedControlType);
            action.AddBinding("<KHRSimpleController>{RightHand}/" + controlPath);
            action.AddBinding("<XRController>{RightHand}/" + controlPath);
            action.AddBinding("<DpvrController>{RightHand}/" + controlPath);
            return RegisterAction(action);
        }

        private InputActionProperty RegisterAction(InputAction action)
        {
            action.Enable();
            ownedActions.Add(action);
            return new InputActionProperty(action);
        }

        private void EnsureInteractionManager()
        {
            if (interactionManager == null)
            {
                var managerObject = new GameObject("XR Interaction Manager");
                managerObject.transform.SetParent(transform, false);
                interactionManager = managerObject.AddComponent<XRInteractionManager>();
            }

            foreach (XRInteractionManager manager in Resources.FindObjectsOfTypeAll<XRInteractionManager>())
            {
                if (manager != null && manager != interactionManager && manager.gameObject.scene.IsValid())
                    manager.enabled = false;
            }
        }

        private static Camera FindMetaCamera(Scene scene)
        {
            foreach (GameObject candidate in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (candidate.scene == scene &&
                    (candidate.name == "OVRCameraRig" || candidate.name.Contains("Camera Rig")))
                {
                    Camera camera = candidate.GetComponentInChildren<Camera>(true);
                    if (camera != null)
                        return camera;
                }
            }
            return null;
        }

        private static void DisableMetaRigRoots(Scene scene)
        {
            foreach (GameObject candidate in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (candidate.scene != scene)
                    continue;

                string objectName = candidate.name;
                bool isMetaRig = objectName == "OVRCameraRig" ||
                                 objectName == "OVRInteraction" ||
                                 objectName.Contains("[BuildingBlock] Camera Rig") ||
                                 objectName.Contains("[BuildingBlock] Hand Tracking") ||
                                 objectName.Contains("[BuildingBlock] Controller Tracking");
                if (isMetaRig)
                    candidate.SetActive(false);
            }
        }

        private static void ConfigureEventSystem()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                var eventSystemObject = new GameObject("DPVR Event System");
                eventSystem = eventSystemObject.AddComponent<EventSystem>();
            }

            XRUIInputModule xrInput = eventSystem.GetComponent<XRUIInputModule>();
            if (xrInput == null)
                xrInput = eventSystem.gameObject.AddComponent<XRUIInputModule>();

            foreach (BaseInputModule module in eventSystem.GetComponents<BaseInputModule>())
                module.enabled = module == xrInput;
        }

        private static void ConfigureWorldSpaceCanvases()
        {
            foreach (Canvas canvas in FindObjectsOfType<Canvas>(true))
            {
                if (!canvas.gameObject.scene.IsValid() || canvas.renderMode != RenderMode.WorldSpace)
                    continue;

                if (canvas.GetComponent<TrackedDeviceGraphicRaycaster>() == null)
                    canvas.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();

                GraphicRaycaster standardRaycaster = canvas.GetComponent<GraphicRaycaster>();
                if (standardRaycaster != null)
                    standardRaycaster.enabled = false;
            }
        }

        private void AdaptSceneContent()
        {
            EnsureInteractionManager();
            ConfigureWorldSpaceCanvases();
            MonoBehaviour[] behaviours = Resources.FindObjectsOfTypeAll<MonoBehaviour>();
            ConvertMetaGrabbables(behaviours);
            BridgeMetaUnityEvents(behaviours);
            DisableMetaBehaviours(behaviours);
        }

        private void ConvertMetaGrabbables(MonoBehaviour[] behaviours)
        {
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour == null || !behaviour.gameObject.scene.IsValid())
                    continue;

                Type type = behaviour.GetType();
                if (!IsOculusInteractionType(type) || type.Name != "Grabbable")
                    continue;

                GameObject target = behaviour.gameObject;
                if (target.GetComponentInChildren<Collider>(true) == null ||
                    target.GetComponent<XRGrabInteractable>() != null)
                    continue;

                Rigidbody body = target.GetComponent<Rigidbody>();
                if (body == null)
                {
                    body = target.AddComponent<Rigidbody>();
                    body.isKinematic = true;
                    body.useGravity = false;
                }

                var grab = target.AddComponent<XRGrabInteractable>();
                grab.interactionManager = interactionManager;
                grab.movementType = XRBaseInteractable.MovementType.Kinematic;
                grab.throwOnDetach = false;
                grab.trackPosition = true;
                grab.trackRotation = true;
                grab.smoothPosition = true;
                grab.smoothRotation = true;
            }
        }

        private void BridgeMetaUnityEvents(MonoBehaviour[] behaviours)
        {
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour == null || !behaviour.gameObject.scene.IsValid())
                    continue;

                Type type = behaviour.GetType();
                if (!IsOculusInteractionType(type) || !type.Name.Contains("InteractableUnityEventWrapper"))
                    continue;

                int instanceId = behaviour.GetInstanceID();
                if (convertedObjects.Contains(instanceId))
                    continue;

                if (behaviour.GetComponentInParent<Selectable>(true) != null)
                {
                    convertedObjects.Add(instanceId);
                    continue;
                }

                XRBaseInteractable interactable = behaviour.GetComponentInParent<XRBaseInteractable>(true);
                if (interactable == null && behaviour.GetComponentInChildren<Collider>(true) != null)
                {
                    interactable = behaviour.gameObject.AddComponent<XRSimpleInteractable>();
                    interactable.interactionManager = interactionManager;
                }
                if (interactable == null)
                    continue;
                interactable.interactionManager = interactionManager;

                UnityEvent whenHover = GetUnityEvent(behaviour, "_whenHover");
                UnityEvent whenUnhover = GetUnityEvent(behaviour, "_whenUnhover");
                UnityEvent whenSelect = GetUnityEvent(behaviour, "_whenSelect");
                UnityEvent whenUnselect = GetUnityEvent(behaviour, "_whenUnselect");

                if (whenHover != null)
                    interactable.hoverEntered.AddListener(_ => whenHover.Invoke());
                if (whenUnhover != null)
                    interactable.hoverExited.AddListener(_ => whenUnhover.Invoke());
                if (whenSelect != null)
                    interactable.selectEntered.AddListener(_ => whenSelect.Invoke());
                if (whenUnselect != null)
                    interactable.selectExited.AddListener(_ => whenUnselect.Invoke());

                convertedObjects.Add(instanceId);
            }
        }

        private static UnityEvent GetUnityEvent(object source, string fieldName)
        {
            Type type = source.GetType();
            while (type != null)
            {
                FieldInfo field = type.GetField(fieldName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null)
                    return field.GetValue(source) as UnityEvent;
                type = type.BaseType;
            }
            return null;
        }

        private static void DisableMetaBehaviours(MonoBehaviour[] behaviours)
        {
            foreach (Behaviour behaviour in behaviours)
            {
                if (behaviour == null || !behaviour.gameObject.scene.IsValid())
                    continue;

                Type type = behaviour.GetType();
                if (IsOculusInteractionType(type) || type.Name.StartsWith("OVR", StringComparison.Ordinal))
                    behaviour.enabled = false;
            }
        }

        private static bool IsOculusInteractionType(Type type)
        {
            string typeNamespace = type.Namespace;
            return !string.IsNullOrEmpty(typeNamespace) &&
                   typeNamespace.StartsWith("Oculus.Interaction", StringComparison.Ordinal);
        }
    }

    /// <summary>Visual and trigger-compatible cursor at the current XRI ray hit.</summary>
    public sealed class DpvrRayCursor : MonoBehaviour
    {
        private XRRayInteractor rayInteractor;
        private SphereCollider triggerCollider;
        private Renderer cursorRenderer;

        public void Configure(XRRayInteractor interactor, SphereCollider collider)
        {
            rayInteractor = interactor;
            triggerCollider = collider;
            cursorRenderer = GetComponent<Renderer>();
        }

        private void LateUpdate()
        {
            if (rayInteractor == null)
                return;

            RaycastHit physicsHit;
            RaycastResult uiHit;
            bool hasPhysicsHit = rayInteractor.TryGetCurrent3DRaycastHit(out physicsHit);
            bool hasUiHit = rayInteractor.TryGetCurrentUIRaycastResult(out uiHit);

            if (hasPhysicsHit)
                transform.position = physicsHit.point - physicsHit.normal * 0.004f;
            else if (hasUiHit)
                transform.position = uiHit.worldPosition;
            else
                transform.position = rayInteractor.transform.position + rayInteractor.transform.forward * 5f;

            if (cursorRenderer != null)
                cursorRenderer.enabled = hasPhysicsHit || hasUiHit;
            if (triggerCollider != null)
                triggerCollider.enabled = hasPhysicsHit;
        }
    }
}
