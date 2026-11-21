# HANDOFF

Ultimo aggiornamento: 2026-11-21 (versione 0.2.0)

## Stato attuale
- **M0a completata** (0.1.0). **M1 quasi completa**: gameplay verificato in rete (anche lo sparo dal client remoto), armi realistiche fatte (M4 con braccia animate, pistola Beretta con animazione procedurale). Manca **M1.6 suoni**.
- **M0b in corso**: Steam integrato e verificato in locale (Steam pronto, lobby creata, server/client FishySteamworks attivi), build Windows zippata pronta. Da fare: **test con l'amico** (`OPEN_QUESTIONS.md` #14), overlay di debug completo, console comandi.
- Progetto Unity: `C:\Users\manue\FpsWaveTest` (Unity 6000.6.9f1, URP). Repo `hrdnlab-bit/fps-wave-test`, branch `main`.
- `C:\Users\manue\My project` contiene un ALTRO gioco (SABBIA): non toccarlo.
- Ultimi commit su `main`: `391a153` (codice lobby, inviti dal gioco, DX11), `eccc40f` (Steam + Multipass), `41cbef3` (pistola), `69771bf` (M4).
- Tag git: il push dei tag dall'ambiente AI è bloccato (403); le versioni sono in `CHANGELOG.md`.
- **Lavoro in due** (dal 2026-11-21): questo file contiene lo stato comune; il lavoro in corso di ciascuno è in `docs/handoff/<persona>.md`, i lock su scene e prefab in `docs/LOCKS.md`, il setup di un PC nuovo in `docs/GUIDA_COLLABORATORE.md`, le istruzioni per Claude in `CLAUDE.md` (radice del repo).

## Come si prova
1. Scena qualsiasi -> Play (parte sempre da `Boot`): l'Editor fa da **Host LAN** e carica `Greybox`. Uscire con **Ctrl+P**.
2. Multiplayer locale: Window > Multiplayer > Multiplayer Play Mode, attivare Player 2-4, Play (join automatico in LAN).
3. Steam in Editor: nel menu "Disconnetti" -> "Host Steam (crea lobby)". In Editor l'overlay di Steam non compare.
4. Controlli: WASD, mouse, Shift sprint, Ctrl/C crouch, Spazio salto, sinistro spara, destro mira, R ricarica, 1 fucile, 2 pistola, Esc libera il cursore, Home (sviluppo) riallinea la visuale.
5. Build per l'amico: `FpsWave/Build/Windows + Zip (test con amici)` -> `Build/FpsWaveTest_0.2.0_win64.zip` (~66 MB). Istruzioni in `LEGGIMI.txt` dentro lo zip.
6. Rigenerazione contenuti (disattivare prima i giocatori virtuali): `Setup/3` (mappa, player, armi), poi `Setup/4` (view model armi; la pistola viene copiata da `Download/FPS Guns 4K - Pistol 2` se non è già nel progetto), `Setup/5` (Steam/Multipass).

## Script chiave
| Area | File |
|---|---|
| Statistiche | `Scripts/Stats/` (StatType, StatModifier, Stat, StatBlock) |
| Combattimento | `Scripts/Combat/` (Health, HitZone, DamageInfo, TrainingDummy) |
| Player | `Scripts/Player/` (FpsInput, PlayerInputReader, PlayerMotor, PlayerLook, PlayerView, PlayerSpawnPoint) |
| Armi | `Scripts/Weapons/` (WeaponDefinition, WeaponState, FireRateLimiter, ShotValidation, PlayerWeapons, ShotFxPool, WeaponViewModel) |
| Rete | `Scripts/Net/` (NetBootstrapMenu: menu LAN/Steam; NetTransports: Multipass; SteamService: Steam, lobby, inviti; GameSession; PlayerSpawnService; EditorInstanceInfo) |
| Editor | `Scripts/Editor/` (M1ContentBuilder `Setup/3`, WeaponViewModelBuilder `Setup/4`, SteamSetupTools `Setup/5`, BuildTools `Build/...`, PlayModeNetworkGuard, ConsoleLogMirror) |
| Terze parti | `Assets/ThirdParty/FishySteamworks/` (codice, nel repo), `Assets/ThirdParty/Fab/M4Animations/` e `Fab/Pistol2/` (binari solo sul PC) |

## Verifica fatta (2026-11-21)
- Compilazione 0 errori; test EditMode **32/32**.
- MPPM: Host + Player 2, rientro nel Play senza errori di porta; sparo da Player 2 confermato dall'utente.
- M4: estrazione, raffica, ricarica (adattata ai 2,2 s). Pistola: texture metalliche, calcio e carrello, ricarica procedurale. ADS non verificato (serve tenere premuto il tasto destro).
- Steam in Editor: `SteamAPI` pronto (account dell'utente, App ID 480), lobby creata, server+client FishySteamworks avviati, mappa caricata, chiusura pulita all'uscita.
- Build 0.2.0 avviata dall'utente: Steam pronto e Host Steam funzionante. L'overlay (Shift+Tab) non si apriva nella build DX12: aggiunti codice lobby e inviti dal gioco, build passata a DirectX 11 (da riverificare l'overlay).

## Problemi aperti / note
- Due client Steam con lo stesso account sullo stesso PC non sono possibili: il join via Steam si prova solo con l'amico.
- Avvisi di API deprecate di Unity 6.6 (`FindFirstObjectByType`, `FindObjectsSortMode`) in `NetBootstrapMenu` e `PlayerSpawnService`: da aggiornare.
- HUD e menu in OnGUI (temporanei).
- Backup asset binari su Google Drive ancora da fare (`OPEN_QUESTIONS.md` #12).

## Prossimi passi
1. Test con l'amico (build 0.2.0): Host Steam -> codice lobby -> Join Steam; verificare movimento, sparo e danno su entrambi; annotare RTT.
2. M1.6 suoni (Sonniss GDC / Freesound CC0) e verifica ADS del fucile.
3. M0b: overlay di debug completo (RTT, banda, tick, oggetti osservati) e console comandi.
4. M2: nemici (Runner, Brute), AI host-side, pooling di rete, a terra e rianimazione.
