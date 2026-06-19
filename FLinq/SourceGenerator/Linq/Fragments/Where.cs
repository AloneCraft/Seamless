namespace Tsavorite.SourceGenerator.Linq.Fragments;
public class Where_predicate(int 番号, Enumerable source, FuncFragment predicate) : Enumerable(番号) {
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        !(method.Parameters.Length > 0 &&
          method.Parameters[0].Type is INamedTypeSymbol { TypeArguments.Length: 3 });
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => this.FuncSupport(predicate,nameof(predicate));
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression =>
        $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}{this.ClosureArgsSuffix(predicate)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {this.e}{this.ClosurePrimaryCtorSuffix(predicate)}";
    public override string GetEnumeratorFields => string.Empty;
    public override string GetTryGetNextBody {
        get {
            var (preCode, condExpr) =this.FuncInvokeCondition(predicate, nameof(predicate),this.temp,this.item);
            var predicateCheck = string.IsNullOrEmpty(preCode)
                ? $"if(!{WrapConditionExpression(condExpr)})"
                : $"{preCode}\n        if(!{WrapConditionExpression(condExpr)})";

            return $$"""
                {{this.Start}}:
                if(!{{this.e}}.TryGetNext(out var {{this.item}})) {
                    current = default!;
                    return false;
                }
                {{this.CounterCall("IncrementLogicalWhere")}}
                {{predicateCheck}}
                    goto {{this.Skip}};
                current = this.{{this.currentValue}} = {{this.item}};
                return true;
                {{this.Skip}}:
                goto {{this.Start}};
                """;
        }
    }
    public override string GetCurrentExpression => this.currentValue;
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
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}";
    public override string GetEnumeratorFields => string.Empty;
    public override string GetTryGetNextBody => $$"""
        while({{this.e}}.TryGetNext(out var {{outerParamName}})) {
            {{this.CounterCall("IncrementLogicalWhere")}}
            var {{this.item}} = {{scalarFrag.GetEnumeratorExpression}};
            {{this.item}}.TryGetNext(out var {{this.temp}});
            {{this.item}}.Dispose();
            if(!{{this.temp}}) continue;
            this.{{this.currentValue}} = {{outerParamName}};
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

public class Where_indexPredicate(int 番号, Enumerable source, FuncFragment predicate) : Where_predicate(番号, source, predicate) {
    public static new bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length > 0 &&
        method.Parameters[0].Type is INamedTypeSymbol { TypeArguments.Length: 3 };
    public override string GetEnumeratorExpression =>
        $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}{this.ClosureArgsSuffix(predicate)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {this.e}{this.ClosurePrimaryCtorSuffix(predicate)}";
    public override string GetEnumeratorFields => $$"""
        private int {{this.a}} = -1;
        """;
    public override string GetTryGetNextBody {
        get {
            var (preCode, condExpr) =this.FuncInvokeCondition(predicate, nameof(predicate),this.temp,this.item,this.a);
            var predicateCheck = string.IsNullOrEmpty(preCode)
                ? $"if(!{WrapConditionExpression(condExpr)})"
                : $"{preCode}\n        if(!{WrapConditionExpression(condExpr)})";

            return $$"""
                {{this.Start}}:
                if(!{{this.e}}.TryGetNext(out var {{this.item}})) {
                    current = default!;
                    return false;
                }
                {{this.CounterCall("IncrementLogicalWhere")}}
                this.{{this.a}}++;
                {{predicateCheck}}
                    goto {{this.Skip}};
                current = this.{{this.currentValue}} = {{this.item}};
                return true;
                {{this.Skip}}:
                goto {{this.Start}};
                """;
        }
    }
}
