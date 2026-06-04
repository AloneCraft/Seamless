namespace Seamless.SourceGenerator.Linq.Fragments;
public class Any(int 番号,Enumerable source):Scalar(番号){
    internal static bool TryBuildWithPredicate(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol containingType,
        ref int uniqueNumber,
        out Fragment fragment,
        out string staticMethods,
        List<string>? unsafeAccessorCollector,
        BuildEnumerableFragmentDelegate buildEnumerable,
        BuildFuncFragmentDelegate buildFunc,
        CreateFragmentDelegate createFragment) {
        fragment = null!;
        staticMethods = string.Empty;
        if(semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method)
            return false;
        ExpressionSyntax sourceExpression;
        var argumentStartIndex = 0;
        if(method.ReducedFrom is not null
            && invocation.Expression is MemberAccessExpressionSyntax { Expression: var receiverExpression }) {
            sourceExpression = receiverExpression;
        } else {
            var definition = method.ReducedFrom ?? method;
            if(definition.ContainingType.ToDisplayString() != "System.Linq.Enumerable"
                || definition.Parameters.Length == 0
                || invocation.ArgumentList.Arguments.Count == 0) {
                return false;
            }
            sourceExpression = invocation.ArgumentList.Arguments[0].Expression;
            argumentStartIndex = 1;
        }
        if(!buildEnumerable(semanticModel, sourceExpression, containingType, ref uniqueNumber, out var sourceFragment, out var sourceStaticMethods, unsafeAccessorCollector))
            return false;
        if(invocation.ArgumentList.Arguments.Count - argumentStartIndex == 1) {
            if(!buildFunc(invocation.ArgumentList.Arguments[argumentStartIndex].Expression, method.Parameters[argumentStartIndex].Type, ref uniqueNumber, out var predicate, semanticModel, containingType))
                return false;
            var anyVariantName = predicate.ParameterTypeNames.Length > 1 ? "indexPredicate" : "predicate";
            var anyPredicateFragment = createFragment("Any", anyVariantName, uniqueNumber++, [sourceFragment, predicate]);
            if(anyPredicateFragment is null)
                return false;
            fragment = anyPredicateFragment;
            staticMethods = JoinStaticMethods(sourceStaticMethods, fragment.StructMethod);
            return true;
        } else if(invocation.ArgumentList.Arguments.Count != argumentStartIndex) {
            return false;
        }
        var anyFragment = createFragment("Any", string.Empty, uniqueNumber++, [sourceFragment]);
        if(anyFragment is null)
            return false;
        fragment = anyFragment;
        staticMethods = JoinStaticMethods(sourceStaticMethods, fragment.StructMethod);
        return true;
    }
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    public override string GetEnumeratorExpression => $"new {EnumeratorTypeName}({source.GetEnumeratorExpression})";
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{DefineScalarEnumeratorStruct("bool")}}
        """;
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {e}";
    public override string GetEnumeratorFields => $$"""
        private bool {{yielded}} = false;
        """;
    public override string GetTryGetNextBody => $$"""
        if(this.{{yielded}}) {
            current = default;
            return false;
        }
        this.{{yielded}} = true;
        this.{{currentValue}} = {{e}}.MoveNext();
        current = this.{{currentValue}};
        return true;
        """;
    public override string GetCurrentExpression => currentValue;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
}

public class Any_predicate(int 番号, Enumerable source, FuncFragment predicate) : BufferedScalar(番号, "bool") {
    private string PredicateFuncType => $"global::System.Func<{source.ElementType}, bool>";
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => FuncSupport(predicate,nameof(predicate));
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression =>
        $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(predicate)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {e}{ClosurePrimaryCtorSuffix(predicate)}";
    protected override string ComputeExpression =>
        $"Compute({e}{ClosureArgsSuffix(predicate)})";
    protected override string ComputeMembers => source.CanProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static bool Compute(TEnum source{{ClosureMethodParameterSuffix(predicate)}}) {
                source.TryGetSpan(out var __span);
                foreach(var {{item}} in __span){
                    bool {{temp}}=default!;
                    {{FuncAssign(predicate,nameof(predicate),temp,item)}}
                    if({{temp}}) return true;
                }
                return false;
            }
            """
        : $$"""
            {{AggressiveInliningAttribute}}
            private static bool Compute(TEnum source{{ClosureMethodParameterSuffix(predicate)}}) {
                while(source.MoveNext()) {
                    var {{item}}=source.Current;
                    bool {{temp}}=default!;
                    {{FuncAssign(predicate,nameof(predicate),temp,item)}}
                    if({{temp}}) return true;
                }
                return false;
            }
            """;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
}

public class Any_indexPredicate(int 番号, Enumerable source, FuncFragment predicate) : BufferedScalar(番号, "bool") {
    private string PredicateFuncType => $"global::System.Func<{source.ElementType}, int, bool>";
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => FuncSupport(predicate,nameof(predicate));
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression =>
        $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(predicate)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {e}{ClosurePrimaryCtorSuffix(predicate)}";
    protected override string ComputeExpression =>
        $"Compute({e}{ClosureArgsSuffix(predicate)})";
    protected override string ComputeMembers => $$"""
        {{AggressiveInliningAttribute}}
        private static bool Compute(TEnum source{{ClosureMethodParameterSuffix(predicate)}}) {
            var {{a}}=0;
            while(source.MoveNext()) {
                var {{item}}=source.Current;
                bool {{temp}}=default!;
                {{FuncAssign(predicate,nameof(predicate),temp,item,a)}}
                if({{temp}}) return true;
                {{a}}++;
            }
            return false;
        }
        """;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
}
