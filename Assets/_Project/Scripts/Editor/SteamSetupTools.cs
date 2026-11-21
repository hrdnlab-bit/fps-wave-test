using FishNet.Managing;
using FishNet.Managing.Transporting;
using FishNet.Transporting;
using FishNet.Transporting.Multipass;
using FishNet.Transporting.Tugboat;
using HrdnLab.FpsWave.Net;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HrdnLab.FpsWave.Editor
{
    /// <summary>
    /// Configura la scena Boot per Steam (M0b): Multipass con Tugboat (LAN, indice 0) e FishySteamworks
    /// (relay Steam peer to peer, indice 1), SteamService e steam_appid.txt (Spacewar 480, solo sviluppo).
    /// Ripetibile.
    /// </summary>
    public static class SteamSetupTools
    {
        public const uint DevAppId = 480; // Spacewar: solo sviluppo, per il rilascio serve un App ID proprio
        public const int MaxPlayers = 4;

        [MenuItem("FpsWave/Setup/5. Configure Steam (Multipass)")]
        public static void ConfigureSteam()
        {
            WriteSteamAppId(ProjectRoot);

            Scene scene = EditorSceneManager.OpenScene(ProjectSetupTools.BootScenePath, OpenSceneMode.Single);
            NetworkManager manager = Object.FindFirstObjectByType<NetworkManager>();
            if (manager == null)
            {
                Debug.LogError("[FpsWave] NetworkManager non trovato in Boot: esegui prima FpsWave/Setup/1.");
                return;
            }
            GameObject go = manager.gameObject;

            Tugboat tugboat = GetOrAdd<Tugboat>(go);
            var tugSo = new SerializedObject(tugboat);
            SetInt(tugSo, "_maximumClients", MaxPlayers);
            tugSo.ApplyModifiedPropertiesWithoutUndo();

            var steam = GetOrAdd<FishySteamworks.FishySteamworks>(go);
            var steamSo = new SerializedObject(steam);
            SetBool(steamSo, "_peerToPeer", true);
            SetInt(steamSo, "_maximumClients", MaxPlayers);
            steamSo.ApplyModifiedPropertiesWithoutUndo();

            Multipass multipass = GetOrAdd<Multipass>(go);
            var mpSo = new SerializedObject(multipass);
            SerializedProperty list = mpSo.FindProperty("_transports");
            list.arraySize = 2;
            list.GetArrayElementAtIndex(0).objectReferenceValue = tugboat;
            list.GetArrayElementAtIndex(1).objectReferenceValue = steam;
            SetBool(mpSo, "GlobalServerActions", false);
            mpSo.ApplyModifiedPropertiesWithoutUndo();

            // Senza TransportManager esplicito FishNet ne aggiunge uno a runtime con il primo transport trovato.
            TransportManager transportManager = GetOrAdd<TransportManager>(go);
            var tmSo = new SerializedObject(transportManager);
            tmSo.FindProperty("Transport").objectReferenceValue = multipass;
            tmSo.ApplyModifiedPropertiesWithoutUndo();

            SteamService service = GetOrAdd<SteamService>(go);
            NetBootstrapMenu menu = go.GetComponent<NetBootstrapMenu>();
            if (menu != null)
            {
                menu.EditorSetSteam(service);
                EditorUtility.SetDirty(menu);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[FpsWave] Steam configurato in Boot: Multipass [Tugboat, FishySteamworks P2P], SteamService, steam_appid.txt (480).");
        }

        public static string ProjectRoot => System.IO.Directory.GetParent(Application.dataPath).FullName;

        /// <summary>steam_appid.txt fa partire Steamworks senza lanciare il gioco da Steam (Editor e build di test).</summary>
        public static void WriteSteamAppId(string folder)
        {
            System.IO.File.WriteAllText(System.IO.Path.Combine(folder, "steam_appid.txt"), DevAppId.ToString());
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            if (go.TryGetComponent(out T existing))
                return existing;
            return ObjectFactory.AddComponent<T>(go);
        }

        private static void SetBool(SerializedObject so, string name, bool value)
        {
            SerializedProperty p = so.FindProperty(name);
            if (p != null) p.boolValue = value;
            else Debug.LogWarning($"[FpsWave] Proprietà {name} non trovata su {so.targetObject.GetType().Name}.");
        }

        private static void SetInt(SerializedObject so, string name, int value)
        {
            SerializedProperty p = so.FindProperty(name);
            if (p != null) p.intValue = value;
            else Debug.LogWarning($"[FpsWave] Proprietà {name} non trovata su {so.targetObject.GetType().Name}.");
        }
    }
}
