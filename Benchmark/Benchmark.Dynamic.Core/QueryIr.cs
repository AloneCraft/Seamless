using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Benchmark.Dynamic.Core;

public enum QueryNodeKind{
    Source,
    Unary,
    Binary,
    Nested,
    Terminal,
}

public enum QueryPredicateKind{
    IsEven,
    IsOdd,
    GreaterThan,
    LessThan,
    ModuloEquals,
}

public enum QuerySelectorKind{
    Identity,
    Add,
    Multiply,
    Negate,
}

public enum QueryBinaryKind{
    Union,
    Concat,
    Except,
    Intersect,
    UnionByIdentity,
    ExceptByIdentity,
    IntersectByIdentity,
}

public enum QueryNestedKind{
    SelectManyXor,
    JoinXor,
    GroupJoinXorCount,
}

public enum QueryTerminalKind{
    Count,
    LongCount,
    Any,
    All,
    Contains,
    Sum,
    Min,
    Max,
    MinBy,
    MaxBy,
    Average,
    AggregateXor,
    ToIntPair,
    ToIntGroup,
    First,
    FirstOrDefault,
    Last,
    LastOrDefault,
    ElementAt,
    ElementAtOrDefault,
    ToArray,
    ToList,
    ToHashSet,
    ToDictionary,
    ToLookup,
}

public sealed record QueryPredicate(QueryPredicateKind Kind,int Operand=0,int Modulus=2);
public sealed record QuerySelector(QuerySelectorKind Kind,int Operand=0);

[JsonPolymorphic(TypeDiscriminatorPropertyName="$arguments")]
[JsonDerivedType(typeof(WhereOperatorArguments),"where")]
[JsonDerivedType(typeof(WhereIndexedOperatorArguments),"whereIndexed")]
[JsonDerivedType(typeof(SelectOperatorArguments),"select")]
[JsonDerivedType(typeof(SelectIndexedOperatorArguments),"selectIndexed")]
[JsonDerivedType(typeof(DistinctOperatorArguments),"distinct")]
[JsonDerivedType(typeof(DistinctByOperatorArguments),"distinctBy")]
[JsonDerivedType(typeof(TakeOperatorArguments),"take")]
[JsonDerivedType(typeof(SkipOperatorArguments),"skip")]
[JsonDerivedType(typeof(ReverseOperatorArguments),"reverse")]
[JsonDerivedType(typeof(OrderByOperatorArguments),"orderBy")]
[JsonDerivedType(typeof(ThenByOperatorArguments),"thenBy")]
public abstract record UnaryOperatorArguments;

public sealed record WhereOperatorArguments(QueryPredicate Predicate):UnaryOperatorArguments;
public sealed record WhereIndexedOperatorArguments(int Modulus,int Remainder):UnaryOperatorArguments;
public sealed record SelectOperatorArguments(QuerySelector Selector):UnaryOperatorArguments;
public sealed record SelectIndexedOperatorArguments:UnaryOperatorArguments;
public sealed record DistinctOperatorArguments:UnaryOperatorArguments;
public sealed record DistinctByOperatorArguments(int Modulus):UnaryOperatorArguments;
public sealed record TakeOperatorArguments(int Count):UnaryOperatorArguments;
public sealed record SkipOperatorArguments(int Count):UnaryOperatorArguments;
public sealed record ReverseOperatorArguments:UnaryOperatorArguments;
public sealed record OrderByOperatorArguments(bool Descending):UnaryOperatorArguments;
public sealed record ThenByOperatorArguments(bool Descending):UnaryOperatorArguments;

public sealed record BinaryOperatorArguments(QueryBinaryKind Kind);
public sealed record NestedOperatorArguments(QueryNestedKind Kind);
public sealed record TerminalOperatorArguments(QueryTerminalKind Kind,QueryPredicate? Predicate=null,int? Value=null);

[JsonPolymorphic(TypeDiscriminatorPropertyName="$node")]
[JsonDerivedType(typeof(SourceNode),"source")]
[JsonDerivedType(typeof(UnaryQueryNode),"unary")]
[JsonDerivedType(typeof(BinaryQueryNode),"binary")]
[JsonDerivedType(typeof(NestedQueryNode),"nested")]
[JsonDerivedType(typeof(TerminalQueryNode),"terminal")]
public abstract record QueryNode(
    string NodeId,
    string OperatorId,
    QueryValueType OutputType,
    QueryCardinality OutputCardinality,
    QueryCardinalityRange Cardinality,
    QueryOrderSemantics OrderSemantics,
    QueryEmptySafety EmptySafety){
    [JsonIgnore]public abstract QueryNodeKind NodeKind{get;}
    [JsonIgnore]public abstract int OperatorCount{get;}
    [JsonIgnore]public abstract int Depth{get;}
    [JsonIgnore]public abstract IReadOnlyList<QueryNode> Children{get;}
}

