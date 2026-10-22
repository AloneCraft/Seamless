namespace Benchmark.Dynamic.Core;

public static class QueryOperatorIds{
    public const string Source="source";
    public const string Where="where";
    public const string WhereIndexed="where.indexedModulo";
    public const string Select="select";
    public const string SelectIndexed="select.indexedXor";
    public const string Distinct="distinct";
    public const string DistinctBy="distinctBy.modulo";
    public const string Take="take";
    public const string Skip="skip";
    public const string Reverse="reverse";
    public const string OrderBy="orderBy";
    public const string ThenBy="thenBy";
    public const string TakeLast="takeLast";
    public const string SkipLast="skipLast";
    public const string TakeWhile="takeWhile";
    public const string SkipWhile="skipWhile";
    public const string Append="append";
    public const string Prepend="prepend";
    public const string DefaultIfEmpty="defaultIfEmpty";
    public const string OrderByKey="orderBy.key";
    public const string GroupBySum="groupBy.sum";
    public const string GroupByCount="groupBy.count";
    public const string GroupByKeyOnly="groupBy.keyOnly";
    public const string Concat="concat";
    public const string Union="union";
    public const string Except="except";
    public const string Intersect="intersect";
    public const string UnionByIdentity="unionBy.identity";
    public const string ExceptByIdentity="exceptBy.identity";
    public const string IntersectByIdentity="intersectBy.identity";
    public const string SelectManyXor="selectMany.xor";
    public const string JoinXor="join.xor";
    public const string GroupJoinXorCount="groupJoin.xorCount";
    public const string Count="count";
    public const string LongCount="longCount";
    public const string Any="any";
    public const string All="all";
    public const string Contains="contains";
    public const string Sum="sum";
    public const string Min="min";
    public const string Max="max";
    public const string MinBy="minBy.identity";
    public const string MaxBy="maxBy.identity";
    public const string Average="average";
    public const string AggregateXor="aggregate.xor";
    public const string ToIntPair="materialize.intPair";
    public const string ToIntGroup="materialize.intGroup";
    public const string First="first";
    public const string FirstOrDefault="firstOrDefault";
    public const string Last="last";
    public const string LastOrDefault="lastOrDefault";
    public const string ElementAt="elementAt";
    public const string ElementAtOrDefault="elementAtOrDefault";
    public const string ToArray="toArray";
    public const string ToList="toList";
    public const string ToHashSet="toHashSet";
    public const string ToDictionary="toDictionary.identity";
    public const string ToLookup="toLookup.modulo";
    public const string Zip="zip";
    public const string CountWhere="countWhere";
    public const string AnyWhere="anyWhere";
    public const string AggregateSum="aggregate.sum";
    public const string AverageSelector="average.selector";
    public const string CountBy="countBy.modulo";
    public const string AggregateBy="aggregateBy.modulo";
    public const string SequenceEqual="sequenceEqual";
}

public sealed record OperatorDescriptor(
    string OperatorId,
    QueryNodeKind NodeKind,
    int Arity,
    QueryValueType InputType,
    QueryValueType OutputType,
    QueryCardinality OutputCardinality,
    bool RequiresNonEmpty,
    bool RequiresDeterministicOrder,
    bool IsInitialOperator,
    bool RequiresUniqueInput=false);

