using System;
using System.Collections.Generic;
using System.Linq;
using MathGame.Answer;
using MathGame.BoardResolution;

namespace MathGame.StageSession
{
    public sealed class StageSession
    {
        private readonly long[] progress;
        internal StageDefinition Definition { get; }
        internal long NextId { get; private set; } = 1;
        internal int RemainingMoves { get; private set; }
        internal int SpentMoves { get; private set; }
        internal long Score { get; private set; }
        internal long CorrectCount { get; private set; }
        internal long MissCount { get; private set; }
        internal long PerfectCount { get; private set; }
        internal long FastCount { get; private set; }
        internal long NormalCount { get; private set; }
        internal int CurrentFastStreak { get; private set; }
        internal int MaximumFastStreak { get; private set; }
        internal long TotalRemoved { get; private set; }
        internal long TotalLong { get; private set; }
        internal long TotalFever { get; private set; }
        public StageSessionStatus Status { get; private set; } = StageSessionStatus.Active;

        private StageSession(StageDefinition definition)
        {
            Definition = definition;
            RemainingMoves = definition.InitialMoves;
            progress = new long[definition.Objectives.Count];
        }

        public StageSessionSnapshot CreateSnapshot() => Snapshot();

        public static StageSessionCreateStatus TryCreate(StageDefinition definition, out StageSession session)
        {
            session = null;
            if (definition == null) return StageSessionCreateStatus.MissingDefinition;
            if (!definition.Id.IsValid) return StageSessionCreateStatus.InvalidDefinitionId;
            if (definition.InitialMoves <= 0) return StageSessionCreateStatus.InvalidMoves;
            if (definition.Objectives == null) return StageSessionCreateStatus.MissingObjectives;
            if (definition.Objectives.Count is < 1 or > 2) return StageSessionCreateStatus.InvalidObjectiveCount;
            foreach (var objective in definition.Objectives)
            {
                if (objective == null)
                    return StageSessionCreateStatus.MissingObjective;
            }
            foreach (var objective in definition.Objectives)
                if (objective.Kind is StageObjectiveKind.RemoveObstacle or StageObjectiveKind.EarnRestorationEnergy or StageObjectiveKind.CreateSpecial or StageObjectiveKind.UseSpecial)
                    return StageSessionCreateStatus.UnsupportedObjective;
            foreach (var objective in definition.Objectives)
            {
                if (!ValidObjective(objective))
                    return StageSessionCreateStatus.InvalidObjective;
            }
            if (definition.Objectives.Select(ObjectiveKey).Distinct().Count() != definition.Objectives.Count)
                return StageSessionCreateStatus.DuplicateObjective;
            if (definition.ScoreConfig == null) return StageSessionCreateStatus.MissingScoreConfig;
            if (!ValidScore(definition.ScoreConfig)) return StageSessionCreateStatus.InvalidScoreConfig;
            session = new StageSession(definition);
            return StageSessionCreateStatus.Succeeded;
        }