public sealed record SourceNode(
    string NodeId,
    SourceTarget SourceTarget,
    SourceSlot Slot,
    QueryCardinalityRange Cardinality,
    QueryOrderSemantics OrderSemantics)
    :QueryNode(NodeId,QueryOperatorIds.Source,QueryValueType.Int,QueryCardinality.Sequence,Cardinality,OrderSemantics,QueryEmptySafety.Safe){
    private static readonly IReadOnlyList<QueryNode> EmptyChildren=Array.Empty<QueryNode>();
    [JsonIgnore]public override QueryNodeKind NodeKind=>QueryNodeKind.Source;
    [JsonIgnore]public override int OperatorCount=>0;
    [JsonIgnore]public override int Depth=>0;
    [JsonIgnore]public override IReadOnlyList<QueryNode> Children=>EmptyChildren;
}

public sealed record UnaryQueryNode(
    string NodeId,
    string OperatorId,
    QueryNode Input,
    UnaryOperatorArguments Arguments,
    QueryValueType OutputType,
    QueryCardinalityRange Cardinality,
    QueryOrderSemantics OrderSemantics)
    :QueryNode(NodeId,OperatorId,OutputType,QueryCardinality.Sequence,Cardinality,OrderSemantics,QueryEmptySafety.Safe){
    [JsonIgnore]public override QueryNodeKind NodeKind=>QueryNodeKind.Unary;
    [JsonIgnore]public override int OperatorCount=>1+this.Input.OperatorCount;
    [JsonIgnore]public override int Depth=>1+this.Input.Depth;
    [JsonIgnore]public override IReadOnlyList<QueryNode> Children=>[this.Input];
}

public sealed record BinaryQueryNode(
    string NodeId,
    string OperatorId,
    QueryNode Left,
    QueryNode Right,
    BinaryOperatorArguments Arguments,
    QueryValueType OutputType,
    QueryCardinalityRange Cardinality,
    QueryOrderSemantics OrderSemantics)
    :QueryNode(NodeId,OperatorId,OutputType,QueryCardinality.Sequence,Cardinality,OrderSemantics,QueryEmptySafety.Safe){
    [JsonIgnore]public override QueryNodeKind NodeKind=>QueryNodeKind.Binary;
    [JsonIgnore]public override int OperatorCount=>1+this.Left.OperatorCount+this.Right.OperatorCount;
    [JsonIgnore]public override int Depth=>1+Math.Max(this.Left.Depth,this.Right.Depth);
    [JsonIgnore]public override IReadOnlyList<QueryNode> Children=>[this.Left,this.Right];
}

public sealed record NestedQueryNode(
    string NodeId,
    string OperatorId,
    QueryNode Outer,
    QueryNode Inner,
    NestedOperatorArguments Arguments,
    QueryValueType OutputType,
    QueryCardinalityRange Cardinality,
    QueryOrderSemantics OrderSemantics)
    :QueryNode(NodeId,OperatorId,OutputType,QueryCardinality.Sequence,Cardinality,OrderSemantics,QueryEmptySafety.Safe){
    [JsonIgnore]public override QueryNodeKind NodeKind=>QueryNodeKind.Nested;
    [JsonIgnore]public override int OperatorCount=>1+this.Outer.OperatorCount+this.Inner.OperatorCount;
    [JsonIgnore]public override int Depth=>1+Math.Max(this.Outer.Depth,this.Inner.Depth);
    [JsonIgnore]public override IReadOnlyList<QueryNode> Children=>[this.Outer,this.Inner];
}

