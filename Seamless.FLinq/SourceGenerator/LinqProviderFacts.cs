using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Seamless.FLinq;

public sealed partial class FLinqIncrementalGenerator {
    private const string SystemLinqEnumerableTypeName = "System.Linq.Enumerable";
    private const string TsavoriteLinqEnumerableTypeName = "Seamless.Tsavorite.Linq.Enumerable";
    private const string PersistedLinqEnumerableTypeName = "Seamless.Persisted.Linq.Enumerable";
    private const string SystemMemoryExtensionsTypeName = "System.MemoryExtensions";

    private static bool IsSupportedLinqEnumerableMethod(IMethodSymbol method) {
        var definition = method.ReducedFrom ?? method;
        var containingType = definition.ContainingType.ToDisplayString();
        return containingType==SystemLinqEnumerableTypeName
            || containingType==TsavoriteLinqEnumerableTypeName
            || containingType==PersistedLinqEnumerableTypeName;
    }

    private static bool IsSupportedLinqInvocation(InvocationExpressionSyntax invocation,SemanticModel semanticModel) {
        if(semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method) {
            return false;
        }

        var definition = method.ReducedFrom ?? method;
        var containingType = definition.ContainingType.ToDisplayString();
        return containingType==SystemLinqEnumerableTypeName
            || containingType==TsavoriteLinqEnumerableTypeName
            || containingType==PersistedLinqEnumerableTypeName
            || (definition.Name=="Contains" && containingType==SystemMemoryExtensionsTypeName);
    }

    private static bool IsTsavoriteConcreteHashSetType(ITypeSymbol? type,out ITypeSymbol elementType) {
        if(TryMatchGenericNamedType(type,"Seamless.Tsavorite.Collections","HashSet`1",out var named)) {
            elementType=named.TypeArguments[0];
            return true;
        }

        elementType=null!;
        return false;
    }

    private static bool IsPersistedConcreteSetType(ITypeSymbol? type,out ITypeSymbol elementType) {
        if(TryMatchGenericNamedType(type,"Seamless.Persisted.Collections","Set`1",out var named)) {
            elementType=named.TypeArguments[0];
            return true;
        }

        elementType=null!;
        return false;
    }

    private static bool TryMatchGenericNamedType(
        ITypeSymbol? type,
        string namespaceName,
        string metadataName,
        out INamedTypeSymbol matched) {
        for(var current = type as INamedTypeSymbol;current is not null;current=current.BaseType) {
            var definition = current.OriginalDefinition;
            if(definition.MetadataName==metadataName
                && definition.ContainingNamespace.ToDisplayString()==namespaceName
                && current.TypeArguments.Length==1) {
                matched=current;
                return true;
            }
        }

        matched=null!;
        return false;
    }
}
