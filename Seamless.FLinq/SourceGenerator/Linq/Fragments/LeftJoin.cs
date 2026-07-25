namespace Seamless.FLinq.Linq.Fragments;

internal class LeftJoin(int 番号,Enumerable outer,Enumerable inner,FuncFragment outerKeySelector,FuncFragment innerKeySelector,FuncFragment resultSelector,string comparer)
    :JoinBase(番号,outer,inner,outerKeySelector,innerKeySelector,resultSelector,comparer) {
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
        private global::Seamless.Linq.Grouping<{{FuncReturnTypeName(this.innerKey)}},{{this.innerSource.ElementType}}>? {{this.group}};
        private int {{this.groupIndex}};
        private int {{this.state}};
        private {{this.outerSource.ElementType}} {{this.outerValue}};
        private static bool IsNullKey<TKey>(TKey key){
            return key is null;
        }
        {{this.BuildLookupBody(true,"IncrementLogicalLeftJoinInner")}}
        """;
    public override string GetTryGetNextBody => $$"""
        {{this.Start}}:
        if({{this.state}}==1&&{{this.group}} is not null){
            if({{this.groupIndex}}<{{this.group}}.Count){
                var {{this.item}}={{this.group}}[{{this.groupIndex}}++];
                {{this.FuncAssignCurrentIndented("        ",this.resultSelector,nameof(this.resultSelector),$"this.{this.currentValue}",this.outerValue,this.item)}}
                return true;
            }
            {{this.group}}=null;
            {{this.state}}=0;
        }
        if(!{{this.e}}.TryGetNext(out var __mn44)){
            current=default!;
            return false;
        }
        {{this.CounterCall("IncrementLogicalLeftJoinOuter")}}
        {{this.outerValue}}=__mn44;
        {{FuncReturnTypeName(this.outerKey)}} {{this.key}}=default!;
        {{this.FuncAssignIndented("",this.outerKey,nameof(this.outerKey),this.key,this.outerValue)}}
        {{this.CounterCall("IncrementHashProbe")}}
        if(!IsNullKey({{this.key}})&&({{this.group}}={{this.lookup}}.GetGroup({{this.key}})) is not null){
            {{this.groupIndex}}=0;
            {{this.state}}=1;
            goto {{this.Start}};
        }
        {{this.FuncAssignCurrentIndented("",this.resultSelector,nameof(this.resultSelector),$"this.{this.currentValue}",this.outerValue,$"default({this.innerSource.ElementType})")}}
        return true;
        """;
    protected override string GetEnumeratorDisposeBody => $"{this.lookup}.Dispose(); {this.e}.Dispose();";
}
