#!/bin/sh
# Verhindert, dass das entfernte Foto oder Zugangsdaten ins Repo kommen
# (Regeln 1 und 2 in CLAUDE.md). Laeuft als pre-commit-Hook und in GitHub Actions.
#
#   sh tools/pruefe_verbotenes.sh          prueft den Index (was gerade committet wird)
#   sh tools/pruefe_verbotenes.sh --alle   prueft alle Dateien im Arbeitsbaum (fuer CI)
#
# Grenze: Ein umbenanntes Foto erkennt das Skript nicht, nur die bekannten Namen.

# Keine Dateinamen-Expansion, damit "*.md" in den Ausnahmen bei Git ankommt.
set -f

if [ "$1" = "--alle" ]; then
  quelle=""
else
  quelle="--cached"
fi

fehler=0

# Diese Datei selbst, die Doku und .gitignore (sperrt GSTU_Config) duerfen die Namen nennen.
ausnahmen=":(exclude)*.md :(exclude)tools/pruefe_verbotenes.sh :(exclude).gitignore"

# 1. Dateinamen: Sprite/Textur des Fotos und die Plugin-Konfiguration
namen=$(git ls-files | grep -i -E 'InfoEasterEgg|GSTU_Config')
if [ -n "$namen" ]; then
  echo "VERBOTEN: Datei mit gesperrtem Namen:"
  echo "$namen"
  fehler=1
fi

# 2. Inhalte: Verweise auf das Foto (z. B. m_Name in Szenen und Prefabs)
# shellcheck disable=SC2086
if git grep $quelle -I -n -i -E -e 'InfoEasterEgg|GSTU_Config' -- . $ausnahmen; then
  echo "VERBOTEN: Verweis auf das entfernte Foto oder die Plugin-Konfiguration (siehe oben)."
  fehler=1
fi

# 3. Zugangsdaten: Google-API-Key, Google-Refresh-Token, OAuth-Client-Secret,
#    private Schluessel, geheime Supabase-Schluessel
# shellcheck disable=SC2086
if git grep $quelle -I -n -E \
    -e 'AIza[0-9A-Za-z_-]{35}' \
    -e '1//0[0-9A-Za-z_-]{20,}' \
    -e 'GOCSPX-[0-9A-Za-z_-]{20,}' \
    -e '-----BEGIN [A-Z ]*PRIVATE KEY-----' \
    -e 'sb_secret_[0-9A-Za-z_-]{10,}' \
    -- . $ausnahmen; then
  echo "VERBOTEN: Sieht nach einem Zugangsschluessel aus (siehe oben). Schluessel nie committen."
  fehler=1
fi

if [ "$fehler" -ne 0 ]; then
  echo "Pruefung fehlgeschlagen. Commit abgebrochen."
  exit 1
fi
echo "Pruefung auf Foto und Schluessel: FEHLER: keine"
