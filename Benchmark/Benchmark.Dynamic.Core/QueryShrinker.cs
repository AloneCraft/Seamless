using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Benchmark.Dynamic.Core;

public sealed record FailureSignature(
    string Status,
    string DiagnosticId,
    string ExceptionType,
    string FirstDifference,
    string NonConvergenceReason){
    public string ToCanonicalJson(){
        return JsonSerializer.Serialize(new{
            status=this.Status??string.Empty,
            diagnosticId=this.DiagnosticId??string.Empty,
            exceptionType=this.ExceptionType??string.Empty,
            firstDifference=this.FirstDifference??string.Empty,
            nonConvergenceReason=this.NonConvergenceReason??string.Empty,
        });
    }

    public string ComputeHash(){
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(this.ToCanonicalJson())));
    }
}

public static class FailureSignatureFactory{
    public static FailureSignature FromWorkerArtifacts(
        WorkerExecutionStatus status,
        string? diagnosticsJson,
        string? resultJson,
        string? optimizationTraceJson){
        return new(
            status.ToString(),
            FirstDiagnosticId(diagnosticsJson),
            FirstExceptionType(resultJson),
            FirstDifference(resultJson),
            NonConvergenceReason(optimizationTraceJson));
    }

    private static string FirstDiagnosticId(string? json){
        try{
            using var document=Parse(json);
            return document?.RootElement.ValueKind==JsonValueKind.Array
                ?document.RootElement.EnumerateArray()
                    .Where(static item=>String(item,"severity").Equals("Error",StringComparison.OrdinalIgnoreCase))
                    .Select(static item=>String(item,"id"))
                    .FirstOrDefault(static value=>value.Length!=0)??string.Empty
                :string.Empty;
        }catch(JsonException){
            return string.Empty;
        }
    }

    private static string FirstExceptionType(string? json){
        try{
            using var document=Parse(json);
            if(document is null||!document.RootElement.TryGetProperty("exceptions",out var exceptions)
                ||exceptions.ValueKind!=JsonValueKind.Object){
                return string.Empty;
            }
            return exceptions.EnumerateObject()
                .OrderBy(static property=>property.Name,StringComparer.Ordinal)
                .Select(static property=>String(property.Value,"type"))
                .FirstOrDefault(static value=>value.Length!=0)??string.Empty;
        }catch(JsonException){
            return string.Empty;
        }
    }

    private static string FirstDifference(string? json){
        try{
            using var document=Parse(json);
            if(document is null||!document.RootElement.TryGetProperty("comparisons",out var comparisons)
                ||comparisons.ValueKind!=JsonValueKind.Object){
                return string.Empty;
            }
            return comparisons.EnumerateObject()
                .OrderBy(static property=>property.Name,StringComparer.Ordinal)
                .Where(static property=>property.Value.TryGetProperty("isMatch",out var match)&&match.ValueKind==JsonValueKind.False)
                .Select(static property=>String(property.Value,"difference"))
                .FirstOrDefault()??string.Empty;
        }catch(JsonException){
            return string.Empty;
        }
    }

    private static string NonConvergenceReason(string? json){
        try{
            using var outer=Parse(json);
            if(outer is null||outer.RootElement.ValueKind!=JsonValueKind.Array){
                return string.Empty;
            }
            foreach(var payload in outer.RootElement.EnumerateArray()){
                var innerJson=payload.ValueKind==JsonValueKind.String?payload.GetString():payload.GetRawText();
                using var inner=Parse(innerJson);
                if(inner is null||!inner.RootElement.TryGetProperty("LayerTerminations",out var terminations)){
                    continue;
                }
                var reason=terminations.EnumerateArray()
                    .Select(static termination=>String(termination,"Termination"))
                    .FirstOrDefault(static termination=>termination.Length!=0&&!termination.Equals("FixedPoint",StringComparison.Ordinal));
                if(reason is not null){
                    return reason;
                }
            }
            return string.Empty;
        }catch(JsonException){
            return string.Empty;
        }
    }

    private static JsonDocument? Parse(string? json){
        return string.IsNullOrWhiteSpace(json)||json=="null"?null:JsonDocument.Parse(json);
    }

    private static string String(JsonElement element,string property){
        return element.ValueKind==JsonValueKind.Object&&element.TryGetProperty(property,out var value)
            &&value.ValueKind==JsonValueKind.String?value.GetString()??string.Empty:string.Empty;
    }
}

public sealed record ShrinkRequest(
    QueryIrDocument OriginalQuery,
    int InputElementCount,
    FailureSignature ExpectedFailure,
    int MaximumCandidates=256,
    int TimeBudgetMilliseconds=5_000);

