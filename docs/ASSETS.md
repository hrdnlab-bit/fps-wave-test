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
| The Free Firearm Sound Library (Ben Jaszczak et al.): spari di AR-15 e Walther PPQ, vicino e media distanza | OpenGameArt | CC0 | https://opengameart.org/content/the-free-firearm-sound-library | 2026-11-22 | `Assets/ThirdParty/Audio/FirearmLibrary/` (da `tools/setup-audio-assets.ps1`; versioni rifinite generate da `Setup/9` in `ThirdParty/_Own/Audio/`) |
| Impact Sounds, Kenney: passi, impatti, piastre, colpi subiti | kenney.nl | CC0 | https://kenney.nl/assets/impact-sounds | 2026-11-22 | `Assets/ThirdParty/Audio/KenneyImpact/` (zip scaricato a mano in Download, poi lo script) |
| Sci-fi Sounds, Kenney: abilità della tuta, carica del Brute | kenney.nl | CC0 | https://kenney.nl/assets/sci-fi-sounds | 2026-11-22 | `Assets/ThirdParty/Audio/KenneySciFi/` (come sopra) |
| Interface Sounds, Kenney: hit marker, voce della tuta, ping, rianimazione | GitHub (Calinou/kenney-interface-sounds) | CC0 | https://kenney.nl/assets/interface-sounds | 2026-11-22 | `Assets/ThirdParty/Audio/KenneyInterface/` (da `tools/setup-audio-assets.ps1`) |
