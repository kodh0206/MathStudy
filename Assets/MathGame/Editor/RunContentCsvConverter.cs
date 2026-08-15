using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MathGame.RunContent;
using UnityEditor;
using UnityEngine;

namespace MathGame.Editor.SceneBuilder
{
    public sealed class RunContentConversionResult
    {
        internal RunContentConversionResult(bool succeeded, string json, string error)
        { Succeeded = succeeded; Json = json; Error = error; }
        public bool Succeeded { get; }
        public string Json { get; }
        public string Error { get; }
    }

    public static class RunContentCsvConverter
    {
        public const string RunCsvPath = "Assets/MathGame/Content/Authoring/RunConfig.csv";
        public const string DifficultyCsvPath = "Assets/MathGame/Content/Authoring/RunDifficulty.csv";
        public const string JsonPath = "Assets/MathGame/Resources/RunContent/run-config.json";

        [MenuItem("MathGame/Content/Build Run Content JSON", priority = 30)]
        public static void BuildRunContentJson()
        {
            if (!File.Exists(RunCsvPath) || !File.Exists(DifficultyCsvPath))
                throw new InvalidOperationException("Run authoring CSV files are missing.");
            var result = ConvertText(File.ReadAllText(RunCsvPath), File.ReadAllText(DifficultyCsvPath));
            if (!result.Succeeded) throw new InvalidOperationException(result.Error);
            Directory.CreateDirectory(Path.GetDirectoryName(JsonPath));
            File.WriteAllText(JsonPath, result.Json + Environment.NewLine);
            AssetDatabase.ImportAsset(JsonPath, ImportAssetOptions.ForceUpdate);
            Debug.Log("Generated validated Run content: " + JsonPath);
        }

        public static RunContentConversionResult ConvertText(string runCsv, string difficultyCsv)
        {
            try
            {
                var runRows = Rows(runCsv, "RunConfig.csv");
                if (runRows.Count != 2) throw new FormatException("RunConfig.csv must contain exactly one data row.");
                RequireHeader(runRows[0], "Id", "InitialTime", "MaximumTime", "DrainPerSecond", "NormalRecovery", "FastRecovery", "PerfectRecovery");
                var run = runRows[1];
                if (string.IsNullOrWhiteSpace(run[0])) throw new FormatException("RunConfig.csv row 2 field Id is required.");

                var tierRows = Rows(difficultyCsv, "RunDifficulty.csv");
                if (tierRows.Count < 2) throw new FormatException("RunDifficulty.csv requires at least one data row.");
                RequireHeader(tierRows[0], "TierId", "UnlockCorrectCycles", "TargetMin", "TargetMax");
                var tierData = new RunDifficultyTierJsonData[tierRows.Count - 1];
                var seen = new HashSet<int>();
                for (var index = 1; index < tierRows.Count; index++)
                {
                    var row = tierRows[index];
                    var tierId = Integer(row[0], "RunDifficulty.csv", index + 1, "TierId");
                    if (!seen.Add(tierId)) throw new FormatException("RunDifficulty.csv row " + (index + 1) + " duplicate TierId " + tierId + ".");
                    tierData[index - 1] = new RunDifficultyTierJsonData
                    {
                        id = tierId,
                        unlockCorrectCycles = Long(row[1], "RunDifficulty.csv", index + 1, "UnlockCorrectCycles"),
                        targetMin = Integer(row[2], "RunDifficulty.csv", index + 1, "TargetMin"),
                        targetMax = Integer(row[3], "RunDifficulty.csv", index + 1, "TargetMax")
                    };
                }

                var data = new RunConfigJsonData
                {
                    schemaVersion = 1,
                    id = run[0],
                    initialTime = Number(run[1], "RunConfig.csv", 2, "InitialTime"),
                    maximumTime = Number(run[2], "RunConfig.csv", 2, "MaximumTime"),
                    drainPerSecond = Number(run[3], "RunConfig.csv", 2, "DrainPerSecond"),
                    normalRecovery = Number(run[4], "RunConfig.csv", 2, "NormalRecovery"),
                    fastRecovery = Number(run[5], "RunConfig.csv", 2, "FastRecovery"),
                    perfectRecovery = Number(run[6], "RunConfig.csv", 2, "PerfectRecovery"),
                    tiers = tierData
                };
                var json = JsonUtility.ToJson(data, true);
                var validation = RunConfigJsonRepository.Parse(json);
                return validation.Succeeded
                    ? new RunContentConversionResult(true, json, null)
                    : new RunContentConversionResult(false, null, "Generated configuration is invalid: " + validation.Error);
            }
            catch (Exception error) { return new RunContentConversionResult(false, null, error.Message); }
        }

        private static List<string[]> Rows(string text, string file)
        {
            if (string.IsNullOrWhiteSpace(text)) throw new FormatException(file + " is empty.");
            var rows = new List<string[]>();
            using var reader = new StringReader(text);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                rows.Add(ParseCsvLine(line).ToArray());
            }
            return rows;
        }

        private static List<string> ParseCsvLine(string line)
        {
            var fields = new List<string>();
            var value = new System.Text.StringBuilder();
            var quoted = false;
            for (var index = 0; index < line.Length; index++)
            {
                var character = line[index];
                if (character == '"')
                {
                    if (quoted && index + 1 < line.Length && line[index + 1] == '"') { value.Append('"'); index++; }
                    else quoted = !quoted;
                }
                else if (character == ',' && !quoted) { fields.Add(value.ToString().Trim()); value.Clear(); }
                else value.Append(character);
            }
            if (quoted) throw new FormatException("Unterminated quoted CSV field.");
            fields.Add(value.ToString().Trim());
            return fields;
        }

        private static void RequireHeader(string[] actual, params string[] expected)
        {
            if (actual.Length != expected.Length) throw new FormatException("CSV header field count is invalid.");
            for (var index = 0; index < expected.Length; index++)
                if (!string.Equals(actual[index], expected[index], StringComparison.Ordinal))
                    throw new FormatException("CSV header field " + (index + 1) + " must be " + expected[index] + ".");
        }
        private static double Number(string value, string file, int row, string field)
        { if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)) throw new FormatException(file + " row " + row + " field " + field + " is malformed."); return parsed; }
        private static int Integer(string value, string file, int row, string field)
        { if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)) throw new FormatException(file + " row " + row + " field " + field + " is malformed."); return parsed; }
        private static long Long(string value, string file, int row, string field)
        { if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)) throw new FormatException(file + " row " + row + " field " + field + " is malformed."); return parsed; }
    }
}