public sealed record TerminalQueryNode(
    string NodeId,
    string OperatorId,
    QueryNode Input,
    TerminalOperatorArguments Arguments,
    QueryValueType OutputType,
    QueryCardinality OutputCardinality,
    QueryCardinalityRange Cardinality,
    QueryOrderSemantics OrderSemantics,
    QueryEmptySafety EmptySafety)
    :QueryNode(NodeId,OperatorId,OutputType,OutputCardinality,Cardinality,OrderSemantics,EmptySafety){
    [JsonIgnore]public override QueryNodeKind NodeKind=>QueryNodeKind.Terminal;
    [JsonIgnore]public override int OperatorCount=>1+this.Input.OperatorCount;
    [JsonIgnore]public override int Depth=>1+this.Input.Depth;
    [JsonIgnore]public override IReadOnlyList<QueryNode> Children=>[this.Input];
}

public sealed record QueryIrDocument(
    int IrSchemaVersion,
    string QueryId,
    Seed256 Seed,
    int RequestedOperatorCount,
    int MaximumDepth,
    SourceTarget SourceTarget,
    TerminalQueryNode Root,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)]
    QueryElementType ElementType=QueryElementType.Int){
    public const int CurrentSchemaVersion=1;
    [JsonIgnore]public int ActualOperatorCount=>this.Root.OperatorCount;
    [JsonIgnore]public int ActualDepth=>this.Root.Depth;
}

public sealed class QueryIrValidationException(IReadOnlyList<string> errors)
    :InvalidOperationException(string.Join(Environment.NewLine,errors)){
    public IReadOnlyList<string> Errors{get;}=errors;
}

public static class QueryIrValidator{
    public const long MaximumSafeCardinality=100_000;

    public static IReadOnlyList<string> Validate(QueryIrDocument document){
        ArgumentNullException.ThrowIfNull(document);
        var errors=new List<string>();
        if(document.IrSchemaVersion!=QueryIrDocument.CurrentSchemaVersion){
            errors.Add($"Unsupported IR schema version: {document.IrSchemaVersion}.");
        }
        if(string.IsNullOrWhiteSpace(document.QueryId)){
            errors.Add("QueryId is required.");
        }
        if(document.RequestedOperatorCount<1){
            errors.Add("RequestedOperatorCount must be at least one.");
        }
        if(document.Root.OperatorCount!=document.RequestedOperatorCount){
            errors.Add($"Expected exactly {document.RequestedOperatorCount} operators but found {document.Root.OperatorCount}.");
        }
        if(document.MaximumDepth<1||document.Root.Depth>document.MaximumDepth){
            errors.Add($"Query depth {document.Root.Depth} exceeds maximum depth {document.MaximumDepth}.");
        }
        var nodeIds=new HashSet<string>(StringComparer.Ordinal);
        ValidateNode(document.Root,document.SourceTarget,nodeIds,errors);
        return errors;
    }

    public static IReadOnlyList<string> ValidateForExecution(
        QueryIrDocument document,
        int inputElementCount){
        var errors=Validate(document).ToList();
        if(inputElementCount<1||inputElementCount>MaximumSafeCardinality){
            errors.Add($"Input element count {inputElementCount} is outside 1..{MaximumSafeCardinality}.");
            return errors;
        }
        var expected=QueryCardinalityRange.Exact(inputElementCount);
        foreach(var source in Enumerate(document.Root).OfType<SourceNode>()){
            if(source.Cardinality!=expected){
                errors.Add($"Source node {source.NodeId} cardinality {source.Cardinality} does not match input count {inputElementCount}.");
            }
        }
        return errors;
    }

    public static void ValidateForExecutionAndThrow(QueryIrDocument document,int inputElementCount){
        var errors=ValidateForExecution(document,inputElementCount);
        if(errors.Count!=0){
            throw new QueryIrValidationException(errors);
        }
    }

    public static void ValidateAndThrow(QueryIrDocument document){
        var errors=Validate(document);
        if(errors.Count!=0){
            throw new QueryIrValidationException(errors);
        }
    }

