using System.Collections.Generic;
using Mahjong.Core;
using UnityEngine;

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
        const float QuarterSize = 0.5f; // Welt-Einheiten pro Viertel-Kachel
        const float TileThickness = 0.6f; // Stapelhoehe einer Ebene

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
        bool _soundOn;
        string _layoutResource;
        float _offsetX;
        float _offsetZ;

        // Kamera-Steuerung (Zoom)
        Camera _camera;
        Vector3 _cameraTarget;
        Vector3 _cameraOffset;
        float _zoom = 1f;
        float _lastPinchDistance;

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

        public void ToggleSound()
        {
            _soundOn = !_soundOn;
            PlayerPrefs.SetInt(SoundPref, _soundOn ? 1 : 0);
            PlayerPrefs.Save();
            Debug.Log("[Mahjong] Ton " + (_soundOn ? "an" : "aus"));
        }

        public string LayoutResource => _layoutResource;

        public bool HasSave => PlayerPrefs.HasKey(SavePref);

        void Start()
        {
            EnsureGameUi();

            _soundOn = PlayerPrefs.GetInt(SoundPref, 1) == 1;
            _layoutResource = PlayerPrefs.GetString(LayoutPref, "Layouts/turtle.layout");

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

            HandleZoom();

            if (Paused)
            {
                return; // Menue offen: Timer und Klicks eingefroren
            }

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
            Paused = false;

            var generated = BoardGenerator.Generate(layout.Positions, seed);
            _board = generated.Board;

            var bounds = BoardView.Build(
                transform, layout, generated, _views, QuarterSize, TileThickness,
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
                transform, _board, _views, QuarterSize, TileThickness, out _offsetX, out _offsetZ);
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
                transform, entry.A, entry.TypeA, QuarterSize, TileThickness, _offsetX, _offsetZ);
            _views[entry.B] = TileView.Create(
                transform, entry.B, entry.TypeB, QuarterSize, TileThickness, _offsetX, _offsetZ);

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

        void HandleClick()
        {
            var cam = Camera.main;

            if (cam == null)
            {
                return; // z. B. nach Recompile-mitten-im-Play: noch kein Rig
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

        void HandleZoom()
        {
            if (_camera == null)
            {
                return;
            }

            // Mausrad (Desktop) oder Pinch (Touch) aendert den Zoomfaktor.
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

            if (Mathf.Abs(scroll) < 0.001f)
            {
                return;
            }

            // Entfernungs-Faktor relativ zur Zuhause-Position; Blick bleibt auf dem
            // Brettzentrum (Zoom bewegt die Kamera entlang der Sichtachse).
            _zoom = Mathf.Clamp(_zoom * Mathf.Exp(-scroll * 0.2f), 0.45f, 1.6f);
            _camera.transform.position = _cameraTarget + _cameraOffset * _zoom;
            _camera.transform.LookAt(_cameraTarget);
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
                return;
            }

            _deadlocked = _board.CountAvailablePairs() == 0;

            if (_deadlocked)
            {
                PlaySound(TileAudio.Deadlock());
                Debug.Log("[Mahjong] Kein Zug mehr moeglich — Mischen (-" + ShufflePenalty + ") oder neue Partie.");
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
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = span * 4f;
            cam.transform.position = center + new Vector3(0f, span * 1.15f, -span * 0.6f);
            cam.transform.LookAt(center);

            // Zoom-Referenz: Brettzentrum + Zuhause-Position; HandleZoom bewegt die
            // Kamera entlang dieser Sichtachse (Standard etwas naeher ans Brett).
            _camera = cam;
            _cameraTarget = center;
            _cameraOffset = cam.transform.position - center;
            _zoom = 0.75f;
            _camera.transform.position = _cameraTarget + _cameraOffset * _zoom;
            _camera.transform.LookAt(_cameraTarget);

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