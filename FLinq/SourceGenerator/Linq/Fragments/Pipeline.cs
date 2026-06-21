using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Tsavorite.SourceGenerator;

namespace Tsavorite.SourceGenerator.Linq.Fragments;

public enum PipelineStageKind {
    Where,
    Select,
    SelectMany,
    Distinct,
    DistinctBy,
    Union,
    Except,
    Intersect,
    UnionBy,
    ExceptBy,
    IntersectBy,
    Concat,
    Append,
    Prepend,
    OrderBy,
    OrderByDescending,
    ThenBy,
    ThenByDescending,
    GroupBy,
    Reverse,
    DefaultIfEmpty,
    AsEnumerable,
    Take,
    Skip,
    TakeWhile,
    SkipWhile,
    Cast,
    OfType,
    SkipLast,
    TakeLast,
    TakeRange,
}

public enum PipelineTerminalKind {
    ToList,
    ToArray,
    Count,
    Any,
    First,
    ToDictionary,
    Sum,
}

public delegate bool PipelineEnumerableBuilder(ExpressionSyntax expression,ref int uniqueNumber,out Enumerable fragment,out string staticMethods);
public delegate bool PipelineFuncBuilder(ExpressionSyntax expression,ITypeSymbol delegateType,ref int uniqueNumber,out FuncFragment func);
public delegate bool PipelineElementTypeGetter(ITypeSymbol type,out ITypeSymbol elementType);

public sealed class PipelineBuildServices(
    SymbolDisplayFormat fullyQualifiedFormat,
    PipelineEnumerableBuilder buildEnumerable,
    PipelineFuncBuilder buildFunc,
    Func<FuncFragment,bool> canUseFunc,
    Func<ExpressionSyntax,string> renderExpression,
    PipelineElementTypeGetter tryGetEnumerableElementType,
    Func<ITypeSymbol,bool> containsAnonymousType,
    Func<string,string,string> joinStaticMethods) {
    public SymbolDisplayFormat FullyQualifiedFormat { get; } = fullyQualifiedFormat;
    public PipelineEnumerableBuilder BuildEnumerable { get; } = buildEnumerable;
    public PipelineFuncBuilder BuildFunc { get; } = buildFunc;
    public Func<FuncFragment,bool> CanUseFunc { get; } = canUseFunc;
    public Func<ExpressionSyntax,string> RenderExpression { get; } = renderExpression;
    public PipelineElementTypeGetter TryGetEnumerableElementType { get; } = tryGetEnumerableElementType;
    public Func<ITypeSymbol,bool> ContainsAnonymousType { get; } = containsAnonymousType;
    public Func<string,string,string> JoinStaticMethods { get; } = joinStaticMethods;
}

public static class PipelineBuilder {
    public static bool TryBuild(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        ref int uniqueNumber,
        out Fragment fragment,
        out string staticMethods,
        PipelineBuildServices services,
        List<string>? unsafeAccessorCollector = null) {
        fragment=null!;
        staticMethods=string.Empty;

        if(!TryExtract(semanticModel,invocation,out var sourceExpression,out var invocations)) {
            return false;
        }
        if(invocations.Count<1) {
            return false;
        }
        if(invocations.Count==1) {
            // 単段でも Range 上で消せる演算は Pipeline 側で受ける。
            // 単段 Where/Select 等は従来どおり Fragment 辞書経路に委ねる。
            if(semanticModel.GetSymbolInfo(invocations[0]).Symbol is not IMethodSymbol m0) {
                return false;
            }
            var n0=(m0.ReducedFrom ?? m0).Name;
            if(n0 is not ("Order" or "OrderDescending" or "OrderBy" or "OrderByDescending" or "Distinct" or "DistinctBy" or "Union" or "Except" or "Intersect" or "UnionBy" or "ExceptBy" or "IntersectBy")) {
                return false;
            }
        }

        var snapshot = uniqueNumber;
        if(!services.BuildEnumerable(sourceExpression,ref snapshot,out var sourceFragment,out var sourceStaticMethods)) {
            return false;
        }

        var stages = new List<PipelineStage>();
        var currentElementType = sourceFragment.ElementType;
        PipelineTerminalKind? terminalKind = null;
        string? terminalResultType = null;
        FuncFragment? terminalFunc1 = null;
        FuncFragment? terminalFunc2 = null;
        string? terminalComparer = null;
        for(var index = 0; index<invocations.Count; index++) {
            var currentInvocation = invocations[index];
            if(semanticModel.GetSymbolInfo(currentInvocation).Symbol is not IMethodSymbol method) {
                return false;
            }
            var methodName = (method.ReducedFrom ?? method).Name;
            var isLast = index==invocations.Count-1;
            if(TryGetTerminalKind(methodName,out var currentTerminalKind)) {
                if(!isLast) {
                    return false;
                }
                if(!TryAppendTerminalArguments(currentTerminalKind,semanticModel,currentInvocation,method,services,ref snapshot,stages,ref currentElementType,out terminalFunc1,out terminalFunc2,out terminalComparer)) {
                    return false;
                }
                terminalKind=currentTerminalKind;
                terminalResultType=semanticModel.GetTypeInfo(currentInvocation).ConvertedType?.ToDisplayString(services.FullyQualifiedFormat)
                    ?? semanticModel.GetTypeInfo(currentInvocation).Type?.ToDisplayString(services.FullyQualifiedFormat)
                    ?? "object";
                continue;
            }
            if(TryAppendStage(semanticModel,currentInvocation,method,services,ref snapshot,stages,ref currentElementType)) {
                continue;
            }
            if(TryAppendFragmentBackedStage(
                semanticModel,
                currentInvocation,
                method,
                services,
                ref snapshot,
                ref sourceFragment,
                ref sourceStaticMethods,
                stages,
                ref currentElementType,
                unsafeAccessorCollector)) {
                continue;
            }
            return false;
        }

        stages=NormalizeStages(stages,services.JoinStaticMethods);
        if(TryOptimizeRangeSetStages(sourceFragment,stages,ref snapshot,out var optimizedSetSource)) {
            AddUnsafeAccessors(stages,unsafeAccessorCollector);
            if(terminalKind is { } optimizedTerminal) {
                if(terminalResultType is null) {
                    return false;
                }
                var terminalPipeline = new PipelineEnumerable(snapshot++,optimizedSetSource,global::System.Array.Empty<PipelineStage>(),currentElementType);
                fragment=new PipelineTerminal(snapshot++,terminalPipeline,optimizedTerminal,terminalFunc1,terminalFunc2,terminalComparer);
            } else {
                fragment=optimizedSetSource;
            }
            uniqueNumber=snapshot;
            staticMethods=services.JoinStaticMethods(sourceStaticMethods,fragment.StructMethod);
            return true;
        }
        if(!ValidateStageCombination(stages,terminalKind)) {
            return false;
        }

        if(TryOptimizeOrderedRange(sourceFragment,stages,ref snapshot,out var optimizedSource)) {
            AddUnsafeAccessors(stages,unsafeAccessorCollector);
            if(terminalKind is { } optimizedTerminal) {
                if(terminalResultType is null) {
                    return false;
                }
                var terminalPipeline = new PipelineEnumerable(snapshot++,optimizedSource,global::System.Array.Empty<PipelineStage>(),currentElementType);
                fragment=new PipelineTerminal(snapshot++,terminalPipeline,optimizedTerminal,terminalFunc1,terminalFunc2,terminalComparer);
            } else {
                fragment=optimizedSource;
            }
            uniqueNumber=snapshot;
            staticMethods=services.JoinStaticMethods(sourceStaticMethods,fragment.StructMethod);
            return true;
        }

        var pipeline = new PipelineEnumerable(snapshot++,sourceFragment,stages.ToArray(),currentElementType);
        AddUnsafeAccessors(stages,unsafeAccessorCollector);
        if(terminalKind is { } terminal) {
            if(terminalResultType is null) {
                return false;
            }
            if(TsavoriteHashSetScanTerminal.TryCreate(snapshot,pipeline,terminal,out var scanTerminal)) {
                fragment=scanTerminal;
                snapshot++;
            } else {
                fragment=new PipelineTerminal(snapshot++,pipeline,terminal,terminalFunc1,terminalFunc2,terminalComparer);
            }
        } else {
            fragment=pipeline;
        }
        uniqueNumber=snapshot;
        staticMethods=services.JoinStaticMethods(sourceStaticMethods,fragment.StructMethod);
        // 安全網: 匿名型→record struct 置換で取りこぼした生の匿名型表示（<anonymous type: ...>）が
        // 生成コードに残ると不正な C# になるため、その場合はフュージョンを諦めて従来の例外スタブへフォールバックする。
        if(staticMethods.IndexOf("<anonymous type",StringComparison.Ordinal)>=0
            || currentElementType.IndexOf("<anonymous type",StringComparison.Ordinal)>=0) {
            return false;
        }
        return true;
    }

    private static bool TryOptimizeOrderedRange(
        Enumerable source,
        IReadOnlyList<PipelineStage> stages,
        ref int uniqueNumber,
        out Enumerable optimizedSource) {
        optimizedSource=null!;
        if(source is not RangeRoot range
            || stages.Count==0
            || !stages.All(IsIdentityOrderStage)) {
            return false;
        }

        var primaryOrders = stages
            .Where(static stage => stage.Kind is PipelineStageKind.OrderBy or PipelineStageKind.OrderByDescending)
            .ToArray();
        if(primaryOrders.Length==0) {
            return false;
        }

        optimizedSource=primaryOrders[^1].Descending
            ? new DescendingRangeRoot(uniqueNumber++,range.StartExpression,range.CountExpression)
            : range;
        return true;
    }

    private static bool IsIdentityOrderStage(PipelineStage stage) {
        if(stage.Kind is not (PipelineStageKind.OrderBy
            or PipelineStageKind.OrderByDescending
            or PipelineStageKind.ThenBy
            or PipelineStageKind.ThenByDescending)) {
            return false;
        }

        if(!string.Equals(stage.Value,"null",StringComparison.Ordinal)) {
            return false;
        }

        return stage.Func is null || stage.Func.IsIdentity;
    }

    private static bool TryOptimizeRangeSetStages(
        Enumerable source,
        IReadOnlyList<PipelineStage> stages,
        ref int uniqueNumber,
        out Enumerable optimizedSource) {
        optimizedSource=null!;
        if(source is not RangeRoot range || stages.Count==0) {
            return false;
        }

        Enumerable current=range;
        var changed=false;
        foreach(var stage in stages) {
            if(IsRangeDistinctNoOp(stage,current)) {
                changed=true;
                continue;
            }

            if(current is RangeRoot unionRange
                && IsRangeUnionNoOp(stage,unionRange)) {
                changed=true;
                continue;
            }

            if(TryCreateRangeExcept(stage,current,uniqueNumber,out var exceptRange)) {
                uniqueNumber++;
                current=exceptRange;
                changed=true;
                continue;
            }

            if(current is RangeRoot intersectRange
                && IsRangeIntersect(stage,intersectRange,out var second)) {
                current=CreateRangeIntersection(uniqueNumber++,intersectRange,second);
                changed=true;
                continue;
            }

            return false;
        }

        optimizedSource=current;
        return changed;
    }

    private static bool IsRangeDistinctNoOp(PipelineStage stage,Enumerable current) =>
        !stage.ContainsAnchor
        && stage.Value=="null"
        && current.ElementType=="int"
        && (stage.Kind==PipelineStageKind.Distinct
            || (stage.Kind==PipelineStageKind.DistinctBy
                && stage.Func is { IsIdentity: true }
                && stage.KeyType==current.ElementType));

    private static bool IsRangeIntersect(PipelineStage stage,RangeRoot current,out RangeRoot second) {
        second=null!;
        if(!TryGetIdentityRangeSetStage(stage,current.ElementType,out var secondRange)
            || stage.Kind is not (PipelineStageKind.Intersect or PipelineStageKind.IntersectBy)) {
            return false;
        }

        second=secondRange;
        return true;
    }

    private static bool IsRangeUnionNoOp(PipelineStage stage,RangeRoot current) {
        if(!TryGetIdentityRangeSetStage(stage,current.ElementType,out var second)
            || stage.Kind is not (PipelineStageKind.Union or PipelineStageKind.UnionBy)) {
            return false;
        }

        return IsSameRange(current,second)
            || IsOneBasedTrailingHalfRange(current,second);
    }

    private static bool TryCreateRangeExcept(PipelineStage stage,Enumerable current,int number,out RangeExceptRoot range) {
        range=null!;
        if(!TryGetIdentityRangeSetStage(stage,current.ElementType,out var second)
            || stage.Kind is not (PipelineStageKind.Except or PipelineStageKind.ExceptBy)) {
            return false;
        }

        range=current switch {
            RangeRoot currentRange => new RangeExceptRoot(number,currentRange,[second]),
            RangeExceptRoot existing => existing.AddExclusion(number,second),
            _ => null!
        };
        return range is not null;
    }

    private static bool TryGetIdentityRangeSetStage(PipelineStage stage,string currentElementType,out RangeRoot second) {
        second=null!;
        if(stage.ContainsAnchor
            || stage.Value!="null"
            || currentElementType!="int"
            || stage.Kind is not (PipelineStageKind.Union or PipelineStageKind.Except or PipelineStageKind.Intersect or PipelineStageKind.UnionBy or PipelineStageKind.ExceptBy or PipelineStageKind.IntersectBy)
            || stage.SecondSource is not RangeRoot secondRange) {
            return false;
        }

        if(stage.Kind is PipelineStageKind.UnionBy or PipelineStageKind.ExceptBy or PipelineStageKind.IntersectBy
            && (stage.Func is not { IsIdentity: true } || stage.KeyType!=currentElementType)) {
            return false;
        }

        second=secondRange;
        return true;
    }

    private static RangeRoot CreateRangeIntersection(int number,RangeRoot left,RangeRoot right) {
        var start=MaxExpression(left.StartExpression,right.StartExpression);
        var end=MinLongExpression(
            ExclusiveEndExpression(left.StartExpression,left.CountExpression),
            ExclusiveEndExpression(right.StartExpression,right.CountExpression));
        var count=$"(int)global::System.Math.Max(0L,{end}-(long)({start}))";
        return new RangeRoot(number,start,count);
    }

    private static string MaxExpression(string left,string right) =>
        $"global::System.Math.Max(({left}),({right}))";

    private static string MinLongExpression(string left,string right) =>
        $"global::System.Math.Min({left},{right})";

    private static string ExclusiveEndExpression(string start,string count) =>
        $"((long)({start})+({count}))";

    private static bool IsSameRange(RangeRoot left,RangeRoot right) =>
        string.Equals(NormalizeRangeExpression(left.StartExpression),NormalizeRangeExpression(right.StartExpression),StringComparison.Ordinal)
        && string.Equals(NormalizeRangeExpression(left.CountExpression),NormalizeRangeExpression(right.CountExpression),StringComparison.Ordinal);

    private static bool IsOneBasedTrailingHalfRange(RangeRoot current,RangeRoot second) {
        var currentStart=NormalizeRangeExpression(current.StartExpression);
        if(currentStart!="1") {
            return false;
        }

        var currentCount=NormalizeRangeExpression(current.CountExpression);
        var secondStart=NormalizeRangeExpression(second.StartExpression);
        var secondCount=NormalizeRangeExpression(second.CountExpression);
        return IsHalfExpression(currentCount,secondCount)
            && IsAddOneExpression(secondCount,secondStart);
    }

    private static bool IsHalfExpression(string sourceCount,string candidate) =>
        string.Equals(candidate,$"{sourceCount}/2",StringComparison.Ordinal)
        || string.Equals(candidate,$"({sourceCount})/2",StringComparison.Ordinal);

    private static bool IsAddOneExpression(string source,string candidate) =>
        string.Equals(candidate,$"{source}+1",StringComparison.Ordinal)
        || string.Equals(candidate,$"({source})+1",StringComparison.Ordinal);

    private static string NormalizeRangeExpression(string expression) =>
        new(expression.Where(static c => !char.IsWhiteSpace(c)).ToArray());

    private static bool TryAppendFragmentBackedStage(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        PipelineBuildServices services,
        ref int uniqueNumber,
        ref Enumerable sourceFragment,
        ref string sourceStaticMethods,
        List<PipelineStage> stages,
        ref string currentElementType,
        List<string>? unsafeAccessorCollector) {
        var methodName = (method.ReducedFrom ?? method).Name;
        if(methodName is not ("Chunk" or "Zip" or "AggregateBy" or "Join" or "GroupJoin")) {
            return false;
        }
        if(stages.Count>0) {
            if(!ValidateStageCombination(stages,null,allowSingleStage: true)) {
                return false;
            }
            var pipeline = new PipelineEnumerable(uniqueNumber++,sourceFragment,stages.ToArray(),currentElementType);
            AddUnsafeAccessors(stages,unsafeAccessorCollector);
            sourceStaticMethods=services.JoinStaticMethods(sourceStaticMethods,pipeline.StructMethod);
            sourceFragment=pipeline;
            stages.Clear();
        }
        if(!TryBuildFragmentBackedStage(semanticModel,invocation,method,services,ref uniqueNumber,sourceFragment,out var nextSource,out var nextStaticMethods,ref currentElementType)) {
            throw Unsupported($"method '{methodName}' could not be built as a Fragment-backed Chain IR stage.");
        }
        sourceStaticMethods=services.JoinStaticMethods(sourceStaticMethods,nextStaticMethods);
        sourceStaticMethods=services.JoinStaticMethods(sourceStaticMethods,nextSource.StructMethod);
        sourceFragment=nextSource;
        return true;
    }

    private static bool TryBuildFragmentBackedStage(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        PipelineBuildServices services,
        ref int uniqueNumber,
        Enumerable sourceFragment,
        out Enumerable nextSource,
        out string staticMethods,
        ref string currentElementType) {
        nextSource=null!;
        staticMethods=string.Empty;
        var methodName = (method.ReducedFrom ?? method).Name;
        switch(methodName) {
            case "Chunk":
                return TryBuildChunkFragment(invocation,method,services,ref uniqueNumber,sourceFragment,out nextSource,ref currentElementType);
            case "Zip":
                return TryBuildZipFragment(invocation,method,services,ref uniqueNumber,sourceFragment,out nextSource,out staticMethods,ref currentElementType);
            case "AggregateBy":
                return TryBuildAggregateByFragment(semanticModel,invocation,method,services,ref uniqueNumber,sourceFragment,out nextSource,ref currentElementType);
            case "CountBy":
                return TryBuildCountByFragment(semanticModel,invocation,method,services,ref uniqueNumber,sourceFragment,out nextSource,ref currentElementType);
            case "Index":
                return TryBuildIndexFragment(semanticModel,invocation,method,services,ref uniqueNumber,sourceFragment,out nextSource,ref currentElementType);
            case "Join":
                return TryBuildJoinFragment(invocation,method,services,ref uniqueNumber,sourceFragment,out nextSource,out staticMethods,ref currentElementType);
            case "GroupJoin":
                return TryBuildGroupJoinFragment(invocation,method,services,ref uniqueNumber,sourceFragment,out nextSource,out staticMethods,ref currentElementType);
            default:
                return false;
        }
    }

    private static bool TryBuildChunkFragment(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        PipelineBuildServices services,
        ref int uniqueNumber,
        Enumerable sourceFragment,
        out Enumerable nextSource,
        ref string currentElementType) {
        nextSource=null!;
        if(invocation.ArgumentList.Arguments.Count!=1
            || method.Parameters.Length==0
            || method.Parameters[0].Type.SpecialType!=SpecialType.System_Int32) {
            return false;
        }
        nextSource=new Chunk(uniqueNumber++,sourceFragment,services.RenderExpression(invocation.ArgumentList.Arguments[0].Expression));
        currentElementType=nextSource.ElementType;
        return true;
    }

    private static bool TryBuildZipFragment(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        PipelineBuildServices services,
        ref int uniqueNumber,
        Enumerable sourceFragment,
        out Enumerable nextSource,
        out string staticMethods,
        ref string currentElementType) {
        nextSource=null!;
        staticMethods=string.Empty;
        if(invocation.ArgumentList.Arguments.Count is not (1 or 2)
            || method.Parameters.Length<1) {
            return false;
        }
        if(!services.BuildEnumerable(invocation.ArgumentList.Arguments[0].Expression,ref uniqueNumber,out var secondSource,out var secondStaticMethods)) {
            return false;
        }
        staticMethods=secondStaticMethods;
        if(invocation.ArgumentList.Arguments.Count==1) {
            nextSource=new Zip(uniqueNumber++,sourceFragment,secondSource);
            currentElementType=nextSource.ElementType;
            return true;
        }
        // 3-sequence Zip: Zip(second, third) — parameters[1] is IEnumerable<T3>, not Func
        if(method.Parameters.Length>=2
            && method.Parameters[1].Type is INamedTypeSymbol { TypeArguments.Length: 1 }) {
            if(!services.BuildEnumerable(invocation.ArgumentList.Arguments[1].Expression,ref uniqueNumber,out var thirdSource,out var thirdStaticMethods)) {
                return false;
            }
            staticMethods=services.JoinStaticMethods(staticMethods,thirdStaticMethods);
            nextSource=new Zip3(uniqueNumber++,sourceFragment,secondSource,thirdSource);
            currentElementType=nextSource.ElementType;
            return true;
        }
        if(method.Parameters.Length<2
            || method.Parameters[1].Type is not INamedTypeSymbol { TypeArguments.Length: 3 }) {
            return false;
        }
        if(!services.BuildFunc(invocation.ArgumentList.Arguments[1].Expression,method.Parameters[1].Type,ref uniqueNumber,out var resultSelector)
            || !services.CanUseFunc(resultSelector)) {
            return false;
        }
        nextSource=new Zip_resultSelector(uniqueNumber++,sourceFragment,secondSource,resultSelector);
        currentElementType=nextSource.ElementType;
        return true;
    }

