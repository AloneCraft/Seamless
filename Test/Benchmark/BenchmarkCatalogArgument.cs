using System.Reflection;
using UI;

namespace Test.Benchmark;

[global::Seamless.FLinq.LinqIntercept(false)]
public sealed class BenchmarkCatalogArgument {
    [Fact]
    public void CreateBenchmarkTypeText_UsesNearestGenericParentArguments(){
        var rangeAssembly=Assembly.Load(new AssemblyName("Benchmark.System.Linq.Enumerable.Range.FLinq"));
        var chainType=rangeAssembly.GetType("Seamless.Linq.Shared.Range_start_count+OrderBy_keySelector_chain",throwOnError:true)!;
        var rangeType=rangeAssembly.GetType("Benchmark.System.Linq.Enumerable.Range.FLinqInt32",throwOnError:true)!;

        Assert.Equal("int",global::UI.BenchmarkCatalog.CreateBenchmarkTypeText(chainType));
        Assert.Equal("int",global::UI.BenchmarkCatalog.CreateBenchmarkTypeText(rangeType));
        Assert.Equal("int,string",global::UI.BenchmarkCatalog.CreateBenchmarkTypeText(typeof(MultipleArgumentBenchmark)));
    }

    [Fact]
    public void Load_StoresParentTypeArgumentsForRangeThenByCases(){
        var sourceTab=global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SystemRange);

        var cases=global::UI.BenchmarkCatalog.Load()
            .Where(item => string.Equals(item.Project,sourceTab.LeftProject,StringComparison.OrdinalIgnoreCase)
                &&item.MethodName is "ThenBy_keySelector" or "ThenByDescending_keySelector")
            .ToArray();

