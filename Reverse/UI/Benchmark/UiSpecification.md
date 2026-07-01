---
phase: ui-reverse
reverse_document: true
canonical_target: Document/UI/Benchmark/UiSpecification.md
evidence_sources:
  - Benchmark/Shared/UI/MainPage.cs
  - Benchmark/UI/UI.csproj
  - Benchmark/Scalable.UI/Scalable.UI.csproj
  - Benchmark/Shared/UI.projitems
confidence: medium
review_status: draft
---

# Benchmark UI Specification

## Current Specification Notice

This reverse document captures the historical Benchmark UI implementation. CSV/JSON persistence, Save/Save As/Open, and external CSV import descriptions below are no longer the current forward specification. Current Benchmark and Scalable UI storage behavior is defined in `Document/UI/UiStorageSpecification.md`.

## 対象

この文書は `Benchmark\UI` と `Benchmark\Scalable.UI` が共有する MAUI ベンチマーク実行UIの現状仕様を、実装から復元したものです。通常版はアプリ名 `Seamless Benchmark`、Scalable版は `Seamless Scalable Benchmark` として構成されます。両方とも `Benchmark\Shared\UI.projitems` 経由で同じ共有UIコードを取り込みます。

証拠:
- `Benchmark/UI/UI.csproj:3`
- `Benchmark/UI/UI.csproj:8`
- `Benchmark/UI/UI.csproj:13`
- `Benchmark/UI/UI.csproj:63`
- `Benchmark/Scalable.UI/Scalable.UI.csproj:3`
- `Benchmark/Scalable.UI/Scalable.UI.csproj:8`
- `Benchmark/Scalable.UI/Scalable.UI.csproj:14`
- `Benchmark/Scalable.UI/Scalable.UI.csproj:54`
- `Benchmark/Shared/UI.projitems:8`

## 画面構成

Benchmark UI は単一の `ContentPage` です。コンストラクタでタイトル、背景色、`BenchmarkRunnerViewModel`、ツールバー、メインコンテンツを作成し、2行のルート `Grid` に配置します。上段はツールバー、下段はベンチマーク操作領域です。

証拠:
- `Benchmark/Shared/UI/MainPage.cs:10`
- `Benchmark/Shared/UI/MainPage.cs:72`
- `Benchmark/Shared/UI/MainPage.cs:80`
- `Benchmark/Shared/UI/MainPage.cs:81`

主要領域:
- ツールバー: 実行、停止、一時停止、保存、名前を付けて保存、開く、しきい値、実行プロファイル、実行履歴を表示します。
- ソースタブ: ベンチマーク対象の比較カテゴリを切り替えます。
- Benchmark 下部タブ: CPU、Log、Improvement Prompt を切り替えます。
- ベンチマーク表: メソッド行、左右プロジェクトの選択/状態/計測値、引数列、比率列を表示します。
- チャート: 選択した計測値を左右比較またはスケール推移で表示します。
- CPU 表: 論理CPUをP core、E core、Unknown core単位で表示し、実行対象CPUを選択します。
- ダイアログ: 終了、未保存、上書き、一時停止中操作の確認を表示します。

証拠:
- `Benchmark/Shared/UI/MainPage.cs:462`
- `Benchmark/Shared/UI/MainPage.cs:836`
- `Benchmark/Shared/UI/MainPage.cs:980`
- `Benchmark/Shared/UI/MainPage.cs:1124`
- `Benchmark/Shared/UI/MainPage.cs:1310`
- `Benchmark/Shared/UI/MainPage.cs:1448`
- `Benchmark/Shared/UI/MainPage.cs:2474`
- `Benchmark/Shared/UI/MainPage.cs:2511`
- `Benchmark/Shared/UI/MainPage.cs:2558`

## ツールバー仕様

ツールバーは `ImageButton` を中心に構成されます。ボタンはロールごとに作成され、クリック時に `HandleToolbarButtonClickAsync` を呼びます。Run ボタンだけはツールチップが `RunButtonTooltipText` にバインドされます。

証拠:
- `Benchmark/Shared/UI/MainPage.cs:462`
- `Benchmark/Shared/UI/MainPage.cs:604`
- `Benchmark/Shared/UI/MainPage.cs:618`
- `Benchmark/Shared/UI/MainPage.cs:628`
- `Benchmark/Shared/UI/MainPage.cs:672`

ツールバー条件:
- しきい値Pickerは `ThresholdOptions` と `SelectedThreshold` にバインドされ、ベンチマーク条件変更可能時だけ有効です。
- 実行プロファイルPickerは `ExecutionProfiles` と `SelectedExecutionProfile` にバインドされ、ベンチマーク条件変更可能時だけ有効です。
- 実行履歴Pickerは `RunOptions` と `SelectedRunOption` にバインドされます。

証拠:
- `Benchmark/Shared/UI/MainPage.cs:494`
- `Benchmark/Shared/UI/MainPage.cs:495`
- `Benchmark/Shared/UI/MainPage.cs:496`
- `Benchmark/Shared/UI/MainPage.cs:504`
- `Benchmark/Shared/UI/MainPage.cs:505`
- `Benchmark/Shared/UI/MainPage.cs:506`
- `Benchmark/Shared/UI/MainPage.cs:514`
- `Benchmark/Shared/UI/MainPage.cs:515`

## ベンチマーク表仕様

ベンチマーク表は Syncfusion `SfDataGrid` で構築され、`BenchmarkRows` を ItemsSource にします。行ごとに選択チェック、左右プロジェクトセル、ステータス、CPU表示、実行テキスト、失敗テキスト、計測値、ツールチップを表示します。

