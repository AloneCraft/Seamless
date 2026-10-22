using System.Globalization;

namespace Benchmark.Dynamic.Core;

public static class QueryExpressionFormatter{
    public static string Format(QueryIrDocument document){
        ArgumentNullException.ThrowIfNull(document);
        QueryIrValidator.ValidateAndThrow(document);
        return FormatNode(document.Root);
    }

    private static string FormatNode(QueryNode node){
        return node switch{
            SourceNode source=>FormatSource(source),
            UnaryQueryNode unary=>FormatUnary(unary),
            BinaryQueryNode binary=>FormatBinary(binary),
            NestedQueryNode nested=>FormatNested(nested),
            TerminalQueryNode terminal=>$"{FormatNode(terminal.Input)}.{FormatTerminal(terminal)}",
            _=>throw new ArgumentOutOfRangeException(nameof(node)),
        };
    }

    private static string FormatSource(SourceNode source){
        if(source.Slot==SourceSlot.Primary){
            return"source";
        }
        return"inner";
    }

    private static string FormatUnary(UnaryQueryNode node){
        var input=FormatNode(node.Input);
        var operation=node.Arguments switch{
            WhereOperatorArguments where=>$"Where({FormatPredicate(where.Predicate)})",
            WhereIndexedOperatorArguments where=>$"Where((p,i)=>i%{Integer(where.Modulus)}=={Integer(where.Remainder)})",
            SelectOperatorArguments select=>$"Select({FormatSelector(select.Selector)})",
            SelectIndexedOperatorArguments=>"Select((p,i)=>p^i)",
            DistinctOperatorArguments=>"Distinct()",
            DistinctByOperatorArguments distinctBy=>$"DistinctBy(p=>(p%{Integer(distinctBy.Modulus)}+{Integer(distinctBy.Modulus)})%{Integer(distinctBy.Modulus)})",
            TakeOperatorArguments take=>$"Take({Integer(take.Count)})",
            SkipOperatorArguments skip=>$"Skip({Integer(skip.Count)})",
            ReverseOperatorArguments=>"Reverse()",
            OrderByOperatorArguments order=>FormatOrderBy(order.Descending),
            ThenByOperatorArguments order=>FormatThenBy(order.Descending),
            TakeLastOperatorArguments takeLast=>$"TakeLast({Integer(takeLast.Count)})",
            SkipLastOperatorArguments skipLast=>$"SkipLast({Integer(skipLast.Count)})",
            TakeWhileOperatorArguments takeWhile=>$"TakeWhile({FormatPredicate(takeWhile.Predicate)})",
            SkipWhileOperatorArguments skipWhile=>$"SkipWhile({FormatPredicate(skipWhile.Predicate)})",
            AppendOperatorArguments append=>$"Append({Integer(append.Value)})",
            PrependOperatorArguments prepend=>$"Prepend({Integer(prepend.Value)})",
            DefaultIfEmptyOperatorArguments defaultIfEmpty=>$"DefaultIfEmpty({Integer(defaultIfEmpty.Value)})",
            OrderByKeyOperatorArguments orderBy=>FormatOrderByKey(orderBy),
            GroupByAggregateOperatorArguments group=>FormatGroupByAggregate(group),
            _=>throw new ArgumentOutOfRangeException(nameof(node)),
        };
        return$"{input}.{operation}";
    }

    private static string FormatOrderByKey(OrderByKeyOperatorArguments order){
        var method=order.Descending?"OrderByDescending":"OrderBy";
        if(order.Modulus==0){
            return$"{method}(p=>p)";
        }
        var modulus=Integer(order.Modulus);
        return$"{method}(p=>(p%{modulus}+{modulus})%{modulus})";
    }

    private static string FormatGroupByAggregate(GroupByAggregateOperatorArguments group){
        var modulus=Integer(group.Modulus);
        var key=$"p=>(p%{modulus}+{modulus})%{modulus}";
        return group.Kind switch{
            GroupByAggregateKind.Sum=>$"GroupBy({key},p=>p).Select(g=>(int)(g.Key+g.Sum(x=>(long)x)))",
            GroupByAggregateKind.Count=>$"GroupBy({key}).Select(g=>g.Key*10+g.Count())",
            _=>$"GroupBy({key}).Select(g=>g.Key)",
        };
    }

    private static string FormatOrderBy(bool descending){
        if(descending){
            return"OrderByDescending(p=>p)";
        }
        return"OrderBy(p=>p)";
    }

    private static string FormatThenBy(bool descending){
        if(descending){
            return"ThenByDescending(p=>p)";
        }
        return"ThenBy(p=>p)";
    }

