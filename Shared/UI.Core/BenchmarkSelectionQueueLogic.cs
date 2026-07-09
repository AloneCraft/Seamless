namespace UI;

/// <summary>
/// チェック済み行辞書から実行キュー対象のベンチマークを組み立てる純ロジック。
/// 行キーは「タブID|行キー」で、選択行の左右両方に存在するケースを実行対象にする。
/// </summary>
[global::Seamless.FLinq.LinqIntercept(false)]
internal static class BenchmarkSelectionQueueLogic{
    public static string CreateRowSelectionKey(BenchmarkSourceTabDefinition sourceTab,string rowKey) =>
        sourceTab.Id+"|"+rowKey;

    public static IEnumerable<BenchmarkCase> CreateSelectedCases(
        IReadOnlyList<BenchmarkCase> catalogCases,
        BenchmarkSourceTabDefinition sourceTab,
        IReadOnlyDictionary<string,bool> rowSelections){
        return BenchmarkRowGroupingLogic.CreateRows(catalogCases,sourceTab)
            .Where(row => row.CasesByProject.Count>0
                &&rowSelections.TryGetValue(CreateRowSelectionKey(sourceTab,row.RowKey),out var selected)
                &&selected)
            .SelectMany(row => sourceTab.ProjectOrder
                .Where(project => row.CasesByProject.ContainsKey(project))
                .Select(project => row.CasesByProject[project]));
    }

    public static IReadOnlyList<BenchmarkCase> CreateSelectedCasesFromAllTabs(
        IReadOnlyList<BenchmarkCase> catalogCases,
        IEnumerable<BenchmarkSourceTabDefinition> sourceTabs,
        IReadOnlyDictionary<string,bool> rowSelections,
        Func<BenchmarkSourceTabDefinition,IEnumerable<BenchmarkCase>?>? overrideCases = null){
        return sourceTabs
            .SelectMany(sourceTab =>
                overrideCases?.Invoke(sourceTab)
                ??CreateSelectedCases(catalogCases,sourceTab,rowSelections))
            .GroupBy(
                static benchmark => string.Join("|",benchmark.Project,benchmark.AssemblyName,benchmark.TypeFullName,benchmark.MethodName,benchmark.ArgumentKey),
                StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToArray();
    }
}
