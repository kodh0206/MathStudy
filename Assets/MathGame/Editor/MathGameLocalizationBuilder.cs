using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace MathGame.Editor.SceneBuilder
{
    public static class MathGameLocalizationBuilder
    {
        const string Root = "Assets/MathGame/Localization";

        [InitializeOnLoadMethod]
        static void ScheduleCleanCheckoutInitialization()
        {
            EditorApplication.delayCall += () =>
            {
                if (LocalizationEditorSettings.GetLocale("en") == null || LocalizationEditorSettings.GetLocale("ko") == null ||
                    LocalizationEditorSettings.GetStringTableCollection("Gameplay") == null ||
                    LocalizationEditorSettings.GetStringTableCollection("Result") == null ||
                    LocalizationEditorSettings.GetStringTableCollection("Start") == null ||
                    LocalizationEditorSettings.GetStringTableCollection("Common") == null ||
                    LocalizationEditorSettings.GetStringTableCollection("Settings") == null)
                    Build();
            };
        }

        [MenuItem("MathGame/Localization/Build Korean and English Tables", priority = 40)]
        public static void Build()
        {
            Directory.CreateDirectory(Root);
            var english = EnsureLocale("en");
            var korean = EnsureLocale("ko");
            EnsureCollection("Gameplay", english, korean, new Dictionary<string, (string en, string ko)>
            {
                ["gameplay.time"] = ("TIME\n{0:0.0}s", "시간\n{0:0.0}초"),
                ["gameplay.target"] = ("TARGET\n{0}", "목표\n{0}"),
                ["gameplay.score"] = ("SCORE\n{0:N0}", "점수\n{0:N0}"),
                ["gameplay.combo"] = ("COMBO\nx{0}", "콤보\nx{0}"),
                ["gameplay.fever"] = ("FEVER\n{0}/{1}", "피버\n{0}/{1}"),
                ["gameplay.tier"] = ("TIER\n{0}", "단계\n{0}"),
                ["gameplay.selected_sum"] = ("CURRENT\n{0} / {1}", "현재 합계\n{0} / {1}")
                ,["gameplay.match"] = ("MATCH", "일치")
                ,["gameplay.label.target"] = ("TARGET", "목표")
                ,["gameplay.label.time"] = ("TIME", "시간")
                ,["gameplay.label.fever"] = ("FEVER", "피버")
                ,["gameplay.label.overdrive"] = ("OVERDRIVE", "오버드라이브")
                ,["gameplay.feedback.normal"] = ("RESOLVED  +{0:0.#} SEC", "해결  +{0:0.#}초")
                ,["gameplay.feedback.fast"] = ("FAST!  +{0:0.#} SEC", "빠름!  +{0:0.#}초")
                ,["gameplay.feedback.perfect"] = ("✓ PERFECT!  +{0:0.#} SEC", "✓ 완벽!  +{0:0.#}초")
                ,["gameplay.feedback.miss"] = ("NO MATCH", "불일치")
                ,["gameplay.ready"] = ("Drag across adjacent cells, then release.", "인접한 칸을 드래그한 뒤 놓으세요.")
                ,["gameplay.run_over"] = ("RUN OVER", "게임 종료")
                ,["gameplay.fever_active"] = ("FEVER ACTIVE", "피버 활성화")
                ,["gameplay.miss"] = ("MISS — no penalty.", "실패 — 불이익이 없습니다.")
                ,["gameplay.resolved"] = ("{0} — board resolved.", "{0} — 보드 해결 완료.")
                ,["gameplay.target_pending"] = ("Finding a playable target…", "플레이 가능한 목표를 찾는 중…")
                ,["gameplay.paused"] = ("Run paused.", "게임이 일시정지되었습니다.")
                ,["gameplay.resumed"] = ("Run resumed.", "게임을 계속합니다.")
                ,["gameplay.save_failed"] = ("Local save failed. Please try again.", "로컬 저장에 실패했습니다. 다시 시도해 주세요.")
                ,["gameplay.grade.normal"] = ("NORMAL", "보통")
                ,["gameplay.grade.fast"] = ("FAST", "빠름")
                ,["gameplay.grade.perfect"] = ("PERFECT", "완벽")
            });
            EnsureCollection("Result", english, korean, new Dictionary<string, (string en, string ko)>
            {
                ["result.summary"] = ("RUN OVER\n\nSCORE  {0:N0}\nSURVIVAL TIME  {1:0.0}s\nMAX COMBO  {2}\nHIGHEST DIFFICULTY  {3}",
                    "게임 종료\n\n점수  {0:N0}\n생존 시간  {1:0.0}초\n최대 콤보  {2}\n최고 난이도  {3}"),
                ["result.play_again"] = ("PLAY AGAIN", "다시 하기"),
                ["result.home"] = ("HOME", "홈"),
                ["result.best_score"] = ("BEST SCORE", "최고 점수"),
                ["result.new_best"] = ("NEW BEST!", "신기록!")
            });
            EnsureCollection("Start", english, korean, new Dictionary<string, (string en, string ko)>
            {
                ["start.title"] = ("SUM//VIVE", "SUM//VIVE"),
                ["start.subtitle"] = ("KEEP THE CORE ONLINE", "코어를 유지하세요"),
                ["start.core_online"] = ("CORE ONLINE", "코어 온라인"),
                ["start.system_online"] = ("SYSTEM ONLINE", "시스템 온라인"),
                ["start.run"] = ("START RUN", "게임 시작"),
                ["start.best_time"] = ("BEST TIME  {0:0.0}s", "최고 시간  {0:0.0}초"),
                ["start.best_score"] = ("BEST SCORE  {0:N0}", "최고 점수  {0:N0}")
            });
            EnsureCollection("Common", english, korean, new Dictionary<string, (string en, string ko)>
            {
                ["common.pause"] = ("PAUSE", "일시정지"), ["common.resume"] = ("RESUME", "계속하기"),
                ["common.restart"] = ("RESTART", "다시 시작"), ["common.confirm"] = ("CONFIRM", "확인"),
                ["common.cancel"] = ("CANCEL", "취소"), ["common.back"] = ("BACK", "뒤로")
            });
            EnsureCollection("Settings", english, korean, new Dictionary<string, (string en, string ko)>
            {
                ["settings.language"] = ("LANGUAGE", "언어"),
                ["settings.language_button"] = ("English / 한국어", "한국어 / English"),
                ["settings.language_changed"] = ("Language changed.", "언어가 변경되었습니다."),
                ["settings.korean"] = ("Korean", "한국어"), ["settings.english"] = ("English", "영어")
            });
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("MathGame Korean and English localization tables are ready.");
        }

        static Locale EnsureLocale(string code)
        {
            var locale = LocalizationEditorSettings.GetLocale(code);
            if (locale != null) return locale;
            locale = Locale.CreateLocale(code);
            AssetDatabase.CreateAsset(locale, $"{Root}/Locale-{code}.asset");
            LocalizationEditorSettings.AddLocale(locale);
            return locale;
        }

        static void EnsureCollection(string name, Locale english, Locale korean,
            Dictionary<string, (string en, string ko)> values)
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(name) ??
                LocalizationEditorSettings.CreateStringTableCollection(name, Root, new List<Locale> { english, korean });
            var en = collection.GetTable(english.Identifier) as StringTable ?? collection.AddNewTable(english.Identifier) as StringTable;
            var ko = collection.GetTable(korean.Identifier) as StringTable ?? collection.AddNewTable(korean.Identifier) as StringTable;
            foreach (var pair in values)
            {
                Set(en, pair.Key, pair.Value.en);
                Set(ko, pair.Key, pair.Value.ko);
            }
            EditorUtility.SetDirty(en); EditorUtility.SetDirty(ko); EditorUtility.SetDirty(en.SharedData);
        }

        static void Set(StringTable table, string key, string value)
        {
            var entry = table.GetEntry(key) ?? table.AddEntry(key, value);
            entry.Value = value;
            entry.IsSmart = value.Contains("{");
        }
    }
}
