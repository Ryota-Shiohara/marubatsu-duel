using System;
using UnityEngine;
using static Marubatsu.Presentation.ArcadeHudArt;
using Trim = Marubatsu.Presentation.ArcadeHudArt.Trim;

namespace Marubatsu.Presentation
{
    public sealed class MatchHud : MonoBehaviour
    {
        enum ScreenState { Title, Fighters, Scan, Versus, Battle }

        const float IntroDuration = 1.65f;
        readonly Vector2[] boardOutline = new Vector2[4];

        GameSession session;
        BoardSnapshot snapshot;
        BoardTracker tracker;
        BoardImageExtractor extractor;
        Camera arCamera;
        Vector2 boardSize;
        ArcadeHudArt art;
        Texture2D[] portraits;
        Action<int, int> beginMatch;
        Action endMatch;
        Func<bool> canReplay;
        ScreenState screen;
        int circleChoice, crossChoice = 2;
        int round = 1, circleMarks, crossMarks;
        bool pickingCircle = true;
        float width, height, scale, introStart;
        Vector2 origin;

        bool Compact => width < 1200;
        bool BoardTracked => tracker != null && tracker.IsTracked;
        bool BoardReady => BoardTracked && extractor.HasFreshFrame && extractor.IsEmptyBoardStable;
        float IntroAge => Time.unscaledTime - introStart;

        public void Initialize(GameSession game, Texture2D[] characterPortraits,
            Action<int, int> onBegin, Action onEnd, Func<bool> replayAvailable)
        {
            session = game;
            portraits = characterPortraits;
            beginMatch = onBegin;
            endMatch = onEnd;
            canReplay = replayAvailable;
            OnSnapshotChanged(session.GetSnapshot());
            session.OnSnapshotChanged += OnSnapshotChanged;
        }

        public void AttachBoard(BoardTracker boardTracker, BoardImageExtractor boardExtractor,
            Camera camera, Vector2 size)
        {
            tracker = boardTracker;
            extractor = boardExtractor;
            arCamera = camera;
            boardSize = size;
        }

        void OnSnapshotChanged(BoardSnapshot current)
        {
            snapshot = current;
            circleMarks = crossMarks = 0;
            foreach (MarkType mark in current.Cells)
            {
                if (mark == MarkType.Circle) circleMarks++;
                if (mark == MarkType.Cross) crossMarks++;
            }
        }

        void Update()
        {
            if (screen != ScreenState.Versus || IntroAge < IntroDuration) return;
            if (BoardReady)
            {
                screen = ScreenState.Battle;
                beginMatch(circleChoice, crossChoice);
            }
            else screen = ScreenState.Scan;
        }

        void OnGUI()
        {
            if (session == null) return;
            bool cameraScreen = screen == ScreenState.Scan || screen == ScreenState.Battle;
            if (cameraScreen && extractor != null && extractor.DiagnosticsVisible) return;

            Rect safe = UnityEngine.Screen.safeArea;
            if (safe.width < 1 || safe.height < 1) return;
            art ??= new ArcadeHudArt(portraits);
            width = Mathf.Clamp(safe.width / safe.height * 900, 900, 1600);
            scale = Mathf.Min(safe.width / width, safe.height / 900);
            height = safe.height / scale;
            origin = new Vector2(safe.center.x - width * scale / 2, UnityEngine.Screen.height - safe.yMax);

            Matrix4x4 previous = GUI.matrix;
            int depth = GUI.depth;
            GUI.depth = -10;
            if (!cameraScreen)
                art.Background(new Rect(0, 0, UnityEngine.Screen.width, UnityEngine.Screen.height));
            GUI.matrix = previous * Matrix4x4.TRS(origin, Quaternion.identity, new Vector3(scale, scale, 1));

            switch (screen)
            {
                case ScreenState.Title: DrawTitle(); break;
                case ScreenState.Fighters: DrawFighters(); break;
                case ScreenState.Scan: DrawScan(); break;
                case ScreenState.Versus: DrawVersus(); break;
                case ScreenState.Battle: DrawBattle(); break;
            }

            GUI.matrix = previous;
            GUI.depth = depth;
        }

