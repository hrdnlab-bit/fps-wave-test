# HANDOFF

Ultimo aggiornamento: 2026-11-21

## Stato attuale
- **M0a completata** (setup + rete locale), tag `v0.1.0`. Prossima milestone: **M1** (player + armi).
- Progetto Unity: `C:\Users\manue\FpsWaveTest` (Universal 3D, Unity 6000.6.9f1), collegato alla sessione AI.
- Repo: `hrdnlab-bit/fps-wave-test`, branch `main`. Clone di lavoro dell'AI: `/home/claude/fps-wave-test`.
- `C:\Users\manue\My project` contiene un ALTRO gioco (SABBIA): non toccarlo.
- Pacchetti: FishNet 4.7.4 (git, versione bloccata in `packages-lock.json`), Multiplayer Play Mode 3.0.0, Test Framework 1.8.0, URP 17.6, Input System 1.20.1.

## Come si prova
1. Aprire la scena `Assets/_Project/Scenes/Boot.unity` (è la prima della build).
2. Window > Multiplayer > Multiplayer Play Mode: attivare Player 2-4.
3. Play: l'istanza principale avvia l'Host, i giocatori virtuali si collegano da soli; il menu in alto a sinistra mostra "Giocatori connessi: N".
4. Senza giocatori virtuali: Play e basta (Host da solo). Per disattivare l'avvio automatico: componente `NetBootstrapMenu` sul NetworkManager, `Auto Start In Editor`.

## Script e strumenti chiave
| File | Ruolo |
|---|---|
| `Scripts/Core/GameInfo.cs` | Versione e `ProtocolVersion` (da incrementare a ogni modifica di rete incompatibile) |
| `Scripts/Net/NetBootstrapMenu.cs` | Menu di sviluppo Host/Join (OnGUI) + avvio automatico in Editor per MPPM |
| `Scripts/Editor/ProjectSetupTools.cs` | Menu `FpsWave/Setup`: crea la scena Boot, rimuove i residui del template |
| `Scripts/Editor/ConsoleLogMirror.cs` | Copia la Console in `Logs/FpsWaveConsole.log` (letto dall'AI per la verifica) |
| `Tests/EditMode/` | Test di logica pura (2/2 verdi) |

## Commit recenti (M0a)
- `chore(deps): add FishNet, Multiplayer Play Mode and Test Framework`
- `net: add Boot scene with NetworkManager, Tugboat and dev host/join menu`
- `chore(editor): add setup tools, console log mirror and remove template leftovers`
- `test: add EditMode test assembly with GameInfo tests`
- `chore: add Unity meta files for project folders`

## Verifica fatta
- Compilazione: 0 errori.
- Test EditMode: 2/2.
- Play Mode: Host + 3 giocatori virtuali connessi (4 totali) via Tugboat, nessun errore nel log.

## Problemi aperti / note
- Il controllo remoto non riesce a portare in primo piano le finestre dei giocatori virtuali: per questo l'avvio è automatico. Per vederle, l'utente può usare "Focus on Player" dal menu ⋮ di Multiplayer Play Mode.
- Il push LFS dall'ambiente AI non funziona: asset binari fuori dal repo (vedi `ASSETS.md`).
- Restano nel template `Mobile_Renderer`/`Mobile_RPAsset` (usati dai livelli di qualità Mobile): innocui, da valutare in M8.
- Cartella Google Drive `FpsWaveTest_Assets` per il backup degli asset: da creare (`OPEN_QUESTIONS.md` #12).

## Prossimi passi (M1)
1. Sistema Stat/Modifier (logica pura + test EditMode).
2. Mappa greybox (generata da strumento Editor, come la scena Boot).
3. Player FPS replicato: movimento client-authoritative (camminata, sprint, crouch, salto), camera, Input System, spawn del player per ogni connessione.
4. Pistola hitscan con validazione server + fucile d'assalto di test, ADS, rinculo, hit marker, bersagli con salute.
5. Overlay FPS/RTT minimo.
6. Asset: pistola + braccia FPS realistiche (prima ricerca su Fab/Asset Store gratuiti; l'utente deve fare il login).
