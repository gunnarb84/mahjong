using System.IO;
using Mahjong.Core;
using UnityEditor;
using UnityEngine;

namespace Mahjong.Game.Editor
{
    /// <summary>
    /// Erzeugt den Stein-Atlas (alle 42 Gesichter als ein PNG) einmalig im Editor:
    /// Menue "Mahjong/Stein-Atlas generieren". Rendert die Symbole per TextMesh in
    /// eine Render-Textur und legt sie in Bein-Farbe + Rand in die Atlaszellen.
    /// Das Spiel laedt danach nur noch das PNG — keine Font-Abhaengigkeit mehr
    /// (wichtig fuer WebGL).
    /// </summary>
    public static class TileAtlasGenerator
    {
        const int CellSize = 512;
        const string OutputPath = "Assets/Resources/Art/TileAtlas.png";

        static readonly Color TileBase = new Color(0.93f, 0.89f, 0.78f);
        static readonly Color BorderColor = new Color(0.62f, 0.56f, 0.42f);

        [MenuItem("Mahjong/Stein-Atlas generieren")]
        public static void Generate()
        {
            var atlas = new Texture2D(
                TileFaces.Columns * CellSize, TileFaces.Rows * CellSize,
                TextureFormat.RGBA32, false);

            // Unbenutzte Zellen einfaerbigen (8x8-Raster, 42 belegt) — neue Texturen
            // sind sonst undefiniert, und WebGL-Compression verlangt definierten Inhalt.
            var fill = new Color32[TileFaces.Columns * CellSize * TileFaces.Rows * CellSize];
            var baseColor = (Color32)TileBase;

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
            cam.backgroundColor = TileBase; // Zell-Hintergrund = Stein-Farbe
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
                cam.orthographicSize = label.text.Contains("\n") ? 1.15f : 0.62f;
                textGo.transform.position = new Vector3(0f, 0f, -8f); // vor der Kamera (Kamera bei z=-10)

                cam.Render();

                var previous = RenderTexture.active;
                RenderTexture.active = rt;
                atlas.ReadPixels(new Rect(0, 0, CellSize, CellSize), col * CellSize, row * CellSize);
                RenderTexture.active = previous;
            }

            atlas.Apply();
            DrawCellBorders(atlas);
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

        /// <summary>
        /// Zeichnet um jede Zelle einen dezenten Rand — bewusst INNEN versetzt:
        /// der aeusserste Rand jeder Zelle bleibt einfarbig Stein-Hintergrund, damit
        /// Mipmaps ueber Zellgrenzen hinweg nichts sichtbares verschmieren.
        /// </summary>
        static void DrawCellBorders(Texture2D atlas)
        {
            const int Inset = 16;   // Abstand Rand <-> Zellkante (Mip-Spielraum)
            const int Width = 4;    // Randstaerke

            for (var index = 0; index < TileFaces.Columns * TileFaces.Rows; index++)
            {
                var px = (index % TileFaces.Columns) * CellSize;
                var py = (index / TileFaces.Columns) * CellSize;

                for (var i = 0; i < CellSize; i++)
                {
                    for (var t = 0; t < Width; t++)
                    {
                        atlas.SetPixel(px + i, py + Inset + t, BorderColor);
                        atlas.SetPixel(px + i, py + CellSize - 1 - Inset - t, BorderColor);
                        atlas.SetPixel(px + Inset + t, py + i, BorderColor);
                        atlas.SetPixel(px + CellSize - 1 - Inset - t, py + i, BorderColor);
                    }
                }
            }
        }
    }
}