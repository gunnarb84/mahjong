using System.IO;
using Mahjong.Core;
using UnityEditor;
using UnityEngine;

namespace Mahjong.Game.Editor
{
    /// <summary>
    /// Erzeugt den Stein-Atlas (alle 42 Gesichter als ein PNG) einmalig im Editor:
    /// Menue "Mahjong/Stein-Atlas generieren". Rendert die Symbole per TextMesh in
    /// eine Render-Textur und legt sie in Stein-Farbe in die Atlaszellen
    /// (randlos — Rahmen wirkten wie aufgeklebte Sticker).
    /// Das Spiel laedt danach nur noch das PNG — keine Font-Abhaengigkeit mehr
    /// (wichtig fuer WebGL).
    /// </summary>
    public static class TileAtlasGenerator
    {
        const int CellSize = 512;
        const string OutputPath = "Assets/Resources/Art/TileAtlas.png";

        static readonly Color TileBase = new Color(0.93f, 0.89f, 0.78f);

        [MenuItem("Mahjong/Stein-Atlas generieren")]
        public static void Generate()
        {
            var atlas = new Texture2D(
                TileFaces.Columns * CellSize, TileFaces.Rows * CellSize,
                TextureFormat.RGBA32, false);

            // Unbenutzte Zellen einfaerbigen (8x8-Raster, 42 belegt) — neue Texturen
            // sind sonst undefiniert, und WebGL-Compression verlangt definierten Inhalt.
            // Alpha 0: der Symbol-Quad zeichnet nur die Glyphe (Alpha-Blend),
            // der Steinkoerper scheint durch — kein sichtbares Quad-Viereck.
            var fill = new Color32[TileFaces.Columns * CellSize * TileFaces.Rows * CellSize];
            var baseColor = (Color32)TileBase;
            baseColor.a = 0;

            for (var i = 0; i < fill.Length; i++)
            {
                fill[i] = baseColor;
            }

            atlas.SetPixels32(fill);

            var rt = new RenderTexture(CellSize, CellSize, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 8, // Kantenglaettung fuer die Glyphen
            };

            var camGo = new GameObject("AtlasCamera");
            camGo.hideFlags = HideFlags.HideAndDontSave;
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.aspect = 1f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(TileBase.r, TileBase.g, TileBase.b, 0f); // Alpha 0 = transparent
            cam.targetTexture = rt;
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            camGo.transform.rotation = Quaternion.identity;

            const int AtlasLayer = 30; // nur der Text liegt auf diesem Layer:
            cam.cullingMask = 1 << AtlasLayer; // Kamera rendert AUSSCHLIESSLICH den Text,
                                               // nie Szenen-Inhalte (auch im Play-Modus)

            var textGo = new GameObject("AtlasText");
            textGo.hideFlags = HideFlags.HideAndDontSave;
            textGo.layer = AtlasLayer;
            textGo.AddComponent<MeshRenderer>();
            var label = textGo.AddComponent<TextMesh>();
            label.fontSize = 384; // Glyphen-Rasterung: ~1:1 zur Zellgroesse -> scharf
            label.characterSize = 0.025f; // 384 * 0.025 = gleiche Weltgroesse wie 96 * 0.1
            label.fontStyle = FontStyle.Bold;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            textGo.GetComponent<MeshRenderer>().sharedMaterial = label.font.material;

            foreach (var type in AllTypes())
            {
                var index = TileFaces.Index(type);
                var col = index % TileFaces.Columns;
                var row = index / TileFaces.Columns;

                label.text = TileSymbol.TextFor(type);
                label.color = TileSymbol.ColorFor(type);
                cam.orthographicSize = OrthoSizeFor(label.text);
                textGo.transform.position = new Vector3(0f, 0f, -8f); // vor der Kamera (Kamera bei z=-10)

                cam.Render();

                var previous = RenderTexture.active;
                RenderTexture.active = rt;
                atlas.ReadPixels(new Rect(0, 0, CellSize, CellSize), col * CellSize, row * CellSize);
                RenderTexture.active = previous;
            }

            atlas.Apply();

            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(textGo);
            rt.Release();

            var directory = Path.GetDirectoryName(OutputPath);

            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(OutputPath, atlas.EncodeToPNG());
            AssetDatabase.Refresh();

            var importer = AssetImporter.GetAtPath(OutputPath) as TextureImporter;

            if (importer != null)
            {
                importer.mipmapEnabled = true; // gegen Aliasing beim Verkleinern; Zellraender
                                               // sind darum nach innen versetzt (kein Bluten)
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 4096; // 8x8 Zellen à 512 px nicht runterskalieren
                importer.anisoLevel = 8;

                // Builds (WebGL + Desktop): komprimiert (Desktop-Browser: S3TC/DXT),
                // volle Aufloesung. Filter/Aniso/Mipmaps erben vom Importer oben.
                foreach (var platform in new[] { "Standalone", "WebGL" })
                {
                    importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
                    {
                        name = platform,
                        overridden = true,
                        maxTextureSize = 4096,
                        textureCompression = TextureImporterCompression.Compressed,
                        format = TextureImporterFormat.Automatic,
                        compressionQuality = 100,
                    });
                }

                importer.SaveAndReimport();
            }

            Debug.Log("[Mahjong] Stein-Atlas erzeugt: " + OutputPath + " (42 Gesichter)");
        }

        /// <summary>
        /// Ortho-Groesse passend zum Symbolinhalt: Einzel-CJK-Zeichen gross,
        /// Punkt-/Bambus-Reihen nach Spaltenzahl x Zeichenzahl. Konstanten so
        /// kalibriert, dass ein "●" ~0.44 Welt-Einheiten und eine Symbolzeile
        /// ~0.62 hoch ist (passend zu characterSize 0.025 @ fontSize 384).
        /// </summary>
        static float OrthoSizeFor(string text)
        {
            var lines = text.Split('\n');

            if (lines.Length == 1 && lines[0].Length == 1)
            {
                return 0.62f; // einzelnes CJK-Zeichen
            }

            var maxCols = 0;

            foreach (var line in lines)
            {
                maxCols = Mathf.Max(maxCols, line.Length);
            }

            var halfWidth = maxCols * 0.44f * 0.5f;
            var halfHeight = lines.Length * 0.62f * 0.5f;
            // 1.45: Symbol bekommt ~30 % Rand zur Steinflaeche (klassischer Look) —
            // 1.08 fuellte die Flaeche randlos aus (sah schlecht aus).
            return Mathf.Max(halfWidth, halfHeight) * 1.45f;
        }

        static System.Collections.Generic.List<TileType> AllTypes()
        {
            var types = new System.Collections.Generic.List<TileType>();

            for (var n = 1; n <= 9; n++)
            {
                types.Add(new TileType(TileKind.Dot, n));
                types.Add(new TileType(TileKind.Bamboo, n));
                types.Add(new TileType(TileKind.Character, n));
            }

            for (var n = 1; n <= 4; n++)
            {
                types.Add(new TileType(TileKind.Wind, n));
                types.Add(new TileType(TileKind.Flower, n));
                types.Add(new TileType(TileKind.Season, n));
            }

            for (var n = 1; n <= 3; n++)
            {
                types.Add(new TileType(TileKind.Dragon, n));
            }

            return types;
        }
    }
}