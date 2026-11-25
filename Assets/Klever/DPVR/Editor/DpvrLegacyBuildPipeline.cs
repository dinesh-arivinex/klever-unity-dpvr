using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;

namespace Klever.DPVR.Editor
{
    public static class DpvrLegacyBuildPipeline
    {

        [MenuItem("Klever/DPVR/Prepare Legacy Project")]
        public static void PrepareForBuild()
        {
            var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Android).Split(';');
            if (!defines.Contains("KLEVER_DPN") || !defines.Contains("KLEVER_DPVR"))
                throw new BuildFailedException("Select KLEVER_DPVR and KLEVER_DPN before the legacy build; do not select KLEVER_OPENXR.");
            if (defines.Contains("KLEVER_OPENXR"))
                throw new BuildFailedException("Legacy and OpenXR rendering must not be selected together.");
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.klever.vr.dpvr.porttest");
            PlayerSettings.productName = "Klever";
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel27;
            // Bundled ExoPlayer AAR metadata requires compile SDK 33 or newer.
            // Unity derives compileSdkVersion from this Android target setting.
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)33;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.useCustomKeystore = false;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.use32BitDisplayBuffer = true;
            PlayerSettings.MTRendering = false;
            PlayerSettings.colorSpace = ColorSpace.Gamma;
            // TriLib can recreate an empty preferences asset during a fresh import.
            // Restore the existing project's built-in-renderer material-mapper choice.
            TriLibCore.TriLibSettings.SetBool("StandardMaterialMapper", true);
            QualitySettings.vSyncCount = 0;
            QualitySettings.antiAliasing = 0;
            EditorUserBuildSettings.buildAppBundle = false;
            var settings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
            if (settings != null && settings.Manager != null)
            {
                settings.Manager.automaticLoading = false;
                settings.Manager.automaticRunning = false;
                // An assigned OpenXR loader still activates its build validators even
                // with automatic startup off. Preserve its asset, but unassign it here.
                XRPackageMetadataStore.RemoveLoader(settings.Manager, "UnityEngine.XR.OpenXR.OpenXRLoader", BuildTargetGroup.Android);
                XRPackageMetadataStore.RemoveLoader(settings.Manager, "Unity.XR.Oculus.OculusLoader", BuildTargetGroup.Android);
                EditorUtility.SetDirty(settings.Manager);
            }
            const string referencePath = "Assets/Resources/DpvrLegacyRigReference.asset";
            var reference = AssetDatabase.LoadAssetAtPath<DpvrLegacyRigReference>(referencePath);
            if (reference == null)
            {
                reference = ScriptableObject.CreateInstance<DpvrLegacyRigReference>();
                AssetDatabase.CreateAsset(reference, referencePath);
            }
            reference.prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DPN/Prefabs/DpnCameraRig.prefab");
            if (reference.prefab == null) throw new BuildFailedException("DPN camera prefab is missing.");
            EditorUtility.SetDirty(reference);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Klever/DPVR/Build Legacy Test APK")]
        public static void BuildTestAndroid()
        {
            var sdkOverride = Environment.GetEnvironmentVariable("KLEVER_ANDROID_SDK_ROOT");
            if (!string.IsNullOrWhiteSpace(sdkOverride))
            {
                if (!File.Exists(Path.Combine(sdkOverride, "platforms/android-33/android.jar")))
                    throw new BuildFailedException("The selected SDK is missing Android platform 33: " + sdkOverride);
                UnityEditor.Android.AndroidExternalToolsSettings.sdkRootPath = sdkOverride;
                Debug.Log("[DPVR-DPN] Android SDK=" + sdkOverride);
            }
            PrepareForBuild();
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new BuildFailedException("Select Android before calling the build method.");
            var output = Environment.GetEnvironmentVariable("KLEVER_DPVR_BUILD_OUTPUT");
            if (string.IsNullOrWhiteSpace(output)) output = "Builds/DPVR/Klever-" + PlayerSettings.bundleVersion + "-dpvr-p1-pro-edu-build" + Environment.GetEnvironmentVariable("KLEVER_ANDROID_VERSION_CODE") + ".apk";
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var compression = Environment.GetEnvironmentVariable("KLEVER_DPVR_FAST_BUILD") == "1"
                ? BuildOptions.CompressWithLz4 : BuildOptions.CompressWithLz4HC;
            UnityEditor.Build.Reporting.BuildReport report;
            using (var version = new Klever.VR.Editor.AndroidBuildVersion())
            report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = output,
                target = BuildTarget.Android,
                options = BuildOptions.Development | compression
            });
            Debug.Log("[DPVR-DPN] Build=" + report.summary.result + " errors=" + report.summary.totalErrors + " output=" + Path.GetFullPath(output));
            if (report.summary.result != BuildResult.Succeeded || report.summary.totalErrors != 0)
                throw new BuildFailedException("Legacy full-app build failed; inspect the build log.");
        }
    }
}
