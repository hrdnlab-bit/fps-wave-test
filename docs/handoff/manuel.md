# HANDOFF di Manuel

Ultimo aggiornamento: 2026-11-22

## Su cosa sto lavorando
- Branch: `manuel/airo-character` (PR verso `main`).
- Obiettivo: personaggio AIRO sul player (corpo visibile agli altri e in prima persona, gambe e braccia funzionanti, armi impugnate), ottimizzato per il multiplayer.
- Stato: in revisione. Allineato a `main` con il lavoro di Matteo (M2 Runner, HUD/menu, sensazione delle armi).

## Fatto
- Modello AIRO in Blender (~75k triangoli, rig a 52 ossa, PBR 4K), esportato in `Assets/ThirdParty/_Own/AIRO/`.
- `Scripts/Player/Character/`: rig procedurale (passo con IK, crouch, salto, mira di busto e testa, mani sull'arma e dita), `PlayerCharacter` in rete (pitch e flag a 10 Hz solo quando cambiano, arma impugnata in SyncVar).
- Armi in terza persona `WM_AssaultRifle` e `WM_Pistol` generate dai view model; traccianti degli altri dalla volata.
- Prima persona: corpo dalla vita in giù + ombra completa.
- `FpsWave/Setup/9. Build Player Character (AIRO)` (il numero 6 ora è "Build Enemies" di Matteo).
- Merge di `main` (PR #3 di Matteo): `PlayerWeapons.EquippedIndex` ora vale anche per gli altri client, `PlayerLook.ViewPitch` usa il nuovo rinculo a molla.

## Verifiche
- Compilazione 0 errori, test EditMode 88/88 (24 nuovi: matematica del passo e rig sul prefab vero; mani a 0.0 cm dall'impugnatura a pitch 0/±45°, passo 0.59 m camminando e 1.01 m correndo, bacino -0.42 m in crouch).
- Play Mode Host + Player 2 (MPPM): il client vede il pilota con pistola e fucile impugnati, cambio arma e pitch sincronizzati; nessun errore (restano solo gli avvisi dei font UI mancanti finché non si esegue `tools/setup-ui-assets.ps1`).

## Lock attivi
- `Player.prefab`, `Prefabs/Weapons/WM_*`, `Art/Characters/AIRO/**`, `Data/Characters/**`, campo del modello in terza persona in `Data/Weapons/*.asset` (vedi `docs/LOCKS.md`).

## Problemi aperti
- Animazione procedurale: credibile ma non al livello di un mocap (niente rotazione del bacino in corsa, piedi che possono scivolare un po' alle velocità intermedie). Se serve più realismo: clip Mixamo per la locomozione mantenendo l'IK delle mani.
- Manca un LOD del corpo (75k triangoli): da fare in Blender se aumentano i player.
- Sul PC di Manuel mancano gli asset UI di Matteo: eseguire `tools/setup-ui-assets.ps1` con Unity chiuso, poi `Setup/7`.

## Prossimi passi
1. PR `manuel/airo-character` verso `main`; caricare `AIRO_Pilot_Unity.zip` su Drive e condividerlo con Matteo.
2. Prova a mano in MPPM: camminata/corsa/crouch del Player 2 visti dall'Host.
3. Test Steam con l'amico, suoni (M1.6), overlay di debug (M0b).
