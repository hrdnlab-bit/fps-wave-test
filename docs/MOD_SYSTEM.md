# MOD SYSTEM E STATISTICHE

## 1. Sistema Stat/Modifier (base di tutto, si implementa in M1)
- `StatType` enum: Danno, Cadenza, Spread, Rinculo, Caricatore, VelRicarica, CritChance, CritMult, SaluteMax, ScudoMax, RigenScudo, VelMovimento, RaggioMagnete, ecc.
- `StatModifier { StatType, ModOp (Add | AddPercent | Multiply), valore, sorgente }`.
- Formula di calcolo *(default)*: `finale = (base + ΣAdd) * (1 + ΣAddPercent) * ΠMultiply`, con clamp per stat (es. spread ≥ 0, cadenza ≤ limite).
- Stessa pipeline per upgrade dello shop, mod, buff temporanei, debuff dei nemici e **mutazioni** della wave (`SPAWN_DIRECTOR.md`).
- Ricalcolo solo quando cambia l'insieme dei modificatori (cache "dirty"), mai in Update.
- Il server calcola le stat autorevoli; il client le ricalcola localmente per la UI.

## 2. Mod (stile Warframe)
- Ogni arma e il player hanno **slot** (arma: 6, player: 6) e una **capacità** (arma: 30 punti).
- Ogni mod ha: rarità, costo di capacità, livello (0-5), uno o più modificatori; il costo cresce di 1 a ogni livello.
- Mod con **beneficio + debuff** (es. +40% danno / -20% precisione): più forti delle mod pure, a costo di capacità più basso.
- Rarità: comune, rara, epica, leggendaria (le leggendarie hanno effetti speciali, es. "le kill critiche esplodono").
- Upgrade di livello: costa Nuclei, costo crescente `nuclei = 2^livello * moltRarità`.
- **Set bonus**: 2 o 3 mod dello stesso set danno un bonus aggiuntivo.
- Polarità (dopo MVP): slot polarizzati dimezzano il costo delle mod corrispondenti.
- In endless la capacità dell'arma può crescere con upgrade dello shop (+2 per upgrade, max 50), così le build continuano a evolvere nei cicli avanzati.

## 3. Dati
- `ModDefinition` (ScriptableObject): id, nome, rarità, costo base, modificatori per livello, set, tag compatibilità (es. solo shotgun).
- Inventario mod per giocatore sul server; il client invia solo richieste (Installa/Rimuovi/Potenzia), il server valida (possesso, slot, capacità, risorse) e risponde.

## 4. Bilanciamento *(default)*
- Una build "full" a fine ciclo 1 (wave 10) deve dare circa 3x il DPS dell'arma base; a fine ciclo 3 circa 8x. La crescita degli HP dei nemici (`SPAWN_DIRECTOR.md`) è calibrata per superarla lentamente: la partita finisce sempre, prima o poi.
- Nessuna singola mod oltre +100% su una stat.
- Debuff mai superiori al 50% del beneficio relativo.
- Strumento: foglio di calcolo del DPS per build + test EditMode che verificano le formule (skill `mod-system-balance`).