    private static void ValidateNode(QueryNode node,SourceTarget sourceTarget,HashSet<string> nodeIds,List<string> errors){
        if(string.IsNullOrWhiteSpace(node.NodeId)||!nodeIds.Add(node.NodeId)){
            errors.Add($"Node ID is empty or duplicated: '{node.NodeId}'.");
        }
        if(node.OutputCardinality==QueryCardinality.Sequence){
            if(node.Cardinality.Maximum is not { }maximum||maximum>MaximumSafeCardinality){
                errors.Add($"Sequence node {node.NodeId} has an unbounded or unsafe maximum cardinality: {node.Cardinality.Maximum?.ToString()??"unbounded"}.");
            }
        }else if(node.Cardinality!=QueryCardinalityRange.Exact(1)){
            errors.Add($"Scalar node {node.NodeId} must have cardinality 1..1.");
        }
        switch(node){
            case SourceNode source:
                if(source.SourceTarget!=sourceTarget){
                    errors.Add($"Source node {source.NodeId} targets {source.SourceTarget}, expected {sourceTarget}.");
                }
                if(source.OutputType!=QueryValueType.Int||source.OutputCardinality!=QueryCardinality.Sequence){
                    errors.Add($"Source node {source.NodeId} must be an Int sequence.");
                }
                ValidateMetadata(
                    source,
                    source.Cardinality,
                    source.SourceTarget.GetInitialOrder(),
                    QueryEmptySafety.Safe,
                    errors);
                break;
            case UnaryQueryNode unary:
                ValidateNode(unary.Input,sourceTarget,nodeIds,errors);
                ValidateUnary(unary,errors);
                break;
            case BinaryQueryNode binary:
                ValidateNode(binary.Left,sourceTarget,nodeIds,errors);
                ValidateNode(binary.Right,sourceTarget,nodeIds,errors);
                var expectedBinaryOperator=binary.Arguments.Kind switch{
                    QueryBinaryKind.Concat=>QueryOperatorIds.Concat,
                    QueryBinaryKind.Union=>QueryOperatorIds.Union,
                    QueryBinaryKind.Except=>QueryOperatorIds.Except,
                    QueryBinaryKind.Intersect=>QueryOperatorIds.Intersect,
                    QueryBinaryKind.UnionByIdentity=>QueryOperatorIds.UnionByIdentity,
                    QueryBinaryKind.ExceptByIdentity=>QueryOperatorIds.ExceptByIdentity,
                    QueryBinaryKind.IntersectByIdentity=>QueryOperatorIds.IntersectByIdentity,
                    _=>null,
                };
                if(expectedBinaryOperator is null||binary.OperatorId!=expectedBinaryOperator){
                    errors.Add($"Binary node {binary.NodeId} has unsupported or mismatched operator {binary.OperatorId}/{binary.Arguments.Kind}.");
                }
                if(binary.Left.OutputCardinality!=QueryCardinality.Sequence||binary.Right.OutputCardinality!=QueryCardinality.Sequence){
                    errors.Add($"Binary node {binary.NodeId} requires two sequences.");
                }
                if(binary.Left.OutputType!=QueryValueType.Int||binary.Right.OutputType!=QueryValueType.Int||binary.OutputType!=QueryValueType.Int){
                    errors.Add($"Binary node {binary.NodeId} currently supports Int only.");
                }
                var sum=QueryCardinalityRange.Add(binary.Left.Cardinality,binary.Right.Cardinality);
                var binaryCardinality=binary.Arguments.Kind switch{
                    QueryBinaryKind.Concat=>sum,
                    QueryBinaryKind.Union or QueryBinaryKind.UnionByIdentity=>new QueryCardinalityRange(0,sum.Maximum),
                    QueryBinaryKind.Except or QueryBinaryKind.ExceptByIdentity=>new QueryCardinalityRange(0,binary.Left.Cardinality.Maximum),
                    QueryBinaryKind.Intersect or QueryBinaryKind.IntersectByIdentity=>new QueryCardinalityRange(
                        0,
                        binary.Left.Cardinality.Maximum is { }leftMaximum&&binary.Right.Cardinality.Maximum is { }rightMaximum
                            ?Math.Min(leftMaximum,rightMaximum)
                            :binary.Left.Cardinality.Maximum??binary.Right.Cardinality.Maximum),
                    _=>binary.Cardinality,
                };
                var binaryOrder=binary.Arguments.Kind==QueryBinaryKind.Concat
                    &&binary.Left.OrderSemantics!=QueryOrderSemantics.Unordered
                    &&binary.Right.OrderSemantics!=QueryOrderSemantics.Unordered
                        ?QueryOrderSemantics.Ordered
                        :QueryOrderSemantics.Unordered;
                ValidateMetadata(binary,binaryCardinality,binaryOrder,QueryEmptySafety.Safe,errors);
                break;
            case NestedQueryNode nested:
                ValidateNode(nested.Outer,sourceTarget,nodeIds,errors);
                ValidateNode(nested.Inner,sourceTarget,nodeIds,errors);
                var expectedNestedOperator=nested.Arguments.Kind switch{
                    QueryNestedKind.SelectManyXor=>QueryOperatorIds.SelectManyXor,
                    QueryNestedKind.JoinXor=>QueryOperatorIds.JoinXor,
                    QueryNestedKind.GroupJoinXorCount=>QueryOperatorIds.GroupJoinXorCount,
                    _=>null,
                };
                if(expectedNestedOperator is null||nested.OperatorId!=expectedNestedOperator){
                    errors.Add($"Nested node {nested.NodeId} has unsupported or mismatched operator {nested.OperatorId}/{nested.Arguments.Kind}.");
                }
                if(nested.Outer.OutputType!=QueryValueType.Int||nested.Inner.OutputType!=QueryValueType.Int||nested.OutputType!=QueryValueType.Int){
                    errors.Add($"Nested node {nested.NodeId} currently supports Int only.");
                }
                var product=QueryCardinalityRange.Multiply(nested.Outer.Cardinality,nested.Inner.Cardinality);
                var nestedCardinality=nested.Arguments.Kind switch{
                    QueryNestedKind.JoinXor=>new QueryCardinalityRange(0,product.Maximum),
                    QueryNestedKind.SelectManyXor=>product,
                    QueryNestedKind.GroupJoinXorCount=>nested.Outer.Cardinality,
                    _=>nested.Cardinality,
                };
                var nestedOrder=nested.Outer.OrderSemantics==QueryOrderSemantics.Unordered
                    ||nested.Inner.OrderSemantics==QueryOrderSemantics.Unordered
                        ?QueryOrderSemantics.Unordered
                        :QueryOrderSemantics.Ordered;
                ValidateMetadata(nested,nestedCardinality,nestedOrder,QueryEmptySafety.Safe,errors);
                break;
            case TerminalQueryNode terminal:
                ValidateNode(terminal.Input,sourceTarget,nodeIds,errors);
                ValidateTerminal(terminal,errors);
                break;
            default:
                errors.Add($"Unknown query node type: {node.GetType().FullName}.");
                break;
        }
    }

