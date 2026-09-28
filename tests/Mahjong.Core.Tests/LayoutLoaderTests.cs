using System;
using System.IO;
using System.Linq;
using Mahjong.Core;
using Xunit;

namespace Mahjong.Core.Tests
{
    public class LayoutLoaderTests
    {
        private static string LayoutsDir =>
            Path.Combine(AppContext.BaseDirectory, "layouts");

        public static string TurtlePath => Path.Combine(LayoutsDir, "turtle.layout");

        [Fact]
        public void TurtleLayout_Hat144Steine()
        {
            var layout = LayoutLoader.LoadFile(TurtlePath);

            Assert.Equal(144, layout.Positions.Count);
        }

        [Fact]
        public void TurtleLayout_Hat5Ebenen()
        {
            var layout = LayoutLoader.LoadFile(TurtlePath);

            Assert.Equal(5, layout.Depth);
        }

        [Fact]
        public void TurtleLayout_EbenenBelegung()
        {
            // Klassisches Turtle: 87 / 36 / 16 / 4 / 1.
            var layout = LayoutLoader.LoadFile(TurtlePath);

            Assert.Equal(87, CountLayer(layout, 0));
            Assert.Equal(36, CountLayer(layout, 1));
            Assert.Equal(16, CountLayer(layout, 2));
            Assert.Equal(4, CountLayer(layout, 3));
            Assert.Equal(1, CountLayer(layout, 4));
        }

        [Fact]
        public void TurtleLayout_PositionenSindEindeutig()
        {
            var layout = LayoutLoader.LoadFile(TurtlePath);

            Assert.Equal(layout.Positions.Count, layout.Positions.Distinct().Count());
        }

        [Fact]
        public void KaputtesLayout_SteinOhneNachbar_Wirft()
        {
            var lines = new[]
            {
                "kmahjongg-layout-v1.1",
                "w8",
                "h2",
                "1.",
                "..",
            };

            Assert.Throws<FormatException>(() => LayoutLoader.Parse(lines));
        }

        [Fact]
        public void KleinesGueltigesLayout_WirdGeparst()
        {
            // 2x1 Steine nebeneinander, Ebene 0.
            var lines = new[]
            {
                "kmahjongg-layout-v1.1",
                "w8",
                "h2",
                "1212",
                "4343",
            };

            var layout = LayoutLoader.Parse(lines);

            Assert.Equal(2, layout.Positions.Count);
            Assert.Equal(new Pos(0, 0, 0), layout.Positions[0]);
            Assert.Equal(new Pos(2, 0, 0), layout.Positions[1]);
        }

        [Fact]
        public void MehrereEbenen_WerdenNachUntenIndiziert()
        {
            // Level 0 = unterste Ebene im File = Layer 0.
            var lines = new[]
            {
                "kmahjongg-layout-v1.1",
                "w8",
                "h2",
                "1212",  // Level 0
                "4343",
                "..12..",  // Level 1, halb versetzt
                "..43..",
            };

            var layout = LayoutLoader.Parse(lines);

            Assert.Equal(3, layout.Positions.Count);
            Assert.Contains(new Pos(0, 0, 0), layout.Positions);
            Assert.Contains(new Pos(2, 0, 0), layout.Positions);
            Assert.Contains(new Pos(2, 0, 1), layout.Positions);
            Assert.Equal(2, layout.Depth);
        }

        private static int CountLayer(LayoutData layout, int layer)
        {
            var count = 0;

            foreach (var p in layout.Positions)
            {
                if (p.Layer == layer)
                {
                    count++;
                }
            }

            return count;
        }
    }
}