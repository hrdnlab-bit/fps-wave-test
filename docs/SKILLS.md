# SKILL IN USO - FPS Wave Test

Principio: solo le skill che servono davvero a questo gioco. Ogni skill aggiunge istruzioni da seguire, quindi meno è meglio. Le skill di processo servono per la qualità, non per chiedere permessi (vedi "Autonomia" in `SYSTEM_PROMPT.md`).

## Attive
| Skill | Tipo | Uso |
|---|---|---|
| `fps-wave-dev-workflow` | Personalizzata | Ciclo di lavoro autonomo: lettura documenti, implementazione, verifica (Editor.log, test, Play Mode host+client), Conventional Commits, sync documenti repo/Progetto, handoff |
| `unity-coop-netcode-review` | Personalizzata | Checklist per il modello PvE MVP (autorità, validazione spari, banda, pooling di rete) + budget di performance |
| `mod-system-balance` | Personalizzata | Formule Stat/Modifier, costi mod, controlli di bilanciamento del DPS contro la difficoltà endless |
| `frontend-design` | Disponibile nell'ambiente | Design di HUD e menu (adattato a Unity UI Toolkit) |
| `skill-creator` | Disponibile nell'ambiente | Creare o modificare le skill sopra |

Le tre skill personalizzate sono anche nel repo, in `.claude/skills/`: le sessioni Claude Code di chiunque cloni il progetto le caricano automaticamente. La copia nel repo è quella di riferimento per il lavoro in due (contiene le regole su branch, lock e handoff per persona).

## Non installate (e perché)
| Skill | Motivo |
|---|---|
| brainstorming, grill-me, writing-plans, executing-plans | Coperte dal ciclo di lavoro autonomo e da ROADMAP/DECISIONS |
| test-driven-development, systematic-debugging, verification-before-completion, code-review | Le parti utili sono integrate in `fps-wave-dev-workflow` e `unity-coop-netcode-review` |
| handoff, claude-md-management, context-mode | Coperte dalle regole di handoff e dai documenti divisi |
| mcp-builder, claude-api, claude-automation-recommender | Non servono al gameplay |
| andrej-karpathy-guidelines, game-development, build-world | Generiche; il dominio è già nei documenti tematici |

Si possono aggiungere più avanti se emerge un bisogno concreto.

## Stato
| Skill | Stato | Data |
|---|---|---|
| fps-wave-dev-workflow | Attiva (salvata dall'utente) | 2026-11-21 |
| unity-coop-netcode-review | Attiva (salvata dall'utente) | 2026-11-21 |
| mod-system-balance | Attiva (salvata dall'utente) | 2026-11-21 |
| frontend-design | Disponibile | 2026-11-21 |
| skill-creator | Disponibile | 2026-11-21 |
