using System.Linq;
using Seamless.Linq;

namespace Test.Seamless.Diagnostics;

public sealed class LambdaStrategySourceTests {
    [Fact, Trait("Category","Unit")]
    public void Explicit_static_delegate_is_emitted_as_static_struct_strategy() {
        Assert.Equal(new[] { 2,3,4 },ExplicitStaticDelegateProbe());

        var section = GeneratedInterceptorSection(nameof(ExplicitStaticDelegateProbe));

        AssertStaticSelectStrategy(section,"p+1");
    }

    [Fact, Trait("Category","Unit")]
    public void Captureless_nonstatic_delegate_is_emitted_as_static_struct_strategy() {
        Assert.Equal(new[] { 2,3,4 },CapturelessNonstaticDelegateProbe());

        var section = GeneratedInterceptorSection(nameof(CapturelessNonstaticDelegateProbe));

        AssertStaticSelectStrategy(section,"p+1");
    }

    [Fact, Trait("Category","Unit")]
    public void Capturing_delegate_is_not_emitted_as_static_struct_strategy() {
        Assert.Equal(new[] { 4,5,6 },CapturingDelegateProbe());

        var section = GeneratedInterceptorSection(nameof(CapturingDelegateProbe));

        Assert.Contains("TEnum __e0, int offset) : global::Seamless.Linq.Carrier.IValueEnumerator",section);
        Assert.Contains("current = this.Current = __it0+offset;",section);
    }

    private static List<int> ExplicitStaticDelegateProbe() =>
        [..new[] { 1,2,3 }.Select(static p => p+1)];

    private static List<int> CapturelessNonstaticDelegateProbe() =>
        [..new[] { 1,2,3 }.Select(p => p+1)];

    private static List<int> CapturingDelegateProbe() {
        var offset = 3;
        return [..new[] { 1,2,3 }.Select(p => p+offset)];
    }

    private static void AssertStaticSelectStrategy(string section,string expressionText) {
        var inlined = expressionText.Replace("p","__it0");
        Assert.Contains($"current = this.Current = {inlined};",section);
        Assert.Contains("(TEnum __e0) : global::Seamless.Linq.Carrier.IValueEnumerator",section);
    }

    private static string GeneratedInterceptorSection(string containingMethodName) {
        var source = GeneratedInterceptorSource();
        var markerIndex = source.IndexOf(containingMethodName,StringComparison.Ordinal);
        Assert.True(markerIndex>=0,$"Generated interceptor source did not contain '{containingMethodName}'.");

        var nextSummaryIndex = source.IndexOf("        /// <summary>",markerIndex+containingMethodName.Length,StringComparison.Ordinal);
        return nextSummaryIndex<0 ? source[markerIndex..] : source[markerIndex..nextSummaryIndex];
    }

    private static string GeneratedInterceptorSource() {
        var generatedRoot = GeneratedSourceRoot();
        Assert.True(Directory.Exists(generatedRoot),$"Generated source directory does not exist: {generatedRoot}");

        var matches = Directory
            .EnumerateFiles(generatedRoot,"*.g.cs",SearchOption.AllDirectories)
            .Select(File.ReadAllText)
            .Where(static text => text.Contains("namespace Intercept.",StringComparison.Ordinal)
                || text.Contains("namespace Intercept {",StringComparison.Ordinal))
            .Where(static text => text.Contains(nameof(ExplicitStaticDelegateProbe),StringComparison.Ordinal))
            .ToArray();

        Assert.Single(matches);
        return matches[0];
    }

    private static string GeneratedSourceRoot() {
        var assemblyDirectory = Path.GetDirectoryName(typeof(LambdaStrategySourceTests).Assembly.Location)
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
