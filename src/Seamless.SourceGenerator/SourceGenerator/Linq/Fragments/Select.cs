namespace Seamless.SourceGenerator.Linq.Fragments;
public class Select_selector(int 番号,Enumerable source,FuncFragment selector):Enumerable(番号){
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        !(method.Parameters.Length > 0 &&
          method.Parameters[0].Type is INamedTypeSymbol { TypeArguments.Length: 3 });
    protected override Enumerable PrimarySource => source;
    private bool UseResultTypeParam => selector.RequiresResultTypeParameter;
    public override string ElementType => UseResultTypeParam ? "TResult" : FuncReturnTypeName(selector);
    protected override string StructTypeParameters =>
        UseResultTypeParam ? "<TEnum, TResult>" : base.StructTypeParameters;
    protected override string StructTypeConstraints =>
        UseResultTypeParam
            ? $"where TEnum : struct, global::System.Collections.Generic.IEnumerator<{source.ElementType}>"
            : base.StructTypeConstraints;
    public override string EnumeratorTypeName =>
        UseResultTypeParam ? $"{Struct}<{source.EnumeratorTypeName}, TResult>" : base.EnumeratorTypeName;
    private string FactorySource => !UseResultTypeParam ? string.Empty : $$"""
        {{AggressiveInliningAttribute}}
        private static global::Seamless.Linq.Carrier.ValueEnumerable<{{Struct}}<TEnum, TResult>, TResult> {{FactoryMethodName}}<TEnum, TResult>(TEnum {{e}}{{ClosurePrimaryCtorSuffix(selector)}})
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<{{source.ElementType}}>
            => new(new {{Struct}}<TEnum, TResult>({{e}}{{ClosureArgsSuffixForFactory(selector)}}));
        """;
    public override string StructMethod => JoinStaticMethods(FuncSupport(selector,nameof(selector)), FactorySource);
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression =>
        $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(selector)})";
    public override string GetEnumerableExpression =>
        UseResultTypeParam
            ? $"{FactoryMethodName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(selector)})"
            : base.GetEnumerableExpression;
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {e}{ClosurePrimaryCtorSuffix(selector)}";
    public override string GetEnumeratorFields => string.Empty;
    public override string GetTryGetNextBody => $$"""
        if({{e}}.MoveNext()) {
            var {{item}} = {{e}}.Current;
            {{FuncAssignCurrent(selector,nameof(selector),$"this.{currentValue}",item)}}
            return true;
        }
        current = default!;
        return false;
        """;
    public override string GetCurrentExpression => currentValue;
}

