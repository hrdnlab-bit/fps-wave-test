# FPS Wave Test - guida per chi si unisce allo sviluppo

Questa guida porta il tuo PC allo stesso punto di quello di Manuel, così tu, lui e i vostri due Claude lavorate sullo stesso progetto senza pestarvi i piedi. Tempo stimato: 1-2 ore, quasi tutte di download.

**Come funziona in due parole:** il codice e i documenti stanno su GitHub (`hrdnlab-bit/fps-wave-test`). Ognuno lavora sul proprio branch con il proprio Claude e porta le modifiche su `main` con una pull request. Quello che ha fatto uno, l'altro (e il suo Claude) lo ritrova con un `git pull`, perché ogni sessione aggiorna i documenti in `docs/`.

---

## 1. Cosa installare

| Programma | Dove | Note |
|---|---|---|
| Git per Windows | https://git-scm.com/download/win | Opzioni di default (include Git LFS e Git Credential Manager) |
| GitHub CLI (`gh`) | https://cli.github.com | Serve al tuo Claude per aprire le pull request |
| Unity Hub + **Unity 6000.6.9f1** | https://unity.com/download | In Unity Hub: Installs > Install Editor. Se quella versione non compare, cercala nell'archivio delle versioni di Unity e usa il pulsante "Install" che apre Unity Hub. Serve la stessa versione esatta di Manuel |
| Steam | https://store.steampowered.com/about | Deve essere aperto e collegato al tuo account quando provi il multiplayer |
| App desktop di Claude | https://claude.ai/download | Si usa la sezione **Code** (Claude Code) |

---

## 2. Accesso a GitHub e clone

