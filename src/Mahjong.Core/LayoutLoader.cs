using System;
using System.Collections.Generic;
using System.IO;

namespace Mahjong.Core
{
    /// <summary>
    /// Ein geparstes Board-Layout: nur Positionen, keine Stein-Typen.
    /// Formate (KMahjongg, Vierteil-Kachel-Raster):
    /// - v1.1: w/h/d-Header, Ebenen = H-Bloecke von unten nach oben.
    /// - v1.0: keine Header, Ebenen durch "# Level N"-Kommentar-Sektionen getrennt.
    /// In beiden markiert '1' die linke obere Ecke eines 2x2-Steins
    /// ('2' rechts oben, '4' links unten, '3' rechts unten).
    /// </summary>
    public sealed class LayoutData
    {
        public string Name { get; }
        public int Width { get; }
        public int Height { get; }
        public List<Pos> Positions { get; }

        public LayoutData(string name, int width, int height, List<Pos> positions)
        {
            Name = name;
            Width = width;
            Height = height;
            Positions = positions;
        }

        /// <summary>Anzahl Ebenen, die das Layout benutzt.</summary>
        public int Depth
        {
            get
            {
                var max = -1;

                foreach (var p in Positions)
                {
                    if (p.Layer > max)
                    {
                        max = p.Layer;
                    }
                }

                return max + 1;
            }
        }
    }

    public static class LayoutLoader
    {
        /// <summary>Laedt ein Layout aus einer Datei im KMahjongg-v1.0/v1.1-Format.</summary>
        public static LayoutData LoadFile(string path)
        {
            return Parse(File.ReadAllLines(path), Path.GetFileNameWithoutExtension(path));
        }

        /// <summary>Pars ein Layout aus Zeilen im KMahjongg-Layout-v1.0/v1.1-Format.</summary>
        public static LayoutData Parse(IEnumerable<string> lines, string name = "layout")
        {
            var rows = new List<string>();
            var sections = new List<List<string>> { new List<string>() };
            var width = 0;
            var height = 0;

            foreach (var raw in lines)
            {
                var line = raw.TrimEnd('\r', '\n', ' ');

                if (line.StartsWith("kmahjongg-layout"))
                {
                    continue; // Versions-Header
                }

                if (line.StartsWith("# Level"))
                {
                    // v1.0: jede Level-Sektion ist eine Ebene.
                    if (sections[sections.Count - 1].Count > 0)
                    {
                        sections.Add(new List<string>());
                    }

                    continue;
                }

                if (line.Length == 0 || line.StartsWith("#"))
                {
                    continue;
                }

                if (line.StartsWith("w") && int.TryParse(line.Substring(1), out var w))
                {
                    width = w;
                    continue;
                }

                if (line.StartsWith("h") && int.TryParse(line.Substring(1), out var h))
                {
                    height = h;
                    continue;
                }

                if (line.StartsWith("d") && int.TryParse(line.Substring(1), out _))
                {
                    continue; // Tiefe ist Information; wir zaehlen Ebenen selbst
                }

                rows.Add(line);
                sections[sections.Count - 1].Add(line);
            }

            if (rows.Count == 0)
            {
                throw new FormatException("Layout ohne Board-Zeilen.");
            }

            IReadOnlyList<IReadOnlyList<string>> layers;

            if (height > 0)
            {
                // v1.1: Zeilen laufen ohne Trennung durch, h-Blockweise Ebenen.
                if (rows.Count % height != 0)
                {
                    throw new FormatException(
                        "Layout-Zeilenanzahl (" + rows.Count + ") ist kein Vielfaches der Hoehe (" + height + ").");
                }

                var layerCount = rows.Count / height;
                var byHeight = new List<IReadOnlyList<string>>(layerCount);

                for (var layer = 0; layer < layerCount; layer++)
                {
                    byHeight.Add(rows.GetRange(layer * height, height));
                }

                layers = byHeight;
            }
            else
            {
                // v1.0: Ebenen = "# Level"-Sektionen.
                for (var i = sections.Count - 1; i >= 0; i--)
                {
                    if (sections[i].Count == 0)
                    {
                        sections.RemoveAt(i);
                    }
                }

                if (sections.Count == 0)
                {
                    throw new FormatException("v1.0-Layout ohne '# Level'-Sektionen.");
                }

                height = sections[0].Count;
                layers = sections;
            }

            if (width <= 0)
            {
                foreach (var layerRows in layers)
                {
                    foreach (var row in layerRows)
                    {
                        width = Math.Max(width, row.Length);
                    }
                }
            }

            var positions = new List<Pos>();

            for (var layer = 0; layer < layers.Count; layer++)
            {
                var layerRows = layers[layer];

                for (var y = 0; y < layerRows.Count; y++)
                {
                    var row = layerRows[y];

                    for (var x = 0; x < row.Length; x++)
                    {
                        // '1' markiert die linke obere Ecke eines 2x2-Steinblocks.
                        if (row[x] == '1')
                        {
                            positions.Add(new Pos(x, y, layer));
                            ValidateBlock(layerRows, y, x);
                        }
                    }
                }
            }

            return new LayoutData(name, width, height, positions);
        }

        /// <summary>
        /// Stellt sicher, dass um eine '1'-Ecke herum ein vollstaendiger 2x2-Block
        /// aus Ziffern liegt — faengt kaputte Layout-Dateien frueh ab.
        /// </summary>
        private static void ValidateBlock(IReadOnlyList<string> layerRows, int y, int x)
        {
            var row = layerRows[y];

            if (!IsDigitCell(row, x + 1))
            {
                throw new FormatException(
                    "Kaputtes Layout: Stein-Ecke ohne rechte Nachbarschaft bei (" + x + "," + y + ").");
            }

            if (y + 1 < layerRows.Count && !IsDigitCell(layerRows[y + 1], x))
            {
                throw new FormatException(
                    "Kaputtes Layout: Stein-Ecke ohne untere Nachbarschaft bei (" + x + "," + y + ").");
            }
        }

        private static bool IsDigitCell(string row, int x) =>
            x < row.Length && row[x] >= '1' && row[x] <= '4';
    }
}