    private static void ValidateUnary(UnaryQueryNode node,List<string> errors){
        if(node.Input.OutputCardinality!=QueryCardinality.Sequence||node.Input.OutputType!=QueryValueType.Int||node.OutputType!=QueryValueType.Int){
            errors.Add($"Unary node {node.NodeId} currently requires and returns an Int sequence.");
        }
        switch(node.Arguments){
            case WhereOperatorArguments where when where.Predicate.Kind==QueryPredicateKind.ModuloEquals&&where.Predicate.Modulus<1:
                errors.Add($"Where node {node.NodeId} requires a positive modulus.");
                break;
            case WhereOperatorArguments:
            case WhereIndexedOperatorArguments {Modulus:>0,Remainder:>=0} indexed when indexed.Remainder<indexed.Modulus:
            case SelectOperatorArguments:
            case SelectIndexedOperatorArguments:
            case DistinctOperatorArguments:
            case DistinctByOperatorArguments {Modulus:>0}:
                break;
            case TakeOperatorArguments take when take.Count<0:
                errors.Add($"Take node {node.NodeId} requires a non-negative count.");
                break;
            case TakeOperatorArguments:
            case SkipOperatorArguments {Count:>=0}:
            case ReverseOperatorArguments:
            case OrderByOperatorArguments:
            case ThenByOperatorArguments when node.Input.OrderSemantics==QueryOrderSemantics.TotalOrdered:
                break;
            case ThenByOperatorArguments:
                errors.Add($"ThenBy node {node.NodeId} requires a total-ordered input.");
                break;
            default:
                errors.Add($"Unary node {node.NodeId} has unsupported arguments.");
                break;
        }
        var expectedOperator=node.Arguments switch{
            WhereOperatorArguments=>QueryOperatorIds.Where,
            WhereIndexedOperatorArguments=>QueryOperatorIds.WhereIndexed,
            SelectOperatorArguments=>QueryOperatorIds.Select,
            SelectIndexedOperatorArguments=>QueryOperatorIds.SelectIndexed,
            DistinctOperatorArguments=>QueryOperatorIds.Distinct,
            DistinctByOperatorArguments=>QueryOperatorIds.DistinctBy,
            TakeOperatorArguments=>QueryOperatorIds.Take,
            SkipOperatorArguments=>QueryOperatorIds.Skip,
            ReverseOperatorArguments=>QueryOperatorIds.Reverse,
            OrderByOperatorArguments=>QueryOperatorIds.OrderBy,
            ThenByOperatorArguments=>QueryOperatorIds.ThenBy,
            _=>null,
        };
        if(expectedOperator is null||node.OperatorId!=expectedOperator){
            errors.Add($"Unary node {node.NodeId} has mismatched operator ID {node.OperatorId}.");
        }
        var expectedCardinality=node.Arguments switch{
            WhereOperatorArguments or WhereIndexedOperatorArguments=>new QueryCardinalityRange(0,node.Input.Cardinality.Maximum),
            SelectOperatorArguments or SelectIndexedOperatorArguments or ReverseOperatorArguments
                or OrderByOperatorArguments or ThenByOperatorArguments=>node.Input.Cardinality,
            DistinctOperatorArguments=>new QueryCardinalityRange(
                node.Input.Cardinality.Minimum>0?1:0,
                node.Input.Cardinality.Maximum),
            DistinctByOperatorArguments distinctBy=>new QueryCardinalityRange(
                node.Input.Cardinality.Minimum>0?1:0,
                node.Input.Cardinality.Maximum is { }maximum?Math.Min(maximum,distinctBy.Modulus):distinctBy.Modulus),
            TakeOperatorArguments take=>new QueryCardinalityRange(
                Math.Min(node.Input.Cardinality.Minimum,take.Count),
                node.Input.Cardinality.Maximum is { }maximum?Math.Min(maximum,take.Count):take.Count),
            SkipOperatorArguments skip=>new QueryCardinalityRange(
                Math.Max(0,node.Input.Cardinality.Minimum-skip.Count),
                node.Input.Cardinality.Maximum is { }maximum?Math.Max(0,maximum-skip.Count):null),
            _=>node.Cardinality,
        };
        var expectedOrder=node.Arguments is DistinctOperatorArguments or DistinctByOperatorArguments
            ?QueryOrderSemantics.Unordered
            :node.Arguments is OrderByOperatorArguments or ThenByOperatorArguments
                ?QueryOrderSemantics.TotalOrdered
                :node.Input.OrderSemantics;
        ValidateMetadata(node,expectedCardinality,expectedOrder,QueryEmptySafety.Safe,errors);
    }

