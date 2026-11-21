---
name: "unity-coop-netcode-review"
description: "Revisione di codice Unity/FishNet per il co-op PvE di FPS Wave Test: autorità, validazione spari, banda, pooling di rete e budget di performance."
---

# Revisione netcode e performance (co-op PvE MVP)

Usa questa checklist prima di committare codice che tocca rete, nemici, spawn, armi o rendering. Riferimenti: `NETCODE.md` e `TECH_BUDGETS.md`.

## Autorità
- [ ] Danno, salute, morte, a terra/rianimazione, wave, spawn, drop, risorse, mod e acquisti vengono decisi SOLO sul server.
- [ ] Il client invia richieste (RPC verso il server), mai stati finali.
- [ ] Ogni RPC dal client valida: chiamante proprietario, valori in range, cooldown/cadenza, risorse sufficienti.
- [ ] Movimento client-authoritative con controllo di plausibilità sul server (velocità max, teletrasporto).
- [ ] Sparo: il client mostra subito il feedback; il server valida cadenza, munizioni, distanza, linea di vista, tolleranza sulla posizione del bersaglio (raggio + velocità * RTT).
- [ ] Nessuna prediction/rollback/lag compensation completa (fuori scope MVP).
- [ ] Codice dell'AI e dello Spawn Director non eseguito sui client.

## Banda
- [ ] Nessun dato sincronizzato ogni frame se non necessario; frequenze come in NETCODE (player 30 Hz, nemici 15-20 Hz vicini, 5-10 Hz lontani).
- [ ] Posizioni/rotazioni quantizzate; nemici replicano solo yaw.
- [ ] Effetti cosmetici (VFX, decal, suoni) come eventi leggeri o calcolati localmente, mai NetworkObject.
- [ ] Interest management per distanza attivo sugli oggetti numerosi.
- [ ] Stima della banda del cambiamento rispetto al budget (down ≤ 48 KB/s, up ≤ 12 KB/s per client).

## Ciclo di vita degli oggetti
- [ ] Nemici, drop, proiettili usano il pool di rete (despawn al pool, non Destroy).
- [ ] Reset completo dello stato all'uscita dal pool (salute, target, flag, sottoscrizioni a eventi).
- [ ] Nessun riferimento a oggetti despawnati conservato.

## Join, disconnessione, determinismo
- [ ] Un client che entra in preparazione riceve lo stato corrente (wave, risorse, oggetti raccolti).
- [ ] Disconnessione di un client: i suoi oggetti vengono rilasciati, la partita continua.
- [ ] Generazione con seed: RNG proprio, nessun UnityEngine.Random, nessuna fisica nel posizionamento.

## Performance
- [ ] Zero allocazioni GC in Update/FixedUpdate/LateUpdate (niente LINQ, string concat, lambda che catturano, new di collezioni).
- [ ] Logica costosa a frequenza ridotta (tick, non ogni frame); raycast a budget per tick.
- [ ] Animator culling e LOD sui nemici; GPU instancing/SRP Batcher compatibili sui materiali.
- [ ] Rispetto di max nemici vivi, batch, SetPass e memoria texture di TECH_BUDGETS.

## Esito
Per ogni voce non soddisfatta: correggi prima del commit, oppure registra il motivo in `DECISIONS.md` se è una scelta consapevole.