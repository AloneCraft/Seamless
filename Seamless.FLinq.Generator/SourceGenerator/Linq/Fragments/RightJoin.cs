namespace Seamless.FLinq.Generator;

internal class RightJoin(int 番号,Enumerable outer,Enumerable inner,FuncFragment outerKeySelector,FuncFragment innerKeySelector,FuncFragment resultSelector,string comparer)
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
        private static global::Seamless.Linq.ValueEnumerable<{{this.Struct}}<TOuterEnum,TInnerEnum,TResult>,TResult> {{this.FactoryMethodName}}<TOuterEnum,TInnerEnum,TResult>(TOuterEnum outer,TInnerEnum inner,global::System.Collections.Generic.IEqualityComparer<{{FuncReturnTypeName(this.outerKey)}}>? comparer{{this.ClosurePrimaryCtorSuffix(this.outerKey,this.innerKey,this.resultSelector)}})
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
    public override string GetEnumeratorExpression => RenderEnumeratorExpressionWithClosure(this.EnumeratorTypeName,new[] { this.outerKey,this.innerKey,this.resultSelector },this.outerSource.GetEnumeratorExpression,this.innerSource.GetEnumeratorExpression,this.comparerExpression);
    public override string GetEnumeratorPrimaryConstructorParameters => $"TOuterEnum {this.e},TInnerEnum {this.innerEnumerator},global::System.Collections.Generic.IEqualityComparer<{FuncReturnTypeName(this.outerKey)}>? comparer{this.ClosurePrimaryCtorSuffix(this.outerKey,this.innerKey,this.resultSelector)}";
    public override string GetEnumerableExpression => this.UseResultTypeParam ? $"{this.FactoryMethodName}({this.outerSource.GetEnumeratorExpression},{this.innerSource.GetEnumeratorExpression},{this.comparerExpression}{this.ClosureArgsSuffix(this.outerKey,this.innerKey,this.resultSelector)})" : base.GetEnumerableExpression;
    // 外側の探索表は、内側の最初の要素を得てから作る(System.Linq と同じ。内側が空なら、外側のキー選択子を評価しない)。
    public override string GetEnumeratorConstructor => string.Empty;
    private string built => $"__built{番号}";
    public override string GetEnumeratorFields => $$"""
        private global::Seamless.Linq.Lookup<{{FuncReturnTypeName(this.outerKey)}},{{this.outerSource.ElementType}}>? {{this.lookup}};
        private bool {{this.built}};
        private global::Seamless.Linq.Grouping<{{FuncReturnTypeName(this.outerKey)}},{{this.outerSource.ElementType}}>? {{this.group}};
        private int {{this.groupIndex}};
        private int {{this.state}};
        private {{this.innerSource.ElementType}} {{this.outerValue}};
        private static bool IsNullKey<TKey>(TKey key){
            return key is null;
        }
        private static global::Seamless.Linq.Lookup<{{FuncReturnTypeName(this.outerKey)}},{{this.outerSource.ElementType}}> BuildOuterLookup(TOuterEnum outer,global::System.Collections.Generic.IEqualityComparer<{{FuncReturnTypeName(this.outerKey)}}>? comparer{{this.ClosureMethodParameterSuffix(this.outerKey,this.innerKey,this.resultSelector)}}){
            var __hasCnt=outer.TryGetNonEnumeratedCount(out var __cnt);
            if(__hasCnt) global::Seamless.Linq.LinqCounterScope.IncrementHashCapacityHintUsed();
            var __lookupBuilder=__hasCnt
                ? new global::Seamless.Linq.LookupBuilder<{{FuncReturnTypeName(this.outerKey)}},{{this.outerSource.ElementType}}>(__cnt,comparer)
                : new global::Seamless.Linq.LookupBuilder<{{FuncReturnTypeName(this.outerKey)}},{{this.outerSource.ElementType}}>(comparer);
            using var {{this.e}}=outer;
            while({{this.e}}.TryGetNext(out var __mn48)){
                {{this.CounterCall("IncrementLogicalRightJoinOuter")}}
                var {{this.item}}=__mn48;
                {{FuncReturnTypeName(this.outerKey)}} {{this.key}}=default!;
                {{this.FuncAssignStaticIndented("        ",this.outerKey,nameof(this.outerKey),this.key,this.item)}}
                if(IsNullKey({{this.key}})){
                    continue;
                }
                {{this.CounterCall("IncrementHashBuild")}}
                {{this.CounterCall("IncrementBufferAdd")}}
                __lookupBuilder.Add({{this.key}},{{this.item}});
            }
            return __lookupBuilder.BuildPooledAndClear();
        }
        """;
    public override string GetTryGetNextBody => $$"""
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
        if(!{{this.innerEnumerator}}.TryGetNext(out var __mn49)){
            current=default!;
            return false;
        }
        {{this.CounterCall("IncrementLogicalRightJoinInner")}}
        {{this.outerValue}}=__mn49;
        if(!this.{{this.built}}){
            this.{{this.built}}=true;
            this.{{this.lookup}}=BuildOuterLookup(this.{{this.e}},this.comparer{{this.ClosureArgsSuffixForCtorBody(this.outerKey,this.innerKey,this.resultSelector)}});
        }
        {{FuncReturnTypeName(this.innerKey)}} {{this.key}}=default!;
        {{this.FuncAssignIndented("",this.innerKey,nameof(this.innerKey),this.key,this.outerValue)}}
        {{this.CounterCall("IncrementHashProbe")}}
        if(!IsNullKey({{this.key}})&&({{this.group}}={{this.lookup}}!.GetGroup({{this.key}})) is not null){
            {{this.groupIndex}}=0;
            {{this.state}}=1;
            goto {{this.Start}};
        }
        {{this.FuncAssignCurrentIndented("",this.resultSelector,nameof(this.resultSelector),$"this.{this.currentValue}",$"default({this.outerSource.ElementType})",this.outerValue)}}
        return true;
        """;
    // 探索表を作る前に捨てるときは、外側の列挙子を探索表の構築が閉じていないので、ここで閉じる。
    protected override string GetEnumeratorDisposeBody => $"{this.lookup}?.Dispose(); {this.innerEnumerator}.Dispose(); if(!this.{this.built}) {{ this.{this.e}.Dispose(); }}";
}
