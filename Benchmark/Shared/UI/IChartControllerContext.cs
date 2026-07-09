namespace UI;

// ここを編集する場合:
// - 複数タブまとめ更新処理の最適化
// - 指標別データの生成アルゴリズム
internal interface IChartControllerContext {
    void UpdateChartCoreForController(bool preserveExistingWhenNoMeasuredPoints);
    void UpdateAllSourceTabChartsCoreForController(bool preserveExistingWhenNoMeasuredPoints);
    IReadOnlyList<RankedRatioPoint> LoadImprovementCandidatesChartDataCore();
    IReadOnlyList<TrendSeries> LoadHistoryTrendChartDataCore();
}
