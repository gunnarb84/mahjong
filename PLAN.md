# Mahjong Solitaire — Unity-Projektplan

Ein Mahjong-Solitaire-Klon (Tile-Matching, klassisches "Shanghai"-Spielprinzip) in Unity,
primär als WebGL-Build im Browser lauffähig, mit Desktop-Builds als einfachem Testweg.

---

## 1. Spielprinzip

- 144 Spielsteine in klassischer "Schildkröten"-Aufbau (Turtle-Layout, mehrere Ebenen).
- Spielregeln:
  - Ein Stein ist **frei**, wenn (a) kein Stein auf ihm liegt und (b) links **oder**
    rechts kein weiterer Stein auf derselben Ebene blockiert.
  - Zwei freie Steine mit gleichem Bild dürfen entfernt werden.
  - Blumen (4) und Jahreszeiten (4) bilden Sonderpaare: jeder Blume passt auf jeden
    anderen Blumen, jede Jahreszeit auf jede andere Jahreszeit.
  - Gewonnen: alle Steine entfernt. Verloren: keine möglichen Paare mehr → Shuffle oder Neustart.
- Hilfsfunktionen: Hinweis (nächstmögliches Paar), Mischen, Undo, Timer, Punktzahl.

### Steinbestand (144 Steine / 42 Typen)

| Gruppe           | Typen                       | Steine |
|------------------|-----------------------------|--------|
| Kreise (Dots)    | 1–9                         | 36     |
| Bambus (Bamboo)  | 1–9                         | 36     |
| Zeichen (Chars)  | 1–9                         | 36     |
| Winde            | Ost, Süd, West, Nord        | 16     |
| Drachen          | Rot, Grün, Weiß             | 12     |
| Blumen           | 4 (jeder zählt als Paar)    | 4      |
| Jahreszeiten     | 4 (jede zählt als Paar)     | 4      |

---

## 2. Technisches Setup

- **Unity:** Unity 6 LTS (6000.x), Standard-3D-Template (URP ist okay, aber für dieses Spiel
  reicht die Built-in/URP-Pipeline — bewusst simpel halten).
- **Pakete:** Input System (Touch + Maus), Unity Test Framework (für die reine Logik),
  TextMeshPro (UI).
- **Versionierung:** Git, `.gitignore` für Unity (Library/, Temp/, Logs/, UserSettings/).
- **C#-Code:** alle Spielregeln in reinen C#-Klassen **ohne** UnityEngine-Abhängigkeit
  (nur `System.*`) → damit kann man die Spiellogik mit Unit-Tests absichern, unabhängig
  von der Engine.

### Projektstruktur

```
Assets/
  Scenes/            MainMenu.unity, Game.unity
  Scripts/
    Core/            Reine C#-Logik (kein UnityEngine):
                       Tile.cs, TileType.cs, BoardState.cs, LayoutData.cs,
                       MatchRules.cs, Solver.cs (Hint/Deadlock-Erkennung)
    Board/           LayoutLoader.cs, BoardGenerator.cs, BoardView.cs, TileView.cs
    Input/           TilePicker.cs (Raycast auf Steine, Touch & Maus)
    GameFlow/        GameManager.cs, ScoreTimer.cs, UndoStack.cs
    UI/              HudController.cs, MainMenu.cs, Settings.cs, WinLoseDialog.cs
  Art/
    Tiles/           Ein Stein-Mesh + ein Textur-Atlas (alle 42 Gesichter)
    Layouts/         Layout-Definitionen als Textdatei/JSON
  Audio/
Tests/
  EditMode/          Unit-Tests für Core (Regeln, Generator, Solver)
```

### Datenformate

- **Layout:** Textdatei, eine Zeile pro Ebene, `x`/`.` für Stein/Frei — so lassen sich
  neue Boards (Turtle, Pyramide, Katze, …) ohne Code neu entwerfen:

  ```
  # Layer 0 (Unterbau)
  xxxx....xxxx
  ...
  # Layer 1
  ...xx..xx...
  ```

- **Stein-IDs:** ein Enum bzw. string-ids wie `dot-1`, `bam-5`, `wind-E`, `flower-2`.
  Der Generator würfelt Typen auf die Positionen, die Layout-Datei kennt nur Positionen.

---

## 3. Kernarchitektur

