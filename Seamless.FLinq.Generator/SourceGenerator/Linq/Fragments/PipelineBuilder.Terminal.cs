using Microsoft.CodeAnalysis.Operations;
namespace Seamless.FLinq.Generator;

// 終端演算の種別判定と引数の取り込み。
internal static partial class PipelineBuilder {
    private static bool TryBuildSmallSideJoinArray(
        SemanticModel model,ExpressionSyntax source,List<InvocationExpressionSyntax> calls,PipelineBuildServices services,
        ref int number,out Fragment fragment){
        fragment=null!;
        if(calls.Count!=3||model.GetSymbolInfo(calls[0]).Symbol is not IMethodSymbol {Name:"Join",ReducedFrom:{} join} method
            ||join.ContainingType.ToDisplayString()!="System.Linq.Enumerable"
            ||model.GetSymbolInfo(calls[1]).Symbol is not IMethodSymbol hint||!IsOrderHint(hint,"WithOrder")
            ||calls[1].ArgumentList.Arguments.Count!=1
            ||model.GetConstantValue(calls[1].ArgumentList.Arguments[0].Expression) is not {HasValue:true,Value:2}
            ||model.GetSymbolInfo(calls[2]).Symbol is not IMethodSymbol {Name:"ToArray",ReducedFrom:{} terminal}
            ||terminal.ContainingType.ToDisplayString()!="System.Linq.Enumerable"){
            return false;
        }
        var args=calls[0].ArgumentList.Arguments;
        var element=method.TypeArguments[0];
        if(args.Count!=4||args.Any(static arg=>arg.NameColon is not null)
            ||element.SpecialType is not (SpecialType.System_Int32 or SpecialType.System_Int64)
            ||!SymbolEqualityComparer.Default.Equals(element,method.TypeArguments[1])
            ||!SymbolEqualityComparer.Default.Equals(element,method.TypeArguments[2])
            ||AnchorAnalysis.ContainsAnchor(calls[0],model)
            ||!IsDirectJoinInput(source)||!IsDirectJoinInput(args[0].Expression)
            ||!IsIdentityJoinKey(args[1].Expression)||!IsIdentityJoinKey(args[2].Expression)
            ||args[3].Expression is not LambdaExpressionSyntax {Body:ExpressionSyntax resultBody}
            ||!IsPureProjection(model.GetOperation(resultBody))){
            return false;
        }
        fragment=new SmallSideJoinArray(number++,element.ToDisplayString(services.FullyQualifiedFormat),
            method.TypeArguments[3].ToDisplayString(services.FullyQualifiedFormat),services.RenderExpression(source),
            services.RenderExpression(args[0].Expression),services.RenderExpression(args[3].Expression));
        return true;

        bool IsIdentityJoinKey(ExpressionSyntax expression)=>
            model.GetOperation(expression) is IAnonymousFunctionOperation {Symbol.Parameters.Length:1} lambda
            &&expression is LambdaExpressionSyntax {Body:ExpressionSyntax body}
            &&model.GetOperation(body) is IParameterReferenceOperation parameter
            &&SymbolEqualityComparer.Default.Equals(parameter.Parameter,lambda.Symbol.Parameters[0]);

        static bool IsPureProjection(IOperation? operation)=>operation is IParameterReferenceOperation
            ||operation is ITupleOperation tuple&&tuple.Elements.All(static item=>item is IParameterReferenceOperation);

        bool IsDirectJoinInput(ExpressionSyntax expression){
            if(model.GetOperation(expression) is not (IParameterReferenceOperation or ILocalReferenceOperation)){return false;}
            return IsKnownJoinInputType(model.GetTypeInfo(expression).Type);
        }
    }

    private static bool IsKnownJoinInputType(ITypeSymbol? type)=>
        type is IArrayTypeSymbol {Rank:1}
        ||type is INamedTypeSymbol named&&named.OriginalDefinition.ToDisplayString() is
            "Seamless.Collections.Tsavorite.HashSet<TKey>" or "Seamless.Collections.Persisted.Set<T>";

    private sealed class SmallSideJoinArray(int number,string type,string resultType,string left,string right,string selector)
        :Scalar(number),DirectExecutionFragment{
        public override string StructMethod=>string.Empty;
        public override string GetEnumeratorStruct=>string.Empty;
        public override string GetTryGetNextBody=>string.Empty;
        public string GetDirectExecutionStatement(string actualVariableName,string actualTypeName)=>
            $"// [Join/順序] 件数を追加走査せず確認し、左が小さければ左にLookupを作る。結果selectorには元の左右順で渡す。\n{actualTypeName} {actualVariableName}=global::Seamless.Linq.Enumerable.SmallSideJoinToArray<{type},{resultType}>({left},{right},{selector});";
    }

