using System;
using UnityEngine;

namespace Marubatsu.Presentation
{
    public sealed class MatchHud : MonoBehaviour
    {
        static readonly Color Panel = new Color32(15, 23, 42, 255);
        static readonly Color PrimaryText = new Color32(248, 250, 252, 255);
        static readonly Color SecondaryText = new Color32(203, 213, 225, 255);
        static readonly Color CircleAccent = new Color32(125, 211, 252, 255);
        static readonly Color CrossAccent = new Color32(253, 186, 116, 255);
        static readonly Color ResultAccent = new Color32(253, 224, 133, 255);
        static readonly string[] JapaneseFonts =
            { "Yu Gothic UI", "Meiryo", "Yu Gothic", "メイリオ", "游ゴシック", "Noto Sans CJK JP" };

        GameSession session;
        BoardSnapshot snapshot;
        BoardTracker tracker;
        BoardImageExtractor extractor;
        Font font;
        GUIStyle captionStyle, headlineStyle, detailStyle, buttonStyle;

        public void Initialize(GameSession game)
        {
            session = game;
            snapshot = session.GetSnapshot();
            session.OnSnapshotChanged += OnSnapshotChanged;
        }

        public void AttachBoard(BoardTracker boardTracker, BoardImageExtractor boardExtractor)
        {
            tracker = boardTracker;
            extractor = boardExtractor;
        }

        void OnSnapshotChanged(BoardSnapshot current) => snapshot = current;

        void OnGUI()
        {
            if (session == null || (extractor != null && extractor.DiagnosticsVisible)) return;
            if (captionStyle == null) InitializeStyles();

            Rect safeArea = Screen.safeArea;
            float width = Mathf.Min(560, safeArea.width - 24);
            if (width <= 32) return;
            float x = safeArea.center.x - width / 2;
            float y = Screen.height - safeArea.yMax + 12;
            bool finished = snapshot.Outcome != OutcomeType.Playing;
            bool inlineButton = finished && width >= 460;
            float textWidth = width - 36 - (inlineButton ? 140 : 0);
            headlineStyle.fontSize = width >= 340 ? 24 : 19;
            var (headline, detail, accent) = Message();

            float headlineHeight = Mathf.Max(32, headlineStyle.CalcHeight(new GUIContent(headline), textWidth));
            float detailHeight = Mathf.Max(40, detailStyle.CalcHeight(new GUIContent(detail), textWidth));
            float bodyHeight = 44 + headlineHeight + detailHeight;
            float panelHeight = bodyHeight + (finished && !inlineButton ? 52 : 0);

            Fill(new Rect(x, y, width, panelHeight), Panel);
            Fill(new Rect(x, y, 4, panelHeight), accent);
            captionStyle.normal.textColor = accent;
            GUI.Label(new Rect(x + 18, y + 12, textWidth, 22), "MARUBATSU DUEL", captionStyle);
            GUI.Label(new Rect(x + 18, y + 34, textWidth, headlineHeight), headline, headlineStyle);
            GUI.Label(new Rect(x + 18, y + 34 + headlineHeight, textWidth, detailHeight), detail, detailStyle);

            if (finished)
            {
                var button = inlineButton
                    ? new Rect(x + width - 142, y + (bodyHeight - 44) / 2, 124, 44)
                    : new Rect(x + 18, y + bodyHeight - 4, width - 36, 44);
                if (GUI.Button(button, Text("次の対戦", "Play again"), buttonStyle))
                    session.ResetGame();
            }
        }

        (string headline, string detail, Color accent) Message()
        {
            switch (snapshot.Outcome)
            {
                case OutcomeType.CircleWon:
                    return (Text("○ やがぽんの勝ち", "O  Yagapon wins"), NextMatchHint(), CircleAccent);
                case OutcomeType.CrossWon:
                    return (Text("× 矢口君の勝ち", "X  Yaguchi wins"), NextMatchHint(), CrossAccent);
                case OutcomeType.Draw:
                    return (Text("引き分け", "Draw"), NextMatchHint(), ResultAccent);
            }

            if (tracker == null || !tracker.IsTracked)
                return (Text("盤面を映してください", "Show the board"),
                    Text("外周の模様までカメラに入れてください。", "Keep the whole border in the camera view."),
                    SecondaryText);

            if (extractor.PaperStatus == PaperMoveStatus.WaitingForEmptyPaper && extractor.HasFreshFrame)
                return (Text("空の盤面を映してください", "Show an empty board"),
                    Text("空の紙を映すと、○から始まります。", "Use a fresh sheet to start with O."),
                    SecondaryText);

            bool circleTurn = snapshot.NextTurn == MarkType.Circle;
            string title = circleTurn ? Text("○ やがぽんの番", "O  Yagapon's turn") :
                Text("× 矢口君の番", "X  Yaguchi's turn");
            string instruction = !extractor.HasFreshFrame
                ? Text("盤面を動かさず、読み取りを待ってください。", "Hold the board still while it is read.")
                : MoveHint(extractor.PaperStatus, circleTurn);
            return (title, instruction, circleTurn ? CircleAccent : CrossAccent);
        }

        string MoveHint(PaperMoveStatus status, bool circleTurn)
        {
            switch (status)
            {
                case PaperMoveStatus.MultipleMarks:
                    return Text("新しい記号は1マスだけにしてください。", "Leave only one new mark on the board.");
                case PaperMoveStatus.WrongTurn:
                    return circleTurn
                        ? Text("新しく書いた×を消し、○を書いてください。", "Erase the new X and write O.")
                        : Text("新しく書いた○を消し、×を書いてください。", "Erase the new O and write X.");
                case PaperMoveStatus.BoardMismatch:
                    return Text("紙の記号を確認し、受理済みの配置に戻してください。", "Restore the marks that were already accepted.");
                case PaperMoveStatus.WaitingForStableCells:
                case PaperMoveStatus.Rejected:
                    return Text("手を離し、盤面を少し止めてください。", "Move your hand away and hold the board still.");
                default:
                    return circleTurn
                        ? Text("空きマスに○を1つ書いてください。", "Write one O in an empty cell.")
                        : Text("空きマスに×を1つ書いてください。", "Write one X in an empty cell.");
            }
        }

        string NextMatchHint() => Text("次の対戦には空の盤面を用意してください。", "Have an empty board ready for the next match.");

        string Text(string japanese, string english) => font != null ? japanese : english;

        void InitializeStyles()
        {
            string[] installed = Font.GetOSInstalledFontNames();
            foreach (string candidate in JapaneseFonts)
            {
                if (!Array.Exists(installed, name => string.Equals(name, candidate, StringComparison.OrdinalIgnoreCase)))
                    continue;
                font = Font.CreateDynamicFontFromOSFont(candidate, 16);
                if (font != null) break;
            }

            captionStyle = LabelStyle(12, FontStyle.Bold, SecondaryText);
            headlineStyle = LabelStyle(24, FontStyle.Bold, PrimaryText);
            detailStyle = LabelStyle(15, FontStyle.Normal, SecondaryText);
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 16 };
            if (font != null) buttonStyle.font = font;
        }

        GUIStyle LabelStyle(int size, FontStyle weight, Color color)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = size,
                fontStyle = weight,
                padding = new RectOffset(),
                wordWrap = true
            };
            if (font != null) style.font = font;
            style.normal.textColor = color;
            return style;
        }

        static void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        void OnDestroy()
        {
            if (session != null) session.OnSnapshotChanged -= OnSnapshotChanged;
            if (font != null) Destroy(font);
        }
    }
}
