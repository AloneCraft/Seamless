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
            var whereVariantName = predicate.ParameterTypeNames.Length > 1 ? "indexPredicate" : "predicate";
            var whereFragment = createFragment("Where", whereVariantName, uniqueNumber++, [sourceFragment, predicate]);
            if(whereFragment is not Enumerable whereEnumerable)
                return false;
            sourceStaticMethods = JoinStaticMethods(sourceStaticMethods, whereFragment.StructMethod);
            sourceFragment = whereEnumerable;
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
