namespace Seamless.FLinq.Linq.Fragments;

internal class Join(int 番号,Enumerable outer,Enumerable inner,FuncFragment outerKeySelector,FuncFragment innerKeySelector,FuncFragment resultSelector,string comparer):JoinBase(番号,outer,inner,outerKeySelector,innerKeySelector,resultSelector,comparer){
    public override string ElementType=> UseResultTypeParam ? "TResult" : FuncReturnTypeName(resultSelector);
    protected override string StructTypeParameters=> UseResultTypeParam ? "<TOuterEnum,TInnerEnum,TResult>" : "<TOuterEnum,TInnerEnum>";
    protected override string StructTypeConstraints=>
        $"where TOuterEnum:struct,global::System.Collections.Generic.IEnumerator<{outerSource.ElementType}>\n    where TInnerEnum:struct,global::System.Collections.Generic.IEnumerator<{innerSource.ElementType}>";
    public override string EnumeratorTypeName=> UseResultTypeParam ? $"{Struct}<{outerSource.EnumeratorTypeName},{innerSource.EnumeratorTypeName},TResult>" : $"{Struct}<{outerSource.EnumeratorTypeName},{innerSource.EnumeratorTypeName}>";
    private string FactorySource=>!UseResultTypeParam ? string.Empty : $$"""
        {{AggressiveInliningAttribute}}
        private static global::Seamless.System.Carrier.ValueEnumerable<{{Struct}}<TOuterEnum,TInnerEnum,TResult>,TResult> {{FactoryMethodName}}<TOuterEnum,TInnerEnum,TResult>(TOuterEnum outer,TInnerEnum inner,global::System.Collections.Generic.IEqualityComparer<{{FuncReturnTypeName(innerKey)}}>? comparer{{ClosurePrimaryCtorSuffix(outerKey,innerKey,resultSelector)}})
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
    public override string GetEnumeratorPrimaryConstructorParameters=>$"TOuterEnum {e},TInnerEnum {innerEnumerator},global::System.Collections.Generic.IEqualityComparer<{FuncReturnTypeName(innerKey)}>? comparer{ClosurePrimaryCtorSuffix(outerKey,innerKey,resultSelector)}";
    public override string GetEnumerableExpression=> UseResultTypeParam ? $"{FactoryMethodName}({outerSource.GetEnumeratorExpression},{innerSource.GetEnumeratorExpression},{comparerExpression}{ClosureArgsSuffix(outerKey,innerKey,resultSelector)})" : base.GetEnumerableExpression;
    public override string GetEnumeratorConstructor=>$"this.{lookup}=BuildLookup(this.{innerEnumerator},this.comparer{ClosureArgsSuffixForCtorBody(outerKey,innerKey,resultSelector)});";
    public override string GetEnumeratorFields=>$$"""
        private global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(innerKey)}},global::System.Collections.Generic.List<{{innerSource.ElementType}}>> {{lookup}};
        private global::System.Collections.Generic.List<{{innerSource.ElementType}}>? {{group}};
        private int {{groupIndex}};
        private int {{state}};
        private {{outerSource.ElementType}} {{outerValue}};
        private static bool IsNullKey<T>(T key){
            return key is null;
        }
        {{BuildLookupBody(true,"IncrementLogicalJoinInner")}}
        """;
    public override string GetTryGetNextBody=>$$"""
        {{Start}}:
        if({{state}}==1&&{{group}} is not null){
            if({{groupIndex}}<{{group}}.Count){
                var {{item}}={{group}}[{{groupIndex}}++];
                {{FuncAssignCurrentIndented("        ",resultSelector,nameof(resultSelector),$"this.{currentValue}",outerValue,item)}}
                return true;
            }
            {{group}}=null;
            {{state}}=0;
        }
        if(!{{e}}.MoveNext()){
            current=default!;
            return false;
        }
        {{CounterCall("IncrementLogicalJoinOuter")}}
        {{outerValue}}={{e}}.Current;
        {{FuncReturnTypeName(outerKey)}} {{key}}=default!;
        {{FuncAssignIndented("",outerKey,nameof(outerKey),key,outerValue)}}
        if(IsNullKey({{key}})){
            goto {{Start}};
        }
        {{CounterCall("IncrementHashProbe")}}
        if(!{{lookup}}.TryGetValue({{key}},out {{group}})){
            goto {{Start}};
        }
        {{groupIndex}}=0;
        {{state}}=1;
        goto {{Start}};
        """;
    public override string GetCurrentExpression=> currentValue;
    protected override string GetEnumeratorDisposeBody=>$"{e}.Dispose();";
}
