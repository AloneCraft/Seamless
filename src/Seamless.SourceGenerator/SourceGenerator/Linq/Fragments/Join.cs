namespace Seamless.SourceGenerator.Linq.Fragments;

internal class Join(int 番号,Enumerable outer,Enumerable inner,FuncFragment outerKeySelector,FuncFragment innerKeySelector,FuncFragment resultSelector,string comparer):JoinBase(番号,outer,inner,outerKeySelector,innerKeySelector,resultSelector,comparer){
    public override string ElementType=> this.UseResultTypeParam ? "TResult" : FuncReturnTypeName(this.resultSelector);
    protected override string StructTypeParameters=> this.UseResultTypeParam ? "<TOuterEnum,TInnerEnum,TResult>" : "<TOuterEnum,TInnerEnum>";
    protected override string StructTypeConstraints=>
        $"where TOuterEnum:struct,global::System.Collections.Generic.IEnumerator<{this.outerSource.ElementType}>\n    where TInnerEnum:struct,global::System.Collections.Generic.IEnumerator<{this.innerSource.ElementType}>";
    public override string EnumeratorTypeName=> this.UseResultTypeParam ? $"{this.Struct}<{this.outerSource.EnumeratorTypeName},{this.innerSource.EnumeratorTypeName},TResult>" : $"{this.Struct}<{this.outerSource.EnumeratorTypeName},{this.innerSource.EnumeratorTypeName}>";
    private string FactorySource=>!this.UseResultTypeParam ? string.Empty : $$"""
        {{AggressiveInliningAttribute}}
        private static global::Seamless.Linq.Carrier.ValueEnumerable<{{this.Struct}}<TOuterEnum,TInnerEnum,TResult>,TResult> {{this.FactoryMethodName}}<TOuterEnum,TInnerEnum,TResult>(TOuterEnum outer,TInnerEnum inner,global::System.Collections.Generic.IEqualityComparer<{{FuncReturnTypeName(this.innerKey)}}>? comparer{{this.ClosurePrimaryCtorSuffix(this.outerKey,this.innerKey,this.resultSelector)}})
            where TOuterEnum:struct,global::System.Collections.Generic.IEnumerator<{{this.outerSource.ElementType}}>
            where TInnerEnum:struct,global::System.Collections.Generic.IEnumerator<{{this.innerSource.ElementType}}>
            => new(new {{this.Struct}}<TOuterEnum,TInnerEnum,TResult>(outer,inner,comparer{{this.ClosureArgsSuffixForFactory(this.outerKey,this.innerKey,this.resultSelector)}}));
        """;
    public override string StructMethod=>JoinStaticMethods(JoinStaticMethods(JoinStaticMethods(
        this.FuncSupport(this.outerKey,nameof(this.outerKey)),
        this.FuncSupport(this.innerKey,nameof(this.innerKey))),
        this.FuncSupport(this.resultSelector,nameof(this.resultSelector))),
        this.FactorySource);
    public override string GetEnumeratorStruct=>$$"""
        {{this.outerSource.GetEnumeratorStruct}}
        {{this.innerSource.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression=>$"new {this.EnumeratorTypeName}({this.outerSource.GetEnumeratorExpression},{this.innerSource.GetEnumeratorExpression},{this.comparerExpression}{this.ClosureArgsSuffix(this.outerKey,this.innerKey,this.resultSelector)})";
    public override string GetEnumeratorPrimaryConstructorParameters=>$"TOuterEnum {this.e},TInnerEnum {this.innerEnumerator},global::System.Collections.Generic.IEqualityComparer<{FuncReturnTypeName(this.innerKey)}>? comparer{this.ClosurePrimaryCtorSuffix(this.outerKey,this.innerKey,this.resultSelector)}";
    public override string GetEnumerableExpression=> this.UseResultTypeParam ? $"{this.FactoryMethodName}({this.outerSource.GetEnumeratorExpression},{this.innerSource.GetEnumeratorExpression},{this.comparerExpression}{this.ClosureArgsSuffix(this.outerKey,this.innerKey,this.resultSelector)})" : base.GetEnumerableExpression;
    public override string GetEnumeratorConstructor=>$"this.{this.lookup}=BuildLookup(this.{this.innerEnumerator},this.comparer{this.ClosureArgsSuffixForCtorBody(this.outerKey,this.innerKey,this.resultSelector)});";
    public override string GetEnumeratorFields=>$$"""
        private global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(this.innerKey)}},global::System.Collections.Generic.List<{{this.innerSource.ElementType}}>> {{this.lookup}};
        private global::System.Collections.Generic.List<{{this.innerSource.ElementType}}>? {{this.group}};
        private int {{this.groupIndex}};
        private int {{this.state}};
        private {{this.outerSource.ElementType}} {{this.outerValue}};
        private static bool IsNullKey<T>(T key){
            return key is null;
        }
        {{this.BuildLookupBody(true)}}
        """;
    public override string GetTryGetNextBody=>$$"""
        {{this.Start}}:
        if({{this.state}}==1&&{{this.group}} is not null){
            if({{this.groupIndex}}<{{this.group}}.Count){
                var {{this.item}}={{this.group}}[{{this.groupIndex}}++];
                {{this.FuncAssignCurrent(this.resultSelector,nameof(this.resultSelector),$"this.{this.currentValue}",this.outerValue,this.item)}}
                return true;
            }
            {{this.group}}=null;
            {{this.state}}=0;
        }
        if(!{{this.e}}.MoveNext()){
            current=default!;
            return false;
        }
        {{this.outerValue}}={{this.e}}.Current;
        {{FuncReturnTypeName(this.outerKey)}} {{this.key}}=default!;
        {{this.FuncAssign(this.outerKey,nameof(this.outerKey),this.key,this.outerValue)}}
        if(IsNullKey({{this.key}})){
            goto {{this.Start}};
        }
        if(!{{this.lookup}}.TryGetValue({{this.key}},out {{this.group}})){
            goto {{this.Start}};
        }
        {{this.groupIndex}}=0;
        {{this.state}}=1;
        goto {{this.Start}};
        """;
    public override string GetCurrentExpression=> this.currentValue;
    protected override string GetEnumeratorDisposeBody=>$"{this.e}.Dispose();";
}
