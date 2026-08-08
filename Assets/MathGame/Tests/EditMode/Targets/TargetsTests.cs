using System;
using System.Collections.Generic;
using System.Linq;
using MathGame.Answer;
using MathGame.Board;
using MathGame.Core.Random;
using MathGame.Targets;
using NUnit.Framework;
using DomainBoard = MathGame.Board.Board;

namespace MathGame.Tests.Targets
{
    public sealed class TargetsTests
    {
        [Test]
        public void SearchReturnsSortedDistinctCanonicalWitnesses()
        {
            var board = Board(2, 3, 4);
            var result = new TargetPathSearcher().Search(board, new TargetSearchConfig(5, 7, 2, 2, 100));

            Assert.That(result.Status, Is.EqualTo(TargetSearchStatus.Succeeded));
            Assert.That(result.Solutions.Select(solution => solution.Target.Value), Is.EqualTo(new[] { 5, 7 }));
            Assert.That(result.Solutions[0].Steps.Select(step => step.Position), Is.EqualTo(new[]
            {
                new BoardPosition(0, 0), new BoardPosition(1, 0)
            }));
            Assert.That(result.Solutions[0].Validate(board), Is.EqualTo(TargetSolutionValidation.Valid));
            Assert.That(result.NodeExpansions, Is.GreaterThanOrEqualTo(3));
        }

        [Test]
        public void ExactSearchLimitDiscardsPartialCandidates()
        {
            var board = Board(2, 3, 4);
            var result = new TargetPathSearcher().Search(board, new TargetSearchConfig(5, 9, 2, 3, 1));
            Assert.That(result.Status, Is.EqualTo(TargetSearchStatus.SearchLimitExceeded));
            Assert.That(result.NodeExpansions, Is.EqualTo(1));
            Assert.That(result.Solutions, Is.Empty);
        }

        [Test]
        public void MissingInvalidAndUnsupportedSearchPrecedenceIsStable()
        {
            var searcher = new TargetPathSearcher();
            Assert.That(searcher.Search(null, null).Status, Is.EqualTo(TargetSearchStatus.MissingBoard));
            var board = Board(1, 2);
            Assert.That(searcher.Search(board, null).Status, Is.EqualTo(TargetSearchStatus.InvalidConfiguration));
            board.TrySetAccess(default, CellAccess.Blocked);
            Assert.That(searcher.Search(board, new TargetSearchConfig(1, 9, 2, 2, 10)).Status,
                Is.EqualTo(TargetSearchStatus.UnsupportedBoardState));
        }

        [Test]
        public void WitnessRejectsEquivalentReplacementAndStaleIdentity()
        {
            var board = Board(2, 3);
            var solution = new TargetPathSearcher().Search(board, new TargetSearchConfig(5, 5, 2, 2, 20)).Solutions[0];
            Assert.That(solution.Validate(Board(2, 3)), Is.EqualTo(TargetSolutionValidation.DifferentBoard));
            board.TryRemoveBlock(default, out _);
            board.TryPlaceBlock(default, new NumberBlock(new BlockId(3), 2));
            Assert.That(solution.Validate(board), Is.EqualTo(TargetSolutionValidation.BlockMismatch));
        }

        [Test]
        public void SelectorUsesOneExactDrawAndHonorsRepetitionCap()
        {
            var search = new TargetPathSearcher().Search(Board(2, 3, 4), new TargetSearchConfig(5, 7, 2, 2, 100));
            var random = new ScriptedRandom(0);
            var selection = new SafeTargetSelector(random).Select(
                search, new TargetSelectionPolicy(1), new TargetHistory(new TargetNumber(5), 1));

            Assert.That(selection.Status, Is.EqualTo(TargetSelectionStatus.Succeeded));
            Assert.That(selection.SelectedSolution.Target.Value, Is.EqualTo(7));
            Assert.That(selection.UpdatedHistory.ConsecutiveCount, Is.EqualTo(1));
            Assert.That(random.Calls, Is.EqualTo(new[] { (0, 1) }));
        }

