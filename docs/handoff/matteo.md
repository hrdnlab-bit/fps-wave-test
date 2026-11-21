# HANDOFF di Matteo

Ultimo aggiornamento: 2026-11-21

## Su cosa sto lavorando
- Branch: `matteo/m2-runner` (commit locale, push e PR da fare dopo il test host + client).
- Obiettivo: M2 nemici (spawn nascosti, Runner, tipi di nemico guidati dai dati, numeri di danno) + nuova interfaccia sci-fi (HUD e menu) su richiesta.
- Stato: in revisione (manca il test host + client).

## Fatto in questa sessione
- Setup del PC secondo `GUIDA_COLLABORATORE.md` (Git, LFS, GitHub CLI `TheRealLordy`, clone, asset Fab).
- `Scripts/Enemies/`: EnemyDirector (NavMesh runtime, cache di visibilità dei nodi a budget, spawn a gruppi dal pool, riciclo, AI a turno), EnemyAgent (Runner: insegue, si ferma, colpisce, posti di attacco), EnemyTarget (registro dei player, ritorno in vita temporaneo), EnemyVisual (animazione procedurale), SpawnScoring (logica pura + test), EnemySpawnNode.
- `Combat/IMovingTarget` + tolleranza della validazione colpi sui bersagli in movimento (`PlayerWeapons`, `ShotValidation`): area di Manuel, modifica piccola, segnalata nella PR.
- `FpsWave/Setup/6. Build Enemies (M2)` (richiamato da `Setup/3`): prefab `Enemy_Runner`, EnemyTarget sul Player, 16 muri di copertura e 102 nodi di spawn nella Greybox.
- Documenti: CHANGELOG, DECISIONS, ROADMAP (task M2), SPAWN_DIRECTOR §13, NETCODE, HANDOFF, LOCKS.
- Seconda tornata (stesso branch): tipi di nemico guidati dai dati (`EnemyDefinition` in `Data/Enemies/`, prefab in `Prefabs/Enemies/`, catalogo in `Editor/EnemyContentBuilder.cs`, codice in `Enemies/Core` e `Enemies/Spawning`); numeri di danno (`UI/DamageNumbers`, `Weapons/HitConfirm`, conferma server di ogni colpo); fix del segno di proiettile sospeso in aria (schizzo breve sui personaggi, hitbox spente alla morte, animazione più vicina alle hitbox). `Health.SetMaxHealth` aggiunto (area comune).
- Terza tornata (stesso branch): HUD "visore" ciano (`UI/Hud/SciFiHud` + `UI/Styles/Hud.uss`) e menu "terminale" ambra (`UI/Menu/GameMenu` + `UI/Styles/Menu.uss`) in UI Toolkit; impostazioni salvate (`Core/GameSettings`); `FpsWave/Setup/7. Build UI`. Toccati file di Manuel: `NetBootstrapMenu` (API pubblica + OnGUI spento), `PlayerInputReader` (sensibilità da impostazioni, input bloccato con menu aperto), `PlayerLook` (FOV da impostazioni); rimosso `PlayerHud`.
- Quarta tornata (stesso branch): sensazione delle armi generica (`Weapons/Feel/`: profilo `WeaponFeel` per arma, `RecoilMath`, `WeaponFeelDriver`), rinculo a molla in `PlayerLook` con tremore e punch del FOV, traccianti e luce del colore dell'energia, calore cosmetico con barra nel HUD, `FpsWave/Setup/8. Apply Weapon Feel`. Toccati file di Manuel: `PlayerWeapons`, `PlayerLook`, `ShotFxPool`, `WeaponDefinition`, `GameInfo` (protocollo 2).
- Quinta tornata (stesso branch): rinculo stile R6 (mira che sale e resta, arma ferma, perno all'impugnatura, arma più vicina); HUD minimale stile Crysis; HUD e menu con texture (Kenney UI Pack Sci-Fi, rubberduck Sci-Fi interface, font Google) generate da `Editor/UiTextureGenerator` in `Setup/7`; `tools/setup-ui-assets.ps1`.

## Verifiche
- Compilazione: 0 errori (`Logs/FpsWaveConsole.log`); restano solo gli avvisi già noti di API deprecate in `NetBootstrapMenu`/`PlayerSpawnService`.
- Test EditMode: 61/61.
- Play Mode solo Host (Editor): spawn fuori vista, inseguimento, attacco con max 3 attaccanti, kill con la pistola (3 colpi) accettata dal server, rimpiazzo automatico. NavMesh in 12-14 ms. Numeri di danno visibili (bianco, poi rosso grande all'uccisione), corpo che cade senza segni sospesi. Nessun errore in console.
- Play Mode host + client (MPPM): **da fare**.

## Lock attivi (vedi docs/LOCKS.md)
- `Boot.unity`, `UI/**`, area interfaccia, `Greybox.unity`, `Player.prefab`, `Prefabs/Enemies/**`, `Data/Enemies/**`, `DefaultPrefabObjects.asset`, area nemici.

## Problemi aperti
- Non vanno nel commit (modifiche automatiche di Unity su questo PC): `Assets/Settings/PC_RPAsset.asset`, `UniversalRenderPipelineGlobalSettings.asset`, `ProjectSettings/GraphicsSettings.asset`, `ProjectSettings/URPProjectSettings.asset`, `.vsconfig`.
- Bilanciamento da rivedere: con 3 attaccanti da 6 danni al secondo un player fermo muore in ~5 s.
- Un avviso "Movimento anomalo del client 0: teletrasporto" è comparso una volta all'uscita dal Play: da controllare se si ripete con i nemici addosso.
- Interest management per distanza non attivo (serve con la mappa vasta).

## Prossimi passi
0. Provare Steam dal nuovo menu (Crea lobby → codice → l'amico entra con il codice) e il pannello Squadra con gli inviti.
1. Test host + client: Window > Multiplayer > Multiplayer Play Mode, Player 2 attivo, Play. Controllare che i Runner si vedano muovere fluidi sul client e che i colpi del client li uccidano.
2. Push del branch e PR verso `main` (modello in `.github/pull_request_template.md`); nella PR segnalare a Manuel la modifica a `PlayerWeapons`/`ShotValidation` e che serve eseguire `Setup/6`.
3. M2.3 Brute: nuova voce nel catalogo di `EnemyContentBuilder` (600 HP, 3.5 m/s, peso più basso, gruppi da 1) + componente di carica in `Scripts/Enemies/Types/`.
4. M2.4 a terra e rianimazione (al posto del ritorno in vita automatico).
