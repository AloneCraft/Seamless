namespace Seamless.FLinq.Generator;

internal class Join(int 番号,Enumerable outer,Enumerable inner,FuncFragment outerKeySelector,FuncFragment innerKeySelector,FuncFragment resultSelector,string comparer):JoinBase(番号,outer,inner,outerKeySelector,innerKeySelector,resultSelector,comparer) {
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
    public override string GetEnumeratorExpression => RenderEnumeratorExpressionWithClosure(this.EnumeratorTypeName,new[] { this.outerKey,this.innerKey,this.resultSelector },this.outerSource.GetEnumeratorExpression,this.innerSource.GetEnumeratorExpression,this.comparerExpression);
    public override string GetEnumeratorPrimaryConstructorParameters => $"TOuterEnum {this.e},TInnerEnum {this.innerEnumerator},global::System.Collections.Generic.IEqualityComparer<{FuncReturnTypeName(this.innerKey)}>? comparer{this.ClosurePrimaryCtorSuffix(this.outerKey,this.innerKey,this.resultSelector)}";
    public override string GetEnumerableExpression => this.UseResultTypeParam ? $"{this.FactoryMethodName}({this.outerSource.GetEnumeratorExpression},{this.innerSource.GetEnumeratorExpression},{this.comparerExpression}{this.ClosureArgsSuffix(this.outerKey,this.innerKey,this.resultSelector)})" : base.GetEnumerableExpression;
    public override string GetEnumeratorConstructor => this.ProbesInnerRange()
        ? $$"""
            // 内側は Range で恒等キーなので、探索表を作らず区間 [始点,始点+個数) で一致を判定する(各キーに一致する内側は自身1つ)。
            this.{{this.innerEnumerator}}.TryGetNonEnumeratedCount(out var __innerCount);
            this.{{this.innerLow}}=__innerCount>0&&this.{{this.innerEnumerator}}.TryGetNext(out var __innerFirst) ? __innerFirst : 0L;
            this.{{this.innerHigh}}=this.{{this.innerLow}}+__innerCount;
            """
        : $"this.{this.lookup}=BuildLookup(this.{this.innerEnumerator},this.comparer{this.ClosureArgsSuffixForCtorBody(this.outerKey,this.innerKey,this.resultSelector)});";
    private string innerLow => $"__innerLow{番号}";
    private string innerHigh => $"__innerHigh{番号}";
    // 内側が .Anchor の無い Range そのもので、両キーが int(内側は恒等)・比較子が既定なら、内側の要素はキーと等しく一意。
    // 外側のキーが区間に入るかだけで一致が決まるので、内側を列挙せず探索表も作らない(FLinq の契約: Range の列挙は観測されない)。
    private bool ProbesInnerRange() =>
        this.comparerExpression=="null"
        &&this.innerSource is RangeRoot
        &&!this.innerSource.TreeContainsAnchor()
        &&this.innerKey.IsIdentity
        &&!this.innerKey.ContainsAnchor
        &&FuncReturnTypeName(this.outerKey)=="int"
        &&FuncReturnTypeName(this.innerKey)=="int";
    public override string GetEnumeratorFields => this.ProbesInnerRange()
        ? $$"""
            private long {{this.innerLow}};
            private long {{this.innerHigh}};
            """
        : $$"""
        private global::Seamless.Linq.Lookup<{{FuncReturnTypeName(this.innerKey)}},{{this.innerSource.ElementType}}> {{this.lookup}};
        private global::Seamless.Linq.Grouping<{{FuncReturnTypeName(this.innerKey)}},{{this.innerSource.ElementType}}>? {{this.group}};
        private int {{this.groupIndex}};
        private int {{this.state}};
        private {{this.outerSource.ElementType}} {{this.outerValue}};
        private static bool IsNullKey<TKey>(TKey key){
            return key is null;
        }
        {{this.BuildLookupBody(true,"IncrementLogicalJoinInner")}}
        """;
    public override string GetTryGetNextBody => this.ProbesInnerRange()
        ? $$"""
        while({{this.e}}.TryGetNext(out var __mn38)){
            {{this.CounterCall("IncrementLogicalJoinOuter")}}
            var {{this.outerValue}}=__mn38;
            int {{this.key}}=default!;
            {{this.FuncAssignIndented("    ",this.outerKey,nameof(this.outerKey),this.key,this.outerValue)}}
            if({{this.key}}>=this.{{this.innerLow}}&&{{this.key}}<this.{{this.innerHigh}}){
                {{this.FuncAssignCurrentIndented("        ",this.resultSelector,nameof(this.resultSelector),$"this.{this.currentValue}",this.outerValue,this.key)}}
                return true;
            }
        }
        current=default!;
        return false;
        """
        : $$"""
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
        if(!{{this.e}}.TryGetNext(out var __mn38)){
            current=default!;
            return false;
        }
        {{this.CounterCall("IncrementLogicalJoinOuter")}}
        {{this.outerValue}}=__mn38;
        {{FuncReturnTypeName(this.outerKey)}} {{this.key}}=default!;
        {{this.FuncAssignIndented("",this.outerKey,nameof(this.outerKey),this.key,this.outerValue)}}
        if(IsNullKey({{this.key}})){
            goto {{this.Start}};
        }
        {{this.CounterCall("IncrementHashProbe")}}
        {{this.group}}={{this.lookup}}.GetGroup({{this.key}});
        if({{this.group}} is null){
            goto {{this.Start}};
        }
        {{this.groupIndex}}=0;
        {{this.state}}=1;
        goto {{this.Start}};
        """;
    public override string GetConsumeExecutionStatement(string consumerName) {
        if(this.UseResultTypeParam
            ||this.outerSource.DirectArrayExpression is not { } outerArrayExpression
            ||this.innerSource.DirectArrayExpression is not { } innerArrayExpression) {
            return base.GetConsumeExecutionStatement(consumerName);
        }

        var outerArrayName = $"__joinOuterArray{番号}";
        var innerArrayName = $"__joinInnerArray{番号}";
        var lookupBuilderName = $"__joinLookupBuilder{番号}";
        var outerIndexName = $"__joinOuterIndex{番号}";
        var innerIndexName = $"__joinInnerIndex{番号}";
        var groupIndexName = $"__joinGroupIndex{番号}";
        var outerItemName = $"__joinOuterItem{番号}";
        var innerItemName = $"__joinInnerItem{番号}";
        var innerKeyName = $"__joinInnerKey{番号}";
        var outerKeyName = $"__joinOuterKey{番号}";
        var groupName = $"__joinGroup{番号}";
        var resultName = $"__joinResult{番号}";
        var hasKeyName = $"__joinHasKey{番号}";
        var localDeclarations = string.Join("\n",new[]{
            this.HoistedLocalDeclarations(this.outerKey,this.innerKey,this.resultSelector),
            this.StaticDelegateLocalDeclarations(this.outerKey,this.innerKey,this.resultSelector),
        }.Where(static text => !string.IsNullOrEmpty(text)));
        var localDeclarationBlock = string.IsNullOrEmpty(localDeclarations) ? string.Empty : localDeclarations+"\n";

        return $$"""
            {{localDeclarationBlock}}
            static bool {{hasKeyName}}<TKey>(TKey key) => key is not null;
            var {{innerArrayName}}={{innerArrayExpression}};
            var {{lookupBuilderName}}=new global::Seamless.Linq.LookupBuilder<{{this.InnerKeyType}},{{this.InnerElementType}}>({{innerArrayName}}.Length,{{this.comparerExpression}},true);
            try {
            for(var {{innerIndexName}}=0;{{innerIndexName}}<{{innerArrayName}}.Length;{{innerIndexName}}++){
                var {{innerItemName}}={{innerArrayName}}[{{innerIndexName}}];
                {{this.InnerKeyType}} {{innerKeyName}}=default!;
                {{this.FuncAssignStaticIndented("    ",this.innerKey,nameof(this.innerKey),innerKeyName,innerItemName)}}
                if({{hasKeyName}}({{innerKeyName}})){
                    {{lookupBuilderName}}.Add({{innerKeyName}},{{innerItemName}});
                }
            }
            var {{outerArrayName}}={{outerArrayExpression}};
            for(var {{outerIndexName}}=0;{{outerIndexName}}<{{outerArrayName}}.Length;{{outerIndexName}}++){
                var {{outerItemName}}={{outerArrayName}}[{{outerIndexName}}];
                {{this.InnerKeyType}} {{outerKeyName}}=default!;
                {{this.FuncAssignStaticIndented("    ",this.outerKey,nameof(this.outerKey),outerKeyName,outerItemName)}}
                if(!{{hasKeyName}}({{outerKeyName}})) continue;
                var {{groupName}}={{lookupBuilderName}}.GetGroup({{outerKeyName}});
                if({{groupName}} is null) continue;
                for(var {{groupIndexName}}=0;{{groupIndexName}}<{{groupName}}.Count;{{groupIndexName}}++){
                    var {{innerItemName}}={{groupName}}[{{groupIndexName}}];
                    {{this.ElementType}} {{resultName}}=default!;
                    {{this.FuncAssignStaticIndented("        ",this.resultSelector,nameof(this.resultSelector),resultName,outerItemName,innerItemName)}}
                    {{consumerName}}.Consume({{resultName}});
                }
            }
            } finally {
                {{lookupBuilderName}}.Dispose();
            }
            """;
    }
    protected override string GetEnumeratorDisposeBody => this.ProbesInnerRange()
        ? $"{this.e}.Dispose(); {this.innerEnumerator}.Dispose();"
        : $"{this.lookup}.Dispose(); {this.e}.Dispose();";
}
