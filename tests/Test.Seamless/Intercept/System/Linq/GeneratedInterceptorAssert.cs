using System.Linq;

namespace Test.Seamless.Intercept.System.Linq;

internal static class GeneratedInterceptorAssert {
    internal static string SourceFor<TMarker>(string methodName) {
        var generatedRoot = GeneratedSourceRoot<TMarker>();
        Assert.True(Directory.Exists(generatedRoot),$"Generated source directory does not exist: {generatedRoot}");

        var matches = Directory
            .EnumerateFiles(generatedRoot,"Seamless.SourceGenerator.LinqInterceptors.g.cs",SearchOption.AllDirectories)
            .Select(File.ReadAllText)
            .Where(static text => text.Contains("internal static partial class LinqInvocationInterceptors",StringComparison.Ordinal))
            .Where(text => text.Contains(methodName,StringComparison.Ordinal))
            .ToArray();

        Assert.Single(matches);
        return matches[0];
    }

    internal static void ContainsMethod<TMarker>(string methodName) {
        var generated = SourceFor<TMarker>(methodName);
        Assert.Contains(methodName,generated);
    }

    private static string GeneratedSourceRoot<TMarker>() {
        var assemblyDirectory = Path.GetDirectoryName(typeof(TMarker).Assembly.Location)
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
