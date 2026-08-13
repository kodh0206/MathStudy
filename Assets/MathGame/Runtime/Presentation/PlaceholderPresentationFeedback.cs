using System.Collections.Generic;
using UnityEngine;

namespace MathGame.Presentation.Unity
{
    public sealed class PlaceholderPresentationFeedback : MonoBehaviour, IPresentationFeedbackPort
    {
        readonly List<PresentationFeedbackCue> played=new List<PresentationFeedbackCue>();
        public IReadOnlyList<PresentationFeedbackCue> Played=>played.AsReadOnly();
        public void Play(PresentationFeedbackCue cue,bool audioEnabled,bool hapticsEnabled)
        {
            played.Add(cue);
            // Placeholder-only: no concrete asset identifier enters domain or contracts.
            if(hapticsEnabled&&Application.isMobilePlatform)Handheld.Vibrate();
        }
    }
}
