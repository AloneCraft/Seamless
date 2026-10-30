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
            case OrderByKeyOperatorArguments:
                return input;
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
            _=>input,
        };
    }

    /// <summary>入力の要素が一意であれば出力も一意か。</summary>
    public static bool PreservesUniqueness(ExtendedUnaryOperatorArguments arguments){
        return arguments is TakeLastOperatorArguments or SkipLastOperatorArguments
            or TakeWhileOperatorArguments or SkipWhileOperatorArguments or OrderByKeyOperatorArguments;
    }
}
