---
phase: ui-reverse
reverse_document: true
canonical_target: Document/UI/ParameterSweep/BindingAndLifecycleSpecification.md
evidence_sources:
  - Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs
  - Benchmark/Tsavorite.ParameterSweep/ParameterSweepPersistence.cs
  - Benchmark/Tsavorite.ParameterSweep.UI/Tsavorite.ParameterSweep.UI.csproj
confidence: high
review_status: draft
---

# Parameter Sweep Binding And Lifecycle Specification

## Current Specification Notice

This reverse document captures historical ParameterSweep binding and persistence behavior. References to CSV persistence are retained only as reverse evidence. Current DB-canonical behavior is defined in `Document/UI/UiStorageSpecification.md`.

## ViewModel構造

`ParameterSweepViewModel` は `INotifyPropertyChanged` を実装する画面状態コンテナです。結果行は `Rows`、履歴は `RunHistory` の `ObservableCollection` として公開されます。

証拠:
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:8`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:28`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:29`

主要プロパティ:
- `SelectedBaselineRun`: 比較対象の過去Run。変更時に `ApplyBaselineComparison` を実行します。
- `IsRunning`: 実行中状態。変更時に `CanRun` も通知します。
- `CanRun`: `!IsRunning`。
- `StatusText`: ステータス表示。
- `MaxSuccessfulCountText`: 最大成功件数入力。
- `MaxSweepStepsText`: 最大探索ステップ入力。

証拠:
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:31`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:39`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:43`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:51`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:55`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:57`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:68`

## 実行ライフサイクル

初期化:
- ViewModelコンストラクタはソリューションルートまたはLocalApplicationData配下から `Work\Tsavorite.ParameterSweep` をルートにして `ParameterSweepRunStore` を作成します。
- コンストラクタ末尾で `RefreshRuns` を呼び、履歴を読み込みます。

証拠:
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:17`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:23`

実行:
- `RunFullAsync` は `RunAsync(smoke:false)` を呼びます。
- `RunSmokeAsync` は `RunAsync(smoke:true)` を呼びます。
- `RunAsync` は実行中なら何もしません。
- 実行開始時に `IsRunning=true` とし、`Rows` をクリアします。
- Smoke実行ではスイープケースを制限します。
- 進捗ごとに `Rows` へ結果行を追加し、`StatusText` を更新します。
- 完了後は結果を保存し、履歴を更新し、保存先をステータスに表示します。
- 例外時は `Failed: <message>` をステータスに表示します。
- finallyで `IsRunning=false` に戻します。

証拠:
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:90`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:92`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:103`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:104`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:107`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:110`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:116`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:118`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:120`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:121`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:122`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:125`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:128`

## 入力値

`CreateOptions` は `MaxSuccessfulCountText` と `MaxSweepStepsText` を正の整数として解釈します。解釈できない場合は、それぞれ `65536` と `8` を既定値にします。Smokeの場合は `maxSuccessfulCount` を最大 `256`、`maxSweepSteps` を最大 `2` に制限します。`LookupProbeCount` は `4096` と `maxSuccessfulCount` の小さい方です。

証拠:
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:132`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:133`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:151`

## 比較ライフサイクル

履歴更新:
- `RefreshRuns` は現在選択中のRunIdを保持し、ストアからRun一覧を再読込します。
- 読込後、以前の選択Runがあれば再選択し、なければ先頭を選びます。

証拠:
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:94`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:95`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:100`

ベースライン比較:
- `ApplyBaselineComparison` はベースライン未選択または現在行が空なら何もしません。
- 選択Runの結果をストアから読み、現在行と比較して `Rows` を比較行へ置き換えます。
- 読込失敗時は何も表示変更しません。

証拠:
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:151`

## 永続化

`ParameterSweepRunStore` はルートディレクトリ配下にRunId単位のフォルダを作り、`results.csv` を保存します。Run一覧はディレクトリを列挙し、`results.csv` の行数から結果数を算出します。読込は `results.csv` をUTF-8で読み、CSVパーサへ渡します。

証拠:
- `Benchmark/Tsavorite.ParameterSweep/ParameterSweepPersistence.cs:5`
- `Benchmark/Tsavorite.ParameterSweep/ParameterSweepPersistence.cs:7`
- `Benchmark/Tsavorite.ParameterSweep/ParameterSweepPersistence.cs:12`
- `Benchmark/Tsavorite.ParameterSweep/ParameterSweepPersistence.cs:18`
- `Benchmark/Tsavorite.ParameterSweep/ParameterSweepPersistence.cs:20`
- `Benchmark/Tsavorite.ParameterSweep/ParameterSweepPersistence.cs:28`
- `Benchmark/Tsavorite.ParameterSweep/ParameterSweepPersistence.cs:45`

CSV列は `RunId`、`MeasuredAt`、`CaseName`、`Collection`、`Generic`、`Backend`、`Sweep`、サイズ群、成功件数、失敗情報、Add/Lookup計測値で構成されます。

証拠:
- `Benchmark/Tsavorite.ParameterSweep/ParameterSweepPersistence.cs:50`

## Unknown

- 実行中にユーザーが入力欄を変更した場合のロックはUI側で確認できません。ボタンのみ `CanRun` で制御されています。
- 実行キャンセル機能は実装上確認できません。
