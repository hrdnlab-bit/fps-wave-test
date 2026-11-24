# ASSET - Fonti, licenze, coerenza

## 1. Regole
- Solo asset **gratuiti** con licenza che consente l'uso in un gioco commerciale.
- **Coerenza stilistica**: tutto fotorealistico PBR (scansioni fotogrammetriche o equivalenti), texel density simile (~512 px/m per l'ambiente, maggiore per armi e mani), palette e luce unificate dal post-processing di zona. Niente mix con low-poly o stilizzato, nemmeno come placeholder visibile in build.
- Ambientazione mista (natura + militare sci-fi): la parte sci-fi deve restare "militare credibile" (metallo verniciato, cemento, cavi, pannelli, luci di servizio). Si scartano kit sci-fi lucidi/cartoon anche se gratuiti.
- Ogni asset importato va registrato nella tabella in fondo (nome, fonte, licenza, link, data). I CC-BY richiedono attribuzione nei crediti.
- Asset di terze parti in `Assets/ThirdParty/<Fonte>/`; gli asset del gioco (prefab, materiali URP adattati) in `Assets/_Project/`.
- **Asset pesanti fuori dal repo** (scelta dell'utente): in `Assets/ThirdParty/` il repo contiene solo i file `.meta` e i file di testo (materiali, prefab, scene), mentre i binari (texture, modelli, audio, video) sono esclusi da `.gitignore`. I `.meta` conservano i GUID: rimettendo i binari con lo stesso nome, tutti i riferimenti restano validi.
- I binari originali vivono sul PC dell'utente; backup consigliato in una cartella Google Drive `FpsWaveTest_Assets` (stessa struttura di `Assets/ThirdParty/`).
- Gli asset creati da noi che sono binari (rari) vanno anch'essi in `Assets/ThirdParty/_Own/` per seguire la stessa regola.

### Come li ottiene un'altra persona
- **Per giocare (l'amico che testa)**: non serve nulla del progetto. Riceve una **build Windows** già compilata (cartella zippata condivisa via Google Drive/WeTransfer); gli asset sono dentro la build. Con l'App ID proprio si userà un branch Steam privato.
- **Per sviluppare** (un secondo sviluppatore, o un PC nuovo): clona il repo, scarica gratis da Fab con il proprio account i pacchetti del registro qui sotto (la licenza Fab non permette di passarsi i file) ed esegui `tools/setup-fab-assets.ps1` **prima di aprire Unity**, che copia e converte tutto con i nomi attesi dai `.meta`. Procedura completa in `GUIDA_COLLABORATORE.md`. Ogni nuovo asset che richiede copie o conversioni va aggiunto allo script.

## 2. Fonti approvate
| Fonte | Contenuto | Licenza | Note |
|---|---|---|---|
| Poly Haven | Texture, HDRI, modelli | CC0 | Nessun login |
| ambientCG | Texture/materiali PBR (anche metalli, pannelli, cemento) | CC0 | Nessun login |
| Fab - Quixel Megascans | Superfici, rocce, vegetazione, props scansionati | Fab Standard License (uso in qualsiasi engine) | Libreria tornata gratuita a novembre 2026; richiede login Epic dell'utente |
| Fab (altri asset gratuiti) | Kit militari/industriali, armi | Fab Standard License o CC | Verificare licenza per asset |
| Unity Asset Store (gratuiti) | Pacchetti vari | Standard Unity Asset Store EULA | Login Unity dell'utente; scegliere solo pacchetti realistici |
| Mixamo | Personaggi e animazioni (mani FPS, creature) | Uso gratuito in giochi, royalty free | Login Adobe dell'utente |
| Sonniss GameAudioGDC | Effetti sonori professionali (armi, sci-fi, ambienti) | Royalty free, commerciale | Pacchetti annuali |
| Freesound | Suoni | Solo CC0 (CC-BY con attribuzione) | Verificare la licenza di ogni file |

## 3. Priorità di acquisizione
1. M1: arma realistica + braccia FPS, materiali di base per la greybox, suoni di sparo/ricarica/impatto.
2. M2: Runner e Brute con animazioni (corsa, attacco, morte, stagger).
3. M7: kit ambientali per le 4 zone (natura, cava, villaggio, complesso militare).
4. M8: VFX, musica, UI, altri nemici e boss.

## 4. Registro asset importati
| Asset | Fonte | Licenza | Link | Data | Dove nel progetto |
|---|---|---|---|---|---|
| FPS Guns 4K - Pistol 2, Heart State Games: Beretta M92 modulare PBR 4K (importata a 2K) | Fab | Fab Standard License (redistribuzione dei file vietata) | https://www.fab.com/listings/0d1c1104-b08d-44e1-88a9-4d59106636ac | 2026-11-21 | `Assets/ThirdParty/Fab/Pistol2/` (copiata e convertita da `FpsWave/Setup/4` dalla cartella estratta in Download) |
| M4 - FPS Weapon Animations Pack FREE (v.1), BarcodeGames: braccia FPS con guanti + M4, animazioni Idle/Draw/Holster/Fire/Reload/Reload_Empty | Fab | Fab Standard License | https://www.fab.com/listings/38da8f1e-cb54-4cda-beb1-6e55642a55a9 | 2026-11-21 | `Assets/ThirdParty/Fab/M4Animations/` (+ `Packed/` con le mappe Specular+Smoothness generate da noi) |
| UI Pack Sci-Fi, Kenney: pannelli "vetro" con tacche (base delle piastre del HUD e dei bottoni del menu), mirini, font Kenney Future | Kenney (kenney.nl) via OpenGameArt | CC0 | https://kenney.nl/assets/ui-pack-sci-fi | 2026-11-21 | `Assets/ThirdParty/Kenney/UIPackSciFi/` (da `tools/setup-ui-assets.ps1`; le piastre 9-slice derivate le genera `Setup/7` in `ThirdParty/_Own/Hud/`) |
| Sci-Fi interface textures, rubberduck: anelli olografici, radar, onda (versioni "no effects") | OpenGameArt | CC0 | https://opengameart.org/content/sci-fi-interface-textures | 2026-11-21 | `Assets/ThirdParty/OpenGameArt/SciFiInterface/` (ritagli `holo_*` generati da `Setup/7` in `ThirdParty/_Own/Hud/`) |
| Font Rajdhani (SemiBold, Bold), Orbitron (variabile), Share Tech Mono | Google Fonts (repo github.com/google/fonts) | SIL Open Font License 1.1 | https://fonts.google.com | 2026-11-21 | `Assets/ThirdParty/GoogleFonts/` (da `tools/setup-ui-assets.ps1`, OFL.txt versionato) |
| AIRO, pilota sci-fi tattico (modello, rig a 52 ossa, texture PBR 4K BaseColor/Normal/MetallicSmoothness/AO/Emission) | Nostro (Blender, Manuel + AI) | Proprietà del progetto | - | 2026-11-22 | `Assets/ThirdParty/_Own/AIRO/` (solo `.meta` nel repo; binari nello zip `AIRO_Pilot_Unity.zip` sul Drive di Manuel, da estrarre nella radice del progetto prima di aprire Unity). Materiale, prefab e mesh derivate li genera `Setup/9` |
| Runner, mutante bio-meccanico (15k triangoli, scheletro stile Mixamo e clip Idle/Run/Attack/Death fatti con `tools/autorig/autorig.py`; texture BaseColor/Normal 4K importate a 2048, MetallicSmoothness 2K) | Nostro (immagine AI + Tripo 3D, rig e animazioni procedurali in Blender, Matteo) | Proprietà del progetto | - | 2026-11-22 | `Assets/ThirdParty/_Own/Runner/` (solo `.meta` nel repo; binari nello zip `Runner_Unity.zip` di Matteo, da estrarre nella radice del progetto). Materiale `M_Enemy_Runner_Model`, controller `Art/Animations/Runner.controller` e prefab li genera `Setup/6`; senza i binari il Runner torna la sagoma greybox |
| Animazioni del Runner: Running (Style 100, Speed 100), Running With Intention, Drunk Run Forward, Injured Run, Sprint, Fast Run, Zombie Running, Zombie Run, Zombie Running Fast, Mutant Run (corse), Zombie Crawl, Zombie Biting, Zombie Stumbling, Zombie Idle, Mutant Swiping, Zombie Attack (swipe e overhead), Zombie Reaction Hit, Zombie Reaction Hit Stumble, Zombie Death, Zombie Dying, Death From Front/Back Headshot, Standing React Death Left/Right/Forward/Backward, Dying (testa: due ginocchia, indietro, un ginocchio; petto; schiena: due ginocchia, un ginocchio), Falling Back Death, Flying Back Death, Falling Forward Death, Death; retarget sullo scheletro del Runner con `tools/autorig/` | Adobe Mixamo (mixamo.com), scaricate da Matteo con il suo account | Uso gratuito anche commerciale nei propri progetti (termini Adobe); vietato ridistribuire i file grezzi | mixamo.com | 2026-11-22 | Dentro `Runner.fbx` (fuori dal repo, zip `Runner_Unity.zip`); gli FBX originali restano nei Download di Matteo |
| (Prima versione della corsa, non più usata) motion capture CMU 127_08 | CMU Graphics Lab Motion Capture Database (mocap.cs.cmu.edu) | Uso libero anche commerciale, crediti graditi | mocap.cs.cmu.edu | 2026-11-22 | Sostituita dalle animazioni Mixamo; il lettore BVH resta in `tools/autorig/bvh.py` |
| Pilot, soldato-pilota con la tuta Mk-VII (nuovo personaggio del player: 40k triangoli, stesso scheletro, texture 2K BaseColor/Normal/MetallicSmoothness, Emission ricavata dalle luci della BaseColor, AO bianca) | Nostro (immagine AI + Tripo 3D, rig con `tools/autorig/autorig.py`, Matteo) | Proprietà del progetto | - | 2026-11-22 | `Assets/ThirdParty/_Own/Pilot/` (solo `.meta`; binari nello zip `Pilot_Unity.zip` di Matteo). Se c'è, `Setup/9` lo usa al posto di AIRO (stesse ossa, stesso prefab `AIRO_Character`); senza, resta AIRO |
| Fucile sci-fi "Scifi gun" di M@xim (corpo, grilletto, caricatore; palette di 6 materiali ricreati in URP) | Fab | CC-BY 4.0: **crediti obbligatori** ("Scifi gun" by M@xim) | fab.com (cercare "Scifi gun" M@xim) | 2026-11-22 | `Assets/ThirdParty/Fab/SciFiRifle/SciRifle.fbx` esportato da `tools/guns/export_scifi_rifle.py` (solo `.meta` nel repo; l'FBX è nello zip di Matteo, CC-BY permette di passarlo). Modificato da noi: scatola del cannocchiale sostituita da un mirino olografico aperto con vetro e reticolo. Montato sulle braccia M4 da `Setup/4` |
| Magma - Scifi Pistol, AF Creations (FBX, due skin, mappe ORM 4K) | Fab | Fab Standard License (redistribuzione dei file vietata: ognuno la scarica col proprio account) | fab.com (cercare "Magma Scifi Pistol" AF Creations) | 2026-11-22 | `Assets/ThirdParty/Fab/SciFiPistol/` (copiata da `tools/setup-fab-assets.ps1` da `scifipistol.zip` in Download, che genera anche `*_MetalSmooth.png` e `*_AO.png`) |
| Animazioni del Pilot: Rifle Aiming Idle, Walk/Run Forward/Backward/Left/Right, Sprint Forward, Idle Crouching Aiming, Walk Crouching Forward/Backward/Left/Right, Jump Loop, Reloading (elenco in `tools/autorig/mixamo_pilot_clips.json`) | Adobe Mixamo, scaricate da Matteo | Uso gratuito anche commerciale (termini Adobe); vietato ridistribuire i file grezzi | mixamo.com | 2026-11-22 | Dentro `Pilot.fbx` (retarget sullo scheletro del Pilot con `tools/autorig/autorig.py`), zip di Matteo |
