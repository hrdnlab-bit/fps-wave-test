# HANDOFF di Matteo

Ultimo aggiornamento: 2026-11-22

## Su cosa sto lavorando
- Branch: `matteo/phase1-suit-brute` (PR verso `main` aperta, da rivedere).
- Obiettivo: Fase 1 delle idee per rendere il gioco unico (vedi ROADMAP, "Fasi delle idee di Matteo").
- Stato: in revisione.

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

- Fase 1 (2026-11-22, branch `matteo/phase1-suit-brute`): `Player/Suit/SuitAbilities`, `Player/PlayerLife`, `Player/PlayerPing`, `UI/SuitCallouts`, `Enemies/Core/EnemyArmor|ArmorPlate|ArmorDebris|IEnemyAbility`, `Enemies/Types/BruteCharge`, HUD (energia, abilità, voce, indicatori, a terra). Toccati file di Manuel: `Health` (ServerDamageScale, ServerSetHealth), `PlayerMotor` (Downed, AbilitySpeedMultiplier, spintone), `PlayerInputReader`/`FpsInput` (Q, F, V, Z/centrale, E), `PlayerWeapons` (piastre, solo pistola a terra), `ShotFxPool` (scintille), `GameInfo` (protocollo 3). Suoni provati e poi tolti su richiesta.

- Merge di `manuel/airo-character` (PR #4, personaggio AIRO) dentro `matteo/phase1-suit-brute`: conflitti in `PlayerWeapons` (tenuto `EquippedIndex` di Manuel con `_observedEquipped` + `ForceSecondary`/`WeaponsDisabled`), `Player.prefab` (versione di Manuel, poi `Setup/9` + `Setup/6/7/8` per rimettere tuta, a terra e ping), documenti uniti. Se la PR #4 entra prima su `main`, la #5 si riduce da sola.

## Verifiche
- Dopo il merge con AIRO: compilazione 0 errori, test EditMode 91/91 (con i test del rig di Manuel), Play solo Host pulito (HUD con la tuta, corpo AIRO).
- Fase 1: compilazione 0 errori, test EditMode 67/67 (dopo aver tolto i suoni); Play solo Host: abilità (energia, tinte, voce), ping, a terra con dissanguamento e rialzo da soli, Brute con piastre colpite (numeri acciaio) e rotte (nucleo scoperto, voce "ARMOR BREACHED"); host + client (MPPM): connessione pulita, HUD del client con tuta, dissanguamento → morte → ritorno del client. **Da provare a mano**: rianimazione tenendo premuto E (non riuscito a creare il momento "uno a terra, l'altro in piedi" con il player virtuale).
- Compilazione: 0 errori (`Logs/FpsWaveConsole.log`); restano solo gli avvisi già noti di API deprecate in `NetBootstrapMenu`/`PlayerSpawnService`.
- Test EditMode: 64/64.
- Play Mode solo Host (Editor): spawn fuori vista, inseguimento, attacco con max 3 attaccanti, kill con la pistola (3 colpi) accettata dal server, rimpiazzo automatico. NavMesh in 12-14 ms. Numeri di danno visibili (bianco, poi rosso grande all'uccisione), corpo che cade senza segni sospesi. Nessun errore in console.
- Play Mode host + client (MPPM, 2026-11-21 23:42): il client si collega, vede i Runner muoversi e il player Host, HUD e menu funzionano, spara senza colpi rifiutati dal server (log puliti su entrambe le istanze). Non sono riuscito a confermare a schermo un'uccisione fatta dal client (mira con il mouse virtuale imprecisa): da riprovare a mano.

## Lock attivi (vedi docs/LOCKS.md)
- `Boot.unity`, `UI/**`, area interfaccia, `Greybox.unity`, `Player.prefab`, `Prefabs/Enemies/**`, `Data/Enemies/**`, `DefaultPrefabObjects.asset`, area nemici.

## Problemi aperti
- Non vanno nel commit (modifiche automatiche di Unity su questo PC): `Assets/Settings/PC_RPAsset.asset`, `UniversalRenderPipelineGlobalSettings.asset`, `ProjectSettings/GraphicsSettings.asset`, `ProjectSettings/URPProjectSettings.asset`, `.vsconfig`.
- Bilanciamento da rivedere: con 3 attaccanti da 6 danni al secondo un player fermo muore in ~5 s.
- Un avviso "Movimento anomalo del client 0: teletrasporto" è comparso una volta all'uscita dal Play: da controllare se si ripete con i nemici addosso.
- Interest management per distanza non attivo (serve con la mappa vasta).

## Prossimi passi
1. Provare a mano la rianimazione in due (MPPM o con un amico) e ritoccare valori (energia, durate, danni del Brute,).
2. Merge della PR della Fase 1 dopo la revisione di Manuel.
3. Fase 2: sistema di wave (M3, `SPAWN_DIRECTOR.md`) e poi contaminazione, nemici che si adattano, voto estrai-o-continua, wave notturne/tempesta.
4. Fase 3: difese del complesso (torrette, recinzioni, gas).
