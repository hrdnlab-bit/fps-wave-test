# CHANGELOG

Formato: [Keep a Changelog](https://keepachangelog.com/it/1.1.0/), versioni semantiche.

## [Unreleased]
### Changed (HUD stile Crysis, rinculo stile Rainbow Six Siege)
- **HUD minimale "visore della tuta"** (riferimento Crysis 2/3): solo bussola stretta in alto, un blocco in basso a destra (arma, munizioni grandi, ricarica, calore, integrità a segmenti con valore), mirino a quattro tratti, hit marker, vignetta leggera ai bordi; avviso "INTEGRITÀ CRITICA" sotto il mirino; FPS/ping in alto a destra solo se attivi. Tolti pannelli pieni, radar, biosegnale, testi di stato, scanline e cornice. Il rosso del danno resta solo ai margini.
- Texture del HUD e del menu generate da `Setup/7` (`Editor/UiTextureGenerator`, in `ThirdParty/_Own/Hud/`), anche dalle sagome Kenney UI Pack Sci-Fi e dagli ologrammi rubberduck (CC0); font Rajdhani/Orbitron/Share Tech Mono (OFL). Il menu usa piastre con tacche, esagoni, scanline e anello olografico. Numeri di danno in Rajdhani Bold.
- **Rinculo stile Rainbow Six Siege**: ogni colpo alza la mira e lì restano anche i proiettili (85% del rinculo permanente nelle automatiche, 80% nelle semi, da compensare col mouse), deriva laterale riconoscibile per arma; l'arma in mano si muove pochissimo (calcio di ~1° attorno all'impugnatura, non attorno all'occhio) così la mira resta visibile; tremore e punch del FOV ridotti.
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
