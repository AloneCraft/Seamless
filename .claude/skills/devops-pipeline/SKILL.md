---
name: devops-pipeline
description: UnityプロジェクトのCI/CD、テスト階層、ビルド自動化、リリース前検証を設計する。
---

# DevOps Pipeline

Unityプロジェクトのビルド・テスト・配布準備を自動化するスキル。

## 対象

- Unity Test Framework
- PlayMode / EditMode テスト
- GameCIなどのCI
- Windowsビルド
- バージョニング
- 成果物の保存

## 進め方

1. 現在のUnityバージョンとライセンス前提を確認する。
2. 最小の検証対象を決める。
3. EditModeテスト、PlayModeテスト、ビルド検証の順に組む。
4. 失敗時に差し戻せるログと成果物を残す。
5. ストア提出へ進む場合は `release-submission` を参照する。

Replay Syndrome では、MIG境界、QTE成否、Choice分岐、イベント購読解除を優先的にテストする。
