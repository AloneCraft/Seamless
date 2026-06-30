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
    public void CreateRowsUsesScalableListTypeInBenchmarkName(){
        var sourceTab = BenchmarkSourceTabs.Get(BenchmarkSourceTab.ScalablePersistedList);
        var rows = BenchmarkRowGroupingLogic.CreateRows(
            [
                Case(sourceTab.LeftProject,"Scalable.Persisted.Collections","Scalable.Persisted.Collections.List.ListString","Add"),
                Case(sourceTab.LeftProject,"Scalable.Persisted.Collections","Scalable.Persisted.Collections.List.ListInt32","Add"),
                Case(sourceTab.LeftProject,"Scalable.Persisted.Collections","Scalable.Persisted.Collections.List.ListBoolean","Add"),
            ],
            sourceTab);

        Assert.Contains(rows,row => row.BenchmarkName=="Scalable.Persisted.List<string>.Add");
        Assert.Contains(rows,row => row.BenchmarkName=="Scalable.Persisted.List<int>.Add");
        Assert.Contains(rows,row => row.BenchmarkName=="Scalable.Persisted.List<System.Boolean>.Add");
        Assert.All(rows,row => Assert.Equal("Add",row.MethodName));
    }

    [Fact]
    public void CreateRowsUsesScalableDictionaryKeyAndValueTypesInBenchmarkName(){
        var sourceTab = BenchmarkSourceTabs.Get(BenchmarkSourceTab.ScalableDictionary);
        var rows = BenchmarkRowGroupingLogic.CreateRows(
            [
                Case(sourceTab.LeftProject,"Scalable.Persisted.Collections","Scalable.Persisted.Collections.Dictionary.DictionaryStringInt32","Add"),
                Case(sourceTab.RightProject,"Scalable.Tsavorite.Collections","Scalable.Tsavorite.Collections.Dictionary.DictionaryStringInt32","Add"),
                Case(sourceTab.LeftProject,"Scalable.Persisted.Collections","Scalable.Persisted.Collections.Dictionary.DictionaryManagedStructManagedStruct","Add"),
                Case(sourceTab.RightProject,"Scalable.Tsavorite.Collections","Scalable.Tsavorite.Collections.Dictionary.DictionaryManagedStructManagedStruct","Add"),
            ],
            sourceTab);

        Assert.Contains(rows,row => row.BenchmarkName=="Scalable.Tsavorite.Dictionary<string,int>.Add");
        Assert.Contains(rows,row => row.BenchmarkName=="Scalable.Tsavorite.Dictionary<ManagedStruct,ManagedStruct>.Add");
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
        Assert.Equal("Scalable.Persisted.List<int>.Add",row.Benchmark);
        Assert.Equal("ListInt32",row.TypeFullName);
    }

    [Fact]
    public void BuildComparisonRowsUsesScalableSetFullNameWithElementCount(){
        var sourceTab = BenchmarkSourceTabs.Get(BenchmarkSourceTab.ScalableSet);
        var rows = BenchmarkComparisonFormatter.BuildComparisonRows(
            [
                Report(sourceTab.LeftProject,"Add","Scalable.Persisted.Collections.Set.SetInt32",10),
                Report(sourceTab.RightProject,"Add","Scalable.Tsavorite.Collections.Set.SetInt32",20),
            ],
            sourceTab,
            131072);

        var row = Assert.Single(rows);
        Assert.Equal("Scalable.Tsavorite.Set<int>.Add/131072",row.Benchmark);
        Assert.Equal("Add",row.MethodName);
        Assert.Equal("SetInt32",row.TypeFullName);
    }

    [Fact]
    public void BuildComparisonRowsUsesScalableDictionaryFullNameWithElementCount(){
        var sourceTab = BenchmarkSourceTabs.Get(BenchmarkSourceTab.ScalableDictionary);
        var rows = BenchmarkComparisonFormatter.BuildComparisonRows(
            [
                Report(sourceTab.LeftProject,"Add","Scalable.Persisted.Collections.Dictionary.DictionaryStringInt32",10),
                Report(sourceTab.RightProject,"Add","Scalable.Tsavorite.Collections.Dictionary.DictionaryStringInt32",20),
            ],
            sourceTab,
            131072);

        var row = Assert.Single(rows);
        var csv = BenchmarkComparisonFormatter.WriteCsv(rows,sourceTab);

        Assert.Equal("Scalable.Tsavorite.Dictionary<string,int>.Add/131072",row.Benchmark);
        Assert.Contains("Scalable.Tsavorite.Dictionary<string,int>.Add/131072",csv,StringComparison.Ordinal);
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
