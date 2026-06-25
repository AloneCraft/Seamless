using System.Reflection;
using Benchmark.Worker;
using BenchmarkDotNet.Attributes;

namespace Benchmark;

public sealed record BenchmarkProject(string DisplayName,string AssemblyName);

public static class BenchmarkCatalog {
    private static readonly BenchmarkProject[] projects = [
        new("SystemFLinq","Benchmark.DotNet.Linq.Enumerable.FLinq"),
        new("SystemZLinq","Benchmark.DotNet.Linq.Enumerable.ZLinq"),
        new("TsavoriteLinq","Benchmark.Tsavorite.Linq.Enumerable.Linq"),
        new("TsavoriteFLinq","Benchmark.Tsavorite.Linq.Enumerable.FLinq"),
        new("PersistedLinq","Benchmark.Persisted.Linq.Enumerable.Linq"),
        new("PersistedFLinq","Benchmark.Persisted.Linq.Enumerable.FLinq"),
        new("Persisted Collection","Benchmark.Persisted.Collection"),
        new("Tsavorite Collection","Benchmark.Tsavorite.Collection"),
    ];

    public static IReadOnlyList<BenchmarkCase> Load() {
        var cases = new List<BenchmarkCase>();
        foreach(var project in projects) {
            var assembly = Assembly.Load(new AssemblyName(project.AssemblyName));
            foreach(var type in assembly.GetTypes().Where(IsConcreteBenchmarkType).OrderBy(static type => type.FullName,StringComparer.Ordinal)) {
                foreach(var method in type.GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static).Where(HasBenchmarkAttribute).OrderBy(static method => method.Name,StringComparer.Ordinal)) {
                    cases.Add(new BenchmarkCase(project.DisplayName,project.AssemblyName,type.FullName!,method.Name));
                }
            }
        }

        return cases;
    }

    private static bool IsConcreteBenchmarkType(Type type) =>
        type is { IsAbstract: false, ContainsGenericParameters: false }&&type.GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static).Any(HasBenchmarkAttribute);

    private static bool HasBenchmarkAttribute(MethodInfo method) =>
        method.GetCustomAttribute<BenchmarkAttribute>() is not null;
}
