using UI;

namespace Test.Benchmark;

public sealed class BenchmarkRowGroupingLogic{
    [Fact]
    public void CreateRowsKeepsSingleComparisonRowWhenEachProjectHasOneMethod(){
        var sourceTab = BenchmarkSourceTabs.Default;
        var rows = global::UI.BenchmarkRowGroupingLogic.CreateRows(
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
        var rows = global::UI.BenchmarkRowGroupingLogic.CreateRows(
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
        var rows = global::UI.BenchmarkRowGroupingLogic.CreateRows(
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
        var rows = global::UI.BenchmarkRowGroupingLogic.CreateRows(
            [
                Case(sourceTab.LeftProject,"Scalable.Persisted.Collections","Scalable.Persisted.Collections.List.ListString","Add"),
                Case(sourceTab.LeftProject,"Scalable.Persisted.Collections","Scalable.Persisted.Collections.List.ListInt32","Add"),
                Case(sourceTab.LeftProject,"Scalable.Persisted.Collections","Scalable.Persisted.Collections.List.ListBoolean","Add"),
            ],
            sourceTab);

        Assert.Contains(rows,row => row.BenchmarkName=="<string>.Add");
        Assert.Contains(rows,row => row.BenchmarkName=="<int>.Add");
        Assert.Contains(rows,row => row.BenchmarkName=="<System.Boolean>.Add");
        Assert.All(rows,row => Assert.Equal("Add",row.MethodName));
    }

    [Fact]
    public void CreateRowsUsesScalableDictionaryKeyAndValueTypesInBenchmarkName(){
        var sourceTab = BenchmarkSourceTabs.Get(BenchmarkSourceTab.ScalableTsavoriteDictionary);
        var rows = global::UI.BenchmarkRowGroupingLogic.CreateRows(
            [
                Case(sourceTab.LeftProject,"Scalable.Tsavorite.Collections","Scalable.Tsavorite.Collections.Dictionary.DictionaryStringInt32","Add"),
                Case(sourceTab.LeftProject,"Scalable.Tsavorite.Collections","Scalable.Tsavorite.Collections.Dictionary.DictionaryManagedStructManagedStruct","Add"),
            ],
            sourceTab);

        Assert.Contains(rows,row => row.BenchmarkName=="<string,int>.Add");
        Assert.Contains(rows,row => row.BenchmarkName=="<ManagedStruct,ManagedStruct>.Add");
        Assert.All(rows,row => Assert.Equal("Add",row.MethodName));
    }

    [Fact]
    public void CreateRowsUsesSeamlessPersistedTypeInBenchmarkName(){
        var sourceTab = BenchmarkSourceTabs.Get(BenchmarkSourceTab.SeamlessPersisted);
        var rows = global::UI.BenchmarkRowGroupingLogic.CreateRows(
            [
                Case(sourceTab.LeftProject,"Persisted.Linq.Enumerable.FLinq","Persisted.Linq.Enumerable.FLinqInt32","Aggregate_func"),
                Case(sourceTab.LeftProject,"Persisted.Linq.Enumerable.FLinq","Persisted.Linq.Enumerable.FLinqString","Aggregate_func"),
                Case(sourceTab.RightProject,"Persisted.Linq.Enumerable.Linq","Persisted.Linq.Enumerable.LinqInt32","Aggregate_func"),
                Case(sourceTab.RightProject,"Persisted.Linq.Enumerable.Linq","Persisted.Linq.Enumerable.LinqString","Aggregate_func"),
            ],
            sourceTab);

        Assert.Contains(rows,row => row.BenchmarkName=="<int>.Aggregate_func");
        Assert.Contains(rows,row => row.BenchmarkName=="<string>.Aggregate_func");
        Assert.All(rows,row => Assert.Equal("Aggregate_func",row.MethodName));
    }

    [Fact]
    public void CreateRowsUsesSeamlessTsavoriteTypeInBenchmarkName(){
        var sourceTab = BenchmarkSourceTabs.Get(BenchmarkSourceTab.SeamlessTsavorite);
        var rows = global::UI.BenchmarkRowGroupingLogic.CreateRows(
            [
                Case(sourceTab.LeftProject,"Tsavorite.Linq.Enumerable.FLinq","Tsavorite.Linq.Enumerable.FLinqInt32","All_predicate"),
                Case(sourceTab.LeftProject,"Tsavorite.Linq.Enumerable.FLinq","Tsavorite.Linq.Enumerable.FLinqString","All_predicate"),
                Case(sourceTab.RightProject,"Tsavorite.Linq.Enumerable.Linq","Tsavorite.Linq.Enumerable.LinqInt32","All_predicate"),
                Case(sourceTab.RightProject,"Tsavorite.Linq.Enumerable.Linq","Tsavorite.Linq.Enumerable.LinqString","All_predicate"),
            ],
            sourceTab);

        Assert.Contains(rows,row => row.BenchmarkName=="<int>.All_predicate");
        Assert.Contains(rows,row => row.BenchmarkName=="<string>.All_predicate");
        Assert.All(rows,row => Assert.Equal("All_predicate",row.MethodName));
    }

    [Fact]
    public void CreateRowsUsesPersistedRightAssemblyWhenSeamlessLinqProjectIsShared(){
        var sourceTab = BenchmarkSourceTabs.Get(BenchmarkSourceTab.SeamlessPersisted);
        var otherTab = BenchmarkSourceTabs.Get(BenchmarkSourceTab.SeamlessTsavorite);
        var rows = global::UI.BenchmarkRowGroupingLogic.CreateRows(
            [
                Case(sourceTab.LeftProject,sourceTab.LeftAssemblyName,"Persisted.Linq.Enumerable.FLinqInt32","AggregateBy_keySelector"),
                Case(sourceTab.LeftProject,sourceTab.LeftAssemblyName,"Persisted.Linq.Enumerable.FLinqString","AggregateBy_keySelector"),
                Case(sourceTab.RightProject,sourceTab.RightAssemblyName,"Persisted.Linq.Enumerable.LinqInt32","AggregateBy_keySelector"),
                Case(sourceTab.RightProject,sourceTab.RightAssemblyName,"Persisted.Linq.Enumerable.LinqString","AggregateBy_keySelector"),
                Case(sourceTab.RightProject,otherTab.RightAssemblyName,"Tsavorite.Linq.Enumerable.LinqInt32","AggregateBy_keySelector"),
                Case(sourceTab.RightProject,otherTab.RightAssemblyName,"Tsavorite.Linq.Enumerable.LinqString","AggregateBy_keySelector"),
            ],
            sourceTab);

        Assert.Equal(2,rows.Count);
        Assert.Contains(rows,row => row.BenchmarkName=="<int>.AggregateBy_keySelector");
        Assert.Contains(rows,row => row.BenchmarkName=="<string>.AggregateBy_keySelector");
        Assert.All(rows,row => Assert.Equal(sourceTab.RightAssemblyName,row.CasesByProject[sourceTab.RightProject].AssemblyName));
    }

    [Fact]
    public void CreateRowsUsesTsavoriteRightAssemblyWhenSeamlessLinqProjectIsShared(){
        var sourceTab = BenchmarkSourceTabs.Get(BenchmarkSourceTab.SeamlessTsavorite);
        var otherTab = BenchmarkSourceTabs.Get(BenchmarkSourceTab.SeamlessPersisted);
        var rows = global::UI.BenchmarkRowGroupingLogic.CreateRows(
            [
                Case(sourceTab.LeftProject,sourceTab.LeftAssemblyName,"Tsavorite.Linq.Enumerable.FLinqInt32","AggregateBy_keySelector"),
                Case(sourceTab.LeftProject,sourceTab.LeftAssemblyName,"Tsavorite.Linq.Enumerable.FLinqString","AggregateBy_keySelector"),
                Case(sourceTab.RightProject,otherTab.RightAssemblyName,"Persisted.Linq.Enumerable.LinqInt32","AggregateBy_keySelector"),
                Case(sourceTab.RightProject,otherTab.RightAssemblyName,"Persisted.Linq.Enumerable.LinqString","AggregateBy_keySelector"),
                Case(sourceTab.RightProject,sourceTab.RightAssemblyName,"Tsavorite.Linq.Enumerable.LinqInt32","AggregateBy_keySelector"),
                Case(sourceTab.RightProject,sourceTab.RightAssemblyName,"Tsavorite.Linq.Enumerable.LinqString","AggregateBy_keySelector"),
            ],
            sourceTab);

        Assert.Equal(2,rows.Count);
        Assert.Contains(rows,row => row.BenchmarkName=="<int>.AggregateBy_keySelector");
        Assert.Contains(rows,row => row.BenchmarkName=="<string>.AggregateBy_keySelector");
        Assert.All(rows,row => Assert.Equal(sourceTab.RightAssemblyName,row.CasesByProject[sourceTab.RightProject].AssemblyName));
    }

    [Fact]
    public void CreateRowsUsesStandardMethod1TypeInBenchmarkName(){
        var sourceTab = BenchmarkSourceTabs.Get(BenchmarkSourceTab.SystemArray);
        var rows = global::UI.BenchmarkRowGroupingLogic.CreateRows(
            [
                Case(sourceTab.LeftProject,"DotNet.Linq.Enumerable.FLinq","DotNet.Linq.Enumerable.FLinqInt32","Any"),
                Case(sourceTab.LeftProject,"DotNet.Linq.Enumerable.FLinq","DotNet.Linq.Enumerable.FLinqString","Any"),
                Case(sourceTab.RightProject,"DotNet.Linq.Enumerable.ZLinq","DotNet.Linq.Enumerable.ZLinqInt32","Any"),
                Case(sourceTab.RightProject,"DotNet.Linq.Enumerable.ZLinq","DotNet.Linq.Enumerable.ZLinqString","Any"),
            ],
            sourceTab);

        Assert.Equal(2,rows.Count);
        Assert.Contains(rows,row => row.BenchmarkName=="<int>.Any");
        Assert.Contains(rows,row => row.BenchmarkName=="<string>.Any");
        Assert.All(rows,row => Assert.Equal("Any",row.MethodName));
    }

    [Fact]
    public void CreateRowsKeepsSystemArrayRightProjectAfterAssemblyFiltering(){
        var sourceTab = BenchmarkSourceTabs.Get(BenchmarkSourceTab.SystemArray);
        var rows = global::UI.BenchmarkRowGroupingLogic.CreateRows(
            [
                Case(sourceTab.LeftProject,sourceTab.LeftAssemblyName,"DotNet.Linq.Enumerable.FLinqInt32","Aggregate_func"),
                Case(sourceTab.RightProject,sourceTab.RightAssemblyName,"DotNet.Linq.Enumerable.ZLinqInt32","Aggregate_func"),
            ],
            sourceTab);

        var row = Assert.Single(rows);

        Assert.True(row.CasesByProject.ContainsKey(sourceTab.LeftProject));
        Assert.True(row.CasesByProject.ContainsKey(sourceTab.RightProject));
        Assert.Equal(sourceTab.RightAssemblyName,row.CasesByProject[sourceTab.RightProject].AssemblyName);
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
        Assert.Equal("<int>.Add",row.Benchmark);
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
        Assert.Equal("<int>.Add",row.Benchmark);
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

        Assert.Equal("<string,int>.Add",row.Benchmark);
        Assert.Equal("Add",row.MethodName);
        Assert.Equal("DictionaryStringInt32",row.TypeFullName);
    }

    [Fact]
    public void BuildComparisonRowsUsesSeamlessPersistedType(){
        var sourceTab = BenchmarkSourceTabs.Get(BenchmarkSourceTab.SeamlessPersisted);
        var rows = BenchmarkComparisonFormatter.BuildComparisonRows(
            [
                Report(sourceTab.LeftProject,"Aggregate_func","Persisted.Linq.Enumerable.FLinqInt32",20),
            ],
            sourceTab);

        var row = Assert.Single(rows);

        Assert.Equal("<int>.Aggregate_func",row.Benchmark);
        Assert.Equal("Aggregate_func",row.MethodName);
        Assert.Equal("int",row.TypeFullName);
    }

    [Fact]
    public void BuildComparisonRowsPairsStandardMethod1ProjectsByType(){
        var sourceTab = BenchmarkSourceTabs.Get(BenchmarkSourceTab.SystemArray);
        var rows = BenchmarkComparisonFormatter.BuildComparisonRows(
            [
                Report(sourceTab.LeftProject,"Any","DotNet.Linq.Enumerable.FLinqInt32",10),
                Report(sourceTab.RightProject,"Any","DotNet.Linq.Enumerable.ZLinqInt32",20),
                Report(sourceTab.LeftProject,"Any","DotNet.Linq.Enumerable.FLinqString",30),
                Report(sourceTab.RightProject,"Any","DotNet.Linq.Enumerable.ZLinqString",40),
            ],
            sourceTab);

        Assert.Equal(2,rows.Count);
        var intRow=Assert.Single(rows,row => row.TypeFullName=="int");
        var stringRow=Assert.Single(rows,row => row.TypeFullName=="string");

        Assert.Equal("<int>.Any",intRow.Benchmark);
        Assert.Equal(10,intRow.GetProject(sourceTab.LeftProject).TimeNanoseconds);
        Assert.Equal(20,intRow.GetProject(sourceTab.RightProject).TimeNanoseconds);
        Assert.Equal("<string>.Any",stringRow.Benchmark);
        Assert.Equal(30,stringRow.GetProject(sourceTab.LeftProject).TimeNanoseconds);
        Assert.Equal(40,stringRow.GetProject(sourceTab.RightProject).TimeNanoseconds);
    }

    [Fact]
    public void BuildComparisonRowsMapsLegacyMethod1TypeToInt(){
        var sourceTab = BenchmarkSourceTabs.Get(BenchmarkSourceTab.SystemArray);
        var rows = BenchmarkComparisonFormatter.BuildComparisonRows(
            [
                Report(sourceTab.LeftProject,"Any","DotNet.Linq.Enumerable.FLinq",10),
                Report(sourceTab.RightProject,"Any","DotNet.Linq.Enumerable.ZLinq",20),
            ],
            sourceTab);

        var row=Assert.Single(rows);

        Assert.Equal("<int>.Any",row.Benchmark);
        Assert.Equal("int",row.TypeFullName);
        Assert.Equal(10,row.GetProject(sourceTab.LeftProject).TimeNanoseconds);
        Assert.Equal(20,row.GetProject(sourceTab.RightProject).TimeNanoseconds);
    }

    [Fact]
    public void ComparisonRowMatchRequiresArgumentKey(){
        var sourceTab = BenchmarkSourceTabs.Get(BenchmarkSourceTab.ScalableTsavoriteSet);
        var arguments = new BenchmarkArgument[] {
            new("Count","131072"),
            new("Index","131072"),
        };
        var cases = new BenchmarkComparisonRowMatchCase[] {
            new("Scalable.Tsavorite.Collections.Set.SetInt32",BenchmarkArgumentFormatting.CreateKey(arguments)),
        };
        var exact = new BenchmarkComparisonRow(
            "<int>.Add",
            sourceTab,
            ProjectMetrics.FromMeasurement(10,0),
            ProjectMetrics.Missing) {
            MethodName="Add",
            TypeFullName="SetInt32",
            ArgumentKey=BenchmarkArgumentFormatting.CreateKey(arguments),
            Arguments=arguments,
        };
        var differentArgument = exact with {
            ArgumentKey="Count=262144,Index=131072",
            Arguments=[
                new BenchmarkArgument("Count","262144"),
                new BenchmarkArgument("Index","131072"),
            ],
        };
        var emptyArgument = exact with {
            ArgumentKey=string.Empty,
            Arguments=[],
        };

        Assert.True(BenchmarkComparisonRowMatchLogic.IsMatch("Add","<int>.Add",sourceTab.Tab,cases,exact));
        Assert.False(BenchmarkComparisonRowMatchLogic.IsMatch("Add","<int>.Add",sourceTab.Tab,cases,differentArgument));
        Assert.False(BenchmarkComparisonRowMatchLogic.IsMatch("Add","<int>.Add",sourceTab.Tab,cases,emptyArgument));
    }

    [Fact]
    public void ComparisonRowMatchRequiresTypeForSeamlessPersistedAndTsavorite(){
        {
            var sourceTab = BenchmarkSourceTabs.Get(BenchmarkSourceTab.SeamlessPersisted);
            var cases = new BenchmarkComparisonRowMatchCase[] {
                new("Persisted.Linq.Enumerable.FLinqInt32",string.Empty),
            };
            var exact = new BenchmarkComparisonRow(
                "<int>.Aggregate_func",
                sourceTab,
                ProjectMetrics.FromMeasurement(10,0),
                ProjectMetrics.Missing) {
                MethodName="Aggregate_func",
                TypeFullName="int",
            };
            var otherType = exact with {
                Benchmark="<string>.Aggregate_func",
                TypeFullName="string",
            };

            Assert.True(BenchmarkComparisonRowMatchLogic.IsMatch("Aggregate_func","<int>.Aggregate_func",sourceTab.Tab,cases,exact));
            Assert.False(BenchmarkComparisonRowMatchLogic.IsMatch("Aggregate_func","<int>.Aggregate_func",sourceTab.Tab,cases,otherType));
        }
        {
            var sourceTab = BenchmarkSourceTabs.Get(BenchmarkSourceTab.SeamlessTsavorite);
            var cases = new BenchmarkComparisonRowMatchCase[] {
                new("Tsavorite.Linq.Enumerable.FLinqInt32",string.Empty),
            };
            var exact = new BenchmarkComparisonRow(
                "<int>.All_predicate",
                sourceTab,
                ProjectMetrics.FromMeasurement(10,0),
                ProjectMetrics.Missing) {
                MethodName="All_predicate",
                TypeFullName="int",
            };
            var otherType = exact with {
                Benchmark="<string>.All_predicate",
                TypeFullName="string",
            };

            Assert.True(BenchmarkComparisonRowMatchLogic.IsMatch("All_predicate","<int>.All_predicate",sourceTab.Tab,cases,exact));
            Assert.False(BenchmarkComparisonRowMatchLogic.IsMatch("All_predicate","<int>.All_predicate",sourceTab.Tab,cases,otherType));
        }
    }

    [Fact]
    public void DefaultComparisonRowGetProjectReturnsMissing(){
        var row = default(BenchmarkComparisonRow);

        var metrics = row.GetProject(BenchmarkSourceTabs.Default.LeftProject);

        Assert.False(metrics.HasMeasuredValue);
    }

    [Fact]
    public void CreateGenericTypeTextReadsTypedBenchmarkClassSuffix(){
        Assert.Equal("int",ScalableBenchmarkNameFormatter.CreateBenchmarkTypeText("Persisted.Linq.Enumerable.FLinqInt32"));
        Assert.Equal("string",ScalableBenchmarkNameFormatter.CreateBenchmarkTypeText("Persisted.Linq.Enumerable.LinqString"));
        Assert.Equal("int",ScalableBenchmarkNameFormatter.CreateBenchmarkTypeText("Tsavorite.Linq.Enumerable.FLinqInt32"));
        Assert.Equal("string",ScalableBenchmarkNameFormatter.CreateBenchmarkTypeText("Tsavorite.Linq.Enumerable.LinqString"));
        Assert.Equal("int",ScalableBenchmarkNameFormatter.CreateBenchmarkTypeText("DotNet.Linq.Enumerable.FLinqInt32"));
        Assert.Equal("string",ScalableBenchmarkNameFormatter.CreateBenchmarkTypeText("DotNet.Linq.Enumerable.ZLinqString"));
        Assert.Equal(string.Empty,ScalableBenchmarkNameFormatter.CreateBenchmarkTypeText("System.Array.FLinq"));
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
            mean.ToString(global::System.Globalization.CultureInfo.InvariantCulture),
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




