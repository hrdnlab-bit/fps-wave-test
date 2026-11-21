using FishNet.Managing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HrdnLab.FpsWave.Editor
{
    /// <summary>
    /// Rende affidabile il Play Mode multiplayer in Editor:
    /// <list type="bullet">
    /// <item>il Play parte sempre dalla scena Boot (anche nei giocatori virtuali di Multiplayer Play Mode,
    /// che altrimenti entrano in Play con l'ultima scena aperta, ad esempio Greybox, senza NetworkManager);</item>
    /// <item>all'uscita dal Play chiude esplicitamente client e server: con il domain reload disattivato
    /// (Enter Play Mode Options) il socket del server Tugboat sopravvive e la porta resta occupata;</item>
    /// <item>durante il Play blocca la ricompilazione degli script, che si applica all'uscita.</item>
    /// </list>
    /// Gira in ogni istanza dell'Editor perché i giocatori virtuali condividono la cartella Assets.
    /// </summary>
    [InitializeOnLoad]
    internal static class PlayModeNetworkGuard
    {
        private const string MenuPath = "FpsWave/Play Mode/Start From Boot";
        private const string PrefKey = "FpsWave.PlayFromBoot";

        private static bool _reloadLocked;

        static PlayModeNetworkGuard()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            // delayCall: AssetDatabase non è ancora pronto durante il costruttore statico all'avvio dell'Editor.
            EditorApplication.delayCall += ApplyStartScene;
        }

        private static bool PlayFromBoot
        {
            get => EditorPrefs.GetBool(PrefKey, true);
            set => EditorPrefs.SetBool(PrefKey, value);
        }

        [MenuItem(MenuPath, priority = 0)]
        private static void TogglePlayFromBoot()
        {
            PlayFromBoot = !PlayFromBoot;
            ApplyStartScene();
        }

        [MenuItem(MenuPath, true)]
        private static bool TogglePlayFromBootValidate()
        {
            Menu.SetChecked(MenuPath, PlayFromBoot);
            return true;
        }

        private static void ApplyStartScene()
        {
            EditorSceneManager.playModeStartScene = PlayFromBoot
                ? AssetDatabase.LoadAssetAtPath<SceneAsset>(ProjectSetupTools.BootScenePath)
                : null;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            switch (change)
            {
                case PlayModeStateChange.ExitingEditMode:
                    ApplyStartScene();
                    break;
                case PlayModeStateChange.EnteredPlayMode:
                    // Una ricompilazione a partita in corso azzera lo stato non serializzato (armi, rete) e
                    // genera eccezioni a ogni frame: gli script modificati si ricaricano all'uscita dal Play.
                    EditorApplication.LockReloadAssemblies();
                    _reloadLocked = true;
                    break;
                case PlayModeStateChange.ExitingPlayMode:
                    StopAllNetworkManagers();
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    if (_reloadLocked)
                    {
                        _reloadLocked = false;
                        EditorApplication.UnlockReloadAssemblies();
                    }
                    break;
            }
        }

        private static void StopAllNetworkManagers()
        {
            var managers = Object.FindObjectsByType<NetworkManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var manager in managers)
            {
                if (manager == null)
                    continue;
                if (manager.ClientManager != null && manager.ClientManager.Started)
                    manager.ClientManager.StopConnection();
                if (manager.ServerManager != null && manager.ServerManager.Started)
                    manager.ServerManager.StopConnection(true);
            }
        }
    }
}
