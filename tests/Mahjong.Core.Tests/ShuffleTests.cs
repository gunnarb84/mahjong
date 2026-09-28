using System;
using System.Collections.Generic;
using System.Linq;
using Mahjong.Core;
using Xunit;

namespace Mahjong.Core.Tests
{
    public class ShuffleTests
    {
        static BoardState BeispielBoard()
        {
            var board = new BoardState();
            board.AddTile(new Pos(0, 0, 0), new TileType(TileKind.Dot, 1));
            board.AddTile(new Pos(4, 0, 0), new TileType(TileKind.Dot, 2));
            board.AddTile(new Pos(8, 0, 0), new TileType(TileKind.Bamboo, 3));
            board.AddTile(new Pos(12, 0, 0), new TileType(TileKind.Bamboo, 3));
            return board;
        }

        [Fact]
        public void Shuffle_ErhaeltSteinbestand()
        {
            var board = BeispielBoard();
            var vorher = board.Tiles.Select(kv => kv.Value.Id).OrderBy(id => id).ToList();

            Assert.True(board.ShuffleTypes(new Random(7)));

            var nachher = board.Tiles.Select(kv => kv.Value.Id).OrderBy(id => id).ToList();
            Assert.Equal(vorher, nachher);
        }

        [Fact]
        public void Shuffle_AendertPositionenNicht()
        {
            var board = BeispielBoard();
            var vorher = board.Tiles.Select(kv => kv.Key).OrderBy(p => p.X).ToList();

            Assert.True(board.ShuffleTypes(new Random(7)));

            var nachher = board.Tiles.Select(kv => kv.Key).OrderBy(p => p.X).ToList();
            Assert.Equal(vorher, nachher);
        }

        [Fact]
        public void Shuffle_GleicherSeed_LiefertGleicheBelegung()
        {
            var a = BeispielBoard();
            var b = BeispielBoard();
            Assert.True(a.ShuffleTypes(new Random(42)));
            Assert.True(b.ShuffleTypes(new Random(42)));

            var aBelegung = a.Tiles.OrderBy(kv => kv.Key.X).Select(kv => kv.Value.Id);
            var bBelegung = b.Tiles.OrderBy(kv => kv.Key.X).Select(kv => kv.Value.Id);
            Assert.Equal(aBelegung, bBelegung);
        }

        [Fact]
        public void Shuffle_ErzeugtMindestensEinenZug()
        {
            var board = BeispielBoard();
            Assert.True(board.ShuffleTypes(new Random(3)));
            Assert.True(board.CountAvailablePairs() > 0);
        }

        [Fact]
        public void Shuffle_EinzelnerStein_LiefertFalse()
        {
            var board = new BoardState();
            board.AddTile(new Pos(0, 0, 0), new TileType(TileKind.Dot, 1));
            Assert.False(board.ShuffleTypes(new Random(1)));
        }

        [Fact]
        public void Shuffle_UnpaarbaresRestboard_LiefertFalse_undBehaeltBelegung()
        {
            // Zwei uebrige Steine, die nie paaren koennen — Mischen kann nicht helfen.
            var board = new BoardState();
            board.AddTile(new Pos(0, 0, 0), new TileType(TileKind.Dot, 1));
            board.AddTile(new Pos(4, 0, 0), new TileType(TileKind.Dot, 2));

            Assert.False(board.ShuffleTypes(new Random(5)));

            Assert.Equal(new TileType(TileKind.Dot, 1), board.GetTile(new Pos(0, 0, 0)));
            Assert.Equal(new TileType(TileKind.Dot, 2), board.GetTile(new Pos(4, 0, 0)));
        }
    }
}