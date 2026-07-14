using System.Reflection;

namespace Test.SourceGenerator;

public sealed class LinqFragmentCoverageTests {
    [Fact, Trait("Category","Unit")]
    public void SystemLinqEnumerablePublicMethodsHaveFragmentOrPipelineSupport() {
        var assembly = typeof(global::Seamless.FLinq.FLinqIncrementalGenerator).Assembly;
        var fragmentTypeNames = new global::System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        foreach(var type in assembly.GetTypes()) {
            if(type.Namespace=="Seamless.FLinq.Linq.Fragments") {
                fragmentTypeNames.Add(type.Name);
            }
        }
        var supportedByPipeline = new global::System.Collections.Generic.HashSet<string>(StringComparer.Ordinal) {
            "Order",
            "OrderDescending",
            "TryGetNonEnumeratedCount",
        };
        var methodNames = new global::System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        foreach(var method in typeof(global::System.Linq.Enumerable).GetMethods(BindingFlags.Public|BindingFlags.Static)) {
            if(!method.IsSpecialName) {
                methodNames.Add(method.Name);
            }
        }
        var unsupported = new global::System.Collections.Generic.List<string>();
        foreach(var methodName in methodNames) {
            if(!HasFragment(fragmentTypeNames,methodName)&&!supportedByPipeline.Contains(methodName)) {
                unsupported.Add(methodName);
            }
        }
        unsupported.Sort(StringComparer.Ordinal);

        Assert.Empty(unsupported);
    }

    private static bool HasFragment(global::System.Collections.Generic.IEnumerable<string> fragmentTypeNames,string methodName) {
        foreach(var typeName in fragmentTypeNames) {
            if(typeName==methodName||typeName==methodName+"Root"||typeName.StartsWith(methodName+"_",StringComparison.Ordinal)) {
                return true;
            }
        }
        return false;
    }
}
