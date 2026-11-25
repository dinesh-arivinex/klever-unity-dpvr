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
        // Rotation-only classroom start that the owner confirmed exposes the keypad.
        // The migrated XRI prefab's saved pose is at the teacher's station.
        public static Vector3 LegacyStartEyePosition => new Vector3(0f, 1.6f, 0f);
        private const string RuntimeRootName = "[Klever] DPVR OpenXR Runtime";
        private readonly List<InputAction> ownedActions = new List<InputAction>();
        private EventSystem runtimeEventSystem;
        private readonly HashSet<int> convertedObjects = new HashSet<int>();
        // Previously held only an XROrigin; the legacy SDK owns its own eye cameras.
        private GameObject runtimeRig;
        private XRInteractionManager interactionManager;
        private Camera interactionCamera;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
#if UNITY_ANDROID && KLEVER_DPVR && (!KLEVER_DPN || !UNITY_EDITOR)
            if (FindObjectOfType<DpvrRuntimeBootstrap>() != null)
                return;

#if KLEVER_DPN
            using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var compatibility = new AndroidJavaClass("com.klever.dpvr.compat.SurfaceCompatibility"))
            {
                if (!compatibility.CallStatic<bool>("prepare", activity))
                    Debug.LogError("[DPVR-DPN] Legacy drawing-surface preparation failed.");
            }
#endif
            var root = new GameObject(RuntimeRootName);
            DontDestroyOnLoad(root);
            root.AddComponent<DpvrRuntimeBootstrap>();
            root.AddComponent<DpvrPerformanceProfile>();
#if KLEVER_DPN
            root.name = "[Klever] DPVR Legacy Runtime";
            root.AddComponent<DpvrSessionDiagnostics>();
            root.AddComponent<DpvrLegacyInputBridge>();
#endif
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

#if KLEVER_DPN
            Debug.Log("[DPVR-DPN] Rendering SDK=" + dpn.DpnManager.DpvrUnityVersion + "; OpenXR loader intentionally disabled.");
#else
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
#endif
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
            ConfigureEventSystem();

            if (runtimeRig == null)
            {
#if KLEVER_DPN
                runtimeRig = CreateLegacyRig(sourceCamera);
#else
                runtimeRig = CreateOpenXrRig(sourceCamera).gameObject;
#endif
            }

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
            interactionCamera = cameraComponent;
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

#if KLEVER_DPN
        private GameObject CreateLegacyRig(Camera sourceCamera)
        {
            EnsureInteractionManager();
            var reference = Resources.Load<DpvrLegacyRigReference>("DpvrLegacyRigReference");
            if (reference == null || reference.prefab == null)
                throw new InvalidOperationException("Run Klever > DPVR > Prepare Legacy Project before building.");
            var rigObject = Instantiate(reference.prefab, transform);
            rigObject.name = "DPVR DPN Camera Rig";
            rigObject.transform.position = LegacyStartEyePosition;
            var rig = rigObject.GetComponent<dpn.DpnCameraRig>();
            if (rig == null || rig._center_eye == null || rig._left_eye == null)
                throw new InvalidOperationException("DPN eye cameras were not initialized.");
            foreach (Camera camera in rigObject.GetComponentsInChildren<Camera>(true))
                camera.tag = "Untagged";
            rig._left_eye.tag = "MainCamera";
            interactionCamera = rig._left_eye;
            if (sourceCamera != null) sourceCamera.enabled = false;
            ConfigureEventSystem();
            CreateRightController(transform, rig._center_eye.transform);
            // Keep SDK objects/references alive, while XRI owns pointing and clicks.
            // The SDK manager otherwise re-enables its separate gaze/ray visuals
            // when the controller connects or the user recenters.
            foreach (var manager in rigObject.GetComponentsInChildren<dpn.DpnPointerManager>(true))
            {
                manager.StopAllCoroutines();
                manager.enabled = false;
            }
            foreach (var pointer in rigObject.GetComponentsInChildren<dpn.DpnBasePointer>(true))
            {
                pointer.enabled = false;
                foreach (var visual in pointer.GetComponentsInChildren<Renderer>(true))
                    visual.enabled = false;
            }
            foreach (var ray in rigObject.GetComponentsInChildren<dpn.DpnBaseRay>(true))
            {
                ray.StopAllCoroutines();
                ray.enabled = false;
                foreach (var visual in ray.GetComponentsInChildren<Renderer>(true))
                    visual.enabled = false;
            }
            rigObject.AddComponent<DpvrLegacyPointerVisuals>().Configure(rigObject);
            Debug.Log("[DPVR-DPN] Native camera rig created; controller ray uses XRI.");
            return rigObject;
        }
