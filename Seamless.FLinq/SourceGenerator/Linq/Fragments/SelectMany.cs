namespace Seamless.FLinq.Linq.Fragments;
/*
以前の loop 展開版の断片は記録として残すが、実際の実装は Enumerator ベースに統一した。
*/

/*
SelectManyBase は外側 source と、各外側要素から作る内側 sequence を平坦化する状態機械の共通基底。

入力例:
  outer.SelectMany(o => o.Children)

生成例:
  Start:
  if(innerEnumerator != null && innerEnumerator.MoveNext()) {
      current = innerEnumerator.Current;
      return true;
  }
  DisposeInnerIfNeeded();
  if(!outer.MoveNext()) return false;
  outerValue = outer.Current;
  innerEnumerator = collectionSelector(outerValue).GetEnumerator();
  goto Start;

バリエーション:
  - resultSelector 付きは current = resultSelector(outerValue, innerValue) を生成する。
  - index selector 付きは外側要素を読むたびに index を増やす。
  - SelectMany_WithInlinedInner は内側 LINQ チェーンを具象 struct enumerator のまま保持する。
*/
public abstract class SelectManyBase(int 番号, Enumerable source) : Enumerable(番号) {
    internal static bool TryBuildInlined(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol containingType,
        ref int uniqueNumber,
        out Fragment fragment,
        out string staticMethods,
        List<string>? unsafeAccessorCollector,
        BuildEnumerableFragmentDelegate buildEnumerable,
        TryGetLambdaBodyDelegate tryGetLambdaBody,
        GetLambdaParametersDelegate getLambdaParameters) {
        fragment = null!;
        staticMethods = string.Empty;
        if(semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method) return false;
        var definition = method.ReducedFrom ?? method;
        if(definition.Name != "SelectMany"
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
        var dataFlow = semanticModel.AnalyzeDataFlow(lambdaBody);
        if(dataFlow?.Succeeded == true
            && dataFlow.DataFlowsIn.Any(symbol =>
                (symbol is ILocalSymbol || symbol is IParameterSymbol)
                && symbol.Name != outerParamName
                && symbol.Name != "this")) {
            return false;
        }
        var snapshot = uniqueNumber;
        var smNumber = snapshot++;
        if(!buildEnumerable(semanticModel, sourceExpression, containingType, ref snapshot, out var outerSource, out var outerSourceStaticMethods, unsafeAccessorCollector))
            return false;
        if(!buildEnumerable(semanticModel, lambdaBody, containingType, ref snapshot, out var innerFrag, out var innerStaticMethods, unsafeAccessorCollector))
            return false;
        // 要素型が TResult ジェネリックパラメータのままだと SelectMany_WithInlinedInner のアウタ struct で
        // 型解決できないため、通常の SelectMany デリゲートパスへフォールバックする。
        // 匿名型レジストリ有効時は内側ラムダが record struct 置換されて具象型名になるため通常ここは通らず、
        // registry-mapped できないラムダ形（本体ルートが匿名生成式でない等）の安全網として残している。
        if(innerFrag.ElementType == "TResult")
            return false;
        var smFrag = new SelectMany_WithInlinedInner(smNumber, outerSource, innerFrag, outerParamName);
        fragment = smFrag;
        uniqueNumber = snapshot;
        staticMethods = JoinStaticMethods(outerSourceStaticMethods, innerStaticMethods);
        staticMethods = JoinStaticMethods(staticMethods, smFrag.StructMethod);
        return true;
    }
    protected override Enumerable PrimarySource => source;
    protected abstract string CollectionElementType { get; }
    protected abstract string CollectionSelectorPrimaryConstructorParameter { get; }
    protected abstract string CreateInnerEnumeratorExpression { get; }
    protected virtual string MoveNextOuterPrologue => string.Empty;
    protected abstract string CurrentValueAssignmentFromInner { get; }

    public abstract override string StructMethod { get; }
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}, {this.CollectionSelectorPrimaryConstructorParameter}";
    public override string GetEnumeratorFields => $$"""
        private global::System.Collections.Generic.IEnumerator<{{this.CollectionElementType}}>? innerEnumerator = null;
        private {{source.ElementType}} {{this.outerValue}} = default!;
        """;
    public override string GetTryGetNextBody => $$"""
        {{this.Start}}:
        if(this.innerEnumerator is not null) {
            if(this.innerEnumerator.MoveNext()) {
                {{this.CounterCall("IncrementLogicalSelectManyInner")}}
                var {{this.item}} = this.innerEnumerator.Current;
                {{this.CurrentValueAssignmentFromInner}}
                return true;
            }
            this.innerEnumerator.Dispose();
            this.innerEnumerator = null;
        }
        if(!{{this.e}}.MoveNext()) {
            current = default!;
            return false;
        }
        {{this.MoveNextOuterPrologue}}
        {{this.CounterCall("IncrementLogicalSelectManyOuter")}}
        this.{{this.outerValue}} = {{this.e}}.Current;
        this.innerEnumerator = {{this.CreateInnerEnumeratorExpression}};
        goto {{this.Start}};
        """;
    public override string GetCurrentExpression => this.currentValue;
    protected override string GetEnumeratorDisposeBody => $$"""
        if(this.innerEnumerator is not null) {
            this.innerEnumerator.Dispose();
        }
        {{this.e}}.Dispose();
        """;
}

