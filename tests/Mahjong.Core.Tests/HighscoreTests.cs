using Mahjong.Core;
using Xunit;

namespace Mahjong.Core.Tests
{
    /// <summary>
    /// Highscore-Liste: Sortierung (Punkte absteigend, Zeit aufsteigend), Top-10-Trim,
    /// Platz-Rueckgabe, Serialisierungsrundgang und Robustheit gegen Defekte.
    /// </summary>
    public class HighscoreTests
    {
        static HighscoreEntry E(int score, int seconds = 600, string layout = "Schildkröte", string date = "2026-09-30")
        {
            return new HighscoreEntry(score, seconds, layout, date);
        }

        [Fact]
        public void Add_SortedByScoreDescending()
        {
            var list = new HighscoreList();
            list.Add(E(1000));
            list.Add(E(1700));
            list.Add(E(1400));

            Assert.Equal(1700, list.Entries[0].Score);
            Assert.Equal(1400, list.Entries[1].Score);
            Assert.Equal(1000, list.Entries[2].Score);
        }

        [Fact]
        public void Add_ScoreTie_FasterTimeWins()
        {
            var list = new HighscoreList();
            list.Add(E(1500, seconds: 700));
            list.Add(E(1500, seconds: 300));

            Assert.Equal(300, list.Entries[0].Seconds);
            Assert.Equal(700, list.Entries[1].Seconds);
        }

        [Fact]
        public void Add_ReturnsPlacement()
        {
            var list = new HighscoreList();
            Assert.Equal(1, list.Add(E(1000)));
            Assert.Equal(1, list.Add(E(1200)));
            Assert.Equal(2, list.Add(E(1100)));
            Assert.Equal(4, list.Add(E(900)));
        }

        [Fact]
        public void Add_TrimsToCapacity_WorstEntriesDisappear()
        {
            var list = new HighscoreList();
            for (var i = 1; i <= 15; i++)
            {
                list.Add(E(i * 100));
            }

            Assert.Equal(HighscoreList.Capacity, list.Entries.Count);
            Assert.Equal(1500, list.Entries[0].Score);
            Assert.Equal(600, list.Entries[9].Score); // unterste Grenze

            // 500 waere abgeschlagen
            Assert.Equal(0, list.Add(E(500)));
            Assert.Equal(HighscoreList.Capacity, list.Entries.Count);
            Assert.Equal(1500, list.Entries[0].Score);
        }

        [Fact]
        public void SerializeDeserialize_RoundTrip()
        {
            var list = new HighscoreList();
            list.Add(E(1700, 413, "Drache", "2026-09-29"));
            list.Add(E(900, 800, "Schildkröte", "2026-09-30"));

            var restored = HighscoreList.Deserialize(list.Serialize());

            Assert.Equal(2, restored.Entries.Count);
            Assert.Equal(1700, restored.Entries[0].Score);
            Assert.Equal(413, restored.Entries[0].Seconds);
            Assert.Equal("Drache", restored.Entries[0].Layout);
            Assert.Equal("2026-09-29", restored.Entries[0].Date);
            Assert.Equal(900, restored.Entries[1].Score);
        }

        [Fact]
        public void Deserialize_EmptyOrNullAndBrokenEntries_NoCrash()
        {
            Assert.Empty(HighscoreList.Deserialize(null).Entries);
            Assert.Empty(HighscoreList.Deserialize("").Entries);
            Assert.Empty(HighscoreList.Deserialize("kaputt").Entries);
            Assert.Empty(HighscoreList.Deserialize("x|1|A;;nicht|numerisch|B").Entries);
        }

        [Fact]
        public void Deserialize_SkipsBrokenEntries_KeepsGood()
        {
            var restored = HighscoreList.Deserialize(";100|55|Katze|2026-01-01;");

            Assert.Single(restored.Entries);
            Assert.Equal(100, restored.Entries[0].Score);
        }
    }
}