        Assert.Equal(2,cases.Length);
        Assert.All(cases,static item => Assert.Equal("int",item.BenchmarkTypeText));
    }

    [Fact]
    public void GetBenchmarkArguments_UsesScalableTsavoriteArgumentNames() {
        var method = LoadMethod("Benchmark.Scalable.Tsavorite.Collections","Benchmark.Scalable.Tsavorite.Collections.Set.SetInt32","Add");
        var arguments = GetArgumentSets(method).First();

        Assert.Equal(
            ["Count","Index","Page","Segment","Memory","Fraction"],
            arguments.Select(static argument => argument.Name).ToArray());
        Assert.Equal(
            ["65536","131072","4096","4096","8388608","1"],
            arguments.Select(static argument => argument.Value).ToArray());
    }

    [Fact]
    public void GetBenchmarkArguments_UsesScalablePersistedArgumentNames() {
        var method = LoadMethod("Benchmark.Scalable.Persisted.Collections","Benchmark.Scalable.Persisted.Collections.Set.SetInt32","Add");
        var arguments = GetArgumentSets(method).First();

        Assert.Equal(
            ["Count","Initial","Data"],
            arguments.Select(static argument => argument.Name).ToArray());
        Assert.Equal(
            ["65536","1","4096"],
            arguments.Select(static argument => argument.Value).ToArray());
    }

    [Fact]
    public void BuildComparisonRows_IncludesScalableTsavoriteArguments() {
        var sourceTab = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.ScalableTsavoriteSet);
        var arguments = GetArgumentSets(LoadMethod("Benchmark.Scalable.Tsavorite.Collections","Benchmark.Scalable.Tsavorite.Collections.Set.SetInt32","Add")).First();
        var rows = global::UI.BenchmarkComparisonFormatter.BuildComparisonRows(
            [
                Report(sourceTab.LeftProject,"Add","Benchmark.Scalable.Tsavorite.Collections.Set.SetInt32",arguments),
            ],
            sourceTab);

        var row = Assert.Single(rows);

        Assert.Equal("65536",global::UI.BenchmarkArgumentFormatting.GetValue(row.Arguments,"Count"));
        Assert.Equal("131072",global::UI.BenchmarkArgumentFormatting.GetValue(row.Arguments,"Index"));
        Assert.Equal("4096",global::UI.BenchmarkArgumentFormatting.GetValue(row.Arguments,"Page"));
        Assert.Equal("4096",global::UI.BenchmarkArgumentFormatting.GetValue(row.Arguments,"Segment"));
        Assert.Equal("8388608",global::UI.BenchmarkArgumentFormatting.GetValue(row.Arguments,"Memory"));
        Assert.Equal("1",global::UI.BenchmarkArgumentFormatting.GetValue(row.Arguments,"Fraction"));
    }

    [Fact]
    public void BuildComparisonRows_IncludesScalablePersistedArgumentsOnly() {
        var sourceTab = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.ScalablePersistedSet);
        var arguments = GetArgumentSets(LoadMethod("Benchmark.Scalable.Persisted.Collections","Benchmark.Scalable.Persisted.Collections.Set.SetInt32","Add")).First();
        var rows = global::UI.BenchmarkComparisonFormatter.BuildComparisonRows(
            [
                Report(sourceTab.LeftProject,"Add","Benchmark.Scalable.Persisted.Collections.Set.SetInt32",arguments),
            ],
            sourceTab);

        var row = Assert.Single(rows);

        Assert.Equal("65536",global::UI.BenchmarkArgumentFormatting.GetValue(row.Arguments,"Count"));
        Assert.Equal("1",global::UI.BenchmarkArgumentFormatting.GetValue(row.Arguments,"Initial"));
        Assert.Equal("4096",global::UI.BenchmarkArgumentFormatting.GetValue(row.Arguments,"Data"));
        Assert.DoesNotContain(row.Arguments,argument => string.Equals(argument.Name,"Index",StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(row.Arguments,argument => string.Equals(argument.Name,"Page",StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(row.Arguments,argument => string.Equals(argument.Name,"Segment",StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(row.Arguments,argument => string.Equals(argument.Name,"Memory",StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(row.Arguments,argument => string.Equals(argument.Name,"Fraction",StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetDisplayArgumentValue_KeepsScalablePersistedSpecificColumns() {
        var sourceTab = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.ScalablePersistedSet);
        var arguments = GetArgumentSets(LoadMethod("Benchmark.Scalable.Persisted.Collections","Benchmark.Scalable.Persisted.Collections.Set.SetInt32","Add")).First();

        Assert.Equal("65536",global::UI.ScalableBenchmarkNameFormatter.GetDisplayArgumentValue(sourceTab,arguments,global::UI.ScalableBenchmarkArgumentColumn.Count));
        Assert.Equal(string.Empty,global::UI.ScalableBenchmarkNameFormatter.GetDisplayArgumentValue(sourceTab,arguments,global::UI.ScalableBenchmarkArgumentColumn.Index));
        Assert.Equal(string.Empty,global::UI.ScalableBenchmarkNameFormatter.GetDisplayArgumentValue(sourceTab,arguments,global::UI.ScalableBenchmarkArgumentColumn.Page));
        Assert.Equal(string.Empty,global::UI.ScalableBenchmarkNameFormatter.GetDisplayArgumentValue(sourceTab,arguments,global::UI.ScalableBenchmarkArgumentColumn.Segment));
        Assert.Equal(string.Empty,global::UI.ScalableBenchmarkNameFormatter.GetDisplayArgumentValue(sourceTab,arguments,global::UI.ScalableBenchmarkArgumentColumn.Memory));
        Assert.Equal(string.Empty,global::UI.ScalableBenchmarkNameFormatter.GetDisplayArgumentValue(sourceTab,arguments,global::UI.ScalableBenchmarkArgumentColumn.Fraction));
        Assert.Equal("1",global::UI.ScalableBenchmarkNameFormatter.GetDisplayArgumentValue(sourceTab,arguments,global::UI.ScalableBenchmarkArgumentColumn.Initial));
        Assert.Equal("4096",global::UI.ScalableBenchmarkNameFormatter.GetDisplayArgumentValue(sourceTab,arguments,global::UI.ScalableBenchmarkArgumentColumn.Data));
    }

    private static MethodInfo LoadMethod(string assemblyName,string typeFullName,string methodName) =>
        Assembly.Load(new AssemblyName(assemblyName))
            .GetType(typeFullName,throwOnError: true)!
            .GetMethod(methodName,BindingFlags.Public|BindingFlags.Instance)!
        ??throw new MissingMethodException(typeFullName,methodName);

    private static IReadOnlyList<IReadOnlyList<global::UI.BenchmarkArgument>> GetArgumentSets(MethodInfo method) {
        var getBenchmarkArguments = typeof(global::UI.BenchmarkCatalog).GetMethod("GetBenchmarkArguments",BindingFlags.Static|BindingFlags.NonPublic)
            ??throw new MissingMethodException(nameof(global::UI.BenchmarkCatalog),"GetBenchmarkArguments");
        return ((IEnumerable<IReadOnlyList<global::UI.BenchmarkArgument>>)getBenchmarkArguments.Invoke(null,[method])!).ToArray();
    }

    private static global::UI.ReportRow Report(string project,string method,string type,IReadOnlyList<global::UI.BenchmarkArgument> arguments) =>
        new(
            project,
            method,
            type,
            method,
            "Completed",
            "1",
            string.Empty,
            string.Empty,
            "0",
            string.Empty,
            string.Empty,
            1,
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
            0,
            global::UI.BenchmarkArgumentFormatting.CreateKey(arguments),
            arguments);

    private class MultipleArgumentParent<TLeft,TRight>;
    private sealed class MultipleArgumentBenchmark:MultipleArgumentParent<int,string>;
}