        Rect Center(float y, float w, float h) => new Rect((width - w) / 2, y, w, h);

        void DrawTitle()
        {
            float title = height * .055f;
            art.Label(Center(title, width, 62), T("飛び出せ！", "JUMP INTO THE ARENA!"),
                45, Color.white, true);
            art.Label(Center(title + 56, width, 117),
                T("<color=#FFCE39>DX</color><color=#6BE8FF>まる</color><color=#FF8434>ばつ</color>げーむ！",
                    "<color=#FFCE39>DX</color> MARUBATSU DUEL"), Compact ? 69 : 104, Color.white, true,
                shadow: T("DXまるばつげーむ！", "DX MARUBATSU DUEL"));
            art.Label(Center(title + 172, width, 42), "M A R U B A T S U   D U E L", 27, Mist, true);
            Banner(Center(title + 220, Mathf.Min(width - 64, 760), 66),
                T("ARで飛び出す！ まるばつバトル！", "PAPER MARKS. AR FIGHTERS."), 31);

            float startY = height * .81f;
            float stage = startY - title - 275;
            float center = width / 2;
            art.Glow(new Rect(center - width * .42f, startY - stage * .91f, width * .62f, stage * 1.18f),
                new Color(0, .58f, 1f, .82f));
            art.Glow(new Rect(center - width * .16f, startY - stage * .91f, width * .56f, stage * 1.18f),
                new Color(1f, .40f, .03f, .74f));
            for (int index = 0; index < 3; index++)
            {
                float lane = width * .25f;
                float x = center + (index - 1) * lane;
                float bob = Mathf.Sin(Time.unscaledTime * 2.8f + index * 1.2f) * 8;
                art.Portrait(new Rect(x - lane * .65f, startY - stage + bob - 9,
                    lane * 1.3f, stage + 10), index, portraits);
            }

            if (art.Button(Center(startY, Mathf.Min(width - 66, 980), 108),
                T("ゲームスタート", "GAME START"), 60, Trim.Gold))
                screen = ScreenState.Fighters;
        }

        void DrawFighters()
        {
            float header = height * .045f;
            Banner(Center(header, width - (Compact ? 56 : 220), 108),
                T("キャラクターセレクト", "CHARACTER SELECT"), Compact ? 52 : 66);
            art.Label(Center(header + 110, width - 50, 56),
                T("○か×の枠を押して、それぞれ違うキャラをえらぼう。",
                  "Choose the O or X slot, then pick a different fighter."), Compact ? 29 : 34, Mist);
            float slotY = height * .23f;
            float slotW = (width - 112) / 2;
            var circleSlot = new Rect(46, slotY, slotW, 88);
            var crossSlot = new Rect(width / 2 + 10, slotY, slotW, 88);
            if (art.Button(circleSlot, PlayerName(true),
                Compact ? 34 : 43, Trim.Blue)) pickingCircle = true;
            if (art.Button(crossSlot, PlayerName(false),
                Compact ? 34 : 43, Trim.Orange)) pickingCircle = false;
            Rect activeSlot = pickingCircle ? circleSlot : crossSlot;
            art.Label(new Rect(activeSlot.xMax - 66, slotY, 47, 88), "‹", 34, Color.white, true);

            float cardW = (width - 128) / 3;
            float cardY = slotY + 110;
            float cardH = Mathf.Min(490, height * .78f - cardY - 18);
            for (int index = 0; index < portraits.Length; index++)
            {
                var card = new Rect(46 + index * (cardW + 18), cardY, cardW, cardH);
                bool occupied = index == (pickingCircle ? crossChoice : circleChoice);
                Trim trim = index == circleChoice ? Trim.Blue : index == crossChoice ? Trim.Orange : Trim.Steel;
                art.Frame(card, trim);
                Color tint = trim == Trim.Blue ? Ice : trim == Trim.Orange ? Flame : Steel;
                tint.a = !occupied && card.Contains(Event.current.mousePosition) ? .8f : .42f;
                art.Glow(new Rect(card.x - 20, card.y + 42, card.width + 40, card.height - 70), tint);
                art.Portrait(new Rect(card.x + 15, card.y + 56, card.width - 30, card.height - 136), index, portraits);
                Banner(new Rect(card.x + 6, card.yMax - 85, card.width - 12, 76), CharacterName(index),
                    Compact ? 32 : 44, trim);
                if (trim != Trim.Steel)
                    art.Label(new Rect(card.x + 15, card.y + 13, 86, 61), Symbol(trim == Trim.Blue), 54, Color.white, true);
                bool previous = GUI.enabled;
                GUI.enabled = previous && !occupied;
                bool chosen = GUI.Button(card, GUIContent.none, GUIStyle.none);
                GUI.enabled = previous;
                if (!chosen) continue;
                if (pickingCircle) circleChoice = index;
                else crossChoice = index;
            }

            float confirmY = Mathf.Max(height * .78f, cardY + cardH + 12);
            if (art.Button(Center(confirmY, Mathf.Min(width - 58, 1050), 105),
                T("このキャラで対戦を始める", "CONFIRM FIGHTERS"), Compact ? 44 : 58, Trim.Gold))
            {
                round = 1;
                screen = ScreenState.Scan;
            }
            Back(Center(Mathf.Min(height - 61, confirmY + 109), 265, 61), ScreenState.Title);
        }

