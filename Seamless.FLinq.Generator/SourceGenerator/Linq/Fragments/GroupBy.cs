using Microsoft.CodeAnalysis.CSharp;
namespace Seamless.FLinq.Generator;

/*
GroupBySupport は GroupBy 系 Fragment が共有する「全件を LookupBuilder に積んで、追加順 group を返す」
コード片を組み立てる。

入力例:
  source.GroupBy(x => x.Category)

生成例:
  if(!built) {
      foreach(var item in source) builder.Add(keySelector(item), elementSelector(item));
      cursor = first = builder.GetRootGroupAndClear();
  }
  if(cursor == null) return false;
  current = cursor;
  cursor = cursor.NextGroupInAddOrder;
  return true;

バリエーション:
  - elementSelector なしは item 自体を group 要素にする。
  - resultSelector ありは current = resultSelector(group.Key, group) を返す。
  - source が span を試せる場合は build loop 内だけ span/while の分岐を持つ。
*/
internal static class GroupBySupport {
    public static string GroupingType(string keyType,string elementType) =>
        $"global::Seamless.Linq.Grouping<{keyType}, {elementType}>";

    public static string Fields(string keyType,string elementType,string cursorName,string firstName,string builtName) => $$"""
        private {{GroupingType(keyType,elementType)}}? {{cursorName}};
        private {{GroupingType(keyType,elementType)}}? {{firstName}};
        private bool {{builtName}};
        """;

    // 初回 TryGetNext で Carrier の LookupBuilder により全グループを構築し、
    // add-order の循環連結リストの先頭(cursor/first)を確定する。
    // 続けて cursor を 1 つ進め、ローカル __cur に取り出す。末尾(first へ戻る)で cursor を null 化。
    public static string BuildAndAdvance(
        string enumeratorName,
        bool canTryProvideSpan,
        string keyType,
        string elementType,
        string keyAssignCode,
        string elementAssignCode,
        string elementName,
        string cursorName,
        string firstName,
        string builtName,
        string itemName,
        string keyName) {
        var addLoopSpan = $$"""
            for(var __i=0;__i<__span.Length;__i++){
                var {{itemName}}=__span[__i];
                {{keyType}} {{keyName}}=default!;
                {{IndentBlock(keyAssignCode,"                ")}}
                {{IndentBlock(elementAssignCode,"                ")}}
                __builder.AddUnpooled({{keyName}},{{elementName}});
            }
            """;
        var addLoopWhile = $$"""
            while({{enumeratorName}}.TryGetNext(out var __mn35)){
                var {{itemName}}=__mn35;
                {{keyType}} {{keyName}}=default!;
                {{IndentBlock(keyAssignCode,"                ")}}
                {{IndentBlock(elementAssignCode,"                ")}}
                __builder.AddUnpooled({{keyName}},{{elementName}});
            }
            """;
        var buildLoop = canTryProvideSpan ? $$"""
            if({{enumeratorName}}.TryGetSpan(out var __span)){
                {{IndentBlock(addLoopSpan,"    ")}}
            }else{
                {{IndentBlock(addLoopWhile,"    ")}}
            }
            """ : addLoopWhile;
        return $$"""
        if(!this.{{builtName}}){
            this.{{builtName}}=true;
            var __hasCnt={{enumeratorName}}.TryGetNonEnumeratedCount(out var __cnt);
            if(__hasCnt) global::Seamless.Linq.LinqCounterScope.IncrementHashCapacityHintUsed();
            var __builder=__hasCnt
                ? new global::Seamless.Linq.LookupBuilder<{{keyType}}, {{elementType}}>(global::System.Math.Min(__cnt,1024),global::System.Collections.Generic.EqualityComparer<{{keyType}}>.Default)
                : new global::Seamless.Linq.LookupBuilder<{{keyType}}, {{elementType}}>(global::System.Collections.Generic.EqualityComparer<{{keyType}}>.Default);
            {{IndentBlock(buildLoop,"            ")}}
            this.{{cursorName}}=this.{{firstName}}=__builder.GetRootGroupAndClear();
        }
        if(this.{{cursorName}} is null){
            current=default!;
            return false;
        }
        var __cur=this.{{cursorName}};
        var __next=__cur.NextGroupInAddOrder;
        if(__next==this.{{firstName}}){
            this.{{cursorName}}=null;
        }else{
            this.{{cursorName}}=__next;
        }
        """;
    }

