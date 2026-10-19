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
    public override string GetEnumeratorConstructor => this.CountsOnly()
        ? $"this.{this.lookup}=BuildCounts(this.{this.innerEnumerator},this.comparer{this.ClosureArgsSuffixForCtorBody(this.outerKey,this.innerKey,this.resultSelector)});"
        : this.ProbesInnerRange()
        ? $$"""
            // 内側は Range で恒等キーなので、探索表を作らず区間 [始点,始点+個数) で一致を判定する(各キーに一致する内側は自身1つ)。
            this.{{this.innerEnumerator}}.TryGetNonEnumeratedCount(out var __innerCount);
            this.{{this.innerLow}}=__innerCount>0&&this.{{this.innerEnumerator}}.TryGetNext(out var __innerFirst) ? __innerFirst : 0L;
            this.{{this.innerHigh}}=this.{{this.innerLow}}+__innerCount;
            """
        : $"this.{this.lookup}=BuildLookup(this.{this.innerEnumerator},this.comparer{this.ClosureArgsSuffixForCtorBody(this.outerKey,this.innerKey,this.resultSelector)});\nthis.{this.arranged}=this.{this.lookup}.Arranged;";
    private string arranged => $"__ar{番号}";
    private string groupEnd => $"__ge{番号}";
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
    // 結果選択子が内側の要素を使わない(第2引数が本体に現れない)なら、一致の件数だけが結果を決める。
    // 内側の要素を溜めず、キーごとの件数だけを数える(内側の列挙・キー選択子の評価の回数と順は変わらない)。
    private bool CountsOnly() =>
        !this.UseResultTypeParam
        &&!this.ProbesInnerRange()
        &&this.resultSelector.CanInlineAssign
        &&!this.resultSelector.CanInvokeAsDelegate
        &&!this.resultSelector.RequiresStatementInlining
        &&GroupBySupport.ResultIgnoresGroup(this.resultSelector);
    private string CountsType => $"global::System.Collections.Generic.Dictionary<{this.InnerKeyType},int>";
    private string BuildCountsBody() => $$"""
        private static {{this.CountsType}} BuildCounts(TInnerEnum inner,global::System.Collections.Generic.IEqualityComparer<{{this.InnerKeyType}}>? comparer{{this.ClosureMethodParameterSuffix(this.outerKey,this.innerKey,this.resultSelector)}}){
            var __hasCnt=inner.TryGetNonEnumeratedCount(out var __cnt);
            if(__hasCnt) global::Seamless.Linq.LinqCounterScope.IncrementHashCapacityHintUsed();
            var __counts=new {{this.CountsType}}(__hasCnt ? __cnt : 0,comparer);
            using var {{this.innerEnumerator}}=inner;
            while({{this.innerEnumerator}}.TryGetNext(out var __mn102)){
                {{this.CounterCall("IncrementLogicalJoinInner")}}
                var {{this.item}}=__mn102;
                {{this.InnerKeyType}} {{this.key}}=default!;
                {{this.FuncAssignStaticIndented("        ",this.innerKey,nameof(this.innerKey),this.key,this.item)}}
                if(IsNullKey({{this.key}})){
                    continue;
                }
                {{this.CounterCall("IncrementHashBuild")}}
                global::System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrAddDefault(__counts,{{this.key}},out _)++;
            }
            return __counts;
        }
        """;
    public override string GetEnumeratorFields => this.CountsOnly()
        ? $$"""
            private {{this.CountsType}} {{this.lookup}};
            private int {{this.groupIndex}};
            private {{this.outerSource.ElementType}} {{this.outerValue}};
            private static bool IsNullKey<TKey>(TKey key){
                return key is null;
            }
            {{this.BuildCountsBody()}}
            """
        : this.ProbesInnerRange()
        ? $$"""
            private long {{this.innerLow}};
            private long {{this.innerHigh}};
            """
        : $$"""
        private global::Seamless.Linq.ContiguousLookupBuilder<{{FuncReturnTypeName(this.innerKey)}},{{this.innerSource.ElementType}}> {{this.lookup}};
        private {{this.innerSource.ElementType}}[] {{this.arranged}};
        private int {{this.groupIndex}};
        private int {{this.groupEnd}};
        private {{this.outerSource.ElementType}} {{this.outerValue}};
        private static bool IsNullKey<TKey>(TKey key){
            return key is null;
        }
        {{this.BuildContiguousLookupBody()}}
        """;
    // 内側の探索表: キーへ初出順のグループ番号を振ってから、内側の要素を1本の配列へグループ順に並べる(グループごとに伸びる配列を作らない)。
    // null キーの内側は結合しない(System.Linq と同じ)。
    private string BuildContiguousLookupBody() => $$"""
        private static global::Seamless.Linq.ContiguousLookupBuilder<{{this.InnerKeyType}},{{this.InnerElementType}}> BuildLookup(TInnerEnum inner,global::System.Collections.Generic.IEqualityComparer<{{this.InnerKeyType}}>? comparer{{this.ClosureMethodParameterSuffix(this.outerKey,this.innerKey,this.resultSelector)}}){
            var __hasCnt=inner.TryGetNonEnumeratedCount(out var __cnt);
            if(__hasCnt) global::Seamless.Linq.LinqCounterScope.IncrementHashCapacityHintUsed();
            var __lookupBuilder=new global::Seamless.Linq.ContiguousLookupBuilder<{{this.InnerKeyType}},{{this.InnerElementType}}>(__hasCnt ? __cnt : 0,__hasCnt ? __cnt : 0,comparer);
            using var {{this.innerEnumerator}}=inner;
            while({{this.innerEnumerator}}.TryGetNext(out var __mn102)){
                {{this.CounterCall("IncrementLogicalJoinInner")}}
                var {{this.item}}=__mn102;
                {{this.InnerKeyType}} {{this.key}}=default!;
                {{this.FuncAssignStaticIndented("        ",this.innerKey,nameof(this.innerKey),this.key,this.item)}}
                if(IsNullKey({{this.key}})){
                    continue;
                }
                {{this.CounterCall("IncrementHashBuild")}}
                {{this.CounterCall("IncrementBufferAdd")}}
                __lookupBuilder.Add({{this.key}},{{this.item}});
            }
            __lookupBuilder.PrepareProbe();
            return __lookupBuilder;
        }
        """;
    public override string GetTryGetNextBody => this.CountsOnly()
        ? $$"""
        {{this.Start}}:
        if({{this.groupIndex}}>0){
            {{this.groupIndex}}--;
            {{this.FuncAssignCurrentIndented("    ",this.resultSelector,nameof(this.resultSelector),$"this.{this.currentValue}",this.outerValue,$"default({this.InnerElementType})!")}}
            return true;
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
        {{this.lookup}}.TryGetValue({{this.key}},out {{this.groupIndex}});
        goto {{this.Start}};
        """
        : this.ProbesInnerRange()
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
        if({{this.groupIndex}}<{{this.groupEnd}}){
            var {{this.item}}={{this.arranged}}[{{this.groupIndex}}++];
            {{this.FuncAssignCurrentIndented("    ",this.resultSelector,nameof(this.resultSelector),$"this.{this.currentValue}",this.outerValue,this.item)}}
            return true;
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
        {{this.lookup}}.TryGetRange({{this.key}},out {{this.groupIndex}},out {{this.groupEnd}});
        goto {{this.Start}};
        """;
    public override string GetConsumeExecutionStatement(string consumerName) {
        if(this.RenderRangeOverlapConsume(consumerName) is { } overlapConsume) {
            return overlapConsume;
        }
        if(this.RenderFilteredRootsConsume(consumerName) is { } filteredConsume) {
            return filteredConsume;
        }
        if(this.UseResultTypeParam
            ||this.outerSource.DirectArrayExpression is not { } outerArrayExpression
            ||this.innerSource.DirectArrayExpression is not { } innerArrayExpression) {
            return base.GetConsumeExecutionStatement(consumerName);
        }

        if(this.CountsOnly()) {
            return this.RenderCountsOnlyArrayConsume(consumerName,outerArrayExpression,innerArrayExpression);
        }
        var outerArrayName = $"__joinOuterArray{番号}";
        var innerArrayName = $"__joinInnerArray{番号}";
        var lookupBuilderName = $"__joinLookupBuilder{番号}";
        var arrangedName = $"__joinArranged{番号}";
        var groupEndName = $"__joinGroupEnd{番号}";
        var outerIndexName = $"__joinOuterIndex{番号}";
        var innerIndexName = $"__joinInnerIndex{番号}";
        var groupIndexName = $"__joinGroupIndex{番号}";
        var outerItemName = $"__joinOuterItem{番号}";
        var innerItemName = $"__joinInnerItem{番号}";
        var innerKeyName = $"__joinInnerKey{番号}";
        var outerKeyName = $"__joinOuterKey{番号}";
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
            var {{lookupBuilderName}}=new global::Seamless.Linq.ContiguousLookupBuilder<{{this.InnerKeyType}},{{this.InnerElementType}}>({{innerArrayName}}.Length,{{innerArrayName}}.Length,{{this.comparerExpression}});
            try {
            for(var {{innerIndexName}}=0;{{innerIndexName}}<{{innerArrayName}}.Length;{{innerIndexName}}++){
                var {{innerItemName}}={{innerArrayName}}[{{innerIndexName}}];
                {{this.InnerKeyType}} {{innerKeyName}}=default!;
                {{this.FuncAssignStaticIndented("    ",this.innerKey,nameof(this.innerKey),innerKeyName,innerItemName)}}
                if({{hasKeyName}}({{innerKeyName}})){
                    {{lookupBuilderName}}.Add({{innerKeyName}},{{innerItemName}});
                }
            }
            {{lookupBuilderName}}.PrepareProbe();
            var {{arrangedName}}={{lookupBuilderName}}.Arranged;
            var {{outerArrayName}}={{outerArrayExpression}};
            for(var {{outerIndexName}}=0;{{outerIndexName}}<{{outerArrayName}}.Length;{{outerIndexName}}++){
                var {{outerItemName}}={{outerArrayName}}[{{outerIndexName}}];
                {{this.InnerKeyType}} {{outerKeyName}}=default!;
                {{this.FuncAssignStaticIndented("    ",this.outerKey,nameof(this.outerKey),outerKeyName,outerItemName)}}
                if(!{{hasKeyName}}({{outerKeyName}})) continue;
                if(!{{lookupBuilderName}}.TryGetRange({{outerKeyName}},out var {{groupIndexName}},out var {{groupEndName}})) continue;
                for(;{{groupIndexName}}<{{groupEndName}};{{groupIndexName}}++){
                    var {{innerItemName}}={{arrangedName}}[{{groupIndexName}}];
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
    // 外側・内側が「根(span を出せる配列・List、または IEnumerable)の上に添字の無い Where が0個以上並ぶ」形なら、
    // Join の列挙子の構造体や Where の構造体を作らずに、差し替えメソッドの中で根を直接回して述語をその場に展開する
    // (構造体→Where の構造体→根の構造体の入れ子は、参照型の共有ジェネリックコードで内側がインライン化されない)。
    // 評価の順は従来と同じ(外側・内側のソース式、内側を最後まで、外側の順)。
    private string? RenderFilteredRootsConsume(string consumerName) {
        if(this.UseResultTypeParam
            ||this.ProbesInnerRange()
            ||!TryPeelWheres(this.outerSource,out var outerRoot,out var outerPredicates)
            ||!TryPeelWheres(this.innerSource,out var innerRoot,out var innerPredicates)) {
            return null;
        }
        string outerExpression;
        string innerExpression;
        try {
            outerExpression=outerRoot.GetEnumeratorExpression;
            innerExpression=innerRoot.GetEnumeratorExpression;
        } catch(NotSupportedException) {
            return null;
        }
        var outerName = $"__joinOuterRoot{番号}";
        var innerName = $"__joinInnerRoot{番号}";
        var outerItemName = $"__joinOuterItem{番号}";
        var innerItemName = $"__joinInnerItem{番号}";
        var innerKeyName = $"__joinInnerKey{番号}";
        var outerKeyName = $"__joinOuterKey{番号}";
        var builderName = $"__joinLookup{番号}";
        var arrangedName = $"__joinArranged{番号}";
        var startName = $"__joinStart{番号}";
        var endName = $"__joinEnd{番号}";
        var countName = $"__joinCapacity{番号}";
        var resultName = $"__joinResult{番号}";
        var hasKeyName = $"__joinHasKey{番号}";
        var functions = outerPredicates.Concat(innerPredicates).Concat(new[] { this.outerKey,this.innerKey,this.resultSelector }).ToArray();
        var localDeclarations = string.Join("\n",new[]{
            this.HoistedLocalDeclarations(functions),
            this.StaticDelegateLocalDeclarations(functions),
        }.Where(static text => !string.IsNullOrEmpty(text)));
        var countsOnly = this.CountsOnly();
        var innerAdd = countsOnly
            ? $"global::System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrAddDefault({builderName},{innerKeyName},out _)++;"
            : $"{builderName}.Add({innerKeyName},{innerItemName});";
        var innerBody = $"{this.InnerKeyType} {innerKeyName}=default!;\n{this.FuncAssignStatic(this.innerKey,nameof(this.innerKey),innerKeyName,innerItemName)}\nif({hasKeyName}({innerKeyName})){{\n    {innerAdd}\n}}";
        var matchedInner = countsOnly ? $"default({this.InnerElementType})!" : $"{arrangedName}[{startName}]";
        var emit = $"{this.ElementType} {resultName}=default!;\n{this.FuncAssignStatic(this.resultSelector,nameof(this.resultSelector),resultName,outerItemName,matchedInner)}\n{consumerName}.Consume({resultName});";
        var probe = countsOnly
            ? $"if(!{builderName}.TryGetValue({outerKeyName},out var {endName})) continue;\nfor(var {startName}=0;{startName}<{endName};{startName}++){{\n{IndentGeneratedStatement(emit,"    ")}\n}}"
            : $"if(!{builderName}.TryGetRange({outerKeyName},out var {startName},out var {endName})) continue;\nfor(;{startName}<{endName};{startName}++){{\n{IndentGeneratedStatement(emit,"    ")}\n}}";
        var outerBody = $"{this.InnerKeyType} {outerKeyName}=default!;\n{this.FuncAssignStatic(this.outerKey,nameof(this.outerKey),outerKeyName,outerItemName)}\nif(!{hasKeyName}({outerKeyName})) continue;\n{probe}";
        var builderDeclaration = countsOnly
            ? $"var {builderName}=new {this.CountsType}({countName},{this.comparerExpression});"
            : $"var {builderName}=new global::Seamless.Linq.ContiguousLookupBuilder<{this.InnerKeyType},{this.InnerElementType}>({countName},{countName},{this.comparerExpression});";
        var prepare = countsOnly ? string.Empty : $"{builderName}.PrepareProbe();\nvar {arrangedName}={builderName}.Arranged;";
        var dispose = countsOnly ? string.Empty : $"{builderName}.Dispose();";
        return $$"""
            {{localDeclarations}}
            static bool {{hasKeyName}}<TKey>(TKey key) => key is not null;
            using var {{outerName}}={{outerExpression}};
            using var {{innerName}}={{innerExpression}};
            var {{countName}}={{innerName}}.TryGetNonEnumeratedCount(out var {{countName}}Value) ? {{countName}}Value : 0;
            {{builderDeclaration}}
            try {
            {{this.RenderRootLoop(innerRoot,innerPredicates,innerName,innerItemName,innerBody)}}
            {{prepare}}
            {{this.RenderRootLoop(outerRoot,outerPredicates,outerName,outerItemName,outerBody)}}
            } finally {
                {{dispose}}
            }
            """;
    }

    private string RenderCountsOnlyArrayConsume(string consumerName,string outerArrayExpression,string innerArrayExpression) {
        var outerArrayName = $"__joinOuterArray{番号}";
        var innerArrayName = $"__joinInnerArray{番号}";
        var countsName = $"__joinCounts{番号}";
        var outerIndexName = $"__joinOuterIndex{番号}";
        var innerIndexName = $"__joinInnerIndex{番号}";
        var matchName = $"__joinMatch{番号}";
        var matchCountName = $"__joinMatchCount{番号}";
        var outerItemName = $"__joinOuterItem{番号}";
        var innerItemName = $"__joinInnerItem{番号}";
        var innerKeyName = $"__joinInnerKey{番号}";
        var outerKeyName = $"__joinOuterKey{番号}";
        var resultName = $"__joinResult{番号}";
        var hasKeyName = $"__joinHasKey{番号}";
        var localDeclarations = string.Join("\n",new[]{
            this.HoistedLocalDeclarations(this.outerKey,this.innerKey,this.resultSelector),
            this.StaticDelegateLocalDeclarations(this.outerKey,this.innerKey,this.resultSelector),
        }.Where(static text => !string.IsNullOrEmpty(text)));
        return $$"""
            {{localDeclarations}}
            static bool {{hasKeyName}}<TKey>(TKey key) => key is not null;
            var {{innerArrayName}}={{innerArrayExpression}};
            var {{countsName}}=new {{this.CountsType}}({{innerArrayName}}.Length,{{this.comparerExpression}});
            for(var {{innerIndexName}}=0;{{innerIndexName}}<{{innerArrayName}}.Length;{{innerIndexName}}++){
                var {{innerItemName}}={{innerArrayName}}[{{innerIndexName}}];
                {{this.InnerKeyType}} {{innerKeyName}}=default!;
                {{this.FuncAssignStaticIndented("    ",this.innerKey,nameof(this.innerKey),innerKeyName,innerItemName)}}
                if({{hasKeyName}}({{innerKeyName}})){
                    global::System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrAddDefault({{countsName}},{{innerKeyName}},out _)++;
                }
            }
            var {{outerArrayName}}={{outerArrayExpression}};
            for(var {{outerIndexName}}=0;{{outerIndexName}}<{{outerArrayName}}.Length;{{outerIndexName}}++){
                var {{outerItemName}}={{outerArrayName}}[{{outerIndexName}}];
                {{this.InnerKeyType}} {{outerKeyName}}=default!;
                {{this.FuncAssignStaticIndented("    ",this.outerKey,nameof(this.outerKey),outerKeyName,outerItemName)}}
                if(!{{hasKeyName}}({{outerKeyName}})||!{{countsName}}.TryGetValue({{outerKeyName}},out var {{matchCountName}})) continue;
                for(var {{matchName}}=0;{{matchName}}<{{matchCountName}};{{matchName}}++){
                    {{this.ElementType}} {{resultName}}=default!;
                    {{this.FuncAssignStaticIndented("        ",this.resultSelector,nameof(this.resultSelector),resultName,outerItemName,$"default({this.InnerElementType})!")}}
                    {{consumerName}}.Consume({{resultName}});
                }
            }
            """;
    }

    // 外側も .Anchor の無い Range で外側のキーが恒等なら、一致するのは2つの区間の重なりの値だけで、内側の要素も外側の要素と等しい。
    // 外側を全部回して区間の内外を調べず、重なりだけを回す(引数の検査は両方の Range について行う)。
    private string? RenderRangeOverlapConsume(string consumerName) {
        if(this.UseResultTypeParam
            ||!this.ProbesInnerRange()
            ||this.outerSource is not RangeRoot outerRange
            ||this.innerSource is not RangeRoot innerRange
            ||this.outerSource.TreeContainsAnchor()
            ||!this.outerKey.IsIdentity
            ||this.outerKey.ContainsAnchor
            ||this.resultSelector.ContainsAnchor) {
            return null;
        }
        var outerStart = $"__joinOuterStart{番号}";
        var outerCount = $"__joinOuterCount{番号}";
        var innerStart = $"__joinInnerStart{番号}";
        var innerCount = $"__joinInnerCount{番号}";
        var low = $"__joinOverlapLow{番号}";
        var high = $"__joinOverlapHigh{番号}";
        var value = $"__joinOverlapValue{番号}";
        var itemName = $"__joinOverlapItem{番号}";
        var resultName = $"__joinOverlapResult{番号}";
        var localDeclarations = string.Join("\n",new[]{
            this.HoistedLocalDeclarations(this.resultSelector),
            this.StaticDelegateLocalDeclarations(this.resultSelector),
        }.Where(static text => !string.IsNullOrEmpty(text)));
        var assign = $"{this.ElementType} {resultName}=default!;\n{this.FuncAssignStatic(this.resultSelector,nameof(this.resultSelector),resultName,itemName,itemName)}\n{consumerName}.Consume({resultName});";
        return $$"""
            {{localDeclarations}}
            {{outerRange.RenderArgumentPreparation(outerStart,outerCount)}}
            {{innerRange.RenderArgumentPreparation(innerStart,innerCount)}}
            var {{low}}=global::System.Math.Max((long){{outerStart}},(long){{innerStart}});
            var {{high}}=global::System.Math.Min((long){{outerStart}}+{{outerCount}},(long){{innerStart}}+{{innerCount}});
            for(var {{value}}={{low}};{{value}}<{{high}};{{value}}++){
                var {{itemName}}=(int){{value}};
                {{IndentGeneratedStatement(assign,"    ")}}
            }
            """;
    }
    public string? RenderDirectFirstInline(string destVar,string destType,bool orDefault){
        if(this.UseResultTypeParam){
            return null;
        }
        string notFoundBlock;
        if(orDefault){
            notFoundBlock=$"{destVar}=default!;";
        }else{
            notFoundBlock="throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();";
        }

        if(this.ProbesInnerRange()
            &&this.outerSource is RangeRoot outerRange
            &&this.innerSource is RangeRoot innerRange
            &&!this.outerSource.TreeContainsAnchor()
            &&this.outerKey.IsIdentity
            &&!this.outerKey.ContainsAnchor
            &&!this.resultSelector.ContainsAnchor){
            var outerStart=$"__joinOuterStart{番号}";
            var outerCount=$"__joinOuterCount{番号}";
            var innerStart=$"__joinInnerStart{番号}";
            var innerCount=$"__joinInnerCount{番号}";
            var low=$"__joinOverlapLow{番号}";
            var high=$"__joinOverlapHigh{番号}";
            var itemName=$"__joinOverlapItem{番号}";
            var localDeclarations=string.Join("\n",new[]{
                this.HoistedLocalDeclarations(this.resultSelector),
                this.StaticDelegateLocalDeclarations(this.resultSelector),
            }.Where(static text => !string.IsNullOrEmpty(text)));
            string localDeclBlock;
            if(string.IsNullOrEmpty(localDeclarations)){
                localDeclBlock=string.Empty;
            }else{
                localDeclBlock=localDeclarations+"\n";
            }
            var assign=this.FuncAssignStatic(this.resultSelector,nameof(this.resultSelector),destVar,itemName,itemName);
            return $$"""
                {{localDeclBlock}}{{destType}} {{destVar}}=default!;
                {{outerRange.RenderArgumentPreparation(outerStart,outerCount)}}
                {{innerRange.RenderArgumentPreparation(innerStart,innerCount)}}
                var {{low}}=global::System.Math.Max((long){{outerStart}},(long){{innerStart}});
                var {{high}}=global::System.Math.Min((long){{outerStart}}+{{outerCount}},(long){{innerStart}}+{{innerCount}});
                if({{low}}<{{high}}){
                    var {{itemName}}=(int){{low}};
                    {{assign}}
                }else{
                    {{notFoundBlock}}
                }
                """;
        }

        if(!TryPeelWheres(this.outerSource,out var outerRoot,out var outerPredicates)
            ||!TryPeelWheres(this.innerSource,out var innerRoot,out var innerPredicates)){
            return null;
        }
        string outerExpression;
        string innerExpression;
        try{
            outerExpression=outerRoot.GetEnumeratorExpression;
            innerExpression=innerRoot.GetEnumeratorExpression;
        }catch(NotSupportedException){
            return null;
        }

        var outerName=$"__joinOuterRoot{番号}";
        var innerName=$"__joinInnerRoot{番号}";
        var outerItemName=$"__joinOuterItem{番号}";
        var innerItemName=$"__joinInnerItem{番号}";
        var innerKeyName=$"__joinInnerKey{番号}";
        var outerKeyName=$"__joinOuterKey{番号}";
        var builderName=$"__joinLookup{番号}";
        var arrangedName=$"__joinArranged{番号}";
        var startName=$"__joinStart{番号}";
        var endName=$"__joinEnd{番号}";
        var countName=$"__joinCapacity{番号}";
        var hasKeyName=$"__joinHasKey{番号}";
        var foundName=$"__joinFound{番号}";
        var functions=outerPredicates.Concat(innerPredicates).Concat(new[]{ this.outerKey,this.innerKey,this.resultSelector }).ToArray();
        var localDeclarationsGeneral=string.Join("\n",new[]{
            this.HoistedLocalDeclarations(functions),
            this.StaticDelegateLocalDeclarations(functions),
        }.Where(static text => !string.IsNullOrEmpty(text)));
        string localDeclBlockGeneral;
        if(string.IsNullOrEmpty(localDeclarationsGeneral)){
            localDeclBlockGeneral=string.Empty;
        }else{
            localDeclBlockGeneral=localDeclarationsGeneral+"\n";
        }
        var countsOnly=this.CountsOnly();
        var innerAdd=countsOnly
            ? $"global::System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrAddDefault({builderName},{innerKeyName},out _)++;"
            : $"{builderName}.Add({innerKeyName},{innerItemName});";
        var innerBody=$"{this.InnerKeyType} {innerKeyName}=default!;\n{this.FuncAssignStatic(this.innerKey,nameof(this.innerKey),innerKeyName,innerItemName)}\nif({hasKeyName}({innerKeyName})){{\n    {innerAdd}\n}}";
        var matchedInner=countsOnly ? $"default({this.InnerElementType})!" : $"{arrangedName}[{startName}]";
        var assignGeneral=this.FuncAssignStatic(this.resultSelector,nameof(this.resultSelector),destVar,outerItemName,matchedInner);
        var probe=countsOnly
            ? $"if(!{builderName}.TryGetValue({outerKeyName},out var {endName})||{endName}<=0) continue;\n{assignGeneral}\n{foundName}=true;\nbreak;"
            : $"if(!{builderName}.TryGetRange({outerKeyName},out var {startName},out var {endName})) continue;\n{assignGeneral}\n{foundName}=true;\nbreak;";
        var outerBody=$"{this.InnerKeyType} {outerKeyName}=default!;\n{this.FuncAssignStatic(this.outerKey,nameof(this.outerKey),outerKeyName,outerItemName)}\nif(!{hasKeyName}({outerKeyName})) continue;\n{probe}";
        var builderDeclaration=countsOnly
            ? $"var {builderName}=new {this.CountsType}({countName},{this.comparerExpression});"
            : $"var {builderName}=new global::Seamless.Linq.ContiguousLookupBuilder<{this.InnerKeyType},{this.InnerElementType}>({countName},{countName},{this.comparerExpression});";
        var prepare=countsOnly ? string.Empty : $"{builderName}.PrepareProbe();\nvar {arrangedName}={builderName}.Arranged;";
        var dispose=countsOnly ? string.Empty : $"{builderName}.Dispose();";

        return $$"""
            {{localDeclBlockGeneral}}{{destType}} {{destVar}}=default!;
            static bool {{hasKeyName}}<TKey>(TKey key) => key is not null;
            using var {{outerName}}={{outerExpression}};
            using var {{innerName}}={{innerExpression}};
            var {{countName}}={{innerName}}.TryGetNonEnumeratedCount(out var {{countName}}Value) ? {{countName}}Value : 0;
            {{builderDeclaration}}
            try {
            {{this.RenderRootLoop(innerRoot,innerPredicates,innerName,innerItemName,innerBody)}}
            {{prepare}}
            var {{foundName}}=false;
            {{this.RenderRootLoop(outerRoot,outerPredicates,outerName,outerItemName,outerBody)}}
            if(!{{foundName}}){
                {{notFoundBlock}}
            }else{
            }
            } finally {
                {{dispose}}
            }
            """;
    }
    protected override string GetEnumeratorDisposeBody => this.CountsOnly()
        ? $"{this.e}.Dispose();"
        : this.ProbesInnerRange()
        ? $"{this.e}.Dispose(); {this.innerEnumerator}.Dispose();"
        : $"{this.lookup}.Dispose(); {this.e}.Dispose();";
}