public class Select_WithInlinedScalar(int 番号, Enumerable source, Fragment scalarFrag, string outerParamName, string elementType)
    : Enumerable(番号) {
    public static bool IsApplicableOverload(IMethodSymbol method) => false;
    public override string ElementType => elementType;
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{scalarFrag.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {e}";
    public override string GetEnumeratorFields => string.Empty;
    public override string GetTryGetNextBody => $$"""
        if({{e}}.MoveNext()) {
            var {{outerParamName}} = {{e}}.Current;
            var {{item}} = {{scalarFrag.GetEnumeratorExpression}};
            {{item}}.TryGetNext(out var {{temp}});
            {{item}}.Dispose();
            this.{{currentValue}} = {{temp}};
            current = this.{{currentValue}};
            return true;
        }
        current = default!;
        return false;
        """;
    public override string GetCurrentExpression => currentValue;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
    public override string GetEnumeratorExpression => $"new {EnumeratorTypeName}({source.GetEnumeratorExpression})";

    internal static bool TryBuild(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol containingType,
        ref int uniqueNumber,
        out Fragment fragment,
        out string staticMethods,
        List<string>? unsafeAccessorCollector,
        BuildEnumerableFragmentDelegate buildEnumerable,
        BuildInvocationFragmentDelegate buildInvocation,
        TryGetLambdaBodyDelegate tryGetLambdaBody,
        GetLambdaParametersDelegate getLambdaParameters) {
        fragment = null!;
        staticMethods = string.Empty;
        if(semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method) return false;
        var definition = method.ReducedFrom ?? method;
        if(definition.Name != "Select"
            || definition.ContainingType.ToDisplayString() != "System.Linq.Enumerable")
            return false;
        if(invocation.ArgumentList.Arguments.Count != 1
            || invocation.Expression is not MemberAccessExpressionSyntax { Expression: var sourceExpression })
            return false;
        var selectorArg = invocation.ArgumentList.Arguments[0].Expression;
        if(selectorArg is not LambdaExpressionSyntax lambda) return false;
        if(!tryGetLambdaBody(lambda, out var lambdaBody)) return false;
        var outerParams = getLambdaParameters(lambda);
        if(outerParams.Count != 1) return false;
        var outerParamName = outerParams[0];
        if(lambdaBody is not InvocationExpressionSyntax innerInvocation) return false;
        if(semanticModel.GetSymbolInfo(innerInvocation).Symbol is not IMethodSymbol innerMethod) return false;
        if((innerMethod.ReducedFrom ?? innerMethod).ContainingType.ToDisplayString() != "System.Linq.Enumerable") return false;
        var snapshot = uniqueNumber;
        var selectNumber = snapshot++;
        if(!buildEnumerable(semanticModel, sourceExpression, containingType, ref snapshot, out var outerSource, out var outerSourceStaticMethods, unsafeAccessorCollector))
            return false;
        if(!buildInvocation(semanticModel, innerInvocation, containingType, ref snapshot, out var innerFrag, out var innerStaticMethods, unsafeAccessorCollector))
            return false;
        if(innerFrag is not Scalar)
            return false;
        if(semanticModel.GetTypeInfo(innerInvocation).Type is not ITypeSymbol innerType)
            return false;
        var elementType = innerType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var selectFrag = new Select_WithInlinedScalar(selectNumber, outerSource, innerFrag, outerParamName, elementType);
        fragment = selectFrag;
        uniqueNumber = snapshot;
        staticMethods = JoinStaticMethods(outerSourceStaticMethods, innerStaticMethods);
        return true;
    }
}

public class Select_indexSelector(int 番号, Enumerable source, FuncFragment selector) : Enumerable(番号) {
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length > 0 &&
        method.Parameters[0].Type is INamedTypeSymbol { TypeArguments.Length: 3 };
    protected override Enumerable PrimarySource => source;
    private bool UseResultTypeParam => selector.RequiresResultTypeParameter;
    public override string ElementType => UseResultTypeParam ? "TResult" : FuncReturnTypeName(selector);
    protected override string StructTypeParameters =>
        UseResultTypeParam ? "<TEnum, TResult>" : base.StructTypeParameters;
    protected override string StructTypeConstraints =>
        UseResultTypeParam
            ? $"where TEnum : struct, global::System.Collections.Generic.IEnumerator<{source.ElementType}>"
            : base.StructTypeConstraints;
    public override string EnumeratorTypeName =>
        UseResultTypeParam ? $"{Struct}<{source.EnumeratorTypeName}, TResult>" : base.EnumeratorTypeName;
    private string FactorySource => !UseResultTypeParam ? string.Empty : $$"""
        {{AggressiveInliningAttribute}}
        private static global::Seamless.Linq.Carrier.ValueEnumerable<{{Struct}}<TEnum, TResult>, TResult> {{FactoryMethodName}}<TEnum, TResult>(TEnum {{e}}{{ClosurePrimaryCtorSuffix(selector)}})
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<{{source.ElementType}}>
            => new(new {{Struct}}<TEnum, TResult>({{e}}{{ClosureArgsSuffixForFactory(selector)}}));
        """;
    public override string StructMethod => JoinStaticMethods(FuncSupport(selector,nameof(selector)), FactorySource);
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression =>
        $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(selector)})";
    public override string GetEnumerableExpression =>
        UseResultTypeParam
            ? $"{FactoryMethodName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(selector)})"
            : base.GetEnumerableExpression;
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {e}{ClosurePrimaryCtorSuffix(selector)}";
    public override string GetEnumeratorFields => $$"""
        private int {{a}} = -1;
        """;
    public override string GetTryGetNextBody => $$"""
        if({{e}}.MoveNext()) {
            var {{item}} = {{e}}.Current;
            this.{{a}}++;
            {{FuncAssignCurrent(selector,nameof(selector),$"this.{currentValue}",item,$"this.{a}")}}
            return true;
        }
        current = default!;
        return false;
        """;
    public override string GetCurrentExpression => currentValue;
}
