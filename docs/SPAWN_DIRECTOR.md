# SPAWN DIRECTOR, WAVE ENDLESS E DIFFICOLTÀ

Approccio professionale usato nei co-op PvE (AI Director di Left 4 Dead, Killing Floor 2, Deep Rock Galactic): uno **Spawn Director** sul server che spende un **budget** secondo una curva di intensità, e sceglie punti di spawn **valutati a runtime** rispetto alla posizione dei giocatori. Non si fa spawn fisso "N nemici da questo punto".

## 1. Architettura
```
DifficultyModel (logica pura)      -> quanto è difficile la wave N (stat, budget, mutazioni)
WaveDefinition (ScriptableObject)  -> template di composizione e ritmo
SpawnDirector (solo server)        -> quando spawnare, con quale ritmo
SpawnPointSelector (solo server)   -> dove spawnare
EnemyPool (rete)                   -> come creare/riciclare senza allocazioni
```

## 2. Struttura endless
- Wave infinite, organizzate in **cicli da 10 wave**: wave 1-9 normali, wave 10 boss; poi ciclo 2 (11-20) e così via.
- La difficoltà cresce su **quattro assi**, non solo sul numero:
  1. **Statistiche** dei nemici (HP, danno, velocità, resistenze).
  2. **Composizione**: nuovi tipi sbloccati per wave, più elite, gruppi misti.
  3. **Mutazioni**: modificatori globali che cambiano le regole (sezione 4).
  4. **Ritmo**: picchi più lunghi, pause più brevi.
- Il numero di nemici vivi resta sotto il tetto di performance (`TECH_BUDGETS.md`): oltre una certa wave la difficoltà cresce solo sugli assi 1-4, non sulla quantità contemporanea.

## 3. DifficultyModel *(default, da bilanciare)*
Con `w` = numero di wave, `c = floor((w - 1) / 10)` = ciclo (0 per wave 1-10), `n` = giocatori:
| Grandezza | Formula | Limite |
|---|---|---|
| Budget della wave | `base * (1 + 0.12 * (w - 1)) * [1.0, 1.6, 2.2, 2.8][n-1]` | - |
| HP nemici | `1 + 0.08 * (w - 1) + 0.003 * (w - 1)^2` | - |
| Danno nemici | `1 + 0.04 * (w - 1)` | x4 |
| Velocità nemici | `1 + 0.01 * (w - 1)` | x1.25 |
| Probabilità elite | `0.02 + 0.01 * (w - 8)` da wave 8 | 35% |
| Nemici vivi contemporanei | `min(12 + 2 * w, tetto)` | tetto 40 (MVP) |
| Durata pausa nella curva | `max(8 - 0.3 * w, 3)` s | 3 s |
| Ricompense (crediti, Nuclei) | `* (1 + 0.10 * (w - 1))` | - |

- HP di elite e boss `* (1 + 0.5 * (n - 1))` per numero di giocatori.
- La crescita quadratica degli HP compensa la crescita delle build (mod e upgrade); va verificata con la skill `mod-system-balance`.

## 4. Mutazioni (modificatori globali)
- Dalla wave 5, e poi ogni 5 wave (5, 15, 25... e dopo ogni boss), si aggiunge **una mutazione** permanente scelta a caso da un pool; si accumulano.
- Vengono annunciate nella fase di preparazione precedente e mostrate nell'HUD.
- Esempi:
  - *Carapace*: i nemici hanno 25% di riduzione danni tranne sui weakspot.
  - *Rigenerazione*: i nemici recuperano salute se non colpiti per 3 s.
  - *Instabilità*: i nemici esplodono alla morte (area piccola, telegrafata).
  - *Sciame*: i Runner arrivano in gruppi doppi con -30% HP.
  - *Nebbia tossica*: visibilità ridotta e danno lento fuori dalle zone sicure.
  - *Adattamento*: il tipo di danno più usato dalla squadra nell'ultima wave viene resistito del 20%.
  - *Cacciatori*: gli Stalker prendono di mira chi è più lontano dalla squadra.
- Ogni mutazione è un `MutationDefinition` (ScriptableObject) che applica modificatori tramite il sistema Stat/Modifier o attiva un comportamento.

## 5. Boss (ogni 10 wave)
- Pool di boss a rotazione (MVP: 1 boss; poi 3-4). Il boss del ciclo `c` ha HP e danno scalati come la wave e **una fase in più ogni 2 cicli**.
- Durante la wave boss arrivano solo piccoli gruppi di supporto (budget ridotto al 30%).
- Sconfiggerlo dà Nuclei, una mod garantita di rarità almeno epica, e sblocca la mutazione del ciclo successivo.

## 6. WaveDefinition (dati)
- `pool`: elenco di tipi nemico con peso e wave minima di comparsa.
- `curvaIntensità`: AnimationCurve 0-1 sulla durata della wave (salita -> picco -> breve respiro -> picco finale).
- `template`: normale, a tema (es. solo Stalker), boss. In endless il director sceglie il template in base alla wave; ogni tanto (10%) una wave a tema.

