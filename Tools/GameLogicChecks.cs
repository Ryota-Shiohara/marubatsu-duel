using System;
using System.Linq;
using Marubatsu;

public static class GameLogicChecks
{
    public static string Run()
    {
        var game = new GameSession();
        int notifications = 0;
        game.OnSnapshotChanged += snapshot => notifications++;

        var initial = game.GetSnapshot();
        Require(initial.Revision == 0 && initial.NextTurn == MarkType.Circle &&
            !initial.LastMove.HasValue && initial.Cells.All(mark => mark == MarkType.Empty), "initial state");
        Require(!game.TryApplyMove(new MoveCandidate(4, MarkType.Cross)) &&
            !game.TryApplyMove(new MoveCandidate(-1, MarkType.Circle)) &&
            !game.TryApplyMove(new MoveCandidate(9, MarkType.Circle)) && notifications == 0,
            "illegal opening moves are silent");

        Move(game, 0, MarkType.Circle);
        Require(!game.TryApplyMove(new MoveCandidate(0, MarkType.Cross)) &&
            game.GetSnapshot().Revision == 1 && notifications == 1, "occupied cell is rejected");
        Move(game, 3, MarkType.Cross);
        Move(game, 1, MarkType.Circle);
        Move(game, 4, MarkType.Cross);
        var threats = game.GetSnapshot();
        Require(threats.LastMove.Value.CellIndex == 4 && threats.NextTurn == MarkType.Circle,
            "central move and next turn are published");
        Require(threats.Threats.Any(t => t.Mark == MarkType.Circle && t.EmptyCell == 2 &&
            t.FilledCells.SequenceEqual(new[] { 0, 1 })) &&
            threats.Threats.Any(t => t.Mark == MarkType.Cross && t.EmptyCell == 5 &&
            t.FilledCells.SequenceEqual(new[] { 3, 4 })), "both players' threats are reported");
        threats.Cells[0] = MarkType.Empty;
        threats.Threats[0].FilledCells[0] = 8;
        Require(game.GetSnapshot().Cells[0] == MarkType.Circle &&
            game.GetSnapshot().Threats[0].FilledCells[0] == 0, "snapshot mutations cannot leak into session");
        Move(game, 2, MarkType.Circle);
        var won = game.GetSnapshot();
        Require(won.Outcome == OutcomeType.CircleWon && !won.NextTurn.HasValue &&
            won.WinningCells.SequenceEqual(new[] { 0, 1, 2 }) && won.Threats.Count == 0 &&
            threats.Threats.Count == 2 && notifications == 5, "victory and past snapshot");
        Require(!game.TryApplyMove(new MoveCandidate(8, MarkType.Cross)) && notifications == 5,
            "terminal state rejects moves");
        game.ResetGame();
        var reset = game.GetSnapshot();
        Require(reset.Revision == 6 && notifications == 6 && reset.NextTurn == MarkType.Circle &&
            reset.Outcome == OutcomeType.Playing && !reset.LastMove.HasValue &&
            reset.Cells.All(mark => mark == MarkType.Empty) && reset.WinningCells.Length == 0, "reset");

        Play(game, 0, 3, 1, 4, 8, 5);
        Require(game.GetSnapshot().Outcome == OutcomeType.CrossWon, "cross wins");
        game.ResetGame();
        Play(game, 0, 1, 2, 4, 3, 5, 7, 6, 8);
        var draw = game.GetSnapshot();
        Require(draw.Outcome == OutcomeType.Draw && draw.WinningCells.Length == 0 &&
            !draw.NextTurn.HasValue && draw.Threats.Count == 0, "draw");
        game.ResetGame();
        Play(game, 0, 1, 2, 3, 6, 5, 8, 7, 4);
        Require(game.GetSnapshot().WinningCells.SequenceEqual(new[] { 0, 4, 8 }),
            "last-cell double win chooses the first line");

        CheckPaperInput();
        return "PASS: game state, both winners, draw, snapshot isolation, and paper input gates.";
    }

    static void CheckPaperInput()
    {
        var game = new GameSession();
        var input = new PaperMoveInput(game);
        var cells = Enumerable.Repeat(CellMark.Empty, 9).ToArray();
        cells[4] = CellMark.Circle;
        input.Observe(cells);
        Require(game.GetSnapshot().Revision == 0, "old marks cannot start a match");
        cells[4] = CellMark.Empty;
        input.Observe(cells);

        cells[4] = CellMark.Circle;
        cells[0] = CellMark.Cross;
        input.Observe(cells);
        Require(game.GetSnapshot().Revision == 0, "two simultaneous marks are held");
        cells[0] = CellMark.Unknown;
        input.Observe(cells);
        Require(game.GetSnapshot().Revision == 0, "an unsettled cell is not assumed empty");
        cells[0] = CellMark.Empty;
        input.Observe(cells);
        input.Observe(cells);
        Require(game.GetSnapshot().Revision == 1 && game.GetSnapshot().Cells[4] == MarkType.Circle,
            "one stable mark is accepted once");

        cells[4] = CellMark.Empty;
        cells[0] = CellMark.Cross;
        input.Observe(cells);
        Require(game.GetSnapshot().Revision == 1, "changed paper cannot overwrite committed board");
        cells[4] = CellMark.Circle;
        input.Observe(cells);
        Require(game.GetSnapshot().Revision == 2 && game.GetSnapshot().Cells[0] == MarkType.Cross,
            "opponent's move is accepted once paper matches");
        game.ResetGame();
        input.Observe(cells);
        Require(game.GetSnapshot().Revision == 3 && !game.GetSnapshot().LastMove.HasValue,
            "reset requires a fresh empty observation");
    }

    static void Play(GameSession game, params int[] indices)
    {
        for (int turn = 0; turn < indices.Length; turn++)
            Move(game, indices[turn], turn % 2 == 0 ? MarkType.Circle : MarkType.Cross);
    }

    static void Move(GameSession game, int index, MarkType mark) =>
        Require(game.TryApplyMove(new MoveCandidate(index, mark)), $"valid move {index}, {mark}");

    static void Require(bool condition, string name)
    {
        if (!condition) throw new Exception("FAIL: " + name);
    }
}
