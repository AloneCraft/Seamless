namespace Tsavorite.SourceGenerator.Linq.Fragments;
public class Select_selector(int 番号,Enumerable source,FuncFragment selector):Enumerable(番号){
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        !(method.Parameters.Length > 0 &&
          method.Parameters[0].Type is INamedTypeSymbol { TypeArguments.Length: 3 });
    protected override Enumerable PrimarySource => source;
    private bool UseResultTypeParam => selector.RequiresResultTypeParameter;
    public override string ElementType => this.UseResultTypeParam ? "TResult" : FuncReturnTypeName(selector);
    protected override string StructTypeParameters =>
        this.UseResultTypeParam ? "<TEnum, TResult>" : base.StructTypeParameters;
    protected override string StructTypeConstraints =>
        this.UseResultTypeParam
            ? $"where TEnum : struct, global::System.Collections.Generic.IEnumerator<{source.ElementType}>, global::Tsavorite.Carrier.IValueEnumerator<{source.ElementType}>"
            : base.StructTypeConstraints;
    public override string EnumeratorTypeName =>
        this.UseResultTypeParam ? $"{this.Struct}<{source.EnumeratorTypeName}, TResult>" : base.EnumeratorTypeName;
    private string FactorySource => !this.UseResultTypeParam ? string.Empty : $$"""
        {{AggressiveInliningAttribute}}
        private static global::Tsavorite.Carrier.ValueEnumerable<{{this.Struct}}<TEnum, TResult>, TResult> {{this.FactoryMethodName}}<TEnum, TResult>(TEnum {{this.e}}{{this.ClosurePrimaryCtorSuffix(selector)}})
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<{{source.ElementType}}>, global::Tsavorite.Carrier.IValueEnumerator<{{source.ElementType}}>
            => new(new {{this.Struct}}<TEnum, TResult>({{this.e}}{{this.ClosureArgsSuffixForFactory(selector)}}));
        """;
    public override string StructMethod => JoinStaticMethods(this.FuncSupport(selector,nameof(selector)),this.FactorySource);
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression =>
        $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}{this.ClosureArgsSuffix(selector)})";
    public override string GetEnumerableExpression =>
        this.UseResultTypeParam
            ? $"{this.FactoryMethodName}({source.GetEnumeratorExpression}{this.ClosureArgsSuffix(selector)})"
            : base.GetEnumerableExpression;
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {this.e}{this.ClosurePrimaryCtorSuffix(selector)}";
    public override string GetEnumeratorFields => string.Empty;
    public override string GetTryGetNextBody => $$"""
        if({{this.e}}.TryGetNext(out var {{this.item}})) {
            {{this.CounterCall("IncrementLogicalSelect")}}
            {{this.FuncAssignCurrent(selector,nameof(selector),$"this.{this.currentValue}",this.item)}}
            return true;
        }
        current = default!;
        return false;
        """;
    public override string GetCurrentExpression => this.currentValue;
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
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}";
    public override string GetEnumeratorFields => string.Empty;
    public override string GetTryGetNextBody => $$"""
        if({{this.e}}.TryGetNext(out var {{outerParamName}})) {
            {{this.CounterCall("IncrementLogicalSelect")}}
            var {{this.item}} = {{scalarFrag.GetEnumeratorExpression}};
            {{this.item}}.TryGetNext(out var {{this.temp}});
            {{this.item}}.Dispose();
            this.{{this.currentValue}} = {{this.temp}};
            current = this.{{this.currentValue}};
            return true;
        }
        current = default!;
        return false;
        """;
    public override string GetCurrentExpression => this.currentValue;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression})";

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
    public override string ElementType => this.UseResultTypeParam ? "TResult" : FuncReturnTypeName(selector);
    protected override string StructTypeParameters =>
        this.UseResultTypeParam ? "<TEnum, TResult>" : base.StructTypeParameters;
    protected override string StructTypeConstraints =>
        this.UseResultTypeParam
            ? $"where TEnum : struct, global::System.Collections.Generic.IEnumerator<{source.ElementType}>, global::Tsavorite.Carrier.IValueEnumerator<{source.ElementType}>"
            : base.StructTypeConstraints;
    public override string EnumeratorTypeName =>
        this.UseResultTypeParam ? $"{this.Struct}<{source.EnumeratorTypeName}, TResult>" : base.EnumeratorTypeName;
    private string FactorySource => !this.UseResultTypeParam ? string.Empty : $$"""
        {{AggressiveInliningAttribute}}
        private static global::Tsavorite.Carrier.ValueEnumerable<{{this.Struct}}<TEnum, TResult>, TResult> {{this.FactoryMethodName}}<TEnum, TResult>(TEnum {{this.e}}{{this.ClosurePrimaryCtorSuffix(selector)}})
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<{{source.ElementType}}>, global::Tsavorite.Carrier.IValueEnumerator<{{source.ElementType}}>
            => new(new {{this.Struct}}<TEnum, TResult>({{this.e}}{{this.ClosureArgsSuffixForFactory(selector)}}));
        """;
    public override string StructMethod => JoinStaticMethods(this.FuncSupport(selector,nameof(selector)),this.FactorySource);
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression =>
        $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}{this.ClosureArgsSuffix(selector)})";
    public override string GetEnumerableExpression =>
        this.UseResultTypeParam
            ? $"{this.FactoryMethodName}({source.GetEnumeratorExpression}{this.ClosureArgsSuffix(selector)})"
            : $"global::Tsavorite.Carrier.ValueEnumerableExtensions.Select({source.GetEnumerableExpression}, {selector.DelegateConstructorArg ?? selector.RenderAsLambda()})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {this.e}{this.ClosurePrimaryCtorSuffix(selector)}";
    public override string GetEnumeratorFields => $"private int {this.a} = -1;";
    public override string GetTryGetNextBody => $$"""
        if({{this.e}}.TryGetNext(out var {{this.item}})) {
            {{this.CounterCall("IncrementLogicalSelect")}}
            this.{{this.a}}++;
            {{this.FuncAssignCurrent(selector,nameof(selector),$"this.{this.currentValue}",this.item,$"this.{this.a}")}}
            return true;
        }
        current = default!;
        return false;
        """;
    public override string GetCurrentExpression => this.currentValue;
}
