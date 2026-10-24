namespace Seamless.FLinq.Generator;

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
    public override string GetEnumeratorExpression => RenderEnumeratorExpressionWithClosure(this.EnumeratorTypeName,new[] { this.outerKey,this.innerKey,this.resultSelector },this.outerSource.GetEnumeratorExpression,this.innerSource.GetEnumeratorExpression,this.comparerExpression);
    public override string GetEnumeratorPrimaryConstructorParameters => $"TOuterEnum {this.e},TInnerEnum {this.innerEnumerator},global::System.Collections.Generic.IEqualityComparer<{FuncReturnTypeName(this.innerKey)}>? comparer{this.ClosurePrimaryCtorSuffix(this.outerKey,this.innerKey,this.resultSelector)}";
    public override string GetEnumerableExpression => this.UseResultTypeParam ? $"{this.FactoryMethodName}({this.outerSource.GetEnumeratorExpression},{this.innerSource.GetEnumeratorExpression},{this.comparerExpression}{this.ClosureArgsSuffix(this.outerKey,this.innerKey,this.resultSelector)})" : base.GetEnumerableExpression;
    public override string GetEnumeratorConstructor => this.CanSkipLookup()
        ? $"DrainInner(this.{this.innerEnumerator});"
        : string.Empty;
    // 内側の探索表は、外側の最初の要素を得てから作る(System.Linq と同じ。外側が空・列を使わない終端なら、内側のキー選択子を評価しない)。
    private string LazyBuildStatement => $"if(!this.{this.built}){{\n    this.{this.built}=true;\n    this.{this.lookup}=BuildLookup(this.{this.innerEnumerator},this.comparer{this.ClosureArgsSuffixForCtorBody(this.outerKey,this.innerKey,this.resultSelector)});\n}}";
    private string built => $"__built{番号}";
    public override string? NonEnumeratedCountExpression =>
        this.InnerEnumerationIsObservable() ? null : this.outerSource.NonEnumeratedCountExpression;
    protected override string GetTryGetNonEnumeratedCountBody => this.InnerEnumerationIsObservable()
        ? DefaultTryGetNonEnumeratedCountBody
        : $$"""
            if(this.{{this.e}}.TryGetNonEnumeratedCount(out count)){
                return true;
            }
            count=0;
            return false;
            """;
    public override string GetEnumeratorFields => $$"""
        private global::Seamless.Linq.Lookup<{{FuncReturnTypeName(this.innerKey)}},{{this.innerSource.ElementType}}>? {{this.lookup}};
        private bool {{this.built}};
        private int {{this.state}};
        {{this.LookupMembers()}}
        """;
    private string LookupMembers() {
        if(!this.CanSkipLookup()) {
            return this.BuildLookupBody(true,"IncrementLogicalGroupJoinInner")+"""

                private static bool IsNullKey<TKey>(TKey key){
                    return key is null;
                }
                """;
        }
        if(!this.InnerEnumerationIsObservable()) {
            return $$"""
                private static void DrainInner(TInnerEnum inner){
                    inner.Dispose();
                }
                """;
        }
        return $$"""
            private static void DrainInner(TInnerEnum inner){
                using var {{this.innerEnumerator}}=inner;
                while({{this.innerEnumerator}}.TryGetNext(out _)){
                    {{this.CounterCall("IncrementLogicalGroupJoinInner")}}
                }
            }
            """;
    }
    public override string GetConsumeExecutionStatement(string consumerName) {
        if(this.CanBypassLookupForConsume()){
            return this.outerSource.GetConsumeExecutionStatement(consumerName);
        }else if(this.CanSkipLookup()){
            return this.RenderSkipLookupConsume(consumerName);
        }else{
            return this.RenderLookupConsume(consumerName);
        }
    }

    private string RenderLookupConsume(string consumerName) {
        if(this.UseResultTypeParam) {
            return base.GetConsumeExecutionStatement(consumerName);
        }
        if(!TryPeelWheres(this.outerSource,out var outerRoot,out var outerPredicates)) {
            outerRoot=this.outerSource;
            outerPredicates=System.Array.Empty<FuncFragment>();
        }
        if(!TryPeelWheres(this.innerSource,out var innerRoot,out var innerPredicates)) {
            innerRoot=this.innerSource;
            innerPredicates=System.Array.Empty<FuncFragment>();
        }
        string outerExpression;
        string innerExpression;
        try {
            outerExpression=outerRoot.GetEnumeratorExpression;
            innerExpression=innerRoot.GetEnumeratorExpression;
        } catch(NotSupportedException) {
            return base.GetConsumeExecutionStatement(consumerName);
        }
        var localDeclarations = string.Join("\n",new[]{
            this.HoistedLocalDeclarations(this.outerKey,this.innerKey,this.resultSelector),
            this.StaticDelegateLocalDeclarations(this.outerKey,this.innerKey,this.resultSelector),
        }.Where(static text => !string.IsNullOrEmpty(text)));

        var outerName = $"__groupJoinOuter{番号}";
        var innerName = $"__groupJoinInner{番号}";
        var lookupName = $"__groupJoinLookup{番号}";
        var lookupBuilderName = $"__groupJoinLookupBuilder{番号}";
        var outerItemName = $"__groupJoinOuterItem{番号}";
        var outerKeyName = $"__groupJoinOuterKey{番号}";
        var groupName = $"__groupJoinGroup{番号}";
        var resultItemName = $"__groupJoinResult{番号}";
        var hasCntName = $"__hasCnt{番号}";
        var cntName = $"__cnt{番号}";
        var innerItemName = $"__groupJoinInnerItem{番号}";
        var innerKeyName = $"__groupJoinInnerKey{番号}";
        var hasKeyName = $"__groupJoinHasKey{番号}";
        var cmpArg = string.IsNullOrEmpty(this.comparerExpression)||"null".Equals(this.comparerExpression,StringComparison.Ordinal)
            ? "null"
            : this.comparerExpression;

        var innerBody = $$"""
            {{this.CounterCall("IncrementLogicalGroupJoinInner")}}
            {{this.InnerKeyType}} {{innerKeyName}}=default!;
            {{this.FuncAssignStaticIndented("",this.innerKey,nameof(this.innerKey),innerKeyName,innerItemName)}}
            if({{hasKeyName}}({{innerKeyName}})){
                {{this.CounterCall("IncrementHashBuild")}}
                {{this.CounterCall("IncrementBufferAdd")}}
                {{lookupBuilderName}}.Add({{innerKeyName}},{{innerItemName}});
            }
            """;

        var outerBody = $$"""
            {{this.CounterCall("IncrementLogicalGroupJoinOuter")}}
            {{FuncReturnTypeName(this.outerKey)}} {{outerKeyName}}=default!;
            {{this.FuncAssignStaticIndented("",this.outerKey,nameof(this.outerKey),outerKeyName,outerItemName)}}
            {{this.CounterCall("IncrementHashProbe")}}
            {{this.ElementType}} {{resultItemName}}=default!;
            if({{lookupName}}!.GetGroup({{outerKeyName}}) is { } {{groupName}}){
                {{this.FuncAssignStaticIndented("    ",this.resultSelector,nameof(this.resultSelector),resultItemName,outerItemName,groupName)}}
            }else{
                {{this.FuncAssignStaticIndented("    ",this.resultSelector,nameof(this.resultSelector),resultItemName,outerItemName,$"(global::System.Collections.Generic.IEnumerable<{this.InnerElementType}>)global::System.Array.Empty<{this.InnerElementType}>()")}}
            }
            {{consumerName}}.Consume({{resultItemName}});
            """;
        // 外側の最初の要素を得てから、内側の探索表を作る(外側が空なら、内側のキー選択子を評価しない。System.Linq と同じ)。
        var builtName = $"__groupJoinBuilt{番号}";
        var lazyOuterBody = $$"""
            if(!{{builtName}}){
                {{builtName}}=true;
                {{IndentLines(this.RenderRootLoop(innerRoot,innerPredicates,innerName,innerItemName,innerBody),"    ").TrimStart()}}
                {{lookupName}}={{lookupBuilderName}}.BuildPooledAndClear();
            }
            {{outerBody}}
            """;

        return $$"""
            {
                static bool {{hasKeyName}}<TKey>(TKey key) => key is not null;
                {{localDeclarations}}
                using var {{innerName}}={{innerExpression}};
                var {{hasCntName}}={{innerName}}.TryGetNonEnumeratedCount(out var {{cntName}});
                int __initialCapacity{{番号}};
                if({{hasCntName}}){
                    global::Seamless.Linq.LinqCounterScope.IncrementHashCapacityHintUsed();
                    __initialCapacity{{番号}}={{cntName}};
                }else{
                    __initialCapacity{{番号}}=0;
                }
                var {{lookupBuilderName}}=new global::Seamless.Linq.LookupBuilder<{{this.InnerKeyType}},{{this.InnerElementType}}>(__initialCapacity{{番号}},(global::System.Collections.Generic.IEqualityComparer<{{this.InnerKeyType}}>?){{cmpArg}},true);
                var {{builtName}}=false;
                global::Seamless.Linq.Lookup<{{this.InnerKeyType}},{{this.InnerElementType}}>? {{lookupName}}=null;
                using var {{outerName}}={{outerExpression}};
                try {
                    {{IndentLines(this.RenderRootLoop(outerRoot,outerPredicates,outerName,outerItemName,lazyOuterBody),"        ").TrimStart()}}
                } finally {
                    {{lookupName}}?.Dispose();
                }
            }
            """;
    }

    public string? RenderDirectFirstInline(string destVar,string destType,bool orDefault) {
        if(this.UseResultTypeParam) {
            return null;
        }
        if(!TryPeelWheres(this.outerSource,out var outerRoot,out var outerPredicates)) {
            outerRoot=this.outerSource;
            outerPredicates=System.Array.Empty<FuncFragment>();
        }
        if(!TryPeelWheres(this.innerSource,out var innerRoot,out var innerPredicates)) {
            innerRoot=this.innerSource;
            innerPredicates=System.Array.Empty<FuncFragment>();
        }
        string outerExpression;
        string innerExpression;
        try {
            outerExpression=outerRoot.GetEnumeratorExpression;
            innerExpression=innerRoot.GetEnumeratorExpression;
        } catch(NotSupportedException) {
            return null;
        }
        var localDeclarations = string.Join("\n",new[]{
            this.HoistedLocalDeclarations(this.outerKey,this.innerKey,this.resultSelector),
            this.StaticDelegateLocalDeclarations(this.outerKey,this.innerKey,this.resultSelector),
        }.Where(static text => !string.IsNullOrEmpty(text)));

        var outerName = $"__groupJoinOuter{番号}";
        var innerName = $"__groupJoinInner{番号}";
        var lookupName = $"__groupJoinLookup{番号}";
        var lookupBuilderName = $"__groupJoinLookupBuilder{番号}";
        var outerItemName = $"__groupJoinOuterItem{番号}";
        var outerKeyName = $"__groupJoinOuterKey{番号}";
        var groupName = $"__groupJoinGroup{番号}";
        var hasCntName = $"__hasCnt{番号}";
        var cntName = $"__cnt{番号}";
        var innerItemName = $"__groupJoinInnerItem{番号}";
        var innerKeyName = $"__groupJoinInnerKey{番号}";
        var hasKeyName = $"__groupJoinHasKey{番号}";
        var foundName = $"__found{番号}";
        var cmpArg = string.IsNullOrEmpty(this.comparerExpression)||"null".Equals(this.comparerExpression,StringComparison.Ordinal)
            ? "null"
            : this.comparerExpression;

        var innerBody = $$"""
            {{this.CounterCall("IncrementLogicalGroupJoinInner")}}
            {{this.InnerKeyType}} {{innerKeyName}}=default!;
            {{this.FuncAssignStaticIndented("",this.innerKey,nameof(this.innerKey),innerKeyName,innerItemName)}}
            if({{hasKeyName}}({{innerKeyName}})){
                {{this.CounterCall("IncrementHashBuild")}}
                {{this.CounterCall("IncrementBufferAdd")}}
                {{lookupBuilderName}}.Add({{innerKeyName}},{{innerItemName}});
            }
            """;

        var outerBody = $$"""
            {{this.CounterCall("IncrementLogicalGroupJoinOuter")}}
            {{FuncReturnTypeName(this.outerKey)}} {{outerKeyName}}=default!;
            {{this.FuncAssignStaticIndented("",this.outerKey,nameof(this.outerKey),outerKeyName,outerItemName)}}
            {{this.CounterCall("IncrementHashProbe")}}
            if({{lookupName}}!.GetGroup({{outerKeyName}}) is { } {{groupName}}){
                {{this.FuncAssignStaticIndented("    ",this.resultSelector,nameof(this.resultSelector),destVar,outerItemName,groupName)}}
            }else{
                {{this.FuncAssignStaticIndented("    ",this.resultSelector,nameof(this.resultSelector),destVar,outerItemName,$"(global::System.Collections.Generic.IEnumerable<{this.InnerElementType}>)global::System.Array.Empty<{this.InnerElementType}>()")}}
            }
            {{foundName}}=true;
            break;
            """;
        // 外側の最初の要素を得てから、内側の探索表を作る(外側が空なら、内側のキー選択子を評価しない。System.Linq と同じ)。
        var builtName = $"__groupJoinBuilt{番号}";
        var lazyOuterBody = $$"""
            if(!{{builtName}}){
                {{builtName}}=true;
                {{IndentLines(this.RenderRootLoop(innerRoot,innerPredicates,innerName,innerItemName,innerBody),"    ").TrimStart()}}
                {{lookupName}}={{lookupBuilderName}}.BuildPooledAndClear();
            }
            {{outerBody}}
            """;

        string notFoundBlock;
        if(orDefault){
            notFoundBlock=$"{destVar}=default!;";
        }else{
            notFoundBlock="throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();";
        }

        return $$"""
            {{destType}} {{destVar}}=default!;
            {
                static bool {{hasKeyName}}<TKey>(TKey key) => key is not null;
                {{localDeclarations}}
                using var {{innerName}}={{innerExpression}};
                var {{hasCntName}}={{innerName}}.TryGetNonEnumeratedCount(out var {{cntName}});
                int __initialCapacity{{番号}};
                if({{hasCntName}}){
                    global::Seamless.Linq.LinqCounterScope.IncrementHashCapacityHintUsed();
                    __initialCapacity{{番号}}={{cntName}};
                }else{
                    __initialCapacity{{番号}}=0;
                }
                var {{lookupBuilderName}}=new global::Seamless.Linq.LookupBuilder<{{this.InnerKeyType}},{{this.InnerElementType}}>(__initialCapacity{{番号}},(global::System.Collections.Generic.IEqualityComparer<{{this.InnerKeyType}}>?){{cmpArg}},true);
                var {{builtName}}=false;
                global::Seamless.Linq.Lookup<{{this.InnerKeyType}},{{this.InnerElementType}}>? {{lookupName}}=null;
                using var {{outerName}}={{outerExpression}};
                var {{foundName}}=false;
                try {
                    {{IndentLines(this.RenderRootLoop(outerRoot,outerPredicates,outerName,outerItemName,lazyOuterBody),"        ").TrimStart()}}
                } finally {
                    {{lookupName}}?.Dispose();
                }
                if(!{{foundName}}){
                    {{notFoundBlock}}
                }else{
                }
            }
            """;
    }

    // 探索表を省く形(結果が外側そのもの)の Consume は、外側を差し替えメソッドから直接回す(span があれば span)。
    // GroupJoin の列挙子の構造体から外側の構造体を呼ぶ入れ子は、参照型の共有ジェネリックコードで内側がインライン化されない。
    // 評価順は列挙子を作るときと同じく外側、内側の順。内側は .Anchor があるときだけ列挙する。
    private string RenderSkipLookupConsume(string consumerName) {
        string outerExpression;
        string innerExpression;
        try {
            outerExpression=this.outerSource.GetEnumeratorExpression;
            innerExpression=this.innerSource.GetEnumeratorExpression;
        } catch(NotSupportedException) {
            return base.GetConsumeExecutionStatement(consumerName);
        }
        var outerName = $"__groupJoinOuter{番号}";
        var innerName = $"__groupJoinInner{番号}";
        var itemName = $"__groupJoinItem{番号}";
        var drain = this.InnerEnumerationIsObservable()
            ? $"while({innerName}.TryGetNext(out _)){{}}"
            : string.Empty;
        var spanExpression = this.outerSource.GetDirectSpanExpression(outerName);
        var loop = spanExpression is null
            ? $$"""
                {{DrainLoopHead(this.outerSource,outerName,itemName)}}
                    {{consumerName}}.Consume({{itemName}});
                }
                """
            : $$"""
                var __groupJoinSpan{{番号}}={{spanExpression}};
                for(var __i{{番号}}=0;__i{{番号}}<__groupJoinSpan{{番号}}.Length;__i{{番号}}++){
                    {{consumerName}}.Consume(__groupJoinSpan{{番号}}[__i{{番号}}]);
                }
                """;
        return $$"""
            using var {{outerName}}={{outerExpression}};
            using var {{innerName}}={{innerExpression}};
            {{drain}}
            {{loop}}
            """;
    }
    public override string GetTryGetNextBody => this.CanSkipLookup() ? $$"""
        if({{this.state}}==1){
            current=default!;
            return false;
        }
        if({{this.e}}.TryGetNext(out var __mn36)){
            {{this.CounterCall("IncrementLogicalGroupJoinOuter")}}
            this.{{this.currentValue}}=__mn36;
            current=__mn36;
            return true;
        }
        {{this.state}}=1;
        current=default!;
        return false;
        """ : $$"""
        if({{this.state}}==1){
            current=default!;
            return false;
        }
        if({{this.e}}.TryGetNext(out var __mn36)){
            {{this.CounterCall("IncrementLogicalGroupJoinOuter")}}
            var {{this.outerValue}}=__mn36;
            {{this.LazyBuildStatement}}
            {{FuncReturnTypeName(this.outerKey)}} {{this.key}}=default!;
            {{this.FuncAssignIndented("    ",this.outerKey,nameof(this.outerKey),this.key,this.outerValue)}}
            {{this.CounterCall("IncrementHashProbe")}}
            if({{this.lookup}}!.GetGroup({{this.key}}) is { } {{this.group}}){
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
    // 探索表を作る前に捨てるときは、内側の列挙子を探索表の構築が閉じていないので、ここで閉じる(探索表を省く形は作るときに閉じている)。
    protected override string GetEnumeratorDisposeBody => this.CanSkipLookup()
        ? $"{this.lookup}?.Dispose(); {this.e}.Dispose();"
        : $"{this.lookup}?.Dispose(); {this.e}.Dispose(); if(!this.{this.built}) {{ this.{this.innerEnumerator}.Dispose(); }}";

    // 結果選択子が内側のグループを使わず、キー選択子が恒等で比較子も既定なら、探索表の中身は結果に現れない。
    // そのときは探索表を作らない。ソース式の評価と列挙子の構築は通常と同じ順・同じ時点で行う。
    private bool CanSkipLookup() =>
        this.comparerExpression=="null"
        &&this.outerKey.IsIdentity
        &&this.innerKey.IsIdentity
        &&this.ResultSelectorReturnsOuter();

    // FLinq の契約では .Anchor の無いソースは列挙に副作用も例外も無いとみなすので、内側の列挙そのものも省く。
    // 内側のソース式に .Anchor があるときだけ、列挙の副作用と例外を残すために列挙する。
    private bool InnerEnumerationIsObservable() => this.innerSource.TreeContainsAnchor();

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
