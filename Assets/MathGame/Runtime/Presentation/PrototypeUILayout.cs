using System;
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
        Button continueButton;
        Button retryButton;
        Button abandonButton;
        Button targetRetryButton;
        Button restartButton;
        Button languageButton;
        Transform objectiveContainer;
        MathGamePrefabRegistry prefabRegistry;
        Camera boardCamera;
        GameplayPresentationRoot boardView;
        Rect lastSafeArea;
        bool runMode;
        bool pauseState;

        void OnEnable() => LocalizationSettings.SelectedLocaleChanged += LocaleChanged;
        void OnDisable() => LocalizationSettings.SelectedLocaleChanged -= LocaleChanged;
        void LocaleChanged(Locale _) => RefreshLocalizedControls();

        void RefreshLocalizedControls()
        {
            var pauseLabel = restartButton?.GetComponentInChildren<Text>();
            if (pauseLabel != null) pauseLabel.text = MathGameLocalization.Get("Common",
                runMode ? (pauseState ? "common.resume" : "common.pause") : "common.restart");
            var languageLabel = languageButton?.GetComponentInChildren<Text>();
            if (languageLabel != null) languageLabel.text = MathGameLocalization.Get("Settings", "settings.language_button");
        }

        public void Build(Camera camera, GameplayPresentationRoot serializedBoardView, Action onContinue, Action onRetry, Action onAbandon, Action onTargetRetry, Action onLanguage, MathGamePrefabRegistry registry=null)
        {
            boardCamera = camera;
            boardView = serializedBoardView;
            prefabRegistry=registry;
            EnsureEventSystem();
            var existingCanvas = GetComponent<Canvas>();
            if (existingCanvas != null && transform.Find("SafeArea/TopSlot/HUD") != null)
            {
                BindPrefabHierarchy(onContinue,onRetry,onAbandon,onTargetRetry,onLanguage);
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
            continueButton = Action(actions, "Continue +5", onContinue);
            retryButton = Action(actions, "Retry", onRetry);
            abandonButton = Action(actions, "Abandon", onAbandon);
            targetRetryButton = Action(actions, "Retry Target", onTargetRetry);
            restartButton = Action(actions, "Restart", onRetry);
            languageButton = Action(actions, "Language", onLanguage);
            ApplySafeArea(true);
        }

        void EnsureEventSystem()
        {
            if(FindFirstObjectByType<EventSystem>()!=null)return;
            var events=new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));events.transform.SetParent(transform.parent,false);
        }

        void BindPrefabHierarchy(Action onContinue,Action onRetry,Action onAbandon,Action onTargetRetry,Action onLanguage)
        {
            canvas=GetComponent<Canvas>();safeArea=transform.Find("SafeArea") as RectTransform;
            target=FindText("SafeArea/TopSlot/HUD/MainStats/Target/Value");moves=FindText("SafeArea/TopSlot/HUD/MainStats/Moves/Value");
            score=FindText("SafeArea/TopSlot/HUD/MainStats/Score/Value");restoration=FindText("SafeArea/TopSlot/HUD/Resources/Restoration");
            fever=FindText("SafeArea/TopSlot/HUD/Resources/Fever");status=FindText("SafeArea/BottomSlot/BottomHUD/Status");
            runStats=transform.Find("SafeArea/TopSlot/HUD/RunStats")?.gameObject;
            runTime=FindText("SafeArea/TopSlot/HUD/RunStats/Time/Value");
            runFever=FindText("SafeArea/TopSlot/HUD/RunStats/Fever/Value");
            runCombo=FindText("SafeArea/TopSlot/HUD/RunStats/Combo/Value");
            runTier=FindText("SafeArea/TopSlot/HUD/RunStats/Tier/Value");
            selectionSum=FindText("SafeArea/BottomSlot/BottomHUD/SelectionSum");
            objectiveContainer=transform.Find("SafeArea/TopSlot/HUD/Objectives");objectives.Clear();
            if(objectiveContainer!=null)foreach(Transform child in objectiveContainer){var value=child.GetComponent<Text>()??child.GetComponentInChildren<Text>();if(value!=null)objectives.Add(value);}
            continueButton=FindButton("SafeArea/BottomSlot/BottomHUD/Actions/Continue");retryButton=FindButton("SafeArea/BottomSlot/BottomHUD/Actions/Retry");
            abandonButton=FindButton("SafeArea/BottomSlot/BottomHUD/Actions/Abandon");targetRetryButton=FindButton("SafeArea/BottomSlot/BottomHUD/Actions/RetryTarget");
            restartButton=FindButton("SafeArea/BottomSlot/BottomHUD/Actions/Restart");
            languageButton=FindButton("SafeArea/BottomSlot/BottomHUD/Actions/Language");
            Wire(continueButton,onContinue);Wire(retryButton,onRetry);Wire(abandonButton,onAbandon);Wire(targetRetryButton,onTargetRetry);Wire(restartButton,onRetry);Wire(languageButton,onLanguage);
        }

        Text FindText(string path)=>transform.Find(path)?.GetComponent<Text>();
        Button FindButton(string path)=>transform.Find(path)?.GetComponent<Button>();
        static void Wire(Button button,Action callback){if(button==null)return;button.onClick.RemoveAllListeners();if(callback!=null)button.onClick.AddListener(()=>callback());}
        void ValidateBoundHierarchy()
        {
            if(safeArea==null||target==null||moves==null||score==null||restoration==null||fever==null||status==null||selectionSum==null||objectiveContainer==null||continueButton==null||retryButton==null||abandonButton==null||targetRetryButton==null||restartButton==null||languageButton==null||runStats==null||runTime==null||runFever==null||runCombo==null||runTier==null)
                throw new InvalidOperationException("GameRoot/HUD prefab contract is incomplete. Rebuild or migrate the presentation prefab explicitly.");
        }

        void ConfigureResponsiveHud()
        {
            var hud = transform.Find("SafeArea/TopSlot/HUD") as RectTransform;
            var mainStats = transform.Find("SafeArea/TopSlot/HUD/MainStats") as RectTransform;
            var resources = transform.Find("SafeArea/TopSlot/HUD/Resources") as RectTransform;
            var objectiveRoot = objectiveContainer as RectTransform;
            if (hud != null) SetRect(hud, new Vector2(0, 1), Vector2.one, new Vector2(24, -404), new Vector2(-24, -24));
            if (mainStats != null) SetRect(mainStats, new Vector2(0, 1), Vector2.one, new Vector2(24, -184), new Vector2(-24, -82));
            if (resources != null) SetRect(resources, new Vector2(0, 1), Vector2.one, new Vector2(24, -274), new Vector2(-24, -194));
            // Fixed prototype HUD placement requested by design: centered at Y 60.
            if (objectiveRoot != null) SetRect(objectiveRoot, Vector2.zero, new Vector2(1, 0), new Vector2(24, 11), new Vector2(-24, 109));

            var stats = mainStats != null ? mainStats.GetComponent<GridLayoutGroup>() : null;
            if (stats != null)
            {
                stats.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                stats.constraintCount = 3;
                stats.cellSize = new Vector2(320, 92);
                stats.spacing = new Vector2(20, 0);
                stats.childAlignment = TextAnchor.MiddleCenter;
            }

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

        public void Refresh(StageSessionSnapshot snapshot, int targetValue, int gauge, int maximumGauge,
            string message, bool failedDecision, bool targetRecovery, bool terminal)
        {
            if (snapshot == null) return;
            target.text = "TARGET\n" + targetValue;
            moves.text = "MOVES\n" + snapshot.RemainingMoves;
            score.text = "SCORE\n" + snapshot.Score;
            restoration.text = "RESTORATION  " + snapshot.ProvisionalRestoration + "/" + snapshot.StageRestorationCapacity;
            fever.text = "FEVER  " + gauge + "/" + maximumGauge;
            status.text = message ?? string.Empty;
            EnsureObjectiveCount(snapshot.Objectives.Count);
            for (var i = 0; i < objectives.Count; i++)
            {
                if (i < snapshot.Objectives.Count)
                {
                    var objective = snapshot.Objectives[i];
                    objectives[i].gameObject.SetActive(true);
                    objectives[i].text = DescribeObjective(objective) + "   " + objective.Current + " / " + objective.Required +
                        (objective.IsComplete ? "   COMPLETE" : string.Empty);
                }
                else objectives[i].gameObject.SetActive(false);
            }
            continueButton.gameObject.SetActive(failedDecision && !snapshot.ContinueUsed);
            retryButton.gameObject.SetActive(failedDecision || terminal);
            abandonButton.gameObject.SetActive(failedDecision);
            targetRetryButton.gameObject.SetActive(targetRecovery);
            restartButton.gameObject.SetActive(!failedDecision && !targetRecovery && !terminal);
            ApplySafeArea(false);
        }

        static string DescribeObjective(ObjectiveProgressSnapshot objective)
        {
            var definition = objective.Definition;
            switch (definition.Kind)
            {
                case StageObjectiveKind.RemoveNumberBlocks:
                    return "Remove number blocks";
                case StageObjectiveKind.CompleteTarget:
                    return "Complete target " + definition.Target.Value;
                case StageObjectiveKind.CompleteLongConnection:
                    return "Make connections of " + definition.MinimumConnectionLength + "+ blocks";
                case StageObjectiveKind.RemoveObstacle:
                    return definition.ObstacleKind.HasValue
                        ? "Destroy " + definition.ObstacleKind.Value + " obstacles"
                        : "Destroy obstacles";
                case StageObjectiveKind.EarnRestorationEnergy:
                    return "Earn restoration energy";
                case StageObjectiveKind.CreateSpecial:
                    return "Create special blocks";
                case StageObjectiveKind.UseSpecial:
                    return "Use special blocks";
                default:
                    return "Complete objective";
            }
        }

        public void SetSelectionSum(long value,int count)
        {
            if(selectionSum!=null)selectionSum.text=MathGameLocalization.Get("Gameplay","gameplay.selected_sum",value);
        }

        public void SetRunMode(bool active)
        {
            runMode = active;
            moves?.transform.parent.gameObject.SetActive(!active);
            restoration?.gameObject.SetActive(!active);
            fever?.gameObject.SetActive(!active);
            objectiveContainer?.gameObject.SetActive(!active);
            runStats?.SetActive(active);
            continueButton?.gameObject.SetActive(false);
            abandonButton?.gameObject.SetActive(false);
            retryButton?.gameObject.SetActive(false);
            if (active && target != null)
            {
                var stats = target.transform.parent.parent.GetComponent<GridLayoutGroup>();
                if (stats != null) { stats.constraintCount = 2; stats.cellSize = new Vector2(480, 92); }
                foreach (var value in new[] { runTime, runFever, runCombo, runTier })
                {
                    value.resizeTextForBestFit = true;
                    value.resizeTextMinSize = 16;
                    value.resizeTextMaxSize = 25;
                }
            }
            RefreshLocalizedControls();
        }

        public void RefreshRun(StageSessionSnapshot snapshot, int targetValue, int gauge, int maximumGauge,
            string message, double remainingTime, int difficultyTier, int combo, bool ended, bool targetRecovery)
        {
            if (snapshot == null) return;
            target.text = MathGameLocalization.Get("Gameplay", "gameplay.target", targetValue);
            score.text = MathGameLocalization.Get("Gameplay", "gameplay.score", snapshot.Score);
            runTime.text = MathGameLocalization.Get("Gameplay", "gameplay.time", remainingTime);
            runFever.text = MathGameLocalization.Get("Gameplay", "gameplay.fever", gauge, maximumGauge);
            runCombo.text = MathGameLocalization.Get("Gameplay", "gameplay.combo", combo);
            runTier.text = MathGameLocalization.Get("Gameplay", "gameplay.tier", difficultyTier + 1);
            status.text = message ?? string.Empty;
            restartButton.gameObject.SetActive(!ended);
            targetRetryButton.gameObject.SetActive(!ended && targetRecovery);
            ApplySafeArea(false);
        }

        public void SetPauseState(bool paused)
        {
            pauseState = paused;
            RefreshLocalizedControls();
        }

        void EnsureObjectiveCount(int count)
        {
            while(objectives.Count<count)
            {
                GameObject instance=null;
                if(prefabRegistry!=null&&prefabRegistry.ObjectiveItemPrefab!=null)instance=Instantiate(prefabRegistry.ObjectiveItemPrefab,objectiveContainer);
                if(instance==null){var value=Label("ObjectiveItem",objectiveContainer,"Objective",26,TextAnchor.MiddleLeft,FontStyle.Normal);objectives.Add(value);continue;}
                var text=instance.GetComponent<Text>()??instance.GetComponentInChildren<Text>();if(text==null)throw new InvalidOperationException("Objective item prefab requires a Text component.");objectives.Add(text);
                ConfigureObjectiveText(text);
            }
        }

        void ApplySafeArea(bool force)
        {
            var area = Screen.safeArea;
            if (!force && area == lastSafeArea) return;
            lastSafeArea = area;
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
