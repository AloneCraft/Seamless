namespace UI;

// ここを編集する場合:
// - チャートデータ生成順序を変更
// - 更新トランザクション追加
internal interface IChartController {
    void UpdateChart(bool preserveExistingWhenNoMeasuredPoints = true);
    void UpdateAllSourceTabCharts(bool preserveExistingWhenNoMeasuredPoints = true);
    IReadOnlyList<RankedRatioPoint> LoadImprovementCandidatesChartData();
    IReadOnlyList<TrendSeries> LoadHistoryTrendChartData();
}
