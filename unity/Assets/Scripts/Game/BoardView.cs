using System.Collections.Generic;
using Mahjong.Core;
using UnityEngine;

namespace Mahjong.Game
{
    /// <summary>
    /// Baut die Steine des Boards auf: ein Wuerfel-Primitive pro Stein, Position
    /// aus der Viertel-Kachel-Koordinate, Gesicht aus der Atlas-Textur.
    /// </summary>
    public static class BoardView
    {
        /// <summary>
        /// Baut alle Steine eines generierten Boards und liefert die Bounding-Box
        /// des Boards (fuer Kamera-Setup). Die Offsets werden nach aussen gereicht,
        /// damit Undo entfernte Steine exakt an der urspruenglichen Stelle neu baut.
        /// </summary>
        public static Bounds Build(
            Transform parent,
            LayoutData layout,
            GeneratedBoard generated,
            Dictionary<Pos, TileView> views,
            float quarterSize,
            float tileThickness,
            out float offsetX,
            out float offsetZ)
        {
            return BuildFromBoard(parent, generated.Board, views, quarterSize, tileThickness, out offsetX, out offsetZ);
        }

        /// <summary>Baut die Steine eines beliebigen Boardzustands (auch Teilboards).</summary>
        public static Bounds BuildFromBoard(
            Transform parent,
            BoardState board,
            Dictionary<Pos, TileView> views,
            float quarterSize,
            float tileThickness,
            out float offsetX,
            out float offsetZ)
        {
            var boardGo = new GameObject("Board");
            boardGo.transform.SetParent(parent, false);

            ComputeOffsets(board, quarterSize, out offsetX, out offsetZ);

            var bounds = new Bounds();

            foreach (var kv in board.Tiles)
            {
                var view = TileView.Create(boardGo.transform, kv.Key, kv.Value, quarterSize, tileThickness, offsetX, offsetZ);
                views.Add(kv.Key, view);
                bounds.Encapsulate(view.transform.position);
            }

            // Bounding-Box um die tatsaechliche Steingroesse erweitern.
            bounds.Expand(new Vector3(1f, tileThickness, 1f));
            return bounds;
        }

        /// <summary>
        /// Welt-Offsets, die die Viertel-Kachel-Koordinaten ins Board-Zentrum schieben.
        /// Oeffentlich, damit Undo Einzelpaare an derselben Stelle wieder aufbauen kann.
        /// </summary>
        public static void ComputeOffsets(
            BoardState board,
            float quarterSize,
            out float offsetX,
            out float offsetZ)
        {
            var minWidth = int.MaxValue;
            var maxWidth = int.MinValue;
            var minHeight = int.MaxValue;
            var maxHeight = int.MinValue;

            foreach (var pos in board.Tiles)
            {
                if (pos.Key.X < minWidth) minWidth = pos.Key.X;
                if (pos.Key.X > maxWidth) maxWidth = pos.Key.X;
                if (pos.Key.Y < minHeight) minHeight = pos.Key.Y;
                if (pos.Key.Y > maxHeight) maxHeight = pos.Key.Y;
            }

            // Weltmittelpunkt des Boards (Steine belegen 2x2 Vierteil-Kacheln).
            offsetX = (minWidth + maxWidth + 2) * quarterSize * 0.5f;
            offsetZ = (minHeight + maxHeight + 2) * quarterSize * 0.5f;
        }
    }
}