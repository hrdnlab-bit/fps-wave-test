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
**M0a completata il 2026-11-21 (tag `v0.1.0`).**
1. M0a.1 Setup repo: `.gitignore`, LFS, struttura cartelle, asmdef, `docs/`, primo commit. **(fatto)**
2. M0a.2 Pacchetti: FishNet, Unity Test Framework, Multiplayer Play Mode; pulizia del template. **(fatto)**
3. M0a.3 Scena Boot + NetworkManager + menu Host/Join (Tugboat), avvio automatico in Editor. **(fatto)**
4. M0a.4 Test con 4 istanze, commit, merge, tag `v0.1.0`. **(fatto)**

## Task di M1
1. M1.1 Sistema Stat/Modifier + test.
2. M1.2 Mappa greybox.
3. M1.3 Player FPS replicato (movimento, camera, input, spawn per connessione).
4. M1.4 Pistola hitscan validata dal server + fucile di test, ADS, feedback, bersagli.
5. M1.5 Overlay FPS/RTT.
6. M1.6 Asset realistici: pistola, fucile, braccia FPS, suoni.
