# CHANGELOG

Formato: [Keep a Changelog](https://keepachangelog.com/it/1.1.0/), versioni semantiche.

## [Unreleased]

## [0.2.0] - 2026-11-21
### Added (M0b - Steam)
- Steam: Steamworks.NET + FishySteamworks, lobby per amici con controllo di versione, ingresso con codice lobby, inviti dal gioco o da Steam, "Unisciti alla partita" dalla lista amici.
- Multipass con Tugboat (LAN/Multiplayer Play Mode) e FishySteamworks (relay Steam) sullo stesso NetworkManager; menu di sviluppo con Host/Join LAN e Host/Join Steam.
- Strumenti `FpsWave/Setup/5. Configure Steam (Multipass)` e `FpsWave/Build/Windows + Zip (test con amici)` (exe, steam_appid.txt, LEGGIMI.txt, zip senza simboli di debug).
- Pistola realistica (Beretta M92, Fab) con animazione procedurale: estrazione, calcio, carrello, ricarica.

### Changed
- Build Windows in DirectX 11 (overlay di Steam più affidabile), esecuzione in background attiva.

### Added (M1)
- Sistema Stat/Modifier (formula unica, clamp, cache) con test.
- Salute autorevole sul server (SyncVar), zone colpibili con weakspot, bersagli di allenamento che si rigenerano.
- Controller FPS client-authoritative: camminata, sprint, crouch con controllo del soffitto, salto; visuale con rinculo; input definito in codice (tastiera/mouse + gamepad); controllo di plausibilità lato server.
- Armi hitscan: pistola (semi-auto) e fucile d'assalto di test (auto), ADS, bloom, ricarica; validazione server di cadenza (token bucket), munizioni, origine, gittata, bersaglio e linea di vista.
- Tracer e segni d'impatto in pool, flash di sparo.
- HUD temporaneo: mirino dinamico, hit marker (colpo/weakspot/critico/kill), munizioni, salute, FPS, RTT.
- Sessione: caricamento globale della scena Greybox all'avvio del server, ritorno a Boot alla disconnessione, spawn dei player a rotazione.
- Strumento `FpsWave/Setup/3. Build M1 Content` (layer, materiali, armi, prefab, mappa greybox con SceneId stabili).
- Tasto di sviluppo Home per riallineare la visuale.
- Fucile d'assalto realistico in prima persona (M4 + braccia con guanti, Fab): animazioni di estrazione, sparo e ricarica (normale e a vuoto, velocità adattata al tempo di ricarica), materiali URP, volata animata; strumento `FpsWave/Setup/4. Build Weapon View Models`.
- Posizioni hip/ADS configurabili per arma (`WeaponDefinition`).

### Fixed
- Ricompilazione degli script bloccata durante il Play (si applica all'uscita): evitava eccezioni a ogni frame per lo stato armi azzerato.
- Multiplayer Play Mode: il Play parte sempre dalla scena Boot (anche nei giocatori virtuali, che entravano con Greybox aperta e non si collegavano) e all'uscita dal Play client e server vengono chiusi esplicitamente (col domain reload disattivato il server Tugboat restava vivo e la porta 7770 risultava occupata). Il menu di sviluppo segnala la porta occupata invece di lasciare un client orfano.
- Intervallo di sparo calcolato in double (evitava un colpo per errore di arrotondamento float).


## [0.1.0] - 2026-11-21 (tag git non pubblicato)
### Added
- FishNet 4.7.4, Multiplayer Play Mode 3.0.0, Test Framework 1.8.0.
- Scena `Boot` con NetworkManager + Tugboat e menu di sviluppo Host/Join; avvio automatico in Editor (Host sull'istanza principale, join dei giocatori virtuali).
- Menu `FpsWave/Setup` (creazione scena Boot, rimozione residui del template).
- Mirror della Console in `Logs/FpsWaveConsole.log`.
- Assembly di test EditMode con i primi test.
- Testato: Host + 3 client locali.

### Removed
- Contenuti dimostrativi del template URP (TutorialInfo, Readme, SampleScene).

## [0.0.1] - 2026-11-21
### Added
- Progetto Unity 6000.6.9f1 (template Universal 3D) in `C:\Users\manue\FpsWaveTest`.
- `.gitignore` Unity, `.gitattributes` con Git LFS, struttura `Assets/_Project`, asmdef `FpsWave.Runtime`, `GameInfo` (versione e protocollo).
- Documenti di progetto in `docs/`, divisi per tema.
- Design endless: DifficultyModel, mutazioni, boss ogni 10 wave.

### Changed
- SYSTEM_PROMPT snellito: solo ruolo, autonomia, ciclo di lavoro, handoff.
- GAME_BASELINE ridotto a visione, pilastri, parametri e indice.
- Modello di rete: da PvP (prediction/lag compensation) a PvE MVP.
- Roadmap: percorso ibrido (M0a -> M1 -> M0b), sistema di stat in M1, mappa greybox da M1, mappa procedurale in M7.
- Ambientazione: valle in quarantena + complesso militare di ricerca sci-fi.
