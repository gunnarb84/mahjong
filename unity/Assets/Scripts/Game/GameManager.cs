using System.Collections.Generic;
using Mahjong.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Mahjong.Game
{
    /// <summary>
    /// Orchestriert eine Partie: Layout laden, Board generieren, Klicks verarbeiten,
    /// Undo/Shuffle/Hint, Timer + Punktzahl, Spielstand speichern/fortsetzen.
    /// Visualisierung uebernimmt BoardView/TileView, die Bedienoberflaeche GameUi.
    ///
    /// Steuerung: Klick auf freien Stein waehlt aus; zweiter Klick auf passenden
    /// Stein entfernt das Paar. Taste R startet eine neue Partie.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        // Viertel-Kachel-Groesse pro Achse: Steine sind laenglich (Portrait wie
        // echte Mahjong-Steine ~30x40 mm) — Flaeche 0,8 x 1,12 Welt-Einheiten.
        const float QuarterSizeX = 0.4f;
        const float QuarterSizeZ = 0.56f;
        const float TileThickness = 0.3f; // Stapelhoehe einer Ebene (Shanghai-Look: ~1/3 der Steinbreite)

        const string LayoutPref = "Mahjong.Layout";
        const string SavePref = "Mahjong.Save";
        const string SoundPref = "Mahjong.Sound";

        // Punkte-Modell (einfach, Feinjustierung spaeter): Paar +100, Hilfen kosten.
        const int PairScore = 100;
        const int HintPenalty = 50;
        const int ShufflePenalty = 100;
        const int UndoPenalty = 30;
        const int WinBonus = 1000;

        /// <summary>Ein zurueckgenommener Zug: beide Positionen + Typen vor dem Entfernen.</summary>
        readonly struct UndoEntry
        {
            public readonly Pos A;
            public readonly Pos B;
            public readonly TileType TypeA;
            public readonly TileType TypeB;

            public UndoEntry(Pos a, Pos b, TileType typeA, TileType typeB)
            {
                A = a;
                B = b;
                TypeA = typeA;
                TypeB = typeB;
            }
        }

        readonly Dictionary<Pos, TileView> _views = new Dictionary<Pos, TileView>();
        readonly List<UndoEntry> _undoStack = new List<UndoEntry>();
        readonly List<TileView> _hinted = new List<TileView>();

        BoardState _board;
        TileView _selected;
        AudioSource _audio;
        MusicPlayer _music;
        bool _soundOn;
        string _layoutResource;
        float _offsetX;
        float _offsetZ;

        // Highscore-Rueckmeldung der aktuellen Partie (gewonnen -> Platz im Top-10)
        int _highscorePlacement;

        // Kamera-Steuerung (Zoom / Winkel / Verschieben)
        const float MinPitchDeg = 15f;
        const float MaxPitchDeg = 88f;
        const float DefaultPitchDeg = 88f;

        Camera _camera;
        Vector3 _cameraTarget; // Brettzentrum (unveraenderlich)
        Vector3 _panTarget;    // aktueller Blickpunkt (verschiebbar)
        Vector2 _lastPointer;
        float _boardSpan;
        float _pitch;          // Erhoehungswinkel (rad)
        float _azimuth;        // Drehung um Y (rad)
        float _zoom = 1f;
        float _lastPinchDistance;
        bool _panning;

        // Partie-Status
        float _elapsed;
        int _pairsRemoved;
        int _penalties;
        bool _won;
        bool _deadlocked;

        public int Score =>
            Mathf.Max(0, _pairsRemoved * PairScore + (_won ? WinBonus : 0) - _penalties);

        public float Elapsed => _elapsed;
        public int TileCount => _board != null ? _board.TileCount : 0;
        public bool Won => _won;
        public bool Deadlocked => _deadlocked;

        /// <summary>True waehrend Menue/Gewonnen-Dialog — friert Klicks und Timer ein.</summary>
        public bool Paused { get; set; }

        public bool SoundOn => _soundOn;

        // ---------------------------------------------------- Kamera per UI steuern

        /// <summary>Aktueller Blickwinkel in Grad (fuer Slider-Anzeige).</summary>
        public float PitchDeg => Mathf.Round(_pitch * Mathf.Rad2Deg);

        /// <summary>Aktuelle Brettdrehung in Grad 0-360 (fuer Slider-Anzeige).</summary>
        public float AzimuthDeg => Mathf.Round(Mathf.Repeat(_azimuth * Mathf.Rad2Deg, 360f));

        /// <summary>Blickwinkel setzen (Slider im Menue).</summary>
        public void SetPitchDeg(float degrees)
        {
            _pitch = Mathf.Clamp(degrees, MinPitchDeg, MaxPitchDeg) * Mathf.Deg2Rad;
            ApplyCamera();
        }

        /// <summary>Brett um die Hochachse drehen (Buttons im Menue, 0-360).</summary>
        public void SetAzimuthDeg(float degrees)
        {
            _azimuth = Mathf.Repeat(degrees, 360f) * Mathf.Deg2Rad;
            ApplyCamera();
        }

        /// <summary>Blickwinkel in 5-Grad-Schritten (Spielfeld-Buttons).</summary>
        public void NudgePitch(float delta) => SetPitchDeg(PitchDeg + delta);

        /// <summary>Brettdrehung in 45-Grad-Schritten (Spielfeld-Buttons).</summary>
        public void NudgeAzimuth(float delta) => SetAzimuthDeg(AzimuthDeg + delta);

        /// <summary>Nur den Blickwinkel auf Standard zuruecksetzen.</summary>
        public void ResetPitchDeg()
        {
            _pitch = DefaultPitchDeg * Mathf.Deg2Rad;
            ApplyCamera();
        }

        /// <summary>Nur die Brettdrehung auf Standard zuruecksetzen.</summary>
        public void ResetAzimuthDeg()
        {
            _azimuth = 0f;
            ApplyCamera();
        }

        public void ToggleSound()
        {
            _soundOn = !_soundOn;
            PlayerPrefs.SetInt(SoundPref, _soundOn ? 1 : 0);
            PlayerPrefs.Save();
            Debug.Log("[Mahjong] Ton " + (_soundOn ? "an" : "aus"));
        }

        /// <summary>Musik-Umschalter im Menue; laeuft pausiert fort statt neu zu starten.</summary>
        public void ToggleMusic()
        {
            if (_music == null)
            {
                _music = MusicPlayer.Ensure();
            }
            _music.Toggle();
        }

        public bool MusicOn => _music != null && _music.On;

        /// <summary>Platz des gewonnenen Laufs in der lokalen Top-10 (0 = keiner).</summary>
        public int HighscorePlacement => _highscorePlacement;

        /// <summary>Anzeige-Label des aktuellen Layouts (aus GameUi.Layouts).</summary>
        public string LayoutLabel
        {
            get
            {
                foreach (var (resource, label) in GameUi.Layouts)
                {
                    if (resource == _layoutResource)
                    {
                        return label;
                    }
                }
                return _layoutResource ?? "";
            }
        }

        public string LayoutResource => _layoutResource;

        public bool HasSave => PlayerPrefs.HasKey(SavePref);

        void Start()
        {
            EnsureGameUi();

            _soundOn = PlayerPrefs.GetInt(SoundPref, 1) == 1;
            _layoutResource = PlayerPrefs.GetString(LayoutPref, "Layouts/turtle.layout");

            // Musik-Playlist laedt erst auf den ersten Click (WebGL-Autoplay-Sperre),
            // aber der Zustand (An/Aus aus PlayerPrefs) ist schon vorher abfragbar.
            _music = MusicPlayer.Ensure();

            if (HasSave)
            {
                ContinueGame();
            }
            else
            {
                NewGame();
            }
        }

        void EnsureGameUi()
        {
            // Eigener Root, damit ClearBoard die UI nicht zerstoert.
            if (FindFirstObjectByType<GameUi>() == null)
            {
                new GameObject("GameUi").AddComponent<GameUi>().Bind(this);
            }
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                NewGame();
                return;
            }

            if (Paused)
            {
                return; // Menue offen: Kamera-Steuerung, Timer und Klicks eingefroren
            }

            HandleCameraInput();

            if (!_won)
            {
                _elapsed += Time.deltaTime;
            }

            HandleClick();
        }

        // ---------------------------------------------------------------- Partie-Start

        /// <summary>Neue Partie mit dem aktuellen Layout und zufaelligem Seed.</summary>
        public void NewGame()
        {
            NewGame(_layoutResource, UnityEngine.Random.Range(0, int.MaxValue));
        }

        /// <summary>Neue Partie mit anderem Layout (wird als Voreinstellung gemerkt).</summary>
        public void NewGame(string layoutResource, int seed)
        {
            _layoutResource = layoutResource;
            PlayerPrefs.SetString(LayoutPref, layoutResource);

            var layoutAsset = Resources.Load<TextAsset>(layoutResource);

            if (layoutAsset == null)
            {
                Debug.LogError("[Mahjong] Layout-Resource nicht gefunden: " + layoutResource);
                return;
            }

            var layout = LayoutLoader.Parse(layoutAsset.text.Split('\n'), layoutAsset.name);

            Debug.Log("[Mahjong] Neue Partie (" + layout.Name + "), Seed " + seed
                + " (fuer reproduzierbares Board)");

            ClearBoard();
            _selected = null;
            _undoStack.Clear();
            _hinted.Clear();
            _elapsed = 0f;
            _pairsRemoved = 0;
            _penalties = 0;
            _won = false;
            _deadlocked = false;
            _highscorePlacement = 0;
            Paused = false;

            var generated = BoardGenerator.Generate(layout.Positions, seed);
            _board = generated.Board;

            var bounds = BoardView.Build(
                transform, layout, generated, _views, QuarterSizeX, QuarterSizeZ, TileThickness,
                out _offsetX, out _offsetZ);
            EnsureSceneRig(bounds);
            RefreshVisuals();
        }

        /// <summary>Liest den gespeicherten Spielstand (PlayerPrefs) wieder ein.</summary>
        public void ContinueGame()
        {
            var payload = PlayerPrefs.GetString(SavePref, "");
            var parts = payload.Split('|');

            if (parts.Length != 5)
            {
                Debug.LogWarning("[Mahjong] Spielstand unlesbar — neue Partie.");
                NewGame();
                return;
            }

            _layoutResource = parts[0];

            var board = BoardState.Deserialize(parts[1]);
            _elapsed = int.Parse(parts[2]);
            _penalties = int.Parse(parts[3]);
            _pairsRemoved = int.Parse(parts[4]);
            _won = false;
            _deadlocked = false;
            Paused = false;

            ClearBoard();
            _selected = null;
            _undoStack.Clear(); // Undo-Historie wird beim Laden nicht mitgenommen
            _hinted.Clear();
            _board = board;

            var bounds = BoardView.BuildFromBoard(
                transform, _board, _views, QuarterSizeX, QuarterSizeZ, TileThickness, out _offsetX, out _offsetZ);
            EnsureSceneRig(bounds);
            RefreshVisuals();

            if (_board.TileCount == 0)
            {
                _won = true; // gespeicherter Endzustand
            }
            else
            {
                _deadlocked = _board.CountAvailablePairs() == 0;
            }

            Debug.Log("[Mahjong] Fortgesetzt: " + _layoutResource + ", " + _board.TileCount
                + " Steine, Punkte " + Score + ", Zeit " + FormatTime(_elapsed));
        }

        /// <summary>Sichert den aktuellen Stand nach jeder Aenderung (WebGL: IndexedDB).</summary>
        public void SaveGame()
        {
            if (_board == null || _won)
            {
                return;
            }

            PlayerPrefs.SetString(SavePref, string.Join("|",
                _layoutResource,
                _board.Serialize(),
                ((int)_elapsed).ToString(),
                _penalties.ToString(),
                _pairsRemoved.ToString()));
            PlayerPrefs.Save();
        }

        void DeleteSave()
        {
            PlayerPrefs.DeleteKey(SavePref);
            PlayerPrefs.Save();
        }

        // ---------------------------------------------------------------- Hilfsaktionen

        public void DoHint()
        {
            if (_board == null || _board.TileCount == 0)
            {
                return;
            }

            ClearHints();

            var pair = _board.FindAnyPair(
                new System.Random(UnityEngine.Random.Range(0, int.MaxValue)));

            if (pair == null)
            {
                _deadlocked = true;
                Debug.Log("[Mahjong] Kein Paar vorhanden — Mischen hilft jetzt.");
                return;
            }

            _penalties += HintPenalty;

            foreach (var pos in new[] { pair.Value.A, pair.Value.B })
            {
                if (_views.TryGetValue(pos, out var view))
                {
                    view.SetHint(true);
                    _hinted.Add(view);
                }
            }

            Debug.Log("[Mahjong] Hinweis: " + pair.Value.A + " + " + pair.Value.B + " (-" + HintPenalty + ")");
        }

        public void DoShuffle()
        {
            if (_board == null || _board.TileCount == 0)
            {
                return;
            }

            var shuffled = _board.ShuffleTypes(
                new System.Random(UnityEngine.Random.Range(0, int.MaxValue)));

            if (!shuffled)
            {
                Debug.Log("[Mahjong] Mischen unmoeglich: die uebrigen Steine paaren nicht mehr.");
                return;
            }

            _penalties += ShufflePenalty;
            _undoStack.Clear(); // Typen haben sich geaendert — alte Zuege sind ungueltig
            ClearHints();
            _deadlocked = false;

            foreach (var kv in _views)
            {
                kv.Value.SetType(_board.GetTile(kv.Key));
            }

            RefreshVisuals();
            SaveGame();
            Debug.Log("[Mahjong] Gemischt (-" + ShufflePenalty + ")");
        }

        public void DoUndo()
        {
            if (_board == null || _undoStack.Count == 0)
            {
                return;
            }

            var entry = _undoStack[_undoStack.Count - 1];
            _undoStack.RemoveAt(_undoStack.Count - 1);

            _board.AddTile(entry.A, entry.TypeA);
            _board.AddTile(entry.B, entry.TypeB);
            _pairsRemoved = Mathf.Max(0, _pairsRemoved - 1);
            _penalties += UndoPenalty;
            _won = false;
            _deadlocked = false;
            ClearHints();
            PlaySound(TileAudio.Select());

            // Beide Steine exakt an der alten Stelle neu aufbauen.
            _views[entry.A] = TileView.Create(
                transform, entry.A, entry.TypeA, QuarterSizeX, QuarterSizeZ, TileThickness, _offsetX, _offsetZ);
            _views[entry.B] = TileView.Create(
                transform, entry.B, entry.TypeB, QuarterSizeX, QuarterSizeZ, TileThickness, _offsetX, _offsetZ);

            RefreshVisuals();
            SaveGame();
            Debug.Log("[Mahjong] Zurueckgenommen (-" + UndoPenalty + ")");
        }

        void ClearHints()
        {
            foreach (var view in _hinted)
            {
                if (view != null)
                {
                    view.SetHint(false);
                }
            }

            _hinted.Clear();
        }

        // ---------------------------------------------------------------- Input

        /// <summary>Zeiger (Maus oder irgendein Finger) liegt auf einem UI-Element?</summary>
        static bool PointerOverUi()
        {
            var es = EventSystem.current;

            if (es == null)
            {
                return false;
            }

            if (Input.touchCount > 0)
            {
                for (var i = 0; i < Input.touchCount; i++)
                {
                    if (es.IsPointerOverGameObject(Input.GetTouch(i).fingerId))
                    {
                        return true;
                    }
                }

                return false;
            }

            return es.IsPointerOverGameObject();
        }

        void HandleClick()
        {
            var cam = Camera.main;

            if (cam == null || PointerOverUi())
            {
                return; // z. B. nach Recompile-mitten-im-Play: noch kein Rig / Klick auf UI
            }

            Ray ray = default;
            var clicked = false;

            if (Input.touchCount > 1)
            {
                return; // Pinch-Zoom: kein Stein-Klick mit zwei Fingern
            }

            if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
            {
                ray = cam.ScreenPointToRay(Input.GetTouch(0).position);
                clicked = true;
            }
            else if (Input.GetMouseButtonDown(0))
            {
                ray = cam.ScreenPointToRay(Input.mousePosition);
                clicked = true;
            }

            if (!clicked)
            {
                return;
            }

            if (Physics.Raycast(ray, out var hit, 500f))
            {
                var tile = hit.collider.GetComponentInParent<TileView>();

                if (tile != null)
                {
                    OnTileClicked(tile);
                    return;
                }
            }

            // Klick ins Leere hebt die Auswahl auf.
            SetSelected(null);
        }

        void OnTileClicked(TileView tile)
        {
            if (_board == null || _views.Count == 0)
            {
                return; // Partie nicht (mehr) initialisiert — z. B. nach Recompile im Play-Modus
            }

            if (!tile.IsFree)
            {
                return; // Blockierte Steine sind nicht waehlbar
            }

            if (_selected == tile)
            {
                SetSelected(null);
                return;
            }

            if (_selected == null)
            {
                SetSelected(tile);
                return;
            }

            var firstPos = _selected.Pos;
            var firstType = _board.GetTile(firstPos);
            var secondType = _board.GetTile(tile.Pos);

            if (_board.TryRemove(firstPos, tile.Pos))
            {
                SetSelected(null);
                _undoStack.Add(new UndoEntry(firstPos, tile.Pos, firstType, secondType));
                _pairsRemoved++;
                PlaySound(TileAudio.Match());
                RemoveTile(_views[firstPos]);
                RemoveTile(tile);
                RefreshVisuals();
                SaveGame();
                CheckGameState();
            }
            else
            {
                // Passt nicht: Auswahl wechselt auf den neuen Stein.
                SetSelected(tile);
            }
        }

        void SetSelected(TileView tile)
        {
            ClearHints();
            _selected = tile;

            if (tile != null)
            {
                PlaySound(TileAudio.Select());
            }

            foreach (var view in _views.Values)
            {
                view.SetSelected(view == _selected);
            }
        }

        void PlaySound(AudioClip clip)
        {
            if (_soundOn && _audio != null && clip != null)
            {
                _audio.PlayOneShot(clip);
            }
        }

        // ---------------------------------------------------------------- Board

        /// <summary>Kamera-Input: Zoom (Rad/Pinch), Winkel (Rechtsklick/zwei Finger), Schieben (Drag).</summary>
        void HandleCameraInput()
        {
            if (_camera == null || PointerOverUi())
            {
                return;
            }

            // --- Zoom: Mausrad oder Pinch ---
            var scroll = Input.mouseScrollDelta.y;

            if (Input.touchCount == 2)
            {
                var distance = Vector2.Distance(
                    Input.GetTouch(0).position, Input.GetTouch(1).position);

                if (_lastPinchDistance > 0f)
                {
                    scroll = (distance - _lastPinchDistance) * 0.02f;
                }

                _lastPinchDistance = distance;
            }
            else
            {
                _lastPinchDistance = 0f;
            }

            if (Mathf.Abs(scroll) > 0.001f)
            {
                _zoom = Mathf.Clamp(_zoom * Mathf.Exp(-scroll * 0.2f), 0.45f, 1.8f);
                ApplyCamera();
            }

            // --- Winkel: rechte Maustaste ziehen ---
            // Guarde: echte Maus UND keine Finger (touchCount == 0). Legacy-
            // Input kann auf Touch-Geraeten ohne Maus mousePresent melden —
            // die zusätzliche Finger-Bedingung schliesst Pinch als Rotations-
            // Quelle aus. Ansicht aendert man auf Touch ueber die Buttons.
            if (Input.mousePresent && Input.touchCount == 0 && Input.GetMouseButton(1))
            {
                _azimuth -= Input.GetAxis("Mouse X") * 0.005f;
                _pitch = Mathf.Clamp(
                    _pitch + Input.GetAxis("Mouse Y") * 0.005f,
                    MinPitchDeg * Mathf.Deg2Rad, MaxPitchDeg * Mathf.Deg2Rad);
                ApplyCamera();
            }

            // --- Winkel: zwei Finger ziehen (Mittelpunkt-Delta) ---
            // Entfernt: beim Pinch wandert der Mittelpunkt unweigerlich mit und
            // verdrehte Winkel/Drehung ungewollt. Ansicht aendert man auf Touch
            // ueber die Buttons unten links; Pinch zoomt ausschliesslich.

            // --- Schieben: Drag auf leerer Flaeche (Maus links / ein Finger) ---
            // Klick auf einen Stein geht an HandleClick; hier panen wir nur daneben.
            // Mauspfad nur mit echter Maus UND ohne Finger auf dem Bildschirm —
            // vermeidet Doppel-Bedienung durch Touch->Maus-Emulation auf
            // Touch-Geraeten (mousePresent kann dort fälschlich true melden).
            if (Input.mousePresent && Input.touchCount == 0)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    _lastPointer = Input.mousePosition;
                    _panning = !Physics.Raycast(_camera.ScreenPointToRay(Input.mousePosition), 500f);
                }

                if (_panning && Input.GetMouseButton(0))
                {
                    var position = (Vector2)Input.mousePosition;
                    PanBy(position - _lastPointer);
                    _lastPointer = position;
                }

                if (Input.GetMouseButtonUp(0))
                {
                    _panning = false;
                }
            }

            if (Input.touchCount == 1)
            {
                var touch = Input.GetTouch(0);

                if (touch.phase == TouchPhase.Began)
                {
                    _lastPointer = touch.position;
                    _panning = !(Physics.Raycast(_camera.ScreenPointToRay(touch.position), out var hit, 500f)
                        && hit.collider.GetComponentInParent<TileView>() != null);
                }
                else if (touch.phase == TouchPhase.Moved && _panning)
                {
                    var position = touch.position;
                    PanBy(position - _lastPointer);
                    _lastPointer = position;
                }
                else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    _panning = false;
                }
            }
            else if (Input.touchCount > 1)
            {
                _panning = false; // zweiter Finger: Pinch/Drehen uebernimmt
            }
        }

        /// <summary>Schiebt den Blickpunkt parallel zur Brett-Ebene (Board folgt dem Finger).</summary>
        void PanBy(Vector2 screenDelta)
        {
            var t = _camera.transform;
            var distance = Vector3.Distance(t.position, _panTarget);
            var perPixel = 2f * distance
                * Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad)
                / Mathf.Max(Screen.height, 1);
            var right = t.right;
            right.y = 0f;
            right.Normalize();
            var forward = t.forward;
            forward.y = 0f;
            forward.Normalize();

            _panTarget -= right * (screenDelta.x * perPixel)
                + forward * (screenDelta.y * perPixel);

            // Blickpunkt im Rahmen des Bretts halten (Zentrum ± 80 % Spannweite).
            _panTarget.x = Mathf.Clamp(_panTarget.x,
                _cameraTarget.x - _boardSpan * 0.8f, _cameraTarget.x + _boardSpan * 0.8f);
            _panTarget.z = Mathf.Clamp(_panTarget.z,
                _cameraTarget.z - _boardSpan * 0.8f, _cameraTarget.z + _boardSpan * 0.8f);
            ApplyCamera();
        }

        /// <summary>Positioniert die Kamera aus Spannweite, Winkel, Zoom und Blickpunkt.</summary>
        void ApplyCamera()
        {
            if (_camera == null)
            {
                return;
            }

            // 1.30 = Distanzfaktor der alten Ansicht (1.15² + 0.6²)^0.5
            var distance = _boardSpan * 1.30f * _zoom;
            var horizontal = new Vector3(Mathf.Sin(_azimuth), 0f, -Mathf.Cos(_azimuth));
            var offset = horizontal * (Mathf.Cos(_pitch) * distance)
                + Vector3.up * (Mathf.Sin(_pitch) * distance);
            var target = _cameraTarget
                + Vector3.ClampMagnitude(_panTarget - _cameraTarget, _boardSpan * 0.8f);
            _camera.transform.position = target + offset;
            _camera.transform.LookAt(target);
        }

        void RemoveTile(TileView tile)
        {
            _views.Remove(tile.Pos);
            tile.PlayRemove();
        }

        void RefreshVisuals()
        {
            foreach (var kv in _views)
            {
                kv.Value.SetFree(_board.IsFree(kv.Key));
            }
        }

        void CheckGameState()
        {
            if (_board.TileCount == 0)
            {
                _won = true;
                PlaySound(TileAudio.Win());
                DeleteSave();
                Debug.Log("[Mahjong] Gewonnen! Punkte " + Score
                    + ", Zeit " + FormatTime(_elapsed));
                RecordHighscore();
                return;
            }

            _deadlocked = _board.CountAvailablePairs() == 0;

            if (_deadlocked)
            {
                PlaySound(TileAudio.Deadlock());
                Debug.Log("[Mahjong] Kein Zug mehr moeglich — Mischen (-" + ShufflePenalty + ") oder neue Partie.");
            }
        }

        /// <summary>Gewonnene Partie in die lokale Top-10 einspielen (nur Gewinne).</summary>
        void RecordHighscore()
        {
            _highscorePlacement = HighscoreStore.Add(new HighscoreEntry(
                Score,
                Mathf.FloorToInt(_elapsed),
                LayoutLabel,
                System.DateTime.Now.ToString("yyyy-MM-dd")));

            if (_highscorePlacement > 0)
            {
                Debug.Log("[Mahjong] Highscore: Platz " + _highscorePlacement);
            }
        }

        void ClearBoard()
        {
            _views.Clear();

            // Alte Board-Hierarchie komplett abräumen (auch leere "Board"-Container).
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
        }

        void EnsureSceneRig(Bounds bounds)
        {
            var center = bounds.center;
            var span = Mathf.Max(bounds.size.x, bounds.size.z);

            var cam = Camera.main;

            if (cam == null)
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                camGo.AddComponent<AudioListener>();
                cam = camGo.AddComponent<Camera>();
            }

            cam.backgroundColor = new Color(0.10f, 0.13f, 0.10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.nearClipPlane = 0.3f; // nicht kleiner: groesser = bessere Tiefen-Praezision
            cam.farClipPlane = span * 4f;
            // Kamera-Fuehrung (Zoom/Winkel/Schieben) via HandleCameraInput/ApplyCamera:
            // Steile Standardansicht (65 Grad), Board komplett im Bild.
            _camera = cam;
            _cameraTarget = center;
            _panTarget = center;
            _boardSpan = span;
            _pitch = DefaultPitchDeg * Mathf.Deg2Rad;
            _azimuth = 0f;
            _zoom = 1f;
            ApplyCamera();

            // Sound-Ausgabe an diesem Objekt (Kamera hat den AudioListener).
            if (_audio == null)
            {
                _audio = gameObject.AddComponent<AudioSource>();
            }

            // Kantenglaettung fuer Stein-Kanten (Game-View + Builds).
            QualitySettings.antiAliasing = 4;

            // Aufhellung der Schattenseiten + weiche Schatten fuer Tiefe.
            RenderSettings.ambientLight = new Color(0.42f, 0.42f, 0.42f);

            if (FindFirstObjectByType<Light>() == null)
            {
                var lightGo = new GameObject("Directional Light");
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.1f;
                light.shadows = LightShadows.Soft;
                light.shadowStrength = 0.6f;
                lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }
        }

        internal static string FormatTime(float seconds)
        {
            var total = (int)seconds;
            return (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
        }
    }
}