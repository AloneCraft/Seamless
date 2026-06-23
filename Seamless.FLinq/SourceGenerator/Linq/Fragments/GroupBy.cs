namespace Seamless.FLinq.Linq.Fragments;

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
internal static class GroupBySupport{
    public static string GroupingType(string keyType,string elementType)=>
        $"global::Seamless.System.Carrier.Grouping<{keyType}, {elementType}>";

    public static string Fields(string keyType,string elementType,string cursorName,string firstName,string builtName)=>$$"""
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
        string keyName){
        var addLoopSpan=$$"""
            for(var __i=0;__i<__span.Length;__i++){
                var {{itemName}}=__span[__i];
                {{keyType}} {{keyName}}=default!;
                {{IndentBlock(keyAssignCode,"                ")}}
                {{IndentBlock(elementAssignCode,"                ")}}
                __builder.Add({{keyName}},{{elementName}});
            }
            """;
        var addLoopWhile=$$"""
            while({{enumeratorName}}.MoveNext()){
                var {{itemName}}={{enumeratorName}}.Current;
                {{keyType}} {{keyName}}=default!;
                {{IndentBlock(keyAssignCode,"                ")}}
                {{IndentBlock(elementAssignCode,"                ")}}
                __builder.Add({{keyName}},{{elementName}});
            }
            """;
        var buildLoop=canTryProvideSpan ? $$"""
            if({{enumeratorName}}.TryGetSpan(out var __span)){
                {{IndentBlock(addLoopSpan,"    ")}}
            }else{
                {{IndentBlock(addLoopWhile,"    ")}}
            }
            """ : addLoopWhile;
        return $$"""
        if(!this.{{builtName}}){
            this.{{builtName}}=true;
            var __builder=new global::Seamless.System.Carrier.LookupBuilder<{{keyType}}, {{elementType}}>(global::System.Collections.Generic.EqualityComparer<{{keyType}}>.Default);
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

    public static string IndentBlock(string text,string indent){
        return string.Join("\n",text.Replace("\r\n","\n").Split('\n').Select(line=>line.Length==0 ? line : indent+line));
    }
}

internal class GroupBy_keySelector(int 番号, Enumerable source, FuncFragment keySelector) : Enumerable(番号) {
    private string KeySelectorFuncType => $"global::System.Func<{source.ElementType}, {FuncReturnTypeName(keySelector)}>";
    private string KeyType => FuncReturnTypeName(keySelector);
    protected override Enumerable PrimarySource => source;
    public override string GetEnumerableExpression =>
        $"global::Seamless.System.Carrier.ValueEnumerableExtensions.GroupBy({source.GetEnumerableExpression}, {keySelector.DelegateConstructorArg ?? keySelector.RenderAsLambda()})";
    public override string StructMethod => FuncSupport(keySelector,nameof(keySelector));
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression =>
        $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(keySelector)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {e}{ClosurePrimaryCtorSuffix(keySelector)}";
    public override string GetEnumeratorFields =>
        GroupBySupport.Fields(KeyType,source.ElementType,group,lookup,hasValue);
    public override string GetTryGetNextBody => GroupBySupport.BuildAndAdvance(e,source.CanTryProvideSpan,KeyType,source.ElementType,FuncAssign(keySelector,nameof(keySelector),key,item),"",item,group,lookup,hasValue,item,key)+$$"""
        current=this.Current=__cur;
        return true;
        """;
    public override string GetCurrentExpression => currentValue;
    public override string ElementType => $"global::System.Linq.IGrouping<{KeyType}, {source.ElementType}>";
}

internal class GroupBy_keySelector_resultSelector(
    int 番号,
    Enumerable source,
    FuncFragment keySelector,
    FuncFragment resultSelector)
    : Enumerable(番号) {
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length == 2
        && method.Parameters[1].Type is INamedTypeSymbol { TypeArguments.Length: 3 };
    protected override Enumerable PrimarySource => source;
    private string KeyType => FuncReturnTypeName(keySelector);
    private bool UseResultTypeParam => resultSelector.RequiresResultTypeParameter;
    public override string ElementType => UseResultTypeParam ? "TResult" : FuncReturnTypeName(resultSelector);
    protected override string StructTypeParameters =>
        UseResultTypeParam ? "<TEnum, TResult>" : base.StructTypeParameters;
    protected override string StructTypeConstraints =>
        UseResultTypeParam
            ? $"where TEnum : struct, global::System.Collections.Generic.IEnumerator<{source.ElementType}>, global::Seamless.System.Carrier.IValueEnumerator<{source.ElementType}>"
            : base.StructTypeConstraints;
    public override string EnumeratorTypeName =>
        UseResultTypeParam ? $"{Struct}<{source.EnumeratorTypeName}, TResult>" : base.EnumeratorTypeName;
    private string FactorySource => !UseResultTypeParam ? string.Empty : $$"""
        {{AggressiveInliningAttribute}}
        private static global::Seamless.System.Carrier.ValueEnumerable<{{Struct}}<TEnum, TResult>, TResult> {{FactoryMethodName}}<TEnum, TResult>(TEnum source{{ClosurePrimaryCtorSuffix(keySelector, resultSelector)}})
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<{{source.ElementType}}>, global::Seamless.System.Carrier.IValueEnumerator<{{source.ElementType}}>
            => new(new {{Struct}}<TEnum, TResult>(source{{ClosureArgsSuffixForFactory(keySelector, resultSelector)}}));
        """;
    public override string StructMethod => JoinStaticMethods(JoinStaticMethods(FuncSupport(keySelector, nameof(keySelector)),FuncSupport(resultSelector, nameof(resultSelector))),FactorySource);
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression =>
        UseResultTypeParam
            ? throw new NotSupportedException($"{GetType().Name} does not support direct GetEnumeratorExpression for anonymous result type; use GetEnumerableExpression.")
            : $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(keySelector, resultSelector)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {e}{ClosurePrimaryCtorSuffix(keySelector, resultSelector)}";
    public override string GetEnumeratorFields =>
        GroupBySupport.Fields(KeyType,source.ElementType,group,lookup,hasValue);
    public override string GetTryGetNextBody => GroupBySupport.BuildAndAdvance(e,source.CanTryProvideSpan,KeyType,source.ElementType,FuncAssign(keySelector,nameof(keySelector),key,item),"",item,group,lookup,hasValue,item,key)+$$"""
        {{FuncAssignCurrentIndented("",resultSelector,nameof(resultSelector),currentValue,"__cur.Key",$"(global::System.Collections.Generic.IEnumerable<{source.ElementType}>)__cur")}}
        return true;
        """;
    public override string GetCurrentExpression => currentValue;
    public override string GetEnumerableExpression =>
        UseResultTypeParam
            ? $"{FactoryMethodName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(keySelector, resultSelector)})"
            : $"global::Seamless.System.Carrier.ValueEnumerableExtensions.GroupBy({source.GetEnumerableExpression}, {keySelector.DelegateConstructorArg ?? keySelector.RenderAsLambda()}, {resultSelector.DelegateConstructorArg ?? resultSelector.RenderAsLambda()})";
}

internal class GroupBy_keySelector_elementSelector(
    int 番号,
    Enumerable source,
    FuncFragment keySelector,
    FuncFragment elementSelector)
    : Enumerable(番号) {
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length == 2
        && method.Parameters[1].Type is INamedTypeSymbol { TypeArguments.Length: 2 };
    protected override Enumerable PrimarySource => source;
    private string KeyType => FuncReturnTypeName(keySelector);
    private string ElemType => FuncReturnTypeName(elementSelector);
    public override string GetEnumerableExpression =>
        $"global::Seamless.System.Carrier.ValueEnumerableExtensions.GroupBy({source.GetEnumerableExpression}, {keySelector.DelegateConstructorArg ?? keySelector.RenderAsLambda()}, {elementSelector.DelegateConstructorArg ?? elementSelector.RenderAsLambda()})";
    public override string StructMethod => JoinStaticMethods(FuncSupport(keySelector, nameof(keySelector)),FuncSupport(elementSelector, nameof(elementSelector)));
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression =>
        $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(keySelector, elementSelector)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {e}{ClosurePrimaryCtorSuffix(keySelector, elementSelector)}";
    public override string GetEnumeratorFields =>
        GroupBySupport.Fields(KeyType,ElemType,group,lookup,hasValue);
    public override string GetTryGetNextBody => GroupBySupport.BuildAndAdvance(e,source.CanTryProvideSpan,KeyType,ElemType,FuncAssign(keySelector,nameof(keySelector),key,item),$"{ElemType} {a}=default!;\n{FuncAssign(elementSelector,nameof(elementSelector),a,item)}",a,group,lookup,hasValue,item,key)+$$"""
        current=this.Current=__cur;
        return true;
        """;
    public override string GetCurrentExpression => currentValue;
    public override string ElementType => $"global::System.Linq.IGrouping<{KeyType}, {ElemType}>";
}

internal class GroupBy_keySelector_elementSelector_resultSelector(
    int 番号,
    Enumerable source,
    FuncFragment keySelector,
    FuncFragment elementSelector,
    FuncFragment resultSelector)
    : Enumerable(番号) {
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length == 3
        && method.Parameters[2].Type is INamedTypeSymbol { TypeArguments.Length: 3 };
    protected override Enumerable PrimarySource => source;
    private bool UseResultTypeParam => resultSelector.RequiresResultTypeParameter;
    private string KeyType => FuncReturnTypeName(keySelector);
    private string ElemType => FuncReturnTypeName(elementSelector);
    public override string ElementType => UseResultTypeParam ? "TResult" : FuncReturnTypeName(resultSelector);
    protected override string StructTypeParameters =>
        UseResultTypeParam ? "<TEnum, TResult>" : base.StructTypeParameters;
    protected override string StructTypeConstraints =>
        UseResultTypeParam
            ? $"where TEnum : struct, global::System.Collections.Generic.IEnumerator<{source.ElementType}>, global::Seamless.System.Carrier.IValueEnumerator<{source.ElementType}>"
            : base.StructTypeConstraints;
    public override string EnumeratorTypeName =>
        UseResultTypeParam ? $"{Struct}<{source.EnumeratorTypeName}, TResult>" : base.EnumeratorTypeName;
    private string FactorySource => !UseResultTypeParam ? string.Empty : $$"""
        {{AggressiveInliningAttribute}}
        private static global::Seamless.System.Carrier.ValueEnumerable<{{Struct}}<TEnum, TResult>, TResult> {{FactoryMethodName}}<TEnum, TResult>(TEnum source{{ClosurePrimaryCtorSuffix(keySelector, elementSelector, resultSelector)}})
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<{{source.ElementType}}>, global::Seamless.System.Carrier.IValueEnumerator<{{source.ElementType}}>
            => new(new {{Struct}}<TEnum, TResult>(source{{ClosureArgsSuffixForFactory(keySelector, elementSelector, resultSelector)}}));
        """;
    public override string StructMethod => JoinStaticMethods(JoinStaticMethods(JoinStaticMethods(FuncSupport(keySelector, nameof(keySelector)),FuncSupport(elementSelector, nameof(elementSelector))),FuncSupport(resultSelector, nameof(resultSelector))),FactorySource);
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression =>
        UseResultTypeParam
            ? throw new NotSupportedException($"{GetType().Name} does not support direct GetEnumeratorExpression for anonymous result type; use GetEnumerableExpression.")
            : $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(keySelector, elementSelector, resultSelector)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {e}{ClosurePrimaryCtorSuffix(keySelector, elementSelector, resultSelector)}";
    public override string GetEnumeratorFields =>
        GroupBySupport.Fields(KeyType,ElemType,group,lookup,hasValue);
    public override string GetTryGetNextBody => GroupBySupport.BuildAndAdvance(e,source.CanTryProvideSpan,KeyType,ElemType,FuncAssign(keySelector,nameof(keySelector),key,item),$"{ElemType} {a}=default!;\n{FuncAssign(elementSelector,nameof(elementSelector),a,item)}",a,group,lookup,hasValue,item,key)+$$"""
        {{FuncAssignCurrentIndented("",resultSelector,nameof(resultSelector),currentValue,"__cur.Key",$"(global::System.Collections.Generic.IEnumerable<{ElemType}>)__cur")}}
        return true;
        """;
    public override string GetCurrentExpression => currentValue;
    public override string GetEnumerableExpression =>
        UseResultTypeParam
            ? $"{FactoryMethodName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(keySelector, elementSelector, resultSelector)})"
            : $"global::Seamless.System.Carrier.ValueEnumerableExtensions.GroupBy({source.GetEnumerableExpression}, {keySelector.DelegateConstructorArg ?? keySelector.RenderAsLambda()}, {elementSelector.DelegateConstructorArg ?? elementSelector.RenderAsLambda()}, {resultSelector.DelegateConstructorArg ?? resultSelector.RenderAsLambda()})";
}