/*
SelectMany_selector は collection selector だけを持つ標準形。

入力例:
  source.SelectMany(x => x.Items)

生成分岐:
  - selector が外側 item に依存しない場合、innerEnumerable0 を constructor に渡して使い回す。
  - selector が item に依存する場合、各 outerValue ごとに selector(outerValue).GetEnumerator() を作る。
  - 匿名型など delegate 保持が必要な場合は selectorDelegate.Invoke(outerValue).GetEnumerator() にする。
*/
public class SelectMany_selector(int 番号, Enumerable source, FuncFragment selector) : SelectManyBase(番号, source) {
    private string SelectorFuncType => $"global::System.Func<{source.ElementType}, global::System.Collections.Generic.IEnumerable<{this.CollectionElementType}>>";
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length == 1 &&
        !(method.Parameters[0].Type is INamedTypeSymbol { TypeArguments.Length: 3 });
    private bool UseResultTypeParam => selector.RequiresResultTypeParameter;
    private bool IsHoistable =>
        !selector.CanInvokeAsDelegate &&
        selector.CanInlineAssign &&
        !selector.RenderInlineExpression("__TsavoriteHoistProbe__").Contains("__TsavoriteHoistProbe__") &&
        !selector.ContainsAnchor;
    protected override string CollectionElementType =>
        this.UseResultTypeParam ? "TResult" : SequenceElementTypeName(FuncReturnTypeName(selector));
    public override string ElementType => this.CollectionElementType;
    protected override string StructTypeParameters =>
        this.UseResultTypeParam ? "<TEnum, TResult>" : base.StructTypeParameters;
    protected override string StructTypeConstraints =>
        this.UseResultTypeParam
            ? $"where TEnum : struct, global::System.Collections.Generic.IEnumerator<{source.ElementType}>"
            : base.StructTypeConstraints;
    public override string EnumeratorTypeName =>
        this.UseResultTypeParam ? $"{this.Struct}<{source.EnumeratorTypeName}, TResult>" : base.EnumeratorTypeName;
    private string FactorySource => !this.UseResultTypeParam ? string.Empty : $$"""
        {{AggressiveInliningAttribute}}
        private static global::Seamless.Linq.ValueEnumerable<{{this.Struct}}<TEnum, TResult>, TResult> {{this.FactoryMethodName}}<TEnum, TResult>(TEnum {{this.e}}{{this.ClosurePrimaryCtorSuffix(selector)}})
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<{{source.ElementType}}>
            => new(new {{this.Struct}}<TEnum, TResult>({{this.e}}{{this.ClosureArgsSuffixForFactory(selector)}}));
        """;
    public override string StructMethod =>
        this.UseResultTypeParam
            ? JoinStaticMethods(selector.SupportingMemberSources,this.FactorySource)
            : this.IsHoistable ? selector.SupportingMemberSources : this.FuncSupport(selector, nameof(selector));
    public override string GetEnumeratorExpression =>
        this.IsHoistable
            ? $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}, {selector.RenderInlineExpression("__unused__")})"
            : $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}{this.ClosureArgsSuffix(selector)})";
    public override string GetEnumerableExpression =>
        this.UseResultTypeParam
            ? $"{this.FactoryMethodName}({source.GetEnumeratorExpression}{this.ClosureArgsSuffix(selector)})"
            : base.GetEnumerableExpression;
    public override string GetEnumeratorPrimaryConstructorParameters =>
        this.IsHoistable
            ? $"TEnum {this.e}, global::System.Collections.Generic.IEnumerable<{this.CollectionElementType}> innerEnumerable0"
            : $"TEnum {this.e}{this.ClosurePrimaryCtorSuffix(selector)}";
    protected override string CollectionSelectorPrimaryConstructorParameter =>
        this.IsHoistable
            ? $"global::System.Collections.Generic.IEnumerable<{this.CollectionElementType}> innerEnumerable0"
            : string.Empty;
    protected override string CreateInnerEnumeratorExpression =>
        selector.CanInvokeAsDelegate
            ? $"{selector.DelegateFieldName}.Invoke({this.outerValue}).GetEnumerator()"
            : this.IsHoistable
                ? "innerEnumerable0.GetEnumerator()"
                : $"((global::System.Collections.Generic.IEnumerable<{this.CollectionElementType}>){selector.RenderInlineExpressionWithHoistedReceiver("this.",this.outerValue)}).GetEnumerator()";
    protected override string CurrentValueAssignmentFromInner => $"current = this.{this.currentValue} = {this.item};";
}

