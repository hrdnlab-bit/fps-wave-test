<#
.SYNOPSIS
  Mette a posto gli asset Fab (fuori dal repo) su un PC nuovo, PRIMA di aprire Unity.

.DESCRIPTION
  Nel repo ci sono solo i .meta degli asset di terze parti (docs/ASSETS.md). Se Unity si apre senza i file,
  cancella quei .meta e i riferimenti di materiali e prefab si rompono. Questo script:
    - copia "M4 - FPS Weapon Animations Pack FREE" (zip m4_animations.zip o cartella "M4 Animations")
      in Assets/ThirdParty/Fab/M4Animations/ e genera Packed/*_SpecSmooth.png (Specular RGB + Gloss in alfa);
    - copia "FPS Guns 4K - Pistol 2" (cartella estratta o zip) in Assets/ThirdParty/Fab/Pistol2/ con i nomi
      usati dal progetto e genera *_MetalSmooth.png (Metallic in R, 1 - Roughness in alfa).
  I due pacchetti vanno scaricati gratis da Fab con il proprio account (link in docs/ASSETS.md).

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File tools\setup-fab-assets.ps1
  powershell -ExecutionPolicy Bypass -File tools\setup-fab-assets.ps1 -Downloads "D:\Scaricati" -Force
#>
param(
    [string]$Downloads = (Join-Path $env:USERPROFILE "Downloads"),
    [string]$Project = (Split-Path -Parent $PSScriptRoot),
    [switch]$Force
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class FpsWaveTexturePacker
{
    static byte[] Read(Bitmap bmp, out int stride)
    {
        var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
        BitmapData data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        stride = data.Stride;
        var bytes = new byte[stride * bmp.Height];
        Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
        bmp.UnlockBits(data);
        return bytes;
    }

    static void Write(int width, int height, byte[] bytes, int stride, string path)
    {
        using (var outBmp = new Bitmap(width, height, PixelFormat.Format32bppArgb))
        {
            BitmapData data = outBmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            for (int y = 0; y < height; y++)
                Marshal.Copy(bytes, y * stride, data.Scan0 + y * data.Stride, width * 4);
            outBmp.UnlockBits(data);
            outBmp.Save(path, ImageFormat.Png);
        }
    }

    static Bitmap LoadSized(string path, int width, int height)
    {
        var src = new Bitmap(path);
        if (src.Width == width && src.Height == height) return src;
        var resized = new Bitmap(src, new Size(width, height));
        src.Dispose();
        return resized;
    }

    // Specular RGB + luminanza della mappa Gloss in alfa (smoothness per URP Lit, workflow Specular).
    public static void PackSpecGloss(string specPath, string glossPath, string outPath)
    {
        using (var spec = new Bitmap(specPath))
        using (var gloss = LoadSized(glossPath, spec.Width, spec.Height))
        {
            int s1, s2;
            byte[] a = Read(spec, out s1);
            byte[] g = Read(gloss, out s2);
            for (int y = 0; y < spec.Height; y++)
                for (int x = 0; x < spec.Width; x++)
                {
                    int i = y * s1 + x * 4, j = y * s2 + x * 4; // ordine BGRA
                    a[i + 3] = (byte)((g[j + 2] * 299 + g[j + 1] * 587 + g[j] * 114) / 1000);
                }
            Write(spec.Width, spec.Height, a, s1, outPath);
        }
    }

    // Metallic in R, 1 - Roughness in alfa (smoothness per URP Lit, workflow Metallic).
    public static void PackMetalRough(string metalPath, string roughPath, string outPath)
    {
        using (var metal = new Bitmap(metalPath))
        using (var rough = LoadSized(roughPath, metal.Width, metal.Height))
        {
            int s1, s2;
            byte[] m = Read(metal, out s1);
            byte[] r = Read(rough, out s2);
            for (int y = 0; y < metal.Height; y++)
                for (int x = 0; x < metal.Width; x++)
                {
                    int i = y * s1 + x * 4, j = y * s2 + x * 4;
                    byte metallic = m[i + 2];
                    m[i] = 0; m[i + 1] = 0; m[i + 2] = metallic;
                    m[i + 3] = (byte)(255 - r[j + 2]);
                }
            Write(metal.Width, metal.Height, m, s1, outPath);
        }
    }
}
"@

function Step($text) { Write-Host "-> $text" -ForegroundColor Cyan }

function Find-Source([string]$folderName, [string]$zipName) {
    $folder = Get-ChildItem -Path $Downloads -Directory -Filter $folderName -Recurse -Depth 2 -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($folder) { return $folder.FullName }
    $zip = Join-Path $Downloads $zipName
    if (Test-Path $zip) {
        $temp = Join-Path $env:TEMP ("fpswave_" + [IO.Path]::GetFileNameWithoutExtension($zipName))
        if (Test-Path $temp) { Remove-Item $temp -Recurse -Force }
        [IO.Compression.ZipFile]::ExtractToDirectory($zip, $temp)
        $inner = Get-ChildItem -Path $temp -Directory -Filter $folderName -Recurse -Depth 2 | Select-Object -First 1
        if ($inner) { return $inner.FullName }
        return $temp
    }
    return $null
}

function Copy-Asset([string]$from, [string]$to) {
    if ((Test-Path $to) -and -not $Force) { return }
    if (-not (Test-Path $from)) { throw "File mancante nel pacchetto: $from" }
    New-Item -ItemType Directory -Force -Path (Split-Path $to) | Out-Null
    Copy-Item $from $to -Force
}

if (-not (Test-Path (Join-Path $Project "Assets"))) { throw "Cartella del progetto non valida: $Project" }
if (Get-Process -Name "Unity" -ErrorAction SilentlyContinue) {
    Write-Warning "Unity è aperto: chiudilo prima di continuare (altrimenti può cancellare i .meta degli asset mancanti)."
    Read-Host "Premi Invio quando Unity è chiuso"
}

$fab = Join-Path $Project "Assets\ThirdParty\Fab"
$missing = @()

# ---------------------------------------------------------------- M4 + braccia
$m4 = Find-Source "M4 Animations" "m4_animations.zip"
if ($m4) {
    Step "M4 + braccia da $m4"
    $dst = Join-Path $fab "M4Animations"
    Get-ChildItem -Path $m4 -File | ForEach-Object { Copy-Asset $_.FullName (Join-Path $dst $_.Name) }
    $packed = Join-Path $dst "Packed"
    New-Item -ItemType Directory -Force -Path $packed | Out-Null
    foreach ($name in "Hand", "Glove") {
        $out = Join-Path $packed "$($name)_SpecSmooth.png"
        if ((Test-Path $out) -and -not $Force) { continue }
        Step "  genero $name`_SpecSmooth.png"
        [FpsWaveTexturePacker]::PackSpecGloss((Join-Path $dst "$($name)_Specular.png"), (Join-Path $dst "$($name)_Gloss.png"), $out)
    }
} else { $missing += "M4 - FPS Weapon Animations Pack FREE (m4_animations.zip o cartella 'M4 Animations')" }

# ---------------------------------------------------------------- Pistola
$pistol = Find-Source "FPS Guns 4K - Pistol 2" "fps_guns_4k_pistol_2.zip"
if ($pistol) {
    Step "Pistola da $pistol"
    $dst = Join-Path $fab "Pistol2"
    Copy-Asset (Join-Path $pistol "Models\Pistol 2.fbx") (Join-Path $dst "Pistol2.fbx")
    foreach ($part in "Body", "Slide", "Bullet") {
        $tex = Join-Path $pistol "Textures\4K\Pistol 2 $part"
        Copy-Asset (Join-Path $tex "$($part)_Base_Color.png") (Join-Path $dst "$($part)_BaseColor.png")
        Copy-Asset (Join-Path $tex "$($part)_Normal_OpenGL.png") (Join-Path $dst "$($part)_Normal.png")
        Copy-Asset (Join-Path $tex "$($part)_Mixed_AO.png") (Join-Path $dst "$($part)_AO.png")
        $out = Join-Path $dst "$($part)_MetalSmooth.png"
        if ((Test-Path $out) -and -not $Force) { continue }
        Step "  genero $part`_MetalSmooth.png (4K, qualche secondo)"
        [FpsWaveTexturePacker]::PackMetalRough((Join-Path $tex "$($part)_Metallic.png"), (Join-Path $tex "$($part)_Roughness.png"), $out)
    }
} else { $missing += "FPS Guns 4K - Pistol 2 (cartella estratta o fps_guns_4k_pistol_2.zip)" }

Write-Host ""
if ($missing.Count -gt 0) {
    Write-Warning "Pacchetti non trovati in $Downloads :"
    $missing | ForEach-Object { Write-Warning "  - $_" }
    Write-Warning "Scaricali da Fab (link in docs/ASSETS.md) e rilancia lo script prima di aprire Unity."
    exit 1
}
Write-Host "Asset pronti. Ora puoi aprire il progetto con Unity Hub." -ForegroundColor Green
