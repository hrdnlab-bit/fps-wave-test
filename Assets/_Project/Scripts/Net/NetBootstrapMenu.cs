using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;

namespace HrdnLab.FpsWave.Net
{
    /// <summary>
    /// Menu minimo di sviluppo (M0a): avvia Host, Client o Server dedicato con il transport locale (Tugboat)
    /// e mostra lo stato della connessione. Verrà sostituito dal menu principale con lobby Steam (M0b).
    /// Usa OnGUI solo perché è uno strumento di debug: non va usato per la UI di gioco.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetBootstrapMenu : MonoBehaviour
    {
        [SerializeField] private NetworkManager _networkManager;
        [SerializeField] private string _address = "localhost";
        [SerializeField] private ushort _port = 7770;

        [Tooltip("Solo nell'Editor: l'istanza principale avvia l'Host e i giocatori virtuali di Multiplayer Play Mode si collegano da soli.")]
        [SerializeField] private bool _autoStartInEditor = true;
        [SerializeField, Min(1)] private int _autoJoinAttempts = 10;

        private static readonly WaitForSecondsRealtime RetryDelay = new(2f);

        private LocalConnectionState _serverState = LocalConnectionState.Stopped;
        private LocalConnectionState _clientState = LocalConnectionState.Stopped;
        private GUIStyle _labelStyle;

        private void Awake()
        {
            if (_networkManager == null)
                _networkManager = FindFirstObjectByType<NetworkManager>();
        }

        // Start e non OnEnable: i sotto-manager di FishNet vengono creati nell'Awake del NetworkManager,
        // il cui ordine rispetto a questo componente non è garantito.
        private void Start()
        {
            if (_networkManager == null)
                return;
            _networkManager.ServerManager.OnServerConnectionState += HandleServerState;
            _networkManager.ClientManager.OnClientConnectionState += HandleClientState;

#if UNITY_EDITOR
            if (_autoStartInEditor)
            {
                if (IsVirtualPlayer())
                    StartCoroutine(AutoJoin());
                else
                    StartHost();
            }
#endif
        }

        /// <summary>
        /// True se questa istanza è un giocatore virtuale di Multiplayer Play Mode
        /// (stesso criterio usato da FishNet: il progetto clone sta in Library/VP/).
        /// </summary>
        private static bool IsVirtualPlayer() =>
            Application.isEditor && Application.dataPath.ToLowerInvariant().Contains("library/vp/");

        private System.Collections.IEnumerator AutoJoin()
        {
            for (int attempt = 0; attempt < _autoJoinAttempts; attempt++)
            {
                yield return RetryDelay;
                if (_clientState == LocalConnectionState.Started)
                    yield break;
                if (_clientState == LocalConnectionState.Stopped)
                    StartClient();
            }
        }

        private void OnDestroy()
        {
            if (_networkManager == null || _networkManager.ServerManager == null || _networkManager.ClientManager == null)
                return;
            _networkManager.ServerManager.OnServerConnectionState -= HandleServerState;
            _networkManager.ClientManager.OnClientConnectionState -= HandleClientState;
        }

        private void HandleServerState(ServerConnectionStateArgs args) => _serverState = args.ConnectionState;
        private void HandleClientState(ClientConnectionStateArgs args) => _clientState = args.ConnectionState;

        /// <summary>Avvia server + client locale (listen server).</summary>
        public void StartHost()
        {
            _networkManager.TransportManager.Transport.SetPort(_port);
            _networkManager.ServerManager.StartConnection();
            _networkManager.ClientManager.StartConnection("localhost", _port);
        }

        /// <summary>Si connette a un Host esistente.</summary>
        public void StartClient()
        {
            _networkManager.ClientManager.StartConnection(_address, _port);
        }

        /// <summary>Chiude client e server locali.</summary>
        public void StopAll()
        {
            if (_networkManager.ClientManager.Started)
                _networkManager.ClientManager.StopConnection();
            if (_networkManager.ServerManager.Started)
                _networkManager.ServerManager.StopConnection(true);
        }

        private void OnGUI()
        {
            if (_networkManager == null)
                return;

            _labelStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 16 };

            GUILayout.BeginArea(new Rect(20, 20, 320, 260), GUI.skin.box);
            GUILayout.Label($"FPS Wave Test {HrdnLab.FpsWave.Core.GameInfo.Version} (proto {HrdnLab.FpsWave.Core.GameInfo.ProtocolVersion})", _labelStyle);

            bool idle = _serverState == LocalConnectionState.Stopped && _clientState == LocalConnectionState.Stopped;
            if (idle)
            {
                if (GUILayout.Button("Host (server + client)", GUILayout.Height(36)))
                    StartHost();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Indirizzo", GUILayout.Width(70));
                _address = GUILayout.TextField(_address);
                GUILayout.EndHorizontal();

                if (GUILayout.Button("Join (client)", GUILayout.Height(36)))
                    StartClient();
            }
            else
            {
                GUILayout.Label($"Server: {_serverState}", _labelStyle);
                GUILayout.Label($"Client: {_clientState}", _labelStyle);
                if (_networkManager.ServerManager.Started)
                    GUILayout.Label($"Giocatori connessi: {_networkManager.ServerManager.Clients.Count}", _labelStyle);
                if (GUILayout.Button("Disconnetti", GUILayout.Height(32)))
                    StopAll();
            }
            GUILayout.EndArea();
        }
    }
}