        public StageAttemptResult ApplyAttempt(StageAttemptCommand command)
        {
            var before = Snapshot();
            if (command == null) return Rejected(StageAttemptApplyStatus.MissingCommand, before);
            if (Status != StageSessionStatus.Active) return Rejected(StageAttemptApplyStatus.SessionAlreadyTerminal, before);
            if (!command.Id.IsValid) return Rejected(StageAttemptApplyStatus.InvalidAttempt, before);
            if (command.Id.Value < NextId) return Rejected(StageAttemptApplyStatus.DuplicateAttempt, before);
            if (command.Id.Value > NextId) return Rejected(StageAttemptApplyStatus.OutOfOrderAttempt, before);
            if (command.Answer == null) return Rejected(StageAttemptApplyStatus.InvalidAttempt, before);
            if (!IsConsistentAnswer(command.Answer) || command.Answer.Outcome == AnswerOutcome.NoSelection)
                return Rejected(StageAttemptApplyStatus.InvalidAnswer, before);
            if (command.Answer.Outcome == AnswerOutcome.Miss)
            {
                if (command.Resolution != null) return Rejected(StageAttemptApplyStatus.UnexpectedResolution, before);
                try
                {
                    if (NextId == long.MaxValue) throw new OverflowException();
                    var nextId = checked(NextId + 1);
                    var missCount = checked(MissCount + 1);
                    NextId = nextId;
                    MissCount = missCount;
                    CurrentFastStreak = 0;
                }
                catch (OverflowException) { return Rejected(StageAttemptApplyStatus.ArithmeticOverflow, before); }
                var events = new[] { new StageSessionEvent(StageSessionEventKind.MissRecorded, -1, 0) };
                return new StageAttemptResult(StageAttemptApplyStatus.AppliedMiss, before, Snapshot(), 0, StageRewardBreakdown.None, events);
            }
            if (!command.Answer.IsCorrect || command.Resolution == null || !command.Resolution.Succeeded || !Correlates(command.Answer, command.Resolution))
                return Rejected(StageAttemptApplyStatus.AnswerResolutionMismatch, before);
            if (RemainingMoves == 0) return Rejected(StageAttemptApplyStatus.NoMovesRemaining, before);

            try
            {
                var answer = command.Answer;
                var removed = command.Resolution.Removed.Count;
                var newProgress = (long[])progress.Clone();
                var objectiveEvents = new List<StageSessionEvent>();
                for (var i = 0; i < Definition.Objectives.Count; i++)
                {
                    var objective = Definition.Objectives[i];
                    long increment = 0;
                    if (objective.Kind == StageObjectiveKind.RemoveNumberBlocks) increment = removed;
                    else if (objective.Kind == StageObjectiveKind.CompleteTarget && objective.Target.Value == answer.Target.Value) increment = 1;
                    else if (objective.Kind == StageObjectiveKind.CompleteLongConnection && answer.SelectedBlockCount >= objective.MinimumConnectionLength) increment = 1;
                    var next = Math.Min(objective.RequiredCount, checked(newProgress[i] + increment));
                    var applied = next - newProgress[i];
                    newProgress[i] = next;
                    if (applied > 0)
                        objectiveEvents.Add(new StageSessionEvent(StageSessionEventKind.ObjectiveProgressed, i, applied));
                }
                var streak = answer.Grade == SpeedGrade.Fast ? checked(CurrentFastStreak + 1) : 0;
                var gradeFever = answer.Grade == SpeedGrade.Perfect ? 25 : answer.Grade == SpeedGrade.Fast ? 15 : 5;
                var lengthFever = answer.SelectedBlockCount == 3 ? 3 : answer.SelectedBlockCount == 4 ? 6 : answer.SelectedBlockCount >= 5 ? 10 : 0;
                var streakFever = answer.Grade == SpeedGrade.Fast && streak >= 2 ? 5 : 0;
                var scoreAward = checked(Definition.ScoreConfig.BaseCorrectScore + GradeScore(answer.Grade) + LengthScore(answer.SelectedBlockCount));
                var reward = new StageRewardBreakdown(gradeFever, lengthFever, streakFever, scoreAward, ConnectionLengthRewardClassifier.Classify(answer.SelectedBlockCount));
                if (NextId == long.MaxValue) throw new OverflowException();
                var nextId = checked(NextId + 1); var score = checked(Score + scoreAward);
                var correct = checked(CorrectCount + 1); var totalRemoved = checked(TotalRemoved + removed);
                var totalLong = checked(TotalLong + (answer.SelectedBlockCount >= 3 ? 1 : 0));
                var totalFever = checked(TotalFever + reward.TotalFeverContribution);
                var perfect = checked(PerfectCount + (answer.Grade == SpeedGrade.Perfect ? 1 : 0));
                var fast = checked(FastCount + (answer.Grade == SpeedGrade.Fast ? 1 : 0));
                var normal = checked(NormalCount + (answer.Grade == SpeedGrade.Normal ? 1 : 0));
                var remaining = RemainingMoves - 1; var spent = checked(SpentMoves + 1);
                var success = newProgress.Select((value, index) => value >= Definition.Objectives[index].RequiredCount).All(value => value);

                Array.Copy(newProgress, progress, progress.Length); NextId = nextId; Score = score; CorrectCount = correct;
                TotalRemoved = totalRemoved; TotalLong = totalLong; TotalFever = totalFever; PerfectCount = perfect;
                FastCount = fast; NormalCount = normal; CurrentFastStreak = streak; MaximumFastStreak = Math.Max(MaximumFastStreak, streak);
                RemainingMoves = remaining; SpentMoves = spent; Status = success ? StageSessionStatus.Success : remaining == 0 ? StageSessionStatus.Failure : StageSessionStatus.Active;
                var events = new List<StageSessionEvent> { new StageSessionEvent(StageSessionEventKind.AnswerAccepted, -1, 0) };
                if (scoreAward > 0) events.Add(new StageSessionEvent(StageSessionEventKind.ScoreAwarded, -1, scoreAward));
                events.AddRange(objectiveEvents); events.Add(new StageSessionEvent(StageSessionEventKind.MoveConsumed, -1, 1));
                if (Status == StageSessionStatus.Success) events.Add(new StageSessionEvent(StageSessionEventKind.StageSucceeded, -1, 0));
                else if (Status == StageSessionStatus.Failure) events.Add(new StageSessionEvent(StageSessionEventKind.StageFailed, -1, 0));
                var applyStatus = Status == StageSessionStatus.Success ? StageAttemptApplyStatus.AppliedSuccess : Status == StageSessionStatus.Failure ? StageAttemptApplyStatus.AppliedFailure : StageAttemptApplyStatus.AppliedContinue;
                return new StageAttemptResult(applyStatus, before, Snapshot(), 1, reward, events.ToArray());
            }
            catch (OverflowException) { return Rejected(StageAttemptApplyStatus.ArithmeticOverflow, before); }
        }

