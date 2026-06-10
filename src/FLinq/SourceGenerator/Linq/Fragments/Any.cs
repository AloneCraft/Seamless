namespace Tsavorite.SourceGenerator.Linq.Fragments;
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
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression})";
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{this.DefineScalarEnumeratorStruct("bool")}}
        """;
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}";
    public override string GetEnumeratorFields => $$"""
        private bool {{this.yielded}} = false;
        """;
    public override string GetTryGetNextBody => $$"""
        if(this.{{this.yielded}}) {
            current = default;
            return false;
        }
        this.{{this.yielded}} = true;
        this.{{this.currentValue}} = {{this.e}}.MoveNext();
        current = this.{{this.currentValue}};
        return true;
        """;
    public override string GetCurrentExpression => this.currentValue;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}

public class Any_predicate(int 番号, Enumerable source, FuncFragment predicate) : BufferedScalar(番号, "bool") {
    private string PredicateFuncType => $"global::System.Func<{source.ElementType}, bool>";
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => this.FuncSupport(predicate,nameof(predicate));
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression =>
        $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}{this.ClosureArgsSuffix(predicate)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {this.e}{this.ClosurePrimaryCtorSuffix(predicate)}";
    protected override string ComputeExpression =>
        $"Compute({this.e}{this.ClosureArgsSuffix(predicate)})";
    protected override string ComputeMembers => source.CanProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static bool Compute(TEnum source{{this.ClosureMethodParameterSuffix(predicate)}}) {
                source.TryGetSpan(out var __span);
                foreach(var {{this.item}} in __span){
                    bool {{this.temp}}=default!;
                    {{this.FuncAssign(predicate,nameof(predicate),this.temp,this.item)}}
                    if({{this.temp}}) return true;
                }
                return false;
            }
            """
        : $$"""
            {{AggressiveInliningAttribute}}
            private static bool Compute(TEnum source{{this.ClosureMethodParameterSuffix(predicate)}}) {
                while(source.MoveNext()) {
                    var {{this.item}}=source.Current;
                    bool {{this.temp}}=default!;
                    {{this.FuncAssign(predicate,nameof(predicate),this.temp,this.item)}}
                    if({{this.temp}}) return true;
                }
                return false;
            }
            """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}

public class Any_indexPredicate(int 番号, Enumerable source, FuncFragment predicate) : BufferedScalar(番号, "bool") {
    private string PredicateFuncType => $"global::System.Func<{source.ElementType}, int, bool>";
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => this.FuncSupport(predicate,nameof(predicate));
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression =>
        $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}{this.ClosureArgsSuffix(predicate)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {this.e}{this.ClosurePrimaryCtorSuffix(predicate)}";
    protected override string ComputeExpression =>
        $"Compute({this.e}{this.ClosureArgsSuffix(predicate)})";
    protected override string ComputeMembers => $$"""
        {{AggressiveInliningAttribute}}
        private static bool Compute(TEnum source{{this.ClosureMethodParameterSuffix(predicate)}}) {
            var {{this.a}}=0;
            while(source.MoveNext()) {
                var {{this.item}}=source.Current;
                bool {{this.temp}}=default!;
                {{this.FuncAssign(predicate,nameof(predicate),this.temp,this.item,this.a)}}
                if({{this.temp}}) return true;
                {{this.a}}++;
            }
            return false;
        }
        """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}
