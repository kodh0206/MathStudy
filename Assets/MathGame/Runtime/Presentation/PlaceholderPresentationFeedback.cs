using System.Collections.Generic;
using UnityEngine;

namespace MathGame.Presentation.Unity
{
    public sealed class PlaceholderPresentationFeedback : MonoBehaviour, IPresentationFeedbackPort
    {
        readonly List<PresentationFeedbackCue> played=new List<PresentationFeedbackCue>();
        AudioSource source;
        readonly Dictionary<PresentationFeedbackCue,AudioClip> clips=new Dictionary<PresentationFeedbackCue,AudioClip>();
        public IReadOnlyList<PresentationFeedbackCue> Played=>played.AsReadOnly();
        void Awake()
        {
            source=GetComponent<AudioSource>();
            if(source==null)source=gameObject.AddComponent<AudioSource>();
            if(source==null)return;
            source.playOnAwake=false;
            source.volume=.22f;
            source.spatialBlend=0f;
        }
        public void Play(PresentationFeedbackCue cue,bool audioEnabled,bool hapticsEnabled)
        {
            played.Add(cue);
            if(audioEnabled&&source!=null)
            {
                var clip=GetClip(cue);
                if(clip!=null)source.PlayOneShot(clip);
            }
            VibrateIfSupported(hapticsEnabled);
        }

        static void VibrateIfSupported(bool enabled)
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            if (enabled) Handheld.Vibrate();
#endif
        }
        AudioClip GetClip(PresentationFeedbackCue cue)
        {
            if(clips.TryGetValue(cue,out var clip))return clip;
            var frequency=cue switch
            {
                PresentationFeedbackCue.Selection=>520f, PresentationFeedbackCue.Correct=>760f,
                PresentationFeedbackCue.Miss=>180f, PresentationFeedbackCue.ObstacleDamaged=>300f,
                PresentationFeedbackCue.ObstacleDestroyed=>420f, PresentationFeedbackCue.FeverEntry=>900f,
                PresentationFeedbackCue.FeverEnd=>620f, PresentationFeedbackCue.Success=>1040f,
                PresentationFeedbackCue.Failure=>140f, _=>680f
            };
            const int rate=22050;const float duration=.075f;var samples=new float[(int)(rate*duration)];
            for(var i=0;i<samples.Length;i++){var fade=1f-i/(float)samples.Length;samples[i]=Mathf.Sin(2f*Mathf.PI*frequency*i/rate)*fade*.35f;}
            clip=AudioClip.Create("Prototype_"+cue,samples.Length,1,rate,false);clip.SetData(samples,0);clips[cue]=clip;return clip;
        }
    }
}
