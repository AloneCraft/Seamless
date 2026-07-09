# このチャートは view か

調査対象:
- 画面上の「比較チャート」
- 画面上の「履歴性能比」

結論:
- **UI の view です。**
- ただし **SQL の `VIEW` ではありません。**

根拠:
- チャートは `Benchmark/Shared/UI/MainPage.cs` で `SfCartesianChart` として生成されている。
- `CreateTrendChartTab(...)` は `Legend` を持つチャート UI を組み立てており、画面部品として扱われている。
- `LoadHistoryTrendChartData()` は `uiRunStore.QueryAnalysisView("BenchmarkPerformanceRatioHistory", 1000)` を呼ぶが、これは **DB 上の分析結果を読む入口** であり、表示要素そのものではない。
- `BenchmarkAnalysisViewMapper.MapPerformanceRatioHistory(...)` は `SourceTab / Type / Benchmark` ごとに `BenchmarkTrendSeries` を作り、最大 `12` 系列に絞っている。

実装上の読み分け:
- **表示の view**: 画面上のチャートコンポーネント
- **分析 view**: `QueryAnalysisView("BenchmarkPerformanceRatioHistory", ...)` のような DB 側の参照先

補足:
- `比較チャート` は `ChartComparisonPoint.DisplayName` を横軸ラベルにしている。
- `履歴性能比` は `RunId` を横軸にした履歴チャートで、系列は `SourceTab / Type / Benchmark` 単位。

参考ファイル:
- [`Benchmark/Shared/UI/MainPage.cs`](../../Benchmark/Shared/UI/MainPage.cs)
- [`Shared/UI.Core/BenchmarkAnalysisViewModels.cs`](../../Shared/UI.Core/BenchmarkAnalysisViewModels.cs)
- [`Benchmark/Shared/UI/ViewModels/RunnerViewModel.cs`](../../Benchmark/Shared/UI/ViewModels/RunnerViewModel.cs)
