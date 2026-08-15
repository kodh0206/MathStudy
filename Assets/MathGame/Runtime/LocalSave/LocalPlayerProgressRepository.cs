using System;
using System.IO;
using MathGame.PlayerProgress;
using UnityEngine;

namespace MathGame.LocalSave
{
    public interface IProgressFileStore
    {
        bool Exists(string path);
        string ReadAllText(string path);
        void WriteAllText(string path, string contents);
        void Copy(string source, string destination, bool overwrite);
        void Move(string source, string destination);
        void Delete(string path);
    }

    public sealed class SystemProgressFileStore : IProgressFileStore
    {
        public bool Exists(string path) => File.Exists(path);
        public string ReadAllText(string path) => File.ReadAllText(path);
        public void WriteAllText(string path, string contents) => File.WriteAllText(path, contents);
        public void Copy(string source, string destination, bool overwrite) => File.Copy(source, destination, overwrite);
        public void Move(string source, string destination) => File.Move(source, destination);
        public void Delete(string path) => File.Delete(path);
    }

    [Serializable]
    internal sealed class PlayerProgressSaveData
    {
        public int version;
        public RunRecordsSaveData runRecords;
        public string[] appliedRunIds;
        public PlayerSettingsSaveData settings;
    }

    [Serializable]
    internal sealed class PlayerSettingsSaveData { public string localeCode; }

    [Serializable]
    internal sealed class RunRecordsSaveData
    {
        public long bestScore;
        public double bestSurvivalDuration;
        public int highestDifficultyReached;
        public int bestCombo;
        public long totalRuns;
    }

    public sealed class LocalPlayerProgressRepository : IPlayerProgressRepository
    {
        public const int CurrentVersion = 2;
        public const string PrimaryFileName = "player_progress.json";
        public const string BackupFileName = "player_progress.backup.json";
        private readonly IProgressFileStore files;
        private readonly string primaryPath;
        private readonly string backupPath;
        private readonly string temporaryPath;

        public LocalPlayerProgressRepository(string directory, IProgressFileStore files = null)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("A save directory is required.", nameof(directory));
            this.files = files ?? new SystemProgressFileStore();
            primaryPath = Path.Combine(directory, PrimaryFileName);
            backupPath = Path.Combine(directory, BackupFileName);
            temporaryPath = primaryPath + ".tmp";
        }

        public static LocalPlayerProgressRepository ForUnityPersistentData() =>
            new LocalPlayerProgressRepository(Application.persistentDataPath);

        public ProgressLoadResult Load()
        {
            var primaryExists = files.Exists(primaryPath);
            if (TryLoad(primaryPath, out var primary, out var primaryError))
                return new ProgressLoadResult(ProgressLoadStatus.LoadedPrimary, primary);
            if (TryLoad(backupPath, out var backup, out var backupError))
                return new ProgressLoadResult(ProgressLoadStatus.LoadedBackup, backup, primaryError);
            if (!primaryExists && !files.Exists(backupPath))
                return new ProgressLoadResult(ProgressLoadStatus.NewPlayer, MathGame.PlayerProgress.PlayerProgress.NewPlayer);
            var diagnostic = string.Join(" | ", new[] { primaryError, backupError });
            return new ProgressLoadResult(primaryError != null && primaryError.StartsWith("Read failed", StringComparison.Ordinal)
                ? ProgressLoadStatus.ReadFailedFallback : ProgressLoadStatus.InvalidDataFallback,
                MathGame.PlayerProgress.PlayerProgress.NewPlayer, diagnostic);
        }