        [Test]
        public void ShuffleUsesFisherYatesAndPreservesSource()
        {
            var source = Board(1, 2, 3);
            var random = new ScriptedRandom(0, 0);
            var result = new BoardShuffler(random).Shuffle(source);

            Assert.That(result.Status, Is.EqualTo(BoardShuffleStatus.Succeeded));
            Assert.That(random.Calls, Is.EqualTo(new[] { (0, 3), (0, 2) }));
            Assert.That(result.Board.Topology, Is.SameAs(source.Topology));
            Assert.That(result.Deltas, Has.Count.EqualTo(3));
            AssertId(source, 0, 1);
            AssertId(result.Board, 0, 2);
        }

        [Test]
        public void CurrentValidRecoveryUsesNoRandomnessAndCostsNoMove()
        {
            var board = Board(2, 3);
            var random = new ScriptedRandom();
            var coordinator = new TargetRecoveryCoordinator(random);
            var result = coordinator.RecoverCurrentTarget(
                board,
                new TargetNumber(5),
                new TargetHistory(new TargetNumber(5), 1),
                new TargetRecoveryConfig(new TargetSearchConfig(5, 5, 2, 2, 20), new TargetSelectionPolicy(2), 2));

            Assert.That(result.Status, Is.EqualTo(TargetRecoveryStatus.CurrentTargetStillValid));
            Assert.That(result.Board, Is.SameAs(board));
            Assert.That(result.Solution.Validate(board), Is.EqualTo(TargetSolutionValidation.Valid));
            Assert.That(result.MoveCost, Is.Zero);
            Assert.That(random.Calls, Is.Empty);
        }

        [Test]
        public void SelectorSoleCandidateUsesMarkedFallbackAndOneDraw()
        {
            var search = new TargetPathSearcher().Search(Board(2, 3), new TargetSearchConfig(5, 5, 2, 2, 20));
            var random = new ScriptedRandom(0);
            var result = new SafeTargetSelector(random).Select(
                search,
                new TargetSelectionPolicy(1),
                new TargetHistory(new TargetNumber(5), 1));

            Assert.That(result.Status, Is.EqualTo(TargetSelectionStatus.Succeeded));
            Assert.That(result.UsedRepetitionFallback, Is.True);
            Assert.That(result.UpdatedHistory.ConsecutiveCount, Is.EqualTo(2));
            Assert.That(random.Calls, Is.EqualTo(new[] { (0, 1) }));
        }

        [Test]
        public void SelectorValidationFailuresConsumeNoRandomness()
        {
            var random = new ScriptedRandom();
            var selector = new SafeTargetSelector(random);
            Assert.That(selector.Select(null, null, null).Status, Is.EqualTo(TargetSelectionStatus.MissingSearchResult));
            var unsuccessful = new TargetPathSearcher().Search(Board(1, 1), new TargetSearchConfig(9, 9, 2, 2, 20));
            Assert.That(selector.Select(unsuccessful, new TargetSelectionPolicy(1), new TargetHistory(null, 0)).Status,
                Is.EqualTo(TargetSelectionStatus.SearchNotSuccessful));
            Assert.That(random.Calls, Is.Empty);
        }

        [Test]
        public void IdentityShuffleSucceedsWithNoDeltasAndExactCalls()
        {
            var source = Board(1, 2, 3);
            var random = new ScriptedRandom(2, 1);
            var result = new BoardShuffler(random).Shuffle(source);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Deltas, Is.Empty);
            Assert.That(random.Calls, Is.EqualTo(new[] { (0, 3), (0, 2) }));
            AssertId(result.Board, 0, 1);
        }

        [Test]
        public void ShuffleRejectsInsufficientAndUnsupportedBoardsWithoutDraws()
        {
            var random = new ScriptedRandom();
            var shuffler = new BoardShuffler(random);
            Assert.That(shuffler.Shuffle(Board(1)).Status, Is.EqualTo(BoardShuffleStatus.InsufficientMovableBlocks));
            var blocked = Board(1, 2);
            blocked.TrySetAccess(default, CellAccess.Blocked);
            Assert.That(shuffler.Shuffle(blocked).Status, Is.EqualTo(BoardShuffleStatus.UnsupportedBoardState));
            Assert.That(random.Calls, Is.Empty);
        }

