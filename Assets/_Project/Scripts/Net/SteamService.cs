using System;
using System.Collections.Generic;
using HrdnLab.FpsWave.Core;
using Steamworks;
using UnityEngine;

namespace HrdnLab.FpsWave.Net
{
    /// <summary>
    /// Steam per il co-op (docs/NETCODE.md): inizializza Steamworks, crea una lobby per amici con i dati di
    /// sessione (SteamID dell'Host e versione di protocollo), gestisce inviti e ingresso da invito o dalla lista
    /// amici. La connessione di gioco vera e propria passa da FishySteamworks (relay Steam) e la avvia
    /// <see cref="NetBootstrapMenu"/> tramite gli eventi qui sotto.
    /// Una sola istanza possiede Steam: le copie duplicate del NetworkManager (ritorno a Boot) non la toccano.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SteamService : MonoBehaviour
    {
        public const string HostKey = "fpswave_host";
        public const string ProtocolKey = "fpswave_proto";
        public const string VersionKey = "fpswave_ver";

        [SerializeField, Range(2, 4)] private int _maxPlayers = 4;

        private static SteamService _owner;

        private Callback<LobbyCreated_t> _lobbyCreated;
        private Callback<LobbyEnter_t> _lobbyEntered;
        private Callback<GameLobbyJoinRequested_t> _joinRequested;
        private CSteamID _lobby = CSteamID.Nil;
        private bool _creatingAsHost;

        /// <summary>Steam inizializzato e utilizzabile in questa istanza.</summary>
        public static bool IsReady { get; private set; }

        /// <summary>Motivo per cui Steam non è disponibile (vuoto se pronto).</summary>
        public static string Status { get; private set; } = "Steam non inizializzato";

        public static SteamService Instance => _owner;

        public bool InLobby => _lobby != CSteamID.Nil;

        /// <summary>Codice della lobby da condividere (l'amico lo incolla in "Join Steam"); vuoto se non in lobby.</summary>
        public string LobbyCode => InLobby ? _lobby.m_SteamID.ToString() : string.Empty;

        /// <summary>Amico Steam per l'invito dal gioco (senza overlay).</summary>
        public readonly struct FriendEntry
        {
            public readonly ulong Id;
            public readonly string Name;
            public readonly bool InGame;
            public FriendEntry(ulong id, string name, bool inGame) { Id = id; Name = name; InGame = inGame; }
        }
        public string LocalName => IsReady ? SteamFriends.GetPersonaName() : string.Empty;

        /// <summary>Lobby creata: l'Host può avviare il server Steam e invitare gli amici.</summary>
        public event Action LobbyHosted;

        /// <summary>Entrato nella lobby di un altro giocatore compatibile: avviare il client verso questo SteamID.</summary>
        public event Action<ulong> JoinHostRequested;

        /// <summary>Errore da mostrare (es. versione diversa, lobby piena).</summary>
        public event Action<string> Error;

        private void Awake()
        {
            if (_owner != null && _owner != this)
                return; // copia duplicata: Steam appartiene già all'altra istanza
            _owner = this;
            Initialize();
        }

        private void Initialize()
        {
            if (IsReady)
                return;
#if UNITY_EDITOR
            if (EditorInstanceInfo.IsVirtualPlayerPath(Application.dataPath, System.IO.Directory.GetCurrentDirectory()))
            {
                Status = "Steam disattivato nei giocatori virtuali (si usa la rete locale)";
                return;
            }
#endif
            try
            {
                if (!Packsize.Test() || !DllCheck.Test())
                {
                    Status = "Steamworks.NET: libreria non compatibile con questa piattaforma";
                    return;
                }
                ESteamAPIInitResult result = SteamAPI.InitEx(out string message);
                if (result != ESteamAPIInitResult.k_ESteamAPIInitResult_OK)
                {
                    Status = $"Steam non disponibile ({result}): {message}. Steam è aperto e con l'account collegato?";
                    return;
                }
            }
            catch (DllNotFoundException e)
            {
                Status = $"steam_api64.dll mancante: {e.Message}";
                return;
            }

            IsReady = true;
            Status = string.Empty;
            _lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
            _lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
            _joinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequested);
            Debug.Log($"[FpsWave][Steam] Pronto: {SteamFriends.GetPersonaName()} ({SteamUser.GetSteamID()}), App ID {SteamUtils.GetAppID()}.");
        }

