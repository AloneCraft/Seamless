namespace Seamless.FLinq.Linq.Fragments;

internal class GroupJoin(int 番号,Enumerable outer,Enumerable inner,FuncFragment outerKeySelector,FuncFragment innerKeySelector,FuncFragment resultSelector,string comparer):JoinBase(番号,outer,inner,outerKeySelector,innerKeySelector,resultSelector,comparer){
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
        private int {{state}};
        {{BuildLookupBody(false,"IncrementLogicalGroupJoinInner")}}
        """;
    public override string GetConsumeExecutionStatement(string consumerName) =>
        CanBypassLookupForConsume()
            ? outerSource.GetConsumeExecutionStatement(consumerName)
            : base.GetConsumeExecutionStatement(consumerName);
    public override string GetTryGetNextBody=>$$"""
        if({{state}}==1){
            current=default!;
            return false;
        }
        if({{e}}.MoveNext()){
            {{CounterCall("IncrementLogicalGroupJoinOuter")}}
            var {{outerValue}}={{e}}.Current;
            {{FuncReturnTypeName(outerKey)}} {{key}}=default!;
            {{FuncAssignIndented("    ",outerKey,nameof(outerKey),key,outerValue)}}
            {{CounterCall("IncrementHashProbe")}}
            if({{lookup}}.TryGetValue({{key}},out var {{group}})){
                {{FuncAssignCurrentIndented("        ",resultSelector,nameof(resultSelector),$"this.{currentValue}",outerValue,group)}}
            }else{
                {{FuncAssignCurrentIndented("        ",resultSelector,nameof(resultSelector),$"this.{currentValue}",outerValue,$"(global::System.Collections.Generic.IEnumerable<{innerSource.ElementType}>)global::System.Array.Empty<{innerSource.ElementType}>()")}}
            }
            current=this.{{currentValue}};
            return true;
        }
        {{state}}=1;
        current=default!;
        return false;
        """;
    public override string GetCurrentExpression=> currentValue;
    protected override string GetEnumeratorDisposeBody=>$"{e}.Dispose();";

    private bool CanBypassLookupForConsume() =>
        comparerExpression=="null"
        && outerSource is RangeRoot
        && innerSource is RangeRoot
        && outerKey.IsIdentity
        && innerKey.IsIdentity
        && ResultSelectorReturnsOuter();

    private bool ResultSelectorReturnsOuter() {
        if(resultSelector.RequiresStatementInlining
            || !resultSelector.CanInlineAssign
            || resultSelector.ParameterNames.Length<2
            || resultSelector.ReturnTypeName!=outerSource.ElementType) {
            return false;
        }

        var expression=resultSelector.InlineBody.Trim();
        while(expression.Length>=2 && expression[0]=='(' && expression[^1]==')') {
            expression=expression[1..^1].Trim();
        }

        return string.Equals(expression,resultSelector.ParameterNames[0],StringComparison.Ordinal);
    }
}
