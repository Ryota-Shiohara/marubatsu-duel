# 矢口くん 3Dモデル

理工展公式マスコット「矢口くん」の公開イラストを参考に作成した非公式の立体化モデル。やがぽん・みたぬきと同様、Unity用の軽量モデル、Genericリグ、まばたき、編集可能なサンプルアニメーションを用意。

## 参考資料（2026-11-06）

- 理工展公式キャラクター紹介: https://www.rikoten.com/about/yaguchi
- 公式掲載イラスト: https://www.rikoten.com/images/yaguchi/%E7%9F%A2%E5%8F%A3%E3%81%8F%E3%82%93.jpg
- 着ぐるみ写真: https://takadanobaba.keizai.biz/photoflash/79/

主に現行公式イラストの丸い頭、中央の黄色い顔、暖色の頭・手足、青い胴体、胸の×印、裾の線、バネ状の手足を再現。着ぐるみ写真は奥行きの参考とし、写真とイラストの差はイラストを優先。公式の3Dデータではなく、背面や寸法は補完している。参考画像はReferencesに保存し、ゲームモデルには含めない。

## 使うファイル

- `Assets/Marubatsu/Art/Yaguchi/Yaguchi_Animated.prefab`: Unity用。待機・手振り・勝利・まばたきのサンプル付き。
- `Assets/Marubatsu/Art/Yaguchi/Yaguchi.fbx`: リグ付きモデル。
- `Assets/Marubatsu/Art/Yaguchi/YaguchiWarm.png`: 頭・手足用の16×128の小さなグラデーションテクスチャ。同じフォルダーのまま扱う。
- `Yaguchi.blend`: パーツ別編集元。
- `Yaguchi_Unity.blend`: リグ追加前の軽量メッシュ。
- `Yaguchi_Rigged.blend`: リグ編集用。
- `Yaguchi_preview.png` / `Yaguchi_side.png` / `Yaguchi_back.png`: Blenderプレビュー。
- `Yaguchi_unity_idle.png` / `Yaguchi_unity_wave.png` / `Yaguchi_unity_victory.png` / `Yaguchi_unity_blink.png`: Unityプレビュー。

高さ約2.3m、横幅約2.3mの編集単位。36mmのマスに置く場合は均一スケール0.012程度から調整する。Colliderは付けていない。ゲームロジックや既存のARシーンへの組み込みは行っていない。

## アニメーション

Root、Body、Head、Arm_L/R、Leg_L/Rの7本。ロボットの構造に合わせて各パーツを対応する骨へ固定する。バネを物理シミュレーションするリグではなく、腕のバネと手、脚のバネと靴をそれぞれ一緒に回転させる軽量なFKリグ。

Blink / Blink_L / Blink_Rは0〜100で開眼〜閉眼を調整する。両目用と左右個別の値は重ねて使わない。詳細は `Docs/CHARACTER_ANIMATION.md` を参照。

## 再生成と検証

Blender 4.1で `--background --factory-startup --python Tools/create_yaguchi.py` を実行するとモデル、テクスチャ、リグ、FBXを再生成。手動編集版は別名で保存する。

`Tools/verify_character_rigs.py` はFBXの三角形数・骨・ウェイト・まばたき・各部の変形を確認する。Unityでは `Marubatsu > Characters > Prepare Animation Examples` でサンプルを生成できる。正確なモデル統計は `rig_stats.json`。

実紙ARでの見え方、9体同時表示時の速度は未検証。

2026-11-06検証結果: 4,433三角形、1メッシュ、6マテリアル、7本のボーン、3つのまばたきシェイプ。BlenderのFBX読み戻しで骨・ウェイト・各部の変形を確認。Unity 6000.3.24f1でGeneric Avatar、テクスチャ、Wave / Victory / Blinkの変形を検証し、実描画プレビューも確認済み。ログは `Logs/yaguchi-animation.log`。
