using System;

namespace Mahjong.Core
{
    /// <summary>
    /// Die Gruppen von Spielsteinen.
    /// </summary>
    public enum TileKind
    {
        /// <summary>Kreise (Dots), Rank 1-9.</summary>
        Dot,

        /// <summary>Bambus, Rank 1-9.</summary>
        Bamboo,

        /// <summary>Zeichen (Characters), Rank 1-9.</summary>
        Character,

        /// <summary>Winde, Rank 1-4 = Ost, Sued, West, Nord.</summary>
        Wind,

        /// <summary>Drachen, Rank 1-3 = Rot, Gruen, Weiss.</summary>
        Dragon,

        /// <summary>Blumen, Rank 1-4. Jede Blume paart mit jeder anderen Blume.</summary>
        Flower,

        /// <summary>Jahreszeiten, Rank 1-4. Jede Jahreszeit paart mit jeder anderen Jahreszeit.</summary>
        Season,
    }

    /// <summary>
    /// Typ eines Spielsteins. Ein Satz hat 34 Standardtypen (Kreise/Bambus/Zeichen 1-9,
    /// 4 Winde, 3 Drachen) mit je 4 Kopien sowie 4 Blumen und 4 Jahreszeiten je 1 Kopie.
    /// </summary>
    public readonly struct TileType : IEquatable<TileType>
    {
        public TileKind Kind { get; }

        /// <summary>
        /// Rank: 1-9 bei Kreisen/Bambus/Zeichen, 1-4 bei Winden/Blumen/Jahreszeiten,
        /// 1-3 bei Drachen.
        /// </summary>
        public int Number { get; }

        public TileType(TileKind kind, int number)
        {
            Kind = kind;
            Number = number;
        }

        /// <summary>
        /// Zwei Steine sind paubar, wenn sie vom gleichen Typ sind — oder beide Blumen
        /// bzw. beide Jahreszeiten mit unterschiedlicher Nummer sind.
        /// </summary>
        public static bool CanPair(TileType a, TileType b)
        {
            if (a.Kind != b.Kind)
            {
                return false;
            }

            if (a.Kind == TileKind.Flower || a.Kind == TileKind.Season)
            {
                return a.Number != b.Number;
            }

            return a.Number == b.Number;
        }

        /// <summary>
        /// Stabile Text-Id, z. B. "dot-1", "bam-5", "wind-E", "flower-2".
        /// Dient spaeter als Key in den Stein-Texturen.
        /// </summary>
        public string Id
        {
            get
            {
                switch (Kind)
                {
                    case TileKind.Dot: return "dot-" + Number;
                    case TileKind.Bamboo: return "bam-" + Number;
                    case TileKind.Character: return "char-" + Number;
                    case TileKind.Wind: return "wind-" + WindName(Number);
                    case TileKind.Dragon: return "dragon-" + DragonName(Number);
                    case TileKind.Flower: return "flower-" + Number;
                    case TileKind.Season: return "season-" + Number;
                    default: throw new ArgumentOutOfRangeException(nameof(Kind), Kind, null);
                }
            }
        }

        private static string WindName(int number)
        {
            switch (number)
            {
                case 1: return "E";
                case 2: return "S";
                case 3: return "W";
                case 4: return "N";
                default: throw new ArgumentOutOfRangeException(nameof(number));
            }
        }

        private static string DragonName(int number)
        {
            switch (number)
            {
                case 1: return "red";
                case 2: return "green";
                case 3: return "white";
                default: throw new ArgumentOutOfRangeException(nameof(number));
            }
        }

        public bool Equals(TileType other) => Kind == other.Kind && Number == other.Number;

        public override bool Equals(object obj) => obj is TileType t && Equals(t);

        public override int GetHashCode() => (int)Kind * 31 + Number;

        public static bool operator ==(TileType a, TileType b) => a.Equals(b);

        public static bool operator !=(TileType a, TileType b) => !a.Equals(b);

        public override string ToString() => Id;
    }
}