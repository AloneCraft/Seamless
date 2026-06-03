using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Seamless.SourceGenerator.Linq.Fragments;

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
        if(invocations.Count<2) {
            return false;
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

        if(!ValidateStageCombination(stages,terminalKind)) {
            return false;
        }

        var pipeline = new PipelineEnumerable(snapshot++,sourceFragment,stages.ToArray(),currentElementType);
        AddUnsafeAccessors(stages,unsafeAccessorCollector);
        if(terminalKind is { } terminal) {
            if(terminalResultType is null) {
                return false;
            }
            fragment=new PipelineTerminal(snapshot++,pipeline,terminal,terminalFunc1,terminalFunc2,terminalComparer);
        } else {
            fragment=pipeline;
        }
        uniqueNumber=snapshot;
        staticMethods=services.JoinStaticMethods(sourceStaticMethods,fragment.StructMethod);
        return true;
    }

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
                || method.ReducedFrom is null
                || (method.ReducedFrom ?? method).ContainingType.ToDisplayString()!="System.Linq.Enumerable") {
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
                if(!services.BuildFunc(invocation.ArgumentList.Arguments[0].Expression,predicateType,ref uniqueNumber,out var predicate)
                    || !services.CanUseFunc(predicate)) {
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
                if(!services.BuildFunc(invocation.ArgumentList.Arguments[0].Expression,selectorType,ref uniqueNumber,out var selector)
                    || !services.CanUseFunc(selector)) {
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
                if(invocation.ArgumentList.Arguments.Count==1
                    && method.Parameters.Length>=1) {
                    comparer=services.RenderExpression(invocation.ArgumentList.Arguments[0].Expression);
                } else if(invocation.ArgumentList.Arguments.Count>1) {
                    return false;
                }
                // Func=null signals identity key (element itself)
                stages.Add(new PipelineStage(kind,null,comparer,currentElementType,currentElementType,keyType: currentElementType,descending: descending));
                return true;
            }
            case "SkipLast":
                if(invocation.ArgumentList.Arguments.Count!=1
                    || method.Parameters.Length==0
                    || method.Parameters[0].Type.SpecialType!=SpecialType.System_Int32) {
                    return false;
                }
                stages.Add(new PipelineStage(PipelineStageKind.SkipLast,null,services.RenderExpression(invocation.ArgumentList.Arguments[0].Expression),currentElementType,currentElementType));
                return true;
            case "TakeLast":
                if(invocation.ArgumentList.Arguments.Count!=1
                    || method.Parameters.Length==0
                    || method.Parameters[0].Type.SpecialType!=SpecialType.System_Int32) {
                    return false;
                }
                stages.Add(new PipelineStage(PipelineStageKind.TakeLast,null,services.RenderExpression(invocation.ArgumentList.Arguments[0].Expression),currentElementType,currentElementType));
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
                    stages.Add(new PipelineStage(PipelineStageKind.TakeRange,null,services.RenderExpression(invocation.ArgumentList.Arguments[0].Expression),currentElementType,currentElementType));
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
        var count = services.RenderExpression(invocation.ArgumentList.Arguments[0].Expression);
        stages.Add(new PipelineStage(kind,null,count,currentElementType,currentElementType));
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
            usesIndex: usesIndex));
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
        if(invocation.ArgumentList.Arguments.Count==1) {
            if(method.Parameters.Length<1) {
                return false;
            }
            comparer=services.RenderExpression(invocation.ArgumentList.Arguments[0].Expression);
        }
        stages.Add(new PipelineStage(PipelineStageKind.Distinct,null,comparer,currentElementType,currentElementType,keyType: currentElementType));
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
        if(invocation.ArgumentList.Arguments.Count==2) {
            if(method.Parameters.Length<2) {
                return false;
            }
            comparer=services.RenderExpression(invocation.ArgumentList.Arguments[1].Expression);
        }
        stages.Add(new PipelineStage(PipelineStageKind.DistinctBy,keySelector,comparer,currentElementType,currentElementType,keyType: keySelector.ReturnTypeName));
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
        if(invocation.ArgumentList.Arguments.Count==2) {
            if(method.Parameters.Length<2) {
                return false;
            }
            comparer=services.RenderExpression(invocation.ArgumentList.Arguments[1].Expression);
        }
        stages.Add(new PipelineStage(kind,null,comparer,currentElementType,currentElementType,secondSource: secondSource,keyType: currentElementType));
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
        if(invocation.ArgumentList.Arguments.Count==3) {
            if(method.Parameters.Length<3) {
                return false;
            }
            comparer=services.RenderExpression(invocation.ArgumentList.Arguments[2].Expression);
        }
        stages.Add(new PipelineStage(kind,keySelector,comparer,currentElementType,currentElementType,secondSource: secondSource,keyType: keySelector.ReturnTypeName));
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
        var element = services.RenderExpression(invocation.ArgumentList.Arguments[0].Expression);
        stages.Add(new PipelineStage(kind,null,element,currentElementType,currentElementType));
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
        if(invocation.ArgumentList.Arguments.Count==1) {
            defaultValue=services.RenderExpression(invocation.ArgumentList.Arguments[0].Expression);
        }
        stages.Add(new PipelineStage(PipelineStageKind.DefaultIfEmpty,null,defaultValue,currentElementType,currentElementType));
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
        if(invocation.ArgumentList.Arguments.Count==2) {
            if(method.Parameters.Length<2) {
                return false;
            }
            comparer=services.RenderExpression(invocation.ArgumentList.Arguments[1].Expression);
        }
        var descending = kind is PipelineStageKind.OrderByDescending or PipelineStageKind.ThenByDescending;
        stages.Add(new PipelineStage(kind,keySelector,comparer,currentElementType,currentElementType,keyType: keySelector.ReturnTypeName,descending: descending));
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
                comparer=services.RenderExpression(invocation.ArgumentList.Arguments[index].Expression);
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
            keyType: keySelector.ReturnTypeName));
        currentElementType=outputType;
        return true;
    }

    private static bool ValidateStageCombination(IReadOnlyList<PipelineStage> stages,PipelineTerminalKind? terminalKind) {
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
        if(orderStageCount>2) return false;
        if(orderStageCount>0 && (selectManyStageCount>0 || binarySetStageCount>0 || concatLikeStageCount>0)) return false;
        if(orderStageCount>0) {
            var orderIndexes = stages.Select(static (stage,index) => (stage,index))
                .Where(static pair => pair.stage.Kind is PipelineStageKind.OrderBy or PipelineStageKind.OrderByDescending or PipelineStageKind.ThenBy or PipelineStageKind.ThenByDescending)
                .Select(static pair => pair.index)
                .ToArray();
            if(stages[orderIndexes[0]].Kind is PipelineStageKind.ThenBy or PipelineStageKind.ThenByDescending) return false;
            if(orderIndexes.Length==2 && orderIndexes[1]!=orderIndexes[0]+1) return false;
        }
        if(stages.Count<2 && terminalKind is null) return false;
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
    bool descending = false) {
    public PipelineStageKind Kind { get; } = kind;
    public FuncFragment? Func { get; } = func;
    public FuncFragment? SecondFunc { get; } = secondFunc;
    public FuncFragment? ThirdFunc { get; } = thirdFunc;
    public string? Value { get; } = value;
    public string InputType { get; } = inputType;
    public string OutputType { get; } = outputType;
    public Enumerable? SecondSource { get; } = secondSource;
    public string? InnerElementType { get; } = innerElementType;
    public string? KeyType { get; } = keyType;
    public bool UsesIndex { get; } = usesIndex;
    public bool Descending { get; } = descending;
}

public interface DirectExecutionFragment {
    string GetDirectExecutionStatement(string actualVariableName,string actualTypeName);
}

