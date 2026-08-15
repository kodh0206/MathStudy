using System;
using System.Collections.Generic;
using System.Linq;
using MathGame.Answer;
using MathGame.BoardResolution;
using MathGame.Restoration.Contracts;

namespace MathGame.StageSession
{
    public sealed class StageSession
    {
        private readonly long[] progress;
        internal StageDefinition Definition { get; }
        internal long NextId { get; private set; } = 1;
        internal long NextEffectId { get; private set; } = 1;
        internal long Version { get; private set; }
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
        internal long TotalDestroyedDust { get; private set; }
        internal long TotalDestroyedBoxes { get; private set; }
        internal StageRunId RunId { get; private set; }
        internal long ProvisionalRestoration { get; private set; }
        internal long GrossRestoration { get; private set; }
        internal long DiscardedRestoration { get; private set; }
        internal RestorationLifecycle RestorationLifecycle { get; private set; } = RestorationLifecycle.Provisional;
        internal bool ContinueUsed { get; private set; }
        public StageSessionStatus Status { get; private set; } = StageSessionStatus.Active;

        private StageSession(StageDefinition definition, StageRunId runId = default)
        {
            Definition = definition;
            RunId = runId;
            RemainingMoves = definition.InitialMoves;
            progress = new long[definition.Objectives.Count];
        }

        public StageSessionSnapshot CreateSnapshot() => Snapshot();

        public StageAttemptPrepareResult PrepareAttempt(StageAttemptCommand command, RestorationAwardEvidence restoration)
        {
            if (command != null && !ReferenceEquals(command.Restoration, restoration))
                command = command.ObstacleResolution != null
                    ? new StageAttemptCommand(command.Id, command.Answer, command.ObstacleResolution, command.Rules, restoration)
                    : command;
            var prospective = CloneForPlanning();
            var result = prospective.ApplyAttemptCore(command, true);
            if (result.Status is StageAttemptApplyStatus.AppliedContinue or StageAttemptApplyStatus.AppliedMiss or StageAttemptApplyStatus.AppliedSuccess or StageAttemptApplyStatus.AppliedFailure)
            {
                var status = result.Status == StageAttemptApplyStatus.AppliedSuccess ? StageAttemptPrepareStatus.PreparedSuccess
                    : result.Status == StageAttemptApplyStatus.AppliedFailure ? StageAttemptPrepareStatus.PreparedFailedPendingDecision
                    : result.Status == StageAttemptApplyStatus.AppliedMiss ? StageAttemptPrepareStatus.PreparedMiss
                    : StageAttemptPrepareStatus.PreparedContinue;
                return new StageAttemptPrepareResult(status, new StageAttemptPlan(this, Version, prospective, result, null), null);
            }
            return new StageAttemptPrepareResult(MapPrepareFailure(result.Status), null, result);
        }

        public StageAttemptBindResult BindWorldCommit(StageAttemptPlan plan, IWorldCommitPlan worldPlan)
        {
            if (plan == null) return new StageAttemptBindResult(StageAttemptBindStatus.MissingAttemptPlan, null);
            if (!ReferenceEquals(plan.Owner, this) || plan.PreparedVersion != Version) return new StageAttemptBindResult(StageAttemptBindStatus.StalePlan, null);
            if (!plan.WouldSucceed) return new StageAttemptBindResult(StageAttemptBindStatus.AttemptPlanNotSuccessful, null);
            if (worldPlan == null) return new StageAttemptBindResult(StageAttemptBindStatus.MissingWorldPlan, null);
            if (!RunId.IsValid || worldPlan.CommitId.Value != RunId.Value || Definition.RestorationConfig == null || !worldPlan.WorldId.Equals(Definition.RestorationConfig.WorldId))
                return new StageAttemptBindResult(StageAttemptBindStatus.WorldPlanMismatch, null);
            return new StageAttemptBindResult(StageAttemptBindStatus.Bound, new StageAttemptPlan(this, Version, plan.Prospective, plan.Result, worldPlan));
        }

