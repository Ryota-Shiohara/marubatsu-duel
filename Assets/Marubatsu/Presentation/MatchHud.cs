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
        static readonly string[] JapaneseNames = { "やがぽん", "みたぬき", "矢口君" };
        static readonly string[] EnglishNames = { "Yagapon", "Mitanuki", "Yaguchi" };

        GameSession session;
        BoardSnapshot snapshot;
        BoardTracker tracker;
        BoardImageExtractor extractor;
        Font font;
        GUIStyle captionStyle, headlineStyle, detailStyle, buttonStyle;
        GUIStyle cardNameStyle;
        GameObject[] roster;
        Texture2D[] portraits;
        Action<int, int> beginMatch;
        Action endMatch;
        int circleChoice, crossChoice = 2;
        bool choosing = true, pickingCircle = true;

        public void Initialize(GameSession game, GameObject[] characters, Texture2D[] characterPortraits,
            Action<int, int> onBegin, Action onEnd)
        {
            session = game;
            snapshot = session.GetSnapshot();
            roster = characters;
            portraits = characterPortraits;
            beginMatch = onBegin;
            endMatch = onEnd;
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
            if (session == null) return;
            if (captionStyle == null) InitializeStyles();
            if (choosing)
            {
                DrawCharacterSelect();
                return;
            }
            if (extractor != null && extractor.DiagnosticsVisible) return;

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
                {
                    choosing = pickingCircle = true;
                    endMatch();
                }
            }
        }

        void DrawCharacterSelect()
        {
            const float width = 660f, height = 390f;
            Rect safe = Screen.safeArea;
            float scale = Mathf.Min(1.25f, Mathf.Min((safe.width - 12f) / width, (safe.height - 12f) / height));
            if (scale <= 0f) return;

            var previousMatrix = GUI.matrix;
            int previousDepth = GUI.depth;
            GUI.depth = -10;
            Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0.02f, 0.04f, 0.09f, 0.9f));
            var origin = new Vector3(safe.center.x - width * scale / 2f,
                Screen.height - safe.center.y - height * scale / 2f, 0f);
            GUI.matrix = previousMatrix * Matrix4x4.TRS(origin, Quaternion.identity, Vector3.one * scale);
            Fill(new Rect(0, 0, width, height), Panel);
            Fill(new Rect(0, 0, width / 2f, 4), CircleAccent);
            Fill(new Rect(width / 2f, 0, width / 2f, 4), CrossAccent);

            headlineStyle.fontSize = 26;
            GUI.Label(new Rect(24, 14, 612, 36), Text("キャラクターセレクト", "CHARACTER SELECT"), headlineStyle);
            GUI.Label(new Rect(24, 53, 612, 24),
                Text("○か×の枠を選び、相手と違うキャラを選んでください。", "Choose an O or X slot, then pick a different fighter."), detailStyle);
            string circleSymbol = Text("○", "O");
            string crossSymbol = Text("×", "X");
            if (DrawPlayerSlot(new Rect(24, 83, 300, 46), $"{circleSymbol}  {CharacterName(circleChoice)}", CircleAccent, pickingCircle))
                pickingCircle = true;
            if (DrawPlayerSlot(new Rect(336, 83, 300, 46), $"{crossSymbol}  {CharacterName(crossChoice)}", CrossAccent, !pickingCircle))
                pickingCircle = false;

            int opponentChoice = pickingCircle ? crossChoice : circleChoice;
            for (int index = 0; index < roster.Length; index++)
            {
                float x = 24 + 208 * index;
                var card = new Rect(x, 145, 196, 170);
                bool available = roster[index] != null && index != opponentChoice;
                Color cardColor = card.Contains(Event.current.mousePosition) && available
                    ? new Color32(51, 65, 85, 255) : new Color32(30, 41, 59, 255);
                Fill(card, cardColor);
                if (portraits != null && index < portraits.Length && portraits[index] != null)
                    GUI.DrawTexture(new Rect(x + 4, 149, 188, 128), portraits[index], ScaleMode.ScaleToFit);
                GUI.Label(new Rect(x + 4, 280, 188, 28), CharacterName(index), cardNameStyle);
                if (!available) Fill(card, new Color(0.02f, 0.04f, 0.09f, 0.62f));
                if (circleChoice == index) DrawBadge(x + 8, circleSymbol, CircleAccent);
                if (crossChoice == index) DrawBadge(x + 158, crossSymbol, CrossAccent);
                bool cardsEnabled = GUI.enabled;
                GUI.enabled = cardsEnabled && available;
                bool picked = GUI.Button(card, GUIContent.none, GUIStyle.none);
                GUI.enabled = cardsEnabled;
                if (picked)
                {
                    if (pickingCircle) circleChoice = index;
                    else crossChoice = index;
                }
            }

            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && circleChoice != crossChoice &&
                roster[circleChoice] != null && roster[crossChoice] != null;
            bool confirmed = GUI.Button(new Rect(24, 329, 612, 43),
                Text("このキャラで対戦を始める", "READY TO FIGHT"), buttonStyle);
            GUI.enabled = previousEnabled;
            GUI.matrix = previousMatrix;
            GUI.depth = previousDepth;
            if (confirmed)
            {
                choosing = false;
                beginMatch(circleChoice, crossChoice);
            }
        }

        bool DrawPlayerSlot(Rect rect, string label, Color accent, bool active)
        {
            Fill(rect, new Color32(30, 41, 59, 255));
            Fill(new Rect(rect.x, rect.y, active ? 6 : 2, rect.height), accent);
            GUI.Label(rect, label, cardNameStyle);
            return GUI.Button(rect, GUIContent.none, GUIStyle.none);
        }

        void DrawBadge(float x, string symbol, Color accent)
        {
            var rect = new Rect(x, 151, 30, 28);
            Fill(rect, Panel);
            Fill(new Rect(x, 151, 30, 2), accent);
            GUI.Label(rect, symbol, cardNameStyle);
        }

        string CharacterName(int index) => Text(JapaneseNames[index], EnglishNames[index]);

        (string headline, string detail, Color accent) Message()
        {
            switch (snapshot.Outcome)
            {
                case OutcomeType.CircleWon:
                    return (Text($"○ {CharacterName(circleChoice)}の勝ち", $"O  {CharacterName(circleChoice)} wins"), NextMatchHint(), CircleAccent);
                case OutcomeType.CrossWon:
                    return (Text($"× {CharacterName(crossChoice)}の勝ち", $"X  {CharacterName(crossChoice)} wins"), NextMatchHint(), CrossAccent);
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
            string name = CharacterName(circleTurn ? circleChoice : crossChoice);
            string title = circleTurn ? Text($"○ {name}の番", $"O  {name}'s turn") :
                Text($"× {name}の番", $"X  {name}'s turn");
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
            cardNameStyle = LabelStyle(18, FontStyle.Bold, PrimaryText);
            cardNameStyle.alignment = TextAnchor.MiddleCenter;
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
