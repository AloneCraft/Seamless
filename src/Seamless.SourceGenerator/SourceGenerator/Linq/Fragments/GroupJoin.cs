namespace Seamless.SourceGenerator.Linq.Fragments;

public class GroupJoin(int 番号,Enumerable outer,Enumerable inner,FuncFragment outerKeySelector,FuncFragment innerKeySelector,FuncFragment resultSelector,string comparer):JoinBase(番号,outer,inner,outerKeySelector,innerKeySelector,resultSelector,comparer){
    public override string ElementType=>UseResultTypeParam ? "TResult" : FuncReturnTypeName(resultSelector);
    protected override string StructTypeParameters=>UseResultTypeParam ? "<TOuterEnum,TInnerEnum,TResult>" : "<TOuterEnum,TInnerEnum>";
    protected override string StructTypeConstraints=>
        $"where TOuterEnum:struct,global::System.Collections.Generic.IEnumerator<{outerSource.ElementType}>\n    where TInnerEnum:struct,global::System.Collections.Generic.IEnumerator<{innerSource.ElementType}>";
    public override string EnumeratorTypeName=>UseResultTypeParam ? $"{Struct}<{outerSource.EnumeratorTypeName},{innerSource.EnumeratorTypeName},TResult>" : $"{Struct}<{outerSource.EnumeratorTypeName},{innerSource.EnumeratorTypeName}>";
    private string FactorySource=>!UseResultTypeParam ? string.Empty : $$"""
        {{AggressiveInliningAttribute}}
        private static global::Seamless.Linq.Carrier.ValueEnumerable<{{Struct}}<TOuterEnum,TInnerEnum,TResult>,TResult> {{FactoryMethodName}}<TOuterEnum,TInnerEnum,TResult>(TOuterEnum outer,TInnerEnum inner,global::System.Collections.Generic.IEqualityComparer<{{FuncReturnTypeName(innerKey)}}>? comparer{{ClosurePrimaryCtorSuffix(outerKey,innerKey,resultSelector)}})
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
    public override string GetEnumeratorPrimaryConstructorParameters=>$"TOuterEnum outer,TInnerEnum inner,global::System.Collections.Generic.IEqualityComparer<{FuncReturnTypeName(innerKey)}>? comparer{ClosurePrimaryCtorSuffix(outerKey,innerKey,resultSelector)}";
    public override string GetEnumerableExpression=>UseResultTypeParam ? $"{FactoryMethodName}({outerSource.GetEnumeratorExpression},{innerSource.GetEnumeratorExpression},{comparerExpression}{ClosureArgsSuffix(outerKey,innerKey,resultSelector)})" : base.GetEnumerableExpression;
    public override string GetEnumeratorFields=>$$"""
        {{ClosureFields(outerKey,innerKey,resultSelector)}}
        private readonly global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(innerKey)}},global::System.Collections.Generic.List<{{innerSource.ElementType}}>> {{lookup}}=BuildLookup(inner,comparer{{ClosureArgsSuffixForFactory(outerKey,innerKey,resultSelector)}});
        private TOuterEnum {{e}}=outer;
        private int {{state}};
        {{BuildLookupBody(false)}}
        """;
    public override string GetTryGetNextBody=>$$"""
        if({{state}}==1){
            current=default!;
            return false;
        }
        if({{e}}.MoveNext()){
            var {{outerValue}}={{e}}.Current;
            {{FuncReturnTypeName(outerKey)}} {{key}}=default!;
            {{FuncAssign(outerKey,nameof(outerKey),key,outerValue)}}
            if({{lookup}}.TryGetValue({{key}},out var {{group}})){
                {{FuncAssignCurrent(resultSelector,nameof(resultSelector),$"this.{currentValue}",outerValue,group)}}
            }else{
                {{FuncAssignCurrent(resultSelector,nameof(resultSelector),$"this.{currentValue}",outerValue,$"(global::System.Collections.Generic.IEnumerable<{innerSource.ElementType}>)global::System.Array.Empty<{innerSource.ElementType}>()")}}
            }
            current=this.{{currentValue}};
            return true;
        }
        {{state}}=1;
        current=default!;
        return false;
        """;
    public override string GetCurrentExpression=>currentValue;
    protected override string GetEnumeratorDisposeBody=>$"{e}.Dispose();";
}