    private static bool TryBuildAggregateByFragment(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        PipelineBuildServices services,
        ref int uniqueNumber,
        Enumerable sourceFragment,
        out Enumerable nextSource,
        ref string currentElementType) {
        nextSource=null!;
        if(invocation.ArgumentList.Arguments.Count!=3
            || method.Parameters.Length<3
            || method.Parameters[0].Type is not INamedTypeSymbol { TypeArguments.Length: 2 }
            || method.Parameters[2].Type is not INamedTypeSymbol { TypeArguments.Length: 3 }) {
            return false;
        }
        if(!services.BuildFunc(invocation.ArgumentList.Arguments[0].Expression,method.Parameters[0].Type,ref uniqueNumber,out var keySelector)
            || !services.CanUseFunc(keySelector)
            || !services.BuildFunc(invocation.ArgumentList.Arguments[2].Expression,method.Parameters[2].Type,ref uniqueNumber,out var func)
            || !services.CanUseFunc(func)
            || !services.TryGetEnumerableElementType(method.ReturnType,out var resultElementType)
            || services.ContainsAnonymousType(resultElementType)) {
            return false;
        }
        var resultElementTypeName = resultElementType.ToDisplayString(services.FullyQualifiedFormat);
        nextSource=new AggregateBy(uniqueNumber++,sourceFragment,keySelector,services.RenderExpression(invocation.ArgumentList.Arguments[1].Expression),func,resultElementTypeName);
        currentElementType=nextSource.ElementType;
        return true;
    }

    private static bool TryBuildCountByFragment(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        PipelineBuildServices services,
        ref int uniqueNumber,
        Enumerable sourceFragment,
        out Enumerable nextSource,
        ref string currentElementType) {
        nextSource=null!;
        if(invocation.ArgumentList.Arguments.Count!=1
            || method.Parameters.Length<1
            || method.Parameters[0].Type is not INamedTypeSymbol { TypeArguments.Length: 2 }) {
            return false;
        }
        if(!services.BuildFunc(invocation.ArgumentList.Arguments[0].Expression,method.Parameters[0].Type,ref uniqueNumber,out var keySelector)
            || !services.CanUseFunc(keySelector)
            || !services.TryGetEnumerableElementType(method.ReturnType,out var resultElementType)
            || services.ContainsAnonymousType(resultElementType)) {
            return false;
        }
        nextSource=new CountBy(uniqueNumber++,sourceFragment,keySelector);
        currentElementType=nextSource.ElementType;
        return true;
    }

    private static bool TryBuildIndexFragment(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        PipelineBuildServices services,
        ref int uniqueNumber,
        Enumerable sourceFragment,
        out Enumerable nextSource,
        ref string currentElementType) {
        nextSource=null!;
        if(invocation.ArgumentList.Arguments.Count!=0
            || !services.TryGetEnumerableElementType(method.ReturnType,out var resultElementType)
            || services.ContainsAnonymousType(resultElementType)) {
            return false;
        }
        nextSource=new Index(uniqueNumber++,sourceFragment);
        currentElementType=nextSource.ElementType;
        return true;
    }

    private static bool TryBuildJoinFragment(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        PipelineBuildServices services,
        ref int uniqueNumber,
        Enumerable sourceFragment,
        out Enumerable nextSource,
        out string staticMethods,
        ref string currentElementType) {
        return TryBuildJoinLikeFragment(false,invocation,method,services,ref uniqueNumber,sourceFragment,out nextSource,out staticMethods,ref currentElementType);
    }

    private static bool TryBuildGroupJoinFragment(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        PipelineBuildServices services,
        ref int uniqueNumber,
        Enumerable sourceFragment,
        out Enumerable nextSource,
        out string staticMethods,
        ref string currentElementType) {
        return TryBuildJoinLikeFragment(true,invocation,method,services,ref uniqueNumber,sourceFragment,out nextSource,out staticMethods,ref currentElementType);
    }

    private static bool TryBuildJoinLikeFragment(
        bool groupJoin,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        PipelineBuildServices services,
        ref int uniqueNumber,
        Enumerable sourceFragment,
        out Enumerable nextSource,
        out string staticMethods,
        ref string currentElementType) {
        nextSource=null!;
        staticMethods=string.Empty;
        if(invocation.ArgumentList.Arguments.Count is not (4 or 5)
            || method.Parameters.Length<4
            || method.Parameters[1].Type is not INamedTypeSymbol { TypeArguments.Length: 2 }
            || method.Parameters[2].Type is not INamedTypeSymbol { TypeArguments.Length: 2 }
            || method.Parameters[3].Type is not INamedTypeSymbol { TypeArguments.Length: 3 }) {
            return false;
        }
        if(!services.BuildEnumerable(invocation.ArgumentList.Arguments[0].Expression,ref uniqueNumber,out var innerSource,out var innerStaticMethods)) {
            return false;
        }
        staticMethods=innerStaticMethods;
        if(!services.BuildFunc(invocation.ArgumentList.Arguments[1].Expression,method.Parameters[1].Type,ref uniqueNumber,out var outerKeySelector)
            || !services.CanUseFunc(outerKeySelector)
            || !services.BuildFunc(invocation.ArgumentList.Arguments[2].Expression,method.Parameters[2].Type,ref uniqueNumber,out var innerKeySelector)
            || !services.CanUseFunc(innerKeySelector)
            || !services.BuildFunc(invocation.ArgumentList.Arguments[3].Expression,method.Parameters[3].Type,ref uniqueNumber,out var resultSelector)
            || !services.CanUseFunc(resultSelector)) {
            return false;
        }
        var comparer = "null";
        if(invocation.ArgumentList.Arguments.Count==5) {
            if(method.Parameters.Length<5) {
                return false;
            }
            comparer=services.RenderExpression(invocation.ArgumentList.Arguments[4].Expression);
        }
        if(groupJoin) {
            nextSource=new GroupJoin(uniqueNumber++,sourceFragment,innerSource,outerKeySelector,innerKeySelector,resultSelector,comparer);
        } else {
            nextSource=new Join(uniqueNumber++,sourceFragment,innerSource,outerKeySelector,innerKeySelector,resultSelector,comparer);
        }
        currentElementType=nextSource.ElementType;
        return true;
    }

    private static bool TryExtract(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        out ExpressionSyntax sourceExpression,
        out List<InvocationExpressionSyntax> invocations) {
        sourceExpression=null!;
        invocations=[];
        ExpressionSyntax current = invocation;
        while(current is InvocationExpressionSyntax currentInvocation
            && currentInvocation.Expression is MemberAccessExpressionSyntax { Expression: var receiverExpression }) {
            if(semanticModel.GetSymbolInfo(currentInvocation).Symbol is not IMethodSymbol method
                || method.ReducedFrom is null) {
                break;
            }
            var containingType = (method.ReducedFrom ?? method).ContainingType.ToDisplayString();
            if(containingType!="System.Linq.Enumerable"
                && containingType!="Tsavorite.Linq.Enumerable") {
                break;
            }
            invocations.Add(currentInvocation);
            current=RemoveParentheses(receiverExpression);
        }

        if(invocations.Count==0) {
            return false;
        }
        invocations.Reverse();
        sourceExpression=RemoveParentheses(current);
        return true;
    }

    private static bool TryGetTerminalKind(string methodName,out PipelineTerminalKind terminalKind) {
        switch(methodName) {
            case "ToList":
                terminalKind=PipelineTerminalKind.ToList;
                return true;
            case "ToArray":
                terminalKind=PipelineTerminalKind.ToArray;
                return true;
            case "Count":
                terminalKind=PipelineTerminalKind.Count;
                return true;
            case "Any":
                terminalKind=PipelineTerminalKind.Any;
                return true;
            case "First":
                terminalKind=PipelineTerminalKind.First;
                return true;
            case "ToDictionary":
                terminalKind=PipelineTerminalKind.ToDictionary;
                return true;
            case "Sum":
                terminalKind=PipelineTerminalKind.Sum;
                return true;
            default:
                terminalKind=default;
                return false;
        }
    }

    private static bool TryAppendTerminalArguments(
        PipelineTerminalKind terminalKind,
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        PipelineBuildServices services,
        ref int uniqueNumber,
        List<PipelineStage> stages,
        ref string currentElementType,
        out FuncFragment? func1,
        out FuncFragment? func2,
        out string? comparerValue) {
        func1=null;
        func2=null;
        comparerValue=null;
        if(terminalKind is PipelineTerminalKind.ToList or PipelineTerminalKind.ToArray) {
            return invocation.ArgumentList.Arguments.Count==0;
        }
        if(terminalKind==PipelineTerminalKind.ToDictionary) {
            return TryAppendToDictionaryTerminalArguments(semanticModel,invocation,method,services,ref uniqueNumber,out func1,out func2,out comparerValue);
        }
        if(terminalKind==PipelineTerminalKind.Sum) {
            // Sum(selector): add Select stage, then Sum() is applied to selected values.
            if(invocation.ArgumentList.Arguments.Count==0) {
                return true;
            }
            if(invocation.ArgumentList.Arguments.Count!=1
                || method.Parameters.Length==0
                || method.Parameters[0].Type is not INamedTypeSymbol { TypeArguments.Length: 2 } selectorType) {
                return false;
            }
            if(!services.BuildFunc(invocation.ArgumentList.Arguments[0].Expression,selectorType,ref uniqueNumber,out var sumSelector)
                || !services.CanUseFunc(sumSelector)) {
                return false;
            }
            var sumSelectElementType = sumSelector.ReturnTypeName;
            stages.Add(new PipelineStage(PipelineStageKind.Select,sumSelector,null,currentElementType,sumSelectElementType));
            currentElementType=sumSelectElementType;
            return true;
        }
        if(invocation.ArgumentList.Arguments.Count==0) {
            return true;
        }
        if(invocation.ArgumentList.Arguments.Count!=1
            || method.Parameters.Length==0
            || method.Parameters[0].Type is not INamedTypeSymbol { TypeArguments.Length: 2 }) {
            return false;
        }
        if(!services.BuildFunc(invocation.ArgumentList.Arguments[0].Expression,method.Parameters[0].Type,ref uniqueNumber,out var predicate)
            || !services.CanUseFunc(predicate)) {
            return false;
        }
        stages.Add(new PipelineStage(PipelineStageKind.Where,predicate,null,currentElementType,currentElementType));
        return true;
    }

    private static bool TryAppendToDictionaryTerminalArguments(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        PipelineBuildServices services,
        ref int uniqueNumber,
        out FuncFragment? func1,
        out FuncFragment? func2,
        out string? comparerValue) {
        func1=null;
        func2=null;
        comparerValue=null;
        if(invocation.ArgumentList.Arguments.Count==0 || method.Parameters.Length==0) {
            return false;
        }
        // First arg is always keySelector Func<TSource,TKey>
        if(method.Parameters[0].Type is not INamedTypeSymbol { TypeArguments.Length: 2 } keySelectorType) {
            return false;
        }
        if(!services.BuildFunc(invocation.ArgumentList.Arguments[0].Expression,keySelectorType,ref uniqueNumber,out func1)
            || !services.CanUseFunc(func1)) {
            return false;
        }
        if(invocation.ArgumentList.Arguments.Count==1) {
            return true;
        }
        // Second arg: elementSelector Func<TSource,TElement> or IEqualityComparer<TKey>
        if(invocation.ArgumentList.Arguments.Count>=2 && method.Parameters.Length>=2) {
            if(method.Parameters[1].Type is INamedTypeSymbol { TypeArguments.Length: 2 } elemSelectorType) {
                if(!services.BuildFunc(invocation.ArgumentList.Arguments[1].Expression,elemSelectorType,ref uniqueNumber,out func2)
                    || !services.CanUseFunc(func2)) {
                    return false;
                }
                if(invocation.ArgumentList.Arguments.Count==3 && method.Parameters.Length>=3) {
                    comparerValue=services.RenderExpression(invocation.ArgumentList.Arguments[2].Expression);
                }
                return true;
            }
            // Second arg is comparer
            comparerValue=services.RenderExpression(invocation.ArgumentList.Arguments[1].Expression);
            return true;
        }
        return true;
    }

    private static bool TryAppendStage(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        PipelineBuildServices services,
        ref int uniqueNumber,
        List<PipelineStage> stages,
        ref string currentElementType) {
        var methodName = (method.ReducedFrom ?? method).Name;
        switch(methodName) {
            case "Where":
                if(invocation.ArgumentList.Arguments.Count!=1
                    || method.Parameters.Length==0
                    || method.Parameters[0].Type is not INamedTypeSymbol { TypeArguments.Length: 2 or 3 } predicateType) {
                    return false;
                }
                if(!services.BuildFunc(invocation.ArgumentList.Arguments[0].Expression,predicateType,ref uniqueNumber,out var predicate)) {
                    return false;
                }
                if(!services.CanUseFunc(predicate)) {
                    return false;
                }
                stages.Add(new PipelineStage(PipelineStageKind.Where,predicate,null,currentElementType,currentElementType,usesIndex: predicateType.TypeArguments.Length==3));
                return true;
            case "Select":
                if(invocation.ArgumentList.Arguments.Count!=1
                    || method.Parameters.Length==0
                    || method.Parameters[0].Type is not INamedTypeSymbol { TypeArguments.Length: 2 or 3 } selectorType) {
                    return false;
                }
                if(!services.BuildFunc(invocation.ArgumentList.Arguments[0].Expression,selectorType,ref uniqueNumber,out var selector)) {
                    return false;
                }
                if(!services.CanUseFunc(selector)) {
                    return false;
                }
                var selectElementType = selector.ReturnTypeName;
                stages.Add(new PipelineStage(PipelineStageKind.Select,selector,null,currentElementType,selectElementType,usesIndex: selectorType.TypeArguments.Length==3));
                currentElementType=selectElementType;
                return true;
            case "SelectMany":
                return TryAppendSelectManyStage(semanticModel,invocation,method,services,ref uniqueNumber,stages,ref currentElementType);
            case "Distinct":
                return TryAppendDistinctStage(invocation,method,services,stages,currentElementType);
            case "DistinctBy":
                return TryAppendDistinctByStage(invocation,method,services,ref uniqueNumber,stages,currentElementType);
            case "Union":
                return TryAppendBinarySetStage(PipelineStageKind.Union,invocation,method,services,ref uniqueNumber,stages,currentElementType);
            case "Except":
                return TryAppendBinarySetStage(PipelineStageKind.Except,invocation,method,services,ref uniqueNumber,stages,currentElementType);
            case "Intersect":
                return TryAppendBinarySetStage(PipelineStageKind.Intersect,invocation,method,services,ref uniqueNumber,stages,currentElementType);
            case "UnionBy":
                return TryAppendBinarySetByStage(PipelineStageKind.UnionBy,invocation,method,services,ref uniqueNumber,stages,currentElementType);
            case "ExceptBy":
                return TryAppendBinarySetByStage(PipelineStageKind.ExceptBy,invocation,method,services,ref uniqueNumber,stages,currentElementType);
            case "IntersectBy":
                return TryAppendBinarySetByStage(PipelineStageKind.IntersectBy,invocation,method,services,ref uniqueNumber,stages,currentElementType);
            case "Concat":
                return TryAppendConcatStage(invocation,services,ref uniqueNumber,stages,currentElementType);
            case "Append":
                return TryAppendElementStage(PipelineStageKind.Append,invocation,services,stages,currentElementType);
            case "Prepend":
                return TryAppendElementStage(PipelineStageKind.Prepend,invocation,services,stages,currentElementType);
            case "OrderBy":
                return TryAppendOrderStage(PipelineStageKind.OrderBy,invocation,method,services,ref uniqueNumber,stages,currentElementType);
            case "OrderByDescending":
                return TryAppendOrderStage(PipelineStageKind.OrderByDescending,invocation,method,services,ref uniqueNumber,stages,currentElementType);
            case "ThenBy":
                return TryAppendOrderStage(PipelineStageKind.ThenBy,invocation,method,services,ref uniqueNumber,stages,currentElementType);
            case "ThenByDescending":
                return TryAppendOrderStage(PipelineStageKind.ThenByDescending,invocation,method,services,ref uniqueNumber,stages,currentElementType);
            case "GroupBy":
                return TryAppendGroupByStage(invocation,method,services,ref uniqueNumber,stages,ref currentElementType);
            case "Reverse":
                stages.Add(new PipelineStage(PipelineStageKind.Reverse,null,null,currentElementType,currentElementType));
                return true;
            case "DefaultIfEmpty":
                return TryAppendDefaultIfEmptyStage(invocation,services,stages,currentElementType);
            case "AsEnumerable":
                stages.Add(new PipelineStage(PipelineStageKind.AsEnumerable,null,null,currentElementType,currentElementType));
                return true;
            case "Skip":
                return TryAppendCountStage(PipelineStageKind.Skip,invocation,method,services,stages,currentElementType);
            case "TakeWhile":
                return TryAppendPredicateStage(PipelineStageKind.TakeWhile,invocation,method,services,ref uniqueNumber,stages,currentElementType);
            case "SkipWhile":
                return TryAppendPredicateStage(PipelineStageKind.SkipWhile,invocation,method,services,ref uniqueNumber,stages,currentElementType);
            case "Cast":
                return TryAppendTypeStage(PipelineStageKind.Cast,method,services,stages,ref currentElementType);
            case "OfType":
                return TryAppendTypeStage(PipelineStageKind.OfType,method,services,stages,ref currentElementType);
            case "Order":
            case "OrderDescending": {
                var descending = methodName=="OrderDescending";
                var kind = descending ? PipelineStageKind.OrderByDescending : PipelineStageKind.OrderBy;
                var comparer = "null";
                var containsAnchor = false;
                if(invocation.ArgumentList.Arguments.Count==1
                    && method.Parameters.Length>=1) {
                    var comparerExpression=invocation.ArgumentList.Arguments[0].Expression;
                    comparer=services.RenderExpression(comparerExpression);
                    containsAnchor=AnchorAnalysis.ContainsAnchor(comparerExpression,null);
                } else if(invocation.ArgumentList.Arguments.Count>1) {
                    return false;
                }
                // Func=null signals identity key (element itself)
                stages.Add(new PipelineStage(kind,null,comparer,currentElementType,currentElementType,keyType: currentElementType,descending: descending,containsAnchor: containsAnchor));
                return true;
            }
            case "SkipLast":
                if(invocation.ArgumentList.Arguments.Count!=1
                    || method.Parameters.Length==0
                    || method.Parameters[0].Type.SpecialType!=SpecialType.System_Int32) {
                    return false;
                }
                var skipLastExpression=invocation.ArgumentList.Arguments[0].Expression;
                stages.Add(new PipelineStage(PipelineStageKind.SkipLast,null,services.RenderExpression(skipLastExpression),currentElementType,currentElementType,containsAnchor: AnchorAnalysis.ContainsAnchor(skipLastExpression,null)));
                return true;
            case "TakeLast":
                if(invocation.ArgumentList.Arguments.Count!=1
                    || method.Parameters.Length==0
                    || method.Parameters[0].Type.SpecialType!=SpecialType.System_Int32) {
                    return false;
                }
                var takeLastExpression=invocation.ArgumentList.Arguments[0].Expression;
                stages.Add(new PipelineStage(PipelineStageKind.TakeLast,null,services.RenderExpression(takeLastExpression),currentElementType,currentElementType,containsAnchor: AnchorAnalysis.ContainsAnchor(takeLastExpression,null)));
                return true;
            case "Take":
                if(invocation.ArgumentList.Arguments.Count!=1 || method.Parameters.Length==0) {
                    return false;
                }
                if(method.Parameters[0].Type.SpecialType==SpecialType.System_Int32) {
                    return TryAppendCountStage(PipelineStageKind.Take,invocation,method,services,stages,currentElementType);
                }
                // Range overload
                if(method.Parameters[0].Type.ToDisplayString()=="global::System.Range"
                    || method.Parameters[0].Type.ToDisplayString()=="System.Range") {
                    var takeRangeExpression=invocation.ArgumentList.Arguments[0].Expression;
                    stages.Add(new PipelineStage(PipelineStageKind.TakeRange,null,services.RenderExpression(takeRangeExpression),currentElementType,currentElementType,containsAnchor: AnchorAnalysis.ContainsAnchor(takeRangeExpression,null)));
                    return true;
                }
                return false;
            default:
                return false;
        }
    }

