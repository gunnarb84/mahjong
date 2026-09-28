using Mahjong.Core;

namespace Mahjong.Game
{
    /// <summary>
    /// Feste Zuordnung Stein-Typ -> Zelle im generierten Stein-Atlas
    /// (8 Spalten x 8 Reihen = 64 Zellen, 42 belegt — 4096² POT-Textur, damit
    /// WebGL sie ohne Probleme mipmapt und komprimiert). Wird vom Atlas-Generator
    /// (Editor) und von TileView (Runtime) gemeinsam benutzt.
    /// </summary>
    public static class TileFaces
    {
        public const int Columns = 8;
        public const int Rows = 8;

        public static int Index(TileType type)
        {
            switch (type.Kind)
            {
                case TileKind.Dot: return type.Number - 1;      // 0-8
                case TileKind.Bamboo: return 8 + type.Number;   // 9-17
                case TileKind.Character: return 17 + type.Number; // 18-26
                case TileKind.Wind: return 26 + type.Number;    // 27-30
                case TileKind.Dragon: return 30 + type.Number;  // 31-33
                case TileKind.Flower: return 33 + type.Number;  // 34-37
                case TileKind.Season: return 37 + type.Number;  // 38-41
                default: return Columns * Rows - 1;
            }
        }
    }
}