        public StageAttemptCommitResult CommitAttempt(StageAttemptPlan plan)
        {
            if (plan == null) return new StageAttemptCommitResult(StageAttemptCommitStatus.MissingPlan, null);
            if (!ReferenceEquals(plan.Owner, this) || plan.PreparedVersion != Version) return new StageAttemptCommitResult(StageAttemptCommitStatus.StalePlan, null);
            if (plan.WouldSucceed && Definition.RestorationConfig != null && !plan.IsWorldBound) return new StageAttemptCommitResult(StageAttemptCommitStatus.WorldPlanRequired, null);
            CopyStateFrom(plan.Prospective);
            var status = plan.Result.Status == StageAttemptApplyStatus.AppliedSuccess ? StageAttemptCommitStatus.CommittedSuccess
                : plan.Result.Status == StageAttemptApplyStatus.AppliedFailure ? StageAttemptCommitStatus.CommittedFailedPendingDecision
                : plan.Result.Status == StageAttemptApplyStatus.AppliedMiss ? StageAttemptCommitStatus.CommittedMiss
                : StageAttemptCommitStatus.CommittedContinue;
            return new StageAttemptCommitResult(status, plan.Result);
        }

        public bool TryContinueFailedAttempt(int additionalMoves)
        {
            if (Status != StageSessionStatus.FailedPendingDecision || additionalMoves != 5 || ContinueUsed) return false;
            try
            {
                RemainingMoves = checked(RemainingMoves + additionalMoves);
                Status = StageSessionStatus.Active;
                ContinueUsed = true;
                RestorationLifecycle = RestorationLifecycle.Provisional;
                Version = checked(Version + 1);
                return true;
            }
            catch (OverflowException) { return false; }
        }

        public bool TryDiscardFailedAttempt()
        {
            if (Status != StageSessionStatus.FailedPendingDecision) return false;
            ProvisionalRestoration = 0;
            RestorationLifecycle = RestorationLifecycle.Discarded;
            Status = StageSessionStatus.Failure;
            Version = checked(Version + 1);
            return true;
        }

        public static StageSessionCreateStatus TryCreate(StageDefinition definition, out StageSession session)
            => TryCreate(definition, default, out session);

        public static StageSessionCreateStatus TryCreate(StageDefinition definition, StageRunId runId, out StageSession session)
        {
            session = null;
            if (definition == null) return StageSessionCreateStatus.MissingDefinition;
            if (!definition.Id.IsValid) return StageSessionCreateStatus.InvalidDefinitionId;
            if (!Enum.IsDefined(typeof(StageSessionMode), definition.Mode)) return StageSessionCreateStatus.InvalidMode;
            if (definition.InitialMoves <= 0) return StageSessionCreateStatus.InvalidMoves;
            if (definition.Objectives == null) return StageSessionCreateStatus.MissingObjectives;
            if (definition.Objectives.Count > 2 ||
                (definition.Mode == StageSessionMode.LegacyStage && definition.Objectives.Count == 0))
                return StageSessionCreateStatus.InvalidObjectiveCount;
            foreach (var objective in definition.Objectives)
            {
                if (objective == null)
                    return StageSessionCreateStatus.MissingObjective;
            }
            foreach (var objective in definition.Objectives)
                if (objective.Kind is StageObjectiveKind.CreateSpecial or StageObjectiveKind.UseSpecial)
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
            var needsRestoration = definition.Objectives.Any(o => o.Kind == StageObjectiveKind.EarnRestorationEnergy) || definition.RestorationConfig != null;
            if (needsRestoration && (definition.RestorationConfig == null || !definition.RestorationConfig.IsValid || !runId.IsValid))
                return StageSessionCreateStatus.InvalidObjective;
            session = new StageSession(definition, runId);
            return StageSessionCreateStatus.Succeeded;
        }

        public StageAttemptResult ApplyAttempt(StageAttemptCommand command) => ApplyAttemptCore(command, false);

