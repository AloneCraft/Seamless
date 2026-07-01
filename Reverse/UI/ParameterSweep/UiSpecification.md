---
phase: ui-reverse
reverse_document: true
canonical_target: Document/UI/ParameterSweep/UiSpecification.md
evidence_sources:
  - Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs
  - Benchmark/Tsavorite.ParameterSweep.UI/Tsavorite.ParameterSweep.UI.csproj
confidence: high
review_status: draft
---

# Parameter Sweep UI Specification

## 対象

この文書は `Benchmark\Tsavorite.ParameterSweep.UI` の現状UI仕様を実装から復元したものです。このアプリはMAUI単一ページアプリで、タイトルは `Tsavorite Parameter Sweep` です。

証拠:
- `Benchmark/Tsavorite.ParameterSweep.UI/Tsavorite.ParameterSweep.UI.csproj:3`
- `Benchmark/Tsavorite.ParameterSweep.UI/Tsavorite.ParameterSweep.UI.csproj:8`
- `Benchmark/Tsavorite.ParameterSweep.UI/Tsavorite.ParameterSweep.UI.csproj:13`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:3`

## 画面構成

画面は `ContentPage` で、コンストラクタで `ParameterSweepViewModel` を `BindingContext` に設定し、`CreateContent` で作ったコンテンツを表示します。

証拠:
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:3`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:10`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:13`

画面は3行の `Grid` です。
- 1行目: ツールバー。
- 2行目: ステータス。
- 3行目: 横スクロール可能な結果表。

証拠:
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:13`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:67`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:69`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:70`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:75`

## ツールバー仕様

ツールバーには以下のUI要素があります。
- `Run sweep` ボタン: `CanRun` に有効状態をバインドし、クリックで `RunFullAsync` を実行します。
- `Smoke` ボタン: `CanRun` に有効状態をバインドし、クリックで `RunSmokeAsync` を実行します。
- `Refresh runs` ボタン: クリックで `RefreshRuns` を実行します。
- `MaxCount` 入力欄: `MaxSuccessfulCountText` にバインドします。
- `Steps` 入力欄: `MaxSweepStepsText` にバインドします。
- 履歴Picker: `RunHistory` をItemsSource、`SelectedBaselineRun` をSelectedItemにバインドします。

証拠:
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:14`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:15`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:18`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:19`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:20`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:22`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:26`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:29`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:31`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:35`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:36`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:56`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:60`

## 結果表仕様

結果表は `CollectionView` で、`Rows` をItemsSourceにします。ヘッダーと各行は同じ列幅定義の `Grid` を使います。行選択は無効です。

証拠:
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:70`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:73`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:75`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:105`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:129`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:153`

列:
- `CaseName`
- `Collection`
- `Generic`
- `Backend`
- `Sweep`
- `Index`
- `Page`
- `Segment`
- `Memory`
- `MF`
- `MaxCount`
- `Failure`
- `Add`
- `Lookup`
- `CountDelta`
- `AddRatio`
- `LookupRatio`
- `FailureChanged`

証拠:
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:105`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:118`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:132`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:149`

各セルは `ParameterSweepComparisonViewRow` の同名または対応プロパティに `Label.Text` をバインドします。

証拠:
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:198`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:206`

## Visual Verification

2026-07-01の検証で、MAUIアプリ起動とスクリーンショット取得を試行しましたが、通常UIのウィンドウ検出失敗時点で処理を停止したため、ParameterSweep UIのスクリーンショットは未取得です。

## Unknown

- このUIには明示的なエラーダイアログやキャンセル操作は実装上確認できません。失敗は `StatusText` に表示されます。
- スクリーンショット照合は未完了です。MAUIアプリが通常のトップレベルウィンドウとして検出できる環境で再取得が必要です。
