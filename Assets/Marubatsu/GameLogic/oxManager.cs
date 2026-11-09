using System;
using System.Collections.Generic;
using UnityEngine;

// --- 仕様書に基づく型定義 ---

public enum MarkType
{
    Empty = 0,
    Circle = 1, // ○
    Cross = 2   // ×
}

public enum OutcomeType
{
    Playing,
    CircleWon,
    CrossWon,
    Draw
}

[Serializable]
public struct MoveCandidate
{
    public int CellIndex; // 0〜8
    public MarkType Mark; // Circle / Cross

    public MoveCandidate(int cellIndex, MarkType mark)
    {
        CellIndex = cellIndex;
        Mark = mark;
    }
}

[Serializable]
public struct ThreatInfo
{
    public MarkType Mark;       // リーチしている記号
    public int[] FilledCells;   // 揃っている2マス
    public int EmptyCell;       // 残りの空きマス
}

[Serializable]
public struct BoardSnapshot
{
    public int Revision;                // 更新番号
    public MarkType[] Cells;            // マス目 (9要素)
    public MarkType? NextTurn;          // 次の手番（終局時は null）
    public MoveCandidate? LastMove;     // 最後に受理した着手（初期/リセット直後は null）
    public List<ThreatInfo> Threats;    // リーチ一覧
    public OutcomeType Outcome;         // 勝敗状態
    public int[] WinningCells;          // 勝利ラインの3マス（それ以外は空配列）
}

// --- メインロジック ---

public class TicTacToeGameManager : MonoBehaviour
{
    // 盤面状態
    private MarkType[] board = new MarkType[9];
    private int revision = 0;
    private MarkType currentTurn = MarkType.Circle; // ○先手
    private OutcomeType currentOutcome = OutcomeType.Playing;
    private MoveCandidate? lastMove = null;
    private int[] winningCells = new int[0];
    private List<ThreatInfo> threats = new List<ThreatInfo>();

    // 勝利ライン定義 (8パターン)
    private readonly int[][] winLines = new int[][]
    {
        new int[] { 0, 1, 2 }, new int[] { 3, 4, 5 }, new int[] { 6, 7, 8 }, // 横
        new int[] { 0, 3, 6 }, new int[] { 1, 4, 7 }, new int[] { 2, 5, 8 }, // 縦
        new int[] { 0, 4, 8 }, new int[] { 2, 4, 6 }                          // 斜め
    };

    // 仕様書で定義された変更通知イベント
    public event Action<BoardSnapshot> OnSnapshotChanged;

    private void Start()
    {
        ResetGame();
    }

