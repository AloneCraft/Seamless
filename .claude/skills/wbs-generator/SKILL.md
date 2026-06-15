---
name: wbs-generator
description: 承認済み仕様から、実装可能な階層型WBSと作業順序を作る。
---

# WBS Generator

ゲーム仕様を、実装・アセット・UI・検証に分解するスキル。

## 進め方

1. 仕様の承認状態を確認する。
2. 体験価値、ゲームループ、UI、データ、C#実装、シーン、テストに分解する。
3. 各タスクに成果物、依存関係、完了条件を付ける。
4. MVPで必要な範囲と後回しにできる範囲を分ける。
5. Unity実装へ進む場合は `unity-game-dev`、アセット生成へ進む場合は `asset-pipeline` を参照する。

## 出力形式

- Epic
- Feature
- Task
- Done条件
- リスク
- 検証方法

Replay Syndrome では、MIG、QTE、Choice、LoS、Replay、Ending分岐を個別のFeatureとして扱う。
