using FishNet.Managing;
using FishNet.Managing.Scened;
using FishNet.Transporting;
using UnityEngine;

namespace HrdnLab.FpsWave.Net
{
    /// <summary>
    /// Flusso di sessione: quando il server parte carica la scena di gioco per tutti (scena globale FishNet);
    /// quando la connessione si chiude si torna alla scena di menu.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameSession : MonoBehaviour
    {
        [SerializeField] private NetworkManager _networkManager;
        [SerializeField] private string _gameplayScene = "Greybox";
        [SerializeField] private string _menuScene = "Boot";

        private void Start()
        {
            if (_networkManager == null)
                _networkManager = GetComponent<NetworkManager>();
            _networkManager.ServerManager.OnServerConnectionState += HandleServerState;
            _networkManager.ClientManager.OnClientConnectionState += HandleClientState;
        }

        private void OnDestroy()
        {
            if (_networkManager == null || _networkManager.ServerManager == null)
                return;
            _networkManager.ServerManager.OnServerConnectionState -= HandleServerState;
            _networkManager.ClientManager.OnClientConnectionState -= HandleClientState;
        }

        private void HandleServerState(ServerConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started)
            {
                var load = new SceneLoadData(_gameplayScene) { ReplaceScenes = ReplaceOption.All };
                _networkManager.SceneManager.LoadGlobalScenes(load);
            }
            else if (args.ConnectionState == LocalConnectionState.Stopped)
            {
                ReturnToMenu();
            }
        }

        private void HandleClientState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Stopped && !_networkManager.ServerManager.Started)
                ReturnToMenu();
        }

        private void ReturnToMenu()
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == _menuScene)
                return;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            UnityEngine.SceneManagement.SceneManager.LoadScene(_menuScene);
        }

#if UNITY_EDITOR
        public void EditorSetup(NetworkManager networkManager, string gameplayScene, string menuScene)
        {
            _networkManager = networkManager;
            _gameplayScene = gameplayScene;
            _menuScene = menuScene;
        }
#endif
    }
}
