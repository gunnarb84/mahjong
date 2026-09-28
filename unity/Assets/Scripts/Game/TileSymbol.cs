using Mahjong.Core;
using UnityEngine;

namespace Mahjong.Game
{
    /// <summary>
    /// Symbolik und Farben der Stein-Gesichter. Genutzt vom Atlas-Generator
    /// (Editor) — das Spiel selbst rendert nur noch die generierte Textur.
    /// </summary>
    public static class TileSymbol
    {
        static readonly Color DotColor = new Color(0.72f, 0.16f, 0.14f);
        static readonly Color BambooColor = new Color(0.15f, 0.45f, 0.20f);
        static readonly Color CharacterColor = new Color(0.13f, 0.25f, 0.62f);
        static readonly Color WindColor = new Color(0.35f, 0.25f, 0.10f);
        static readonly Color DragonColor = new Color(0.55f, 0.15f, 0.45f);
        static readonly Color FlowerColor = new Color(0.62f, 0.22f, 0.42f);
        static readonly Color SeasonColor = new Color(0.15f, 0.45f, 0.48f);

        static readonly string[] CharNumerals = { "一", "二", "三", "四", "五", "六", "七", "八", "九" };
        static readonly string[] WindGlyphs = { "東", "南", "西", "北" };
        static readonly string[] DragonGlyphs = { "中", "發", "口" };
        static readonly string[] FlowerGlyphs = { "梅", "蘭", "竹", "菊" };
        static readonly string[] SeasonGlyphs = { "春", "夏", "秋", "冬" };

        /// <summary>Symbolszene wie beim Original: Punktreihen, Bambus, CJK-Zeichen.</summary>
        public static string TextFor(TileType type)
        {
            switch (type.Kind)
            {
                case TileKind.Dot:
                case TileKind.Bamboo:
                    var glyph = type.Kind == TileKind.Dot ? "●" : "|";
                    return RowsFor(type.Number, glyph);

                case TileKind.Character: return CharNumerals[type.Number - 1];
                case TileKind.Wind: return WindGlyphs[type.Number - 1];
                case TileKind.Dragon: return DragonGlyphs[type.Number - 1];
                case TileKind.Flower: return FlowerGlyphs[type.Number - 1];
                case TileKind.Season: return SeasonGlyphs[type.Number - 1];
                default: return "?";
            }
        }

        public static Color ColorFor(TileType type)
        {
            switch (type.Kind)
            {
                case TileKind.Dot: return DotColor;
                case TileKind.Bamboo: return BambooColor;
                case TileKind.Character: return CharacterColor;
                case TileKind.Wind: return WindColor;
                case TileKind.Dragon: return DragonColor;
                case TileKind.Flower: return FlowerColor;
                case TileKind.Season: return SeasonColor;
                default: return Color.gray;
            }
        }

        /// <summary>N Glyphen: bis 3 in einer Reihe, darunter auf zwei Reihen verteilt.</summary>
        static string RowsFor(int n, string glyph)
        {
            if (n <= 3)
            {
                return Repeat(glyph, n);
            }

            var upper = (n + 1) / 2;
            var lower = n - upper;
            return Repeat(glyph, upper) + "\n" + Repeat(glyph, lower);
        }

        static string Repeat(string s, int count)
        {
            var result = "";

            for (var i = 0; i < count; i++)
            {
                result += s;
            }

            return result;
        }
    }
}