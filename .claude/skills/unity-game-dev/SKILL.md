---
name: unity-game-dev
description: Unity 6.4 / URP 17.4 / 新 Input System 1.19 前提で、Replay Syndrome のC#実装・改修を行う。
---

# Unity Game Dev

Replay Syndrome のUnity/C#実装を行うスキル。

## 必須ルール

- 名前空間は `ReplaySyndrome.<機能>`。
- 旧Input API（`Input.GetKey`, `Input.GetAxis`）は使わない。`InputActionReference` と新 Input System を使う。
- UIは uGUI（`Text`, `Button`, `Slider`, `Image`）を使う。
- 依存は `[SerializeField] private` で受ける。
- イベント購読は `OnEnable` で登録し、`OnDisable` で解除する。
- ポーズ中も進むUIは `WaitForSecondsRealtime` を使う。
- クラスには `/// <summary>` を付ける。
- ブレースはAllmanスタイル。1行ifでも `{ }` を付ける。

## 既存コア機構

- `MIGManager`: 精神侵食ゲージ管理。`ApplyDelta(float)` で更新。
- `GameStateManager`: `SetPaused(bool)` でポーズ状態を制御。
- `QTEManager`: QTE開始、成否判定、MIG差分適用。
- `ChoiceManager`: 選択肢表示、選択結果、MIG差分適用。
- `LoSObjectSwap`: 現実/記憶マテリアル切替。
- `ReplaySequenceController`: リプレイ演出、カメラ抵抗、メモリーフラッシュ制御。

## 実装前確認

1. 既存コードの命名・責務・イベント設計を読む。
2. 変更範囲を最小化する。
3. Editor専用APIは必ず `Assets/_Project/Editor/` 配下に置く。
4. コンパイル確認はUnityエディタ、IDE、または利用可能なビルド手段で行う。