    // 結果選択子が、その場に展開できる式で、グループ(第2引数)を参照しないか。
    // 参照しなければ、結果はキーの初出順にキーだけから作ったものなので、グループを作らずにキーの重複を除けばよい。
    // 展開するときはキーだけを渡す(グループの引数は本体に現れないので置き換える先が無い)。
    public static bool ResultIgnoresGroup(FuncFragment resultSelector) {
        if(resultSelector.RequiresStatementInlining
            ||resultSelector.CanInvokeAsDelegate
            ||!resultSelector.CanInlineAssign
            ||resultSelector.ParameterNames.Length!=2) {
            return false;
        }
        var body = SyntaxFactory.ParseExpression(resultSelector.InlineBody);
        if(body.ContainsDiagnostics) {
            return false;
        }
        var groupName = resultSelector.ParameterNames[1];
        return !body.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>().Any(identifier => identifier.Identifier.ValueText==groupName);
    }

    // グループを作らずキーの重複だけを除いて結果を逐次流してよいか(FLinq の契約: .Anchor の無い要素選択子は評価しなくてよい)。
    // 逐次流すと、後段が途中で止めたときにソースとキー選択子の評価が減るので、それらと結果選択子に .Anchor があれば使わない。
    public static bool CanStreamDistinctKeys(Enumerable source,FuncFragment resultSelector,params FuncFragment[] otherFunctions) =>
        !resultSelector.ContainsAnchor
        &&!otherFunctions.Any(static function => function.ContainsAnchor)
        &&!source.TreeContainsAnchor()
        &&ResultIgnoresGroup(resultSelector);

    public static string DistinctKeyFields(string keyType,string seenName) =>
        $"private global::System.Collections.Generic.HashSet<{keyType}>? {seenName};";

    // キーの初出のときだけ結果を1つ流す。resultAssignCode は current を設定する文。
    public static string AdvanceDistinctKey(
        string enumeratorName,
        string keyType,
        string keyAssignCode,
        string resultAssignCode,
        string itemName,
        string keyName,
        string seenName) => $$"""
        if(this.{{seenName}} is null){
            var __hasCnt={{enumeratorName}}.TryGetNonEnumeratedCount(out var __cnt);
            this.{{seenName}}=__hasCnt
                ? new global::System.Collections.Generic.HashSet<{{keyType}}>(global::System.Math.Min(__cnt,1024))
                : new global::System.Collections.Generic.HashSet<{{keyType}}>();
        }
        while({{enumeratorName}}.TryGetNext(out var __mn35)){
            var {{itemName}}=__mn35;
            {{keyType}} {{keyName}}=default!;
            {{IndentBlock(keyAssignCode,"            ").TrimStart()}}
            if(this.{{seenName}}.Add({{keyName}})){
                {{IndentBlock(resultAssignCode,"                ").TrimStart()}}
                return true;
            }
        }
        current=default!;
        return false;
        """;

    public static string IndentBlock(string text,string indent) {
        return string.Join("\n",text.Replace("\r\n","\n").Split('\n').Select(line => line.Length==0 ? line : indent+line));
    }

}

internal class GroupBy_keySelector(int 番号,Enumerable source,FuncFragment keySelector):Enumerable(番号) {
    // Explicit Fragment child visitor
    protected override IEnumerable<Fragment> GetChildFragments() {
        foreach(var child in base.GetChildFragments()) {
                yield return child;
        }
        if(source is { } __visitorChild_source) {
            yield return __visitorChild_source;
        }
    }
    protected override IEnumerable<FuncFragment> GetChildFunctions() {
        foreach(var function in base.GetChildFunctions()) {
                yield return function;
        }
        if(keySelector is { } __visitorChild_keySelector) {
            yield return __visitorChild_keySelector;
        }
    }

