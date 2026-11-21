# CONVENZIONI DI PROGETTO

## 1. Git
- Repo: github.com/hrdnlab-bit/fps-wave-test.
- Conventional Commits: `feat`, `fix`, `refactor`, `perf`, `net`, `docs`, `chore`, `test`. Formato: `tipo(ambito): descrizione breve in inglese`, corpo di 1-3 righe con cosa e perché.
  Esempio: `feat(weapons): add hitscan fire with server validation`.
- Un commit per modifica logica. Branch `<persona>/<argomento>` (es. `manuel/m2-enemies`); su `main` si entra solo con una **pull request** dopo la verifica (`main` è protetto: niente push diretti né force push per i collaboratori). Versioni in `CHANGELOG.md` (i tag non si pubblicano dall'ambiente AI).
- `.gitignore` Unity standard (Library, Temp, Obj, Logs, UserSettings, Build, file di progetto dell'IDE) + esclusione dei binari in `Assets/ThirdParty/` (si versionano solo i `.meta` e i file di testo, vedi `ASSETS.md`). `.gitattributes` con LFS resta come rete di sicurezza ma non si usa.
- `CHANGELOG.md` aggiornato a ogni merge.
- Nessun token o credenziale nel repo o in chat.

## 1b. Lingua
- **Tutto il testo del gioco è in inglese** (HUD, menu, messaggi di rete/Steam, nomi di armi e nemici, overlay di debug a schermo). Documenti, commenti nel codice e log di sviluppo restano in italiano.

## 2. Flusso file tra AI e PC
- PC di Manuel: `C:\Users\manue\FpsWaveTest` (non è un clone git: è la copia di lavoro dell'Editor, sincronizzata dall'AI). Sugli altri PC la cartella del progetto **è** un clone git e Claude Code lavora direttamente lì (vedi `GUIDA_COLLABORATORE.md`).
- L'AI lavora su un clone del repo nel proprio ambiente cloud (commit e push da lì) e sincronizza i file nella cartella del PC.
- I file generati dall'Editor sul PC (`.meta`, scene, prefab, ProjectSettings, Packages) vengono riportati nel clone prima di ogni commit.
- L'utente non deve fare commit manuali; se modifica qualcosa nell'Editor lo segnala, così l'AI risincronizza.

## 2b. Lavoro in due
- Ognuno lavora sul proprio branch e apre una PR verso `main`; l'altro (o il suo Claude) la può rivedere, ma non serve approvazione per unirla.
- Inizio sessione: `git pull` su `main`, `docs/LOCKS.md`, il proprio `docs/handoff/<persona>.md`. Fine sessione: aggiornare il proprio handoff, togliere i propri lock, push.
- `docs/HANDOFF.md` = stato comune (milestone, come si prova, verifiche, problemi aperti): si aggiorna nella PR quando cambia per tutti.
- Divisione per aree per ridurre i conflitti (es. uno armi/rete, l'altro nemici/mappa); le aree attive si dichiarano in `LOCKS.md`.
- Dopo un `git pull` che porta modifiche a `Packages/` o `ProjectSettings/`, riaprire Unity prima di lavorare. Dopo modifiche agli strumenti `FpsWave/Setup`, rieseguire lo strumento interessato.
- Asset binari nuovi: registrarli in `ASSETS.md` con il link alla fonte e, se servono conversioni, aggiornare `tools/setup-fab-assets.ps1`, così l'altro li rimette a posto sul suo PC.
- Sessioni di Manuel: la cartella Unity sul suo PC non è un clone; la skill `sync-collaboratore` (in `.claude/skills/`) la allinea a `main` a ogni ripresa, con recap delle modifiche del collaboratore. Il commit allineato è in `.fpswave-sync.json` nella cartella del progetto sul PC.
- Merge delle scene: driver `unityyamlmerge` già indicato in `.gitattributes`; ognuno lo configura sul proprio PC (comando in `GUIDA_COLLABORATORE.md`).

## 3. Verifica (ciclo obbligatorio)
1. Compilazione: lettura di `Logs/FpsWaveConsole.log` (mirror della Console scritto da `ConsoleLogMirror`, azzerato a ogni compilazione) e, se serve, della Console tramite controllo dell'Editor: 0 errori. L'accesso diretto all'Editor.log in AppData non è concesso.
2. Test EditMode (Unity Test Framework, Window > General > Test Runner) per la logica pura.
3. Play Mode: host + client con Multiplayer Play Mode per le funzionalità di rete; l'avvio automatico di `NetBootstrapMenu` collega i giocatori virtuali senza clic.
4. Scene e configurazioni ripetitive si generano con strumenti Editor (menu `FpsWave/...`) invece che a mano: sono ripetibili e versionabili.
5. Prima di eseguire gli strumenti `FpsWave/Setup` disattivare i giocatori virtuali di Multiplayer Play Mode: bloccano `DefaultPrefabObjects.asset`.
6. Test di sparo da remoto: il tasto Home (solo Editor/development build) riallinea la visuale alla direzione di spawn; sparare con pressione/rilascio del mouse senza spostarlo.
7. Uscire dal Play Mode con Ctrl+P (Esc è riservato dal sistema per il controllo remoto; un clic sul pulsante Play col cursore bloccato diventa uno sparo).
8. Durante il Play gli script modificati non si ricompilano: le modifiche si applicano all'uscita.
9. Il Play parte sempre da `Boot` (menu `FpsWave/Play Mode/Start From Boot`), anche se è aperta un'altra scena; all'uscita la rete viene chiusa, quindi si può rientrare subito senza errori di porta.
10. Build per i test: `FpsWave/Build/Windows + Zip (test con amici)` crea `Build/FpsWaveTest_<versione>/` e lo zip (Build/ è fuori da git). Prima di passare una build nuova, se la rete è cambiata in modo incompatibile, incrementare `GameInfo.ProtocolVersion`.
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
      Enemies/      (Core: scheda del tipo, AI, bersagli, grafica; Spawning: EnemyDirector, SpawnScoring, nodi; Types: comportamenti speciali)
      Spawning/     (DifficultyModel, WaveDefinition, Mutation - M3)
      Loot/
      Stats/        (Stat, Modifier, Mod)
      Map/
      UI/           (Hud/: SciFiHud; Menu/: GameMenu; DamageNumbers)
      Editor/       (FpsWave.Editor.asmdef, quando serve)
    Tests/
      EditMode/  PlayMode/
    Data/           (ScriptableObject: armi, nemici, wave, mutazioni, mod, tabelle drop)
    Prefabs/
    Scenes/         (Boot, MainMenu, Greybox, Game)
    UI/             (Styles/*.uss, Themes/*.tss, PanelSettings_Hud/Menu: generati da Setup/7)
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
