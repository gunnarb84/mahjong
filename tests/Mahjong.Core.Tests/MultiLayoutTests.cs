using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mahjong.Core;
using Xunit;

namespace Mahjong.Core.Tests
{
    /// <summary>
    /// Deckt ALLE Layouts im layouts-Ordner ab: jedes muss gueltig, gerade in der
    /// Steinzahl und vom Generator loesbar sein — neue Layout-Dateien sind damit
    /// automatisch getestet.
    /// </summary>
    public class MultiLayoutTests
    {
        public static IEnumerable<object[]> AlleLayouts() =>
            Directory.GetFiles("layouts", "*.layout")
                .OrderBy(p => p)
                .Select(p => new object[] { p });

        [Theory]
        [MemberData(nameof(AlleLayouts))]
        public void JedesLayout_IstGueltigGeradeUndLoesbar(string path)
        {
            var layout = LayoutLoader.LoadFile(path);
            var count = layout.Positions.Count;

            Assert.True(count % 2 == 0, path + ": ungerade Steinzahl " + count);
            Assert.True(count >= 4, path + ": zu wenige Steine (" + count + ")");

            var generated = BoardGenerator.Generate(layout.Positions, seed: 1);

            Assert.Equal(count, generated.Board.TileCount);
            Assert.Equal(count / 2, generated.Solution.Count);
            Assert.True(generated.Board.CountAvailablePairs() > 0, path + ": Start-Deadlock");
        }
    }
}