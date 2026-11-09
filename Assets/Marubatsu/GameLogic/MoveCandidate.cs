using System;

public enum MarkType
{
    Empty,
    Circle,
    Cross
}

[Serializable]
public struct MoveCandidate
{
    public int CellIndex;
    public MarkType Mark;

    public MoveCandidate(int cellIndex, MarkType mark)
    {
        CellIndex = cellIndex;
        Mark = mark;
    }
}