    private void Update()
    {
        // ----------------------------------------------------
        // カメラなし仮入力テスト機能（数字キー 0~8 で入力）
        // ----------------------------------------------------
        for (int i = 0; i <= 8; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha0 + i) || Input.GetKeyDown(KeyCode.Keypad0 + i))
            {
                var candidate = new MoveCandidate(i, currentTurn);
                Debug.Log($"[仮入力] マス {i} に {currentTurn} を試行");
                TryApplyMove(candidate);
            }
        }

        // 'R' キーで盤面リセット
        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetGame();
        }
    }

    /// <summary>
    /// 認識側・仮入力からの着手受入メイン処理
    /// </summary>
    public bool TryApplyMove(MoveCandidate candidate)
    {
        // 1. 終局後の着手拒否
        if (currentOutcome != OutcomeType.Playing)
        {
            Debug.LogWarning("[拒否] 終局後の着手は受け付けません。");
            return false;
        }

        // 2. マス番号の範囲チェック (0〜8)
        if (candidate.CellIndex < 0 || candidate.CellIndex >= 9)
        {
            Debug.LogWarning($"[拒否] 無効なマス番号: {candidate.CellIndex}");
            return false;
        }

        // 3. 手番一致チェック
        if (candidate.Mark != currentTurn)
        {
            Debug.LogWarning($"[拒否] 手番違い。現在の手番: {currentTurn}, 入力: {candidate.Mark}");
            return false;
        }

        // 4. 空欄チェック
        if (board[candidate.CellIndex] != MarkType.Empty)
        {
            Debug.LogWarning($"[拒否] マス {candidate.CellIndex} は既に埋まっています。");
            return false;
        }

        // --- 着手の受理と更新 ---
        board[candidate.CellIndex] = candidate.Mark;
        lastMove = candidate;
        revision++;

        // 勝敗・引き分けのチェック
        int[] winLine = GetWinningLine(candidate.Mark);
        if (winLine != null)
        {
            currentOutcome = (candidate.Mark == MarkType.Circle) ? OutcomeType.CircleWon : OutcomeType.CrossWon;
            winningCells = winLine;
            threats.Clear(); // 終局時は Threats は空
            Debug.Log($"🎉 【勝敗確定】 {currentOutcome} の勝利！");
        }
        else if (IsBoardFull())
        {
            currentOutcome = OutcomeType.Draw;
            winningCells = new int[0];
            threats.Clear(); // 終局時は Threats は空
            Debug.Log("🤝 【勝敗確定】 引き分け！");
        }
        else
        {
            // 対局継続中：リーチ計算と手番交代
            CalculateThreats();
            currentTurn = (currentTurn == MarkType.Circle) ? MarkType.Cross : MarkType.Circle;
        }

        // 状態変更通知
        OnSnapshotChanged?.Invoke(GetSnapshot());
        return true;
    }

    /// <summary>
    /// 現在の最新盤面スナップショットを取得
    /// </summary>
    public BoardSnapshot GetSnapshot()
    {
        MarkType[] cellsCopy = new MarkType[9];
        Array.Copy(board, cellsCopy, 9);

        return new BoardSnapshot
        {
            Revision = revision,
            Cells = cellsCopy,
            NextTurn = (currentOutcome == OutcomeType.Playing) ? currentTurn : (MarkType?)null,
            LastMove = lastMove,
            Threats = new List<ThreatInfo>(threats),
            Outcome = currentOutcome,
            WinningCells = (int[])winningCells.Clone()
        };
    }

    /// <summary>
    /// 盤面のリセット
    /// </summary>
    public void ResetGame()
    {
        for (int i = 0; i < 9; i++)
        {
            board[i] = MarkType.Empty;
        }

        revision++;
        currentTurn = MarkType.Circle;
        currentOutcome = OutcomeType.Playing;
        lastMove = null;
        winningCells = new int[0];
        threats.Clear();

        Debug.Log("🔄 盤面をリセットしました（先手: ○）");
        OnSnapshotChanged?.Invoke(GetSnapshot());
    }

    // --- 内部判定処理 ---

    private int[] GetWinningLine(MarkType mark)
    {
        foreach (var line in winLines)
        {
            if (board[line[0]] == mark && board[line[1]] == mark && board[line[2]] == mark)
            {
                return line;
            }
        }
        return null;
    }

    private void CalculateThreats()
    {
        threats.Clear();
        foreach (var line in winLines)
        {
            CheckLineThreat(line, MarkType.Circle);
            CheckLineThreat(line, MarkType.Cross);
        }
    }

    private void CheckLineThreat(int[] line, MarkType mark)
    {
        List<int> filled = new List<int>();
        int emptyCell = -1;

        foreach (int idx in line)
        {
            if (board[idx] == mark)
            {
                filled.Add(idx);
            }
            else if (board[idx] == MarkType.Empty)
            {
                emptyCell = idx;
            }
        }

        // 2マス埋まっていて1マスが空いている場合がリーチ
        if (filled.Count == 2 && emptyCell != -1)
        {
            threats.Add(new ThreatInfo
            {
                Mark = mark,
                FilledCells = filled.ToArray(),
                EmptyCell = emptyCell
            });
            Debug.Log($"⚡ 【リーチ検知】 {mark} がマス [{filled[0]}, {filled[1]}] を指定。残りマス: {emptyCell}");
        }
    }

    private bool IsBoardFull()
    {
        foreach (var cell in board)
        {
            if (cell == MarkType.Empty) return false;
        }
        return true;
    }
}