        [Test]
        public void RecoveryExhaustsExactBoundedIdentityAttemptsWithoutPublishingBoard()
        {
            var board = Board(1, 1);
            var random = new ScriptedRandom(1, 1);
            var result = new TargetRecoveryCoordinator(random).SelectNextTarget(
                board,
                new TargetHistory(null, 0),
                new TargetRecoveryConfig(
                    new TargetSearchConfig(9, 9, 2, 2, 100),
                    new TargetSelectionPolicy(1),
                    2));

            Assert.That(result.Status, Is.EqualTo(TargetRecoveryStatus.UnrecoverableDeadlock));
            Assert.That(result.ShuffleAttemptCount, Is.EqualTo(2));
            Assert.That(result.Board, Is.Null);
            Assert.That(result.Solution, Is.Null);
            Assert.That(result.Deltas, Is.Empty);
            Assert.That(result.BoardChanged, Is.False);
            Assert.That(result.MoveCost, Is.Zero);
            Assert.That(random.Calls, Is.EqualTo(new[] { (0, 2), (0, 2) }));
        }

        [Test]
        public void MaximumHistoryCanSelectAlternativeAndResetWithoutOverflow()
        {
            var search = new TargetPathSearcher().Search(Board(2, 3, 4), new TargetSearchConfig(5, 7, 2, 2, 100));
            var random = new ScriptedRandom(0);
            var result = new SafeTargetSelector(random).Select(
                search,
                new TargetSelectionPolicy(int.MaxValue),
                new TargetHistory(new TargetNumber(5), int.MaxValue));

            Assert.That(result.Status, Is.EqualTo(TargetSelectionStatus.Succeeded));
            Assert.That(result.SelectedSolution.Target.Value, Is.EqualTo(7));
            Assert.That(result.UpdatedHistory.ConsecutiveCount, Is.EqualTo(1));
            Assert.That(random.Calls, Is.EqualTo(new[] { (0, 1) }));
        }

        [Test]
        public void SoleSameTargetAtMaximumHistoryOverflowsAfterExactSelectionDraw()
        {
            var search = new TargetPathSearcher().Search(Board(2, 3), new TargetSearchConfig(5, 5, 2, 2, 20));
            var random = new ScriptedRandom(0);
            var result = new SafeTargetSelector(random).Select(
                search,
                new TargetSelectionPolicy(int.MaxValue),
                new TargetHistory(new TargetNumber(5), int.MaxValue));

            Assert.That(result.Status, Is.EqualTo(TargetSelectionStatus.HistoryOverflow));
            Assert.That(result.SelectedSolution, Is.Null);
            Assert.That(random.Calls, Is.EqualTo(new[] { (0, 1) }));
        }

        [Test]
        public void OneCellUnsupportedPrecedesInsufficientMovableBlocks()
        {
            var shuffler = new BoardShuffler(new ScriptedRandom());
            var blocked = Board(1);
            blocked.TrySetAccess(default, CellAccess.Blocked);
            Assert.That(shuffler.Shuffle(blocked).Status, Is.EqualTo(BoardShuffleStatus.UnsupportedBoardState));

            var empty = new DomainBoard(BoardTopology.CreateRectangular(1, 1));
            Assert.That(shuffler.Shuffle(empty).Status, Is.EqualTo(BoardShuffleStatus.UnsupportedBoardState));
        }

        [Test]
        public void RecoverySucceedsAfterOneShuffleWithSharedRandomOrderAndCoherentFinalDeltas()
        {
            var board = SquareBoard();
            var random = new ScriptedRandom(2, 2, 1, 0);
            var result = new TargetRecoveryCoordinator(random).SelectNextTarget(
                board,
                new TargetHistory(null, 0),
                RecoveryConfig(2));

            Assert.That(result.Status, Is.EqualTo(TargetRecoveryStatus.RecoveredByShuffle));
            Assert.That(result.ShuffleAttemptCount, Is.EqualTo(1));
            Assert.That(result.Solution.Target.Value, Is.EqualTo(5));
            Assert.That(result.Solution.Validate(result.Board), Is.EqualTo(TargetSolutionValidation.Valid));
            Assert.That(random.Calls, Is.EqualTo(new[] { (0, 4), (0, 3), (0, 2), (0, 1) }));
            Assert.That(result.Deltas.Select(delta => delta.To), Is.Ordered.By("Row").Then.By("Column"));
            foreach (var delta in result.Deltas)
            {
                Assert.That(board.TryFindBlock(delta.Block.Id, out var original), Is.True);
                Assert.That(original, Is.EqualTo(delta.From));
                Assert.That(result.Board.TryFindBlock(delta.Block.Id, out var final), Is.True);
                Assert.That(final, Is.EqualTo(delta.To));
            }
        }

