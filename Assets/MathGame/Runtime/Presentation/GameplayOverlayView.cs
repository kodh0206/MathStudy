using System.Text;
using MathGame.Connection;
using MathGame.Board;
using System.Collections.Generic;
using UnityEngine;

namespace MathGame.Presentation.Unity
{
    // Placeholder presentation uses built-in TextMesh/LineRenderer only; concrete art bindings remain external.
    public sealed class GameplayOverlayView : MonoBehaviour
    {
        TextMesh hud;
        LineRenderer connection;

        public string HudText => hud == null ? string.Empty : hud.text;
        public int Target { get; private set; }

        void Awake()
        {
            var hudObject = new GameObject("GameplayHUD_AccessibleText");
            hudObject.transform.SetParent(transform, false);
            hudObject.transform.localPosition = new Vector3(0, 6, -1);
            hud = hudObject.AddComponent<TextMesh>();
            hud.anchor = TextAnchor.UpperLeft;
            hud.characterSize = .25f;

            var lineObject = new GameObject("ConnectionLine");
            lineObject.transform.SetParent(transform, false);
            connection = lineObject.AddComponent<LineRenderer>();
            connection.useWorldSpace = false;
            connection.widthMultiplier = .08f;
        }

        public void ApplyEnvelope(PresentationEnvelope envelope)
        {
            if (envelope?.Session == null || hud == null) return;
            var text = new StringBuilder();
            text.Append("Target ").Append(Target)
                .Append("  Moves ").Append(envelope.Session.RemainingMoves)
                .Append("  Score ").Append(envelope.Session.Score)
                .Append("  Restoration ").Append(envelope.Session.ProvisionalRestoration);
            if (envelope.Fever != null)
                text.Append("  Fever ").Append(envelope.Fever.Gauge).Append('/').Append(envelope.Fever.MaximumGauge)
                    .Append(" ").Append(envelope.Fever.RemainingSeconds.ToString("0.0"));
            for (var i = 0; i < envelope.Session.Objectives.Count; i++)
            {
                var objective = envelope.Session.Objectives[i];
                text.Append("\nObjective ").Append(i + 1).Append(": ")
                    .Append(objective.Current).Append('/').Append(objective.Required);
            }
            hud.text = text.ToString();
        }

        public void SetTarget(int target)
        {
            Target = target;
            if (hud != null && !hud.text.StartsWith("Target " + target + " ")) hud.text = "Target " + target + "  " + hud.text;
        }

        public void ShowPath(ConnectionPathSnapshot path)
        {
            if (connection == null) return;
            var entries = path?.Entries;
            connection.positionCount = entries?.Count ?? 0;
            if (entries == null) return;
            for (var i = 0; i < entries.Count; i++)
                connection.SetPosition(i, new Vector3(entries[i].Position.Column, entries[i].Position.Row, -.2f));
        }

        public void ShowPositions(IReadOnlyList<BoardPosition> positions, long sum)
        {
            if (connection == null) return;
            connection.positionCount = positions?.Count ?? 0;
            if (positions != null)
                for (var i = 0; i < positions.Count; i++)
                    connection.SetPosition(i, new Vector3(positions[i].Column, positions[i].Row, -.2f));
            if (hud != null) hud.text = "Sum " + sum + "\n" + hud.text;
        }

        public void ShowResult(bool success, string detail)
        {
            if (hud != null) hud.text = (success ? "SUCCESS" : "FAILED") + "\n" + (detail ?? string.Empty);
        }

        public void ShowStatus(string status)
        {
            if (hud != null) hud.text = status + "\n" + hud.text;
        }

        public void ShowMilestone(long milestone)
        {
            ShowStatus("RESTORATION " + milestone + "%");
        }
    }
}