    private static bool TryAppendCountStage(
        PipelineStageKind kind,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        PipelineBuildServices services,
        List<PipelineStage> stages,
        string currentElementType) {
        if(invocation.ArgumentList.Arguments.Count!=1
            || method.Parameters.Length==0
            || method.Parameters[0].Type.SpecialType!=SpecialType.System_Int32) {
            return false;
        }
        var countExpression=invocation.ArgumentList.Arguments[0].Expression;
        var count = services.RenderExpression(countExpression);
        stages.Add(new PipelineStage(kind,null,count,currentElementType,currentElementType,containsAnchor: AnchorAnalysis.ContainsAnchor(countExpression,null)));
        return true;
    }

    private static bool TryAppendPredicateStage(
        PipelineStageKind kind,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        PipelineBuildServices services,
        ref int uniqueNumber,
        List<PipelineStage> stages,
        string currentElementType) {
        if(invocation.ArgumentList.Arguments.Count!=1
            || method.Parameters.Length==0
            || method.Parameters[0].Type is not INamedTypeSymbol { TypeArguments.Length: 2 or 3 } predicateType) {
            return false;
        }
        if(!services.BuildFunc(invocation.ArgumentList.Arguments[0].Expression,predicateType,ref uniqueNumber,out var predicate)
            || !services.CanUseFunc(predicate)) {
            return false;
        }
        stages.Add(new PipelineStage(kind,predicate,null,currentElementType,currentElementType,usesIndex: predicateType.TypeArguments.Length==3));
        return true;
    }

    private static bool TryAppendTypeStage(
        PipelineStageKind kind,
        IMethodSymbol method,
        PipelineBuildServices services,
        List<PipelineStage> stages,
        ref string currentElementType) {
        if(!services.TryGetEnumerableElementType(method.ReturnType,out var resultElementType)
            || services.ContainsAnonymousType(resultElementType)) {
            return false;
        }
        var outputType = resultElementType.ToDisplayString(services.FullyQualifiedFormat);
        stages.Add(new PipelineStage(kind,null,null,currentElementType,outputType));
        currentElementType=outputType;
        return true;
    }

    private static bool TryAppendSelectManyStage(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        PipelineBuildServices services,
        ref int uniqueNumber,
        List<PipelineStage> stages,
        ref string currentElementType) {
        // 匿名型要素は型名を生成コードに書けず ToList 等の終端で破綻するため、
        // SelectMany インライン化（record struct 置換 + 出口 Select）のフォールバック経路へ委ねる。
        if(method.TypeArguments.Any(services.ContainsAnonymousType)) {
            return false;
        }
        if(invocation.ArgumentList.Arguments.Count is not (1 or 2)
            || method.Parameters.Length!=invocation.ArgumentList.Arguments.Count
            || method.Parameters[0].Type is not INamedTypeSymbol collectionSelectorType) {
            return false;
        }
        var typeArgumentLength = collectionSelectorType.TypeArguments.Length;
        if(typeArgumentLength is not (2 or 3)) {
            return false;
        }
        var usesIndex = typeArgumentLength==3;
        if(!services.BuildFunc(invocation.ArgumentList.Arguments[0].Expression,method.Parameters[0].Type,ref uniqueNumber,out var collectionSelector)
            || !services.CanUseFunc(collectionSelector)
            || collectionSelector.RequiresStatementInlining) {
            return false;
        }
        if(!TryGetSequenceElementType(collectionSelector.ReturnTypeName,out var innerElementType)) {
            return false;
        }
        var innerIsArray = invocation.ArgumentList.Arguments[0].Expression is LambdaExpressionSyntax { Body: ExpressionSyntax collectionSelectorBodyExpr }
            && semanticModel.GetTypeInfo(collectionSelectorBodyExpr).Type is IArrayTypeSymbol;

        FuncFragment? resultSelector = null;
        var outputType = innerElementType;
        if(invocation.ArgumentList.Arguments.Count==2) {
            if(method.Parameters[1].Type is not INamedTypeSymbol { TypeArguments.Length: 3 }) {
                return false;
            }
            if(!services.BuildFunc(invocation.ArgumentList.Arguments[1].Expression,method.Parameters[1].Type,ref uniqueNumber,out resultSelector)
                || !services.CanUseFunc(resultSelector)) {
                return false;
            }
            outputType=resultSelector.ReturnTypeName;
        }

        stages.Add(new PipelineStage(
            PipelineStageKind.SelectMany,
            collectionSelector,
            null,
            currentElementType,
            outputType,
            secondFunc: resultSelector,
            innerElementType: innerElementType,
            keyType: null,
            usesIndex: usesIndex,
            innerIsArray: innerIsArray));
        currentElementType=outputType;
        return true;
    }

    private static bool TryGetSequenceElementType(string sequenceTypeName,out string elementTypeName) {
        elementTypeName=string.Empty;
        if(sequenceTypeName.EndsWith("[]",StringComparison.Ordinal)) {
            elementTypeName=sequenceTypeName[..^2];
            return true;
        }
        const string enumerablePrefix = "global::System.Collections.Generic.IEnumerable<";
        if(sequenceTypeName.StartsWith(enumerablePrefix,StringComparison.Ordinal)
            && sequenceTypeName.EndsWith(">",StringComparison.Ordinal)) {
            elementTypeName=sequenceTypeName[enumerablePrefix.Length..^1];
            return true;
        }
        return false;
    }

    private static bool TryAppendDistinctStage(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        PipelineBuildServices services,
        List<PipelineStage> stages,
        string currentElementType) {
        if(invocation.ArgumentList.Arguments.Count>1) {
            return false;
        }
        var comparer = "null";
        var containsAnchor = false;
        if(invocation.ArgumentList.Arguments.Count==1) {
            if(method.Parameters.Length<1) {
                return false;
            }
            var comparerExpression=invocation.ArgumentList.Arguments[0].Expression;
            comparer=services.RenderExpression(comparerExpression);
            containsAnchor=AnchorAnalysis.ContainsAnchor(comparerExpression,null);
        }
        stages.Add(new PipelineStage(PipelineStageKind.Distinct,null,comparer,currentElementType,currentElementType,keyType: currentElementType,containsAnchor: containsAnchor));
        return true;
    }

    private static bool TryAppendDistinctByStage(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        PipelineBuildServices services,
        ref int uniqueNumber,
        List<PipelineStage> stages,
        string currentElementType) {
        if(invocation.ArgumentList.Arguments.Count is not (1 or 2)
            || method.Parameters.Length<1
            || method.Parameters[0].Type is not INamedTypeSymbol { TypeArguments.Length: 2 }) {
            return false;
        }
        if(!services.BuildFunc(invocation.ArgumentList.Arguments[0].Expression,method.Parameters[0].Type,ref uniqueNumber,out var keySelector)
            || !services.CanUseFunc(keySelector)) {
            return false;
        }
        var comparer = "null";
        var containsAnchor = false;
        if(invocation.ArgumentList.Arguments.Count==2) {
            if(method.Parameters.Length<2) {
                return false;
            }
            var comparerExpression=invocation.ArgumentList.Arguments[1].Expression;
            comparer=services.RenderExpression(comparerExpression);
            containsAnchor=AnchorAnalysis.ContainsAnchor(comparerExpression,null);
        }
        stages.Add(new PipelineStage(PipelineStageKind.DistinctBy,keySelector,comparer,currentElementType,currentElementType,keyType: keySelector.ReturnTypeName,containsAnchor: containsAnchor));
        return true;
    }

    private static bool TryAppendBinarySetStage(
        PipelineStageKind kind,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        PipelineBuildServices services,
        ref int uniqueNumber,
        List<PipelineStage> stages,
        string currentElementType) {
        if(invocation.ArgumentList.Arguments.Count is not (1 or 2)
            || method.Parameters.Length<1) {
            return false;
        }
        if(!services.BuildEnumerable(invocation.ArgumentList.Arguments[0].Expression,ref uniqueNumber,out var secondSource,out _)) {
            return false;
        }
        var comparer = "null";
        var containsAnchor = false;
        if(invocation.ArgumentList.Arguments.Count==2) {
            if(method.Parameters.Length<2) {
                return false;
            }
            var comparerExpression=invocation.ArgumentList.Arguments[1].Expression;
            comparer=services.RenderExpression(comparerExpression);
            containsAnchor=AnchorAnalysis.ContainsAnchor(comparerExpression,null);
        }
        stages.Add(new PipelineStage(kind,null,comparer,currentElementType,currentElementType,secondSource: secondSource,keyType: currentElementType,containsAnchor: containsAnchor));
        return true;
    }

    private static bool TryAppendBinarySetByStage(
        PipelineStageKind kind,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        PipelineBuildServices services,
        ref int uniqueNumber,
        List<PipelineStage> stages,
        string currentElementType) {
        if(invocation.ArgumentList.Arguments.Count is not (2 or 3)
            || method.Parameters.Length<2
            || method.Parameters[1].Type is not INamedTypeSymbol { TypeArguments.Length: 2 }) {
            return false;
        }
        if(!services.BuildEnumerable(invocation.ArgumentList.Arguments[0].Expression,ref uniqueNumber,out var secondSource,out _)) {
            return false;
        }
        if(!services.BuildFunc(invocation.ArgumentList.Arguments[1].Expression,method.Parameters[1].Type,ref uniqueNumber,out var keySelector)
            || !services.CanUseFunc(keySelector)) {
            return false;
        }
        var comparer = "null";
        var containsAnchor = false;
        if(invocation.ArgumentList.Arguments.Count==3) {
            if(method.Parameters.Length<3) {
                return false;
            }
            var comparerExpression=invocation.ArgumentList.Arguments[2].Expression;
            comparer=services.RenderExpression(comparerExpression);
            containsAnchor=AnchorAnalysis.ContainsAnchor(comparerExpression,null);
        }
        stages.Add(new PipelineStage(kind,keySelector,comparer,currentElementType,currentElementType,secondSource: secondSource,keyType: keySelector.ReturnTypeName,containsAnchor: containsAnchor));
        return true;
    }

    private static bool TryAppendConcatStage(
        InvocationExpressionSyntax invocation,
        PipelineBuildServices services,
        ref int uniqueNumber,
        List<PipelineStage> stages,
        string currentElementType) {
        if(invocation.ArgumentList.Arguments.Count!=1) {
            return false;
        }
        if(!services.BuildEnumerable(invocation.ArgumentList.Arguments[0].Expression,ref uniqueNumber,out var secondSource,out _)) {
            return false;
        }
        stages.Add(new PipelineStage(PipelineStageKind.Concat,null,null,currentElementType,currentElementType,secondSource: secondSource));
        return true;
    }

    private static bool TryAppendElementStage(
        PipelineStageKind kind,
        InvocationExpressionSyntax invocation,
        PipelineBuildServices services,
        List<PipelineStage> stages,
        string currentElementType) {
        if(invocation.ArgumentList.Arguments.Count!=1) {
            return false;
        }
        var elementExpression=invocation.ArgumentList.Arguments[0].Expression;
        var element = services.RenderExpression(elementExpression);
        stages.Add(new PipelineStage(kind,null,element,currentElementType,currentElementType,containsAnchor: AnchorAnalysis.ContainsAnchor(elementExpression,null)));
        return true;
    }

    private static bool TryAppendDefaultIfEmptyStage(
        InvocationExpressionSyntax invocation,
        PipelineBuildServices services,
        List<PipelineStage> stages,
        string currentElementType) {
        if(invocation.ArgumentList.Arguments.Count>1) {
            return false;
        }
        var defaultValue = "default!";
        var containsAnchor = false;
        if(invocation.ArgumentList.Arguments.Count==1) {
            var defaultValueExpression=invocation.ArgumentList.Arguments[0].Expression;
            defaultValue=services.RenderExpression(defaultValueExpression);
            containsAnchor=AnchorAnalysis.ContainsAnchor(defaultValueExpression,null);
        }
        stages.Add(new PipelineStage(PipelineStageKind.DefaultIfEmpty,null,defaultValue,currentElementType,currentElementType,containsAnchor: containsAnchor));
        return true;
    }

    private static bool TryAppendOrderStage(
        PipelineStageKind kind,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        PipelineBuildServices services,
        ref int uniqueNumber,
        List<PipelineStage> stages,
        string currentElementType) {
        if(invocation.ArgumentList.Arguments.Count is not (1 or 2)
            || method.Parameters.Length<1
            || method.Parameters[0].Type is not INamedTypeSymbol { TypeArguments.Length: 2 }) {
            return false;
        }
        if(kind is PipelineStageKind.ThenBy or PipelineStageKind.ThenByDescending
            && !stages.Any(static stage => stage.Kind is PipelineStageKind.OrderBy or PipelineStageKind.OrderByDescending)) {
            return false;
        }
        if(!services.BuildFunc(invocation.ArgumentList.Arguments[0].Expression,method.Parameters[0].Type,ref uniqueNumber,out var keySelector)
            || !services.CanUseFunc(keySelector)) {
            return false;
        }
        var comparer = "null";
        var containsAnchor = false;
        if(invocation.ArgumentList.Arguments.Count==2) {
            if(method.Parameters.Length<2) {
                return false;
            }
            var comparerExpression=invocation.ArgumentList.Arguments[1].Expression;
            comparer=services.RenderExpression(comparerExpression);
            containsAnchor=AnchorAnalysis.ContainsAnchor(comparerExpression,null);
        }
        var descending = kind is PipelineStageKind.OrderByDescending or PipelineStageKind.ThenByDescending;
        stages.Add(new PipelineStage(kind,keySelector,comparer,currentElementType,currentElementType,keyType: keySelector.ReturnTypeName,descending: descending,containsAnchor: containsAnchor));
        return true;
    }

    private static bool TryAppendGroupByStage(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        PipelineBuildServices services,
        ref int uniqueNumber,
        List<PipelineStage> stages,
        ref string currentElementType) {
        if(invocation.ArgumentList.Arguments.Count is < 1 or > 4
            || method.Parameters.Length<1
            || method.Parameters[0].Type is not INamedTypeSymbol { TypeArguments.Length: 2 }) {
            return false;
        }
        if(!services.BuildFunc(invocation.ArgumentList.Arguments[0].Expression,method.Parameters[0].Type,ref uniqueNumber,out var keySelector)
            || !services.CanUseFunc(keySelector)) {
            return false;
        }
        FuncFragment? elementSelector = null;
        FuncFragment? resultSelector = null;
        var comparer = "null";
        var containsAnchor = false;
        for(var index = 1; index<invocation.ArgumentList.Arguments.Count; index++) {
            if(method.Parameters.Length<=index) {
                return false;
            }
            if(method.Parameters[index].Type is INamedTypeSymbol { TypeArguments.Length: 2 } elementFuncType) {
                if(elementSelector is not null) {
                    return false;
                }
                if(!services.BuildFunc(invocation.ArgumentList.Arguments[index].Expression,elementFuncType,ref uniqueNumber,out elementSelector)
                    || !services.CanUseFunc(elementSelector)) {
                    return false;
                }
            } else if(method.Parameters[index].Type is INamedTypeSymbol { TypeArguments.Length: 3 } resultFuncType) {
                if(resultSelector is not null) {
                    return false;
                }
                if(!services.BuildFunc(invocation.ArgumentList.Arguments[index].Expression,resultFuncType,ref uniqueNumber,out resultSelector)
                    || !services.CanUseFunc(resultSelector)) {
                    return false;
                }
            } else {
                var comparerExpression=invocation.ArgumentList.Arguments[index].Expression;
                comparer=services.RenderExpression(comparerExpression);
                containsAnchor|=AnchorAnalysis.ContainsAnchor(comparerExpression,null);
            }
        }
        var groupElementType = elementSelector?.ReturnTypeName ?? currentElementType;
        var outputType = resultSelector?.ReturnTypeName ?? $"global::System.Linq.IGrouping<{keySelector.ReturnTypeName},{groupElementType}>";
        stages.Add(new PipelineStage(
            PipelineStageKind.GroupBy,
            keySelector,
            comparer,
            currentElementType,
            outputType,
            secondFunc: elementSelector,
            thirdFunc: resultSelector,
            innerElementType: groupElementType,
            keyType: keySelector.ReturnTypeName,
            containsAnchor: containsAnchor));
        currentElementType=outputType;
        return true;
    }

    private static List<PipelineStage> NormalizeStages(List<PipelineStage> stages,Func<string,string,string> joinStaticMethods) {
        if(stages.Count<=1) {
            return stages;
        }
        stages=MergeDistinctStages(stages);
        stages=MergeDistinctByStages(stages);
        stages=MergeTakeStages(stages);
        stages=MergeSkipStages(stages);
        stages=CancelReverseStages(stages);
        stages=MergeWhereStages(stages,joinStaticMethods);
        return stages;
    }

    private static List<PipelineStage> MergeDistinctStages(List<PipelineStage> stages) {
        var normalized = new List<PipelineStage>(stages.Count);
        foreach(var stage in stages) {
            if(stage.Kind==PipelineStageKind.Distinct
                && !stage.ContainsAnchor
                && normalized.Count>0
                && normalized[^1].Kind==PipelineStageKind.Distinct
                && !normalized[^1].ContainsAnchor
                && string.Equals(normalized[^1].Value,stage.Value,StringComparison.Ordinal)) {
                // 同じ Distinct の連続は結果集合が変わらないので、生成前に 1 段へ畳む。
                // ここで runtime の分岐を増やさず、set の再構築も避ける。
                continue;
            }
            normalized.Add(stage);
        }
        return normalized;
    }

    private static List<PipelineStage> MergeDistinctByStages(List<PipelineStage> stages) {
        var normalized = new List<PipelineStage>(stages.Count);
        foreach(var stage in stages) {
            if(stage.Kind==PipelineStageKind.DistinctBy
                && !stage.ContainsAnchor
                && normalized.Count>0
                && normalized[^1].Kind==PipelineStageKind.DistinctBy
                && !normalized[^1].ContainsAnchor
                && string.Equals(normalized[^1].Value,stage.Value,StringComparison.Ordinal)
                && normalized[^1].KeyType==stage.KeyType
                && normalized[^1].Func is { } leftFunc
                && stage.Func is { } rightFunc
                && DistinctByKeySelectorEquals(leftFunc,rightFunc)) {
                // 同じ keySelector/comparer の DistinctBy も冪等なので、ここで重ねない。
                // key 投影と HashSet 構築の重複を防ぐための compile-time 正規化。
                continue;
            }
            normalized.Add(stage);
        }
        return normalized;
    }

    private static bool DistinctByKeySelectorEquals(FuncFragment left,FuncFragment right) {
        if(left.RequiresStatementInlining
            || right.RequiresStatementInlining
            || left.ContainsAnchor
            || right.ContainsAnchor
            || left.ParameterNames.Length!=right.ParameterNames.Length
            || left.ParameterTypeNames.Length!=right.ParameterTypeNames.Length
            || left.ReturnTypeName!=right.ReturnTypeName) {
            return false;
        }
        for(var index = 0; index<left.ParameterTypeNames.Length; index++) {
            if(left.ParameterTypeNames[index]!=right.ParameterTypeNames[index]) {
                return false;
            }
        }
        try {
            return string.Equals(left.RenderAsLambda(),right.RenderAsLambda(),StringComparison.Ordinal);
        } catch(InvalidOperationException) {
            return false;
        }
    }

    private static List<PipelineStage> MergeTakeStages(List<PipelineStage> stages) {
        var normalized = new List<PipelineStage>(stages.Count);
        foreach(var stage in stages) {
            if(stage.Kind==PipelineStageKind.Take
                && !stage.ContainsAnchor
                && normalized.Count>0
                && normalized[^1].Kind==PipelineStageKind.Take
                && !normalized[^1].ContainsAnchor) {
                var previous = normalized[^1];
                normalized[^1]=new PipelineStage(
                    PipelineStageKind.Take,
                    null,
                    MinCountExpression(previous.Value!,stage.Value!),
                    previous.InputType,
                    previous.OutputType);
                continue;
            }
            normalized.Add(stage);
        }
        return normalized;
    }

    private static List<PipelineStage> MergeSkipStages(List<PipelineStage> stages) {
        var normalized = new List<PipelineStage>(stages.Count);
        foreach(var stage in stages) {
            if(stage.Kind==PipelineStageKind.Skip
                && !stage.ContainsAnchor
                && normalized.Count>0
                && normalized[^1].Kind==PipelineStageKind.Skip
                && !normalized[^1].ContainsAnchor) {
                var previous = normalized[^1];
                normalized[^1]=new PipelineStage(
                    PipelineStageKind.Skip,
                    null,
                    AddCountExpression(previous.Value!,stage.Value!),
                    previous.InputType,
                    previous.OutputType);
                continue;
            }
            normalized.Add(stage);
        }
        return normalized;
    }

