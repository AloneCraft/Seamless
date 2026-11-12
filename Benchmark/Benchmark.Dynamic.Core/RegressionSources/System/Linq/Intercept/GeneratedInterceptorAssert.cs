using System.Text.RegularExpressions;
namespace Test.System.Linq.Intercept;
internal static class GeneratedInterceptorAssert{
    internal static string SourceFor<TMarker>(string methodName){
        var marker=typeof(TMarker);
        var matches=global::Benchmark.Dynamic.Core.RegressionGeneratedSources.Current.Where(text=>
            text.Contains($"namespace Intercept.{marker.Namespace}",StringComparison.Ordinal)
            &&Regex.IsMatch(text,$@"(?<![A-Za-z0-9_])class {Regex.Escape(marker.Name)}(?![A-Za-z0-9_])")
            &&text.Contains($"<c>{methodName}()</c>",StringComparison.Ordinal)).ToArray();
        return Assert.Single(matches);
    }
    internal static void ContainsMethod<TMarker>(string methodName)=>Assert.Contains(methodName,SourceFor<TMarker>(methodName));
}
