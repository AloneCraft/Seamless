using System.Text.Json;
using Benchmark.Dynamic.Core;

namespace Benchmark.Dynamic.Tests;

public sealed record CorpusEntry(string Id,string Stage,IReadOnlyList<string> Methods);

public sealed record CorpusFile(string Description,string Origin,IReadOnlyList<CorpusEntry> Entries);

/// <summary>Dynamic の規則では、そのチェーンを作れない(または FLinq が既知の理由で扱えない)ことを表す。</summary>
public sealed class CorpusChainNotConvertibleException(string reason):Exception(reason);

/// <summary>
/// 旧チェーンと同じ演算子の並びを使い、DynamicのIRを追加検査する。
/// 引数はIRで表現できる値へ置き換わるため、旧コードと同じ処理であることは保証しない。
/// 原式の回帰検査にはLegacyChainCatalogを使う(Selectの加算項やBy演算のキー射影も保持する)。
/// 移植元のキー射影つき演算(UnionBy など)は、Dynamic に無いので恒等キーの版にする(近似)。
/// 作り方は、固定した並びで骨組みを生成し、各演算子の引数を差し替えてから
/// <see cref="QueryIrNormalizer"/> で件数・順序を再計算して検証する。
/// 移植元と同じく、空の入力や範囲外の添字で例外になる並びも許す(<see cref="QueryIrDocument.ExpectExceptions"/>)。
/// </summary>
public static class CorpusChainBuilder{
    private sealed record Step(string OperatorId,QueryNodeKind Kind,UnaryOperatorArguments? Unary=null,TerminalOperatorArguments? Terminal=null);

    private static readonly QueryPredicate Even=new(QueryPredicateKind.IsEven);