    private static List<PipelineStage> CancelReverseStages(List<PipelineStage> stages) {
        var normalized = new List<PipelineStage>(stages.Count);
        foreach(var stage in stages) {
            if(stage.Kind==PipelineStageKind.Reverse
                && normalized.Count>0
                && normalized[^1].Kind==PipelineStageKind.Reverse) {
                normalized.RemoveAt(normalized.Count-1);
                continue;
            }
            normalized.Add(stage);
        }
        return normalized;
    }

    private static List<PipelineStage> MergeWhereStages(List<PipelineStage> stages,Func<string,string,string> joinStaticMethods) {
        var normalized = new List<PipelineStage>(stages.Count);
        foreach(var stage in stages) {
            if(stage.Kind==PipelineStageKind.Where
                && !stage.UsesIndex
                && normalized.Count>0
                && normalized[^1].Kind==PipelineStageKind.Where
                && !normalized[^1].UsesIndex
                && TryMergeWhereFunc(normalized[^1].Func!,stage.Func!,joinStaticMethods,out var mergedFunc)) {
                var previous = normalized[^1];
                normalized[^1]=new PipelineStage(
                    PipelineStageKind.Where,
                    mergedFunc,
                    null,
                    previous.InputType,
                    previous.OutputType);
                continue;
            }
            normalized.Add(stage);
        }
        return normalized;
    }

    private static bool TryMergeWhereFunc(FuncFragment left,FuncFragment right,Func<string,string,string> joinStaticMethods,out FuncFragment merged) {
        merged=null!;
        if(left.RequiresStatementInlining
            || right.RequiresStatementInlining
            || !left.CanInlineAssign
            || !right.CanInlineAssign
            || left.ParameterTypeNames.Length!=right.ParameterTypeNames.Length
            || left.ParameterNames.Length!=right.ParameterNames.Length
            || left.ParameterTypeNames.Length!=1
            || left.ReturnTypeName!="bool"
            || right.ReturnTypeName!="bool") {
            return false;
        }
        for(var index = 0; index<left.ParameterTypeNames.Length; index++) {
            if(left.ParameterTypeNames[index]!=right.ParameterTypeNames[index]) {
                return false;
            }
        }
        var parameterNames = left.ParameterNames.ToArray();
        try {
            var leftExpr = left.RenderInlineExpression(parameterNames);
            var rightExpr = right.RenderInlineExpression(parameterNames);
            merged=new LambdaFunc {
                ReturnTypeName="bool",
                ParameterTypeNames=left.ParameterTypeNames.ToArray(),
                ParameterNames=parameterNames,
                CanGenerateMethod=left.CanGenerateMethod&&right.CanGenerateMethod,
                CanInlineAssign=true,
                InlineBody=$"({leftExpr})&&({rightExpr})",
                CapturedLocals=left.CapturedLocals.Concat(right.CapturedLocals).Distinct().ToArray(),
                HoistedLocals=left.HoistedLocals.Concat(right.HoistedLocals).Distinct().ToArray(),
                BodyUnsafeAccessorCodes=left.BodyUnsafeAccessorCodes.Concat(right.BodyUnsafeAccessorCodes).Distinct().ToArray(),
                SupportingMemberSources=joinStaticMethods(left.SupportingMemberSources,right.SupportingMemberSources),
                ContainsAnchor=left.ContainsAnchor || right.ContainsAnchor
            };
            return true;
        } catch(InvalidOperationException) {
            return false;
        }
    }

    private static string MinCountExpression(string left,string right) =>
        $"global::System.Math.Min(global::System.Math.Max(0,{left}),global::System.Math.Max(0,{right}))";

    private static string AddCountExpression(string left,string right) =>
        $"global::System.Math.Max(0,{left})+global::System.Math.Max(0,{right})";

    private static bool ValidateStageCombination(IReadOnlyList<PipelineStage> stages,PipelineTerminalKind? terminalKind,bool allowSingleStage = false) {
        var selectManyStageCount = stages.Count(static stage => stage.Kind==PipelineStageKind.SelectMany);
        var binarySetStageCount = stages.Count(static stage => stage.Kind is PipelineStageKind.Union or PipelineStageKind.Except or PipelineStageKind.Intersect or PipelineStageKind.UnionBy or PipelineStageKind.ExceptBy or PipelineStageKind.IntersectBy);
        var concatLikeStageCount = stages.Count(static stage => stage.Kind is PipelineStageKind.Concat or PipelineStageKind.Append or PipelineStageKind.Prepend);
        var orderStageCount = stages.Count(static stage => stage.Kind is PipelineStageKind.OrderBy or PipelineStageKind.OrderByDescending or PipelineStageKind.ThenBy or PipelineStageKind.ThenByDescending);
        var groupByStageCount = stages.Count(static stage => stage.Kind==PipelineStageKind.GroupBy);
        var reverseStageCount = stages.Count(static stage => stage.Kind==PipelineStageKind.Reverse);
        var defaultIfEmptyStageCount = stages.Count(static stage => stage.Kind==PipelineStageKind.DefaultIfEmpty);
        var skipLastStageCount = stages.Count(static stage => stage.Kind==PipelineStageKind.SkipLast);
        var takeLastStageCount = stages.Count(static stage => stage.Kind==PipelineStageKind.TakeLast);
        var takeRangeStageCount = stages.Count(static stage => stage.Kind==PipelineStageKind.TakeRange);
        var bufferingStageCount = reverseStageCount+skipLastStageCount+takeLastStageCount+takeRangeStageCount+defaultIfEmptyStageCount;
        if(selectManyStageCount>1) return false;
        if(groupByStageCount>1) return false;
        if(bufferingStageCount>1) return false;
        if(binarySetStageCount>1) return false;
        if(groupByStageCount>0 && (selectManyStageCount>0 || binarySetStageCount>0 || concatLikeStageCount>0)) return false;
        if(bufferingStageCount>0 && (selectManyStageCount>0 || binarySetStageCount>0 || concatLikeStageCount>0 || orderStageCount>0 || groupByStageCount>0)) return false;
        if(binarySetStageCount>0 && selectManyStageCount>0) return false;
        if(concatLikeStageCount>1) return false;
        if(concatLikeStageCount>0 && (selectManyStageCount>0 || binarySetStageCount>0)) return false;
        if(orderStageCount>0 && (selectManyStageCount>0 || binarySetStageCount>0 || concatLikeStageCount>0)) return false;
        if(orderStageCount>0) {
            var orderIndexes = stages.Select(static (stage,index) => (stage,index))
                .Where(static pair => pair.stage.Kind is PipelineStageKind.OrderBy or PipelineStageKind.OrderByDescending or PipelineStageKind.ThenBy or PipelineStageKind.ThenByDescending)
                .Select(static pair => pair.index)
                .ToArray();
            if(stages[orderIndexes[0]].Kind is PipelineStageKind.ThenBy or PipelineStageKind.ThenByDescending) return false;
            for(var i=1;i<orderIndexes.Length;i++) {
                if(orderIndexes[i]!=orderIndexes[i-1]+1) return false;
            }
        }
        // 単段でも順序ステージ（Order/OrderDescending）は実バッファ＋ソートを伴うので許可する。
        if(stages.Count<2 && !allowSingleStage && terminalKind is null && orderStageCount==0) return false;
        return true;
    }

    private static void AddUnsafeAccessors(IEnumerable<PipelineStage> stages,List<string>? unsafeAccessorCollector) {
        if(unsafeAccessorCollector is null) {
            return;
        }
        foreach(var stage in stages) {
            AddFuncUnsafeAccessors(stage.Func,unsafeAccessorCollector);
            AddFuncUnsafeAccessors(stage.SecondFunc,unsafeAccessorCollector);
            AddFuncUnsafeAccessors(stage.ThirdFunc,unsafeAccessorCollector);
            if(stage.SecondSource is { } secondSource) {
                unsafeAccessorCollector.AddRange(secondSource.CollectAllUnsafeAccessorCodes());
            }
        }
    }

    private static void AddFuncUnsafeAccessors(FuncFragment? func,List<string> unsafeAccessorCollector) {
        if(func is null) {
            return;
        }
        unsafeAccessorCollector.AddRange(func.BodyUnsafeAccessorCodes);
        if(func is InlinedScalarFunc inlinedScalarFunc) {
            unsafeAccessorCollector.AddRange(inlinedScalarFunc.ScalarFragment.CollectAllUnsafeAccessorCodes());
        }
    }

    private static ExpressionSyntax RemoveParentheses(ExpressionSyntax expression) {
        while(expression is ParenthesizedExpressionSyntax parenthesized) {
            expression=parenthesized.Expression;
        }
        return expression;
    }

    private static NotSupportedException Unsupported(string reason) =>
        new($"Chain IR is not implemented for this LINQ chain: {reason}");
}

public sealed class PipelineStage(
    PipelineStageKind kind,
    FuncFragment? func,
    string? value,
    string inputType,
    string outputType,
    FuncFragment? secondFunc = null,
    FuncFragment? thirdFunc = null,
    Enumerable? secondSource = null,
    string? innerElementType = null,
    string? keyType = null,
    bool usesIndex = false,
    bool descending = false,
    bool innerIsArray = false,
    bool containsAnchor = false) {
    public PipelineStageKind Kind { get; } = kind;
    public FuncFragment? Func { get; } = func;
    public FuncFragment? SecondFunc { get; } = secondFunc;
    public FuncFragment? ThirdFunc { get; } = thirdFunc;
    public string? Value { get; } = value;
    public string InputType { get; } = inputType;
    public string OutputType { get; } = outputType;
    public Enumerable? SecondSource { get; } = secondSource;
    public string? InnerElementType { get; } = innerElementType;
    // SelectMany の collection selector が配列 (T[]) を返す場合、IEnumerator<T> を介さず
    // インデックスベースで直接走査する specialize パスを使う。
    public bool InnerIsArray { get; } = innerIsArray;
    public string? KeyType { get; } = keyType;
    public bool UsesIndex { get; } = usesIndex;
    public bool Descending { get; } = descending;
    public bool ContainsAnchor { get; } = containsAnchor
        || func?.ContainsAnchor == true
        || secondFunc?.ContainsAnchor == true
        || thirdFunc?.ContainsAnchor == true
        || (value is not null && AnchorAnalysis.LooksLikeAnchorText(value));
}

public interface DirectExecutionFragment {
    string GetDirectExecutionStatement(string actualVariableName,string actualTypeName);
}

