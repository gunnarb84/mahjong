using Mahjong.Core;
using UnityEngine;

namespace Mahjong.Game
{
    /// <summary>
    /// Lokale Highscores in PlayerPrefs (WebGL: IndexedDB) — Eintraege nur
    /// fuer GEWONNENE Partien. Serialisierung ueber Mahjong.Core.HighscoreList
    /// (Logik + Tests liegen dort), hier nur Laden/Speichern.
    /// </summary>
    public static class HighscoreStore
    {
        const string Pref = "Mahjong.Highscores";

        public static HighscoreList Load()
        {
            return HighscoreList.Deserialize(PlayerPrefs.GetString(Pref, ""));
        }

        /// <summary>Spielt einen Eintrag ein und persistiert die Liste. Rueckgabe: Platz oder 0.</summary>
        public static int Add(HighscoreEntry entry)
        {
            var list = Load();
            var placement = list.Add(entry);
            PlayerPrefs.SetString(Pref, list.Serialize());
            PlayerPrefs.Save();
            return placement;
        }
    }
}