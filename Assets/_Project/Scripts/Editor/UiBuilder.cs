using FishNet.Managing;
using HrdnLab.FpsWave.Combat;
using HrdnLab.FpsWave.Net;
using HrdnLab.FpsWave.Player;
using HrdnLab.FpsWave.UI;
using HrdnLab.FpsWave.Weapons;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace HrdnLab.FpsWave.Editor
{
    /// <summary>
    /// Collega l'interfaccia in UI Toolkit: due PanelSettings (HUD sotto, menu sopra) con il tema predefinito di Unity,
    /// il HUD sul Player (UIDocument + SciFiHud, attivi solo per il player locale tramite PlayerView) e il menu di gioco
    /// sul NetworkManager della scena Boot (UIDocument + GameMenu; il pannello OnGUI di sviluppo viene spento).
    /// Lo stile sta in Assets/_Project/UI/Styles (Hud.uss, Menu.uss). Richiamato anche da "3. Build M1 Content".
    /// </summary>
    public static class UiBuilder
    {
        private const string UiDir = "Assets/_Project/UI";
        private const string ThemePath = UiDir + "/Themes/FpsWaveRuntimeTheme.tss";
        private const string HudStylePath = UiDir + "/Styles/Hud.uss";
        private const string MenuStylePath = UiDir + "/Styles/Menu.uss";
        private const string HudPanelPath = UiDir + "/PanelSettings_Hud.asset";
        private const string MenuPanelPath = UiDir + "/PanelSettings_Menu.asset";
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player.prefab";

        [MenuItem("FpsWave/Setup/7. Build UI")]
        public static void BuildMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            Build();
            EditorSceneManager.OpenScene(ProjectSetupTools.BootScenePath);
        }

        public static void Build()
        {
            // Texture del HUD "armatura" (procedurali + ricavate dagli asset CC0 in ThirdParty), poi reimport degli stili
            // perché gli url() puntino ai file appena creati.
            UiTextureGenerator.GenerateAll();
            AssetDatabase.ImportAsset(HudStylePath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(MenuStylePath, ImportAssetOptions.ForceUpdate);

            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            var hudStyle = AssetDatabase.LoadAssetAtPath<StyleSheet>(HudStylePath);
            var menuStyle = AssetDatabase.LoadAssetAtPath<StyleSheet>(MenuStylePath);
            if (theme == null || hudStyle == null || menuStyle == null)
            {
                Debug.LogError($"[FpsWave] File dell'interfaccia mancanti in {UiDir} (tema .tss, Hud.uss, Menu.uss).");
                return;
            }

            PanelSettings hudPanel = EnsurePanel(HudPanelPath, theme, sortingOrder: 0);
            PanelSettings menuPanel = EnsurePanel(MenuPanelPath, theme, sortingOrder: 100);
            ConfigurePlayer(hudPanel, hudStyle);
            ConfigureBoot(menuPanel, menuStyle);
            AssetDatabase.SaveAssets();
            Debug.Log("[FpsWave] Interfaccia collegata: HUD sul Player, menu di gioco nella scena Boot.");
        }

        private static PanelSettings EnsurePanel(string path, ThemeStyleSheet theme, int sortingOrder)
        {
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panel, path);
            }
            panel.themeStyleSheet = theme;
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            panel.sortingOrder = sortingOrder;
            panel.clearColor = false;
            EditorUtility.SetDirty(panel);
            return panel;
        }

        private static void ConfigurePlayer(PanelSettings panel, StyleSheet style)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                // Via il vecchio HUD OnGUI (PlayerHud), anche se il suo script non esiste più.
                foreach (MonoBehaviour behaviour in root.GetComponents<MonoBehaviour>())
                    if (behaviour != null && behaviour.GetType().Name == "PlayerHud")
                        Object.DestroyImmediate(behaviour, true);
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root);

                if (!root.TryGetComponent(out UIDocument document))
                    document = root.AddComponent<UIDocument>();
                document.panelSettings = panel;
                document.sortingOrder = 0;
                document.enabled = false; // lo accende PlayerView solo per il player locale

                if (!root.TryGetComponent(out SciFiHud hud))
                    hud = root.AddComponent<SciFiHud>();
                hud.EditorSetup(root.GetComponent<PlayerWeapons>(), root.GetComponent<Health>(), style);
                hud.enabled = false;

                var numbersFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/ThirdParty/GoogleFonts/Rajdhani-Bold.ttf");
                if (root.TryGetComponent(out DamageNumbers numbers) && numbersFont != null)
                    numbers.EditorSetFont(numbersFont);

                if (root.TryGetComponent(out PlayerView view))
                {
                    var so = new SerializedObject(view);
                    SerializedProperty list = so.FindProperty("_ownerOnlyBehaviours");
                    list.arraySize = 2;
                    list.GetArrayElementAtIndex(0).objectReferenceValue = document;
                    list.GetArrayElementAtIndex(1).objectReferenceValue = hud;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigureBoot(PanelSettings panel, StyleSheet style)
        {
            Scene boot = EditorSceneManager.OpenScene(ProjectSetupTools.BootScenePath, OpenSceneMode.Single);
            NetworkManager manager = Object.FindAnyObjectByType<NetworkManager>();
            if (manager == null)
            {
                Debug.LogError("[FpsWave] NetworkManager non trovato nella scena Boot: eseguire prima FpsWave/Setup/1 e 5.");
                return;
            }
            GameObject go = manager.gameObject;
            NetBootstrapMenu net = go.GetComponent<NetBootstrapMenu>();

            if (!go.TryGetComponent(out UIDocument document))
                document = go.AddComponent<UIDocument>();
            document.panelSettings = panel;
            document.sortingOrder = 100;

            if (!go.TryGetComponent(out GameMenu menu))
                menu = go.AddComponent<GameMenu>();
            menu.EditorSetup(net, style);

            if (net != null)
            {
                var so = new SerializedObject(net);
                SerializedProperty debugGui = so.FindProperty("_drawDebugGui");
                if (debugGui != null)
                    debugGui.boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorUtility.SetDirty(go);
            EditorSceneManager.MarkSceneDirty(boot);
            EditorSceneManager.SaveScene(boot);
        }
    }
}
