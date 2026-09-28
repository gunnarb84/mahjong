# Mahjong Solitaire (Unity)

Mahjong-Solitaire-Klon (klassisches "Shanghai") in Unity, primär für **WebGL**.
Siehe [PLAN.md](PLAN.md) für den Gesamtplan.

## Aktueller Stand

**M1–M7 Code abgeschlossen** — Spielregeln als reines C# (`src/Mahjong.Core`, ohne
Unity-Abhängigkeit, lauffähig unter .NET und Unity), vollständiger Spielablauf mit UI:

- `TileType` — Stein-Typen + Paar-Regeln (inkl. Blumen/Jahreszeiten)
- `Pos` — Position in Viertel-Kachel-Einheiten (halbe Versätze möglich)
- `BoardState` — Freisteh-Regel, Zug-Regeln, Deadlock-Erkennung, Hint-Suche,
  `ShuffleTypes`, `Serialize`/`Deserialize` (Spielstand)
- `LayoutLoader` — KMahjongg-Layoutdateien **v1.0 und v1.1** (`assets/layouts/`:
  turtle = klassisch 144 Steine, pyramid = 204, dragon + cat = je 144)
- `BoardGenerator` — erzeugt **garantiert lösbar** Belegungen inkl. Solution;
  der Stein-Pool skaliert mit der Layoutgröße (> 144 Steine → zusätzliche Paare)
- Unity: `GameManager` (Spielablauf, Klick-Input, R = neue Partie, Timer, Punkte,
  Spielstand in PlayerPrefs → Fortsetzung beim Start), `GameUi` (uGUI zur Laufzeit:
  HUD mit Zeit/Punkte/Steine + Buttons Hinweis/Mischen/Undo/Menü, Menü-Panel mit
  Layout-Auswahl + Ton-Schalter, Gewonnen-/Deadlock-Dialoge), `BoardView` / `TileView`
  (Atlas-Textur, gerundete Steine über ein geteiltes Rounded-Box-Mesh, Auswahl gold /
  Hinweis cyan, Entfern-Animation), `TileAudio` (synthetische Sounds per PCM —
  Auswahl/Match/Sieg/Deadlock, WebGL-tauglich ohne Assets), `GameBootstrap`
  (baut die Szene zur Laufzeit auf)

Steuerung: Klick wählt freien Stein, zweiter Klick auf passenden Stein entfernt
das Paar. **R** = neue Partie, **Mausrad/Pinch** = Zoom, Menü = Vollbild an/aus.
Punkte: Paar +100, Hinweis −50, Mischen −100, Undo −30, Gewinn-Bonus +1000.
Der Spielstand wird nach jedem Zug gesichert — beim nächsten Start automatisch
fortgesetzt (Gewinnen löscht ihn).

## WebGL-Build

**Live: https://gunnarb84.github.io/mahjong/**

1. Einmal im Editor: **Mahjong → Stein-Atlas generieren** (4096² POT-Atlas)
2. **Mahjong → WebGL bauen** — Ausgabe in `build/WebGL/` (Gzip + Decompression-
   Fallback, läuft auf jedem Hoster), Mahjong-Template mit Ladebalken
3. Bereitstellen: `build/WebGL/` auf GitHub Pages hochladen (Branch `gh-pages`,
   Unterordner reicht) oder via butler auf itch.io pushen

## Tests

```bash
dotnet test
```

Die Tests decken ab: Freisteh-Regel (bündig, halb versetzt, Eckberührung, Verdeckung),
Paar-Regeln, Shuffle, Board-Serialisierung und — für **alle** Layoutdateien im
`layouts`-Ordner — Gültigkeit, gerade Steinzahl und Generator-Lösbarkeit (Solution
wird replayed). Neue Layouts sind damit automatisch abgedeckt.

## Struktur

```
src/Mahjong.Core/    Reine Spiellogik (Quelle der Wahrheit; Tests via dotnet test)
tests/               xUnit-Tests für die Core-Logik
assets/layouts/      Board-Layouts im KMahjongg-Format (v1.0 + v1.1)
unity/               Unity-Projekt (M3+M5): Skripte, Layout-Ressourcen, Atlas
tools/sync-core.sh   Kopiert src/Mahjong.Core/*.cs nach unity/Assets/Scripts/Core/
```

## Unity öffnen (M2)

1. Unity Hub starten → **Installs** → Unity **6 LTS** installieren (Modul
   **WebGL Build Support** gleich mitwählen, brauchen wir in M7).
2. Unity Hub → **Projects → Add** → Ordner `unity/` wählen → mit der installierten
   6er-Version öffnen.
3. Beliebige (leere) Szene öffnen → **Play** drücken: Das Spiel baut sich selbst auf
   (Bootstrap-Skript erzeugt Kamera, Licht und Board).
4. Einmalig: Menü **Mahjong → Stein-Atlas generieren** — erzeugt alle 42 Stein-Gesichter
   als `Assets/Resources/Art/TileAtlas.png` (danach erneut Play).
5. Steuerung: Klick auf freien Stein wählt aus, zweiter Klick auf passenden Stein
   entfernt das Paar. **R** = neue Partie. Konsole zeigt Seed, Sieg und Deadlock.

Der Core in `unity/Assets/Scripts/Core/` ist eine Kopie von `src/` — nach Änderungen
an der Logik immer `tools/sync-core.sh` ausführen.