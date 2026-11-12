namespace Benchmark.Dynamic.Core;

public enum GroupByAggregateKind{
    Sum,
    Count,
    KeyOnly,
}

/// <summary>
/// チェーン検証から移した単項演算子。件数と順序の規則は <see cref="ExtendedUnaryShape"/> に集める。
/// </summary>
public abstract record ExtendedUnaryOperatorArguments:UnaryOperatorArguments;

public sealed record TakeLastOperatorArguments(int Count):ExtendedUnaryOperatorArguments;
public sealed record SkipLastOperatorArguments(int Count):ExtendedUnaryOperatorArguments;
public sealed record TakeWhileOperatorArguments(QueryPredicate Predicate):ExtendedUnaryOperatorArguments;
public sealed record SkipWhileOperatorArguments(QueryPredicate Predicate):ExtendedUnaryOperatorArguments;
public sealed record AppendOperatorArguments(int Value):ExtendedUnaryOperatorArguments;
public sealed record PrependOperatorArguments(int Value):ExtendedUnaryOperatorArguments;
public sealed record DefaultIfEmptyOperatorArguments(int Value):ExtendedUnaryOperatorArguments;

/// <summary>Modulus が 0 のとき恒等キー、1 以上のとき (value%Modulus+Modulus)%Modulus をキーにする安定整列。</summary>
public sealed record OrderByKeyOperatorArguments(int Modulus,bool Descending):ExtendedUnaryOperatorArguments;

public sealed record GroupByAggregateOperatorArguments(GroupByAggregateKind Kind,int Modulus):ExtendedUnaryOperatorArguments;

/// <summary>Order() / OrderDescending()。要素自身がキーの整列(比較できる要素型だけ)。</summary>
public sealed record OrderOperatorArguments(bool Descending):ExtendedUnaryOperatorArguments;
/// <summary>Shuffle().Order()。Shuffle だけでは結果が決まらないので、直後に整列して比べる。</summary>
public sealed record ShuffleOrderOperatorArguments:ExtendedUnaryOperatorArguments;
/// <summary>Cast&lt;object&gt;().Cast&lt;T&gt;()。object を経由して元の型へ戻す。</summary>
public sealed record CastOperatorArguments:ExtendedUnaryOperatorArguments;
/// <summary>Cast&lt;object&gt;().OfType&lt;T&gt;()。同じ型への OfType なので、null だけが落ちる。</summary>
public sealed record OfTypeOperatorArguments:ExtendedUnaryOperatorArguments;
public sealed record AsEnumerableOperatorArguments:ExtendedUnaryOperatorArguments;
/// <summary>Chunk(Size) の各塊を SelectMany で元の列へ戻す。</summary>
public sealed record ChunkFlattenOperatorArguments(int Size):ExtendedUnaryOperatorArguments;
/// <summary>Index() の各組を item^index にする(int の要素だけ)。</summary>
public sealed record IndexXorOperatorArguments:ExtendedUnaryOperatorArguments;

public static class ExtendedUnaryShape{
    public static string GetOperatorId(ExtendedUnaryOperatorArguments arguments){
        return arguments switch{
            TakeLastOperatorArguments=>QueryOperatorIds.TakeLast,
            SkipLastOperatorArguments=>QueryOperatorIds.SkipLast,
            TakeWhileOperatorArguments=>QueryOperatorIds.TakeWhile,
            SkipWhileOperatorArguments=>QueryOperatorIds.SkipWhile,
            AppendOperatorArguments=>QueryOperatorIds.Append,
            PrependOperatorArguments=>QueryOperatorIds.Prepend,
            DefaultIfEmptyOperatorArguments=>QueryOperatorIds.DefaultIfEmpty,
            OrderByKeyOperatorArguments=>QueryOperatorIds.OrderByKey,
            GroupByAggregateOperatorArguments{Kind:GroupByAggregateKind.Sum}=>QueryOperatorIds.GroupBySum,
            GroupByAggregateOperatorArguments{Kind:GroupByAggregateKind.Count}=>QueryOperatorIds.GroupByCount,
            GroupByAggregateOperatorArguments{Kind:GroupByAggregateKind.KeyOnly}=>QueryOperatorIds.GroupByKeyOnly,
            OrderOperatorArguments=>QueryOperatorIds.Order,
            ShuffleOrderOperatorArguments=>QueryOperatorIds.ShuffleOrder,
            CastOperatorArguments=>QueryOperatorIds.Cast,
            OfTypeOperatorArguments=>QueryOperatorIds.OfType,
            AsEnumerableOperatorArguments=>QueryOperatorIds.AsEnumerable,
            ChunkFlattenOperatorArguments=>QueryOperatorIds.ChunkFlatten,
            IndexXorOperatorArguments=>QueryOperatorIds.IndexXor,
            _=>throw new ArgumentOutOfRangeException(nameof(arguments)),
        };
    }

