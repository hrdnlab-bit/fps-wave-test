#!/usr/bin/env python3
"""Piano di sincronizzazione repo -> cartella Unity sul PC di Manuel (non è un clone git).

Uso (dalla radice del clone):
  python3 .claude/skills/sync-collaboratore/plan_sync.py --from <sha-allineato-sul-PC> [--to origin/main]

Stampa un JSON con:
  commits      commit nuovi (autore, data, titolo), per il recap
  docs         documenti cambiati (da leggere per il recap, non vanno sul PC)
  copy         file da copiare sul PC (aggiunti/modificati nelle cartelle che usa Unity)
  check_local  sottoinsieme di copy che esisteva già: prima di sovrascriverlo va confrontato
               con la versione sul PC (se diversa = modifica locale di Manuel, non sovrascrivere)
  delete       file da togliere dal PC (rimossi o rinominati nel repo)
  hold_meta    .meta nuovi di asset binari fuori dal repo: copiarli SOLO se il binario è già sul PC,
               altrimenti Unity li cancella all'aggiornamento
  reopen_unity True se cambiano Packages/ o ProjectSettings/ (riaprire o aggiornare Unity)
  setup_tools  strumenti FpsWave/Setup toccati (da rieseguire)
"""
import argparse
import json
import subprocess
import sys

SCOPE = ("Assets/", "Packages/", "ProjectSettings/", "tools/", "steam_appid.txt")
DOC_PREFIXES = ("docs/", "CLAUDE.md", "README.md", ".claude/", ".github/")
SETUP_TOOLS = {
    "Assets/_Project/Scripts/Editor/M1ContentBuilder.cs": "FpsWave/Setup/3. Build M1 Content",
    "Assets/_Project/Scripts/Editor/WeaponViewModelBuilder.cs": "FpsWave/Setup/4. Build Weapon View Models",
    "Assets/_Project/Scripts/Editor/SteamSetupTools.cs": "FpsWave/Setup/5. Configure Steam (Multipass)",
}


def git(*args: str) -> str:
    return subprocess.run(["git", *args], check=True, capture_output=True, text=True).stdout


def in_scope(path: str) -> bool:
    return path.startswith(SCOPE)


def is_ignored(path: str) -> bool:
    return subprocess.run(["git", "check-ignore", "-q", path]).returncode == 0


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--from", dest="frm", required=True)
    parser.add_argument("--to", default="origin/main")
    a = parser.parse_args()

    log = git("log", "--reverse", "--no-merges", "--format=%h%x1f%an%x1f%ad%x1f%s", "--date=format:%d/%m %H:%M",
              f"{a.frm}..{a.to}")
    commits = [dict(zip(("sha", "author", "date", "title"), line.split("\x1f"))) for line in log.splitlines() if line]

    copy, check_local, delete, hold_meta, docs = [], [], [], [], []
    for line in git("diff", "--name-status", "-M", a.frm, a.to).splitlines():
        parts = line.split("\t")
        status = parts[0][0]
        if status == "R":
            old, new = parts[1], parts[2]
            if in_scope(old):
                delete.append(old)
            path = new
        else:
            path = parts[1]
        if path.startswith(DOC_PREFIXES):
            docs.append(path)
        if not in_scope(path):
            continue
        if status == "D":
            delete.append(path)
            continue
        if path.endswith(".meta") and path.startswith("Assets/ThirdParty/") and status in "AR":
            asset = path[:-5]
            # Binario escluso da git (non è nel repo): serve sul PC prima del .meta.
            if is_ignored(asset):
                hold_meta.append(path)
                continue
        copy.append(path)
        if status == "M":
            check_local.append(path)

    reopen = any(p.startswith(("Packages/", "ProjectSettings/")) for p in copy + delete)
    tools = sorted({SETUP_TOOLS[p] for p in copy if p in SETUP_TOOLS})
    json.dump({
        "from": a.frm, "to": git("rev-parse", "--short", a.to).strip(),
        "commits": commits, "docs": sorted(set(docs)), "copy": copy, "check_local": check_local,
        "delete": delete, "hold_meta": hold_meta, "reopen_unity": reopen, "setup_tools": tools,
    }, sys.stdout, indent=2, ensure_ascii=False)
    print()
    return 0


if __name__ == "__main__":
    sys.exit(main())