    private static void ValidateTerminal(TerminalQueryNode node,List<string> errors){
        if(node.Input.OutputCardinality!=QueryCardinality.Sequence||node.Input.OutputType!=QueryValueType.Int){
            errors.Add($"Terminal node {node.NodeId} currently requires an Int sequence.");
        }
        var expected=GetTerminalShape(node.Arguments.Kind);
        var expectedOperator=GetTerminalOperatorId(node.Arguments.Kind);
        if(node.OperatorId!=expectedOperator){
            errors.Add($"Terminal node {node.NodeId} has mismatched operator ID {node.OperatorId}, expected {expectedOperator}.");
        }
        if(node.OutputType!=expected.Type||node.OutputCardinality!=expected.Cardinality){
            errors.Add($"Terminal node {node.NodeId} has output {node.OutputCardinality}/{node.OutputType}, expected {expected.Cardinality}/{expected.Type}.");
        }
        if(node.Arguments.Kind is QueryTerminalKind.Min or QueryTerminalKind.Max or QueryTerminalKind.MinBy or QueryTerminalKind.MaxBy or QueryTerminalKind.Average
            or QueryTerminalKind.First or QueryTerminalKind.Last or QueryTerminalKind.ElementAt){
            if(!node.Input.Cardinality.IsDefinitelyNonEmpty){
                errors.Add($"Terminal node {node.NodeId} requires a statically non-empty input.");
            }
        }
        if(node.Arguments.Kind==QueryTerminalKind.All&&node.Arguments.Predicate is null){
            errors.Add($"All terminal {node.NodeId} requires a predicate.");
        }
        if(node.Arguments.Kind==QueryTerminalKind.Contains&&node.Arguments.Value is null){
            errors.Add($"Contains terminal {node.NodeId} requires a value.");
        }
        if(node.Arguments.Kind is QueryTerminalKind.ElementAt or QueryTerminalKind.ElementAtOrDefault){
            if(node.Arguments.Value is not { }index||index<0){
                errors.Add($"ElementAt terminal {node.NodeId} requires a non-negative index.");
            }else if(node.Arguments.Kind==QueryTerminalKind.ElementAt&&index>=node.Input.Cardinality.Minimum){
                errors.Add($"ElementAt terminal {node.NodeId} index {index} is outside the statically safe minimum cardinality.");
            }
        }
        if(node.Arguments.Kind==QueryTerminalKind.ToDictionary&&!QuerySequenceSemantics.HasUniqueValues(node.Input)){
            errors.Add($"ToDictionary terminal {node.NodeId} requires an input whose values are statically unique.");
        }
        var expectedCardinality=node.OutputCardinality==QueryCardinality.Scalar
            ?QueryCardinalityRange.Exact(1)
            :node.Arguments.Kind==QueryTerminalKind.ToLookup
                ?new QueryCardinalityRange(node.Input.Cardinality.Minimum>0?1:0,Math.Min(node.Input.Cardinality.Maximum??4,4))
                :node.Arguments.Kind==QueryTerminalKind.ToHashSet
                    ?new QueryCardinalityRange(node.Input.Cardinality.Minimum>0?1:0,node.Input.Cardinality.Maximum)
                    :node.Input.Cardinality;
        var expectedOrder=node.OutputCardinality==QueryCardinality.Scalar
            ?QueryOrderSemantics.TotalOrdered
            :node.Arguments.Kind==QueryTerminalKind.ToHashSet
                ?QueryOrderSemantics.Unordered
                :node.Arguments.Kind is QueryTerminalKind.ToDictionary or QueryTerminalKind.ToLookup
                    ?QueryOrderSemantics.TotalOrdered
                    :node.Input.OrderSemantics;
        var expectedEmptySafety=node.Arguments.Kind is QueryTerminalKind.Min or QueryTerminalKind.Max
            or QueryTerminalKind.MinBy or QueryTerminalKind.MaxBy or QueryTerminalKind.Average
            or QueryTerminalKind.First or QueryTerminalKind.Last or QueryTerminalKind.ElementAt
                ?QueryEmptySafety.RequiresNonEmpty
                :QueryEmptySafety.Safe;
        ValidateMetadata(node,expectedCardinality,expectedOrder,expectedEmptySafety,errors);
    }