        private StageAttemptResult Rejected(StageAttemptApplyStatus status, StageSessionSnapshot before)
            => new StageAttemptResult(status, before, before, 0, StageRewardBreakdown.None, Array.Empty<StageSessionEvent>());
        private StageSessionSnapshot Snapshot() => new StageSessionSnapshot(this, Definition.Objectives.Select((definition, index) => new ObjectiveProgressSnapshot(index, definition, progress[index])).ToArray());
        private long GradeScore(SpeedGrade grade) => grade == SpeedGrade.Perfect ? Definition.ScoreConfig.PerfectBonus : grade == SpeedGrade.Fast ? Definition.ScoreConfig.FastBonus : Definition.ScoreConfig.NormalBonus;
        private long LengthScore(int length) => Definition.ScoreConfig.LengthRules.Where(rule => rule.MinimumLength <= length).OrderBy(rule => rule.MinimumLength).Select(rule => rule.Bonus).LastOrDefault();

        private static bool Correlates(AnswerResult answer, BoardResolutionResult resolution)
        {
            if (resolution.Removed.Count != answer.SelectedBlockCount) return false;
            for (var i = 0; i < resolution.Removed.Count; i++)
            {
                var removed = resolution.Removed[i]; var entry = answer.Snapshot.Entries[i];
                if (removed.Position != entry.Position || removed.Block != entry.Block) return false;
            }
            return true;
        }

        private static bool IsConsistentAnswer(AnswerResult answer)
        {
            // AnswerResult instances produced by the public AnswerValidator already satisfy these
            // combinations. These branches are defensive review guards for future contract changes;
            // tests must not use reflection or a test-only construction seam to fabricate them.
            if (!answer.Target.IsValid || answer.Snapshot == null ||
                double.IsNaN(answer.InteractiveElapsedSeconds) ||
                double.IsInfinity(answer.InteractiveElapsedSeconds) ||
                answer.InteractiveElapsedSeconds < 0)
                return false;

            switch (answer.Outcome)
            {
                case AnswerOutcome.NoSelection:
                    return answer.Snapshot.IsEmpty && answer.Relation == AnswerRelation.None &&
                        answer.MissReason == AnswerMissReason.None && answer.Grade == SpeedGrade.None;
                case AnswerOutcome.Miss when answer.Relation == AnswerRelation.BelowTarget:
                    return !answer.Snapshot.IsEmpty && answer.SubmittedSum < answer.Target.Value &&
                        answer.MissReason == AnswerMissReason.UnderTarget && answer.Grade == SpeedGrade.Miss;
                case AnswerOutcome.Miss when answer.Relation == AnswerRelation.AboveTarget:
                    return !answer.Snapshot.IsEmpty && answer.SubmittedSum > answer.Target.Value &&
                        answer.MissReason == AnswerMissReason.OverTarget && answer.Grade == SpeedGrade.Miss;
                case AnswerOutcome.Miss when answer.Relation == AnswerRelation.MatchesTarget:
                    return answer.Snapshot.Count == 1 && answer.SubmittedSum == answer.Target.Value &&
                        answer.MissReason == AnswerMissReason.InsufficientConnectionLength &&
                        answer.Grade == SpeedGrade.Miss;
                case AnswerOutcome.Correct:
                    return answer.Snapshot.Count >= 2 && answer.SubmittedSum == answer.Target.Value &&
                        answer.Relation == AnswerRelation.MatchesTarget && answer.MissReason == AnswerMissReason.None &&
                        (answer.Grade == SpeedGrade.Perfect || answer.Grade == SpeedGrade.Fast ||
                            answer.Grade == SpeedGrade.Normal);
                default:
                    return false;
            }
        }
        private static bool ValidObjective(StageObjectiveDefinition objective)
        {
            if (!Enum.IsDefined(typeof(StageObjectiveKind), objective.Kind) || objective.RequiredCount <= 0) return false;
            return objective.Kind == StageObjectiveKind.RemoveNumberBlocks ? !objective.Target.IsValid && objective.MinimumConnectionLength == 0
                : objective.Kind == StageObjectiveKind.CompleteTarget ? objective.Target.IsValid && objective.MinimumConnectionLength == 0
                : objective.Kind == StageObjectiveKind.CompleteLongConnection && !objective.Target.IsValid && objective.MinimumConnectionLength >= 3;
        }
        private static string ObjectiveKey(StageObjectiveDefinition objective)
            => $"{objective.Kind}:{objective.Target.Value}:{objective.MinimumConnectionLength}";
        private static bool ValidScore(ScoreRewardConfig score)
        {
            if (score.BaseCorrectScore < 0 || score.PerfectBonus < 0 || score.FastBonus < 0 || score.NormalBonus < 0 || score.LengthRules == null) return false;
            var thresholds = new HashSet<int>();
            return score.LengthRules.All(rule => rule.MinimumLength >= 2 && rule.Bonus >= 0 && thresholds.Add(rule.MinimumLength));
        }
    }
}