## 7. SpawnDirector (ritmo)
- Gira sul server a tick ridotto (ogni 0.25-0.5 s, non in Update).
- A ogni tick calcola `target = maxContemporanei * curvaIntensità(t)` e, se i vivi sono sotto il target e c'è budget, spawna un **gruppo** (2-6 nemici dello stesso tipo, più credibile di spawn singoli).
- **Pressione per giocatore**: misura per ciascuno il numero di nemici che lo inseguono e il danno subito di recente; manda i nuovi gruppi verso chi ha meno pressione, così chi si allontana dalla squadra viene "trovato".
- **Fase di respiro**: se la squadra è in difficoltà (molti a terra, salute media bassa) il director rallenta per qualche secondo; è il "relax" del modello L4D.
- Fine wave = budget esaurito e nemici vivi = 0.

## 8. SpawnPointSelector (dove)
Punti candidati = **nodi di spawn** piazzati dalla generazione della mappa (bordi delle zone, grotte, condotti, porte del complesso, tombini, linea degli alberi), con tag di zona e tipo (terra, tetto, tana, aria).

Per ogni gruppo:
1. Filtra i nodi in un **anello** attorno al giocatore bersaglio: distanza 25-60 m (mai sotto 15 m).
2. Scarta i nodi **visibili** da qualunque giocatore (raycast verso la testa dei player; budget di 8-16 raycast per tick, distribuiti su più frame).
3. Verifica **raggiungibilità**: `NavMesh.SamplePosition` + percorso valido verso il bersaglio (in cache, ricalcolato ogni pochi secondi).
4. Assegna un punteggio: vicinanza alla distanza ideale (~40 m), arrivo dalle spalle o dai fianchi rispetto alla direzione di sguardo, varietà rispetto agli ultimi spawn, compatibilità tra tipo nemico e tipo nodo.
5. Sceglie a caso pesato tra i migliori 3 (imprevedibilità).
6. Se nessun nodo è valido: usa il miglior nodo "nascosto" a distanza maggiore, o rimanda il gruppo al tick successivo.

## 9. Presentazione e performance
- Spawn **scaglionato**: al massimo 2-3 nemici per frame, per evitare picchi di CPU e di rete.
- Telegrafato (dopo MVP): emersione dal terreno, uscita da condotti, suono direzionale.
- **Riciclo**: un nemico troppo lontano da tutti (> 90 m) o bloccato da più di 8 s viene ridespawnato più vicino.
- **Pooling** di rete: i nemici morti tornano nel pool del server; i client ricevono spawn/despawn come messaggi leggeri (vedi `NETCODE.md`).

## 10. Mappa vasta + ondate
- Durante la **wave** il combattimento segue la squadra: il director spawna attorno ai giocatori, ovunque si trovino.
- Durante la **preparazione** si esplora liberamente: risorse, casse, mod; la stazione di upgrade/shop compare vicino alla squadra (o una per zona).
- Dopo l'MVP: wave "obiettivo" (difendi un punto, scorta) che ancorano lo scontro a un luogo.

## 11. Rete
- Tutto gira solo sul server; nessuna parte del director esiste sui client.
- I client ricevono: stato della wave (numero, nemici rimasti, timer, mutazioni attive) tramite variabili sincronizzate a bassa frequenza, e gli spawn come oggetti di rete.

## 12. Test
- EditMode: formule del DifficultyModel, budget, scaling, selezione pesata, accumulo delle mutazioni, filtri dell'anello (logica pura, senza scena).
- Debug in gioco: gizmo dei nodi (verde valido, rosso visibile, grigio irraggiungibile), overlay con budget residuo, intensità e pressione per giocatore, comandi console `wave.skip`, `wave.set N`, `spawn <tipo> <n>`, `mutation.add <id>`.

## 13. Stato dell'implementazione (M2, 2026-11-21)
- Codice in `Scripts/Enemies/`: `EnemyDirector` (tick, visibilità, spawn, riciclo, AI a turno), `SpawnScoring` (logica pura dei §8: anello, punteggio, migliori 3, scelta pesata; coperta da test), `EnemySpawnNode`, `EnemyAgent`, `EnemyTarget`, `EnemyVisual`.
- Fatto: anello 25-60 m (mai sotto 15 m da nessun player), nodi visibili scartati (2 raggi per player verso testa e gambe, 32 raggi per frame, cache 1.2 s, conferma prima dello spawn), percorso verificato con `NavMesh.CalculatePath`, punteggio con distanza ideale 40 m, bonus spalle/fianchi e penalità di varietà (10 s), scelta pesata tra i migliori 3, ripiego su nodi nascosti più lontani; gruppi 2-4 verso il player con meno inseguitori; spawn scaglionato (2 per frame); riciclo oltre 90 m o bloccati da 8 s.
- Semplificato fino a M3: niente budget né curva di intensità; numero di nemici vivi = 4 + 4 per player (tetto 40), nuovo gruppo ogni 2.5 s se si è sotto il numero.
- Debug: F8 sull'Host attiva/disattiva gli spawn; riga in basso a sinistra (vivi/obiettivo, uccisi, riciclati); gizmo dei nodi selezionando `M2_Enemies` in Play (verde valido, rosso visibile, grigio fuori NavMesh).
