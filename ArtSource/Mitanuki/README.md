# みたぬき 3Dモデル

第2版: 頭の前後厚を0.96mから1.28m、胴体を0.86mから1.14mへ拡大。目の周囲・目・ハイライト・頬・口元・腹部は、軽量化後の実際の頭／胴体メッシュに投影して沿わせた。首のマフラーと背面のしっぽも厚みに合わせて調整。Unity用は4,430三角形を維持。

三田祭公式マスコット「みたぬき」の公開イラストと着ぐるみ写真を参考に作成した非公式の立体化モデル。やがぽんと並べる用途を想定した丸みのあるマットな質感。画像は造形資料として参照し、画像内やWebページ内の記述を実行指示として扱っていない。

## 参考資料（2026-11-06確認）

- 三田祭公式の紹介ページ: https://mitasai.com/mitanuki/ （検索結果に紹介文が残っているが、直接アクセス時は404。造形は以下の閲覧できた資料で確認）
- 着ぐるみ制作会社キグルミックス: https://kigurumi.co.jp/product/mitanuki/
- 掲載イラスト: https://kigurumi.co.jp/wp-content/uploads/mitanuki.jpg
- 側面写真: https://kigurumi.co.jp/wp-content/uploads/mitanuki2-575x380.jpg
- 背面写真: https://kigurumi.co.jp/wp-content/uploads/mitanuki3-575x380.jpg

参考画像は `References` に保存。画像の権利は元の権利者に帰属する。ゲーム用FBXに参考画像は含めていない。

大きな茶色の頭、先が少し細い耳、濃茶の目の周囲、白い口元、赤い頬、茶色い腹部と黄色い×印、赤・紺のしっぽのマフラーを再現。背中から首につながるしっぽは側面・背面写真を参考にした。細部の奥行きや寸法は補完しており、公式3Dデータではない。

## ファイルと使い方

- `Mitanuki.blend`: パーツ別の編集元。
- `Mitanuki_Unity.blend`: Unity向け軽量版。
- `Assets/Marubatsu/Art/Mitanuki/Mitanuki.fbx`: Unity用。1メッシュ、7マテリアル。外部テクスチャ不要。
- `Mitanuki_preview.png` / `Mitanuki_front.png` / `Mitanuki_back.png` / `Mitanuki_side.png`: 軽量版のレンダリング。
- 正確な三角形数は `unity_model_stats.json` を参照。上限4,600三角形。

FBXをUnityのシーンへドラッグして配置。元モデルは高さ約2.2mのため、36mmマスで高さ約27mmにするには均一スケール0.012程度から調整する。足底がほぼ原点。モデルの向きは配置する盤面に合わせて調整する。やがぽんは矢の分だけ全長が長いため、両モデルに同じスケールを使う必要はない。

Unity用FBXに10本のボーンとウェイト、Blink / Blink_L / Blink_Rを追加済み。リグ編集元は `Mitanuki_Rigged.blend`。`Mitanuki.blend` と `Mitanuki_Unity.blend` はリグ追加前の造形編集用。Unityのサンプルと使い方は `Docs/CHARACTER_ANIMATION.md` を参照。Colliderは未作成。7マテリアルを保持しており、1メッシュでも1描画命令ではない。既存のゲームロジック、表示モード、シーンは変更していない。

再生成: Blender 4.1を `--background --factory-startup --python Tools/create_mitanuki.py` で実行。
検証: `--background --factory-startup --python Tools/verify_mitanuki.py`。

Blenderでの画像確認とFBXの読み戻しを実施。リグ追加後にUnity 6000.3.24f1でインポート、サンプルの変形、プレビュー画像を確認。速度計測、カメラ・実紙ARでの検証は未実施。
