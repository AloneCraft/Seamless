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
    internal static bool IsDatabaseExecutionSourceType(ITypeSymbol? type)=>IsTransactionRowsType(type,out _)||IsDatabaseRowsType(type)||IsDatabaseTableType(type);
    private static bool IsTransactionRowsType(ITypeSymbol? type,out ITypeSymbol elementType){
        if(TryMatchGenericNamedType(type,"Seamless.Transactions.Collections","TransactionRows`2",2,out var named)){elementType=named.TypeArguments[1];return true;}
        elementType=null!;return false;
    }
    private static bool IsTransactionCollectionType(ITypeSymbol? type)=>
        TryMatchGenericNamedType(type,"Seamless.Transactions.Collections","TransactionCollection`2",2,out _)
        ||TryMatchGenericNamedType(type,"Seamless.Transactions.Collections","ReadOnlyTransactionCollection`2",2,out _);
    private static bool IsDatabaseRowsType(ITypeSymbol? type)=>TryMatchGenericNamedType(type,"Seamless.Database","DatabaseRows`2",2,out _);
    private static bool IsDatabaseTableType(ITypeSymbol? type)=>TryMatchGenericNamedType(type,"Seamless.Database","StoredTable`2",2,out _)
        ||TryMatchGenericNamedType(type,"Seamless.Database","MaterializedView`2",2,out _);

    // [関係代入] 表のプロパティの型は基底の Table(式も表す型)。型だけでは保存された表か取引の表か分からない。
    private static bool IsBaseTableType(ITypeSymbol? type)=>type is INamedTypeSymbol named
        &&named.OriginalDefinition.ContainingNamespace.ToDisplayString()=="Seamless.Database"
        &&named.OriginalDefinition.MetadataName is "Table`2" or "Table`1";

    /// <summary>
    /// 基底の Table 型の元データを、表のプロパティを持つ型で振り分ける。取引の入口(TypedTransaction・SchemaView)の表は取引の表、
    /// コンテナ・スキーマの表は保存された表として、具体的な型への変換を付ける。表の式やローカル変数など、決められないものは元のまま(普通の列挙)。
    /// </summary>
    private static void ResolveDatabaseTableSource(ExpressionSyntax expression,SemanticModel model,ref ITypeSymbol? expressionType,ref string sourceExpression){
        var target=expression;
        while(target is ParenthesizedExpressionSyntax parenthesized){target=parenthesized.Expression;}
        // 表の型の演算を System.Linq へ下げたときは IEnumerable<T> への変換が挟まる(SetCallLowering)。
        if(target is CastExpressionSyntax cast){
            target=cast.Expression;
            while(target is ParenthesizedExpressionSyntax inner){target=inner.Expression;}
        }
        var tableType=model.GetTypeInfo(target).Type;
        if(!IsBaseTableType(tableType)||model.GetSymbolInfo(target).Symbol is not IPropertySymbol property){return;}
        var arguments=((INamedTypeSymbol)tableType!).TypeArguments;
        var key=arguments[0];var row=arguments.Length==2?arguments[1]:arguments[0];
        INamedTypeSymbol? resolved=null;
        if(IsTransactionEntryType(property.ContainingType)){
            resolved=model.Compilation.GetTypeByMetadataName("Seamless.Transactions.Collections.TransactionCollection`2")?.Construct(key,row);
        }else if(arguments.Length==2&&IsDatabaseOwnerType(property.ContainingType)){
            resolved=model.Compilation.GetTypeByMetadataName("Seamless.Database.StoredTable`2")?.Construct(key,row);
        }
        if(resolved is null){return;}
        expressionType=resolved;
        sourceExpression="(("+resolved.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)+")("+sourceExpression+"))";
    }

    private static bool IsTransactionEntryType(INamedTypeSymbol? type)=>type is not null
        &&(type.Name=="TypedTransaction"&&IsDatabaseOwnerType(type.ContainingType)
            ||type.Name.StartsWith("SchemaView",StringComparison.Ordinal)&&IsTransactionEntryType(type.ContainingType));

    private static bool IsDatabaseOwnerType(INamedTypeSymbol? type){
        for(var current=type;current is not null;current=current.BaseType){
            if(current.ContainingNamespace?.ToDisplayString()=="Seamless.Database"&&current.Name is "DatabaseContainer" or "DatabaseSchema"){return true;}
        }
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

    // マルチ用(並列区間の操作用オブジェクト)。HashSet`1 / Set`2 を継承しない別系統なので、別に判定する。
    private static bool IsTsavoriteParallelHashSetType(ITypeSymbol? type,out ITypeSymbol elementType) {
        if(TryMatchGenericNamedType(type,"Seamless.Collections.Tsavorite","ParallelHashSet`1",out var named)) {
            elementType=named.TypeArguments[0];
            return true;
        }

        elementType=null!;
        return false;
    }

    private static bool IsTsavoriteParallelSetType(ITypeSymbol? type,out ITypeSymbol elementType) {
        if(TryMatchGenericNamedType(type,"Seamless.Collections.Tsavorite","ParallelSet`2",2,out var named)) {
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
