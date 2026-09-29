using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Mahjong.Game
{
    /// <summary>
    /// Bedienoberflaeche (uGUI), komplett zur Laufzeit gebaut — konsistent mit dem
    /// Bootstrap-Ansatz (keine Szenen-Assets). Bestandteile:
    /// - HUD oben: Zeit / Punkte / Reststeine + Buttons Hinweis/Mischen/Undo/Menue
    /// - Menue-Panel: Weiterspielen, Neue Partie, Layout-Auswahl
    /// - Gewonnen- und Deadlock-Dialoge
    /// Fortsetzung: beim Start wird ein gespeicherter Spielstand automatisch geladen.
    /// </summary>
    public class GameUi : MonoBehaviour
    {
        /// <summary>Angebotene Layouts (Resource-Name, Anzeige-Label).</summary>
        public static readonly (string Resource, string Label)[] Layouts =
        {
            ("Layouts/turtle.layout", "Schildkröte"),
            ("Layouts/pyramid.layout", "Pyramide (204)"),
            ("Layouts/dragon.layout", "Drache"),
            ("Layouts/cat.layout", "Katze"),
        };

        static Sprite _whiteSprite;

        static Sprite WhiteSprite
        {
            get
            {
                if (_whiteSprite == null)
                {
                    _whiteSprite = Sprite.Create(
                        Texture2D.whiteTexture,
                        new Rect(0f, 0f, 4f, 4f),
                        new Vector2(0.5f, 0.5f),
                        100f);
                }

                return _whiteSprite;
            }
        }

        GameManager _manager;
        Text _hudText;
        Text _winScoreText;
        Text _soundToggleText;
        GameObject _menuPanel;
        GameObject _winDialog;
        GameObject _deadlockDialog;
        Text _pitchValueText;
        Text _azimuthValueText;

        public void Bind(GameManager manager)
        {
            _manager = manager;
            Build();
        }

        void Build()
        {
            // Canvas + EventSystem (ohne EventSystem keine Button-Klicks).
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var esGo = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                esGo.transform.SetParent(transform, false);
            }

            BuildHud(canvasGo.transform);
            BuildViewControls(canvasGo.transform);
            _menuPanel = BuildMenu(canvasGo.transform);
            _winDialog = BuildWinDialog(canvasGo.transform);
            _deadlockDialog = BuildDeadlockDialog(canvasGo.transform);
        }

        // ---------------------------------------------------------------- HUD

        void BuildHud(Transform canvas)
        {
            var bar = new GameObject("HudBar", typeof(Image));
            var rt = bar.GetComponent<RectTransform>();
            rt.SetParent(canvas, false);

            // Balken oben, volle Breite, 56 hoch (Anker oben-stretch).
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(0f, -56f);
            rt.offsetMax = new Vector2(0f, 0f);

            var image = bar.GetComponent<Image>();
            image.sprite = WhiteSprite;
            image.color = new Color(0f, 0f, 0f, 0.35f);

            _hudText = MakeText(bar.transform, "", 24, TextAnchor.MiddleLeft, Color.white);
            var trt = _hudText.rectTransform;
            trt.anchorMin = new Vector2(0f, 0f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.pivot = new Vector2(0f, 0.5f);
            trt.offsetMin = new Vector2(16f, 0f);
            trt.offsetMax = new Vector2(-440f, 0f);

            // Buttons rechts, manuell positioniert (LayoutGroups sparen wir uns hier).
            var labels = new[] { "Hinweis", "Mischen", "Undo", "Menü" };
            var actions = new UnityAction[]
            {
                () => _manager.DoHint(),
                () => _manager.DoShuffle(),
                () => _manager.DoUndo(),
                () => ToggleMenu(),
            };

            for (var i = 0; i < labels.Length; i++)
            {
                var button = MakeButton(
                    bar.transform, labels[i], actions[i], new Vector2(100f, 40f), 18);
                var brt = button.GetComponent<RectTransform>();
                brt.anchorMin = new Vector2(1f, 0.5f);
                brt.anchorMax = new Vector2(1f, 0.5f);
                brt.pivot = new Vector2(1f, 0.5f);
                brt.anchoredPosition = new Vector2(-16f - (labels.Length - 1 - i) * 110f, 0f);
            }
        }

        // ---------------------------------------------------------------- Menue

        GameObject BuildMenu(Transform canvas)
        {
            var panel = MakePanel(canvas, new Color(0.03f, 0.06f, 0.03f, 0.92f));
            panel.name = "MenuPanel";
            panel.gameObject.SetActive(false);

            var title = MakeText(panel.transform, "Mahjong Solitaire", 52, TextAnchor.MiddleCenter,
                new Color(0.95f, 0.92f, 0.78f));
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.offsetMin = new Vector2(0f, -116f);
            title.rectTransform.offsetMax = new Vector2(0f, -36f);

            // Scrollbarer Bereich unter dem Titel: alles (Aktionen + Layouts)
            // kommt in EINE Column — das Menue passt damit auf jedes
            // Hoehenverhaeltnis (Tablet-Portrait!) statt an feste Prozent-Anker
            // zu kollidieren. ScrollRect macht lange Listen bedienbar.
            var scrollGo = new GameObject("MenuScroll", typeof(RectTransform), typeof(ScrollRect));
            var srt = scrollGo.GetComponent<RectTransform>();
            srt.SetParent(panel.transform, false);
            srt.anchorMin = new Vector2(0f, 0f);
            srt.anchorMax = new Vector2(1f, 1f);
            srt.offsetMin = new Vector2(0f, 20f);
            srt.offsetMax = new Vector2(0f, -128f);
            var scrollRect = scrollGo.GetComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.scrollSensitivity = 24f;

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            var vrt = viewport.GetComponent<RectTransform>();
            vrt.SetParent(scrollGo.transform, false);
            vrt.anchorMin = Vector2.zero;
            vrt.anchorMax = Vector2.one;
            vrt.offsetMin = Vector2.zero;
            vrt.offsetMax = Vector2.zero;
            var viewportImage = viewport.GetComponent<Image>();
            viewportImage.sprite = WhiteSprite;
            viewportImage.color = new Color(0f, 0f, 0f, 0f); // unsichtbar, dient als Raycast-Ziehflaeche
            scrollRect.viewport = vrt;

            var content = new GameObject("Content", typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            var crt = content.GetComponent<RectTransform>();
            crt.SetParent(vrt, false);
            crt.anchorMin = new Vector2(0.5f, 1f);
            crt.anchorMax = new Vector2(0.5f, 1f);
            crt.pivot = new Vector2(0.5f, 1f);
            var group = content.GetComponent<VerticalLayoutGroup>();
            group.padding = new RectOffset(0, 24, 16, 0);
            group.spacing = 10f;
            group.childControlWidth = false;
            group.childControlHeight = false;
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = false;
            group.childAlignment = TextAnchor.UpperCenter;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.content = crt;

            AddMenuButton(content.transform, "Weiterspielen", () => ToggleMenu());
            AddMenuButton(content.transform, "Neue Partie", () =>
            {
                ToggleMenu();
                _manager.NewGame();
            });
            AddMenuButton(content.transform, "Vollbild an/aus", () =>
            {
                Screen.fullScreen = !Screen.fullScreen;
            });

            var caption = MakeText(content.transform, "Layout wählen (startet neue Partie)", 20,
                TextAnchor.MiddleCenter, new Color(0.8f, 0.8f, 0.8f));
            caption.rectTransform.sizeDelta = new Vector2(480f, 44f);
            var captionElement = caption.gameObject.AddComponent<LayoutElement>();
            captionElement.preferredHeight = 44f;

            var layouts = content.transform;

            foreach (var (resource, label) in Layouts)
            {
                var res = resource; // Closure-Sicherheit
                AddMenuButton(layouts, label, () =>
                {
                    ToggleMenu();
                    _manager.NewGame(res, UnityEngine.Random.Range(0, int.MaxValue));
                });
            }

            _soundToggleText = MakeText(
                AddMenuButton(layouts, "", () =>
                {
                    _manager.ToggleSound();
                    UpdateSoundToggleText();
                }).transform,
                "", 24, TextAnchor.MiddleCenter, Color.white);
            UpdateSoundToggleText();

            return panel.gameObject;
        }

        GameObject MakeColumn(Transform parent, Vector2 anchorY, float width, float shiftY = 0f)
        {
            var go = new GameObject("Column", typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, anchorY.y);
            rt.anchorMax = new Vector2(0.5f, anchorY.y);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, shiftY);

            var group = go.GetComponent<VerticalLayoutGroup>();
            group.spacing = 10f;
            group.childControlWidth = false;
            group.childControlHeight = false;
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = false;
            group.childAlignment = TextAnchor.UpperCenter;

            go.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return go;
        }

        Button AddMenuButton(Transform column, string label, UnityAction onClick)
        {
            var button = MakeButton(column, label, onClick, new Vector2(340f, 52f), 24);

            var element = button.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = 340f;
            element.preferredHeight = 52f;

            return button;
        }

        // ---------------------------------------------------------------- Dialoge

        GameObject BuildWinDialog(Transform canvas)
        {
            var window = MakeWindow(canvas, "WinDialog", new Vector2(560f, 260f), new Color(0.05f, 0.10f, 0.06f, 0.96f));

            var title = MakeText(window.transform, "GEWONNEN!", 44, TextAnchor.MiddleCenter, new Color(0.55f, 0.95f, 0.60f));
            CenteredText(title, 78f);

            _winScoreText = MakeText(window.transform, "", 26, TextAnchor.MiddleCenter, Color.white);
            CenteredText(_winScoreText, 14f);

            var column = MakeColumn(window.transform, new Vector2(0.5f, 0.5f), 340f, shiftY: -78f);

            AddMenuButton(column.transform, "Neue Partie", () => _manager.NewGame());
            AddMenuButton(column.transform, "Menü", () =>
            {
                window.SetActive(false);
                ToggleMenu();
            });

            window.SetActive(false);
            return window;
        }

        GameObject BuildDeadlockDialog(Transform canvas)
        {
            var window = MakeWindow(canvas, "DeadlockDialog", new Vector2(560f, 260f), new Color(0.12f, 0.07f, 0.02f, 0.96f));

            var title = MakeText(window.transform, "Kein Zug mehr möglich", 36, TextAnchor.MiddleCenter, new Color(0.95f, 0.75f, 0.45f));
            CenteredText(title, 84f);

            var hint = MakeText(window.transform, "Mische die verbleibenden Steine oder starte neu.", 22, TextAnchor.MiddleCenter, Color.white);
            CenteredText(hint, 22f);

            var column = MakeColumn(window.transform, new Vector2(0.5f, 0.5f), 340f, shiftY: -128f);

            AddMenuButton(column.transform, "Mischen", () => _manager.DoShuffle());
            AddMenuButton(column.transform, "Neue Partie", () => _manager.NewGame());

            window.SetActive(false);
            return window;
        }

        static GameObject MakeWindow(Transform canvas, string name, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(canvas, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;

            var image = go.GetComponent<Image>();
            image.sprite = WhiteSprite;
            image.color = color;

            return go;
        }

        // ---------------------------------------------------------------- Laufzeit-Update

        void Update()
        {
            if (_manager == null || _hudText == null)
            {
                return;
            }

            SyncViewLabels();

            _hudText.text = "Zeit " + GameManager.FormatTime(_manager.Elapsed)
                + "    Punkte " + _manager.Score
                + "    Steine " + _manager.TileCount;

            var menuOpen = _menuPanel != null && _menuPanel.activeSelf;

            if (_winDialog != null)
            {
                var showWin = _manager.Won && !menuOpen;
                _winDialog.SetActive(showWin);

                if (showWin && _winScoreText != null)
                {
                    _winScoreText.text = "Punkte " + _manager.Score
                        + "   ·   Zeit " + GameManager.FormatTime(_manager.Elapsed);
                }
            }

            if (_deadlockDialog != null)
            {
                _deadlockDialog.SetActive(_manager.Deadlocked && !_manager.Won && !menuOpen);
            }

            _manager.Paused = _manager.Won || menuOpen;
        }

        /// <summary>
        /// Anzeige-Werte an die echte Kamera-Pose anpassen (auch nach Gesten-Orbit
        /// oder Rechtsklick-Drehung). Vor Partie-Start liefert PitchDeg 0.
        /// </summary>
        void SyncViewLabels()
        {
            if (_manager == null || _pitchValueText == null || _manager.PitchDeg <= 0f)
            {
                return;
            }

            var pitch = Mathf.RoundToInt(_manager.PitchDeg) + "°";

            if (_pitchValueText.text != pitch)
            {
                _pitchValueText.text = pitch;
            }

            var azimuth = Mathf.RoundToInt(_manager.AzimuthDeg) + "°";

            if (_azimuthValueText.text != azimuth)
            {
                _azimuthValueText.text = azimuth;
            }
        }

        void ToggleMenu()
        {
            if (_menuPanel != null)
            {
                _menuPanel.SetActive(!_menuPanel.activeSelf);

                if (_menuPanel.activeSelf)
                {
                    UpdateSoundToggleText();
                }
            }
        }

        void UpdateSoundToggleText()
        {
            if (_soundToggleText != null)
            {
                _soundToggleText.text = "Ton: " + (_manager != null && _manager.SoundOn ? "An" : "Aus");
            }
        }

        // ---------------------------------------------------------------- UI-Helfer

        static Text MakeText(Transform parent, string text, int size, TextAnchor anchor, Color color)
        {
            var go = new GameObject("Text", typeof(Text));
            var t = go.GetComponent<Text>();
            t.transform.SetParent(parent, false);
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.supportRichText = false;
            return t;
        }

        /// <summary>Text mittig im Eltern-Fenster platzieren (Y-Versatz vom Zentrum).</summary>
        static void CenteredText(Text t, float yOffset)
        {
            var rt = t.rectTransform;
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, yOffset);
            rt.offsetMin = new Vector2(24f, 0f);
            rt.offsetMax = new Vector2(-24f, 0f);
        }

        static Image MakePanel(Transform parent, Color color)
        {
            var go = new GameObject("Panel", typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var image = go.GetComponent<Image>();
            image.sprite = WhiteSprite;
            image.color = color;
            return image;
        }

        // --------------------------------------------------- Ansicht-Regler (Spielfeld)

        /// <summary>
        /// Kompaktes Bedienfeld unten links, immer sichtbar: Winkel in
        /// 5-Grad-Schritten, Drehung in 45-Grad-Schritten, jeweils mit eigenem
        /// Reset — Buttons statt Slider (Slider-Drag kollidierte mit der
        /// Brett-Steuerung).
        /// </summary>
        void BuildViewControls(Transform canvas)
        {
            var panel = new GameObject("ViewControls", typeof(Image));
            var prt = panel.GetComponent<RectTransform>();
            prt.SetParent(canvas, false);
            prt.anchorMin = new Vector2(0f, 0f);
            prt.anchorMax = new Vector2(0f, 0f);
            prt.pivot = new Vector2(0f, 0f);
            prt.sizeDelta = new Vector2(300f, 104f);
            prt.anchoredPosition = new Vector2(12f, 12f);

            var image = panel.GetComponent<Image>();
            image.sprite = WhiteSprite;
            image.color = new Color(0f, 0f, 0f, 0.35f);

            MakeViewRow(panel.transform, "Winkel", true,
                () => _manager.NudgePitch(-5f),
                () => _manager.NudgePitch(5f),
                () => _manager.ResetPitchDeg(),
                out _pitchValueText);

            MakeViewRow(panel.transform, "Drehung", false,
                () => _manager.NudgeAzimuth(-45f),
                () => _manager.NudgeAzimuth(45f),
                () => _manager.ResetAzimuthDeg(),
                out _azimuthValueText);
        }

        /// <summary>Zeile im Ansicht-Panel: Label, Minus, Wert, Plus, Reset.</summary>
        void MakeViewRow(
            Transform panel,
            string label,
            bool topRow,
            UnityAction minus,
            UnityAction plus,
            UnityAction reset,
            out Text valueText)
        {
            var row = new GameObject("Row " + label, typeof(RectTransform));
            var rr = row.GetComponent<RectTransform>();
            rr.SetParent(panel, false);
            rr.anchorMin = new Vector2(0f, 1f);
            rr.anchorMax = new Vector2(1f, 1f);
            rr.pivot = new Vector2(0.5f, 1f);
            rr.offsetMin = new Vector2(12f, topRow ? -50f : -96f);
            rr.offsetMax = new Vector2(-12f, topRow ? -6f : -52f);

            var caption = MakeText(row.transform, label, 18, TextAnchor.MiddleLeft, Color.white);
            var crt = caption.rectTransform;
            crt.anchorMin = new Vector2(0f, 0.5f);
            crt.anchorMax = new Vector2(0f, 0.5f);
            crt.pivot = new Vector2(0f, 0.5f);
            crt.sizeDelta = new Vector2(64f, 26f);
            crt.anchoredPosition = Vector2.zero;

            valueText = MakeText(row.transform, "0°", 18, TextAnchor.MiddleCenter,
                new Color(0.85f, 0.9f, 0.85f));
            var vrt = valueText.rectTransform;
            vrt.anchorMin = new Vector2(0f, 0.5f);
            vrt.anchorMax = new Vector2(0f, 0.5f);
            vrt.pivot = new Vector2(0f, 0.5f);
            vrt.sizeDelta = new Vector2(46f, 26f);
            vrt.anchoredPosition = new Vector2(108f, 0f);

            MakeStepButton(row.transform, "-", 40f, minus, 66f);
            MakeStepButton(row.transform, "+", 40f, plus, 158f);

            var resetBtn = MakeButton(row.transform, "Reset", reset, new Vector2(84f, 38f), 16);
            var brt = resetBtn.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(1f, 0.5f);
            brt.anchorMax = new Vector2(1f, 0.5f);
            brt.pivot = new Vector2(1f, 0.5f);
            brt.anchoredPosition = Vector2.zero;
        }

        /// <summary>Quadratischer +/- Schrittknopf, links verankert in der Zeile.</summary>
        static void MakeStepButton(Transform row, string label, float size, UnityAction onClick, float x)
        {
            var button = MakeButton(row, label, onClick, new Vector2(size, size), 22);
            var rt = button.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(x, 0f);
        }

        static Button MakeButton(Transform parent, string label, UnityAction onClick, Vector2 size, int fontSize)
        {
            var go = new GameObject("Btn " + label, typeof(Image), typeof(Button));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.sizeDelta = size;

            var image = go.GetComponent<Image>();
            image.sprite = WhiteSprite;
            image.color = new Color(0.25f, 0.34f, 0.25f, 1f);

            var button = go.GetComponent<Button>();
            button.onClick.AddListener(onClick);

            var text = MakeText(go.transform, label, fontSize, TextAnchor.MiddleCenter, Color.white);
            var trt = text.rectTransform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;

            return button;
        }
    }
}