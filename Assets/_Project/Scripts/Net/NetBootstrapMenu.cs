using FishNet.Managing;
using FishNet.Transporting;
using HrdnLab.FpsWave.Core;
using UnityEngine;

namespace HrdnLab.FpsWave.Net
{
    /// <summary>
    /// Menu di sviluppo (M0a/M0b): Host e Join in rete locale (Tugboat) o via Steam (lobby per amici, inviti,
    /// relay Steam). In Editor l'istanza principale fa da Host LAN e i giocatori virtuali di Multiplayer Play Mode
    /// si collegano da soli. Usa OnGUI perché è uno strumento di debug: sarà sostituito dal menu principale in UI Toolkit.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetBootstrapMenu : MonoBehaviour
    {
        [SerializeField] private NetworkManager _networkManager;
        [SerializeField] private SteamService _steam;
        [SerializeField] private string _address = "localhost";
        [SerializeField] private ushort _port = 7770;

        [Tooltip("Solo nell'Editor: l'istanza principale avvia l'Host LAN e i giocatori virtuali di Multiplayer Play Mode si collegano da soli.")]
        [SerializeField] private bool _autoStartInEditor = true;
        [SerializeField, Min(1)] private int _autoJoinAttempts = 10;
        [Tooltip("Pannello OnGUI di sviluppo. Spento quando c'è il menu di gioco (GameMenu), che usa le stesse funzioni.")]
        [SerializeField] private bool _drawDebugGui = true;

        private static readonly WaitForSecondsRealtime RetryDelay = new(2f);

        private LocalConnectionState _serverState = LocalConnectionState.Stopped;
        private LocalConnectionState _clientState = LocalConnectionState.Stopped;
        private GUIStyle _labelStyle;
        private GUIStyle _wrapStyle;
        private string _lastError;
        private bool _hostStarting;
        private NetMode _mode = NetMode.Lan;
        private string _lobbyCode = string.Empty;
        private readonly System.Collections.Generic.List<SteamService.FriendEntry> _friends = new();
        private float _nextFriendsRefresh;
        private Vector2 _friendsScroll;

        private void Awake()
        {
            if (_networkManager == null)
                _networkManager = GetComponent<NetworkManager>();
            if (_networkManager == null)
                _networkManager = FindFirstObjectByType<NetworkManager>();
            if (_steam == null)
                _steam = GetComponent<SteamService>();
        }

        // Start e non OnEnable: i sotto-manager di FishNet vengono creati nell'Awake del NetworkManager,
        // il cui ordine rispetto a questo componente non è garantito.
        private void Start()
        {
            if (_networkManager == null || !OwnsManager)
                return;
            _networkManager.ServerManager.OnServerConnectionState += HandleServerState;
            _networkManager.ClientManager.OnClientConnectionState += HandleClientState;
            if (_steam != null)
            {
                _steam.LobbyHosted += HandleLobbyHosted;
                _steam.JoinHostRequested += HandleJoinSteamHost;
                _steam.Error += HandleSteamError;
            }

#if UNITY_EDITOR
            if (_autoStartInEditor)
            {
                if (IsVirtualPlayer())
                    StartCoroutine(AutoJoin());
                else
                    StartHostLan();
            }
#endif
        }

        /// <summary>
        /// False per la copia di Boot che FishNet distrugge al ritorno al menu (il NetworkManager persistente è un altro).
        /// </summary>
        private bool OwnsManager => _networkManager != null && _networkManager.gameObject == gameObject;

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
                    JoinLan();
            }
        }

        // Con il domain reload disattivato (Enter Play Mode Options) un server non fermato tiene occupata
        // la porta anche dopo l'uscita dal Play: si chiude sempre tutto esplicitamente.
        private void OnApplicationQuit() => StopAll();

        private void OnDestroy()
        {
            if (_networkManager == null || _networkManager.ServerManager == null || _networkManager.ClientManager == null)
                return;
            if (!OwnsManager)
                return;
            StopAll();
            _networkManager.ServerManager.OnServerConnectionState -= HandleServerState;
            _networkManager.ClientManager.OnClientConnectionState -= HandleClientState;
            if (_steam != null)
            {
                _steam.LobbyHosted -= HandleLobbyHosted;
                _steam.JoinHostRequested -= HandleJoinSteamHost;
                _steam.Error -= HandleSteamError;
            }
        }

        // ---------------------------------------------------------------- API per il menu di gioco (GameMenu)

        public SteamService Steam => _steam;
        public NetMode Mode => _mode;
        public string LastError => _lastError;
        public string Address { get => _address; set => _address = string.IsNullOrWhiteSpace(value) ? "localhost" : value.Trim(); }
        public bool IsServer => _networkManager != null && _networkManager.ServerManager.Started;
        public bool IsClientConnected => _clientState == LocalConnectionState.Started;
        public bool IsIdle => _serverState == LocalConnectionState.Stopped && _clientState == LocalConnectionState.Stopped && !_hostStarting;
        public bool IsConnecting => !IsIdle && !IsClientConnected;
        public int ConnectedPlayers => IsServer ? _networkManager.ServerManager.Clients.Count : 0;
        public bool IsSteamHost => _mode == NetMode.Steam && _steam != null && _steam.InLobby && IsServer;
        public bool DrawDebugGui { get => _drawDebugGui; set => _drawDebugGui = value; }

        public void ClearError() => _lastError = null;

        /// <summary>Entra nella lobby Steam di un amico con il codice che ti ha mandato.</summary>
        public void JoinSteamByCode(string code)
        {
            _lastError = null;
            if (_steam == null || !SteamService.IsReady)
            {
                _lastError = $"Steam non disponibile. {SteamService.Status}";
                return;
            }
            if (!_steam.JoinLobbyByCode(code))
                _lastError ??= "Codice lobby non valido.";
        }

        /// <summary>Si collega a un Host in rete locale all'indirizzo indicato.</summary>
        public void JoinLan(string address)
        {
            Address = address;
            JoinLan();
        }

        // ---------------------------------------------------------------- Stato

        private void HandleServerState(ServerConnectionStateArgs args)
        {
            _serverState = _networkManager.ServerManager.Started ? LocalConnectionState.Started : args.ConnectionState;
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
            if (args.ConnectionState == LocalConnectionState.Stopped && !_networkManager.ServerManager.Started)
                _steam?.LeaveLobby();
        }

        private void HandleClientState(ClientConnectionStateArgs args)
        {
            _clientState = args.ConnectionState;
            if (args.ConnectionState == LocalConnectionState.Stopped && !_networkManager.ServerManager.Started)
                _steam?.LeaveLobby();
        }

        private void ReportHostFailure()
        {
            _hostStarting = false;
            _lastError = _mode == NetMode.Lan
                ? $"Impossibile fare da Host: porta {_port} occupata (un'altra istanza o un Play precedente). " +
                  "Chiudi le altre finestre di gioco o i giocatori virtuali e riprova."
                : $"Impossibile avviare il server Steam. {SteamService.Status}";
            Debug.LogWarning($"[FpsWave] {_lastError}");
        }

        // ---------------------------------------------------------------- LAN

        /// <summary>Host in rete locale: server Tugboat + client locale.</summary>
        public void StartHostLan()
        {
            _lastError = null;
            _mode = NetMode.Lan;
            _hostStarting = true;
            if (!NetTransports.StartServer(_networkManager, NetMode.Lan, _port))
            {
                ReportHostFailure();
                return;
            }
            NetTransports.StartClient(_networkManager, NetMode.Lan, "localhost", _port);
        }

        /// <summary>Si collega a un Host in rete locale.</summary>
        public void JoinLan()
        {
            _lastError = null;
            _mode = NetMode.Lan;
            NetTransports.StartClient(_networkManager, NetMode.Lan, _address, _port);
        }

        // ---------------------------------------------------------------- Steam

        /// <summary>Host via Steam: crea la lobby per amici; il server parte quando la lobby è pronta.</summary>
        public void StartHostSteam()
        {
            _lastError = null;
            if (_steam == null || !SteamService.IsReady)
            {
                _lastError = $"Steam non disponibile. {SteamService.Status}";
                return;
            }
            _mode = NetMode.Steam;
            _steam.CreateLobby();
        }

        private void HandleLobbyHosted()
        {
            _mode = NetMode.Steam;
            _hostStarting = true;
            if (!NetTransports.StartServer(_networkManager, NetMode.Steam, _port))
            {
                _steam.LeaveLobby();
                ReportHostFailure();
                return;
            }
            // Client locale dell'Host: FishySteamworks usa una connessione interna, l'indirizzo non conta.
            NetTransports.StartClient(_networkManager, NetMode.Steam, "localhost", _port);
        }

        private void HandleJoinSteamHost(ulong hostSteamId)
        {
            if (_networkManager.ServerManager.Started || _networkManager.ClientManager.Started)
                NetTransports.StopAll(_networkManager);
            _lastError = null;
            _mode = NetMode.Steam;
            if (!NetTransports.StartClient(_networkManager, NetMode.Steam, hostSteamId.ToString(), _port))
                _lastError = "Connessione all'Host Steam non riuscita.";
        }

        private void HandleSteamError(string message)
        {
            _lastError = message;
            Debug.LogWarning($"[FpsWave][Steam] {message}");
        }

        /// <summary>Chiude client e server su tutti i transport ed esce dalla lobby Steam.</summary>
        public void StopAll()
        {
            NetTransports.StopAll(_networkManager);
            _steam?.LeaveLobby();
        }

        // ---------------------------------------------------------------- UI di sviluppo

        private void OnGUI()
        {
            if (_networkManager == null || !OwnsManager || !_drawDebugGui)
                return;

            _labelStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 16 };
            _wrapStyle ??= new GUIStyle(GUI.skin.label) { wordWrap = true };

            bool idle = _serverState == LocalConnectionState.Stopped && _clientState == LocalConnectionState.Stopped;
            bool steamHost = !idle && _mode == NetMode.Steam && _steam != null && _steam.InLobby && _networkManager.ServerManager.Started;
            float height = (idle ? 400 : steamHost ? 420 : 190) + (_lastError != null ? 70 : 0);
            GUILayout.BeginArea(new Rect(20, 20, 360, height), GUI.skin.box);
            GUILayout.Label($"FPS Wave Test {GameInfo.Version} (proto {GameInfo.ProtocolVersion})", _labelStyle);
            GUILayout.Label(SteamService.IsReady ? $"Steam: {_steam?.LocalName}" : $"Steam: no ({SteamService.Status})", _wrapStyle);
            if (_lastError != null)
                GUILayout.Label(_lastError, _wrapStyle);

            if (idle)
            {
                GUILayout.Label("Steam (amici via Internet)");
                GUI.enabled = SteamService.IsReady;
                if (GUILayout.Button("Host Steam (crea lobby)", GUILayout.Height(34)))
                    StartHostSteam();
                GUI.enabled = true;
                GUILayout.BeginHorizontal();
                GUILayout.Label("Codice", GUILayout.Width(50));
                _lobbyCode = GUILayout.TextField(_lobbyCode);
                GUILayout.EndHorizontal();
                if (GUILayout.Button("Join Steam (codice lobby)", GUILayout.Height(30)) && _steam != null)
                {
                    _lastError = null;
                    _steam.JoinLobbyByCode(_lobbyCode);
                }
                GUI.enabled = true;
                GUILayout.Label("Oppure accetta l'invito Steam (con il gioco già aperto).", _wrapStyle);

                GUILayout.Space(6);
                GUILayout.Label("Rete locale (stesso PC / LAN)");
                if (GUILayout.Button("Host LAN", GUILayout.Height(30)))
                    StartHostLan();
                GUILayout.BeginHorizontal();
                GUILayout.Label("Indirizzo", GUILayout.Width(70));
                _address = GUILayout.TextField(_address);
                GUILayout.EndHorizontal();
                if (GUILayout.Button("Join LAN", GUILayout.Height(30)))
                    JoinLan();
            }
            else
            {
                GUILayout.Label($"Modalità: {(_mode == NetMode.Steam ? "Steam" : "LAN")}", _labelStyle);
                GUILayout.Label($"Server: {_serverState}   Client: {_clientState}");
                if (_networkManager.ServerManager.Started)
                    GUILayout.Label($"Giocatori connessi: {_networkManager.ServerManager.Clients.Count}");
                if (_mode == NetMode.Steam && _steam != null && _steam.InLobby && _networkManager.ServerManager.Started)
                    DrawSteamHostPanel();
                if (GUILayout.Button("Disconnetti", GUILayout.Height(30)))
                    StopAll();
            }
            GUILayout.EndArea();
        }

        /// <summary>Codice lobby da copiare e inviti diretti agli amici online: non serve l'overlay di Steam.</summary>
        private void DrawSteamHostPanel()
        {
            GUILayout.Label($"Codice lobby: {_steam.LobbyCode}");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Copia codice", GUILayout.Height(26)))
                GUIUtility.systemCopyBuffer = _steam.LobbyCode;
            if (GUILayout.Button("Overlay Steam", GUILayout.Height(26)))
                _steam.OpenInviteDialog();
            GUILayout.EndHorizontal();

            if (Time.unscaledTime >= _nextFriendsRefresh)
            {
                _nextFriendsRefresh = Time.unscaledTime + 3f;
                _steam.GetOnlineFriends(_friends);
            }
            GUILayout.Label(_friends.Count == 0 ? "Nessun amico online." : "Amici online (* = sta giocando):");
            _friendsScroll = GUILayout.BeginScrollView(_friendsScroll, GUILayout.Height(150));
            foreach (SteamService.FriendEntry friend in _friends)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label((friend.InGame ? "* " : "") + friend.Name);
                if (GUILayout.Button("Invita", GUILayout.Width(70)))
                {
                    _steam.InviteFriend(friend.Id);
                    _lastError = $"Invito inviato a {friend.Name}: deve accettarlo dalla chat di Steam con il gioco aperto, " +
                                 "oppure usare il codice lobby.";
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
        }

#if UNITY_EDITOR
        public void EditorSetSteam(SteamService steam) => _steam = steam;
#endif
    }
}