        void DrawScan()
        {
            bool tracked = BoardTracked;
            bool ready = BoardReady;
            var viewport = new Rect(25, Compact ? 263 : 169, width - 50, height - (Compact ? 445 : 348));
            if (tracked) DrawTrackedOutline(viewport, ready);
            DrawCorners(viewport, 64, 6, tracked ? Ice : Mist);
            if (!tracked)
                DrawCorners(Center(viewport.center.y - 61, 198, 122), 32, 4, Ice);

            Banner(Center(15, Mathf.Min(width - 40, 950), 94),
                T("盤面を映してください", "SHOW THE BOARD"), Compact ? 47 : 57);
            art.Label(Center(110, width - 40, 42),
                T("外周の模様までカメラに入れてください。", "Keep the entire border in view."), 30, Color.white);
            DrawScanPlayer(true, 29, Compact ? 151 : 160);
            DrawScanPlayer(false, width - (Compact ? 270 : 332), Compact ? 151 : 160);
            string status = ready ? "BOARD DETECTED!" : tracked ? "READING BOARD..." : "SCANNING...";
            Banner(Center(Compact ? 263 : 165, Compact ? 442 : 520, 75), status, 38, ready ? Trim.Blue : Trim.Steel);
            string hint = ready ? T("空の盤面で対戦を始めます", "Blank board confirmed")
                : tracked ? T("何も書いていない盤面を映してください。", "Hold a blank board still.")
                : T("カメラに盤面を向けてください。", "Point the camera at the board.");
            Banner(Center(height - 233, Mathf.Min(width - 64, 820), 69), hint, Compact ? 27 : 33, display: false);
            if (art.Button(Center(height - 152, Mathf.Min(width - 65, 700), 107),
                "READY?", 66, Trim.Gold, ready))
            {
                introStart = Time.unscaledTime;
                screen = ScreenState.Versus;
            }
            Back(new Rect(23, height - 66, 244, 64), ScreenState.Fighters);
            if (!Compact)
                art.Label(new Rect(width - 370, height - 67, 346, 62), "01  SCAN   /   02  READY", 23, Mist, true);
        }

