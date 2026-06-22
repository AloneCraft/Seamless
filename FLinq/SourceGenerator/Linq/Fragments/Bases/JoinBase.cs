namespace Seamless.FLinq.Linq.Fragments;

internal abstract class JoinBase(int 番号,Enumerable outer,Enumerable inner,FuncFragment outerKeySelector,FuncFragment innerKeySelector,FuncFragment resultSelector,string comparer):Enumerable(番号){
    protected readonly Enumerable outerSource=outer;
    protected readonly Enumerable innerSource=inner;
    protected readonly FuncFragment outerKey=outerKeySelector;
    protected readonly FuncFragment innerKey=innerKeySelector;
    protected readonly FuncFragment resultSelector=resultSelector;
    protected readonly string comparerExpression=comparer;
    protected override Enumerable PrimarySource=> this.outerSource;
    protected bool UseResultTypeParam=> this.resultSelector.RequiresResultTypeParameter;
    protected string InnerKeyType=>FuncReturnTypeName(this.innerKey);
    protected string InnerElementType=> this.innerSource.ElementType;
    protected string OuterElementType=> this.outerSource.ElementType;
    protected string BuildLookupBody(bool skipNullKeyCheck,string logicalInnerCounterMethod)=>skipNullKeyCheck ? $$"""
        private static global::System.Collections.Generic.Dictionary<{{this.InnerKeyType}},global::System.Collections.Generic.List<{{this.InnerElementType}}>> BuildLookup(TInnerEnum inner,global::System.Collections.Generic.IEqualityComparer<{{this.InnerKeyType}}>? comparer{{this.ClosureMethodParameterSuffix(this.outerKey,this.innerKey,this.resultSelector)}}){
            var {{this.lookup}}=new global::System.Collections.Generic.Dictionary<{{this.InnerKeyType}},global::System.Collections.Generic.List<{{this.InnerElementType}}>>(comparer ?? global::System.Collections.Generic.EqualityComparer<{{this.InnerKeyType}}>.Default);
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
                if(!{{this.lookup}}.TryGetValue({{this.key}},out var {{this.group}})){
                    {{this.group}}=new global::System.Collections.Generic.List<{{this.InnerElementType}}>();
                    {{this.lookup}}.Add({{this.key}},{{this.group}});
                }
                {{this.CounterCall("IncrementBufferAdd")}}
                {{this.group}}.Add({{this.item}});
            }
            return {{this.lookup}};
        }
        """ : $$"""
        private static global::System.Collections.Generic.Dictionary<{{this.InnerKeyType}},global::System.Collections.Generic.List<{{this.InnerElementType}}>> BuildLookup(TInnerEnum inner,global::System.Collections.Generic.IEqualityComparer<{{this.InnerKeyType}}>? comparer{{this.ClosureMethodParameterSuffix(this.outerKey,this.innerKey,this.resultSelector)}}){
            var {{this.lookup}}=new global::System.Collections.Generic.Dictionary<{{this.InnerKeyType}},global::System.Collections.Generic.List<{{this.InnerElementType}}>>(comparer ?? global::System.Collections.Generic.EqualityComparer<{{this.InnerKeyType}}>.Default);
            using var {{this.innerEnumerator}}=inner;
            while({{this.innerEnumerator}}.MoveNext()){
                {{this.CounterCall(logicalInnerCounterMethod)}}
                var {{this.item}}={{this.innerEnumerator}}.Current;
                {{this.InnerKeyType}} {{this.key}}=default!;
                {{this.FuncAssignStaticIndented("        ",this.innerKey,nameof(this.innerKey),this.key,this.item)}}
                {{this.CounterCall("IncrementHashBuild")}}
                if(!{{this.lookup}}.TryGetValue({{this.key}},out var {{this.group}})){
                    {{this.group}}=new global::System.Collections.Generic.List<{{this.InnerElementType}}>();
                    {{this.lookup}}.Add({{this.key}},{{this.group}});
                }
                {{this.CounterCall("IncrementBufferAdd")}}
                {{this.group}}.Add({{this.item}});
            }
            return {{this.lookup}};
        }
        """;
    protected static string BuildFactorySource(string structName,string factoryMethodName,string outerTypeName,string innerTypeName,string resultTypeName,string comparerTypeName,string outerCtorSuffix,string innerCtorSuffix,string resultCtorSuffix,string outerFactoryArgsSuffix,string innerFactoryArgsSuffix,string resultFactoryArgsSuffix,bool useResultTypeParam)=>!useResultTypeParam ? string.Empty : $$"""
        {{AggressiveInliningAttribute}}
        private static global::Seamless.Collections.Tsavorite.Carrier.ValueEnumerable<{{structName}}<TOuterEnum,TInnerEnum,TResult>,TResult> {{factoryMethodName}}<TOuterEnum,TInnerEnum,TResult>(TOuterEnum outer,TInnerEnum inner,global::System.Collections.Generic.IEqualityComparer<{{comparerTypeName}}>? comparer{{outerCtorSuffix}}{{innerCtorSuffix}}{{resultCtorSuffix}})
            where TOuterEnum : struct, global::System.Collections.Generic.IEnumerator<{{outerTypeName}}>
            where TInnerEnum : struct, global::System.Collections.Generic.IEnumerator<{{innerTypeName}}>
            => new(new {{structName}}<TOuterEnum,TInnerEnum,TResult>(outer,inner,comparer{{outerFactoryArgsSuffix}}{{innerFactoryArgsSuffix}}{{resultFactoryArgsSuffix}}));
        """;
}
