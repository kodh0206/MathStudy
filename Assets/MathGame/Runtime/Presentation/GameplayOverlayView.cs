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
        LineRenderer connection;

        public string HudText => string.Empty;
        public int Target { get; private set; }

        void Awake()
        {
            var lineObject = new GameObject("ConnectionLine");
            lineObject.transform.SetParent(transform, false);
            connection = lineObject.AddComponent<LineRenderer>();
            connection.useWorldSpace = false;
            connection.widthMultiplier = .08f;
        }

        public void ApplyEnvelope(PresentationEnvelope envelope)
        {
            // The responsive Canvas owned by PrototypeUILayout renders the HUD.
        }

        public void SetTarget(int target)
        {
            Target = target;
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
        }

        public void ShowResult(bool success, string detail)
        {
            // Result copy is rendered by the responsive prototype Canvas.
        }

        public void ShowStatus(string status)
        {
            // Status copy is rendered by the responsive prototype Canvas.
        }

        public void ShowMilestone(long milestone)
        {
            ShowStatus("RESTORATION " + milestone + "%");
        }
    }
}
