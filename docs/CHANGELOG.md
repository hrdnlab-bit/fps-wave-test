# CHANGELOG

Formato: [Keep a Changelog](https://keepachangelog.com/it/1.1.0/), versioni semantiche.

## [Unreleased]

## [0.1.0] - 2026-11-21
### Added
- FishNet 4.7.4, Multiplayer Play Mode 3.0.0, Test Framework 1.8.0.
- Scena `Boot` con NetworkManager + Tugboat e menu di sviluppo Host/Join; avvio automatico in Editor (Host sull'istanza principale, join dei giocatori virtuali).
- Menu `FpsWave/Setup` (creazione scena Boot, rimozione residui del template).
- Mirror della Console in `Logs/FpsWaveConsole.log`.
- Assembly di test EditMode con i primi test.
- Testato: Host + 3 client locali.

### Removed
- Contenuti dimostrativi del template URP (TutorialInfo, Readme, SampleScene).

## [0.0.1] - 2026-11-21
### Added
- Progetto Unity 6000.6.9f1 (template Universal 3D) in `C:\Users\manue\FpsWaveTest`.
- `.gitignore` Unity, `.gitattributes` con Git LFS, struttura `Assets/_Project`, asmdef `FpsWave.Runtime`, `GameInfo` (versione e protocollo).
- Documenti di progetto in `docs/`, divisi per tema.
- Design endless: DifficultyModel, mutazioni, boss ogni 10 wave.

### Changed
- SYSTEM_PROMPT snellito: solo ruolo, autonomia, ciclo di lavoro, handoff.
- GAME_BASELINE ridotto a visione, pilastri, parametri e indice.
- Modello di rete: da PvP (prediction/lag compensation) a PvE MVP.
- Roadmap: percorso ibrido (M0a -> M1 -> M0b), sistema di stat in M1, mappa greybox da M1, mappa procedurale in M7.
- Ambientazione: valle in quarantena + complesso militare di ricerca sci-fi.
