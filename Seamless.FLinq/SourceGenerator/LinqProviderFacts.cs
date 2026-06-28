using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Seamless.FLinq;

public sealed partial class FLinqIncrementalGenerator {
    private const string SystemLinqEnumerableTypeName = "System.Linq.Enumerable";
    private const string SeamlessLinqEnumerableTypeName = "Seamless.Linq.Enumerable";
    private const string SystemMemoryExtensionsTypeName = "System.MemoryExtensions";

    private static bool IsSupportedLinqEnumerableMethod(IMethodSymbol method) {
        var definition = method.ReducedFrom ?? method;
        var containingType = definition.ContainingType.ToDisplayString();
        return containingType==SystemLinqEnumerableTypeName
            || containingType==SeamlessLinqEnumerableTypeName;
    }

    private static bool IsSupportedLinqInvocation(InvocationExpressionSyntax invocation,SemanticModel semanticModel) {
        if(semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method) {
            return false;
        }

        var definition = method.ReducedFrom ?? method;
        var containingType = definition.ContainingType.ToDisplayString();
        return containingType==SystemLinqEnumerableTypeName
            || containingType==SeamlessLinqEnumerableTypeName
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

    private static bool IsTsavoriteConcreteSetType(ITypeSymbol? type,out ITypeSymbol elementType) {
        if(TryMatchGenericNamedType(type,"Seamless.Tsavorite.Collections","Set`2",2,out var named)) {
            elementType=named.TypeArguments[1];
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
        out INamedTypeSymbol matched) =>
        TryMatchGenericNamedType(type,namespaceName,metadataName,1,out matched);

    private static bool TryMatchGenericNamedType(
        ITypeSymbol? type,
        string namespaceName,
        string metadataName,
        int typeArgumentCount,
        out INamedTypeSymbol matched) {
        for(var current = type as INamedTypeSymbol;current is not null;current=current.BaseType) {
            var definition = current.OriginalDefinition;
            if(definition.MetadataName==metadataName
                && definition.ContainingNamespace.ToDisplayString()==namespaceName
                && current.TypeArguments.Length==typeArgumentCount) {
                matched=current;
                return true;
            }
        }

        matched=null!;
        return false;
    }
}
