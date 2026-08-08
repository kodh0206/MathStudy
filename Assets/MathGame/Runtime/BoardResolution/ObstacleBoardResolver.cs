using System;
using System.Collections.Generic;
using System.Linq;
using MathGame.Board;
using MathGame.Core.Random;
using DomainBoard = MathGame.Board.Board;

namespace MathGame.BoardResolution
{
    public sealed class ObstacleBoardResolver
    {
        private readonly IRandomSource random;
        public ObstacleBoardResolver(IRandomSource randomSource) { random = randomSource ?? throw new ArgumentNullException(nameof(randomSource)); }

        public ObstacleResolutionResult Resolve(ObstacleResolutionRequest request)
        {
            var failure = Validate(request);
            if (failure != ObstacleResolutionFailure.None) return ObstacleResolutionResult.Fail(failure);
            var source = request.Board;
            var selected = new List<RemovedNumberDelta>(); var collateral = new List<RemovedNumberDelta>();
            var removed = new HashSet<BoardPosition>();
            if (request.Mode != ObstacleResolutionMode.FeverEnd)
            {
                var origin = request.Mode == ObstacleResolutionMode.NormalAnswer ? RemovalOrigin.Normal : RemovalOrigin.Fever;
                foreach (var entry in request.Answer.Snapshot.Entries) { removed.Add(entry.Position); selected.Add(new RemovedNumberDelta(entry.Position, entry.Block, RemovedNumberCause.Selected, origin)); }
                if (request.Mode == ObstacleResolutionMode.FeverAnswer)
                    foreach (var position in source.EnumerateActivePositions().OrderBy(p => p.Row).ThenBy(p => p.Column))
                    { source.TryGetCell(position, out var cell); if (!cell.IsRemovableNumber || removed.Contains(position)) continue; if (request.Answer.Snapshot.Entries.Any(e => Distance(e.Position, position) == 1)) { removed.Add(position); collateral.Add(new RemovedNumberDelta(position, cell.Block.Value, RemovedNumberCause.FeverExpanded, RemovalOrigin.Fever)); } }
            }
            else BuildEndRemovals(request, collateral, removed);

            var damage = new List<ObstacleDamageDelta>(); var destroyed = new List<ObstacleDestroyedEvidence>(); var survivingBoxes = new HashSet<BoardPosition>();
            foreach (var position in source.EnumerateActivePositions())
            {
                source.TryGetCell(position, out var cell);
                if (cell.Dust.HasValue && removed.Contains(position)) { var d = cell.Dust.Value; damage.Add(new ObstacleDamageDelta(d.Id, ObstacleKind.Dust, position, 1, 1, 1, 0, Origin(request.Mode))); destroyed.Add(new ObstacleDestroyedEvidence(d.Id, ObstacleKind.Dust, position, Origin(request.Mode))); }
                if (!cell.Box.HasValue) continue;
                var qualifies = removed.Any(p => Distance(p, position) == 1);
                if (!qualifies) { survivingBoxes.Add(position); continue; }
                var box = cell.Box.Value; var potency = request.Mode == ObstacleResolutionMode.NormalAnswer ? 1 : 2; var applied = Math.Min(potency, box.CurrentHitPoints); var after = box.CurrentHitPoints - applied; var origin = potency == 2 ? ObstacleDamageOrigin.Fever : ObstacleDamageOrigin.Normal;
                damage.Add(new ObstacleDamageDelta(box.Id, ObstacleKind.Box, position, box.CurrentHitPoints, potency, applied, after, origin));
                if (after == 0) destroyed.Add(new ObstacleDestroyedEvidence(box.Id, ObstacleKind.Box, position, origin)); else survivingBoxes.Add(position);
            }

            var layout = BoardLayout.Create(source.Topology, survivingBoxes); var placements = new Dictionary<BoardPosition, NumberBlock>(); var moved = new List<MovedBlockDelta>(); var spawnPositions = new List<BoardPosition>();
            for (var column = 0; column < source.Topology.Width; column++)
            {
                var row = 0;
                while (row < source.Topology.Height)
                {
                    while (row < source.Topology.Height && (!source.IsActive(new BoardPosition(column, row)) || survivingBoxes.Contains(new BoardPosition(column, row)))) row++;
                    if (row >= source.Topology.Height) break;
                    var segment = new List<BoardPosition>();
                    while (row < source.Topology.Height) { var p = new BoardPosition(column, row); if (!source.IsActive(p) || survivingBoxes.Contains(p)) break; segment.Add(p); row++; }
                    var survivors = new List<KeyValuePair<BoardPosition, NumberBlock>>();
                    foreach (var p in segment) { source.TryGetCell(p, out var c); if (c.Block.HasValue && !removed.Contains(p)) survivors.Add(new KeyValuePair<BoardPosition, NumberBlock>(p, c.Block.Value)); }
                    for (var i = 0; i < survivors.Count; i++) { placements[segment[i]] = survivors[i].Value; if (survivors[i].Key != segment[i]) moved.Add(new MovedBlockDelta(survivors[i].Key, segment[i], survivors[i].Value)); }
                    for (var i = survivors.Count; i < segment.Count; i++) spawnPositions.Add(segment[i]);
                }
            }
            if (request.NextBlockIdValue > int.MaxValue - spawnPositions.Count) return ObstacleResolutionResult.Fail(ObstacleResolutionFailure.BlockIdRangeExhausted);
            var spawned = new List<SpawnedBlockDelta>(); var nextId = request.NextBlockIdValue;
            foreach (var p in spawnPositions) { var value = random.NextInt(request.RefillValues.MinimumValue, request.RefillValues.MaximumValue + 1); if (value < request.RefillValues.MinimumValue || value > request.RefillValues.MaximumValue) throw new InvalidOperationException("Random source contract violation."); var block = new NumberBlock(new BlockId(nextId++), value); placements[p] = block; spawned.Add(new SpawnedBlockDelta(p, block)); }
            var replacement = new DomainBoard(layout);
            foreach (var p in source.EnumerateActivePositions())
            {
                source.TryGetCell(p, out var old);
                if (survivingBoxes.Contains(p)) { var boxDelta = damage.FirstOrDefault(d => d.Kind == ObstacleKind.Box && d.Position == p); var hp = boxDelta.Id.IsValid ? boxDelta.HitPointsAfter : old.Box.Value.CurrentHitPoints; if (replacement.TryPlaceBox(p, new BoxState(old.Box.Value.Id, hp)) != BoardMutationResult.Succeeded) return ObstacleResolutionResult.Fail(ObstacleResolutionFailure.FinalBoardMutationRejected); continue; }
                if (!placements.TryGetValue(p, out var block) || replacement.TryPlaceBlock(p, block) != BoardMutationResult.Succeeded) return ObstacleResolutionResult.Fail(ObstacleResolutionFailure.FinalBoardMutationRejected);
                if (old.Dust.HasValue && !removed.Contains(p) && replacement.TryPlaceDust(p, old.Dust.Value) != BoardMutationResult.Succeeded) return ObstacleResolutionResult.Fail(ObstacleResolutionFailure.FinalBoardMutationRejected);
            }
            return new ObstacleResolutionResult(ObstacleResolutionFailure.None, replacement, request.Mode, request.SystemEffectId, nextId, selected, collateral, moved, spawned, damage, destroyed);
        }

