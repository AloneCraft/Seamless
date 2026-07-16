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
internal abstract class JoinBase(int 番号,Enumerable outer,Enumerable inner,FuncFragment outerKeySelector,FuncFragment innerKeySelector,FuncFragment resultSelector,string comparer):Enumerable(番号) {
    protected readonly Enumerable outerSource = outer;
    protected readonly Enumerable innerSource = inner;
    protected readonly FuncFragment outerKey = outerKeySelector;
    protected readonly FuncFragment innerKey = innerKeySelector;
    protected readonly FuncFragment resultSelector = resultSelector;
    protected readonly string comparerExpression = comparer;
    protected override Enumerable PrimarySource => this.outerSource;
    protected bool UseResultTypeParam => this.resultSelector.RequiresResultTypeParameter;
    protected string InnerKeyType => FuncReturnTypeName(this.innerKey);
    protected string InnerElementType => this.innerSource.ElementType;
    protected string OuterElementType => this.outerSource.ElementType;
    protected string BuildLookupBody(bool skipNullKeyCheck,string logicalInnerCounterMethod) => skipNullKeyCheck ? $$"""
        private static global::Seamless.Linq.Lookup<{{this.InnerKeyType}},{{this.InnerElementType}}> BuildLookup(TInnerEnum inner,global::System.Collections.Generic.IEqualityComparer<{{this.InnerKeyType}}>? comparer{{this.ClosureMethodParameterSuffix(this.outerKey,this.innerKey,this.resultSelector)}}){
            var __lookupBuilder=new global::Seamless.Linq.LookupBuilder<{{this.InnerKeyType}},{{this.InnerElementType}}>(0,comparer,true);
            using var {{this.innerEnumerator}}=inner;
            while({{this.innerEnumerator}}.MoveNext()){
                {{this.CounterCall(logicalInnerCounterMethod)}}
                var {{this.item}}={{this.innerEnumerator}}.Current;
                {{this.InnerKeyType}} {{this.key}}=default!;
                {{this.FuncAssignStaticIndented("        ",this.innerKey,nameof(this.innerKey),this.key,this.item)}}
                if(IsNullKey({{this.key}})){
                    continue;
                }
                {{this.CounterCall("IncrementHashBuild")}}
                {{this.CounterCall("IncrementBufferAdd")}}
                __lookupBuilder.Add({{this.key}},{{this.item}});
            }
            return __lookupBuilder.BuildPooledAndClear();
        }
        """ : $$"""
        private static global::Seamless.Linq.Lookup<{{this.InnerKeyType}},{{this.InnerElementType}}> BuildLookup(TInnerEnum inner,global::System.Collections.Generic.IEqualityComparer<{{this.InnerKeyType}}>? comparer{{this.ClosureMethodParameterSuffix(this.outerKey,this.innerKey,this.resultSelector)}}){
            var __lookupBuilder=new global::Seamless.Linq.LookupBuilder<{{this.InnerKeyType}},{{this.InnerElementType}}>(0,comparer,true);
            using var {{this.innerEnumerator}}=inner;
            while({{this.innerEnumerator}}.MoveNext()){
                {{this.CounterCall(logicalInnerCounterMethod)}}
                var {{this.item}}={{this.innerEnumerator}}.Current;
                {{this.InnerKeyType}} {{this.key}}=default!;
                {{this.FuncAssignStaticIndented("        ",this.innerKey,nameof(this.innerKey),this.key,this.item)}}
                {{this.CounterCall("IncrementHashBuild")}}
                {{this.CounterCall("IncrementBufferAdd")}}
                __lookupBuilder.Add({{this.key}},{{this.item}});
            }
            return __lookupBuilder.BuildPooledAndClear();
        }
        """;
    protected static string BuildFactorySource(string structName,string factoryMethodName,string outerTypeName,string innerTypeName,string resultTypeName,string comparerTypeName,string outerCtorSuffix,string innerCtorSuffix,string resultCtorSuffix,string outerFactoryArgsSuffix,string innerFactoryArgsSuffix,string resultFactoryArgsSuffix,bool useResultTypeParam) => !useResultTypeParam ? string.Empty : $$"""
        {{AggressiveInliningAttribute}}
        private static global::Seamless.Linq.ValueEnumerable<{{structName}}<TOuterEnum,TInnerEnum,TResult>,TResult> {{factoryMethodName}}<TOuterEnum,TInnerEnum,TResult>(TOuterEnum outer,TInnerEnum inner,global::System.Collections.Generic.IEqualityComparer<{{comparerTypeName}}>? comparer{{outerCtorSuffix}}{{innerCtorSuffix}}{{resultCtorSuffix}})
            where TOuterEnum : struct, global::System.Collections.Generic.IEnumerator<{{outerTypeName}}>
            where TInnerEnum : struct, global::System.Collections.Generic.IEnumerator<{{innerTypeName}}>
            => new(new {{structName}}<TOuterEnum,TInnerEnum,TResult>(outer,inner,comparer{{outerFactoryArgsSuffix}}{{innerFactoryArgsSuffix}}{{resultFactoryArgsSuffix}}));
        """;
}
