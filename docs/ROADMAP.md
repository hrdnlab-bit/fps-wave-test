# ROADMAP

Ogni milestone è giocabile e testata in multiplayer (host + client). Percorso scelto: **ibrido** (rete locale minima, poi subito gameplay; Steam e strumenti di debug completi dopo M1).

| # | Milestone | Contenuto | Fatto quando |
|---|---|---|---|
| M0a | Setup + rete locale | Progetto Unity URP, repo + LFS, struttura cartelle e asmdef, FishNet + Tugboat, scena Boot e menu minimo Host/Join, Multiplayer Play Mode | 2-4 istanze connesse in locale |
| M1 | Player + arma | Controller FPS replicato, camera, sistema Stat/Modifier, pistola hitscan con validazione server + fucile d'assalto di test, ADS, feedback (rinculo, muzzle, hit marker), greybox map, arma + braccia realistiche, overlay FPS/RTT minimo | Host e client si muovono e sparano a bersagli, danno coerente su tutti |
| M0b | Steam + debug | Steamworks.NET + FishySteamworks, lobby Steam (crea/invita/entra), controllo versione al join, overlay di debug completo, console comandi | Partita via Steam con un amico |
| M2 | Nemici | Runner e Brute, AI host-side con NavMesh, salute/danno/morte, pooling di rete, stato a terra e rianimazione | 40 nemici in scena entro budget, rianimazione funzionante |
| M3 | Wave endless | DifficultyModel, WaveDefinition, SpawnDirector, SpawnPointSelector, stati della wave sincronizzati, HUD wave, wipe e schermata finale, primo boss a wave 10, prime mutazioni | Si arriva oltre la wave 10 con scaling percepibile |
| M4 | Drop e risorse | Tabelle di drop, oggetti a terra con magnetismo, crediti/materiali condivisi, Nuclei per giocatore | Risorse coerenti su tutti i client |
| M5 | Fine wave + shop | Fase di preparazione, timer, pronto, stazione upgrade, shop | Ciclo combatti -> potenzia completo |
| M6 | Mod | Slot, capacità, rarità, livelli, debuff, set bonus, UI | Build diverse con DPS misurabile |
| M7 | Mappa a zone | Terreno, 4 zone, generazione con seed, nodi di spawn generati, NavMesh a runtime | Stessa mappa su tutti i client, spawn corretti in ogni zona |
| M8 | Polish | Art pass, audio, VFX, Spitter/Stalker/Drone/Elite, altri boss e mutazioni, ottimizzazione, soak test endless, bilanciamento, impostazioni, IT/EN | Sessione di 60+ minuti entro budget |

## Task di M0a
**M0a completata il 2026-11-21 (versione 0.1.0; tag git non pubblicato, vedi HANDOFF).**
1. M0a.1 Setup repo: `.gitignore`, LFS, struttura cartelle, asmdef, `docs/`, primo commit. **(fatto)**
2. M0a.2 Pacchetti: FishNet, Unity Test Framework, Multiplayer Play Mode; pulizia del template. **(fatto)**
3. M0a.3 Scena Boot + NetworkManager + menu Host/Join (Tugboat), avvio automatico in Editor. **(fatto)**
4. M0a.4 Test con 4 istanze, commit, merge, tag `v0.1.0`. **(fatto)**

## Task di M1
1. M1.1 Sistema Stat/Modifier + test. **(fatto)**
2. M1.2 Mappa greybox (generata da `FpsWave/Setup/3. Build M1 Content`). **(fatto)**
3. M1.3 Player FPS replicato (movimento, camera, input, spawn per connessione). **(fatto)**
4. M1.4 Pistola hitscan validata dal server + fucile di test, ADS, feedback, bersagli. **(fatto)**
5. M1.5 Overlay FPS/RTT (nell'HUD). **(fatto)**
6. M1.6 Asset realistici: pistola, fucile, braccia FPS, suoni. **(armi fatte: M4 con braccia, pistola Beretta; suoni da fare)**

## Task di M0b
1. M0b.1 Steamworks.NET + FishySteamworks, Multipass LAN/Steam. **(fatto)**
2. M0b.2 Lobby Steam per amici: crea, invita, entra (codice, invito, lista amici). **(fatto, test con l'amico da fare)**
3. M0b.3 Controllo versione al join. **(fatto, sui dati della lobby)**
4. M0b.4 Build Windows zippata per i test. **(fatto: `FpsWave/Build/Windows + Zip`)**
5. M0b.5 Overlay di debug completo (RTT, banda, tick, oggetti osservati). **(da fare)**
6. M0b.6 Console comandi. **(da fare)**

## Task di M2
1. M2.1 Runner: prefab di rete in pool, AI server con NavMeshAgent (insegui, fermati, colpisci), posti di attacco. **(fatto, branch `matteo/m2-runner`)**
2. M2.2 Spawn: nodi di spawn dietro i ripari, EnemyDirector con anello/visibilità/percorso/pressione per player, riciclo. **(fatto, numero di nemici fisso fino a M3)**
3. M2.3 Brute (600 HP, carica). **(fatto in Fase 1: corazza a piastre, nucleo, carica)**
4. M2.4 Stato a terra e rianimazione dei player (sostituisce il ritorno in vita automatico). **(fatto in Fase 1, `PlayerLife`; il wipe arriva con M3)**
5. M2.5 Verifica a 40 nemici entro budget (profiler) e test host + client via Steam. **(da fare)**

## Fasi delle idee di Matteo (dal 2026-11-22)
1. **Fase 1** (fatta, branch `matteo/phase1-suit-brute`): tuta con energia e tre abilità, Brute con piastre e carica, ping, a terra e rianimazione, voce della tuta (i suoni provati sono stati tolti).
2. **Fase 2** (da fare): sistema di wave (M3), poi contaminazione che si espande nella valle (zone da riconquistare riattivando pompe/generatori), nemici che si adattano a come gioca la squadra, voto "estrai o continua" dopo ogni boss, wave notturne e di tempesta (torcia e visore termico).
3. **Fase 3** (da fare): difese del complesso di ricerca attivabili con i crediti (torrette, recinzioni elettrificate, sfiati di gas).
