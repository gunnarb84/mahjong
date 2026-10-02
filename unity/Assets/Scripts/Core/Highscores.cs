namespace Mahjong.Core
{
    /// <summary>
    /// Ein gewonnener Lauf: Punktzahl, Dauer (Sekunden), Layout-Anzeige und Datum
    /// (ISO, z. B. "2026-09-30"). Reine Datenklasse — Erzeugung passiert im Unity-Layer.
    /// </summary>
    public struct HighscoreEntry
    {
        public int Score;
        public int Seconds;
        public string Layout;
        public string Date;

        public HighscoreEntry(int score, int seconds, string layout, string date)
        {
            Score = score;
            Seconds = seconds;
            Layout = layout;
            Date = date;
        }
    }

    /// <summary>
    /// Lokale Highscore-Liste (Top N, N = 10): gewonnenen Partien, sortiert nach
    /// Punkten absteigend, bei Gleichstand nach Zeit aufsteigend. Serialisierung
    /// als ein String (Eintraege mit ';' getrennt, Felder mit '|') — der Unity-Layer
    /// legt ihn in PlayerPrefs ab; nichts hier haengt an UnityEngine.
    /// </summary>
    public class HighscoreList
    {
        public const int Capacity = 10;

        const char EntrySeparator = ';';
        const char FieldSeparator = '|';

        readonly System.Collections.Generic.List<HighscoreEntry> _entries =
            new System.Collections.Generic.List<HighscoreEntry>();

        /// <summary>Aktuelle Eintraege, bereits sortiert (1. = bester).</summary>
        public System.Collections.Generic.IReadOnlyList<HighscoreEntry> Entries => _entries;

        /// <summary>
        /// Fuegt einen Eintrag ein und trimmt auf Capacity. Rueckgabe: 1-basierter
        /// Platz (so wie der Eintrag in der Liste landete) oder 0, wenn er es
        /// nicht unter die besten N geschafft hat.
        /// </summary>
        public int Add(HighscoreEntry entry)
        {
            _entries.Add(entry);
            _entries.Sort(Compare);
            if (_entries.Count > Capacity)
            {
                _entries.RemoveRange(Capacity, _entries.Count - Capacity);
            }

            if (!_entries.Contains(entry))
            {
                return 0;
            }

            return _entries.IndexOf(entry) + 1;
        }

        static int Compare(HighscoreEntry a, HighscoreEntry b)
        {
            // Punkte absteigend; Gleichstand: kuerzere Zeit zuerst; dann Datum.
            if (a.Score != b.Score)
            {
                return b.Score - a.Score;
            }

            if (a.Seconds != b.Seconds)
            {
                return a.Seconds - b.Seconds;
            }

            return string.CompareOrdinal(a.Date ?? "", b.Date ?? "");
        }

        public string Serialize()
        {
            var fields = new string[_entries.Count][];
            for (var i = 0; i < _entries.Count; i++)
            {
                fields[i] = new[]
                {
                    _entries[i].Score.ToString(),
                    _entries[i].Seconds.ToString(),
                    _entries[i].Layout ?? "",
                    _entries[i].Date ?? "",
                };
            }

            // Eintraege mit ';' verketten, Felder mit '|'.
            var result = "";
            for (var i = 0; i < fields.Length; i++)
            {
                if (i > 0)
                {
                    result += EntrySeparator;
                }
                result += string.Join(FieldSeparator.ToString(), fields[i]);
            }

            return result;
        }

        /// <summary>Liest Serialisierung ein; defekte Eintraege werden uebersprungen.</summary>
        public static HighscoreList Deserialize(string serialized)
        {
            var list = new HighscoreList();

            if (string.IsNullOrEmpty(serialized))
            {
                return list;
            }

            foreach (var raw in serialized.Split(EntrySeparator))
            {
                var parts = raw.Split(FieldSeparator);
                if (parts.Length < 3)
                {
                    continue;
                }

                int score;
                int seconds;
                if (!int.TryParse(parts[0], out score) || !int.TryParse(parts[1], out seconds))
                {
                    continue;
                }

                list._entries.Add(new HighscoreEntry(
                    score, seconds,
                    parts[2],
                    parts.Length > 3 ? parts[3] : ""));
            }

            list._entries.Sort(Compare);
            return list;
        }
    }
}