        private StageAttemptResult ApplyAttemptCore(StageAttemptCommand command, bool prepared)
        {
            var before = Snapshot();
            if (Definition.RestorationConfig != null && !prepared) return Rejected(StageAttemptApplyStatus.PreparationRequired, before);
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
                if (command.Resolution != null || command.ObstacleResolution != null) return Rejected(StageAttemptApplyStatus.UnexpectedResolution, before);
                try
                {
                    if (NextId == long.MaxValue) throw new OverflowException();
                    var nextId = checked(NextId + 1);
                    var missCount = checked(MissCount + 1);
                    NextId = nextId;
                    MissCount = missCount;
                    if (command.Rules.Mode == StageAttemptMode.Normal)
                        CurrentFastStreak = 0;
                    Version++;
                }
                catch (OverflowException) { return Rejected(StageAttemptApplyStatus.ArithmeticOverflow, before); }
                var events = new[] { new StageSessionEvent(StageSessionEventKind.MissRecorded, -1, 0) };
                return new StageAttemptResult(StageAttemptApplyStatus.AppliedMiss, before, Snapshot(), 0, StageRewardBreakdown.None, events, command.Id, command.Rules.Mode, command.Rules.ScoreMultiplier);
            }
            var legacyValid = command.Resolution != null && command.Resolution.Succeeded && Correlates(command.Answer, command.Resolution);
            var obstacleValid = command.ObstacleResolution != null && command.ObstacleResolution.Succeeded && command.ObstacleResolution.Mode != ObstacleResolutionMode.FeverEnd && Correlates(command.Answer, command.ObstacleResolution);
            if (!command.Answer.IsCorrect || (!legacyValid && !obstacleValid) || (command.Resolution != null && command.ObstacleResolution != null))
                return Rejected(StageAttemptApplyStatus.AnswerResolutionMismatch, before);
            if (Definition.Mode == StageSessionMode.LegacyStage && RemainingMoves == 0) return Rejected(StageAttemptApplyStatus.NoMovesRemaining, before);

            if (Definition.RestorationConfig != null)
            {
                if (command.Restoration == null) return Rejected(StageAttemptApplyStatus.MissingRestorationEvidence, before);
                if (!command.Restoration.IsValid) return Rejected(StageAttemptApplyStatus.InvalidRestorationAward, before);
                if (!command.Restoration.RunId.Equals(RunId) || command.Restoration.Source != RestorationAwardSource.Answer || command.Restoration.SourceId != command.Id.Value)
                    return Rejected(StageAttemptApplyStatus.RestorationSourceMismatch, before);
                var expectedLength = command.Answer.SelectedBlockCount;
                var expectedFever = command.Rules.Mode == StageAttemptMode.Fever;
                var lengthTenths = expectedLength <= 2 ? 10 : expectedLength == 3 ? 12 : expectedLength == 4 ? 15 : 20;
                var expectedAward = checked(10L * lengthTenths * (expectedFever ? 20 : 10) / 100L);
                if (command.Restoration.SubmittedLength != expectedLength || command.Restoration.FeverAnswer != expectedFever || command.Restoration.GrossAward != expectedAward)
                    return Rejected(StageAttemptApplyStatus.InvalidRestorationAward, before);
            }
            else if (command.Restoration != null) return Rejected(StageAttemptApplyStatus.UnexpectedRestorationEvidence, before);

