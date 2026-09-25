using System;
using System.Collections.Generic;
using MathGame.Board;
using MathGame.BoardResolution;
using MathGame.ObstacleFlow;
using MathGame.Restoration;
using MathGame.Fever;

namespace MathGame.Presentation
{
    public enum PresentationEventKind
    {
        RemoveSelected, RemoveCollateral, DamageObstacle, DestroyObstacle,
        MoveBlock, SpawnBlock, ReconfigurationStart, ShuffleBlock, ReconfigurationComplete, PresentTarget, RestorationMilestone,
        Miss, FeverEntry, FeverClearScore, FeverEndAnnouncement, FeverEndTelegraph, FeverEndWave, FeverEndFade,
        FeverEnd, StageSuccess, StageFailure, Reconcile
    }

    public readonly struct PresentationEvent
    {
        public PresentationEvent(PresentationEventKind kind, BoardPosition position, long identity, BoardPosition? from = null)
        { Kind = kind; Position = position; Identity = identity; From = from; }
        public PresentationEventKind Kind { get; }
        public BoardPosition Position { get; }
        public long Identity { get; }
        public BoardPosition? From { get; }
    }

    public sealed class ObstaclePresentationPlan : IPresentationPlan
    {
        internal ObstaclePresentationPlan(PresentationEnvelope envelope, PresentationSettings settings, List<PresentationEvent> events, bool replaysAnswer)
        { Envelope = envelope; Settings = settings; Events = events.AsReadOnly(); ReplaysAnswer = replaysAnswer; }
        public PresentationEnvelope Envelope { get; }
        public PresentationSettings Settings { get; }
        public IReadOnlyList<PresentationEvent> Events { get; }
        public bool ReplaysAnswer { get; }
    }

    public static class ObstaclePresentationPlanBuilder
    {
        public static ObstaclePresentationPlan ForMiss(PresentationEnvelope envelope, PresentationSettings settings)
        {
            if (envelope == null || settings == null || envelope.AcknowledgementKind != PresentationAcknowledgementKind.Answer)
                throw new ArgumentException("A miss acknowledgement envelope is required.");
            return new ObstaclePresentationPlan(envelope, settings,
                new List<PresentationEvent>
                {
                    new PresentationEvent(PresentationEventKind.Miss, default, envelope.SourceId),
                    new PresentationEvent(PresentationEventKind.Reconcile, default, envelope.Gameplay.Token.Revision)
                }, false);
        }

        public static ObstaclePresentationPlan ForTerminal(PresentationEnvelope envelope, PresentationSettings settings, bool success)
        {
            if (envelope == null || settings == null) throw new ArgumentNullException();
            return new ObstaclePresentationPlan(envelope, settings,
                new List<PresentationEvent>
                {
                    new PresentationEvent(success ? PresentationEventKind.StageSuccess : PresentationEventKind.StageFailure, default, envelope.SourceId),
                    new PresentationEvent(PresentationEventKind.Reconcile, default, envelope.Gameplay.Token.Revision)
                }, false);
        }
        public static ObstaclePresentationPlan ForAnswer(PresentationEnvelope envelope, PresentationSettings settings, ObstacleAnswerFlowResult result)
        {
            if (envelope == null || settings == null || result?.ResolutionResult == null || !result.ResolutionResult.Succeeded)
                throw new ArgumentException("A committed obstacle result is required.");
            var events = ResolutionEvents(result.ResolutionResult);
            var feverRemovalScore = result.StageResult?.Reward.FeverRemovalScoreAwarded ?? 0;
            if (feverRemovalScore > 0)
                events.Add(new PresentationEvent(PresentationEventKind.FeverClearScore, default, feverRemovalScore));
            if (result.TargetResult?.BoardChanged == true)
            {
                events.Add(new PresentationEvent(PresentationEventKind.ReconfigurationStart, default, result.TargetResult.ShuffleAttemptCount));
                foreach (var delta in result.TargetResult.Deltas)
                    events.Add(new PresentationEvent(PresentationEventKind.ShuffleBlock, delta.To, delta.Block.Id.Value, delta.From));
                events.Add(new PresentationEvent(PresentationEventKind.ReconfigurationComplete, default, result.SelectedTarget.Target.Value));
            }
            if (result.SelectedTarget != null) events.Add(new PresentationEvent(PresentationEventKind.PresentTarget, default, result.SelectedTarget.Target.Value));
            events.Add(new PresentationEvent(PresentationEventKind.Reconcile, default, envelope.Gameplay.Token.Revision));
            return new ObstaclePresentationPlan(envelope, settings, events, true);
        }

        public static ObstaclePresentationPlan ForTargetRetry(PresentationEnvelope envelope, PresentationSettings settings, ObstacleAnswerFlowResult result)
        {
            if (envelope == null || settings == null || result?.TargetResult == null || !result.IsInputReady)
                throw new ArgumentException("A successful target retry is required.");
            var events = new List<PresentationEvent>();
            if (result.TargetResult.BoardChanged)
            {
                events.Add(new PresentationEvent(PresentationEventKind.ReconfigurationStart, default, result.TargetResult.ShuffleAttemptCount));
                foreach (var delta in result.TargetResult.Deltas)
                    events.Add(new PresentationEvent(PresentationEventKind.ShuffleBlock, delta.To, delta.Block.Id.Value, delta.From));
                events.Add(new PresentationEvent(PresentationEventKind.ReconfigurationComplete, default, result.SelectedTarget.Target.Value));
            }
            events.Add(new PresentationEvent(PresentationEventKind.PresentTarget, default, result.SelectedTarget.Target.Value));
            events.Add(new PresentationEvent(PresentationEventKind.Reconcile, default, envelope.Gameplay.Token.Revision));
            return new ObstaclePresentationPlan(envelope, settings, events, false);
        }

