namespace Test.Benchmark;

[global::Seamless.FLinq.LinqIntercept(false)]
public sealed class BenchmarkSelectionQueueLogic{
    [Fact]
    public void CreateSelectedCases(){
        var sourceTab = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SystemArray);
        var catalog=Catalog(sourceTab);
        {
            var selections = SelectRows(catalog,sourceTab,true);
            var cases = global::UI.BenchmarkSelectionQueueLogic.CreateSelectedCases(catalog,sourceTab,selections).ToArray();
            Assert.Equal(4,cases.Length);
            Assert.Contains(cases,item => item.Project==sourceTab.LeftProject);
            Assert.Contains(cases,item => item.Project==sourceTab.RightProject);
        }
        {
            var selections = new Dictionary<string,bool>(StringComparer.OrdinalIgnoreCase){
                [global::UI.BenchmarkSelectionQueueLogic.CreateRowSelectionKey(sourceTab,"unknown-row")]=true,
            };
            foreach(var row in global::UI.BenchmarkRowGroupingLogic.CreateRows(catalog,sourceTab)){
                selections[global::UI.BenchmarkSelectionQueueLogic.CreateRowSelectionKey(sourceTab,row.RowKey)]=false;
            }
            Assert.Empty(global::UI.BenchmarkSelectionQueueLogic.CreateSelectedCases(catalog,sourceTab,selections));
        }
    }

    [Fact]
    public void CreateSelectedCasesIncludesBothSystemArrayProjects(){
        var sourceTab = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SystemArray);
        var catalog = new[]{
            new global::UI.BenchmarkCase(sourceTab.LeftProject,sourceTab.LeftAssemblyName,"Benchmark.System.Linq.Enumerable.FLinqInt32","Aggregate_func"),
            new global::UI.BenchmarkCase(sourceTab.RightProject,sourceTab.RightAssemblyName,"Benchmark.System.Linq.Enumerable.ZLinqInt32","Aggregate_func"),
        };
        var selections = SelectRows(catalog,sourceTab,true);

        var cases = global::UI.BenchmarkSelectionQueueLogic.CreateSelectedCases(catalog,sourceTab,selections).ToArray();

        Assert.Equal(2,cases.Length);
        Assert.Contains(cases,item => item.Project==sourceTab.LeftProject&&item.AssemblyName==sourceTab.LeftAssemblyName);
        Assert.Contains(cases,item => item.Project==sourceTab.RightProject&&item.AssemblyName==sourceTab.RightAssemblyName);
    }

    [Fact]
    public void CreateSelectedCasesFromAllTabs(){
        {
            var sourceTab = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SystemArray);
            var catalog=Catalog(sourceTab);
            var selections = SelectRows(catalog,sourceTab,true);
            var snapshot = global::UI.BenchmarkSelectionQueueLogic.CreateSelectedCasesFromAllTabs(catalog,[sourceTab],selections);
            var queue = global::UI.BenchmarkSelectionQueueLogic.CreateSelectedCasesFromAllTabs(catalog,[sourceTab],selections);
            Assert.Equal(4,snapshot.Count);
            Assert.Equal(
                snapshot.Select(CaseIdentity).ToArray(),
                queue.Select(CaseIdentity).ToArray());
        }
        {
            var persisted = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SeamlessPersisted);
            var tsavorite = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SeamlessTsavorite);
            Assert.Equal(persisted.RightProject,tsavorite.RightProject);
            var catalog=new[]{
                new global::UI.BenchmarkCase(persisted.RightProject,persisted.RightAssemblyName,"Benchmark.Persisted.Linq.Enumerable.LinqInt32","Sum"),
                new global::UI.BenchmarkCase(tsavorite.RightProject,tsavorite.RightAssemblyName,"Benchmark.Tsavorite.Linq.Enumerable.LinqInt32","Sum"),
            };
            var selections = new Dictionary<string,bool>(StringComparer.OrdinalIgnoreCase);
            foreach(var tab in new[]{ persisted,tsavorite }){
                foreach(var row in global::UI.BenchmarkRowGroupingLogic.CreateRows(catalog,tab)){
                    selections[global::UI.BenchmarkSelectionQueueLogic.CreateRowSelectionKey(tab,row.RowKey)]=true;
                }
            }
            var queue = global::UI.BenchmarkSelectionQueueLogic.CreateSelectedCasesFromAllTabs(catalog,[persisted,tsavorite],selections);
            Assert.Equal(2,queue.Count);
            Assert.Contains(queue,item => item.AssemblyName==persisted.RightAssemblyName);
            Assert.Contains(queue,item => item.AssemblyName==tsavorite.RightAssemblyName);
        }
        {
            var sourceTab = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SystemArray);
            var catalog=Catalog(sourceTab);
            var selections = SelectRows(catalog,sourceTab,true);
            var overrideCase = new global::UI.BenchmarkCase(sourceTab.LeftProject,"Live.Assembly","Live.Type","Aggregate");
            var queue = global::UI.BenchmarkSelectionQueueLogic.CreateSelectedCasesFromAllTabs(
                catalog,
                [sourceTab],
                selections,
                _ => new[]{ overrideCase });
            var item = Assert.Single(queue);
            Assert.Equal("Live.Type",item.TypeFullName);
        }
    }

    private static string CaseIdentity(global::UI.BenchmarkCase benchmark) =>
        string.Join("|",benchmark.Project,benchmark.AssemblyName,benchmark.TypeFullName,benchmark.MethodName,benchmark.ArgumentKey);

    private static global::UI.BenchmarkCase[] Catalog(global::UI.BenchmarkSourceTabDefinition sourceTab) => [
        new(sourceTab.LeftProject,sourceTab.LeftAssemblyName,"Left.Type","Aggregate"),
        new(sourceTab.LeftProject,sourceTab.LeftAssemblyName,"Left.Type","Sum"),
        new(sourceTab.RightProject,sourceTab.RightAssemblyName,"Right.Type","Aggregate"),
        new(sourceTab.RightProject,sourceTab.RightAssemblyName,"Right.Type","Sum"),
    ];

    private static Dictionary<string,bool> SelectRows(
        IReadOnlyList<global::UI.BenchmarkCase> catalog,
        global::UI.BenchmarkSourceTabDefinition sourceTab,
        bool selected){
        var selections = new Dictionary<string,bool>(StringComparer.OrdinalIgnoreCase);
        foreach(var row in global::UI.BenchmarkRowGroupingLogic.CreateRows(catalog,sourceTab)){
            selections[global::UI.BenchmarkSelectionQueueLogic.CreateRowSelectionKey(sourceTab,row.RowKey)]=selected;
        }
        return selections;
    }
}