証拠:
- `Benchmark/Shared/UI/MainPage.cs:1448`
- `Benchmark/Shared/UI/MainPage.cs:1470`
- `Benchmark/Shared/UI/MainPage.cs:1605`
- `Benchmark/Shared/UI/MainPage.cs:1987`
- `Benchmark/Shared/UI/MainPage.cs:1988`
- `Benchmark/Shared/UI/MainPage.cs:2009`
- `Benchmark/Shared/UI/MainPage.cs:2019`
- `Benchmark/Shared/UI/MainPage.cs:2022`
- `Benchmark/Shared/UI/MainPage.cs:2023`
- `Benchmark/Shared/UI/MainPage.cs:2040`
- `Benchmark/Shared/UI/MainPage.cs:2049`
- `Benchmark/Shared/UI/MainPage.cs:2059`
- `Benchmark/Shared/UI/MainPage.cs:2180`

選択操作:
- 行単位、列グループ単位、左右セル単位の選択状態を持ちます。
- ヘッダーの集約チェックは `IsAllBenchmarkSelectionChecked` または `BenchmarkColumnSelectionGroup.IsChecked` を OneWay で表示し、タップ時にViewModel側の選択操作を呼びます。
- チャート上の行/バーをタップすると、対応するベンチマーク行または左右セルを選択します。

証拠:
- `Benchmark/Shared/UI/MainPage.cs:1798`
- `Benchmark/Shared/UI/MainPage.cs:1802`
- `Benchmark/Shared/UI/MainPage.cs:1816`
- `Benchmark/Shared/UI/MainPage.cs:1820`
- `Benchmark/Shared/UI/MainPage.cs:1823`
- `Benchmark/Shared/UI/MainPage.cs:1830`
- `Benchmark/Shared/UI/MainPage.cs:3266`
- `Benchmark/Shared/UI/MainPage.cs:3331`

## チャート仕様

チャートは Syncfusion `SfCartesianChart` と `StackingColumn100Series` で左右比較を表示します。系列の ItemsSource は `ChartPoints` です。高さは `ChartHeight` にバインドされます。チャートには独自の `GraphicsView` オーバーレイが重ねられ、選択枠、ラベル、ヒットテストを担当します。

証拠:
- `Benchmark/Shared/UI/MainPage.cs:3114`
- `Benchmark/Shared/UI/MainPage.cs:3138`
- `Benchmark/Shared/UI/MainPage.cs:3154`
- `Benchmark/Shared/UI/MainPage.cs:3174`
- `Benchmark/Shared/UI/MainPage.cs:3209`
- `Benchmark/Shared/UI/MainPage.cs:3266`
- `Benchmark/Shared/UI/MainPage.cs:3306`
- `Benchmark/Shared/UI/MainPage.cs:3331`

チャートの表示モードは `BenchmarkChartMode.ElementComparison` と `BenchmarkChartMode.ScaleTrend` です。指標は `Time` と `Allocated` です。

証拠:
- `Benchmark/Shared/UI/BenchmarkRunnerModels.cs:18`
- `Benchmark/Shared/UI/BenchmarkRunnerModels.cs:23`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:648`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:677`

## CPU選択仕様

CPU表示は `CpuSections` を元に作られます。ViewModelはCPUトポロジを読み込み、P core、E core、Unknown core のセクションへ再構築します。UI上ではセクション、行、列、個別CPU単位で選択できます。少なくとも1つのCPUが選択されるようにViewModelが補正します。

証拠:
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:229`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:230`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:203`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:3594`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:750`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:758`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:767`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:780`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:3676`

## ダイアログ/終了仕様

終了時は `RequestCloseAsync` が実行中、停止中、未保存結果の有無を確認します。必要な場合は独自オーバーレイの確認ダイアログを表示し、選択に応じて停止、保存、破棄、キャンセルを行います。

証拠:
- `Benchmark/Shared/UI/MainPage.cs:122`
- `Benchmark/Shared/UI/MainPage.cs:141`
- `Benchmark/Shared/UI/MainPage.cs:145`
- `Benchmark/Shared/UI/MainPage.cs:155`
- `Benchmark/Shared/UI/MainPage.cs:255`
- `Benchmark/Shared/UI/MainPage.cs:269`

ファイル/フォルダ操作:
- `Open` はCSVまたはフォルダを選択できます。
- CSVはプラットフォーム別のファイルタイプ指定を持ちます。
- 保存先/エクスポート先はフォルダPickerを使います。

証拠:
- `Benchmark/Shared/UI/MainPage.cs:413`
- `Benchmark/Shared/UI/MainPage.cs:421`
- `Benchmark/Shared/UI/MainPage.cs:449`

## Visual Verification

2026-07-01の検証で、MAUIアプリ起動とスクリーンショット取得を試行しましたが、通常UIプロセスは `MainWindowHandle=0` のままウィンドウ検出できず、スクリーンショットは未取得です。Scalable UIは既存プロセスが出力ファイルをロックしていましたが、同じく `MainWindowHandle=0` で画面証跡として利用できませんでした。

## Unknown

- 実行画面のスクリーンショット照合は未完了です。MAUIアプリが通常のトップレベルウィンドウとして検出できる環境で再取得が必要です。
- 既存ソース内に文字化けしている日本語表示が複数あります。これは実装事実として記録し、改善判断は `Reverse/UI/UIDebtFindings.md` に分離します。
