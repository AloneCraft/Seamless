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
        if(invocation.Expression is not MemberAccessExpressionSyntax { Expression: var sourceExpression })
            return false;
        if(!buildEnumerable(semanticModel, sourceExpression, containingType, ref uniqueNumber, out var sourceFragment, out var sourceStaticMethods, unsafeAccessorCollector))
            return false;
        if(semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method)
            return false;
        if(invocation.ArgumentList.Arguments.Count == 1) {
            if(!buildFunc(invocation.ArgumentList.Arguments[0].Expression, method.Parameters[0].Type, ref uniqueNumber, out var predicate, semanticModel, containingType))
                return false;
            var whereVariantName = predicate.ParameterTypeNames.Length > 1 ? "index" : string.Empty;
            var whereFragment = createFragment("Where", whereVariantName, uniqueNumber++, [sourceFragment, predicate]);
            if(whereFragment is not Enumerable whereEnumerable)
                return false;
            sourceStaticMethods = Fragment.JoinStaticMethods(sourceStaticMethods, whereFragment.StructMethod);
            sourceFragment = whereEnumerable;
        }
        var anyFragment = createFragment("Any", string.Empty, uniqueNumber++, [sourceFragment]);
        if(anyFragment is null)
            return false;
        fragment = anyFragment;
        staticMethods = Fragment.JoinStaticMethods(sourceStaticMethods, fragment.StructMethod);
        return true;
    }
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression})";
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{this.DefineScalarEnumeratorStruct("bool")}}
        """;
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}";
    public override string GetEnumeratorFields => $$"""
        private bool {{this.currentValue}} = false;
        private bool {{this.yielded}} = false;
        """;
    public override string GetTryGetNextBody => $$"""
        if({{this.yielded}}) {
            current = default;
            return false;
        }
        {{this.yielded}} = true;
        {{this.currentValue}} = {{this.e}}.MoveNext();
        current = {{this.currentValue}};
        return true;
        """;
    public override string GetCurrentExpression => this.currentValue;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}
