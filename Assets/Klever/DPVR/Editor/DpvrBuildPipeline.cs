using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.Features.Interactions;

namespace Klever.DPVR.Editor
{
    /// <summary>
    /// Deterministic P1 Pro Edu project preparation, validation, and APK build entrypoint.
    /// Use from the Unity menu or with -executeMethod in CI.
    /// </summary>
    public static class DpvrBuildPipeline
    {
        private const string ApplicationId = "com.klever.vr.dpvr";
        private const string TargetDevice = "DPVR P1 Pro Edu";
        private const string TargetFirmware = "P1PV002B002SP897-OVERSEAS";
        private const string OpenXrLoaderType = "UnityEngine.XR.OpenXR.OpenXRLoader";
        private const string OculusLoaderType = "Unity.XR.Oculus.OculusLoader";
        private const string OutputPath = "Builds/DPVR/Klever_VR_P1Pro_Edu.apk";
        private const string AndroidManifestPath = "Assets/Plugins/Android/AndroidManifest.xml";

        [MenuItem("Klever/DPVR/Prepare OpenXR Project")]
        public static void PrepareProject()
        {
            PrepareProjectInternal();
            ValidateProject(true);
            Debug.Log("[DPVR] OpenXR project configuration completed.");
        }

        [MenuItem("Klever/DPVR/Validate Project")]
        public static void ValidateMenu()
        {
            ValidateProject(true);
        }

        [MenuItem("Klever/DPVR/Build Android APK")]
        public static void BuildAndroid()
        {
            PrepareAndValidateForBuild();

            string absoluteOutput = Path.GetFullPath(OutputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absoluteOutput));
            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = absoluteOutput,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.CompressWithLz4HC
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException(
                    "DPVR APK build failed with " + report.summary.totalErrors + " error(s). See the Unity Editor log.");

            Debug.Log("[DPVR] APK built successfully: " + absoluteOutput);
        }

        internal static void PrepareAndValidateForBuild()
        {
            PrepareProjectInternal();
            ValidateProject(false);
        }

        private static void PrepareProjectInternal()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android &&
                !EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
                throw new BuildFailedException("Unable to switch to Android. Install Unity Android Build Support.");

            IgnoreMetaProjectSetupTasksForDpvr();

