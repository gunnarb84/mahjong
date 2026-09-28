namespace Mahjong.Core
{
    /// <summary>
    /// Position eines Steins auf dem Board, in Viertel-Kachel-Einheiten.
    /// Ein Stein belegt 2x2 Einheiten: [X, X+2) x [Y, Y+2) auf seiner Ebene.
    /// Viertel-Einheiten erlauben die typischen halb versetzten Reihen
    /// (z. B. beim klassischen Turtle-Layout).
    /// Hoehere Layer liegen optisch ueber niedrigeren.
    /// </summary>
    public readonly struct Pos : System.IEquatable<Pos>
    {
        public int X { get; }
        public int Y { get; }
        public int Layer { get; }

        public Pos(int x, int y, int layer)
        {
            X = x;
            Y = y;
            Layer = layer;
        }

        public bool Equals(Pos other) => X == other.X && Y == other.Y && Layer == other.Layer;

        public override bool Equals(object obj) => obj is Pos p && Equals(p);

        public override int GetHashCode() => (X * 73856093) ^ (Y * 19349663) ^ (Layer * 83492791);

        public static bool operator ==(Pos a, Pos b) => a.Equals(b);

        public static bool operator !=(Pos a, Pos b) => !a.Equals(b);

        public override string ToString() => "(" + X + "," + Y + ",L" + Layer + ")";
    }
}