            try
            {
                var answer = command.Answer;
                var removed = command.ObstacleResolution != null ? command.ObstacleResolution.Removed.Count : command.Resolution.Removed.Count;
                var newProgress = (long[])progress.Clone();
                var objectiveEvents = new List<StageSessionEvent>();
                var restorationGross = command.Restoration?.GrossAward ?? 0;
                var prospectiveGrossRestoration = checked(GrossRestoration + restorationGross);
                var availableRestoration = Definition.RestorationConfig == null ? 0 : Definition.RestorationConfig.StageCapacity - ProvisionalRestoration;
                var restorationApplied = Math.Min(restorationGross, Math.Max(0, availableRestoration));
                var prospectiveRestoration = checked(ProvisionalRestoration + restorationApplied);
                var prospectiveDiscardedRestoration = checked(DiscardedRestoration + restorationGross - restorationApplied);
                for (var i = 0; i < Definition.Objectives.Count; i++)
                {
                    var objective = Definition.Objectives[i];
                    long increment = 0;
                    if (objective.Kind == StageObjectiveKind.RemoveNumberBlocks) increment = removed;
                    else if (objective.Kind == StageObjectiveKind.RemoveObstacle && command.ObstacleResolution != null)
                        increment = command.ObstacleResolution.DestroyedObstacles.Count(e => e.Kind == objective.ObstacleKind.Value);
                    else if (objective.Kind == StageObjectiveKind.CompleteTarget && objective.Target.Value == answer.Target.Value) increment = 1;
                    else if (objective.Kind == StageObjectiveKind.CompleteLongConnection && answer.SelectedBlockCount >= objective.MinimumConnectionLength) increment = 1;
                    else if (objective.Kind == StageObjectiveKind.EarnRestorationEnergy) increment = restorationGross;
                    var next = Math.Min(objective.RequiredCount, checked(newProgress[i] + increment));
                    var applied = next - newProgress[i];
                    newProgress[i] = next;
                    if (applied > 0)
                        objectiveEvents.Add(new StageSessionEvent(StageSessionEventKind.ObjectiveProgressed, i, applied));
                }
                var isNormalAttempt = command.Rules.Mode == StageAttemptMode.Normal;
                var streak = isNormalAttempt
                    ? answer.Grade == SpeedGrade.Fast ? checked(CurrentFastStreak + 1) : 0
                    : CurrentFastStreak;
                var gradeFever = isNormalAttempt ? answer.Grade == SpeedGrade.Perfect ? 25 : answer.Grade == SpeedGrade.Fast ? 15 : 5 : 0;
                var lengthFever = isNormalAttempt ? answer.SelectedBlockCount == 3 ? 3 : answer.SelectedBlockCount == 4 ? 6 : answer.SelectedBlockCount >= 5 ? 10 : 0 : 0;
                var streakFever = isNormalAttempt && answer.Grade == SpeedGrade.Fast && streak >= 2 ? 5 : 0;
                var scoreAward = checked((Definition.ScoreConfig.BaseCorrectScore + GradeScore(answer.Grade) + LengthScore(answer.SelectedBlockCount)) * command.Rules.ScoreMultiplier);
                var reward = new StageRewardBreakdown(gradeFever, lengthFever, streakFever, scoreAward, ConnectionLengthRewardClassifier.Classify(answer.SelectedBlockCount));
                if (NextId == long.MaxValue) throw new OverflowException();
                var nextId = checked(NextId + 1); var score = checked(Score + scoreAward);
                var correct = checked(CorrectCount + 1); var totalRemoved = checked(TotalRemoved + removed);
                var totalLong = checked(TotalLong + (answer.SelectedBlockCount >= 3 ? 1 : 0));
                var totalFever = checked(TotalFever + reward.TotalFeverContribution);
                var perfect = checked(PerfectCount + (answer.Grade == SpeedGrade.Perfect ? 1 : 0));
                var fast = checked(FastCount + (answer.Grade == SpeedGrade.Fast ? 1 : 0));
                var normal = checked(NormalCount + (answer.Grade == SpeedGrade.Normal ? 1 : 0));
                var destroyedDust = checked(TotalDestroyedDust + (command.ObstacleResolution?.DestroyedObstacles.Count(e => e.Kind == MathGame.Board.ObstacleKind.Dust) ?? 0));
                var destroyedBoxes = checked(TotalDestroyedBoxes + (command.ObstacleResolution?.DestroyedObstacles.Count(e => e.Kind == MathGame.Board.ObstacleKind.Box) ?? 0));
                var moveCost = Definition.Mode == StageSessionMode.ContinuousRun ? 0 : command.Rules.CorrectMoveCost;
                var remaining = RemainingMoves - moveCost; var spent = checked(SpentMoves + moveCost);
                var success = Definition.Mode == StageSessionMode.LegacyStage && newProgress.Select((value, index) => value >= Definition.Objectives[index].RequiredCount).All(value => value);

                Array.Copy(newProgress, progress, progress.Length); NextId = nextId; Score = score; CorrectCount = correct;
                TotalRemoved = totalRemoved; TotalLong = totalLong; TotalFever = totalFever; PerfectCount = perfect;
                FastCount = fast; NormalCount = normal; CurrentFastStreak = streak; MaximumFastStreak = Math.Max(MaximumFastStreak, streak);
                TotalDestroyedDust = destroyedDust; TotalDestroyedBoxes = destroyedBoxes;
                GrossRestoration = prospectiveGrossRestoration; ProvisionalRestoration = prospectiveRestoration; DiscardedRestoration = prospectiveDiscardedRestoration;
                RemainingMoves = remaining; SpentMoves = spent; Status = success ? StageSessionStatus.Success : remaining == 0 ? (Definition.RestorationConfig == null ? StageSessionStatus.Failure : StageSessionStatus.FailedPendingDecision) : StageSessionStatus.Active;
                RestorationLifecycle = Status == StageSessionStatus.Success ? RestorationLifecycle.CommittedSuccess : Status == StageSessionStatus.FailedPendingDecision ? RestorationLifecycle.FailedPendingDecision : RestorationLifecycle.Provisional;
                Version++;
                var events = new List<StageSessionEvent> { new StageSessionEvent(StageSessionEventKind.AnswerAccepted, -1, 0) };
                if (scoreAward > 0) events.Add(new StageSessionEvent(StageSessionEventKind.ScoreAwarded, -1, scoreAward));
                events.AddRange(objectiveEvents);
                if (moveCost > 0) events.Add(new StageSessionEvent(StageSessionEventKind.MoveConsumed, -1, moveCost));
                if (Status == StageSessionStatus.Success) events.Add(new StageSessionEvent(StageSessionEventKind.StageSucceeded, -1, 0));
                else if (Status is StageSessionStatus.Failure or StageSessionStatus.FailedPendingDecision) events.Add(new StageSessionEvent(StageSessionEventKind.StageFailed, -1, 0));
                var applyStatus = Status == StageSessionStatus.Success ? StageAttemptApplyStatus.AppliedSuccess : Status is StageSessionStatus.Failure or StageSessionStatus.FailedPendingDecision ? StageAttemptApplyStatus.AppliedFailure : StageAttemptApplyStatus.AppliedContinue;
                return new StageAttemptResult(applyStatus, before, Snapshot(), moveCost, reward, events.ToArray(), command.Id, command.Rules.Mode, command.Rules.ScoreMultiplier);
            }
            catch (OverflowException) { return Rejected(StageAttemptApplyStatus.ArithmeticOverflow, before); }
        }