            PlayerSettings.productName = "Klever VR DPVR";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, ApplicationId);
            // The supplied P1 Pro global firmware is Android 8.1 (API 27). Any
            // higher minimum is rejected by Android before the application starts.
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel27;
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)33;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.androidTVCompatibility = false;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
            ConfigureSigning();

            XRGeneralSettings generalSettings =
                XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
            if (generalSettings == null || generalSettings.Manager == null)
                throw new BuildFailedException("XR Plug-in Management settings are missing for Android.");

            XRManagerSettings manager = generalSettings.Manager;
            manager.automaticLoading = true;
            manager.automaticRunning = true;
            XRPackageMetadataStore.RemoveLoader(manager, OculusLoaderType, BuildTargetGroup.Android);
            if (!XRPackageMetadataStore.AssignLoader(manager, OpenXrLoaderType, BuildTargetGroup.Android))
                throw new BuildFailedException("Unable to assign the Unity OpenXR loader for Android.");

            OpenXRSettings openXrSettings =
                OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (openXrSettings == null)
                throw new BuildFailedException("Android OpenXR settings were not generated.");

            foreach (OpenXRFeature feature in openXrSettings.GetFeatures())
            {
                if (!IsMetaOpenXrFeature(feature))
                    continue;

                feature.enabled = false;
                EditorUtility.SetDirty(feature);
            }

            openXrSettings.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
            openXrSettings.depthSubmissionMode = OpenXRSettings.DepthSubmissionMode.None;

            KHRSimpleControllerProfile controllerProfile =
                openXrSettings.GetFeature<KHRSimpleControllerProfile>();
            if (controllerProfile == null)
                throw new BuildFailedException("Khronos Simple Controller Profile is unavailable.");
            controllerProfile.enabled = true;

            EditorUtility.SetDirty(generalSettings);
            EditorUtility.SetDirty(manager);
            EditorUtility.SetDirty(openXrSettings);
            EditorUtility.SetDirty(controllerProfile);
            AssetDatabase.SaveAssets();
        }

        private static void ConfigureSigning()
        {
            string keystorePath = Environment.GetEnvironmentVariable("DPVR_KEYSTORE_PATH");
            string keystorePassword = Environment.GetEnvironmentVariable("DPVR_KEYSTORE_PASSWORD");
            string keyAlias = Environment.GetEnvironmentVariable("DPVR_KEY_ALIAS");
            string keyAliasPassword = Environment.GetEnvironmentVariable("DPVR_KEY_ALIAS_PASSWORD");

            bool hasReleaseSigning = !string.IsNullOrWhiteSpace(keystorePath) &&
                                     File.Exists(keystorePath) &&
                                     !string.IsNullOrWhiteSpace(keystorePassword) &&
                                     !string.IsNullOrWhiteSpace(keyAlias) &&
                                     !string.IsNullOrWhiteSpace(keyAliasPassword);
            PlayerSettings.Android.useCustomKeystore = hasReleaseSigning;
            if (!hasReleaseSigning)
            {
                Debug.LogWarning(
                    "[DPVR] Release signing variables are incomplete; building a debug-signed device-test APK.");
                return;
            }

            PlayerSettings.Android.keystoreName = Path.GetFullPath(keystorePath);
            PlayerSettings.Android.keystorePass = keystorePassword;
            PlayerSettings.Android.keyaliasName = keyAlias;
            PlayerSettings.Android.keyaliasPass = keyAliasPassword;
        }

        private static void ValidateProject(bool logOnly)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            if (PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android) != ApplicationId)
                errors.Add("Android application identifier must be " + ApplicationId + ".");
            if (PlayerSettings.Android.targetArchitectures != AndroidArchitecture.ARM64)
                errors.Add("Android architecture must be ARM64 only.");
            if (PlayerSettings.Android.minSdkVersion != AndroidSdkVersions.AndroidApiLevel27)
                errors.Add("P1 Pro Edu global firmware requires Android minimum API level 27.");
            if (PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android) != ScriptingImplementation.IL2CPP)
                errors.Add("Android scripting backend must be IL2CPP.");

            GraphicsDeviceType[] graphicsApis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
            if (graphicsApis.Length != 1 || graphicsApis[0] != GraphicsDeviceType.OpenGLES3)
                errors.Add("DPVR requires OpenGLES3 only; remove Vulkan.");
            if (!XRPackageMetadataStore.IsLoaderAssigned(OpenXrLoaderType, BuildTargetGroup.Android))
                errors.Add("Unity OpenXR loader is not assigned for Android.");
            if (XRPackageMetadataStore.IsLoaderAssigned(OculusLoaderType, BuildTargetGroup.Android))
                errors.Add("Oculus loader must not be assigned to the DPVR Android build.");
            if (!EditorBuildSettings.scenes.Any(scene => scene.enabled))
                errors.Add("At least one scene must be enabled in Build Settings.");

            ValidateOpenXrAndroidManifest(errors);

            OpenXRSettings openXrSettings =
                OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            KHRSimpleControllerProfile profile =
                openXrSettings == null ? null : openXrSettings.GetFeature<KHRSimpleControllerProfile>();
            if (profile == null || !profile.enabled)
                errors.Add("Khronos Simple Controller Profile must be enabled.");
            if (openXrSettings != null &&
                openXrSettings.GetFeatures().Any(feature => feature.enabled && IsMetaOpenXrFeature(feature)))
                errors.Add("Meta/Oculus OpenXR features must be disabled for the DPVR build.");
            if (openXrSettings != null &&
                openXrSettings.renderMode != OpenXRSettings.RenderMode.SinglePassInstanced)
                errors.Add("OpenXR render mode must use Single Pass Instanced.");
            if (openXrSettings != null &&
                openXrSettings.depthSubmissionMode != OpenXRSettings.DepthSubmissionMode.None)
                errors.Add("OpenXR depth submission must be disabled for the P1 Pro Edu profile.");

            warnings.Add(TargetDevice + " must run the DPVR-supplied global firmware " + TargetFirmware + ".");
            warnings.Add("Confirm the firmware exposes the AOSP OpenXR runtime and controller profile before fleet rollout.");
            warnings.Add("Meta XR remains installed only for legacy scene serialization; the Oculus loader must stay disabled.");
            warnings.Add("Backend HTTP and MQTT/1883 remain enabled for compatibility; migrate production to HTTPS and MQTTS.");
            warnings.Add("Physical-device testing is mandatory for controller mapping, media decoding, thermal stability, and resume behavior.");

            foreach (string warning in warnings)
                Debug.LogWarning("[DPVR] " + warning);
            foreach (string error in errors)
                Debug.LogError("[DPVR] " + error);

            if (errors.Count > 0 && !logOnly)
                throw new BuildFailedException("DPVR validation failed:\n- " + string.Join("\n- ", errors));

            if (errors.Count == 0)
                Debug.Log("[DPVR] Project validation passed with " + warnings.Count + " release warning(s).");
        }

        private static void ValidateOpenXrAndroidManifest(List<string> errors)
        {
            if (!File.Exists(AndroidManifestPath))
            {
                errors.Add("Custom AndroidManifest.xml is missing; OpenXR runtime discovery cannot be verified.");
                return;
            }

            string manifest = File.ReadAllText(AndroidManifestPath);
            string[] requiredDeclarations =
            {
                "org.khronos.openxr.permission.OPENXR_SYSTEM",
                "org.khronos.openxr.runtime_broker",
                "org.khronos.openxr.system_runtime_broker",
                "org.khronos.openxr.OpenXRRuntimeService",
                "org.khronos.openxr.OpenXRApiLayerService",
                "org.khronos.openxr.intent.category.IMMERSIVE_HMD"
            };

            foreach (string declaration in requiredDeclarations)
            {
                if (!manifest.Contains(declaration))
                    errors.Add("AndroidManifest.xml is missing required OpenXR declaration: " + declaration);
            }
        }

        private static bool IsMetaOpenXrFeature(OpenXRFeature feature)
        {
            if (feature == null)
                return false;

            string typeName = feature.GetType().FullName ?? feature.GetType().Name;
            return typeName.IndexOf("Meta.XR", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   typeName.IndexOf("MetaQuest", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   typeName.IndexOf("OculusQuest", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Meta XR remains installed so the legacy scene can deserialize its authored
        /// components, but Meta's Quest-only project rules require API 32. Those rules
        /// are not applicable to this API-27 DPVR branch and would otherwise overwrite
        /// the verified P1 Pro settings during a build.
        /// </summary>
        private static void IgnoreMetaProjectSetupTasksForDpvr()
        {
            Type setupType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("OVRProjectSetup", false))
                .FirstOrDefault(type => type != null);
            if (setupType == null)
                return;

            MethodInfo getTasks = setupType.GetMethod(
                "GetTasks",
                BindingFlags.Static | BindingFlags.NonPublic,
                null,
                new[] { typeof(BuildTargetGroup) },
                null);
            if (getTasks == null || !(getTasks.Invoke(null, new object[] { BuildTargetGroup.Android }) is IEnumerable tasks))
                return;

            int ignoredCount = 0;
            foreach (object task in tasks)
            {
                MethodInfo setIgnored = task.GetType().GetMethod(
                    "SetIgnored",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (setIgnored == null)
                    continue;

                setIgnored.Invoke(task, new object[] { BuildTargetGroup.Android, true });
                ignoredCount++;
            }

            if (ignoredCount > 0)
                Debug.Log("[DPVR] Ignored " + ignoredCount + " Meta Quest project rule(s) for the P1 Pro build.");
        }
    }

    /// <summary>Prevents a manual Android build from accidentally using Oculus settings.</summary>
    public sealed class DpvrAndroidBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => -10000;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform == BuildTarget.Android)
                DpvrBuildPipeline.PrepareAndValidateForBuild();
        }
    }
}
