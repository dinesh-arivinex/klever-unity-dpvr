# DPVR P1 Pro Edu deployment checks

Run these checks after applying the DPVR global firmware and before installing a
Klever release APK. The script is read-only: it validates the firmware archive,
connected device identity, Android API, firmware build, ARM64 ABI, and reports
the device's visible XR packages/features.

```powershell
.\validate-device.ps1 `
  -AdbPath "C:\Users\Admin\Desktop\klever\klever_vr\DPVR AIO Firmware Upgrade\adb.exe" `
  -FirmwareZipPath "C:\Users\Admin\Desktop\klever\klever_vr\DPVR AIO Firmware Upgrade\update.zip"
```

Expected baseline:

- Device: `P1Pro`
- Firmware: `P1PV002B002SP897-OVERSEAS`
- Android: 8.1 / API 27
- ABI: `arm64-v8a`
- Firmware ZIP SHA-256:
  `67C03A0A722865F57F48BEC0BE2D930F43A134238F35EE9F1F53792BACF381D3`

The script requires exactly one authorized ADB device. It deliberately stops on
an unexpected model, firmware, API, ABI, unauthorized device, or hash mismatch.
After it passes, install the release-signed APK and capture Unity/Android logs for
the `[DPVR] Active XR loader`, `[DPVR] OpenXR runtime`, and Android-baseline lines.
