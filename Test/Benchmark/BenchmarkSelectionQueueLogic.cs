namespace Test.Benchmark;

// 実行開始時の選択スナップショットと実行キュー生成が同じ規則で一致することを検証する。
[global::Seamless.FLinq.LinqIntercept(false)]
public sealed class BenchmarkSelectionQueueLogic{
    [Fact]
    public void CreateSelectedCases(){
        var sourceTab = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SystemArray);
        var catalog = Catalog(sourceTab.LeftProject,sourceTab.RightProject);
        {
            //選択したセルだけがキューに入る
            var selections = SelectCells(catalog,sourceTab,leftSelected:true,rightSelected:false);
            var cases = global::UI.BenchmarkSelectionQueueLogic.CreateSelectedCases(catalog,sourceTab,selections).ToArray();
            Assert.Equal(2,cases.Length);
            Assert.All(cases,item => Assert.Equal(sourceTab.LeftProject,item.Project));
        }
        {
            //未選択(false)と未知キーは含めない
            var selections = new Dictionary<string,bool>(StringComparer.OrdinalIgnoreCase){
                [global::UI.BenchmarkSelectionQueueLogic.CreateCellSelectionKey(sourceTab,"unknown-row",sourceTab.LeftProject)]=true,
            };
            foreach(var row in global::UI.BenchmarkRowGroupingLogic.CreateRows(catalog,sourceTab)){
                selections[global::UI.BenchmarkSelectionQueueLogic.CreateCellSelectionKey(sourceTab,row.RowKey,sourceTab.RightProject)]=false;
            }
            Assert.Empty(global::UI.BenchmarkSelectionQueueLogic.CreateSelectedCases(catalog,sourceTab,selections));
        }
    }

    [Fact]
    public void CreateSelectedCasesIncludesBothSystemArrayProjects(){
        var sourceTab = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SystemArray);
        var catalog = new[]{
            new global::UI.BenchmarkCase(sourceTab.LeftProject,sourceTab.LeftAssemblyName,"DotNet.Linq.Enumerable.FLinqInt32","Aggregate_func"),
            new global::UI.BenchmarkCase(sourceTab.RightProject,sourceTab.RightAssemblyName,"DotNet.Linq.Enumerable.ZLinqInt32","Aggregate_func"),
        };
        var selections = SelectCells(catalog,sourceTab,leftSelected:true,rightSelected:true);

        var cases = global::UI.BenchmarkSelectionQueueLogic.CreateSelectedCases(catalog,sourceTab,selections).ToArray();

        Assert.Equal(2,cases.Length);
        Assert.Contains(cases,item => item.Project==sourceTab.LeftProject&&item.AssemblyName==sourceTab.LeftAssemblyName);
        Assert.Contains(cases,item => item.Project==sourceTab.RightProject&&item.AssemblyName==sourceTab.RightAssemblyName);
    }

    [Fact]
    public void CreateSelectedCasesCanSelectOnlySystemArrayRightProject(){
        var sourceTab = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SystemArray);
        var catalog = new[]{
            new global::UI.BenchmarkCase(sourceTab.LeftProject,sourceTab.LeftAssemblyName,"DotNet.Linq.Enumerable.FLinqInt32","Aggregate_func"),
            new global::UI.BenchmarkCase(sourceTab.RightProject,sourceTab.RightAssemblyName,"DotNet.Linq.Enumerable.ZLinqInt32","Aggregate_func"),
        };
        var selections = SelectCells(catalog,sourceTab,leftSelected:false,rightSelected:true);

        var item = Assert.Single(global::UI.BenchmarkSelectionQueueLogic.CreateSelectedCases(catalog,sourceTab,selections));

        Assert.Equal(sourceTab.RightProject,item.Project);
        Assert.Equal(sourceTab.RightAssemblyName,item.AssemblyName);
    }

    [Fact]
    public void CreateSelectedCasesFromAllTabs(){
        {
            //スナップショット(1回目)とキュー生成(2回目)が同一辞書から同一結果になる
            var sourceTab = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SystemArray);
            var catalog = Catalog(sourceTab.LeftProject,sourceTab.RightProject);
            var selections = SelectCells(catalog,sourceTab,leftSelected:true,rightSelected:true);
            var snapshot = global::UI.BenchmarkSelectionQueueLogic.CreateSelectedCasesFromAllTabs(catalog,[sourceTab],selections);
            var queue = global::UI.BenchmarkSelectionQueueLogic.CreateSelectedCasesFromAllTabs(catalog,[sourceTab],selections);
            Assert.Equal(4,snapshot.Count);
            Assert.Equal(
                snapshot.Select(CaseIdentity).ToArray(),
                queue.Select(CaseIdentity).ToArray());
        }
        {
            //複数タブで同じプロジェクトのケースが重複しても 1 件に畳まれる
            var persisted = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SeamlessPersisted);
            var tsavorite = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SeamlessTsavorite);
            Assert.Equal(persisted.RightProject,tsavorite.RightProject);
            var shared = new global::UI.BenchmarkCase(persisted.RightProject,"Shared.Assembly","Shared.Type","Sum");
            var catalog = new[]{ shared };
            var selections = new Dictionary<string,bool>(StringComparer.OrdinalIgnoreCase);
            foreach(var tab in new[]{ persisted,tsavorite }){
                foreach(var row in global::UI.BenchmarkRowGroupingLogic.CreateRows(catalog,tab)){
                    selections[global::UI.BenchmarkSelectionQueueLogic.CreateCellSelectionKey(tab,row.RowKey,persisted.RightProject)]=true;
                }
            }
            var queue = global::UI.BenchmarkSelectionQueueLogic.CreateSelectedCasesFromAllTabs(catalog,[persisted,tsavorite],selections);
            Assert.Single(queue);
        }
        {
            //表示中タブの差し替え(生きた行状態)が優先される
            var sourceTab = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SystemArray);
            var catalog = Catalog(sourceTab.LeftProject,sourceTab.RightProject);
            var selections = SelectCells(catalog,sourceTab,leftSelected:true,rightSelected:true);
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

    private static global::UI.BenchmarkCase[] Catalog(string leftProject,string rightProject) => [
        new(leftProject,"Left.Assembly","Left.Type","Aggregate"),
        new(leftProject,"Left.Assembly","Left.Type","Sum"),
        new(rightProject,"Right.Assembly","Right.Type","Aggregate"),
        new(rightProject,"Right.Assembly","Right.Type","Sum"),
    ];

    private static Dictionary<string,bool> SelectCells(
        IReadOnlyList<global::UI.BenchmarkCase> catalog,
        global::UI.BenchmarkSourceTabDefinition sourceTab,
        bool leftSelected,
        bool rightSelected){
        var selections = new Dictionary<string,bool>(StringComparer.OrdinalIgnoreCase);
        foreach(var row in global::UI.BenchmarkRowGroupingLogic.CreateRows(catalog,sourceTab)){
            if(row.CasesByProject.ContainsKey(sourceTab.LeftProject)){
                selections[global::UI.BenchmarkSelectionQueueLogic.CreateCellSelectionKey(sourceTab,row.RowKey,sourceTab.LeftProject)]=leftSelected;
            }
            if(row.CasesByProject.ContainsKey(sourceTab.RightProject)){
                selections[global::UI.BenchmarkSelectionQueueLogic.CreateCellSelectionKey(sourceTab,row.RowKey,sourceTab.RightProject)]=rightSelected;
            }
        }
        return selections;
    }
}