        public static ObstaclePresentationPlan ForFeverEnd(PresentationEnvelope envelope, PresentationSettings settings,
            ObstacleEndFlowResult result, FeverEndEffectTier tier)
        {
            if(envelope==null||settings==null||result?.ResolutionResult==null||!result.ResolutionResult.Succeeded)
                throw new ArgumentException("A committed Fever-end result is required.");
            var events=FeverEndEvents(result.ResolutionResult, tier, result.SystemEffectResult?.FeverRemovalScoreAwarded ?? 0);
            if(result.TargetResult?.BoardChanged==true)
            {
                events.Add(new PresentationEvent(PresentationEventKind.ReconfigurationStart,default,result.TargetResult.ShuffleAttemptCount));
                foreach(var delta in result.TargetResult.Deltas)events.Add(new PresentationEvent(PresentationEventKind.ShuffleBlock,delta.To,delta.Block.Id.Value,delta.From));
                events.Add(new PresentationEvent(PresentationEventKind.ReconfigurationComplete,default,result.SelectedTarget.Target.Value));
            }
            if(result.SelectedTarget!=null)events.Add(new PresentationEvent(PresentationEventKind.PresentTarget,default,result.SelectedTarget.Target.Value));
            events.Add(new PresentationEvent(PresentationEventKind.Reconcile,default,envelope.Gameplay.Token.Revision));
            return new ObstaclePresentationPlan(envelope,settings,events,false);
        }

        public static List<PresentationEvent> FeverEndEvents(ObstacleResolutionResult resolution, FeverEndEffectTier tier, long feverRemovalScore = 0)
        {
            if (resolution == null || !resolution.Succeeded)
                throw new ArgumentException("A successful committed Fever-end resolution is required.", nameof(resolution));
            var events=new List<PresentationEvent>
            {
                new PresentationEvent(PresentationEventKind.FeverEndAnnouncement, default, (long)tier)
            };
            if (tier == FeverEndEffectTier.None)
                events.Add(new PresentationEvent(PresentationEventKind.FeverEndFade, default, (long)tier));
            else
            {
                foreach (var delta in resolution.Removed)
                    events.Add(new PresentationEvent(PresentationEventKind.FeverEndTelegraph, delta.Position,
                        delta.Block.Id.Value));
                events.Add(new PresentationEvent(PresentationEventKind.FeverEndWave, default, (long)tier));
            }
            if (feverRemovalScore > 0)
                events.Add(new PresentationEvent(PresentationEventKind.FeverClearScore, default, feverRemovalScore));
            events.AddRange(ResolutionEvents(resolution));
            return events;
        }

        public static ObstaclePresentationPlan ForTerminalFeverEnd(PresentationEnvelope envelope,
            PresentationSettings settings, ObstacleEndFlowResult result, FeverEndEffectTier tier)
        {
            if (envelope == null || settings == null || result?.ResolutionResult == null ||
                !result.ResolutionResult.Succeeded || envelope.AcknowledgementKind != PresentationAcknowledgementKind.Terminal)
                throw new ArgumentException("A committed terminal Fever-end result is required.");
            var events = FeverEndEvents(result.ResolutionResult, tier, result.SystemEffectResult?.FeverRemovalScoreAwarded ?? 0);
            events.Add(new PresentationEvent(PresentationEventKind.StageSuccess, default, envelope.SourceId));
            events.Add(new PresentationEvent(PresentationEventKind.Reconcile, default, envelope.Gameplay.Token.Revision));
            return new ObstaclePresentationPlan(envelope, settings, events, false);
        }

        public static IReadOnlyList<PresentationEvent> ForWorldCommit(WorldRestorationCommitResult result, ExactlyOnceMilestoneTracker tracker)
        {
            var output = new List<PresentationEvent>();
            if (result?.Plan == null || tracker == null) return output.AsReadOnly();
            foreach (var milestone in result.Plan.CrossedMilestones)
                if (tracker.TryMark(result, milestone)) output.Add(new PresentationEvent(PresentationEventKind.RestorationMilestone, default, (int)milestone));
            return output.AsReadOnly();
        }

        static List<PresentationEvent> ResolutionEvents(ObstacleResolutionResult result)
        {
            var output = new List<PresentationEvent>();
            foreach (var delta in result.SelectedRemoved) output.Add(new PresentationEvent(PresentationEventKind.RemoveSelected, delta.Position, delta.Block.Id.Value));
            foreach (var delta in result.CollateralRemoved) output.Add(new PresentationEvent(PresentationEventKind.RemoveCollateral, delta.Position, delta.Block.Id.Value));
            foreach (var delta in result.ObstacleDamage)
            {
                output.Add(new PresentationEvent(PresentationEventKind.DamageObstacle, delta.Position, delta.Id.Value));
                if (delta.WasDestroyed) output.Add(new PresentationEvent(PresentationEventKind.DestroyObstacle, delta.Position, delta.Id.Value));
            }
            foreach (var delta in result.Moved) output.Add(new PresentationEvent(PresentationEventKind.MoveBlock, delta.To, delta.Block.Id.Value, delta.From));
            foreach (var delta in result.Spawned) output.Add(new PresentationEvent(PresentationEventKind.SpawnBlock, delta.Destination, delta.Block.Id.Value));
            return output;
        }
    }
}
