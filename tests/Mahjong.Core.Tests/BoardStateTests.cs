using System;
using System.Linq;
using Mahjong.Core;
using Xunit;

namespace Mahjong.Core.Tests
{
    public class BoardStateTests
    {
        private static readonly TileType Any = new TileType(TileKind.Dot, 1);
        private static readonly TileType Other = new TileType(TileKind.Dot, 2);

        private static BoardState Board(params Pos[] positions)
        {
            var board = new BoardState();

            foreach (var p in positions)
            {
                board.AddTile(p, Any);
            }

            return board;
        }

        // ---------------------------------------------------------------- Freisteh-Regel

        [Fact]
        public void EinzelnerStein_IstFrei()
        {
            var board = Board(new Pos(4, 4, 0));

            Assert.True(board.IsFree(new Pos(4, 4, 0)));
        }

        [Fact]
        public void MittlererStein_MitZweiNachbarn_IstNichtFrei()
        {
            // Drei buendige Steine: der mittlere ist eingeschlossen, die Ränder sind frei.
            var board = Board(new Pos(0, 0, 0), new Pos(2, 0, 0), new Pos(4, 0, 0));

            Assert.True(board.IsFree(new Pos(0, 0, 0)));  // links frei
            Assert.False(board.IsFree(new Pos(2, 0, 0))); // beide Seiten blockiert
            Assert.True(board.IsFree(new Pos(4, 0, 0)));  // rechts frei
        }

        [Fact]
        public void Randstein_NachbarNurAufEinerSeite_IstFrei()
        {
            // Ein Nachbar rechts: der Stein kann nach links hinausgeschoben werden.
            var board = Board(new Pos(0, 0, 0), new Pos(2, 0, 0));

            Assert.True(board.IsFree(new Pos(0, 0, 0)));
            Assert.True(board.IsFree(new Pos(2, 0, 0)));
        }

        [Fact]
        public void Eckberuehrung_BlockiertNicht()
        {
            // Diagonal buendig: keine Hoehen-Ueberlappung, kein Block.
            var board = Board(new Pos(0, 0, 0), new Pos(2, 2, 0));

            Assert.True(board.IsFree(new Pos(0, 0, 0)));
            Assert.True(board.IsFree(new Pos(2, 2, 0)));
        }

        [Fact]
        public void HalbVersetztDiagonal_BlockiertNicht()
        {
            // Typischer Turtle-Fall: Reihen um eine halbe Kachel versetzt.
            var board = Board(new Pos(0, 0, 0), new Pos(1, 2, 0));

            Assert.True(board.IsFree(new Pos(0, 0, 0)));
            Assert.True(board.IsFree(new Pos(1, 2, 0)));
        }

        [Fact]
        public void SteinDarueber_Blockiert()
        {
            var board = Board(new Pos(0, 0, 0), new Pos(0, 0, 1));

            Assert.False(board.IsFree(new Pos(0, 0, 0)));
            Assert.True(board.IsFree(new Pos(0, 0, 1)));
        }

        [Fact]
        public void HalbVersetzterSteinDarueber_Blockiert()
        {
            // Pyramidenspitzen stehen haeufig halb versetzt oben drauf.
            var board = Board(new Pos(0, 0, 0), new Pos(1, 1, 1));

            Assert.False(board.IsFree(new Pos(0, 0, 0)));
            Assert.True(board.IsFree(new Pos(1, 1, 1)));
        }

        [Fact]
        public void SteinZweiEbenenDarueber_BlockiertNicht()
        {
            var board = Board(new Pos(0, 0, 0), new Pos(0, 0, 2));

            Assert.True(board.IsFree(new Pos(0, 0, 0)));
        }

        // ---------------------------------------------------------------- Zug-Regeln

        [Fact]
        public void ZweiFreiGleicherTyp_CanRemove()
        {
            var board = Board(new Pos(0, 0, 0), new Pos(6, 0, 0));

            Assert.True(board.CanRemove(new Pos(0, 0, 0), new Pos(6, 0, 0)));
        }