        void DrawScanPlayer(bool circle, float x, float y)
        {
            float w = Compact ? 240 : 303;
            float h = Compact ? 116 : 216;
            Trim trim = Side(circle);
            art.Frame(new Rect(x, y, w, h), trim);
            if (Compact)
            {
                art.Portrait(new Rect(x + 13, y + 13, 85, h - 26), Choice(circle), portraits);
                art.Label(new Rect(x + 91, y + 5, w - 104, h - 10), PlayerName(circle), 25, Color.white, true);
            }
            else
            {
                art.Portrait(new Rect(x + 20, y + 60, w - 40, h - 74), Choice(circle), portraits);
                Banner(new Rect(x + 2, y + 2, w - 4, 70), PlayerName(circle), 33, trim);
            }
        }

        void DrawTrackedOutline(Rect viewport, bool ready)
        {
            if (arCamera == null) return;
            for (int index = 0; index < 4; index++)
            {
                float x = index == 0 || index == 3 ? -.5f : .5f;
                float z = index < 2 ? .5f : -.5f;
                Vector3 world = tracker.transform.TransformPoint(x * boardSize.x, 0, z * boardSize.y);
                Vector3 point = arCamera.WorldToScreenPoint(world);
                if (point.z <= arCamera.nearClipPlane) return;
                var screenPoint = new Vector2(point.x, UnityEngine.Screen.height - point.y);
                boardOutline[index] = (screenPoint - origin) / scale;
                if (!viewport.Contains(boardOutline[index])) return;
            }
            Color edge = ready ? Ice : Mist;
            for (int index = 0; index < 4; index++)
            {
                Vector2 a = boardOutline[index], b = boardOutline[(index + 1) % 4];
                Line(a, b, 16, new Color(edge.r, edge.g, edge.b, .25f));
                Line(a, b, 7, edge);
                Line(a, b, 2, Color.white);
            }
        }

        static void DrawCorners(Rect rect, float length, float thickness, Color color)
        {
            for (int index = 0; index < 4; index++)
            {
                bool left = index % 2 == 0, top = index < 2;
                float x = left ? rect.x : rect.xMax - length;
                float y = top ? rect.y : rect.yMax - thickness;
                Fill(new Rect(x, y, length, thickness), color);
                x = left ? rect.x : rect.xMax - thickness;
                y = top ? rect.y : rect.yMax - length;
                Fill(new Rect(x, y, thickness, length), color);
            }
        }

        void DrawVersus()
        {
            float progress = Mathf.SmoothStep(0, 1, Mathf.Clamp01(IntroAge / .40f));
            float y = height * .29f;
            float body = height * .49f;
            float shift = (1 - progress) * width * .28f;
            art.Glow(new Rect(-width * .18f, y - 50, width * .77f, body + 150), new Color(0, .65f, 1f, .82f));
            art.Glow(new Rect(width * .41f, y - 50, width * .77f, body + 150), new Color(1f, .37f, 0f, .82f));
            DrawClash(width / 2, y + body * .54f, progress);
            art.Portrait(new Rect(20 - shift, y, width * .48f, body), circleChoice, portraits, mirror: circleChoice == 0);
            art.Portrait(new Rect(width * .52f + shift - 20, y, width * .48f, body), crossChoice, portraits);
            Rect clash = Center(y + body * .24f, width * .35f, body * .60f);
            int size = Compact ? 156 : 236;
            art.Label(new Rect(clash.x + 6, clash.y + 9, clash.width, clash.height),
                "VS", size, new Color32(180, 68, 2, 255), true);
            art.Label(new Rect(clash.x - 3, clash.y - 4, clash.width, clash.height),
                "VS", size, new Color32(255, 249, 191, 255), true);
            art.Label(clash, "VS", size, new Color32(255, 201, 45, 255), true);
            Banner(Center(height * .06f, Mathf.Min(width - 60, 1050), 132),
                T("対戦開始！", "LET'S BATTLE!"), Compact ? 78 : 101);
            art.Label(Center(height * .06f + 136, width - 30, 60), "R E A D Y   T O   D U E L !", 32, Color.white, true);
            float tagW = (width - 84) / 2;
            Banner(new Rect(24, height * .81f, tagW, 108), PlayerName(true), Compact ? 43 : 60, Trim.Blue);
            Banner(new Rect(width - tagW - 24, height * .81f, tagW, 108), PlayerName(false),
                Compact ? 43 : 60, Trim.Orange);
        }

