# Seamless Verification

`Verification.Core` と `Verification.Infrastructure` は、通常ベンチマーク、Scalable、Parameter Sweep、Dynamic LINQ、テストプロジェクトを同じ「計画 → 実行 → 保存 → 比較 → 再現」の流れで扱う検証基盤です。MAUI アプリと `seamless-verify` は同じ `VerificationWorkspace` を呼び出します。

## 最初の実行

検証DBは自動作成されません。既存データを暗黙に削除・移行しないため、リポジトリごとに一度だけ明示的に初期化します。

```powershell
dotnet run --project Verification.Cli -- init --root G:\GitHub\Seamless
dotnet run --project Verification.Cli -- plan --root G:\GitHub\Seamless --suite changed --profile quick --output Work\Verification\plan.json
dotnet run --project Verification.Cli -- run --root G:\GitHub\Seamless --plan Work\Verification\plan.json
```

ツールとしてpack・installした場合のコマンド名は `seamless-verify` です。既存DBツールの `seamless database ...` とは別のコマンドです。

検証DBの正本は `Work/Verification/Verification.db` です。ログ、生成コード、Dynamicの再現入力などの大きな成果物は `Work/Verification/Artifacts/<run-id>/` に置き、SQLiteから相対パス、SHA-256、長さで参照します。

## コマンド

| 操作 | 例 | 動作 |
|---|---|---|
| 初期化 | `seamless-verify init` | 新しいSQLite DBを `CreateNew` で作る。既存DBは上書きしない |
| 計画 | `seamless-verify plan --suite changed --profile quick` | Git差分、未追跡ファイル、ProjectReference、`.projitems` から対象と理由を生成する |
| 実行 | `seamless-verify run --plan plan.json` | 計画作成時とソース指紋が同じことを確認して実行する |
| 一括実行 | `seamless-verify run --suite all --profile full` | 全対象の完全検証を計画して実行する |
| 履歴 | `seamless-verify list` / `show --run <id>` | 生結果、条件、診断、成果物、基準比較を表示する |
| 基準固定 | `seamless-verify baseline --run <id>` | 全ケース成功済みのrunだけを比較基準に設定する |
| 比較 | `seamless-verify compare --run <id> [--baseline <id>]` | 固定基準と候補を比較する |
| 再現 | `seamless-verify replay --run <id> [--case <id>]` | 失敗ケースまたは指定ケースを現在のビルドで再実行する |
| 再開 | `seamless-verify resume --run <id>` | 同じソース指紋の未完了ケースを測り直す |
| 失敗縮小 | `seamless-verify shrink --run <id> --case <id>` | Dynamic失敗の署名を維持しながら入力を縮小する |

`changed` で変更の所属を特定できない場合は、狭い対象を推測せず共通スイートを選びます。`quick` の実行時間枠に入らなかったケースは `NotRun` または `Cancelled` のまま保存され、成功にはなりません。

## 検証スイート

| suite | アダプター | 主な対象 |
|---|---|---|
| `changed` / `all` | `dotnet-test` と代表計測 | 変更依存関係または全テストプロジェクト |
| `benchmark` | `headless-benchmark` | 同一Worker内のFLinq／比較対象の反復測定 |
| `scalable` | `scalable-worker` | 件数を増やしたコレクション測定 |
| `sweep` | `parameter-sweep` | Tsavorite容量・設定の成功境界 |
| `dynamic` | `dynamic-worker` | Seed固定の生成クエリ、全実装経路、診断、生成コード |

性能比較はケース版、入力ハッシュ、意味論、Codec版、測定方法、OS／ランタイム、CPU、構成がすべて一致するときだけ行います。条件が違う結果は `Incompatible` です。性能ケースは5回以上の有効な生測定が必要で、中央値が既定の5%境界を超えて悪化すると `Regressed` になります。容量スイープは最大成功件数と失敗種別を比較します。結果ダイジェストの不一致は性能とは分けて `CorrectnessFailure` にします。

基準は成功しただけでは更新されません。`baseline` を明示的に実行したときだけ切り替わります。校正値は本測定として保存しません。

## MAUIアプリ

```powershell
dotnet run --project Verification.UI -c Release -f net10.0-windows10.0.19041.0
```

画面の入口は「変更後の確認」「性能比較」「容量・設定探索」「クエリの正しさ検証」です。未初期化のリポジトリでは初期化ボタンだけを提示し、起動時にDBを作りません。計画には対象と選定理由を表示し、実行後はケース条件、生測定、資源使用量、診断、成果物ハッシュ、固定基準との比較を表示します。

## 異常終了と再現性

run開始時に全ケースを `NotRun` として保存し、開始時に `Running`、完了時に最終状態へ更新します。プロセスが落ちて `run.lock` を保持する所有者がいなくなった場合、次回のWorkspace開始時にrunと実行中ケースを `Interrupted` にします。`resume` はソース指紋が同じ場合だけ許可します。

Dynamicのreplayは保存された `replay-request.json` のSHA-256を照合してから使用します。改変、成果物ルート外へのパス、異なる失敗署名は拒否します。

## 旧性能ゲート

`Shared/UI.Core/BenchmarkPerformanceGate.cs` と `Benchmark.Headless/PerformanceGate*` は旧UI／既存Run 249互換のためだけに残しています。固定run ID、11対象、N=4096という契約は新しい検証フローには使用しません。新しい判定の正本は `Verification.Core.VerificationComparison` と各結果の `ComparisonConditions` です。

## CIと固定性能環境

通常CIは `Verification.Tests`、`Execution.Tests`、DBの重要回帰、CLI／MAUIのReleaseビルドを実行します。`.github/workflows/verification.yml` の `workflow_dispatch` または夜間実行では `full` 計画を起動できます。

厳密な性能基準は、CPUと測定条件が固定された環境で作成してください。ホストが異なるrunは比較不可になるため、一般のホステッドCI結果を固定性能基準へ流用しません。