        public StageSystemEffectPrepareResult PrepareSystemEffect(ObstacleResolutionResult result)
            => PrepareSystemEffect(result, null);

        public StageSystemEffectPrepareResult PrepareSystemEffect(ObstacleResolutionResult result, RestorationAwardEvidence restoration)
        {
            var before = Snapshot();
            if (result == null) return PreparedFailure(StageSystemEffectPrepareStatus.MissingResult, before);
            if (Status != StageSessionStatus.Active) return PreparedFailure(StageSystemEffectPrepareStatus.SessionAlreadyTerminal, before);
            if (!result.Succeeded) return PreparedFailure(StageSystemEffectPrepareStatus.ResolutionNotSucceeded, before);
            if (result.Mode != ObstacleResolutionMode.FeverEnd) return PreparedFailure(StageSystemEffectPrepareStatus.NotSystemEffect, before);
            if (!result.SystemEffectId.IsValid) return PreparedFailure(StageSystemEffectPrepareStatus.InvalidEffectId, before);
            if (result.SystemEffectId.Value < NextEffectId) return PreparedFailure(StageSystemEffectPrepareStatus.DuplicateEffect, before);
            if (result.SystemEffectId.Value > NextEffectId) return PreparedFailure(StageSystemEffectPrepareStatus.OutOfOrderEffect, before);
            if (NextEffectId == long.MaxValue) return PreparedFailure(StageSystemEffectPrepareStatus.ArithmeticOverflow, before);
            var large = result.Pattern == FeverEndPattern.Large;
            if (Definition.RestorationConfig != null && large && restoration == null) return PreparedFailure(StageSystemEffectPrepareStatus.InvalidEvidence, before);
            if ((!large || Definition.RestorationConfig == null) && restoration != null) return PreparedFailure(StageSystemEffectPrepareStatus.InvalidEvidence, before);
            if (restoration != null && (!restoration.IsValid || !restoration.RunId.Equals(RunId) || restoration.Source != RestorationAwardSource.LargeFeverEnd || restoration.SourceId != result.SystemEffectId.Value || restoration.GrossAward != 50)) return PreparedFailure(StageSystemEffectPrepareStatus.InvalidEvidence, before);
            try
            {
                var uniqueBlocks = new HashSet<MathGame.Board.BlockId>(); foreach (var removed in result.Removed) if (!uniqueBlocks.Add(removed.Block.Id)) return PreparedFailure(StageSystemEffectPrepareStatus.InvalidEvidence, before);
                var uniqueObstacles = new HashSet<MathGame.Board.ObstacleId>(); foreach (var destroyed in result.DestroyedObstacles) if (!uniqueObstacles.Add(destroyed.Id)) return PreparedFailure(StageSystemEffectPrepareStatus.InvalidEvidence, before);
                var nextProgress = (long[])progress.Clone(); var events = new List<StageSessionEvent>();
                var restorationGross = restoration?.GrossAward ?? 0;
                var prospectiveGrossRestoration = checked(GrossRestoration + restorationGross);
                var availableRestoration = Definition.RestorationConfig == null ? 0 : Definition.RestorationConfig.StageCapacity - ProvisionalRestoration;
                var restorationApplied = Math.Min(restorationGross, Math.Max(0, availableRestoration));
                var prospectiveRestoration = checked(ProvisionalRestoration + restorationApplied);
                var prospectiveDiscardedRestoration = checked(DiscardedRestoration + restorationGross - restorationApplied);
                for (var i = 0; i < Definition.Objectives.Count; i++)
                {
                    var objective = Definition.Objectives[i]; long increment = 0;
                    if (objective.Kind == StageObjectiveKind.RemoveNumberBlocks) increment = uniqueBlocks.Count;
                    else if (objective.Kind == StageObjectiveKind.RemoveObstacle) foreach (var evidence in result.DestroyedObstacles) if (evidence.Kind == objective.ObstacleKind.Value) increment++;
                    else if (objective.Kind == StageObjectiveKind.EarnRestorationEnergy) increment = restorationGross;
                    var next = Math.Min(objective.RequiredCount, checked(nextProgress[i] + increment)); var applied = next - nextProgress[i]; nextProgress[i] = next;
                    if (applied > 0) events.Add(new StageSessionEvent(StageSessionEventKind.ObjectiveProgressed, i, applied));
                }
                var totalRemoved = checked(TotalRemoved + uniqueBlocks.Count);
                var totalDust = checked(TotalDestroyedDust + result.DestroyedObstacles.Count(e => e.Kind == MathGame.Board.ObstacleKind.Dust));
                var totalBoxes = checked(TotalDestroyedBoxes + result.DestroyedObstacles.Count(e => e.Kind == MathGame.Board.ObstacleKind.Box));
                var success = Definition.Mode == StageSessionMode.LegacyStage && nextProgress.Select((value, index) => value >= Definition.Objectives[index].RequiredCount).All(value => value);
                if (success) events.Add(new StageSessionEvent(StageSessionEventKind.StageSucceeded, -1, 0));
                var prospectiveStatus = success ? StageSessionStatus.Success : StageSessionStatus.Active;
                var plan = new StageSystemEffectPlan(this, Version, result.SystemEffectId, before, SnapshotProspective(nextProgress, totalRemoved, totalDust, totalBoxes, prospectiveGrossRestoration, prospectiveRestoration, prospectiveDiscardedRestoration, prospectiveStatus), nextProgress, totalRemoved, totalDust, totalBoxes, prospectiveGrossRestoration, prospectiveRestoration, prospectiveDiscardedRestoration, prospectiveStatus, events.ToArray());
                return new StageSystemEffectPrepareResult(success ? StageSystemEffectPrepareStatus.PreparedSuccess : StageSystemEffectPrepareStatus.PreparedContinue, plan, before);
            }
            catch (OverflowException) { return PreparedFailure(StageSystemEffectPrepareStatus.ArithmeticOverflow, before); }
        }

