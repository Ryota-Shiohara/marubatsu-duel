# Phase 1 検証手順・記録

## 自動確認済みの範囲（2026-11-02）

- Unity 6000.3.24f1 / Vuforia 11.4.4でC#コンパイルとシーン生成に成功。
- 最終の検証バッチは終了コード0、`[Phase1Validation] PASS`。
- 180 mmターゲットに対する左上・中央・右下セルの物理位置、不正セル番号の拒否、画像の1800 × 1800維持と読み取り可否、カメラ1台、Missing Scriptなし、シーンの画像参照を検証。
- 初回の検証で画像のNPOT自動リサイズを検出し、`TextureImporterNPOTScale.None` に修正して再検証した。
- 最終バッチでC#コンパイルのエラー・警告なし。
- Git初期化、Unity生成物のignore、tgzのGit LFS属性を確認。コミット・push・デプロイは未実施。
- 初回作成時点ではライセンスを設定してのVuforia初期化、実カメラからのCPUコピー、追跡・復帰、星評価は未検証だった。

### Editorでのカメラ表示確認（2026-11-02）

- ユーザーが設定したローカルライセンスとカメラで、Vuforia初期化と640 × 480 / GRAYSCALE / stride 640 / 307200バイトのCPUコピー成功ログを確認。
- ARCameraのClear FlagsがSkyboxのままだとGameビューに空が描かれる問題を確認。Solid Colorへ変更してシーンを保存し、再生後に実カメラ映像がGameビューへ表示されることを確認した。生成コードにも同じ設定を反映。
- 印刷物での盤面追跡・9セルへの配置・消失と復帰・Target Managerの星評価は引き続き未検証。

再確認はUnityメニュー `Marubatsu > Validate Phase 1 Assets`。画像設定を含めて再適用するバッチ用エントリーは `Marubatsu.Editor.PhaseOneSetup.ConfigureAndValidate`。

## 準備

- Unity 6000.3.24f1、Vuforia 11.4.4、Built-in。
- Vuforia Configurationに各自のライセンスを設定。既存プロジェクトからキーをコピーする処理は行っていない。
- Webcam Play Modeとカメラデバイスを選択。他アプリのカメラ利用を閉じる。
- A4 SVGを100%で印刷し、ターゲット180 mmと検尺100 mmを確認。光沢を避け、平らな台紙に固定する。
- 印刷物のTOP側とシーンのrow=0側が一致することを確認。

## 最初の実機確認

1. Phase1_BoardTrackingを再生する。
2. `[Phase1] Instant target ready`、`[CameraFrameProbe] CPU copy OK` が出ることを確認。出ない場合は初期化エラー・ライセンス・選択カメラ・形式登録結果を確認する。
3. 紙を映し、`[BoardTracker] TRACKED` と各セル中央の立方体9個を確認する。
4. 紙を動かして追従を見る。180度回転でもrow/columnの向きが紙について動くことを確認。
5. 紙を視界から外すと表示が消え、再認識で同じ配置に戻ることを確認。EXTENDED_TRACKEDとLIMITEDでは非表示にする。
6. 中央に○を記入し、次に全9マスを○×で埋める。これは追跡評価であり、まだ筆跡分類は行わない。

## Target Managerへの登録

開発者アカウントでDevice Databaseを作り、`Assets/Marubatsu/Art/BoardTarget.png` をImage Targetとして登録。実寸幅は **0.18 m** を基準とし、Unityで `GetSize()` が0.18 × 0.18になることを確認する。教材の例の100をそのまま入力しない。

星評価と黄色い特徴点の分布を確認し、可能なら4～5星を目標とする。データベースのUnity用パッケージをインポートし、VuforiaのImage TargetをFrom Databaseで作成、`BoardTracker` と `BoardPreview` を付ける。標準の `DefaultObserverEventHandler` がある場合は削除し、このハンドラーによる表示管理へ一本化する。

同時に `ARCamera` の `PhaseOneBootstrap` を無効にして、同一盤面をInstant TargetとDB Targetの二重で追跡しない。`CameraFrameProbe` は残す。登録はアカウント操作が必要であり、今回自動実施していない。

## 記録表

各条件を30秒、再認識は3回測る。数値は実測後に記入する。

| 条件 | TRACKED率 | 再認識時間 | セル中心のずれ | 結果 |
|---|---|---|---|---|
| 空欄・正面 | 未測定 | 未測定 | 未測定 | 未実施 |
| 中央1マス記入 | 未測定 | 未測定 | 未測定 | 未実施 |
| 全9マス記入 | 未測定 | 未測定 | 未測定 | 未実施 |
| 90/180度回転 | 未測定 | 未測定 | 未測定 | 未実施 |
| 30/45度の斜視 | 未測定 | 未測定 | 未測定 | 未実施 |
| 外周1辺を手で遮蔽 | 未測定 | 未測定 | 未測定 | 未実施 |
| 画面外へ移動後に復帰 | 未測定 | 未測定 | 未測定 | 未実施 |

初期目標案: 空欄・全9マスの静止時TRACKED率95%以上、再認識2秒以内、セル中心ずれがセル幅の10%以内。これは製品仕様として確定した性能保証ではなく、実験時の判定目安。距離・カメラ型番・映像解像度・照明・角度・ターゲット種別と星評価も記録する。

現状のログは状態遷移のみ。TRACKED率は録画からの時間集計、または後続の計測用スクリプトで算出する。スクリーンショットだけで安定性を評価しない。