        public ProgressSaveResult Save(MathGame.PlayerProgress.PlayerProgress progress)
        {
            if (!TryMap(progress, out var data, out var validationError))
                return new ProgressSaveResult(ProgressSaveStatus.InvalidProgress, validationError);
            try
            {
                var json = JsonUtility.ToJson(data, true);
                if (files.Exists(temporaryPath)) files.Delete(temporaryPath);
                files.WriteAllText(temporaryPath, json);
                if (!TryParse(files.ReadAllText(temporaryPath), out _, out var verifyError))
                    return new ProgressSaveResult(ProgressSaveStatus.WriteFailed, "Temporary save verification failed: " + verifyError);
                if (TryLoad(primaryPath, out _, out _)) files.Copy(primaryPath, backupPath, true);
                if (files.Exists(primaryPath)) files.Delete(primaryPath);
                files.Move(temporaryPath, primaryPath);
                return new ProgressSaveResult(ProgressSaveStatus.Saved);
            }
            catch (Exception exception)
            {
                try { if (files.Exists(temporaryPath)) files.Delete(temporaryPath); } catch { }
                return new ProgressSaveResult(ProgressSaveStatus.WriteFailed, exception.Message);
            }
        }

        private bool TryLoad(string path, out MathGame.PlayerProgress.PlayerProgress progress, out string error)
        {
            progress = null; error = null;
            if (!files.Exists(path)) { error = "Missing: " + path; return false; }
            try { return TryParse(files.ReadAllText(path), out progress, out error); }
            catch (Exception exception) { error = "Read failed: " + exception.Message; return false; }
        }

        private static bool TryParse(string json, out MathGame.PlayerProgress.PlayerProgress progress, out string error)
        {
            progress = null; error = null;
            if (string.IsNullOrWhiteSpace(json)) { error = "Save JSON is empty."; return false; }
            PlayerProgressSaveData data;
            try { data = JsonUtility.FromJson<PlayerProgressSaveData>(json); }
            catch (Exception exception) { error = "Malformed JSON: " + exception.Message; return false; }
            if (data == null || data.version < 1 || data.version > CurrentVersion) { error = "Unsupported save version."; return false; }
            if (data.runRecords == null || data.appliedRunIds == null) { error = "Required save fields are missing."; return false; }
            try
            {
                var records = new RunRecords(data.runRecords.bestScore, data.runRecords.bestSurvivalDuration,
                    data.runRecords.highestDifficultyReached, data.runRecords.bestCombo, data.runRecords.totalRuns);
                var settings = data.version >= 2 && data.settings != null
                    ? new PlayerSettings(data.settings.localeCode)
                    : PlayerSettings.Default;
                progress = new MathGame.PlayerProgress.PlayerProgress(records, data.appliedRunIds, settings);
                if (progress.AppliedRunIds.Count > records.TotalRuns) { error = "Applied run count exceeds total runs."; progress = null; return false; }
                return true;
            }
            catch (Exception exception) { error = "Invalid save values: " + exception.Message; return false; }
        }

        private static bool TryMap(MathGame.PlayerProgress.PlayerProgress progress, out PlayerProgressSaveData data, out string error)
        {
            data = null; error = null;
            if (progress == null) { error = "Progress is missing."; return false; }
            try
            {
                var records = progress.RunRecords;
                if (progress.AppliedRunIds.Count > records.TotalRuns) { error = "Applied run count exceeds total runs."; return false; }
                data = new PlayerProgressSaveData
                {
                    version = CurrentVersion,
                    runRecords = new RunRecordsSaveData
                    {
                        bestScore = records.BestScore, bestSurvivalDuration = records.BestSurvivalDuration,
                        highestDifficultyReached = records.HighestDifficultyReached, bestCombo = records.BestCombo,
                        totalRuns = records.TotalRuns
                    },
                    appliedRunIds = new string[progress.AppliedRunIds.Count],
                    settings = new PlayerSettingsSaveData { localeCode = progress.Settings.LocaleCode }
                };
                for (var i = 0; i < data.appliedRunIds.Length; i++) data.appliedRunIds[i] = progress.AppliedRunIds[i];
                return true;
            }
            catch (Exception exception) { error = exception.Message; return false; }
        }
    }
}
