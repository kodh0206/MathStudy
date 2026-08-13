using System;
using UnityEngine;

namespace MathGame.Presentation.Unity
{
    public sealed class GameplayPresentationDriver : MonoBehaviour
    {
        GameplayPresentationCoordinator coordinator;
        GameplayPresentationRoot root;
        public PresentationAcknowledgementStatus LastAcknowledgementStatus { get; private set; }
        public void Initialize(GameplayPresentationCoordinator value, GameplayPresentationRoot presentationRoot)
        {
            if(coordinator!=null)throw new InvalidOperationException("Already initialized.");
            coordinator=value??throw new ArgumentNullException(nameof(value));root=presentationRoot??throw new ArgumentNullException(nameof(presentationRoot));root.PlaybackCompleted+=Completed;
        }
        public void Pause(){root?.SetPaused(true);coordinator?.Pause();}
        public void Resume(){root?.SetPaused(false);coordinator?.ResumeOrReconcile();}
        void Completed(){if(coordinator!=null)LastAcknowledgementStatus=coordinator.CompletePlayback();}
        void OnDestroy(){if(root!=null)root.PlaybackCompleted-=Completed;coordinator?.Dispose();coordinator=null;root=null;}
    }
}
