# FPS Wave Test

FPS cooperativo PvE a ondate infinite (1-4 giocatori, Steam), grafica realistica, sistema di mod stile Warframe.

- Engine: Unity 6000.6.9f1, URP
- Netcode: FishNet (Tugboat in locale, FishySteamworks su Steam)
- Documentazione: cartella [`docs/`](docs/), partendo da [`docs/HANDOFF.md`](docs/HANDOFF.md) e [`docs/GAME_BASELINE.md`](docs/GAME_BASELINE.md)

## Aprire il progetto (PC nuovo)
Guida completa: [`docs/GUIDA_COLLABORATORE.md`](docs/GUIDA_COLLABORATORE.md). In breve:
1. Installare Git, Steam e Unity 6000.6.9f1, poi clonare il repo.
2. Scaricare da Fab i due pacchetti gratuiti indicati in `docs/ASSETS.md` ed eseguire
   `powershell -ExecutionPolicy Bypass -File tools\setup-fab-assets.ps1` e `tools\setup-ui-assets.ps1` **prima** di aprire Unity (poi `FpsWave/Setup/7`).
3. Unity Hub -> Add -> cartella del repo. Play da qualsiasi scena (parte da `Boot`).

## Lavoro in due
Branch `<persona>/<argomento>`, pull request verso `main`, lock su scene e prefab in `docs/LOCKS.md`,
stato personale in `docs/handoff/`. Istruzioni per Claude in [`CLAUDE.md`](CLAUDE.md). Dettagli in `docs/CONVENTIONS.md`.
