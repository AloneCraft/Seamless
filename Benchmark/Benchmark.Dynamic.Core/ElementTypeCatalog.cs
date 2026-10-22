namespace Benchmark.Dynamic.Core;

/// <summary>クエリの要素型。<c>Int</c> を既定値(0)とし、IR JSON では既定値を書き出さない。</summary>
public enum QueryElementType{
    Int,
}

/// <summary>
/// 要素型ごとの違い(C# 型名、述語・選択子のラムダ)を1か所に集める。
/// 型を足す・外すときは、ここへ記述子を1つ足すだけで済むようにする。
/// </summary>
public sealed record ElementTypeDescriptor(
    QueryElementType Id,
    string ClrTypeName,
    Func<QueryPredicate,string> EmitPredicate,
    Func<QuerySelector,string> EmitSelector);

public static class ElementTypeCatalog{
    public static ElementTypeDescriptor Int{get;}=new(
        QueryElementType.Int,
        "global::System.Int32",
        static predicate=>predicate.Kind switch{
            QueryPredicateKind.IsEven=>"value=>(value&1)==0",
            QueryPredicateKind.IsOdd=>"value=>(value&1)!=0",
            QueryPredicateKind.GreaterThan=>$"value=>value>{predicate.Operand}",
            QueryPredicateKind.LessThan=>$"value=>value<{predicate.Operand}",
            QueryPredicateKind.ModuloEquals=>$"value=>value%{predicate.Modulus}=={predicate.Operand}",
            _=>throw new ArgumentOutOfRangeException(nameof(predicate)),
        },
        static selector=>selector.Kind switch{
            QuerySelectorKind.Identity=>"value=>value",
            QuerySelectorKind.Add=>$"value=>value+({selector.Operand})",
            QuerySelectorKind.Multiply=>$"value=>value*({selector.Operand})",
            QuerySelectorKind.Negate=>"value=>-value",
            _=>throw new ArgumentOutOfRangeException(nameof(selector)),
        });

    public static ElementTypeDescriptor Get(QueryElementType type){
        return type switch{
            QueryElementType.Int=>Int,
            _=>throw new ArgumentOutOfRangeException(nameof(type)),
        };
    }
}