    private static bool TryBuildSetParallelTerminal(
        SemanticModel model,ExpressionSyntax source,List<InvocationExpressionSyntax> calls,PipelineBuildServices services,
        ref int number,out Fragment fragment){
        fragment=null!;
        if(calls.Count<3
            ||model.GetSymbolInfo(calls[0]).Symbol is not IMethodSymbol {Name:"Union" or "Join",ReducedFrom:{} set} method
            ||set.ContainingType.ToDisplayString()!="System.Linq.Enumerable"
            ||model.GetSymbolInfo(calls[calls.Count-1]).Symbol is not IMethodSymbol{ReducedFrom:{} terminal}
            ||terminal.ContainingType.ToDisplayString()!="System.Linq.Enumerable"
            ||!TryGetTerminalKind(terminal.Name,out _)
            ||model.GetSymbolInfo(calls[1]).Symbol is not IMethodSymbol firstHint
            ||!IsOrderHint(firstHint,"Parallel")&&!IsOrderHint(firstHint,"WithOrder")){
            return false;
        }
        var isJoin=method.Name=="Join";
        var inputArgs=calls[0].ArgumentList.Arguments;
        // 取引行JoinのParallelは既存の予算付き経路が処理する。
        if(isJoin&&!IsKnownJoinInputType(model.GetTypeInfo(source).Type)
            ||!isJoin&&inputArgs.Count is not (1 or 2)){return false;}
        ExpressionSyntax? slots=null;
        var order=0;
        var hasOrder=false;
        var hasTail=false;
        var tail=new List<(string Prefix,string Suffix)>();
        for(var index=1;index<calls.Count-1;index++){
            var call=calls[index];
            if(model.GetSymbolInfo(call).Symbol is not IMethodSymbol hint){return false;}
            if(!IsOrderHint(hint,"Parallel")&&!IsOrderHint(hint,"WithOrder")){
                hasTail=true;
                if(!AppendTail(call)){return false;}
                continue;
            }
            if(hasTail||call.ArgumentList.Arguments.Count!=1){
                throw Unsupported($"Place the {method.Name} hints immediately after {method.Name}.");
            }
            var argument=call.ArgumentList.Arguments[0].Expression;
            if(IsOrderHint(hint,"Parallel")){
                if(slots is not null){throw Unsupported("Use one Parallel hint per query chain.");}
                slots=argument;
            }else if(IsOrderHint(hint,"WithOrder")){
                if(hasOrder){throw Unsupported("Use one WithOrder hint per query chain.");}
                hasOrder=true;
                if(model.GetConstantValue(argument) is not {HasValue:true,Value:int value}||value is <0 or >2){
                    throw Unsupported("WithOrder requires a constant QueryOrder value.");
                }
                order=value;
            }
        }
        if(slots is null||isJoin&&!hasOrder){return false;}
        if(isJoin||hasTail||terminal.Name!="ToArray"){
            if(!AppendTail(calls[calls.Count-1])){return false;}
            fragment=new OrderedSetTerminal(number++,method.Name,services.RenderExpression(calls[0]),services.RenderExpression(slots),tail);
            return true;
        }
        var element=method.TypeArguments[0];
        var right=calls[0].ArgumentList.Arguments[0].Expression;
        var canReorder=order==2&&calls[0].ArgumentList.Arguments.Count==1
            &&element.SpecialType is SpecialType.System_Int32 or SpecialType.System_Int64
            &&!AnchorAnalysis.ContainsAnchor(source,model)&&!AnchorAnalysis.ContainsAnchor(right,model);
        var type=element.ToDisplayString(services.FullyQualifiedFormat);
        var input=canReorder
            ?$"global::System.Linq.Enumerable.Concat<{type}>({services.RenderExpression(source)},{services.RenderExpression(right)})"
            :services.RenderExpression(calls[0]);
        fragment=new ParallelUnionArray(number++,input,services.RenderExpression(slots),canReorder);
        return true;

        bool AppendTail(InvocationExpressionSyntax call){
            if(model.GetSymbolInfo(call).Symbol is not IMethodSymbol{ReducedFrom.ContainingType:{} owner} operation
                ||owner.ToDisplayString()!="System.Linq.Enumerable"
                ||operation.TypeArguments.Any(services.ContainsAnonymousType)){return false;}
            var types=string.Join(",",operation.TypeArguments.Select(type=>type.ToDisplayString(services.FullyQualifiedFormat)));
            var args=string.Concat(call.ArgumentList.Arguments.Select(arg=>","+
                (arg.NameColon?.ToString()??string.Empty)+services.RenderExpression(arg.Expression)));
            tail.Add(($"global::System.Linq.Enumerable.{operation.Name}<{types}>(",args+")"));
            return true;
        }
    }