public class SelectMany_indexSelector(int 番号, Enumerable source, FuncFragment selector) : SelectManyBase(番号, source) {
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length == 1 &&
        method.Parameters[0].Type is INamedTypeSymbol { TypeArguments.Length: 3 };
    public override string StructMethod => this.FuncSupport(selector,nameof(selector));
    public override string GetEnumeratorExpression =>
        $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}{this.ClosureArgsSuffix(selector)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {this.e}{this.ClosurePrimaryCtorSuffix(selector)}";
    protected override string CollectionSelectorPrimaryConstructorParameter => string.Empty;
    public override string GetEnumeratorFields => $$"""
        {{base.GetEnumeratorFields}}
        private int {{this.a}} = -1;
        """;
    protected override string MoveNextOuterPrologue => $"this.{this.a}++;";
    protected override string CreateInnerEnumeratorExpression =>
        $"((global::System.Collections.Generic.IEnumerable<{this.CollectionElementType}>){selector.RenderInlineExpressionWithHoistedReceiver("this.",this.outerValue,this.a)}).GetEnumerator()";
    protected override string CurrentValueAssignmentFromInner => $"current = this.{this.currentValue} = {this.item};";
    protected override string CollectionElementType => SequenceElementTypeName(FuncReturnTypeName(selector));
    public override string ElementType => this.CollectionElementType;
}

public class SelectMany_selector_resultSelector(int 番号, Enumerable source, FuncFragment collectionSelector, FuncFragment resultSelector) : SelectManyBase(番号, source) {
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length == 2 &&
        !(method.Parameters[0].Type is INamedTypeSymbol { TypeArguments.Length: 3 });
    public override string StructMethod => $$"""
        {{this.FuncSupport(collectionSelector,nameof(collectionSelector))}}
        {{this.FuncSupport(resultSelector,nameof(resultSelector))}}
        """;
    public override string GetEnumeratorExpression =>
        $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}{this.ClosureArgsSuffix(collectionSelector,resultSelector)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {this.e}{this.ClosurePrimaryCtorSuffix(collectionSelector,resultSelector)}";
    public override string GetEnumeratorFields => $$"""
        {{base.GetEnumeratorFields}}
        """;
    protected override string CollectionSelectorPrimaryConstructorParameter => string.Empty;
    protected override string CreateInnerEnumeratorExpression =>
        $"((global::System.Collections.Generic.IEnumerable<{this.CollectionElementType}>){collectionSelector.RenderInlineExpressionWithHoistedReceiver("this.",this.outerValue)}).GetEnumerator()";
    protected override string CurrentValueAssignmentFromInner =>
        this.FuncAssignCurrent(resultSelector,nameof(resultSelector),$"this.{this.currentValue}",this.outerValue,this.item);
    protected override string CollectionElementType => SequenceElementTypeName(FuncReturnTypeName(collectionSelector));
    public override string ElementType => FuncReturnTypeName(resultSelector);
}

