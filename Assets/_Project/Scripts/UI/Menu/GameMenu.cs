using System.Collections.Generic;
using HrdnLab.FpsWave.Core;
using HrdnLab.FpsWave.Net;
using HrdnLab.FpsWave.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace HrdnLab.FpsWave.UI
{
    /// <summary>
    /// Menu di gioco "terminale di comando" in UI Toolkit (stile in UI/Styles/Menu.uss).
    /// Vive sul NetworkManager persistente (scena Boot) e usa le funzioni di <see cref="NetBootstrapMenu"/>:
    /// - fuori partita: principale (Gioca / Impostazioni / Esci), schieramento (lobby Steam o LAN), connessione in corso;
    /// - in partita: Esc apre la pausa (Riprendi, Impostazioni, squadra con codice lobby e inviti, Abbandona, Esci);
    /// - impostazioni salvate e applicate subito (<see cref="GameSettings"/>).
    /// Con un menu aperto il player non riceve input (<see cref="UiState.MenuOpen"/>).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class GameMenu : MonoBehaviour
    {
        private enum Phase { FrontEnd, Connecting, InGame }
        private enum MenuScreen { None, Main, Play, Settings, Connecting, Pause }

        private const float LobbyJoinTimeout = 15f;

        [SerializeField] private NetBootstrapMenu _net;
        [SerializeField] private StyleSheet _styleSheet;

        private static GameMenu s_instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_instance = null;

        private UIDocument _document;
        private VisualElement _root;
        private readonly Dictionary<MenuScreen, VisualElement> _screens = new();
        private MenuScreen _screen = MenuScreen.None;
        private MenuScreen _settingsReturn = MenuScreen.Main;
        private Phase _phase = Phase.FrontEnd;
        private bool _paused;
        private float _pendingJoinUntil = -1f;

        // Schermata Gioca
        private Button _hostSteamButton;
        private Button _joinSteamButton;
        private TextField _lobbyCodeField;
        private TextField _addressField;

        // Connessione
        private VisualElement _spinner;
        private Label _connectingDetail;

        // Pausa
        private Label _pauseInfo;
        private VisualElement _squadSteam;
        private VisualElement _squadLan;
        private VisualElement _squadClient;
        private Label _lobbyCodeLabel;
        private ScrollView _friendsList;
        private readonly List<SteamService.FriendEntry> _friends = new();
        private float _nextFriendsRefresh;

        // Impostazioni
        private Slider _sensitivity, _fov, _volume;
        private Label _sensitivityValue, _fovValue, _volumeValue;
        private Toggle _fullscreen, _vsync, _showFps, _damageNumbers;
        private DropdownField _quality;

        // Barre e messaggi
        private Label _steamStatus;
        private VisualElement _steamDot;
        private VisualElement _toast;
        private Label _toastText;
        private string _shownError;
        private float _toastHideAt = -1f;

        // ------------------------------------------------------------------ Ciclo di vita

        private void Awake()
        {
            // La copia della scena Boot ricaricata al ritorno al menu viene distrutta da FishNet: resta la prima.
            if (s_instance != null && s_instance != this)
            {
                enabled = false;
                return;
            }
            s_instance = this;
            _document = GetComponent<UIDocument>();
            if (_net == null)
                _net = GetComponent<NetBootstrapMenu>();
        }

        private void OnEnable()
        {
            if (s_instance != this)
                return;
            UiState.MenuHandlesEscape = true;
            if (_net != null)
                _net.DrawDebugGui = false;
        }

        private void OnDisable()
        {
            if (s_instance != this)
                return;
            UiState.MenuHandlesEscape = false;
            UiState.MenuOpen = false;
        }

        private void OnDestroy()
        {
            if (s_instance == this)
                s_instance = null;
        }

        private void Update()
        {
            if (s_instance != this || _net == null)
                return;
            if (_root == null)
            {
                Build();
                if (_root == null)
                    return;
                ShowScreen(MenuScreen.Main);
            }

            float now = Time.unscaledTime;
            UpdatePhase(now);
            HandleEscape();

            if (_screen == MenuScreen.Connecting && _spinner != null)
                _spinner.style.rotate = new Rotate(now * 300f % 360f);
            if (_screen == MenuScreen.Pause)
                UpdatePauseInfo(now);
            UpdateStatusBar();
            UpdateToast(now);
        }

        // ------------------------------------------------------------------ Stato della sessione

        private void UpdatePhase(float now)
        {
            bool waitingLobby = _pendingJoinUntil > now && _net.IsIdle;
            if (_pendingJoinUntil > 0f && now >= _pendingJoinUntil && _net.IsIdle)
            {
                _pendingJoinUntil = -1f;
                ShowToast("No response from the lobby: check the code and make sure the host is in game.", true);
            }

            Phase phase = _net.IsClientConnected ? Phase.InGame
                : _net.IsConnecting || waitingLobby ? Phase.Connecting
                : Phase.FrontEnd;
            // Uscendo dalla partita si passa per uno stato "in chiusura" che non è una nuova connessione:
            // si resta in partita finché tutto è fermo, poi si torna al menu principale.
            if (_phase == Phase.InGame && phase == Phase.Connecting)
                return;
            if (phase == _phase)
                return;

            Phase previous = _phase;
            _phase = phase;
            switch (phase)
            {
                case Phase.Connecting:
                    _connectingDetail.text = waitingLobby ? "SEARCHING FOR STEAM LOBBY" : _net.Mode == NetMode.Steam ? "CONNECTING VIA STEAM" : "CONNECTING OVER LAN";
                    ShowScreen(MenuScreen.Connecting);
                    break;
                case Phase.InGame:
                    _pendingJoinUntil = -1f;
                    _paused = false;
                    ShowScreen(MenuScreen.None);
                    PlayerInputReader.LockCursor(true);
                    break;
                default:
                    _paused = false;
                    // Dalla partita si torna al menu principale; da una connessione annullata o fallita, a "Gioca".
                    ShowScreen(previous == Phase.InGame ? MenuScreen.Main
                        : previous == Phase.Connecting ? MenuScreen.Play
                        : _screen == MenuScreen.None ? MenuScreen.Main : _screen);
                    break;
            }
        }

        private void HandleEscape()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
                return;

            if (_phase == Phase.InGame)
            {
                if (_screen == MenuScreen.Settings)
                    ShowScreen(MenuScreen.Pause);
                else if (_paused)
                    Resume();
                else
                    Pause();
            }
            else if (_phase == Phase.FrontEnd && _screen != MenuScreen.Main)
            {
                ShowScreen(_screen == MenuScreen.Settings ? _settingsReturn : MenuScreen.Main);
            }
        }

        private void Pause()
        {
            _paused = true;
            ShowScreen(MenuScreen.Pause);
        }

        private void Resume()
        {
            _paused = false;
            ShowScreen(MenuScreen.None);
            PlayerInputReader.LockCursor(true);
        }

        private void ShowScreen(MenuScreen screen)
        {
            _screen = screen;
            foreach (KeyValuePair<MenuScreen, VisualElement> pair in _screens)
                pair.Value.style.display = pair.Key == screen ? DisplayStyle.Flex : DisplayStyle.None;

            bool visible = screen != MenuScreen.None;
            _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            _root.EnableInClassList("menu--overlay", _phase == Phase.InGame);
            UiState.MenuOpen = visible;
            if (visible)
            {
                UnityEngine.Cursor.lockState = CursorLockMode.None;
                UnityEngine.Cursor.visible = true;
            }

            if (screen == MenuScreen.Settings)
                RefreshSettings();
            if (screen == MenuScreen.Play)
            {
                _addressField.SetValueWithoutNotify(_net.Address);
                bool steamReady = SteamService.IsReady;
                _hostSteamButton.SetEnabled(steamReady);
                _joinSteamButton.SetEnabled(steamReady);
            }
            if (screen == MenuScreen.Pause)
                _nextFriendsRefresh = 0f;
        }

        // ------------------------------------------------------------------ Costruzione

        private void Build()
        {
            VisualElement docRoot = _document != null ? _document.rootVisualElement : null;
            if (docRoot == null)
                return;
            _root = new VisualElement { name = "menu" };
            _root.AddToClassList("menu");
            if (_styleSheet != null)
                _root.styleSheets.Add(_styleSheet);
            docRoot.Add(_root);

            var backdrop = El("menu-backdrop");
            backdrop.Add(El("menu-layer", "menu-bg-image"));
            backdrop.Add(El("menu-layer", "menu-hex"));
            backdrop.Add(El("menu-ring"));
            for (int i = 1; i < 12; i++)
            {
                var v = El("menu-grid-line");
                v.style.left = Length.Percent(i * 100f / 12f);
                v.style.top = 0; v.style.bottom = 0; v.style.width = 1;
                backdrop.Add(v);
            }
            for (int i = 1; i < 7; i++)
            {
                var h = El("menu-grid-line");
                h.style.top = Length.Percent(i * 100f / 7f);
                h.style.left = 0; h.style.right = 0; h.style.height = 1;
                backdrop.Add(h);
            }
            backdrop.Add(El("menu-layer", "menu-scan"));
            backdrop.Add(El("menu-layer", "menu-vignette"));
            _root.Add(backdrop);

            var frame = El("menu-frame");
            frame.Add(El("corner", "corner--tl"));
            frame.Add(El("corner", "corner--tr"));
            frame.Add(El("corner", "corner--bl"));
            frame.Add(El("corner", "corner--br"));
            _root.Add(frame);

            var topbar = El("menu-topbar");
            topbar.Add(Txt("FPS WAVE // COMMAND TERMINAL", "menu-topbar-text", "font-mono"));
            topbar.Add(El("menu-spacer"));
            topbar.Add(Txt($"BUILD {GameInfo.Version} · PROTOCOL {GameInfo.ProtocolVersion}", "menu-topbar-text", "font-mono"));
            _root.Add(topbar);

            AddScreen(MenuScreen.Main, BuildMain());
            AddScreen(MenuScreen.Play, BuildPlay());
            AddScreen(MenuScreen.Settings, BuildSettings());
            AddScreen(MenuScreen.Connecting, BuildConnecting());
            AddScreen(MenuScreen.Pause, BuildPause());

            var footer = El("menu-footer");
            _steamDot = El("status-dot");
            _steamStatus = Txt("STEAM", "menu-footer-text");
            footer.Add(_steamDot);
            footer.Add(_steamStatus);
            footer.Add(El("menu-spacer"));
            footer.Add(Txt("[ESC] BACK", "menu-footer-text"));
            _root.Add(footer);

            _toast = El("toast");
            _toastText = Txt(string.Empty, "toast-text");
            var close = new Button(() => { _net.ClearError(); HideToast(); }) { text = "✕" };
            close.AddToClassList("toast-close");
            _toast.Add(_toastText);
            _toast.Add(close);
            _toast.style.display = DisplayStyle.None;
            _root.Add(_toast);
        }

        private void AddScreen(MenuScreen id, VisualElement screen)
        {
            screen.style.display = DisplayStyle.None;
            _screens[id] = screen;
            _root.Add(screen);
        }

        private VisualElement BuildMain()
        {
            var screen = El("screen");
            var col = El("screen-col");
            col.Add(Txt("QUARANTINE PROTOCOL // SECTOR 7", "title-kicker"));
            col.Add(Txt("FPS WAVE", "title"));
            col.Add(Txt("WAVE-BASED CO-OP PVE · 1 TO 4 OPERATORS", "title-sub"));
            col.Add(MenuButton("01", "PLAY", () => ShowScreen(MenuScreen.Play), "menu-button--primary"));
            col.Add(MenuButton("02", "SETTINGS", () => OpenSettings(MenuScreen.Main)));
            col.Add(MenuButton("03", "QUIT", QuitGame, "menu-button--danger"));
            screen.Add(col);
            return screen;
        }

        private VisualElement BuildPlay()
        {
            var screen = El("screen");
            var col = El("screen-col", "screen-col--wide");
            col.Add(Txt("DEPLOYMENT", "screen-title"));
            col.Add(Txt("CHOOSE HOW TO JOIN YOUR SQUAD", "screen-hint"));

            var cards = El("cards");

            var steam = Card();
            steam.Add(Txt("STEAM · FRIENDS", "card-title"));
            steam.Add(Txt("Create a lobby and send the code to your friends, or join a friend's lobby. Works over the Internet.", "card-text"));
            _hostSteamButton = MenuButton("A", "CREATE LOBBY", () => { _net.StartHostSteam(); ShowErrorIfAny(); }, "menu-button--primary");
            steam.Add(_hostSteamButton);
            steam.Add(Txt("LOBBY CODE", "field-label"));
            _lobbyCodeField = Field(string.Empty);
            steam.Add(_lobbyCodeField);
            _joinSteamButton = MenuButton("B", "JOIN WITH CODE", JoinSteam);
            steam.Add(_joinSteamButton);
            cards.Add(steam);

            var lan = Card();
            lan.Add(Txt("LOCAL NETWORK", "card-title"));
            lan.Add(Txt("Same PC or same network: one player hosts, the others enter their address (port 7770).", "card-text"));
            lan.Add(MenuButton("C", "HOST ON LAN", () => _net.StartHostLan(), "menu-button--primary"));
            lan.Add(Txt("HOST ADDRESS", "field-label"));
            _addressField = Field("localhost");
            lan.Add(_addressField);
            lan.Add(MenuButton("D", "JOIN ON LAN", () => _net.JoinLan(_addressField.value)));
            cards.Add(lan);

            col.Add(cards);
            var back = MenuButton("◄", "BACK", () => ShowScreen(MenuScreen.Main), "menu-button--small");
            back.style.width = 260;
            back.style.marginTop = 20;
            col.Add(back);
            screen.Add(col);
            return screen;
        }

        private VisualElement BuildConnecting()
        {
            var screen = El("screen");
            var box = El("connecting");
            _spinner = El("spinner");
            box.Add(_spinner);
            box.Add(Txt("CONNECTING", "connecting-text"));
            _connectingDetail = Txt(string.Empty, "screen-hint");
            _connectingDetail.style.marginTop = 10;
            box.Add(_connectingDetail);
            var cancel = MenuButton("✕", "CANCEL", CancelConnection, "menu-button--small");
            cancel.style.width = 240;
            box.Add(cancel);
            screen.Add(box);
            return screen;
        }

        private VisualElement BuildPause()
        {
            var screen = El("screen");
            var col = El("screen-col");
            col.Add(Txt("SESSION ACTIVE", "title-kicker"));
            col.Add(Txt("PAUSED", "screen-title"));
            _pauseInfo = Txt(string.Empty, "screen-hint");
            col.Add(_pauseInfo);
            col.Add(MenuButton("01", "RESUME", Resume, "menu-button--primary"));
            col.Add(MenuButton("02", "SETTINGS", () => OpenSettings(MenuScreen.Pause)));
            col.Add(MenuButton("03", "LEAVE MISSION", Disconnect, "menu-button--danger"));
            col.Add(MenuButton("04", "QUIT GAME", QuitGame, "menu-button--danger"));
            screen.Add(col);

            var side = El("screen-side");
            var card = Card();
            card.style.width = 520;
            card.style.flexGrow = 0;
            card.style.marginRight = 0;
            card.Add(Txt("SQUAD", "card-title"));

            _squadSteam = El();
            _squadSteam.Add(Txt("Send this code to your friends: they enter it under PLAY → JOIN WITH CODE.", "card-text"));
            _squadSteam.Add(Txt("LOBBY CODE", "field-label"));
            _lobbyCodeLabel = Txt("—", "lobby-code");
            _squadSteam.Add(_lobbyCodeLabel);
            var row = El("card-row");
            var copy = MenuButton("⧉", "COPY", CopyLobbyCode, "menu-button--small");
            copy.style.flexGrow = 1;
            copy.style.marginRight = 8;
            var overlay = MenuButton("⌘", "OVERLAY", () => _net.Steam?.OpenInviteDialog(), "menu-button--small");
            overlay.style.flexGrow = 1;
            row.Add(copy);
            row.Add(overlay);
            _squadSteam.Add(row);
            _squadSteam.Add(Txt("FRIENDS ONLINE", "field-label"));
            _friendsList = new ScrollView(ScrollViewMode.Vertical);
            _friendsList.AddToClassList("friends-list");
            _squadSteam.Add(_friendsList);
            card.Add(_squadSteam);

            _squadLan = El();
            _squadLan.Add(Txt("LAN session: others join from PLAY → JOIN ON LAN with this PC's address (port 7770).", "card-text"));
            card.Add(_squadLan);

            _squadClient = El();
            _squadClient.Add(Txt("You are in another operator's session. Ask the host for the code to invite more friends.", "card-text"));
            card.Add(_squadClient);

            side.Add(card);
            screen.Add(side);
            return screen;
        }

        private VisualElement BuildSettings()
        {
            var screen = El("screen");
            var col = El("screen-col", "screen-col--wide");
            col.Add(Txt("SETTINGS", "screen-title"));
            col.Add(Txt("CHANGES ARE APPLIED AND SAVED INSTANTLY", "screen-hint"));

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.style.maxWidth = 900;
            var list = El("settings-list");

            list.Add(Txt("CONTROLS", "settings-section"));
            _sensitivity = SliderRow(list, "MOUSE SENSITIVITY", GameSettings.MinSensitivity, GameSettings.MaxSensitivity, out _sensitivityValue,
                v => { GameSettings.MouseSensitivity = v; _sensitivityValue.text = SensitivityText(v); });

            list.Add(Txt("DISPLAY", "settings-section"));
            _fov = SliderRow(list, "FIELD OF VIEW", GameSettings.MinFov, GameSettings.MaxFov, out _fovValue,
                v => { GameSettings.FieldOfView = Mathf.Round(v); _fovValue.text = Mathf.RoundToInt(v) + "°"; });
            _fullscreen = ToggleRow(list, "FULLSCREEN", v => GameSettings.Fullscreen = v);
            _vsync = ToggleRow(list, "VSYNC", v => GameSettings.VSync = v);
            _quality = DropdownRow(list, "GRAPHICS QUALITY", new List<string>(QualitySettings.names), i => GameSettings.QualityLevel = i);

            list.Add(Txt("AUDIO", "settings-section"));
            _volume = SliderRow(list, "MASTER VOLUME", 0f, 1f, out _volumeValue,
                v => { GameSettings.MasterVolume = v; _volumeValue.text = Mathf.RoundToInt(v * 100f) + "%"; });

            list.Add(Txt("INTERFACE", "settings-section"));
            _showFps = ToggleRow(list, "SHOW FPS AND PING", v => GameSettings.ShowFps = v);
            _damageNumbers = ToggleRow(list, "DAMAGE NUMBERS", v => GameSettings.ShowDamageNumbers = v);

            scroll.Add(list);
            col.Add(scroll);

            var buttons = El("card-row");
            buttons.style.marginTop = 16;
            var back = MenuButton("◄", "BACK", () => ShowScreen(_settingsReturn), "menu-button--small");
            back.style.width = 260;
            back.style.marginRight = 12;
            var reset = MenuButton("↺", "DEFAULTS", () => { GameSettings.ResetToDefaults(); RefreshSettings(); }, "menu-button--small");
            reset.style.width = 260;
            buttons.Add(back);
            buttons.Add(reset);
            col.Add(buttons);
            screen.Add(col);
            return screen;
        }

        // ------------------------------------------------------------------ Azioni

        private void JoinSteam()
        {
            string code = _lobbyCodeField.value;
            _net.JoinSteamByCode(code);
            if (_net.LastError == null)
                _pendingJoinUntil = Time.unscaledTime + LobbyJoinTimeout;
            ShowErrorIfAny();
        }

        private void CancelConnection()
        {
            _pendingJoinUntil = -1f;
            _net.StopAll();
            ShowScreen(MenuScreen.Play);
        }

        private void Disconnect()
        {
            _paused = false;
            _net.StopAll(); // GameSession riporta alla scena Boot; il menu torna alla schermata principale
        }

        private void OpenSettings(MenuScreen returnTo)
        {
            _settingsReturn = returnTo;
            ShowScreen(MenuScreen.Settings);
        }

        private void CopyLobbyCode()
        {
            if (_net.Steam == null || !_net.Steam.InLobby)
                return;
            GUIUtility.systemCopyBuffer = _net.Steam.LobbyCode;
            ShowToast("Lobby code copied to clipboard.", false);
        }

        private static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------------------------------------------ Aggiornamenti

        private void UpdatePauseInfo(float now)
        {
            string mode = _net.Mode == NetMode.Steam ? "STEAM" : "LOCAL NETWORK";
            string role = _net.IsServer ? $"HOST · {_net.ConnectedPlayers} OPERATORS" : "OPERATOR";
            string info = $"{mode} · {role}";
            if (_pauseInfo.text != info)
                _pauseInfo.text = info;

            bool steamHost = _net.IsSteamHost;
            _squadSteam.style.display = steamHost ? DisplayStyle.Flex : DisplayStyle.None;
            _squadLan.style.display = !steamHost && _net.IsServer ? DisplayStyle.Flex : DisplayStyle.None;
            _squadClient.style.display = !_net.IsServer ? DisplayStyle.Flex : DisplayStyle.None;

            if (!steamHost || now < _nextFriendsRefresh)
                return;
            _nextFriendsRefresh = now + 3f;
            _lobbyCodeLabel.text = _net.Steam.LobbyCode;
            _net.Steam.GetOnlineFriends(_friends);
            _friendsList.Clear();
            if (_friends.Count == 0)
            {
                _friendsList.Add(Txt("No friends online.", "card-text"));
                return;
            }
            foreach (SteamService.FriendEntry friend in _friends)
            {
                var row = El("friend-row");
                var name = Txt(friend.InGame ? "● " + friend.Name : friend.Name, "friend-name");
                if (friend.InGame)
                    name.AddToClassList("friend-ingame");
                row.Add(name);
                ulong id = friend.Id;
                string friendName = friend.Name;
                var invite = new Button(() =>
                {
                    _net.Steam.InviteFriend(id);
                    ShowToast($"Invite sent to {friendName}: they accept it from Steam chat with the game running.", false);
                }) { text = "INVITE" };
                invite.AddToClassList("friend-invite");
                row.Add(invite);
                _friendsList.Add(row);
            }
        }

        private void UpdateStatusBar()
        {
            bool ready = SteamService.IsReady;
            string text = ready ? $"STEAM · {_net.Steam?.LocalName}".ToUpperInvariant() : "STEAM UNAVAILABLE · LAN ONLY";
            if (_steamStatus.text != text)
            {
                _steamStatus.text = text;
                _steamDot.EnableInClassList("status-dot--off", !ready);
            }
        }

        private void RefreshSettings()
        {
            _sensitivity.SetValueWithoutNotify(GameSettings.MouseSensitivity);
            _sensitivityValue.text = SensitivityText(GameSettings.MouseSensitivity);
            _fov.SetValueWithoutNotify(GameSettings.FieldOfView);
            _fovValue.text = Mathf.RoundToInt(GameSettings.FieldOfView) + "°";
            _volume.SetValueWithoutNotify(GameSettings.MasterVolume);
            _volumeValue.text = Mathf.RoundToInt(GameSettings.MasterVolume * 100f) + "%";
            _fullscreen.SetValueWithoutNotify(GameSettings.Fullscreen);
            _vsync.SetValueWithoutNotify(GameSettings.VSync);
            _showFps.SetValueWithoutNotify(GameSettings.ShowFps);
            _damageNumbers.SetValueWithoutNotify(GameSettings.ShowDamageNumbers);
            if (_quality.choices.Count > 0)
                _quality.SetValueWithoutNotify(_quality.choices[Mathf.Clamp(GameSettings.QualityLevel, 0, _quality.choices.Count - 1)]);
        }

        private static string SensitivityText(float v) => (v / GameSettings.DefaultSensitivity).ToString("0.00") + "x";

        // ------------------------------------------------------------------ Messaggi

        private void ShowErrorIfAny()
        {
            if (_net.LastError != null)
                ShowToast(_net.LastError, true);
        }

        private void UpdateToast(float now)
        {
            string error = _net.LastError;
            if (error != null && error != _shownError)
            {
                _shownError = error;
                ShowToast(error, true);
            }
            if (_toastHideAt > 0f && now >= _toastHideAt)
                HideToast();
        }

        private void ShowToast(string message, bool isError)
        {
            _toastText.text = message;
            _toast.style.display = DisplayStyle.Flex;
            _toast.style.backgroundColor = isError ? new Color(0.47f, 0.09f, 0.08f, 0.9f) : new Color(0.12f, 0.3f, 0.16f, 0.9f);
            _toast.style.borderLeftColor = isError ? new Color(1f, 0.35f, 0.27f) : new Color(0.43f, 0.86f, 0.47f);
            _toastHideAt = Time.unscaledTime + (isError ? 8f : 3f);
        }

        private void HideToast()
        {
            _toast.style.display = DisplayStyle.None;
            _toastHideAt = -1f;
        }

        // ------------------------------------------------------------------ Elementi

        /// <summary>Scheda: piastra a angoli tagliati con trama esagonale e bordo luminoso.</summary>
        private static VisualElement Card()
        {
            VisualElement card = El("card");
            card.Add(new VisualElement { pickingMode = PickingMode.Ignore }.WithClass("card-hex"));
            card.Add(new VisualElement { pickingMode = PickingMode.Ignore }.WithClass("card-edge"));
            return card;
        }

        private static VisualElement El(params string[] classes)
        {
            var e = new VisualElement();
            foreach (string c in classes)
                e.AddToClassList(c);
            return e;
        }

        private static Label Txt(string text, params string[] classes)
        {
            var label = new Label(text);
            foreach (string c in classes)
                label.AddToClassList(c);
            return label;
        }

        private static Button MenuButton(string index, string text, System.Action onClick, params string[] extraClasses)
        {
            var button = new Button(onClick);
            button.AddToClassList("menu-button");
            button.Add(new VisualElement { pickingMode = PickingMode.Ignore }.WithClass("button-edge"));
            foreach (string c in extraClasses)
                button.AddToClassList(c);
            var idx = Txt(index, "button-index", "font-mono");
            idx.pickingMode = PickingMode.Ignore;
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            button.Add(idx);
            button.Add(label);
            return button;
        }

        private static TextField Field(string value)
        {
            var field = new TextField { value = value };
            field.AddToClassList("menu-field");
            return field;
        }

        private static Slider SliderRow(VisualElement parent, string name, float min, float max, out Label valueLabel, System.Action<float> onChange)
        {
            var row = El("setting-row");
            row.Add(Txt(name, "setting-name"));
            var slider = new Slider(min, max);
            slider.AddToClassList("setting-control");
            Label value = Txt(string.Empty, "setting-value");
            slider.RegisterValueChangedCallback(e => onChange(e.newValue));
            row.Add(slider);
            row.Add(value);
            parent.Add(row);
            valueLabel = value;
            return slider;
        }

        private static Toggle ToggleRow(VisualElement parent, string name, System.Action<bool> onChange)
        {
            var row = El("setting-row");
            row.Add(Txt(name, "setting-name"));
            var toggle = new Toggle();
            toggle.AddToClassList("setting-control");
            toggle.RegisterValueChangedCallback(e => onChange(e.newValue));
            row.Add(toggle);
            parent.Add(row);
            return toggle;
        }

        private static DropdownField DropdownRow(VisualElement parent, string name, List<string> choices, System.Action<int> onChange)
        {
            var row = El("setting-row");
            row.Add(Txt(name, "setting-name"));
            var dropdown = new DropdownField(choices, 0);
            dropdown.AddToClassList("setting-control");
            dropdown.RegisterValueChangedCallback(e => onChange(dropdown.index));
            row.Add(dropdown);
            parent.Add(row);
            return dropdown;
        }

#if UNITY_EDITOR
        public void EditorSetup(NetBootstrapMenu net, StyleSheet styleSheet)
        {
            _net = net;
            _styleSheet = styleSheet;
        }
#endif
    }

    internal static class VisualElementExtensions
    {
        public static VisualElement WithClass(this VisualElement element, string className)
        {
            element.AddToClassList(className);
            return element;
        }
    }
}
