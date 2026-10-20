namespace Seamless.FLinq.Generator;

// 終端演算の種別判定と引数の取り込み。
internal static partial class PipelineBuilder {
    private static bool TryExtract(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        out ExpressionSyntax sourceExpression,
        out List<InvocationExpressionSyntax> invocations) {
        sourceExpression=null!;
        invocations=new List<InvocationExpressionSyntax>();
        ExpressionSyntax current = invocation;
        while(current is InvocationExpressionSyntax currentInvocation
            &&currentInvocation.Expression is MemberAccessExpressionSyntax { Expression: var receiverExpression }) {
            if(semanticModel.GetSymbolInfo(currentInvocation).Symbol is not IMethodSymbol method
                ||method.ReducedFrom is null) {
                break;
            }
            var containingType = (method.ReducedFrom??method).ContainingType.ToDisplayString();
            if(containingType!="System.Linq.Enumerable"
                &&containingType!="Seamless.Linq.Enumerable") {
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
            case "ToHashSet":
                terminalKind=PipelineTerminalKind.ToHashSet;
                return true;
            case "Count":
                terminalKind=PipelineTerminalKind.Count;
                return true;
            case "LongCount":
                terminalKind=PipelineTerminalKind.LongCount;
                return true;
            case "Any":
                terminalKind=PipelineTerminalKind.Any;
                return true;
            case "All":
                terminalKind=PipelineTerminalKind.All;
                return true;
            case "Contains":
                terminalKind=PipelineTerminalKind.Contains;
                return true;
            case "First":
                terminalKind=PipelineTerminalKind.First;
                return true;
            case "FirstOrDefault":
                terminalKind=PipelineTerminalKind.FirstOrDefault;
                return true;
            case "Last":
                terminalKind=PipelineTerminalKind.Last;
                return true;
            case "LastOrDefault":
                terminalKind=PipelineTerminalKind.LastOrDefault;
                return true;
            case "ToDictionary":
                terminalKind=PipelineTerminalKind.ToDictionary;
                return true;
            case "Sum":
                terminalKind=PipelineTerminalKind.Sum;
                return true;
            case "Average":
                terminalKind=PipelineTerminalKind.Average;
                return true;
            case "Min":
                terminalKind=PipelineTerminalKind.Min;
                return true;
            case "Max":
                terminalKind=PipelineTerminalKind.Max;
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
        out string? comparerValue,
        out string? terminalValue) {
        func1=null;
        func2=null;
        comparerValue=null;
        terminalValue=null;
        if(terminalKind is PipelineTerminalKind.ToList or PipelineTerminalKind.ToArray) {
            return invocation.ArgumentList.Arguments.Count==0;
        }
        if(terminalKind==PipelineTerminalKind.ToHashSet) {
            if(invocation.ArgumentList.Arguments.Count==0) {
                return true;
            }
            if(invocation.ArgumentList.Arguments.Count==1) {
                comparerValue=services.RenderExpression(invocation.ArgumentList.Arguments[0].Expression);
                return true;
            }
            return false;
        }
        if(terminalKind==PipelineTerminalKind.All) {
            if(invocation.ArgumentList.Arguments.Count!=1
                ||method.Parameters.Length==0
                ||method.Parameters[0].Type is not INamedTypeSymbol { TypeArguments.Length: 2 } allPredType) {
                return false;
            }
            if(!services.BuildFunc(invocation.ArgumentList.Arguments[0].Expression,allPredType,ref uniqueNumber,out func1)
                ||!services.CanUseFunc(func1)) {
                return false;
            }
            return true;
        }
        if(terminalKind==PipelineTerminalKind.Contains) {
            if(invocation.ArgumentList.Arguments.Count==0||method.Parameters.Length==0) {
                return false;
            }
            terminalValue=services.RenderExpression(invocation.ArgumentList.Arguments[0].Expression);
            if(invocation.ArgumentList.Arguments.Count>=2&&method.Parameters.Length>=2) {
                comparerValue=services.RenderExpression(invocation.ArgumentList.Arguments[1].Expression);
            }
            return true;
        }
        if(terminalKind==PipelineTerminalKind.ToDictionary) {
            return TryAppendToDictionaryTerminalArguments(semanticModel,invocation,method,services,ref uniqueNumber,out func1,out func2,out comparerValue);
        }
        if(terminalKind is PipelineTerminalKind.Sum or PipelineTerminalKind.Average or PipelineTerminalKind.Min or PipelineTerminalKind.Max) {
            // Sum/Average/Min/Max(selector): add Select stage, then terminal is applied to selected values.
            if(invocation.ArgumentList.Arguments.Count==0) {
                return true;
            }
            if(invocation.ArgumentList.Arguments.Count!=1
                ||method.Parameters.Length==0
                ||method.Parameters[0].Type is not INamedTypeSymbol { TypeArguments.Length: 2 } selectorType) {
                return false;
            }
            if(!services.BuildFunc(invocation.ArgumentList.Arguments[0].Expression,selectorType,ref uniqueNumber,out var selector)
                ||!services.CanUseFunc(selector)) {
                return false;
            }
            var selectElementType = selector.ReturnTypeName;
            stages.Add(new PipelineStage(PipelineStageKind.Select,selector,null,currentElementType,selectElementType));
            currentElementType=selectElementType;
            return true;
        }
        if(invocation.ArgumentList.Arguments.Count==0) {
            return true;
        }
        if(invocation.ArgumentList.Arguments.Count!=1
            ||method.Parameters.Length==0
            ||method.Parameters[0].Type is not INamedTypeSymbol { TypeArguments.Length: 2 }) {
            return false;
        }
        if(!services.BuildFunc(invocation.ArgumentList.Arguments[0].Expression,method.Parameters[0].Type,ref uniqueNumber,out var predicate)
            ||!services.CanUseFunc(predicate)) {
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
        if(invocation.ArgumentList.Arguments.Count==0||method.Parameters.Length==0) {
            return false;
        }
        // First arg is always keySelector Func<TSource,TKey>
        if(method.Parameters[0].Type is not INamedTypeSymbol { TypeArguments.Length: 2 } keySelectorType) {
            return false;
        }
        if(!services.BuildFunc(invocation.ArgumentList.Arguments[0].Expression,keySelectorType,ref uniqueNumber,out func1)
            ||!services.CanUseFunc(func1)) {
            return false;
        }
        if(invocation.ArgumentList.Arguments.Count==1) {
            return true;
        }
        // Second arg: elementSelector Func<TSource,TElement> or IEqualityComparer<TKey>
        if(invocation.ArgumentList.Arguments.Count>=2&&method.Parameters.Length>=2) {
            if(method.Parameters[1].Type is INamedTypeSymbol { TypeArguments.Length: 2 } elemSelectorType) {
                if(!services.BuildFunc(invocation.ArgumentList.Arguments[1].Expression,elemSelectorType,ref uniqueNumber,out func2)
                    ||!services.CanUseFunc(func2)) {
                    return false;
                }
                if(invocation.ArgumentList.Arguments.Count==3&&method.Parameters.Length>=3) {
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
}
