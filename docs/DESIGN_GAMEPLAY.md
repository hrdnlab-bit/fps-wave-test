# DESIGN GAMEPLAY

Valori *(default)* = scelti dall'AI, da bilanciare con i playtest.

## 1. Player
- Feel: **tattico-arcade** (riferimenti Killing Floor 2, Deep Rock Galactic): movimento rapido e reattivo, rinculo marcato ma controllabile, niente stamina.
- Movimento: camminata 4.5 m/s, sprint 7 m/s, crouch 2.5 m/s, salto 1.2 m, scivolata da sprint (dopo MVP).
- Camera prima persona, FOV regolabile 70-110 (default 90).
- Salute 100 + scudo 50 che si rigenera dopo 4 s senza danni (tuta militare sperimentale, coerente con l'ambientazione). *(scudo non ancora fatto)*
- **Tuta Mk-VII** (fatta, Fase 1): energia 100 che si ricarica (14/s dopo 1.5 s) e tre abilità: Pulse scan (Q, 25, nemici entro 40 m rivelati a tutta la squadra per 5 s, ricarica 6 s), Armor mode (F, 40, danni -60% per 4 s, movimento -15%), Overcharge (V, 30, corsa +35% per 3.5 s). Le mod potranno modificarle.
- **Ping di squadra** (tasto centrale o Z): punto o nemico, visibile a tutti con la distanza per 7 s.
- Statistiche modificabili tramite il sistema Stat/Modifier (`MOD_SYSTEM.md`).
- Classi: nessuna nell'MVP. I ruoli emergono da armi e mod.
- Input: nuovo Input System, tastiera/mouse e controller, rimappatura tasti.

## 2. Co-op: a terra, rianimazione, morte
- A 0 HP il giocatore va **a terra**: può strisciare e usare la pistola, 30 s prima di morire. *(fatto, `PlayerLife`)*
- Rianimazione con il "tether" della tuta: chi rianima e chi viene rianimato ricevono uno scudo breve (danni -50% per 3 s).
- Temporaneo finché non ci sono le wave: da soli o tutti a terra ci si rialza dopo 6 s; da morti si torna dopo 10 s.
- Un compagno lo rianima tenendo premuto Interagisci per 3 s (rianimato con 30% HP).
- Un giocatore morto è spettatore e rientra all'inizio della fase di fine wave.
- Wipe = tutti a terra o morti -> fine partita.
- Fuoco amico: disattivato.

## 3. Armi
- Stile: militare moderno/near-future (realistiche, con dettagli tecnologici), coerenti con il complesso di ricerca.
- Categorie MVP: pistola, fucile d'assalto, shotgun. Dopo MVP: sniper, SMG, lanciagranate, armi sperimentali (energia) come drop rari.
- Tutte hitscan nell'MVP; proiettili fisici solo per esplosivi ed energia (dopo MVP).
- Statistiche: danno, cadenza, spread, rinculo (pattern + recupero), caricatore, ricarica, chance critico, moltiplicatore critico, moltiplicatore weakspot (testa 2x).
- **Gunplay di riferimento: Call of Duty moderno.** **ADS** (tasto destro): zoom leggero (FOV -15/-20%), arma precisa (niente bloom), tutto il rinculo (salita che si compensa col mouse, parte che torna da sola lasciando il grilletto), movimento al 60%, transizione morbida 0.17-0.24 s. **Hip-fire**: la mira sale appena, la dispersione cresce sparando, muovendosi e in aria (cala accovacciati). **Sprint-to-fire**: sparare o mirare interrompe la corsa, 0.13-0.22 s per rialzare l'arma. **Flinch** quando si viene colpiti.
- **Inizio partita: solo pistola.** La primaria (fucile d'assalto, shotgun, poi le altre) si compra allo shop dalla prima fase di preparazione; la pistola base deve quindi bastare per la wave 1 (TTK del Runner con la pistola ~0.6-0.9 s).
- Inventario: 1 primaria + 1 secondaria (pistola) + granate (dopo MVP).
- Feedback: rinculo camera, muzzle flash, tracer, decal di impatto (solo sull'ambiente; sui nemici uno schizzo breve), hit marker (bianco = colpo, rosso = kill, giallo = critico), **numeri di danno** sobri sul punto colpito (bianco normale, oro tenue per i colpi di precisione; solo per chi spara, confermati dal server), traccianti a scia che viaggiano alla velocità del proiettile (al tiratore 1 colpo su 3 delle automatiche), suono di impatto distinto *(non ancora: suoni tolti per ora)*.
- **Sensazione delle armi** (uguale per tutte, ritoccabile per arma: `WeaponFeel` nella scheda dell'arma): rinculo che scatta e torna a molla con una parte permanente da compensare, schema orizzontale riconoscibile per arma, primo colpo più forte e crescita nella raffica, rinculo ridotto mirando (~35%) e accovacciati; arma in mano a molla (calcio, oscillazione che segue il mouse, passo, corsa, inclinazione laterale, atterraggio, respiro); tremore e punch del FOV sotto fuoco prolungato. Armi a energia (`UsesHeat`): colore dell'energia per luce e traccianti e calore cosmetico con barra nel HUD; le armi a proiettili usano colori caldi realistici e niente calore.
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
- **Brute** (fatto, Fase 1): corazza a piastre (petto 150, schiena 120, spalle 80, elmo 90 HP) che assorbono l'85% del danno e si rompono con detriti, scoprendo il **nucleo luminoso** sul petto (punto debole). **Carica**: tra 6 e 16 m con il bersaglio in vista si prepara 0.75 s, scatta a 12 m/s per al massimo 1.3 s, 30 danni e spintone a chi prende, poi resta fermo 1.1 s (finestra per colpire il nucleo); ricarica 7 s. Le piastre sono un sistema generico (`EnemyArmor`) riusabile da elite e boss.

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
- HUD (fatto in parte, `SciFiHud`, stile minimale alla Crysis): salute/scudo, munizioni, wave e nemici rimasti, mutazioni attive, crediti di squadra, bussola con indicatori dei compagni e degli obiettivi, indicatore di compagno a terra.
- Menu (stile "terminale" ambra, `GameMenu`): fatti principale, schieramento con lobby Steam (crea, codice, inviti) e LAN, pausa, impostazioni (sensibilità, FOV, volume, schermo intero, VSync, qualità, FPS, numeri di danno). Da fare: upgrade, mod, shop, rimappatura dei tasti.
- Schermata di fine partita: wave raggiunta, record personale e di squadra, statistiche.
- Lingua del gioco: inglese (documenti e codice in italiano).
