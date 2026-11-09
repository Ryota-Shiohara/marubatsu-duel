using System;
using UnityEngine;

public sealed class TicTacToeGameManager : MonoBehaviour
{
    [SerializeField] bool enableKeyboardMoves;

    public GameSession Session { get; } = new GameSession();
    public event Action<BoardSnapshot> OnSnapshotChanged
    {
        add => Session.OnSnapshotChanged += value;
        remove => Session.OnSnapshotChanged -= value;
    }

    public bool TryApplyMove(MoveCandidate candidate) => Session.TryApplyMove(candidate);
    public BoardSnapshot GetSnapshot() => Session.GetSnapshot();
    public void ResetGame() => Session.ResetGame();

    void Update()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetGame();
            return;
        }

        if (!enableKeyboardMoves) return;
        var turn = GetSnapshot().NextTurn;
        if (!turn.HasValue) return;
        for (int index = 0; index < GameSession.CellCount; index++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha0 + index) || Input.GetKeyDown(KeyCode.Keypad0 + index))
            {
                TryApplyMove(new MoveCandidate(index, turn.Value));
                break;
            }
        }
#endif
    }
}