        public StageSystemEffectCommitResult CommitSystemEffect(StageSystemEffectPlan plan)
        {
            var before = Snapshot();
            if (plan == null) return CommitFailure(StageSystemEffectCommitStatus.MissingPlan, before);
            if (Status != StageSessionStatus.Active) return CommitFailure(StageSystemEffectCommitStatus.SessionAlreadyTerminal, before);
            if (!ReferenceEquals(plan.Owner, this) || plan.PreparedSessionVersion != Version || plan.EffectId.Value != NextEffectId) return CommitFailure(StageSystemEffectCommitStatus.StalePlan, before);
            if (plan.WouldSucceed && Definition.RestorationConfig != null && !plan.IsWorldBound) return CommitFailure(StageSystemEffectCommitStatus.StalePlan, before);
            Array.Copy(plan.ProspectiveProgress, progress, progress.Length); TotalRemoved = plan.ProspectiveTotalRemoved; TotalDestroyedDust = plan.ProspectiveDestroyedDust; TotalDestroyedBoxes = plan.ProspectiveDestroyedBoxes; GrossRestoration = plan.ProspectiveGrossRestoration; ProvisionalRestoration = plan.ProspectiveRestoration; DiscardedRestoration = plan.ProspectiveDiscardedRestoration; Status = plan.ProspectiveStatus; RestorationLifecycle = Status == StageSessionStatus.Success ? RestorationLifecycle.CommittedSuccess : RestorationLifecycle.Provisional; NextEffectId = checked(NextEffectId + 1); Version++;
            return new StageSystemEffectCommitResult(Status == StageSessionStatus.Success ? StageSystemEffectCommitStatus.CommittedSuccess : StageSystemEffectCommitStatus.CommittedContinue, plan.EffectId, before, Snapshot(), plan.Events);
        }