        private void BuildEndRemovals(ObstacleResolutionRequest request, List<RemovedNumberDelta> result, HashSet<BoardPosition> removed)
        {
            var source = request.Board; var cause = request.Pattern == FeverEndPattern.RandomThree ? RemovedNumberCause.FeverEndRandom : request.Pattern == FeverEndPattern.Small ? RemovedNumberCause.FeverEndSmall : request.Pattern == FeverEndPattern.Center ? RemovedNumberCause.FeverEndCenter : RemovedNumberCause.FeverEndLarge;
            var eligible = source.EnumerateActivePositions().Where(p => { source.TryGetCell(p, out var c); return c.IsRemovableNumber; }).OrderBy(p => p.Row).ThenBy(p => p.Column).ToList();
            if (request.Pattern == FeverEndPattern.RandomThree) for (var i = 0; i < Math.Min(3, eligible.Count); i++) { var j = random.NextInt(i, eligible.Count); if (j < i || j >= eligible.Count) throw new InvalidOperationException("Random source contract violation."); var t = eligible[i]; eligible[i] = eligible[j]; eligible[j] = t; removed.Add(eligible[i]); }
            else if (request.Pattern != FeverEndPattern.None) { var radius = request.Pattern == FeverEndPattern.Small ? 1 : request.Pattern == FeverEndPattern.Center ? 2 : 3; foreach (var p in eligible) if (Distance(p, request.Center.Value) <= radius) removed.Add(p); }
            foreach (var p in removed.OrderBy(p => p.Row).ThenBy(p => p.Column)) { source.TryGetCell(p, out var c); result.Add(new RemovedNumberDelta(p, c.Block.Value, cause, RemovalOrigin.Fever)); }
        }
        private static ObstacleDamageOrigin Origin(ObstacleResolutionMode mode) => mode == ObstacleResolutionMode.NormalAnswer ? ObstacleDamageOrigin.Normal : ObstacleDamageOrigin.Fever;
        private static long Distance(BoardPosition a, BoardPosition b) => Math.Abs((long)a.Column - b.Column) + Math.Abs((long)a.Row - b.Row);
        private static ObstacleResolutionFailure Validate(ObstacleResolutionRequest request)
        {
            if (request == null) return ObstacleResolutionFailure.MissingRequest; if (request.Board == null) return ObstacleResolutionFailure.MissingBoard;
            if (request.Mode != ObstacleResolutionMode.FeverEnd && request.Answer == null) return ObstacleResolutionFailure.MissingAnswer; if (request.RefillValues == null) return ObstacleResolutionFailure.MissingRefillRange;
            if (!Enum.IsDefined(typeof(ObstacleResolutionMode), request.Mode)) return ObstacleResolutionFailure.InvalidMode;
            if (request.Mode == ObstacleResolutionMode.FeverEnd && !Enum.IsDefined(typeof(FeverEndPattern), request.Pattern)) return ObstacleResolutionFailure.InvalidMode;
            if (request.Mode == ObstacleResolutionMode.FeverEnd && !request.SystemEffectId.IsValid) return ObstacleResolutionFailure.InvalidSystemEffectId;
            var spatial = request.Pattern is FeverEndPattern.Small or FeverEndPattern.Center or FeverEndPattern.Large; if (request.Mode == ObstacleResolutionMode.FeverEnd && spatial && !request.Center.HasValue) return ObstacleResolutionFailure.MissingCenter; if (request.Mode == ObstacleResolutionMode.FeverEnd && !spatial && request.Center.HasValue) return ObstacleResolutionFailure.UnexpectedCenter;
            if (!request.RefillValues.IsValid) return ObstacleResolutionFailure.InvalidRefillRange; if (request.NextBlockIdValue <= 0) return ObstacleResolutionFailure.InvalidNextBlockId; if (!request.Board.ValidateStable().IsStable) return ObstacleResolutionFailure.InvalidLayeredBoard;
            if (request.Mode != ObstacleResolutionMode.FeverEnd) { if (!request.Answer.IsCorrect) return ObstacleResolutionFailure.AnswerNotCorrect; if (request.Answer.Snapshot == null || request.Answer.Snapshot.IsEmpty) return ObstacleResolutionFailure.EmptySelection; var seen = new HashSet<BoardPosition>(); foreach (var e in request.Answer.Snapshot.Entries) { if (!seen.Add(e.Position)) return ObstacleResolutionFailure.DuplicateSelection; if (request.Board.TryGetCell(e.Position, out var c) != CellLookupResult.Succeeded) return ObstacleResolutionFailure.SelectedPositionMissing; if (!c.IsSelectable || c.Block.Value != e.Block) return ObstacleResolutionFailure.SelectedBlockMismatch; } }
            if (spatial && (!request.Board.IsActive(request.Center.Value))) return ObstacleResolutionFailure.InvalidCenter;
            var max = 0; foreach (var p in request.Board.EnumerateActivePositions()) { request.Board.TryGetCell(p, out var c); if (c.Block.HasValue && c.Block.Value.Id.Value > max) max = c.Block.Value.Id.Value; } if (request.NextBlockIdValue <= max) return ObstacleResolutionFailure.NextBlockIdCollision;
            return ObstacleResolutionFailure.None;
        }
    }
}
