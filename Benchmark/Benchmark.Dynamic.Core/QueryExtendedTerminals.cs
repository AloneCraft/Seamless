namespace Benchmark.Dynamic.Core;

/// <summary>Zip の件数規則。順序は <see cref="QueryBinaryKind.Concat"/> と同じく、両方が順序付きのときだけ順序付きとする。</summary>
public static class ExtendedBinaryShape{
    public static QueryCardinalityRange ZipCardinality(QueryCardinalityRange left,QueryCardinalityRange right){
        long? maximum=left.Maximum is { }leftMaximum&&right.Maximum is { }rightMaximum
            ?Math.Min(leftMaximum,rightMaximum)
            :left.Maximum??right.Maximum;
        return new(Math.Min(left.Minimum,right.Minimum),maximum);
    }
}

/// <summary>
/// チェーン検証から移した終端演算子。引数は既存の <see cref="TerminalOperatorArguments"/> のフィールドを流用する
/// (新しいプロパティを足すと既存のIR JSONとハッシュが変わるため)。
/// CountWhere/AnyWhere は Predicate、CountBy/AggregateBy は Value を法(2以上)、
/// SequenceEqual は Value を比較相手(0=副Source、1=同じチェーン)として使う。
/// </summary>
public static class ExtendedTerminalShape{
    public static bool IsExtended(QueryTerminalKind kind){
        return kind is QueryTerminalKind.CountWhere or QueryTerminalKind.AnyWhere or QueryTerminalKind.AggregateSum
            or QueryTerminalKind.AverageSelector or QueryTerminalKind.CountBy or QueryTerminalKind.AggregateBy
            or QueryTerminalKind.SequenceEqual;
    }

    public static string GetOperatorId(QueryTerminalKind kind){
        return kind switch{
            QueryTerminalKind.CountWhere=>QueryOperatorIds.CountWhere,
            QueryTerminalKind.AnyWhere=>QueryOperatorIds.AnyWhere,
            QueryTerminalKind.AggregateSum=>QueryOperatorIds.AggregateSum,
            QueryTerminalKind.AverageSelector=>QueryOperatorIds.AverageSelector,
            QueryTerminalKind.CountBy=>QueryOperatorIds.CountBy,
            QueryTerminalKind.AggregateBy=>QueryOperatorIds.AggregateBy,
            QueryTerminalKind.SequenceEqual=>QueryOperatorIds.SequenceEqual,
            _=>throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    public static QueryTerminalKind GetKind(string operatorId){
        return operatorId switch{
            QueryOperatorIds.CountWhere=>QueryTerminalKind.CountWhere,
            QueryOperatorIds.AnyWhere=>QueryTerminalKind.AnyWhere,
            QueryOperatorIds.AggregateSum=>QueryTerminalKind.AggregateSum,
            QueryOperatorIds.AverageSelector=>QueryTerminalKind.AverageSelector,
            QueryOperatorIds.CountBy=>QueryTerminalKind.CountBy,
            QueryOperatorIds.AggregateBy=>QueryTerminalKind.AggregateBy,
            QueryOperatorIds.SequenceEqual=>QueryTerminalKind.SequenceEqual,
            _=>throw new ArgumentOutOfRangeException(nameof(operatorId)),
        };
    }

    public static (QueryValueType Type,QueryCardinality Cardinality) Shape(QueryTerminalKind kind){
        return kind switch{
            QueryTerminalKind.CountWhere=>(QueryValueType.Int,QueryCardinality.Scalar),
            QueryTerminalKind.AnyWhere or QueryTerminalKind.SequenceEqual=>(QueryValueType.Bool,QueryCardinality.Scalar),
            QueryTerminalKind.AggregateSum=>(QueryValueType.Long,QueryCardinality.Scalar),
            QueryTerminalKind.AverageSelector=>(QueryValueType.Double,QueryCardinality.Scalar),
            QueryTerminalKind.CountBy or QueryTerminalKind.AggregateBy=>(QueryValueType.Int,QueryCardinality.Sequence),
            _=>throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    public static bool RequiresNonEmpty(QueryTerminalKind kind){
        return kind==QueryTerminalKind.AverageSelector;
    }

    public static string? Validate(TerminalOperatorArguments arguments){
        return arguments.Kind switch{
            QueryTerminalKind.CountWhere or QueryTerminalKind.AnyWhere when arguments.Predicate is null=>
                $"{arguments.Kind} requires a predicate.",
            QueryTerminalKind.CountWhere or QueryTerminalKind.AnyWhere when arguments.Predicate is {Kind:QueryPredicateKind.ModuloEquals,Modulus:<1}=>
                $"{arguments.Kind} requires a positive modulus.",
            QueryTerminalKind.CountBy or QueryTerminalKind.AggregateBy when arguments.Value is not >=2=>
                $"{arguments.Kind} requires a modulus of at least 2.",
            QueryTerminalKind.SequenceEqual when arguments.Value is not (0 or 1)=>
                "SequenceEqual requires Value 0 (secondary source) or 1 (the same chain).",
            _=>null,
        };
    }

    /// <summary>出力が列の終端(CountBy/AggregateBy)の件数。スカラーは 1 件。</summary>
    public static QueryCardinalityRange Cardinality(TerminalOperatorArguments arguments,QueryCardinalityRange input){
        if(Shape(arguments.Kind).Cardinality==QueryCardinality.Scalar){
            return QueryCardinalityRange.Exact(1);
        }
        var modulus=arguments.Value??2;
        return new(
            input.Minimum>0?1:0,
            input.Maximum is { }maximum?Math.Min(maximum,modulus):modulus);
    }

    /// <summary>
    /// CountBy/AggregateBy の出力は、キーの初出順になる。入力が順序なしなら順序なし、
    /// それ以外は順序付き(キーの並びは決まるが全順序ではない)。スカラーは全順序扱い。
    /// </summary>
    public static QueryOrderSemantics Order(QueryTerminalKind kind,QueryOrderSemantics input){
        if(Shape(kind).Cardinality==QueryCardinality.Scalar){
            return QueryOrderSemantics.TotalOrdered;
        }
        return input==QueryOrderSemantics.Unordered?QueryOrderSemantics.Unordered:QueryOrderSemantics.Ordered;
    }
}