        [Test]
        public void RecoveryRetriesIdentityThenSucceedsOnSecondAttemptUsingSameStream()
        {
            var board = SquareBoard();
            var random = new ScriptedRandom(3, 2, 1, 2, 2, 1, 0);
            var result = new TargetRecoveryCoordinator(random).SelectNextTarget(
                board,
                new TargetHistory(null, 0),
                RecoveryConfig(2));

            Assert.That(result.Status, Is.EqualTo(TargetRecoveryStatus.RecoveredByShuffle));
            Assert.That(result.ShuffleAttemptCount, Is.EqualTo(2));
            Assert.That(random.Calls, Is.EqualTo(new[]
            {
                (0, 4), (0, 3), (0, 2),
                (0, 4), (0, 3), (0, 2),
                (0, 1)
            }));
        }

        [Test]
        public void RecoverySelectsAlternateOnCurrentBoardWithoutShuffle()
        {
            var board = Board(2, 3, 4);
            var random = new ScriptedRandom(0);
            var result = new TargetRecoveryCoordinator(random).RecoverCurrentTarget(
                board,
                new TargetNumber(6),
                new TargetHistory(new TargetNumber(6), 1),
                new TargetRecoveryConfig(
                    new TargetSearchConfig(5, 7, 2, 2, 100),
                    new TargetSelectionPolicy(1),
                    2));

            Assert.That(result.Status, Is.EqualTo(TargetRecoveryStatus.SelectedOnCurrentBoard));
            Assert.That(result.Board, Is.SameAs(board));
            Assert.That(result.BoardChanged, Is.False);
            Assert.That(result.ShuffleAttemptCount, Is.Zero);
            Assert.That(result.Deltas, Is.Empty);
            Assert.That(random.Calls, Is.EqualTo(new[] { (0, 2) }));
        }

        [Test]
        public void RecoverySearchLimitAndInvalidInputsNeverPublishSafeBoard()
        {
            var board = SquareBoard();
            var coordinator = new TargetRecoveryCoordinator(new ScriptedRandom());
            var limited = coordinator.SelectNextTarget(
                board,
                new TargetHistory(null, 0),
                new TargetRecoveryConfig(new TargetSearchConfig(5, 5, 2, 4, 1), new TargetSelectionPolicy(1), 1));
            Assert.That(limited.Status, Is.EqualTo(TargetRecoveryStatus.SearchLimitExceeded));
            Assert.That(limited.Board, Is.Null);
            Assert.That(coordinator.SelectNextTarget(null, null, null).Status, Is.EqualTo(TargetRecoveryStatus.MissingInput));
        }

        private static DomainBoard Board(params int[] values)
        {
            var board = new DomainBoard(BoardTopology.CreateRectangular(values.Length, 1));
            for (var index = 0; index < values.Length; index++)
                board.TryPlaceBlock(new BoardPosition(index, 0), new NumberBlock(new BlockId(index + 1), values[index]));
            return board;
        }

        private static DomainBoard SquareBoard()
        {
            var board = new DomainBoard(BoardTopology.CreateRectangular(2, 2));
            var values = new[] { 1, 2, 3, 4 };
            var index = 0;
            foreach (var position in board.EnumerateActivePositions())
                board.TryPlaceBlock(position, new NumberBlock(new BlockId(index + 1), values[index++]));
            return board;
        }

        private static TargetRecoveryConfig RecoveryConfig(int attempts)
        {
            return new TargetRecoveryConfig(
                new TargetSearchConfig(5, 5, 2, 2, 500),
                new TargetSelectionPolicy(2),
                attempts);
        }

        private static void AssertId(DomainBoard board, int column, int id)
        {
            board.TryGetCell(new BoardPosition(column, 0), out var cell);
            Assert.That(cell.Block.Value.Id.Value, Is.EqualTo(id));
        }

        private sealed class ScriptedRandom : IRandomSource
        {
            private readonly Queue<int> values;
            public ScriptedRandom(params int[] values) { this.values = new Queue<int>(values); }
            public List<(int, int)> Calls { get; } = new List<(int, int)>();
            public int NextInt(int minInclusive, int maxExclusive)
            { Calls.Add((minInclusive, maxExclusive)); return values.Dequeue(); }
            public float NextFloat() => throw new NotSupportedException();
        }
    }
}
