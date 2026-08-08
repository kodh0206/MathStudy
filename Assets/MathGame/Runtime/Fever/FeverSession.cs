using System;
using MathGame.Answer;
using MathGame.StageSession;

namespace MathGame.Fever
{
    internal sealed class FeverSession
    {
        private FeverSessionSnapshot snapshot;
        internal FeverSession(StageAttemptId nextExpected) { snapshot = new FeverSessionSnapshot(0, 0, 0, default); NextExpected = nextExpected; }
        internal StageAttemptId NextExpected { get; }
        internal FeverSessionSnapshot Snapshot => snapshot;
        internal bool Preview(StageAttemptId id, AnswerResult answer, out FeverSessionSnapshot next, out StageAttemptRules rules, out FeverGameplayModifiers modifiers)
        {
            next = snapshot; rules = null; modifiers = FeverGameplayModifiers.None;
            if (!id.IsValid || id.Value != (snapshot.LastCommittedAttemptId.IsValid ? snapshot.LastCommittedAttemptId.Value + 1 : NextExpected.Value) || answer == null || (answer.Outcome != AnswerOutcome.Correct && answer.Outcome != AnswerOutcome.Miss)) return false;
            var combo = answer.IsCorrect ? checked(snapshot.CurrentCombo + 1) : 0;
            var total = answer.IsCorrect ? checked(snapshot.TotalCorrectAnswers + 1) : snapshot.TotalCorrectAnswers;
            next = new FeverSessionSnapshot(total, combo, Math.Max(snapshot.MaximumCombo, combo), id);
            var multiplier = Multiplier(combo);
            rules = StageAttemptRules.CreateFever(multiplier);
            if (answer.IsCorrect) modifiers = new FeverGameplayModifiers(multiplier);
            return true;
        }
        internal void Commit(FeverSessionSnapshot next) => snapshot = next;
        internal static int Multiplier(int combo) => combo <= 1 ? 1 : combo == 2 ? 2 : combo == 3 ? 3 : 5;
    }
}
