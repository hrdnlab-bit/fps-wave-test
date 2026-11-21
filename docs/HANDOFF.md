# HANDOFF

Ultimo aggiornamento: 2026-11-21

## Stato attuale
- **M0a completata** (setup + rete locale, versione 0.1.0).
- **M1 in corso**: nucleo di gameplay fatto e verificato (M1.1-M1.5); manca **M1.6 asset realistici** (serve il login dell'utente su Fab/Asset Store).
- Progetto Unity: `C:\Users\manue\FpsWaveTest` (Unity 6000.6.9f1, URP). Repo: `hrdnlab-bit/fps-wave-test`, branch `main` (merge di `feature/m1-player-weapons`).
- `C:\Users\manue\My project` contiene un ALTRO gioco (SABBIA): non toccarlo.
- Ultimo commit: `576df0f fix(net): reliable MPPM play sessions` (su `main`, branch `fix/m1-mppm-port`).
- Tag git: il push dei tag dall'ambiente AI è bloccato (403); le versioni sono tracciate in `CHANGELOG.md`. Il tag `v0.1.0` NON esiste su GitHub.

## Come si prova
1. Scena `Assets/_Project/Scenes/Boot.unity` -> Play: l'istanza principale fa da Host e carica la mappa `Greybox`.
2. Controlli: WASD, mouse, Shift sprint, Ctrl/C crouch, Spazio salto, tasto sinistro spara, tasto destro mira (ADS), R ricarica, 1 fucile (test), 2 pistola, Esc libera il cursore, Home (solo sviluppo) riallinea la visuale.
3. Poligono davanti allo spawn: bersagli a 10/20/35/50 m (diventano rossi con i danni, scuri da morti, si rigenerano dopo 3 s), muro per il test della linea di vista, cover, rampa + piattaforma, tunnel basso per il crouch.
4. Multiplayer: Window > Multiplayer > Multiplayer Play Mode, attivare Player 2-4, poi Play (join automatico).
5. Il Play parte sempre da `Boot` anche con un'altra scena aperta; per uscire usare **Ctrl+P** (un clic sul pulsante Play col cursore bloccato spara).
6. Se si rigenera il contenuto (`FpsWave/Setup/3. Build M1 Content`), **disattivare prima i giocatori virtuali**: bloccano `DefaultPrefabObjects.asset` (errore "Accesso negato").

## Script chiave (M1)
| Area | File |
|---|---|
| Statistiche | `Scripts/Stats/` (StatType, StatModifier, Stat, StatBlock) |
| Combattimento | `Scripts/Combat/` (Health SyncVar server, HitZone, DamageCalculator, TrainingDummy) |
| Player | `Scripts/Player/` (FpsInput, PlayerInputReader, PlayerMotor, PlayerLook, PlayerView, PlayerSpawnPoint) |
| Armi | `Scripts/Weapons/` (WeaponDefinition SO, WeaponState, FireRateLimiter, ShotValidation, PlayerWeapons, ShotFxPool) |
| Sessione | `Scripts/Net/` (GameSession: carica Greybox; PlayerSpawnService; NetBootstrapMenu) |
| UI | `Scripts/UI/PlayerHud.cs` (temporaneo, OnGUI) |
| Play Mode | `Scripts/Editor/PlayModeNetworkGuard.cs` (Play da Boot, chiusura rete all'uscita), `Scripts/Net/EditorInstanceInfo.cs` (riconosce i giocatori virtuali) |
| Editor | `Scripts/Editor/M1ContentBuilder.cs` (genera layer, materiali, armi, prefab, Greybox con SceneId stabili, collega Boot) |
| Dati | `Data/Weapons/Pistol.asset`, `AssaultRifle.asset` |

## Verifica fatta (M1)
- Compilazione 0 errori; test EditMode **29/29** (2026-11-21, dopo il fix MPPM).
- Fix MPPM verificato: Host + Player 2 collegati, uscita e rientro nel Play dopo 12 s senza errori di porta, Player 2 ricollegato in entrambe le sessioni.
- Host da solo: caricamento Greybox, spawn, HUD; pistola 12 -> 9 colpi con danno replicato sul bersaglio; fucile ~13 colpi in 1.2 s (600 RPM), bersaglio abbattuto, ricarica 17 -> 30; nessun colpo rifiutato dal server.
- Host + Player 2 (MPPM): connessione, due player spawnati, 0 errori su entrambe le istanze.
- Non verificato a mano: sparo del client remoto (le finestre dei giocatori virtuali non sono controllabili da remoto) -> da provare dall'utente ora che Player 2 si collega.

## Problemi aperti / note
- Il primo frame dopo lo spawn può muovere la visuale se il mouse si muove (cursore bloccato subito): accettabile.
- Scena "Untitled" vuota nella gerarchia dopo il caricamento globale di FishNet: innocua.
- HUD e menu in OnGUI (temporanei, allocano poco ma non zero): da sostituire con UI Toolkit (M0b/M5).
- Asset: vedi `OPEN_QUESTIONS.md` #13 (login e scelta del pacchetto armi/braccia).

## Risposte dell'utente (2026-11-21)
- Procedura asset: Fab nel browser dell'app, login dell'utente, l'AI propone nome/licenza/dimensione prima di scaricare. CC-BY accettati.
- Ordine: M1.6 prima di M0b.
- Test sparo da Player 2: bloccato dall'errore di porta, ora corretto; da ripetere.

## Prossimi passi
1. M1.6: asset realistici di pistola, fucile, braccia FPS e suoni (serve login utente; vedi opzioni in `ASSETS.md`).
2. Chiudere M1 (versione 0.2.0) dopo il test dello sparo da client remoto.
3. M0b: Steam (Steamworks.NET + FishySteamworks), lobby, controllo versione al join, overlay debug completo, console comandi.
