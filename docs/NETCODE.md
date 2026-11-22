# NETCODE - Modello PvE MVP

## 1. Libreria
**FishNet** (Fish-Networking, gratuito e open source) con:
- **Tugboat** per test locali (più istanze sullo stesso PC, LAN);
- **FishySteamworks** + **Steamworks.NET** per Steam (lobby, inviti, relay/NAT traversal via Steam Datagram Relay), Spacewar App ID 480 in sviluppo.

Motivi della scelta (dettagli in `DECISIONS.md`): attivamente mantenuto, pooling e interest management integrati, transport Steam mantenuto dallo stesso autore, buone prestazioni di banda. Alternative valutate: Netcode for GameObjects + transport Facepunch della community; Mirror + FizzySteamworks.

## 2. Principio
Co-op PvE tra amici: l'obiettivo è **reattività locale** e **economia autorevole**, non anti-cheat competitivo. Niente prediction/rollback né lag compensation completa.

| Sistema | Autorità | Come |
|---|---|---|
| Movimento del player | Client (owner) | NetworkTransform client-authoritative; il server fa controlli di plausibilità (velocità massima, teletrasporti) e corregge solo se fuori soglia |
| Sparo | Client rileva, server valida | Il client fa il raycast locale e mostra subito il feedback (muzzle, tracer, hit marker); invia `Shot(seq, origine, direzione, idBersaglio, puntoImpatto, weakspot)`; il server controlla cadenza, munizioni, distanza, linea di vista sulla geometria statica e che il bersaglio fosse entro una tolleranza dalla sua posizione (raggio + velocità * RTT); poi applica il danno |
| Danno, salute, morte, a terra/rianimazione | Server | Variabili sincronizzate |
| Nemici e AI | Server | NavMesh solo sull'Host; i client interpolano le posizioni |
| Wave, difficoltà, mutazioni, spawn, drop, risorse, mod, acquisti | Server | I client inviano richieste, il server valida e risponde |
| Mappa | Deterministica | Seed condiviso (vedi `MAP_ZONES.md`) |
| Effetti cosmetici (VFX, decal, suoni) | Locale | Eventi leggeri, mai oggetti di rete |