#endif

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
#if KLEVER_DPN
            controller.uiScrollAction = CreateControllerActionProperty(
                "DPVR Touchpad Scroll", InputActionType.Value, "uiScroll", "Vector2");
#endif

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
            // Migrated physical keypad buttons use trigger BoxColliders.
            // XRRayInteractor defaults to Ignore, which makes these unclickable.
            rayInteractor.raycastTriggerInteraction = QueryTriggerInteraction.Collide;
            rayInteractor.raycastMask = Physics.DefaultRaycastLayers;

            var lineVisual = controllerObject.AddComponent<XRInteractorLineVisual>();
            lineVisual.stopLineAtFirstRaycastHit = true;
            lineVisual.stopLineAtSelection = false;
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
            // The sports cursor remains a trigger, but must not occlude its own ray.
            cursorObject.layer = 2; // Unity's built-in Ignore Raycast layer.
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
#if KLEVER_DPN
            if (Debug.isDebugBuild)
                controllerObject.AddComponent<DpvrViewDiagnostics>().Configure(head, rayInteractor);
#endif
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
            // The migrated scene uses an XRI XROrigin rather than an OVRCameraRig.
            // Retain its authored eye position before deactivating the old rig.
            foreach (var origin in Resources.FindObjectsOfTypeAll<XROrigin>())
                if (origin.gameObject.scene == scene && origin.Camera != null)
                    return origin.Camera;
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
                bool isMetaRig = candidate.GetComponent<XROrigin>() != null ||
                                 objectName == "OVRCameraRig" ||
                                 objectName == "OVRInteraction" ||
                                 objectName.Contains("[BuildingBlock] Camera Rig") ||
                                 objectName.Contains("[BuildingBlock] Hand Tracking") ||
                                 objectName.Contains("[BuildingBlock] Controller Tracking");
                if (isMetaRig)
                    candidate.SetActive(false);
            }
        }

        private void ConfigureEventSystem()
        {
            if (runtimeEventSystem == null)
            {
                var eventSystemObject = new GameObject("DPVR Event System");
                eventSystemObject.transform.SetParent(transform, false);
                runtimeEventSystem = eventSystemObject.AddComponent<EventSystem>();
            }
            EventSystem eventSystem = runtimeEventSystem;
            eventSystem.enabled = true;
            EventSystem.current = eventSystem;

            XRUIInputModule xrInput = eventSystem.GetComponent<XRUIInputModule>();
            if (xrInput == null)
                xrInput = eventSystem.gameObject.AddComponent<XRUIInputModule>();
            xrInput.enabled = true;

            foreach (BaseInputModule module in eventSystem.GetComponents<BaseInputModule>())
                module.enabled = module == xrInput;

            // The native rig can add a second EventSystem. Keep its objects for
            // SDK references but route all scene UI through the selected XRI one.
            foreach (EventSystem other in FindObjectsOfType<EventSystem>(true))
            {
                if (other == eventSystem) continue;
                foreach (BaseInputModule module in other.GetComponents<BaseInputModule>())
                    module.enabled = false;
                other.enabled = false;
            }
        }

        private void ConfigureWorldSpaceCanvases()
        {
            foreach (Canvas canvas in FindObjectsOfType<Canvas>(true))
            {
                if (!canvas.gameObject.scene.IsValid() || canvas.renderMode != RenderMode.WorldSpace)
                    continue;

                // Authored canvases may still reference the disabled Meta camera.
                if (interactionCamera != null)
                    canvas.worldCamera = interactionCamera;
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
            // The authored XRI objects may still point to a scene manager that the
            // platform bootstrap disables. Move registrations without losing events.
            foreach (var interactable in Resources.FindObjectsOfTypeAll<XRBaseInteractable>())
            {
                if (interactable != null && interactable.gameObject.scene.IsValid() &&
                    interactable.interactionManager != interactionManager)
                    interactable.interactionManager = interactionManager;
            }
            ConfigureWorldSpaceCanvases();
#if KLEVER_DPN
            DpvrKeypadRouting.ConfigureAll(interactionManager);
            DpvrLessonInteraction.ConfigureAll(interactionManager);
            DpvrControllerGuide.ConfigureAll();
#endif
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

#if KLEVER_DPN
    // Development diagnostics never record entered digits or registration data.
    public sealed class DpvrViewDiagnostics : MonoBehaviour
    {
        private Transform head;
        private XRRayInteractor ray;
        private float nextReport;
        private int reports;

        public void Configure(Transform headTransform, XRRayInteractor interactor)
        {
            head = headTransform;
            ray = interactor;
            nextReport = Time.unscaledTime + 3f;
        }

        private void LateUpdate()
        {
            if (head == null || ray == null) return;
            if (reports < 3 && Time.unscaledTime >= nextReport)
            {
                int total = 0, active = 0;
                float nearest = float.PositiveInfinity;
                foreach (var key in Resources.FindObjectsOfTypeAll<XRSimpleInteractable>())
                {
                    if (!key.gameObject.scene.IsValid() || key.name.Length != 1 || !char.IsDigit(key.name[0])) continue;
                    total++;
                    if (key.gameObject.activeInHierarchy) active++;
                    nearest = Mathf.Min(nearest, Vector3.Distance(head.position, key.transform.position));
                }
                int oldRigs = 0;
                foreach (var origin in Resources.FindObjectsOfTypeAll<XROrigin>())
                    if (origin.gameObject.scene.IsValid() && origin.gameObject.activeInHierarchy) oldRigs++;
                Debug.Log("[DPVR-VIEW] eye=" + head.position.ToString("F3") + " forward=" + head.forward.ToString("F3") +
                          " digitKeys=" + active + "/" + total + " nearest=" + nearest.ToString("F2") + " activeXrOrigins=" + oldRigs);
                reports++;
                nextReport = Time.unscaledTime + 10f;
            }
            if (DpvrInput.SelectWasPressedThisFrame)
            {
                bool hit = ray.TryGetCurrent3DRaycastHit(out var result);
                IXRInteractable target = null;
                if (hit) ray.interactionManager.TryGetInteractableForCollider(result.collider, out target);
                string category = !hit ? "none" : target != null ? "interactable" : "non-interactable collider";
                Debug.Log("[DPVR-VIEW] select hit=" + category + " distance=" + (hit ? result.distance.ToString("F2") : "none") +
                          " callbacks=" + (target is XRBaseInteractable button ? button.selectEntered.GetPersistentEventCount() : 0));
            }
        }
    }

    // DpnDaydreamController.EnableInternalObjects calls ray.SetVisible(true)
    // directly on connection changes, even with its pointer manager disabled.
    // Hide only those SDK renderers after its updates; preserve tracking objects.
    [DefaultExecutionOrder(20000)]
    public sealed class DpvrLegacyPointerVisuals : MonoBehaviour
    {
        private readonly HashSet<Renderer> visuals = new HashSet<Renderer>();

        public void Configure(GameObject rig)
        {
            foreach (var pointer in rig.GetComponentsInChildren<dpn.DpnBasePointer>(true))
                foreach (var visual in pointer.GetComponentsInChildren<Renderer>(true))
                    visuals.Add(visual);
            foreach (var ray in rig.GetComponentsInChildren<dpn.DpnBaseRay>(true))
                foreach (var visual in ray.GetComponentsInChildren<Renderer>(true))
                    visuals.Add(visual);
            // Native tracking remains enabled; the ray-only interaction does not
            // need the SDK's second animated controller model in front of the UI.
            foreach (var controller in rig.GetComponentsInChildren<dpn.DpnDaydreamController>(true))
                if (controller.model != null)
                    foreach (var visual in controller.model.GetComponentsInChildren<Renderer>(true))
                        visuals.Add(visual);
        }

        private void LateUpdate()
        {
            foreach (var visual in visuals)
                if (visual != null && visual.enabled) visual.enabled = false;
        }
    }
#endif

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