        public StageSystemEffectPrepareResult BindWorldCommit(StageSystemEffectPlan plan, IWorldCommitPlan worldPlan)
        {
            var before = Snapshot();
            if (plan == null || worldPlan == null || !plan.WouldSucceed || !ReferenceEquals(plan.Owner, this) || plan.PreparedSessionVersion != Version)
                return PreparedFailure(StageSystemEffectPrepareStatus.InvalidEvidence, before);
            if (!RunId.IsValid || worldPlan.CommitId.Value != RunId.Value || Definition.RestorationConfig == null || !worldPlan.WorldId.Equals(Definition.RestorationConfig.WorldId))
                return PreparedFailure(StageSystemEffectPrepareStatus.InvalidEvidence, before);
            var bound = new StageSystemEffectPlan(this, Version, plan.EffectId, plan.Before, plan.ProspectiveAfter, plan.ProspectiveProgress, plan.ProspectiveTotalRemoved, plan.ProspectiveDestroyedDust, plan.ProspectiveDestroyedBoxes, plan.ProspectiveGrossRestoration, plan.ProspectiveRestoration, plan.ProspectiveDiscardedRestoration, plan.ProspectiveStatus, plan.Events.ToArray(), worldPlan);
            return new StageSystemEffectPrepareResult(StageSystemEffectPrepareStatus.PreparedSuccess, bound, before);
        }

        private StageSystemEffectPrepareResult PreparedFailure(StageSystemEffectPrepareStatus status, StageSessionSnapshot before) => new StageSystemEffectPrepareResult(status, null, before);
        private StageSystemEffectCommitResult CommitFailure(StageSystemEffectCommitStatus status, StageSessionSnapshot before) => new StageSystemEffectCommitResult(status, default, before, before, null);
        private StageSessionSnapshot SnapshotProspective(long[] prospectiveProgress, long totalRemoved, long totalDust, long totalBoxes, long grossRestoration, long provisionalRestoration, long discardedRestoration, StageSessionStatus status)
        {
            var copy = new StageSession(Definition, RunId) { NextId = NextId, NextEffectId = NextEffectId + 1, Version = Version + 1, RemainingMoves = RemainingMoves, SpentMoves = SpentMoves, Score = Score, CorrectCount = CorrectCount, MissCount = MissCount, PerfectCount = PerfectCount, FastCount = FastCount, NormalCount = NormalCount, CurrentFastStreak = CurrentFastStreak, MaximumFastStreak = MaximumFastStreak, TotalRemoved = totalRemoved, TotalLong = TotalLong, TotalFever = TotalFever, TotalDestroyedDust = totalDust, TotalDestroyedBoxes = totalBoxes, ProvisionalRestoration = provisionalRestoration, GrossRestoration = grossRestoration, DiscardedRestoration = discardedRestoration, RestorationLifecycle = status == StageSessionStatus.Success ? RestorationLifecycle.CommittedSuccess : RestorationLifecycle.Provisional, Status = status };
            Array.Copy(prospectiveProgress, copy.progress, prospectiveProgress.Length);
            return copy.Snapshot();
        }

        private StageAttemptResult Rejected(StageAttemptApplyStatus status, StageSessionSnapshot before)
            => new StageAttemptResult(status, before, before, 0, StageRewardBreakdown.None, Array.Empty<StageSessionEvent>());
        private StageSessionSnapshot Snapshot() => new StageSessionSnapshot(this, Definition.Objectives.Select((definition, index) => new ObjectiveProgressSnapshot(index, definition, progress[index])).ToArray());