    private sealed class OrderedSetTerminal(int number,string operation,string input,string slots,List<(string Prefix,string Suffix)> tail):Scalar(number),DirectExecutionFragment{
        public override string StructMethod=>string.Empty;
        public override string GetEnumeratorStruct=>string.Empty;
        public override string GetTryGetNextBody=>string.Empty;
        public string GetDirectExecutionStatement(string actualVariableName,string actualTypeName){
            var source=$"__orderedInput{number}";
            var expression=source;
            foreach(var call in tail){
                expression=call.Prefix+expression+call.Suffix;
            }
            return $$"""
                // [{{operation}}/順序] 通常LINQの列挙順と遅延列挙を維持する。
                var {{source}}={{input}};
                global::System.ArgumentOutOfRangeException.ThrowIfNegativeOrZero({{slots}},"maxExecutionSlots");
                {{actualTypeName}} {{actualVariableName}}={{expression}};
                """;
        }
    }

    private sealed class ParallelUnionArray(int number,string input,string slots,bool canReorder)
        :Scalar(number),DirectExecutionFragment{
        public override string StructMethod=>string.Empty;
        public override string GetEnumeratorStruct=>string.Empty;
        public override string GetTryGetNextBody=>string.Empty;
        public string GetDirectExecutionStatement(string actualVariableName,string actualTypeName){
            if(canReorder){
                return $"// [Union/順序] 既定比較で分割し、分割内の初出を採用する。分割間の順序を復元しない。\n{actualTypeName} {actualVariableName}=global::Seamless.Linq.Enumerable.ParallelUnionToArray({input},{slots});";
            }
            return $$"""
                // [Union/順序] 既存の初出順を維持する。実行枠の式は一度だけ検証する。
                var __unionInput{{number}}={{input}};
                global::System.ArgumentOutOfRangeException.ThrowIfNegativeOrZero({{slots}},"maxExecutionSlots");
                {{actualTypeName}} {{actualVariableName}}=global::System.Linq.Enumerable.ToArray(__unionInput{{number}});
                """;
        }
    }

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
            if(PreservesSystemUnionCall(semanticModel,currentInvocation,method)){
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

    internal static bool IsPreservedSystemUnionSource(SemanticModel model,ExpressionSyntax source)=>
        RemoveParentheses(source) is InvocationExpressionSyntax invocation
        &&model.GetSymbolInfo(invocation).Symbol is IMethodSymbol method
        &&PreservesSystemUnionCall(model,invocation,method);

    internal static bool IsStaticToArrayOfPreservedUnion(SemanticModel model,InvocationExpressionSyntax invocation,IMethodSymbol method)=>
        method.ReducedFrom is null&&method.Name=="ToArray"
        &&method.ContainingType.ToDisplayString()=="System.Linq.Enumerable"
        &&invocation.ArgumentList.Arguments.Count==1
        &&IsPreservedSystemUnionSource(model,invocation.ArgumentList.Arguments[0].Expression);

    internal static bool PreservesSystemUnionCall(SemanticModel model,InvocationExpressionSyntax invocation,IMethodSymbol method){
        if(method.Name is not("Union" or "UnionBy")||!IsSystemEnumerable(method)){
            return false;
        }
        // 静的呼び出しは名前付き引数も含め、元の呼び出しで検証と評価の順序を保つ。
        if(method.ReducedFrom is null){
            return true;
        }
        if(invocation.Expression is MemberAccessExpressionSyntax member
            &&RemoveParentheses(member.Expression) is InvocationExpressionSyntax inner
            &&model.GetSymbolInfo(inner).Symbol is IMethodSymbol innerMethod&&IsSystemEnumerable(innerMethod)
            &&(innerMethod.Name is "Distinct" or "DistinctBy"||(method.Name=="Union"&&innerMethod.Name=="Union"))){
            return true;
        }

        // 右辺の検証と最初のキー評価は、終端で列挙を短絡する場合も観測される。
        SyntaxNode receiver=invocation;
        while(receiver.Parent is ParenthesizedExpressionSyntax parent){receiver=parent;}
        return receiver.Parent is MemberAccessExpressionSyntax access&&access.Expression==receiver
            &&access.Parent is InvocationExpressionSyntax terminal
            &&model.GetSymbolInfo(terminal).Symbol is IMethodSymbol terminalMethod
            &&terminalMethod.Name is "First" or "FirstOrDefault" or "Any" or "Contains"
            &&IsSystemEnumerable(terminalMethod);

        static bool IsSystemEnumerable(IMethodSymbol symbol)=>
            (symbol.ReducedFrom??symbol).ContainingType.ToDisplayString()=="System.Linq.Enumerable";
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
            case "ElementAt":
                terminalKind=PipelineTerminalKind.ElementAt;
                return true;
            case "ElementAtOrDefault":
                terminalKind=PipelineTerminalKind.ElementAtOrDefault;
                return true;
            case "MinBy":
                terminalKind=PipelineTerminalKind.MinBy;
                return true;
            case "MaxBy":
                terminalKind=PipelineTerminalKind.MaxBy;
                return true;
            case "Aggregate":
                terminalKind=PipelineTerminalKind.Aggregate;
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
        if(terminalKind is PipelineTerminalKind.Last or PipelineTerminalKind.LastOrDefault){
            if(invocation.ArgumentList.Arguments.Count==0){return true;}
            if(invocation.ArgumentList.Arguments.Count!=1||method.Parameters.Length==0
                ||method.Parameters[0].Type is not INamedTypeSymbol{TypeArguments.Length:2}){
                return false;
            }
            if(!services.BuildFunc(invocation.ArgumentList.Arguments[0].Expression,method.Parameters[0].Type,ref uniqueNumber,out func1)
                ||!services.CanUseFunc(func1)){
                return false;
            }
            // 元terminal predicateを保持する。Where stageとして追加しない。
            return true;
        }
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
        if(terminalKind is PipelineTerminalKind.ElementAt or PipelineTerminalKind.ElementAtOrDefault) {
            if(invocation.ArgumentList.Arguments.Count!=1||method.Parameters.Length==0) {
                return false;
            }
            if(method.Parameters[0].Type.Name=="Index"||method.Parameters[0].Type.ToDisplayString()=="System.Index") {
                return false;
            }
            terminalValue=services.RenderExpression(invocation.ArgumentList.Arguments[0].Expression);
            return true;
        }
        if(terminalKind is PipelineTerminalKind.MinBy or PipelineTerminalKind.MaxBy) {
            if(invocation.ArgumentList.Arguments.Count!=1
                ||method.Parameters.Length==0
                ||method.Parameters[0].Type is not INamedTypeSymbol { TypeArguments.Length: 2 } keySelectorType) {
                return false;
            }
            if(!services.BuildFunc(invocation.ArgumentList.Arguments[0].Expression,keySelectorType,ref uniqueNumber,out func1)
                ||!services.CanUseFunc(func1)) {
                return false;
            }
            return true;
        }
        if(terminalKind==PipelineTerminalKind.Aggregate) {
            if(invocation.ArgumentList.Arguments.Count==0||method.Parameters.Length==0) {
                return false;
            }
            if(invocation.ArgumentList.Arguments.Count==1) {
                if(method.Parameters[0].Type is not INamedTypeSymbol { TypeArguments.Length: 3 } funcType) {
                    return false;
                }
                if(!services.BuildFunc(invocation.ArgumentList.Arguments[0].Expression,funcType,ref uniqueNumber,out func1)
                    ||!services.CanUseFunc(func1)) {
                    return false;
                }
                return true;
            }
            if(invocation.ArgumentList.Arguments.Count==2) {
                terminalValue=services.RenderExpression(invocation.ArgumentList.Arguments[0].Expression);
                if(method.Parameters[1].Type is not INamedTypeSymbol { TypeArguments.Length: 3 } funcType) {
                    return false;
                }
                if(!services.BuildFunc(invocation.ArgumentList.Arguments[1].Expression,funcType,ref uniqueNumber,out func1)
                    ||!services.CanUseFunc(func1)) {
                    return false;
                }
                return true;
            }
            if(invocation.ArgumentList.Arguments.Count==3) {
                terminalValue=services.RenderExpression(invocation.ArgumentList.Arguments[0].Expression);
                if(method.Parameters[1].Type is not INamedTypeSymbol { TypeArguments.Length: 3 } funcType) {
                    return false;
                }
                if(!services.BuildFunc(invocation.ArgumentList.Arguments[1].Expression,funcType,ref uniqueNumber,out func1)
                    ||!services.CanUseFunc(func1)) {
                    return false;
                }
                if(method.Parameters[2].Type is not INamedTypeSymbol { TypeArguments.Length: 2 } resSelectorType) {
                    return false;
                }
                if(!services.BuildFunc(invocation.ArgumentList.Arguments[2].Expression,resSelectorType,ref uniqueNumber,out func2)
                    ||!services.CanUseFunc(func2)) {
                    return false;
                }
                return true;
            }
            return false;
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