public sealed record ShrinkCandidate(
    int CandidateIndex,
    string DerivationKind,
    QueryIrDocument Query,
    int InputElementCount);

public sealed record ShrinkResult(
    QueryIrDocument OriginalQuery,
    QueryIrDocument ShrunkQuery,
    int OriginalInputElementCount,
    int ShrunkInputElementCount,
    FailureSignature ExpectedFailure,
    int EvaluatedCandidateCount,
    bool TimedOut,
    IReadOnlyList<string> AppliedDerivations);

public sealed class QueryShrinker{
    public async Task<ShrinkResult> ShrinkAsync(
        ShrinkRequest request,
        Func<ShrinkCandidate,CancellationToken,ValueTask<FailureSignature?>> evaluate,
        CancellationToken cancellationToken=default){
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(evaluate);
        ValidateRequest(request);
        QueryIrValidator.ValidateAndThrow(request.OriginalQuery);
        var started=Stopwatch.GetTimestamp();
        var current=request.OriginalQuery;
        var currentInputCount=request.InputElementCount;
        var evaluated=0;
        var timedOut=false;
        var derivations=new List<string>();
        var visited=new HashSet<string>(StringComparer.Ordinal){Fingerprint(current,currentInputCount)};
        while(evaluated<request.MaximumCandidates){
            cancellationToken.ThrowIfCancellationRequested();
            if(Stopwatch.GetElapsedTime(started)>=TimeSpan.FromMilliseconds(request.TimeBudgetMilliseconds)){
                timedOut=true;
                break;
            }
            var accepted=false;
            foreach(var state in EnumerateCandidateStates(current,currentInputCount)){
                cancellationToken.ThrowIfCancellationRequested();
                if(evaluated>=request.MaximumCandidates){
                    break;
                }
                if(Stopwatch.GetElapsedTime(started)>=TimeSpan.FromMilliseconds(request.TimeBudgetMilliseconds)){
                    timedOut=true;
                    break;
                }
                var fingerprint=Fingerprint(state.Query,state.InputElementCount);
                if(!visited.Add(fingerprint)){
                    continue;
                }
                var candidate=new ShrinkCandidate(evaluated,state.DerivationKind,state.Query,state.InputElementCount);
                evaluated++;
                var signature=await evaluate(candidate,cancellationToken).ConfigureAwait(false);
                if(signature!=request.ExpectedFailure){
                    continue;
                }
                current=state.Query;
                currentInputCount=state.InputElementCount;
                derivations.Add(state.DerivationKind);
                accepted=true;
                break;
            }
            if(timedOut||!accepted){
                break;
            }
        }
        return new(
            request.OriginalQuery,
            current,
            request.InputElementCount,
            currentInputCount,
            request.ExpectedFailure,
            evaluated,
            timedOut,
            derivations);
    }

    public IReadOnlyList<ShrinkCandidate> EnumerateCandidates(QueryIrDocument query,int inputElementCount){
        ArgumentNullException.ThrowIfNull(query);
        if(inputElementCount<1){
            throw new ArgumentOutOfRangeException(nameof(inputElementCount));
        }
        QueryIrValidator.ValidateAndThrow(query);
        return EnumerateCandidateStates(query,inputElementCount)
            .Select((state,index)=>new ShrinkCandidate(index,state.DerivationKind,state.Query,state.InputElementCount))
            .ToArray();
    }

