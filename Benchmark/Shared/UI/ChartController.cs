namespace UI;

// ここを編集する場合:
// - ChartController 経由の更新処理を最適化
// - chart用のデータ供給順序を変更
internal sealed class ChartController:IChartController {
    private readonly IChartControllerContext context;

    public ChartController(IChartControllerContext context) {
        this.context=context;
    }

    public void UpdateChart(bool preserveExistingWhenNoMeasuredPoints = true) =>
        this.context.UpdateChartCoreForController(preserveExistingWhenNoMeasuredPoints);

    public void UpdateAllSourceTabCharts(bool preserveExistingWhenNoMeasuredPoints = true) =>
        this.context.UpdateAllSourceTabChartsCoreForController(preserveExistingWhenNoMeasuredPoints);

    public IReadOnlyList<RankedRatioPoint> LoadImprovementCandidatesChartData() =>
        this.context.LoadImprovementCandidatesChartDataCore();

    public IReadOnlyList<TrendSeries> LoadHistoryTrendChartData() =>
        this.context.LoadHistoryTrendChartDataCore();
}
