namespace Seamless.FLinq.Linq.Fragments;

/*
JoinBase は Join/GroupJoin/LeftJoin/RightJoin 系の lookup 構築を共有する基底。

入力例:
  outer.Join(inner, o => o.Id, i => i.ParentId, (o, i) => ...)

生成例:
  var lookup = BuildLookup(inner, comparer);
  while(outer.MoveNext()) {
      var outerKey = outerKeySelector(outer.Current);
      if(lookup.TryGetValue(outerKey, out var group)) {
          foreach(var innerItem in group) {
              current = resultSelector(outer.Current, innerItem);
              return true;
          }
      }
  }

バリエーション:
  - Join は inner lookup の一致要素を順に返す。
  - GroupJoin は一致 group 全体を resultSelector に渡す。
  - LeftJoin/RightJoin は一致しない側の default/null 相当も状態機械で返す。
  - resultSelector が匿名型を返す場合は TResult factory か delegate 保持に寄せる。
*/
internal abstract class JoinBase(int 番号,Enumerable outer,Enumerable inner,FuncFragment outerKeySelector,FuncFragment innerKeySelector,FuncFragment resultSelector,string comparer):Enumerable(番号){
    protected readonly Enumerable outerSource=outer;
    protected readonly Enumerable innerSource=inner;
    protected readonly FuncFragment outerKey=outerKeySelector;
    protected readonly FuncFragment innerKey=innerKeySelector;
    protected readonly FuncFragment resultSelector=resultSelector;
    protected readonly string comparerExpression=comparer;
    protected override Enumerable PrimarySource=> outerSource;
    protected bool UseResultTypeParam=> resultSelector.RequiresResultTypeParameter;
    protected string InnerKeyType=>FuncReturnTypeName(innerKey);
    protected string InnerElementType=> innerSource.ElementType;
    protected string OuterElementType=> outerSource.ElementType;
    protected string BuildLookupBody(bool skipNullKeyCheck,string logicalInnerCounterMethod)=>skipNullKeyCheck ? $$"""
        private static global::System.Collections.Generic.Dictionary<{{InnerKeyType}},global::System.Collections.Generic.List<{{InnerElementType}}>> BuildLookup(TInnerEnum inner,global::System.Collections.Generic.IEqualityComparer<{{InnerKeyType}}>? comparer{{ClosureMethodParameterSuffix(outerKey,innerKey,resultSelector)}}){
            var {{lookup}}=new global::System.Collections.Generic.Dictionary<{{InnerKeyType}},global::System.Collections.Generic.List<{{InnerElementType}}>>(comparer ?? global::System.Collections.Generic.EqualityComparer<{{InnerKeyType}}>.Default);
            using var {{innerEnumerator}}=inner;
            while({{innerEnumerator}}.MoveNext()){
                {{CounterCall(logicalInnerCounterMethod)}}
                var {{item}}={{innerEnumerator}}.Current;
                {{InnerKeyType}} {{key}}=default!;
                {{FuncAssignStaticIndented("        ",innerKey,nameof(innerKey),key,item)}}
                if(IsNullKey({{key}})){
                    continue;
                }
                {{CounterCall("IncrementHashBuild")}}
                if(!{{lookup}}.TryGetValue({{key}},out var {{group}})){
                    {{group}}=new global::System.Collections.Generic.List<{{InnerElementType}}>();
                    {{lookup}}.Add({{key}},{{group}});
                }
                {{CounterCall("IncrementBufferAdd")}}
                {{group}}.Add({{item}});
            }
            return {{lookup}};
        }
        """ : $$"""
        private static global::System.Collections.Generic.Dictionary<{{InnerKeyType}},global::System.Collections.Generic.List<{{InnerElementType}}>> BuildLookup(TInnerEnum inner,global::System.Collections.Generic.IEqualityComparer<{{InnerKeyType}}>? comparer{{ClosureMethodParameterSuffix(outerKey,innerKey,resultSelector)}}){
            var {{lookup}}=new global::System.Collections.Generic.Dictionary<{{InnerKeyType}},global::System.Collections.Generic.List<{{InnerElementType}}>>(comparer ?? global::System.Collections.Generic.EqualityComparer<{{InnerKeyType}}>.Default);
            using var {{innerEnumerator}}=inner;
            while({{innerEnumerator}}.MoveNext()){
                {{CounterCall(logicalInnerCounterMethod)}}
                var {{item}}={{innerEnumerator}}.Current;
                {{InnerKeyType}} {{key}}=default!;
                {{FuncAssignStaticIndented("        ",innerKey,nameof(innerKey),key,item)}}
                {{CounterCall("IncrementHashBuild")}}
                if(!{{lookup}}.TryGetValue({{key}},out var {{group}})){
                    {{group}}=new global::System.Collections.Generic.List<{{InnerElementType}}>();
                    {{lookup}}.Add({{key}},{{group}});
                }
                {{CounterCall("IncrementBufferAdd")}}
                {{group}}.Add({{item}});
            }
            return {{lookup}};
        }
        """;
    protected static string BuildFactorySource(string structName,string factoryMethodName,string outerTypeName,string innerTypeName,string resultTypeName,string comparerTypeName,string outerCtorSuffix,string innerCtorSuffix,string resultCtorSuffix,string outerFactoryArgsSuffix,string innerFactoryArgsSuffix,string resultFactoryArgsSuffix,bool useResultTypeParam)=>!useResultTypeParam ? string.Empty : $$"""
        {{AggressiveInliningAttribute}}
        private static global::Seamless.System.Carrier.ValueEnumerable<{{structName}}<TOuterEnum,TInnerEnum,TResult>,TResult> {{factoryMethodName}}<TOuterEnum,TInnerEnum,TResult>(TOuterEnum outer,TInnerEnum inner,global::System.Collections.Generic.IEqualityComparer<{{comparerTypeName}}>? comparer{{outerCtorSuffix}}{{innerCtorSuffix}}{{resultCtorSuffix}})
            where TOuterEnum : struct, global::System.Collections.Generic.IEnumerator<{{outerTypeName}}>
            where TInnerEnum : struct, global::System.Collections.Generic.IEnumerator<{{innerTypeName}}>
            => new(new {{structName}}<TOuterEnum,TInnerEnum,TResult>(outer,inner,comparer{{outerFactoryArgsSuffix}}{{innerFactoryArgsSuffix}}{{resultFactoryArgsSuffix}}));
        """;
}
