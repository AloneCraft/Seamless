namespace Seamless.FLinq.Generator;

internal static class UnorderedSourceSafety {
    public static bool IsUnorderedSourceType(ITypeSymbol? type) {
        if(type is not INamedTypeSymbol named) return false;
        var definition=named.OriginalDefinition;
        var ns=definition.ContainingNamespace.ToDisplayString();
        return ns switch {
            "Seamless.Collections.Tsavorite" => definition.MetadataName is "HashSet`1" or "Set`1" or "Set`2",
            "Seamless.Collections.Persisted" => definition.MetadataName=="Set`1",
            "Seamless.Collections" => definition.MetadataName is "ISet`1" or "IUnorderedEnumerable`1",
            _ => false,
        };
    }
}
