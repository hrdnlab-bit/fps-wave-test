---
name: "fps-wave-dev-workflow"
description: "Ciclo di lavoro autonomo per il gioco Unity FPS Wave Test: leggere i documenti, implementare, verificare, committare e sincronizzare docs e handoff."
---

# FPS Wave Test - workflow di sviluppo autonomo

Usa questa skill per ogni task di sviluppo del progetto FPS Wave Test (Unity 6, FishNet, co-op PvE a ondate).

## 0. Autonomia
- Procedi senza chiedere conferma per design di dettaglio, codice, scene, prefab, asset gratuiti con licenza compatibile, commit e push.
- Registra ogni scelta non banale in `DECISIONS.md` (data, decisione, motivo, alternative).
- Fermati solo per: spese, login/credenziali (le inserisce l'utente), azioni irreversibili fuori dal progetto, contraddizioni coi pilastri del baseline.

## 1. Prima di iniziare
0. Lavoro in due (vedi `docs/CONVENTIONS.md`, sezione "Lavoro in due"): `git pull` su `main`, poi leggi `docs/LOCKS.md` e il tuo file in `docs/handoff/`. Non modificare file bloccati da altri; blocca in `LOCKS.md` scene, prefab e asset condivisi prima di toccarli.
1. Leggi `HANDOFF.md`, poi `GAME_BASELINE.md`, poi i documenti tematici pertinenti (DESIGN_GAMEPLAY, SPAWN_DIRECTOR, MOD_SYSTEM, MAP_ZONES, NETCODE, TECH_BUDGETS, ASSETS, CONVENTIONS, ROADMAP).
2. Leggi il codice esistente della funzionalità che tocchi.
3. Scrivi in 2-4 righe: obiettivo, file toccati, impatto stimato su rete e FPS rispetto a TECH_BUDGETS. Se sfora, scegli l'alternativa più leggera.

## 2. Implementazione
- Struttura e namespace da CONVENTIONS (`HrdnLab.FpsWave.<Feature>`, cartelle in `Assets/_Project/`).
- Logica pura (formule, tabelle, director) in classi C# senza MonoBehaviour, con test EditMode scritti prima o insieme al codice.
- Configurazione in ScriptableObject, niente numeri magici.
- Zero allocazioni in Update; pooling per tutto ciò che nasce e muore spesso.
- Passi piccoli: un commit per modifica logica.

## 3. Verifica (non dichiarare finito senza)
1. Compilazione: leggi `Logs/FpsWaveConsole.log` nella cartella del progetto (mirror della Console, azzerato a ogni compilazione) o la Console dell'Editor. Obiettivo: 0 errori, nessun warning nuovo.
2. Test EditMode verdi.
3. Se il task tocca la rete: Play Mode con Multiplayer Play Mode (host + almeno 1 client), comportamento identico su entrambi.
4. Se il task tocca la performance: controllo col profiler o con l'overlay di debug.
5. Se una verifica fallisce: individua la causa con un'ipotesi alla volta (riproduci, isola, correggi, riverifica). Non applicare fix a caso.

## 4. Commit e push
- Conventional Commits in inglese: `tipo(ambito): descrizione`, tipi feat, fix, refactor, perf, net, docs, chore, test. Corpo di 1-3 righe con cosa e perché.
- Prima del commit riporta nel clone i file generati dall'Editor sul PC (.meta, scene, prefab, ProjectSettings).
- Branch `<persona>/<argomento>` (es. `manuel/m2-enemies`), un branch per lavoro; su `main` si entra solo con una pull request (`gh pr create`), dopo la verifica. Prima della PR: `git pull --rebase origin main` e nuova verifica.
- Mai credenziali nel repo.

## 5. Documenti
- Ogni informazione vive in un solo documento. Aggiorna quello tematico toccato, `CHANGELOG.md` e `HANDOFF.md`.
- `docs/` nel repo è la fonte di verità. Se la sessione è collegata al Progetto claude.ai, aggiorna anche la copia nel Progetto; altrimenti basta il repo.
- Il tuo stato di lavoro va nel tuo file `docs/handoff/<persona>.md`; `HANDOFF.md` contiene solo lo stato comune (aggiornalo nella PR quando cambia per tutti). Rimuovi i tuoi lock da `LOCKS.md` a fine lavoro.

## 6. Fine milestone / handoff
- Aggiorna `HANDOFF.md`: stato, milestone completate, commit recenti, decisioni, problemi aperti, prossimi passi, script chiave.
- In chat: riepilogo breve, come provarla, problemi noti, prossimo passo. Non incollare file interi.