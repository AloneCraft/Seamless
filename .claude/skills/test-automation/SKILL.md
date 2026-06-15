---
name: test-automation
description: Unity Test Framework を使い、Replay Syndrome のテストファースト開発、単体テスト、結合テスト、総合テスト、回帰テスト、ローカル/CI自動実行、テスト雛形生成、失敗ログ整理を行う。Unity/C# 実装前にテストを先に書く依頼、既存機能へテストを追加する依頼、テスト自動化ツールやCI向けテストコマンドを整備する依頼で使う。
---

# Test Automation

Replay Syndrome の Unity 6.4 / Unity Test Framework 向けに、テストファーストで検証を設計し、単体・結合・総合テストを自動化するスキル。

## 基本方針

1. 先に期待仕様を短く確認し、失敗するテストを書く。
2. テストを実行し、失敗理由が期待どおりか確認する。
3. 最小実装でテストを通す。
4. 関連する回帰テストを追加し、EditMode と PlayMode の両方を必要最小限で回す。
5. 失敗ログ、結果 XML、再実行コマンドを残す。

物理ファイルの大きな生成やCI追加は、仕様が承認済みかユーザーが明示的に依頼した場合に行う。

## テスト階層

- 単体テスト: 純粋な C# ロジック、ScriptableObject の値、MIG 増減、閾値判定、QTE/Choice の結果計算。原則 EditMode。
- 結合テスト: Manager と UI、イベント購読、GameStateManager の pause、MIG と QTE/Choice の連携。EditMode を優先し、フレーム進行が必要なら PlayMode。
- 総合テスト: シーン、Bootstrapper、HospitalRoomEventSequence、ReplaySequenceController、LoS 表示切替などのユーザー体験の流れ。PlayMode。

配置の標準:

```text
Assets/_Project/Tests/EditMode/
Assets/_Project/Tests/PlayMode/
Assets/_Project/Tests/System/
```

テストクラス名は `<対象>Tests`、メソッド名は `Condition_ExpectedResult` を基本にする。

## Replay Syndrome で優先する観点

- `MIGManager.ApplyDelta(float)` は 0-100 に clamp される。
- MIG のゾーン境界は Stable 0-25 / Awakening 26-50 / Critical 51-75 / Collapse 76-100。
- 閾値をまたいだ時だけ `OnThresholdEntered` が発火する。
- UI はイベント購読を `OnEnable` で行い、`OnDisable` で解除する。
- QTE / Choice の成功・失敗・選択結果は MIG 差分へ反映される。
- ポーズ中に進む UI は `WaitForSecondsRealtime` を使う。
- Input は新 Input System の `InputActionReference` を使い、旧 Input API を使わない。
- Editor API は `Assets/_Project/Editor/` 外のランタイムコードから呼ばない。

## 実装ワークフロー

1. 変更対象の仕様、既存コード、依存関係を読む。
2. 先にテストケース一覧を作る。境界値、成功、失敗、解除漏れ、null 安全性を含める。
3. `scripts/New-ReplaySyndromeTestScaffold.ps1` で必要なテストフォルダを作る。
4. EditMode で書けるものは EditMode に置く。Scene、Coroutine、フレーム更新、実 Input が必要なものだけ PlayMode に置く。
5. `scripts/Invoke-UnityTests.ps1` で対象レイヤーを実行する。
6. 失敗を確認してから実装し、再実行する。

## 自動化ツール

### テスト雛形生成

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .claude/skills/test-automation/scripts/New-ReplaySyndromeTestScaffold.ps1 -ProjectRoot . -Layer All
```

`-Layer` は `EditMode`, `PlayMode`, `System`, `All`。フォルダだけを作り、`.meta` は Unity に任せる。

### Unity Test Runner 実行

プロジェクトでは次のラッパーを優先する。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Run-ReplaySyndromeTests.ps1 -TestPlatform all
```

スキル同梱スクリプトを直接使う場合:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .claude/skills/test-automation/scripts/Invoke-UnityTests.ps1 -ProjectRoot . -TestPlatform editmode
powershell -NoProfile -ExecutionPolicy Bypass -File .claude/skills/test-automation/scripts/Invoke-UnityTests.ps1 -ProjectRoot . -TestPlatform playmode
powershell -NoProfile -ExecutionPolicy Bypass -File .claude/skills/test-automation/scripts/Invoke-UnityTests.ps1 -ProjectRoot . -TestPlatform all
```

出力先:

```text
TestResults/
  editmode-results.xml
  editmode.log
  playmode-results.xml
  playmode.log
```

Unity の場所が自動検出できない場合は `-UnityPath` を渡す。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .claude/skills/test-automation/scripts/Invoke-UnityTests.ps1 -ProjectRoot . -UnityPath "C:\Program Files\Unity\Hub\Editor\6000.4.0f1\Editor\Unity.exe" -TestPlatform all
```

## テストを書く時の規約

- namespace は `ReplaySyndrome.Tests.<Layer>` を使う。
- テストも C# 識別子は英語にする。ゲーム内文字列だけ日本語を許可する。
- Allman ブレース、1行 if でも `{ }` を付ける。
- Unity オブジェクトを作ったら `TearDown` で `Object.DestroyImmediate` または PlayMode では `Object.Destroy` する。
- イベントテストは購読解除の検証も入れる。
- 時間依存テストは可能な限り時計を注入する。難しい場合は PlayMode で `UnityTest` と `yield return` を使う。
- テストが不安定なら本体設計を見直す。待ち時間を伸ばすだけで解決しない。

## 完了条件

- 追加/変更した挙動に対する失敗先行のテストがある。
- 単体、結合、総合のどの層で保証したか説明できる。
- 実行コマンドと結果 XML / ログの場所を報告できる。
- テスト未実行なら、理由と次に実行すべき正確なコマンドを報告する。