    private static string FormatBinary(BinaryQueryNode node){
        var left=FormatNode(node.Left);
        var right=FormatNode(node.Right);
        return node.Arguments.Kind switch{
            QueryBinaryKind.Concat=>$"{left}.Concat({right})",
            QueryBinaryKind.Union=>$"{left}.Union({right})",
            QueryBinaryKind.Except=>$"{left}.Except({right})",
            QueryBinaryKind.Intersect=>$"{left}.Intersect({right})",
            QueryBinaryKind.UnionByIdentity=>$"{left}.UnionBy({right},p=>p)",
            QueryBinaryKind.ExceptByIdentity=>$"{left}.ExceptBy({right},p=>p)",
            QueryBinaryKind.IntersectByIdentity=>$"{left}.IntersectBy({right},p=>p)",
            _=>throw new ArgumentOutOfRangeException(nameof(node)),
        };
    }

    private static string FormatNested(NestedQueryNode node){
        var outer=FormatNode(node.Outer);
        var inner=FormatNode(node.Inner);
        return node.Arguments.Kind switch{
            QueryNestedKind.SelectManyXor=>$"{outer}.SelectMany(p=>{inner}.Select(i=>p^i))",
            QueryNestedKind.JoinXor=>$"{outer}.Join({inner},p=>p,i=>i,(p,i)=>p^i)",
            QueryNestedKind.GroupJoinXorCount=>$"{outer}.GroupJoin({inner},p=>(p%8+8)%8,i=>(i%8+8)%8,(p,g)=>p^g.Count())",
            _=>throw new ArgumentOutOfRangeException(nameof(node)),
        };
    }

    private static string FormatTerminal(TerminalQueryNode node){
        var arguments=node.Arguments;
        return arguments.Kind switch{
            QueryTerminalKind.Count=>"Count()",
            QueryTerminalKind.LongCount=>"LongCount()",
            QueryTerminalKind.Any=>"Any()",
            QueryTerminalKind.All=>$"All({FormatPredicate(arguments.Predicate??throw new InvalidOperationException("All predicate is missing."))})",
            QueryTerminalKind.Contains=>$"Contains({Integer(arguments.Value??0)})",
            QueryTerminalKind.Sum=>"Sum(p=>(long)p)",
            QueryTerminalKind.Min=>"Min()",
            QueryTerminalKind.Max=>"Max()",
            QueryTerminalKind.MinBy=>"MinBy(p=>p)",
            QueryTerminalKind.MaxBy=>"MaxBy(p=>p)",
            QueryTerminalKind.Average=>"Average()",
            QueryTerminalKind.AggregateXor=>"Aggregate(0,(a,p)=>a^p)",
            QueryTerminalKind.ToIntPair=>"ToIntPair(count,xor)",
            QueryTerminalKind.ToIntGroup=>"ToIntGroup(key:0)",
            QueryTerminalKind.First=>"First()",
            QueryTerminalKind.FirstOrDefault=>"FirstOrDefault()",
            QueryTerminalKind.Last=>"Last()",
            QueryTerminalKind.LastOrDefault=>"LastOrDefault()",
            QueryTerminalKind.ElementAt=>$"ElementAt({Integer(arguments.Value??0)})",
            QueryTerminalKind.ElementAtOrDefault=>$"ElementAtOrDefault({Integer(arguments.Value??0)})",
            QueryTerminalKind.ToArray=>"ToArray()",
            QueryTerminalKind.ToList=>"ToList()",
            QueryTerminalKind.ToHashSet=>"ToHashSet()",
            QueryTerminalKind.ToDictionary=>"ToDictionary(p=>p,p=>p).OrderBy(key)",
            QueryTerminalKind.ToLookup=>"ToLookup(p=>p&3,p=>p).OrderBy(key,value)",
            _=>throw new ArgumentOutOfRangeException(nameof(node)),
        };
    }

    private static string FormatPredicate(QueryPredicate predicate){
        return predicate.Kind switch{
            QueryPredicateKind.IsEven=>"p=>(p&1)==0",
            QueryPredicateKind.IsOdd=>"p=>(p&1)!=0",
            QueryPredicateKind.GreaterThan=>$"p=>p>{Integer(predicate.Operand)}",
            QueryPredicateKind.LessThan=>$"p=>p<{Integer(predicate.Operand)}",
            QueryPredicateKind.ModuloEquals=>$"p=>p%{Integer(predicate.Modulus)}=={Integer(predicate.Operand)}",
            _=>throw new ArgumentOutOfRangeException(nameof(predicate)),
        };
    }

    private static string FormatSelector(QuerySelector selector){
        return selector.Kind switch{
            QuerySelectorKind.Identity=>"p=>p",
            QuerySelectorKind.Add=>$"p=>p+({Integer(selector.Operand)})",
            QuerySelectorKind.Multiply=>$"p=>p*({Integer(selector.Operand)})",
            QuerySelectorKind.Negate=>"p=>-p",
            _=>throw new ArgumentOutOfRangeException(nameof(selector)),
        };
    }

    private static string Integer(int value){
        return value.ToString(CultureInfo.InvariantCulture);
    }
}
