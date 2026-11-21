# DESIGN GAMEPLAY

Valori *(default)* = scelti dall'AI, da bilanciare con i playtest.

## 1. Player
- Feel: **tattico-arcade** (riferimenti Killing Floor 2, Deep Rock Galactic): movimento rapido e reattivo, rinculo marcato ma controllabile, niente stamina.
- Movimento: camminata 4.5 m/s, sprint 7 m/s, crouch 2.5 m/s, salto 1.2 m, scivolata da sprint (dopo MVP).
- Camera prima persona, FOV regolabile 70-110 (default 90).
- Salute 100 + scudo 50 che si rigenera dopo 4 s senza danni (tuta militare sperimentale, coerente con l'ambientazione).
- Statistiche modificabili tramite il sistema Stat/Modifier (`MOD_SYSTEM.md`).
- Classi: nessuna nell'MVP. I ruoli emergono da armi e mod.
- Input: nuovo Input System, tastiera/mouse e controller, rimappatura tasti.

## 2. Co-op: a terra, rianimazione, morte
- A 0 HP il giocatore va **a terra**: può strisciare e usare la pistola, 30 s prima di morire.
- Un compagno lo rianima tenendo premuto Interagisci per 3 s (rianimato con 30% HP).
- Un giocatore morto è spettatore e rientra all'inizio della fase di fine wave.
- Wipe = tutti a terra o morti -> fine partita.
- Fuoco amico: disattivato.

## 3. Armi
- Stile: militare moderno/near-future (realistiche, con dettagli tecnologici), coerenti con il complesso di ricerca.
- Categorie MVP: pistola, fucile d'assalto, shotgun. Dopo MVP: sniper, SMG, lanciagranate, armi sperimentali (energia) come drop rari.
- Tutte hitscan nell'MVP; proiettili fisici solo per esplosivi ed energia (dopo MVP).
- Statistiche: danno, cadenza, spread, rinculo (pattern + recupero), caricatore, ricarica, chance critico, moltiplicatore critico, moltiplicatore weakspot (testa 2x).
- **ADS** (tasto destro): zoom leggero (FOV -15/-25% secondo l'arma), spread ridotto del 60%, rinculo ridotto del 30%, movimento al 60%, transizione 0.15-0.3 s per arma. Hip-fire comunque utilizzabile a corta distanza.
- **Inizio partita: solo pistola.** La primaria (fucile d'assalto, shotgun, poi le altre) si compra allo shop dalla prima fase di preparazione; la pistola base deve quindi bastare per la wave 1 (TTK del Runner con la pistola ~0.6-0.9 s).
- Inventario: 1 primaria + 1 secondaria (pistola) + granate (dopo MVP).
- Feedback: rinculo camera, muzzle flash, tracer, decal di impatto, hit marker (bianco = colpo, rosso = kill, giallo = critico), suono di impatto distinto.
- TTK target *(default)*: nemico base in 0.4-0.7 s col fucile d'assalto base, 0.6-0.9 s con la pistola base, alla wave 1.

## 4. Nemici
| Tipo | Ruolo | HP base | Velocità | Costo budget | Compare dalla wave |
|---|---|---|---|---|---|
| Runner | Massa, corpo a corpo | 60 | 6 m/s | 1 | 1 |
| Brute (tank) | Spugna di danni, carica | 600 | 3.5 m/s | 6 | 3 |
| Spitter (ranged) | Attacco a distanza, costringe a muoversi | 120 | 4 m/s | 3 | 4 |
| Stalker (veloce) | Fiancheggia, isola i giocatori | 90 | 8 m/s | 4 | 6 |
| Drone bio-meccanico | Volante, disturba, esplode | 50 | 7 m/s | 3 | 12 |
| Elite | Variante potenziata con affisso (corazza, rigenerazione, esplosione) | x3 | - | x4 | 8 |
| Boss | Ogni 10 wave, a rotazione, con fasi multiple | - | - | - | 10, 20, 30... |

- AI solo sull'Host: state machine (Idle -> Chase -> Attack -> Stagger -> Dead), NavMeshAgent.
- Ogni nemico ha una tabella di drop pesata (vedi sezione 5).
- Scaling endless, mutazioni e boss: vedi `SPAWN_DIRECTOR.md`.
- MVP: Runner e Brute (M2), poi gli altri.

## 5. Risorse e drop
| Risorsa | Fonte | Uso | Condivisione |
|---|---|---|---|
| Crediti | Tutti i nemici | Acquisti allo shop | *(default)* Condivisi di squadra: chi raccoglie riempie la cassa comune |
| Materiali | Nemici e nodi nella mappa | Upgrade base di armi/player | Condivisi di squadra |
| Nuclei (rari) | Elite, boss, casse | Livelli delle mod | Per giocatore |
| Mod | Elite, boss, casse | Installazione | *(default)* Drop istanziato: ogni giocatore vede il proprio |

- Drop fisici con magnetismo (raggio 3 m) e scomparsa dopo 60 s (lampeggiano negli ultimi 10 s).
- Tabelle pesate in ScriptableObject; il server decide il drop, i client vedono solo l'esito.
- In endless le ricompense crescono con la wave (vedi `SPAWN_DIRECTOR.md`), così la progressione tiene il passo con la difficoltà.

## 6. Fase di fine wave
- Durata 60 s *(default)*, 90 s dopo una wave boss. Timer visibile.
- Si può: raccogliere i drop rimasti, usare la stazione di upgrade, comprare allo shop, installare/rimuovere mod, rianimare i morti (rientrano automaticamente).
- Viene mostrata l'eventuale nuova mutazione della wave successiva.
- Avvio anticipato: tutti i giocatori vivi premono "Pronto" (unanimità).
- Durante la preparazione si fa il preload degli asset della wave successiva.

## 7. UI/UX
- HUD: salute/scudo, munizioni, wave e nemici rimasti, mutazioni attive, crediti di squadra, bussola con indicatori dei compagni e degli obiettivi, indicatore di compagno a terra.
- Menu: upgrade, mod, shop, lobby Steam (inviti, join), impostazioni (grafica, audio, sensibilità, FOV, rimappatura).
- Schermata di fine partita: wave raggiunta, record personale e di squadra, statistiche.
- Lingue: IT ed EN.
