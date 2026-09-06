using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace MathGame.Presentation.Unity
{
    public static class MathGameLocalization
    {
        public const string English = "en";
        public const string Korean = "ko";
        static readonly Dictionary<string, StringTable> LoadedTables =
            new Dictionary<string, StringTable>(StringComparer.OrdinalIgnoreCase);
        static readonly string[] RuntimeTables = { "Common", "Gameplay", "Result", "Settings", "Start" };
#if UNITY_WEBGL && !UNITY_EDITOR
        static readonly Dictionary<string, string> WebEnglish = new Dictionary<string, string>
        {
            ["Gameplay/gameplay.time"] = "TIME\n{0:0.0}s", ["Gameplay/gameplay.target"] = "TARGET\n{0}",
            ["Gameplay/gameplay.score"] = "SCORE\n{0:N0}", ["Gameplay/gameplay.combo"] = "COMBO\nx{0}",
            ["Gameplay/gameplay.fever"] = "FEVER\n{0}/{1}", ["Gameplay/gameplay.tier"] = "TIER\n{0}",
            ["Gameplay/gameplay.selected_sum"] = "CURRENT\n{0} / {1}", ["Gameplay/gameplay.match"] = "MATCH",
            ["Gameplay/gameplay.label.target"] = "TARGET", ["Gameplay/gameplay.label.time"] = "TIME",
            ["Gameplay/gameplay.label.fever"] = "FEVER", ["Gameplay/gameplay.label.overdrive"] = "OVERDRIVE",
            ["Gameplay/gameplay.feedback.normal"] = "RESOLVED  +{0:0.#} SEC",
            ["Gameplay/gameplay.feedback.fast"] = "FAST!  +{0:0.#} SEC",
            ["Gameplay/gameplay.feedback.perfect"] = "✓ PERFECT!  +{0:0.#} SEC",
            ["Gameplay/gameplay.feedback.miss"] = "NO MATCH",
            ["Gameplay/gameplay.ready"] = "Drag across adjacent cells, then release.",
            ["Gameplay/gameplay.run_over"] = "RUN OVER", ["Gameplay/gameplay.fever_active"] = "FEVER ACTIVE",
            ["Gameplay/gameplay.miss"] = "MISS — no penalty.", ["Gameplay/gameplay.resolved"] = "{0} — board resolved.",
            ["Gameplay/gameplay.target_pending"] = "Finding a playable target…",
            ["Gameplay/gameplay.reconfiguring"] = "RECONFIGURING...", ["Gameplay/gameplay.paused"] = "Run paused.",
            ["Gameplay/gameplay.resumed"] = "Run resumed.",
            ["Gameplay/gameplay.save_failed"] = "Local save failed. Please try again.",
            ["Gameplay/gameplay.grade.normal"] = "NORMAL", ["Gameplay/gameplay.grade.fast"] = "FAST",
            ["Gameplay/gameplay.grade.perfect"] = "PERFECT",
            ["Result/result.summary"] = "RUN OVER\n\nSCORE  {0:N0}\nSURVIVAL TIME  {1:0.0}s\nMAX COMBO  {2}\nHIGHEST DIFFICULTY  {3}",
            ["Result/result.play_again"] = "PLAY AGAIN", ["Result/result.home"] = "HOME",
            ["Result/result.best_score"] = "BEST SCORE", ["Result/result.new_best"] = "NEW BEST!",
            ["Start/start.title"] = "SUM//VIVE", ["Start/start.subtitle"] = "KEEP THE CORE ONLINE",
            ["Start/start.core_online"] = "CORE ONLINE", ["Start/start.system_online"] = "SYSTEM ONLINE",
            ["Start/start.run"] = "START RUN", ["Start/start.best_time"] = "BEST TIME  {0:0.0}s",
            ["Start/start.best_score"] = "BEST SCORE  {0:N0}",
            ["Common/common.pause"] = "PAUSE", ["Common/common.resume"] = "RESUME",
            ["Common/common.restart"] = "RESTART", ["Common/common.confirm"] = "CONFIRM",
            ["Common/common.cancel"] = "CANCEL", ["Common/common.back"] = "BACK",
            ["Settings/settings.language"] = "LANGUAGE", ["Settings/settings.language_button"] = "English",
            ["Settings/settings.language_changed"] = "Web version uses English.",
            ["Settings/settings.korean"] = "Korean", ["Settings/settings.english"] = "English"
        };
#endif

        public static bool IsTableReady(string table)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return true;
#else
            var locale = LocalizationSettings.SelectedLocale;
            return locale != null && LoadedTables.ContainsKey(CacheKey(locale.Identifier.Code, table));
#endif
        }

        public static string Get(string table, string key, params object[] arguments)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (!WebEnglish.TryGetValue(table + "/" + key, out var template))
            {
                Debug.LogError($"[MathGame][Localization] Missing WebGL English text: {table}/{key}.");
                return $"[{key}]";
            }
            return arguments == null || arguments.Length == 0
                ? template
                : string.Format(CultureInfo.InvariantCulture, template, arguments);
#else
            var locale = LocalizationSettings.SelectedLocale;
            StringTable loaded;
            var value = locale != null && LoadedTables.TryGetValue(CacheKey(locale.Identifier.Code, table), out loaded)
                ? loaded.GetEntry(key)?.GetLocalizedString(arguments)
                : null;
            if (string.IsNullOrWhiteSpace(value))
            {
                Debug.LogWarning($"[MathGame][Localization] Table entry is not preloaded or is missing: {table}/{key}.");
                return $"[{key}]";
            }
            return value;
#endif
        }

        /// <summary>
        /// WebGL cannot use the localization package's synchronous WaitForCompletion path.
        /// Load both supported locales before any interactive presentation calls Get().
        /// </summary>
        public static IEnumerator PreloadRuntimeTables()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            yield break;
#else
            LoadedTables.Clear();
            var locales = LocalizationSettings.AvailableLocales.Locales
                .Where(locale => locale.Identifier.Code == English || locale.Identifier.Code == Korean)
                .ToArray();
            foreach (var locale in locales)
            foreach (var tableName in RuntimeTables)
            {
                var operation = LocalizationSettings.StringDatabase.GetTableAsync(tableName, locale);
                yield return operation;
                if (operation.Status == AsyncOperationStatus.Succeeded && operation.Result != null)
                    LoadedTables[CacheKey(locale.Identifier.Code, tableName)] = operation.Result;
                else
                    Debug.LogError($"[MathGame][Localization] Failed to preload {tableName}/{locale.Identifier.Code}.");
            }
#endif
        }

        static string CacheKey(string localeCode, string table) => localeCode + "/" + table;

        public static string ResolveSupportedCode(string savedCode, SystemLanguage deviceLanguage)
        {
            if (savedCode == English || savedCode == Korean) return savedCode;
            return deviceLanguage == SystemLanguage.Korean ? Korean : English;
        }

        public static bool Select(string code)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return true;
#else
            var locale = LocalizationSettings.AvailableLocales.Locales
                .FirstOrDefault(item => string.Equals(item.Identifier.Code, code, StringComparison.OrdinalIgnoreCase));
            if (locale == null) return false;
            LocalizationSettings.SelectedLocale = locale;
            return true;
#endif
        }

        public static string SelectedCode
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return English;
#else
                return LocalizationSettings.SelectedLocale?.Identifier.Code ?? English;
#endif
            }
        }
    }
}