    /// <summary>引数が構文上妥当かを返す。妥当でなければ理由を返す。</summary>
    public static string? Validate(ExtendedUnaryOperatorArguments arguments){
        return arguments switch{
            TakeLastOperatorArguments {Count:<0}=>"TakeLast requires a non-negative count.",
            SkipLastOperatorArguments {Count:<0}=>"SkipLast requires a non-negative count.",
            TakeWhileOperatorArguments {Predicate.Kind:QueryPredicateKind.ModuloEquals,Predicate.Modulus:<1}=>"TakeWhile requires a positive modulus.",
            SkipWhileOperatorArguments {Predicate.Kind:QueryPredicateKind.ModuloEquals,Predicate.Modulus:<1}=>"SkipWhile requires a positive modulus.",
            OrderByKeyOperatorArguments {Modulus:<0}=>"OrderByKey requires a non-negative modulus.",
            GroupByAggregateOperatorArguments {Modulus:<1}=>"GroupBy aggregate requires a positive modulus.",
            ChunkFlattenOperatorArguments {Size:<1}=>"Chunk requires a positive size.",
            _=>null,
        };
    }

    public static QueryCardinalityRange Cardinality(ExtendedUnaryOperatorArguments arguments,QueryCardinalityRange input){
        switch(arguments){
            case TakeLastOperatorArguments takeLast:
                return new(
                    Math.Min(input.Minimum,takeLast.Count),
                    input.Maximum is { }takeMaximum?Math.Min(takeMaximum,takeLast.Count):takeLast.Count);
            case SkipLastOperatorArguments skipLast:
                return new(
                    Math.Max(0,input.Minimum-skipLast.Count),
                    input.Maximum is { }skipMaximum?Math.Max(0,skipMaximum-skipLast.Count):null);
            case TakeWhileOperatorArguments or SkipWhileOperatorArguments:
                return new(0,input.Maximum);
            case AppendOperatorArguments or PrependOperatorArguments:
                return new(
                    QueryCardinalityRange.SaturatingAdd(input.Minimum,1),
                    input.Maximum is { }appendMaximum?QueryCardinalityRange.SaturatingAdd(appendMaximum,1):null);
            case DefaultIfEmptyOperatorArguments:
                return new(
                    Math.Max(1,input.Minimum),
                    input.Maximum is { }defaultMaximum?Math.Max(1,defaultMaximum):null);
            case OrderByKeyOperatorArguments or OrderOperatorArguments or ShuffleOrderOperatorArguments
                or CastOperatorArguments or AsEnumerableOperatorArguments or ChunkFlattenOperatorArguments or IndexXorOperatorArguments:
                return input;
            case OfTypeOperatorArguments:
                // 同じ型への OfType は null だけを落とす。
                return new(0,input.Maximum);
            case GroupByAggregateOperatorArguments group:
                return new(
                    input.Minimum>0?1:0,
                    input.Maximum is { }groupMaximum?Math.Min(groupMaximum,group.Modulus):group.Modulus);
            default:
                throw new ArgumentOutOfRangeException(nameof(arguments));
        }
    }

    public static QueryOrderSemantics Order(ExtendedUnaryOperatorArguments arguments,QueryOrderSemantics input){
        return arguments switch{
            // 同じキーの要素の相対順序は安定整列で決まるが、全順序(TotalOrdered)にはならない。
            OrderByKeyOperatorArguments=>input==QueryOrderSemantics.Unordered?QueryOrderSemantics.Unordered:QueryOrderSemantics.Ordered,
            OrderOperatorArguments or ShuffleOrderOperatorArguments=>QueryOrderSemantics.TotalOrdered,
            _=>input,
        };
    }

    /// <summary>入力の要素が一意であれば出力も一意か。</summary>
    public static bool PreservesUniqueness(ExtendedUnaryOperatorArguments arguments){
        return arguments is TakeLastOperatorArguments or SkipLastOperatorArguments
            or TakeWhileOperatorArguments or SkipWhileOperatorArguments or OrderByKeyOperatorArguments
            or OrderOperatorArguments or ShuffleOrderOperatorArguments or CastOperatorArguments
            or OfTypeOperatorArguments or AsEnumerableOperatorArguments or ChunkFlattenOperatorArguments;
    }
}