    private static void ValidateMetadata(
        QueryNode node,
        QueryCardinalityRange expectedCardinality,
        QueryOrderSemantics expectedOrder,
        QueryEmptySafety expectedEmptySafety,
        List<string> errors){
        if(node.Cardinality!=expectedCardinality){
            errors.Add($"Node {node.NodeId} cardinality {node.Cardinality} does not match expected {expectedCardinality}.");
        }
        if(node.OrderSemantics!=expectedOrder){
            errors.Add($"Node {node.NodeId} order {node.OrderSemantics} does not match expected {expectedOrder}.");
        }
        if(node.EmptySafety!=expectedEmptySafety){
            errors.Add($"Node {node.NodeId} empty safety {node.EmptySafety} does not match expected {expectedEmptySafety}.");
        }
    }

    private static IEnumerable<QueryNode> Enumerate(QueryNode node){
        yield return node;
        foreach(var child in node.Children){
            foreach(var descendant in Enumerate(child)){
                yield return descendant;
            }
        }
    }

    internal static (QueryValueType Type,QueryCardinality Cardinality)GetTerminalShape(QueryTerminalKind kind){
        return kind switch{
            QueryTerminalKind.Count=>(QueryValueType.Int,QueryCardinality.Scalar),
            QueryTerminalKind.LongCount or QueryTerminalKind.Sum=>(QueryValueType.Long,QueryCardinality.Scalar),
            QueryTerminalKind.Any or QueryTerminalKind.All or QueryTerminalKind.Contains=>(QueryValueType.Bool,QueryCardinality.Scalar),
            QueryTerminalKind.Average=>(QueryValueType.Double,QueryCardinality.Scalar),
            QueryTerminalKind.ToIntPair=>(QueryValueType.IntPair,QueryCardinality.Scalar),
            QueryTerminalKind.ToIntGroup=>(QueryValueType.IntGroup,QueryCardinality.Scalar),
            QueryTerminalKind.ToDictionary=>(QueryValueType.IntPair,QueryCardinality.Sequence),
            QueryTerminalKind.ToLookup=>(QueryValueType.IntGroup,QueryCardinality.Sequence),
            QueryTerminalKind.ToArray or QueryTerminalKind.ToList or QueryTerminalKind.ToHashSet=>(QueryValueType.Int,QueryCardinality.Sequence),
            _=>(QueryValueType.Int,QueryCardinality.Scalar),
        };
    }

