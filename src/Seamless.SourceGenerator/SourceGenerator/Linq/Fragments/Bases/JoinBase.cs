namespace Seamless.SourceGenerator.Linq.Fragments;

internal abstract class JoinBase(int 番号,Enumerable outer,Enumerable inner,FuncFragment outerKeySelector,FuncFragment innerKeySelector,FuncFragment resultSelector,string comparer):Enumerable(番号){
    protected readonly Enumerable outerSource=outer;
    protected readonly Enumerable innerSource=inner;
    protected readonly FuncFragment outerKey=outerKeySelector;
    protected readonly FuncFragment innerKey=innerKeySelector;
    protected readonly FuncFragment resultSelector=resultSelector;
    protected readonly string comparerExpression=comparer;
    protected override Enumerable PrimarySource=>outerSource;
    protected bool UseResultTypeParam=>resultSelector.RequiresResultTypeParameter;
    protected string InnerKeyType=>FuncReturnTypeName(innerKey);
    protected string InnerElementType=>innerSource.ElementType;
    protected string OuterElementType=>outerSource.ElementType;
    protected string BuildLookupBody(bool skipNullKeyCheck)=>skipNullKeyCheck ? $$"""
        private static global::System.Collections.Generic.Dictionary<{{InnerKeyType}},global::System.Collections.Generic.List<{{InnerElementType}}>> BuildLookup(TInnerEnum inner,global::System.Collections.Generic.IEqualityComparer<{{InnerKeyType}}>? comparer{{ClosureMethodParameterSuffix(outerKey,innerKey,resultSelector)}}){
            var {{lookup}}=new global::System.Collections.Generic.Dictionary<{{InnerKeyType}},global::System.Collections.Generic.List<{{InnerElementType}}>>(comparer ?? global::System.Collections.Generic.EqualityComparer<{{InnerKeyType}}>.Default);
            using var {{innerEnumerator}}=inner;
            while({{innerEnumerator}}.MoveNext()){
                var {{item}}={{innerEnumerator}}.Current;
                {{InnerKeyType}} {{key}}=default!;
                {{FuncAssignStatic(innerKey,nameof(innerKey),key,item)}}
                if(IsNullKey({{key}})){
                    continue;
                }
                if(!{{lookup}}.TryGetValue({{key}},out var {{group}})){
                    {{group}}=new global::System.Collections.Generic.List<{{InnerElementType}}>();
                    {{lookup}}.Add({{key}},{{group}});
                }
                {{group}}.Add({{item}});
            }
            return {{lookup}};
        }
        """ : $$"""
        private static global::System.Collections.Generic.Dictionary<{{InnerKeyType}},global::System.Collections.Generic.List<{{InnerElementType}}>> BuildLookup(TInnerEnum inner,global::System.Collections.Generic.IEqualityComparer<{{InnerKeyType}}>? comparer{{ClosureMethodParameterSuffix(outerKey,innerKey,resultSelector)}}){
            var {{lookup}}=new global::System.Collections.Generic.Dictionary<{{InnerKeyType}},global::System.Collections.Generic.List<{{InnerElementType}}>>(comparer ?? global::System.Collections.Generic.EqualityComparer<{{InnerKeyType}}>.Default);
            using var {{innerEnumerator}}=inner;
            while({{innerEnumerator}}.MoveNext()){
                var {{item}}={{innerEnumerator}}.Current;
                {{InnerKeyType}} {{key}}=default!;
                {{FuncAssignStatic(innerKey,nameof(innerKey),key,item)}}
                if(!{{lookup}}.TryGetValue({{key}},out var {{group}})){
                    {{group}}=new global::System.Collections.Generic.List<{{InnerElementType}}>();
                    {{lookup}}.Add({{key}},{{group}});
                }
                {{group}}.Add({{item}});
            }
            return {{lookup}};
        }
        """;
    protected static string BuildFactorySource(string structName,string factoryMethodName,string outerTypeName,string innerTypeName,string resultTypeName,string comparerTypeName,string outerCtorSuffix,string innerCtorSuffix,string resultCtorSuffix,string outerFactoryArgsSuffix,string innerFactoryArgsSuffix,string resultFactoryArgsSuffix,bool useResultTypeParam)=>!useResultTypeParam ? string.Empty : $$"""
        {{AggressiveInliningAttribute}}
        private static global::Seamless.Linq.Carrier.ValueEnumerable<{{structName}}<TOuterEnum,TInnerEnum,TResult>,TResult> {{factoryMethodName}}<TOuterEnum,TInnerEnum,TResult>(TOuterEnum outer,TInnerEnum inner,global::System.Collections.Generic.IEqualityComparer<{{comparerTypeName}}>? comparer{{outerCtorSuffix}}{{innerCtorSuffix}}{{resultCtorSuffix}})
            where TOuterEnum : struct, global::System.Collections.Generic.IEnumerator<{{outerTypeName}}>
            where TInnerEnum : struct, global::System.Collections.Generic.IEnumerator<{{innerTypeName}}>
            => new(new {{structName}}<TOuterEnum,TInnerEnum,TResult>(outer,inner,comparer{{outerFactoryArgsSuffix}}{{innerFactoryArgsSuffix}}{{resultFactoryArgsSuffix}}));
        """;
}
