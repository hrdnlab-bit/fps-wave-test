# BUDGET TECNICI E OTTIMIZZAZIONE

## 1. PC medio di riferimento *(default)*
GPU RTX 3060 / RX 6600, CPU Ryzen 5 3600 / i5-10400, 16 GB RAM, SSD, 1080p, preset grafico "Alto". Il PC host deve reggere anche server + AI.

## 2. Budget *(default, da validare con il profiler)*
| Parametro | Target |
|---|---|
| FPS | ≥ 60 stabili, 1% low ≥ 45 |
| Frame time | ≤ 16.6 ms (Host: ≤ 3 ms per logica server + AI) |
| RTT giocabile | < 100 ms (ottimale < 50 ms) |
| Stress test rete | 150 ms, 2-5% packet loss, jitter 20 ms |
| Tick rate | 30 Hz |
| Banda per client | down ≤ 48 KB/s medi (picco 96), up ≤ 12 KB/s |
| Nemici vivi contemporanei | 40 nell'MVP, obiettivo 80; in endless il tetto non cresce oltre questo valore |
| Batch / SetPass | ≤ 2500 / ≤ 300 |
| Triangoli visibili | ≤ 3 M |
| Memoria texture | ≤ 2.5 GB VRAM |
| GC alloc per frame in gameplay | 0 B (tolleranza: picchi solo a cambio wave) |

Regola: ogni nuova meccanica o asset dichiara l'impatto stimato su rete e FPS prima di essere implementata.

## 3. Ottimizzazione
- Object pooling: proiettili/tracer, nemici, drop, VFX, audio, decal (anche in rete).
- Addressables: preload degli asset della wave successiva durante la preparazione.
- Shader warmup / Shader Variant Collections, SRP Batcher, GPU instancing, LOD, occlusion culling, texture streaming (mipmap streaming).
- Nemici: Animator culling, LOD con riduzione della frequenza di animazione oltre 30 m, AI aggiornata a frequenza ridotta per i nemici lontani.
- Nessuna allocazione in Update: niente LINQ, stringhe o lambda che catturano in gameplay. Job/Burst solo dove il profiler lo giustifica.
- Rete: quantizzazione, delta, frequenza per distanza, messaggi aggregati.
- Player (AIRO): ~75k triangoli a personaggio (4 player = ~0.3 M, 10% del budget), un materiale URP Lit con instancing, skinning a 4 ossa, texture BaseColor/Normal 4K e le altre 2K con mipmap streaming. Rig procedurale ~0.05 ms a personaggio, ogni 3 frame per i remoti oltre 30 m o fuori camera. Manca un LOD del corpo (da fare in Blender se servono più di 4 player o la mappa vasta).
- Partite lunghe (endless): test di "soak" di 60+ minuti per controllare memory leak, crescita del pool e degrado delle prestazioni.

## 4. Misura
Unity Profiler, Frame Debugger, Memory Profiler, statistiche di rete di FishNet; overlay di debug in gioco (FPS, frame time, nemici vivi, banda, RTT).
