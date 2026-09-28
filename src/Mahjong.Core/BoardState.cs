using System;
using System.Collections.Generic;

namespace Mahjong.Core
{
    /// <summary>
    /// Zustand des Boards: welche Positionen mit welchen Stein-Typen belegt sind.
    /// Reine Spiellogik ohne Engine-Abhaengigkeit — alle Regeln hier sind unit-getestet.
    ///
    /// Zustand des Boards: welche Positionen mit welchen Stein-Typen belegt sind.
    /// Reine Spiellogik ohne Engine-Abhaengigkeit — alle Regeln hier sind unit-getestet.
    ///
    /// Geometrie: Ein Stein belegt [X, X+2) x [Y, Y+2) Viertel-Kachel-Einheiten auf
    /// seiner Ebene. Ein Stein ist frei, wenn
    ///   1. kein Stein der Ebene darueber seine Grundflaeche ueberlappt, und
    ///   2. mindestens eine Seite (links ODER rechts) frei ist — der Stein kann
    ///      zur offenen Seite hinausgeschoben werden. Angrenzend heisst: buendig
    ///      (Abstand 2) oder halb versetzt (Abstand 1) mit Hoehen-Ueberlappung;
    ///      reine Eckberuehrung blockiert nicht.
    /// </summary>
    public sealed class BoardState
    {
        private readonly Dictionary<Pos, TileType> _tiles;

        public BoardState()
        {
            _tiles = new Dictionary<Pos, TileType>();
        }

        private BoardState(Dictionary<Pos, TileType> tiles)
        {
            _tiles = tiles;
        }

        /// <summary>Anzahl verbleibender Steine.</summary>
        public int TileCount => _tiles.Count;

        /// <summary>Alle belegten Positionen mit Typ.</summary>
        public IEnumerable<KeyValuePair<Pos, TileType>> Tiles => _tiles;

        public bool HasTile(Pos p) => _tiles.ContainsKey(p);

        public TileType GetTile(Pos p) => _tiles[p];

        /// <summary>Setzt einen Stein. Fuer Board-Aufbau und Tests.</summary>
        public void AddTile(Pos p, TileType type)
        {
            if (_tiles.ContainsKey(p))
            {
                throw new InvalidOperationException("Position bereits belegt: " + p);
            }

            _tiles[p] = type;
        }

        /// <summary>Entfernt einen Stein ohne Regelpruefung. Fuer Board-Aufbau/Undo.</summary>
        public void RemoveForced(Pos p) => _tiles.Remove(p);

        /// <summary>Unabhaengige Kopie dieses Boards.</summary>
        public BoardState Clone() => new BoardState(new Dictionary<Pos, TileType>(_tiles));

        // ---------------------------------------------------------------- Serialisierung

        /// <summary>
        /// Kompakte Serialisierung fuer Spielstand-Speicherung (z. B. PlayerPrefs):
        /// "x,y,ebene:typ,zahl;..." — Typ ist (int)TileKind, Zahl die Stein-Nummer.
        /// </summary>
        public string Serialize()
        {
            var sb = new System.Text.StringBuilder();
            var first = true;

            foreach (var kv in _tiles)
            {
                if (!first)
                {
                    sb.Append(';');
                }

                first = false;
                sb.Append(kv.Key.X).Append(',').Append(kv.Key.Y).Append(',').Append(kv.Key.Layer);
                sb.Append(':').Append((int)kv.Value.Kind).Append(',').Append(kv.Value.Number);
            }

            return sb.ToString();
        }

        /// <summary>Gegenteil von <see cref="Serialize"/>; wirft FormatException bei Mist.</summary>
        public static BoardState Deserialize(string text)
        {
            var board = new BoardState();

            if (string.IsNullOrEmpty(text))
            {
                return board;
            }

            foreach (var entry in text.Split(';'))
            {
                if (entry.Length == 0)
                {
                    continue;
                }

                var parts = entry.Split(':');

                if (parts.Length != 2)
                {
                    throw new FormatException("Ungueltiger Eintrag: " + entry);
                }

                var pos = parts[0].Split(',');
                var typ = parts[1].Split(',');

                if (pos.Length != 3 || typ.Length != 2)
                {
                    throw new FormatException("Ungueltiger Eintrag: " + entry);
                }

                board.AddTile(
                    new Pos(int.Parse(pos[0]), int.Parse(pos[1]), int.Parse(pos[2])),
                    new TileType((TileKind)int.Parse(typ[0]), int.Parse(typ[1])));
            }

            return board;
        }

        // ---------------------------------------------------------------- Freisteh-Regel

        private static bool OverlapsX(Pos a, Pos b) => b.X < a.X + 2 && b.X + 2 > a.X;

        private static bool OverlapsY(Pos a, Pos b) => b.Y < a.Y + 2 && b.Y + 2 > a.Y;

