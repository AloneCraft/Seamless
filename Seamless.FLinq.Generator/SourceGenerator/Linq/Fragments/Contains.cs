namespace Seamless.FLinq;

internal class Contains(int 番号,Enumerable source,string value,string resultType):BufferedScalar(番号,resultType) {
    // Explicit Fragment child visitor
    protected override IEnumerable<Fragment> GetChildFragments() {
        foreach(var child in base.GetChildFragments()) {
                yield return child;
        }
        if(source is { } __visitorChild_source) {
            yield return __visitorChild_source;
        }
    }

    public static bool IsApplicableVariant(IMethodSymbol method) =>
        method.Parameters.Length==1;
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => RenderEnumeratorExpression(this.EnumeratorTypeName,source.GetEnumeratorExpression,value);
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}, {source.ElementType} value";
    protected override string ComputeExpression => $"Compute({this.e}, value)";
    public override string? RenderInlineComputation(string destVar,string destType) =>
        this.RenderRangeContainsInline(destVar,destType)
        ??this.RenderDirectSpanContainsInline(destVar,destType)
        ??this.RenderProviderNativeContainsInline(destVar,destType)
        ??this.RenderGenericCollectionContainsInline(destVar,destType)
        ??this.RenderComputeInline(destVar,destType,value);
    // Persisted/Tsavorite の根(ProviderNativeRootBase)は mmap ハッシュ索引で Contains が O(1)
    // (PersistedSetRoot.cs 参照: "Set<T>.Contains は .hindex で O(1)。native scan の O(n) を回避する")。
    // 標準の Compute 経路は TryGetNext ベースの O(n) 線形走査で、要素ごとに逆シリアライズの実コストを
    // 払うため(Count/ToHashSet と同根)、この O(1) ルックアップを直接埋め込む。
    // comparer 指定がある場合は集合自身の等価とは一致しない可能性があるため対象外
    // (Contains_comparer クラスはこの分岐を持たず、常に Compute 経路)。
    private string? RenderProviderNativeContainsInline(string destVar,string destType) {
        if(source is not ProviderNativeRootBase nativeRoot) {
            return null;
        }
        if(nativeRoot.NativeContainsExpression(value) is not { } lookupExpression) {
            return null;
        }
        return $"{destType} {destVar}={lookupExpression};";
    }
    // 静的型が IEnumerable<T> までしか分からない場合(Root, useEnumerableInterface=true)、
    // System.Linq.Enumerable.Contains(BCL) は実行時に source is ICollection<T> を判定して
    // O(1) の collection.Contains(value) を使う。TryGetNonEnumeratedCount/TryGetSpan(Root.cs)は
    // 既に同じ実行時判定パターンを持つが、Contains の Compute 経路だけ持っておらず常に O(n) の
    // 手動走査になっていた。BCL と同じ判定をここで追加する。
    // comparer 指定時は対象外(RenderProviderNativeContainsInline と同じ理由。Contains_comparer は
    // この分岐を持たない)。中間演算子を挟む場合(source が Root 以外)も対象外
    // (ICollection<T> の中身が Contains の入力と一致しなくなるため)。
    private string? RenderGenericCollectionContainsInline(string destVar,string destType) {
        if(source is not Root { DirectEnumerableExpression: { } enumerableExpression }) {
            return null;
        }
        var sourceVar = $"__genericSource{番号}";
        var collectionVar = $"__collection{番号}";
        var enumeratorVar = $"__genericEnum{番号}";
        var itemVar = $"__genericItem{番号}";
        // RenderInlineComputation が成功する経路では ComputeMembers(別スコープの Struct0.Compute)は
        // 生成されないため、else 節のフォールバックもここで直接インライン化する必要がある
        // (Compute という名前を呼んでも解決できない)。
        // enumerableExpression(=元の source 式)は副作用や非冪等性があり得るため、
        // is 判定と else 節の enumerator 構築の両方で使う前に必ず1回だけローカル変数へ評価する。
        return $$"""
            {{destType}} {{destVar}};
            var {{sourceVar}}={{enumerableExpression}};
            if({{sourceVar}} is global::System.Collections.Generic.ICollection<{{source.ElementType}}> {{collectionVar}}){
                {{destVar}}={{collectionVar}}.Contains({{value}});
            }else{
                var comparer=global::System.Collections.Generic.EqualityComparer<{{source.ElementType}}>.Default;
                {{destVar}}=false;
                using(var {{enumeratorVar}}=new {{source.EnumeratorTypeName}}({{sourceVar}})){
                    while({{enumeratorVar}}.TryGetNext(out var {{itemVar}})){
                        if(comparer.Equals({{itemVar}},{{value}})){
                            {{destVar}}=true;
                            break;
                        }
                    }
                }
            }
            """;
    }
    private string? RenderDirectSpanContainsInline(string destVar,string destType) {
        var span = $"__span{this.result}";
        return this.RenderSpanInline(source,destVar,destType,span,spanName => $$"""
            var comparer=global::System.Collections.Generic.EqualityComparer<{{source.ElementType}}>.Default;
            var __value={{value}};
            {{destVar}}=false;
            foreach(var item in {{spanName}}){
                if(comparer.Equals(item,__value)){
                    {{destVar}}=true;
                    break;
                }
            }
            """);
    }
    private string? RenderRangeContainsInline(string destVar,string destType) {
        if(source is not RangeRoot range||source.ElementType!="int") {
            return null;
        }
        var start = $"__start{番号}";
        var count = $"__count{番号}";
        var target = $"__value{番号}";
        return $$"""
            {{destType}} {{destVar}};
            {
                var {{start}}={{range.StartExpression}};
                var {{count}}={{range.CountExpression}};
                var {{target}}={{value}};
                {{destVar}}=(uint)({{target}} - {{start}}) < (uint){{count}};
            }
            """;
    }
    protected override string ComputeMembers => source.CanProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source, {{source.ElementType}} value){
                var comparer=global::System.Collections.Generic.EqualityComparer<{{source.ElementType}}>.Default;
                source.TryGetSpan(out var __span);
                foreach(var item in __span){
                    if(comparer.Equals(item,value)) return true;
                }
                return false;
            }
            """
        : $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source, {{source.ElementType}} value){
                var comparer=global::System.Collections.Generic.EqualityComparer<{{source.ElementType}}>.Default;
                while(source.TryGetNext(out var __mn16)){
                    if(comparer.Equals(__mn16,value)) return true;
                }
                return false;
            }
            """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}

