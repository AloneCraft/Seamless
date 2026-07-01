namespace UI;

public sealed class BenchmarkRowGroupingLogicTests{
    [Fact]
    public void CreateRowsKeepsSingleComparisonRowWhenEachProjectHasOneMethod(){
        var sourceTab = BenchmarkSourceTabs.Default;
        var rows = BenchmarkRowGroupingLogic.CreateRows(
            [
                Case(sourceTab.LeftProject,"Left.Assembly","Left.Route","Where"),
                Case(sourceTab.RightProject,"Right.Assembly","Right.Route","Where"),
            ],
            sourceTab);

        var row = Assert.Single(rows);
        Assert.Equal("Where",row.MethodName);
        Assert.True(row.CasesByProject.ContainsKey(sourceTab.LeftProject));
        Assert.True(row.CasesByProject.ContainsKey(sourceTab.RightProject));
    }

    [Fact]
    public void CreateRowsSplitsSameProjectMethodByType(){
        var sourceTab = BenchmarkSourceTabs.Default;
        var rows = BenchmarkRowGroupingLogic.CreateRows(
            [
                Case(sourceTab.LeftProject,"Left.Assembly","Left.TypeA","Where"),
                Case(sourceTab.LeftProject,"Left.Assembly","Left.TypeB","Where"),
                Case(sourceTab.RightProject,"Right.Assembly","Right.TypeA","Where"),
                Case(sourceTab.RightProject,"Right.Assembly","Right.TypeB","Where"),
            ],
            sourceTab);

        Assert.Equal(2,rows.Count);
        Assert.NotEqual(rows[0].RowKey,rows[1].RowKey);
        Assert.All(rows,row => Assert.Equal("Where",row.MethodName));
        Assert.All(rows,row => Assert.Equal("Where",row.BenchmarkName));
    }

    [Fact]
    public void CreateRowsSplitsSameProjectMethodByArgumentKey(){
        var sourceTab = BenchmarkSourceTabs.Default;
        var rows = BenchmarkRowGroupingLogic.CreateRows(
            [
                Case(sourceTab.LeftProject,"Left.Assembly","Left.TypeA","Add","Count=1",[new BenchmarkArgument("Count","1")]),
                Case(sourceTab.LeftProject,"Left.Assembly","Left.TypeA","Add","Count=2",[new BenchmarkArgument("Count","2")]),
                Case(sourceTab.RightProject,"Right.Assembly","Right.TypeA","Add","Count=1",[new BenchmarkArgument("Count","1")]),
                Case(sourceTab.RightProject,"Right.Assembly","Right.TypeA","Add","Count=2",[new BenchmarkArgument("Count","2")]),
            ],
            sourceTab);

        Assert.Equal(2,rows.Count);
        Assert.NotEqual(rows[0].RowKey,rows[1].RowKey);
        Assert.Contains(rows,row => row.RowKey.Contains("Count=1",StringComparison.Ordinal));
        Assert.Contains(rows,row => row.RowKey.Contains("Count=2",StringComparison.Ordinal));
        Assert.All(rows,row => Assert.Equal("Add",row.MethodName));
    }

    [Fact]
    public void CreateRowsUsesScalableListTypeInBenchmarkName(){
        var sourceTab = BenchmarkSourceTabs.Get(BenchmarkSourceTab.ScalablePersistedList);
        var rows = BenchmarkRowGroupingLogic.CreateRows(
            [
                Case(sourceTab.LeftProject,"Scalable.Persisted.Collections","Scalable.Persisted.Collections.List.ListString","Add"),
                Case(sourceTab.LeftProject,"Scalable.Persisted.Collections","Scalable.Persisted.Collections.List.ListInt32","Add"),
                Case(sourceTab.LeftProject,"Scalable.Persisted.Collections","Scalable.Persisted.Collections.List.ListBoolean","Add"),
            ],
            sourceTab);

        Assert.Contains(rows,row => row.BenchmarkName=="<string>.Add_000064_0004096_0008192_0016384_05");
        Assert.Contains(rows,row => row.BenchmarkName=="<int>.Add_000064_0004096_0008192_0016384_05");
        Assert.Contains(rows,row => row.BenchmarkName=="<System.Boolean>.Add_000064_0004096_0008192_0016384_05");
        Assert.All(rows,row => Assert.Equal("Add",row.MethodName));
    }

    [Fact]
    public void CreateRowsUsesScalableDictionaryKeyAndValueTypesInBenchmarkName(){
        var sourceTab = BenchmarkSourceTabs.Get(BenchmarkSourceTab.ScalableTsavoriteDictionary);
        var rows = BenchmarkRowGroupingLogic.CreateRows(
            [
                Case(sourceTab.LeftProject,"Scalable.Tsavorite.Collections","Scalable.Tsavorite.Collections.Dictionary.DictionaryStringInt32","Add"),
                Case(sourceTab.LeftProject,"Scalable.Tsavorite.Collections","Scalable.Tsavorite.Collections.Dictionary.DictionaryManagedStructManagedStruct","Add"),
            ],
            sourceTab);

        Assert.Contains(rows,row => row.BenchmarkName=="<string,int>.Add_131072_0004096_0004096_0016384_10");
        Assert.Contains(rows,row => row.BenchmarkName=="<ManagedStruct,ManagedStruct>.Add_131072_0004096_0004096_0016384_10");
        Assert.All(rows,row => Assert.Equal("Add",row.MethodName));
    }

