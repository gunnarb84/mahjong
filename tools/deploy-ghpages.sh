#!/bin/zsh
# Deploy: WebGL-Build auf den gh-pages-Branch pushen (GitHub Pages).
# Vorher im Editor "Mahjong/WebGL bauen" ausfuehren.
# Methode: Git-Plumbing ohne Worktree/Klon (beides ist in sandboxed Umgebungen
# unzuverlaessig). Der Deploy-Commit entsteht aus einem temporaeren Index
# (GIT_INDEX_FILE) und wird per Refspec als gh-pages gepusht.
# Aufruf: tools/deploy-ghpages.sh
set -euo pipefail
cd "$(dirname "$0")/.."

[ -d build/WebGL ] || { echo "FEHLER: build/WebGL fehlt — erst 'Mahjong/WebGL bauen' im Editor."; exit 1; }

# Zsh-Falle: "$VAR:refs/..." liest den Doppelpunkt als Modifier — deshalb
# den Refspec nie direkt mit "$COMMIT:..." zusammensetzen.
touch build/WebGL/.nojekyll

export GIT_INDEX_FILE="$PWD/.git/deploy-index"
git read-tree --empty

for f in index.html .nojekyll Build/WebGL.loader.js \
         Build/WebGL.data.unityweb Build/WebGL.framework.js.unityweb \
         Build/WebGL.wasm.unityweb \
         StreamingAssets/UnityServicesProjectConfiguration.json; do
  B=$(git hash-object -w "build/WebGL/$f")
  git update-index --add --cacheinfo "100644,$B,$f"
done

TREE=$(git write-tree)
COMMIT=$(git commit-tree "$TREE" -m "Deploy WebGL-Build ($(date '+%Y-%m-%d %H:%M'))")
unset GIT_INDEX_FILE
rm -f .git/deploy-index

git push -f origin "$COMMIT":refs/heads/gh-pages

echo "Fertig: gh-pages aktualisiert — Pages served den Build in 1-2 Minuten."
echo "  https://gunnarb84.github.io/mahjong/"