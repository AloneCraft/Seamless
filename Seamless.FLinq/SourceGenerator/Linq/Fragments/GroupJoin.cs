namespace Seamless.FLinq;

internal class GroupJoin(int 番号,Enumerable outer,Enumerable inner,FuncFragment outerKeySelector,FuncFragment innerKeySelector,FuncFragment resultSelector,string comparer):JoinBase(番号,outer,inner,outerKeySelector,innerKeySelector,resultSelector,comparer) {
    public override string ElementType => this.UseResultTypeParam ? "TResult" : FuncReturnTypeName(this.resultSelector);
    protected override string StructTypeParameters => this.UseResultTypeParam
        ? this.StructTypeParametersWithOuter("TOuterEnum","TInnerEnum","TResult")
        : this.StructTypeParametersWithOuter("TOuterEnum","TInnerEnum");
    protected override string StructTypeConstraints =>
        $"where TOuterEnum:struct,global::Seamless.Linq.IValueEnumerator<{this.outerSource.ElementType}>\n    where TInnerEnum:struct,global::Seamless.Linq.IValueEnumerator<{this.innerSource.ElementType}>";
    public override string EnumeratorTypeName => this.UseResultTypeParam
        ? this.EnumeratorTypeNameWithOuter(this.outerSource.EnumeratorTypeName,this.innerSource.EnumeratorTypeName,"TResult")
        : this.EnumeratorTypeNameWithOuter(this.outerSource.EnumeratorTypeName,this.innerSource.EnumeratorTypeName);
    private string FactorySource => !this.UseResultTypeParam ? string.Empty : $$"""
        {{AggressiveInliningAttribute}}
        private static global::Seamless.Linq.ValueEnumerable<{{this.Struct}}<TOuterEnum,TInnerEnum,TResult>,TResult> {{this.FactoryMethodName}}<TOuterEnum,TInnerEnum,TResult>(TOuterEnum outer,TInnerEnum inner,global::System.Collections.Generic.IEqualityComparer<{{FuncReturnTypeName(this.innerKey)}}>? comparer{{this.ClosurePrimaryCtorSuffix(this.outerKey,this.innerKey,this.resultSelector)}})
            where TOuterEnum:struct,global::Seamless.Linq.IValueEnumerator<{{this.outerSource.ElementType}}>
            where TInnerEnum:struct,global::Seamless.Linq.IValueEnumerator<{{this.innerSource.ElementType}}>
            => new(new {{this.Struct}}<TOuterEnum,TInnerEnum,TResult>(outer,inner,comparer{{this.ClosureArgsSuffixForFactory(this.outerKey,this.innerKey,this.resultSelector)}}));
        """;
    public override string StructMethod => JoinStaticMethods(JoinStaticMethods(JoinStaticMethods(
        this.FuncSupport(this.outerKey,nameof(this.outerKey)),
        this.FuncSupport(this.innerKey,nameof(this.innerKey))),
        this.FuncSupport(this.resultSelector,nameof(this.resultSelector))),
        this.FactorySource);
    public override string GetEnumeratorStruct => $$"""
        {{this.outerSource.GetEnumeratorStruct}}
        {{this.innerSource.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({this.outerSource.GetEnumeratorExpression},{this.innerSource.GetEnumeratorExpression},{this.comparerExpression}{this.ClosureArgsSuffix(this.outerKey,this.innerKey,this.resultSelector)})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TOuterEnum {this.e},TInnerEnum {this.innerEnumerator},global::System.Collections.Generic.IEqualityComparer<{FuncReturnTypeName(this.innerKey)}>? comparer{this.ClosurePrimaryCtorSuffix(this.outerKey,this.innerKey,this.resultSelector)}";
    public override string GetEnumerableExpression => this.UseResultTypeParam ? $"{this.FactoryMethodName}({this.outerSource.GetEnumeratorExpression},{this.innerSource.GetEnumeratorExpression},{this.comparerExpression}{this.ClosureArgsSuffix(this.outerKey,this.innerKey,this.resultSelector)})" : base.GetEnumerableExpression;
    public override string GetEnumeratorConstructor => $"this.{this.lookup}=BuildLookup(this.{this.innerEnumerator},this.comparer{this.ClosureArgsSuffixForCtorBody(this.outerKey,this.innerKey,this.resultSelector)});";
    public override string GetEnumeratorFields => $$"""
        private global::Seamless.Linq.Lookup<{{FuncReturnTypeName(this.innerKey)}},{{this.innerSource.ElementType}}> {{this.lookup}};
        private int {{this.state}};
        {{this.BuildLookupBody(false,"IncrementLogicalGroupJoinInner")}}
        """;
    public override string GetConsumeExecutionStatement(string consumerName) =>
        this.CanBypassLookupForConsume()
            ? this.outerSource.GetConsumeExecutionStatement(consumerName)
            : base.GetConsumeExecutionStatement(consumerName);
    public override string GetTryGetNextBody => $$"""
        if({{this.state}}==1){
            current=default!;
            return false;
        }
        if({{this.e}}.TryGetNext(out var __mn36)){
            {{this.CounterCall("IncrementLogicalGroupJoinOuter")}}
            var {{this.outerValue}}=__mn36;
            {{FuncReturnTypeName(this.outerKey)}} {{this.key}}=default!;
            {{this.FuncAssignIndented("    ",this.outerKey,nameof(this.outerKey),this.key,this.outerValue)}}
            {{this.CounterCall("IncrementHashProbe")}}
            if({{this.lookup}}.GetGroup({{this.key}}) is { } {{this.group}}){
                {{this.FuncAssignCurrentIndented("        ",this.resultSelector,nameof(this.resultSelector),$"this.{this.currentValue}",this.outerValue,this.group)}}
            }else{
                {{this.FuncAssignCurrentIndented("        ",this.resultSelector,nameof(this.resultSelector),$"this.{this.currentValue}",this.outerValue,$"(global::System.Collections.Generic.IEnumerable<{this.innerSource.ElementType}>)global::System.Array.Empty<{this.innerSource.ElementType}>()")}}
            }
            current=this.{{this.currentValue}};
            return true;
        }
        {{this.state}}=1;
        current=default!;
        return false;
        """;
    protected override string GetEnumeratorDisposeBody => $"{this.lookup}.Dispose(); {this.e}.Dispose();";

    private bool CanBypassLookupForConsume() =>
        this.comparerExpression=="null"
        &&this.outerSource is RangeRoot
        &&this.innerSource is RangeRoot
        &&this.outerKey.IsIdentity
        &&this.innerKey.IsIdentity
        &&this.ResultSelectorReturnsOuter();

    private bool ResultSelectorReturnsOuter() {
        if(this.resultSelector.RequiresStatementInlining
            ||!this.resultSelector.CanInlineAssign
            ||this.resultSelector.ParameterNames.Length<2
            ||this.resultSelector.ReturnTypeName!=this.outerSource.ElementType) {
            return false;
        }

        var expression = this.resultSelector.InlineBody.Trim();
        while(expression.Length>=2&&expression[0]=='('&&expression[expression.Length-1]==')') {
            expression=expression.Substring(1,expression.Length-2).Trim();
        }

        return string.Equals(expression,this.resultSelector.ParameterNames[0],StringComparison.Ordinal);
    }
}
