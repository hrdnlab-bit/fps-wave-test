# CONVENZIONI DI PROGETTO

## 1. Git
- Repo: github.com/hrdnlab-bit/fps-wave-test.
- Conventional Commits: `feat`, `fix`, `refactor`, `perf`, `net`, `docs`, `chore`, `test`. Formato: `tipo(ambito): descrizione breve in inglese`, corpo di 1-3 righe con cosa e perché.
  Esempio: `feat(weapons): add hitscan fire with server validation`.
- Un commit per modifica logica. Branch `feature/mN-nome` per ogni milestone, merge su `main` a milestone verificata, tag `v0.N.0`.
- `.gitignore` Unity standard (Library, Temp, Obj, Logs, UserSettings, Build, file di progetto dell'IDE) + Git LFS per texture, modelli, audio, video (`.gitattributes`).
- `CHANGELOG.md` aggiornato a ogni merge.
- Nessun token o credenziale nel repo o in chat.

## 2. Flusso file tra AI e PC
- Progetto Unity sul PC: `C:\Users\manue\FpsWaveTest` (non è un clone git: è la copia di lavoro dell'Editor).
- L'AI lavora su un clone del repo nel proprio ambiente cloud (commit e push da lì) e sincronizza i file nella cartella del PC.
- I file generati dall'Editor sul PC (`.meta`, scene, prefab, ProjectSettings, Packages) vengono riportati nel clone prima di ogni commit.
- L'utente non deve fare commit manuali; se modifica qualcosa nell'Editor lo segnala, così l'AI risincronizza.

## 3. Verifica (ciclo obbligatorio)
1. Compilazione: lettura dell'Editor.log (`%LOCALAPPDATA%\Unity\Editor\Editor.log`) e/o della Console tramite controllo dell'Editor: 0 errori.
2. Test EditMode (Unity Test Framework) per la logica pura.
3. Play Mode: host + client con Multiplayer Play Mode per le funzionalità di rete.
4. Solo dopo: commit, push, aggiornamento dei documenti.

## 4. Struttura cartelle
```
Assets/
  _Project/
    Scripts/        (FpsWave.Runtime.asmdef)
      Core/         (bootstrap, servizi, pooling, RNG, console di debug, GameInfo)
      Net/          (sessione, lobby Steam, transport)
      Player/
      Weapons/
      Enemies/
      Spawning/     (DifficultyModel, SpawnDirector, WaveDefinition, Mutation)
      Loot/
      Stats/        (Stat, Modifier, Mod)
      Map/
      UI/
      Editor/       (FpsWave.Editor.asmdef, quando serve)
    Tests/
      EditMode/  PlayMode/
    Data/           (ScriptableObject: armi, nemici, wave, mutazioni, mod, tabelle drop)
    Prefabs/
    Scenes/         (Boot, MainMenu, Greybox, Game)
    Art/  Audio/  VFX/  Settings/
  ThirdParty/       (asset e plugin esterni, invariati)
docs/               (documenti di progetto)
```

## 5. Codice
- Namespace: `HrdnLab.FpsWave.<Funzionalità>` (es. `HrdnLab.FpsWave.Spawning`).
- Assembly definition: `FpsWave.Runtime`, `FpsWave.Editor`, `FpsWave.Tests.EditMode`, `FpsWave.Tests.PlayMode`.
- Nomi: PascalCase per tipi e metodi, `_camelCase` per campi privati, `[SerializeField] private` invece di campi pubblici.
- Commenti XML sulle API pubbliche; commenti brevi sul "perché", non sul "cosa".
- Configurazione in ScriptableObject, nessun numero magico nel gameplay.