        private void Start()
        {
            if (_owner != this || !IsReady)
                return;
            // Avvio del gioco da invito quando non era già aperto: "+connect_lobby <id>".
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "+connect_lobby" && ulong.TryParse(args[i + 1], out ulong lobbyId))
                {
                    SteamMatchmaking.JoinLobby(new CSteamID(lobbyId));
                    break;
                }
            }
        }

        private void Update()
        {
            if (_owner == this && IsReady)
                SteamAPI.RunCallbacks();
        }

        private void OnDestroy()
        {
            if (_owner != this)
                return;
            LeaveLobby();
            _lobbyCreated?.Dispose();
            _lobbyEntered?.Dispose();
            _joinRequested?.Dispose();
            if (IsReady)
                SteamAPI.Shutdown();
            IsReady = false;
            Status = "Steam non inizializzato";
            _owner = null;
        }

        // ---------------------------------------------------------------- API

        /// <summary>Crea la lobby per amici (inviti e "Unisciti alla partita" dalla lista amici).</summary>
        public void CreateLobby()
        {
            if (!IsReady)
            {
                Error?.Invoke(Status);
                return;
            }
            LeaveLobby();
            _creatingAsHost = true;
            SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, _maxPlayers);
        }

        /// <summary>Entra nella lobby indicata dal codice (ID numerico mostrato all'Host). False se il codice non è valido.</summary>
        public bool JoinLobbyByCode(string code)
        {
            if (!IsReady)
            {
                Error?.Invoke(Status);
                return false;
            }
            if (!ulong.TryParse(code?.Trim(), out ulong id) || id == 0)
            {
                Error?.Invoke("Codice lobby non valido: deve essere il numero mostrato all'Host.");
                return false;
            }
            LeaveLobby();
            SteamMatchmaking.JoinLobby(new CSteamID(id));
            return true;
        }

        /// <summary>
        /// Riempie <paramref name="result"/> con gli amici online (prima chi sta già giocando), per invitarli dal gioco.
        /// Da chiamare a intervalli, non a ogni frame.
        /// </summary>
        public void GetOnlineFriends(List<FriendEntry> result)
        {
            result.Clear();
            if (!IsReady)
                return;
            AppId_t app = SteamUtils.GetAppID();
            int count = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);
            for (int i = 0; i < count; i++)
            {
                CSteamID id = SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagImmediate);
                if (SteamFriends.GetFriendPersonaState(id) == EPersonaState.k_EPersonaStateOffline)
                    continue;
                bool inGame = SteamFriends.GetFriendGamePlayed(id, out FriendGameInfo_t game) && game.m_gameID.AppID() == app;
                result.Add(new FriendEntry(id.m_SteamID, SteamFriends.GetFriendPersonaName(id), inGame));
            }
            result.Sort((a, b) => a.InGame != b.InGame ? (a.InGame ? -1 : 1) : string.CompareOrdinal(a.Name, b.Name));
        }

        /// <summary>Invia l'invito alla lobby a un amico (arriva nella chat di Steam).</summary>
        public void InviteFriend(ulong friendId)
        {
            if (IsReady && InLobby)
                SteamMatchmaking.InviteUserToLobby(_lobby, new CSteamID(friendId));
        }

        /// <summary>Apre l'overlay di Steam per invitare amici nella lobby (se l'overlay è disponibile).</summary>
        public void OpenInviteDialog()
        {
            if (IsReady && InLobby)
                SteamFriends.ActivateGameOverlayInviteDialog(_lobby);
        }

        /// <summary>Esce dalla lobby (fine partita o disconnessione).</summary>
        public void LeaveLobby()
        {
            if (!IsReady || _lobby == CSteamID.Nil)
                return;
            SteamMatchmaking.LeaveLobby(_lobby);
            SteamFriends.ClearRichPresence();
            _lobby = CSteamID.Nil;
            _creatingAsHost = false;
        }

        // ---------------------------------------------------------------- Callback

        private void OnLobbyCreated(LobbyCreated_t data)
        {
            if (data.m_eResult != EResult.k_EResultOK)
            {
                _creatingAsHost = false;
                Error?.Invoke($"Creazione lobby fallita: {data.m_eResult}");
                return;
            }
            _lobby = new CSteamID(data.m_ulSteamIDLobby);
            SteamMatchmaking.SetLobbyData(_lobby, HostKey, SteamUser.GetSteamID().m_SteamID.ToString());
            SteamMatchmaking.SetLobbyData(_lobby, ProtocolKey, GameInfo.ProtocolVersion.ToString());
            SteamMatchmaking.SetLobbyData(_lobby, VersionKey, GameInfo.Version);
            SteamFriends.SetRichPresence("connect", $"+connect_lobby {_lobby.m_SteamID}");
            Debug.Log($"[FpsWave][Steam] Lobby creata: {_lobby.m_SteamID}.");
            LobbyHosted?.Invoke();
        }

        private void OnJoinRequested(GameLobbyJoinRequested_t data)
        {
            // Invito accettato o "Unisciti alla partita" mentre il gioco è già aperto.
            LeaveLobby();
            SteamMatchmaking.JoinLobby(data.m_steamIDLobby);
        }

        private void OnLobbyEntered(LobbyEnter_t data)
        {
            var lobby = new CSteamID(data.m_ulSteamIDLobby);
            if (_creatingAsHost)
                return; // l'Host entra nella propria lobby subito dopo averla creata

            if (data.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
            {
                Error?.Invoke($"Impossibile entrare nella lobby ({(EChatRoomEnterResponse)data.m_EChatRoomEnterResponse}).");
                return;
            }

            _lobby = lobby;
            string proto = SteamMatchmaking.GetLobbyData(lobby, ProtocolKey);
            string version = SteamMatchmaking.GetLobbyData(lobby, VersionKey);
            if (!LobbyCompatibility.IsCompatible(proto, GameInfo.ProtocolVersion))
            {
                LeaveLobby();
                Error?.Invoke($"Versione diversa: l'Host ha la {version} (protocollo {proto}), tu la {GameInfo.Version} " +
                              $"(protocollo {GameInfo.ProtocolVersion}). Aggiornate entrambi alla stessa build.");
                return;
            }
            if (!ulong.TryParse(SteamMatchmaking.GetLobbyData(lobby, HostKey), out ulong host) || host == 0)
            {
                LeaveLobby();
                Error?.Invoke("Lobby senza Host valido.");
                return;
            }
            Debug.Log($"[FpsWave][Steam] Entrato nella lobby {lobby.m_SteamID}, Host {host}.");
            JoinHostRequested?.Invoke(host);
        }
    }

    /// <summary>Controllo di compatibilità della lobby, separato per i test EditMode.</summary>
    public static class LobbyCompatibility
    {
        public static bool IsCompatible(string lobbyProtocol, int localProtocol) =>
            int.TryParse(lobbyProtocol, out int remote) && remote == localProtocol;
    }
}
