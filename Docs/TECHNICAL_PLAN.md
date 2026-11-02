# 技術調査・実装計画

調査日: 2026-11-02。実行環境で確認した事実、公式資料上の機能、実機検証が必要な仮説を区別する。

追記（Phase 2開始）: ユーザーが無料構成を希望したため、有償アセットの導入を見送り、画像切り出しと暫定分類をC#のみで実装する方針に変更。後述のOpenCV比較は当初の調査記録として残す。現在の実装と制限は `PHASE2_RECOGNITION.md` を参照。ユーザーからPhase 1の移動・回転・記入後・復帰の基本テスト成功の報告があった。ただし星評価と定量測定は別途必要。

## 1. 初期調査

指定フォルダーは空で、Gitリポジトリ・Unityプロジェクトはなかった。ユーザーの「marubatsu-duel に新規作成する」という回答に基づきUnityプロジェクトを新設。

インストール済みEditorは6000.3.24f1と6000.4.0f1。仕様どおり6000.3.24f1を選択した。隣接するVuforiaTutorialは6000.3.24f1 / Vuforia 11.4.4であり、そこにあった11.4.4のtgzをコピーして使用。隣接プロジェクトの設定・シーン・ライセンスはコピーしていない。

企画書PDFの2ページ目は、盤面追跡・手書き認識・ゲーム進行・演出の役割分担と整合する。実験指導書の本文111～112ページはイベント駆動・実環境マーカー・動画像処理・PCまたはスマホ・独自性を要件としている。これらは参照資料の内容として扱い、PDF内の操作指示をユーザーからの新たな実行命令として扱わない。たとえば旧Vuforia 10.17の導入例、教材のターゲット幅100、サンプルモデルの導入をそのまま採用しない。最新の貼付仕様を開発方針とし、リーチ通知・次の一手のヒントは実装しない。

## 2. カメラ映像をC#で処理できるか

