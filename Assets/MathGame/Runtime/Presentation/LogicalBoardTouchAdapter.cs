using System;
using System.Collections.Generic;
using MathGame.Board;
using UnityEngine;

namespace MathGame.Presentation.Unity
{
    public sealed class LogicalBoardTouchAdapter : MonoBehaviour
    {
        readonly HashSet<BoardPosition> visited = new HashSet<BoardPosition>();
        BoardPosition? last;
        public float CellSize = 1f;
        public int Width = 5;
        public int Height = 5;
        public event Action<BoardPosition> LogicalCellEntered;

        public void Begin(Vector2 localPosition)
        {
            visited.Clear(); last = null;
            Add(localPosition);
        }

        public void Drag(Vector2 localPosition)
        {
            if (!LogicalBoardTouch.TryHit(localPosition.x, localPosition.y, CellSize, Width, Height, out var hit)) return;
            if (last.HasValue)
                foreach (var position in LogicalBoardTouch.Interpolate(last.Value, hit)) EmitOnce(position);
            else EmitOnce(hit);
            last = hit;
        }

        public void End() { last = null; visited.Clear(); }
        void Add(Vector2 point) { if (LogicalBoardTouch.TryHit(point.x, point.y, CellSize, Width, Height, out var hit)) { EmitOnce(hit); last = hit; } }
        void EmitOnce(BoardPosition position) { if (visited.Add(position)) LogicalCellEntered?.Invoke(position); }
    }
}
