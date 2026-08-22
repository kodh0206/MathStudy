using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MathGame.Presentation.Unity
{
    /// <summary>Presentation-only binding for the continuous-run HUD.</summary>
    public sealed class RunHUDView : MonoBehaviour
    {
        [SerializeField] GameObject content;
        [SerializeField] Text targetValue;
        [SerializeField] Text timeValue;
        [SerializeField] Image timeFill;
        [SerializeField] Text scoreValue;
        [SerializeField] Text comboValue;
        [SerializeField] Text tierValue;
        [SerializeField] Text feverValue;
        [SerializeField] Image feverFill;
        bool critical;
        Coroutine timePulse;
        Coroutine targetPulse;

        static readonly Color Cyan = new Color(.18f, .88f, 1f, 1f);
        static readonly Color Warning = new Color(1f, .62f, .20f, 1f);
        static readonly Color Critical = new Color(1f, .24f, .22f, 1f);

        public bool IsComplete => content != null && targetValue != null && timeValue != null && timeFill != null &&
            scoreValue != null && comboValue != null && tierValue != null && feverValue != null && feverFill != null;

        public void SetVisible(bool value)
        {
            if (content != null) content.SetActive(value);
            if (!value) ResetTransientState();
        }

        public void Present(int target, double remainingTime, double maximumTime, long score, int combo,
            int tier, int fever, int maximumFever)
        {
            EnsureProgressBars();
            SetLabel("TargetPanel/Label", "gameplay.label.target");
            SetLabel("SurvivalPanel/Label", "gameplay.label.time");
            targetValue.text = target.ToString();
            timeValue.text = Math.Max(0, remainingTime).ToString("0.0");
            scoreValue.text = MathGameLocalization.Get("Gameplay", "gameplay.score", score);
            comboValue.text = MathGameLocalization.Get("Gameplay", "gameplay.combo", Math.Max(0, combo));
            tierValue.text = MathGameLocalization.Get("Gameplay", "gameplay.tier", Math.Max(1, tier + 1));
            feverValue.text = MathGameLocalization.Get("Gameplay",
                fever >= maximumFever ? "gameplay.label.overdrive" : "gameplay.label.fever");

            var timeRatio = maximumTime <= 0 ? 0 : Mathf.Clamp01((float)(remainingTime / maximumTime));
            SetNormalizedProgress(timeFill, timeRatio);
            var timeColor = timeRatio <= .15f ? Critical : timeRatio <= .35f ? Warning : Cyan;
            critical = timeRatio > 0 && timeRatio <= .15f;
            timeFill.color = timeColor;
            timeValue.color = timeColor;
            SetNormalizedProgress(feverFill,
                maximumFever <= 0 ? 0 : Mathf.Clamp01(fever / (float)maximumFever));
            feverFill.color = fever >= maximumFever
                ? new Color(1f, .78f, .22f, 1f)
                : new Color(.96f, .48f, .12f, 1f);
        }

        void EnsureProgressBars()
        {
            ConfigureHorizontalFill(timeFill);
            ConfigureHorizontalFill(feverFill);
        }

        static void ConfigureHorizontalFill(Image image)
        {
            if (image == null) return;
            image.enabled = true;
            image.raycastTarget = false;
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = (int)Image.OriginHorizontal.Left;
            image.fillClockwise = true;
            if (image.transform.parent != null)
                image.transform.parent.gameObject.SetActive(true);
        }

        static void SetNormalizedProgress(Image image, float value)
        {
            if (image == null) return;
            var normalized = Mathf.Clamp01(value);
            image.fillAmount = normalized;

            // Unity does not generate Filled geometry for every sprite-less Image
            // configuration. Managed placeholder gauges intentionally have no art
            // sprite, so crop their RectTransform as a deterministic fallback.
            var rect = image.rectTransform;
            if (image.sprite == null)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = new Vector2(normalized, 1f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                image.enabled = normalized > 0f;
            }
            else
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                image.enabled = true;
            }
        }

        public void PulseTimeGain()
        {
            if (timeValue == null || !isActiveAndEnabled) return;
            if (timePulse != null) StopCoroutine(timePulse);
            timePulse = StartCoroutine(Pulse(timeValue.rectTransform, 1.14f, .18f));
        }

        public void PulseTarget()
        {
            if (targetValue == null || !isActiveAndEnabled) return;
            if (targetPulse != null) StopCoroutine(targetPulse);
            targetPulse = StartCoroutine(Pulse(targetValue.rectTransform, 1.12f, .20f));
        }

        void Update()
        {
            if (timeValue == null || timePulse != null) return;
            timeValue.rectTransform.localScale = critical
                ? Vector3.one * (1f + .035f * (1f + Mathf.Sin(Time.unscaledTime * 10f)))
                : Vector3.one;
        }

        IEnumerator Pulse(RectTransform value, float maximum, float duration)
        {
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var wave = Mathf.Sin(Mathf.Clamp01(elapsed / duration) * Mathf.PI);
                value.localScale = Vector3.one * Mathf.Lerp(1f, maximum, wave);
                yield return null;
            }
            value.localScale = Vector3.one;
            if (value == timeValue.rectTransform) timePulse = null;
            if (value == targetValue.rectTransform) targetPulse = null;
        }

        void ResetTransientState()
        {
            if (timePulse != null) StopCoroutine(timePulse);
            if (targetPulse != null) StopCoroutine(targetPulse);
            timePulse = targetPulse = null;
            if (timeValue != null) timeValue.rectTransform.localScale = Vector3.one;
            if (targetValue != null) targetValue.rectTransform.localScale = Vector3.one;
        }

        void OnDisable() => ResetTransientState();

        void SetLabel(string path, string key)
        {
            var label = content != null ? content.transform.Find(path)?.GetComponent<Text>() : null;
            if (label != null) label.text = MathGameLocalization.Get("Gameplay", key);
        }

#if UNITY_EDITOR
        public void Configure(GameObject root, Text target, Text time, Image timeGauge, Text score, Text combo,
            Text tier, Text fever, Image feverGauge)
        {
            content = root;
            targetValue = target;
            timeValue = time;
            timeFill = timeGauge;
            scoreValue = score;
            comboValue = combo;
            tierValue = tier;
            feverValue = fever;
            feverFill = feverGauge;
        }
#endif
    }
}