public sealed class PipelineEnumerable(int 番号,Enumerable source,IReadOnlyList<PipelineStage> stages,string elementType):Enumerable(番号) {
    protected override Enumerable PrimarySource => source;
    public Enumerable Source => source;
    public IReadOnlyList<PipelineStage> Stages => stages;
    public override string ElementType => elementType;
    private void AppendCounterCall(StringBuilder builder,string indent,string methodName) {
        var call=this.CounterCall(methodName);
        if(!string.IsNullOrEmpty(call)) {
            builder.AppendLine($"{indent}{call}");
        }
    }
    public override string StructMethod {
        get {
            var result = string.Empty;
            foreach(var source in this.Funcs.Select(static f => f.SupportingMemberSources)) {
                result=JoinStaticMethods(result,source);
            }
            foreach(var source in this.SecondSources.Select(static source => source.StructMethod)) {
                result=JoinStaticMethods(result,source);
            }
            return result;
        }
    }
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{this.SecondSourcesStructs}}
        {{base.GetEnumeratorStruct}}
        """;
    protected override string StructTypeParameters {
        get {
            var secondSources = this.SecondSources.ToArray();
            if(secondSources.Length==0) {
                return base.StructTypeParameters;
            }
            var parameters = new List<string> { "TEnum" };
            for(var index = 0; index<secondSources.Length; index++) {
                parameters.Add(SecondTypeParameterName(index));
            }
            return $"<{string.Join(", ",parameters)}>";
        }
    }
    protected override string StructTypeConstraints {
        get {
            var constraints = new List<string> { $"where TEnum : struct, global::System.Collections.Generic.IEnumerator<{source.ElementType}>, global::Tsavorite.Carrier.IValueEnumerator<{source.ElementType}>" };
            foreach(var (second,index) in this.SecondSources.Select(static (second,index) => (second,index))) {
                constraints.Add($"where {SecondTypeParameterName(index)} : struct, global::System.Collections.Generic.IEnumerator<{second.ElementType}>, global::Tsavorite.Carrier.IValueEnumerator<{second.ElementType}>");
            }
            return string.Join("\n    ",constraints);
        }
    }
    public override string EnumeratorTypeName {
        get {
            var secondSources = this.SecondSources.ToArray();
            if(secondSources.Length==0) {
                return base.EnumeratorTypeName;
            }
            var arguments = new List<string> { source.EnumeratorTypeName };
            arguments.AddRange(secondSources.Select(static second => second.EnumeratorTypeName));
            return $"{this.Struct}<{string.Join(", ",arguments)}>";
        }
    }
    public override string GetEnumeratorExpression {
        get {
            var args = new List<string> { source.GetEnumeratorExpression };
            foreach(var second in this.SecondSources) {
                args.Add(second.GetEnumeratorExpression);
            }
            foreach(var stage in this.CountStages()) {
                args.Add(stage.Value!);
            }
            foreach(var stage in this.RangeStages()) {
                args.Add(stage.Value!);
            }
            foreach(var stage in this.ComparerStages()) {
                args.Add(stage.Value!);
            }
            foreach(var stage in this.OrderComparerStages()) {
                args.Add(stage.Value!);
            }
            args.AddRange(this.ClosureArgsWithoutPrefix());
            return $"new {this.EnumeratorTypeName}({string.Join(", ",args)})";
        }
    }
    public override string GetConsumeExecutionStatement(string consumerName) =>
        this.RenderPureOrderingDirectConsume(consumerName)
        ?? base.GetConsumeExecutionStatement(consumerName);

    private string? RenderPureOrderingDirectConsume(string consumerName) {
        if(stages.Count==0
            || !stages.All(static stage => stage.Kind is PipelineStageKind.OrderBy
                or PipelineStageKind.OrderByDescending
                or PipelineStageKind.ThenBy
                or PipelineStageKind.ThenByDescending)
            || stages[0].Kind is PipelineStageKind.ThenBy or PipelineStageKind.ThenByDescending
            || source.DirectEnumerableExpression is not { } sourceExpression) {
            return null;
        }

        var expression=sourceExpression;
        foreach(var stage in stages) {
            var methodName=stage.Kind.ToString();
            string argumentList;
            if(stage.Func is { } func) {
                if(TryRenderDirectLambda(func) is not { } lambda) {
                    return null;
                }
                argumentList=stage.Value=="null"
                    ? lambda
                    : $"{lambda}, {stage.Value}";
            } else {
                methodName=stage.Kind switch {
                    PipelineStageKind.OrderBy => "Order",
                    PipelineStageKind.OrderByDescending => "OrderDescending",
                    _ => methodName
                };
                argumentList=stage.Value=="null"
                    ? string.Empty
                    : stage.Value!;
            }
            expression=string.IsNullOrEmpty(argumentList)
                ? $"global::System.Linq.Enumerable.{methodName}({expression})"
                : $"global::System.Linq.Enumerable.{methodName}({expression}, {argumentList})";
        }

        var itemName=$"__orderedItem{番号}";
        return $$"""
            foreach(var {{itemName}} in {{expression}}) {
                {{consumerName}}.Consume({{itemName}});
            }
            """;
    }

    private static string? TryRenderDirectLambda(FuncFragment func) {
        if(func.CapturedLocals.Length!=0 || func.HoistedLocals.Length!=0 || func.RequiresStatementInlining) {
            return null;
        }
        try {
            return func.RenderAsLambda();
        } catch(InvalidOperationException) {
            return null;
        }
    }
    public override string GetEnumeratorPrimaryConstructorParameters {
        get {
            var parameters = new List<string> { $"TEnum {this.e}" };
            foreach(var (second,index) in this.SecondSources.Select(static (second,index) => (second,index))) {
                parameters.Add($"{SecondTypeParameterName(index)} {this.SecondEnumeratorName(index)}");
            }
            foreach(var (stage,index) in this.CountStages().Select(static (stage,index) => (stage,index))) {
                parameters.Add($"int {this.CountName(index)}");
            }
            foreach(var (stage,index) in this.RangeStages().Select(static (stage,index) => (stage,index))) {
                parameters.Add($"global::System.Range {this.RangeName(this.RangeOrdinal(index))}");
            }
            foreach(var (stage,index) in this.ComparerStages().Select(static (stage,index) => (stage,index))) {
                parameters.Add($"global::System.Collections.Generic.IEqualityComparer<{ComparerKeyType(stage)}>? {this.ComparerName(index)}");
            }
            foreach(var (stage,index) in this.OrderComparerStages().Select(static (stage,index) => (stage,index))) {
                parameters.Add($"global::System.Collections.Generic.IComparer<{ComparerKeyType(stage)}>? {this.OrderComparerName(index)}");
            }
            parameters.AddRange(this.ClosureParametersWithoutPrefix());
            return string.Join(", ",parameters);
        }
    }
    public override string GetEnumeratorFields {
        get {
            var builder = new StringBuilder();
            var countIndex = 0;
            for(var index = 0; index<stages.Count; index++) {
                var stage = stages[index];
                if(stage.Kind is PipelineStageKind.Where or PipelineStageKind.Select && stage.UsesIndex) {
                    builder.AppendLine($"private int {this.StateName(index)} = -1;");
                } else if(stage.Kind==PipelineStageKind.Take || stage.Kind==PipelineStageKind.Skip) {
                    var countName = this.CountName(countIndex++);
                    builder.AppendLine($"private int {countName}Value;");
                    builder.AppendLine($"private int {this.StateName(index)} = 0;");
                } else if(stage.Kind==PipelineStageKind.SelectMany) {
                    if(stage.InnerIsArray) {
                        builder.AppendLine($"private {stage.InnerElementType}[] {this.InnerArrayName(index)} = global::System.Array.Empty<{stage.InnerElementType}>();");
                        builder.AppendLine($"private int {this.InnerIndexName(index)} = 0;");
                        builder.AppendLine($"private bool {this.InnerHasValueName(index)} = false;");
                    } else {
                        builder.AppendLine($"private global::System.Collections.Generic.IEnumerator<{stage.InnerElementType}>? {this.InnerEnumeratorName(index)} = null;");
                    }
                    builder.AppendLine($"private {stage.InputType} {this.OuterName(index)} = default!;");
                    if(stage.UsesIndex) {
                        builder.AppendLine($"private int {this.StateName(index)} = -1;");
                    }
                } else if(stage.Kind is PipelineStageKind.Distinct or PipelineStageKind.DistinctBy) {
                    builder.AppendLine($"private global::System.Collections.Generic.HashSet<{ComparerKeyType(stage)}> {this.SetName(index)};");
                } else if(stage.Kind is PipelineStageKind.Union or PipelineStageKind.UnionBy) {
                    builder.AppendLine($"private global::System.Collections.Generic.HashSet<{ComparerKeyType(stage)}> {this.SetName(index)};");
                    builder.AppendLine($"private bool {this.StateName(index)} = false;");
                } else if(stage.Kind is PipelineStageKind.Except or PipelineStageKind.Intersect or PipelineStageKind.ExceptBy or PipelineStageKind.IntersectBy) {
                    builder.AppendLine($"private global::System.Collections.Generic.HashSet<{ComparerKeyType(stage)}> {this.SecondSetName(index)};");
                    builder.AppendLine($"private global::System.Collections.Generic.HashSet<{ComparerKeyType(stage)}> {this.SetName(index)};");
                    builder.AppendLine($"private bool {this.StateName(index)} = false;");
                } else if(stage.Kind is PipelineStageKind.Concat or PipelineStageKind.Append or PipelineStageKind.Prepend) {
                    builder.AppendLine($"private int {this.StateName(index)} = 0;");
                } else if(stage.Kind is PipelineStageKind.OrderBy or PipelineStageKind.OrderByDescending) {
                    builder.AppendLine(this.OrderSortItemType(index));
                    builder.AppendLine($"private global::System.Collections.Generic.List<{this.SortItemName(index)}>? {this.SortedItemsName(index)} = null;");
                    builder.AppendLine($"private int {this.StateName(index)} = 0;");
                } else if(stage.Kind==PipelineStageKind.GroupBy) {
                    builder.AppendLine(this.GroupingType(index,stage));
                    builder.AppendLine($"private global::System.Collections.Generic.List<{this.GroupValueType(index,stage)}>? {this.GroupItemsName(index)} = null;");
                    builder.AppendLine($"private int {this.StateName(index)} = 0;");
                } else if(stage.Kind==PipelineStageKind.Reverse) {
                    builder.AppendLine($"private global::System.Collections.Generic.List<{stage.OutputType}>? {this.ReverseItemsName(index)} = null;");
                    builder.AppendLine($"private int {this.StateName(index)} = 0;");
                } else if(stage.Kind==PipelineStageKind.DefaultIfEmpty) {
                    builder.AppendLine($"private bool {this.StateName(index)} = false;");
                } else if(stage.Kind==PipelineStageKind.TakeWhile) {
                    builder.AppendLine($"private bool {this.StateName(index)} = false;");
                    if(stage.UsesIndex) {
                        builder.AppendLine($"private int {this.IndexName(index)} = -1;");
                    }
                } else if(stage.Kind==PipelineStageKind.SkipWhile) {
                    builder.AppendLine($"private bool {this.StateName(index)} = true;");
                    if(stage.UsesIndex) {
                        builder.AppendLine($"private int {this.IndexName(index)} = -1;");
                    }
                } else if(stage.Kind==PipelineStageKind.SkipLast) {
                    builder.AppendLine($"private global::System.Collections.Generic.Queue<{stage.OutputType}> {this.QueueName(index)} = new();");
                } else if(stage.Kind==PipelineStageKind.TakeLast) {
                    builder.AppendLine($"private global::System.Collections.Generic.List<{stage.OutputType}>? {this.ReverseItemsName(index)} = null;");
                    builder.AppendLine($"private int {this.StateName(index)} = 0;");
                } else if(stage.Kind==PipelineStageKind.TakeRange) {
                    builder.AppendLine($"private global::System.Collections.Generic.List<{stage.OutputType}>? {this.ReverseItemsName(index)} = null;");
                    builder.AppendLine($"private int {this.StateName(index)} = 0;");
                    builder.AppendLine($"private int {this.StateEndName(index)} = 0;");
                }
            }
            return builder.ToString().TrimEnd();
        }
    }
    public override string GetEnumeratorConstructor {
        get {
            var builder = new StringBuilder();
            var countIndex = 0;
            for(var index = 0; index<stages.Count; index++) {
                var stage = stages[index];
                if(stage.Kind==PipelineStageKind.Take || stage.Kind==PipelineStageKind.Skip) {
                    var countName = this.CountName(countIndex++);
                    builder.AppendLine($"this.{countName}Value = global::System.Math.Max(0, {countName});");
                } else if(stage.Kind is PipelineStageKind.Distinct or PipelineStageKind.DistinctBy) {
                    builder.AppendLine($"this.{this.SetName(index)} = new({this.ComparerName(this.ComparerOrdinal(index))});");
                } else if(stage.Kind is PipelineStageKind.Union or PipelineStageKind.UnionBy) {
                    builder.AppendLine($"this.{this.SetName(index)} = new({this.ComparerName(this.ComparerOrdinal(index))});");
                } else if(stage.Kind is PipelineStageKind.Except or PipelineStageKind.Intersect or PipelineStageKind.ExceptBy or PipelineStageKind.IntersectBy) {
                    builder.AppendLine($"this.{this.SecondSetName(index)} = new({this.ComparerName(this.ComparerOrdinal(index))});");
                    builder.AppendLine($"this.{this.SetName(index)} = new({this.ComparerName(this.ComparerOrdinal(index))});");
                }
            }
            return builder.ToString().TrimEnd();
        }
    }
    public override string GetTryGetNextBody => this.BuildLoop(this.e,"current = this.Current = {0};\nreturn true;","current = default!;\nreturn false;");
    public override string GetCurrentExpression => this.currentValue;
    protected override string GetEnumeratorDisposeBody {
        get {
            var builder = new StringBuilder();
            for(var index = 0; index<stages.Count; index++) {
                if(stages[index].Kind==PipelineStageKind.SelectMany && !stages[index].InnerIsArray) {
                    builder.AppendLine($"if(this.{this.InnerEnumeratorName(index)} is not null) {{");
                    builder.AppendLine($"    this.{this.InnerEnumeratorName(index)}.Dispose();");
                    builder.AppendLine("}");
                }
            }
            foreach(var index in this.SecondSources.Select(static (_,index) => index)) {
                builder.AppendLine($"{this.SecondEnumeratorName(index)}.Dispose();");
            }
            builder.AppendLine($"{this.e}.Dispose();");
            return builder.ToString().TrimEnd();
        }
    }

    internal string BuildDirectLoop(string enumeratorName,string successStatement,string endStatement) => this.BuildLoop(enumeratorName,successStatement,endStatement);

    private IEnumerable<FuncFragment> Funcs => stages.SelectMany(static stage => new[] { stage.Func, stage.SecondFunc, stage.ThirdFunc }).Where(static func => func is not null).Cast<FuncFragment>();
    private IEnumerable<Enumerable> SecondSources => stages.Select(static stage => stage.SecondSource).Where(static source => source is not null).Cast<Enumerable>();
    private string SecondSourcesStructs {
        get {
            var result = string.Empty;
            foreach(var source in this.SecondSources.Select(static source => source.GetEnumeratorStruct)) {
                result=JoinStaticMethods(result,source);
            }
            return result;
        }
    }

    private IEnumerable<PipelineStage> CountStages() =>
        stages.Where(static stage => stage.Kind==PipelineStageKind.Take || stage.Kind==PipelineStageKind.Skip || stage.Kind==PipelineStageKind.SkipLast || stage.Kind==PipelineStageKind.TakeLast);

    private IEnumerable<PipelineStage> ComparerStages() =>
        stages.Where(static stage => stage.Kind is PipelineStageKind.Distinct or PipelineStageKind.DistinctBy or PipelineStageKind.Union or PipelineStageKind.Except or PipelineStageKind.Intersect or PipelineStageKind.UnionBy or PipelineStageKind.ExceptBy or PipelineStageKind.IntersectBy or PipelineStageKind.GroupBy);

    private IEnumerable<PipelineStage> OrderComparerStages() =>
        stages.Where(static stage => stage.Kind is PipelineStageKind.OrderBy or PipelineStageKind.OrderByDescending or PipelineStageKind.ThenBy or PipelineStageKind.ThenByDescending);

    private string BuildLoop(string enumeratorName,string successStatement,string endStatement) {
        var binarySetIndex = stages.Select(static (stage,index) => (stage,index))
            .Where(static pair => pair.stage.Kind is PipelineStageKind.Union or PipelineStageKind.Except or PipelineStageKind.Intersect or PipelineStageKind.UnionBy or PipelineStageKind.ExceptBy or PipelineStageKind.IntersectBy)
            .Select(static pair => pair.index)
            .ToArray();
        if(binarySetIndex.Length==1) {
            return this.BuildBinarySetLoop(enumeratorName,successStatement,endStatement,binarySetIndex[0]);
        }
        if(binarySetIndex.Length>1) {
            throw new NotSupportedException("Multiple binary set stages are not supported in PipelineEnumerable.");
        }
        var selectManyIndex = stages.Select(static (stage,index) => (stage,index)).Where(static pair => pair.stage.Kind==PipelineStageKind.SelectMany).Select(static pair => pair.index).ToArray();
        if(selectManyIndex.Length==1) {
            return this.BuildSelectManyLoop(enumeratorName,successStatement,endStatement,selectManyIndex[0]);
        }
        var groupByIndex = stages.Select(static (stage,index) => (stage,index))
            .Where(static pair => pair.stage.Kind==PipelineStageKind.GroupBy)
            .Select(static pair => pair.index)
            .ToArray();
        if(groupByIndex.Length==1) {
            return this.BuildGroupByLoop(enumeratorName,successStatement,endStatement,groupByIndex[0]);
        }
        if(groupByIndex.Length>1) {
            throw new NotSupportedException("Multiple GroupBy stages are not supported in PipelineEnumerable.");
        }
        var reverseIndex = stages.Select(static (stage,index) => (stage,index))
            .Where(static pair => pair.stage.Kind==PipelineStageKind.Reverse)
            .Select(static pair => pair.index)
            .ToArray();
        if(reverseIndex.Length==1) {
            return this.BuildReverseLoop(enumeratorName,successStatement,endStatement,reverseIndex[0]);
        }
        if(reverseIndex.Length>1) {
            throw new NotSupportedException("Multiple Reverse stages are not supported in PipelineEnumerable.");
        }
        var defaultIfEmptyIndex = stages.Select(static (stage,index) => (stage,index))
            .Where(static pair => pair.stage.Kind==PipelineStageKind.DefaultIfEmpty)
            .Select(static pair => pair.index)
            .ToArray();
        if(defaultIfEmptyIndex.Length==1) {
            return this.BuildDefaultIfEmptyLoop(enumeratorName,successStatement,endStatement,defaultIfEmptyIndex[0]);
        }
        if(defaultIfEmptyIndex.Length>1) {
            throw new NotSupportedException("Multiple DefaultIfEmpty stages are not supported in PipelineEnumerable.");
        }
        var orderIndex = stages.Select(static (stage,index) => (stage,index))
            .Where(static pair => pair.stage.Kind is PipelineStageKind.OrderBy or PipelineStageKind.OrderByDescending)
            .Select(static pair => pair.index)
            .ToArray();
        if(orderIndex.Length==1) {
            return this.BuildOrderLoop(enumeratorName,successStatement,endStatement,orderIndex[0]);
        }
        if(orderIndex.Length>1) {
            // 連続した OrderBy チェーン: 最後の OrderBy が前の順序を完全に置き換える（LINQ の仕様通り）
            return this.BuildOrderLoop(enumeratorName,successStatement,endStatement,orderIndex[^1]);
        }
        var skipLastIndex = stages.Select(static (stage,index) => (stage,index))
            .Where(static pair => pair.stage.Kind==PipelineStageKind.SkipLast)
            .Select(static pair => pair.index).ToArray();
        if(skipLastIndex.Length==1) {
            return this.BuildSkipLastLoop(enumeratorName,successStatement,endStatement,skipLastIndex[0]);
        }
        var takeLastIndex = stages.Select(static (stage,index) => (stage,index))
            .Where(static pair => pair.stage.Kind==PipelineStageKind.TakeLast)
            .Select(static pair => pair.index).ToArray();
        if(takeLastIndex.Length==1) {
            return this.BuildTakeLastLoop(enumeratorName,successStatement,endStatement,takeLastIndex[0]);
        }
        var takeRangeIndex = stages.Select(static (stage,index) => (stage,index))
            .Where(static pair => pair.stage.Kind==PipelineStageKind.TakeRange)
            .Select(static pair => pair.index).ToArray();
        if(takeRangeIndex.Length==1) {
            return this.BuildTakeRangeLoop(enumeratorName,successStatement,endStatement,takeRangeIndex[0]);
        }
        var concatLikeIndex = stages.Select(static (stage,index) => (stage,index))
            .Where(static pair => pair.stage.Kind is PipelineStageKind.Concat or PipelineStageKind.Append or PipelineStageKind.Prepend)
            .Select(static pair => pair.index)
            .ToArray();
        if(concatLikeIndex.Length==1) {
            return this.BuildConcatLikeLoop(enumeratorName,successStatement,endStatement,concatLikeIndex[0]);
        }
        if(concatLikeIndex.Length>1) {
            throw new NotSupportedException("Multiple concat-like stages are not supported in PipelineEnumerable.");
        }

        var builder = new StringBuilder();
        builder.AppendLine($"while({enumeratorName}.TryGetNext(out var {this.ItemName(0)})) {{");
        this.AppendCounterCall(builder,"    ","IncrementPhysicalMoveNext");
        var currentName = this.ItemName(0);
        for(var index = 0; index<stages.Count; index++) {
            var stage = stages[index];
            switch(stage.Kind) {
                case PipelineStageKind.Where:
                    this.AppendWhere(builder,stage,currentName,index);
                    break;
                case PipelineStageKind.Select:
                    currentName =this.AppendSelect(builder,stage,currentName,index);
                    break;
                case PipelineStageKind.SelectMany:
                    throw new NotSupportedException("Multiple SelectMany stages are not supported in PipelineEnumerable.");
                case PipelineStageKind.Distinct:
                    this.AppendDistinct(builder,stage,currentName,index);
                    break;
                case PipelineStageKind.DistinctBy:
                    this.AppendDistinctBy(builder,stage,currentName,index);
                    break;
                case PipelineStageKind.Union:
                case PipelineStageKind.Except:
                case PipelineStageKind.Intersect:
                case PipelineStageKind.UnionBy:
                case PipelineStageKind.ExceptBy:
                case PipelineStageKind.IntersectBy:
                    throw new NotSupportedException("Binary set stage requires a dedicated PipelineEnumerable loop.");
                case PipelineStageKind.Concat:
                case PipelineStageKind.Append:
                case PipelineStageKind.Prepend:
                    throw new NotSupportedException("Concat-like stage requires a dedicated PipelineEnumerable loop.");
                case PipelineStageKind.OrderBy:
                case PipelineStageKind.OrderByDescending:
                case PipelineStageKind.ThenBy:
                case PipelineStageKind.ThenByDescending:
                    throw new NotSupportedException("Ordering stage requires a dedicated PipelineEnumerable loop.");
                case PipelineStageKind.GroupBy:
                    throw new NotSupportedException("GroupBy stage requires a dedicated PipelineEnumerable loop.");
                case PipelineStageKind.Reverse:
                    throw new NotSupportedException("Reverse stage requires a dedicated PipelineEnumerable loop.");
                case PipelineStageKind.DefaultIfEmpty:
                    throw new NotSupportedException("DefaultIfEmpty stage requires a dedicated PipelineEnumerable loop.");
                case PipelineStageKind.AsEnumerable:
                    break;
                case PipelineStageKind.Take:
                    this.AppendTake(builder,index,this.CountOrdinal(index));
                    break;
                case PipelineStageKind.Skip:
                    this.AppendSkip(builder,index,this.CountOrdinal(index));
                    break;
                case PipelineStageKind.TakeWhile:
                    this.AppendTakeWhile(builder,stage,currentName,index);
                    break;
                case PipelineStageKind.SkipWhile:
                    this.AppendSkipWhile(builder,stage,currentName,index);
                    break;
                case PipelineStageKind.Cast:
                    currentName =this.AppendCast(builder,stage,currentName,index);
                    break;
                case PipelineStageKind.OfType:
                    currentName =this.AppendOfType(builder,stage,currentName,index);
                    break;
            }
        }
        builder.AppendLine(IndentBlock(string.Format(successStatement,currentName),"    "));
        builder.AppendLine("}");
        builder.AppendLine(endStatement);
        return builder.ToString().TrimEnd();
    }

    private string BuildConcatLikeLoop(string enumeratorName,string successStatement,string endStatement,int concatIndex) {
        var stage = stages[concatIndex];
        var builder = new StringBuilder();
        var endLabel = this.EndLabel(concatIndex);
        var needsEndLabel = this.StagesAfterNeedEndLabel(concatIndex);
        var breakStatement = "break;";
        if(needsEndLabel) {
            breakStatement=$"goto {endLabel};";
        }
        builder.AppendLine("while(true) {");
        if(stage.Kind==PipelineStageKind.Prepend) {
            builder.AppendLine($"    if(this.{this.StateName(concatIndex)}==0) {{");
            builder.AppendLine($"        this.{this.StateName(concatIndex)}=1;");
            var prependName = this.ItemName(concatIndex+1);
            builder.AppendLine($"        var {prependName}=({stage.OutputType})({stage.Value});");
            var currentName = prependName;
            for(var index = concatIndex+1; index<stages.Count; index++) {
                currentName=this.AppendStage(builder,stages[index],currentName,index,breakStatement);
            }
            builder.AppendLine(IndentBlock(string.Format(successStatement,currentName),"        "));
            builder.AppendLine("    }");
        }
        builder.AppendLine($"    if(this.{this.StateName(concatIndex)}<=1) {{");
        builder.AppendLine($"        while({enumeratorName}.TryGetNext(out var {this.ItemName(0)})) {{");
        this.AppendCounterCall(builder,"            ","IncrementPhysicalMoveNext");
        var sourceCurrentName = this.ItemName(0);
        for(var index = 0; index<concatIndex; index++) {
            sourceCurrentName=this.AppendStage(builder,stages[index],sourceCurrentName,index);
        }
        for(var index = concatIndex+1; index<stages.Count; index++) {
            sourceCurrentName=this.AppendStage(builder,stages[index],sourceCurrentName,index,breakStatement);
        }
        builder.AppendLine(IndentBlock(string.Format(successStatement,sourceCurrentName),"            "));
        builder.AppendLine("        }");
        builder.AppendLine($"        this.{this.StateName(concatIndex)}=2;");
        builder.AppendLine("    }");
        if(stage.Kind==PipelineStageKind.Concat) {
            builder.AppendLine($"    while({this.SecondEnumeratorName(this.SecondOrdinal(concatIndex))}.TryGetNext(out var {this.ItemName(concatIndex+1)})) {{");
            this.AppendCounterCall(builder,"        ","IncrementPhysicalMoveNext");
            var secondName = this.ItemName(concatIndex+1);
            builder.AppendLine($"        {secondName}=({stage.OutputType})(object?){secondName}!;");
            var currentName = secondName;
            for(var index = concatIndex+1; index<stages.Count; index++) {
                currentName=this.AppendStage(builder,stages[index],currentName,index,breakStatement);
            }
            builder.AppendLine(IndentBlock(string.Format(successStatement,currentName),"        "));
            builder.AppendLine("    }");
            builder.AppendLine($"    this.{this.StateName(concatIndex)}=3;");
        } else if(stage.Kind==PipelineStageKind.Append) {
            builder.AppendLine($"    if(this.{this.StateName(concatIndex)}==2) {{");
            builder.AppendLine($"        this.{this.StateName(concatIndex)}=3;");
            var appendName = this.ItemName(concatIndex+1);
            builder.AppendLine($"        var {appendName}=({stage.OutputType})({stage.Value});");
            var currentName = appendName;
            for(var index = concatIndex+1; index<stages.Count; index++) {
                currentName=this.AppendStage(builder,stages[index],currentName,index,breakStatement);
            }
            builder.AppendLine(IndentBlock(string.Format(successStatement,currentName),"        "));
            builder.AppendLine("    }");
        }
        builder.AppendLine(IndentBlock(endStatement,"    "));
        builder.AppendLine("}");
        if(needsEndLabel) {
            builder.AppendLine($"{endLabel}:");
            builder.AppendLine(endStatement);
        }
        return builder.ToString().TrimEnd();
    }

    private string BuildOrderLoop(string enumeratorName,string successStatement,string endStatement,int orderIndex) {
        var orderStages = this.OrderStages(orderIndex).ToArray();
        var primaryOrder = orderStages[0];
        var secondaryOrder = orderStages.Length==2 ? orderStages[1] : null;
        var lastOrderIndex = orderIndex+orderStages.Length-1;
        var builder = new StringBuilder();
        builder.AppendLine($"if(this.{this.SortedItemsName(orderIndex)} is null) {{");
        builder.AppendLine($"    global::System.Collections.Generic.List<{this.SortItemName(orderIndex)}> {this.SortedItemsName(orderIndex)};");
        builder.AppendLine($"    if({enumeratorName}.TryGetNonEnumeratedCount(out var __sortCount{orderIndex})) {{");
        builder.AppendLine($"        {this.SortedItemsName(orderIndex)}=new global::System.Collections.Generic.List<{this.SortItemName(orderIndex)}>(__sortCount{orderIndex});");
        builder.AppendLine("    }else{");
        builder.AppendLine($"        {this.SortedItemsName(orderIndex)}=new global::System.Collections.Generic.List<{this.SortItemName(orderIndex)}>();");
        builder.AppendLine("    }");
        builder.AppendLine("    var __sortIndex=0;");
        builder.AppendLine($"    while({enumeratorName}.TryGetNext(out var {this.ItemName(0)})) {{");
        this.AppendCounterCall(builder,"        ","IncrementPhysicalMoveNext");
        var currentName = this.ItemName(0);
        for(var index = 0; index<orderIndex; index++) {
            currentName=this.AppendStage(builder,stages[index],currentName,index);
        }
        var primaryKeyName = this.KeyName(orderIndex);
        builder.AppendLine($"        {ComparerKeyType(primaryOrder)} {primaryKeyName}=default!;");
        if(primaryOrder.Func is null) {
            // 恒等キー（Order/OrderDescending）はキー型＝要素型なので、ボクシングを挟まず直接代入する。
            builder.AppendLine($"        {primaryKeyName}={currentName};");
        } else {
            builder.AppendLine(IndentBlock(this.FuncAssign(primaryOrder.Func,primaryOrder.Kind.ToString(),primaryKeyName,currentName),"        "));
        }
        if(secondaryOrder is { }) {
            var secondaryKeyName = this.KeyName(orderIndex+1);
            builder.AppendLine($"        {ComparerKeyType(secondaryOrder)} {secondaryKeyName}=default!;");
            if(secondaryOrder.Func is null) {
                builder.AppendLine($"        {secondaryKeyName}={currentName};");
            } else {
                builder.AppendLine(IndentBlock(this.FuncAssign(secondaryOrder.Func,secondaryOrder.Kind.ToString(),secondaryKeyName,currentName),"        "));
            }
            builder.AppendLine($"        {this.SortedItemsName(orderIndex)}.Add(new {this.SortItemName(orderIndex)}({currentName},{primaryKeyName},{secondaryKeyName},__sortIndex++));");
        } else {
            builder.AppendLine($"        {this.SortedItemsName(orderIndex)}.Add(new {this.SortItemName(orderIndex)}({currentName},{primaryKeyName},__sortIndex++));");
        }
        builder.AppendLine("    }");
        var primaryComparerName = this.OrderComparerLocalName(orderIndex);
        if(!CanUseDirectDefaultOrderCompare(primaryOrder)) {
            builder.AppendLine($"    var {primaryComparerName}={this.OrderComparerName(this.OrderComparerOrdinal(orderIndex))} ?? global::System.Collections.Generic.Comparer<{ComparerKeyType(primaryOrder)}>.Default;");
        }
        string? secondaryComparerName = null;
        if(secondaryOrder is { }) {
            secondaryComparerName=this.OrderComparerLocalName(orderIndex+1);
            if(!CanUseDirectDefaultOrderCompare(secondaryOrder)) {
                builder.AppendLine($"    var {secondaryComparerName}={this.OrderComparerName(this.OrderComparerOrdinal(orderIndex+1))} ?? global::System.Collections.Generic.Comparer<{ComparerKeyType(secondaryOrder)}>.Default;");
            }
        }
        builder.AppendLine($"    {this.SortedItemsName(orderIndex)}.Sort((left,right) => {{");
        builder.AppendLine($"        var compare={this.OrderCompareCall(primaryOrder,"left.Key","right.Key",primaryComparerName)};");
        builder.AppendLine($"        if(compare!=0) return {this.OrderCompareExpression(primaryOrder,"compare")};");
        if(secondaryOrder is { }) {
            builder.AppendLine($"        var compare2={this.OrderCompareCall(secondaryOrder,"left.SecondKey","right.SecondKey",secondaryComparerName!)};");
            builder.AppendLine($"        if(compare2!=0) return {this.OrderCompareExpression(secondaryOrder,"compare2")};");
        }
        builder.AppendLine("        return left.Index.CompareTo(right.Index);");
        builder.AppendLine("    });");
        builder.AppendLine($"    this.{this.SortedItemsName(orderIndex)}={this.SortedItemsName(orderIndex)};");
        builder.AppendLine($"    this.{this.StateName(orderIndex)}=0;");
        builder.AppendLine("}");
        builder.AppendLine($"while(this.{this.StateName(orderIndex)}<this.{this.SortedItemsName(orderIndex)}.Count) {{");
        var sortedCurrentName = this.ItemName(lastOrderIndex+1);
        builder.AppendLine($"    var {sortedCurrentName}=this.{this.SortedItemsName(orderIndex)}[this.{this.StateName(orderIndex)}++].Value;");
        currentName=sortedCurrentName;
        for(var index = lastOrderIndex+1; index<stages.Count; index++) {
            currentName=this.AppendStage(builder,stages[index],currentName,index);
        }
        builder.AppendLine(IndentBlock(string.Format(successStatement,currentName),"    "));
        builder.AppendLine("}");
        builder.AppendLine(endStatement);
        return builder.ToString().TrimEnd();
    }

    private string BuildGroupByLoop(string enumeratorName,string successStatement,string endStatement,int groupByIndex) {
        var stage = stages[groupByIndex];
        var builder = new StringBuilder();
        builder.AppendLine($"if(this.{this.GroupItemsName(groupByIndex)} is null) {{");
        builder.AppendLine($"    var {this.GroupItemsName(groupByIndex)}=new global::System.Collections.Generic.List<{this.GroupValueType(groupByIndex,stage)}>();");
        builder.AppendLine($"    var __groupMap=new global::System.Collections.Generic.Dictionary<{ComparerKeyType(stage)},{this.GroupingName(groupByIndex)}>({this.ComparerName(this.ComparerOrdinal(groupByIndex))});");
        builder.AppendLine($"    var __groupOrder=new global::System.Collections.Generic.List<{this.GroupingName(groupByIndex)}>();");
        builder.AppendLine($"    while({enumeratorName}.TryGetNext(out var {this.ItemName(0)})) {{");
        this.AppendCounterCall(builder,"        ","IncrementPhysicalMoveNext");
        var currentName = this.ItemName(0);
        for(var index = 0; index<groupByIndex; index++) {
            currentName=this.AppendStage(builder,stages[index],currentName,index);
        }
        var keyName = this.KeyName(groupByIndex);
        builder.AppendLine($"        {ComparerKeyType(stage)} {keyName}=default!;");
        builder.AppendLine(IndentBlock(this.FuncAssign(stage.Func!,nameof(PipelineStageKind.GroupBy),keyName,currentName),"        "));
        var groupElementName = this.ItemName(groupByIndex+1);
        builder.AppendLine($"        {stage.InnerElementType} {groupElementName}=default!;");
        if(stage.SecondFunc is { } elementSelector) {
            builder.AppendLine(IndentBlock(this.FuncAssign(elementSelector,nameof(PipelineStageKind.GroupBy),groupElementName,currentName),"        "));
        } else {
            builder.AppendLine($"        {groupElementName}=({stage.InnerElementType})(object?){currentName}!;");
        }
        builder.AppendLine($"        if(!__groupMap.TryGetValue({keyName},out var {this.GroupName(groupByIndex)})) {{");
        builder.AppendLine($"            {this.GroupName(groupByIndex)}=new {this.GroupingName(groupByIndex)}({keyName});");
        builder.AppendLine($"            __groupMap.Add({keyName},{this.GroupName(groupByIndex)});");
        builder.AppendLine($"            __groupOrder.Add({this.GroupName(groupByIndex)});");
        if(stage.ThirdFunc is null) {
            builder.AppendLine($"            {this.GroupItemsName(groupByIndex)}.Add({this.GroupName(groupByIndex)});");
        }
        builder.AppendLine("        }");
        builder.AppendLine($"        {this.GroupName(groupByIndex)}.Add({groupElementName});");
        builder.AppendLine("    }");
        if(stage.ThirdFunc is { } resultSelector) {
            builder.AppendLine($"    foreach(var {this.GroupName(groupByIndex)} in __groupOrder) {{");
            var resultName = this.ItemName(groupByIndex+2);
            builder.AppendLine($"        {stage.OutputType} {resultName}=default!;");
            builder.AppendLine(IndentBlock(this.FuncAssign(resultSelector,nameof(PipelineStageKind.GroupBy),resultName,$"{this.GroupName(groupByIndex)}.Key",$"(global::System.Collections.Generic.IEnumerable<{stage.InnerElementType}>){this.GroupName(groupByIndex)}"),"        "));
            builder.AppendLine($"        {this.GroupItemsName(groupByIndex)}.Add({resultName});");
            builder.AppendLine("    }");
        }
        // GroupBy バッファ収集後、後続に OrderBy/ThenBy があればソートして融合する
        var orderAfterGroup=-1;
        for(var checkIdx=groupByIndex+1; checkIdx<stages.Count; checkIdx++) {
            if(stages[checkIdx].Kind is PipelineStageKind.OrderBy or PipelineStageKind.OrderByDescending) {
                orderAfterGroup=checkIdx;
                break;
            }
        }
        if(orderAfterGroup>=0) {
            var primaryOrder=stages[orderAfterGroup];
            var thenByIdx=orderAfterGroup+1<stages.Count
                && stages[orderAfterGroup+1].Kind is PipelineStageKind.ThenBy or PipelineStageKind.ThenByDescending
                ? (int?)(orderAfterGroup+1) : null;
            var primaryCmpLocal= this.OrderComparerLocalName(orderAfterGroup);
            builder.AppendLine($"    var {primaryCmpLocal}={this.OrderComparerName(this.OrderComparerOrdinal(orderAfterGroup))} ?? global::System.Collections.Generic.Comparer<{ComparerKeyType(primaryOrder)}>.Default;");
            if(thenByIdx.HasValue) {
                var thenStage=stages[thenByIdx.Value];
                var secondaryCmpLocal= this.OrderComparerLocalName(thenByIdx.Value);
                builder.AppendLine($"    var {secondaryCmpLocal}={this.OrderComparerName(this.OrderComparerOrdinal(thenByIdx.Value))} ?? global::System.Collections.Generic.Comparer<{ComparerKeyType(thenStage)}>.Default;");
            }
            builder.AppendLine($"    {this.GroupItemsName(groupByIndex)}.Sort((left,right) => {{");
            var leftPrimKey=$"__lpk{orderAfterGroup}";
            var rightPrimKey=$"__rpk{orderAfterGroup}";
            builder.AppendLine($"        {ComparerKeyType(primaryOrder)} {leftPrimKey}=default!;");
            if(primaryOrder.Func is null) {
                builder.AppendLine($"        {leftPrimKey}=left;");
            } else {
                builder.AppendLine(IndentBlock(this.FuncAssign(primaryOrder.Func,primaryOrder.Kind.ToString(),leftPrimKey,"left"),"        "));
            }
            builder.AppendLine($"        {ComparerKeyType(primaryOrder)} {rightPrimKey}=default!;");
            if(primaryOrder.Func is null) {
                builder.AppendLine($"        {rightPrimKey}=right;");
            } else {
                builder.AppendLine(IndentBlock(this.FuncAssign(primaryOrder.Func,primaryOrder.Kind.ToString(),rightPrimKey,"right"),"        "));
            }
            builder.AppendLine($"        var __gcmp={primaryCmpLocal}.Compare({leftPrimKey},{rightPrimKey});");
            builder.AppendLine($"        if(__gcmp!=0) return {this.OrderCompareExpression(primaryOrder,"__gcmp")};");
            if(thenByIdx.HasValue) {
                var thenStage=stages[thenByIdx.Value];
                var secondaryCmpLocal= this.OrderComparerLocalName(thenByIdx.Value);
                var leftSecKey=$"__lsk{thenByIdx.Value}";
                var rightSecKey=$"__rsk{thenByIdx.Value}";
                builder.AppendLine($"        {ComparerKeyType(thenStage)} {leftSecKey}=default!;");
                builder.AppendLine(IndentBlock(this.FuncAssign(thenStage.Func!,thenStage.Kind.ToString(),leftSecKey,"left"),"        "));
                builder.AppendLine($"        {ComparerKeyType(thenStage)} {rightSecKey}=default!;");
                builder.AppendLine(IndentBlock(this.FuncAssign(thenStage.Func!,thenStage.Kind.ToString(),rightSecKey,"right"),"        "));
                builder.AppendLine($"        var __gcmp2={secondaryCmpLocal}.Compare({leftSecKey},{rightSecKey});");
                builder.AppendLine($"        if(__gcmp2!=0) return {this.OrderCompareExpression(thenStage,"__gcmp2")};");
            }
            builder.AppendLine("        return 0;");
            builder.AppendLine("    });");
        }
        builder.AppendLine($"    this.{this.GroupItemsName(groupByIndex)}={this.GroupItemsName(groupByIndex)};");
        builder.AppendLine($"    this.{this.StateName(groupByIndex)}=0;");
        builder.AppendLine("}");
        builder.AppendLine($"while(this.{this.StateName(groupByIndex)}<this.{this.GroupItemsName(groupByIndex)}.Count) {{");
        // stages.Count+1 は任意の ItemName(index+1) より大きい値なので OrderBy が間にある場合でも名前衝突しない
        currentName=this.ItemName(stages.Count+1);
        builder.AppendLine($"    var {currentName}=this.{this.GroupItemsName(groupByIndex)}[this.{this.StateName(groupByIndex)}++];");
        for(var index = groupByIndex+1; index<stages.Count; index++) {
            currentName=this.AppendStage(builder,stages[index],currentName,index);
        }
        builder.AppendLine(IndentBlock(string.Format(successStatement,currentName),"    "));
        builder.AppendLine("}");
        builder.AppendLine(endStatement);
        return builder.ToString().TrimEnd();
    }

    private string BuildReverseLoop(string enumeratorName,string successStatement,string endStatement,int reverseIndex) {
        var stage = stages[reverseIndex];
        var builder = new StringBuilder();
        builder.AppendLine($"if(this.{this.ReverseItemsName(reverseIndex)} is null) {{");
        builder.AppendLine($"    var {this.ReverseItemsName(reverseIndex)}=new global::System.Collections.Generic.List<{stage.OutputType}>();");
        builder.AppendLine($"    while({enumeratorName}.TryGetNext(out var {this.ItemName(0)})) {{");
        this.AppendCounterCall(builder,"        ","IncrementPhysicalMoveNext");
        var currentName = this.ItemName(0);
        for(var index = 0; index<reverseIndex; index++) {
            currentName=this.AppendStage(builder,stages[index],currentName,index);
        }
        builder.AppendLine($"        {this.ReverseItemsName(reverseIndex)}.Add({currentName});");
        builder.AppendLine("    }");
        builder.AppendLine($"    {this.ReverseItemsName(reverseIndex)}.Reverse();");
        builder.AppendLine($"    this.{this.ReverseItemsName(reverseIndex)}={this.ReverseItemsName(reverseIndex)};");
        builder.AppendLine($"    this.{this.StateName(reverseIndex)}=0;");
        builder.AppendLine("}");
        builder.AppendLine($"while(this.{this.StateName(reverseIndex)}<this.{this.ReverseItemsName(reverseIndex)}.Count) {{");
        currentName=this.ItemName(reverseIndex+1);
        builder.AppendLine($"    var {currentName}=this.{this.ReverseItemsName(reverseIndex)}[this.{this.StateName(reverseIndex)}++];");
        for(var index = reverseIndex+1; index<stages.Count; index++) {
            currentName=this.AppendStage(builder,stages[index],currentName,index);
        }
        builder.AppendLine(IndentBlock(string.Format(successStatement,currentName),"    "));
        builder.AppendLine("}");
        builder.AppendLine(endStatement);
        return builder.ToString().TrimEnd();
    }

    private string BuildSkipLastLoop(string enumeratorName,string successStatement,string endStatement,int skipLastIndex) {
        var builder = new StringBuilder();
        var countOrd = this.CountOrdinal(skipLastIndex);
        builder.AppendLine($"while({enumeratorName}.TryGetNext(out var {this.ItemName(0)})) {{");
        this.AppendCounterCall(builder,"    ","IncrementPhysicalMoveNext");
        var currentName = this.ItemName(0);
        for(var index = 0; index<skipLastIndex; index++) {
            currentName=this.AppendStage(builder,stages[index],currentName,index);
        }
        builder.AppendLine($"    this.{this.QueueName(skipLastIndex)}.Enqueue({currentName});");
        builder.AppendLine($"    if(this.{this.QueueName(skipLastIndex)}.Count > global::System.Math.Max(0,{this.CountName(countOrd)})) {{");
        var dequeueItem = this.ItemName(skipLastIndex+1);
        builder.AppendLine($"        var {dequeueItem}=this.{this.QueueName(skipLastIndex)}.Dequeue();");
        currentName=dequeueItem;
        for(var index = skipLastIndex+1; index<stages.Count; index++) {
            currentName=this.AppendStage(builder,stages[index],currentName,index);
        }
        builder.AppendLine(IndentBlock(string.Format(successStatement,currentName),"        "));
        builder.AppendLine("    }");
        builder.AppendLine("}");
        builder.AppendLine(endStatement);
        return builder.ToString().TrimEnd();
    }

    private string BuildTakeLastLoop(string enumeratorName,string successStatement,string endStatement,int takeLastIndex) {
        var stage = stages[takeLastIndex];
        var countOrd = this.CountOrdinal(takeLastIndex);
        var builder = new StringBuilder();
        builder.AppendLine($"if(this.{this.ReverseItemsName(takeLastIndex)} is null) {{");
        builder.AppendLine($"    var __tmpList=new global::System.Collections.Generic.List<{stage.OutputType}>();");
        builder.AppendLine($"    while({enumeratorName}.TryGetNext(out var {this.ItemName(0)})) {{");
        this.AppendCounterCall(builder,"        ","IncrementPhysicalMoveNext");
        var currentName = this.ItemName(0);
        for(var index = 0; index<takeLastIndex; index++) {
            currentName=this.AppendStage(builder,stages[index],currentName,index);
        }
        builder.AppendLine($"        __tmpList.Add({currentName});");
        builder.AppendLine("    }");
        builder.AppendLine($"    var __takeN=global::System.Math.Max(0,global::System.Math.Min(global::System.Math.Max(0,{this.CountName(countOrd)}),__tmpList.Count));");
        builder.AppendLine($"    var __takeStart=__tmpList.Count-__takeN;");
        builder.AppendLine($"    this.{this.ReverseItemsName(takeLastIndex)}=__tmpList;");
        builder.AppendLine($"    this.{this.StateName(takeLastIndex)}=__takeStart;");
        builder.AppendLine("}");
        builder.AppendLine($"while(this.{this.StateName(takeLastIndex)}<this.{this.ReverseItemsName(takeLastIndex)}.Count) {{");
        currentName=this.ItemName(takeLastIndex+1);
        builder.AppendLine($"    var {currentName}=this.{this.ReverseItemsName(takeLastIndex)}[this.{this.StateName(takeLastIndex)}++];");
        for(var index = takeLastIndex+1; index<stages.Count; index++) {
            currentName=this.AppendStage(builder,stages[index],currentName,index);
        }
        builder.AppendLine(IndentBlock(string.Format(successStatement,currentName),"    "));
        builder.AppendLine("}");
        builder.AppendLine(endStatement);
        return builder.ToString().TrimEnd();
    }

    private string BuildTakeRangeLoop(string enumeratorName,string successStatement,string endStatement,int takeRangeIndex) {
        var stage = stages[takeRangeIndex];
        var rangeParamName = this.RangeName(this.RangeOrdinal(takeRangeIndex));
        var builder = new StringBuilder();
        builder.AppendLine($"if(this.{this.ReverseItemsName(takeRangeIndex)} is null) {{");
        builder.AppendLine($"    var __tmpRange=new global::System.Collections.Generic.List<{stage.OutputType}>();");
        builder.AppendLine($"    while({enumeratorName}.TryGetNext(out var {this.ItemName(0)})) {{");
        this.AppendCounterCall(builder,"        ","IncrementPhysicalMoveNext");
        var currentName = this.ItemName(0);
        for(var index = 0; index<takeRangeIndex; index++) {
            currentName=this.AppendStage(builder,stages[index],currentName,index);
        }
        builder.AppendLine($"        __tmpRange.Add({currentName});");
        builder.AppendLine("    }");
        builder.AppendLine($"    var (__rangeOff,__rangeLen)={rangeParamName}.GetOffsetAndLength(__tmpRange.Count);");
        builder.AppendLine($"    this.{this.ReverseItemsName(takeRangeIndex)}=__tmpRange;");
        builder.AppendLine($"    this.{this.StateName(takeRangeIndex)}=__rangeOff;");
        builder.AppendLine($"    this.{this.StateEndName(takeRangeIndex)}=__rangeOff+__rangeLen;");
        builder.AppendLine("}");
        builder.AppendLine($"while(this.{this.StateName(takeRangeIndex)}<this.{this.StateEndName(takeRangeIndex)}) {{");
        currentName=this.ItemName(takeRangeIndex+1);
        builder.AppendLine($"    var {currentName}=this.{this.ReverseItemsName(takeRangeIndex)}[this.{this.StateName(takeRangeIndex)}++];");
        for(var index = takeRangeIndex+1; index<stages.Count; index++) {
            currentName=this.AppendStage(builder,stages[index],currentName,index);
        }
        builder.AppendLine(IndentBlock(string.Format(successStatement,currentName),"    "));
        builder.AppendLine("}");
        builder.AppendLine(endStatement);
        return builder.ToString().TrimEnd();
    }

    private string BuildDefaultIfEmptyLoop(string enumeratorName,string successStatement,string endStatement,int defaultIfEmptyIndex) {
        var stage = stages[defaultIfEmptyIndex];
        var builder = new StringBuilder();
        builder.AppendLine($"while({enumeratorName}.TryGetNext(out var {this.ItemName(0)})) {{");
        this.AppendCounterCall(builder,"    ","IncrementPhysicalMoveNext");
        var currentName = this.ItemName(0);
        for(var index = 0; index<defaultIfEmptyIndex; index++) {
            currentName=this.AppendStage(builder,stages[index],currentName,index);
        }
        builder.AppendLine($"    this.{this.StateName(defaultIfEmptyIndex)}=true;");
        for(var index = defaultIfEmptyIndex+1; index<stages.Count; index++) {
            currentName=this.AppendStage(builder,stages[index],currentName,index);
        }
        builder.AppendLine(IndentBlock(string.Format(successStatement,currentName),"    "));
        builder.AppendLine("}");
        builder.AppendLine($"while(!this.{this.StateName(defaultIfEmptyIndex)}) {{");
        builder.AppendLine($"    this.{this.StateName(defaultIfEmptyIndex)}=true;");
        currentName=this.ItemName(defaultIfEmptyIndex+1);
        builder.AppendLine($"    {stage.OutputType} {currentName}={stage.Value};");
        for(var index = defaultIfEmptyIndex+1; index<stages.Count; index++) {
            currentName=this.AppendStage(builder,stages[index],currentName,index);
        }
        builder.AppendLine(IndentBlock(string.Format(successStatement,currentName),"    "));
        builder.AppendLine("}");
        builder.AppendLine(endStatement);
        return builder.ToString().TrimEnd();
    }

    private string BuildBinarySetLoop(string enumeratorName,string successStatement,string endStatement,int setIndex) {
        var stage = stages[setIndex];
        if(stage.Kind is PipelineStageKind.Union or PipelineStageKind.UnionBy) {
            return this.BuildUnionLoop(enumeratorName,successStatement,endStatement,setIndex);
        }

        var builder = new StringBuilder();
        builder.AppendLine($"if(!this.{this.StateName(setIndex)}) {{");
        builder.AppendLine($"    while({this.SecondEnumeratorName(this.SecondOrdinal(setIndex))}.TryGetNext(out var {this.ItemName(setIndex+1)})) {{");
        this.AppendCounterCall(builder,"        ","IncrementPhysicalMoveNext");
        builder.AppendLine($"        this.{this.SecondSetName(setIndex)}.Add(({ComparerKeyType(stage)})(object?){this.ItemName(setIndex+1)}!);");
        builder.AppendLine("    }");
        builder.AppendLine($"    this.{this.StateName(setIndex)}=true;");
        builder.AppendLine("}");
        builder.AppendLine($"while({enumeratorName}.TryGetNext(out var {this.ItemName(0)})) {{");
        this.AppendCounterCall(builder,"    ","IncrementPhysicalMoveNext");
        var currentName = this.ItemName(0);
        for(var index = 0; index<setIndex; index++) {
            currentName=this.AppendStage(builder,stages[index],currentName,index);
        }
        var setKeyName = currentName;
        if(stage.Kind is PipelineStageKind.ExceptBy or PipelineStageKind.IntersectBy) {
            setKeyName=this.KeyName(setIndex);
            builder.AppendLine($"    {ComparerKeyType(stage)} {setKeyName}=default!;");
            builder.AppendLine(IndentBlock(this.FuncAssign(stage.Func!,stage.Kind.ToString(),setKeyName,currentName),"    "));
        }
        if(stage.Kind is PipelineStageKind.Except or PipelineStageKind.ExceptBy) {
            builder.AppendLine($"    if(this.{this.SecondSetName(setIndex)}.Contains({setKeyName}) || !this.{this.SetName(setIndex)}.Add({setKeyName})) continue;");
        } else {
            builder.AppendLine($"    if(!this.{this.SecondSetName(setIndex)}.Contains({setKeyName}) || !this.{this.SetName(setIndex)}.Add({setKeyName})) continue;");
        }
        for(var index = setIndex+1; index<stages.Count; index++) {
            currentName=this.AppendStage(builder,stages[index],currentName,index);
        }
        builder.AppendLine(IndentBlock(string.Format(successStatement,currentName),"    "));
        builder.AppendLine("}");
        builder.AppendLine(endStatement);
        return builder.ToString().TrimEnd();
    }

    private string BuildUnionLoop(string enumeratorName,string successStatement,string endStatement,int unionIndex) {
        var builder = new StringBuilder();
        var endLabel = this.EndLabel(unionIndex);
        var needsEndLabel = this.StagesAfterNeedEndLabel(unionIndex);
        var breakStatement = "break;";
        if(needsEndLabel) {
            breakStatement=$"goto {endLabel};";
        }
        builder.AppendLine("while(true) {");
        builder.AppendLine($"    if(!this.{this.StateName(unionIndex)}) {{");
        builder.AppendLine($"        while({enumeratorName}.TryGetNext(out var {this.ItemName(0)})) {{");
        this.AppendCounterCall(builder,"            ","IncrementPhysicalMoveNext");
        var currentName = this.ItemName(0);
        for(var index = 0; index<unionIndex; index++) {
            currentName=this.AppendStage(builder,stages[index],currentName,index);
        }
        var firstKeyName = currentName;
        if(stages[unionIndex].Kind==PipelineStageKind.UnionBy) {
            firstKeyName=this.KeyName(unionIndex);
            builder.AppendLine($"            {ComparerKeyType(stages[unionIndex])} {firstKeyName}=default!;");
            builder.AppendLine(IndentBlock(this.FuncAssign(stages[unionIndex].Func!,nameof(PipelineStageKind.UnionBy),firstKeyName,currentName),"            "));
        }
        builder.AppendLine($"            if(!this.{this.SetName(unionIndex)}.Add({firstKeyName})) continue;");
        for(var index = unionIndex+1; index<stages.Count; index++) {
            currentName=this.AppendStage(builder,stages[index],currentName,index,breakStatement);
        }
        builder.AppendLine(IndentBlock(string.Format(successStatement,currentName),"            "));
        builder.AppendLine("        }");
        builder.AppendLine($"        this.{this.StateName(unionIndex)}=true;");
        builder.AppendLine("    }");
        builder.AppendLine($"    while({this.SecondEnumeratorName(this.SecondOrdinal(unionIndex))}.TryGetNext(out var {this.ItemName(unionIndex+1)})) {{");
        this.AppendCounterCall(builder,"        ","IncrementPhysicalMoveNext");
        var secondCurrentName = this.ItemName(unionIndex+1);
        builder.AppendLine($"        {secondCurrentName}=({stages[unionIndex].OutputType})(object?){secondCurrentName}!;");
        var secondKeyName = secondCurrentName;
        if(stages[unionIndex].Kind==PipelineStageKind.UnionBy) {
            secondKeyName=this.KeyName(unionIndex+1);
            builder.AppendLine($"        {ComparerKeyType(stages[unionIndex])} {secondKeyName}=default!;");
            builder.AppendLine(IndentBlock(this.FuncAssign(stages[unionIndex].Func!,nameof(PipelineStageKind.UnionBy),secondKeyName,secondCurrentName),"        "));
        }
        builder.AppendLine($"        if(!this.{this.SetName(unionIndex)}.Add({secondKeyName})) continue;");
        currentName=secondCurrentName;
        for(var index = unionIndex+1; index<stages.Count; index++) {
            currentName=this.AppendStage(builder,stages[index],currentName,index,breakStatement);
        }
        builder.AppendLine(IndentBlock(string.Format(successStatement,currentName),"        "));
        builder.AppendLine("    }");
        if(needsEndLabel) {
            builder.AppendLine($"    {endLabel}:");
        }
        builder.AppendLine(IndentBlock(endStatement,"    "));
        builder.AppendLine("}");
        return builder.ToString().TrimEnd();
    }

    private string BuildSelectManyLoop(string enumeratorName,string successStatement,string endStatement,int selectManyIndex) {
        var builder = new StringBuilder();
        var selectManyStage = stages[selectManyIndex];
        var endLabel = this.EndLabel(selectManyIndex);
        var needsEndLabel = this.StagesAfterNeedEndLabel(selectManyIndex);
        var breakStatement = "break;";
        if(needsEndLabel) {
            breakStatement=$"goto {endLabel};";
        }
        var innerItemName = this.ItemName(selectManyIndex+1);
        builder.AppendLine("while(true) {");
        if(selectManyStage.InnerIsArray) {
            // 内側シーケンスが配列の場合は IEnumerator<T> を介さず、インデックスで直接走査する。
            var arrayName = this.InnerArrayName(selectManyIndex);
            var indexName = this.InnerIndexName(selectManyIndex);
            var hasValueName = this.InnerHasValueName(selectManyIndex);
            builder.AppendLine($"    if(this.{hasValueName}) {{");
            builder.AppendLine($"        if(this.{indexName} < this.{arrayName}.Length) {{");
            builder.AppendLine($"            var {innerItemName}=this.{arrayName}[this.{indexName}];");
            builder.AppendLine($"            this.{indexName}++;");
            this.AppendSelectManyTail(builder,selectManyStage,selectManyIndex,innerItemName,breakStatement,successStatement,"            ");
            builder.AppendLine("        }");
            builder.AppendLine($"        this.{hasValueName}=false;");
            builder.AppendLine("    }");
            builder.AppendLine($"    while({enumeratorName}.TryGetNext(out var {this.ItemName(0)})) {{");
            this.AppendCounterCall(builder,"        ","IncrementPhysicalMoveNext");
            var outerCurrentName0 = this.AppendSelectManyHead(builder,selectManyStage,selectManyIndex,this.ItemName(0));
            var collectionExpression0 = this.SelectManyCollectionExpression(selectManyStage,selectManyIndex,outerCurrentName0);
            builder.AppendLine($"        this.{arrayName}={collectionExpression0};");
            builder.AppendLine($"        this.{indexName}=0;");
            builder.AppendLine($"        this.{hasValueName}=true;");
            builder.AppendLine("        break;");
            builder.AppendLine("    }");
            builder.AppendLine($"    if(!this.{hasValueName}) {{");
            builder.AppendLine(IndentBlock(endStatement,"        "));
            builder.AppendLine("    }");
        } else {
            var innerEnumeratorName = this.InnerEnumeratorName(selectManyIndex);
            builder.AppendLine($"    if(this.{innerEnumeratorName} is not null) {{");
            builder.AppendLine($"        if(this.{innerEnumeratorName}.MoveNext()) {{");
            builder.AppendLine($"            var {innerItemName}=this.{innerEnumeratorName}.Current;");
            this.AppendSelectManyTail(builder,selectManyStage,selectManyIndex,innerItemName,breakStatement,successStatement,"            ");
            builder.AppendLine("        }");
            builder.AppendLine($"        this.{innerEnumeratorName}.Dispose();");
            builder.AppendLine($"        this.{innerEnumeratorName}=null;");
            builder.AppendLine("    }");
            builder.AppendLine($"    while({enumeratorName}.TryGetNext(out var {this.ItemName(0)})) {{");
            this.AppendCounterCall(builder,"        ","IncrementPhysicalMoveNext");
            var outerCurrentName = this.AppendSelectManyHead(builder,selectManyStage,selectManyIndex,this.ItemName(0));
            var collectionExpression = this.SelectManyCollectionExpression(selectManyStage,selectManyIndex,outerCurrentName);
            builder.AppendLine($"        this.{innerEnumeratorName}=((global::System.Collections.Generic.IEnumerable<{selectManyStage.InnerElementType}>){collectionExpression}).GetEnumerator();");
            builder.AppendLine("        break;");
            builder.AppendLine("    }");
            builder.AppendLine($"    if(this.{innerEnumeratorName} is null) {{");
            builder.AppendLine(IndentBlock(endStatement,"        "));
            builder.AppendLine("    }");
        }
        builder.AppendLine("}");
        if(needsEndLabel) {
            builder.AppendLine($"{endLabel}:");
            builder.AppendLine(endStatement);
        }
        return builder.ToString().TrimEnd();
    }

    // SelectMany の内側要素を取得した後、resultSelector の適用と後続ステージの処理・成功文の出力までを行う。
    private string AppendSelectManyTail(StringBuilder builder,PipelineStage selectManyStage,int selectManyIndex,string innerItemName,string breakStatement,string successStatement,string indent) {
        this.AppendCounterCall(builder,indent,"IncrementLogicalSelectManyInner");
        var currentName = innerItemName;
        if(selectManyStage.SecondFunc is { } resultSelector) {
            // resultSelector の出力は専用名にする。後続 Select 段は配列 index(=selectManyIndex+1)から
            // ItemName(selectManyIndex+2) を出力に使うため、ItemName(selectManyIndex+2) を流用すると衝突する。
            currentName=this.SelectManyResultName(selectManyIndex);
            builder.AppendLine(IndentBlock($"{selectManyStage.OutputType} {currentName}=default!;",indent));
            builder.AppendLine(IndentBlock(this.FuncAssign(resultSelector,nameof(PipelineStageKind.SelectMany),currentName,this.OuterName(selectManyIndex),innerItemName),indent));
        }
        for(var index = selectManyIndex+1; index<stages.Count; index++) {
            var stage = stages[index];
            currentName=this.AppendStage(builder,stage,currentName,index,breakStatement);
        }
        builder.AppendLine(IndentBlock(string.Format(successStatement,currentName),indent));
        return currentName;
    }

    // SelectMany より前段のステージを適用し、outerValue フィールドへの代入とインデックス更新までを行う。
    private string AppendSelectManyHead(StringBuilder builder,PipelineStage selectManyStage,int selectManyIndex,string outerCurrentName) {
        var currentName = outerCurrentName;
        for(var index = 0; index<selectManyIndex; index++) {
            var stage = stages[index];
            currentName=this.AppendStage(builder,stage,currentName,index);
        }
        var outerName = this.OuterName(selectManyIndex);
        this.AppendCounterCall(builder,"        ","IncrementLogicalSelectManyOuter");
        builder.AppendLine($"        this.{outerName}={currentName};");
        if(selectManyStage.UsesIndex) {
            builder.AppendLine($"        this.{this.StateName(selectManyIndex)}++;");
        }
        return currentName;
    }

    private string SelectManyCollectionExpression(PipelineStage selectManyStage,int selectManyIndex,string currentName) {
        string[] selectorArguments;
        if(selectManyStage.UsesIndex) {
            selectorArguments=[currentName,$"this.{this.StateName(selectManyIndex)}"];
        } else {
            selectorArguments=[currentName];
        }
        return this.FuncInvoke(selectManyStage.Func!,nameof(PipelineStageKind.SelectMany),selectorArguments);
    }

    private string AppendStage(StringBuilder builder,PipelineStage stage,string currentName,int index,string breakStatement = "break;") {
        switch(stage.Kind) {
            case PipelineStageKind.Where:
                this.AppendWhere(builder,stage,currentName,index);
                return currentName;
            case PipelineStageKind.Select:
                return this.AppendSelect(builder,stage,currentName,index);
            case PipelineStageKind.Take:
                this.AppendTake(builder,index,this.CountOrdinal(index),breakStatement);
                return currentName;
            case PipelineStageKind.Skip:
                this.AppendSkip(builder,index,this.CountOrdinal(index));
                return currentName;
            case PipelineStageKind.TakeWhile:
                this.AppendTakeWhile(builder,stage,currentName,index,breakStatement);
                return currentName;
            case PipelineStageKind.SkipWhile:
                this.AppendSkipWhile(builder,stage,currentName,index);
                return currentName;
            case PipelineStageKind.Cast:
                return this.AppendCast(builder,stage,currentName,index);
            case PipelineStageKind.OfType:
                return this.AppendOfType(builder,stage,currentName,index);
            case PipelineStageKind.Distinct:
                this.AppendDistinct(builder,stage,currentName,index);
                return currentName;
            case PipelineStageKind.DistinctBy:
                this.AppendDistinctBy(builder,stage,currentName,index);
                return currentName;
            case PipelineStageKind.Union:
            case PipelineStageKind.Except:
            case PipelineStageKind.Intersect:
            case PipelineStageKind.UnionBy:
            case PipelineStageKind.ExceptBy:
            case PipelineStageKind.IntersectBy:
                return currentName;
            case PipelineStageKind.Concat:
            case PipelineStageKind.Append:
            case PipelineStageKind.Prepend:
                return currentName;
            case PipelineStageKind.OrderBy:
            case PipelineStageKind.OrderByDescending:
            case PipelineStageKind.ThenBy:
            case PipelineStageKind.ThenByDescending:
                return currentName;
            case PipelineStageKind.GroupBy:
                return currentName;
            case PipelineStageKind.Reverse:
                return currentName;
            case PipelineStageKind.AsEnumerable:
                return currentName;
            default:
                throw new NotSupportedException($"Unsupported nested pipeline stage: {stage.Kind}.");
        }
    }

    private void AppendWhere(StringBuilder builder,PipelineStage stage,string currentName,int index) {
        string[] arguments;
        if(stage.UsesIndex) {
            builder.AppendLine($"    this.{this.StateName(index)}++;");
            arguments=[currentName,$"this.{this.StateName(index)}"];
        } else {
            arguments=[currentName];
        }
        this.AppendCounterCall(builder,"    ","IncrementLogicalWhere");
        var (preCode,condExpr) =this.FuncInvokeCondition(stage.Func!,nameof(PipelineStageKind.Where),this.TempName(index),arguments);
        if(!string.IsNullOrEmpty(preCode)) {
            builder.AppendLine(IndentBlock(preCode,"    "));
        }
        builder.AppendLine($"    if(!{WrapConditionExpression(condExpr)}) continue;");
    }

    private string AppendSelect(StringBuilder builder,PipelineStage stage,string currentName,int index) {
        var nextName = this.ItemName(index+1);
        builder.AppendLine($"    {stage.OutputType} {nextName}=default!;");
        string[] arguments;
        if(stage.UsesIndex) {
            builder.AppendLine($"    this.{this.StateName(index)}++;");
            arguments=[currentName,$"this.{this.StateName(index)}"];
        } else {
            arguments=[currentName];
        }
        this.AppendCounterCall(builder,"    ","IncrementLogicalSelect");
        builder.AppendLine(IndentBlock(this.FuncAssign(stage.Func!,nameof(PipelineStageKind.Select),nextName,arguments),"    "));
        return nextName;
    }

    private void AppendTake(StringBuilder builder,int index,int countIndex,string breakStatement = "break;") {
        var stateName = this.StateName(index);
        var countName = this.CountName(countIndex);
        builder.AppendLine($"    if(this.{stateName}>=this.{countName}Value) {{");
        builder.AppendLine($"        {breakStatement}");
        builder.AppendLine("    }");
        builder.AppendLine($"    this.{stateName}++;");
    }

    private void AppendSkip(StringBuilder builder,int index,int countIndex) {
        var stateName = this.StateName(index);
        var countName = this.CountName(countIndex);
        builder.AppendLine($"    if(this.{stateName}<this.{countName}Value) {{");
        builder.AppendLine($"        this.{stateName}++;");
        builder.AppendLine("        continue;");
        builder.AppendLine("    }");
    }

    private void AppendTakeWhile(StringBuilder builder,PipelineStage stage,string currentName,int index,string breakStatement = "break;") {
        var stateName = this.StateName(index);
        builder.AppendLine($"    if(this.{stateName}) {breakStatement}");
        string[] arguments;
        if(stage.UsesIndex) {
            builder.AppendLine($"    this.{this.IndexName(index)}++;");
            arguments=[currentName,$"this.{this.IndexName(index)}"];
        } else {
            arguments=[currentName];
        }
        var (preCode,condExpr) =this.FuncInvokeCondition(stage.Func!,nameof(PipelineStageKind.TakeWhile),this.TempName(index),arguments);
        if(!string.IsNullOrEmpty(preCode)) {
            builder.AppendLine(IndentBlock(preCode,"    "));
        }
        builder.AppendLine($"    if(!{WrapConditionExpression(condExpr)}) {{");
        builder.AppendLine($"        this.{stateName}=true;");
        builder.AppendLine($"        {breakStatement}");
        builder.AppendLine("    }");
    }

    private void AppendSkipWhile(StringBuilder builder,PipelineStage stage,string currentName,int index) {
        var stateName = this.StateName(index);
        string[] arguments;
        if(stage.UsesIndex) {
            arguments=[currentName,$"this.{this.IndexName(index)}"];
        } else {
            arguments=[currentName];
        }
        var (preCode,condExpr) =this.FuncInvokeCondition(stage.Func!,nameof(PipelineStageKind.SkipWhile),this.TempName(index),arguments);
        builder.AppendLine($"    if(this.{stateName}) {{");
        if(stage.UsesIndex) {
            builder.AppendLine($"        this.{this.IndexName(index)}++;");
        }
        if(!string.IsNullOrEmpty(preCode)) {
            builder.AppendLine(IndentBlock(preCode,"        "));
        }
        builder.AppendLine($"        if({WrapConditionExpression(condExpr)}) continue;");
        builder.AppendLine($"        this.{stateName}=false;");
        builder.AppendLine("    }");
    }

    private string AppendCast(StringBuilder builder,PipelineStage stage,string currentName,int index) {
        var nextName = this.ItemName(index+1);
        builder.AppendLine($"    var {nextName}=({stage.OutputType})(object?){currentName};");
        return nextName;
    }

    private string AppendOfType(StringBuilder builder,PipelineStage stage,string currentName,int index) {
        var nextName = this.ItemName(index+1);
        builder.AppendLine($"    if({currentName} is not {stage.OutputType} {nextName}) continue;");
        return nextName;
    }

    private void AppendDistinct(StringBuilder builder,PipelineStage stage,string currentName,int index) {
        builder.AppendLine($"    if(!this.{this.SetName(index)}.Add({currentName})) continue;");
    }

    private void AppendDistinctBy(StringBuilder builder,PipelineStage stage,string currentName,int index) {
        var keyName = this.KeyName(index);
        builder.AppendLine($"    {ComparerKeyType(stage)} {keyName}=default!;");
        builder.AppendLine(IndentBlock(this.FuncAssign(stage.Func!,nameof(PipelineStageKind.DistinctBy),keyName,currentName),"    "));
        builder.AppendLine($"    if(!this.{this.SetName(index)}.Add({keyName})) continue;");
    }

    private IEnumerable<string> ClosureArgsWithoutPrefix() {
        var suffix = this.ClosureArgsSuffix(this.Funcs.ToArray());
        if(string.IsNullOrEmpty(suffix)) {
            yield break;
        }
        foreach(var part in SplitTopLevelComma(suffix[2..])) {
            yield return part.Trim();
        }
    }

    private IEnumerable<string> ClosureParametersWithoutPrefix() {
        var suffix = this.ClosurePrimaryCtorSuffix(this.Funcs.ToArray());
        if(string.IsNullOrEmpty(suffix)) {
            yield break;
        }
        foreach(var part in SplitTopLevelComma(suffix[2..])) {
            yield return part.Trim();
        }
    }

    private static IEnumerable<string> SplitTopLevelComma(string text) {
        var parenDepth = 0;
        var bracketDepth = 0;
        var braceDepth = 0;
        var angleDepth = 0;
        var start = 0;
        var inString = false;
        var inChar = false;
        var inVerbatimString = false;
        for(var index = 0;index<=text.Length;index++) {
            if(index==text.Length) {
                yield return text[start..index];
                yield break;
            }

            var current = text[index];
            var next = index+1<text.Length ? text[index+1] : '\0';
            if(inString) {
                if(inVerbatimString && current=='"' && next=='"') {
                    index++;
                    continue;
                }
                if(!inVerbatimString && current=='\\') {
                    index++;
                    continue;
                }
                if(current=='"') {
                    inString=false;
                    inVerbatimString=false;
                }
                continue;
            }
            if(inChar) {
                if(current=='\\') {
                    index++;
                    continue;
                }
                if(current=='\'') {
                    inChar=false;
                }
                continue;
            }

            if(current=='@' && next=='"') {
                inString=true;
                inVerbatimString=true;
                index++;
                continue;
            }
            if(current=='"') {
                inString=true;
                continue;
            }
            if(current=='\'') {
                inChar=true;
                continue;
            }

            switch(current) {
                case '(':
                    parenDepth++;
                    break;
                case ')':
                    parenDepth--;
                    break;
                case '[':
                    bracketDepth++;
                    break;
                case ']':
                    bracketDepth--;
                    break;
                case '{':
                    braceDepth++;
                    break;
                case '}':
                    braceDepth--;
                    break;
                case '<':
                    angleDepth++;
                    break;
                case '>':
                    angleDepth--;
                    break;
                case ',' when parenDepth==0 && bracketDepth==0 && braceDepth==0 && angleDepth==0:
                    yield return text[start..index];
                    start=index+1;
                    break;
            }
        }
    }

    private string QueueName(int index) => $"__queue{this.item}_{index}";
    private string StateEndName(int index) => $"__stateEnd{this.item}_{index}";
    private string RangeName(int index) => $"__range{this.item}_{index}";
    private int RangeOrdinal(int stageIndex) =>
        stages.Take(stageIndex).Count(static stage => stage.Kind==PipelineStageKind.TakeRange);
    private IEnumerable<PipelineStage> RangeStages() =>
        stages.Where(static stage => stage.Kind==PipelineStageKind.TakeRange);
    private string ItemName(int index) => $"{this.item}_{index}";
    private string SelectManyResultName(int index) => $"__smr{this.item}_{index}";
    private string TempName(int index) => $"{this.temp}_{index}";
    private string StateName(int index) => $"{this.state}_{index}";
    private string IndexName(int index) => $"__idx{this.item}_{index}";
    private string CountName(int index) => $"__cnt{this.item}_{index}";
    private string ComparerName(int index) => $"__cmp{this.item}_{index}";
    private string OrderComparerName(int index) => $"__ordcmp{this.item}_{index}";
    private string OrderComparerLocalName(int index) => $"__ordcmpLocal{this.item}_{index}";
    private static string SecondTypeParameterName(int index) => $"TSecond{index}";
    private string SecondEnumeratorName(int index) => $"__second{this.item}_{index}";
    private string InnerEnumeratorName(int index) => $"__inner{this.item}_{index}";
    private string InnerArrayName(int index) => $"__innerArr{this.item}_{index}";
    private string InnerIndexName(int index) => $"__innerIdx{this.item}_{index}";
    private string InnerHasValueName(int index) => $"__innerHas{this.item}_{index}";
    private string OuterName(int index) => $"{this.outerValue}_{index}";
    private string SetName(int index) => $"__set{this.item}_{index}";
    private string SecondSetName(int index) => $"__set2{this.item}_{index}";
    private string SortItemName(int index) => $"__sortitem{this.item}_{index}";
    private string SortedItemsName(int index) => $"__sorted{this.item}_{index}";
    private string GroupingName(int index) => $"__grouping{this.item}_{index}";
    private string GroupItemsName(int index) => $"__groups{this.item}_{index}";
    private string GroupName(int index) => $"__group{this.item}_{index}";
    private string ReverseItemsName(int index) => $"__reversed{this.item}_{index}";
    private string EndLabel(int index) => $"__end{this.item}_{index}";
    private string KeyName(int index) => $"{this.key}_{index}";
    private bool StagesAfterNeedEndLabel(int index) =>
        stages.Skip(index+1).Any(static stage => stage.Kind is PipelineStageKind.Take or PipelineStageKind.TakeWhile);
    private int CountOrdinal(int stageIndex) =>
        stages.Take(stageIndex).Count(static stage => stage.Kind==PipelineStageKind.Take || stage.Kind==PipelineStageKind.Skip);
    private int ComparerOrdinal(int stageIndex) =>
        stages.Take(stageIndex).Count(static stage => stage.Kind is PipelineStageKind.Distinct or PipelineStageKind.DistinctBy or PipelineStageKind.Union or PipelineStageKind.Except or PipelineStageKind.Intersect or PipelineStageKind.UnionBy or PipelineStageKind.ExceptBy or PipelineStageKind.IntersectBy or PipelineStageKind.GroupBy);
    private int OrderComparerOrdinal(int stageIndex) =>
        stages.Take(stageIndex).Count(static stage => stage.Kind is PipelineStageKind.OrderBy or PipelineStageKind.OrderByDescending or PipelineStageKind.ThenBy or PipelineStageKind.ThenByDescending);
    private int SecondOrdinal(int stageIndex) =>
        stages.Take(stageIndex).Count(static stage => stage.SecondSource is not null);
    private static string ComparerKeyType(PipelineStage stage) => stage.KeyType ?? stage.OutputType;
    private IEnumerable<PipelineStage> OrderStages(int orderIndex) {
        yield return stages[orderIndex];
        if(orderIndex+1<stages.Count
            && stages[orderIndex+1].Kind is PipelineStageKind.ThenBy or PipelineStageKind.ThenByDescending) {
            yield return stages[orderIndex+1];
        }
    }
    private string OrderCompareExpression(PipelineStage stage,string compareName) {
        if(stage.Descending) {
            return $"-{compareName}";
        }
        return compareName;
    }
    private string OrderCompareCall(PipelineStage stage,string left,string right,string comparerName) =>
        CanUseDirectDefaultOrderCompare(stage)
            ? $"{left}.CompareTo({right})"
            : $"{comparerName}.Compare({left},{right})";
    private static bool CanUseDirectDefaultOrderCompare(PipelineStage stage) =>
        stage.Value=="null"
        && (ComparerKeyType(stage) is "byte" or "sbyte"
            or "short" or "ushort"
            or "int" or "uint"
            or "long" or "ulong"
            or "float" or "double" or "decimal"
            or "char" or "string"
            or "global::System.Byte" or "global::System.SByte"
            or "global::System.Int16" or "global::System.UInt16"
            or "global::System.Int32" or "global::System.UInt32"
            or "global::System.Int64" or "global::System.UInt64"
            or "global::System.Single" or "global::System.Double" or "global::System.Decimal"
            or "global::System.Char" or "global::System.String");

    private string OrderSortItemType(int orderIndex) {
        var orderStages = this.OrderStages(orderIndex).ToArray();
        var primaryOrder = orderStages[0];
        if(orderStages.Length==2) {
            var secondaryOrder = orderStages[1];
            return $$"""
            private readonly struct {{this.SortItemName(orderIndex)}} {
                public {{this.SortItemName(orderIndex)}}({{primaryOrder.InputType}} value,{{ComparerKeyType(primaryOrder)}} key,{{ComparerKeyType(secondaryOrder)}} secondKey,int index) {
                    this.Value=value;
                    this.Key=key;
                    this.SecondKey=secondKey;
                    this.Index=index;
                }
                public {{primaryOrder.InputType}} Value { get; }
                public {{ComparerKeyType(primaryOrder)}} Key { get; }
                public {{ComparerKeyType(secondaryOrder)}} SecondKey { get; }
                public int Index { get; }
            }
            """;
        }
        return $$"""
        private readonly struct {{this.SortItemName(orderIndex)}} {
            public {{this.SortItemName(orderIndex)}}({{primaryOrder.InputType}} value,{{ComparerKeyType(primaryOrder)}} key,int index) {
                this.Value=value;
                this.Key=key;
                this.Index=index;
            }
            public {{primaryOrder.InputType}} Value { get; }
            public {{ComparerKeyType(primaryOrder)}} Key { get; }
            public int Index { get; }
        }
        """;
    }

    private string GroupValueType(int index,PipelineStage stage) {
        if(stage.ThirdFunc is not null) {
            return stage.OutputType;
        }
        return this.GroupingName(index);
    }

    private string GroupingType(int index,PipelineStage stage) => $$"""
        private sealed class {{this.GroupingName(index)}}:global::System.Linq.IGrouping<{{ComparerKeyType(stage)}},{{stage.InnerElementType}}> {
            private readonly global::System.Collections.Generic.List<{{stage.InnerElementType}}> values = new();
            public {{this.GroupingName(index)}}({{ComparerKeyType(stage)}} key) {
                this.Key=key;
            }
            public {{ComparerKeyType(stage)}} Key { get; }
            public void Add({{stage.InnerElementType}} value) => this.values.Add(value);
            public global::System.Collections.Generic.IEnumerator<{{stage.InnerElementType}}> GetEnumerator() => this.values.GetEnumerator();
            global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator() => this.GetEnumerator();
        }
        """;

    private static string IndentBlock(string text,string indent) {
        if(string.IsNullOrWhiteSpace(text)) {
            return string.Empty;
        }
        var lines = text.Replace("\r\n","\n").Split('\n');
        var builder = new StringBuilder();
        for(var index = 0; index<lines.Length; index++) {
            if(!string.IsNullOrWhiteSpace(lines[index])) {
                builder.Append(indent).Append(lines[index]);
            }
            if(index<lines.Length-1) {
                builder.AppendLine();
            }
        }
        return builder.ToString();
    }
}

public sealed class PipelineTerminal(int 番号,PipelineEnumerable pipeline,PipelineTerminalKind terminalKind,FuncFragment? func1=null,FuncFragment? func2=null,string? comparerValue=null):Scalar(番号),DirectExecutionFragment {
    protected override Enumerable PrimarySource => pipeline;
    private string ResultType {
        get {
            if(terminalKind==PipelineTerminalKind.Any) {
                return "bool";
            }
            if(terminalKind==PipelineTerminalKind.Count) {
                return "int";
            }
            if(terminalKind==PipelineTerminalKind.First) {
                return pipeline.ElementType;
            }
            throw new NotSupportedException($"{terminalKind} does not support scalar enumerator generation.");
        }
    }
    public override string StructMethod => pipeline.StructMethod;
    public override string GetEnumeratorStruct {
        get {
            if(terminalKind is PipelineTerminalKind.ToList or PipelineTerminalKind.ToArray or PipelineTerminalKind.ToDictionary or PipelineTerminalKind.Sum) {
                return pipeline.GetEnumeratorStruct;
            }
            return $$"""
                {{pipeline.GetEnumeratorStruct}}
                {{this.DefineScalarEnumeratorStruct(this.ResultType)}}
                """;
        }
    }
    public override string GetEnumeratorExpression {
        get {
            if(terminalKind is PipelineTerminalKind.ToList or PipelineTerminalKind.ToArray or PipelineTerminalKind.ToDictionary or PipelineTerminalKind.Sum) {
                throw new NotSupportedException($"{nameof(PipelineTerminal)} uses direct execution.");
            }
            return $"new {this.EnumeratorTypeName}({pipeline.GetEnumeratorExpression})";
        }
    }
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}";
    public override string GetEnumeratorConstructor => $"this.Current = Compute({this.e});";
    public override string GetEnumeratorFields => $$"""
        public {{this.ResultType}} Current { get; private set; }
        private bool {{this.yielded}} = false;
        {{AggressiveInliningAttribute}}
        private static {{this.ResultType}} Compute(TEnum source) {
            {{this.ComputeBody}}
        }
        """;
    public override string GetTryGetNextBody => $$"""
        if(this.{{this.yielded}}) {
            current = default!;
            return false;
        }
        this.{{this.yielded}} = true;
        current = this.Current;
        return true;
        """;
    public override string GetCurrentExpression => "Current";
    protected override string GetCurrentPropertyDeclaration(string currentType) => string.Empty;
    private string ComputeBody {
        get {
            if(terminalKind==PipelineTerminalKind.Count) {
                return "int count = 0;\nwhile(source.TryGetNext(out _)) count++;\nreturn count;";
            }
            if(terminalKind==PipelineTerminalKind.Any) {
                return "return source.TryGetNext(out _);";
            }
            if(terminalKind==PipelineTerminalKind.First) {
                return "if(source.TryGetNext(out var item)) return item;\nthrow global::Tsavorite.GeneratedCodeHelpers.NoElements();";
            }
            throw new NotSupportedException($"{terminalKind} does not support scalar enumerator generation.");
        }
    }
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
    public string GetDirectExecutionStatement(string actualVariableName,string actualTypeName) {
        var builder = new StringBuilder();
        if(terminalKind==PipelineTerminalKind.ToList) {
            builder.AppendLine($"using var {this.EnumeratorName()}={pipeline.GetEnumeratorExpression};");
            builder.AppendLine($"{actualTypeName} {actualVariableName};");
            builder.AppendLine($"if({this.EnumeratorName()}.TryGetNonEnumeratedCount(out var __count{this.result})) {{");
            builder.AppendLine($"    {actualVariableName}=new(__count{this.result});");
            builder.AppendLine($"}}else{{");
            builder.AppendLine($"    {actualVariableName}=new();");
            builder.AppendLine($"}}");
            builder.AppendLine($"while({this.EnumeratorName()}.TryGetNext(out var __item{this.result})) {actualVariableName}.Add(__item{this.result});");
            return builder.ToString().TrimEnd();
        }
        if(terminalKind==PipelineTerminalKind.ToArray) {
            var listName = $"{actualVariableName}_list";
            var countName = $"{actualVariableName}_count";
            var indexName = $"{actualVariableName}_index";
            var endLabel = $"__arrayEnd{this.result}";
            builder.AppendLine($"using var {this.EnumeratorName()}={pipeline.GetEnumeratorExpression};");
            builder.AppendLine($"{actualTypeName} {actualVariableName};");
            builder.AppendLine($"if({this.EnumeratorName()}.TryGetNonEnumeratedCount(out var {countName})) {{");
            builder.AppendLine($"    {actualVariableName}=global::System.GC.AllocateUninitializedArray<{pipeline.ElementType}>({countName});");
            builder.AppendLine($"    var {indexName}=0;");
            builder.AppendLine($"    while({this.EnumeratorName()}.TryGetNext(out var __item{this.result})) {{");
            builder.AppendLine($"        if((uint){indexName}>=(uint){actualVariableName}.Length) {{");
            builder.AppendLine($"            var {listName}=new global::System.Collections.Generic.List<{pipeline.ElementType}>({actualVariableName});");
            builder.AppendLine($"            do {{");
            builder.AppendLine($"                {listName}.Add(__item{this.result});");
            builder.AppendLine($"            }} while({this.EnumeratorName()}.TryGetNext(out __item{this.result}));");
            builder.AppendLine($"            {actualVariableName}={listName}.ToArray();");
            builder.AppendLine($"            goto {endLabel};");
            builder.AppendLine($"        }}");
            builder.AppendLine($"        {actualVariableName}[{indexName}++]=__item{this.result};");
            builder.AppendLine($"    }}");
            builder.AppendLine($"    if({indexName}!={actualVariableName}.Length) global::System.Array.Resize(ref {actualVariableName},{indexName});");
            builder.AppendLine($"}}else{{");
            builder.AppendLine($"    var {listName}=new global::System.Collections.Generic.List<{pipeline.ElementType}>();");
            builder.AppendLine($"    while({this.EnumeratorName()}.TryGetNext(out var __item{this.result})) {listName}.Add(__item{this.result});");
            builder.AppendLine($"    {actualVariableName}={listName}.ToArray();");
            builder.AppendLine($"}}");
            builder.AppendLine($"{endLabel}:;");
            return builder.ToString().TrimEnd();
        }
        if(terminalKind==PipelineTerminalKind.Count) {
            builder.AppendLine($"{actualTypeName} {actualVariableName}=0;");
            builder.AppendLine($"using var {this.EnumeratorName()}={pipeline.GetEnumeratorExpression};");
            builder.AppendLine($"while({this.EnumeratorName()}.TryGetNext(out _)) {actualVariableName}++;");
            return builder.ToString().TrimEnd();
        }
        if(terminalKind==PipelineTerminalKind.Any) {
            builder.AppendLine($"{actualTypeName} {actualVariableName}=false;");
            builder.AppendLine($"using var {this.EnumeratorName()}={pipeline.GetEnumeratorExpression};");
            builder.AppendLine($"if({this.EnumeratorName()}.TryGetNext(out _)) {actualVariableName}=true;");
            return builder.ToString().TrimEnd();
        }
        if(terminalKind==PipelineTerminalKind.First) {
            builder.AppendLine($"{actualTypeName} {actualVariableName}=default!;");
            builder.AppendLine($"using var {this.EnumeratorName()}={pipeline.GetEnumeratorExpression};");
            builder.AppendLine($"if({this.EnumeratorName()}.TryGetNext(out var __item{this.result})) {{");
            builder.AppendLine($"    {actualVariableName}=__item{this.result};");
            builder.AppendLine("} else {");
            builder.AppendLine("    throw global::Tsavorite.GeneratedCodeHelpers.NoElements();");
            builder.AppendLine("}");
            return builder.ToString().TrimEnd();
        }
        if(terminalKind==PipelineTerminalKind.Sum) {
            builder.AppendLine($"{actualTypeName} {actualVariableName}=default!;");
            builder.AppendLine($"using var {this.EnumeratorName()}={pipeline.GetEnumeratorExpression};");
            builder.AppendLine($"while({this.EnumeratorName()}.TryGetNext(out var __item{this.result})) {actualVariableName}+=__item{this.result};");
            return builder.ToString().TrimEnd();
        }
        if(terminalKind==PipelineTerminalKind.ToDictionary) {
            var func1Code = func1;
            var func2Code = func2;
            var cmpArg = comparerValue ?? "null";
            var keyType = func1Code?.ReturnTypeName ?? "object";
            var valType = func2Code?.ReturnTypeName ?? pipeline.ElementType;
            var dictTypeName = $"global::System.Collections.Generic.Dictionary<{keyType},{valType}>";
            var eName = this.EnumeratorName();
            var itemName = $"__dItem{this.result}";
            var keyName = $"__dKey{this.result}";
            var valName = $"__dVal{this.result}";
            builder.AppendLine($"using var {eName}={pipeline.GetEnumeratorExpression};");
            builder.AppendLine($"{actualTypeName} {actualVariableName};");
            builder.AppendLine($"if({eName}.TryGetNonEnumeratedCount(out var __count{this.result})) {{");
            builder.AppendLine($"    {actualVariableName}=new {dictTypeName}(__count{this.result},(global::System.Collections.Generic.IEqualityComparer<{keyType}>?){cmpArg});");
            builder.AppendLine($"}}else{{");
            builder.AppendLine($"    {actualVariableName}=new {dictTypeName}((global::System.Collections.Generic.IEqualityComparer<{keyType}>?){cmpArg});");
            builder.AppendLine($"}}");
            builder.AppendLine($"while({eName}.TryGetNext(out var {itemName})) {{");
            builder.AppendLine($"    {keyType} {keyName}=default!;");
            AppendIndented(builder,this.FuncAssign(func1Code!,"ToDictionary",keyName,itemName),"    ");
            if(func2Code is not null) {
                builder.AppendLine($"    {valType} {valName}=default!;");
                AppendIndented(builder,this.FuncAssign(func2Code,"ToDictionary",valName,itemName),"    ");
                builder.AppendLine($"    {actualVariableName}[{keyName}]={valName};");
            } else {
                builder.AppendLine($"    {actualVariableName}[{keyName}]=({valType})(object?){itemName}!;");
            }
            builder.AppendLine("}");
            return builder.ToString().TrimEnd();
        }
        throw new NotSupportedException($"Unsupported pipeline terminal: {terminalKind}.");
    }

    private string EnumeratorName() => $"__pipeline{this.result}";
    private static void AppendIndented(StringBuilder sb,string text,string indent) {
        foreach(var line in text.Replace("\r\n","\n").Split('\n')) {
            if(!string.IsNullOrWhiteSpace(line)) sb.AppendLine($"{indent}{line.Trim()}");
        }
    }
}
