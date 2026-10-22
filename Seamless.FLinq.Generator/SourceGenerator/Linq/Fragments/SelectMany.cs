namespace Seamless.FLinq.Generator;
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
internal abstract class SelectManyBase(int 番号,Enumerable source):Enumerable(番号) {
    protected readonly Enumerable source = source;
    // Explicit Fragment child visitor
    protected override IEnumerable<Fragment> GetChildFragments() {
        foreach(var child in base.GetChildFragments()) {
                yield return child;
        }
        if(source is { } __visitorChild_source) {
            yield return __visitorChild_source;
        }
    }

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
        fragment=null!;
        staticMethods=string.Empty;
        if(semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method) return false;
        var definition = method.ReducedFrom??method;
        if(definition.Name!="SelectMany"
            ||definition.ContainingType.ToDisplayString()!="System.Linq.Enumerable")
            return false;
        if(invocation.ArgumentList.Arguments.Count!=1
            ||invocation.Expression is not MemberAccessExpressionSyntax { Expression: var sourceExpression })
            return false;
        var selectorArg = invocation.ArgumentList.Arguments[0].Expression;
        if(selectorArg is not LambdaExpressionSyntax lambda) return false;
        if(!tryGetLambdaBody(lambda,out var lambdaBody)) return false;
        var outerParams = getLambdaParameters(lambda);
        // 添字付き selector((x,i) => ...) も引数は1個なのでここまで来る。本体が添字を参照していれば
        // 下のデータフロー検査が outerParamName 以外の引数として弾くため、ここでは2引数まで許す。
        if(outerParams.Count is not (1 or 2)) return false;
        var outerParamName = outerParams[0];
        var dataFlow = semanticModel.AnalyzeDataFlow(lambdaBody);
        if(dataFlow?.Succeeded==true
            &&dataFlow.DataFlowsIn.Any(symbol =>
                (symbol is ILocalSymbol||symbol is IParameterSymbol)
                &&symbol.Name!=outerParamName
                &&symbol.Name!="this")) {
            return false;
        }
        var snapshot = uniqueNumber;
        var smNumber = snapshot++;
        if(!buildEnumerable(semanticModel,sourceExpression,containingType,ref snapshot,out var outerSource,out var outerSourceStaticMethods,unsafeAccessorCollector))
            return false;
        if(!buildEnumerable(semanticModel,lambdaBody,containingType,ref snapshot,out var innerFrag,out var innerStaticMethods,unsafeAccessorCollector))
            return false;
        // 要素型が TResult ジェネリックパラメータのままだと SelectMany_WithInlinedInner のアウタ struct で
        // 型解決できないため、通常の SelectMany デリゲートパスへフォールバックする。
        // 匿名型レジストリ有効時は内側ラムダが record struct 置換されて具象型名になるため通常ここは通らず、
        // registry-mapped できないラムダ形（本体ルートが匿名生成式でない等）の安全網として残している。
        if(innerFrag.ElementType=="TResult")
            return false;
        var identitySelector=lambdaBody is IdentifierNameSyntax identityIdentifier
            &&identityIdentifier.Identifier.ValueText==outerParamName;
        var smFrag = new SelectMany_WithInlinedInner(smNumber,outerSource,innerFrag,outerParamName,identitySelector);
        fragment=smFrag;
        uniqueNumber=snapshot;
        staticMethods=JoinStaticMethods(outerSourceStaticMethods,innerStaticMethods);
        staticMethods=JoinStaticMethods(staticMethods,smFrag.StructMethod);
        return true;
    }
    protected override Enumerable PrimarySource => source;
    protected abstract string CollectionElementType { get; }
    protected abstract string CollectionSelectorPrimaryConstructorParameter { get; }
    // 内側 sequence 式(まだ .GetEnumerator() を呼ぶ前、IEnumerable<TInner> のまま)。
    // GetTryGetNextBody 側で一度だけ評価し、実行時に配列かどうか判定して
    // 配列なら添字アクセス(span相当)、そうでなければ IEnumerator<T> にフォールバックする。
    protected abstract string CreateInnerSequenceExpression { get; }
    protected virtual string MoveNextOuterPrologue => string.Empty;
    // 内側の列を作る式(ラムダを含む)の直前に置く文。struct のメソッドの中のラムダは this のフィールドを取り込めない(CS1673)ので、
    // ラムダが使う外部のローカル(struct ではフィールドになっている)を、同じ名前のローカルへ写してから式を評価する。
    // 同じ名前のローカルはフィールドを隠すので、式の文面は変えずに済む。
    // 外側の値は、直前の TryGetNext(out var ...Next)のローカルをそのまま渡す(フィールドではなくローカルを取り込ませる)。
    protected virtual string InnerSequenceLocalCopies => string.Empty;
    protected string OuterValueLocal => $"{this.outerValue}Next";
    protected static string CopyCapturedToLocals(params FuncFragment[] funcs) {
        var builder = new System.Text.StringBuilder();
        var seen = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
        foreach(var func in funcs) {
            foreach(var (name,_) in func.CapturedLocals) {
                if(seen.Add(name)) {
                    builder.Append($"var {name} = this.{name};{"\n"}        ");
                }
            }
        }
        return builder.ToString();
    }
    protected abstract string CurrentValueAssignmentFromInner { get; }
    private string innerArray => $"__ia{番号}";
    private string innerIndex => $"__ii{番号}";
    private string innerCount => $"__ic{番号}";

    public abstract override string StructMethod { get; }
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}, {this.CollectionSelectorPrimaryConstructorParameter}";
    // 内側 sequence は外側 collectionSelector が返す IEnumerable<TInner> 由来で、生成時点では
    // 具象型が分からない。実行時に配列であれば添字アクセスの速いパスへ、そうでなければ
    // 従来通り IEnumerator<T>(interface dispatch) にフォールバックする(外側 source の
    // TryGetSpan と同じ考え方を内側にも適用)。
    public override string GetEnumeratorFields => $$"""
        private {{this.CollectionElementType}}[]? {{this.innerArray}} = null;
        private int {{this.innerIndex}};
        private int {{this.innerCount}};
        private global::System.Collections.Generic.IEnumerator<{{this.CollectionElementType}}>? innerEnumerator = null;
        private {{source.ElementType}} {{this.outerValue}} = default!;
        """;
    public override string GetTryGetNextBody => $$"""
        {{this.Start}}:
        if(this.{{this.innerArray}} is not null){
            if(this.{{this.innerIndex}}<this.{{this.innerCount}}){
                {{this.CounterCall("IncrementLogicalSelectManyInner")}}
                var {{this.item}} = this.{{this.innerArray}}[this.{{this.innerIndex}}];
                this.{{this.innerIndex}}++;
                {{this.CurrentValueAssignmentFromInner}}
                return true;
            }
            this.{{this.innerArray}} = null;
        }else if(this.innerEnumerator is not null){
            if(this.innerEnumerator.MoveNext()){
                {{this.CounterCall("IncrementLogicalSelectManyInner")}}
                var {{this.item}} = this.innerEnumerator.Current;
                {{this.CurrentValueAssignmentFromInner}}
                return true;
            }
            this.innerEnumerator.Dispose();
            this.innerEnumerator = null;
        }
        if(!{{this.e}}.TryGetNext(out var {{this.outerValue}}Next)){
            current=default!;
            return false;
        }
        {{this.MoveNextOuterPrologue}}
        {{this.CounterCall("IncrementLogicalSelectManyOuter")}}
        this.{{this.outerValue}} = {{this.outerValue}}Next;
        {{this.InnerSequenceLocalCopies}}
        var {{this.temp}} = {{this.CreateInnerSequenceExpression}};
        if({{this.temp}} is {{this.CollectionElementType}}[] {{this.innerArray}}Local){
            this.{{this.innerArray}} = {{this.innerArray}}Local;
            this.{{this.innerIndex}} = 0;
            this.{{this.innerCount}} = {{this.innerArray}}Local.Length;
        }else{
            this.innerEnumerator = {{this.temp}}.GetEnumerator();
        }
        goto {{this.Start}};
        """;
    protected override string GetEnumeratorDisposeBody => $$"""
        if(this.innerEnumerator is not null){
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
internal class SelectMany_selector(int 番号,Enumerable source,FuncFragment selector):SelectManyBase(番号,source) {
    private readonly FuncFragment selector = selector;
    // Explicit Fragment child visitor
    protected override IEnumerable<FuncFragment> GetChildFunctions() {
        foreach(var function in base.GetChildFunctions()) {
                yield return function;
        }
        if(selector is { } __visitorChild_selector) {
            yield return __visitorChild_selector;
        }
    }

    private string SelectorFuncType => $"global::System.Func<{source.ElementType}, global::System.Collections.Generic.IEnumerable<{this.CollectionElementType}>>";
    public static bool IsApplicableVariant(IMethodSymbol method) =>
        method.Parameters.Length==1&&
        !(method.Parameters[0].Type is INamedTypeSymbol { TypeArguments.Length: 3 });
    private bool UseResultTypeParam => selector.RequiresResultTypeParameter;
    private bool IsHoistable =>
        !selector.CanInvokeAsDelegate&&
        selector.CanInlineAssign&&
        !selector.RenderInlineExpression("__TsavoriteHoistProbe__").Contains("__TsavoriteHoistProbe__")&&
        !selector.ContainsAnchor;
    protected override string CollectionElementType =>
        this.UseResultTypeParam ? "TResult" : SequenceElementTypeName(FuncReturnTypeName(selector));
    public override string ElementType => this.CollectionElementType;
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
    public override string StructMethod =>
        this.UseResultTypeParam
            ? JoinStaticMethods(selector.SupportingMemberSources,this.FactorySource)
            : this.IsHoistable ? selector.SupportingMemberSources : this.FuncSupport(selector,nameof(selector));
    public override string GetEnumeratorExpression =>
        this.IsHoistable
            ? RenderEnumeratorExpression(this.EnumeratorTypeName,source.GetEnumeratorExpression,selector.RenderInlineExpression("__unused__"))
            : RenderEnumeratorExpressionWithClosure(this.EnumeratorTypeName,new[] { selector },source.GetEnumeratorExpression);
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
    protected override string CreateInnerSequenceExpression =>
        selector.CanInvokeAsDelegate
            ? $"{selector.DelegateFieldName}.Invoke({this.outerValue})"
            : this.IsHoistable
                ? "innerEnumerable0"
                : $"((global::System.Collections.Generic.IEnumerable<{this.CollectionElementType}>){selector.RenderInlineExpressionWithHoistedReceiver("this.",this.OuterValueLocal)})";
    protected override string InnerSequenceLocalCopies =>
        selector.CanInvokeAsDelegate||this.IsHoistable ? string.Empty : CopyCapturedToLocals(selector);
    protected override string CurrentValueAssignmentFromInner => $"current = this.{this.currentValue} = {this.item};";
    public override string GetConsumeExecutionStatement(string consumerName){
        if(this.UseResultTypeParam){
            return base.GetConsumeExecutionStatement(consumerName);
        }else{
        }
        var outerPlaceholder=$"__FLINQ_SELECTMANY_OUTER_{番号}__";
        string outerStatement;
        try{
            outerStatement=this.source.GetConsumeExecutionStatement(outerPlaceholder);
        }catch(NotSupportedException){
            return base.GetConsumeExecutionStatement(consumerName);
        }
        if(string.IsNullOrWhiteSpace(outerStatement)||!outerStatement.Contains($"{outerPlaceholder}.Consume(")){
            return base.GetConsumeExecutionStatement(consumerName);
        }else{
        }
        var localDeclarations=string.Join("\n",new[]{
            this.HoistedLocalDeclarations(this.selector),
            this.StaticDelegateLocalDeclarations(this.selector),
        }.Where(static text => !string.IsNullOrEmpty(text)));
        string localDeclarationBlock;
        if(string.IsNullOrEmpty(localDeclarations)){
            localDeclarationBlock=string.Empty;
        }else{
            localDeclarationBlock=localDeclarations+"\n";
        }
        var innerSeqName=$"__innerSeq{番号}";
        var innerArrName=$"__innerArr{番号}";
        var innerIdxName=$"__innerIdx{番号}";
        var innerItemName=$"__innerItem{番号}";
        var escapedPlaceholder=System.Text.RegularExpressions.Regex.Escape(outerPlaceholder);
        var pattern=$@"(?<indent>[ \t]*){escapedPlaceholder}\.Consume\((?<arg>.+?)\);";
        var replacedStatement=System.Text.RegularExpressions.Regex.Replace(outerStatement,pattern,match => {
            var indent=match.Groups["indent"].Value;
            var arg=match.Groups["arg"].Value;
            var sb=new global::System.Text.StringBuilder();
            sb.AppendLine($"{indent}global::System.Collections.Generic.IEnumerable<{this.CollectionElementType}> {innerSeqName} = default!;");
            sb.AppendLine(this.FuncAssignStaticIndented(indent,this.selector,nameof(this.selector),innerSeqName,arg));
            sb.AppendLine($"{indent}if({innerSeqName} is {this.CollectionElementType}[] {innerArrName}){{");
            sb.AppendLine($"{indent}    for(var {innerIdxName}=0;{innerIdxName}<{innerArrName}.Length;{innerIdxName}++){{");
            sb.AppendLine($"{indent}        var {innerItemName}={innerArrName}[{innerIdxName}];");
            sb.AppendLine($"{indent}        {consumerName}.Consume({innerItemName});");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine($"{indent}}}else{{");
            sb.AppendLine($"{indent}    foreach(var {innerItemName} in {innerSeqName}){{");
            sb.AppendLine($"{indent}        {consumerName}.Consume({innerItemName});");
            sb.AppendLine($"{indent}    }}");
            sb.Append($"{indent}}}");
            return sb.ToString();
        });
        return localDeclarationBlock+replacedStatement;
    }
}

internal class SelectMany_indexSelector(int 番号,Enumerable source,FuncFragment selector):SelectManyBase(番号,source) {
    // Explicit Fragment child visitor
    protected override IEnumerable<FuncFragment> GetChildFunctions() {
        foreach(var function in base.GetChildFunctions()) {
                yield return function;
        }
        if(selector is { } __visitorChild_selector) {
            yield return __visitorChild_selector;
        }
    }

    public static bool IsApplicableVariant(IMethodSymbol method) =>
        method.Parameters.Length==1&&
        method.Parameters[0].Type is INamedTypeSymbol { TypeArguments.Length: 3 };
    public override string StructMethod => this.FuncSupport(selector,nameof(selector));
    public override string GetEnumeratorExpression =>
        RenderEnumeratorExpressionWithClosure(this.EnumeratorTypeName,new[] { selector },source.GetEnumeratorExpression);
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {this.e}{this.ClosurePrimaryCtorSuffix(selector)}";
    protected override string CollectionSelectorPrimaryConstructorParameter => string.Empty;
    public override string GetEnumeratorFields => $$"""
        {{base.GetEnumeratorFields}}
        private int {{this.a}} = -1;
        """;
    protected override string MoveNextOuterPrologue => $"this.{this.a}++;";
    protected override string CreateInnerSequenceExpression =>
        $"((global::System.Collections.Generic.IEnumerable<{this.CollectionElementType}>){selector.RenderInlineExpressionWithHoistedReceiver("this.",this.OuterValueLocal,$"{this.a}Local")})";
    protected override string InnerSequenceLocalCopies => $"var {this.a}Local = this.{this.a};{"\n"}        "+CopyCapturedToLocals(selector);
    protected override string CurrentValueAssignmentFromInner => $"current = this.{this.currentValue} = {this.item};";
    protected override string CollectionElementType => SequenceElementTypeName(FuncReturnTypeName(selector));
    public override string ElementType => this.CollectionElementType;
}

internal class SelectMany_selector_resultSelector(int 番号,Enumerable source,FuncFragment collectionSelector,FuncFragment resultSelector):SelectManyBase(番号,source) {
    private readonly FuncFragment collectionSelector = collectionSelector;
    private readonly FuncFragment resultSelector = resultSelector;
    // Explicit Fragment child visitor
    protected override IEnumerable<FuncFragment> GetChildFunctions() {
        foreach(var function in base.GetChildFunctions()) {
                yield return function;
        }
        if(collectionSelector is { } __visitorChild_collectionSelector) {
            yield return __visitorChild_collectionSelector;
        }
        if(resultSelector is { } __visitorChild_resultSelector) {
            yield return __visitorChild_resultSelector;
        }
    }

    public static bool IsApplicableVariant(IMethodSymbol method) =>
        method.Parameters.Length==2&&
        !(method.Parameters[0].Type is INamedTypeSymbol { TypeArguments.Length: 3 });
    public override string StructMethod => $$"""
        {{this.FuncSupport(collectionSelector,nameof(collectionSelector))}}
        {{this.FuncSupport(resultSelector,nameof(resultSelector))}}
        """;
    public override string GetEnumeratorExpression =>
        RenderEnumeratorExpressionWithClosure(this.EnumeratorTypeName,new[] { collectionSelector,resultSelector },source.GetEnumeratorExpression);
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {this.e}{this.ClosurePrimaryCtorSuffix(collectionSelector,resultSelector)}";
    public override string GetEnumeratorFields => $$"""
        {{base.GetEnumeratorFields}}
        """;
    protected override string CollectionSelectorPrimaryConstructorParameter => string.Empty;
    protected override string CreateInnerSequenceExpression =>
        $"((global::System.Collections.Generic.IEnumerable<{this.CollectionElementType}>){collectionSelector.RenderInlineExpressionWithHoistedReceiver("this.",this.OuterValueLocal)})";
    protected override string CurrentValueAssignmentFromInner =>
        this.FuncAssignCurrent(resultSelector,nameof(resultSelector),$"this.{this.currentValue}",this.outerValue,this.item);
    protected override string CollectionElementType => SequenceElementTypeName(FuncReturnTypeName(collectionSelector));
    public override string ElementType => FuncReturnTypeName(resultSelector);
    public override string GetConsumeExecutionStatement(string consumerName){
        var outerPlaceholder=$"__FLINQ_SELECTMANY_OUTER_{番号}__";
        string outerStatement;
        try{
            outerStatement=this.source.GetConsumeExecutionStatement(outerPlaceholder);
        }catch(NotSupportedException){
            return base.GetConsumeExecutionStatement(consumerName);
        }
        if(string.IsNullOrWhiteSpace(outerStatement)||!outerStatement.Contains($"{outerPlaceholder}.Consume(")){
            return base.GetConsumeExecutionStatement(consumerName);
        }else{
        }
        var localDeclarations=string.Join("\n",new[]{
            this.HoistedLocalDeclarations(this.collectionSelector,this.resultSelector),
            this.StaticDelegateLocalDeclarations(this.collectionSelector,this.resultSelector),
        }.Where(static text => !string.IsNullOrEmpty(text)));
        string localDeclarationBlock;
        if(string.IsNullOrEmpty(localDeclarations)){
            localDeclarationBlock=string.Empty;
        }else{
            localDeclarationBlock=localDeclarations+"\n";
        }
        var innerSeqName=$"__innerSeq{番号}";
        var innerArrName=$"__innerArr{番号}";
        var innerIdxName=$"__innerIdx{番号}";
        var innerItemName=$"__innerItem{番号}";
        var resultName=$"__selectManyResult{番号}";
        var escapedPlaceholder=System.Text.RegularExpressions.Regex.Escape(outerPlaceholder);
        var pattern=$@"(?<indent>[ \t]*){escapedPlaceholder}\.Consume\((?<arg>.+?)\);";
        var replacedStatement=System.Text.RegularExpressions.Regex.Replace(outerStatement,pattern,match => {
            var indent=match.Groups["indent"].Value;
            var arg=match.Groups["arg"].Value;
            var sb=new global::System.Text.StringBuilder();
            sb.AppendLine($"{indent}global::System.Collections.Generic.IEnumerable<{this.CollectionElementType}> {innerSeqName} = default!;");
            sb.AppendLine(this.FuncAssignStaticIndented(indent,this.collectionSelector,nameof(this.collectionSelector),innerSeqName,arg));
            sb.AppendLine($"{indent}if({innerSeqName} is {this.CollectionElementType}[] {innerArrName}){{");
            sb.AppendLine($"{indent}    for(var {innerIdxName}=0;{innerIdxName}<{innerArrName}.Length;{innerIdxName}++){{");
            sb.AppendLine($"{indent}        var {innerItemName}={innerArrName}[{innerIdxName}];");
            sb.AppendLine($"{indent}        {this.ElementType} {resultName}=default!;");
            sb.AppendLine(this.FuncAssignStaticIndented(indent+"        ",this.resultSelector,nameof(this.resultSelector),resultName,arg,innerItemName));
            sb.AppendLine($"{indent}        {consumerName}.Consume({resultName});");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine($"{indent}}}else{{");
            sb.AppendLine($"{indent}    foreach(var {innerItemName} in {innerSeqName}){{");
            sb.AppendLine($"{indent}        {this.ElementType} {resultName}=default!;");
            sb.AppendLine(this.FuncAssignStaticIndented(indent+"        ",this.resultSelector,nameof(this.resultSelector),resultName,arg,innerItemName));
            sb.AppendLine($"{indent}        {consumerName}.Consume({resultName});");
            sb.AppendLine($"{indent}    }}");
            sb.Append($"{indent}}}");
            return sb.ToString();
        });
        return localDeclarationBlock+replacedStatement;
    }
}

/// <summary>
/// selector の本体 LINQ チェーンを struct のまま展開する SelectMany。
/// source generator の生成コードを検証する。
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
internal class SelectMany_WithInlinedInner(int 番号,Enumerable source,Enumerable innerFragment,string outerParamName,bool identitySelector = false)
    :SelectManyBase(番号,source) {
    // Explicit Fragment child visitor
    protected override IEnumerable<Fragment> GetChildFragments() {
        foreach(var child in base.GetChildFragments()) {
                yield return child;
        }
        if(innerFragment is { } __visitorChild_innerFragment) {
            yield return __visitorChild_innerFragment;
        }
    }


    public static bool IsApplicableVariant(IMethodSymbol method) => false;
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

    // source generator の生成コードを検証する。
    public override string GetEnumeratorFields => $$"""
        private {{this.InnerEnumeratorTypeName}} innerEnumerator = default!;
        private bool {{this.hasValue}} = false;
        private {{source.ElementType}} {{this.outerValue}} = default!;
        """;

    // bool flag replaces the null check
    public override string GetTryGetNextBody => $$"""
        {{this.Start}}:
        if(this.{{this.hasValue}}){
            if(this.innerEnumerator.TryGetNext(out var {{this.item}})){
                {{this.CounterCall("IncrementLogicalSelectManyInner")}}
                {{this.CurrentValueAssignmentFromInner}}
                return true;
            }
            this.innerEnumerator.Dispose();
            this.{{this.hasValue}} = false;
        }
        if(!{{this.e}}.TryGetNext(out var __mn50)){
            current=default!;
            return false;
        }
        {{this.CounterCall("IncrementLogicalSelectManyOuter")}}
        this.{{this.outerValue}} = __mn50;
        this.innerEnumerator = {{this.CreateInnerSequenceExpression}};
        this.{{this.hasValue}} = true;
        goto {{this.Start}};
        """;

    protected override string GetEnumeratorDisposeBody => $$"""
        if(this.{{this.hasValue}}){
            this.innerEnumerator.Dispose();
        }
        {{this.e}}.Dispose();
        """;

    // Instantiate the inner struct, substituting outer param name with the outerValue field.
    protected override string CreateInnerSequenceExpression =>
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
        RenderEnumeratorExpression(this.EnumeratorTypeName,source.GetEnumeratorExpression);
    private string? ConsumeOuterEnumerableExpression => identitySelector?source.DirectEnumerableExpression:null;
    private string ConsumeOuterSourceName => $"__selectManyOuterSource{番号}";

    public override string GetConsumePreparationStatement() {
        if(this.ConsumeOuterEnumerableExpression is not { } outerEnumerableExpression) {
            return base.GetConsumePreparationStatement();
        }

        return $$"""
            var {{this.ConsumeOuterSourceName}}={{outerEnumerableExpression}};
            global::System.ArgumentNullException.ThrowIfNull({{this.ConsumeOuterSourceName}},"source");
            """;
    }

    public override string GetConsumeExecutionStatement(string consumerName) {
        if(this.ConsumeOuterEnumerableExpression is null) {
            return base.GetConsumeExecutionStatement(consumerName);
        }

        var outerEnumeratorName=$"__selectManyOuterEnumerator{番号}";
        var innerSourceName=$"__selectManyInnerSource{番号}";
        var innerEnumeratorName=$"__selectManyInnerEnumerator{番号}";
        return $$"""
            {
                {{this.CounterCall("IncrementEnumerationLoopUsed")}}
                using(var {{outerEnumeratorName}}={{this.ConsumeOuterSourceName}}.GetEnumerator()){
                    while({{outerEnumeratorName}}.MoveNext()){
                        {{this.CounterCall("IncrementLogicalSelectManyOuter")}}
                        global::System.Collections.Generic.IEnumerable<{{innerFragment.ElementType}}> {{innerSourceName}}={{outerEnumeratorName}}.Current;
                        using(var {{innerEnumeratorName}}={{innerSourceName}}.GetEnumerator()){
                            while({{innerEnumeratorName}}.MoveNext()){
                                {{this.CounterCall("IncrementLogicalSelectManyInner")}}
                                {{consumerName}}.Consume({{innerEnumeratorName}}.Current);
                            }
                        }
                    }
                }
            }
            """;
    }
}

internal class SelectMany_indexSelector_resultSelector(int 番号,Enumerable source,FuncFragment collectionSelector,FuncFragment resultSelector):SelectManyBase(番号,source) {
    // Explicit Fragment child visitor
    protected override IEnumerable<FuncFragment> GetChildFunctions() {
        foreach(var function in base.GetChildFunctions()) {
                yield return function;
        }
        if(collectionSelector is { } __visitorChild_collectionSelector) {
            yield return __visitorChild_collectionSelector;
        }
        if(resultSelector is { } __visitorChild_resultSelector) {
            yield return __visitorChild_resultSelector;
        }
    }

    public static bool IsApplicableVariant(IMethodSymbol method) =>
        method.Parameters.Length==2&&
        method.Parameters[0].Type is INamedTypeSymbol { TypeArguments.Length: 3 };
    public override string StructMethod => $$"""
        {{this.FuncSupport(collectionSelector,nameof(collectionSelector))}}
        {{this.FuncSupport(resultSelector,nameof(resultSelector))}}
        """;
    public override string GetEnumeratorExpression =>
        RenderEnumeratorExpressionWithClosure(this.EnumeratorTypeName,new[] { collectionSelector,resultSelector },source.GetEnumeratorExpression);
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {this.e}{this.ClosurePrimaryCtorSuffix(collectionSelector,resultSelector)}";
    public override string GetEnumeratorFields => $$"""
        {{base.GetEnumeratorFields}}
        private int {{this.a}} = -1;
        """;
    protected override string CollectionSelectorPrimaryConstructorParameter => string.Empty;
    protected override string MoveNextOuterPrologue => $"this.{this.a}++;";
    protected override string CreateInnerSequenceExpression =>
        $"((global::System.Collections.Generic.IEnumerable<{this.CollectionElementType}>){collectionSelector.RenderInlineExpressionWithHoistedReceiver("this.",this.OuterValueLocal,$"{this.a}Local")})";
    protected override string InnerSequenceLocalCopies => $"var {this.a}Local = this.{this.a};";
    protected override string CurrentValueAssignmentFromInner =>
        this.FuncAssignCurrent(resultSelector,nameof(resultSelector),$"this.{this.currentValue}",this.outerValue,this.item);
    protected override string CollectionElementType => SequenceElementTypeName(FuncReturnTypeName(collectionSelector));
    public override string ElementType => FuncReturnTypeName(resultSelector);
}
