using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace MathGame.Presentation.Unity
{
    public static class MathGameLocalization
    {
        public const string English = "en";
        public const string Korean = "ko";

        public static string Get(string table, string key, params object[] arguments)
        {
            var value = LocalizationSettings.StringDatabase.GetLocalizedString(table, key, arguments);
            if (string.IsNullOrWhiteSpace(value))
            {
                Debug.LogError($"[MathGame][Localization] Missing {table}/{key}.");
                return $"[{key}]";
            }
            return value;
        }

        public static string ResolveSupportedCode(string savedCode, SystemLanguage deviceLanguage)
        {
            if (savedCode == English || savedCode == Korean) return savedCode;
            return deviceLanguage == SystemLanguage.Korean ? Korean : English;
        }

        public static bool Select(string code)
        {
            var locale = LocalizationSettings.AvailableLocales.Locales
                .FirstOrDefault(item => string.Equals(item.Identifier.Code, code, StringComparison.OrdinalIgnoreCase));
            if (locale == null) return false;
            LocalizationSettings.SelectedLocale = locale;
            return true;
        }

        public static string SelectedCode => LocalizationSettings.SelectedLocale?.Identifier.Code ?? English;
    }
}
