# CHANGELOG

Formato: [Keep a Changelog](https://keepachangelog.com/it/1.1.0/), versioni semantiche.

## [Unreleased]
### Changed (mirino olografico del fucile sci-fi, Matteo)
- Il "cannocchiale" del fucile era una scatola chiusa con uno schermino dietro: mirando si vedeva solo il retro. `tools/guns/export_scifi_rifle.py` ora lo toglie e costruisce un **mirino olografico nello stile del fucile**: anima nera con i gusci chiari ai fianchi tagliati in obliquo, righe rosse, cappuccio a otto lati con calotta chiara, emettitore ambra, pulsanti e manopola del morsetto sulla slitta, spigoli smussati; il corpo copre i resti del vecchio attacco. Vetro separato (`SciRifle_Lens`) con finestra ~3,6 x 2,4 cm.
- Il vetro usa `SciRifle_Lens.mat` (URP Unlit additivo, HDR) con un reticolo rosso generato da `Setup/4` (`SciRifle_Reticle.asset`, texture a un canale in YAML fuori da Git LFS: anello, punto, quattro tacche). Mirando, il centro del vetro va sull'asse della camera a 12 cm dall'occhio (`HoloEyeRelief`).

### Added (animazioni del personaggio e armi sci-fi, Matteo)
- **Armi sci-fi al posto di M4 e Beretta**: fucile sci-fi di M@xim (CC-BY) montato sulle braccia animate del pacchetto M4 (stesse clip di sparo, ricarica, estrazione; caricatore e grilletto sulle ossa giuste), mirino a 8,6 cm sopra la canna; pistola "Magma" di AF Creations (Fab) con ricarica procedurale a fasi. Prefab `VM_SciRifle` e `VM_SciPistol` generati da `Setup/4`.
- **Cambio arma con holster**: l'arma attuale scende (clip Holster o caduta procedurale, 0,26 s), poi sale la nuova; ripensamento durante l'holster = torna su l'arma di prima.
- **Corpo in terza persona animato con clip Mixamo** (17: idle, camminata/corsa in 4 direzioni, sprint, accucciato in 4 direzioni, aria, ricarica) in `Pilot.fbx`; `PilotAnimatorBuilder` (in `Setup/9`) crea `Pilot.controller`: blend 2D per velocità e direzione alle velocità del gioco, livello del busto per la ricarica acceso solo mentre si ricarica. Sopra le clip `CharacterRig` aggiunge mira (busto e testa sul pitch) e mani sull'arma con l'IK; senza clip resta la posa procedurale di Manuel.
- **Ricarica vista dagli altri**: `RpcObserverReload` dal server a tutti tranne il proprietario; il corpo fa la clip di ricarica e l'arma si inclina. Cambio arma visto dagli altri: l'arma scende e risale.
### Fixed
- Corpo del Pilot a 56 m sotto (o sopra) il player e braccio alzato: l'altezza veniva misurata sulla posa di default dell'FBX con le clip, diversa da quella in gioco. Ora i piedi a terra si calcolano dalle ossa dei piedi + suola (dalla bindpose) e `CharacterRig` li ricontrolla al primo frame animato.
- Prefab delle armi in terza persona senza mesh dopo un secondo `Setup/9` nella stessa sessione: la mesh esistente viene sovrascritta (stesso GUID) e il prefab reimportato.

### Changed (andature diverse e morte senza gambe, Matteo)
- **Sei andature intere** al posto delle corse ibride (due delle tre erano in realtà la stessa clip): corsa piegata, corsa feroce, Zombie Run, barcollante, zoppicante, Mutant Run. Il server ne sceglie una per nemico (`SyncVar<byte>`, pesata: le veloci più frequenti) e la velocità di movimento cambia con l'andatura (6,2 / 5 / 4,4 / 3 / 2,8 / 2,8 m/s), così in un gruppo si vedono corse e ritmi diversi.
- **Senza gambe il corpo cade**: perdendo una gamba lo zombie crolla a faccia in giù (non più barcollando in piedi) e poi striscia; ucciso senza gambe (da terra o da in piedi) crolla subito a terra invece di restare sospeso. Tolta la clip "barcollando" dalle morti alle gambe.
- Hitbox della testa calcolata sulla media di tutte le andature. Debug: **L** colpisce gli stinchi (sx/dx a turno), J e L restano sullo stesso nemico finché è vivo.

### Added (zombie veloci, morti a caso e smembramento a colpi, Matteo)
- **Corse da zombie veloce**: tre andature a caso per nemico, gambe da sprint e fast run di Mixamo con il busto da zombie (in avanti, braccia ad artiglio; la terza col busto del Mutant Run), retarget ibrido in `tools/autorig/` (`upper` in `mixamo_runner_clips.json`).
- **Morte diversa per parte colpita e a caso**: 7 tipi (indietro, avanti, testa da davanti, testa da dietro, di lato a sinistra, a destra, gambe) con 21 animazioni Mixamo in tutto; il server sceglie tipo e variante (a volte crolla di lato anche colpito di fronte). Reazione al colpo in due varianti.
- **Arti che si staccano a forza di colpi** (`EnemyHitRegions`, server): danno accumulato per arto, oltre la soglia (braccio 34% della salute, avambraccio 26%, coscia 45%, stinco 36%) l'arto si stacca anche da vivo. Senza una gamba lo zombie crolla e **striscia** (0,8 m/s, attacco da terra, hitbox sdraiate). Colpi al busto: mai arti staccati.
- **Colpo alla testa letale**: la testa **esplode** (65%: schegge di cranio, brandelli, nube di sangue), vola via intera (20%) o resta (15%).
- **Monconi realistici** al posto della sfera rossa: carne frastagliata, pelle strappata e osso che sporge, della misura dell'arto, anche sull'estremità del pezzo che vola.
- Debug: **J** colpisce il nemico più vicino a un arto senza ucciderlo (per vedere gli arti staccarsi da vivi), K uccide con un colpo su una parte a caso, F10 niente danno.

### Added (animazioni Mixamo, morti e smembramento del Runner, Matteo)
- **Animazioni Mixamo** sul Runner (retarget in `tools/autorig/`): sprint e fast run a caso per nemico (5,1 e 4,6 m/s, accelerate oltre con `RunRate`), corsetta zombie alle velocità intermedie, idle zombie, tre attacchi (artigliata del mutante, swipe e colpo dall'alto, scelti a caso), reazione ai colpi (al massimo una ogni 1,3 s).
- **Morti diverse in base al colpo**: colpo alla testa = crollo secco, dal davanti = cade all'indietro, alle spalle o alle gambe = cade in avanti. Il server manda alla morte punto del colpo e direzione nello spazio locale del nemico (`EnemyAgent.RpcKilled`, ~28 byte); cadavere 3 s.
- **Smembramento** (`EnemyGore`, solo client): l'arto più vicino al colpo letale si stacca (testa sul colpo alla testa, braccio, avambraccio, coscia, gamba): l'osso si chiude sulla mesh, vola un pezzo fisico ricavato dalla mesh, moncone e spruzzo di sangue.
- Strumenti di prova solo Editor/dev nell'EnemyDirector: **K** uccide il nemico più vicino con un colpo su una parte a caso, **F10** nemici senza danno (F9 su questo PC era preso da un altro programma).

### Added (corsa realistica del Runner, Matteo)
- **Corsa da motion capture vera** (CMU 127_08) al posto di quella fatta a mano: retarget per direzione delle ossa (A-pose del modello contro T-pose della ripresa), loop di 0,61 s senza cuciture, sul posto, con un filo di curvatura in più da mostro. La clip va a 3,69 m/s; sopra quella velocità accelera in proporzione (parametro `RunRate`), così i piedi non scivolano a 6 m/s. Hitbox della testa ricalcolata sulla nuova posa di corsa.

### Added (scheletri e animazioni, Matteo)
- **Runner animato**: scheletro e clip Idle, Run, Attack, Death fatti in Blender con `tools/autorig/autorig.py`; `Setup/6` crea l'Animator con il controller `Runner.controller` (blend Idle/Run sulla velocità, trigger Attack, bool Dead). Fase e ritmo casuali per non avere lo sciame sincronizzato; Animator spento oltre la distanza di animazione; cadavere 1,5 s per vedere la caduta. Hitbox della testa e del busto nella posa media di corsa (fisse, uguali sul server).
- **Nuovo personaggio del player (Pilot)**: modello Tripo con lo stesso scheletro di AIRO, preso da `Setup/9` se i binari ci sono (gambe, mira e mani sull'arma di Manuel funzionano uguali, 12/12 test del rig). Piedi a terra calcolati dai vertici veri (prima i bounds larghi dello SkinnedMeshRenderer alzavano il corpo).

### Added (modello del Runner, Matteo)
- **Runner con il modello vero**: mutante bio-meccanico (visore fuso, pelle cremisi, tubi sulla schiena) al posto della capsula, generato da immagine AI + Tripo 3D e ridotto a 15k triangoli per lo sciame. Mesh statica: si muove con l'animazione procedurale di `EnemyVisual` (inclinazione, oscillazione, affondo, caduta). Hitbox della testa ricavata dal modello (testa protesa in avanti). `Setup/6` usa il modello se i binari ci sono (`EnemyTypeSpec.ModelDir`), altrimenti la sagoma greybox.

### Added (personaggio AIRO, Manuel)
- **Corpo del player: pilota sci-fi AIRO** (modello nostro da Blender, ~75k triangoli, PBR 4K, rig umanoide a 52 ossa) al posto della capsula, costruito da `FpsWave/Setup/9. Build Player Character (AIRO)` (richiamato anche da `Setup/3`).
- **Animazione procedurale** (`Player/Character/`): gambe con IK a due ossa sul ciclo del passo (camminata, corsa, strafe, indietro), bacino che scende in crouch, piedi sollevati in aria, busto e testa che seguono la mira, braccia con IK sull'impugnatura dell'arma in terza persona e dita chiuse sull'arma.
- **Armi in terza persona** (`Prefabs/Weapons/WM_*`, `WorldWeaponModel`) generate dai view model: fucile e pistola impugnati dal corpo, con rinculo visibile e tracciante degli altri giocatori che parte dalla volata.
- **Prima persona**: si vedono gambe e bacino guardando in basso (mesh senza testa, collo, braccia e piastra) e l'ombra completa del personaggio con la posa dell'arma.
- Test EditMode del rig sul prefab vero (passo, strafe, crouch, salto, mani sull'impugnatura a ±45°): 24 test nuovi.

### Added (Fase 1: tuta, Brute, squadra)
- **Tuta Mk-VII**: energia (100, si ricarica dopo 1.5 s) autorevole sul server e tre abilità: **Pulse scan** (Q, 25: rivela a tutta la squadra i nemici entro 40 m per 5 s, anche dietro i muri), **Armor mode** (F, 40: danni subiti -60% per 4 s, movimento -15%), **Overcharge** (V, 30: corsa +35% per 3.5 s). Barra SUIT e tasti nel HUD, bordo colorato quando sono attive.
- **Brute**: secondo nemico (600 HP, 3.5 m/s, raro finché non ci sono le wave) con **corazza a piastre che si rompono** (petto, schiena, spalle, elmo: ognuna assorbe l'85% del danno finché regge, poi salta via con detriti) e **nucleo luminoso** sul petto come punto debole; **carica**: si ferma, si prepara (affondo all'indietro), scatta in linea retta e spinge via chi prende. Sistema di piastre generico (`EnemyArmor` + `ArmorPlate`) e abilità dei nemici (`IEnemyAbility`).
- **A terra e rianimazione** (`Player/PlayerLife`): a 0 HP si va a terra (si striscia, solo pistola, 30 s); un compagno tiene premuto E per 3 s e lo rialza al 30% con uno scudo breve per entrambi; da soli (o tutti a terra) ci si rialza dopo 6 s, morti si torna dopo 10 s (temporaneo fino alle wave). Indicatore "REVIVE" sui compagni a terra, pannello DOWNED con il tempo, prompt per rianimare.
- **Ping di squadra** (tasto centrale del mouse o Z): punto o nemico (il ping lo segue), visibile a tutti con la distanza per 7 s.
- **Voce della tuta**: messaggi brevi sotto la bussola (armatura critica, a terra, compagno a terra, rianimato, scansione, piastra rotta, energia insufficiente).
- Numeri di danno color acciaio sulle piastre, scintille invece del sangue sul metallo.

### Changed
- `GameInfo.ProtocolVersion` 2 → 3 (nuovi componenti di rete sul Player e sul Brute).
- Il ritorno in vita automatico di `EnemyTarget` è sostituito da `PlayerLife`.
- Suoni: il sistema audio e gli effetti sonori provati in questa fase sono stati tolti su richiesta di Matteo (il gioco resta senza suoni per ora).

### Changed (HUD stile Crysis, rinculo stile Rainbow Six Siege)
- **HUD "visore della tuta"** (riferimento Crysis 2/3), sottile e senza pannelli pieni: obiettivo in alto a sinistra, bussola con direzione in un riquadro, telemetria in alto a destra (se attiva); in basso a sinistra integrità (valore/massimo, stato NOMINALE/DANNEGGIATA/CRITICA, segmenti, righello); in basso a destra arma (tasto, nome, AUTO/SEMI), munizioni grandi con una tacca per colpo, ricarica, striscia delle armi in dotazione; angoli sottili attorno ai blocchi; mirino a quattro tratti; rosso del danno solo ai margini. Misure su 1920x1080 scalate con lo schermo. Tolti radar, biosegnale, scanline e cornice.
- **Calore solo per le armi a energia** (`WeaponFeel.UsesHeat`, spento per le armi a proiettili attuali): senza calore niente barra nel HUD, niente luce che vira al caldo né tremolio.
- Texture del HUD e del menu generate da `Setup/7` (`Editor/UiTextureGenerator`, in `ThirdParty/_Own/Hud/`), anche dalle sagome Kenney UI Pack Sci-Fi e dagli ologrammi rubberduck (CC0); font Rajdhani/Orbitron/Share Tech Mono (OFL). Il menu usa piastre con tacche, esagoni, scanline e anello olografico. Numeri di danno in Rajdhani Bold.
- **Rinculo stile Rainbow Six Siege**: ogni colpo alza la mira e lì restano anche i proiettili (85% del rinculo permanente nelle automatiche, 80% nelle semi, da compensare col mouse), deriva laterale riconoscibile per arma; l'arma in mano si muove pochissimo (calcio di ~1° attorno all'impugnatura, non attorno all'occhio) così la mira resta visibile; tremore e punch del FOV ridotti.
- **Rinculo rivisto**: i colpi singoli (tap) saltano meno della raffica (primo colpo 0.75x nelle automatiche, pistola più morbida), anche col tap la mira sale e resta come in Siege; salita con variazione casuale per colpo (±15% automatiche, ±20% semi) e un po' più di deriva laterale casuale, senza esagerare.
- **Traccianti da gioco AAA**: scia corta che viaggia a 420 m/s invece di una linea fissa dalla canna al bersaglio (muovendosi non resta una riga nel mondo); per chi spara solo un colpo su tre delle automatiche e nessuno per le semiautomatiche; colore caldo e realistico per le armi a proiettili (`WeaponFeel.BallisticColor`, anche per la luce di volata), colore dell'energia solo per le armi a energia.
- **Numeri di danno sobri**: bianco per i colpi normali, oro tenue per quelli di precisione (testa/punto debole/critico), niente rosso né "!" ; pop più piccolo, salita più corta, solo un'ombra morbida.
- **Fuoco da fianco a dispersione**: da fianco la mira non sale più (piccolo calcio visivo che torna da solo, `WeaponFeel.HipRecoilMultiplier` 0.2-0.3) e la precisione peggiora a ogni colpo fino al massimo del bloom; il bloom ora cala solo dopo 0.2 s senza sparare (`WeaponState.BloomRecoveryDelay`), prima si annullava già tra un colpo e l'altro. Mirando il bloom pesa solo il 20% e resta il rinculo alla Siege (salita da compensare).
- **Maneggio dell'arma più realistico e morbido**: oscillazione del mouse su una molla (si assesta con un rimbalzo minimo), inerzia del corpo (l'arma pesa partendo, frenando e cambiando direzione), passo sincronizzato con la distanza percorsa e più ampio in corsa, transizioni smorzate di corsa e accovacciamento (nuova posa accovacciati), arma che si abbassa allo stacco del salto, sale in caduta e affonda all'atterraggio.
- **Movimento dell'arma più lento e morbido (rif. Call of Duty)**: passo a ritmo umano (~2 passi/s camminando, ~2.7 correndo, prima ~5.6), ondeggio più piccolo e senza spigoli, transizioni di corsa (0.24 s) e accovacciamento (0.2 s) più lente, oscillazione del mouse e inclinazioni più morbide.
- **Calcio visivo dello sparo ridotto**: calcio dell'arma dimezzato (circa 0.3° sul fucile), rinculo della camera da fianco 0.12-0.18, tremore e punch del FOV dimezzati, calcio procedurale dei view model 6° → 2.5°.
- **Gunplay stile Call of Duty moderno**:
  - rinculo: primo colpo morbido, salita che cresce nella raffica con schema riconoscibile per arma, lasciando il grilletto la mira torna in parte da sola (parte permanente 0.55 automatiche, 0.4 semi);
  - dispersione da fianco che cresce sparando, muovendosi (+50% a piena velocità) e in aria (+90%), cala accovacciati; mirando niente bloom e dispersione quasi nulla (ADS spread 0.04 fucile, 0.08 pistola);
  - sprint-to-fire: sparare o mirare interrompe la corsa (`PlayerMotor.SprintBlocked`) e dopo la corsa serve un attimo prima del primo colpo (`WeaponDefinition.SprintToFireTime`: 0.22 s fucile, 0.13 s pistola); il colpo della semiautomatica premuto in quel momento parte appena l'arma è pronta;
  - mira con curva morbida (ease in/out) per posa dell'arma e FOV; tempi ADS 0.24 s fucile, 0.17 s pistola; FOV in mira del fucile 0.8;
  - flinch: un colpo subito sposta un po' la visuale (torna da sola) e fa tremare la camera.
- **Sobbalzo visivo dello sparo ancora ridotto**: calcio dell'arma 0.25x (automatiche) / 0.35x (semi), tremore e punch del FOV ridotti ancora di più, calcio procedurale 1.2°; la clip di sparo del pacchetto M4 (calcio molto ampio) si vede solo per 45 ms e poi sfuma su Idle (`WeaponViewModel._fireClipHold`).
- **Un filo di salita anche da fianco**: calcio della camera da fianco 0.15 (automatiche) / 0.2 (semi), di cui resta il 30-35% (`WeaponFeel.HipPermanentFraction`).
- **Gioco in inglese**: HUD, menu, messaggi Steam/rete, nomi delle armi e overlay di debug (vedi `CONVENTIONS.md`).
- **Arma più vicina alla camera** e un filo più bassa da fianco (4.5 cm più vicina, 1.2 cm più bassa; in mira non cambia).
- Nuovo script `tools/setup-ui-assets.ps1` (asset dell'interfaccia fuori dal repo, come quelli Fab).

### Added (sensazione delle armi)
- **Sensazione delle armi valida per qualunque modello** (`Weapons/Feel/`), con un profilo per arma (`WeaponFeel` nella WeaponDefinition, sezione "Sensazione"): rinculo della camera a due parti (una permanente da compensare, una che scatta e torna a molla), schema orizzontale riconoscibile per arma + parte casuale, primo colpo più forte e crescita nella raffica, riduzione mirando e accovacciati; calcio dell'arma a molla (indietro, su, rotazione), oscillazione che segue il mouse in ritardo, ondeggio di passo e corsa, inclinazione laterale, affondo all'atterraggio, posa di corsa, respiro da fermi, entrata dal basso al cambio arma; tremore a "trauma" e punch del FOV a ogni colpo. La forza resta nelle statistiche (RecoilPitch/RecoilYaw, modificabili dalle mod).
- **Tocchi sci-fi (cosmetici)**: colore dell'energia per arma su luce di volata e traccianti (visibili anche agli altri giocatori), lampo che si spegne in modo morbido, **calore** che cresce sparando (luce che vira al bianco-arancio, tremolio dell'arma oltre il 70%) con barra "CALORE"/"RAFFREDDAMENTO" nel HUD. Nessun effetto sul bilanciamento.
- Profili di partenza: automatica (raffica controllabile, salita marcata) e semi-automatica (colpo secco, ritorno rapido); strumento `FpsWave/Setup/8. Apply Weapon Feel` (richiamato da `Setup/3`).

### Changed (rete)
- `GameInfo.ProtocolVersion` 1 → 2: conferma colpo con danno, `EnemyTarget` sul player e colore del tracciante cambiano i messaggi di rete (le build 0.2.0 non possono unirsi a questa versione).
### Added (interfaccia)
- **HUD "visore da combattimento"** in UI Toolkit (palette ciano): integrità a 20 segmenti con colori di avviso e allarme "integrità critica", munizioni grandi con una tacca per colpo e barra di ricarica, mirino dinamico e hit marker colorati, bordo rosso quando si subisce danno, telemetria (link e FPS), striscia obiettivo. Sostituisce l'HUD OnGUI temporaneo (`PlayerHud`, rimosso).
- **Menu di gioco "terminale di comando"** in UI Toolkit (palette ambra, diversa dal HUD): principale (Gioca, Impostazioni, Esci), schieramento (crea lobby Steam / entra con codice, ospita / entra in LAN), connessione in corso con annulla, messaggi di errore; in partita Esc apre la pausa (Riprendi, Impostazioni, squadra con codice lobby, copia e inviti agli amici online, Abbandona, Esci). Il pannello OnGUI di sviluppo di `NetBootstrapMenu` resta nel codice ma è spento.
- **Impostazioni** salvate e applicate subito (`GameSettings`): sensibilità del mouse, FOV, volume, schermo intero, VSync, qualità grafica, FPS a schermo, numeri di danno.
- Strumento `FpsWave/Setup/7. Build UI` (PanelSettings, tema, HUD sul Player, menu nella scena Boot; richiamato anche da `Setup/3`).
### Added (M2 - nemici, prima parte)
- Primo nemico, il **Runner** (60 HP, 6 m/s, corpo a corpo): AI solo sul server con NavMeshAgent (insegue, si ferma, colpisce), bersaglio = player vivo più vicino con isteresi, al massimo 3 nemici attaccano lo stesso player e gli altri aspettano appena fuori portata.
- **EnemyDirector** (solo server): NavMesh costruito a runtime, nodi di spawn valutati a runtime (anello 25-60 m attorno al player con meno pressione, mai visibili da nessun player, percorso valido), gruppi da 2-4 scaglionati (max 2 spawn per frame), riciclo dei nemici bloccati o a oltre 90 m. Finché non arrivano le wave (M3) tiene in campo 4 + 4 nemici per player (tetto 40). F8 sull'Host attiva/disattiva gli spawn; riga di debug in basso a sinistra.
- Pool di rete per i nemici (despawn al pool, cache iniziale di 24), NetworkTransform server-authoritative a 15 Hz con solo posizione + yaw.
- Grafica procedurale del Runner sui client (corsa, affondo, colore per la salute, posa a terra) senza dati di rete aggiuntivi; hitbox separate dal modello animato.
- Greybox: 16 muri di copertura alti 4 m e 102 nodi di spawn ai lati dei ripari.
- **Tipi di nemico guidati dai dati**: ogni tipo ha la sua scheda `EnemyDefinition` (`Data/Enemies/`: salute, movimento, attacco, peso di spawn, gruppo, costo, wave d'ingresso) e il suo prefab (`Prefabs/Enemies/`); il director sceglie il tipo di ogni gruppo in base al peso. Catalogo unico dei tipi in `EnemyContentBuilder` (una voce = un nemico). Codice in `Scripts/Enemies/Core` e `Scripts/Enemies/Spawning`.
- **Numeri di danno**: il server conferma ogni colpo a chi ha sparato (punto, danno, critico/testa/uccisione) e il client mostra numeri che si gonfiano, salgono e svaniscono; bianco normale, oro testa, giallo con "!" critico, rosso uccisione. Funzionano anche sui bersagli del poligono.
- Strumento `FpsWave/Setup/6. Build Enemies` (richiamato anche da `Setup/3`).
- Test EditMode della selezione dei punti di spawn, della scelta del tipo, della conferma colpo, delle curve dei numeri di danno e dei calcoli del HUD (53/53).

### Fixed
- Il segno nero del proiettile restava sospeso in aria dopo la morte del nemico: sui personaggi ora c'è uno schizzo rosso che sparisce in 0.14 s (nessun segno permanente) e da morto il nemico non è più colpibile (hitbox spente), così i colpi finiscono sull'ambiente.

### Changed
- Validazione dei colpi: tolleranza sul bersaglio allargata di velocità * 0.35 s per i bersagli in movimento (`IMovingTarget`), necessaria per colpire i nemici che corrono.
- Conferma colpo dal server a ogni colpo andato a segno (prima solo per kill e critici), ~20 byte per colpo.
- Animazione del Runner più contenuta (inclinazione e affondo ridotti) perché il corpo visibile resti vicino alle hitbox.
- Player: componente `EnemyTarget` (registro dei bersagli dei nemici). TEMPORANEO: dopo la morte il player torna in vita sul posto dopo 5 s, finché non arrivano "a terra" e rianimazione.

## [0.2.0] - 2026-11-21
### Added (M0b - Steam)
- Steam: Steamworks.NET + FishySteamworks, lobby per amici con controllo di versione, ingresso con codice lobby, inviti dal gioco o da Steam, "Unisciti alla partita" dalla lista amici.
- Multipass con Tugboat (LAN/Multiplayer Play Mode) e FishySteamworks (relay Steam) sullo stesso NetworkManager; menu di sviluppo con Host/Join LAN e Host/Join Steam.
- Strumenti `FpsWave/Setup/5. Configure Steam (Multipass)` e `FpsWave/Build/Windows + Zip (test con amici)` (exe, steam_appid.txt, LEGGIMI.txt, zip senza simboli di debug).
- Pistola realistica (Beretta M92, Fab) con animazione procedurale: estrazione, calcio, carrello, ricarica.

### Changed
- Build Windows in DirectX 11 (overlay di Steam più affidabile), esecuzione in background attiva.

### Added (M1)
- Sistema Stat/Modifier (formula unica, clamp, cache) con test.
- Salute autorevole sul server (SyncVar), zone colpibili con weakspot, bersagli di allenamento che si rigenerano.
- Controller FPS client-authoritative: camminata, sprint, crouch con controllo del soffitto, salto; visuale con rinculo; input definito in codice (tastiera/mouse + gamepad); controllo di plausibilità lato server.
- Armi hitscan: pistola (semi-auto) e fucile d'assalto di test (auto), ADS, bloom, ricarica; validazione server di cadenza (token bucket), munizioni, origine, gittata, bersaglio e linea di vista.
- Tracer e segni d'impatto in pool, flash di sparo.
- HUD temporaneo: mirino dinamico, hit marker (colpo/weakspot/critico/kill), munizioni, salute, FPS, RTT.
- Sessione: caricamento globale della scena Greybox all'avvio del server, ritorno a Boot alla disconnessione, spawn dei player a rotazione.
- Strumento `FpsWave/Setup/3. Build M1 Content` (layer, materiali, armi, prefab, mappa greybox con SceneId stabili).
- Tasto di sviluppo Home per riallineare la visuale.
- Fucile d'assalto realistico in prima persona (M4 + braccia con guanti, Fab): animazioni di estrazione, sparo e ricarica (normale e a vuoto, velocità adattata al tempo di ricarica), materiali URP, volata animata; strumento `FpsWave/Setup/4. Build Weapon View Models`.
- Posizioni hip/ADS configurabili per arma (`WeaponDefinition`).

### Fixed
- Ricompilazione degli script bloccata durante il Play (si applica all'uscita): evitava eccezioni a ogni frame per lo stato armi azzerato.
- Multiplayer Play Mode: il Play parte sempre dalla scena Boot (anche nei giocatori virtuali, che entravano con Greybox aperta e non si collegavano) e all'uscita dal Play client e server vengono chiusi esplicitamente (col domain reload disattivato il server Tugboat restava vivo e la porta 7770 risultava occupata). Il menu di sviluppo segnala la porta occupata invece di lasciare un client orfano.
- Intervallo di sparo calcolato in double (evitava un colpo per errore di arrotondamento float).


## [0.1.0] - 2026-11-21 (tag git non pubblicato)
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