1. **Core (pure C#):** `BoardState` hält Steine als `(x, y, layer)`-Grid + Typ-Id.
   `MatchRules.IsFree(state, tile)` und `CanRemove(state, a, b)` implementieren die Regeln.
   `Solver.FindAnyPair` für Hint, `Solver.CountMoves` für Deadlock-Check.
2. **BoardGenerator (solvable by construction):** statt zufällig zu belegen und zu hoffen,
   wird rückwärts generiert — beginnend mit leerem Board werden 72 Paare in **umgekehrter
   Entfernungsreihenfolge** auf freie Positionen gesetzt. Ergebnis ist garantiert lösbar
   (mindestens über die generierte Reihenfolge). Seed-basiert → reproduzierbare Boards.
3. **View-Layer:** 144 Instanzen eines einzigen Stein-Meshes; die 42 Gesichter kommen aus
   einem Atlastextur, pro Stein nur Material-UV-Wechsel. Selektion, Match, Remove sind
   Animationen auf den `TileView`s — die Logik selbst kennt keine Szene.
4. **Input:** ein Raycaster (Mouse + Touch über das Input System), Klick wählt Stein an;
   zweiter Klick auf passenden Stein → Match, sonst Wechsel der Auswahl.
5. **GameFlow:** `GameManager` orchestriert Generator, Board, Undo-Stack, Timer, Win/Lose.

---

## 4. Meilensteine

### M0 — Projekt aufsetzen (½ Tag)
- [ ] Unity 6 LTS installieren, Projekt anlegen, WebGL-Build-Support
- [ ] Git-Repo initialisieren mit Unity-`.gitignore`
- [ ] Ordnerstruktur + leere MainMenu/Game-Szenen
- [ ] Input System + TMP einrichten

### M1 — Spielregeln in pure C# (1–2 Tage) ✅ erledigt
- [x] `Tile`, `TileType`, `BoardState` (Grid + Ebenen)
- [x] `MatchRules`: IsFree, CanRemove (inkl. Blumen/Jahreszeiten-Sonderpaare)
- [x] EditMode-Unit-Tests: freie Steine, blockierte Kanten, Blumen-Regeln
- [x] LayoutLoader: Turtle-Layout-Textdatei parsen (144 Steine)

### M2 — Board erzeugen & darstellen (2–3 Tage) ✅ Code fertig — im Editor verifizieren
- [x] Stein-Mesh + Atlastextur mit 42 Gesichtern — **verschoben auf M6**; M2 nutzt
      Platzhalter (farbcodierte Würfel + Text-Label), damit die Spielmechanik zuerst steht
- [x] `TileView` + `BoardView`: Board aus Layout + generierten Typen aufbauen
- [x] Raycast-Picking (Maus), Auswahl-Highlight
- [ ] Erstes Öffnen im Unity-Editor: kompilieren, Play-Test gegen das Turtle-Layout

### M3 — Spielablauf (2 Tage) ✅ Code fertig — im Editor verifizieren
- [x] Match-Flow: erstes/zweites Klicken, entfernen mit Animation
- [x] Win-Erkennung (Board leer), Lose-Erkennung (0 mögliche Züge → Deadlock-Hinweis)
- [x] Undo-Stack, Shuffle (Typen auf verbleibenden Positionen, garantiert ≥ 1 Zug
      oder "unmöglich"), Hint über `BoardState.FindAnyPair` (Hinweis-Steine leuchten cyan)
- [x] Timer + einfache Punktzahl (Paar +100; Hint −50, Shuffle −100, Undo −30,
      Win-Bonus +1000). HUD vorerst als OnGUI-Provisorium (Bootstrap-freundlich),
      echtes uGUI-Canvas kommt mit M5

### M4 — Garantiert lösbarer Generator (1–2 Tage) ✅ Logik erledigt (in M1 vorgezogen)
- [x] Board-Generator mit Seed (Wildcard-Abbau + Typ-Zuweisung statt Reverse-Deal —
      gleiche Garantie, einfacher: Freitheit ist geometrisch, also wird das Board
      vorwärts abgebaut und die Züge anschließend mit den 72 Stein-Paaren belegt)
- [x] Unit-Test: generiertes Board ist per Regeln tatsächlich lösbar (Solution-Replay)
- [ ] Deadlock-Handling: "Kein Zug mehr"-Dialog mit Shuffle/Neustart (UI, kommt mit M3/M5)

### M5 — UI & Meta (2–3 Tage) ✅ Code fertig — im Editor verifizieren
- [x] Hauptmenü (uGUI-Panel, laufzeitgebaut): Weiterspielen, Neue Partie, Layout-Auswahl
      (Schildkröte/Pyramide/Drache/Katze — 3 neue Layouts, Loader unterstützt jetzt
      KMahjongg v1.0 **und** v1.1). Fortsetzung: Spielstand (Board-Serialisierung im
      Core) landet nach jedem Zug in PlayerPrefs und wird beim Start automatisch geladen
- [x] HUD: uGUI-Leiste oben — Zeit, Punkte, Reststeine + Buttons
      (Hinweis/Mischen/Undo/Menü), ersetzt das M3-OnGUI-Provisorium
- [x] Win-Dialog (Punkte + Zeit, Neue Partie/Menü), Deadlock-Dialog (Mischen/Neue Partie)
- [~] Einstellungen: Layout-Wahl via PlayerPrefs persistiert; eigenes Settings-Panel
      (Sound-Toggle etc.) folgt mit M6, wenn Sound existiert
- [x] Responsive Canvas: CanvasScaler "ScaleWithScreenSize" (Referenz 1280×720)

### M6 — Polish (2–3 Tage) ✅ Code fertig — im Editor verifizieren
- [x] Stein-Atlas mit 42 Gesichtern (vorgezogen, ersetzt den TextMesh-Platzhalter:
      prozeduraler Generator im Editor-Menü "Mahjong/Stein-Atlas generieren",
      TextMesh→RenderTexture→PNG, kein Font-Dependence im Spiel → WebGL-tauglich;
      fontSize 384 ≈ 1:1-Raster + 8× MSAA für scharfe Glyphen)
- [x] Optischer Feinschliff: prozeduraler Rounded-Box-Mesh (ein geteiltes Mesh,
      Rundung an Kanten, nahtlos zwischen Flächen), Beleuchtung (Ambient + weiche
      Directional-Schatten), freie Steine hell / blockierte abgedunkelt
- [x] Sound: Auswahl, Match, Win, Deadlock — synthetisch per PCM zur Laufzeit
      (TileAudio, keine Assets → WebGL-tauglich), Ton-Schalter im Menü (PlayerPrefs)
- [x] Feinere Animationen: Stein-Entfernen (Aufploppen + Schrumpfen + Aufstieg);
      Win-Konfetti/Partikel vertagt — nur wenn Nutzerwunsch
- [x] 2–3 zusätzliche Layouts (Pyramide, Katze, Drache)
- [x] Einstellungen: Ton-Toggle im Menü (M5-Rest)
- [x] Erstes Öffnen im Unity-Editor: Atlas neu generiert, Play-Test (Optik/Sound/Menü/Zoom)

### M7 — WebGL-Optimierung & Deployment (1–2 Tage) ✅ Build verifiziert (läuft im Browser)
- [x] Build-Size: Atlas auf 4096² POT-Zellen umgestellt (8×8, 42 belegt) →
      WebGL-Texturkompression (DXT/S3TC via Plattform-Override) + saubere Mipmaps;
      kein Threading im Code (geprüft); IL2CPP/WASM + Strip-Debug-Code
- [x] Load-Balance: Mahjong-WebGL-Template mit Ladebalken (dunkler Hintergrund,
      touch-action: none gegen Browser-Pinch-Zoom, viewport user-scalable=no);
      Menüpunkt "Mahjong/WebGL bauen" (Gzip + Decompression-Fallback → läuft
      auf jedem Hoster ohne Server-Konfiguration, z. B. GitHub Pages)
- [x] Vollbild-Button im Menü (Browser-Spielfläche)
- [ ] Touch-Verhalten im Mobile-Browser testen (Safari/Chrome — braucht echtes Gerät)
- [ ] Deployment: GitHub Pages oder itch.io (build/WebGL hochladen — Webserver
      mit korrektem MIME/CORS oder itch.io, das Gzip selbst verarbeitet)

**Gesamtschätzung:** ca. 12–18 Tage in Teilzeit, ~1 Woche Vollzeit.

---

## 5. WebGL-Besonderheiten (Checkliste)

- **Kein Multithreading** — Solver/Generator synchron und klein halten (144 Steine sind
  winzig, das ist unkritisch).
- **Kein `Application.persistentDataPath`-Schreiben** — Fortschritt über `PlayerPrefs`
  (funktioniert in WebGL via IndexedDB) oder URL-Seed.
- **Build-Größe:** ein Mesh + ein Atlas statt 144 Materialien; Texture 2048px reicht.
  Kompression auf ASTC (iOS-Browser) + ETC2/Fallback prüfen.
- **Pointer Events:** WebGL hat Touch + Maus — Input System mit "Both" konfigurieren,
  Doppel-Tap-Zoom der Browser-Seite unterdrücken (Canvas `touch-action: none`).
- **Fullscreen-Button** einbauen (Browser-Spielfläche ist sonst eng).

---

## 6. Risiken & Gegenmaßnahmen

| Risiko | Gegenmaßnahme |
|---|---|
| Logikfehler in Freisteh-Regel (klassischer Bug) | 100 % der Regeln in pure-C#-Core mit Unit-Tests |
| Generiertes Board ist nicht lösbar | Reverse-Deal-Verfahren + Unit-Test, kein Zufalls-Bestücken |
| WebGL-Build zu groß / lädt zu lang | Ein Mesh + ein Atlas, Kompression, kein Video/Audio-Schwergewicht |
| Touch trifft falschen Stein auf überlappenden Ebenen | Raycast nach Z-Sortierung, Klick nur auf vordersten Stein |
| Unity-Editor-Frust | Erst die reine C#-Logik mit Tests bauen (M1), UI zuletzt |

---

## 7. Erster konkreter Schritt

1. Unity Hub + Unity 6 LTS installieren
2. Projekt im Ordner anlegen (`Unity-Version 6000.x`), WebGL-Build-Support aktivieren
3. Git init + Unity-.gitignore
4. Mit `Core/Tile.cs` + `Core/BoardState.cs` + erstem Unit-Test starten — die Regeln
   sind das Herzstück und kommen ganz ohne Unity-Editor aus, man kann sie direkt
   mit `dotnet test`-ähnlichem Workflow (Unity Test Framework im Editor) verifizieren.

Wenn du magst, kann ich dir als Nächstes das Grundgerüst konkret anlegen:
`Tile.cs`, `BoardState.cs`, `MatchRules.cs` inkl. Unit-Tests und das Turtle-Layout
als Textdatei — die kannst du dann direkt in ein frisches Unity-Projekt kopieren.