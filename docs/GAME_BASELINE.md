# GAME BASELINE - FPS Wave Test

Versione: 0.3 | Ultimo aggiornamento: 2026-11-21

> Indice e fonte di verità di alto livello. I dettagli sono nei documenti tematici (vedi tabella in `SYSTEM_PROMPT.md`). I valori marcati *(default)* sono stati scelti in autonomia dall'AI e sono modificabili: ogni scelta è motivata in `DECISIONS.md`.

## 1. Visione
FPS in prima persona, cooperativo PvE a ondate infinite, 1-4 giocatori, grafica realistica PBR. I nemici uccisi rilasciano risorse per potenziare personaggio, armi ed equipaggiamento, con un sistema di mod stile Warframe. Tra una wave e l'altra c'è una fase di preparazione. La mappa è vasta, divisa in zone riconoscibili, con elementi posizionati in modo randomizzato ma controllato.

## 2. Pilastri di design
1. Gunplay soddisfacente e leggibile.
2. Ciclo combatti -> raccogli -> potenzia -> combatti, ben ritmato.
3. Build-crafting profondo tramite mod.
4. Co-op fino a 4 giocatori, ci si salva a vicenda.
5. Mappa vasta e varia che influenza le scelte tattiche.
6. Difficoltà che cresce per qualità, non solo per quantità: nemici più forti, nuove meccaniche e mutazioni a ogni ciclo.

## 3. Parametri base
| Parametro | Valore |
|---|---|
| Genere | FPS co-op PvE a ondate infinite |
| Giocatori | 1-4, listen server (Host + Client) via Steam |
| Ambientazione | Valle montana in quarantena con un complesso militare di ricerca sci-fi: natura e villaggio abbandonato + installazioni militari high-tech. Nemici: creature mutate dall'esperimento, con varianti bio-meccaniche (scelta utente: mix delle opzioni "valle in quarantena" e "militare sci-fi") |
| Tono | Serio, teso, militare; non horror puro |
| Piattaforma | PC Windows. Steam Deck: obiettivo secondario (controller supportato da subito) |
| Struttura partita | **Wave infinite**, difficoltà crescente a ogni wave (statistiche, composizione, mutazioni), **boss ogni 10 wave**. Si gioca per arrivare il più lontano possibile (record per wave raggiunta). Dettagli in `SPAWN_DIRECTOR.md` |
| Progressione | *(default)* MVP: solo dentro la partita (run-based). Meta-progressione persistente dopo M8 |
| Grafica | Realistica PBR, URP |
| Engine | Unity 6000.6.9f1 |
| Netcode | FishNet + FishySteamworks (vedi `NETCODE.md`) |
| Progetto | `C:\Users\manue\FpsWaveTest`, repo `hrdnlab-bit/fps-wave-test` |

## 4. Sintesi dei sistemi (dettagli nei documenti)
- Player, armi, nemici, co-op, risorse, fase di fine wave -> `DESIGN_GAMEPLAY.md`
- Wave, difficoltà endless e spawn -> `SPAWN_DIRECTOR.md`
- Mod e statistiche -> `MOD_SYSTEM.md`
- Mappa e zone -> `MAP_ZONES.md`
- Rete -> `NETCODE.md`
- Budget e ottimizzazione -> `TECH_BUDGETS.md`
- Asset -> `ASSETS.md`

## 5. Condizioni di fine partita
- Nessuna vittoria finale: la partita continua finché la squadra resiste.
- Sconfitta (wipe): tutti i giocatori a terra o morti contemporaneamente. Schermata finale con wave raggiunta, kill, danni, mod usate.
- Traguardi intermedi: ogni boss sconfitto (wave 10, 20, 30...) è un "checkpoint" di prestigio mostrato nel riepilogo.

## 6. Fuori scope per l'MVP
PvP, prediction/rollback, lag compensation completa, host migration, meta-progressione, classi, salvataggio a metà partita, mod-tool per utenti, localizzazione oltre IT/EN.
