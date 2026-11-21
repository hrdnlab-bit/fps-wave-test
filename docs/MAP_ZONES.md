# MAPPA E ZONE

## 1. Struttura
- Mappa ~600 x 600 m *(default MVP)*, completamente esplorabile, con un'area centrale di partenza (avamposto militare).
- Il terreno di base e i landmark principali sono **fatti a mano** (Terrain di Unity + pezzi modulari); props, risorse, casse, cover, nodi di spawn e POI minori sono **generati con seed** secondo regole.
- Fino a M7 si usa una mappa **greybox** fissa per testare gameplay e spawn.

## 2. Zone *(default)*
| Zona | Caratteristica unica | Effetto tattico | Risorsa tipica |
|---|---|---|---|
| Foresta | Visibilità ridotta, nebbia | Nemici che arrivano all'improvviso, scontri ravvicinati | Materiali organici |
| Cava | Verticalità, terrazze di roccia | Posizioni rialzate, cadute | Minerali (materiali) |
| Villaggio abbandonato | Molta cover, interni | Corridoi e colli di bottiglia | Casse di crediti |
| Complesso di ricerca militare (sci-fi) | Pericoli ambientali (gas, campi elettrici, torrette danneggiate), interni high-tech | Trappole utilizzabili contro i nemici, porte e condotti da cui escono i nemici | Nuclei |

- Ogni zona ha una palette di luce e post-processing propria (volume locale) per essere riconoscibile a colpo d'occhio.
- Landmark visibili da lontano (torre di comunicazione del complesso, ciminiera, diga) per l'orientamento.
- Coerenza tra naturale e sci-fi: il complesso usa materiali realistici (cemento, metallo verniciato, vetro) con pochi elementi luminosi; niente estetica "neon arcade".

## 3. Generazione con seed
- L'Host sceglie il seed e lo invia ai client all'ingresso; tutti generano lo **stesso layout** in locale (niente replica oggetto per oggetto).
- Regole: pesi per zona, distanza minima tra oggetti, pendenza massima, esclusione da strade e da raggio dei landmark, densità per categoria.
- Determinismo:
  - RNG proprio (es. xorshift/PCG) con sotto-seed per zona e categoria: cambiare una categoria non sposta le altre;
  - mai `UnityEngine.Random`, mai fisica o ordine di iterazione non deterministico per il posizionamento;
  - stesso build tra Host e client (controllo di versione al join).
- Oggetti interattivi (casse, nodi risorsa) hanno un id deterministico derivato dal seed: il server ne replica solo lo stato (aperta/raccolta).
- In endless i nodi risorsa si **ricaricano** ogni N wave, così l'esplorazione resta utile.
- NavMesh: costruita a runtime **solo sull'Host** (NavMeshSurface) dopo la generazione; i client non ne hanno bisogno.

## 4. Performance
- Pezzi modulari con LOD e GPU instancing; occlusion culling sui pezzi statici fatti a mano; per i generati, culling per distanza e per zona.
- Vegetazione via terrain details/instancing.
