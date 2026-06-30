using System.Reflection;
using UI;

namespace Test.Benchmark;

[global::Seamless.FLinq.LinqIntercept(false)]
public sealed class BenchmarkCatalogArgumentTests {
    [Fact]
    public void GetBenchmarkArguments_UsesScalableTsavoriteArgumentNames() {
        var method = LoadMethod("Scalable.Tsavorite.Collections","Scalable.Tsavorite.Collections.Set.SetInt32","Add");
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
        var method = LoadMethod("Scalable.Persisted.Collections","Scalable.Persisted.Collections.Set.SetInt32","Add");
        var arguments = GetArgumentSets(method).First();

        Assert.Equal(
            ["Count","Initial","Data"],
            arguments.Select(static argument => argument.Name).ToArray());
        Assert.Equal(
            ["65536","1","4096"],
            arguments.Select(static argument => argument.Value).ToArray());
    }

    [Fact]
    public void WriteCsv_IncludesScalableTsavoriteArgumentColumns() {
        var sourceTab = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.ScalableTsavoriteSet);
        var arguments = GetArgumentSets(LoadMethod("Scalable.Tsavorite.Collections","Scalable.Tsavorite.Collections.Set.SetInt32","Add")).First();
        var rows = global::UI.BenchmarkComparisonFormatter.BuildComparisonRows(
            [
                Report(sourceTab.LeftProject,"Add","Scalable.Tsavorite.Collections.Set.SetInt32",arguments),
            ],
            sourceTab);

        var csv = global::UI.BenchmarkComparisonFormatter.WriteCsv(rows,sourceTab);

        Assert.Contains(",65536,131072,4096,4096,8388608,1,,",csv,StringComparison.Ordinal);
    }

    [Fact]
    public void GetDisplayArgumentValue_FillsScalablePersistedStorageColumns() {
        var sourceTab = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.ScalablePersistedSet);
        var arguments = GetArgumentSets(LoadMethod("Scalable.Persisted.Collections","Scalable.Persisted.Collections.Set.SetInt32","Add")).First();

        Assert.Equal("65536",global::UI.ScalableBenchmarkNameFormatter.GetDisplayArgumentValue(sourceTab,arguments,global::UI.ScalableBenchmarkArgumentColumn.Count));
        Assert.Equal("64",global::UI.ScalableBenchmarkNameFormatter.GetDisplayArgumentValue(sourceTab,arguments,global::UI.ScalableBenchmarkArgumentColumn.Index));
        Assert.Equal("4096",global::UI.ScalableBenchmarkNameFormatter.GetDisplayArgumentValue(sourceTab,arguments,global::UI.ScalableBenchmarkArgumentColumn.Page));
        Assert.Equal("8192",global::UI.ScalableBenchmarkNameFormatter.GetDisplayArgumentValue(sourceTab,arguments,global::UI.ScalableBenchmarkArgumentColumn.Segment));
        Assert.Equal("8388608",global::UI.ScalableBenchmarkNameFormatter.GetDisplayArgumentValue(sourceTab,arguments,global::UI.ScalableBenchmarkArgumentColumn.Memory));
        Assert.Equal("0.5",global::UI.ScalableBenchmarkNameFormatter.GetDisplayArgumentValue(sourceTab,arguments,global::UI.ScalableBenchmarkArgumentColumn.Fraction));
        Assert.Equal("1",global::UI.ScalableBenchmarkNameFormatter.GetDisplayArgumentValue(sourceTab,arguments,global::UI.ScalableBenchmarkArgumentColumn.Initial));
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
}
