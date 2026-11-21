# LOCKS - chi sta modificando cosa

Scene, prefab e asset YAML di Unity si fondono male in git: prima di modificarli si **blocca** qui il file o l'area, con un commit sul proprio branch (o direttamente su `main` se è solo il lock). A fine lavoro (PR unita) si toglie la riga.

Regole:
- Si bloccano: `*.unity`, `*.prefab`, `Assets/_Project/Data/**`, `ProjectSettings/**`, `Packages/manifest.json`, materiali e controller condivisi.
- Il codice C# non si blocca: i conflitti si risolvono con il merge normale. Se si cambia un'API usata dall'altro, scriverlo nella PR.
- Scene e prefab generati dagli strumenti `FpsWave/Setup`: in caso di conflitto si tiene una versione e si rigenera.
- Lock più vecchio di 3 giorni senza attività: si chiede prima di ignorarlo.

| File o area | Chi | Branch | Dal | Note |
|---|---|---|---|---|
| `Assets/_Project/Scenes/Greybox.unity` | Matteo | `matteo/m2-runner` | 2026-11-21 | muri di copertura, nodi di spawn, EnemyDirector |
| `Assets/_Project/Prefabs/Player.prefab` | Matteo | `matteo/m2-runner` | 2026-11-21 | aggiunto EnemyTarget |
| `Assets/_Project/Prefabs/Enemy_Runner.prefab` | Matteo | `matteo/m2-runner` | 2026-11-21 | nuovo |
| `Assets/DefaultPrefabObjects.asset` | Matteo | `matteo/m2-runner` | 2026-11-21 | registrato Enemy_Runner |
| Area nemici / spawn / wave (`Scripts/Enemies/`) | Matteo | `matteo/m2-runner` | 2026-11-21 | area dichiarata |
