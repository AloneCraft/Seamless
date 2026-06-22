namespace Seamless.FLinq.Linq.Fragments;
/*
Where_predicate は source.Where(item => 条件) を「条件に合うまで source を進める」状態機械へ変換する。

入力例:
  source.Where(x => x.Price > limit)

constructor 引数:
  - source: 上流の Enumerable Fragment。
  - predicate: item だけを受ける FuncFragment。inline 可能なら if 条件へ直接展開される。

生成例:
  Start:
  if(!source.TryGetNext(out var item)) return false;
  if(!predicate(item)) goto Start;
  current = item;
  return true;

バリエーション:
  - predicate がキャプチャを持つ場合、必要な値だけ Enumerator constructor に渡して this.capture 経由で使う。
  - predicate が匿名型や delegate 保持対象なら this.predicateDelegate.Invoke(item) になる。
  - Where_indexPredicate は index をフィールドで増やし、predicate(item, index) を呼ぶ。
*/
public class Where_predicate(int 番号, Enumerable source, FuncFragment predicate) : Enumerable(番号) {
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        !(method.Parameters.Length > 0 &&
          method.Parameters[0].Type is INamedTypeSymbol { TypeArguments.Length: 3 });
    protected override Enumerable PrimarySource => source;
    internal Enumerable Source => source;
    internal FuncFragment Predicate => predicate;
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

/*
Where_WithInlinedScalarPredicate は predicate 本体が LINQ scalar を返す特殊形を内側 Fragment として展開する。

入力例:
  people.Where(p => p.Children.Any(c => c.Age > 10))

生成例:
  while(source.TryGetNext(out var p)) {
      using var inner = childrenOfP.Any(...).GetEnumerator();
      inner.TryGetNext(out var keep);
      if(!keep) continue;
      current = p;
      return true;
  }

設計意図:
  lambda 内の Any/Count などを System.Linq 呼び出しのまま残さず、外側 item を使う内側 Fragment として再帰的に生成する。
*/
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

/*
Where_indexPredicate は source.Where((item, index) => 条件) 用。

生成例:
  index++;
  if(!predicate(item, index)) goto Start;

通常版との差分:
  index フィールドを -1 で持ち、source から要素を1つ読むたびに checked なしで増やして predicate に渡す。
*/
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
