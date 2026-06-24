using System.Reflection;
using BenchmarkDotNet.Attributes;

namespace Benchmark;

public sealed record BenchmarkProject(string DisplayName,string AssemblyName);

public sealed record BenchmarkCase(
    string Project,
    string AssemblyName,
    string TypeFullName,
    string MethodName) {
    public string DisplayName => $"{Project} / {MethodName}";
}

public static class BenchmarkCatalog {
    private static readonly BenchmarkProject[] projects = [
        new("System FLinq","Benchmark.DotNet.Linq.Enumerable.FLinq"),
        new("System ZLinq","Benchmark.DotNet.Linq.Enumerable.ZLinq"),
        new("Tsavorite LINQ","Benchmark.Tsavorite.Linq.Enumerable.Linq"),
        new("Tsavorite FLinq","Benchmark.Tsavorite.Linq.Enumerable.FLinq"),
        new("Persisted LINQ","Benchmark.Persisted.Linq.Enumerable.Linq"),
        new("Persisted FLinq","Benchmark.Persisted.Linq.Enumerable.FLinq"),
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
