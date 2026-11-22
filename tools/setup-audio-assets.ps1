<#
.SYNOPSIS
  Scarica e mette a posto i suoni gratuiti del gioco (fuori dal repo), PRIMA di aprire Unity.

.DESCRIPTION
  Come per gli asset Fab e dell'interfaccia, nel repo ci sono solo i .meta (docs/ASSETS.md). Questo script prende:
    - "The Free Firearm Sound Library" (CC0, OpenGameArt, 194 MB .7z): spari di AR-15 e Walther PPQ
      -> Assets/ThirdParty/Audio/FirearmLibrary/   (servono tar.exe di Windows 11 o 7-Zip per estrarre il .7z)
    - Kenney "Impact Sounds" e "Sci-fi Sounds" (CC0): passi, impatti, piastre, abilità della tuta
      -> Assets/ThirdParty/Audio/KenneyImpact/ e KenneySciFi/
      (kenney.nl non ha link diretti: scaricarli a mano da https://kenney.nl/assets/impact-sounds e
       https://kenney.nl/assets/sci-fi-sounds e lasciarli in Download come kenney_impact-sounds.zip e kenney_sci-fi-sounds.zip)
    - Kenney "Interface Sounds" (CC0) da GitHub (Calinou/kenney-interface-sounds): hit marker, voce della tuta, ping
      -> Assets/ThirdParty/Audio/KenneyInterface/
  Poi in Unity: FpsWave > Setup > 9. Build Audio (rifinisce gli spari in Assets/ThirdParty/_Own/Audio e crea la libreria).

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File tools\setup-audio-assets.ps1
#>
param(
    [string]$Downloads = (Join-Path $env:USERPROFILE "Downloads"),
    [string]$Project = (Split-Path -Parent $PSScriptRoot),
    [switch]$Force
)

$ErrorActionPreference = "Stop"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
Add-Type -AssemblyName System.IO.Compression.FileSystem

$audio = Join-Path $Project "Assets\ThirdParty\Audio"
New-Item -ItemType Directory -Force -Path $audio | Out-Null

function Extract-Prefixed([string]$zipPath, [string]$destDir, [string[]]$prefixes, [string]$folderInZip) {
    New-Item -ItemType Directory -Force -Path $destDir | Out-Null
    $zip = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        foreach ($entry in $zip.Entries) {
            $key = $entry.FullName.Replace('\', '/')
            if ($key -eq "License.txt") {
                [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, (Join-Path $destDir "License.txt"), $true)
                continue
            }
            if (-not $key.StartsWith($folderInZip)) { continue }
            $file = $key.Substring($folderInZip.Length)
            foreach ($p in $prefixes) {
                if ($file.StartsWith($p)) {
                    [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, (Join-Path $destDir $file), $true)
                    break
                }
            }
        }
    } finally { $zip.Dispose() }
}

# --- The Free Firearm Sound Library (CC0) ---------------------------------------
$fireDir = Join-Path $audio "FirearmLibrary"
$fireTargets = @{
    "AR15_near.wav" = "Prepared SFX Library/AR-15/D_32P.wav"
    "AR15_mid.wav"  = "Prepared SFX Library/AR-15/D_24P.wav"
    "PPQ_near.wav"  = "Prepared SFX Library/Walther PPQ/X_39P.wav"
    "PPQ_mid.wav"   = "Prepared SFX Library/Walther PPQ/X_31P.wav"
}
$missing = $fireTargets.Keys | Where-Object { -not (Test-Path (Join-Path $fireDir $_)) }
if ($missing -or $Force) {
    $sevenZip = Join-Path $Downloads "Prepared SFX Library.7z"
    if (-not (Test-Path $sevenZip)) {
        Write-Host "-> scarico The Free Firearm Sound Library (194 MB)..."
        Invoke-WebRequest -Uri "https://opengameart.org/sites/default/files/Prepared%20SFX%20Library.7z" -OutFile $sevenZip -UseBasicParsing
    }
    $tmp = Join-Path $env:TEMP "fpswave-firearms"
    New-Item -ItemType Directory -Force -Path $tmp | Out-Null
    $members = $fireTargets.Values
    $ok = $false
    try { & tar.exe -xf $sevenZip -C $tmp @members 2>$null; $ok = ($LASTEXITCODE -eq 0) } catch { $ok = $false }
    if (-not $ok) {
        $sevenExe = @("$env:ProgramFiles\7-Zip\7z.exe", "${env:ProgramFiles(x86)}\7-Zip\7z.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
        if (-not $sevenExe) { throw "Per estrarre il .7z serve Windows 11 (tar.exe) oppure 7-Zip installato." }
        & $sevenExe x $sevenZip "-o$tmp" -y @members | Out-Null
    }
    New-Item -ItemType Directory -Force -Path $fireDir | Out-Null
    foreach ($name in $fireTargets.Keys) {
        Copy-Item (Join-Path $tmp $fireTargets[$name]) (Join-Path $fireDir $name) -Force
    }
    Set-Content -Path (Join-Path $fireDir "License.txt") -Encoding UTF8 -Value @(
        "The Free Firearm Sound Library - Ben Jaszczak, Brian Nelson, Kevin Heras, Matthew Nanney.",
        "License: CC0 1.0 (no rights reserved). Source: https://opengameart.org/content/the-free-firearm-sound-library",
        "Files: AR-15 D_32P (near) / D_24P (mid), Walther PPQ X_39P (near) / X_31P (mid), renamed.")
    Remove-Item $tmp -Recurse -Force
}

# --- Kenney Impact Sounds e Sci-fi Sounds (CC0, download manuale da kenney.nl) --------
$impactZip = Join-Path $Downloads "kenney_impact-sounds.zip"
$scifiZip = Join-Path $Downloads "kenney_sci-fi-sounds.zip"
if (Test-Path $impactZip) {
    Extract-Prefixed $impactZip (Join-Path $audio "KenneyImpact") @("footstep_concrete_", "impactGeneric_light_", "impactMetal_light_", "impactMetal_heavy_", "impactPlate_light_", "impactPlate_heavy_", "impactSoft_medium_", "impactSoft_heavy_", "impactPunch_heavy_") "Audio/"
} else {
    Write-Warning "Manca ${impactZip}: scaricarlo da https://kenney.nl/assets/impact-sounds (Download, Continue without donating)."
}
if (Test-Path $scifiZip) {
    Extract-Prefixed $scifiZip (Join-Path $audio "KenneySciFi") @("forceField_", "thrusterFire_", "computerNoise_", "explosionCrunch_", "spaceEngineLow_", "lowFrequency_explosion_") "Audio/"
} else {
    Write-Warning "Manca ${scifiZip}: scaricarlo da https://kenney.nl/assets/sci-fi-sounds (Download, Continue without donating)."
}

# --- Kenney Interface Sounds (CC0, mirror GitHub) -------------------------------------
$uiDir = Join-Path $audio "KenneyInterface"
if (-not (Test-Path (Join-Path $uiDir "tick_002.wav")) -or $Force) {
    $uiZip = Join-Path $Downloads "kenney-interface-sounds.zip"
    if (-not (Test-Path $uiZip)) {
        Invoke-WebRequest -Uri "https://codeload.github.com/Calinou/kenney-interface-sounds/zip/HEAD" -OutFile $uiZip -UseBasicParsing
    }
    $wanted = @("tick_001", "tick_002", "tick_004", "confirmation_001", "confirmation_002", "confirmation_003", "error_004", "error_006", "bong_001", "select_003", "select_006", "pluck_001", "toggle_001", "glitch_002", "switch_002")
    New-Item -ItemType Directory -Force -Path $uiDir | Out-Null
    $zip = [System.IO.Compression.ZipFile]::OpenRead($uiZip)
    try {
        foreach ($entry in $zip.Entries) {
            $name = [System.IO.Path]::GetFileNameWithoutExtension($entry.Name)
            if ($entry.Name -eq "LICENSE.txt") {
                [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, (Join-Path $uiDir "License.txt"), $true)
            } elseif ($wanted -contains $name -and $entry.Name.EndsWith(".wav")) {
                [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, (Join-Path $uiDir $entry.Name), $true)
            }
        }
    } finally { $zip.Dispose() }
}

Write-Host ""
Write-Host "Suoni pronti. In Unity: FpsWave > Setup > 9. Build Audio (spari rifiniti + libreria dei suoni)." -ForegroundColor Green