    private static readonly IReadOnlyDictionary<string,Step> Steps=new Dictionary<string,Step>(StringComparer.Ordinal){
        // 単項(中間)
        ["Where"]=new(QueryOperatorIds.Where,QueryNodeKind.Unary,new WhereOperatorArguments(Even)),
        ["Select"]=new(QueryOperatorIds.Select,QueryNodeKind.Unary,new SelectOperatorArguments(new(QuerySelectorKind.Multiply,3))),
        ["Take"]=new(QueryOperatorIds.Take,QueryNodeKind.Unary,new TakeOperatorArguments(12)),
        ["Skip"]=new(QueryOperatorIds.Skip,QueryNodeKind.Unary,new SkipOperatorArguments(4)),
        ["TakeLast"]=new(QueryOperatorIds.TakeLast,QueryNodeKind.Unary,new TakeLastOperatorArguments(8)),
        ["SkipLast"]=new(QueryOperatorIds.SkipLast,QueryNodeKind.Unary,new SkipLastOperatorArguments(4)),
        ["TakeWhile"]=new(QueryOperatorIds.TakeWhile,QueryNodeKind.Unary,new TakeWhileOperatorArguments(new(QueryPredicateKind.LessThan,200))),
        ["SkipWhile"]=new(QueryOperatorIds.SkipWhile,QueryNodeKind.Unary,new SkipWhileOperatorArguments(new(QueryPredicateKind.LessThan,20))),
        ["Distinct"]=new(QueryOperatorIds.Distinct,QueryNodeKind.Unary,new DistinctOperatorArguments()),
        ["DistinctBy"]=new(QueryOperatorIds.DistinctBy,QueryNodeKind.Unary,new DistinctByOperatorArguments(32)),
        ["Append"]=new(QueryOperatorIds.Append,QueryNodeKind.Unary,new AppendOperatorArguments(77)),
        ["Prepend"]=new(QueryOperatorIds.Prepend,QueryNodeKind.Unary,new PrependOperatorArguments(11)),
        ["DefaultIfEmpty"]=new(QueryOperatorIds.DefaultIfEmpty,QueryNodeKind.Unary,new DefaultIfEmptyOperatorArguments(0)),
        ["Reverse"]=new(QueryOperatorIds.Reverse,QueryNodeKind.Unary,new ReverseOperatorArguments()),
        ["OrderBy"]=new(QueryOperatorIds.OrderByKey,QueryNodeKind.Unary,new OrderByKeyOperatorArguments(32,false)),
        ["OrderByDescending"]=new(QueryOperatorIds.OrderByKey,QueryNodeKind.Unary,new OrderByKeyOperatorArguments(32,true)),
        ["GroupBy_Sum"]=new(QueryOperatorIds.GroupBySum,QueryNodeKind.Unary,new GroupByAggregateOperatorArguments(GroupByAggregateKind.Sum,16)),
        ["GroupBy_Count"]=new(QueryOperatorIds.GroupByCount,QueryNodeKind.Unary,new GroupByAggregateOperatorArguments(GroupByAggregateKind.Count,16)),
        ["GroupBy_KeyOnly"]=new(QueryOperatorIds.GroupByKeyOnly,QueryNodeKind.Unary,new GroupByAggregateOperatorArguments(GroupByAggregateKind.KeyOnly,16)),
        // 二項(右側は副Source)
        ["Concat"]=new(QueryOperatorIds.Concat,QueryNodeKind.Binary),
        ["Union"]=new(QueryOperatorIds.Union,QueryNodeKind.Binary),
        ["Intersect"]=new(QueryOperatorIds.Intersect,QueryNodeKind.Binary),
        ["Except"]=new(QueryOperatorIds.Except,QueryNodeKind.Binary),
        ["UnionBy"]=new(QueryOperatorIds.UnionByIdentity,QueryNodeKind.Binary),
        ["IntersectBy"]=new(QueryOperatorIds.IntersectByIdentity,QueryNodeKind.Binary),
        ["ExceptBy"]=new(QueryOperatorIds.ExceptByIdentity,QueryNodeKind.Binary),
        ["Zip"]=new(QueryOperatorIds.Zip,QueryNodeKind.Binary),
        // 入れ子(内側は副Source)
        ["Join"]=new(QueryOperatorIds.JoinXor,QueryNodeKind.Nested),
        ["GroupJoin"]=new(QueryOperatorIds.GroupJoinXorCount,QueryNodeKind.Nested),
        // 終端
        ["ToArray"]=Terminal(QueryOperatorIds.ToArray,QueryTerminalKind.ToArray),
        ["ToList"]=Terminal(QueryOperatorIds.ToList,QueryTerminalKind.ToList),
        ["ToHashSet"]=Terminal(QueryOperatorIds.ToHashSet,QueryTerminalKind.ToHashSet),
        ["Count"]=Terminal(QueryOperatorIds.Count,QueryTerminalKind.Count),
        ["Count_Pred"]=Terminal(QueryOperatorIds.CountWhere,QueryTerminalKind.CountWhere,Even),
        ["LongCount"]=Terminal(QueryOperatorIds.LongCount,QueryTerminalKind.LongCount),
        ["Sum"]=Terminal(QueryOperatorIds.Sum,QueryTerminalKind.Sum),
        ["Min"]=Terminal(QueryOperatorIds.Min,QueryTerminalKind.Min),
        ["Max"]=Terminal(QueryOperatorIds.Max,QueryTerminalKind.Max),
        ["MinBy"]=Terminal(QueryOperatorIds.MinBy,QueryTerminalKind.MinBy),
        ["MaxBy"]=Terminal(QueryOperatorIds.MaxBy,QueryTerminalKind.MaxBy),
        ["Average"]=Terminal(QueryOperatorIds.AverageSelector,QueryTerminalKind.AverageSelector),
        ["Aggregate"]=Terminal(QueryOperatorIds.AggregateSum,QueryTerminalKind.AggregateSum),
        ["Any"]=Terminal(QueryOperatorIds.Any,QueryTerminalKind.Any),
        ["Any_Pred"]=Terminal(QueryOperatorIds.AnyWhere,QueryTerminalKind.AnyWhere,new QueryPredicate(QueryPredicateKind.ModuloEquals,0,7)),
        ["All"]=Terminal(QueryOperatorIds.All,QueryTerminalKind.All,new QueryPredicate(QueryPredicateKind.GreaterThan,-10001)),
        ["Contains"]=Terminal(QueryOperatorIds.Contains,QueryTerminalKind.Contains,value:10),
        ["First"]=Terminal(QueryOperatorIds.First,QueryTerminalKind.First),
        ["FirstOrDefault"]=Terminal(QueryOperatorIds.FirstOrDefault,QueryTerminalKind.FirstOrDefault),
        ["Last"]=Terminal(QueryOperatorIds.Last,QueryTerminalKind.Last),
        ["LastOrDefault"]=Terminal(QueryOperatorIds.LastOrDefault,QueryTerminalKind.LastOrDefault),
        ["ElementAt"]=Terminal(QueryOperatorIds.ElementAt,QueryTerminalKind.ElementAt,value:2),
        ["ElementAtOrDefault"]=Terminal(QueryOperatorIds.ElementAtOrDefault,QueryTerminalKind.ElementAtOrDefault,value:2),
        ["SequenceEqual"]=Terminal(QueryOperatorIds.SequenceEqual,QueryTerminalKind.SequenceEqual,value:0),
        ["CountBy_ToArray"]=Terminal(QueryOperatorIds.CountBy,QueryTerminalKind.CountBy,value:16),
        ["AggregateBy_ToArray"]=Terminal(QueryOperatorIds.AggregateBy,QueryTerminalKind.AggregateBy,value:16),
    };

