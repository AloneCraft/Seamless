namespace Seamless.FLinq;
/*
Select_selector は source.Select(item => 値) を「source から1件読み、selector の結果を Current に入れる」
enumerator へ変換する。

入力例:
  source.Select(x => x.Price * tax)

constructor 引数:
  - source: 上流 Enumerable Fragment。
  - selector: item を result へ写像する FuncFragment。

生成例:
  if(source.TryGetNext(out var item)) {
      current = selector(item);
      return true;
  }
  return false;

バリエーション:
  - selector が inline 可能なら current = item.Price * this.tax; のように式展開される。
  - 匿名型戻り値など型名を書けない場合は TResult factory を生成し、ValueEnumerable<Struct<TEnum,TResult>,TResult> で閉じる。
  - Select_indexSelector は index フィールドを増やして selector(item, index) を呼ぶ。
*/
internal class Select_selector(int 番号,Enumerable source,FuncFragment selector):Enumerable(番号) {
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        !(method.Parameters.Length>0&&
          method.Parameters[0].Type is INamedTypeSymbol { TypeArguments.Length: 3 });
    protected override Enumerable PrimarySource => source;
    private bool UseResultTypeParam => selector.RequiresResultTypeParameter;
    internal Enumerable Source => source;
    internal FuncFragment Selector => selector;
    internal bool RequiresResultTypeParameter => this.UseResultTypeParam;
    public override string ElementType => this.UseResultTypeParam ? "TResult" : FuncReturnTypeName(selector);
    protected override string StructTypeParameters =>
        this.UseResultTypeParam ? "<TEnum, TResult>" : base.StructTypeParameters;
    protected override string StructTypeConstraints =>
        this.UseResultTypeParam
            ? $"where TEnum : struct, global::Seamless.Linq.IValueEnumerator<{source.ElementType}>"
            : base.StructTypeConstraints;
    public override string EnumeratorTypeName =>
        this.UseResultTypeParam ? $"{this.Struct}<{source.EnumeratorTypeName}, TResult>" : base.EnumeratorTypeName;
    private string FactorySource => !this.UseResultTypeParam ? string.Empty : $$"""
        {{AggressiveInliningAttribute}}
        private static global::Seamless.Linq.ValueEnumerable<{{this.Struct}}<TEnum, TResult>, TResult> {{this.FactoryMethodName}}<TEnum, TResult>(TEnum {{this.e}}{{this.ClosurePrimaryCtorSuffix(selector)}})
            where TEnum : struct, global::Seamless.Linq.IValueEnumerator<{{source.ElementType}}>
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
    public override string? NonEnumeratedCountExpression => source.NonEnumeratedCountExpression;
    public override bool CountPreserving => source.CountPreserving;
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {this.e}{this.ClosurePrimaryCtorSuffix(selector)}";
    public override string GetEnumeratorFields => string.Empty;
    protected override string GetTryGetNonEnumeratedCountBody => $"return this.{this.e}.TryGetNonEnumeratedCount(out count);";
    protected override string GetTryCopyToBody => $$"""
        if(!this.{{this.e}}.TryGetNonEnumeratedCount(out var __count)){
            return false;
        }
        var __start = __offset.GetOffset(__count);
        if(__start < 0 || __start > __count || destination.Length > __count - __start){
            return false;
        }
        if(this.{{this.e}}.TryGetSpan(out var __span)){
            for(var __i=0; __i<destination.Length; __i++){
                var {{this.item}} = __span[__start + __i];
                {{this.FuncAssignIndented("            ",selector,nameof(selector),"destination[__i]",this.item)}}
            }
            return true;
        }
        for(var __i=0; __i<__start; __i++){
            if(!this.{{this.e}}.TryGetNext(out _)){
                return false;
            }
        }
        for(var __i=0; __i<destination.Length; __i++){
            if(!this.{{this.e}}.TryGetNext(out var {{this.item}})){
                return false;
            }
            {{this.FuncAssignIndented("        ",selector,nameof(selector),"destination[__i]",this.item)}}
        }
        return true;
        """;
    public override string GetTryGetNextBody => $$"""
        if({{this.e}}.TryGetNext(out var {{this.item}})){
            {{this.CounterCall("IncrementLogicalSelect")}}
            {{this.FuncAssignCurrentIndented("    ",selector,nameof(selector),$"this.{this.currentValue}",this.item)}}
            return true;
        }
        current=default!;
        return false;
        """;
}

/*
Select_WithInlinedScalar は selector 本体が LINQ scalar を返すとき、内側の scalar Fragment をそのまま使う特殊形。

入力例:
  parents.Select(p => p.Children.Count(c => c.Active))

生成例:
  if(source.TryGetNext(out var p)) {
      using var inner = childrenOfP.Count(...).GetEnumerator();
      inner.TryGetNext(out var value);
      current = value;
      return true;
  }

設計意図:
  selector 内 LINQ を delegate 呼び出しに閉じ込めず、生成済み Fragment の TryGetNext で計算する。
*/
internal class Select_WithInlinedScalar(int 番号,Enumerable source,Fragment scalarFrag,string outerParamName,string elementType)
    :Enumerable(番号) {
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
    public override string? NonEnumeratedCountExpression => source.NonEnumeratedCountExpression;
    public override bool CountPreserving => source.CountPreserving;
    protected override string GetTryGetNonEnumeratedCountBody => $"return this.{this.e}.TryGetNonEnumeratedCount(out count);";
    public override string GetTryGetNextBody => $$"""
        if({{this.e}}.TryGetNext(out var {{outerParamName}})){
            {{this.CounterCall("IncrementLogicalSelect")}}
            var {{this.item}} = {{scalarFrag.GetEnumeratorExpression}};
            {{this.item}}.TryGetNext(out var {{this.temp}});
            {{this.item}}.Dispose();
            this.{{this.currentValue}} = {{this.temp}};
            current = this.{{this.currentValue}};
            return true;
        }
        current=default!;
        return false;
        """;
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
        fragment=null!;
        staticMethods=string.Empty;
        if(semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method) return false;
        var definition = method.ReducedFrom??method;
        if(definition.Name!="Select"
            ||definition.ContainingType.ToDisplayString()!="System.Linq.Enumerable")
            return false;
        if(invocation.ArgumentList.Arguments.Count!=1
            ||invocation.Expression is not MemberAccessExpressionSyntax { Expression: var sourceExpression })
            return false;
        var selectorArg = invocation.ArgumentList.Arguments[0].Expression;
        if(selectorArg is not LambdaExpressionSyntax lambda) return false;
        if(!tryGetLambdaBody(lambda,out var lambdaBody)) return false;
        var outerParams = getLambdaParameters(lambda);
        if(outerParams.Count!=1) return false;
        var outerParamName = outerParams[0];
        if(lambdaBody is not InvocationExpressionSyntax innerInvocation) return false;
        if(semanticModel.GetSymbolInfo(innerInvocation).Symbol is not IMethodSymbol innerMethod) return false;
        if((innerMethod.ReducedFrom??innerMethod).ContainingType.ToDisplayString()!="System.Linq.Enumerable") return false;
        var snapshot = uniqueNumber;
        var selectNumber = snapshot++;
        if(!buildEnumerable(semanticModel,sourceExpression,containingType,ref snapshot,out var outerSource,out var outerSourceStaticMethods,unsafeAccessorCollector))
            return false;
        if(!buildInvocation(semanticModel,innerInvocation,containingType,ref snapshot,out var innerFrag,out var innerStaticMethods,unsafeAccessorCollector))
            return false;
        if(innerFrag is not Scalar)
            return false;
        if(semanticModel.GetTypeInfo(innerInvocation).Type is not ITypeSymbol innerType)
            return false;
        var elementType = innerType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var selectFrag = new Select_WithInlinedScalar(selectNumber,outerSource,innerFrag,outerParamName,elementType);
        fragment=selectFrag;
        uniqueNumber=snapshot;
        staticMethods=JoinStaticMethods(outerSourceStaticMethods,innerStaticMethods);
        return true;
    }
}

/*
Select_indexSelector は source.Select((item, index) => 値) 用。

生成例:
  if(source.TryGetNext(out var item)) {
      index++;
      current = selector(item, index);
      return true;
  }

通常版との差分:
  index を Enumerator フィールドとして保持し、各要素の selector 呼び出しに追加引数として渡す。
*/
internal class Select_indexSelector(int 番号,Enumerable source,FuncFragment selector):Enumerable(番号) {
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length>0&&
        method.Parameters[0].Type is INamedTypeSymbol { TypeArguments.Length: 3 };
    protected override Enumerable PrimarySource => source;
    private bool UseResultTypeParam => selector.RequiresResultTypeParameter;
    public override string ElementType => this.UseResultTypeParam ? "TResult" : FuncReturnTypeName(selector);
    protected override string StructTypeParameters =>
        this.UseResultTypeParam ? "<TEnum, TResult>" : base.StructTypeParameters;
    protected override string StructTypeConstraints =>
        this.UseResultTypeParam
            ? $"where TEnum : struct, global::Seamless.Linq.IValueEnumerator<{source.ElementType}>"
            : base.StructTypeConstraints;
    public override string EnumeratorTypeName =>
        this.UseResultTypeParam ? $"{this.Struct}<{source.EnumeratorTypeName}, TResult>" : base.EnumeratorTypeName;
    private string FactorySource => !this.UseResultTypeParam ? string.Empty : $$"""
        {{AggressiveInliningAttribute}}
        private static global::Seamless.Linq.ValueEnumerable<{{this.Struct}}<TEnum, TResult>, TResult> {{this.FactoryMethodName}}<TEnum, TResult>(TEnum {{this.e}}{{this.ClosurePrimaryCtorSuffix(selector)}})
            where TEnum : struct, global::Seamless.Linq.IValueEnumerator<{{source.ElementType}}>
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
            : $"global::Seamless.Linq.Enumerable.Select({source.GetEnumerableExpression}, {selector.DelegateConstructorArg??selector.RenderAsLambda()})";
    public override string? NonEnumeratedCountExpression => source.NonEnumeratedCountExpression;
    public override bool CountPreserving => source.CountPreserving;
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {this.e}{this.ClosurePrimaryCtorSuffix(selector)}";
    public override string GetEnumeratorFields => $"private int {this.a} = -1;";
    protected override string GetTryGetNonEnumeratedCountBody => $"return this.{this.e}.TryGetNonEnumeratedCount(out count);";
    public override string GetTryGetNextBody => $$"""
        if({{this.e}}.TryGetNext(out var {{this.item}})){
            {{this.CounterCall("IncrementLogicalSelect")}}
            this.{{this.a}}++;
            {{this.FuncAssignCurrentIndented("    ",selector,nameof(selector),$"this.{this.currentValue}",this.item,$"this.{this.a}")}}
            return true;
        }
        current=default!;
        return false;
        """;
}