        void DrawClash(float x, float y, float progress)
        {
            var center = new Vector2(x, y);
            float radius = Mathf.Min(width * .36f, 520);
            for (int index = 0; index < 44; index++)
            {
                float angle = index * 2.39996f;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float inner = (index % 5) * 13 + 35;
                float outer = radius * (.5f + index % 7 * .071f) * Mathf.Lerp(.35f, 1, progress);
                Color tint = direction.x < 0 ? Ice : Flame;
                tint.a = .48f;
                Line(center + direction * inner, center + direction * outer, 5, tint);
                Line(center + direction * inner, center + direction * (outer * .78f), 2, Color.white);
            }
            art.Glow(new Rect(x - radius, y - radius, radius * 2, radius * 2), new Color(1f, .76f, .37f, .72f));
        }

        void DrawBattle()
        {
            bool playing = snapshot.Outcome == OutcomeType.Playing;
            bool circle = snapshot.NextTurn == MarkType.Circle;
            bool usable = BoardTracked && extractor.HasFreshFrame;
            bool narrow = Compact;
            Banner(Center(8, narrow ? 530 : 460, 80), T("ARバトル", "AR BATTLE"), 51);
            Banner(Center(85, narrow ? 294 : 224, 63), $"ROUND {round}", 32);
            float playerW = narrow ? (width - 69) / 2 : Mathf.Min(515, (width - 494) / 2);
            float playerY = narrow ? 148 : 24;
            DrawBattlePlayer(new Rect(16, playerY, playerW, 115), true, playing && circle);
            DrawBattlePlayer(new Rect(width - playerW - 16, playerY, playerW, 115), false, playing && !circle);

            if (!playing)
            {
                DrawResult();
                return;
            }

            string title = usable ? T($"{PlayerName(circle)}の番", $"{PlayerName(circle)}'s turn")
                : T("盤面を映してください", "SHOW THE BOARD");
            string detail = !BoardTracked
                ? T("外周の模様までカメラに入れてください。", "Keep the whole border in the camera view.")
                : !usable
                    ? T("盤面を動かさず、読み取りを待ってください。", "Hold the board still while it is read.")
                    : MoveHint(extractor.PaperStatus, circle);
            Banner(Center(height - 113, width - 44, 103), detail, narrow ? 29 : 37, display: false);
            Banner(Center(height - 178, Mathf.Min(width - 70, 720), 79), title, narrow ? 35 : 43,
                usable ? Side(circle) : Trim.Steel);
        }

        void DrawBattlePlayer(Rect rect, bool circle, bool active)
        {
            Trim trim = Side(circle);
            art.Frame(rect, trim);
            float portraitX = circle ? rect.x + 13 : rect.xMax - 105;
            var portraitRect = new Rect(portraitX, rect.y + 13, 92, rect.height - 26);
            if (active)
            {
                Color tint = circle ? Ice : Flame;
                tint.a = .62f;
                art.Glow(new Rect(portraitX - 22, rect.y - 10, 136, rect.height + 20), tint);
            }
            art.Portrait(portraitRect, Choice(circle), portraits);
            float labelX = circle ? rect.x + 110 : rect.x + 12;
            float labelW = rect.width - 124;
            art.Label(new Rect(labelX, rect.y + 12, labelW, 49), PlayerName(circle), Compact ? 28 : 33,
                Color.white, true);
            float pipW = Mathf.Min(45, (labelW - 27) / 5);
            float pipX = labelX + (labelW - 5 * pipW) / 2;
            int marks = circle ? circleMarks : crossMarks;
            for (int index = 0; index < 5; index++)
            {
                Color tint = index < marks ? (circle ? Ice : Gold) : new Color(.26f, .36f, .48f, .8f);
                Vector2 start = new Vector2(pipX + index * pipW + 5, rect.y + 86);
                Line(start, start + new Vector2(pipW - 9, -12), 8, tint);
            }
        }

