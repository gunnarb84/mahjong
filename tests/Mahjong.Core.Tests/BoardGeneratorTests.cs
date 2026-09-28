using System;
using System.Collections.Generic;
using System.Linq;
using Mahjong.Core;
using Xunit;

namespace Mahjong.Core.Tests
{
    public class BoardGeneratorTests
    {
        private static LayoutData Turtle =>
            LayoutLoader.LoadFile(LayoutLoaderTests.TurtlePath);

        [Fact]
        public void Generiert_AllePositionen()
        {
            var generated = BoardGenerator.Generate(Turtle.Positions, seed: 42);

            Assert.Equal(144, generated.Board.TileCount);
            Assert.Equal(
                Turtle.Positions.Count,
                generated.Positions().Count());
        }

        [Fact]
        public void Generiert_72Zuege()
        {
            var generated = BoardGenerator.Generate(Turtle.Positions, seed: 42);

            Assert.Equal(72, generated.Solution.Count);
        }

        [Fact]
        public void Steinbestand_IstKorrekt()
        {
            var generated = BoardGenerator.Generate(Turtle.Positions, seed: 42);

            var counts = new Dictionary<TileType, int>();

            foreach (var kv in generated.Board.Tiles)
            {
                counts.TryGetValue(kv.Value, out var n);
                counts[kv.Value] = n + 1;
            }

            // 34 Standardtypen je 4x
            foreach (var type in BoardGenerator.StandardTypes())
            {
                Assert.Equal(4, counts[type]);
            }

            // Blumen/Jahreszeiten je 1x
            for (var n = 1; n <= 4; n++)
            {
                Assert.Equal(1, counts[new TileType(TileKind.Flower, n)]);
                Assert.Equal(1, counts[new TileType(TileKind.Season, n)]);
            }

            Assert.Equal(144, counts.Values.Sum());
        }

        [Fact]
        public void Solution_IstTatsaechlichSpielbar()
        {
            var generated = BoardGenerator.Generate(Turtle.Positions, seed: 42);
            var board = generated.Board.Clone();

            foreach (var move in generated.Solution)
            {
                Assert.True(
                    board.TryRemove(move.First, move.Second),
                    "Zug " + move + " war nicht gueltig — Loesung kaputt.");
            }

            Assert.Equal(0, board.TileCount);
        }

        [Fact]
        public void Solution_AnfangspositionenSindFrei()
        {
            // Der erste Zug der Loesung muss mit freien Steinen des fertigen Boards moeglich sein.
            var generated = BoardGenerator.Generate(Turtle.Positions, seed: 7);
            var first = generated.Solution[0];

            Assert.True(generated.Board.IsFree(first.First));
            Assert.True(generated.Board.IsFree(first.Second));
        }

        [Fact]
        public void GleicherSeed_LiefertGleichesBoard()
        {
            var a = BoardGenerator.Generate(Turtle.Positions, seed: 123);
            var b = BoardGenerator.Generate(Turtle.Positions, seed: 123);

            Assert.Equal(
                a.Board.Tiles.OrderBy(t => t.Key.GetHashCode()).Select(t => t.Key + ":" + t.Value).ToList(),
                b.Board.Tiles.OrderBy(t => t.Key.GetHashCode()).Select(t => t.Key + ":" + t.Value).ToList());
        }

        [Fact]
        public void VerschiedeneSeeds_LiefernVerschiedeneBelegungen()
        {
            var a = BoardGenerator.Generate(Turtle.Positions, seed: 1);
            var b = BoardGenerator.Generate(Turtle.Positions, seed: 2);

            var tilesA = a.Board.Tiles.ToDictionary(t => t.Key, t => t.Value);

            var differs = b.Board.Tiles.Any(t => tilesA.TryGetValue(t.Key, out var v) && v != t.Value);

            Assert.True(differs);
        }

        [Fact]
        public void MehrereSeeds_ErzeugenAlleLoesbareBoards()
        {
            // Stresstest ueber mehrere Seeds — hier mit moderater Menge, damit der Test schnell bleibt.
            for (var seed = 1; seed <= 10; seed++)
            {
                var generated = BoardGenerator.Generate(Turtle.Positions, seed);
                var board = generated.Board.Clone();

                foreach (var move in generated.Solution)
                {
                    Assert.True(board.TryRemove(move.First, move.Second));
                }

                Assert.Equal(0, board.TileCount);
            }
        }

        [Fact]
        public void UngeradePositionenAnzahl_Wirft()
        {
            var positions = new List<Pos>
            {
                new Pos(0, 0, 0),
                new Pos(2, 0, 0),
                new Pos(4, 0, 0),
            };

            Assert.Throws<ArgumentException>(() => BoardGenerator.Generate(positions, 1));
        }
    }

    internal static class GeneratedBoardExtensions
    {
        public static IEnumerable<Pos> Positions(this GeneratedBoard board)
        {
            foreach (var kv in board.Board.Tiles)
            {
                yield return kv.Key;
            }
        }
    }
}