        [Fact]
        public void ZweiFreiVerschiedenerTyp_CanRemoveNicht()
        {
            var board = new BoardState();
            board.AddTile(new Pos(0, 0, 0), Any);
            board.AddTile(new Pos(6, 0, 0), Other);

            Assert.False(board.CanRemove(new Pos(0, 0, 0), new Pos(6, 0, 0)));
        }

        [Fact]
        public void BlockierterStein_CanRemoveNicht()
        {
            // (2,0) ist von (0,0) und (4,0) eingeschlossen, obwohl der Typ zum Partner passt.
            var board = Board(new Pos(0, 0, 0), new Pos(2, 0, 0), new Pos(4, 0, 0), new Pos(6, 0, 0));

            Assert.False(board.CanRemove(new Pos(2, 0, 0), new Pos(6, 0, 0)));
        }

        [Fact]
        public void TryRemove_EntferntBeideSteine()
        {
            var board = Board(new Pos(0, 0, 0), new Pos(6, 0, 0), new Pos(10, 0, 0));

            Assert.True(board.TryRemove(new Pos(0, 0, 0), new Pos(6, 0, 0)));
            Assert.Equal(1, board.TileCount);
            Assert.False(board.HasTile(new Pos(0, 0, 0)));
            Assert.False(board.HasTile(new Pos(6, 0, 0)));
        }

        [Fact]
        public void TryRemove_Ungueltig_AendertNichts()
        {
            var board = new BoardState();
            board.AddTile(new Pos(0, 0, 0), Any);
            board.AddTile(new Pos(2, 0, 0), Other);

            Assert.False(board.TryRemove(new Pos(0, 0, 0), new Pos(2, 0, 0)));
            Assert.Equal(2, board.TileCount);
        }

        // ---------------------------------------------------------------- Solver

        [Fact]
        public void KeinPaar_FindAnyPairLiefertNull()
        {
            var board = new BoardState();
            board.AddTile(new Pos(0, 0, 0), Any);
            board.AddTile(new Pos(6, 0, 0), Other);

            Assert.Null(board.FindAnyPair());
            Assert.Equal(0, board.CountAvailablePairs());
        }

        [Fact]
        public void VorhandenesPaar_WirdGefunden()
        {
            var board = new BoardState();
            board.AddTile(new Pos(0, 0, 0), Other);
            board.AddTile(new Pos(6, 0, 0), Any);
            board.AddTile(new Pos(10, 0, 0), Any);

            var pair = board.FindAnyPair();

            Assert.NotNull(pair);
            Assert.Equal(1, board.CountAvailablePairs()); // nur Any+Any
        }

        [Fact]
        public void BlumenPaar_WirdUeberGruppenregelGefunden()
        {
            var board = new BoardState();
            board.AddTile(new Pos(0, 0, 0), new TileType(TileKind.Flower, 1));
            board.AddTile(new Pos(6, 0, 0), new TileType(TileKind.Flower, 4));

            Assert.NotNull(board.FindAnyPair());
        }

        [Fact]
        public void LeeresBoard_KeinPaar()
        {
            var board = new BoardState();

            Assert.Equal(0, board.TileCount);
            Assert.Null(board.FindAnyPair());
        }

        [Fact]
        public void IsFree_OhneStein_Wirft()
        {
            var board = Board();

            Assert.Throws<InvalidOperationException>(() => board.IsFree(new Pos(0, 0, 0)));
        }

        [Fact]
        public void AddTile_DoppeltePosition_Wirft()
        {
            var board = Board(new Pos(0, 0, 0));

            Assert.Throws<InvalidOperationException>(() => board.AddTile(new Pos(0, 0, 0), Any));
        }

        [Fact]
        public void Clone_IstUnabhaengig()
        {
            var board = Board(new Pos(0, 0, 0), new Pos(6, 0, 0));
            var clone = board.Clone();
            clone.TryRemove(new Pos(0, 0, 0), new Pos(6, 0, 0));

            Assert.Equal(2, board.TileCount);
            Assert.Equal(0, clone.TileCount);
        }
    }
}