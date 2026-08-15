using System.Collections.Generic;
using MathGame.Answer;
using MathGame.LocalSave;
using MathGame.PlayerProgress;
using MathGame.SurvivalRun;
using NUnit.Framework;
using ProgressModel = MathGame.PlayerProgress.PlayerProgress;

namespace MathGame.Tests
{
    public sealed class PlayerProgressTests
    {
        [Test]
        public void DefaultAndFirstRunUpdateEveryRecordAndTruthfulFlags()
        {
            var service = new PlayerProgressService(ProgressModel.NewPlayer);
            var update = service.ApplyCompletedRun(Result("run-1", 100, 12.5, 3, 2));
            Assert.That(update.Status, Is.EqualTo(ProgressUpdateStatus.Applied));
            Assert.That(update.NewBestScore && update.NewBestSurvivalDuration && update.NewBestCombo && update.NewHighestDifficulty, Is.True);
            Assert.That(update.After.RunRecords.TotalRuns, Is.EqualTo(1));
            Assert.That(update.After.RunRecords.BestScore, Is.EqualTo(100));
        }

        [Test]
        public void LowerRunDoesNotReduceRecordsAndDuplicateDoesNotCount()
        {
            var service = new PlayerProgressService(ProgressModel.NewPlayer);
            var best = Result("run-best", 500, 30, 6, 4);
            service.ApplyCompletedRun(best);
            var lower = service.ApplyCompletedRun(Result("run-low", 100, 10, 2, 1));
            Assert.That(lower.NewBestScore || lower.NewBestSurvivalDuration || lower.NewBestCombo || lower.NewHighestDifficulty, Is.False);
            Assert.That(lower.After.RunRecords.TotalRuns, Is.EqualTo(2));
            Assert.That(lower.After.RunRecords.BestScore, Is.EqualTo(500));
            Assert.That(service.ApplyCompletedRun(best).Status, Is.EqualTo(ProgressUpdateStatus.DuplicateRun));
            Assert.That(service.Current.RunRecords.TotalRuns, Is.EqualTo(2));
        }

        [Test]
        public void IndependentRecordsCanImproveAcrossDifferentRuns()
        {
            var service = new PlayerProgressService(ProgressModel.NewPlayer);
            service.ApplyCompletedRun(Result("a", 500, 10, 1, 1));
            var update = service.ApplyCompletedRun(Result("b", 400, 20, 5, 3));
            Assert.That(update.NewBestScore, Is.False);
            Assert.That(update.NewBestSurvivalDuration && update.NewBestCombo && update.NewHighestDifficulty, Is.True);
            Assert.That(update.After.RunRecords.BestScore, Is.EqualTo(500));
        }

        [Test]
        public void LocalSaveRoundTripsAndSurvivesRepositoryRecreation()
        {
            var files = new MemoryFiles();
            var repository = new LocalPlayerProgressRepository("test", files);
            var service = new PlayerProgressService(ProgressModel.NewPlayer);
            var progress = service.ApplyCompletedRun(Result("saved", 250, 17, 4, 2)).After;
            Assert.That(repository.Save(progress).Status, Is.EqualTo(ProgressSaveStatus.Saved));
            Assert.That(files.Values["test\\player_progress.json"], Does.Contain("\"version\": 1"));
            var loaded = new LocalPlayerProgressRepository("test", files).Load();
            Assert.That(loaded.Status, Is.EqualTo(ProgressLoadStatus.LoadedPrimary));
            Assert.That(loaded.Progress.RunRecords.BestScore, Is.EqualTo(250));
            Assert.That(new PlayerProgressService(loaded.Progress).ApplyCompletedRun(Result("saved", 250, 17, 4, 2)).Status,
                Is.EqualTo(ProgressUpdateStatus.DuplicateRun));
        }