/// <summary>
/// selector の本体 LINQ チェーンを struct のまま展開する SelectMany。
/// <br/>IEnumerator&lt;T&gt;? ではなく具象 struct 型フィールドを使うことで、
/// インターフェースディスパッチを排除し、毎外要素でのデリゲート呼び出しもなくす。
/// </summary>
/*
SelectMany_WithInlinedInner は collection selector の内側 LINQ を Fragment として展開できた場合の高速形。

入力例:
  outer.SelectMany(o => o.Children.Where(c => c.Active).Select(c => c.Name))

生成例:
  private InnerStruct innerEnumerator;
  private bool hasInner;
  if(hasInner && innerEnumerator.TryGetNext(out var item)) return item;
  if(!outer.MoveNext()) return false;
  innerEnumerator = new InnerStruct(outerValue を差し込んだ source);
  hasInner = true;

設計意図:
  innerEnumerator を IEnumerator<T>? にせず具象 struct 型で持つことで、interface dispatch と delegate 呼び出しを避ける。
*/
public class SelectMany_WithInlinedInner(int 番号, Enumerable source, Enumerable innerFragment, string outerParamName)
    : SelectManyBase(番号, source) {

    public static bool IsApplicableOverload(IMethodSymbol method) => false;
    private string InnerEnumeratorTypeName => innerFragment.EnumeratorTypeName;

    public override string StructMethod => string.Empty;

    // outer source structs + inner chain structs + outer SelectMany struct
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{innerFragment.GetEnumeratorStruct}}
        {{this.DefineEnumeratorStructCore(this.ElementType,this.GetEnumeratorDisposeBody)}}
        """;

    // No selector parameter — outer struct only needs the outer source enumerator
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}";

    // Use concrete struct type + bool flag instead of IEnumerator<T>?
    public override string GetEnumeratorFields => $$"""
        private {{this.InnerEnumeratorTypeName}} innerEnumerator = default!;
        private bool {{this.hasValue}} = false;
        private {{source.ElementType}} {{this.outerValue}} = default!;
        """;

    // bool flag replaces the null check
    public override string GetTryGetNextBody => $$"""
        {{this.Start}}:
        if(this.{{this.hasValue}}) {
            if(this.innerEnumerator.TryGetNext(out var {{this.item}})) {
                {{this.CounterCall("IncrementLogicalSelectManyInner")}}
                {{this.CurrentValueAssignmentFromInner}}
                return true;
            }
            this.innerEnumerator.Dispose();
            this.{{this.hasValue}} = false;
        }
        if(!{{this.e}}.MoveNext()) {
            current = default!;
            return false;
        }
        {{this.CounterCall("IncrementLogicalSelectManyOuter")}}
        this.{{this.outerValue}} = {{this.e}}.Current;
        this.innerEnumerator = {{this.CreateInnerEnumeratorExpression}};
        this.{{this.hasValue}} = true;
        goto {{this.Start}};
        """;

    protected override string GetEnumeratorDisposeBody => $$"""
        if(this.{{this.hasValue}}) {
            this.innerEnumerator.Dispose();
        }
        {{this.e}}.Dispose();
        """;

    // Instantiate the inner struct, substituting outer param name with the outerValue field.
    protected override string CreateInnerEnumeratorExpression =>
        System.Text.RegularExpressions.Regex.Replace(
            innerFragment.GetEnumeratorExpression,
            $@"(?<![a-zA-Z0-9_]){System.Text.RegularExpressions.Regex.Escape(outerParamName)}(?![a-zA-Z0-9_])",
            this.outerValue);

    // Required by SelectManyBase but not used (GetEnumeratorPrimaryConstructorParameters is overridden)
    protected override string CollectionSelectorPrimaryConstructorParameter => string.Empty;
    protected override string CurrentValueAssignmentFromInner => $"current = this.{this.currentValue} = {this.item};";
    protected override string CollectionElementType => innerFragment.ElementType;
    public override string ElementType => innerFragment.ElementType;

    // GetEnumeratorExpression: outer struct takes only the outer source enumerator
    public override string GetEnumeratorExpression =>
        $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression})";
}

public class SelectMany_indexSelector_resultSelector(int 番号, Enumerable source, FuncFragment collectionSelector, FuncFragment resultSelector) : SelectManyBase(番号, source) {
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length == 2 &&
        method.Parameters[0].Type is INamedTypeSymbol { TypeArguments.Length: 3 };
    public override string StructMethod => $$"""
        {{this.FuncSupport(collectionSelector,nameof(collectionSelector))}}
        {{this.FuncSupport(resultSelector,nameof(resultSelector))}}
        """;
    public override string GetEnumeratorExpression =>
        $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}{this.ClosureArgsSuffix(collectionSelector,resultSelector)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {this.e}{this.ClosurePrimaryCtorSuffix(collectionSelector,resultSelector)}";
    public override string GetEnumeratorFields => $$"""
        {{base.GetEnumeratorFields}}
        private int {{this.a}} = -1;
        """;
    protected override string CollectionSelectorPrimaryConstructorParameter => string.Empty;
    protected override string MoveNextOuterPrologue => $"this.{this.a}++;";
    protected override string CreateInnerEnumeratorExpression =>
        $"((global::System.Collections.Generic.IEnumerable<{this.CollectionElementType}>){collectionSelector.RenderInlineExpressionWithHoistedReceiver("this.",this.outerValue,this.a)}).GetEnumerator()";
    protected override string CurrentValueAssignmentFromInner =>
        this.FuncAssignCurrent(resultSelector,nameof(resultSelector),$"this.{this.currentValue}",this.outerValue,this.item);
    protected override string CollectionElementType => SequenceElementTypeName(FuncReturnTypeName(collectionSelector));
    public override string ElementType => FuncReturnTypeName(resultSelector);
}
