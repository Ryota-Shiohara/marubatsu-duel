# やがぽん・みたぬき・矢口くんのアニメーション

## Unityで使うファイル

- `Assets/Marubatsu/Art/Mitanuki/Mitanuki_Animated.prefab`
- `Assets/Marubatsu/Art/Yagapon/Yagapon_Animated.prefab`
- `Assets/Marubatsu/Art/Yaguchi/Yaguchi_Animated.prefab`

上記は `Marubatsu > Characters > Prepare Animation Examples` で生成する。指定Unity 6000.3.24f1で実行可能。新しいPrefab・AnimationClip・AnimatorControllerだけ生成し、既存のサンプル編集内容やゲームシーンは上書きしない。FBXはGeneric形式、BlendShape有効、ボーン階層を保持して取り込む。

Prefabをシーンに配置すると `Idle` が既定状態。AnimatorのParametersで `Wave` / `Victory` / `Blink` のTriggerを押すと手振り・勝利・まばたきのサンプルを再生し、終了後Idleへ戻る。スクリプトからも `animator.SetTrigger("Wave")` などで再生できる。

`Animations` フォルダー内の `.anim` はUnityで編集可能。Prefabインスタンスを選び、Animationウィンドウでクリップを選んで調整する。ユーザーが作るアニメーションのたたき台であり、全可動域での品質を保証するものではない。手足の極端な回転や歩行は追加のウェイト調整が必要になる。

## ボーン

| 名前 | 用途 |
|---|---|
| Root | 全体の浮遊・ジャンプ。やがぽんの矢も一緒に動く |
| Body | 体の傾き |
| Head | 顔と頭。目・口も追従 |
| Arm_L / Arm_R | 左右の手振り・勝利ポーズ |
| Leg_L / Leg_R | 左右の足。小さな揺れ向け |
| Tail_01〜03 | みたぬきのしっぽ。首との接続が崩れないよう小さく動かす |
| Arrow | やがぽんの矢。体とは独立して調整可能 |

`L/R` はキャラクターから見た左右。みたぬきは10本、やがぽんは8本、矢口くんは7本。IKコントローラーではなく、Unityで直接キーフレームを設定するFKリグ。スキンは各頂点最大4本以下、重みの合計を1に正規化。やがぽんは一体の体形に合わせて頭・手足の境界をなめらかに配分している。矢口くんは機械的な構造に合わせたパーツ単位の固定ウェイトで、腕のバネと手、脚のバネと靴をそれぞれ一緒に動かす。

## まばたき

Skinned Mesh RendererのBlendShapesで `Blink`（両目）、`Blink_L`、`Blink_R`（片目）を0〜100で動かす。FBXインポート後は名前にメッシュ名の接頭辞が付く場合がある。`Blink` と左右個別の値を同時に上げると変形が重なるため、どちらかの方式だけを使う。

閉じる際の眼球は顔の曲面に沿って変形し、みたぬきの白いハイライトは内側へ隠す。笑顔・口パク用のシェイプは今回の範囲外。

## Blender編集・再生成

- `ArtSource/Mitanuki/Mitanuki_Rigged.blend`
- `ArtSource/Yagapon/Yagapon_Rigged.blend`
- `ArtSource/Yaguchi/Yaguchi_Rigged.blend`

上記がリグ付き編集ファイル。元のパーツ別モデルおよび `_Unity.blend`（リグ追加前の軽量メッシュ）も保存している。

Blender 4.1で `--background --factory-startup --python Tools/rig_characters.py` を実行すると、軽量メッシュから3体のリグとFBXを再生成し、動作確認用のポーズをレンダリングする。再生成は手動のリグ修正を上書きするため、手動編集版は別名保存する。

ポリゴン数はみたぬき4,430、やがぽん4,510三角形で変更なし。ただしスキニングとブレンドシェイプの処理は追加されるので、静止メッシュと実行コストが同一という意味ではない。

## 検証

`Tools/verify_character_rigs.py` は実際のFBXをBlenderに読み戻し、三角形数、ボーン数、正規化ウェイト、3つの表情シェイプ、頭・左右手足・しっぽ／矢の変形を検証する。

Unityの生成処理はGeneric Avatar・スキン・BlendShapeの取り込みと、サンプルを実際に適用してBakeMeshした頂点の変形を検証する。AR認識と組み合わせた表示・実紙・9体同時の速度計測は別途必要。

2026-11-06: BlenderのFBX読み戻し検証、およびUnity 6000.3.24f1のバッチ実行で両モデルのインポートとWave / Victory / Blinkの変形検証に成功。Unity取り込み後はみたぬき4,430、やがぽん4,484三角形（FBX内は4,510）。メッシュTransformのスケールは1。実行ログは `Logs/character-animation-setup.log`。

FBXの単位はFBX側に記録し、ボーンやメッシュに100倍スケールを残さない設定で書き出す。Unityで新しいサンプルを作る場合も、ボーンのローカル軸に合わせて回転を設定する。

`Marubatsu > Characters > Render Animation Previews` でUnity標準のプレビュー専用環境を使い、各サンプルを適用したメッシュ画像を `ArtSource/<名前>/<名前>_unity_idle.png` などへ出力できる。実際のゲームシーンは変更しない。Unityでの最終検証ログは `Logs/character-animation-final.log`。

矢口くんもUnity 6000.3.24f1で取り込み・テクスチャ・変形・実描画を確認済み。4,433三角形、7本の骨、3つのまばたき。専用ログは `Logs/yaguchi-animation.log`。矢口くんだけのサンプルを再生成するバッチ用エントリポイントは `Marubatsu.Editor.CharacterAnimationSetup.PrepareYaguchi`（同キャラのサンプルクリップを上書きする）。

## AR勝利演出

`Assets/Marubatsu/Resources/MarubatsuVictory/` のFBXは、既存の3体のリグに適用するアニメーション専用ファイル。各FBXに `VictoryTakeoff`、`VictorySpin`、`VictoryPose`、`VictoryLoop`、`Defeat` の5クリップがある。FBXのルート階層を保持するため、Unityのインポート設定で **Preserve Hierarchy** を有効にする。

Blender 4.1で `blender --background --factory-startup --python-exit-code 1 --python Tools/victory_actions.py` を実行すると、既存の `*_Rigged.blend` から勝利用FBXのみを生成する。Unityの `Marubatsu > Characters > Validate Victory Clips` で、現在の対戦用Prefabのボーン階層にクリップが適用され、スキンが変形することを検証できる。

`Phase1_BoardTracking` のARCameraにある `PhaseOneBootstrap` の **Victory Settings** で、各段階の時間、浮上高さ、登場間隔、回転の緩急、リング半径、粒子数を調整できる。既定の演出は4秒。勝利表示後も3体は盤面上で浮遊し、画面下部に再戦とキャラクター選択が表示される。

実紙では、いつもの印刷用紙で対戦を開始して○・×それぞれの勝利を作る。縦・横・斜めの勝利で3体だけが浮上すること、紙を動かしたり傾けたりしたときにキャラクター・光・勝者名が同じ位置関係を保つことを確認する。途中で紙を画角から外して戻し、演出が続きから再開することを確認する。再戦時は空の用紙を使う。途中リセットは `R` キーでも確認できる。