    private static string GetTerminalOperatorId(QueryTerminalKind kind){
        return kind switch{
            QueryTerminalKind.Count=>QueryOperatorIds.Count,
            QueryTerminalKind.LongCount=>QueryOperatorIds.LongCount,
            QueryTerminalKind.Any=>QueryOperatorIds.Any,
            QueryTerminalKind.All=>QueryOperatorIds.All,
            QueryTerminalKind.Contains=>QueryOperatorIds.Contains,
            QueryTerminalKind.Sum=>QueryOperatorIds.Sum,
            QueryTerminalKind.Min=>QueryOperatorIds.Min,
            QueryTerminalKind.Max=>QueryOperatorIds.Max,
            QueryTerminalKind.MinBy=>QueryOperatorIds.MinBy,
            QueryTerminalKind.MaxBy=>QueryOperatorIds.MaxBy,
            QueryTerminalKind.Average=>QueryOperatorIds.Average,
            QueryTerminalKind.AggregateXor=>QueryOperatorIds.AggregateXor,
            QueryTerminalKind.ToIntPair=>QueryOperatorIds.ToIntPair,
            QueryTerminalKind.ToIntGroup=>QueryOperatorIds.ToIntGroup,
            QueryTerminalKind.First=>QueryOperatorIds.First,
            QueryTerminalKind.FirstOrDefault=>QueryOperatorIds.FirstOrDefault,
            QueryTerminalKind.Last=>QueryOperatorIds.Last,
            QueryTerminalKind.LastOrDefault=>QueryOperatorIds.LastOrDefault,
            QueryTerminalKind.ElementAt=>QueryOperatorIds.ElementAt,
            QueryTerminalKind.ElementAtOrDefault=>QueryOperatorIds.ElementAtOrDefault,
            QueryTerminalKind.ToArray=>QueryOperatorIds.ToArray,
            QueryTerminalKind.ToList=>QueryOperatorIds.ToList,
            QueryTerminalKind.ToHashSet=>QueryOperatorIds.ToHashSet,
            QueryTerminalKind.ToDictionary=>QueryOperatorIds.ToDictionary,
            QueryTerminalKind.ToLookup=>QueryOperatorIds.ToLookup,
            _=>throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }
}

public static class QuerySequenceSemantics{
    public static bool HasUniqueValues(QueryNode node){
        ArgumentNullException.ThrowIfNull(node);
        return node switch{
            SourceNode=>true,
            UnaryQueryNode unary=>unary.Arguments switch{
                SelectIndexedOperatorArguments=>false,
                DistinctOperatorArguments or DistinctByOperatorArguments=>true,
                WhereOperatorArguments or WhereIndexedOperatorArguments
                    or SelectOperatorArguments
                    or TakeOperatorArguments or SkipOperatorArguments or ReverseOperatorArguments
                    or OrderByOperatorArguments or ThenByOperatorArguments=>HasUniqueValues(unary.Input),
                _=>false,
            },
            BinaryQueryNode binary=>binary.Arguments.Kind is not QueryBinaryKind.Concat,
            NestedQueryNode=>false,
            TerminalQueryNode terminal=>HasUniqueValues(terminal.Input),
            _=>false,
        };
    }
}

public static class QueryIrJson{
    private static readonly JsonSerializerOptions SerializerOptions=CreateOptions();
    public static JsonSerializerOptions Options=>SerializerOptions;

    public static string Serialize(QueryIrDocument document){
        QueryIrValidator.ValidateAndThrow(document);
        return JsonSerializer.Serialize(document,SerializerOptions);
    }

    public static QueryIrDocument Deserialize(string json){
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var document=JsonSerializer.Deserialize<QueryIrDocument>(json,SerializerOptions)??throw new JsonException("Query IR JSON was null.");
        QueryIrValidator.ValidateAndThrow(document);
        return document;
    }

    public static string ComputeHash(QueryIrDocument document){
        var bytes=Encoding.UTF8.GetBytes(Serialize(document));
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }

    private static JsonSerializerOptions CreateOptions(){
        var options=new JsonSerializerOptions{
            PropertyNamingPolicy=JsonNamingPolicy.CamelCase,
            WriteIndented=false,
            UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}
