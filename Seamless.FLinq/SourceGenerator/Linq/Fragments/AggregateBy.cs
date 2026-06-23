namespace Seamless.FLinq.Linq.Fragments;
/*
AggregateBy は key ごとに accumulator を持つ grouping 集約を生成する。

入力例:
  source.AggregateBy(x => x.GroupId, seed, (acc, item) => Step(acc, item))

生成例:
  foreach(var item in source) {
      key = keySelector(item);
      if(!map.TryGetValue(key, out acc)) acc = seed;
      map[key] = func(acc, item);
  }
  foreach(var pair in map) current = pair;

バリエーション:
  - keySelector/func はそれぞれ FuncFragment として closure/delegate/inline に対応する。
  - resultElementType は返す KeyValuePair 相当の要素型を決める。
*/
public class AggregateBy(int 番号, Enumerable source, FuncFragment keySelector, string seed, FuncFragment func, string resultElementType) : Enumerable(番号) {
    protected override Enumerable PrimarySource => source;
    private string KeyType => FuncReturnTypeName(keySelector);

    public override string StructMethod => $$"""
        {{FuncSupport(keySelector,nameof(keySelector))}}
        {{FuncSupport(func,nameof(func))}}
        """;
    public override string GetEnumerableExpression =>
        $"global::Seamless.System.Carrier.ValueEnumerableExtensions.AggregateBy({source.GetEnumerableExpression}, {keySelector.DelegateConstructorArg ?? keySelector.RenderAsLambda()}, {seed}, {func.DelegateConstructorArg ?? func.RenderAsLambda()})";
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression =>
        $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(keySelector,func)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {e}{ClosurePrimaryCtorSuffix(keySelector,func)}";
    public override string GetEnumeratorFields => $$"""
        private global::System.Collections.Generic.Dictionary<{{KeyType}}, {{FuncReturnTypeName(func)}}>? {{lookup}};
        private global::System.Collections.Generic.Dictionary<{{KeyType}}, {{FuncReturnTypeName(func)}}>.Enumerator {{innerEnumerator}};
        """;
    public override string GetTryGetNextBody => $$"""
        if(this.{{lookup}} is null) {
            if({{e}}.TryGetSpan(out var __span)) {
                this.{{lookup}}=new global::System.Collections.Generic.Dictionary<{{KeyType}}, {{FuncReturnTypeName(func)}}>(__span.Length);
                for(var __i=0;__i<__span.Length;__i++) {
                    var {{item}}=__span[__i];
                    {{KeyType}} {{key}}=default!;
                    {{FuncAssignIndented("            ",keySelector,nameof(keySelector),key,item)}}
                    ref var __agg=ref global::System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrAddDefault(this.{{lookup}},{{key}},out var __exists);
                    if(!__exists) {
                        __agg={{seed}};
                    }
                    {{FuncAssignIndented("            ",func,nameof(func),"__agg","__agg",item)}}
                }
            }else{
                if({{e}}.TryGetNonEnumeratedCount(out var __count)) {
                    this.{{lookup}}=new global::System.Collections.Generic.Dictionary<{{KeyType}}, {{FuncReturnTypeName(func)}}>(__count);
                }else{
                    this.{{lookup}}=new global::System.Collections.Generic.Dictionary<{{KeyType}}, {{FuncReturnTypeName(func)}}>();
                }
                while({{e}}.MoveNext()) {
                    var {{item}}={{e}}.Current;
                    {{KeyType}} {{key}}=default!;
                    {{FuncAssignIndented("            ",keySelector,nameof(keySelector),key,item)}}
                    ref var __agg=ref global::System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrAddDefault(this.{{lookup}},{{key}},out var __exists);
                    if(!__exists) {
                        __agg={{seed}};
                    }
                    {{FuncAssignIndented("            ",func,nameof(func),"__agg","__agg",item)}}
                }
            }
            this.{{innerEnumerator}}=this.{{lookup}}.GetEnumerator();
        }
        if(this.{{innerEnumerator}}.MoveNext()) {
            var __kv=this.{{innerEnumerator}}.Current;
            current=this.{{currentValue}}=new {{resultElementType}}(__kv.Key,__kv.Value);
            return true;
        }
        current=default!;
        return false;
        """;
    public override string GetCurrentExpression => currentValue;
    public override string ElementType => resultElementType;
}
