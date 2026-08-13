using System;
using MathGame.Answer;
using MathGame.Board;
using MathGame.BoardResolution;
using MathGame.Connection;
using MathGame.ObstacleFlow;
using MathGame.Restoration.Contracts;
using MathGame.StageSession;
using MathGame.Targets;

namespace MathGame.Presentation
{
    public sealed class PathCommandRequest
    {
        public PathCommandRequest(PresentationCommandId commandId, GameplayStateToken token, BoardPosition position)
        { CommandId = commandId; Token = token; Position = position; }
        public PresentationCommandId CommandId { get; }
        public GameplayStateToken Token { get; }
        public BoardPosition Position { get; }
    }

    public sealed class ReleasePathRequest
    {
        public ReleasePathRequest(PresentationCommandId commandId, GameplayStateToken token, TargetNumber target,
            double elapsedSeconds, StageAttemptId attemptId, RefillValueRange refill, TargetHistory history,
            TargetRecoveryConfig targetConfig, bool feverMode)
        { CommandId=commandId;Token=token;Target=target;ElapsedSeconds=elapsedSeconds;AttemptId=attemptId;Refill=refill;History=history;TargetConfig=targetConfig;FeverMode=feverMode; }
        public PresentationCommandId CommandId { get; }
        public GameplayStateToken Token { get; }
        public TargetNumber Target { get; }
        public double ElapsedSeconds { get; }
        public StageAttemptId AttemptId { get; }
        public RefillValueRange Refill { get; }
        public TargetHistory History { get; }
        public TargetRecoveryConfig TargetConfig { get; }
        public bool FeverMode { get; }
    }

    public sealed class TargetRetryRequest
    {
        public TargetRetryRequest(PresentationCommandId commandId, GameplayStateToken token, TargetHistory history, TargetRecoveryConfig config)
        { CommandId=commandId;Token=token;History=history;Config=config; }
        public PresentationCommandId CommandId{get;} public GameplayStateToken Token{get;}
        public TargetHistory History{get;} public TargetRecoveryConfig Config{get;}
    }

    public sealed class FeverEndCommandRequest
    {
        public FeverEndCommandRequest(PresentationCommandId commandId, GameplayStateToken token, RefillValueRange refill,
            TargetHistory history, TargetRecoveryConfig config)
        {CommandId=commandId;Token=token;Refill=refill;History=history;Config=config;}
        public PresentationCommandId CommandId{get;} public GameplayStateToken Token{get;}
        public RefillValueRange Refill{get;} public TargetHistory History{get;} public TargetRecoveryConfig Config{get;}
    }

    public enum FailedDecisionChoice { Continue, Retry, Abandon }
    public sealed class FailedDecisionRequest
    {
        public FailedDecisionRequest(PresentationCommandId commandId, GameplayStateToken token, FailedDecisionChoice choice,
            ContinueGrant continueGrant = null, StageDefinition retryDefinition = null)
        { CommandId=commandId;Token=token;Choice=choice;ContinueGrant=continueGrant;RetryDefinition=retryDefinition; }
        public PresentationCommandId CommandId{get;} public GameplayStateToken Token{get;}
        public FailedDecisionChoice Choice{get;} public ContinueGrant ContinueGrant{get;} public StageDefinition RetryDefinition{get;}
    }

    public sealed class GameplayCommandResult
    {
        public GameplayCommandResult(PresentationCommandStatus status, GameplayStateToken token,
            ConnectionStepResult? step = null, ConnectionPathSnapshot path = null, AnswerResult answer = null,
            ObstacleAnswerFlowResult answerFlow = null, MathGame.ObstacleFlow.FailedDecisionResult failedDecision = null,
            ObstacleEndFlowResult endFlow = null)
        { Status=status;Token=token;Step=step;Path=path;Answer=answer;AnswerFlow=answerFlow;FailedDecision=failedDecision;EndFlow=endFlow; }
        public PresentationCommandStatus Status{get;} public GameplayStateToken Token{get;}
        public ConnectionStepResult? Step{get;} public ConnectionPathSnapshot Path{get;} public AnswerResult Answer{get;}
        public ObstacleAnswerFlowResult AnswerFlow{get;} public MathGame.ObstacleFlow.FailedDecisionResult FailedDecision{get;}
        public ObstacleEndFlowResult EndFlow{get;}
    }
}
