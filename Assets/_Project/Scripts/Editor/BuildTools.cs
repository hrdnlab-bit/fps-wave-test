using System.IO;
using System.IO.Compression;
using HrdnLab.FpsWave.Core;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HrdnLab.FpsWave.Editor
{
    /// <summary>
    /// Build Windows da condividere per i test (docs/ASSETS.md, "Come li ottiene un'altra persona"):
    /// cartella Build/FpsWaveTest_&lt;versione&gt;/ con exe, steam_appid.txt (Spacewar, solo sviluppo) e LEGGIMI.txt,
    /// più lo zip pronto da inviare. Build/ è esclusa da git.
    /// </summary>
    public static class BuildTools
    {
        private const string ProductName = "FPS Wave Test";
        private const string ExeName = "FpsWaveTest.exe";
        private static readonly string[] Scenes =
        {
            ProjectSetupTools.BootScenePath,
            "Assets/_Project/Scenes/Greybox.unity",
        };

        [MenuItem("FpsWave/Build/Windows + Zip (test con amici)")]
        public static void BuildWindowsZip()
        {
            string root = SteamSetupTools.ProjectRoot;
            string folderName = $"FpsWaveTest_{GameInfo.Version}";
            string outDir = Path.Combine(root, "Build", folderName);
            string zipPath = Path.Combine(root, "Build", $"{folderName}_win64.zip");
            if (Directory.Exists(outDir))
                Directory.Delete(outDir, true);
            Directory.CreateDirectory(outDir);

            PlayerSettings.productName = ProductName;
            PlayerSettings.bundleVersion = GameInfo.Version;
            PlayerSettings.runInBackground = true; // in multiplayer il gioco non deve fermarsi senza focus
            PlayerSettings.visibleInBackground = true;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;

            var options = new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = Path.Combine(outDir, ExeName),
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"[FpsWave] Build fallita: {report.summary.result}, errori {report.summary.totalErrors}.");
                return;
            }

            SteamSetupTools.WriteSteamAppId(outDir);
            File.WriteAllText(Path.Combine(outDir, "LEGGIMI.txt"), ReadMe());

            if (File.Exists(zipPath))
                File.Delete(zipPath);
            ZipFolder(outDir, zipPath, folderName);

            long mb = new FileInfo(zipPath).Length / (1024 * 1024);
            Debug.Log($"[FpsWave] Build pronta: {outDir} | zip {zipPath} ({mb} MB), durata {report.summary.totalTime.TotalSeconds:F0} s.");
            EditorUtility.RevealInFinder(zipPath);
        }

        /// <summary>Comprime la cartella saltando i file di debug di Burst/IL2CPP che non vanno distribuiti.</summary>
        private static void ZipFolder(string folder, string zipPath, string rootName)
        {
            using FileStream stream = File.Create(zipPath);
            using var zip = new ZipArchive(stream, ZipArchiveMode.Create);
            foreach (string file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(folder, file).Replace('\\', '/');
                if (relative.Contains("DoNotShip"))
                    continue;
                ZipArchiveEntry entry = zip.CreateEntry($"{rootName}/{relative}", System.IO.Compression.CompressionLevel.Optimal);
                using Stream entryStream = entry.Open();
                using FileStream input = File.OpenRead(file);
                input.CopyTo(entryStream);
            }
        }

        private static string ReadMe() =>
$@"FPS Wave Test {GameInfo.Version} - build di test (protocollo {GameInfo.ProtocolVersion})

PRIMA DI INIZIARE
- Steam deve essere aperto e con il tuo account collegato.
- Estrai TUTTO lo zip in una cartella (non avviare il gioco da dentro lo zip).
- Se Windows mostra ""PC protetto da Windows"": Ulteriori informazioni > Esegui comunque.
- Se il firewall chiede l'accesso alla rete: consenti.

GIOCARE INSIEME VIA STEAM
1. Chi ospita: avvia {ExeName} > ""Host Steam (crea lobby)"" > ""Invita amici"" e scegli l'amico.
2. L'amico: avvia {ExeName}, poi accetta l'invito nella chat/overlay di Steam
   (oppure nella lista amici: tasto destro sul nome > ""Unisciti alla partita"").
3. Entrambi dovete avere la STESSA versione di questa build.

Nota: in Steam il gioco compare come ""Spacewar"" (App ID 480 di sviluppo): è normale.

COMANDI
WASD movimento, mouse mira, Shift scatto, Ctrl/C accovacciati, Spazio salto,
tasto sinistro spara, tasto destro mira, R ricarica, 1 fucile, 2 pistola, Esc libera il cursore.
";
    }
}
