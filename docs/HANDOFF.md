# HANDOFF

Ultimo aggiornamento: 2026-11-21

## Stato attuale
- Milestone corrente: **M0a** (setup + rete locale). Completato M0a.1 (setup repo).
- Progetto Unity: `C:\Users\manue\FpsWaveTest` (Universal 3D, Unity 6000.6.9f1), cartella collegata alla sessione AI.
- Repo: `hrdnlab-bit/fps-wave-test`, primo commit con progetto base, `.gitignore`, LFS, struttura `Assets/_Project`, asmdef `FpsWave.Runtime`, `docs/`.
- Clone di lavoro dell'AI: `/home/claude/fps-wave-test` (ambiente cloud).
- `C:\Users\manue\My project` contiene un ALTRO gioco (SABBIA): non toccarlo.
- Scelte dell'utente del 2026-11-21: percorso ibrido, ambientazione valle in quarantena + complesso militare sci-fi, endless con boss ogni 10 wave, test Steam con un amico.

## Commit recenti
- `chore: initial Unity project setup` (progetto base, git, LFS, struttura, docs)

## Problemi aperti
- I nuovi file/cartelle creati dall'AI sul PC aspettano che Unity generi i `.meta`: vanno riportati nel repo al prossimo commit.
- Il template contiene ancora `TutorialInfo/`, `Readme.asset`, `SampleScene` e i renderer Mobile: da rimuovere in M0a.2.
- Domande da fare prima di M1: vedi `OPEN_QUESTIONS.md` #7-#8.

## Prossimi passi
1. M0a.2: aggiungere FishNet (UPM da GitHub), Unity Test Framework, Multiplayer Play Mode; pulire il template; impostare il renderer PC come unico.
2. M0a.3: scena Boot con NetworkManager + Tugboat e menu minimo Host/Join.
3. M0a.4: test con 2-4 istanze (Multiplayer Play Mode), merge, tag `v0.1.0`.
