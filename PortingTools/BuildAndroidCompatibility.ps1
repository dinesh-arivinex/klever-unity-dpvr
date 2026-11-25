[CmdletBinding()]
param(
    [string]$EditorRoot = 'C:\Program Files\Unity\Hub\Editor\2022.3.12f1\Editor',
    [string]$AndroidPlatform = 'android-32'
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$androidRoot = Join-Path $EditorRoot 'Data\PlaybackEngines\AndroidPlayer'
$javaBin = Join-Path $androidRoot 'OpenJDK\bin'
$androidJar = Join-Path $androidRoot "SDK\platforms\$AndroidPlatform\android.jar"
$source = Join-Path $PSScriptRoot 'AndroidSource\SurfaceCompatibility.java'
$classes = Join-Path $projectRoot ('Builds\CompatibilityClasses\' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmssfff'))
$jar = Join-Path $projectRoot 'Assets\Plugins\Android\klever-dpvr-surface.jar'
foreach ($required in @($androidJar, $source, (Join-Path $javaBin 'javac.exe'), (Join-Path $javaBin 'jar.exe'))) {
    if (-not (Test-Path -LiteralPath $required)) { throw "Required file missing: $required" }
}
New-Item -ItemType Directory -Path $classes -Force | Out-Null
& (Join-Path $javaBin 'javac.exe') --release 8 -classpath $androidJar -d $classes $source
if ($LASTEXITCODE -ne 0) { throw 'Java compatibility helper compilation failed.' }
& (Join-Path $javaBin 'jar.exe') cf $jar -C $classes .
if ($LASTEXITCODE -ne 0) { throw 'Java compatibility helper packaging failed.' }
Get-FileHash -LiteralPath $source, $jar -Algorithm SHA256
