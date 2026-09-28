using System;
using System.Collections.Generic;

namespace Mahjong.Core
{
    /// <summary>Ein Zug der Loesung: zwei zu entfernende Positionen.</summary>
    public readonly struct Move : System.IEquatable<Move>
    {
        public Pos First { get; }
        public Pos Second { get; }

        public Move(Pos first, Pos second)
        {
            First = first;
            Second = second;
        }

        public bool Equals(Move other) =>
            (First == other.First && Second == other.Second)
            || (First == other.Second && Second == other.First);

        public override string ToString() => First + " + " + Second;
    }

    /// <summary>Ergebnis des Generators: Board plus garantiert funktionierende Loesung.</summary>
    public sealed class GeneratedBoard
    {
        public BoardState Board { get; }
        public IReadOnlyList<Move> Solution { get; }

        public GeneratedBoard(BoardState board, IReadOnlyList<Move> solution)
        {
            Board = board;
            Solution = solution;
        }
    }

    /// <summary>
    /// Erzeugt garantiert loesbare Boards. Vorgehen:
    ///
    /// 1. Das vollstaendige Layout wird mit Wildcard-Steinen belegt — die
    ///    Freisteh-Regel ist rein geometrisch, Typen spielen dafuer keine Rolle.
    /// 2. Das Board wird fortlaufend abgebaut: je zwei gleichzeitig freie Steine
    ///    werden entfernt, bis nichts mehr uebrig ist. Jede dieser Entfernungen
    ///    ist ein Zug der Loesung — ist der Abbau vollstaendig, ist die Zugfolge
    ///    beweisbar eine gueltige Loesung.
    /// 3. Die Zuege bekommen die 72 Stein-Paare des Standard-Satzes zugewiesen
    ///    (jeder Zug ist damit per Definition ein paarendes Paar).
    ///
    /// Scheitert eine Zufalls-Abbaureihenfolge (Deadlock: weniger als 2 freie
    /// Steine uebrig), wird neu gewuerfelt — der Turtle und andere Boards sind
    /// nach wenigen Versuchen praktisch immer abbaubar.
    /// </summary>
    public static class BoardGenerator
    {
        public const int MaxAttempts = 10000;

        private static readonly TileType Wildcard = new TileType(TileKind.Dot, 1);

        public static GeneratedBoard Generate(IReadOnlyList<Pos> positions, int seed)
        {
            if (positions == null)
            {
                throw new ArgumentNullException(nameof(positions));
            }

            if (positions.Count % 2 != 0)
            {
                throw new ArgumentException("Layout braucht gerade Steinanzahl, hat " + positions.Count + ".");
            }

            var rng = new Random(seed);

            for (var attempt = 0; attempt < MaxAttempts; attempt++)
            {
                if (TryGenerate(positions, rng, out var moves))
                {
                    var board = new BoardState();
                    var pool = BuildPairPool(positions.Count / 2);
                    Shuffle(pool, rng);

                    for (var i = 0; i < moves.Count; i++)
                    {
                        board.AddTile(moves[i].First, pool[i].First);
                        board.AddTile(moves[i].Second, pool[i].Second);
                    }

                    return new GeneratedBoard(board, moves);
                }
            }

            throw new InvalidOperationException(
                "Keine loesbare Belegung nach " + MaxAttempts + " Versuchen gefunden.");
        }

        private static bool TryGenerate(IReadOnlyList<Pos> positions, Random rng, out List<Move>? moves)
        {
            moves = null;

            var board = new BoardState();

            foreach (var p in positions)
            {
                board.AddTile(p, Wildcard);
            }

            var found = new List<Move>();

            while (board.TileCount > 0)
            {
                // Freitheit ist geometrisch; zwei freie Steine sind gleichzeitig
                // entnehmbar, weil FreeTiles den aktuellen Boardzustand inklusive
                // des jeweiligen Partners einbezieht.
                var free = board.FreeTiles();

                if (free.Count < 2)
                {
                    return false; // Deadlock dieser Abbaureihenfolge: neu wuerfeln
                }

                var a = free[rng.Next(free.Count)];
                Pos b;

                do
                {
                    b = free[rng.Next(free.Count)];
                }
                while (b == a);

                board.RemoveForced(a);
                board.RemoveForced(b);
                found.Add(new Move(a, b));
            }

            moves = found;
            return true;
        }

        // ---------------------------------------------------------------- Stein-Pool

        /// <summary>
        /// Paare eines Standard-Satzes, skalierend auf die Layoutgroesse:
        /// 4 Blumen-/Jahreszeiten-Paare, dann Runde fuer Runde je ein Paar
        /// pro Standardtyp (2 Runden = 72 Paare = der klassische 144er-Satz).
        /// Groessere Layouts bekommen weitere Runden (manche Typen dann mehr als 4x).
        /// </summary>
        public static List<(TileType First, TileType Second)> BuildPairPool(int pairCount = 72)
        {
            if (pairCount < 4)
            {
                pairCount = 4; // die Blumen-/Jahreszeiten-Basis bleibt immer drin
            }

            var pairs = new List<(TileType, TileType)>();
            pairs.Add((new TileType(TileKind.Flower, 1), new TileType(TileKind.Flower, 2)));
            pairs.Add((new TileType(TileKind.Flower, 3), new TileType(TileKind.Flower, 4)));
            pairs.Add((new TileType(TileKind.Season, 1), new TileType(TileKind.Season, 2)));
            pairs.Add((new TileType(TileKind.Season, 3), new TileType(TileKind.Season, 4)));

            var standard = StandardTypes();

            while (pairs.Count < pairCount)
            {
                var type = standard[(pairs.Count - 4) % standard.Count];
                pairs.Add((type, type));
            }

            return pairs;
        }

        /// <summary>Die 34 Standardtypen (ohne Blumen/Jahreszeiten).</summary>
        public static List<TileType> StandardTypes()
        {
            var types = new List<TileType>();

            for (var n = 1; n <= 9; n++)
            {
                types.Add(new TileType(TileKind.Dot, n));
                types.Add(new TileType(TileKind.Bamboo, n));
                types.Add(new TileType(TileKind.Character, n));
            }

            for (var n = 1; n <= 4; n++)
            {
                types.Add(new TileType(TileKind.Wind, n));
            }

            for (var n = 1; n <= 3; n++)
            {
                types.Add(new TileType(TileKind.Dragon, n));
            }

            return types;
        }

        private static void Shuffle<T>(List<T> list, Random random)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                var tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }
    }
}