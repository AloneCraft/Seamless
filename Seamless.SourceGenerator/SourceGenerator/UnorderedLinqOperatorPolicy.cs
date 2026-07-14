namespace Seamless.SourceGenerator;

public static class UnorderedLinqOperatorPolicy {
    public static bool IsUnorderedSourceType(ITypeSymbol? type) {
        if(type is not INamedTypeSymbol named) {
            return false;
        }
        var definition = named.OriginalDefinition;
        var ns = definition.ContainingNamespace.ToDisplayString();
        return ns switch {
            "Seamless.Tsavorite.Collections" => definition.MetadataName is "HashSet`1" or "Set`1" or "Set`2",
            "Seamless.Persisted.Collections" => definition.MetadataName=="Set`1",
            "Seamless.Collections" => definition.MetadataName is "ISet`1" or "IUnorderedEnumerable`1",
            _ => false,
        };
    }

    public static bool IsRestricted(IMethodSymbol method) {
        var definition = method.ReducedFrom??method;
        var name = definition.Name;
        if(name.StartsWith("Order",StringComparison.Ordinal)
            ||name.StartsWith("ThenBy",StringComparison.Ordinal)
            ||name.StartsWith("First",StringComparison.Ordinal)
            ||name.StartsWith("Last",StringComparison.Ordinal)
            ||name.StartsWith("ElementAt",StringComparison.Ordinal)
            ||name.StartsWith("Skip",StringComparison.Ordinal)
            ||name.StartsWith("Take",StringComparison.Ordinal)) {
            return true;
        }
        if(name is "Reverse" or "Shuffle" or "Index" or "Zip" or "SequenceEqual"
            or "Chunk" or "Append" or "Prepend" or "Concat") {
            return true;
        }
        return (name is "Where" or "Select" or "SelectMany")&&UsesIndex(definition);
    }

    public static bool IsExplicitOptIn(IMethodSymbol method) =>
        (method.ReducedFrom??method).Name=="AsEnumerable";

    private static bool UsesIndex(IMethodSymbol method) {
        foreach(var parameter in method.Parameters.Skip(1)) {
            if(parameter.Type is not INamedTypeSymbol delegateType||delegateType.DelegateInvokeMethod is not { } invoke) {
                continue;
            }
            var parameters = invoke.Parameters;
            if(parameters.Length>=2&&parameters[1].Type.SpecialType==SpecialType.System_Int32) {
                return true;
            }
        }
        return false;
    }
}