internal class Contains_comparer(int 番号,Enumerable source,string value,string comparer,string resultType):BufferedScalar(番号,resultType) {
    // Explicit Fragment child visitor
    protected override IEnumerable<Fragment> GetChildFragments() {
        foreach(var child in base.GetChildFragments()) {
                yield return child;
        }
        if(source is { } __visitorChild_source) {
            yield return __visitorChild_source;
        }
    }

    public static bool IsApplicableVariant(IMethodSymbol method) =>
        method.Parameters.Length==2;
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => RenderEnumeratorExpression(this.EnumeratorTypeName,source.GetEnumeratorExpression,value,comparer);
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}, {source.ElementType} value, global::System.Collections.Generic.IEqualityComparer<{source.ElementType}>? comparer";
    protected override string ComputeExpression => $"Compute({this.e}, value, comparer)";
    public override string? RenderInlineComputation(string destVar,string destType) =>
        this.RenderDirectSpanContainsComparerInline(destVar,destType)
        ??this.RenderComputeInline(destVar,destType,value,comparer);
    // comparer=="null"(呼び出し側が実質比較子なし。無引数 Contains が MemoryExtensions の
    // 比較子省略可能な3引数オーバーロードへ解決されて Contains_comparer 側に来るケースを含む)のときは
    // ContainsSpan(typeof(T) 実行時分岐で既知プリミティブ型なら MemoryExtensions.Contains の
    // ベクトル化探索へ振る、SequenceEqualSpan と同型)を使う。明示的な comparer があるときは
    // 従来どおり要素ごとループ。
    private string? RenderDirectSpanContainsComparerInline(string destVar,string destType) {
        var span = $"__span{this.result}";
        if(comparer=="null") {
            return this.RenderSpanInline(source,destVar,destType,span,spanName =>
                $"{destVar}=global::Seamless.Linq.Enumerable.ContainsSpan({spanName},{value});");
        }
        return this.RenderSpanInline(source,destVar,destType,span,spanName => $$"""
            global::System.Collections.Generic.IEqualityComparer<{{source.ElementType}}>? __comparer={{comparer}};
            __comparer??=global::System.Collections.Generic.EqualityComparer<{{source.ElementType}}>.Default;
            var __value={{value}};
            {{destVar}}=false;
            foreach(var item in {{spanName}}){
                if(__comparer.Equals(item,__value)){
                    {{destVar}}=true;
                    break;
                }
            }
            """);
    }
    protected override string ComputeMembers => source.CanProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source, {{source.ElementType}} value, global::System.Collections.Generic.IEqualityComparer<{{source.ElementType}}>? comparer){
                comparer??=global::System.Collections.Generic.EqualityComparer<{{source.ElementType}}>.Default;
                source.TryGetSpan(out var __span);
                foreach(var item in __span){
                    if(comparer.Equals(item,value)) return true;
                }
                return false;
            }
            """
        : $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source, {{source.ElementType}} value, global::System.Collections.Generic.IEqualityComparer<{{source.ElementType}}>? comparer){
                comparer??=global::System.Collections.Generic.EqualityComparer<{{source.ElementType}}>.Default;
                while(source.TryGetNext(out var __mn17)){
                    if(comparer.Equals(__mn17,value)) return true;
                }
                return false;
            }
            """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}
