using System.Linq;
using Mahjong.Core;
using Xunit;

namespace Mahjong.Core.Tests
{
    public class SerializationTests
    {
        [Fact]
        public void Roundtrip_ErhaeltAlleSteine()
        {
            var board = new BoardState();
            board.AddTile(new Pos(0, 0, 0), new TileType(TileKind.Dot, 1));
            board.AddTile(new Pos(2, 1, 1), new TileType(TileKind.Dragon, 2));
            board.AddTile(new Pos(4, 0, 0), new TileType(TileKind.Season, 3));

            var restored = BoardState.Deserialize(board.Serialize());

            Assert.Equal(board.TileCount, restored.TileCount);

            foreach (var kv in board.Tiles)
            {
                Assert.Equal(kv.Value, restored.GetTile(kv.Key));
            }
        }

        [Fact]
        public void Roundtrip_GeneriertesBoard_UndFreistehRegelBleibt()
        {
            var layout = LayoutLoader.LoadFile("layouts/turtle.layout");
            var generated = BoardGenerator.Generate(layout.Positions, seed: 42);

            var restored = BoardState.Deserialize(generated.Board.Serialize());

            Assert.Equal(generated.Board.TileCount, restored.TileCount);
            Assert.Equal(generated.Board.CountAvailablePairs(), restored.CountAvailablePairs());
        }

        [Fact]
        public void LeererString_LiefertLeeresBoard()
        {
            Assert.Equal(0, BoardState.Deserialize("").TileCount);
        }

        [Fact]
        public void UngueltigerEintrag_Wirft()
        {
            Assert.Throws<System.FormatException>(() => BoardState.Deserialize("0,0,0"));
            Assert.Throws<System.FormatException>(() => BoardState.Deserialize("0,0:0,1"));
        }
    }
}