## 3. Banda e frequenze *(default)*
- Tick server: **30 Hz** (sufficiente per PvE, dimezza il costo CPU dell'Host rispetto a 60).
- Player: 30 Hz. Nemici: 15-20 Hz entro 40 m, 5-10 Hz oltre; interest management per distanza.
- Quantizzazione di posizioni e rotazioni (yaw in 16 bit, pitch in 8 bit); i nemici replicano solo yaw.
- Messaggi aggregati per tick; spari automatici raggruppati.
- Budget: vedi `TECH_BUDGETS.md`.

## 4. Sessione
- Lobby Steam: crea, invita, entra (anche da invito Steam); max 4.
- Join in corso: solo durante la preparazione; chi entra durante una wave è spettatore fino alla preparazione successiva.
- Disconnessione Host: fine partita e ritorno al menu per tutti (host migration fuori scope).
- Controllo di versione (`GameInfo.ProtocolVersion`) al momento del join.

## 4b. Steam (M0b, implementato)
- Pacchetti: Steamworks.NET 2025.165.0 (Package Manager, URL git fissato) e FishySteamworks 4.1.0 copiato in `Assets/ThirdParty/FishySteamworks/` (licenza BSD 2-Clause, asmdef proprio).
- `NetworkManager` in Boot con **Multipass**: indice 0 = Tugboat (LAN, Multiplayer Play Mode), indice 1 = FishySteamworks peer to peer (relay Steam). Azioni server globali **disattivate**: si avvia solo il transport scelto, sempre tramite `NetTransports` (anche l'arresto; `ServerManager.Start/StopConnection` non vanno usati direttamente). Configurazione ripetibile: `FpsWave/Setup/5. Configure Steam (Multipass)`.
- `SteamService`: `SteamAPI.InitEx` (serve `steam_appid.txt` con 480 nella cartella del progetto e accanto all'exe), callback, lobby **solo amici** da 4 posti con dati `fpswave_host` (SteamID64 dell'Host), `fpswave_proto`, `fpswave_ver`.
- Ingresso: **codice lobby** (l'Host lo copia, l'amico lo incolla in "Join Steam"), invito dal gioco (elenco amici online, `InviteUserToLobby`), "Unisciti alla partita" dalla lista amici (rich presence `+connect_lobby`), invito dall'overlay quando disponibile.
- **Controllo versione al join**: prima di connettersi il client confronta `fpswave_proto` con `GameInfo.ProtocolVersion`; se diverso esce dalla lobby e mostra le due versioni.
- Steam è disattivato nei giocatori virtuali di Multiplayer Play Mode (usano Tugboat); l'Editor principale inizializza Steam ma in Editor l'overlay non compare.
- Overlay di Steam: con il gioco avviato fuori dal client Steam e DirectX 12 non si aggancia; le build Windows usano DirectX 11 e l'ingresso non dipende dall'overlay.

## 5. Struttura del codice
- Per funzionalità (`Player/`, `Weapons/`, `Enemies/`...); in ogni funzionalità la logica server è in metodi `[Server]` o in classi `*Server`, quella client in `*Client` / `*View`, i dati condivisi in `*Data`/ScriptableObject.
- Logica pura (formule, tabelle, director, difficoltà) in classi C# senza MonoBehaviour: testabile in EditMode e indipendente dalla rete.

## 6. Test
- Locale: **Multiplayer Play Mode** di Unity 6 (fino a 4 istanze) + Tugboat.
- Condizioni degradate: simulatore di latenza/perdita (150 ms, 2-5% loss, jitter).
- Steam: PC dell'utente + PC di un amico con il suo account Steam (non si possono avviare due client Steam con lo stesso account sulla stessa macchina). Il build di test va condiviso con l'amico (zip o branch Steam dopo il rilascio dell'App ID).
- Overlay di debug: RTT, banda in/out, tick, oggetti osservati.

## Nemici (implementazione M2)
- `NetworkObject` in pool (`DespawnType.Pool`, cache di 24 sul server), `NetworkTransform` server-authoritative ogni 2 tick (15 Hz a 30 Hz di tick), solo posizione + yaw (rotazione X/Z bloccate).
- NavMeshAgent attivo solo sul server; salute in SyncVar; attacco = danno sul server + un `ObserversRpc` senza parametri per l'affondo cosmetico.
- Animazione procedurale calcolata sui client dalla posizione interpolata: nessun dato in più in rete.
- Validazione colpi: tolleranza + velocità del bersaglio * 0.35 s (`IMovingTarget`).
- Interest management per distanza non ancora attivo (Greybox piccola: tutti vedono tutto); da aggiungere con la mappa vasta (M7).
- Conferma colpo: `TargetRpc` a chi ha sparato per ogni colpo andato a segno (punto 12 byte + danno `ushort` + flag `byte` critico/testa/uccisione); alimenta i numeri di danno e l'hit marker confermato. Con il fucile a 600 colpi/min sono ~200 B/s nel caso peggiore.
- `RpcShotFx` (tracciante per gli altri giocatori) porta l'indice dell'arma per il colore dell'energia (1 byte). Rinculo, molle, tremore e calore sono solo locali: nessun dato in rete. Protocollo di rete alla versione 2.

## Fase 1 (tuta, a terra, ping, Brute)
- `SuitAbilities` (Player): energia `SyncVar<float>` replicata a passi di 1 punto, flag delle abilità attive `SyncVar<byte>`; il client chiede con `ServerRpc(byte)`, il server valida (vivo, energia, ricarica) e avvisa tutti con `ObserversRpc(byte, Vector3)` (suoni e scansione). Riduzione dei danni via `Health.ServerDamageScale` (solo server).
- `PlayerLife` (Player): stato `SyncVar<byte>`, avanzamento della rianimazione e tempo rimasto `SyncVar<float>` (il tempo a 4 Hz). Chi rianima manda `ServerRpc(PlayerLife)` ogni 0.15 s mentre tiene premuto; il server controlla distanza e stato.
- `PlayerPing`: `ServerRpc(Vector3, NetworkObject)` limitato dal server, poi `ObserversRpc` a tutti (≈20 byte per ping).
- `EnemyArmor` (Brute): salute delle piastre solo sul server, piastre rotte in una `SyncVar<byte>` (maschera); la conferma colpo porta due flag in più (`ArmorHit`, `ArmorBreak`).
- `BruteCharge`: solo server (movimento con `NavMeshAgent.Move`), due `ObserversRpc` cosmetici (preparazione, impatto) e un `TargetRpc` al proprietario colpito per lo spintone (`PlayerMotor.TargetKnockback`). La tolleranza della validazione colpi usa la velocità di carica (`IEnemyAbility.ServerSpeedOverride`).
- Suoni: solo locali; gli spari degli altri partono da `RpcShotFx` già esistente.
- Protocollo di rete alla versione 3.
