using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Tsavorite.SourceGenerator;

public sealed partial class TsavoriteIncrementalGenerator {
    private const string SystemLinqEnumerableTypeName = "System.Linq.Enumerable";
    private const string TsavoriteLinqEnumerableTypeName = "Tsavorite.Linq.Enumerable";
    private const string SystemMemoryExtensionsTypeName = "System.MemoryExtensions";

    private static bool IsSupportedLinqEnumerableMethod(IMethodSymbol method) {
        var definition = method.ReducedFrom ?? method;
        var containingType = definition.ContainingType.ToDisplayString();
        return containingType==SystemLinqEnumerableTypeName
            || containingType==TsavoriteLinqEnumerableTypeName;
    }

    private static bool IsSupportedLinqInvocation(InvocationExpressionSyntax invocation,SemanticModel semanticModel) {
        if(semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method) {
            return false;
        }

        var definition = method.ReducedFrom ?? method;
        var containingType = definition.ContainingType.ToDisplayString();
        return containingType==SystemLinqEnumerableTypeName
            || containingType==TsavoriteLinqEnumerableTypeName
            || (definition.Name=="Contains" && containingType==SystemMemoryExtensionsTypeName);
    }

    private static bool IsTsavoriteConcreteHashSetType(ITypeSymbol? type,out ITypeSymbol elementType) {
        if(type is INamedTypeSymbol {
            ContainingNamespace: { } ns,
            MetadataName: "HashSet`1",
            TypeArguments.Length: 1
        } named
            && ns.ToDisplayString()=="Tsavorite.Collections") {
            elementType=named.TypeArguments[0];
            return true;
        }

        elementType=null!;
        return false;
    }
}