    private static IEnumerable<CandidateState> EnumerateCandidateStates(QueryIrDocument query,int inputElementCount){
        if(query.Root.Arguments.Kind!=QueryTerminalKind.Count){
            var countRoot=query.Root with{
                OperatorId=QueryOperatorIds.Count,
                Arguments=new(QueryTerminalKind.Count),
                OutputType=QueryValueType.Int,
                OutputCardinality=QueryCardinality.Scalar,
                Cardinality=QueryCardinalityRange.Exact(1),
                OrderSemantics=QueryOrderSemantics.TotalOrdered,
                EmptySafety=QueryEmptySafety.Safe,
            };
            yield return State("terminal.count",query with{Root=countRoot},inputElementCount);
        }

        foreach(var node in Enumerate(query.Root.Input).OfType<UnaryQueryNode>()){
            yield return State($"unary.remove:{node.OperatorId}:{node.NodeId}",ReplaceInput(query,node.NodeId,node.Input),inputElementCount);
        }
        foreach(var node in Enumerate(query.Root.Input).OfType<BinaryQueryNode>()){
            yield return State($"binary.left:{node.OperatorId}:{node.NodeId}",ReplaceInput(query,node.NodeId,node.Left),inputElementCount);
            yield return State($"binary.right:{node.OperatorId}:{node.NodeId}",ReplaceInput(query,node.NodeId,node.Right),inputElementCount);
        }
        foreach(var node in Enumerate(query.Root.Input).OfType<NestedQueryNode>()){
            yield return State($"nested.outer:{node.OperatorId}:{node.NodeId}",ReplaceInput(query,node.NodeId,node.Outer),inputElementCount);
            yield return State($"nested.inner:{node.OperatorId}:{node.NodeId}",ReplaceInput(query,node.NodeId,node.Inner),inputElementCount);
        }

        if(query.Root.Arguments.Kind==QueryTerminalKind.All
            &&query.Root.Arguments.Predicate is not {Kind:QueryPredicateKind.IsEven}){
            var root=query.Root with{Arguments=query.Root.Arguments with{Predicate=new(QueryPredicateKind.IsEven)}};
            yield return State("predicate.terminal.isEven",query with{Root=root},inputElementCount);
        }
        foreach(var node in Enumerate(query.Root.Input).OfType<UnaryQueryNode>()){
            if(node.Arguments is WhereOperatorArguments {Predicate.Kind:not QueryPredicateKind.IsEven}){
                var simplified=node with{Arguments=new WhereOperatorArguments(new(QueryPredicateKind.IsEven))};
                yield return State($"predicate.where.isEven:{node.NodeId}",ReplaceInput(query,node.NodeId,simplified),inputElementCount);
            }
        }
        foreach(var node in Enumerate(query.Root.Input).OfType<UnaryQueryNode>()){
            if(node.Arguments is SelectOperatorArguments {Selector.Kind:not QuerySelectorKind.Identity}){
                var simplified=node with{Arguments=new SelectOperatorArguments(new(QuerySelectorKind.Identity))};
                yield return State($"selector.identity:{node.NodeId}",ReplaceInput(query,node.NodeId,simplified),inputElementCount);
            }
        }

        foreach(var reducedInput in new[]{Math.Max(1,inputElementCount/2),1}.Distinct().Where(value=>value<inputElementCount)){
            yield return State($"input.count:{reducedInput}",query,reducedInput);
        }

        CandidateState State(string derivation,QueryIrDocument document,int count){
            return new(derivation,QueryIrNormalizer.Normalize(document,count),count);
        }
    }

    private static QueryIrDocument ReplaceInput(QueryIrDocument query,string nodeId,QueryNode replacement){
        var input=ReplaceNode(query.Root.Input,nodeId,replacement);
        return query with{Root=query.Root with{Input=input}};
    }

    private static QueryNode ReplaceNode(QueryNode node,string nodeId,QueryNode replacement){
        if(StringComparer.Ordinal.Equals(node.NodeId,nodeId)){
            return replacement;
        }
        return node switch{
            UnaryQueryNode unary=>unary with{Input=ReplaceNode(unary.Input,nodeId,replacement)},
            BinaryQueryNode binary=>binary with{
                Left=ReplaceNode(binary.Left,nodeId,replacement),
                Right=ReplaceNode(binary.Right,nodeId,replacement),
            },
            NestedQueryNode nested=>nested with{
                Outer=ReplaceNode(nested.Outer,nodeId,replacement),
                Inner=ReplaceNode(nested.Inner,nodeId,replacement),
            },
            _=>node,
        };
    }

    private static IEnumerable<QueryNode> Enumerate(QueryNode node){
        yield return node;
        foreach(var child in node.Children){
            foreach(var descendant in Enumerate(child)){
                yield return descendant;
            }
        }
    }

    private static string Fingerprint(QueryIrDocument query,int inputElementCount){
        return $"{inputElementCount}:{QueryIrJson.ComputeHash(query)}";
    }

    private static void ValidateRequest(ShrinkRequest request){
        if(request.InputElementCount<1){
            throw new ArgumentOutOfRangeException(nameof(request.InputElementCount));
        }
        if(request.MaximumCandidates<1){
            throw new ArgumentOutOfRangeException(nameof(request.MaximumCandidates));
        }
        if(request.TimeBudgetMilliseconds<1){
            throw new ArgumentOutOfRangeException(nameof(request.TimeBudgetMilliseconds));
        }
        ArgumentNullException.ThrowIfNull(request.ExpectedFailure);
    }

    private sealed record CandidateState(string DerivationKind,QueryIrDocument Query,int InputElementCount);
}