public sealed class PipelineEnumerable(int 番号,Enumerable source,IReadOnlyList<PipelineStage> stages,string elementType):Enumerable(番号) {
    protected override Enumerable PrimarySource => source;
    public override string ElementType => elementType;
    public override string StructMethod {
        get {
            var result = string.Empty;
            foreach(var source in Funcs.Select(static f => f.SupportingMemberSources)) {
                result=JoinStaticMethods(result,source);
            }
            foreach(var source in SecondSources.Select(static source => source.StructMethod)) {
                result=JoinStaticMethods(result,source);
            }
            return result;
        }
    }
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{SecondSourcesStructs}}
        {{base.GetEnumeratorStruct}}
        """;
    protected override string StructTypeParameters {
        get {
            var secondSources = SecondSources.ToArray();
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
            var constraints = new List<string> { $"where TEnum : struct, global::System.Collections.Generic.IEnumerator<{source.ElementType}>" };
            foreach(var (second,index) in SecondSources.Select(static (second,index) => (second,index))) {
                constraints.Add($"where {SecondTypeParameterName(index)} : struct, global::System.Collections.Generic.IEnumerator<{second.ElementType}>");
            }
            return string.Join("\n    ",constraints);
        }
    }
    public override string EnumeratorTypeName {
        get {
            var secondSources = SecondSources.ToArray();
            if(secondSources.Length==0) {
                return base.EnumeratorTypeName;
            }
            var arguments = new List<string> { source.EnumeratorTypeName };
            arguments.AddRange(secondSources.Select(static second => second.EnumeratorTypeName));
            return $"{Struct}<{string.Join(", ",arguments)}>";
        }
    }
    public override string GetEnumeratorExpression {
        get {
            var args = new List<string> { source.GetEnumeratorExpression };
            foreach(var second in SecondSources) {
                args.Add(second.GetEnumeratorExpression);
            }
            foreach(var stage in CountStages()) {
                args.Add(stage.Value!);
            }
            foreach(var stage in RangeStages()) {
                args.Add(stage.Value!);
            }
            foreach(var stage in ComparerStages()) {
                args.Add(stage.Value!);
            }
            foreach(var stage in OrderComparerStages()) {
                args.Add(stage.Value!);
            }
            args.AddRange(ClosureArgsWithoutPrefix());
            return $"new {EnumeratorTypeName}({string.Join(", ",args)})";
        }
    }
    public override string GetEnumeratorPrimaryConstructorParameters {
        get {
            var parameters = new List<string> { $"TEnum {e}" };
            foreach(var (second,index) in SecondSources.Select(static (second,index) => (second,index))) {
                parameters.Add($"{SecondTypeParameterName(index)} {SecondEnumeratorName(index)}");
            }
            foreach(var (stage,index) in CountStages().Select(static (stage,index) => (stage,index))) {
                parameters.Add($"int {CountName(index)}");
            }
            foreach(var (stage,index) in RangeStages().Select(static (stage,index) => (stage,index))) {
                parameters.Add($"global::System.Range {RangeName(RangeOrdinal(index))}");
            }
            foreach(var (stage,index) in ComparerStages().Select(static (stage,index) => (stage,index))) {
                parameters.Add($"global::System.Collections.Generic.IEqualityComparer<{ComparerKeyType(stage)}>? {ComparerName(index)}");
            }
            foreach(var (stage,index) in OrderComparerStages().Select(static (stage,index) => (stage,index))) {
                parameters.Add($"global::System.Collections.Generic.IComparer<{ComparerKeyType(stage)}>? {OrderComparerName(index)}");
            }
            parameters.AddRange(ClosureParametersWithoutPrefix());
            return string.Join(", ",parameters);
        }
    }
    public override string GetEnumeratorFields {
        get {
            var builder = new System.Text.StringBuilder();
            var countIndex = 0;
            for(var index = 0; index<stages.Count; index++) {
                var stage = stages[index];
                if(stage.Kind is PipelineStageKind.Where or PipelineStageKind.Select && stage.UsesIndex) {
                    builder.AppendLine($"private int {StateName(index)} = -1;");
                } else if(stage.Kind==PipelineStageKind.Take || stage.Kind==PipelineStageKind.Skip) {
                    var countName = CountName(countIndex++);
                    builder.AppendLine($"private readonly int {countName}Value = global::System.Math.Max(0,{countName});");
                    builder.AppendLine($"private int {StateName(index)} = 0;");
                } else if(stage.Kind==PipelineStageKind.SelectMany) {
                    builder.AppendLine($"private global::System.Collections.Generic.IEnumerator<{stage.InnerElementType}>? {InnerEnumeratorName(index)} = null;");
                    builder.AppendLine($"private {stage.InputType} {OuterName(index)} = default!;");
                    if(stage.UsesIndex) {
                        builder.AppendLine($"private int {StateName(index)} = -1;");
                    }
                } else if(stage.Kind is PipelineStageKind.Distinct or PipelineStageKind.DistinctBy) {
                    builder.AppendLine($"private readonly global::System.Collections.Generic.HashSet<{ComparerKeyType(stage)}> {SetName(index)} = new({ComparerName(ComparerOrdinal(index))});");
                } else if(stage.Kind is PipelineStageKind.Union or PipelineStageKind.UnionBy) {
                    builder.AppendLine($"private readonly global::System.Collections.Generic.HashSet<{ComparerKeyType(stage)}> {SetName(index)} = new({ComparerName(ComparerOrdinal(index))});");
                    builder.AppendLine($"private bool {StateName(index)} = false;");
                } else if(stage.Kind is PipelineStageKind.Except or PipelineStageKind.Intersect or PipelineStageKind.ExceptBy or PipelineStageKind.IntersectBy) {
                    builder.AppendLine($"private readonly global::System.Collections.Generic.HashSet<{ComparerKeyType(stage)}> {SecondSetName(index)} = new({ComparerName(ComparerOrdinal(index))});");
                    builder.AppendLine($"private readonly global::System.Collections.Generic.HashSet<{ComparerKeyType(stage)}> {SetName(index)} = new({ComparerName(ComparerOrdinal(index))});");
                    builder.AppendLine($"private bool {StateName(index)} = false;");
                } else if(stage.Kind is PipelineStageKind.Concat or PipelineStageKind.Append or PipelineStageKind.Prepend) {
                    builder.AppendLine($"private int {StateName(index)} = 0;");
                } else if(stage.Kind is PipelineStageKind.OrderBy or PipelineStageKind.OrderByDescending) {
                    builder.AppendLine(OrderSortItemType(index));
                    builder.AppendLine($"private global::System.Collections.Generic.List<{SortItemName(index)}>? {SortedItemsName(index)} = null;");
                    builder.AppendLine($"private int {StateName(index)} = 0;");
                } else if(stage.Kind==PipelineStageKind.GroupBy) {
                    builder.AppendLine(GroupingType(index,stage));
                    builder.AppendLine($"private global::System.Collections.Generic.List<{GroupValueType(index,stage)}>? {GroupItemsName(index)} = null;");
                    builder.AppendLine($"private int {StateName(index)} = 0;");
                } else if(stage.Kind==PipelineStageKind.Reverse) {
                    builder.AppendLine($"private global::System.Collections.Generic.List<{stage.OutputType}>? {ReverseItemsName(index)} = null;");
                    builder.AppendLine($"private int {StateName(index)} = 0;");
                } else if(stage.Kind==PipelineStageKind.DefaultIfEmpty) {
                    builder.AppendLine($"private bool {StateName(index)} = false;");
                } else if(stage.Kind==PipelineStageKind.TakeWhile) {
                    builder.AppendLine($"private bool {StateName(index)} = false;");
                    if(stage.UsesIndex) {
                        builder.AppendLine($"private int {IndexName(index)} = -1;");
                    }
                } else if(stage.Kind==PipelineStageKind.SkipWhile) {
                    builder.AppendLine($"private bool {StateName(index)} = true;");
                    if(stage.UsesIndex) {
                        builder.AppendLine($"private int {IndexName(index)} = -1;");
                    }
                } else if(stage.Kind==PipelineStageKind.SkipLast) {
                    builder.AppendLine($"private global::System.Collections.Generic.Queue<{stage.OutputType}> {QueueName(index)} = new();");
                } else if(stage.Kind==PipelineStageKind.TakeLast) {
                    builder.AppendLine($"private global::System.Collections.Generic.List<{stage.OutputType}>? {ReverseItemsName(index)} = null;");
                    builder.AppendLine($"private int {StateName(index)} = 0;");
                } else if(stage.Kind==PipelineStageKind.TakeRange) {
                    builder.AppendLine($"private global::System.Collections.Generic.List<{stage.OutputType}>? {ReverseItemsName(index)} = null;");
                    builder.AppendLine($"private int {StateName(index)} = 0;");
                    builder.AppendLine($"private int {StateEndName(index)} = 0;");
                }
            }
            return builder.ToString().TrimEnd();
        }
    }
    public override string GetTryGetNextBody => BuildLoop(e,"current = this.Current = {0};\nreturn true;","current = default!;\nreturn false;");
    public override string GetCurrentExpression => currentValue;
    protected override string GetEnumeratorDisposeBody {
        get {
            var builder = new System.Text.StringBuilder();
            for(var index = 0; index<stages.Count; index++) {
                if(stages[index].Kind==PipelineStageKind.SelectMany) {
                    builder.AppendLine($"if(this.{InnerEnumeratorName(index)} is not null) {{");
                    builder.AppendLine($"    this.{InnerEnumeratorName(index)}.Dispose();");
                    builder.AppendLine("}");
                }
            }
            foreach(var index in SecondSources.Select(static (_,index) => index)) {
                builder.AppendLine($"{SecondEnumeratorName(index)}.Dispose();");
            }
            builder.AppendLine($"{e}.Dispose();");
            return builder.ToString().TrimEnd();
        }
    }

    internal string BuildDirectLoop(string enumeratorName,string successStatement,string endStatement) => BuildLoop(enumeratorName,successStatement,endStatement);

    private IEnumerable<FuncFragment> Funcs => stages.SelectMany(static stage => new[] { stage.Func, stage.SecondFunc, stage.ThirdFunc }).Where(static func => func is not null).Cast<FuncFragment>();
    private IEnumerable<Enumerable> SecondSources => stages.Select(static stage => stage.SecondSource).Where(static source => source is not null).Cast<Enumerable>();
    private string SecondSourcesStructs {
        get {
            var result = string.Empty;
            foreach(var source in SecondSources.Select(static source => source.GetEnumeratorStruct)) {
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
            return BuildBinarySetLoop(enumeratorName,successStatement,endStatement,binarySetIndex[0]);
        }
        if(binarySetIndex.Length>1) {
            throw new NotSupportedException("Multiple binary set stages are not supported in PipelineEnumerable.");
        }
        var selectManyIndex = stages.Select(static (stage,index) => (stage,index)).Where(static pair => pair.stage.Kind==PipelineStageKind.SelectMany).Select(static pair => pair.index).ToArray();
        if(selectManyIndex.Length==1) {
            return BuildSelectManyLoop(enumeratorName,successStatement,endStatement,selectManyIndex[0]);
        }
        var groupByIndex = stages.Select(static (stage,index) => (stage,index))
            .Where(static pair => pair.stage.Kind==PipelineStageKind.GroupBy)
            .Select(static pair => pair.index)
            .ToArray();
        if(groupByIndex.Length==1) {
            return BuildGroupByLoop(enumeratorName,successStatement,endStatement,groupByIndex[0]);
        }
        if(groupByIndex.Length>1) {
            throw new NotSupportedException("Multiple GroupBy stages are not supported in PipelineEnumerable.");
        }
        var reverseIndex = stages.Select(static (stage,index) => (stage,index))
            .Where(static pair => pair.stage.Kind==PipelineStageKind.Reverse)
            .Select(static pair => pair.index)
            .ToArray();
        if(reverseIndex.Length==1) {
            return BuildReverseLoop(enumeratorName,successStatement,endStatement,reverseIndex[0]);
        }
        if(reverseIndex.Length>1) {
            throw new NotSupportedException("Multiple Reverse stages are not supported in PipelineEnumerable.");
        }
        var defaultIfEmptyIndex = stages.Select(static (stage,index) => (stage,index))
            .Where(static pair => pair.stage.Kind==PipelineStageKind.DefaultIfEmpty)
            .Select(static pair => pair.index)
            .ToArray();
        if(defaultIfEmptyIndex.Length==1) {
            return BuildDefaultIfEmptyLoop(enumeratorName,successStatement,endStatement,defaultIfEmptyIndex[0]);
        }
        if(defaultIfEmptyIndex.Length>1) {
            throw new NotSupportedException("Multiple DefaultIfEmpty stages are not supported in PipelineEnumerable.");
        }
        var orderIndex = stages.Select(static (stage,index) => (stage,index))
            .Where(static pair => pair.stage.Kind is PipelineStageKind.OrderBy or PipelineStageKind.OrderByDescending)
            .Select(static pair => pair.index)
            .ToArray();
        if(orderIndex.Length==1) {
            return BuildOrderLoop(enumeratorName,successStatement,endStatement,orderIndex[0]);
        }
        if(orderIndex.Length>1) {
            throw new NotSupportedException("Multiple primary ordering stages are not supported in PipelineEnumerable.");
        }
        var skipLastIndex = stages.Select(static (stage,index) => (stage,index))
            .Where(static pair => pair.stage.Kind==PipelineStageKind.SkipLast)
            .Select(static pair => pair.index).ToArray();
        if(skipLastIndex.Length==1) {
            return BuildSkipLastLoop(enumeratorName,successStatement,endStatement,skipLastIndex[0]);
        }
        var takeLastIndex = stages.Select(static (stage,index) => (stage,index))
            .Where(static pair => pair.stage.Kind==PipelineStageKind.TakeLast)
            .Select(static pair => pair.index).ToArray();
        if(takeLastIndex.Length==1) {
            return BuildTakeLastLoop(enumeratorName,successStatement,endStatement,takeLastIndex[0]);
        }
        var takeRangeIndex = stages.Select(static (stage,index) => (stage,index))
            .Where(static pair => pair.stage.Kind==PipelineStageKind.TakeRange)
            .Select(static pair => pair.index).ToArray();
        if(takeRangeIndex.Length==1) {
            return BuildTakeRangeLoop(enumeratorName,successStatement,endStatement,takeRangeIndex[0]);
        }
        var concatLikeIndex = stages.Select(static (stage,index) => (stage,index))
            .Where(static pair => pair.stage.Kind is PipelineStageKind.Concat or PipelineStageKind.Append or PipelineStageKind.Prepend)
            .Select(static pair => pair.index)
            .ToArray();
        if(concatLikeIndex.Length==1) {
            return BuildConcatLikeLoop(enumeratorName,successStatement,endStatement,concatLikeIndex[0]);
        }
        if(concatLikeIndex.Length>1) {
            throw new NotSupportedException("Multiple concat-like stages are not supported in PipelineEnumerable.");
        }

        var builder = new System.Text.StringBuilder();
        builder.AppendLine($"while({enumeratorName}.MoveNext()) {{");
        builder.AppendLine($"    var {ItemName(0)}={enumeratorName}.Current;");
        var currentName = ItemName(0);
        for(var index = 0; index<stages.Count; index++) {
            var stage = stages[index];
            switch(stage.Kind) {
                case PipelineStageKind.Where:
                    AppendWhere(builder,stage,currentName,index);
                    break;
                case PipelineStageKind.Select:
                    currentName = AppendSelect(builder,stage,currentName,index);
                    break;
                case PipelineStageKind.SelectMany:
                    throw new NotSupportedException("Multiple SelectMany stages are not supported in PipelineEnumerable.");
                case PipelineStageKind.Distinct:
                    AppendDistinct(builder,stage,currentName,index);
                    break;
                case PipelineStageKind.DistinctBy:
                    AppendDistinctBy(builder,stage,currentName,index);
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
                    AppendTake(builder,index,CountOrdinal(index));
                    break;
                case PipelineStageKind.Skip:
                    AppendSkip(builder,index,CountOrdinal(index));
                    break;
                case PipelineStageKind.TakeWhile:
                    AppendTakeWhile(builder,stage,currentName,index);
                    break;
                case PipelineStageKind.SkipWhile:
                    AppendSkipWhile(builder,stage,currentName,index);
                    break;
                case PipelineStageKind.Cast:
                    currentName = AppendCast(builder,stage,currentName,index);
                    break;
                case PipelineStageKind.OfType:
                    currentName = AppendOfType(builder,stage,currentName,index);
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
        var builder = new System.Text.StringBuilder();
        var endLabel = EndLabel(concatIndex);
        var needsEndLabel = StagesAfterNeedEndLabel(concatIndex);
        var breakStatement = "break;";
        if(needsEndLabel) {
            breakStatement=$"goto {endLabel};";
        }
        builder.AppendLine("while(true) {");
        if(stage.Kind==PipelineStageKind.Prepend) {
            builder.AppendLine($"    if(this.{StateName(concatIndex)}==0) {{");
            builder.AppendLine($"        this.{StateName(concatIndex)}=1;");
            var prependName = ItemName(concatIndex+1);
            builder.AppendLine($"        var {prependName}=({stage.OutputType})({stage.Value});");
            var currentName = prependName;
            for(var index = concatIndex+1; index<stages.Count; index++) {
                currentName=AppendStage(builder,stages[index],currentName,index,breakStatement);
            }
            builder.AppendLine(IndentBlock(string.Format(successStatement,currentName),"        "));
            builder.AppendLine("    }");
        }
        builder.AppendLine($"    if(this.{StateName(concatIndex)}<=1) {{");
        builder.AppendLine($"        while({enumeratorName}.MoveNext()) {{");
        builder.AppendLine($"            var {ItemName(0)}={enumeratorName}.Current;");
        var sourceCurrentName = ItemName(0);
        for(var index = 0; index<concatIndex; index++) {
            sourceCurrentName=AppendStage(builder,stages[index],sourceCurrentName,index);
        }
        for(var index = concatIndex+1; index<stages.Count; index++) {
            sourceCurrentName=AppendStage(builder,stages[index],sourceCurrentName,index,breakStatement);
        }
        builder.AppendLine(IndentBlock(string.Format(successStatement,sourceCurrentName),"            "));
        builder.AppendLine("        }");
        builder.AppendLine($"        this.{StateName(concatIndex)}=2;");
        builder.AppendLine("    }");
        if(stage.Kind==PipelineStageKind.Concat) {
            builder.AppendLine($"    while({SecondEnumeratorName(SecondOrdinal(concatIndex))}.MoveNext()) {{");
            var secondName = ItemName(concatIndex+1);
            builder.AppendLine($"        var {secondName}=({stage.OutputType})(object?){SecondEnumeratorName(SecondOrdinal(concatIndex))}.Current!;");
            var currentName = secondName;
            for(var index = concatIndex+1; index<stages.Count; index++) {
                currentName=AppendStage(builder,stages[index],currentName,index,breakStatement);
            }
            builder.AppendLine(IndentBlock(string.Format(successStatement,currentName),"        "));
            builder.AppendLine("    }");
            builder.AppendLine($"    this.{StateName(concatIndex)}=3;");
        } else if(stage.Kind==PipelineStageKind.Append) {
            builder.AppendLine($"    if(this.{StateName(concatIndex)}==2) {{");
            builder.AppendLine($"        this.{StateName(concatIndex)}=3;");
            var appendName = ItemName(concatIndex+1);
            builder.AppendLine($"        var {appendName}=({stage.OutputType})({stage.Value});");
            var currentName = appendName;
            for(var index = concatIndex+1; index<stages.Count; index++) {
                currentName=AppendStage(builder,stages[index],currentName,index,breakStatement);
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
        var orderStages = OrderStages(orderIndex).ToArray();
        var primaryOrder = orderStages[0];
        var secondaryOrder = orderStages.Length==2 ? orderStages[1] : null;
        var lastOrderIndex = orderIndex+orderStages.Length-1;
        var builder = new System.Text.StringBuilder();
        builder.AppendLine($"if(this.{SortedItemsName(orderIndex)} is null) {{");
        builder.AppendLine($"    var {SortedItemsName(orderIndex)}=new global::System.Collections.Generic.List<{SortItemName(orderIndex)}>();");
        builder.AppendLine("    var __sortIndex=0;");
        builder.AppendLine($"    while({enumeratorName}.MoveNext()) {{");
        builder.AppendLine($"        var {ItemName(0)}={enumeratorName}.Current;");
        var currentName = ItemName(0);
        for(var index = 0; index<orderIndex; index++) {
            currentName=AppendStage(builder,stages[index],currentName,index);
        }
        var primaryKeyName = KeyName(orderIndex);
        builder.AppendLine($"        {ComparerKeyType(primaryOrder)} {primaryKeyName}=default!;");
        if(primaryOrder.Func is null) {
            builder.AppendLine($"        {primaryKeyName}=({ComparerKeyType(primaryOrder)})(object?){currentName}!;");
        } else {
            builder.AppendLine(IndentBlock(FuncAssign(primaryOrder.Func,primaryOrder.Kind.ToString(),primaryKeyName,currentName),"        "));
        }
        if(secondaryOrder is { }) {
            var secondaryKeyName = KeyName(orderIndex+1);
            builder.AppendLine($"        {ComparerKeyType(secondaryOrder)} {secondaryKeyName}=default!;");
            if(secondaryOrder.Func is null) {
                builder.AppendLine($"        {secondaryKeyName}=({ComparerKeyType(secondaryOrder)})(object?){currentName}!;");
            } else {
                builder.AppendLine(IndentBlock(FuncAssign(secondaryOrder.Func,secondaryOrder.Kind.ToString(),secondaryKeyName,currentName),"        "));
            }
            builder.AppendLine($"        {SortedItemsName(orderIndex)}.Add(new {SortItemName(orderIndex)}({currentName},{primaryKeyName},{secondaryKeyName},__sortIndex++));");
        } else {
            builder.AppendLine($"        {SortedItemsName(orderIndex)}.Add(new {SortItemName(orderIndex)}({currentName},{primaryKeyName},__sortIndex++));");
        }
        builder.AppendLine("    }");
        var primaryComparerName = OrderComparerLocalName(orderIndex);
        builder.AppendLine($"    var {primaryComparerName}={OrderComparerName(OrderComparerOrdinal(orderIndex))} ?? global::System.Collections.Generic.Comparer<{ComparerKeyType(primaryOrder)}>.Default;");
        string? secondaryComparerName = null;
        if(secondaryOrder is { }) {
            secondaryComparerName=OrderComparerLocalName(orderIndex+1);
            builder.AppendLine($"    var {secondaryComparerName}={OrderComparerName(OrderComparerOrdinal(orderIndex+1))} ?? global::System.Collections.Generic.Comparer<{ComparerKeyType(secondaryOrder)}>.Default;");
        }
        builder.AppendLine($"    {SortedItemsName(orderIndex)}.Sort((left,right) => {{");
        builder.AppendLine($"        var compare={primaryComparerName}.Compare(left.Key,right.Key);");
        builder.AppendLine($"        if(compare!=0) return {OrderCompareExpression(primaryOrder,"compare")};");
        if(secondaryOrder is { }) {
            builder.AppendLine($"        var compare2={secondaryComparerName}.Compare(left.SecondKey,right.SecondKey);");
            builder.AppendLine($"        if(compare2!=0) return {OrderCompareExpression(secondaryOrder,"compare2")};");
        }
        builder.AppendLine("        return left.Index.CompareTo(right.Index);");
        builder.AppendLine("    });");
        builder.AppendLine($"    this.{SortedItemsName(orderIndex)}={SortedItemsName(orderIndex)};");
        builder.AppendLine($"    this.{StateName(orderIndex)}=0;");
        builder.AppendLine("}");
        builder.AppendLine($"while(this.{StateName(orderIndex)}<this.{SortedItemsName(orderIndex)}.Count) {{");
        var sortedCurrentName = ItemName(lastOrderIndex+1);
        builder.AppendLine($"    var {sortedCurrentName}=this.{SortedItemsName(orderIndex)}[this.{StateName(orderIndex)}++].Value;");
        currentName=sortedCurrentName;
        for(var index = lastOrderIndex+1; index<stages.Count; index++) {
            currentName=AppendStage(builder,stages[index],currentName,index);
        }
        builder.AppendLine(IndentBlock(string.Format(successStatement,currentName),"    "));
        builder.AppendLine("}");
        builder.AppendLine(endStatement);
        return builder.ToString().TrimEnd();
    }

    private string BuildGroupByLoop(string enumeratorName,string successStatement,string endStatement,int groupByIndex) {
        var stage = stages[groupByIndex];
        var builder = new System.Text.StringBuilder();
        builder.AppendLine($"if(this.{GroupItemsName(groupByIndex)} is null) {{");
        builder.AppendLine($"    var {GroupItemsName(groupByIndex)}=new global::System.Collections.Generic.List<{GroupValueType(groupByIndex,stage)}>();");
        builder.AppendLine($"    var __groupMap=new global::System.Collections.Generic.Dictionary<{ComparerKeyType(stage)},{GroupingName(groupByIndex)}>({ComparerName(ComparerOrdinal(groupByIndex))});");
        builder.AppendLine($"    var __groupOrder=new global::System.Collections.Generic.List<{GroupingName(groupByIndex)}>();");
        builder.AppendLine($"    while({enumeratorName}.MoveNext()) {{");
        builder.AppendLine($"        var {ItemName(0)}={enumeratorName}.Current;");
        var currentName = ItemName(0);
        for(var index = 0; index<groupByIndex; index++) {
            currentName=AppendStage(builder,stages[index],currentName,index);
        }
        var keyName = KeyName(groupByIndex);
        builder.AppendLine($"        {ComparerKeyType(stage)} {keyName}=default!;");
        builder.AppendLine(IndentBlock(FuncAssign(stage.Func!,nameof(PipelineStageKind.GroupBy),keyName,currentName),"        "));
        var groupElementName = ItemName(groupByIndex+1);
        builder.AppendLine($"        {stage.InnerElementType} {groupElementName}=default!;");
        if(stage.SecondFunc is { } elementSelector) {
            builder.AppendLine(IndentBlock(FuncAssign(elementSelector,nameof(PipelineStageKind.GroupBy),groupElementName,currentName),"        "));
        } else {
            builder.AppendLine($"        {groupElementName}=({stage.InnerElementType})(object?){currentName}!;");
        }
        builder.AppendLine($"        if(!__groupMap.TryGetValue({keyName},out var {GroupName(groupByIndex)})) {{");
        builder.AppendLine($"            {GroupName(groupByIndex)}=new {GroupingName(groupByIndex)}({keyName});");
        builder.AppendLine($"            __groupMap.Add({keyName},{GroupName(groupByIndex)});");
        builder.AppendLine($"            __groupOrder.Add({GroupName(groupByIndex)});");
        if(stage.ThirdFunc is null) {
            builder.AppendLine($"            {GroupItemsName(groupByIndex)}.Add({GroupName(groupByIndex)});");
        }
        builder.AppendLine("        }");
        builder.AppendLine($"        {GroupName(groupByIndex)}.Add({groupElementName});");
        builder.AppendLine("    }");
        if(stage.ThirdFunc is { } resultSelector) {
            builder.AppendLine($"    foreach(var {GroupName(groupByIndex)} in __groupOrder) {{");
            var resultName = ItemName(groupByIndex+2);
            builder.AppendLine($"        {stage.OutputType} {resultName}=default!;");
            builder.AppendLine(IndentBlock(FuncAssign(resultSelector,nameof(PipelineStageKind.GroupBy),resultName,$"{GroupName(groupByIndex)}.Key",$"(global::System.Collections.Generic.IEnumerable<{stage.InnerElementType}>){GroupName(groupByIndex)}"),"        "));
            builder.AppendLine($"        {GroupItemsName(groupByIndex)}.Add({resultName});");
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
            var primaryCmpLocal=OrderComparerLocalName(orderAfterGroup);
            builder.AppendLine($"    var {primaryCmpLocal}={OrderComparerName(OrderComparerOrdinal(orderAfterGroup))} ?? global::System.Collections.Generic.Comparer<{ComparerKeyType(primaryOrder)}>.Default;");
            if(thenByIdx.HasValue) {
                var thenStage=stages[thenByIdx.Value];
                var secondaryCmpLocal=OrderComparerLocalName(thenByIdx.Value);
                builder.AppendLine($"    var {secondaryCmpLocal}={OrderComparerName(OrderComparerOrdinal(thenByIdx.Value))} ?? global::System.Collections.Generic.Comparer<{ComparerKeyType(thenStage)}>.Default;");
            }
            builder.AppendLine($"    {GroupItemsName(groupByIndex)}.Sort((left,right) => {{");
            var leftPrimKey=$"__lpk{orderAfterGroup}";
            var rightPrimKey=$"__rpk{orderAfterGroup}";
            builder.AppendLine($"        {ComparerKeyType(primaryOrder)} {leftPrimKey}=default!;");
            if(primaryOrder.Func is null) {
                builder.AppendLine($"        {leftPrimKey}=({ComparerKeyType(primaryOrder)})(object?)left!;");
            } else {
                builder.AppendLine(IndentBlock(FuncAssign(primaryOrder.Func,primaryOrder.Kind.ToString(),leftPrimKey,"left"),"        "));
            }
            builder.AppendLine($"        {ComparerKeyType(primaryOrder)} {rightPrimKey}=default!;");
            if(primaryOrder.Func is null) {
                builder.AppendLine($"        {rightPrimKey}=({ComparerKeyType(primaryOrder)})(object?)right!;");
            } else {
                builder.AppendLine(IndentBlock(FuncAssign(primaryOrder.Func,primaryOrder.Kind.ToString(),rightPrimKey,"right"),"        "));
            }
            builder.AppendLine($"        var __gcmp={primaryCmpLocal}.Compare({leftPrimKey},{rightPrimKey});");
            builder.AppendLine($"        if(__gcmp!=0) return {OrderCompareExpression(primaryOrder,"__gcmp")};");
            if(thenByIdx.HasValue) {
                var thenStage=stages[thenByIdx.Value];
                var secondaryCmpLocal=OrderComparerLocalName(thenByIdx.Value);
                var leftSecKey=$"__lsk{thenByIdx.Value}";
                var rightSecKey=$"__rsk{thenByIdx.Value}";
                builder.AppendLine($"        {ComparerKeyType(thenStage)} {leftSecKey}=default!;");
                builder.AppendLine(IndentBlock(FuncAssign(thenStage.Func!,thenStage.Kind.ToString(),leftSecKey,"left"),"        "));
                builder.AppendLine($"        {ComparerKeyType(thenStage)} {rightSecKey}=default!;");
                builder.AppendLine(IndentBlock(FuncAssign(thenStage.Func!,thenStage.Kind.ToString(),rightSecKey,"right"),"        "));
                builder.AppendLine($"        var __gcmp2={secondaryCmpLocal}.Compare({leftSecKey},{rightSecKey});");
                builder.AppendLine($"        if(__gcmp2!=0) return {OrderCompareExpression(thenStage,"__gcmp2")};");
            }
            builder.AppendLine("        return 0;");
            builder.AppendLine("    });");
        }
        builder.AppendLine($"    this.{GroupItemsName(groupByIndex)}={GroupItemsName(groupByIndex)};");
        builder.AppendLine($"    this.{StateName(groupByIndex)}=0;");
        builder.AppendLine("}");
        builder.AppendLine($"while(this.{StateName(groupByIndex)}<this.{GroupItemsName(groupByIndex)}.Count) {{");
        // stages.Count+1 は任意の ItemName(index+1) より大きい値なので OrderBy が間にある場合でも名前衝突しない
        currentName=ItemName(stages.Count+1);
        builder.AppendLine($"    var {currentName}=this.{GroupItemsName(groupByIndex)}[this.{StateName(groupByIndex)}++];");
        for(var index = groupByIndex+1; index<stages.Count; index++) {
            currentName=AppendStage(builder,stages[index],currentName,index);
        }
        builder.AppendLine(IndentBlock(string.Format(successStatement,currentName),"    "));
        builder.AppendLine("}");
        builder.AppendLine(endStatement);
        return builder.ToString().TrimEnd();
    }

    private string BuildReverseLoop(string enumeratorName,string successStatement,string endStatement,int reverseIndex) {
        var stage = stages[reverseIndex];
        var builder = new System.Text.StringBuilder();
        builder.AppendLine($"if(this.{ReverseItemsName(reverseIndex)} is null) {{");
        builder.AppendLine($"    var {ReverseItemsName(reverseIndex)}=new global::System.Collections.Generic.List<{stage.OutputType}>();");
        builder.AppendLine($"    while({enumeratorName}.MoveNext()) {{");
        builder.AppendLine($"        var {ItemName(0)}={enumeratorName}.Current;");
        var currentName = ItemName(0);
        for(var index = 0; index<reverseIndex; index++) {
            currentName=AppendStage(builder,stages[index],currentName,index);
        }
        builder.AppendLine($"        {ReverseItemsName(reverseIndex)}.Add({currentName});");
        builder.AppendLine("    }");
        builder.AppendLine($"    {ReverseItemsName(reverseIndex)}.Reverse();");
        builder.AppendLine($"    this.{ReverseItemsName(reverseIndex)}={ReverseItemsName(reverseIndex)};");
        builder.AppendLine($"    this.{StateName(reverseIndex)}=0;");
        builder.AppendLine("}");
        builder.AppendLine($"while(this.{StateName(reverseIndex)}<this.{ReverseItemsName(reverseIndex)}.Count) {{");
        currentName=ItemName(reverseIndex+1);
        builder.AppendLine($"    var {currentName}=this.{ReverseItemsName(reverseIndex)}[this.{StateName(reverseIndex)}++];");
        for(var index = reverseIndex+1; index<stages.Count; index++) {
            currentName=AppendStage(builder,stages[index],currentName,index);
        }
        builder.AppendLine(IndentBlock(string.Format(successStatement,currentName),"    "));
        builder.AppendLine("}");
        builder.AppendLine(endStatement);
        return builder.ToString().TrimEnd();
    }

    private string BuildSkipLastLoop(string enumeratorName,string successStatement,string endStatement,int skipLastIndex) {
        var builder = new System.Text.StringBuilder();
        var countOrd = CountOrdinal(skipLastIndex);
        builder.AppendLine($"while({enumeratorName}.MoveNext()) {{");
        builder.AppendLine($"    var {ItemName(0)}={enumeratorName}.Current;");
        var currentName = ItemName(0);
        for(var index = 0; index<skipLastIndex; index++) {
            currentName=AppendStage(builder,stages[index],currentName,index);
        }
        builder.AppendLine($"    this.{QueueName(skipLastIndex)}.Enqueue({currentName});");
        builder.AppendLine($"    if(this.{QueueName(skipLastIndex)}.Count > global::System.Math.Max(0,{CountName(countOrd)})) {{");
        var dequeueItem = ItemName(skipLastIndex+1);
        builder.AppendLine($"        var {dequeueItem}=this.{QueueName(skipLastIndex)}.Dequeue();");
        currentName=dequeueItem;
        for(var index = skipLastIndex+1; index<stages.Count; index++) {
            currentName=AppendStage(builder,stages[index],currentName,index);
        }
        builder.AppendLine(IndentBlock(string.Format(successStatement,currentName),"        "));
        builder.AppendLine("    }");
        builder.AppendLine("}");
        builder.AppendLine(endStatement);
        return builder.ToString().TrimEnd();
    }

    private string BuildTakeLastLoop(string enumeratorName,string successStatement,string endStatement,int takeLastIndex) {
        var stage = stages[takeLastIndex];
        var countOrd = CountOrdinal(takeLastIndex);
        var builder = new System.Text.StringBuilder();
        builder.AppendLine($"if(this.{ReverseItemsName(takeLastIndex)} is null) {{");
        builder.AppendLine($"    var __tmpList=new global::System.Collections.Generic.List<{stage.OutputType}>();");
        builder.AppendLine($"    while({enumeratorName}.MoveNext()) {{");
        builder.AppendLine($"        var {ItemName(0)}={enumeratorName}.Current;");
        var currentName = ItemName(0);
        for(var index = 0; index<takeLastIndex; index++) {
            currentName=AppendStage(builder,stages[index],currentName,index);
        }
        builder.AppendLine($"        __tmpList.Add({currentName});");
        builder.AppendLine("    }");
        builder.AppendLine($"    var __takeN=global::System.Math.Max(0,global::System.Math.Min(global::System.Math.Max(0,{CountName(countOrd)}),__tmpList.Count));");
        builder.AppendLine($"    var __takeStart=__tmpList.Count-__takeN;");
        builder.AppendLine($"    this.{ReverseItemsName(takeLastIndex)}=__tmpList;");
        builder.AppendLine($"    this.{StateName(takeLastIndex)}=__takeStart;");
        builder.AppendLine("}");
        builder.AppendLine($"while(this.{StateName(takeLastIndex)}<this.{ReverseItemsName(takeLastIndex)}.Count) {{");
        currentName=ItemName(takeLastIndex+1);
        builder.AppendLine($"    var {currentName}=this.{ReverseItemsName(takeLastIndex)}[this.{StateName(takeLastIndex)}++];");
        for(var index = takeLastIndex+1; index<stages.Count; index++) {
            currentName=AppendStage(builder,stages[index],currentName,index);
        }
        builder.AppendLine(IndentBlock(string.Format(successStatement,currentName),"    "));
        builder.AppendLine("}");
        builder.AppendLine(endStatement);
        return builder.ToString().TrimEnd();
    }

    private string BuildTakeRangeLoop(string enumeratorName,string successStatement,string endStatement,int takeRangeIndex) {
        var stage = stages[takeRangeIndex];
        var rangeParamName = RangeName(RangeOrdinal(takeRangeIndex));
        var builder = new System.Text.StringBuilder();
        builder.AppendLine($"if(this.{ReverseItemsName(takeRangeIndex)} is null) {{");
        builder.AppendLine($"    var __tmpRange=new global::System.Collections.Generic.List<{stage.OutputType}>();");
        builder.AppendLine($"    while({enumeratorName}.MoveNext()) {{");
        builder.AppendLine($"        var {ItemName(0)}={enumeratorName}.Current;");
        var currentName = ItemName(0);
        for(var index = 0; index<takeRangeIndex; index++) {
            currentName=AppendStage(builder,stages[index],currentName,index);
        }
        builder.AppendLine($"        __tmpRange.Add({currentName});");
        builder.AppendLine("    }");
        builder.AppendLine($"    var (__rangeOff,__rangeLen)={rangeParamName}.GetOffsetAndLength(__tmpRange.Count);");
        builder.AppendLine($"    this.{ReverseItemsName(takeRangeIndex)}=__tmpRange;");
        builder.AppendLine($"    this.{StateName(takeRangeIndex)}=__rangeOff;");
        builder.AppendLine($"    this.{StateEndName(takeRangeIndex)}=__rangeOff+__rangeLen;");
        builder.AppendLine("}");
        builder.AppendLine($"while(this.{StateName(takeRangeIndex)}<this.{StateEndName(takeRangeIndex)}) {{");
        currentName=ItemName(takeRangeIndex+1);
        builder.AppendLine($"    var {currentName}=this.{ReverseItemsName(takeRangeIndex)}[this.{StateName(takeRangeIndex)}++];");
        for(var index = takeRangeIndex+1; index<stages.Count; index++) {
            currentName=AppendStage(builder,stages[index],currentName,index);
        }
        builder.AppendLine(IndentBlock(string.Format(successStatement,currentName),"    "));
        builder.AppendLine("}");
        builder.AppendLine(endStatement);
        return builder.ToString().TrimEnd();
    }

    private string BuildDefaultIfEmptyLoop(string enumeratorName,string successStatement,string endStatement,int defaultIfEmptyIndex) {
        var stage = stages[defaultIfEmptyIndex];
        var builder = new System.Text.StringBuilder();
        builder.AppendLine($"while({enumeratorName}.MoveNext()) {{");
        builder.AppendLine($"    var {ItemName(0)}={enumeratorName}.Current;");
        var currentName = ItemName(0);
        for(var index = 0; index<defaultIfEmptyIndex; index++) {
            currentName=AppendStage(builder,stages[index],currentName,index);
        }
        builder.AppendLine($"    this.{StateName(defaultIfEmptyIndex)}=true;");
        for(var index = defaultIfEmptyIndex+1; index<stages.Count; index++) {
            currentName=AppendStage(builder,stages[index],currentName,index);
        }
        builder.AppendLine(IndentBlock(string.Format(successStatement,currentName),"    "));
        builder.AppendLine("}");
        builder.AppendLine($"while(!this.{StateName(defaultIfEmptyIndex)}) {{");
        builder.AppendLine($"    this.{StateName(defaultIfEmptyIndex)}=true;");
        currentName=ItemName(defaultIfEmptyIndex+1);
        builder.AppendLine($"    {stage.OutputType} {currentName}={stage.Value};");
        for(var index = defaultIfEmptyIndex+1; index<stages.Count; index++) {
            currentName=AppendStage(builder,stages[index],currentName,index);
        }
        builder.AppendLine(IndentBlock(string.Format(successStatement,currentName),"    "));
        builder.AppendLine("}");
        builder.AppendLine(endStatement);
        return builder.ToString().TrimEnd();
    }

    private string BuildBinarySetLoop(string enumeratorName,string successStatement,string endStatement,int setIndex) {
        var stage = stages[setIndex];
        if(stage.Kind is PipelineStageKind.Union or PipelineStageKind.UnionBy) {
            return BuildUnionLoop(enumeratorName,successStatement,endStatement,setIndex);
        }

        var builder = new System.Text.StringBuilder();
        builder.AppendLine($"if(!this.{StateName(setIndex)}) {{");
        builder.AppendLine($"    while({SecondEnumeratorName(SecondOrdinal(setIndex))}.MoveNext()) {{");
        builder.AppendLine($"        this.{SecondSetName(setIndex)}.Add(({ComparerKeyType(stage)})(object?){SecondEnumeratorName(SecondOrdinal(setIndex))}.Current!);");
        builder.AppendLine("    }");
        builder.AppendLine($"    this.{StateName(setIndex)}=true;");
        builder.AppendLine("}");
        builder.AppendLine($"while({enumeratorName}.MoveNext()) {{");
        builder.AppendLine($"    var {ItemName(0)}={enumeratorName}.Current;");
        var currentName = ItemName(0);
        for(var index = 0; index<setIndex; index++) {
            currentName=AppendStage(builder,stages[index],currentName,index);
        }
        var setKeyName = currentName;
        if(stage.Kind is PipelineStageKind.ExceptBy or PipelineStageKind.IntersectBy) {
            setKeyName=KeyName(setIndex);
            builder.AppendLine($"    {ComparerKeyType(stage)} {setKeyName}=default!;");
            builder.AppendLine(IndentBlock(FuncAssign(stage.Func!,stage.Kind.ToString(),setKeyName,currentName),"    "));
        }
        if(stage.Kind is PipelineStageKind.Except or PipelineStageKind.ExceptBy) {
            builder.AppendLine($"    if(this.{SecondSetName(setIndex)}.Contains({setKeyName}) || !this.{SetName(setIndex)}.Add({setKeyName})) continue;");
        } else {
            builder.AppendLine($"    if(!this.{SecondSetName(setIndex)}.Contains({setKeyName}) || !this.{SetName(setIndex)}.Add({setKeyName})) continue;");
        }
        for(var index = setIndex+1; index<stages.Count; index++) {
            currentName=AppendStage(builder,stages[index],currentName,index);
        }
        builder.AppendLine(IndentBlock(string.Format(successStatement,currentName),"    "));
        builder.AppendLine("}");
        builder.AppendLine(endStatement);
        return builder.ToString().TrimEnd();
    }

    private string BuildUnionLoop(string enumeratorName,string successStatement,string endStatement,int unionIndex) {
        var builder = new System.Text.StringBuilder();
        var endLabel = EndLabel(unionIndex);
        var needsEndLabel = StagesAfterNeedEndLabel(unionIndex);
        var breakStatement = "break;";
        if(needsEndLabel) {
            breakStatement=$"goto {endLabel};";
        }
        builder.AppendLine("while(true) {");
        builder.AppendLine($"    if(!this.{StateName(unionIndex)}) {{");
        builder.AppendLine($"        while({enumeratorName}.MoveNext()) {{");
        builder.AppendLine($"            var {ItemName(0)}={enumeratorName}.Current;");
        var currentName = ItemName(0);
        for(var index = 0; index<unionIndex; index++) {
            currentName=AppendStage(builder,stages[index],currentName,index);
        }
        var firstKeyName = currentName;
        if(stages[unionIndex].Kind==PipelineStageKind.UnionBy) {
            firstKeyName=KeyName(unionIndex);
            builder.AppendLine($"            {ComparerKeyType(stages[unionIndex])} {firstKeyName}=default!;");
            builder.AppendLine(IndentBlock(FuncAssign(stages[unionIndex].Func!,nameof(PipelineStageKind.UnionBy),firstKeyName,currentName),"            "));
        }
        builder.AppendLine($"            if(!this.{SetName(unionIndex)}.Add({firstKeyName})) continue;");
        for(var index = unionIndex+1; index<stages.Count; index++) {
            currentName=AppendStage(builder,stages[index],currentName,index,breakStatement);
        }
        builder.AppendLine(IndentBlock(string.Format(successStatement,currentName),"            "));
        builder.AppendLine("        }");
        builder.AppendLine($"        this.{StateName(unionIndex)}=true;");
        builder.AppendLine("    }");
        builder.AppendLine($"    while({SecondEnumeratorName(SecondOrdinal(unionIndex))}.MoveNext()) {{");
        var secondCurrentName = ItemName(unionIndex+1);
        builder.AppendLine($"        var {secondCurrentName}=({stages[unionIndex].OutputType})(object?){SecondEnumeratorName(SecondOrdinal(unionIndex))}.Current!;");
        var secondKeyName = secondCurrentName;
        if(stages[unionIndex].Kind==PipelineStageKind.UnionBy) {
            secondKeyName=KeyName(unionIndex+1);
            builder.AppendLine($"        {ComparerKeyType(stages[unionIndex])} {secondKeyName}=default!;");
            builder.AppendLine(IndentBlock(FuncAssign(stages[unionIndex].Func!,nameof(PipelineStageKind.UnionBy),secondKeyName,secondCurrentName),"        "));
        }
        builder.AppendLine($"        if(!this.{SetName(unionIndex)}.Add({secondKeyName})) continue;");
        currentName=secondCurrentName;
        for(var index = unionIndex+1; index<stages.Count; index++) {
            currentName=AppendStage(builder,stages[index],currentName,index,breakStatement);
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
        var builder = new System.Text.StringBuilder();
        var selectManyStage = stages[selectManyIndex];
        var innerEnumeratorName = InnerEnumeratorName(selectManyIndex);
        var endLabel = EndLabel(selectManyIndex);
        var needsEndLabel = StagesAfterNeedEndLabel(selectManyIndex);
        var breakStatement = "break;";
        if(needsEndLabel) {
            breakStatement=$"goto {endLabel};";
        }
        builder.AppendLine("while(true) {");
        builder.AppendLine($"    if(this.{innerEnumeratorName} is not null) {{");
        builder.AppendLine($"        if(this.{innerEnumeratorName}.MoveNext()) {{");
        var innerItemName = ItemName(selectManyIndex+1);
        builder.AppendLine($"            var {innerItemName}=this.{innerEnumeratorName}.Current;");
        var currentName = innerItemName;
        if(selectManyStage.SecondFunc is { } resultSelector) {
            currentName=ItemName(selectManyIndex+2);
            builder.AppendLine($"            {selectManyStage.OutputType} {currentName}=default!;");
            builder.AppendLine(IndentBlock(FuncAssign(resultSelector,nameof(PipelineStageKind.SelectMany),currentName,OuterName(selectManyIndex),innerItemName),"            "));
        }
        for(var index = selectManyIndex+1; index<stages.Count; index++) {
            var stage = stages[index];
            currentName=AppendStage(builder,stage,currentName,index,breakStatement);
        }
        builder.AppendLine(IndentBlock(string.Format(successStatement,currentName),"            "));
        builder.AppendLine("        }");
        builder.AppendLine($"        this.{innerEnumeratorName}.Dispose();");
        builder.AppendLine($"        this.{innerEnumeratorName}=null;");
        builder.AppendLine("    }");
        builder.AppendLine($"    while({enumeratorName}.MoveNext()) {{");
        var outerCurrentName = ItemName(0);
        builder.AppendLine($"        var {outerCurrentName}={enumeratorName}.Current;");
        currentName=outerCurrentName;
        for(var index = 0; index<selectManyIndex; index++) {
            var stage = stages[index];
            currentName=AppendStage(builder,stage,currentName,index);
        }
        var outerName = OuterName(selectManyIndex);
        builder.AppendLine($"        this.{outerName}={currentName};");
        if(selectManyStage.UsesIndex) {
            builder.AppendLine($"        this.{StateName(selectManyIndex)}++;");
        }
        string[] selectorArguments;
        if(selectManyStage.UsesIndex) {
            selectorArguments=[currentName,$"this.{StateName(selectManyIndex)}"];
        } else {
            selectorArguments=[currentName];
        }
        var collectionExpression = FuncInvoke(selectManyStage.Func!,nameof(PipelineStageKind.SelectMany),selectorArguments);
        builder.AppendLine($"        this.{innerEnumeratorName}=((global::System.Collections.Generic.IEnumerable<{selectManyStage.InnerElementType}>){collectionExpression}).GetEnumerator();");
        builder.AppendLine("        break;");
        builder.AppendLine("    }");
        builder.AppendLine($"    if(this.{innerEnumeratorName} is null) {{");
        builder.AppendLine(IndentBlock(endStatement,"        "));
        builder.AppendLine("    }");
        builder.AppendLine("}");
        if(needsEndLabel) {
            builder.AppendLine($"{endLabel}:");
            builder.AppendLine(endStatement);
        }
        return builder.ToString().TrimEnd();
    }

    private string AppendStage(System.Text.StringBuilder builder,PipelineStage stage,string currentName,int index,string breakStatement = "break;") {
        switch(stage.Kind) {
            case PipelineStageKind.Where:
                AppendWhere(builder,stage,currentName,index);
                return currentName;
            case PipelineStageKind.Select:
                return AppendSelect(builder,stage,currentName,index);
            case PipelineStageKind.Take:
                AppendTake(builder,index,CountOrdinal(index),breakStatement);
                return currentName;
            case PipelineStageKind.Skip:
                AppendSkip(builder,index,CountOrdinal(index));
                return currentName;
            case PipelineStageKind.TakeWhile:
                AppendTakeWhile(builder,stage,currentName,index,breakStatement);
                return currentName;
            case PipelineStageKind.SkipWhile:
                AppendSkipWhile(builder,stage,currentName,index);
                return currentName;
            case PipelineStageKind.Cast:
                return AppendCast(builder,stage,currentName,index);
            case PipelineStageKind.OfType:
                return AppendOfType(builder,stage,currentName,index);
            case PipelineStageKind.Distinct:
                AppendDistinct(builder,stage,currentName,index);
                return currentName;
            case PipelineStageKind.DistinctBy:
                AppendDistinctBy(builder,stage,currentName,index);
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

    private void AppendWhere(System.Text.StringBuilder builder,PipelineStage stage,string currentName,int index) {
        string[] arguments;
        if(stage.UsesIndex) {
            builder.AppendLine($"    this.{StateName(index)}++;");
            arguments=[currentName,$"this.{StateName(index)}"];
        } else {
            arguments=[currentName];
        }
        var (preCode,condExpr) = FuncInvokeCondition(stage.Func!,nameof(PipelineStageKind.Where),TempName(index),arguments);
        if(!string.IsNullOrEmpty(preCode)) {
            builder.AppendLine(IndentBlock(preCode,"    "));
            builder.AppendLine($"    if(!{condExpr}) continue;");
            return;
        }
        builder.AppendLine($"    if(!{WrapConditionExpression(condExpr)}) continue;");
    }

    private string AppendSelect(System.Text.StringBuilder builder,PipelineStage stage,string currentName,int index) {
        var nextName = ItemName(index+1);
        builder.AppendLine($"    {stage.OutputType} {nextName}=default!;");
        string[] arguments;
        if(stage.UsesIndex) {
            builder.AppendLine($"    this.{StateName(index)}++;");
            arguments=[currentName,$"this.{StateName(index)}"];
        } else {
            arguments=[currentName];
        }
        builder.AppendLine(IndentBlock(FuncAssign(stage.Func!,nameof(PipelineStageKind.Select),nextName,arguments),"    "));
        return nextName;
    }

    private void AppendTake(System.Text.StringBuilder builder,int index,int countIndex,string breakStatement = "break;") {
        var stateName = StateName(index);
        var countName = CountName(countIndex);
        builder.AppendLine($"    if(this.{stateName}>=this.{countName}Value) {{");
        builder.AppendLine($"        {breakStatement}");
        builder.AppendLine("    }");
        builder.AppendLine($"    this.{stateName}++;");
    }

    private void AppendSkip(System.Text.StringBuilder builder,int index,int countIndex) {
        var stateName = StateName(index);
        var countName = CountName(countIndex);
        builder.AppendLine($"    if(this.{stateName}<this.{countName}Value) {{");
        builder.AppendLine($"        this.{stateName}++;");
        builder.AppendLine("        continue;");
        builder.AppendLine("    }");
    }

    private void AppendTakeWhile(System.Text.StringBuilder builder,PipelineStage stage,string currentName,int index,string breakStatement = "break;") {
        var stateName = StateName(index);
        builder.AppendLine($"    if(this.{stateName}) {breakStatement}");
        string[] arguments;
        if(stage.UsesIndex) {
            builder.AppendLine($"    this.{IndexName(index)}++;");
            arguments=[currentName,$"this.{IndexName(index)}"];
        } else {
            arguments=[currentName];
        }
        var (preCode,condExpr) = FuncInvokeCondition(stage.Func!,nameof(PipelineStageKind.TakeWhile),TempName(index),arguments);
        if(!string.IsNullOrEmpty(preCode)) {
            builder.AppendLine(IndentBlock(preCode,"    "));
            builder.AppendLine($"    if(!{condExpr}) {{");
        } else {
            builder.AppendLine($"    if(!{WrapConditionExpression(condExpr)}) {{");
        }
        builder.AppendLine($"        this.{stateName}=true;");
        builder.AppendLine($"        {breakStatement}");
        builder.AppendLine("    }");
    }

    private void AppendSkipWhile(System.Text.StringBuilder builder,PipelineStage stage,string currentName,int index) {
        var stateName = StateName(index);
        string[] arguments;
        if(stage.UsesIndex) {
            arguments=[currentName,$"this.{IndexName(index)}"];
        } else {
            arguments=[currentName];
        }
        var (preCode,condExpr) = FuncInvokeCondition(stage.Func!,nameof(PipelineStageKind.SkipWhile),TempName(index),arguments);
        if(!string.IsNullOrEmpty(preCode)) {
            builder.AppendLine($"    if(this.{stateName}) {{");
            if(stage.UsesIndex) {
                builder.AppendLine($"        this.{IndexName(index)}++;");
            }
            builder.AppendLine(IndentBlock(preCode,"        "));
            builder.AppendLine($"        if({condExpr}) continue;");
            builder.AppendLine($"        this.{stateName}=false;");
            builder.AppendLine("    }");
            return;
        }
        builder.AppendLine($"    if(this.{stateName}) {{");
        if(stage.UsesIndex) {
            builder.AppendLine($"        this.{IndexName(index)}++;");
        }
        builder.AppendLine($"        if({WrapConditionExpression(condExpr)}) continue;");
        builder.AppendLine($"        this.{stateName}=false;");
        builder.AppendLine("    }");
    }

    private string AppendCast(System.Text.StringBuilder builder,PipelineStage stage,string currentName,int index) {
        var nextName = ItemName(index+1);
        builder.AppendLine($"    var {nextName}=({stage.OutputType})(object?){currentName};");
        return nextName;
    }

    private string AppendOfType(System.Text.StringBuilder builder,PipelineStage stage,string currentName,int index) {
        var nextName = ItemName(index+1);
        builder.AppendLine($"    if({currentName} is not {stage.OutputType} {nextName}) continue;");
        return nextName;
    }

    private void AppendDistinct(System.Text.StringBuilder builder,PipelineStage stage,string currentName,int index) {
        builder.AppendLine($"    if(!this.{SetName(index)}.Add({currentName})) continue;");
    }

    private void AppendDistinctBy(System.Text.StringBuilder builder,PipelineStage stage,string currentName,int index) {
        var keyName = KeyName(index);
        builder.AppendLine($"    {ComparerKeyType(stage)} {keyName}=default!;");
        builder.AppendLine(IndentBlock(FuncAssign(stage.Func!,nameof(PipelineStageKind.DistinctBy),keyName,currentName),"    "));
        builder.AppendLine($"    if(!this.{SetName(index)}.Add({keyName})) continue;");
    }

    private IEnumerable<string> ClosureArgsWithoutPrefix() {
        var suffix = ClosureArgsSuffix(Funcs.ToArray());
        if(string.IsNullOrEmpty(suffix)) {
            yield break;
        }
        foreach(var part in suffix[2..].Split(',')) {
            yield return part.Trim();
        }
    }

    private IEnumerable<string> ClosureParametersWithoutPrefix() {
        var suffix = ClosurePrimaryCtorSuffix(Funcs.ToArray());
        if(string.IsNullOrEmpty(suffix)) {
            yield break;
        }
        foreach(var part in suffix[2..].Split(',')) {
            yield return part.Trim();
        }
    }

    private string QueueName(int index) => $"__queue{item}_{index}";
    private string StateEndName(int index) => $"__stateEnd{item}_{index}";
    private string RangeName(int index) => $"__range{item}_{index}";
    private int RangeOrdinal(int stageIndex) =>
        stages.Take(stageIndex).Count(static stage => stage.Kind==PipelineStageKind.TakeRange);
    private IEnumerable<PipelineStage> RangeStages() =>
        stages.Where(static stage => stage.Kind==PipelineStageKind.TakeRange);
    private string ItemName(int index) => $"{item}_{index}";
    private string TempName(int index) => $"{temp}_{index}";
    private string StateName(int index) => $"{state}_{index}";
    private string IndexName(int index) => $"__idx{item}_{index}";
    private string CountName(int index) => $"__cnt{item}_{index}";
    private string ComparerName(int index) => $"__cmp{item}_{index}";
    private string OrderComparerName(int index) => $"__ordcmp{item}_{index}";
    private string OrderComparerLocalName(int index) => $"__ordcmpLocal{item}_{index}";
    private static string SecondTypeParameterName(int index) => $"TSecond{index}";
    private string SecondEnumeratorName(int index) => $"__second{item}_{index}";
    private string InnerEnumeratorName(int index) => $"__inner{item}_{index}";
    private string OuterName(int index) => $"{outerValue}_{index}";
    private string SetName(int index) => $"__set{item}_{index}";
    private string SecondSetName(int index) => $"__set2{item}_{index}";
    private string SortItemName(int index) => $"__sortitem{item}_{index}";
    private string SortedItemsName(int index) => $"__sorted{item}_{index}";
    private string GroupingName(int index) => $"__grouping{item}_{index}";
    private string GroupItemsName(int index) => $"__groups{item}_{index}";
    private string GroupName(int index) => $"__group{item}_{index}";
    private string ReverseItemsName(int index) => $"__reversed{item}_{index}";
    private string EndLabel(int index) => $"__end{item}_{index}";
    private string KeyName(int index) => $"{key}_{index}";
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

    private string OrderSortItemType(int orderIndex) {
        var orderStages = OrderStages(orderIndex).ToArray();
        var primaryOrder = orderStages[0];
        if(orderStages.Length==2) {
            var secondaryOrder = orderStages[1];
            return $$"""
            private readonly struct {{SortItemName(orderIndex)}} {
                public {{SortItemName(orderIndex)}}({{primaryOrder.InputType}} value,{{ComparerKeyType(primaryOrder)}} key,{{ComparerKeyType(secondaryOrder)}} secondKey,int index) {
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
        private readonly struct {{SortItemName(orderIndex)}} {
            public {{SortItemName(orderIndex)}}({{primaryOrder.InputType}} value,{{ComparerKeyType(primaryOrder)}} key,int index) {
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
        return GroupingName(index);
    }

    private string GroupingType(int index,PipelineStage stage) => $$"""
        private sealed class {{GroupingName(index)}}:global::System.Linq.IGrouping<{{ComparerKeyType(stage)}},{{stage.InnerElementType}}> {
            private readonly global::System.Collections.Generic.List<{{stage.InnerElementType}}> values = new();
            public {{GroupingName(index)}}({{ComparerKeyType(stage)}} key) {
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
        var builder = new System.Text.StringBuilder();
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
                {{DefineScalarEnumeratorStruct(ResultType)}}
                """;
        }
    }
    public override string GetEnumeratorExpression {
        get {
            if(terminalKind is PipelineTerminalKind.ToList or PipelineTerminalKind.ToArray or PipelineTerminalKind.ToDictionary or PipelineTerminalKind.Sum) {
                throw new NotSupportedException($"{nameof(PipelineTerminal)} uses direct execution.");
            }
            return $"new {EnumeratorTypeName}({pipeline.GetEnumeratorExpression})";
        }
    }
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {e}";
    public override string GetEnumeratorFields => $$"""
        public {{ResultType}} Current { get; private set; } = Compute({{e}});
        private bool {{yielded}} = false;
        {{AggressiveInliningAttribute}}
        private static {{ResultType}} Compute(TEnum source) {
            {{ComputeBody}}
        }
        """;
    public override string GetTryGetNextBody => $$"""
        if(this.{{yielded}}) {
            current = default!;
            return false;
        }
        this.{{yielded}} = true;
        current = this.Current;
        return true;
        """;
    public override string GetCurrentExpression => "Current";
    protected override string GetCurrentPropertyDeclaration(string currentType) => string.Empty;
    private string ComputeBody {
        get {
            if(terminalKind==PipelineTerminalKind.Count) {
                return "int count = 0;\nwhile(source.MoveNext()) count++;\nreturn count;";
            }
            if(terminalKind==PipelineTerminalKind.Any) {
                return "return source.MoveNext();";
            }
            if(terminalKind==PipelineTerminalKind.First) {
                return "if(source.MoveNext()) return source.Current;\nthrow global::Seamless.GeneratedCodeHelpers.NoElements();";
            }
            throw new NotSupportedException($"{terminalKind} does not support scalar enumerator generation.");
        }
    }
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
    public string GetDirectExecutionStatement(string actualVariableName,string actualTypeName) {
        var builder = new System.Text.StringBuilder();
        if(terminalKind==PipelineTerminalKind.ToList) {
            builder.AppendLine($"{actualTypeName} {actualVariableName}=new();");
            builder.AppendLine($"using var {EnumeratorName()}={pipeline.GetEnumeratorExpression};");
            builder.AppendLine($"while({EnumeratorName()}.MoveNext()) {actualVariableName}.Add({EnumeratorName()}.Current);");
            return builder.ToString().TrimEnd();
        }
        if(terminalKind==PipelineTerminalKind.ToArray) {
            var listName = $"{actualVariableName}_list";
            builder.AppendLine($"var {listName}=new global::System.Collections.Generic.List<{pipeline.ElementType}>();");
            builder.AppendLine($"using var {EnumeratorName()}={pipeline.GetEnumeratorExpression};");
            builder.AppendLine($"while({EnumeratorName()}.MoveNext()) {listName}.Add({EnumeratorName()}.Current);");
            builder.AppendLine($"{actualTypeName} {actualVariableName}={listName}.ToArray();");
            return builder.ToString().TrimEnd();
        }
        if(terminalKind==PipelineTerminalKind.Count) {
            builder.AppendLine($"{actualTypeName} {actualVariableName}=0;");
            builder.AppendLine($"using var {EnumeratorName()}={pipeline.GetEnumeratorExpression};");
            builder.AppendLine($"while({EnumeratorName()}.MoveNext()) {actualVariableName}++;");
            return builder.ToString().TrimEnd();
        }
        if(terminalKind==PipelineTerminalKind.Any) {
            builder.AppendLine($"{actualTypeName} {actualVariableName}=false;");
            builder.AppendLine($"using var {EnumeratorName()}={pipeline.GetEnumeratorExpression};");
            builder.AppendLine($"if({EnumeratorName()}.MoveNext()) {actualVariableName}=true;");
            return builder.ToString().TrimEnd();
        }
        if(terminalKind==PipelineTerminalKind.First) {
            builder.AppendLine($"{actualTypeName} {actualVariableName}=default!;");
            builder.AppendLine($"using var {EnumeratorName()}={pipeline.GetEnumeratorExpression};");
            builder.AppendLine($"if({EnumeratorName()}.MoveNext()) {{");
            builder.AppendLine($"    {actualVariableName}={EnumeratorName()}.Current;");
            builder.AppendLine("} else {");
            builder.AppendLine("    throw global::Seamless.GeneratedCodeHelpers.NoElements();");
            builder.AppendLine("}");
            return builder.ToString().TrimEnd();
        }
        if(terminalKind==PipelineTerminalKind.Sum) {
            builder.AppendLine($"{actualTypeName} {actualVariableName}=default!;");
            builder.AppendLine($"using var {EnumeratorName()}={pipeline.GetEnumeratorExpression};");
            builder.AppendLine($"while({EnumeratorName()}.MoveNext()) {actualVariableName}+={EnumeratorName()}.Current;");
            return builder.ToString().TrimEnd();
        }
        if(terminalKind==PipelineTerminalKind.ToDictionary) {
            var func1Code = func1;
            var func2Code = func2;
            var cmpArg = comparerValue ?? "null";
            var keyType = func1Code?.ReturnTypeName ?? "object";
            var valType = func2Code?.ReturnTypeName ?? pipeline.ElementType;
            var dictTypeName = $"global::System.Collections.Generic.Dictionary<{keyType},{valType}>";
            var eName = EnumeratorName();
            var itemName = $"__dItem{result}";
            var keyName = $"__dKey{result}";
            var valName = $"__dVal{result}";
            builder.AppendLine($"{actualTypeName} {actualVariableName}=new {dictTypeName}((global::System.Collections.Generic.IEqualityComparer<{keyType}>?){cmpArg});");
            builder.AppendLine($"using var {eName}={pipeline.GetEnumeratorExpression};");
            builder.AppendLine($"while({eName}.MoveNext()) {{");
            builder.AppendLine($"    var {itemName}={eName}.Current;");
            builder.AppendLine($"    {keyType} {keyName}=default!;");
            AppendIndented(builder,FuncAssign(func1Code!,"ToDictionary",keyName,itemName),"    ");
            if(func2Code is not null) {
                builder.AppendLine($"    {valType} {valName}=default!;");
                AppendIndented(builder,FuncAssign(func2Code,"ToDictionary",valName,itemName),"    ");
                builder.AppendLine($"    {actualVariableName}[{keyName}]={valName};");
            } else {
                builder.AppendLine($"    {actualVariableName}[{keyName}]=({valType})(object?){itemName}!;");
            }
            builder.AppendLine("}");
            return builder.ToString().TrimEnd();
        }
        throw new NotSupportedException($"Unsupported pipeline terminal: {terminalKind}.");
    }

    private string EnumeratorName() => $"__pipeline{result}";
    private static void AppendIndented(System.Text.StringBuilder sb,string text,string indent) {
        foreach(var line in text.Replace("\r\n","\n").Split('\n')) {
            if(!string.IsNullOrWhiteSpace(line)) sb.AppendLine($"{indent}{line.Trim()}");
        }
    }
}
