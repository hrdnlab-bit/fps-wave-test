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
        private string _lastError;
        private bool _hostStarting;

        private void Awake()
        {
            if (_networkManager == null)
                _networkManager = GetComponent<NetworkManager>();
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
        /// (il progetto clone sta in Library/VP/; vedi <see cref="EditorInstanceInfo"/>).
        /// </summary>
        private static bool IsVirtualPlayer() =>
            Application.isEditor && EditorInstanceInfo.IsVirtualPlayerPath(
                Application.dataPath, System.IO.Directory.GetCurrentDirectory());

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

        // Con il domain reload disattivato (Enter Play Mode Options) un server non fermato tiene occupata
        // la porta anche dopo l'uscita dal Play: si chiude sempre tutto esplicitamente.
        private void OnApplicationQuit() => StopAll();

        private void OnDestroy()
        {
            if (_networkManager == null || _networkManager.ServerManager == null || _networkManager.ClientManager == null)
                return;
            // Solo se il NetworkManager è il nostro: una copia duplicata di Boot (ritorno al menu) viene
            // distrutta da FishNet e non deve chiudere la sessione del manager persistente.
            if (_networkManager.gameObject == gameObject)
                StopAll();
            _networkManager.ServerManager.OnServerConnectionState -= HandleServerState;
            _networkManager.ClientManager.OnClientConnectionState -= HandleClientState;
        }

        private void HandleServerState(ServerConnectionStateArgs args)
        {
            _serverState = args.ConnectionState;
            if (args.ConnectionState == LocalConnectionState.Started)
            {
                _hostStarting = false;
            }
            else if (args.ConnectionState == LocalConnectionState.Stopped && _hostStarting)
            {
                // Il bind fallito (porta occupata) arriva come Stopped asincrono, dopo l'avvio del client locale:
                // si ferma anche il client, altrimenti resta in attesa di un server che non esiste.
                if (_networkManager.ClientManager.Started)
                    _networkManager.ClientManager.StopConnection();
                ReportHostFailure();
            }
        }

        private void ReportHostFailure()
        {
            _hostStarting = false;
            _lastError = $"Impossibile fare da Host: porta {_port} occupata (un'altra istanza o un Play precedente). " +
                         "Chiudi le altre finestre di gioco o i giocatori virtuali e riprova.";
            Debug.LogWarning($"[FpsWave] {_lastError}");
        }

        private void HandleClientState(ClientConnectionStateArgs args) => _clientState = args.ConnectionState;

        /// <summary>Avvia server + client locale (listen server).</summary>
        public void StartHost()
        {
            _lastError = null;
            _networkManager.TransportManager.Transport.SetPort(_port);
            _hostStarting = true;
            if (!_networkManager.ServerManager.StartConnection())
            {
                ReportHostFailure();
                return;
            }
            _networkManager.ClientManager.StartConnection("localhost", _port);
        }

        /// <summary>Si connette a un Host esistente.</summary>
        public void StartClient()
        {
            _lastError = null;
            _networkManager.ClientManager.StartConnection(_address, _port);
        }

        /// <summary>Chiude client e server locali.</summary>
        public void StopAll()
        {
            if (_networkManager == null || _networkManager.ClientManager == null || _networkManager.ServerManager == null)
                return;
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

            bool idleArea = _serverState == LocalConnectionState.Stopped && _clientState == LocalConnectionState.Stopped;
            float height = (idleArea ? 200 : 150) + (_lastError != null ? 70 : 0);
            GUILayout.BeginArea(new Rect(20, 20, 320, height), GUI.skin.box);
            GUILayout.Label($"FPS Wave Test {HrdnLab.FpsWave.Core.GameInfo.Version} (proto {HrdnLab.FpsWave.Core.GameInfo.ProtocolVersion})", _labelStyle);
            if (_lastError != null)
                GUILayout.Label(_lastError);

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
