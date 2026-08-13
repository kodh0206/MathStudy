using System;
using MathGame.ObstacleFlow;

namespace MathGame.Presentation
{
    public sealed class PresentationAcknowledgement
    {
        public PresentationAcknowledgement(PresentationSequenceId sequenceId, GameplayStateToken token,
            PresentationAcknowledgementKind kind, long sourceId)
        { SequenceId = sequenceId; Token = token; Kind = kind; SourceId = sourceId; }
        public PresentationSequenceId SequenceId { get; }
        public GameplayStateToken Token { get; }
        public PresentationAcknowledgementKind Kind { get; }
        public long SourceId { get; }
    }

    public interface IGameplayCommandPort
    {
        GameplayStateToken CurrentToken { get; }
        bool IsStageTerminated { get; }
        GameplayCommandResult BeginPath(PathCommandRequest request);
        GameplayCommandResult ExtendPath(PathCommandRequest request);
        GameplayCommandResult ReleasePath(ReleasePathRequest request);
        GameplayCommandResult CancelPath(PresentationCommandId commandId, GameplayStateToken token);
        GameplayCommandResult RetryTargetRecovery(TargetRetryRequest request);
        GameplayCommandResult ResolveFeverEnd(FeverEndCommandRequest request);
        GameplayCommandResult ResolveFailedDecision(FailedDecisionRequest request);
        PresentationAcknowledgementStatus Acknowledge(PresentationAcknowledgement acknowledgement);
    }

    public interface IPresentationPlan
    {
        PresentationEnvelope Envelope { get; }
        PresentationSettings Settings { get; }
    }

    public interface IPresentationViewPort
    {
        void Prepare(IPresentationPlan plan);
        void CancelPlayback();
        void ApplyFinalState(IPresentationPlan plan);
        void TearDown();
    }

    public sealed class PresentationPlan : IPresentationPlan
    {
        public PresentationPlan(PresentationEnvelope envelope, PresentationSettings settings)
        { Envelope = envelope ?? throw new ArgumentNullException(nameof(envelope)); Settings = settings ?? throw new ArgumentNullException(nameof(settings)); }
        public PresentationEnvelope Envelope { get; }
        public PresentationSettings Settings { get; }
    }

    public sealed class GameplayPresentationCoordinator : IDisposable
    {
        readonly IGameplayCommandPort gameplay;
        readonly IPresentationViewPort views;
        long lastSequence;
        long lastAcknowledged;
        bool disposed;
        IPresentationPlan active;

        public GameplayPresentationCoordinator(IGameplayCommandPort gameplay, IPresentationViewPort views)
        { this.gameplay = gameplay ?? throw new ArgumentNullException(nameof(gameplay)); this.views = views ?? throw new ArgumentNullException(nameof(views)); }

        public PresentationPhase Phase { get; private set; } = PresentationPhase.Idle;
        public PresentationEnvelope ActiveEnvelope => active?.Envelope;

        public PresentationCommandStatus Prepare(IPresentationPlan plan)
        {
            if (disposed) return PresentationCommandStatus.Disposed;
            if (plan?.Envelope == null) return PresentationCommandStatus.MissingRequest;
            if (gameplay.IsStageTerminated) return PresentationCommandStatus.StageTerminated;
            if (plan.Envelope.Gameplay.Token != gameplay.CurrentToken) return PresentationCommandStatus.StaleGameplayToken;
            if (plan.Envelope.SequenceId.Value <= lastSequence) return PresentationCommandStatus.DuplicateCommand;
            if (plan.Envelope.SequenceId.Value != lastSequence + 1) return PresentationCommandStatus.OutOfOrderCommand;
            if (Phase is not PresentationPhase.Idle) return PresentationCommandStatus.PresentationStillRunning;
            active = plan;
            lastSequence = plan.Envelope.SequenceId.Value;
            Phase = PresentationPhase.Preparing;
            try { views.Prepare(plan); }
            catch
            {
                active = null;
                Phase = PresentationPhase.Faulted;
                return PresentationCommandStatus.DomainRejected;
            }
            if (Phase == PresentationPhase.Preparing) Phase = PresentationPhase.Playing;
            return PresentationCommandStatus.Accepted;
        }

        public PresentationCommandStatus Pause()
        {
            if (disposed) return PresentationCommandStatus.Disposed;
            if (Phase != PresentationPhase.Playing) return PresentationCommandStatus.InvalidStageState;
            Phase = PresentationPhase.Paused;
            return PresentationCommandStatus.Accepted;
        }

        public PresentationCommandStatus ResumeOrReconcile()
        {
            if (disposed) return PresentationCommandStatus.Disposed;
            if (Phase != PresentationPhase.Paused) return PresentationCommandStatus.InvalidStageState;
            if (active.Envelope.Gameplay.Token == gameplay.CurrentToken) { Phase = PresentationPhase.Playing; return PresentationCommandStatus.Accepted; }
            // A stale plan must never reconcile or acknowledge an older Board. The caller must prepare a fresh snapshot plan.
            active = null;
            Phase = PresentationPhase.Idle;
            return PresentationCommandStatus.StaleGameplayToken;
        }

        public PresentationAcknowledgementStatus CompletePlayback() => ReconcileAndAcknowledge(true);

        public PresentationCommandStatus CancelBeforeReconcile()
        {
            if (disposed) return PresentationCommandStatus.Disposed;
            if (active == null) return PresentationCommandStatus.MissingRequest;
            try
            {
                views.CancelPlayback();
                Phase = PresentationPhase.Reconciling;
                views.ApplyFinalState(active);
            }
            catch { Phase = PresentationPhase.Faulted; return PresentationCommandStatus.DomainRejected; }
            Phase = PresentationPhase.AwaitingAcknowledgement;
            return PresentationCommandStatus.Accepted;
        }

        PresentationAcknowledgementStatus ReconcileAndAcknowledge(bool requireCurrentToken)
        {
            if (disposed) return PresentationAcknowledgementStatus.Disposed;
            if (active == null) return PresentationAcknowledgementStatus.MissingAcknowledgement;
            if (gameplay.IsStageTerminated) return PresentationAcknowledgementStatus.StageTerminated;
            if (requireCurrentToken && active.Envelope.Gameplay.Token != gameplay.CurrentToken) return PresentationAcknowledgementStatus.StaleGameplayToken;
            if (active.Envelope.SequenceId.Value <= lastAcknowledged) return PresentationAcknowledgementStatus.DuplicateAcknowledgement;

            // This block is deliberately synchronous and non-cancellable: there is no callback/await gap.
            Phase = PresentationPhase.Reconciling;
            try { views.ApplyFinalState(active); }
            catch { Phase = PresentationPhase.Faulted; return PresentationAcknowledgementStatus.WrongPhase; }
            var acknowledgement = new PresentationAcknowledgement(active.Envelope.SequenceId, gameplay.CurrentToken,
                active.Envelope.AcknowledgementKind, active.Envelope.SourceId);
            var status = gameplay.Acknowledge(acknowledgement);
            if (status == PresentationAcknowledgementStatus.Accepted)
            {
                lastAcknowledged = active.Envelope.SequenceId.Value;
                active = null;
                Phase = PresentationPhase.Idle;
            }
            else Phase = PresentationPhase.Faulted;
            return status;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            active = null;
            views.TearDown();
            Phase = PresentationPhase.Disposed;
        }
    }
}
