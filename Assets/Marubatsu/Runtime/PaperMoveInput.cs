using System;
using System.Collections.Generic;

namespace Marubatsu
{
    public enum PaperMoveStatus
    {
        WaitingForEmptyPaper,
        WaitingForStableCells,
        BoardMismatch,
        MultipleMarks,
        WrongTurn,
        Ready,
        Accepted,
        Rejected,
        Finished
    }

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

        public PaperMoveStatus Observe(IReadOnlyList<CellMark> stableCells)
        {
            if (stableCells == null || stableCells.Count != GameSession.CellCount)
                throw new ArgumentException("Expected nine stable cell observations.", nameof(stableCells));

            BoardSnapshot snapshot = session.GetSnapshot();
            if (snapshot.Outcome != OutcomeType.Playing) return PaperMoveStatus.Finished;
            if (!snapshot.LastMove.HasValue && snapshot.Revision != resetRevision)
            {
                resetRevision = snapshot.Revision;
                emptyPaperConfirmed = false;
            }

            if (!emptyPaperConfirmed && !snapshot.LastMove.HasValue)
            {
                for (int index = 0; index < stableCells.Count; index++)
                    if (stableCells[index] != CellMark.Empty) return PaperMoveStatus.WaitingForEmptyPaper;
                emptyPaperConfirmed = true;
            }

            MoveCandidate? candidate = null;
            for (int index = 0; index < stableCells.Count; index++)
            {
                CellMark observed = stableCells[index];
                if (observed == CellMark.Unknown) return PaperMoveStatus.WaitingForStableCells;
                MarkType mark = observed == CellMark.Circle ? MarkType.Circle :
                    observed == CellMark.Cross ? MarkType.Cross : MarkType.Empty;
                MarkType accepted = snapshot.Cells[index];
                if (accepted != MarkType.Empty)
                {
                    if (mark != accepted) return PaperMoveStatus.BoardMismatch;
                    continue;
                }

                if (mark == MarkType.Empty) continue;
                if (candidate.HasValue) return PaperMoveStatus.MultipleMarks;
                candidate = new MoveCandidate(index, mark);
            }

            if (!candidate.HasValue) return PaperMoveStatus.Ready;
            if (candidate.Value.Mark != snapshot.NextTurn) return PaperMoveStatus.WrongTurn;
            return session.TryApplyMove(candidate.Value) ? PaperMoveStatus.Accepted : PaperMoveStatus.Rejected;
        }
    }
}
