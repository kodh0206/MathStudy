using System;
using System.Collections.Generic;
using MathGame.Board;
using MathGame.BoardResolution;
using MathGame.Restoration;
using System.Numerics;

namespace MathGame.Presentation
{
    public static class FeverAreaCenterSelector
    {
        public static bool TrySelect(BoardTopology topology, IReadOnlyList<RemovedNumberDelta> footprint, out BoardPosition center)
        {
            center = default;
            if (topology == null || footprint == null || footprint.Count == 0) return false;
            long columns = 0, rows = 0;
            for (var i = 0; i < footprint.Count; i++) { columns += footprint[i].Position.Column; rows += footprint[i].Position.Row; }
            BigInteger bestDistance = default;
            var found = false;
            foreach (var position in topology.EnumerateActivePositions())
            {
                var dc = (BigInteger)position.Column * footprint.Count - columns;
                var dr = (BigInteger)position.Row * footprint.Count - rows;
                var distance = dc * dc + dr * dr;
                if (!found || distance < bestDistance)
                { found = true; bestDistance = distance; center = position; }
            }
            return found;
        }
    }

    public static class LogicalBoardTouch
    {
        public const double HitRadiusFactor = 0.45d;

        public static bool TryHit(double localX, double localY, double cellSize, int width, int height, out BoardPosition position)
        {
            position = default;
            if (!(cellSize > 0) || width <= 0 || height <= 0 || double.IsNaN(localX) || double.IsNaN(localY)) return false;
            var column = (int)Math.Round(localX / cellSize, MidpointRounding.AwayFromZero);
            var row = (int)Math.Round(localY / cellSize, MidpointRounding.AwayFromZero);
            if (column < 0 || column >= width || row < 0 || row >= height) return false;
            var dx = localX - column * cellSize;
            var dy = localY - row * cellSize;
            if (dx * dx + dy * dy > cellSize * cellSize * HitRadiusFactor * HitRadiusFactor) return false;
            position = new BoardPosition(column, row);
            return true;
        }

        public static IReadOnlyList<BoardPosition> Interpolate(BoardPosition from, BoardPosition to)
        {
            var output = new List<BoardPosition>();
            var x = from.Column; var y = from.Row;
            var dx = Math.Abs(to.Column - x); var sx = x < to.Column ? 1 : -1;
            var dy = -Math.Abs(to.Row - y); var sy = y < to.Row ? 1 : -1;
            var error = dx + dy;
            while (x != to.Column || y != to.Row)
            {
                var twice = 2 * error;
                if (twice >= dy) { error += dy; x += sx; output.Add(new BoardPosition(x, y)); }
                if ((x != to.Column || y != to.Row) && twice <= dx) { error += dx; y += sy; output.Add(new BoardPosition(x, y)); }
            }
            return output.AsReadOnly();
        }
    }

    public sealed class ExactlyOnceMilestoneTracker
    {
        readonly HashSet<string> shown = new HashSet<string>();
        public bool TryMark(WorldRestorationCommitResult result, MathGame.Restoration.Contracts.WorldRestorationMilestone milestone)
        {
            if (result?.Plan == null) return false;
            var key = result.Plan.WorldId.Value + ":" + result.Plan.CommitId.Value + ":" + (int)milestone;
            return shown.Add(key);
        }
    }
}
