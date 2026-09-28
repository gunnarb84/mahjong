#!/usr/bin/env bash
# Synchronisiert die reine C#-Spiellogik aus src/Mahjong.Core in das Unity-Projekt.
# Quelle der Wahrheit ist src/ (dort laufen auch die Unit-Tests via `dotnet test`).
set -euo pipefail
cd "$(dirname "$0")/.."

mkdir -p unity/Assets/Scripts/Core
cp src/Mahjong.Core/*.cs unity/Assets/Scripts/Core/

echo "Synced core files:"
ls -1 unity/Assets/Scripts/Core/