        void DrawResult()
        {
            bool draw = snapshot.Outcome == OutcomeType.Draw;
            bool circle = snapshot.Outcome == OutcomeType.CircleWon;
            string result = draw ? T("ひきわけ！", "DRAW!")
                : T($"{PlayerName(circle)}の勝利！", $"{PlayerName(circle)} WINS!");
            Banner(Center(height - 180, Mathf.Min(width - 50, 790), 90), result, 47, draw ? Trim.Steel : Side(circle));

            if (!canReplay())
            {
                if (!BoardTracked)
                    Banner(Center(height - 92, Mathf.Min(width - 50, 700), 76),
                        T("盤面を映してください", "SHOW THE BOARD"), 32, Trim.Steel);
                return;
            }

            float w = Mathf.Min(500, (width - 66) / 2);
            if (art.Button(new Rect(width / 2 - w - 5, height - 92, w, 81),
                T("同じキャラで再戦", "REMATCH"), 34, Trim.Gold))
            {
                endMatch();
                round++;
                screen = ScreenState.Scan;
            }
            if (art.Button(new Rect(width / 2 + 5, height - 92, w, 81),
                T("キャラ選択", "CHARACTERS"), 33, Trim.Blue))
            {
                endMatch();
                screen = ScreenState.Fighters;
                pickingCircle = true;
            }
        }

        void Banner(Rect rect, string label, int size, Trim trim = Trim.Blue, bool display = true)
        {
            art.Frame(rect, trim);
            art.Label(new Rect(rect.x + 12, rect.y + 1, rect.width - 24, rect.height - 2),
                label, size, Color.white, display);
        }

        void Back(Rect rect, ScreenState destination)
        {
            if (art.Button(rect, T("‹  もどる", "‹  BACK"), 28, Trim.Steel))
                screen = destination;
        }

        static Trim Side(bool circle) => circle ? Trim.Blue : Trim.Orange;
        int Choice(bool circle) => circle ? circleChoice : crossChoice;
        string Symbol(bool circle) => circle ? T("○", "O") : T("×", "X");
        string PlayerName(bool circle) => $"{Symbol(circle)}  {CharacterName(Choice(circle))}";
        string CharacterName(int index) => MatchCharacterRoster.CharacterName(index, art.SupportsJapanese);
        string T(string japanese, string english) => art.SupportsJapanese ? japanese : english;

        string MoveHint(PaperMoveStatus status, bool circleTurn)
        {
            switch (status)
            {
                case PaperMoveStatus.MultipleMarks:
                    return T("新しい記号は1マスだけにしてください。", "Leave only one new mark on the board.");
                case PaperMoveStatus.WrongTurn:
                    return circleTurn
                        ? T("新しく書いた×を消し、○を書いてください。", "Erase the new X and write O.")
                        : T("新しく書いた○を消し、×を書いてください。", "Erase the new O and write X.");
                case PaperMoveStatus.BoardMismatch:
                    return T("紙の記号を確認し、受理済みの配置に戻してください。", "Restore the marks that were already accepted.");
                case PaperMoveStatus.WaitingForEmptyPaper:
                    return T("何も書いていない盤面を映してください。", "Show a blank board to begin.");
                case PaperMoveStatus.WaitingForStableCells:
                case PaperMoveStatus.Rejected:
                    return T("手を離し、盤面を少し止めてください。", "Move your hand away and hold the board still.");
                default:
                    return circleTurn
                        ? T("空きマスに○を1つ書いてください。", "Write one O in an empty cell.")
                        : T("空きマスに×を1つ書いてください。", "Write one X in an empty cell.");
            }
        }


        void OnDestroy()
        {
            if (session != null) session.OnSnapshotChanged -= OnSnapshotChanged;
            art?.Dispose();
        }
    }
}
