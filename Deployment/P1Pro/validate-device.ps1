[CmdletBinding()]
param(
    [string]$AdbPath = "adb",
    [string]$FirmwareZipPath,
    [string]$ExpectedDevice = "P1Pro",
    [string]$ExpectedFirmware = "P1PV002B002SP897-OVERSEAS",
    [int]$ExpectedSdk = 27
)

$ErrorActionPreference = "Stop"
$expectedFirmwareSha256 = "67C03A0A722865F57F48BEC0BE2D930F43A134238F35EE9F1F53792BACF381D3"

function Write-Check {
    param([string]$Name, [string]$Value)
    Write-Host ("[OK] {0}: {1}" -f $Name, $Value)
}

function Invoke-Adb {
    param([string[]]$Arguments)
    $output = & $AdbPath @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "ADB failed: $($output -join [Environment]::NewLine)"
    }
    return @($output)
}

if ($FirmwareZipPath) {
    if (-not (Test-Path -LiteralPath $FirmwareZipPath -PathType Leaf)) {
        throw "Firmware archive not found: $FirmwareZipPath"
    }

    $actualHash = (Get-FileHash -LiteralPath $FirmwareZipPath -Algorithm SHA256).Hash
    if ($actualHash -ne $expectedFirmwareSha256) {
        throw "Firmware SHA-256 mismatch. Expected $expectedFirmwareSha256 but found $actualHash."
    }
    Write-Check "Firmware SHA-256" $actualHash
}

$adbCommand = Get-Command $AdbPath -ErrorAction SilentlyContinue
if ($null -eq $adbCommand -and -not (Test-Path -LiteralPath $AdbPath -PathType Leaf)) {
    throw "adb executable not found: $AdbPath"
}

$deviceLines = @(Invoke-Adb @("devices")) |
    Select-Object -Skip 1 |
    ForEach-Object { $_.ToString().Trim() } |
    Where-Object { $_ }

$unauthorized = @($deviceLines | Where-Object { $_ -match "\s(unauthorized|offline)$" })
if ($unauthorized.Count -gt 0) {
    throw "ADB device is unauthorized or offline. Put on the headset, accept USB debugging, and retry."
}

$authorized = @($deviceLines | Where-Object { $_ -match "\sdevice$" })
if ($authorized.Count -ne 1) {
    throw "Expected exactly one authorized headset, found $($authorized.Count). Disconnect every other Android device."
}

$serial = ($authorized[0] -split "\s+")[0]
Write-Check "ADB serial" $serial

function Get-DeviceProperty {
    param([string]$Name)
    return ((Invoke-Adb @("-s", $serial, "shell", "getprop", $Name)) -join "").Trim()
}

$device = Get-DeviceProperty "ro.product.device"
$model = Get-DeviceProperty "ro.product.model"
$sdkText = Get-DeviceProperty "ro.build.version.sdk"
$incremental = Get-DeviceProperty "ro.build.version.incremental"
$display = Get-DeviceProperty "ro.build.display.id"
$fingerprint = Get-DeviceProperty "ro.build.fingerprint"
$abis = Get-DeviceProperty "ro.product.cpu.abilist"

$identity = (($device + $model) -replace "\s", "").ToLowerInvariant()
if (-not $identity.Contains(($ExpectedDevice -replace "\s", "").ToLowerInvariant())) {
    throw "Connected hardware is '$device / $model', not the expected $ExpectedDevice. Do not flash or install."
}
Write-Check "Device" "$device / $model"

$sdk = 0
if (-not [int]::TryParse($sdkText, [ref]$sdk) -or $sdk -ne $ExpectedSdk) {
    throw "Android SDK is '$sdkText'; expected API $ExpectedSdk for the supplied global firmware."
}
Write-Check "Android SDK" $sdkText

if ($incremental -ne $ExpectedFirmware -and $display -notlike "*$ExpectedFirmware*") {
    throw "Firmware is '$incremental' (display '$display'); expected $ExpectedFirmware."
}
Write-Check "Firmware" $ExpectedFirmware

if ($abis -notmatch "(^|,)arm64-v8a(,|$)") {
    throw "The device does not advertise arm64-v8a. Current Unity output is ARM64-only. Reported ABIs: $abis"
}
Write-Check "ABIs" $abis
Write-Check "Fingerprint" $fingerprint

$xrPackages = @(Invoke-Adb @("-s", $serial, "shell", "pm", "list", "packages")) |
    Where-Object { $_ -match "(?i)(openxr|deepoon|dpvr)" }
$vrFeatures = @(Invoke-Adb @("-s", $serial, "shell", "pm", "list", "features")) |
    Where-Object { $_ -match "(?i)(vr|headtracking)" }

Write-Host "[INFO] XR/DPVR packages:"
if ($xrPackages.Count -eq 0) { Write-Host "  (none exposed as an Android package; verify the runtime from Unity logs)" }
else { $xrPackages | ForEach-Object { Write-Host "  $_" } }

Write-Host "[INFO] VR/head-tracking features:"
if ($vrFeatures.Count -eq 0) { Write-Host "  (none declared; the app manifest keeps head-tracking optional)" }
else { $vrFeatures | ForEach-Object { Write-Host "  $_" } }

Write-Host "[PASS] P1 Pro Edu firmware/device baseline is ready for APK testing."

