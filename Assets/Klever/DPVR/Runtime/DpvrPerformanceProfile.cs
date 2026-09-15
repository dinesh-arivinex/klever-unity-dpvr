using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;

namespace Klever.DPVR
{
    /// <summary>Conservative standalone-VR defaults for sustained P1 MAX sessions.</summary>
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
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.PerTexture;
            QualitySettings.skinWeights = SkinWeights.TwoBones;
            XRSettings.eyeTextureResolutionScale = 1f;
            Application.backgroundLoadingPriority = ThreadPriority.Low;
            Application.targetFrameRate = GetRuntimeRefreshRate();
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

            return 72;
        }
    }
}
