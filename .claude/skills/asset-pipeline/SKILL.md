---
name: asset-pipeline
description: 承認済み仕様をUnityプロジェクト内のデータ構造、ScriptableObject、マテリアル、シーン構成、生成アセット配置へ落とし込む。
---

# Asset Pipeline

承認済みの仕様を、Unityで扱える物理ファイルとシーン構成に変換するスキル。

## 進め方

1. 仕様がユーザーに承認済みか確認する。未承認ならファイル生成を行わない。
2. 生成対象を分類する: ScriptableObject、Material、Prefab、Scene、Editor Tool、テストデータ。
3. `Assets/_Project/` 配下の既存ディレクトリ規約に従って配置する。
4. `.meta` ファイルやGUIDは手で書き換えない。
5. 大量生成が必要な場合は `Assets/_Project/Editor/` 配下にワンショットのEditorツールを作る。

## Replay Syndrome の配置規約

- ScriptableObject: `Assets/_Project/ScriptableObjects/<機能>/`
- Material: `Assets/_Project/Materials/<機能>/`
- Runtime Script: `Assets/_Project/Scripts/<機能>/`
- Editor Script: `Assets/_Project/Editor/`
- Scene: `Assets/_Project/Scenes/`

## 注意点

- マテリアルは URP 前提で `Universal Render Pipeline/Lit` を使う。
- private な `[SerializeField]` への配線は `SerializedObject` + `FindProperty` を使う。
- 実装コードに進む場合は `unity-game-dev` の規約を適用する。