    private string KeySelectorFuncType => $"global::System.Func<{source.ElementType}, {FuncReturnTypeName(keySelector)}>";
    private string KeyType => FuncReturnTypeName(keySelector);
    protected override Enumerable PrimarySource => source;
    public override string GetEnumerableExpression =>
        $"global::Seamless.Linq.Enumerable.GroupBy({source.GetEnumerableExpression}, {keySelector.DelegateConstructorArg??keySelector.RenderAsLambda()})";
    public override string StructMethod => this.FuncSupport(keySelector,nameof(keySelector));
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression =>
        RenderEnumeratorExpressionWithClosure(this.EnumeratorTypeName,new[] { keySelector },source.GetEnumeratorExpression);
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {this.e}{this.ClosurePrimaryCtorSuffix(keySelector)}";
    public override string GetEnumeratorFields =>
        GroupBySupport.Fields(this.KeyType,source.ElementType,this.group,this.lookup,this.hasValue);
    public override string GetTryGetNextBody => GroupBySupport.BuildAndAdvance(this.e,source.CanTryProvideSpan,this.KeyType,source.ElementType,this.FuncAssign(keySelector,nameof(keySelector),this.key,this.item),"",this.item,this.group,this.lookup,this.hasValue,this.item,this.key)+$$"""
        current=this.Current=__cur;
        return true;
        """;
    public override string ElementType => $"global::System.Linq.IGrouping<{this.KeyType}, {source.ElementType}>";
    // Range の x%C のキーは、剰余類からグループを直接作る(RangeModuloDistinctRoot.RenderGroupsConsume)。
    public override string GetConsumeExecutionStatement(string consumerName) =>
        source is RangeRoot range
            &&!source.TreeContainsAnchor()
            &&RangeModuloOrderRoot.TryGetModulus(keySelector,out var modulus)
            ? RangeModuloDistinctRoot.RenderGroupsConsume(range,modulus,番号,consumerName,this.ElementType,"int",string.Empty,static (destination,value) => $"{destination}={value};")
            : base.GetConsumeExecutionStatement(consumerName);
}