        [Test]
        public void MissingIsNewPlayerAndCorruptPrimaryRecoversBackup()
        {
            var files = new MemoryFiles();
            var repository = new LocalPlayerProgressRepository("test", files);
            Assert.That(repository.Load().Status, Is.EqualTo(ProgressLoadStatus.NewPlayer));
            var service = new PlayerProgressService(ProgressModel.NewPlayer);
            repository.Save(service.ApplyCompletedRun(Result("one", 10, 1, 1, 1)).After);
            repository.Save(service.ApplyCompletedRun(Result("two", 20, 2, 2, 2)).After);
            files.Values["test\\player_progress.json"] = "{ malformed";
            var recovered = repository.Load();
            Assert.That(recovered.Status, Is.EqualTo(ProgressLoadStatus.LoadedBackup));
            Assert.That(recovered.Progress.RunRecords.TotalRuns, Is.EqualTo(1));
        }

        [Test]
        public void InvalidPrimaryAndBackupFallBackWithoutCrashing()
        {
            var files = new MemoryFiles();
            files.Values["test\\player_progress.json"] = "{}";
            files.Values["test\\player_progress.backup.json"] = "{}";
            var loaded = new LocalPlayerProgressRepository("test", files).Load();
            Assert.That(loaded.Status, Is.EqualTo(ProgressLoadStatus.InvalidDataFallback));
            Assert.That(loaded.Progress.RunRecords.TotalRuns, Is.Zero);
        }

        [Test]
        public void TotalRunOverflowRejectsAtomically()
        {
            var initial = new ProgressModel(new RunRecords(10, 5, 2, 3, long.MaxValue), new[] { "old" });
            var service = new PlayerProgressService(initial);
            var update = service.ApplyCompletedRun(Result("new", 20, 6, 4, 3));
            Assert.That(update.Status, Is.EqualTo(ProgressUpdateStatus.Overflow));
            Assert.That(service.Current, Is.SameAs(initial));
            Assert.That(service.Current.AppliedRunIds, Does.Not.Contain("new"));
        }

        [Test]
        public void AlteredTemporaryWriteIsRejectedAndMoveFailureRetainsRecoverableBackup()
        {
            var files = new MemoryFiles();
            var repository = new LocalPlayerProgressRepository("test", files);
            var service = new PlayerProgressService(ProgressModel.NewPlayer);
            var first = service.ApplyCompletedRun(Result("first", 10, 1, 1, 1)).After;
            Assert.That(repository.Save(first).Succeeded, Is.True);

            files.AlterNextWrite = true;
            Assert.That(repository.Save(first).Status, Is.EqualTo(ProgressSaveStatus.WriteFailed));
            Assert.That(repository.Load().Status, Is.EqualTo(ProgressLoadStatus.LoadedPrimary));

            var second = service.ApplyCompletedRun(Result("second", 20, 2, 2, 2)).After;
            files.ThrowOnNextMove = true;
            Assert.That(repository.Save(second).Status, Is.EqualTo(ProgressSaveStatus.WriteFailed));
            var recovered = repository.Load();
            Assert.That(recovered.Status, Is.EqualTo(ProgressLoadStatus.LoadedBackup));
            Assert.That(recovered.Progress.RunRecords.TotalRuns, Is.EqualTo(1));
        }

        private static RunResult Result(string id, long score, double duration, int combo, int tier)
        {
            var run = new SurvivalRunSession(SurvivalRunConfig.TemporaryPrototype, id);
            run.RecordStatistics(score, combo);
            run.Tick(duration, false);
            for (var i = 1; i <= tier * 6; i++)
            {
                run.PrepareCorrectCycle(i, SpeedGrade.Normal, out var plan);
                run.CommitCorrectCycle(plan);
            }
            return run.EndExactlyOnce();
        }

        private sealed class MemoryFiles : IProgressFileStore
        {
            public readonly Dictionary<string, string> Values = new Dictionary<string, string>();
            public bool AlterNextWrite;
            public bool ThrowOnNextMove;
            public bool Exists(string path) => Values.ContainsKey(path);
            public string ReadAllText(string path) => Values[path];
            public void WriteAllText(string path, string contents)
            {
                Values[path] = AlterNextWrite ? "{}" : contents;
                AlterNextWrite = false;
            }
            public void Copy(string source, string destination, bool overwrite) => Values[destination] = Values[source];
            public void Move(string source, string destination)
            {
                if (ThrowOnNextMove) { ThrowOnNextMove = false; throw new System.IO.IOException("Injected move failure."); }
                Values[destination] = Values[source]; Values.Remove(source);
            }
            public void Delete(string path) => Values.Remove(path);
        }
    }
}
