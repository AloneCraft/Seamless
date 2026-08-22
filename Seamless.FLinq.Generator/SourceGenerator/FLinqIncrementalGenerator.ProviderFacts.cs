namespace Seamless.FLinq.Generator;

public sealed partial class FLinqIncrementalGenerator {
    private const string SystemLinqEnumerableTypeName = "System.Linq.Enumerable";
    private const string SeamlessLinqEnumerableTypeName = "Seamless.Linq.Enumerable";
    private const string SystemMemoryExtensionsTypeName = "System.MemoryExtensions";

    // Span/ReadOnlySpan への暗黙変換により、T[] レシーバーでは System.Linq.Enumerable ではなく
    // System.MemoryExtensions のオーバーロードへ解決されることがある(例: array.SequenceEqual(array2))。
    // SequenceEqual/Contains は Fragment 側で候補登録済みのため、対象判定でも Enumerable 系と同格に扱う。
    // 2026-08: Contains.cs の RenderDirectSpanContainsComparerInline にあった
    // `var __comparer={{comparer}};`(型注釈なしのため comparer=="null" だと CS0815)は
    // 型注釈を付けて修正済み。当初はゲートをここまで広げただけで実測したところ、値型(int等)の
    // 無引数 Contains が BCL の MemoryExtensions.Contains(SIMDベクトル化探索)から生成器の素朴な
    // 要素ごとループへ後退し、実測 7.6〜7.9倍の性能退行を起こすことを確認したためゲートを一旦閉じた。
    // その後 ContainsSpan<T>(Seamless.Linq/SequenceEqual.cs、SequenceEqualSpan と同型の
    // typeof(T) 実行時分岐)を追加し、comparer=="null" のときはこれを呼ぶよう
    // RenderDirectSpanContainsComparerInline を修正したことで、値型は BCL の SIMD Contains へ
    // 再度乗るようになった。これによりゲートを再度開く。
    private static bool IsSupportedLinqEnumerableMethod(IMethodSymbol method) {
        var definition = method.ReducedFrom??method;
        var containingType = definition.ContainingType.ToDisplayString();
        return containingType==SystemLinqEnumerableTypeName
            ||containingType==SeamlessLinqEnumerableTypeName
            ||((definition.Name=="SequenceEqual"||definition.Name=="Contains")&&containingType==SystemMemoryExtensionsTypeName);
    }

    private static bool IsSupportedLinqInvocation(InvocationExpressionSyntax invocation,SemanticModel semanticModel) {
        if(semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method) {
            return false;
        }

        var definition = method.ReducedFrom??method;
        var containingType = definition.ContainingType.ToDisplayString();
        return containingType==SystemLinqEnumerableTypeName
            ||containingType==SeamlessLinqEnumerableTypeName
            ||((definition.Name=="Contains"||definition.Name=="SequenceEqual")&&containingType==SystemMemoryExtensionsTypeName);
    }

    private static bool IsTsavoriteConcreteHashSetType(ITypeSymbol? type,out ITypeSymbol elementType) {
        if(TryMatchGenericNamedType(type,"Seamless.Collections.Tsavorite","HashSet`1",out var named)) {
            elementType=named.TypeArguments[0];
            return true;
        }

        elementType=null!;
        return false;
    }

    private static bool IsTsavoriteConcreteSetType(ITypeSymbol? type,out ITypeSymbol elementType) {
        if(TryMatchGenericNamedType(type,"Seamless.Collections.Tsavorite","Set`2",2,out var named)) {
            elementType=named.TypeArguments[1];
            return true;
        }

        elementType=null!;
        return false;
    }

    private static bool IsPersistedConcreteSetType(ITypeSymbol? type,out ITypeSymbol elementType) {
        if(TryMatchGenericNamedType(type,"Seamless.Collections.Persisted","Set`1",out var named)) {
            elementType=named.TypeArguments[0];
            return true;
        }

        elementType=null!;
        return false;
    }

    private static bool IsPersistedConcreteListType(ITypeSymbol? type,out ITypeSymbol elementType) {
        if(TryMatchGenericNamedType(type,"Seamless.Collections","PersistedList`1",out var named)) {
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
                &&definition.ContainingNamespace.ToDisplayString()==namespaceName
                &&current.TypeArguments.Length==typeArgumentCount) {
                matched=current;
                return true;
            }
        }

        matched=null!;
        return false;
    }
}
