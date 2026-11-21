# FPS Wave Test - istruzioni per Claude

FPS co-op PvE a ondate infinite (Unity 6000.6.9f1, URP, FishNet + Steam). Due sviluppatori lavorano sullo stesso repo da PC diversi, ognuno con il proprio Claude.

## All'inizio di ogni sessione
1. Chiedi o ricava con chi stai lavorando (nome usato in `docs/handoff/`). Se non esiste il suo file, crealo da `docs/handoff/_TEMPLATE.md`.
2. `git pull` su `main`, poi leggi in ordine: `docs/HANDOFF.md` (stato comune), `docs/handoff/<persona>.md` (lavoro in corso di questa persona), `docs/LOCKS.md` (chi sta toccando cosa), `docs/GAME_BASELINE.md` (indice).
3. Leggi i documenti tematici del task (mappa in `docs/SYSTEM_PROMPT.md`) e riprendi dai prossimi passi della persona.

## Regole
- Ruolo, autonomia e ciclo di lavoro: `docs/SYSTEM_PROMPT.md`. Convenzioni (git, verifica, cartelle, lavoro in due): `docs/CONVENTIONS.md`.
- Skill del progetto in `.claude/skills/`: `fps-wave-dev-workflow` (ciclo di lavoro), `unity-coop-netcode-review` (rete e performance), `mod-system-balance` (stat, mod, bilanciamento).
- Un branch per lavoro (`<persona>/<argomento>`), su `main` solo con pull request dopo la verifica (compilazione 0 errori, test EditMode, Play Mode host + client se tocca la rete).
- Prima di modificare scene, prefab, asset `Data/` o `ProjectSettings/`, controlla e aggiorna `docs/LOCKS.md`. Non modificare ciò che è bloccato da un altro.
- `docs/` è la fonte di verità: ogni modifica a design, rete, asset o decisioni va nel documento tematico nello stesso branch. Il Progetto claude.ai di Manuel è una copia che aggiorna lui.
- Asset binari di terze parti fuori dal repo (si versionano solo i `.meta`, vedi `docs/ASSETS.md`); su un PC nuovo vanno messi a posto con `tools/setup-fab-assets.ps1` PRIMA di aprire Unity.
- Mai credenziali o token nel repo o in chat.
