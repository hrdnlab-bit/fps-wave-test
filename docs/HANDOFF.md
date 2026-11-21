# HANDOFF

Ultimo aggiornamento: 2026-11-21

## Stato attuale
- **M0a completata** (setup + rete locale, versione 0.1.0).
- **M1 in corso**: nucleo di gameplay fatto e verificato (M1.1-M1.5), sparo dal client remoto confermato dall'utente. **M1.6** in corso: fucile M4 realistico con braccia e animazioni fatto; mancano pistola e suoni.
- Progetto Unity: `C:\Users\manue\FpsWaveTest` (Unity 6000.6.9f1, URP). Repo: `hrdnlab-bit/fps-wave-test`, branch `main` (merge di `feature/m1-player-weapons`).
- `C:\Users\manue\My project` contiene un ALTRO gioco (SABBIA): non toccarlo.
- Ultimo commit: `69771bf feat(weapons): realistic M4 first-person view model with arms` (su `main`, branch `feature/m1-assets`).
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
| View model | `Scripts/Weapons/WeaponViewModel.cs` (animazioni, compensazione osso camera), `Scripts/Editor/WeaponViewModelBuilder.cs` (menu `FpsWave/Setup/4`) |
| Editor | `Scripts/Editor/M1ContentBuilder.cs` (genera layer, materiali, armi, prefab, Greybox con SceneId stabili, collega Boot) |
| Dati | `Data/Weapons/Pistol.asset`, `AssaultRifle.asset` |

## Verifica fatta (M1)
- Compilazione 0 errori; test EditMode **29/29** (2026-11-21, dopo il fix MPPM).
- Fix MPPM verificato: Host + Player 2 collegati, uscita e rientro nel Play dopo 12 s senza errori di porta, Player 2 ricollegato in entrambe le sessioni.
- Host da solo: caricamento Greybox, spawn, HUD; pistola 12 -> 9 colpi con danno replicato sul bersaglio; fucile ~13 colpi in 1.2 s (600 RPM), bersaglio abbattuto, ricarica 17 -> 30; nessun colpo rifiutato dal server.
- Host + Player 2 (MPPM): connessione, due player spawnati, 0 errori su entrambe le istanze.
- Sparo da Player 2 verificato dall'utente (2026-11-21).
- M4: estrazione, raffica (tracer, luce, rinculo) e ricarica verificati in Play. ADS non verificato (serve tenere premuto il tasto destro): da provare a mano, i valori sono in `AssaultRifle.asset` (`_viewAdsOffset`).

## Problemi aperti / note
- Il primo frame dopo lo spawn può muovere la visuale se il mouse si muove (cursore bloccato subito): accettabile.
- Scena "Untitled" vuota nella gerarchia dopo il caricamento globale di FishNet: innocua.
- HUD e menu in OnGUI (temporanei, allocano poco ma non zero): da sostituire con UI Toolkit (M0b/M5).
- Asset binari sul PC: `Assets/ThirdParty/Fab/M4Animations/` (fuori dal repo). Da fare il backup in Google Drive (`OPEN_QUESTIONS.md` #12).
- Pistola "FPS Guns 4K - Pistol 2" scaricata dall'utente in `Downloads/fps_guns_4k_pistol_2.zip` (148 MB): il trasferimento verso l'ambiente AI fallisce per dimensione/timeout; serve estrarla sul PC (o riprovare).
- Se si rigenera `3. Build M1 Content`, rieseguire poi `4. Build Weapon View Models`.

## Risposte dell'utente (2026-11-21)
- Procedura asset: Fab nel browser dell'app, login dell'utente, l'AI propone nome/licenza/dimensione prima di scaricare. CC-BY accettati.
- Ordine: M1.6 prima di M0b.
- Test sparo da Player 2: bloccato dall'errore di porta, ora corretto; da ripetere.

## Prossimi passi
1. M1.6: pistola (estrarre lo zip sul PC in `Assets/ThirdParty/Fab/Pistol2/`, materiali URP, view model senza braccia o con le braccia dell'M4) e suoni (Sonniss GDC / Freesound CC0).
2. Verifica ADS del fucile e regolazione di `_viewAdsOffset`.
3. Chiudere M1 (versione 0.2.0).
4. M0b: Steam (Steamworks.NET + FishySteamworks), lobby, controllo versione al join, overlay debug completo, console comandi.
