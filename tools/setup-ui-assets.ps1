<#
.SYNOPSIS
  Scarica e mette a posto gli asset gratuiti dell'interfaccia (fuori dal repo), PRIMA di aprire Unity.

.DESCRIPTION
  Come per gli asset Fab, nel repo ci sono solo i .meta (docs/ASSETS.md). Questo script scarica:
    - Kenney "UI Pack - Sci-Fi" (CC0) da OpenGameArt: sagome dei pannelli, mirini, font Kenney Future
      -> Assets/ThirdParty/Kenney/UIPackSciFi/
    - rubberduck "Sci-Fi interface textures" (CC0) da OpenGameArt: pannelli olografici 2048 px
      -> Assets/ThirdParty/OpenGameArt/SciFiInterface/
    - Google Fonts Rajdhani, Orbitron, Share Tech Mono (SIL Open Font License) da GitHub
      -> Assets/ThirdParty/GoogleFonts/
  Le texture procedurali del HUD (Assets/ThirdParty/_Own/Hud/) le genera Unity: FpsWave > Setup > 7. Build UI.
  Se i download sono già in Download (kenney_ui-pack-scifi.zip, sci-fi-interface-textures.zip) li usa senza riscaricarli.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File tools\setup-ui-assets.ps1
#>
param(
    [string]$Downloads = (Join-Path $env:USERPROFILE "Downloads"),
    [string]$Project = (Split-Path -Parent $PSScriptRoot),
    [switch]$Force
)

$ErrorActionPreference = "Stop"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Get-Zip([string]$name, [string]$url) {
    $local = Join-Path $Downloads $name
    if ((Test-Path $local) -and -not $Force) {
        Write-Host "-> uso $local"
        return $local
    }
    Write-Host "-> scarico $url"
    Invoke-WebRequest -Uri $url -OutFile $local -UseBasicParsing
    return $local
}

function Extract-Selected([string]$zipPath, [string]$destDir, [hashtable]$map) {
    New-Item -ItemType Directory -Force -Path $destDir | Out-Null
    $zip = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        foreach ($entry in $zip.Entries) {
            $key = $entry.FullName.Replace('\', '/')
            if ($map.ContainsKey($key)) {
                $target = Join-Path $destDir $map[$key]
                [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $target, $true)
            }
        }
    } finally { $zip.Dispose() }
}

$thirdParty = Join-Path $Project "Assets\ThirdParty"

# --- Kenney UI Pack Sci-Fi (CC0) -------------------------------------------------
$kenneyZip = Get-Zip "kenney_ui-pack-scifi.zip" "https://opengameart.org/sites/default/files/kenney_ui-pack-scifi.zip"
$kenneyMap = @{ "License.txt" = "License.txt"; "Font/Kenney Future.ttf" = "KenneyFuture.ttf"; "Font/Kenney Future Narrow.ttf" = "KenneyFutureNarrow.ttf" }
foreach ($n in "panel_glass","panel_glass_notch_bl","panel_glass_notch_br","panel_glass_notch_tl","panel_glass_notch_tr","panel_glass_notches","panel_glass_notches_top","crosshair_a","crosshair_b","crosshair_c","crosshair_d") {
    $kenneyMap["PNG/Extra/Default/$n.png"] = "$n.png"
}
Extract-Selected $kenneyZip (Join-Path $thirdParty "Kenney\UIPackSciFi") $kenneyMap

# --- rubberduck Sci-Fi interface textures (CC0) ----------------------------------
$rdZip = Get-Zip "sci-fi-interface-textures.zip" "https://opengameart.org/sites/default/files/sci-fi-interface-textures.zip"
$rdMap = @{}
foreach ($i in 1..4) { $rdMap["interface_${i}_no_effects.png"] = "interface_${i}_no_effects.png" }
Extract-Selected $rdZip (Join-Path $thirdParty "OpenGameArt\SciFiInterface") $rdMap

# --- Google Fonts (SIL OFL) -----------------------------------------------------
$fontsDir = Join-Path $thirdParty "GoogleFonts"
New-Item -ItemType Directory -Force -Path $fontsDir | Out-Null
$fonts = @{
    "Rajdhani-SemiBold.ttf" = "rajdhani/Rajdhani-SemiBold.ttf"
    "Rajdhani-Bold.ttf" = "rajdhani/Rajdhani-Bold.ttf"
    "Orbitron-Variable.ttf" = "orbitron/Orbitron%5Bwght%5D.ttf"
    "ShareTechMono-Regular.ttf" = "sharetechmono/ShareTechMono-Regular.ttf"
    "OFL.txt" = "rajdhani/OFL.txt"
}
foreach ($name in $fonts.Keys) {
    $target = Join-Path $fontsDir $name
    if ((Test-Path $target) -and -not $Force) { continue }
    Invoke-WebRequest -Uri ("https://raw.githubusercontent.com/google/fonts/main/ofl/" + $fonts[$name]) -OutFile $target -UseBasicParsing
}

Write-Host ""
Write-Host "Asset interfaccia pronti. In Unity: FpsWave > Setup > 7. Build UI (genera le texture del HUD)." -ForegroundColor Green
