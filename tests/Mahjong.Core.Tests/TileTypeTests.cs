using Mahjong.Core;
using Xunit;

namespace Mahjong.Core.Tests
{
    public class TileTypeTests
    {
        [Fact]
        public void GleicherTyp_Paart()
        {
            Assert.True(TileType.CanPair(new TileType(TileKind.Dot, 3), new TileType(TileKind.Dot, 3)));
            Assert.True(TileType.CanPair(new TileType(TileKind.Wind, 2), new TileType(TileKind.Wind, 2)));
        }

        [Fact]
        public void VerschiedeneRanks_PaarenNicht()
        {
            Assert.False(TileType.CanPair(new TileType(TileKind.Dot, 3), new TileType(TileKind.Dot, 4)));
            Assert.False(TileType.CanPair(new TileType(TileKind.Dot, 3), new TileType(TileKind.Bamboo, 3)));
            Assert.False(TileType.CanPair(new TileType(TileKind.Wind, 1), new TileType(TileKind.Wind, 2)));
            Assert.False(TileType.CanPair(new TileType(TileKind.Dragon, 1), new TileType(TileKind.Dragon, 2)));
        }

        [Fact]
        public void Blumen_PaarenGruppenweise()
        {
            Assert.True(TileType.CanPair(new TileType(TileKind.Flower, 1), new TileType(TileKind.Flower, 2)));
            Assert.True(TileType.CanPair(new TileType(TileKind.Flower, 4), new TileType(TileKind.Flower, 1)));
        }

        [Fact]
        public void Jahreszeiten_PaarenGruppenweise()
        {
            Assert.True(TileType.CanPair(new TileType(TileKind.Season, 3), new TileType(TileKind.Season, 1)));
        }

        [Fact]
        public void BlumeMitJahreszeit_PaartNicht()
        {
            Assert.False(TileType.CanPair(new TileType(TileKind.Flower, 1), new TileType(TileKind.Season, 2)));
        }

        [Fact]
        public void Ids_SindStabil()
        {
            Assert.Equal("dot-1", new TileType(TileKind.Dot, 1).Id);
            Assert.Equal("bam-9", new TileType(TileKind.Bamboo, 9).Id);
            Assert.Equal("char-5", new TileType(TileKind.Character, 5).Id);
            Assert.Equal("wind-E", new TileType(TileKind.Wind, 1).Id);
            Assert.Equal("dragon-green", new TileType(TileKind.Dragon, 2).Id);
            Assert.Equal("flower-3", new TileType(TileKind.Flower, 3).Id);
        }
    }
}