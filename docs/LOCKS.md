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
| `Assets/_Project/Prefabs/Player.prefab` | Matteo | `matteo/m2-runner` | 2026-11-21 | aggiunti EnemyTarget, DamageNumbers, HUD (UIDocument + SciFiHud), WeaponFeelDriver |
| `Assets/_Project/Prefabs/Enemies/**` | Matteo | `matteo/m2-runner` | 2026-11-21 | prefab dei nemici (generati da Setup/6) |
| `Assets/_Project/Data/Enemies/**` | Matteo | `matteo/m2-runner` | 2026-11-21 | schede dei tipi di nemico |
| `Assets/_Project/Scenes/Boot.unity` | Matteo | `matteo/m2-runner` | 2026-11-21 | menu di gioco (UIDocument + GameMenu) sul NetworkManager |
| `Assets/_Project/UI/**` | Matteo | `matteo/m2-runner` | 2026-11-21 | stili, tema e PanelSettings di HUD e menu |
| `Assets/ThirdParty/{Kenney,OpenGameArt,GoogleFonts,_Own/Hud}/**` | Matteo | `matteo/m2-runner` | 2026-11-21 | asset e texture dell'interfaccia |
| Area interfaccia (`Scripts/UI/`) | Matteo | `matteo/m2-runner` | 2026-11-21 | HUD e menu nuovi; avvisare Manuel prima dell'overlay di debug (M0b) |
| `Assets/_Project/Data/Weapons/**` | Matteo | `matteo/m2-runner` | 2026-11-21 | profili di sensazione (Setup/8) |
| Area sensazione armi (`Scripts/Weapons/Feel/`) | Matteo | `matteo/m2-runner` | 2026-11-21 | nuova; tocca `PlayerWeapons`, `PlayerLook`, `ShotFxPool` (area di Manuel) |
| `Assets/DefaultPrefabObjects.asset` | Matteo | `matteo/m2-runner` | 2026-11-21 | registrato Enemy_Runner |
| Area nemici / spawn / wave (`Scripts/Enemies/`) | Matteo | `matteo/m2-runner` | 2026-11-21 | area dichiarata |
