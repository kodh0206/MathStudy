using System;
using System.Collections;
using System.Collections.Generic;
using MathGame.StageSession;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace MathGame.Presentation.Unity
{
    /// <summary>Presentation-only responsive HUD and board viewport for the prototype.</summary>
    public sealed class PrototypeUILayout : MonoBehaviour
    {
        const float ReferenceWidth = 1080f;
        const float ReferenceHeight = 1920f;
        readonly List<Text> objectives = new List<Text>();
        Canvas canvas;
        RectTransform safeArea;
        Text target;
        Text moves;
        Text score;
        Text restoration;
        Text fever;
        Text status;
        Text selectionSum;
        Text runTime;
        Text runFever;
        Text runCombo;
        Text runTier;
        GameObject runStats;
        RunHUDView runHud;
        Button targetRetryButton;
        Button languageButton;
        Transform objectiveContainer;
        MathGamePrefabRegistry prefabRegistry;
        Camera boardCamera;
        GameplayPresentationRoot boardView;
        Rect lastSafeArea;
        int lastScreenWidth;
        int lastScreenHeight;
        bool runMode;
        readonly Dictionary<RectTransform, Coroutine> pulses = new Dictionary<RectTransform, Coroutine>();
        int displayedTarget = int.MinValue;
        int displayedCombo;
        int displayedGauge = -1;
        double displayedTime = double.NaN;
        [SerializeField, Min(0)] float lowTimeWarningSeconds = 8f;
        static readonly Color LowTimeColor = new Color(1f, .42f, .32f, 1f);
        Vector2 selectionSumBaseline;
        bool hasSelectionSumBaseline;
        string transientFeedback;
        float transientFeedbackUntil;

        void OnEnable() => LocalizationSettings.SelectedLocaleChanged += LocaleChanged;
        void OnDisable()
        {
            LocalizationSettings.SelectedLocaleChanged -= LocaleChanged;
            ResetPolish();
        }
        void LocaleChanged(Locale _) => RefreshLocalizedControls();

        void RefreshLocalizedControls()
        {
            var languageLabel = languageButton?.GetComponentInChildren<Text>();
            if (languageLabel != null) languageLabel.text = MathGameLocalization.Get("Settings", "settings.language_button");
        }

        public void Build(Camera camera, GameplayPresentationRoot serializedBoardView, Action onTargetRetry,
            Action onLanguage, MathGamePrefabRegistry registry=null)
        {
            boardCamera = camera;
            boardView = serializedBoardView;
            prefabRegistry=registry;
            EnsureEventSystem();
            var existingCanvas = GetComponent<Canvas>();
            if (existingCanvas != null && transform.Find("SafeArea/TopSlot/HUD") != null)
            {
                BindPrefabHierarchy(onTargetRetry,onLanguage);
                ValidateBoundHierarchy();
                ConfigureResponsiveHud();
                ApplySafeArea(true);
                return;
            }
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;
            gameObject.AddComponent<GraphicRaycaster>();

            safeArea = Rect("SafeArea", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var hud = Panel("HUD", safeArea, new Color(.035f, .055f, .09f, .96f),
                new Vector2(0, 1), Vector2.one, new Vector2(24, -390), new Vector2(-24, -24));
            var title = Label("Title", hud, "MATH GAME PROTOTYPE", 38, TextAnchor.MiddleCenter, FontStyle.Bold);
            SetRect(title.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(20, -72), new Vector2(-20, -14));

            var mainStats = Rect("MainStats", hud, new Vector2(0, 1), Vector2.one, new Vector2(24, -184), new Vector2(-24, -82));
            var statsGrid = mainStats.gameObject.AddComponent<GridLayoutGroup>();
            statsGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            statsGrid.constraintCount = 3;
            statsGrid.cellSize = new Vector2(320, 92);
            statsGrid.spacing = new Vector2(20, 0);
            statsGrid.childAlignment = TextAnchor.MiddleCenter;
            target = Stat(mainStats, "Target");
            moves = Stat(mainStats, "Moves");
            score = Stat(mainStats, "Score");

            var resources = Rect("Resources", hud, new Vector2(0, 1), Vector2.one, new Vector2(24, -264), new Vector2(-24, -194));
            restoration = Label("Restoration", resources, "Restoration 0/0", 29, TextAnchor.MiddleLeft, FontStyle.Normal);
            SetRect(restoration.rectTransform, new Vector2(0, 0), new Vector2(.5f, 1), Vector2.zero, new Vector2(-10, 0));
            fever = Label("Fever", resources, "Fever 0/0", 29, TextAnchor.MiddleRight, FontStyle.Normal);
            SetRect(fever.rectTransform, new Vector2(.5f, 0), Vector2.one, new Vector2(10, 0), Vector2.zero);

            var objectiveRoot = Rect("Objectives", hud, new Vector2(0, 0), Vector2.one, new Vector2(24, 28), new Vector2(-24, 116));
            objectiveContainer=objectiveRoot;
            var objectiveLayout = objectiveRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            objectiveLayout.spacing = 6;
            objectiveLayout.childControlHeight = true;
            objectiveLayout.childForceExpandHeight = true;

            var bottom = Panel("BottomHUD", safeArea, new Color(.035f, .055f, .09f, .96f),
                Vector2.zero, new Vector2(1, 0), new Vector2(24, 24), new Vector2(-24, 280));
            status = Label("Status", bottom, "Starting prototype...", 26, TextAnchor.UpperCenter, FontStyle.Normal);
            SetRect(status.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(24, -104), new Vector2(-24, -16));
            selectionSum = Label("SelectionSum", bottom, "SELECTED SUM  0", 30, TextAnchor.MiddleCenter, FontStyle.Bold);
            SetRect(selectionSum.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(24, -154), new Vector2(-24, -104));
            var actions = Rect("Actions", bottom, Vector2.zero, Vector2.one, new Vector2(20, 18), new Vector2(-20, -116));
            var actionLayout = actions.gameObject.AddComponent<HorizontalLayoutGroup>();
            actionLayout.spacing = 12;
            actionLayout.childControlWidth = true;
            actionLayout.childForceExpandWidth = true;
            targetRetryButton = Action(actions, "Retry Target", onTargetRetry);
            languageButton = Action(actions, "Language", onLanguage);
            ApplySafeArea(true);
        }

        void EnsureEventSystem()
        {
            if(FindFirstObjectByType<EventSystem>()!=null)return;
            var events=new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));events.transform.SetParent(transform.parent,false);
        }

        void BindPrefabHierarchy(Action onTargetRetry,Action onLanguage)
        {
            canvas=GetComponent<Canvas>();safeArea=transform.Find("SafeArea") as RectTransform;
            target=FindText("SafeArea/TopSlot/HUD/MainStats/Target/Value");moves=FindText("SafeArea/TopSlot/HUD/MainStats/Moves/Value");
            score=FindText("SafeArea/TopSlot/HUD/MainStats/Score/Value");restoration=FindText("SafeArea/TopSlot/HUD/Resources/Restoration");
            fever=FindText("SafeArea/TopSlot/HUD/Resources/Fever");status=FindText("SafeArea/BottomSlot/BottomHUD/Status");
            runStats=transform.Find("SafeArea/TopSlot/HUD/RunStats")?.gameObject;
            runHud=transform.Find("SafeArea/TopSlot/HUD")?.GetComponent<RunHUDView>();
            runTime=FindText("SafeArea/TopSlot/HUD/RunStats/Time/Value");
            runFever=FindText("SafeArea/TopSlot/HUD/RunStats/Fever/Value");
            runCombo=FindText("SafeArea/TopSlot/HUD/RunStats/Combo/Value");
            runTier=FindText("SafeArea/TopSlot/HUD/RunStats/Tier/Value");
            selectionSum=FindText("SafeArea/BottomSlot/BottomHUD/SelectionSum");
            objectiveContainer=transform.Find("SafeArea/TopSlot/HUD/Objectives");objectives.Clear();
            if(objectiveContainer!=null)foreach(Transform child in objectiveContainer){var value=child.GetComponent<Text>()??child.GetComponentInChildren<Text>();if(value!=null)objectives.Add(value);}
            targetRetryButton=FindButton("SafeArea/BottomSlot/BottomHUD/Actions/RetryTarget");
            languageButton=FindButton("SafeArea/BottomSlot/BottomHUD/Actions/Language");
            Wire(targetRetryButton,onTargetRetry);Wire(languageButton,onLanguage);
        }

        Text FindText(string path)=>transform.Find(path)?.GetComponent<Text>();
        Button FindButton(string path)=>transform.Find(path)?.GetComponent<Button>();
        static void Wire(Button button,Action callback){if(button==null)return;button.onClick.RemoveAllListeners();if(callback!=null)button.onClick.AddListener(()=>callback());}
        void ValidateBoundHierarchy()
        {
            if(safeArea==null||status==null||selectionSum==null||targetRetryButton==null||languageButton==null||runHud?.IsComplete!=true)
                throw new InvalidOperationException("GameRoot/HUD prefab contract is incomplete. Rebuild or migrate the presentation prefab explicitly.");
        }

        void ConfigureResponsiveHud()
        {
            var hud = transform.Find("SafeArea/TopSlot/HUD") as RectTransform;
            var mainStats = transform.Find("SafeArea/TopSlot/HUD/MainStats") as RectTransform;
            var runStatsRect = transform.Find("SafeArea/TopSlot/HUD/RunStats") as RectTransform;
            var resources = transform.Find("SafeArea/TopSlot/HUD/Resources") as RectTransform;
            var objectiveRoot = objectiveContainer as RectTransform;
            if (hud != null) SetRect(hud, new Vector2(0, 1), Vector2.one, new Vector2(24, -404), new Vector2(-24, -24));
            if (mainStats != null) SetRect(mainStats, new Vector2(0, 1), Vector2.one, new Vector2(24, -184), new Vector2(-24, -82));
            if (resources != null) SetRect(resources, new Vector2(0, 1), Vector2.one, new Vector2(24, -274), new Vector2(-24, -194));
            if (runStatsRect != null) SetRect(runStatsRect, new Vector2(0, 1), Vector2.one, new Vector2(24, -374), new Vector2(-24, -194));
            // Fixed prototype HUD placement requested by design: centered at Y 60.
            if (objectiveRoot != null) SetRect(objectiveRoot, Vector2.zero, new Vector2(1, 0), new Vector2(24, 11), new Vector2(-24, 109));

            var stats = mainStats != null ? mainStats.GetComponent<GridLayoutGroup>() : null;
            if (stats != null)
            {
                stats.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                stats.constraintCount = 3;
                stats.spacing = new Vector2(12, 0);
                stats.childAlignment = TextAnchor.MiddleCenter;
            }

            var runGrid = runStatsRect != null ? runStatsRect.GetComponent<GridLayoutGroup>() : null;
            if (runGrid != null)
            {
                runGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                runGrid.constraintCount = 2;
                runGrid.spacing = new Vector2(12, 10);
                runGrid.childAlignment = TextAnchor.MiddleCenter;
            }

            Canvas.ForceUpdateCanvases();
            ResizeGridToAvailableWidth(stats, mainStats, runMode ? 2 : 3, 92);
            ResizeGridToAvailableWidth(runGrid, runStatsRect, 2, 84);

            var objectiveLayout = objectiveRoot != null ? objectiveRoot.GetComponent<VerticalLayoutGroup>() : null;
            if (objectiveLayout != null)
            {
                objectiveLayout.padding = new RectOffset(0, 0, 0, 0);
                objectiveLayout.spacing = 6;
                objectiveLayout.childAlignment = TextAnchor.UpperLeft;
                objectiveLayout.childControlWidth = true;
                objectiveLayout.childForceExpandWidth = true;
                objectiveLayout.childControlHeight = true;
                objectiveLayout.childForceExpandHeight = false;
            }

            ConfigureStatusText(restoration, TextAnchor.MiddleLeft);
            ConfigureStatusText(fever, TextAnchor.MiddleRight);
            foreach (var objective in objectives) ConfigureObjectiveText(objective);
        }

        static void ConfigureStatusText(Text text, TextAnchor alignment)
        {
            if (text == null) return;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 20;
            text.resizeTextMaxSize = 29;
        }

        static void ConfigureObjectiveText(Text text)
        {
            if (text == null) return;
            var parent = text.transform.parent;
            var root = parent != null && parent.GetComponent<VerticalLayoutGroup>() == null
                ? parent as RectTransform
                : text.rectTransform;
            var layout = root != null ? root.GetComponent<LayoutElement>() : null;
            if (root != null && layout == null) layout = root.gameObject.AddComponent<LayoutElement>();
            if (layout != null) { layout.minHeight = 40; layout.preferredHeight = 44; layout.flexibleWidth = 1; }
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 18;
            text.resizeTextMaxSize = 26;
        }

        public void SetSelectionSum(long value,int count)
        {
            if(selectionSum==null)return;
            CaptureSelectionSumBaseline();
            selectionSum.text=MathGameLocalization.Get("Gameplay","gameplay.selected_sum",value,
                displayedTarget == int.MinValue ? 0 : displayedTarget);
            if (displayedTarget != int.MinValue && value == displayedTarget && count > 0)
                selectionSum.text += "\n" + MathGameLocalization.Get("Gameplay", "gameplay.match");
            selectionSum.color = displayedTarget != int.MinValue && value == displayedTarget && count > 0
                ? new Color(.45f, 1f, .68f, 1f)
                : displayedTarget != int.MinValue && value > displayedTarget
                    ? new Color(1f, .55f, .24f, 1f)
                    : new Color(.55f, .9f, 1f, 1f);
            Pulse(selectionSum.rectTransform, displayedTarget != int.MinValue && value == displayedTarget ? 1.12f : 1.05f, .09f);
        }

        public void SetRunMode(bool active)
        {
            runMode = active;
            moves?.transform.parent.gameObject.SetActive(!active);
            restoration?.gameObject.SetActive(!active);
            fever?.gameObject.SetActive(!active);
            objectiveContainer?.gameObject.SetActive(!active);
            runStats?.SetActive(active && runHud == null);
            runHud?.SetVisible(active);
            var title = transform.Find("SafeArea/TopSlot/HUD/Title");
            if (title != null) title.gameObject.SetActive(!active);
            if (active)
            {
                var useAuthoredRunHud = runHud?.IsComplete == true;
                target?.transform.parent.gameObject.SetActive(!useAuthoredRunHud);
                score?.transform.parent.gameObject.SetActive(!useAuthoredRunHud);
                languageButton?.gameObject.SetActive(false);
            }
            else
            {
                target?.transform.parent.gameObject.SetActive(true);
                score?.transform.parent.gameObject.SetActive(true);
                languageButton?.gameObject.SetActive(true);
            }
            if (active && target != null)
            {
                var stats = target.transform.parent.parent.GetComponent<GridLayoutGroup>();
                if (stats != null) stats.constraintCount = 2;
                foreach (var value in new[] { runTime, runFever, runCombo, runTier })
                {
                    value.resizeTextForBestFit = true;
                    value.resizeTextMinSize = 16;
                    value.resizeTextMaxSize = 25;
                }
            }
            Canvas.ForceUpdateCanvases();
            var mainStats = target != null ? target.transform.parent.parent as RectTransform : null;
            ResizeGridToAvailableWidth(mainStats != null ? mainStats.GetComponent<GridLayoutGroup>() : null, mainStats, active ? 2 : 3, 92);
            RefreshLocalizedControls();
        }

        public void RefreshRun(StageSessionSnapshot snapshot, int targetValue, int gauge, int maximumGauge,
            string message, double remainingTime, double maximumTime, int difficultyTier, int combo, bool ended, bool targetRecovery)
        {
            if (snapshot == null) return;
            if (displayedTarget != targetValue)
            {
                displayedTarget = targetValue;
                if (target != null) Pulse(target.rectTransform, 1.1f, .14f);
                runHud?.PulseTarget();
            }
            if (runHud == null)
            {
                if (target != null) target.text = MathGameLocalization.Get("Gameplay", "gameplay.target", targetValue);
                if (score != null) score.text = MathGameLocalization.Get("Gameplay", "gameplay.score", snapshot.Score);
                if (runTime != null) runTime.text = MathGameLocalization.Get("Gameplay", "gameplay.time", remainingTime);
                if (runFever != null) runFever.text = MathGameLocalization.Get("Gameplay", "gameplay.fever", gauge, maximumGauge);
                if (runCombo != null) runCombo.text = MathGameLocalization.Get("Gameplay", "gameplay.combo", combo);
                if (runTier != null) runTier.text = MathGameLocalization.Get("Gameplay", "gameplay.tier", difficultyTier + 1);
            }
            runHud?.Present(targetValue, remainingTime, maximumTime, snapshot.Score, combo, difficultyTier, gauge, maximumGauge);
            if (!double.IsNaN(displayedTime) && remainingTime > displayedTime + .01)
                if (runTime != null) Pulse(runTime.rectTransform, 1.12f, .16f);
            if (combo != displayedCombo)
                if (runCombo != null) Pulse(runCombo.rectTransform, combo > displayedCombo ? 1.05f + Mathf.Min(combo, 8) * .012f : 1.03f, .13f);
            if (displayedGauge >= 0 && gauge > displayedGauge)
                if (runFever != null) Pulse(runFever.rectTransform, gauge >= maximumGauge ? 1.13f : 1.04f, gauge >= maximumGauge ? .18f : .09f);
            displayedTime = remainingTime;
            displayedCombo = combo;
            displayedGauge = gauge;
            if (runTime != null)
                runTime.color = remainingTime > 0 && remainingTime <= lowTimeWarningSeconds ? LowTimeColor : Color.white;
            if (status != null)
                status.text = Time.unscaledTime < transientFeedbackUntil ? transientFeedback : string.Empty;
            if (targetRetryButton != null) targetRetryButton.gameObject.SetActive(!ended && targetRecovery);
            ApplySafeArea(false);
        }

        public void PresentCorrect(MathGame.Answer.SpeedGrade grade, double recoveredTime)
        {
            transientFeedback = MathGameLocalization.Get("Gameplay", "gameplay.feedback." + grade.ToString().ToLowerInvariant(), recoveredTime);
            transientFeedbackUntil = Time.unscaledTime + .65f;
            if (status != null)
            {
                status.text = transientFeedback;
                status.color = grade == MathGame.Answer.SpeedGrade.Perfect
                    ? new Color(1f, .86f, .32f) : grade == MathGame.Answer.SpeedGrade.Fast
                        ? new Color(.42f, .9f, 1f) : Color.white;
                Pulse(status.rectTransform, grade == MathGame.Answer.SpeedGrade.Perfect ? 1.12f : 1.07f, .15f);
            }
            if (recoveredTime > 0)
            {
                if (runTime != null) Pulse(runTime.rectTransform, 1.14f, .18f);
                runHud?.PulseTimeGain();
            }
        }

        public void PresentMiss()
        {
            transientFeedback = MathGameLocalization.Get("Gameplay", "gameplay.feedback.miss");
            transientFeedbackUntil = Time.unscaledTime + .45f;
            if (selectionSum != null) StartCoroutine(Shake(selectionSum.rectTransform, .12f));
            if (status != null) { status.color = new Color(1f, .52f, .48f); Pulse(status.rectTransform, 1.06f, .10f); }
        }

        public void PresentFever(bool entering)
        {
            if (runFever == null) return;
            runFever.color = entering ? new Color(1f, .82f, .28f) : Color.white;
            Pulse(runFever.rectTransform, entering ? 1.14f : 1.06f, entering ? .22f : .12f);
        }

        public void PresentRunEnd()
        {
            if (runTime != null) Pulse(runTime.rectTransform, 1.16f, .22f);
            if (status != null) Pulse(status.rectTransform, 1.1f, .18f);
        }

        public void ResetPolish()
        {
            StopAllCoroutines();
            foreach (var transformValue in pulses.Keys) if (transformValue != null) transformValue.localScale = Vector3.one;
            pulses.Clear();
            displayedTarget = int.MinValue;
            displayedCombo = 0;
            displayedGauge = -1;
            displayedTime = double.NaN;
            transientFeedback = null;
            transientFeedbackUntil = 0;
            if (selectionSum != null) { selectionSum.color = Color.white; selectionSum.rectTransform.localScale = Vector3.one; }
            if (selectionSum != null && hasSelectionSumBaseline) selectionSum.rectTransform.anchoredPosition = selectionSumBaseline;
            if (status != null) { status.color = Color.white; status.rectTransform.localScale = Vector3.one; }
            if (runTime != null) { runTime.color = Color.white; runTime.rectTransform.localScale = Vector3.one; }
            if (runFever != null) { runFever.color = Color.white; runFever.rectTransform.localScale = Vector3.one; }
            if (runCombo != null) runCombo.rectTransform.localScale = Vector3.one;
            if (target != null) target.rectTransform.localScale = Vector3.one;
        }

        void StopTransientResponses()
        {
            StopAllCoroutines();
            foreach (var transformValue in pulses.Keys) if (transformValue != null) transformValue.localScale = Vector3.one;
            pulses.Clear();
            if (selectionSum != null && hasSelectionSumBaseline) selectionSum.rectTransform.anchoredPosition = selectionSumBaseline;
        }

        void CaptureSelectionSumBaseline()
        {
            if (selectionSum == null || hasSelectionSumBaseline) return;
            selectionSumBaseline = selectionSum.rectTransform.anchoredPosition;
            hasSelectionSumBaseline = true;
        }

        void Pulse(RectTransform value, float scale, float duration)
        {
            if (value == null || !isActiveAndEnabled) return;
            if (pulses.TryGetValue(value, out var active) && active != null) StopCoroutine(active);
            pulses[value] = StartCoroutine(PulseRoutine(value, scale, duration));
        }

        IEnumerator PulseRoutine(RectTransform value, float scale, float duration)
        {
            value.localScale = Vector3.one;
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var wave = Mathf.Sin(Mathf.Clamp01(elapsed / duration) * Mathf.PI);
                value.localScale = Vector3.one * Mathf.Lerp(1f, scale, wave);
                yield return null;
            }
            value.localScale = Vector3.one;
            pulses.Remove(value);
        }

        static IEnumerator Shake(RectTransform value, float duration)
        {
            var origin = value.anchoredPosition;
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var strength = 8f * (1f - elapsed / duration);
                value.anchoredPosition = origin + Vector2.right * Mathf.Sin(elapsed * 120f) * strength;
                yield return null;
            }
            value.anchoredPosition = origin;
        }

        void EnsureObjectiveCount(int count)
        {
            while(objectives.Count<count)
            {
                GameObject instance=null;
                if(instance==null){var value=Label("ObjectiveItem",objectiveContainer,"Objective",26,TextAnchor.MiddleLeft,FontStyle.Normal);objectives.Add(value);continue;}
                var text=instance.GetComponent<Text>()??instance.GetComponentInChildren<Text>();if(text==null)throw new InvalidOperationException("Objective item prefab requires a Text component.");objectives.Add(text);
                ConfigureObjectiveText(text);
            }
        }

        void ApplySafeArea(bool force)
        {
            var area = Screen.safeArea;
            if (!force && area == lastSafeArea && lastScreenWidth == Screen.width && lastScreenHeight == Screen.height) return;
            lastSafeArea = area;
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            safeArea.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            safeArea.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
            if (boardCamera != null)
            {
                if(boardCamera.GetComponent<PhysicsRaycaster>()==null)boardCamera.gameObject.AddComponent<PhysicsRaycaster>();
                var normalizedSafe=new Rect(area.xMin/Screen.width,area.yMin/Screen.height,area.width/Screen.width,area.height/Screen.height);
                boardCamera.rect=new Rect(normalizedSafe.x+.08f*normalizedSafe.width,normalizedSafe.y+.18f*normalizedSafe.height,.84f*normalizedSafe.width,.56f*normalizedSafe.height);
                boardView?.FrameCamera(boardCamera);
            }
            Canvas.ForceUpdateCanvases();
            var mainStats = transform.Find("SafeArea/TopSlot/HUD/MainStats") as RectTransform;
            var runStatsRect = transform.Find("SafeArea/TopSlot/HUD/RunStats") as RectTransform;
            ResizeGridToAvailableWidth(mainStats != null ? mainStats.GetComponent<GridLayoutGroup>() : null, mainStats, runMode ? 2 : 3, 92);
            ResizeGridToAvailableWidth(runStatsRect != null ? runStatsRect.GetComponent<GridLayoutGroup>() : null, runStatsRect, 2, 84);
        }

        static void ResizeGridToAvailableWidth(GridLayoutGroup grid, RectTransform rect, int columns, float cellHeight)
        {
            if (grid == null || rect == null || columns < 1) return;
            var width = rect.rect.width;
            if (width <= 0) return;
            var usable = width - grid.padding.horizontal - grid.spacing.x * (columns - 1);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            grid.cellSize = new Vector2(Mathf.Max(1, Mathf.Floor(usable / columns)), cellHeight);
        }

        static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            var value = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            value.SetParent(parent, false);
            SetRect(value, min, max, offsetMin, offsetMax);
            return value;
        }

        static RectTransform Panel(string name, Transform parent, Color color, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            var value = Rect(name, parent, min, max, offsetMin, offsetMax);
            value.gameObject.AddComponent<Image>().color = color;
            return value;
        }

        static Text Label(string name, Transform parent, string value, int size, TextAnchor anchor, FontStyle style)
        {
            var text = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)).GetComponent<Text>();
            text.transform.SetParent(parent, false);
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = anchor;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        static Text Stat(Transform parent, string name)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            root.transform.SetParent(parent, false);
            root.GetComponent<Image>().color = new Color(.11f, .17f, .26f, 1);
            var value = Label("Value", root.transform, name.ToUpperInvariant(), 30, TextAnchor.MiddleCenter, FontStyle.Bold);
            SetRect(value.rectTransform, Vector2.zero, Vector2.one, new Vector2(8, 6), new Vector2(-8, -6));
            return value;
        }

        static Button Action(Transform parent, string label, Action callback)
        {
            var root = new GameObject(label.Replace(" ", string.Empty), typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            root.transform.SetParent(parent, false);
            root.GetComponent<Image>().color = new Color(.12f, .42f, .62f, 1);
            var button = root.GetComponent<Button>();
            if (callback != null) button.onClick.AddListener(() => callback());
            var text = Label("Label", root.transform, label, 25, TextAnchor.MiddleCenter, FontStyle.Bold);
            SetRect(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(8, 4), new Vector2(-8, -4));
            return button;
        }

        static void SetRect(RectTransform value, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            value.anchorMin = min;
            value.anchorMax = max;
            value.offsetMin = offsetMin;
            value.offsetMax = offsetMax;
        }
    }
}
