# Klever VR — DPVR P1 Pro Edu Port

This branch is the DPVR-specific Android/OpenXR edition of Klever VR. It targets
the DPVR P1 Pro Edu global firmware and preserves the Meta project in its original
branch/worktree.

## Supported baseline

- Unity `2022.3.67f2` with Android Build Support, SDK, NDK, and OpenJDK
- DPVR P1 Pro Edu global firmware `P1PV002B002SP897-OVERSEAS`
- Unity OpenXR `1.8.1`
- XR Interaction Toolkit `2.5.4`
- OpenGLES3, IL2CPP, ARM64, Android API 27 minimum
- Khronos Simple Controller Profile

The supplied global firmware is expected to provide the headset OpenXR runtime.
The application logs the active XR loader, OpenXR runtime, Android SDK, device
identifier, firmware display name, and supported ABIs at startup. A pilot headset
must confirm those values before the APK is released to the fleet. Do not import
the legacy `com.dpvr.openxr` package unless DPVR support explicitly requires it
for this exact firmware image.

## Prepare and build

1. Open this project using Unity `2022.3.67f2`.
2. Allow Package Manager to resolve the Unity OpenXR package.
3. Run **Klever > DPVR > Prepare OpenXR Project**.
4. Run **Klever > DPVR > Validate Project** and resolve all errors.
5. Run **Klever > DPVR > Build Android APK**.

The APK is written to `Builds/DPVR/Klever_VR_P1Pro_Edu.apk`.

Device-test APKs use Android debug signing when release credentials are absent.
Production CI must supply `DPVR_KEYSTORE_PATH`, `DPVR_KEYSTORE_PASSWORD`,
`DPVR_KEY_ALIAS`, and `DPVR_KEY_ALIAS_PASSWORD`; credentials and keystores must
never be committed to Git.

CI can invoke the same deterministic pipeline:

```text
Unity.exe -batchmode -quit -projectPath <project> \
  -executeMethod Klever.DPVR.Editor.DpvrBuildPipeline.BuildAndroid \
  -logFile Builds/DPVR/unity-build.log
```

## Runtime architecture

- `DpvrRuntimeBootstrap` replaces the Meta camera/controller rig at runtime with
  an OpenXR `XROrigin`, XRI ray interactor, and world-space UI input.
- `DpvrThreeDofArmModel` supplies a stable virtual controller position from the
  rotational-only P1 Pro controller.
- Meta-authored grab objects are adapted to `XRGrabInteractable` at runtime.
- Meta UnityEvent wrappers on non-UI objects are bridged to XRI events, allowing
  existing assessment callbacks to remain intact.
- UI buttons are handled through `XRUIInputModule` and
  `TrackedDeviceGraphicRaycaster`.
- DPVR-only behavior is guarded by `KLEVER_DPVR` and `KLEVER_OPENXR` Android
  scripting symbols.

The Meta SDK remains installed as a serialization/compile compatibility layer
because the production scene contains Meta component types. The Oculus XR loader,
Meta camera rig, controller/hand tracking roots, Oculus input module, and Meta
runtime behaviours are disabled in the DPVR build.

During project preparation, every Meta/Oculus OpenXR feature is disabled and the
Meta Quest project-setup rules are marked ignored. This is intentional: those
Quest rules require Android API 32 and must not overwrite the P1 Pro firmware's
API-27 baseline. With the Meta OpenXR feature disabled, Meta's own Android build
processor excludes the OVRPlugin native AAR while the managed assemblies remain
available to deserialize the legacy scene.

## P1 Pro interaction policy

P1 Pro is treated as a 3DoF device. Controller-ray selection is the primary interaction.
True hand tracking, positional grabbing, and Quest-style physical throwing cannot
be reproduced. Basketball and archery use the existing orientation/cursor-based
launch path and must be accepted as 3DoF adaptations rather than 6DoF parity.

## Performance profile

The DPVR runtime applies conservative sustained-performance defaults:

- 2x MSAA
- single-pass instanced stereo rendering
- one pixel light
- real-time shadows and reflection probes disabled
- OpenXR depth submission disabled
- eye texture scale `1.0`
- per-texture anisotropic filtering (never force-enabled globally)
- runtime display refresh rate used as the frame-rate target
- low-priority background loading
- no controller throw velocity calculation

Do not raise render scale, MSAA, shadow quality, or texture resolution without
profiling GPU time, memory, and thermals on a physical P1 Pro.

## Required device acceptance tests

- Cold launch, headset removal/resume, focus loss, recenter, and application exit
- Right-controller rotation, select, UI ray, scroll, grab, release, and cursor
- MCQ, fill-in-the-blanks, matching, basketball, and archery workflows
- Device registration, offline/online transition, MQTT reconnect, and live view
- Standard/360 video, audio, Vuplex WebView, and dynamic GLB/FBX loading
- Deep links and ObjectX external Android application launch
- 60-minute thermal/memory soak with representative lessons
- APK upgrade over the previous DPVR build without loss of device registration

## Release blockers

1. Build and compile verification in Unity `2022.3.67f2`.
2. Physical P1 Pro Edu validation on firmware `P1PV002B002SP897-OVERSEAS`.
3. Confirm the exact DPVR Input System control layout using Input Debugger. The
   project prefers the Khronos Simple Controller layout and retains generic
   `XRController` and vendor `DpvrController` fallbacks.
4. Confirm AVPro, Vuplex, FMOD, and native model-loader licenses and runtime support.
5. Migrate `http://44.222.108.144` and MQTT port `1883` to authenticated TLS before
   production deployment. Cleartext remains enabled only for current backend
   compatibility.
6. For mainland-China deployment, replace the Google connectivity probe and
   validate/fallback Google TTS, Translation, and YouTube dependencies.

## DPVR references

- Unity September 2025 security advisory (the branch uses patched
  `2022.3.67f2`; do not build a release with the former `2022.3.12f1` pin):
  <https://unity.com/security/sept-2025-01>
- DPVR OpenXR 1.1.0 tutorial (published for P1 Max; use as configuration guidance,
  not as proof of P1 Pro firmware compatibility):
  <https://static.dpvr.com/apps/developer/DPVROpenXRSDKTutorial1.1.0/index.html>
- DPVR SDK portal: <https://developer.dpvr.com/sdk.html#dpvrsdk>
