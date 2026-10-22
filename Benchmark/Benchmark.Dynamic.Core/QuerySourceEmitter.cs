using System.Security.Cryptography;
using System.Text;

namespace Benchmark.Dynamic.Core;

public sealed record RangeSourceFixture(int Start,int Count);

public sealed record QuerySourceEmissionOptions{
    public string GeneratedNamespace{get;init;}="Benchmark.Dynamic.Generated";
    public string? ClassName{get;init;}
}

public sealed record QuerySourceEmissionResult(
    string SourceText,
    string SourceHash,
    string GeneratedNamespace,
    string ClassName,
    string QualifiedTypeName,
    IReadOnlyDictionary<ExecutionPath,string> MethodNames);

public sealed class QuerySourceEmitter{
    private static readonly IReadOnlyDictionary<ExecutionPath,string> DefaultMethodNames=
        new Dictionary<ExecutionPath,string>{
            [ExecutionPath.SystemLinq]="RunSystem",
            [ExecutionPath.FLinqNoOptimizations]="RunFLinqUnoptimized",
            [ExecutionPath.FLinqOptimized]="RunFLinq",
            [ExecutionPath.ZLinq]="RunZLinq",
        };

    public QuerySourceEmissionResult Emit(QueryIrDocument document,QuerySourceEmissionOptions? options=null){
        ArgumentNullException.ThrowIfNull(document);
        QueryIrValidator.ValidateAndThrow(document);
        options??=new();
        var generatedNamespace=ValidateNamespace(options.GeneratedNamespace);
        var irHash=QueryIrJson.ComputeHash(document);
        var className=string.IsNullOrWhiteSpace(options.ClassName)
            ?$"DynamicQuery_{irHash[..16]}"
            :ValidateIdentifier(options.ClassName);
        var element=ElementTypeCatalog.Get(document.ElementType);
        var builder=new StringBuilder(8_192);
        builder.AppendLine("#nullable enable");
        builder.AppendLine("using System.Linq;");
        builder.AppendLine("using ZLinq;");
        builder.Append("namespace ").Append(generatedNamespace).AppendLine(";");
        builder.AppendLine();
        builder.Append("public sealed class ").Append(className).AppendLine("{");
        this.EmitMethod(builder,document,element,ExecutionPath.SystemLinq,null);
        this.EmitMethod(
            builder,
            document,
            element,
            ExecutionPath.FLinqNoOptimizations,
            "[global::Seamless.FLinq.LinqIntercept(true,Optimizations=global::Seamless.FLinq.LinqOptimizations.None)]");
        this.EmitMethod(
            builder,
            document,
            element,
            ExecutionPath.FLinqOptimized,
            "[global::Seamless.FLinq.LinqIntercept(true,Optimizations=global::Seamless.FLinq.LinqOptimizations.All,ConvergenceMode=global::Seamless.FLinq.LinqConvergenceMode.GlobalFixedPoint,EmitOptimizationTrace=true)]");
        var zLinqSupported=QueryZLinqSupport.IsSupported(document);
        if(zLinqSupported){
            this.EmitMethod(builder,document,element,ExecutionPath.ZLinq,null);
        }
        builder.AppendLine("}");
        var sourceText=builder.ToString();
        var sourceHash=Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sourceText)));
        return new(
            sourceText,
            sourceHash,
            generatedNamespace,
            className,
            $"{generatedNamespace}.{className}",
            zLinqSupported
                ?DefaultMethodNames
                :DefaultMethodNames.Where(static pair=>pair.Key!=ExecutionPath.ZLinq).ToDictionary());
    }

    private void EmitMethod(
        StringBuilder builder,
        QueryIrDocument document,
        ElementTypeDescriptor element,
        ExecutionPath path,
        string? attribute){
        var methodName=DefaultMethodNames[path];
        if(attribute is not null){
            builder.Append("    public static global::Benchmark.Dynamic.Core.DynamicQueryValue ")
                .Append(methodName)
                .AppendLine("(object? primary,object? secondary){");
            builder.Append("        return ")
                .Append(methodName)
                .AppendLine("Core(primary,secondary);");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.Append("    ").AppendLine(attribute);
            builder.Append("    private static global::Benchmark.Dynamic.Core.DynamicQueryValue ")
                .Append(methodName)
                .AppendLine("Core(object? primary,object? secondary){");
        }else{
            builder.Append("    public static global::Benchmark.Dynamic.Core.DynamicQueryValue ")
                .Append(methodName)
                .AppendLine("(object? primary,object? secondary){");
        }
        builder.Append("        var primarySource=")
            .Append(EmitFixtureCast(document.SourceTarget,element,path,"primary"))
            .AppendLine(";");
        builder.Append("        var secondarySource=")
            .Append(EmitFixtureCast(document.SourceTarget,element,path,"secondary"))
            .AppendLine(";");
        builder.Append("        var result=")
            .Append(this.EmitTerminal(document.Root,element,path))
            .AppendLine(";");
        builder.Append("        return ")
            .Append(EmitMaterialization(document.Root))
            .AppendLine(";");
        builder.AppendLine("    }");
        builder.AppendLine();
    }

    private string EmitTerminal(TerminalQueryNode node,ElementTypeDescriptor element,ExecutionPath path){
        var input=this.EmitSequence(node.Input,element,path);
        if(path!=ExecutionPath.SystemLinq){
            return node.Arguments.Kind switch{
                QueryTerminalKind.Count=>$"({input}).Count()",
                QueryTerminalKind.LongCount=>$"({input}).LongCount()",
                QueryTerminalKind.Any=>$"({input}).Any()",
                QueryTerminalKind.All=>$"({input}).All({element.EmitPredicate(node.Arguments.Predicate!)})",
                QueryTerminalKind.Contains=>$"({input}).Contains({node.Arguments.Value!.Value})",
                QueryTerminalKind.Sum=>$"({input}).Sum(static value=>(long)value)",
                QueryTerminalKind.Min=>$"({input}).Min()",
                QueryTerminalKind.Max=>$"({input}).Max()",
                QueryTerminalKind.MinBy=>$"({input}).MinBy(static value=>value)",
                QueryTerminalKind.MaxBy=>$"({input}).MaxBy(static value=>value)",
                QueryTerminalKind.Average=>$"({input}).Average()",
                QueryTerminalKind.AggregateXor=>$"({input}).Aggregate(0,(accumulator,value)=>accumulator^value)",
                QueryTerminalKind.ToIntPair=>$"new global::Benchmark.Dynamic.Core.IntPair(({input}).Count(),({input}).Aggregate(0,(accumulator,value)=>accumulator^value))",
                QueryTerminalKind.ToIntGroup=>$"global::Benchmark.Dynamic.Core.DynamicQueryMaterializer.CreateCanonicalIntGroup(0,({input}).ToArray())",
                QueryTerminalKind.First=>$"({input}).First()",
                QueryTerminalKind.FirstOrDefault=>$"({input}).FirstOrDefault()",
                QueryTerminalKind.Last=>$"({input}).Last()",
                QueryTerminalKind.LastOrDefault=>$"({input}).LastOrDefault()",
                QueryTerminalKind.ElementAt=>$"({input}).ElementAt({node.Arguments.Value!.Value})",
                QueryTerminalKind.ElementAtOrDefault=>$"({input}).ElementAtOrDefault({node.Arguments.Value!.Value})",
                QueryTerminalKind.ToArray=>$"({input}).ToArray()",
                QueryTerminalKind.ToList=>$"({input}).ToList()",
                QueryTerminalKind.ToHashSet=>$"({input}).ToHashSet()",
                QueryTerminalKind.ToDictionary=>$"global::Benchmark.Dynamic.Core.DynamicQueryMaterializer.CreateCanonicalDictionaryPairs(({input}).ToDictionary(static value=>value,static value=>value))",
                QueryTerminalKind.ToLookup=>$"global::Benchmark.Dynamic.Core.DynamicQueryMaterializer.CreateCanonicalLookupGroups(({input}).ToLookup(static value=>value&3,static value=>value))",
                QueryTerminalKind.CountWhere=>$"({input}).Count({element.EmitPredicate(node.Arguments.Predicate!)})",
                QueryTerminalKind.AnyWhere=>$"({input}).Any({element.EmitPredicate(node.Arguments.Predicate!)})",
                QueryTerminalKind.AggregateSum=>$"({input}).Aggregate(0L,(accumulator,value)=>accumulator+value)",
                QueryTerminalKind.AverageSelector=>$"({input}).Average(static value=>(long)value)",
                QueryTerminalKind.CountBy=>$"({input}).CountBy({EmitModuloKey(node.Arguments.Value!.Value)}).Select(pair=>pair.Key+pair.Value).ToArray()",
                QueryTerminalKind.AggregateBy=>$"({input}).AggregateBy({EmitModuloKey(node.Arguments.Value!.Value)},0,(accumulator,value)=>accumulator+value).Select(pair=>pair.Key+pair.Value).ToArray()",
                QueryTerminalKind.SequenceEqual=>$"({input}).SequenceEqual({this.EmitSequenceEqualOther(node,input,path)})",
                _=>throw new ArgumentOutOfRangeException(nameof(node)),
            };
        }
        return node.Arguments.Kind switch{
            QueryTerminalKind.Count=>$"global::System.Linq.Enumerable.Count({input})",
            QueryTerminalKind.LongCount=>$"global::System.Linq.Enumerable.LongCount({input})",
            QueryTerminalKind.Any=>$"global::System.Linq.Enumerable.Any({input})",
            QueryTerminalKind.All=>$"global::System.Linq.Enumerable.All({input},{element.EmitPredicate(node.Arguments.Predicate!)})",
            QueryTerminalKind.Contains=>$"global::System.Linq.Enumerable.Contains({input},{node.Arguments.Value!.Value})",
            QueryTerminalKind.Sum=>$"global::System.Linq.Enumerable.Sum({input},static value=>(long)value)",
            QueryTerminalKind.Min=>$"global::System.Linq.Enumerable.Min({input})",
            QueryTerminalKind.Max=>$"global::System.Linq.Enumerable.Max({input})",
            QueryTerminalKind.MinBy=>$"global::System.Linq.Enumerable.MinBy({input},static value=>value)",
            QueryTerminalKind.MaxBy=>$"global::System.Linq.Enumerable.MaxBy({input},static value=>value)",
            QueryTerminalKind.Average=>$"global::System.Linq.Enumerable.Average({input})",
            QueryTerminalKind.AggregateXor=>$"global::System.Linq.Enumerable.Aggregate({input},0,(accumulator,value)=>accumulator^value)",
            QueryTerminalKind.ToIntPair=>$"new global::Benchmark.Dynamic.Core.IntPair(global::System.Linq.Enumerable.Count({input}),global::System.Linq.Enumerable.Aggregate({input},0,(accumulator,value)=>accumulator^value))",
            QueryTerminalKind.ToIntGroup=>$"global::Benchmark.Dynamic.Core.DynamicQueryMaterializer.CreateCanonicalIntGroup(0,global::System.Linq.Enumerable.ToArray({input}))",
            QueryTerminalKind.First=>$"global::System.Linq.Enumerable.First({input})",
            QueryTerminalKind.FirstOrDefault=>$"global::System.Linq.Enumerable.FirstOrDefault({input})",
            QueryTerminalKind.Last=>$"global::System.Linq.Enumerable.Last({input})",
            QueryTerminalKind.LastOrDefault=>$"global::System.Linq.Enumerable.LastOrDefault({input})",
            QueryTerminalKind.ElementAt=>$"global::System.Linq.Enumerable.ElementAt({input},{node.Arguments.Value!.Value})",
            QueryTerminalKind.ElementAtOrDefault=>$"global::System.Linq.Enumerable.ElementAtOrDefault({input},{node.Arguments.Value!.Value})",
            QueryTerminalKind.ToArray=>$"global::System.Linq.Enumerable.ToArray({input})",
            QueryTerminalKind.ToList=>$"global::System.Linq.Enumerable.ToList({input})",
            QueryTerminalKind.ToHashSet=>$"global::System.Linq.Enumerable.ToHashSet({input})",
            QueryTerminalKind.ToDictionary=>$"global::Benchmark.Dynamic.Core.DynamicQueryMaterializer.CreateCanonicalDictionaryPairs(global::System.Linq.Enumerable.ToDictionary({input},static value=>value,static value=>value))",
            QueryTerminalKind.ToLookup=>$"global::Benchmark.Dynamic.Core.DynamicQueryMaterializer.CreateCanonicalLookupGroups(global::System.Linq.Enumerable.ToLookup({input},static value=>value&3,static value=>value))",
            QueryTerminalKind.CountWhere=>$"global::System.Linq.Enumerable.Count({input},{element.EmitPredicate(node.Arguments.Predicate!)})",
            QueryTerminalKind.AnyWhere=>$"global::System.Linq.Enumerable.Any({input},{element.EmitPredicate(node.Arguments.Predicate!)})",
            QueryTerminalKind.AggregateSum=>$"global::System.Linq.Enumerable.Aggregate({input},0L,(accumulator,value)=>accumulator+value)",
            QueryTerminalKind.AverageSelector=>$"global::System.Linq.Enumerable.Average({input},static value=>(long)value)",
            QueryTerminalKind.CountBy=>$"global::System.Linq.Enumerable.ToArray(global::System.Linq.Enumerable.Select(global::System.Linq.Enumerable.CountBy({input},{EmitModuloKey(node.Arguments.Value!.Value)}),pair=>pair.Key+pair.Value))",
            QueryTerminalKind.AggregateBy=>$"global::System.Linq.Enumerable.ToArray(global::System.Linq.Enumerable.Select(global::System.Linq.Enumerable.AggregateBy({input},{EmitModuloKey(node.Arguments.Value!.Value)},0,(accumulator,value)=>accumulator+value),pair=>pair.Key+pair.Value))",
            QueryTerminalKind.SequenceEqual=>$"global::System.Linq.Enumerable.SequenceEqual({input},{this.EmitSequenceEqualOther(node,input,path)})",
            _=>throw new ArgumentOutOfRangeException(nameof(node)),
        };
    }

    private string EmitSequence(QueryNode node,ElementTypeDescriptor element,ExecutionPath path){
        return node switch{
            SourceNode source=>EmitSource(source,path),
            UnaryQueryNode unary=>this.EmitUnary(unary,element,path),
            BinaryQueryNode binary=>this.EmitBinary(binary,element,path),
            NestedQueryNode nested=>this.EmitNested(nested,element,path),
            _=>throw new InvalidOperationException($"Node {node.NodeId} is not a sequence node."),
        };
    }

    private string EmitUnary(UnaryQueryNode node,ElementTypeDescriptor element,ExecutionPath path){
        var input=this.EmitSequence(node.Input,element,path);
        if(path!=ExecutionPath.SystemLinq){
            return node.Arguments switch{
                WhereOperatorArguments where=>$"({input}).Where({element.EmitPredicate(where.Predicate)})",
                WhereIndexedOperatorArguments where=>$"({input}).Where((value,index)=>index%{where.Modulus}=={where.Remainder})",
                SelectOperatorArguments select=>$"({input}).Select({element.EmitSelector(select.Selector)})",
                SelectIndexedOperatorArguments=>$"({input}).Select((value,index)=>value^index)",
                DistinctOperatorArguments=>$"({input}).Distinct()",
                DistinctByOperatorArguments distinctBy=>$"({input}).DistinctBy(value=>(value%{distinctBy.Modulus}+{distinctBy.Modulus})%{distinctBy.Modulus})",
                TakeOperatorArguments take=>$"({input}).Take({take.Count})",
                SkipOperatorArguments skip=>$"({input}).Skip({skip.Count})",
                ReverseOperatorArguments=>$"({input}).Reverse()",
                OrderByOperatorArguments order when order.Descending=>$"({input}).OrderByDescending(value=>value)",
                OrderByOperatorArguments=>$"({input}).OrderBy(value=>value)",
                ThenByOperatorArguments order when order.Descending=>$"({input}).ThenByDescending(value=>value)",
                ThenByOperatorArguments=>$"({input}).ThenBy(value=>value)",
                ExtendedUnaryOperatorArguments extended=>EmitExtendedUnary(extended,input,element,path),
                _=>throw new InvalidOperationException($"Unsupported unary arguments at {node.NodeId}."),
            };
        }
        return node.Arguments switch{
            WhereOperatorArguments where=>$"global::System.Linq.Enumerable.Where({input},{element.EmitPredicate(where.Predicate)})",
            WhereIndexedOperatorArguments where=>$"global::System.Linq.Enumerable.Where({input},(value,index)=>index%{where.Modulus}=={where.Remainder})",
            SelectOperatorArguments select=>$"global::System.Linq.Enumerable.Select({input},{element.EmitSelector(select.Selector)})",
            SelectIndexedOperatorArguments=>$"global::System.Linq.Enumerable.Select({input},(value,index)=>value^index)",
            DistinctOperatorArguments=>$"global::System.Linq.Enumerable.Distinct({input})",
            DistinctByOperatorArguments distinctBy=>$"global::System.Linq.Enumerable.DistinctBy({input},value=>(value%{distinctBy.Modulus}+{distinctBy.Modulus})%{distinctBy.Modulus})",
            TakeOperatorArguments take=>$"global::System.Linq.Enumerable.Take({input},{take.Count})",
            SkipOperatorArguments skip=>$"global::System.Linq.Enumerable.Skip({input},{skip.Count})",
            ReverseOperatorArguments=>$"global::System.Linq.Enumerable.Reverse({input})",
            OrderByOperatorArguments order when order.Descending=>$"global::System.Linq.Enumerable.OrderByDescending({input},value=>value)",
            OrderByOperatorArguments=>$"global::System.Linq.Enumerable.OrderBy({input},value=>value)",
            ThenByOperatorArguments order when order.Descending=>$"global::System.Linq.Enumerable.ThenByDescending({input},value=>value)",
            ThenByOperatorArguments=>$"global::System.Linq.Enumerable.ThenBy({input},value=>value)",
            ExtendedUnaryOperatorArguments extended=>EmitExtendedUnary(extended,input,element,path),
            _=>throw new InvalidOperationException($"Unsupported unary arguments at {node.NodeId}."),
        };
    }

    // SequenceEqual の比較相手。Value が 1 なら同じチェーン(同一式を2回評価する)、0 なら副Source。
    private string EmitSequenceEqualOther(TerminalQueryNode node,string input,ExecutionPath path){
        if(node.Arguments.Value==1){
            return path==ExecutionPath.ZLinq?$"({input}).ToArray()":$"({input})";
        }
        var target=FindSourceTarget(node.Input);
        var secondary=new SourceNode(
            "secondary",
            target,
            SourceSlot.Secondary,
            QueryCardinalityRange.Exact(0),
            target.GetInitialOrder());
        var source=EmitSource(secondary,path);
        return path==ExecutionPath.ZLinq?$"({source}).ToArray()":source;
    }

    private static SourceTarget FindSourceTarget(QueryNode node){
        if(node is SourceNode source){
            return source.SourceTarget;
        }
        return FindSourceTarget(node.Children[0]);
    }

    private static string EmitModuloKey(int modulus){
        return modulus==0?"value=>value":$"value=>(value%{modulus}+{modulus})%{modulus}";
    }

    // System.Linq は System.Linq.Enumerable を完全修飾の静的呼び出しで書く。
    // FLinq と ZLinq は拡張メソッド構文で書く。ただしグループ内の集計は、ZLinq では
    // グループを AsValueEnumerable に変えて ZLinq の集計へ結び付ける(IGrouping は IEnumerable なので
    // そのままだと System.Linq に結び付き、呼び出し先の検証で拒否される)。
    private static string EmitExtendedUnary(
        ExtendedUnaryOperatorArguments arguments,
        string input,
        ElementTypeDescriptor element,
        ExecutionPath path){
        var system=path==ExecutionPath.SystemLinq;
        var groupValues=path==ExecutionPath.ZLinq?"global::ZLinq.ValueEnumerable.AsValueEnumerable(group)":"group";
        string Call(string method,string callArguments)=>system
            ?$"global::System.Linq.Enumerable.{method}({input}{(callArguments.Length==0?string.Empty:","+callArguments)})"
            :$"({input}).{method}({callArguments})";
        switch(arguments){
            case TakeLastOperatorArguments takeLast:
                return Call("TakeLast",takeLast.Count.ToString());
            case SkipLastOperatorArguments skipLast:
                return Call("SkipLast",skipLast.Count.ToString());
            case TakeWhileOperatorArguments takeWhile:
                return Call("TakeWhile",element.EmitPredicate(takeWhile.Predicate));
            case SkipWhileOperatorArguments skipWhile:
                return Call("SkipWhile",element.EmitPredicate(skipWhile.Predicate));
            case AppendOperatorArguments append:
                return Call("Append",$"({append.Value})");
            case PrependOperatorArguments prepend:
                return Call("Prepend",$"({prepend.Value})");
            case DefaultIfEmptyOperatorArguments defaultIfEmpty:
                return Call("DefaultIfEmpty",$"({defaultIfEmpty.Value})");
            case OrderByKeyOperatorArguments orderBy:
                return Call(orderBy.Descending?"OrderByDescending":"OrderBy",EmitModuloKey(orderBy.Modulus));
            case GroupByAggregateOperatorArguments group:{
                var key=EmitModuloKey(group.Modulus);
                if(system){
                    var grouped=$"global::System.Linq.Enumerable.GroupBy({input},{key})";
                    return group.Kind switch{
                        GroupByAggregateKind.Sum=>$"global::System.Linq.Enumerable.Select(global::System.Linq.Enumerable.GroupBy({input},{key},value=>value),group=>(int)(group.Key+global::System.Linq.Enumerable.Sum(group,static item=>(long)item)))",
                        GroupByAggregateKind.Count=>$"global::System.Linq.Enumerable.Select({grouped},group=>group.Key*10+global::System.Linq.Enumerable.Count(group))",
                        _=>$"global::System.Linq.Enumerable.Select({grouped},group=>group.Key)",
                    };
                }
                return group.Kind switch{
                    GroupByAggregateKind.Sum=>$"({input}).GroupBy({key},value=>value).Select(group=>(int)(group.Key+{groupValues}.Sum(static item=>(long)item)))",
                    GroupByAggregateKind.Count=>$"({input}).GroupBy({key}).Select(group=>group.Key*10+{groupValues}.Count())",
                    _=>$"({input}).GroupBy({key}).Select(group=>group.Key)",
                };
            }
            default:
                throw new InvalidOperationException($"Unsupported extended unary arguments: {arguments.GetType().Name}.");
        }
    }

    private string EmitBinary(BinaryQueryNode node,ElementTypeDescriptor element,ExecutionPath path){
        var left=this.EmitSequence(node.Left,element,path);
        var right=this.EmitSequence(node.Right,element,path);
        if(path==ExecutionPath.ZLinq){
            return node.Arguments.Kind switch{
                QueryBinaryKind.Concat=>$"({left}).Concat(({right}).ToArray())",
                QueryBinaryKind.Union=>$"({left}).Union(({right}).ToArray())",
                QueryBinaryKind.Except=>$"({left}).Except(({right}).ToArray())",
                QueryBinaryKind.Intersect=>$"({left}).Intersect(({right}).ToArray())",
                QueryBinaryKind.UnionByIdentity=>$"({left}).UnionBy(({right}).ToArray(),value=>value)",
                QueryBinaryKind.ExceptByIdentity=>$"({left}).ExceptBy(({right}).ToArray(),value=>value)",
                QueryBinaryKind.IntersectByIdentity=>$"({left}).IntersectBy(({right}).ToArray(),value=>value)",
                QueryBinaryKind.Zip=>$"({left}).Zip(({right}).ToArray(),(leftValue,rightValue)=>leftValue+rightValue)",
                _=>throw new InvalidOperationException($"Unsupported binary arguments at {node.NodeId}."),
            };
        }
        if(path is ExecutionPath.FLinqNoOptimizations or ExecutionPath.FLinqOptimized){
            return node.Arguments.Kind switch{
                QueryBinaryKind.Concat=>$"({left}).Concat({right})",
                QueryBinaryKind.Union=>$"({left}).Union({right})",
                QueryBinaryKind.Except=>$"({left}).Except({right})",
                QueryBinaryKind.Intersect=>$"({left}).Intersect({right})",
                QueryBinaryKind.UnionByIdentity=>$"({left}).UnionBy({right},value=>value)",
                QueryBinaryKind.ExceptByIdentity=>$"({left}).ExceptBy({right},value=>value)",
                QueryBinaryKind.IntersectByIdentity=>$"({left}).IntersectBy({right},value=>value)",
                QueryBinaryKind.Zip=>$"({left}).Zip({right},(leftValue,rightValue)=>leftValue+rightValue)",
                _=>throw new InvalidOperationException($"Unsupported binary arguments at {node.NodeId}."),
            };
        }
        return node.Arguments.Kind switch{
            QueryBinaryKind.Concat=>$"global::System.Linq.Enumerable.Concat({left},{right})",
            QueryBinaryKind.Union=>$"global::System.Linq.Enumerable.Union({left},{right})",
            QueryBinaryKind.Except=>$"global::System.Linq.Enumerable.Except({left},{right})",
            QueryBinaryKind.Intersect=>$"global::System.Linq.Enumerable.Intersect({left},{right})",
            QueryBinaryKind.UnionByIdentity=>$"global::System.Linq.Enumerable.UnionBy({left},{right},value=>value)",
            QueryBinaryKind.ExceptByIdentity=>$"global::System.Linq.Enumerable.ExceptBy({left},{right},value=>value)",
            QueryBinaryKind.IntersectByIdentity=>$"global::System.Linq.Enumerable.IntersectBy({left},{right},value=>value)",
            QueryBinaryKind.Zip=>$"global::System.Linq.Enumerable.Zip({left},{right},(leftValue,rightValue)=>leftValue+rightValue)",
            _=>throw new InvalidOperationException($"Unsupported binary arguments at {node.NodeId}."),
        };
    }

    private string EmitNested(NestedQueryNode node,ElementTypeDescriptor element,ExecutionPath path){
        var outer=this.EmitSequence(node.Outer,element,path);
        var inner=this.EmitSequence(node.Inner,element,path);
        if(node.Arguments.Kind==QueryNestedKind.GroupJoinXorCount){
            const string OuterKey="outerValue=>(outerValue%8+8)%8";
            const string InnerKey="innerValue=>(innerValue%8+8)%8";
            const string Result="(outerValue,group)=>outerValue^global::Benchmark.Dynamic.Core.DynamicQueryMaterializer.CountWithLimit(group)";
            if(path==ExecutionPath.ZLinq){
                return $"({outer}).GroupJoin(({inner}).ToArray(),{OuterKey},{InnerKey},{Result})";
            }
            if(path is ExecutionPath.FLinqNoOptimizations or ExecutionPath.FLinqOptimized){
                return $"({outer}).GroupJoin({inner},{OuterKey},{InnerKey},{Result})";
            }
            return $"global::System.Linq.Enumerable.GroupJoin({outer},{inner},{OuterKey},{InnerKey},{Result})";
        }
        if(node.Arguments.Kind==QueryNestedKind.JoinXor){
            if(path==ExecutionPath.ZLinq){
                return $"({outer}).Join(({inner}).ToArray(),outerValue=>outerValue,innerValue=>innerValue,(outerValue,innerValue)=>outerValue^innerValue)";
            }
            if(path is ExecutionPath.FLinqNoOptimizations or ExecutionPath.FLinqOptimized){
                return $"({outer}).Join({inner},outerValue=>outerValue,innerValue=>innerValue,(outerValue,innerValue)=>outerValue^innerValue)";
            }
            return $"global::System.Linq.Enumerable.Join({outer},{inner},outerValue=>outerValue,innerValue=>innerValue,(outerValue,innerValue)=>outerValue^innerValue)";
        }
        if(node.Arguments.Kind==QueryNestedKind.SelectManyXor){
            if(path==ExecutionPath.ZLinq){
                return $"({outer}).SelectMany(outerValue=>({inner}).Select(innerValue=>outerValue^innerValue).ToArray())";
            }
            if(path is ExecutionPath.FLinqNoOptimizations or ExecutionPath.FLinqOptimized){
                return $"({outer}).SelectMany(outerValue=>({inner}).Select(innerValue=>outerValue^innerValue))";
            }
            return $"global::System.Linq.Enumerable.SelectMany({outer},outerValue=>global::System.Linq.Enumerable.Select({inner},innerValue=>outerValue^innerValue))";
        }
        throw new InvalidOperationException($"Unsupported nested arguments at {node.NodeId}.");
    }

    private static string EmitSource(SourceNode source,ExecutionPath path){
        var parameter=source.Slot==SourceSlot.Primary?"primarySource":"secondarySource";
        if(path==ExecutionPath.ZLinq){
            return source.SourceTarget.GetSourceKind() switch{
                SourceKind.Array=>$"global::ZLinq.ValueEnumerable.AsValueEnumerable({parameter})",
                SourceKind.List=>$"global::ZLinq.ValueEnumerable.AsValueEnumerable({parameter})",
                SourceKind.Range=>$"global::ZLinq.ValueEnumerable.Range({parameter}.Start,{parameter}.Count)",
                _=>$"global::ZLinq.ValueEnumerable.AsValueEnumerable({parameter})",
            };
        }
        if(source.SourceTarget.GetSourceKind()==SourceKind.Range){
            return $"global::System.Linq.Enumerable.Range({parameter}.Start,{parameter}.Count)";
        }
        return parameter;
    }

    private static string EmitFixtureCast(SourceTarget target,ElementTypeDescriptor element,ExecutionPath path,string parameter){
        var type=target.GetSourceKind() switch{
            SourceKind.Range=>"global::Benchmark.Dynamic.Core.RangeSourceFixture",
            SourceKind.Array when path is ExecutionPath.SystemLinq or ExecutionPath.FLinqNoOptimizations or ExecutionPath.FLinqOptimized or ExecutionPath.ZLinq=>element.ClrTypeName+"[]",
            SourceKind.List when path is ExecutionPath.SystemLinq or ExecutionPath.ZLinq=>$"global::System.Collections.Generic.List<{element.ClrTypeName}>",
            SourceKind.Persisted when target==SourceTarget.SeamlessPersisted&&path is ExecutionPath.FLinqNoOptimizations or ExecutionPath.FLinqOptimized=>$"global::Seamless.Collections.Persisted.Set<{element.ClrTypeName}>",
            SourceKind.Tsavorite when target==SourceTarget.SeamlessTsavorite&&path is ExecutionPath.FLinqNoOptimizations or ExecutionPath.FLinqOptimized=>$"global::Seamless.Collections.Tsavorite.HashSet<{element.ClrTypeName}>",
            _=>$"global::System.Collections.Generic.IEnumerable<{element.ClrTypeName}>",
        };
        return $"({type}){parameter}!";
    }

    private static string EmitMaterialization(TerminalQueryNode root){
        if(root.OutputCardinality==QueryCardinality.Sequence){
            return $"global::Benchmark.Dynamic.Core.DynamicQueryValue.FromObject(result,global::Benchmark.Dynamic.Core.QueryValueType.{root.OutputType},global::Benchmark.Dynamic.Core.QueryCardinality.Sequence,global::Benchmark.Dynamic.Core.QueryOrderSemantics.{root.OrderSemantics})";
        }
        var atomType=root.OutputType switch{
            QueryValueType.Int=>"IntQueryAtom",
            QueryValueType.Long=>"LongQueryAtom",
            QueryValueType.Bool=>"BoolQueryAtom",
            QueryValueType.Double=>"DoubleQueryAtom",
            QueryValueType.IntPair=>"IntPairQueryAtom",
            QueryValueType.IntGroup=>"IntGroupQueryAtom",
            _=>throw new ArgumentOutOfRangeException(nameof(root)),
        };
        return $"global::Benchmark.Dynamic.Core.DynamicQueryValue.Scalar(new global::Benchmark.Dynamic.Core.{atomType}(result))";
    }

    private static string ValidateNamespace(string value){
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var parts=value.Split('.');
        if(parts.Any(static part=>!IsIdentifier(part))){
            throw new ArgumentException("GeneratedNamespace must be a dot-separated C# identifier.",nameof(value));
        }
        return value;
    }

    private static string ValidateIdentifier(string value){
        if(!IsIdentifier(value)){
            throw new ArgumentException("ClassName must be a C# identifier.",nameof(value));
        }
        return value;
    }

    private static bool IsIdentifier(string value){
        if(string.IsNullOrEmpty(value)||!(char.IsLetter(value[0])||value[0]=='_')){
            return false;
        }
        return value.Skip(1).All(static character=>char.IsLetterOrDigit(character)||character=='_');
    }
}
