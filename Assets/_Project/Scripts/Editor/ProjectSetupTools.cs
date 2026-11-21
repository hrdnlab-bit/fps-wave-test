using System.Collections.Generic;
using FishNet.Managing;
using FishNet.Transporting.Tugboat;
using HrdnLab.FpsWave.Net;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HrdnLab.FpsWave.Editor
{
    /// <summary>
    /// Strumenti di setup del progetto, richiamabili dal menu "FpsWave/Setup".
    /// Permettono di (ri)generare scene e configurazioni in modo ripetibile invece che a mano.
    /// </summary>
    public static class ProjectSetupTools
    {
        public const string BootScenePath = "Assets/_Project/Scenes/Boot.unity";

        private static readonly string[] TemplateLeftovers =
        {
            "Assets/TutorialInfo",
            "Assets/Readme.asset",
            "Assets/Scenes/SampleScene.unity",
            "Assets/Scenes",
        };

        /// <summary>Rimuove i contenuti dimostrativi del template URP.</summary>
        [MenuItem("FpsWave/Setup/2. Remove Template Leftovers")]
        public static void RemoveTemplateLeftovers()
        {
            var removed = new List<string>();
            foreach (string path in TemplateLeftovers)
            {
                // Controllo per GUID e non caricando l'asset: un asset il cui script è già stato rimosso
                // (es. Readme.asset senza ReadmeEditor) non si carica ma va comunque eliminato.
                if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path, AssetPathToGUIDOptions.OnlyExistingAssets)))
                {
                    if (AssetDatabase.DeleteAsset(path))
                        removed.Add(path);
                }
            }
            AssetDatabase.Refresh();
            Debug.Log(removed.Count > 0
                ? $"[FpsWave] Rimossi: {string.Join(", ", removed)}"
                : "[FpsWave] Nessun residuo del template da rimuovere.");
        }

        /// <summary>
        /// Crea la scena Boot con NetworkManager + Tugboat e il menu di sviluppo,
        /// e la imposta come prima scena della build.
        /// </summary>
        [MenuItem("FpsWave/Setup/1. Create Boot Scene")]
        public static void CreateBootScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera e luce minime: la scena Boot non contiene gameplay.
            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.09f, 0.1f);
            cameraGo.AddComponent<AudioListener>();

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // NetworkManager: ObjectFactory.AddComponent esegue Reset(), che assegna i DefaultPrefabObjects di FishNet.
            var netGo = new GameObject("NetworkManager");
            var networkManager = ObjectFactory.AddComponent<NetworkManager>(netGo);
            ObjectFactory.AddComponent<Tugboat>(netGo);
            var menu = ObjectFactory.AddComponent<NetBootstrapMenu>(netGo);

            var so = new SerializedObject(menu);
            so.FindProperty("_networkManager").objectReferenceValue = networkManager;
            so.ApplyModifiedPropertiesWithoutUndo();

            System.IO.Directory.CreateDirectory("Assets/_Project/Scenes");
            EditorSceneManager.SaveScene(scene, BootScenePath);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(BootScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"[FpsWave] Scena Boot creata in {BootScenePath} e impostata come prima scena della build.");
        }
    }
}
