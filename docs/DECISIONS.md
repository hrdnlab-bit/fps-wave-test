# REGISTRO DECISIONI

Formato: data | decisione | motivo | alternative scartate | chi.

| Data | Decisione | Motivo | Alternative | Chi |
|---|---|---|---|---|
| 2026-11-21 | Sviluppo autonomo: l'AI decide e registra, chiede solo per azioni irreversibili, spese e credenziali | Richiesta dell'utente | Approvazione a ogni passo | Utente |
| 2026-11-21 | Modello di rete PvE MVP: movimento client-authoritative con controlli di plausibilità, sparo rilevato dal client e validato dal server, niente prediction/lag compensation | Standard dei co-op PvE; molto meno complesso e meno bug di desync | Prediction + reconciliation + lag compensation (modello PvP) | Utente + AI |
| 2026-11-21 | Netcode: FishNet + Tugboat (locale) + FishySteamworks/Steamworks.NET (Steam) | Mantenuto attivamente, pooling e interest management integrati, transport Steam mantenuto dallo stesso autore | NGO + Facepunch transport (community, meno curato); Mirror + FizzySteamworks (valido, meno efficiente in banda) | AI |
| 2026-11-21 | Unity 6000.6.9f1 (già installato) | Evita un'installazione; Unity consiglia le update release per progetti nuovi | Unity 6.3 LTS | AI |
| 2026-11-21 | Spawn con Spawn Director a budget + selezione dei punti a punteggio | Standard professionale (L4D, KF2, DRG), scala con giocatori e wave | Spawn fissi per punto | AI |
| 2026-11-21 | Ambientazione: valle in quarantena + complesso militare di ricerca sci-fi; creature mutate con varianti bio-meccaniche | Scelta dell'utente (mix delle opzioni 1 e 3); il lato sci-fi resta "militare credibile" per restare coerente con asset realistici | Solo valle; urbano; desertico | Utente |
| 2026-11-21 | Partita endless: difficoltà crescente a ogni wave su statistiche, composizione, mutazioni e ritmo; boss ogni 10 wave | Scelta dell'utente | 10 wave + boss finale; 5 wave rapide | Utente |
| 2026-11-21 | Formule di difficoltà endless (HP con termine quadratico, danno e velocità con tetto, mutazioni ogni 5 wave, tetto nemici vivi fisso) | Difficoltà che cresce per qualità rispettando il budget di performance | Crescita solo del numero di nemici | AI (default) |
| 2026-11-21 | Percorso di sviluppo ibrido: setup + rete locale (M0a), poi gunplay (M1), poi Steam e debug (M0b) | Scelta dell'utente: si vede presto il gameplay senza rifare il codice di rete | M0 completo prima; gunplay single player prima | Utente |
| 2026-11-21 | Test Steam con il PC di un amico | Disponibilità dell'utente | Due PC propri | Utente |
| 2026-11-21 | Progressione solo dentro la partita nell'MVP | Scope contenuto | Meta-progressione subito | AI (default) |
| 2026-11-21 | Crediti e materiali condivisi di squadra, mod e Nuclei per giocatore | Meno frizione in co-op, build personali | Tutto per giocatore | AI (default) |
| 2026-11-21 | A terra 30 s, rianimazione 3 s, wipe = tutti a terra; fuoco amico off | Pilastro "ci si salva a vicenda" | Morte immediata | AI (default) |
| 2026-11-21 | Tick 30 Hz, max 40 nemici vivi nell'MVP | Costo CPU contenuto sull'Host | 60 Hz | AI (default) |
| 2026-11-21 | Disconnessione Host = fine partita; join solo in preparazione | Semplicità MVP | Host migration | AI (default) |
| 2026-11-21 | Documenti divisi per tema, un'informazione in un solo file, copia nel repo (`docs/`) e nel Progetto | Meno incoerenze, meno contesto consumato | Baseline unico | Utente + AI |
| 2026-11-21 | Progetto in `C:\Users\manue\FpsWaveTest`, template Universal 3D locale, senza AI Assistant, senza Unity Cloud e senza repo creato da Unity Hub | Cartella dedicata richiesta dall'utente; il repo esiste già | Riutilizzare `My project` (contiene un altro gioco) | Utente + AI |
| 2026-11-21 | Asset pesanti fuori dal repo: in `Assets/ThirdParty/` si versionano solo `.meta` e file di testo; binari sul PC con backup su Google Drive; l'amico riceve una build zippata | Il push LFS dall'ambiente dell'AI viene rifiutato; zero costi; i `.meta` mantengono i GUID | LFS dal PC dell'utente; repo senza LFS | Utente |
| 2026-11-21 | Feel tattico-arcade, ADS classico | Scelta dell'utente | Tattico realistico; arcade veloce; solo hip-fire | Utente |
| 2026-11-21 | Si parte con la sola pistola, la primaria si compra allo shop | Scelta dell'utente: progressione percepibile dalla prima wave | Fucile + pistola; scelta in lobby | Utente |
| 2026-11-21 | Scene e setup generati da strumenti Editor (`FpsWave/Setup`) | Ripetibili, versionabili, non dipendono dal controllo remoto dell'interfaccia | Costruzione manuale nell'Editor | AI |
| 2026-11-21 | In Editor avvio automatico: Host sull'istanza principale, join automatico dei giocatori virtuali MPPM | Test multiplayer senza clic; le finestre dei virtual player non sono controllabili da remoto | Menu manuale in ogni finestra | AI |
| 2026-11-21 | Mirror della Console su `Logs/FpsWaveConsole.log` | Verifica automatica di errori/warning senza accesso ad AppData | Screenshot della Console | AI |
| 2026-11-21 | Menu di sviluppo in OnGUI (temporaneo) | Rapido per M0a; sarà sostituito da UI Toolkit con la lobby Steam (M0b) | UI Toolkit subito | AI |
| 2026-11-21 | Skill attive: fps-wave-dev-workflow, unity-coop-netcode-review, mod-system-balance (+ frontend-design, skill-creator) | Solo quelle utili al gioco | L'elenco completo di 19 skill | Utente + AI |
