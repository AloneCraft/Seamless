namespace Seamless.SourceGenerator.Linq.Fragments;
public class Where(int 番号, Enumerable source, FuncFragment predicate) : Enumerable(番号) {
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        !(method.Parameters.Length > 0 &&
          method.Parameters[0].Type is INamedTypeSymbol { TypeArguments.Length: 3 });
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => FuncSupport(predicate,nameof(predicate));
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression =>
        $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(predicate)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {e}{ClosurePrimaryCtorSuffix(predicate)}";
    public override string GetEnumeratorFields => $"private {source.ElementType} {currentValue} = default!;";
    public override string GetTryGetNextBody => $$"""
        {{Start}}:
        if(!{{e}}.MoveNext()) {
            current = default!;
            return false;
        }
        var {{item}} = {{e}}.Current;
        bool {{temp}} = default!;
        {{FuncAssign(predicate,nameof(predicate),temp,item)}}
        if(!{{temp}})
            goto {{Skip}};
        {{currentValue}} = {{item}};
        current = {{currentValue}};
        return true;
        {{Skip}}:
        goto {{Start}};
        """;
    public override string GetCurrentExpression => currentValue;
    public override string ElementType => source.ElementType;
}

public class Where_WithInlinedScalarPredicate(int 番号, Enumerable source, Fragment scalarFrag, string outerParamName)
    : Enumerable(番号) {
    public static bool IsApplicableOverload(IMethodSymbol method) => false;
    public override string ElementType => source.ElementType;
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{scalarFrag.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {e}";
    public override string GetEnumeratorFields => $"private {ElementType} {currentValue} = default!;";
    public override string GetTryGetNextBody => $$"""
        while({{e}}.MoveNext()) {
            var {{outerParamName}} = {{e}}.Current;
            var {{item}} = {{scalarFrag.GetEnumeratorExpression}};
            {{item}}.TryGetNext(out var {{temp}});
            {{item}}.Dispose();
            if(!{{temp}}) continue;
            {{currentValue}} = {{outerParamName}};
            current = {{currentValue}};
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
        if(definition.Name != "Where"
            || definition.ContainingType.ToDisplayString() != "System.Linq.Enumerable")
            return false;
        if(invocation.ArgumentList.Arguments.Count != 1
            || invocation.Expression is not MemberAccessExpressionSyntax { Expression: var sourceExpression })
            return false;
        var predicateArg = invocation.ArgumentList.Arguments[0].Expression;
        if(predicateArg is not LambdaExpressionSyntax lambda) return false;
        if(!tryGetLambdaBody(lambda, out var lambdaBody)) return false;
        var outerParams = getLambdaParameters(lambda);
        if(outerParams.Count != 1) return false;
        var outerParamName = outerParams[0];
        if(lambdaBody is not InvocationExpressionSyntax innerInvocation) return false;
        if(semanticModel.GetSymbolInfo(innerInvocation).Symbol is not IMethodSymbol innerMethod) return false;
        if((innerMethod.ReducedFrom ?? innerMethod).ContainingType.ToDisplayString() != "System.Linq.Enumerable") return false;
        var snapshot = uniqueNumber;
        var whereNumber = snapshot++;
        if(!buildEnumerable(semanticModel, sourceExpression, containingType, ref snapshot, out var outerSource, out var outerSourceStaticMethods, unsafeAccessorCollector))
            return false;
        if(!buildInvocation(semanticModel, innerInvocation, containingType, ref snapshot, out var innerFrag, out var innerStaticMethods, unsafeAccessorCollector))
            return false;
        if(innerFrag is not Scalar)
            return false;
        var whereFrag = new Where_WithInlinedScalarPredicate(whereNumber, outerSource, innerFrag, outerParamName);
        fragment = whereFrag;
        uniqueNumber = snapshot;
        staticMethods = JoinStaticMethods(outerSourceStaticMethods, innerStaticMethods);
        return true;
    }
}

public class Where_index(int 番号, Enumerable source, FuncFragment predicate) : Where(番号, source, predicate) {
    public static new bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length > 0 &&
        method.Parameters[0].Type is INamedTypeSymbol { TypeArguments.Length: 3 };
    public override string GetEnumeratorExpression =>
        $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(predicate)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {e}{ClosurePrimaryCtorSuffix(predicate)}";
    public override string GetEnumeratorFields => $$"""
        private {{source.ElementType}} {{currentValue}} = default!;
        private int {{a}} = -1;
        """;
    public override string GetTryGetNextBody => $$"""
        {{Start}}:
        if(!{{e}}.MoveNext()) {
            current = default!;
            return false;
        }
        var {{item}} = {{e}}.Current;
        {{a}}++;
        bool {{temp}} = default!;
        {{FuncAssign(predicate,nameof(predicate),temp,item,a)}}
        if(!{{temp}})
            goto {{Skip}};
        {{currentValue}} = {{item}};
        current = {{currentValue}};
        return true;
        {{Skip}}:
        goto {{Start}};
        """;
}