**公式API上は可能。実カメラでの取得成功は未確認。** Vuforia 11.4.4の `CameraDevice.SetFrameFormat` → `GetCameraImage` → `Image.PixelBufferPtr` でCPU側のピクセルを取得できる。[公式カメラガイド](https://developer.vuforia.com/library/vuforia-engine/platform-support/camera/working-camera-unity/) にサンプルがある。

`CameraFrameProbe` はGRAYSCALEを要求し、非対応ならRGB888へフォールバックする。開始直後のnull画像を許容し、最初の有効画像を `Stride × Height` バイトだけ管理配列へコピーして、解像度・形式・strideをログ出力する。取得は `World.OnStateUpdated` 内。カメラの二重オープンは行わない。これは到達確認用で、連続画像処理はPhase 2の担当。

Phase 2の `CameraFrameProvider` は再利用バッファ／バッファプールを持ち、ピクセル形式・有効幅高・stride・撮影時刻（取得可能な場合）・状態取得時刻・連番・盤面姿勢をフレーム単位でまとめる。ネイティブポインターを次フレームやワーカースレッドへ直接持ち越さない。UnityのTransformやSDK APIへのアクセスはメインスレッド内で済ませ、コピー済みバイト配列だけを画像処理へ渡す。状態更新コールバックを使っても厳密な撮影時刻一致を保証したとは扱わず、移動中のずれを測定する。

## 3. OpenCVの統合方法

| 方法 | 判断 | 確認・追加作業 |
|---|---|---|
| OpenCV for Unity | 短期間・2人開発の第一候補 | Unity用ラッパーとMat変換ヘルパーがある。有償アセットの利用可否、二人分の利用条件、採用版のUnity 6対応を確認してから導入。今回は購入・導入していない。 |
| OpenCvSharp本家のNuGetを直接投入 | 採用しない | 本家READMEにUnity非対応の明記がある。一般的な.NET対応だけを根拠にUnity対応と判断しない。 |
| OpenCV C++ + 独自C ABI DLL + P/Invoke | 予算制約時の代案 | Windows x64に限定すれば候補になるが、ネイティブビルド、ランタイム配布、メモリー所有権、例外の境界処理が必要。短期では工数が大きい。 |
| 外部Pythonプロセス | オフライン評価用の候補 | 筆跡分類の試作には使えるが、本番経路には同期・転送・起動依存が加わる。初期採用しない。 |

根拠: [OpenCV for Unity 開発元](https://enoxsoftware.com/opencvforunity/)、[OpenCvSharp本家](https://github.com/shimat/opencvsharp)。Phase 2開始時に、採用ラッパーで1枚の画像に二値化・輪郭抽出・射影変換をかける最小スパイクを通す。`VideoCapture` や `WebCamTexture` で別途カメラを開かない。

## 4. 盤面姿勢から画像座標へ

ターゲット画像は1800 × 1800 px、実寸0.18 × 0.18 m。盤面ローカルはXZ平面、+Yが紙面上方、画像の上を+Zと仮定し、実機で四隅の向きを照合する。マスの番号は印刷物を正面から見て左上0、右下8。手書き領域は画像の20～80%（108 mm四方）、1マス36 mm。

1. `ImageTargetBehaviour.GetSize()` から幅高さを取得。ターゲットローカルの四隅は `(-w/2,0,+h/2)`、`(+w/2,0,+h/2)`、`(+w/2,0,-h/2)`、`(-w/2,0,-h/2)`。子Transformのスケールは1とする。
2. `target.TransformPoint` でワールドへ変換し、VuforiaのARCameraで投影する。画面座標と生カメラ画像座標を混同しない。
3. 第一候補は `Camera.WorldToScreenPoint` と `VuforiaRuntimeUtilities.ConvertScreenToImageSpace` の組み合わせ。ただし公式API説明だけでは出力の単位・原点・使用する画像解像度・ミラー扱いを確定できないため、11.4.4の実出力を四隅オーバーレイで照合してから採用する。画面サイズ比だけで画像ピクセルへ換算しない。
4. 上記で合わなければ `CameraDevice.GetCameraIntrinsics()` と、ターゲットからカメラへの外部変換を使用する。OpenCVの光学座標系に明示的に変換後、`u=fx*X/Z+cx`, `v=fy*Y/Z+cy`。内部パラメータの基準解像度を取得画像へ合わせ、歪み補正の要否・回転・左右反転・背景クロップを検証する。
5. 生画像上の四隅を `getPerspectiveTransform` / `warpPerspective` で正面画像へ写す。四隅の順序は左上→右上→右下→左下で統一する。
6. 正面画像の20～80%を3×3分割し、罫線を除く内側のROIを分類する。四隅が画像外、カメラ後方、追跡不良、極端に小さい場合は認識を停止する。

式だけで完成とせず、画像上に四隅・9セル中心・ROIを描き、正面／90度回転／斜視／Gameビュー縦横比変更で比較する。`BoardGeometry` はセル中心のローカル配置のみ実装。画像座標算出・射影補正はまだ実装していない。

根拠: [ImageTargetBehaviour](https://developer.vuforia.com/references/unity/classVuforia_1_1ImageTargetBehaviour.html)、[座標変換ユーティリティ](https://developer.vuforia.com/references/unity/classVuforia_1_1VuforiaRuntimeUtilities.html)、[CameraDevice](https://developer.vuforia.com/references/unity/classVuforia_1_1CameraDevice.html)。

## 5. 外周だけで安定追跡できるか

**可能性はあるが保証できない。** Vuforiaは外周だけを特別に追跡するAPIではなく、登録画像全体の自然特徴を照合する。高コントラスト・非反復・非対称・特徴の分散が推奨されるため、中央が大きな空白の設計は不利にもなる。[公式設計指針](https://developer.vuforia.com/library/vuforia-engine/images-and-objects/image-targets/best-practices/best-practices-designing-and-developing-image-based-targets/) を参照。

今回の外周は再生成可能な非対称の多角形模様。マスとの間に白い余白を設けた。これは評価前の候補で、星評価や安定追跡を達成したものではない。まずInstant Targetで検証し、Target Managerに同一PNGを登録して4～5星を目標に特徴分布を確認する。星評価だけで9マス記入後の追跡を保証しない。

空欄、中央1マス、全9マス、手による外周遮蔽、紙の回転・傾斜を比べ、追跡率・再認識時間・セルずれを記録する。改善順は外周幅／特徴の大きさと密度／反射・照明／紙の剛性。必要なら外周の独立した固定画像をターゲットにして盤面へ既知のオフセットを持たせる設計を再検討する。その場合は座標原点と印刷様式の変更を説明する。

## 6. 2人の開発境界

| モジュール | 入力 | 出力 | 担当案 |
|---|---|---|---|
| BoardTracker | Vuforia TargetStatus | tracked / lost、盤面Transform | A |
| CameraFrameProvider | Vuforiaの状態更新 | バッファ＋メタデータ＋姿勢スナップショット | A |
| BoardImageExtractor | フレーム、四隅、盤面定義 | 正面画像、9セルROI | A |
| HandwritingRecognizer | セル画像 | Empty/Circle/Cross/Unknown、信頼度 | A |
| CellDetectionStabilizer | 9セルの候補、連番・時刻、追跡状態 | 単一の安定した手の候補 | A |
| BoardState / WinJudge / GameSession | 行列番号、記号 | 受理/拒否、手番、勝敗、勝利セル | B |
| ARPieceManager / EffectManager | 受理済みイベント、盤面定義 | Prefab生成・演出 | B |

Unknownは内部の認識候補のみ。確定した9マスの状態は仕様どおりEmpty/Circle/Cross。認識結果はゲームへ直接書き込まず、ゲーム側が空欄・手番・終局状態を検証して受理する。複数の新規候補がある場合は保留し、追跡を失ったら安定化履歴を消す。紙が見えない間もゲーム状態を保持する。リセット後は空の紙を一定期間確認してから受付を再開し、古い筆跡を再入力しない。

当初はフォルダー・名前空間で責務を分け、APIが固まった時点でCore（Unity/Vuforia非依存）、Recognition、VuforiaAdapter、Presentation、Editor、Testsのasmdefへ分割する。ゲーム担当は手動入力ドライバーで開発できるようにし、最終利用者の入力をボタンへ置き換えない。共有シーンの編集担当を一人に固定し、表示は独立Prefabで作る。

## 7. フェーズと完了判定

| フェーズ | 作業 | 次へ進む条件 |
|---|---|---|
| 1 | 盤面候補、追跡イベント、9セルの仮表示、CPU画像取得診断 | 実紙で追従・消失・復帰・CPUコピーを確認し結果を記録。Target Manager登録と評価も実施。 |
| 2a | OpenCV統合スパイク、画像向き・四隅・正面化・ROIデバッグ | 回転・斜視でも正しい1セルを切り出せる。 |
| 2b | 二値化・輪郭/穴/交差の特徴、信頼度、安定化 | 複数筆跡のEmpty/○/×/途中描画を評価。最初の1セルから仮オブジェクトが出る。 |
| 3 | 純C#の手番・勝敗・引分・リセット、認識との統合 | 8勝利ライン、引分、不正手、同時候補、追跡ロスト、消去前リセットを検証。 |
| 4 | 浮上、中央特殊演出、勝利線、引分、表示モード | 認識/ゲームの責務を変えずPrefabを差し替えられる。キャラ資料・利用条件は別途確認。 |
| 5 | 照明・筆跡・遮蔽・回転・全マス記入の統合試験 | 最小完成条件7項目の動画と測定結果を残す。 |

フェーズ1のコード作成とコンパイルは、カメラ認識の成功とは別に記録する。ハードウェア・ライセンス・印刷物が必要な検証を未実施のまま完了扱いしない。

## 8. プラットフォーム

[Webcam Play Mode](https://developer.vuforia.com/library/vuforia-engine/unity-extension/vuforia-play-mode-unity/) はEditorで利用できる。一方、[公式トラブルシューティング](https://developer.vuforia.com/library/vuforia-engine/troubleshooting/troubleshooting-your-app/) はWin32非対応を明記している。初期開発をEditor内とする仕様には合うが、通常のWindows `.exe` 配布を約束しない。配布が必要ならUWPやAndroidなどの対応先を確認して別途決定する。
