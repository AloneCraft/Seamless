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
internal class AggregateBy(int 番号,Enumerable source,FuncFragment keySelector,string seed,FuncFragment func,string resultElementType):Enumerable(番号) {
    protected override Enumerable PrimarySource => source;
    private string KeyType => FuncReturnTypeName(keySelector);
    private static string CapacityExpression(string sourceCountExpression) =>
        $"global::System.Math.Min({sourceCountExpression},1024)";

    public override string StructMethod => $$"""
        {{this.FuncSupport(keySelector,nameof(keySelector))}}
        {{this.FuncSupport(func,nameof(func))}}
        """;
    public override string GetEnumerableExpression =>
        $"global::Seamless.Linq.Enumerable.AggregateBy({source.GetEnumerableExpression}, {keySelector.DelegateConstructorArg??keySelector.RenderAsLambda()}, {seed}, {func.DelegateConstructorArg??func.RenderAsLambda()})";
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression =>
        $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}{this.ClosureArgsSuffix(keySelector,func)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {this.e}{this.ClosurePrimaryCtorSuffix(keySelector,func)}";
    public override string GetEnumeratorFields => $$"""
        private global::Seamless.Linq.Internal.DictionarySlim<{{this.KeyType}},{{FuncReturnTypeName(func)}}>? {{this.lookup}};
        private global::Seamless.Linq.Internal.DictionarySlim<{{this.KeyType}},{{FuncReturnTypeName(func)}}>.Enumerator {{this.innerEnumerator}};
        """;
    public override string GetTryGetNextBody => $$"""
        if(this.{{this.lookup}} is null){
            if({{this.e}}.TryGetSpan(out var __span)){
                this.{{this.lookup}}=new global::Seamless.Linq.Internal.DictionarySlim<{{this.KeyType}},{{FuncReturnTypeName(func)}}>({{CapacityExpression("__span.Length")}});
                for(var __i=0;__i<__span.Length;__i++){
                    var {{this.item}}=__span[__i];
                    {{this.KeyType}} {{this.key}}=default!;
                    {{this.FuncAssignIndented("            ",keySelector,nameof(keySelector),this.key,this.item)}}
                    ref var __agg=ref this.{{this.lookup}}.GetValueRefOrAddDefault({{this.key}},out var __exists);
                    if(!__exists){
                        __agg={{seed}};
                    }
                    {{this.FuncAssignIndented("            ",func,nameof(func),"__agg","__agg",this.item)}}
                }
            }else{
                if({{this.e}}.TryGetNonEnumeratedCount(out var __count)){
                    this.{{this.lookup}}=new global::Seamless.Linq.Internal.DictionarySlim<{{this.KeyType}},{{FuncReturnTypeName(func)}}>({{CapacityExpression("__count")}});
                }else{
                    this.{{this.lookup}}=new global::Seamless.Linq.Internal.DictionarySlim<{{this.KeyType}},{{FuncReturnTypeName(func)}}>();
                }
                while({{this.e}}.TryGetNext(out var __mn0)){
                    var {{this.item}}=__mn0;
                    {{this.KeyType}} {{this.key}}=default!;
                    {{this.FuncAssignIndented("            ",keySelector,nameof(keySelector),this.key,this.item)}}
                    ref var __agg=ref this.{{this.lookup}}.GetValueRefOrAddDefault({{this.key}},out var __exists);
                    if(!__exists){
                        __agg={{seed}};
                    }
                    {{this.FuncAssignIndented("            ",func,nameof(func),"__agg","__agg",this.item)}}
                }
            }
            this.{{this.innerEnumerator}}=this.{{this.lookup}}.GetEnumerator();
        }
        if(this.{{this.innerEnumerator}}.TryGetNext(out var __kv)){
            current=this.{{this.currentValue}}=new {{resultElementType}}(__kv.Key,__kv.Value);
            return true;
        }
        current=default!;
        return false;
        """;
    public override string ElementType => resultElementType;
    protected override string GetEnumeratorDisposeBody => $"this.{this.lookup}?.Dispose(); {this.e}.Dispose();";

}
