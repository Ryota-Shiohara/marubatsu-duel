using System;
using System.Collections.Generic;

public sealed class GameSession
{
    public const int CellCount = 9;

    static readonly int[][] WinLines =
    {
        new[] { 0, 1, 2 }, new[] { 3, 4, 5 }, new[] { 6, 7, 8 },
        new[] { 0, 3, 6 }, new[] { 1, 4, 7 }, new[] { 2, 5, 8 },
        new[] { 0, 4, 8 }, new[] { 2, 4, 6 }
    };

    readonly MarkType[] board = new MarkType[CellCount];
    readonly List<ThreatInfo> threats = new List<ThreatInfo>();
    int revision;
    MarkType currentTurn = MarkType.Circle;
    OutcomeType outcome = OutcomeType.Playing;
    MoveCandidate? lastMove;
    int[] winningCells = Array.Empty<int>();

    public event Action<BoardSnapshot> OnSnapshotChanged;

    public bool TryApplyMove(MoveCandidate candidate)
    {
        if (outcome != OutcomeType.Playing || candidate.CellIndex < 0 ||
            candidate.CellIndex >= CellCount || candidate.Mark != currentTurn ||
            board[candidate.CellIndex] != MarkType.Empty)
            return false;

        board[candidate.CellIndex] = candidate.Mark;
        lastMove = candidate;
        revision++;

        winningCells = GetWinningLine(candidate.Mark);
        threats.Clear();
        if (winningCells.Length != 0)
            outcome = candidate.Mark == MarkType.Circle ? OutcomeType.CircleWon : OutcomeType.CrossWon;
        else if (Array.IndexOf(board, MarkType.Empty) < 0)
            outcome = OutcomeType.Draw;
        else
        {
            CalculateThreats();
            currentTurn = currentTurn == MarkType.Circle ? MarkType.Cross : MarkType.Circle;
        }

        OnSnapshotChanged?.Invoke(GetSnapshot());
        return true;
    }

    public BoardSnapshot GetSnapshot()
    {
        var copiedThreats = new List<ThreatInfo>(threats.Count);
        foreach (var threat in threats)
        {
            var copy = threat;
            copy.FilledCells = (int[])threat.FilledCells.Clone();
            copiedThreats.Add(copy);
        }

        return new BoardSnapshot
        {
            Revision = revision,
            Cells = (MarkType[])board.Clone(),
            NextTurn = outcome == OutcomeType.Playing ? currentTurn : (MarkType?)null,
            LastMove = lastMove,
            Threats = copiedThreats,
            Outcome = outcome,
            WinningCells = (int[])winningCells.Clone()
        };
    }

    public void ResetGame()
    {
        Array.Clear(board, 0, board.Length);
        revision++;
        currentTurn = MarkType.Circle;
        outcome = OutcomeType.Playing;
        lastMove = null;
        winningCells = Array.Empty<int>();
        threats.Clear();
        OnSnapshotChanged?.Invoke(GetSnapshot());
    }

    int[] GetWinningLine(MarkType mark)
    {
        foreach (var line in WinLines)
            if (board[line[0]] == mark && board[line[1]] == mark && board[line[2]] == mark)
                return line;
        return Array.Empty<int>();
    }

    void CalculateThreats()
    {
        foreach (var line in WinLines)
        {
            for (int offset = 0; offset < line.Length; offset++)
            {
                int empty = line[offset];
                if (board[empty] != MarkType.Empty) continue;

                int first = line[(offset + 1) % 3];
                int second = line[(offset + 2) % 3];
                MarkType mark = board[first];
                if (mark != MarkType.Empty && mark == board[second])
                    threats.Add(new ThreatInfo
                    {
                        Mark = mark,
                        FilledCells = new[] { Math.Min(first, second), Math.Max(first, second) },
                        EmptyCell = empty
                    });
                break;
            }
        }
    }
}
