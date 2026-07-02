namespace UI;

/// <summary>
/// チェック済みセル辞書から実行キュー対象のベンチマークを組み立てる純ロジック。
/// 実行開始時の選択スナップショットと実行キュー生成が同じ規則になるよう、ViewModel から共有する。
/// セルキーは「タブID|行キー|プロジェクト」で、SQLite の UiSelections(BenchmarkCell) と同じ形式。
/// </summary>
[global::Seamless.FLinq.LinqIntercept(false)]
internal static class BenchmarkSelectionQueueLogic{
    public static string CreateCellSelectionKey(BenchmarkSourceTabDefinition sourceTab,string rowKey,string project) =>
        sourceTab.Id+"|"+rowKey+"|"+project;

    /// <summary>単一タブの選択セルから実行対象ケースを列挙する。選択辞書に無いセル・未選択セルは含めない。</summary>
    public static IEnumerable<BenchmarkCase> CreateSelectedCases(
        IReadOnlyList<BenchmarkCase> catalogCases,
        BenchmarkSourceTabDefinition sourceTab,
        IReadOnlyDictionary<string,bool> cellSelections){
        return BenchmarkRowGroupingLogic.CreateRows(catalogCases,sourceTab)
            .SelectMany(row => sourceTab.ProjectOrder.Select(project => (Row:row,Project:project)))
            .Where(item =>
                item.Row.CasesByProject.ContainsKey(item.Project)
                &&cellSelections.TryGetValue(CreateCellSelectionKey(sourceTab,item.Row.RowKey,item.Project),out var selected)
                &&selected)
            .Select(item => item.Row.CasesByProject[item.Project]);
    }

    /// <summary>
    /// 全タブの選択セルから重複を除いた実行キュー対象を組み立てる。
    /// <paramref name="overrideCases"/> は表示中タブなど、生きた行状態を優先したいタブの差し替え用。
    /// </summary>
    public static IReadOnlyList<BenchmarkCase> CreateSelectedCasesFromAllTabs(
        IReadOnlyList<BenchmarkCase> catalogCases,
        IEnumerable<BenchmarkSourceTabDefinition> sourceTabs,
        IReadOnlyDictionary<string,bool> cellSelections,
        Func<BenchmarkSourceTabDefinition,IEnumerable<BenchmarkCase>?>? overrideCases = null){
        return sourceTabs
            .SelectMany(sourceTab =>
                overrideCases?.Invoke(sourceTab)
                ??CreateSelectedCases(catalogCases,sourceTab,cellSelections))
            .GroupBy(
                static benchmark => string.Join("|",benchmark.Project,benchmark.AssemblyName,benchmark.TypeFullName,benchmark.MethodName,benchmark.ArgumentKey),
                StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToArray();
    }
}