internal class GroupBy_keySelector_resultSelector(
    int 番号,
    Enumerable source,
    FuncFragment keySelector,
    FuncFragment resultSelector)
    :Enumerable(番号) {
    // Explicit Fragment child visitor
    protected override IEnumerable<Fragment> GetChildFragments() {
        foreach(var child in base.GetChildFragments()) {
                yield return child;
        }
        if(source is { } __visitorChild_source) {
            yield return __visitorChild_source;
        }
    }
    protected override IEnumerable<FuncFragment> GetChildFunctions() {
        foreach(var function in base.GetChildFunctions()) {
                yield return function;
        }
        if(keySelector is { } __visitorChild_keySelector) {
            yield return __visitorChild_keySelector;
        }
        if(resultSelector is { } __visitorChild_resultSelector) {
            yield return __visitorChild_resultSelector;
        }
    }

    public static bool IsApplicableVariant(IMethodSymbol method) =>
        method.Parameters.Length==2
        &&method.Parameters[1].Type is INamedTypeSymbol { TypeArguments.Length: 3 };
    protected override Enumerable PrimarySource => source;
    private string KeyType => FuncReturnTypeName(keySelector);
    private bool UseResultTypeParam => resultSelector.RequiresResultTypeParameter;
    public override string ElementType => this.UseResultTypeParam ? "TResult" : FuncReturnTypeName(resultSelector);
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
        private static global::Seamless.Linq.ValueEnumerable<{{this.Struct}}<TEnum, TResult>, TResult> {{this.FactoryMethodName}}<TEnum, TResult>(TEnum source{{this.ClosurePrimaryCtorSuffix(keySelector,resultSelector)}})
            where TEnum : struct, global::Seamless.Linq.IValueEnumerator<{{source.ElementType}}>
            => new(new {{this.Struct}}<TEnum, TResult>(source{{this.ClosureArgsSuffixForFactory(keySelector,resultSelector)}}));
        """;
    public override string StructMethod => JoinStaticMethods(JoinStaticMethods(this.FuncSupport(keySelector,nameof(keySelector)),this.FuncSupport(resultSelector,nameof(resultSelector))),this.FactorySource);
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression =>
        this.UseResultTypeParam
            ? throw new NotSupportedException($"{this.GetType().Name} does not support direct GetEnumeratorExpression for anonymous result type; use GetEnumerableExpression.")
            : RenderEnumeratorExpressionWithClosure(this.EnumeratorTypeName,new[] { keySelector,resultSelector },source.GetEnumeratorExpression);
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {this.e}{this.ClosurePrimaryCtorSuffix(keySelector,resultSelector)}";
    private bool IgnoresGroup => GroupBySupport.CanStreamDistinctKeys(source,resultSelector,keySelector);
    // 結果がグループを使わず Range の x%C のキーなら、キーの初出の値だけを流して結果を作る(集合を使わない)。
    public override string GetConsumeExecutionStatement(string consumerName) {
        if(!this.IgnoresGroup
            ||source is not RangeRoot range
            ||!RangeModuloOrderRoot.TryGetModulus(keySelector,out var modulus)) {
            return base.GetConsumeExecutionStatement(consumerName);
        }
        var resultName = $"__result{番号}";
        return RangeModuloDistinctRoot.RenderFirstOccurrencesConsume(range,modulus,番号,this.HoistedLocalDeclarations(resultSelector),value => $$"""
            {{this.ElementType}} {{resultName}}=default!;
            {{this.FuncAssignIndented("",resultSelector,nameof(resultSelector),resultName,$"(int)({value}%{modulus})")}}
            {{consumerName}}.Consume({{resultName}});
            """);
    }
    public override string GetEnumeratorFields => this.IgnoresGroup
        ? GroupBySupport.DistinctKeyFields(this.KeyType,this.lookup)
        : GroupBySupport.Fields(this.KeyType,source.ElementType,this.group,this.lookup,this.hasValue);
    public override string GetTryGetNextBody => this.IgnoresGroup
        ? GroupBySupport.AdvanceDistinctKey(this.e,this.KeyType,this.FuncAssign(keySelector,nameof(keySelector),this.key,this.item),this.FuncAssignCurrentIndented("",resultSelector,nameof(resultSelector),this.currentValue,this.key),this.item,this.key,this.lookup)
        : GroupBySupport.BuildAndAdvance(this.e,source.CanTryProvideSpan,this.KeyType,source.ElementType,this.FuncAssign(keySelector,nameof(keySelector),this.key,this.item),"",this.item,this.group,this.lookup,this.hasValue,this.item,this.key)+$$"""
        {{this.FuncAssignCurrentIndented("",resultSelector,nameof(resultSelector),this.currentValue,"__cur.Key",$"(global::System.Collections.Generic.IEnumerable<{source.ElementType}>)__cur")}}
        return true;
        """;
    public override string GetEnumerableExpression =>
        this.UseResultTypeParam
            ? $"{this.FactoryMethodName}({source.GetEnumeratorExpression}{this.ClosureArgsSuffix(keySelector,resultSelector)})"
            : $"global::Seamless.Linq.Enumerable.GroupBy({source.GetEnumerableExpression}, {keySelector.DelegateConstructorArg??keySelector.RenderAsLambda()}, {resultSelector.DelegateConstructorArg??resultSelector.RenderAsLambda()})";
}

internal class GroupBy_keySelector_elementSelector(
    int 番号,
    Enumerable source,
    FuncFragment keySelector,
    FuncFragment elementSelector)
    :Enumerable(番号) {
    // Explicit Fragment child visitor
    protected override IEnumerable<Fragment> GetChildFragments() {
        foreach(var child in base.GetChildFragments()) {
                yield return child;
        }
        if(source is { } __visitorChild_source) {
            yield return __visitorChild_source;
        }
    }
    protected override IEnumerable<FuncFragment> GetChildFunctions() {
        foreach(var function in base.GetChildFunctions()) {
                yield return function;
        }
        if(keySelector is { } __visitorChild_keySelector) {
            yield return __visitorChild_keySelector;
        }
        if(elementSelector is { } __visitorChild_elementSelector) {
            yield return __visitorChild_elementSelector;
        }
    }

    public static bool IsApplicableVariant(IMethodSymbol method) =>
        method.Parameters.Length==2
        &&method.Parameters[1].Type is INamedTypeSymbol { TypeArguments.Length: 2 };
    protected override Enumerable PrimarySource => source;
    private string KeyType => FuncReturnTypeName(keySelector);
    private string ElemType => FuncReturnTypeName(elementSelector);
    public override string GetEnumerableExpression =>
        $"global::Seamless.Linq.Enumerable.GroupBy({source.GetEnumerableExpression}, {keySelector.DelegateConstructorArg??keySelector.RenderAsLambda()}, {elementSelector.DelegateConstructorArg??elementSelector.RenderAsLambda()})";
    public override string StructMethod => JoinStaticMethods(this.FuncSupport(keySelector,nameof(keySelector)),this.FuncSupport(elementSelector,nameof(elementSelector)));
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression =>
        RenderEnumeratorExpressionWithClosure(this.EnumeratorTypeName,new[] { keySelector,elementSelector },source.GetEnumeratorExpression);
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {this.e}{this.ClosurePrimaryCtorSuffix(keySelector,elementSelector)}";
    public override string GetEnumeratorFields =>
        GroupBySupport.Fields(this.KeyType,this.ElemType,this.group,this.lookup,this.hasValue);
    public override string GetTryGetNextBody => GroupBySupport.BuildAndAdvance(this.e,source.CanTryProvideSpan,this.KeyType,this.ElemType,this.FuncAssign(keySelector,nameof(keySelector),this.key,this.item),$"{this.ElemType} {this.a}=default!;\n{this.FuncAssign(elementSelector,nameof(elementSelector),this.a,this.item)}",this.a,this.group,this.lookup,this.hasValue,this.item,this.key)+$$"""
        current=this.Current=__cur;
        return true;
        """;
    public override string ElementType => $"global::System.Linq.IGrouping<{this.KeyType}, {this.ElemType}>";
    // Range の x%C のキーは、剰余類からグループを直接作る(RangeModuloDistinctRoot.RenderGroupsConsume)。要素選択子はその場に展開する。
    public override string GetConsumeExecutionStatement(string consumerName) =>
        source is RangeRoot range
            &&!source.TreeContainsAnchor()
            &&!elementSelector.ContainsAnchor
            &&!elementSelector.CanInvokeAsDelegate
            &&!elementSelector.RequiresStatementInlining
            &&elementSelector.CanInlineAssign
            &&RangeModuloOrderRoot.TryGetModulus(keySelector,out var modulus)
            ? RangeModuloDistinctRoot.RenderGroupsConsume(range,modulus,番号,consumerName,this.ElementType,this.ElemType,
                this.HoistedLocalDeclarations(elementSelector),
                (destination,value) => this.FuncAssign(elementSelector,nameof(elementSelector),destination,value))
            : base.GetConsumeExecutionStatement(consumerName);
}

internal class GroupBy_keySelector_elementSelector_resultSelector(
    int 番号,
    Enumerable source,
    FuncFragment keySelector,
    FuncFragment elementSelector,
    FuncFragment resultSelector)
    :Enumerable(番号) {
    // Explicit Fragment child visitor
    protected override IEnumerable<Fragment> GetChildFragments() {
        foreach(var child in base.GetChildFragments()) {
                yield return child;
        }
        if(source is { } __visitorChild_source) {
            yield return __visitorChild_source;
        }
    }
    protected override IEnumerable<FuncFragment> GetChildFunctions() {
        foreach(var function in base.GetChildFunctions()) {
                yield return function;
        }
        if(keySelector is { } __visitorChild_keySelector) {
            yield return __visitorChild_keySelector;
        }
        if(elementSelector is { } __visitorChild_elementSelector) {
            yield return __visitorChild_elementSelector;
        }
        if(resultSelector is { } __visitorChild_resultSelector) {
            yield return __visitorChild_resultSelector;
        }
    }

    public static bool IsApplicableVariant(IMethodSymbol method) =>
        method.Parameters.Length==3
        &&method.Parameters[2].Type is INamedTypeSymbol { TypeArguments.Length: 3 };
    protected override Enumerable PrimarySource => source;
    private bool UseResultTypeParam => resultSelector.RequiresResultTypeParameter;
    private string KeyType => FuncReturnTypeName(keySelector);
    private string ElemType => FuncReturnTypeName(elementSelector);
    public override string ElementType => this.UseResultTypeParam ? "TResult" : FuncReturnTypeName(resultSelector);
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
        private static global::Seamless.Linq.ValueEnumerable<{{this.Struct}}<TEnum, TResult>, TResult> {{this.FactoryMethodName}}<TEnum, TResult>(TEnum source{{this.ClosurePrimaryCtorSuffix(keySelector,elementSelector,resultSelector)}})
            where TEnum : struct, global::Seamless.Linq.IValueEnumerator<{{source.ElementType}}>
            => new(new {{this.Struct}}<TEnum, TResult>(source{{this.ClosureArgsSuffixForFactory(keySelector,elementSelector,resultSelector)}}));
        """;
    public override string StructMethod => JoinStaticMethods(JoinStaticMethods(JoinStaticMethods(this.FuncSupport(keySelector,nameof(keySelector)),this.FuncSupport(elementSelector,nameof(elementSelector))),this.FuncSupport(resultSelector,nameof(resultSelector))),this.FactorySource);
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression =>
        this.UseResultTypeParam
            ? throw new NotSupportedException($"{this.GetType().Name} does not support direct GetEnumeratorExpression for anonymous result type; use GetEnumerableExpression.")
            : RenderEnumeratorExpressionWithClosure(this.EnumeratorTypeName,new[] { keySelector,elementSelector,resultSelector },source.GetEnumeratorExpression);
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {this.e}{this.ClosurePrimaryCtorSuffix(keySelector,elementSelector,resultSelector)}";
    private bool IgnoresGroup => GroupBySupport.CanStreamDistinctKeys(source,resultSelector,keySelector,elementSelector);
    public override string GetEnumeratorFields => this.IgnoresGroup
        ? GroupBySupport.DistinctKeyFields(this.KeyType,this.lookup)
        : GroupBySupport.Fields(this.KeyType,this.ElemType,this.group,this.lookup,this.hasValue);
    public override string GetTryGetNextBody => this.IgnoresGroup
        ? GroupBySupport.AdvanceDistinctKey(this.e,this.KeyType,this.FuncAssign(keySelector,nameof(keySelector),this.key,this.item),this.FuncAssignCurrentIndented("",resultSelector,nameof(resultSelector),this.currentValue,this.key),this.item,this.key,this.lookup)
        : GroupBySupport.BuildAndAdvance(this.e,source.CanTryProvideSpan,this.KeyType,this.ElemType,this.FuncAssign(keySelector,nameof(keySelector),this.key,this.item),$"{this.ElemType} {this.a}=default!;\n{this.FuncAssign(elementSelector,nameof(elementSelector),this.a,this.item)}",this.a,this.group,this.lookup,this.hasValue,this.item,this.key)+$$"""
        {{this.FuncAssignCurrentIndented("",resultSelector,nameof(resultSelector),this.currentValue,"__cur.Key",$"(global::System.Collections.Generic.IEnumerable<{this.ElemType}>)__cur")}}
        return true;
        """;
    public override string GetEnumerableExpression =>
        this.UseResultTypeParam
            ? $"{this.FactoryMethodName}({source.GetEnumeratorExpression}{this.ClosureArgsSuffix(keySelector,elementSelector,resultSelector)})"
            : $"global::Seamless.Linq.Enumerable.GroupBy({source.GetEnumerableExpression}, {keySelector.DelegateConstructorArg??keySelector.RenderAsLambda()}, {elementSelector.DelegateConstructorArg??elementSelector.RenderAsLambda()}, {resultSelector.DelegateConstructorArg??resultSelector.RenderAsLambda()})";
}