1. Crea un account GitHub (se non ce l'hai) e manda il tuo nome utente a Manuel: ti invia l'invito come collaboratore. Accettalo dall'email o da https://github.com/hrdnlab-bit/fps-wave-test/invitations.
2. Apri **Git Bash** (o il Prompt dei comandi) e clona il progetto in una cartella senza spazi né OneDrive, per esempio:
   ```
   git clone https://github.com/hrdnlab-bit/fps-wave-test.git C:\Users\<tuo-utente>\FpsWaveTest
   cd C:\Users\<tuo-utente>\FpsWaveTest
   ```
3. Configura Git (una volta sola, dentro la cartella del progetto):
   ```
   git config user.name "Il tuo nome"
   git config user.email "la-tua-email-di-github"
   git lfs install
   git config merge.unityyamlmerge.name "Unity SmartMerge"
   git config merge.unityyamlmerge.driver "\"C:/Program Files/Unity/Hub/Editor/6000.6.9f1/Editor/Data/Tools/UnityYAMLMerge.exe\" merge -p %O %B %A %A"
   git config merge.unityyamlmerge.recursive binary
   ```
   Le ultime tre righe fanno usare a Git lo strumento di Unity per unire scene e prefab (si riducono i conflitti).
4. Accedi a GitHub dalla riga di comando (serve al tuo Claude per push e pull request):
   ```
   gh auth login
   ```
   Scegli GitHub.com > HTTPS > accesso dal browser.

---

## 3. Asset 3D (prima di aprire Unity!)

Modelli e texture delle armi **non sono nel repo**: la licenza Fab non permette di ridistribuirli, quindi ognuno li scarica gratis con il proprio account. Nel repo ci sono solo i file `.meta`, che tengono collegati materiali e prefab.

> **Importante:** se apri Unity prima di aver messo a posto gli asset, Unity cancella quei `.meta` e i riferimenti si rompono. Segui l'ordine qui sotto.

1. Accedi a https://www.fab.com con un account Epic e, per ognuno dei due pacchetti, premi **Aggiungi a La mia libreria** e poi **Scarica** (formato **fbx**). Lascia i file nella cartella **Download**:
   - **M4 - FPS Weapon Animations Pack FREE**: https://www.fab.com/listings/38da8f1e-cb54-4cda-beb1-6e55642a55a9 (file `m4_animations.zip`, va bene anche zippato)
   - **FPS Guns 4K - Pistol 2**: https://www.fab.com/listings/0d1c1104-b08d-44e1-88a9-4d59106636ac (file `fps_guns_4k_pistol_2.zip`; puoi lasciarlo zippato o estrarlo in `Download\FPS Guns 4K - Pistol 2`)
2. Con Unity chiuso, dalla cartella del progetto esegui:
   ```
   powershell -ExecutionPolicy Bypass -File tools\setup-fab-assets.ps1
   ```
   Lo script copia i file in `Assets/ThirdParty/Fab/` con i nomi giusti e genera le texture combinate che servono a URP. Alla fine deve dire **"Asset pronti"**. Se i file non sono in Download, aggiungi `-Downloads "percorso\della\cartella"`.
3. Controlla con `git status`: non deve risultare nessun file modificato o cancellato (gli asset sono esclusi da git apposta).

---

## 4. Primo avvio di Unity

1. Unity Hub > **Add** > **Add project from disk** > scegli la cartella `FpsWaveTest`.
2. Il primo avvio è lungo: Unity scarica i pacchetti (FishNet, Steamworks.NET) e importa tutto. Serve Git installato, perché alcuni pacchetti arrivano da GitHub.
3. Premi **Play**: si parte sempre dalla scena Boot, l'Editor fa da Host LAN e ti trovi nel poligono di tiro. Per uscire dal Play usa **Ctrl+P**.
4. Se la Console mostra errori, oppure se `git status` mostra `.meta` cancellati in `Assets/ThirdParty`, chiudi Unity e ripristina i `.meta`:
   ```
   git checkout -- Assets/ThirdParty
   powershell -ExecutionPolicy Bypass -File tools\setup-fab-assets.ps1 -Force
   ```
   Poi riapri Unity ed esegui **FpsWave > Setup > 4. Build Weapon View Models**.

---

## 5. Collegare il tuo Claude

1. Apri l'app desktop di Claude > sezione **Code** > apri la cartella del progetto (`C:\Users\<tuo-utente>\FpsWaveTest`).
2. Claude legge da solo `CLAUDE.md` e le skill del progetto in `.claude/skills/`, cioè le stesse regole che usa il Claude di Manuel.
3. Primo messaggio da scrivere:
   > Sono **<tuo nome>**. Leggi CLAUDE.md e i documenti indicati, crea il mio file in docs/handoff/ e dimmi lo stato del progetto e cosa posso prendere in carico.
4. Durante il lavoro il tuo Claude modifica i file nella cartella, verifica in Unity (compilazione, test, Play) e fa commit e pull request a nome tuo. Usa il tuo abbonamento.

---

## 6. Come si lavora in due

**Inizio sessione** (lo fa il tuo Claude, ma è bene saperlo):
- `git pull` su `main`;
- lettura di `docs/HANDOFF.md` (stato comune), `docs/handoff/<tuo-nome>.md` (il tuo lavoro) e `docs/LOCKS.md` (chi sta toccando cosa);
- se nel pull sono cambiati `Packages/` o `ProjectSettings/`, riavvio di Unity.

**Durante:**
- un branch per argomento: `<tuo-nome>/<argomento>` (es. `<tuo-nome>/m2-nemici`);
- prima di toccare scene (`.unity`), prefab, `Assets/_Project/Data/` o `ProjectSettings/`, si scrive una riga in `docs/LOCKS.md`; ciò che è bloccato dall'altro non si tocca;
- il codice C# non si blocca: i conflitti si risolvono normalmente.

**Fine sessione:**
- il tuo Claude aggiorna il tuo `docs/handoff/<tuo-nome>.md`, `CHANGELOG.md` e i documenti tematici;
- toglie i tuoi lock;
- fa push del branch e apre una pull request verso `main` (modello già pronto). Quando è verificata, la si unisce.
- Così, quando Manuel fa `git pull`, il suo Claude riprende esattamente da dove sei arrivato tu (e viceversa).

**Dividetevi le aree** per avere meno conflitti. Esempio: uno armi, rete e UI, l'altro nemici, wave e mappa (`docs/ROADMAP.md` per i prossimi lavori: M2 nemici, M3 wave). Le aree attive si dichiarano in `LOCKS.md`.

---

## 7. Provare il multiplayer insieme

Non serve una build: funziona anche dall'Editor, ognuno sul suo PC con il suo account Steam aperto.
1. Chi ospita: Play > nel menu in alto a sinistra **Disconnetti** > **Host Steam (crea lobby)** > **Copia codice**, e manda il codice all'altro.
2. L'altro: Play > **Disconnetti** > incolla il codice nel campo **Codice** > **Join Steam (codice lobby)**.
3. Dovete avere la stessa versione del progetto (lo stesso `main`): il gioco controlla la versione di rete e avvisa se è diversa.

Per una build da far provare ad altri: **FpsWave > Build > Windows + Zip** (istruzioni per chi gioca dentro lo zip, in `LEGGIMI.txt`).

---

## 8. Problemi comuni

| Problema | Soluzione |
|---|---|
| Armi rosa o mancanti, `.meta` cancellati | Sezione 4, punto 4 |
| "Porta 7770 occupata" | Chiudi altre istanze del gioco o i giocatori virtuali di Multiplayer Play Mode, poi riprova |
| "Steam non disponibile" | Apri Steam e accedi, poi rifai Play |
| Conflitto su una scena o un prefab | Tieni una delle due versioni e rigenera con il menu **FpsWave > Setup** (scene e prefab nascono da quegli strumenti) |
| Pacchetti in errore dopo un pull | Chiudi Unity, riaprilo e lascia finire l'importazione |

Domande sul progetto: chiedile al tuo Claude, che ha tutto in `docs/`.
