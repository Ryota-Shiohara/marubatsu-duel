using System;
using System.Collections.Generic;

namespace Marubatsu
{
    /// <summary>Converts stable paper observations into at most one game move.</summary>
    public sealed class PaperMoveInput
    {
        readonly GameSession session;
        int resetRevision = -1;
        bool emptyPaperConfirmed;

        public PaperMoveInput(GameSession session)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public string Observe(IReadOnlyList<CellMark> stableCells)
        {
            if (stableCells == null || stableCells.Count != GameSession.CellCount)
                throw new ArgumentException("Expected nine stable cell observations.", nameof(stableCells));

            BoardSnapshot snapshot = session.GetSnapshot();
            if (snapshot.Outcome != OutcomeType.Playing) return snapshot.Outcome.ToString();
            if (!snapshot.LastMove.HasValue && snapshot.Revision != resetRevision)
            {
                resetRevision = snapshot.Revision;
                emptyPaperConfirmed = false;
            }

            if (!emptyPaperConfirmed && !snapshot.LastMove.HasValue)
            {
                for (int index = 0; index < stableCells.Count; index++)
                    if (stableCells[index] != CellMark.Empty) return "Show empty paper";
                emptyPaperConfirmed = true;
            }

            MoveCandidate? candidate = null;
            for (int index = 0; index < stableCells.Count; index++)
            {
                CellMark observed = stableCells[index];
                if (observed == CellMark.Unknown) return "Waiting for stable cells";
                MarkType mark = observed == CellMark.Circle ? MarkType.Circle :
                    observed == CellMark.Cross ? MarkType.Cross : MarkType.Empty;
                MarkType accepted = snapshot.Cells[index];
                if (accepted != MarkType.Empty)
                {
                    if (mark != accepted) return "Paper differs from game";
                    continue;
                }

                if (mark == MarkType.Empty) continue;
                if (candidate.HasValue) return "Multiple new marks";
                candidate = new MoveCandidate(index, mark);
            }

            if (!candidate.HasValue) return $"{snapshot.NextTurn} to move";
            if (candidate.Value.Mark != snapshot.NextTurn) return $"{snapshot.NextTurn} to move";
            return session.TryApplyMove(candidate.Value) ? "Move accepted" : "Move rejected";
        }
    }
}