    [Fact]
    public void BuildComparisonRowsUsesTypeOnlyForDuplicateProjectMethods(){
        var sourceTab = BenchmarkSourceTabs.Default;
        var rows = BenchmarkComparisonFormatter.BuildComparisonRows(
            [
                Report(sourceTab.LeftProject,"Where","Left.Route",10),
                Report(sourceTab.RightProject,"Where","Right.Route",20),
            ],
            sourceTab);

        var row = Assert.Single(rows);
        Assert.Equal("Where",row.Benchmark);
        Assert.Equal(string.Empty,row.TypeFullName);
    }

    [Fact]
    public void BuildComparisonRowsSplitsDuplicateProjectMethodsByShortType(){
        var sourceTab = BenchmarkSourceTabs.Default;
        var rows = BenchmarkComparisonFormatter.BuildComparisonRows(
            [
                Report(sourceTab.LeftProject,"Where","Left.TypeA",10),
                Report(sourceTab.LeftProject,"Where","Left.TypeB",11),
                Report(sourceTab.RightProject,"Where","Right.TypeA",20),
                Report(sourceTab.RightProject,"Where","Right.TypeB",21),
            ],
            sourceTab);

        Assert.Equal(2,rows.Count);
        Assert.Contains(rows,row => row.TypeFullName=="TypeA");
        Assert.Contains(rows,row => row.TypeFullName=="TypeB");
    }

    [Fact]
    public void BuildComparisonRowsKeepsScalableTypeForSingleMethod(){
        var sourceTab = BenchmarkSourceTabs.Get(BenchmarkSourceTab.ScalablePersistedList);
        var rows = BenchmarkComparisonFormatter.BuildComparisonRows(
            [
                Report(sourceTab.LeftProject,"Add","Scalable.Persisted.Collections.List.ListInt32",10),
            ],
            sourceTab);

        var row = Assert.Single(rows);
        Assert.Equal("<int>.Add_000064_0004096_0008192_0016384_05",row.Benchmark);
        Assert.Equal("ListInt32",row.TypeFullName);
    }

    [Fact]
    public void BuildComparisonRowsUsesScalableSetFullNameWithElementCount(){
        var sourceTab = BenchmarkSourceTabs.Get(BenchmarkSourceTab.ScalableTsavoriteSet);
        var rows = BenchmarkComparisonFormatter.BuildComparisonRows(
            [
                Report(sourceTab.LeftProject,"Add","Scalable.Tsavorite.Collections.Set.SetInt32",20),
            ],
            sourceTab,
            131072);

        var row = Assert.Single(rows);
        Assert.Equal("<int>.Add_131072_0004096_0004096_16777216_10",row.Benchmark);
        Assert.Equal("Add",row.MethodName);
        Assert.Equal("SetInt32",row.TypeFullName);
    }

    [Fact]
    public void BuildComparisonRowsUsesScalableDictionaryFullNameWithElementCount(){
        var sourceTab = BenchmarkSourceTabs.Get(BenchmarkSourceTab.ScalableTsavoriteDictionary);
        var rows = BenchmarkComparisonFormatter.BuildComparisonRows(
            [
                Report(sourceTab.LeftProject,"Add","Scalable.Tsavorite.Collections.Dictionary.DictionaryStringInt32",20),
            ],
            sourceTab,
            131072);

        var row = Assert.Single(rows);
        var csv = BenchmarkComparisonFormatter.WriteCsv(rows,sourceTab);

        Assert.Equal("<string,int>.Add_131072_0004096_0004096_16777216_10",row.Benchmark);
        Assert.Contains("<string,int>.Add_131072_0004096_0004096_16777216_10",csv,StringComparison.Ordinal);
        Assert.Equal("Add",row.MethodName);
        Assert.Equal("DictionaryStringInt32",row.TypeFullName);
    }

    [Fact]
    public void DefaultComparisonRowGetProjectReturnsMissing(){
        var row = default(BenchmarkComparisonRow);

        var metrics = row.GetProject(BenchmarkSourceTabs.Default.LeftProject);

        Assert.False(metrics.HasMeasuredValue);
    }

    private static BenchmarkCase Case(string project,string assembly,string type,string method) =>
        new(project,assembly,type,method);

    private static BenchmarkCase Case(string project,string assembly,string type,string method,string argumentKey,IReadOnlyList<BenchmarkArgument> arguments) =>
        new(project,assembly,type,method,argumentKey,arguments);

    private static ReportRow Report(string project,string method,string type,double mean) =>
        new(
            project,
            method,
            type,
            method,
            "Completed",
            mean.ToString(System.Globalization.CultureInfo.InvariantCulture),
            string.Empty,
            string.Empty,
            "0",
            string.Empty,
            string.Empty,
            mean,
            0,
            0,
            0,
            0,
            0,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            0);
}
