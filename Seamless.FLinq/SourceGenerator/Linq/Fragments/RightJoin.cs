namespace Seamless.FLinq.Linq.Fragments;

internal class RightJoin(int 番号,Enumerable outer,Enumerable inner,FuncFragment outerKeySelector,FuncFragment innerKeySelector,FuncFragment resultSelector,string comparer)
    : JoinBase(番号,outer,inner,outerKeySelector,innerKeySelector,resultSelector,comparer) {
    public override string ElementType=> this.UseResultTypeParam ? "TResult" : FuncReturnTypeName(this.resultSelector);
    protected override string StructTypeParameters=> this.UseResultTypeParam ? "<TOuterEnum,TInnerEnum,TResult>" : "<TOuterEnum,TInnerEnum>";
    protected override string StructTypeConstraints=>
        $"where TOuterEnum:struct,global::System.Collections.Generic.IEnumerator<{this.outerSource.ElementType}>\n    where TInnerEnum:struct,global::System.Collections.Generic.IEnumerator<{this.innerSource.ElementType}>";
    public override string EnumeratorTypeName=> this.UseResultTypeParam ? $"{this.Struct}<{this.outerSource.EnumeratorTypeName},{this.innerSource.EnumeratorTypeName},TResult>" : $"{this.Struct}<{this.outerSource.EnumeratorTypeName},{this.innerSource.EnumeratorTypeName}>";
    private string FactorySource=>!this.UseResultTypeParam ? string.Empty : $$"""
        {{AggressiveInliningAttribute}}
        private static global::Seamless.Linq.ValueEnumerable<{{this.Struct}}<TOuterEnum,TInnerEnum,TResult>,TResult> {{this.FactoryMethodName}}<TOuterEnum,TInnerEnum,TResult>(TOuterEnum outer,TInnerEnum inner,global::System.Collections.Generic.IEqualityComparer<{{FuncReturnTypeName(this.outerKey)}}>? comparer{{this.ClosurePrimaryCtorSuffix(this.outerKey,this.innerKey,this.resultSelector)}})
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
    public override string GetEnumeratorPrimaryConstructorParameters=>$"TOuterEnum {this.e},TInnerEnum {this.innerEnumerator},global::System.Collections.Generic.IEqualityComparer<{FuncReturnTypeName(this.outerKey)}>? comparer{this.ClosurePrimaryCtorSuffix(this.outerKey,this.innerKey,this.resultSelector)}";
    public override string GetEnumerableExpression=> this.UseResultTypeParam ? $"{this.FactoryMethodName}({this.outerSource.GetEnumeratorExpression},{this.innerSource.GetEnumeratorExpression},{this.comparerExpression}{this.ClosureArgsSuffix(this.outerKey,this.innerKey,this.resultSelector)})" : base.GetEnumerableExpression;
    public override string GetEnumeratorConstructor=>$"this.{this.lookup}=BuildOuterLookup(this.{this.e},this.comparer{this.ClosureArgsSuffixForCtorBody(this.outerKey,this.innerKey,this.resultSelector)});";
    public override string GetEnumeratorFields=>$$"""
        private global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(this.outerKey)}},global::System.Collections.Generic.List<{{this.outerSource.ElementType}}>> {{this.lookup}};
        private global::System.Collections.Generic.List<{{this.outerSource.ElementType}}>? {{this.group}};
        private int {{this.groupIndex}};
        private int {{this.state}};
        private {{this.innerSource.ElementType}} {{this.outerValue}};
        private static bool IsNullKey<T>(T key){
            return key is null;
        }
        private static global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(this.outerKey)}},global::System.Collections.Generic.List<{{this.outerSource.ElementType}}>> BuildOuterLookup(TOuterEnum outer,global::System.Collections.Generic.IEqualityComparer<{{FuncReturnTypeName(this.outerKey)}}>? comparer{{this.ClosureMethodParameterSuffix(this.outerKey,this.innerKey,this.resultSelector)}}){
            var {{this.lookup}}=new global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(this.outerKey)}},global::System.Collections.Generic.List<{{this.outerSource.ElementType}}>>(comparer ?? global::System.Collections.Generic.EqualityComparer<{{FuncReturnTypeName(this.outerKey)}}>.Default);
            using var {{this.e}}=outer;
            while({{this.e}}.MoveNext()){
                {{this.CounterCall("IncrementLogicalRightJoinOuter")}}
                var {{this.item}}={{this.e}}.Current;
                {{FuncReturnTypeName(this.outerKey)}} {{this.key}}=default!;
                {{this.FuncAssignStaticIndented("        ",this.outerKey,nameof(this.outerKey),this.key,this.item)}}
                if(IsNullKey({{this.key}})){
                    continue;
                }
                {{this.CounterCall("IncrementHashBuild")}}
                if(!{{this.lookup}}.TryGetValue({{this.key}},out var {{this.group}})){
                    {{this.group}}=new global::System.Collections.Generic.List<{{this.outerSource.ElementType}}>();
                    {{this.lookup}}.Add({{this.key}},{{this.group}});
                }
                {{this.CounterCall("IncrementBufferAdd")}}
                {{this.group}}.Add({{this.item}});
            }
            return {{this.lookup}};
        }
        """;
    public override string GetTryGetNextBody=>$$"""
        {{this.Start}}:
        if({{this.state}}==1&&{{this.group}} is not null){
            if({{this.groupIndex}}<{{this.group}}.Count){
                var {{this.item}}={{this.group}}[{{this.groupIndex}}++];
                {{this.FuncAssignCurrentIndented("        ",this.resultSelector,nameof(this.resultSelector),$"this.{this.currentValue}",this.item,this.outerValue)}}
                return true;
            }
            {{this.group}}=null;
            {{this.state}}=0;
        }
        if(!{{this.innerEnumerator}}.MoveNext()){
            current=default!;
            return false;
        }
        {{this.CounterCall("IncrementLogicalRightJoinInner")}}
        {{this.outerValue}}={{this.innerEnumerator}}.Current;
        {{FuncReturnTypeName(this.innerKey)}} {{this.key}}=default!;
        {{this.FuncAssignIndented("",this.innerKey,nameof(this.innerKey),this.key,this.outerValue)}}
        {{this.CounterCall("IncrementHashProbe")}}
        if(!IsNullKey({{this.key}})&&{{this.lookup}}.TryGetValue({{this.key}},out {{this.group}})){
            {{this.groupIndex}}=0;
            {{this.state}}=1;
            goto {{this.Start}};
        }
        {{this.FuncAssignCurrentIndented("",this.resultSelector,nameof(this.resultSelector),$"this.{this.currentValue}",$"default({this.outerSource.ElementType})",this.outerValue)}}
        return true;
        """;
    public override string GetCurrentExpression=> this.currentValue;
    protected override string GetEnumeratorDisposeBody=>$"{this.innerEnumerator}.Dispose();";
}
