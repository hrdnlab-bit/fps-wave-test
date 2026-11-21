---
name: "mod-system-balance"
description: "Formule e controlli di bilanciamento per stat, modificatori e mod stile Warframe di FPS Wave Test; usala quando si toccano armi, upgrade, mod o drop."
---

# Bilanciamento Stat, Modifier e Mod

Riferimento: `MOD_SYSTEM.md`, `DESIGN_GAMEPLAY.md`, `SPAWN_DIRECTOR.md`.

## Formula unica
`finale = (base + somma Add) * (1 + somma AddPercent) * prodotto Multiply`, poi clamp per stat.
- Stessa pipeline per upgrade dello shop, mod, buff e debuff.
- Ricalcolo solo quando cambia l'insieme dei modificatori.
- Server autorevole; il client ricalcola solo per la UI.

## DPS di riferimento
`DPS = danno * cadenza * (1 + critChance * (critMult - 1)) * fattoreRicarica`
con `fattoreRicarica = tempoCaricatore / (tempoCaricatore + tempoRicarica)` e `tempoCaricatore = caricatore / cadenza`.
Il weakspot si valuta a parte (ipotesi di 30% di colpi alla testa).

## Vincoli
- Build completa di fine partita: 3-4x il DPS dell'arma base.
- Nessuna singola mod oltre +100% su una stat.
- Debuff di una mod mai oltre il 50% del beneficio relativo.
- Costo mod: costo base + livello; capacità arma 30.
- Upgrade livello mod: `nuclei = 2^livello * moltRarità` (comune 1, rara 2, epica 3, leggendaria 5).
- TTK del nemico base col fucile d'assalto base: 0.4-0.7 s alla wave 1; verifica che con lo scaling HP `* (1 + 0.08 * (wave - 1))` e gli upgrade attesi resti sotto 1.2 s alla wave 10.

## Controlli obbligatori quando cambi numeri
1. Aggiorna o aggiungi test EditMode che verificano la formula e i clamp.
2. Ricalcola il DPS di: arma base, build tipica a metà partita, build massima. Riporta i tre valori nel commit o in `DECISIONS.md`.
3. Verifica che nessuna combinazione di 2 mod dello stesso set superi i vincoli.
4. Controlla che le ricompense per wave (crediti, Nuclei) permettano la progressione attesa: circa 1 upgrade arma o 1 livello mod per wave.