public static class QueryIrNormalizer{
    public static QueryIrDocument Normalize(QueryIrDocument document,int inputElementCount){
        ArgumentNullException.ThrowIfNull(document);
        if(inputElementCount<1){
            throw new ArgumentOutOfRangeException(nameof(inputElementCount));
        }
        var root=(TerminalQueryNode)NormalizeNode(document.Root,inputElementCount);
        var normalized=document with{
            RequestedOperatorCount=root.OperatorCount,
            MaximumDepth=Math.Max(1,root.Depth),
            Root=root,
        };
        QueryIrValidator.ValidateAndThrow(normalized);
        return normalized;
    }

    private static QueryNode NormalizeNode(QueryNode node,int inputElementCount){
        return node switch{
            SourceNode source=>source with{
                Cardinality=QueryCardinalityRange.Exact(inputElementCount),
                OrderSemantics=source.SourceTarget.GetInitialOrder(),
            },
            UnaryQueryNode unary=>NormalizeUnary(unary,inputElementCount),
            BinaryQueryNode binary=>NormalizeBinary(binary,inputElementCount),
            NestedQueryNode nested=>NormalizeNested(nested,inputElementCount),
            TerminalQueryNode terminal=>NormalizeTerminal(terminal,inputElementCount),
            _=>throw new InvalidOperationException($"Unsupported query node type: {node.GetType().FullName}."),
        };
    }

    private static UnaryQueryNode NormalizeUnary(UnaryQueryNode node,int inputElementCount){
        var input=NormalizeNode(node.Input,inputElementCount);
        var cardinality=node.Arguments switch{
            WhereOperatorArguments=>new QueryCardinalityRange(0,input.Cardinality.Maximum),
            WhereIndexedOperatorArguments=>new QueryCardinalityRange(0,input.Cardinality.Maximum),
            SelectOperatorArguments=>input.Cardinality,
            SelectIndexedOperatorArguments=>input.Cardinality,
            DistinctOperatorArguments=>new QueryCardinalityRange(input.Cardinality.Minimum>0?1:0,input.Cardinality.Maximum),
            DistinctByOperatorArguments distinctBy=>new QueryCardinalityRange(
                input.Cardinality.Minimum>0?1:0,
                input.Cardinality.Maximum is { }maximum?Math.Min(maximum,distinctBy.Modulus):distinctBy.Modulus),
            TakeOperatorArguments take=>new QueryCardinalityRange(
                Math.Min(input.Cardinality.Minimum,take.Count),
                input.Cardinality.Maximum is { }maximum?Math.Min(maximum,take.Count):take.Count),
            SkipOperatorArguments skip=>new QueryCardinalityRange(
                Math.Max(0,input.Cardinality.Minimum-skip.Count),
                input.Cardinality.Maximum is { }maximum?Math.Max(0,maximum-skip.Count):null),
            ReverseOperatorArguments=>input.Cardinality,
            OrderByOperatorArguments or ThenByOperatorArguments=>input.Cardinality,
            ExtendedUnaryOperatorArguments extended=>ExtendedUnaryShape.Cardinality(extended,input.Cardinality),
            _=>throw new InvalidOperationException($"Unsupported unary arguments: {node.Arguments.GetType().FullName}."),
        };
        var order=node.Arguments switch{
            DistinctOperatorArguments or DistinctByOperatorArguments=>QueryOrderSemantics.Unordered,
            OrderByOperatorArguments or ThenByOperatorArguments=>QueryOrderSemantics.TotalOrdered,
            ExtendedUnaryOperatorArguments extended=>ExtendedUnaryShape.Order(extended,input.OrderSemantics),
            _=>input.OrderSemantics,
        };
        return node with{Input=input,Cardinality=cardinality,OrderSemantics=order};
    }

