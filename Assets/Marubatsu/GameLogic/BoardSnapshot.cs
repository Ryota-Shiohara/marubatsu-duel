using System;
using System.Collections.Generic;

public enum OutcomeType
{
    Playing,
    CircleWon,
    CrossWon,
    Draw
}

[Serializable]
public struct ThreatInfo
{
    public MarkType Mark;
    public int[] FilledCells;
    public int EmptyCell;
}

/// <summary>A detached copy of the session state. Treat its collections as read-only.</summary>
[Serializable]
public struct BoardSnapshot
{
    public int Revision;
    public MarkType[] Cells;
    public MarkType? NextTurn;
    public MoveCandidate? LastMove;
    public List<ThreatInfo> Threats;
    public OutcomeType Outcome;
    public int[] WinningCells;
}
