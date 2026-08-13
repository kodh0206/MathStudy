using System;
using System.Collections.Generic;
using MathGame.Board;
using UnityEngine;

namespace MathGame.Presentation.Unity
{
    public sealed class LogicalBoardTouchAdapter : MonoBehaviour
    {
        readonly List<BoardPosition> path = new List<BoardPosition>();
        readonly HashSet<BoardPosition> selected = new HashSet<BoardPosition>();
        BoardPosition? lastHit;
        int activePointerId = int.MinValue;

        public float CellSize = 1f;
        public int Width = 5;
        public int Height = 5;
        public event Action<BoardPosition> LogicalCellEntered;
        public event Action Released;
        public event Action Cancelled;
        public event Action<IReadOnlyList<BoardPosition>> PathChanged;

        public bool Begin(int pointerId, Vector2 localPosition)
        {
            if (activePointerId != int.MinValue) return false;
            activePointerId = pointerId; path.Clear(); selected.Clear(); lastHit = null;
            AddHit(localPosition); return true;
        }
        public void Begin(Vector2 localPosition) => Begin(0, localPosition);

        public bool Drag(int pointerId, Vector2 localPosition)
        {
            if (pointerId != activePointerId) return false;
            if (!LogicalBoardTouch.TryHit(localPosition.x, localPosition.y, CellSize, Width, Height, out var hit)) return false;
            if (lastHit.HasValue) foreach (var position in LogicalBoardTouch.Interpolate(lastHit.Value, hit)) EmitGesture(position);
            else EmitGesture(hit);
            lastHit = hit; return true;
        }
        public void Drag(Vector2 localPosition) => Drag(0, localPosition);

        public bool Release(int pointerId)
        {
            if (pointerId != activePointerId) return false;
            Clear(); Released?.Invoke(); return true;
        }
        public void End() => Release(activePointerId);

        public bool Cancel(int pointerId)
        {
            if (pointerId != activePointerId) return false;
            Clear(); Cancelled?.Invoke(); return true;
        }

        void AddHit(Vector2 point)
        {
            if (!LogicalBoardTouch.TryHit(point.x, point.y, CellSize, Width, Height, out var hit)) return;
            EmitGesture(hit); lastHit = hit;
        }

        void EmitGesture(BoardPosition position)
        {
            if (path.Count >= 2 && path[path.Count - 2] == position)
            {
                selected.Remove(path[path.Count - 1]); path.RemoveAt(path.Count - 1);
                LogicalCellEntered?.Invoke(position); PathChanged?.Invoke(path.AsReadOnly()); return;
            }
            if (!selected.Add(position)) return;
            path.Add(position); LogicalCellEntered?.Invoke(position); PathChanged?.Invoke(path.AsReadOnly());
        }

        void Clear(){activePointerId=int.MinValue;lastHit=null;path.Clear();selected.Clear();PathChanged?.Invoke(path.AsReadOnly());}
        void OnDisable(){if(activePointerId!=int.MinValue)Cancel(activePointerId);}
    }
}
