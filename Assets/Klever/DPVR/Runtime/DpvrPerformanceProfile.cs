using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;

namespace Klever.DPVR
{
    /// <summary>Conservative standalone-VR defaults for sustained P1 Pro Edu sessions.</summary>
    [DefaultExecutionOrder(-9999)]
    public sealed class DpvrPerformanceProfile : MonoBehaviour
    {
        private void Awake()
        {
            QualitySettings.vSyncCount = 0;
            QualitySettings.antiAliasing = 2;
            QualitySettings.pixelLightCount = 1;
            QualitySettings.shadows = ShadowQuality.Disable;
            QualitySettings.shadowDistance = 20f;
            QualitySettings.softParticles = false;
            QualitySettings.realtimeReflectionProbes = false;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
            QualitySettings.skinWeights = SkinWeights.TwoBones;
#if KLEVER_DPN
            dpn.DpnManager.eyeTextureScale = 1f;
#else
            XRSettings.eyeTextureResolutionScale = 1f;
#endif
            Application.backgroundLoadingPriority = ThreadPriority.Low;
#if KLEVER_DPN
            // The connected P1 Pro firmware reported 72 Hz in the native SDK trace.
            Application.targetFrameRate = 72;
#else
            Application.targetFrameRate = GetRuntimeRefreshRate();
#endif
        }

        private static int GetRuntimeRefreshRate()
        {
            var displays = new List<XRDisplaySubsystem>();
            SubsystemManager.GetInstances(displays);
            for (int i = 0; i < displays.Count; i++)
            {
                float refreshRate;
                if (displays[i].running && displays[i].TryGetDisplayRefreshRate(out refreshRate))
                    return Mathf.Max(60, Mathf.RoundToInt(refreshRate));
            }

            // P1 Pro firmware should normally report the panel refresh rate. Use a
            // conservative fallback rather than assuming the newer P1 Max profile.
            return 60;
        }
    }
}