        /// <summary>Eine Ebene darueber und Grundflaeche ueberlappt → verdeckt.</summary>
        private bool IsCovered(Pos p)
        {
            foreach (var kv in _tiles)
            {
                if (kv.Key.Layer != p.Layer + 1)
                {
                    continue;
                }

                if (OverlapsX(p, kv.Key) && OverlapsY(p, kv.Key))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Links blockiert: Stein derselben Ebene linker Hand mit Hoehen-Ueberlappung.
        /// Buendig (X-Abstand 2) oder halb versetzt (X-Abstand 1) zaehlen,
        /// reine Eckberuehrung (Y-Abstand 2) nicht.
        /// </summary>
        private bool IsBlockedOnSide(Pos p, bool left)
        {
            foreach (var kv in _tiles)
            {
                var q = kv.Key;

                if (q.Layer != p.Layer || q == p)
                {
                    continue;
                }

                var dx = q.X - p.X;

                if (left)
                {
                    // muss links liegen: bündig (-2) oder halb versetzt (-1)
                    if (dx >= 0 || dx < -2)
                    {
                        continue;
                    }
                }
                else
                {
                    // muss rechts liegen: buendig (+2) oder halb versetzt (+1)
                    if (dx <= 0 || dx > 2)
                    {
                        continue;
                    }
                }

                if (OverlapsY(p, q))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Ist dieser Stein aktuell entnehmbar?</summary>
        public bool IsFree(Pos p)
        {
            if (!HasTile(p))
            {
                throw new InvalidOperationException("Kein Stein an Position " + p);
            }

            return !IsCovered(p)
                && (!IsBlockedOnSide(p, left: true) || !IsBlockedOnSide(p, left: false));
        }

        // ---------------------------------------------------------------- Zug-Regeln

        /// <summary>Koennen diese beiden Steine (jetzt) als Paar entfernt werden?</summary>
        public bool CanRemove(Pos a, Pos b)
        {
            if (a == b || !HasTile(a) || !HasTile(b))
            {
                return false;
            }

            if (!IsFree(a) || !IsFree(b))
            {
                return false;
            }

            return TileType.CanPair(GetTile(a), GetTile(b));
        }

        /// <summary>
        /// Entfernt ein Paar. Liefert false, wenn der Zug ungueltig ist
        /// (Regeln verletzt) — dann wird nichts veraendert.
        /// </summary>
        public bool TryRemove(Pos a, Pos b)
        {
            if (!CanRemove(a, b))
            {
                return false;
            }

            _tiles.Remove(a);
            _tiles.Remove(b);
            return true;
        }

        // ---------------------------------------------------------------- Solver / Hinweise

        /// <summary>
        /// Mischt die Stein-Typen auf den aktuellen Positionen neu (Positionen bleiben
        /// erhalten). Nimmt die erste Anordnung mit mindestens einem moeglichen Zug.
        /// Liefert false, wenn auch nach maxAttempts Versuchen kein Zug moeglich wird —
        /// das heisst bei >= 2 verbleibenden Steinen, dass diese nicht mehr paaren koennen.
        /// </summary>
        public bool ShuffleTypes(Random random, int maxAttempts = 500)
        {
            if (_tiles.Count < 2)
            {
                return false;
            }

            var original = new Dictionary<Pos, TileType>(_tiles);
            var positions = new List<Pos>(_tiles.Keys);
            var types = new List<TileType>();

            foreach (var p in positions)
            {
                types.Add(_tiles[p]);
            }

            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                Shuffle(types, random);
                _tiles.Clear();

                for (var i = 0; i < positions.Count; i++)
                {
                    _tiles[positions[i]] = types[i];
                }

                if (CountAvailablePairs() > 0)
                {
                    return true;
                }
            }

            // Kein gueltiger Mix gefunden: urspruengliche Belegung wiederherstellen.
            _tiles.Clear();

            foreach (var kv in original)
            {
                _tiles[kv.Key] = kv.Value;
            }

            return false;
        }

        /// <summary>Alle aktuell freien Positionen.</summary>
        public List<Pos> FreeTiles()
        {
            var result = new List<Pos>();

            foreach (var kv in _tiles)
            {
                if (IsFree(kv.Key))
                {
                    result.Add(kv.Key);
                }
            }

            return result;
        }

        /// <summary>
        /// Findet irgendein currently entfernbares Paar. Mit <paramref name="random"/>
        /// zufaellig unter den moeglichen; ohne stabil (erste Fundstelle).
        /// Null, wenn kein Zug mehr moeglich ist (Deadlock).
        /// </summary>
        public (Pos A, Pos B)? FindAnyPair(Random? random = null)
        {
            var free = FreeTiles();

            // Zufaellige Reihenfolge, damit Hints variieren.
            if (random != null)
            {
                Shuffle(free, random);
            }

            for (var i = 0; i < free.Count; i++)
            {
                for (var j = i + 1; j < free.Count; j++)
                {
                    if (TileType.CanPair(GetTile(free[i]), GetTile(free[j])))
                    {
                        return (free[i], free[j]);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Anzahl momentan moeglicher Paar-Zuege. 0 bedeutet: kein Zug mehr moeglich.
        /// (Blumen/Jahreszeiten paaren gruppenweise — das zaehlt CanPair hier korrekt mit.)
        /// </summary>
        public int CountAvailablePairs()
        {
            var free = FreeTiles();
            var count = 0;

            for (var i = 0; i < free.Count; i++)
            {
                for (var j = i + 1; j < free.Count; j++)
                {
                    if (TileType.CanPair(GetTile(free[i]), GetTile(free[j])))
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static void Shuffle<T>(List<T> list, Random random)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                var tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }
    }
}