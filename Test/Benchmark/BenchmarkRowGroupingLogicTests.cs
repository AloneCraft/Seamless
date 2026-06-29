namespace Benchmark;

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
        Assert.Contains(rows,row => row.BenchmarkName.Contains("TypeA",StringComparison.Ordinal));
        Assert.Contains(rows,row => row.BenchmarkName.Contains("TypeB",StringComparison.Ordinal));
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
