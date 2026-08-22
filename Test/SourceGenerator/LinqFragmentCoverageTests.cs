using System.Collections;
using System.Reflection;

using Seamless.FLinq.Generator;
namespace Test.SourceGenerator;

public sealed class LinqFragmentCoverageTests {
    private static readonly Assembly GeneratorAssembly=typeof(FLinqIncrementalGenerator).Assembly;
    private static readonly Type RegistryType=GeneratorAssembly.GetType("Seamless.FLinq.Generator.OperatorRegistry",throwOnError:true)!;

    [Fact, Trait("Category","Unit")]
    public void SystemLinqEnumerablePublicMethodsHaveRegistryCoverage() {
        var methodNames = new global::System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        foreach(var method in typeof(global::System.Linq.Enumerable).GetMethods(BindingFlags.Public|BindingFlags.Static)) {
            if(!method.IsSpecialName) {
                methodNames.Add(method.Name);
            }
        }

        var isKnownMethod=RegistryType.GetMethod("IsKnownEnumerableMethodName",BindingFlags.Static|BindingFlags.NonPublic)!;
        var unsupported = methodNames
            .Where(methodName => !(bool)isKnownMethod.Invoke(null,new object?[] { methodName })!)
            .OrderBy(static methodName => methodName,StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(unsupported);
    }

    [Fact, Trait("Category","Unit")]
    public void RegistryContractHasNoDuplicatePriorityFactoryCoverageOrCapabilityErrors() {
        var validationErrors=(IEnumerable)RegistryType.GetProperty("ValidationErrors",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;
        Assert.Empty(validationErrors.Cast<string>());

        var descriptors=((IEnumerable)RegistryType.GetProperty("Descriptors",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!).Cast<object>().ToArray();
        var keys=descriptors.Select(static descriptor => GetPropertyValue(descriptor,"Key")!.ToString()).ToArray();
        Assert.Equal(descriptors.Length,keys.Distinct(StringComparer.Ordinal).Count());
        foreach(var group in descriptors.GroupBy(static descriptor => GetPropertyValue(descriptor,"Key")!.GetType().GetProperty("MethodName")!.GetValue(GetPropertyValue(descriptor,"Key"))!.ToString(),StringComparer.Ordinal)) {
            Assert.Equal(group.Count(),group.Select(static descriptor => (int)GetPropertyValue(descriptor,"Priority")!).Distinct().Count());
        }
        foreach(var descriptor in descriptors) {
            var fragmentType=(Type)GetPropertyValue(descriptor,"FragmentType")!;
            var argumentsType=(Type)GetPropertyValue(descriptor,"ArgumentsType")!;
            Assert.True(GeneratorAssembly.GetType("Seamless.FLinq.Fragment",throwOnError:true)!.IsAssignableFrom(fragmentType),GetPropertyValue(descriptor,"Key")!.ToString());
            Assert.True(GeneratorAssembly.GetType("Seamless.FLinq.FragmentArguments",throwOnError:true)!.IsAssignableFrom(argumentsType),GetPropertyValue(descriptor,"Key")!.ToString());
            if(GetPropertyValue(descriptor,"Capabilities")!.ToString()!.Contains("ProviderNativeIterate",StringComparison.Ordinal)) {
                Assert.Equal("Fragment",GetPropertyValue(descriptor,"Coverage")!.ToString());
            }
        }
    }

    [Fact, Trait("Category","Unit")]
    public void RegistryExplicitlyClassifiesExcludedPublicApis() {
        var descriptors=((IEnumerable)RegistryType.GetProperty("Descriptors",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!).Cast<object>();
        var excluded = descriptors
            .Where(static descriptor => GetPropertyValue(descriptor,"Coverage")!.ToString()=="Excluded")
            .Select(static descriptor => GetPropertyValue(descriptor,"Key")!.GetType().GetProperty("MethodName")!.GetValue(GetPropertyValue(descriptor,"Key"))!.ToString())
            .OrderBy(static methodName => methodName,StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { "Order","OrderDescending","TryGetNonEnumeratedCount" },excluded);
    }
    [Fact, Trait("Category","Unit")]
    public void CoverageManifestDeclaresExceptionAndShapeForEveryPublicEnumerableMethod() {
        var manifest=(IEnumerable)RegistryType.GetProperty("CoverageManifest",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;
        var entries=manifest.Cast<object>().ToArray();
        var methodProperty=entries[0].GetType().GetProperty("MethodName",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!;
        var exceptionProperty=entries[0].GetType().GetProperty("ExceptionCoverage",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!;
        var names=entries.Select(entry => (string)methodProperty.GetValue(entry)!).ToArray();
        Assert.Equal(names.Length,names.Distinct(StringComparer.Ordinal).Count());
        var excluded=new global::System.Collections.Generic.HashSet<string>(new[] { "Order","OrderDescending","TryGetNonEnumeratedCount" },StringComparer.Ordinal);
        var publicNames=typeof(global::System.Linq.Enumerable).GetMethods(BindingFlags.Public|BindingFlags.Static)
            .Where(static method => !method.IsSpecialName).Select(static method => method.Name).ToHashSet(StringComparer.Ordinal);
        foreach(var publicName in publicNames) {
            Assert.Contains(publicName,names);
        }
        foreach(var entry in entries) {
            var name=(string)methodProperty.GetValue(entry)!;
            var exception=exceptionProperty.GetValue(entry)!.ToString();
            Assert.Equal(excluded.Contains(name) ? "ExplicitlyExcluded" : "StandardLinq",exception);
        }
    }

    private static object? GetPropertyValue(object value,string propertyName) =>
        value.GetType().GetProperty(propertyName,BindingFlags.Instance|BindingFlags.Public)!.GetValue(value);
}