        private StageSession CloneForPlanning()
        {
            var copy = new StageSession(Definition, RunId);
            copy.CopyStateFrom(this);
            return copy;
        }

        private void CopyStateFrom(StageSession source)
        {
            Array.Copy(source.progress, progress, progress.Length);
            NextId = source.NextId; NextEffectId = source.NextEffectId; Version = source.Version;
            RemainingMoves = source.RemainingMoves; SpentMoves = source.SpentMoves; Score = source.Score;
            CorrectCount = source.CorrectCount; MissCount = source.MissCount; PerfectCount = source.PerfectCount;
            FastCount = source.FastCount; NormalCount = source.NormalCount; CurrentFastStreak = source.CurrentFastStreak;
            MaximumFastStreak = source.MaximumFastStreak; TotalRemoved = source.TotalRemoved; TotalLong = source.TotalLong;
            TotalFever = source.TotalFever; TotalDestroyedDust = source.TotalDestroyedDust; TotalDestroyedBoxes = source.TotalDestroyedBoxes;
            RunId = source.RunId; ProvisionalRestoration = source.ProvisionalRestoration; GrossRestoration = source.GrossRestoration;
            DiscardedRestoration = source.DiscardedRestoration; RestorationLifecycle = source.RestorationLifecycle; ContinueUsed = source.ContinueUsed; Status = source.Status;
        }

        private static StageAttemptPrepareStatus MapPrepareFailure(StageAttemptApplyStatus status)
        {
            if (status == StageAttemptApplyStatus.MissingRestorationEvidence) return StageAttemptPrepareStatus.MissingRestorationEvidence;
            if (status == StageAttemptApplyStatus.UnexpectedRestorationEvidence) return StageAttemptPrepareStatus.UnexpectedRestorationEvidence;
            if (status == StageAttemptApplyStatus.RestorationSourceMismatch) return StageAttemptPrepareStatus.RestorationSourceMismatch;
            if (status == StageAttemptApplyStatus.InvalidRestorationAward) return StageAttemptPrepareStatus.InvalidRestorationAward;
            if (status == StageAttemptApplyStatus.ArithmeticOverflow) return StageAttemptPrepareStatus.ArithmeticOverflow;
            return StageAttemptPrepareStatus.Rejected;
        }
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
        private static bool Correlates(AnswerResult answer, ObstacleResolutionResult resolution)
        {
            if (resolution.SelectedRemoved.Count != answer.SelectedBlockCount) return false;
            for (var i = 0; i < resolution.SelectedRemoved.Count; i++) { var removed = resolution.SelectedRemoved[i]; var entry = answer.Snapshot.Entries[i]; if (removed.Position != entry.Position || removed.Block != entry.Block) return false; }
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
            return objective.Kind == StageObjectiveKind.RemoveNumberBlocks ? !objective.Target.IsValid && objective.MinimumConnectionLength == 0 && !objective.ObstacleKind.HasValue
                : objective.Kind == StageObjectiveKind.CompleteTarget ? objective.Target.IsValid && objective.MinimumConnectionLength == 0
                : objective.Kind == StageObjectiveKind.CompleteLongConnection ? !objective.Target.IsValid && objective.MinimumConnectionLength >= 3
                : objective.Kind == StageObjectiveKind.EarnRestorationEnergy ? !objective.Target.IsValid && objective.MinimumConnectionLength == 0 && !objective.ObstacleKind.HasValue
                : objective.Kind == StageObjectiveKind.RemoveObstacle && !objective.Target.IsValid && objective.MinimumConnectionLength == 0 && objective.ObstacleKind.HasValue;
        }
        private static string ObjectiveKey(StageObjectiveDefinition objective)
            => $"{objective.Kind}:{objective.Target.Value}:{objective.MinimumConnectionLength}:{objective.ObstacleKind}";
        private static bool ValidScore(ScoreRewardConfig score)
        {
            if (score.BaseCorrectScore < 0 || score.PerfectBonus < 0 || score.FastBonus < 0 || score.NormalBonus < 0 || score.LengthRules == null) return false;
            var thresholds = new HashSet<int>();
            return score.LengthRules.All(rule => rule.MinimumLength >= 2 && rule.Bonus >= 0 && thresholds.Add(rule.MinimumLength));
        }
    }
}
