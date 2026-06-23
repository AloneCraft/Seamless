namespace Seamless.FLinq.Linq.Fragments;

internal class RightJoin(int 番号,Enumerable outer,Enumerable inner,FuncFragment outerKeySelector,FuncFragment innerKeySelector,FuncFragment resultSelector,string comparer)
    : JoinBase(番号,outer,inner,outerKeySelector,innerKeySelector,resultSelector,comparer) {
    public override string ElementType=> UseResultTypeParam ? "TResult" : FuncReturnTypeName(resultSelector);
    protected override string StructTypeParameters=> UseResultTypeParam ? "<TOuterEnum,TInnerEnum,TResult>" : "<TOuterEnum,TInnerEnum>";
    protected override string StructTypeConstraints=>
        $"where TOuterEnum:struct,global::System.Collections.Generic.IEnumerator<{outerSource.ElementType}>\n    where TInnerEnum:struct,global::System.Collections.Generic.IEnumerator<{innerSource.ElementType}>";
    public override string EnumeratorTypeName=> UseResultTypeParam ? $"{Struct}<{outerSource.EnumeratorTypeName},{innerSource.EnumeratorTypeName},TResult>" : $"{Struct}<{outerSource.EnumeratorTypeName},{innerSource.EnumeratorTypeName}>";
    private string FactorySource=>!UseResultTypeParam ? string.Empty : $$"""
        {{AggressiveInliningAttribute}}
        private static global::Seamless.System.Carrier.ValueEnumerable<{{Struct}}<TOuterEnum,TInnerEnum,TResult>,TResult> {{FactoryMethodName}}<TOuterEnum,TInnerEnum,TResult>(TOuterEnum outer,TInnerEnum inner,global::System.Collections.Generic.IEqualityComparer<{{FuncReturnTypeName(outerKey)}}>? comparer{{ClosurePrimaryCtorSuffix(outerKey,innerKey,resultSelector)}})
            where TOuterEnum:struct,global::System.Collections.Generic.IEnumerator<{{outerSource.ElementType}}>
            where TInnerEnum:struct,global::System.Collections.Generic.IEnumerator<{{innerSource.ElementType}}>
            => new(new {{Struct}}<TOuterEnum,TInnerEnum,TResult>(outer,inner,comparer{{ClosureArgsSuffixForFactory(outerKey,innerKey,resultSelector)}}));
        """;
    public override string StructMethod=>JoinStaticMethods(JoinStaticMethods(JoinStaticMethods(
        FuncSupport(outerKey,nameof(outerKey)),
        FuncSupport(innerKey,nameof(innerKey))),
        FuncSupport(resultSelector,nameof(resultSelector))),
        FactorySource);
    public override string GetEnumeratorStruct=>$$"""
        {{outerSource.GetEnumeratorStruct}}
        {{innerSource.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression=>$"new {EnumeratorTypeName}({outerSource.GetEnumeratorExpression},{innerSource.GetEnumeratorExpression},{comparerExpression}{ClosureArgsSuffix(outerKey,innerKey,resultSelector)})";
    public override string GetEnumeratorPrimaryConstructorParameters=>$"TOuterEnum {e},TInnerEnum {innerEnumerator},global::System.Collections.Generic.IEqualityComparer<{FuncReturnTypeName(outerKey)}>? comparer{ClosurePrimaryCtorSuffix(outerKey,innerKey,resultSelector)}";
    public override string GetEnumerableExpression=> UseResultTypeParam ? $"{FactoryMethodName}({outerSource.GetEnumeratorExpression},{innerSource.GetEnumeratorExpression},{comparerExpression}{ClosureArgsSuffix(outerKey,innerKey,resultSelector)})" : base.GetEnumerableExpression;
    public override string GetEnumeratorConstructor=>$"this.{lookup}=BuildOuterLookup(this.{e},this.comparer{ClosureArgsSuffixForCtorBody(outerKey,innerKey,resultSelector)});";
    public override string GetEnumeratorFields=>$$"""
        private global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(outerKey)}},global::System.Collections.Generic.List<{{outerSource.ElementType}}>> {{lookup}};
        private global::System.Collections.Generic.List<{{outerSource.ElementType}}>? {{group}};
        private int {{groupIndex}};
        private int {{state}};
        private {{innerSource.ElementType}} {{outerValue}};
        private static bool IsNullKey<T>(T key){
            return key is null;
        }
        private static global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(outerKey)}},global::System.Collections.Generic.List<{{outerSource.ElementType}}>> BuildOuterLookup(TOuterEnum outer,global::System.Collections.Generic.IEqualityComparer<{{FuncReturnTypeName(outerKey)}}>? comparer{{ClosureMethodParameterSuffix(outerKey,innerKey,resultSelector)}}){
            var {{lookup}}=new global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(outerKey)}},global::System.Collections.Generic.List<{{outerSource.ElementType}}>>(comparer ?? global::System.Collections.Generic.EqualityComparer<{{FuncReturnTypeName(outerKey)}}>.Default);
            using var {{e}}=outer;
            while({{e}}.MoveNext()){
                {{CounterCall("IncrementLogicalRightJoinOuter")}}
                var {{item}}={{e}}.Current;
                {{FuncReturnTypeName(outerKey)}} {{key}}=default!;
                {{FuncAssignStaticIndented("        ",outerKey,nameof(outerKey),key,item)}}
                if(IsNullKey({{key}})){
                    continue;
                }
                {{CounterCall("IncrementHashBuild")}}
                if(!{{lookup}}.TryGetValue({{key}},out var {{group}})){
                    {{group}}=new global::System.Collections.Generic.List<{{outerSource.ElementType}}>();
                    {{lookup}}.Add({{key}},{{group}});
                }
                {{CounterCall("IncrementBufferAdd")}}
                {{group}}.Add({{item}});
            }
            return {{lookup}};
        }
        """;
    public override string GetTryGetNextBody=>$$"""
        {{Start}}:
        if({{state}}==1&&{{group}} is not null){
            if({{groupIndex}}<{{group}}.Count){
                var {{item}}={{group}}[{{groupIndex}}++];
                {{FuncAssignCurrentIndented("        ",resultSelector,nameof(resultSelector),$"this.{currentValue}",item,outerValue)}}
                return true;
            }
            {{group}}=null;
            {{state}}=0;
        }
        if(!{{innerEnumerator}}.MoveNext()){
            current=default!;
            return false;
        }
        {{CounterCall("IncrementLogicalRightJoinInner")}}
        {{outerValue}}={{innerEnumerator}}.Current;
        {{FuncReturnTypeName(innerKey)}} {{key}}=default!;
        {{FuncAssignIndented("",innerKey,nameof(innerKey),key,outerValue)}}
        {{CounterCall("IncrementHashProbe")}}
        if(!IsNullKey({{key}})&&{{lookup}}.TryGetValue({{key}},out {{group}})){
            {{groupIndex}}=0;
            {{state}}=1;
            goto {{Start}};
        }
        {{FuncAssignCurrentIndented("",resultSelector,nameof(resultSelector),$"this.{currentValue}",$"default({outerSource.ElementType})",outerValue)}}
        return true;
        """;
    public override string GetCurrentExpression=> currentValue;
    protected override string GetEnumeratorDisposeBody=>$"{innerEnumerator}.Dispose();";
}
