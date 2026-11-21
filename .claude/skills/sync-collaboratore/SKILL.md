---
name: "sync-collaboratore"
description: "Solo nelle sessioni di Manuel: all'inizio o alla ripresa di ogni sessione, e quando chiede di aggiornare, scarica da GitHub le modifiche del collaboratore (Matteo Rodigliano), fa un recap veloce e allinea la cartella Unity sul PC."
---

# Sync dal collaboratore (sessioni di Manuel)

La cartella Unity di Manuel (`C:\Users\manue\FpsWaveTest`) **non è un clone git**: la allinea l'AI copiando i file dal clone nel cloud. Il commit a cui è allineata è scritto sul PC in `C:\Users\manue\FpsWaveTest\.fpswave-sync.json` (`{"commit": "<sha>", "date": "..."}`).

Esegui questa procedura **senza chiedere conferma** all'inizio di ogni sessione di Manuel, a ogni ripresa dopo una pausa e quando lui chiede "aggiorna" / "cosa ha fatto Matteo". Se non c'è niente di nuovo, una riga ("Nessuna novità da Matteo, PC allineato a `<sha>`") e si prosegue.

## 1. Scarica e calcola
1. Nel clone: `git fetch origin` (shallow: `--depth=200`), poi `git checkout main && git pull --ff-only`.
2. Leggi il marker dal PC (stage di `.fpswave-sync.json`). Se manca, usa l'ultimo commit di cui la sessione sa che è sul PC e crealo a fine procedura.
3. `python3 .claude/skills/sync-collaboratore/plan_sync.py --from <sha-marker>` e leggi il JSON.
4. Guarda anche i lavori **non ancora su `main`**: `git branch -r` (branch `matteo/...`) e le PR aperte (`gh api "repos/hrdnlab-bit/fps-wave-test/pulls?state=open"`). Questi non vanno sul PC: vanno solo nel recap.

## 2. Recap veloce (in chat, 5-10 righe)
- Per ogni commit/PR di Matteo: cosa ha fatto in una riga (leggi i diff e `docs/handoff/matteo*.md`).
- Cosa cambia per Manuel: API toccate, lock attivi in `docs/LOCKS.md`, strumenti `FpsWave/Setup` da rieseguire, asset nuovi da scaricare (con link da `docs/ASSETS.md`), Unity da riaprire.
- Eventuali conflitti con il lavoro in corso di Manuel (`docs/handoff/manuel.md`).

## 3. Allinea il PC
1. **Unity in Play?** Non serve fermarlo: con la ricompilazione bloccata durante il Play le modifiche si applicano all'uscita. Avvisa Manuel.
2. **Modifiche locali**: fai lo stage dal PC dei file in `check_local` e confrontali (ignorando CRLF) con la versione al commit del marker (`git show <sha>:<file>`). Se sono diversi, Manuel li ha cambiati sul PC: **non sovrascriverli**, segnalali e chiedi come procedere.
3. Copia sul PC i file di `copy` (`device_commit_files`, massimo 50 per chiamata; prima scrivili in `/mnt/user-data/outputs/sync/...` e aspetta qualche secondo che siano leggibili; poi controlla le dimensioni sul PC con `device_list_dir`).
4. `hold_meta`: per ognuno controlla se il binario esiste sul PC. Se c'è, copia il `.meta`; se manca, **non copiarlo** e di' a Manuel quale pacchetto scaricare (e di eseguire `tools/setup-fab-assets.ps1` se lo script è stato aggiornato) prima di aggiornare Unity.
5. `delete`: senza shell sul PC non si possono cancellare file; elencali a Manuel (percorsi completi) e chiedi di eliminarli, insieme ai loro `.meta`, con Unity chiuso o dalla finestra Project.
6. Aggiorna il marker sul PC con lo sha di destinazione.

## 4. Verifica e prosegui
1. Porta Unity in primo piano e fai aggiornare (Ctrl+R): compilazione 0 errori in `Logs/FpsWaveConsole.log`.
2. Se `reopen_unity`: chiedi a Manuel di riavviare Unity (o fallo tu se il controllo del computer è attivo). Se `setup_tools` non è vuoto, rieseguili (giocatori virtuali disattivati).
3. Test EditMode se è cambiato codice; Play rapido se è cambiata la rete o il gameplay.
4. Riprendi dai prossimi passi di `docs/handoff/manuel.md`, tenendo conto dei lock di Matteo.
