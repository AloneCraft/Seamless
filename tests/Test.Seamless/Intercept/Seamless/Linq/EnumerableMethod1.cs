using System;
using System.IO;
using System.Linq;

using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.Seamless.Linq;

public sealed class EnumerableMethod1 {
    [Fact, Trait("Category","Intercept")]
    public void Aggregate_func_StaticDelegate_Intercepted() {
        var source = new[] { 1 };

        Assert.Equal(
            SystemEnumerable.Aggregate(source,static (left,right) => left+right),
            source.Aggregate(static (left,right) => left+right));

        var generated = GeneratedInterceptorSource();
        Assert.Contains(nameof(Aggregate_func_StaticDelegate_Intercepted),generated);
        Assert.Contains("private struct Struct1",generated);
        Assert.Contains("public static global::System.Int32 func(global::System.Int32 left, global::System.Int32 right) => left+right;",generated);
        Assert.Contains("using var e0=source.GetEnumerator();",generated);
        Assert.Contains("goto Start0;",generated);
        Assert.Contains("Head0:",generated);
        Assert.Contains("Start0:;",generated);
        Assert.Contains(".func(result",generated);
        Assert.DoesNotContain("while(",generated,StringComparison.Ordinal);
        Assert.DoesNotContain("Aggregate_seed_func",generated,StringComparison.Ordinal);
        Assert.DoesNotContain("return global::Seamless.Linq.Enumerable.Aggregate(source,func);",generated,StringComparison.Ordinal);
    }

    private static string GeneratedInterceptorSource() {
        var generatedRoot = GeneratedSourceRoot();
        Assert.True(Directory.Exists(generatedRoot),$"Generated source directory does not exist: {generatedRoot}");

        var matches = Directory
            .EnumerateFiles(generatedRoot,"*.g.cs",SearchOption.AllDirectories)
            .Select(File.ReadAllText)
            .Where(static text => text.Contains("internal static partial class LinqInvocationInterceptors",StringComparison.Ordinal))
            .Where(static text => text.Contains(nameof(Aggregate_func_StaticDelegate_Intercepted),StringComparison.Ordinal))
            .ToArray();

        Assert.Single(matches);
        return matches[0];
    }

    private static string GeneratedSourceRoot() {
        var assemblyDirectory = Path.GetDirectoryName(typeof(EnumerableMethod1).Assembly.Location)
            ?? throw new InvalidOperationException("Could not resolve test assembly directory.");
        var targetFrameworkDirectory = new DirectoryInfo(assemblyDirectory);
        var configurationDirectory = targetFrameworkDirectory.Parent
            ?? throw new InvalidOperationException("Could not resolve configuration directory.");
        var projectDirectory = configurationDirectory.Parent?.Parent
            ?? throw new InvalidOperationException("Could not resolve project directory.");

        return Path.Combine(
            projectDirectory.FullName,
            "obj",
            configurationDirectory.Name,
            targetFrameworkDirectory.Name,
            "generated");
    }
}
