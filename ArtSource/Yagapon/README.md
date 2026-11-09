# やがぽん 3Dモデル 第3版・Unity軽量版

提供画像 `yagapon_transparent.webp` を参考にBlender 4.1で作成した立体モデル。
参考画像は左斜め前から見た姿として解釈。体の幅と奥行きを増やして球に近づけ、顔を矢の先端方向へ向け、両足の間を矢が通るまたがり姿勢に修正した。背面と奥行きは補完している。公式の立体形状を再現したものではない。

- 編集元: `Yagapon.blend`（照明・プレビューカメラを含む）
- Unity用: `Assets/Marubatsu/Art/Yagapon/Yagapon.fbx`（軽量モデルのみ）
- 軽量版Blender: `Yagapon_Unity.blend`
- 軽量版プレビュー: `Yagapon_unity_preview.png` / `Yagapon_unity_side.png`
- プレビュー: `Yagapon_preview.png`（左斜め前） / `Yagapon_front.png`（正面） / `Yagapon_side.png`（側面）
- 再生成: Blenderを `--background --factory-startup --python Tools/create_yagapon.py` で実行
- 編集元は20,804三角形、17メッシュ。Unity用は4,510三角形、1メッシュ、6色のマテリアル。外部テクスチャ不要。
- 三角形数を約78%削減。9体で40,590三角形（元は187,236）。6マテリアルのため、1メッシュでも1描画命令になるとは限らない。Unityでのフレーム時間は未計測。
- 再生成スクリプトは編集元を保存後、自動で軽量化する。編集元から軽量版だけ更新する場合は `Tools/optimize_yagapon.py` をBlenderで実行する。
- Unity用FBXは8本のボーン・正規化ウェイトとBlink / Blink_L / Blink_R付き。リグ編集元は `Yagapon_Rigged.blend`。造形用の `.blend` / `_Unity.blend` はリグ追加前の状態を保持。Unityサンプルと使い方は `Docs/CHARACTER_ANIMATION.md` を参照。Colliderは未作成。

## Unityでの使い方

第3版では、丸い左右方向の厚みを維持しつつ、側面の輪郭を元画像の上が細く下がふっくらした形に調整した。頭頂部を少し前へ寄せ、顔も調整後の表面に沿わせている。両足と矢の配置は第2版を維持。

1. Projectウィンドウから `Assets/Marubatsu/Art/Yagapon/Yagapon.fbx` をシーンにドラッグする。
2. FBXのMaterialsでマテリアルのインポートを有効にする。個別に色を調整したい場合はExtract Materialsで展開する。
3. モデルはメートル単位で、高さ約2.1m、矢の方向の全長約3.9m。36mmのマスに置く場合は、まず均一スケール `0.008` 程度（全長約31mm）から調整する。
4. 空の親オブジェクトの子に配置し、盤面での向き・高さを調整してPrefab化する。

Blenderでの生成・レンダリングとFBXの読み戻しを確認。リグ追加後にUnity 6000.3.24f1でインポート、サンプルの変形、プレビュー画像を確認。ARカメラ・実紙での配置・パフォーマンスは未検証。既存シーンや認識・ゲームロジックへの組み込みは行っていない。
