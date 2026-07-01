namespace UI;

[global::Seamless.FLinq.LinqIntercept(false)]
internal sealed class BenchmarkRunProjectionService{
    public IReadOnlyList<BenchmarkResultItem> GetVisibleResults(
        IEnumerable<BenchmarkResultItem> results,
        int? selectedElementCount) =>
        results
            .Where(item => selectedElementCount is null||item.ElementCount==0||item.ElementCount==selectedElementCount)
            .ToArray();

    public IReadOnlyList<BenchmarkComparisonRow> BuildComparisonRowsForElementCount(
        BenchmarkRunSession session,
        BenchmarkSourceTabDefinition sourceTab,
        int elementCount,
        Func<BenchmarkSourceTabDefinition,IReadOnlyList<BenchmarkResultItem>,ReportRow[]> createRows){
        var sourceResults = session.GetResults(sourceTab.Tab)
            .Where(result => result.ElementCount==elementCount
                &&result.ElementCount>0
                &&sourceTab.ContainsProject(result.Project)
                &&string.Equals(result.Status,"Completed",StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var rows = createRows(sourceTab,sourceResults);
        return rows.Length==0
            ? []
            : BenchmarkComparisonFormatter.BuildComparisonRows(rows,sourceTab);
    }

    public IReadOnlyList<BenchmarkComparisonRow> BuildLatestComparisonRows(
        BenchmarkRunSession session,
        BenchmarkSourceTabDefinition sourceTab,
        Func<BenchmarkSourceTabDefinition,IReadOnlyList<BenchmarkResultItem>,ReportRow[]> createRows){
        var sourceResults = session.GetResults(sourceTab.Tab)
            .Where(result => result.ElementCount>0
                &&sourceTab.ContainsProject(result.Project)
                &&string.Equals(result.Status,"Completed",StringComparison.OrdinalIgnoreCase))
            .GroupBy(static result => result.Method+"|"+result.TypeFullName+"|"+result.ArgumentKey,StringComparer.OrdinalIgnoreCase)
            .SelectMany(static group => group
                .GroupBy(static result => result.Project,StringComparer.OrdinalIgnoreCase)
                .Select(static projectGroup => projectGroup
                    .OrderByDescending(static result => result.StartedAt)
                    .ThenByDescending(static result => result.ElementCount)
                    .First()))
            .ToArray();
        var rows = createRows(sourceTab,sourceResults);
        return rows.Length==0
            ? []
            : BenchmarkComparisonFormatter.BuildComparisonRows(rows,sourceTab);
    }

    public IReadOnlyList<BenchmarkComparisonRow> BuildDisplayedRowsForElementCount(
        IEnumerable<BenchmarkResultItem> results,
        BenchmarkSourceTabDefinition sourceTab,
        int elementCount,
        Func<BenchmarkSourceTabDefinition,IReadOnlyList<BenchmarkResultItem>,ReportRow[]> createRows){
        var sourceResults = results
            .Where(result => result.ElementCount==elementCount
                &&result.ElementCount>0
                &&sourceTab.ContainsProject(result.Project)
                &&string.Equals(result.Status,"Completed",StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var rows = createRows(sourceTab,sourceResults);
        return rows.Length==0
            ? []
            : BenchmarkComparisonFormatter.BuildComparisonRows(rows,sourceTab);
    }

    public IReadOnlyList<BenchmarkComparisonRow> BuildDisplayedRows(
        IEnumerable<BenchmarkResultItem> results,
        BenchmarkSourceTabDefinition sourceTab,
        Func<BenchmarkSourceTabDefinition,IReadOnlyList<BenchmarkResultItem>,ReportRow[]> createRows){
        var sourceResults = results
            .Where(result => result.ElementCount>0
                &&sourceTab.ContainsProject(result.Project)
                &&string.Equals(result.Status,"Completed",StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var rows = createRows(sourceTab,sourceResults);
        return rows.Length==0
            ? []
            : BenchmarkComparisonFormatter.BuildComparisonRows(rows,sourceTab);
    }
}