public static class BuiltInOperatorCatalog{
    public const int OperatorSetVersion=14;
    private static readonly OperatorDescriptor[] DescriptorArray=[
        new(QueryOperatorIds.Where,QueryNodeKind.Unary,1,QueryValueType.Int,QueryValueType.Int,QueryCardinality.Sequence,false,false,true),
        new(QueryOperatorIds.WhereIndexed,QueryNodeKind.Unary,1,QueryValueType.Int,QueryValueType.Int,QueryCardinality.Sequence,false,true,false),
        new(QueryOperatorIds.Select,QueryNodeKind.Unary,1,QueryValueType.Int,QueryValueType.Int,QueryCardinality.Sequence,false,false,true),
        new(QueryOperatorIds.SelectIndexed,QueryNodeKind.Unary,1,QueryValueType.Int,QueryValueType.Int,QueryCardinality.Sequence,false,true,false),
        new(QueryOperatorIds.Distinct,QueryNodeKind.Unary,1,QueryValueType.Int,QueryValueType.Int,QueryCardinality.Sequence,false,false,true),
        new(QueryOperatorIds.DistinctBy,QueryNodeKind.Unary,1,QueryValueType.Int,QueryValueType.Int,QueryCardinality.Sequence,false,false,false),
        new(QueryOperatorIds.Take,QueryNodeKind.Unary,1,QueryValueType.Int,QueryValueType.Int,QueryCardinality.Sequence,false,true,true),
        new(QueryOperatorIds.Skip,QueryNodeKind.Unary,1,QueryValueType.Int,QueryValueType.Int,QueryCardinality.Sequence,false,true,false),
        new(QueryOperatorIds.Reverse,QueryNodeKind.Unary,1,QueryValueType.Int,QueryValueType.Int,QueryCardinality.Sequence,false,true,false),
        new(QueryOperatorIds.OrderBy,QueryNodeKind.Unary,1,QueryValueType.Int,QueryValueType.Int,QueryCardinality.Sequence,false,false,true),
        new(QueryOperatorIds.ThenBy,QueryNodeKind.Unary,1,QueryValueType.Int,QueryValueType.Int,QueryCardinality.Sequence,false,false,true),
        new(QueryOperatorIds.TakeLast,QueryNodeKind.Unary,1,QueryValueType.Int,QueryValueType.Int,QueryCardinality.Sequence,false,true,false),
        new(QueryOperatorIds.SkipLast,QueryNodeKind.Unary,1,QueryValueType.Int,QueryValueType.Int,QueryCardinality.Sequence,false,true,false),
        new(QueryOperatorIds.TakeWhile,QueryNodeKind.Unary,1,QueryValueType.Int,QueryValueType.Int,QueryCardinality.Sequence,false,true,false),
        new(QueryOperatorIds.SkipWhile,QueryNodeKind.Unary,1,QueryValueType.Int,QueryValueType.Int,QueryCardinality.Sequence,false,true,false),
        new(QueryOperatorIds.Append,QueryNodeKind.Unary,1,QueryValueType.Int,QueryValueType.Int,QueryCardinality.Sequence,false,false,false),
        new(QueryOperatorIds.Prepend,QueryNodeKind.Unary,1,QueryValueType.Int,QueryValueType.Int,QueryCardinality.Sequence,false,false,false),
        new(QueryOperatorIds.DefaultIfEmpty,QueryNodeKind.Unary,1,QueryValueType.Int,QueryValueType.Int,QueryCardinality.Sequence,false,false,false),
        new(QueryOperatorIds.OrderByKey,QueryNodeKind.Unary,1,QueryValueType.Int,QueryValueType.Int,QueryCardinality.Sequence,false,false,false),
        new(QueryOperatorIds.GroupBySum,QueryNodeKind.Unary,1,QueryValueType.Int,QueryValueType.Int,QueryCardinality.Sequence,false,false,false),
        new(QueryOperatorIds.GroupByCount,QueryNodeKind.Unary,1,QueryValueType.Int,QueryValueType.Int,QueryCardinality.Sequence,false,false,false),
        new(QueryOperatorIds.GroupByKeyOnly,QueryNodeKind.Unary,1,QueryValueType.Int,QueryValueType.Int,QueryCardinality.Sequence,false,false,false),
        new(QueryOperatorIds.Concat,QueryNodeKind.Binary,2,QueryValueType.Int,QueryValueType.Int,QueryCardinality.Sequence,false,false,true),
        new(QueryOperatorIds.Union,QueryNodeKind.Binary,2,QueryValueType.Int,QueryValueType.Int,QueryCardinality.Sequence,false,false,true),
        new(QueryOperatorIds.Except,QueryNodeKind.Binary,2,QueryValueType.Int,QueryValueType.Int,QueryCardinality.Sequence,false,false,false),
        new(QueryOperatorIds.Intersect,QueryNodeKind.Binary,2,QueryValueType.Int,QueryValueType.Int,QueryCardinality.Sequence,false,false,false),
        new(QueryOperatorIds.UnionByIdentity,QueryNodeKind.Binary,2,QueryValueType.Int,QueryValueType.Int,QueryCardinality.Sequence,false,false,false),
        new(QueryOperatorIds.ExceptByIdentity,QueryNodeKind.Binary,2,QueryValueType.Int,QueryValueType.Int,QueryCardinality.Sequence,false,false,false),
        new(QueryOperatorIds.IntersectByIdentity,QueryNodeKind.Binary,2,QueryValueType.Int,QueryValueType.Int,QueryCardinality.Sequence,false,false,false),
        new(QueryOperatorIds.Zip,QueryNodeKind.Binary,2,QueryValueType.Int,QueryValueType.Int,QueryCardinality.Sequence,false,true,false),
        new(QueryOperatorIds.SelectManyXor,QueryNodeKind.Nested,2,QueryValueType.Int,QueryValueType.Int,QueryCardinality.Sequence,false,false,true),
        new(QueryOperatorIds.JoinXor,QueryNodeKind.Nested,2,QueryValueType.Int,QueryValueType.Int,QueryCardinality.Sequence,false,false,true),
        new(QueryOperatorIds.GroupJoinXorCount,QueryNodeKind.Nested,2,QueryValueType.Int,QueryValueType.Int,QueryCardinality.Sequence,false,false,false),
        new(QueryOperatorIds.Count,QueryNodeKind.Terminal,1,QueryValueType.Int,QueryValueType.Int,QueryCardinality.Scalar,false,false,true),
        new(QueryOperatorIds.LongCount,QueryNodeKind.Terminal,1,QueryValueType.Int,QueryValueType.Long,QueryCardinality.Scalar,false,false,true),
        new(QueryOperatorIds.Any,QueryNodeKind.Terminal,1,QueryValueType.Int,QueryValueType.Bool,QueryCardinality.Scalar,false,false,true),
        new(QueryOperatorIds.All,QueryNodeKind.Terminal,1,QueryValueType.Int,QueryValueType.Bool,QueryCardinality.Scalar,false,false,true),
        new(QueryOperatorIds.Contains,QueryNodeKind.Terminal,1,QueryValueType.Int,QueryValueType.Bool,QueryCardinality.Scalar,false,false,true),
        new(QueryOperatorIds.Sum,QueryNodeKind.Terminal,1,QueryValueType.Int,QueryValueType.Long,QueryCardinality.Scalar,false,false,true),
        new(QueryOperatorIds.Min,QueryNodeKind.Terminal,1,QueryValueType.Int,QueryValueType.Int,QueryCardinality.Scalar,true,false,true),
        new(QueryOperatorIds.Max,QueryNodeKind.Terminal,1,QueryValueType.Int,QueryValueType.Int,QueryCardinality.Scalar,true,false,true),
        new(QueryOperatorIds.MinBy,QueryNodeKind.Terminal,1,QueryValueType.Int,QueryValueType.Int,QueryCardinality.Scalar,true,false,false),
        new(QueryOperatorIds.MaxBy,QueryNodeKind.Terminal,1,QueryValueType.Int,QueryValueType.Int,QueryCardinality.Scalar,true,false,false),
        new(QueryOperatorIds.Average,QueryNodeKind.Terminal,1,QueryValueType.Int,QueryValueType.Double,QueryCardinality.Scalar,true,false,true),
        new(QueryOperatorIds.AggregateXor,QueryNodeKind.Terminal,1,QueryValueType.Int,QueryValueType.Int,QueryCardinality.Scalar,false,false,false),
        new(QueryOperatorIds.ToIntPair,QueryNodeKind.Terminal,1,QueryValueType.Int,QueryValueType.IntPair,QueryCardinality.Scalar,false,false,false),
        new(QueryOperatorIds.ToIntGroup,QueryNodeKind.Terminal,1,QueryValueType.Int,QueryValueType.IntGroup,QueryCardinality.Scalar,false,false,false),
        new(QueryOperatorIds.First,QueryNodeKind.Terminal,1,QueryValueType.Int,QueryValueType.Int,QueryCardinality.Scalar,true,true,false),
        new(QueryOperatorIds.FirstOrDefault,QueryNodeKind.Terminal,1,QueryValueType.Int,QueryValueType.Int,QueryCardinality.Scalar,false,true,false),
        new(QueryOperatorIds.Last,QueryNodeKind.Terminal,1,QueryValueType.Int,QueryValueType.Int,QueryCardinality.Scalar,true,true,false),
        new(QueryOperatorIds.LastOrDefault,QueryNodeKind.Terminal,1,QueryValueType.Int,QueryValueType.Int,QueryCardinality.Scalar,false,true,false),
        new(QueryOperatorIds.ElementAt,QueryNodeKind.Terminal,1,QueryValueType.Int,QueryValueType.Int,QueryCardinality.Scalar,true,true,false),
        new(QueryOperatorIds.ElementAtOrDefault,QueryNodeKind.Terminal,1,QueryValueType.Int,QueryValueType.Int,QueryCardinality.Scalar,false,true,false),
        new(QueryOperatorIds.ToArray,QueryNodeKind.Terminal,1,QueryValueType.Int,QueryValueType.Int,QueryCardinality.Sequence,false,false,true),
        new(QueryOperatorIds.ToList,QueryNodeKind.Terminal,1,QueryValueType.Int,QueryValueType.Int,QueryCardinality.Sequence,false,false,true),
        new(QueryOperatorIds.ToHashSet,QueryNodeKind.Terminal,1,QueryValueType.Int,QueryValueType.Int,QueryCardinality.Sequence,false,false,false),
        new(QueryOperatorIds.ToDictionary,QueryNodeKind.Terminal,1,QueryValueType.Int,QueryValueType.IntPair,QueryCardinality.Sequence,false,false,false,true),
        new(QueryOperatorIds.ToLookup,QueryNodeKind.Terminal,1,QueryValueType.Int,QueryValueType.IntGroup,QueryCardinality.Sequence,false,false,false),
        new(QueryOperatorIds.CountWhere,QueryNodeKind.Terminal,1,QueryValueType.Int,QueryValueType.Int,QueryCardinality.Scalar,false,false,false),
        new(QueryOperatorIds.AnyWhere,QueryNodeKind.Terminal,1,QueryValueType.Int,QueryValueType.Bool,QueryCardinality.Scalar,false,false,false),
        new(QueryOperatorIds.AggregateSum,QueryNodeKind.Terminal,1,QueryValueType.Int,QueryValueType.Long,QueryCardinality.Scalar,false,false,false),
        new(QueryOperatorIds.AverageSelector,QueryNodeKind.Terminal,1,QueryValueType.Int,QueryValueType.Double,QueryCardinality.Scalar,true,false,false),
        new(QueryOperatorIds.CountBy,QueryNodeKind.Terminal,1,QueryValueType.Int,QueryValueType.Int,QueryCardinality.Sequence,false,false,false),
        new(QueryOperatorIds.AggregateBy,QueryNodeKind.Terminal,1,QueryValueType.Int,QueryValueType.Int,QueryCardinality.Sequence,false,false,false),
        new(QueryOperatorIds.SequenceEqual,QueryNodeKind.Terminal,1,QueryValueType.Int,QueryValueType.Bool,QueryCardinality.Scalar,false,true,false),
    ];
    private static readonly IReadOnlyList<OperatorDescriptor> ReadOnlyDescriptors=Array.AsReadOnly(DescriptorArray);
    private static readonly IReadOnlyDictionary<string,OperatorDescriptor> ById=DescriptorArray.ToDictionary(
        static descriptor=>descriptor.OperatorId,
        StringComparer.Ordinal);

    public static IReadOnlyList<OperatorDescriptor> All=>ReadOnlyDescriptors;
    public static IReadOnlyList<OperatorDescriptor> Initial=>ReadOnlyDescriptors;

    public static OperatorDescriptor Get(string operatorId){
        ArgumentException.ThrowIfNullOrWhiteSpace(operatorId);
        if(!ById.TryGetValue(operatorId,out var descriptor)){
            throw new KeyNotFoundException($"Unknown operator ID: {operatorId}.");
        }
        return descriptor;
    }

    public static bool TryGet(string operatorId,out OperatorDescriptor descriptor){
        ArgumentException.ThrowIfNullOrWhiteSpace(operatorId);
        return ById.TryGetValue(operatorId,out descriptor!);
    }
}
