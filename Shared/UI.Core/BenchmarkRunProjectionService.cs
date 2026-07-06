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
        int preferredElementCount) =>
        SelectPerProjectResults(methodGroup,sourceTab,preferredElementCount);

    private static IReadOnlyList<BenchmarkResultItem> SelectLatestElementCountResults(
        IGrouping<string,BenchmarkResultItem> methodGroup,
        BenchmarkSourceTabDefinition sourceTab) =>
        SelectPerProjectResults(methodGroup,sourceTab,preferredElementCount: null);

    // プロジェクトごとに独立して選ぶ: 優先軸(preferredElementCount)がありその軸の結果があればそれ、
    // 無ければそのプロジェクト自身の最新完了結果。Fast/Adaptiveでは同じメソッドでもプロジェクトごとに
    // 打ち切り軸が異なるのが通常運転のため、「両プロジェクトが同じ軸に揃っている」ことを要求しない
    // (要求すると、揃わなかった側が丸ごと脱落し時間比/メモリ比がMissing扱いになってしまう)。
    private static IReadOnlyList<BenchmarkResultItem> SelectPerProjectResults(
        IGrouping<string,BenchmarkResultItem> methodGroup,
        BenchmarkSourceTabDefinition sourceTab,
        int? preferredElementCount){
        return sourceTab.ProjectOrder
            .Select(project => {
                var projectResults = methodGroup.Where(result => string.Equals(result.Project,project,StringComparison.OrdinalIgnoreCase));
                if(preferredElementCount is { } preferred){
                    var atPreferred = projectResults
                        .Where(result => result.AxisCount==preferred)
                        .OrderByDescending(static result => result.StartedAt??DateTimeOffset.MinValue)
                        .FirstOrDefault();
                    if(atPreferred is not null){
                        return atPreferred;
                    }
                }

                return projectResults
                    .OrderByDescending(static result => result.StartedAt??DateTimeOffset.MinValue)
                    .FirstOrDefault();
            })
            .Where(static result => result is not null)
            .Select(static result => result!)
            .ToArray();
    }
}
