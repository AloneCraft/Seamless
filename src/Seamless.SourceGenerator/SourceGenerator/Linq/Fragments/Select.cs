namespace Seamless.SourceGenerator.Linq.Fragments;
public class Select(int 番号,Enumerable source,FuncFragment selector):Enumerable(番号){
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        !(method.Parameters.Length > 0 &&
          method.Parameters[0].Type is INamedTypeSymbol { TypeArguments.Length: 3 });
    protected override Enumerable PrimarySource => source;
    // static ラムダなら selector helper を struct として定義する。
    public override string StructMethod => selector.CanInlineAssign ? string.Empty : this.DefineStruct(selector,nameof(selector));
    // source 側 enumerator と Select 自身の enumerator をまとめて出す。
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    // Carrier/Enumerator モードでは source enumerable と selector delegate を渡す。
    // NeedsStructClosure: 非 static ラムダ + ContainingTypeName あり → @this を primary ctor で渡してインライン展開。
    // CanInlineAssign && CanGenerateMethod: static ラムダ → source のみ。
    // それ以外 (ParameterFunc または非 static ラムダで closure なし): デリゲートを ctor に渡す。
    public override string GetEnumeratorExpression =>
        NeedsStructClosure(selector)
            ? $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}, {this.ClosureArgsSuffix(selector)})"
            : selector.CanInlineAssign && selector.CanGenerateMethod
                ? $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression})"
                : $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}, {this.FuncDelegate(selector,nameof(selector))})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        NeedsStructClosure(selector)
            ? $"TEnum {this.e}, {this.ClosurePrimaryCtorSuffix(selector)}"
            : selector.CanInlineAssign && selector.CanGenerateMethod
                ? $"TEnum {this.e}"
                : $"TEnum {this.e}, global::System.Func<{source.ElementType}, {this.ElementType}> selector";
    // selector と現在値を保持する。source enumerator は primary ctor backing field。
    public override string GetEnumeratorFields => $"private {this.ElementType} {this.currentValue} = default!;";
    // 1 要素読むごとに selector を適用して TResult を流す。
    public override string GetTryGetNextBody => $$"""
        if({{this.e}}.MoveNext()) {
            var {{this.item}} = {{this.e}}.Current;
            {{this.AssignSelectedValue}}
            current = {{this.currentValue}};
            return true;
        }
        current = default!;
        return false;
        """;
    // Current は selector 適用後の値。
    public override string GetCurrentExpression => this.currentValue;
    private string AssignSelectedValue => this.FuncAssign(selector,nameof(selector),this.currentValue,this.item);
    // Select の要素型は selector 戻り値型。
    public override string ElementType => FuncReturnTypeName(selector);
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
    public override string GetEnumeratorFields => $"private {this.ElementType} {this.currentValue} = default!;";
    public override string GetTryGetNextBody => $$"""
        if({{this.e}}.MoveNext()) {
            var {{outerParamName}} = {{this.e}}.Current;
            var {{this.item}} = {{scalarFrag.GetEnumeratorExpression}};
            {{this.item}}.TryGetNext(out {{this.currentValue}});
            {{this.item}}.Dispose();
            current = {{this.currentValue}};
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
        staticMethods = Fragment.JoinStaticMethods(outerSourceStaticMethods, innerStaticMethods);
        return true;
    }
}
