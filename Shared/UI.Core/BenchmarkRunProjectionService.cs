namespace UI;

[global::Seamless.FLinq.LinqIntercept(false)]
internal sealed class BenchmarkRunProjectionService{
    public IReadOnlyList<BenchmarkResultItem> GetVisibleResults(
        IEnumerable<BenchmarkResultItem> results,
        int? selectedElementCount) =>
        results
            .Where(item => selectedElementCount is null||item.AxisCount==0||item.AxisCount==selectedElementCount)
            .ToArray();

    public IReadOnlyList<BenchmarkComparisonRow> BuildComparisonRowsForElementCount(
        IReadOnlyList<BenchmarkResultItem> sessionResults,
        BenchmarkSourceTabDefinition sourceTab,
        int elementCount,
        Func<BenchmarkSourceTabDefinition,IReadOnlyList<BenchmarkResultItem>,ReportRow[]> createRows){
        var sourceResults = sessionResults
            .Where(result => result.AxisCount==elementCount
                &&result.AxisCount>0
                &&sourceTab.ContainsProject(result.Project)
                &&string.Equals(result.Status,"Completed",StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var rows = createRows(sourceTab,sourceResults);
        return rows.Length==0
            ? []
            : BenchmarkComparisonFormatter.BuildComparisonRows(rows,sourceTab);
    }

    public IReadOnlyList<BenchmarkComparisonRow> BuildLatestComparisonRows(
        IReadOnlyList<BenchmarkResultItem> sessionResults,
        BenchmarkSourceTabDefinition sourceTab,
        Func<BenchmarkSourceTabDefinition,IReadOnlyList<BenchmarkResultItem>,ReportRow[]> createRows){
        var sourceResults = sessionResults
            .Where(result => result.AxisCount>0
                &&sourceTab.ContainsProject(result.Project)
                &&string.Equals(result.Status,"Completed",StringComparison.OrdinalIgnoreCase))
            .GroupBy(static result => result.Method+"|"+result.TypeFullName+"|"+result.ArgumentKey,StringComparer.OrdinalIgnoreCase)
            .SelectMany(group => SelectLatestElementCountResults(group,sourceTab))
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
            .Where(result => result.AxisCount==elementCount
                &&result.AxisCount>0
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
            .Where(result => result.AxisCount>0
                &&sourceTab.ContainsProject(result.Project)
                &&string.Equals(result.Status,"Completed",StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var rows = createRows(sourceTab,sourceResults);
        return rows.Length==0
            ? []
            : BenchmarkComparisonFormatter.BuildComparisonRows(rows,sourceTab);
    }

    public IReadOnlyList<BenchmarkComparisonRow> BuildDisplayedRowsForSelectedElementCount(
        IReadOnlyList<BenchmarkResultItem> results,
        BenchmarkSourceTabDefinition sourceTab,
        int? selectedElementCount,
        Func<BenchmarkSourceTabDefinition,IReadOnlyList<BenchmarkResultItem>,ReportRow[]> createRows){
        if(selectedElementCount is not { } displayedElementCount){
            return this.BuildDisplayedRows(results,sourceTab,createRows);
        }

        // 適応的ループ回数では行(メソッド)ごとに終端Nが異なるため、選択Nの厳密一致だけでは
        // 選択Nちょうどで確定した行しか表示されない。メソッド単位で「選択Nの計測があればそれ、
        // 無ければそのメソッド自身の最新Nの計測」を採用する。
        var sourceResults = results
            .Where(result => result.AxisCount>0
                &&sourceTab.ContainsProject(result.Project)
                &&string.Equals(result.Status,"Completed",StringComparison.OrdinalIgnoreCase))
            .GroupBy(static result => result.Method+"|"+result.TypeFullName+"|"+result.ArgumentKey,StringComparer.OrdinalIgnoreCase)
            .SelectMany(group => SelectPreferredElementCountResults(group,sourceTab,displayedElementCount))
            .ToArray();
        var rows = createRows(sourceTab,sourceResults);
        return rows.Length==0
            ? []
            : BenchmarkComparisonFormatter.BuildComparisonRows(rows,sourceTab);
    }

    private static IReadOnlyList<BenchmarkResultItem> SelectPreferredElementCountResults(
        IGrouping<string,BenchmarkResultItem> methodGroup,
        BenchmarkSourceTabDefinition sourceTab,
        int preferredElementCount){
        var preferred = methodGroup
            .Where(result => result.AxisCount==preferredElementCount)
            .GroupBy(static result => result.Project,StringComparer.OrdinalIgnoreCase)
            .Select(static projectGroup => projectGroup
                .OrderByDescending(static result => result.StartedAt??DateTimeOffset.MinValue)
                .First())
            .ToArray();
        if(preferred.Length>0){
            return sourceTab.ProjectOrder
                .Select(project => preferred.FirstOrDefault(result => string.Equals(result.Project,project,StringComparison.OrdinalIgnoreCase)))
                .Where(static result => result is not null)
                .Select(static result => result!)
                .ToArray();
        }

        return SelectLatestElementCountResults(methodGroup,sourceTab);
    }

    private static IReadOnlyList<BenchmarkResultItem> SelectLatestElementCountResults(
        IGrouping<string,BenchmarkResultItem> methodGroup,
        BenchmarkSourceTabDefinition sourceTab){
        var elementGroup = methodGroup
            .GroupBy(static result => result.AxisCount)
            .Select(group => new LatestElementCountGroup(
                group.Key,
                group.Max(static result => result.StartedAt??DateTimeOffset.MinValue),
                group
                    .GroupBy(static result => result.Project,StringComparer.OrdinalIgnoreCase)
                    .Select(static projectGroup => projectGroup
                        .OrderByDescending(static result => result.StartedAt??DateTimeOffset.MinValue)
                        .First())
                    .ToArray()))
            .OrderByDescending(static group => group.LatestStartedAt)
            .ThenByDescending(static group => group.ElementCount)
            .FirstOrDefault();
        if(elementGroup.Results is null||elementGroup.Results.Length==0){
            return [];
        }

        return sourceTab.ProjectOrder
            .Select(project => elementGroup.Results.FirstOrDefault(result => string.Equals(result.Project,project,StringComparison.OrdinalIgnoreCase)))
            .Where(static result => result is not null)
            .Select(static result => result!)
            .ToArray();
    }

    private readonly record struct LatestElementCountGroup(
        int ElementCount,
        DateTimeOffset LatestStartedAt,
        BenchmarkResultItem[] Results);
}