    private static BinaryQueryNode NormalizeBinary(BinaryQueryNode node,int inputElementCount){
        var left=NormalizeNode(node.Left,inputElementCount);
        var right=NormalizeNode(node.Right,inputElementCount);
        var sum=QueryCardinalityRange.Add(left.Cardinality,right.Cardinality);
        var cardinality=node.Arguments.Kind switch{
            QueryBinaryKind.Zip=>ExtendedBinaryShape.ZipCardinality(left.Cardinality,right.Cardinality),
            QueryBinaryKind.Concat=>sum,
            QueryBinaryKind.Union=>new QueryCardinalityRange(0,sum.Maximum),
            QueryBinaryKind.Except=>new QueryCardinalityRange(0,left.Cardinality.Maximum),
            QueryBinaryKind.Intersect=>new QueryCardinalityRange(
                0,
                left.Cardinality.Maximum is { }leftMaximum&&right.Cardinality.Maximum is { }rightMaximum
                    ?Math.Min(leftMaximum,rightMaximum)
                    :left.Cardinality.Maximum??right.Cardinality.Maximum),
            QueryBinaryKind.UnionByIdentity=>new QueryCardinalityRange(0,sum.Maximum),
            QueryBinaryKind.ExceptByIdentity=>new QueryCardinalityRange(0,left.Cardinality.Maximum),
            QueryBinaryKind.IntersectByIdentity=>new QueryCardinalityRange(
                0,
                left.Cardinality.Maximum is { }leftMaximum&&right.Cardinality.Maximum is { }rightMaximum
                    ?Math.Min(leftMaximum,rightMaximum)
                    :left.Cardinality.Maximum??right.Cardinality.Maximum),
            _=>throw new InvalidOperationException($"Unsupported binary kind: {node.Arguments.Kind}."),
        };
        var order=node.Arguments.Kind is QueryBinaryKind.Concat or QueryBinaryKind.Zip
            &&left.OrderSemantics!=QueryOrderSemantics.Unordered
            &&right.OrderSemantics!=QueryOrderSemantics.Unordered
                ?QueryOrderSemantics.Ordered
                :QueryOrderSemantics.Unordered;
        return node with{Left=left,Right=right,Cardinality=cardinality,OrderSemantics=order};
    }

    private static NestedQueryNode NormalizeNested(NestedQueryNode node,int inputElementCount){
        var outer=NormalizeNode(node.Outer,inputElementCount);
        var inner=NormalizeNode(node.Inner,inputElementCount);
        var product=QueryCardinalityRange.Multiply(outer.Cardinality,inner.Cardinality);
        var cardinality=node.Arguments.Kind switch{
            QueryNestedKind.JoinXor=>new QueryCardinalityRange(0,product.Maximum),
            QueryNestedKind.SelectManyXor=>product,
            QueryNestedKind.GroupJoinXorCount=>outer.Cardinality,
            _=>throw new InvalidOperationException($"Unsupported nested kind: {node.Arguments.Kind}."),
        };
        var order=outer.OrderSemantics==QueryOrderSemantics.Unordered||inner.OrderSemantics==QueryOrderSemantics.Unordered
            ?QueryOrderSemantics.Unordered
            :QueryOrderSemantics.Ordered;
        return node with{Outer=outer,Inner=inner,Cardinality=cardinality,OrderSemantics=order};
    }

    private static TerminalQueryNode NormalizeTerminal(TerminalQueryNode node,int inputElementCount){
        var input=NormalizeNode(node.Input,inputElementCount);
        var shape=QueryIrValidator.GetTerminalShape(node.Arguments.Kind);
        var cardinality=shape.Cardinality==QueryCardinality.Scalar
            ?QueryCardinalityRange.Exact(1)
            :ExtendedTerminalShape.IsExtended(node.Arguments.Kind)
                ?ExtendedTerminalShape.Cardinality(node.Arguments,input.Cardinality)
            :node.Arguments.Kind==QueryTerminalKind.ToLookup
                ?new QueryCardinalityRange(input.Cardinality.Minimum>0?1:0,Math.Min(input.Cardinality.Maximum??4,4))
                :node.Arguments.Kind==QueryTerminalKind.ToHashSet
                    ?new QueryCardinalityRange(input.Cardinality.Minimum>0?1:0,input.Cardinality.Maximum)
                :input.Cardinality;
        var order=shape.Cardinality==QueryCardinality.Scalar
            ?QueryOrderSemantics.TotalOrdered
            :ExtendedTerminalShape.IsExtended(node.Arguments.Kind)
                ?ExtendedTerminalShape.Order(node.Arguments.Kind,input.OrderSemantics)
            :node.Arguments.Kind==QueryTerminalKind.ToHashSet
                ?QueryOrderSemantics.Unordered
                :node.Arguments.Kind is QueryTerminalKind.ToDictionary or QueryTerminalKind.ToLookup
                    ?QueryOrderSemantics.TotalOrdered
                    :input.OrderSemantics;
        var emptySafety=node.Arguments.Kind is QueryTerminalKind.Min or QueryTerminalKind.Max or QueryTerminalKind.MinBy or QueryTerminalKind.MaxBy or QueryTerminalKind.Average
            or QueryTerminalKind.First or QueryTerminalKind.Last or QueryTerminalKind.ElementAt
            or QueryTerminalKind.AverageSelector
            ?QueryEmptySafety.RequiresNonEmpty
            :QueryEmptySafety.Safe;
        return node with{
            Input=input,
            OutputType=shape.Type,
            OutputCardinality=shape.Cardinality,
            Cardinality=cardinality,
            OrderSemantics=order,
            EmptySafety=emptySafety,
        };
    }
}