    private static Step Terminal(string operatorId,QueryTerminalKind kind,QueryPredicate? predicate=null,int? value=null){
        return new(operatorId,QueryNodeKind.Terminal,Terminal:new TerminalOperatorArguments(kind,predicate,value));
    }

    public static CorpusFile Load(string fileName){
        var path=Path.Combine(AppContext.BaseDirectory,"Corpus",fileName);
        var options=new JsonSerializerOptions{PropertyNameCaseInsensitive=true};
        return JsonSerializer.Deserialize<CorpusFile>(File.ReadAllText(path),options)
            ??throw new InvalidDataException($"{fileName} is empty.");
    }

    /// <summary>作れないときは <see cref="CorpusChainNotConvertibleException"/>。</summary>
    public static QueryIrDocument Build(CorpusEntry entry,SourceTarget target,int inputElementCount){
        ArgumentNullException.ThrowIfNull(entry);
        if(entry.Methods.Contains("SelectMany")){
            throw new CorpusChainNotConvertibleException(
                "SelectMany: FLinq-generated code does not compile (CS1673), a known capability limit");
        }
        var steps=entry.Methods.Select(static name=>Steps.TryGetValue(name,out var step)
            ?step
            :throw new CorpusChainNotConvertibleException($"unknown method {name}")).ToArray();
        if(steps.Length<2||steps[^1].Kind!=QueryNodeKind.Terminal||steps[..^1].Any(static step=>step.Kind==QueryNodeKind.Terminal)){
            throw new CorpusChainNotConvertibleException("the chain must be intermediates followed by one terminal");
        }
        var intermediates=steps[..^1];
        QueryIrDocument skeleton;
        try{
            skeleton=new RandomQueryGenerator().Generate(new RandomQueryGenerationOptions{
                MasterSeed=Seed256.Parse("0xC0DE5"),
                QueryIndex=0,
                QueryId=entry.Id,
                SourceTarget=target,
                OperatorCount=steps.Length,
                MaximumDepth=steps.Length,
                InputElementCount=inputElementCount,
                EnableBinaryOperators=true,
                EnableNestedOperators=true,
                ForcedOperatorSequence=intermediates.Select(static step=>step.OperatorId).ToArray(),
                ForcedTerminalOperatorId=steps[^1].OperatorId,
                ExpectExceptions=true,
            });
        }catch(ForcedOperatorNotApplicableException exception){
            throw new CorpusChainNotConvertibleException("rejected by Dynamic's ordering rules: "+exception.Message);
        }
        var root=skeleton.Root with{
            Input=ReplaceArguments(skeleton.Root.Input,intermediates),
            Arguments=steps[^1].Terminal!,
        };
        try{
            return QueryIrNormalizer.Normalize(skeleton with{Root=root},inputElementCount);
        }catch(QueryIrValidationException exception){
            throw new CorpusChainNotConvertibleException("rejected by IR validation: "+exception.Message);
        }
    }

    private static QueryNode ReplaceArguments(QueryNode node,IReadOnlyList<Step> steps){
        return node switch{
            SourceNode=>node,
            UnaryQueryNode unary=>unary with{
                Input=ReplaceArguments(unary.Input,steps),
                Arguments=steps[unary.OperatorCount-1].Unary??unary.Arguments,
            },
            BinaryQueryNode binary=>binary with{Left=ReplaceArguments(binary.Left,steps)},
            NestedQueryNode nested=>nested with{Outer=ReplaceArguments(nested.Outer,steps)},
            _=>throw new InvalidOperationException($"Unexpected node {node.GetType().Name}."),
        };
    }
}
