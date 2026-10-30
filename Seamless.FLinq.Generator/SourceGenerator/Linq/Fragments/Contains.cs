namespace Seamless.FLinq.Generator;

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
    protected override string ComputeExpression => $"Compute(ref {this.e}, value)";
    public override string? RenderInlineComputation(string destVar,string destType) =>
        this.RenderRangeContainsInline(destVar,destType)
        ??this.RenderDirectSpanContainsInline(destVar,destType)
        ??this.RenderConsumeBasedContainsInline(destVar,destType)
        ??this.RenderProviderNativeContainsInline(destVar,destType)
        ??this.RenderGenericCollectionContainsInline(destVar,destType)
        ??this.RenderComputeInline(destVar,destType,value);

    private string? RenderConsumeBasedContainsInline(string destVar,string destType) {
        if(source is not (Join or Zip or Zip_resultSelector or GroupJoin or SelectManyBase)) {
            return null;
        }
        var placeholder = $"__FLINQ_CONTAINS_{番号}__";
        string consumeStatement;
        try {
            consumeStatement=source.GetConsumeExecutionStatement(placeholder);
        } catch(NotSupportedException) {
            return null;
        }
        if(string.IsNullOrWhiteSpace(consumeStatement)
            ||consumeStatement.Contains("TryGetNext")
            ||consumeStatement.Contains("GetRemainingEnumerator")
            ||!consumeStatement.Contains($"{placeholder}.Consume(")) {
            return null;
        }
        var endLabel = $"__containsEnd{番号}";
        var escaped = global::System.Text.RegularExpressions.Regex.Escape(placeholder);
        var pattern = $@"(?<indent>[ \t]*){escaped}\.Consume\((?<arg>.+?)\);";
        var replaced = global::System.Text.RegularExpressions.Regex.Replace(consumeStatement,pattern,match => {
            var indent = match.Groups["indent"].Value;
            var arg = match.Groups["arg"].Value;
            return $"{indent}if(global::System.Collections.Generic.EqualityComparer<{source.ElementType}>.Default.Equals({arg}, {value})) {{\n{indent}    {destVar}=true;\n{indent}    goto {endLabel};\n{indent}}}";
        });
        return $$"""
            {{destType}} {{destVar}}=false;
            {{replaced}}
            {{endLabel}}:;
            """ ;
    }
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
        var arrayVar = $"__genericArray{番号}";
        var listVar = $"__genericList{番号}";
        var collectionVar = $"__collection{番号}";
        var readOnlySetVar = $"__readOnlySet{番号}";
        var enumeratorVar = $"__genericEnum{番号}";
        var itemVar = $"__genericItem{番号}";
        var targetVar = $"__genericValue{番号}";
        // RenderInlineComputation が成功する経路では ComputeMembers(別スコープの Struct0.Compute)は
        // 生成されないため、else 節のフォールバックもここで直接インライン化する必要がある
        // (Compute という名前を呼んでも解決できない)。
        // enumerableExpression(=元の source 式)は副作用や非冪等性があり得るため、
        // is 判定と else 節の enumerator 構築の両方で使う前に必ず1回だけローカル変数へ評価する。
        // 静的型が IEnumerable<T> までしか分からなくても、実行時に T[]/List<T> なら
        // ContainsSpan(SIMD対応)へ振る。これを ICollection<T> より先に判定しないと、
        // List<T> は ICollection<T> を実装するため非ベクトル化の List<T>.Contains(内部は
        // 単純スカラーループ)に流れてしまい、配列/List直参照時と同じ高速化を取りこぼす
        // (実測: List<int> 4096要素で ContainsSpan 経由に対し約8倍遅い)。
        // ICollection<T> を実装しない読み取り専用集合(例: Seamless.Collections.Persisted.Set<T> は
        // Seamless.Collections.ISet<T> 経由で System.Collections.Generic.IReadOnlySet<T> のみを実装し、
        // O(1) の Contains(T) を持つ)を取りこぼさないよう、ICollection<T> の次に判定する。
        return $$"""
            {{destType}} {{destVar}};
            var {{sourceVar}}={{enumerableExpression}};
            var {{targetVar}}={{value}};
            global::System.ArgumentNullException.ThrowIfNull({{sourceVar}});
            if({{sourceVar}} is {{source.ElementType}}[] {{arrayVar}}){
                {{destVar}}=global::Seamless.Linq.Enumerable.ContainsSpan(global::System.MemoryExtensions.AsSpan({{arrayVar}}),{{targetVar}});
            }else if({{sourceVar}} is global::System.Collections.Generic.List<{{source.ElementType}}> {{listVar}}){
                {{destVar}}=global::Seamless.Linq.Enumerable.ContainsSpan(global::System.Runtime.InteropServices.CollectionsMarshal.AsSpan({{listVar}}),{{targetVar}});
            }else if({{sourceVar}} is global::System.Collections.Generic.ICollection<{{source.ElementType}}> {{collectionVar}}){
                {{destVar}}={{collectionVar}}.Contains({{targetVar}});
            }else if({{sourceVar}} is global::System.Collections.Generic.IReadOnlySet<{{source.ElementType}}> {{readOnlySetVar}}){
                {{destVar}}={{readOnlySetVar}}.Contains({{targetVar}});
            }else{
                var comparer=global::System.Collections.Generic.EqualityComparer<{{source.ElementType}}>.Default;
                {{destVar}}=false;
                using(global::System.Collections.Generic.IEnumerator<{{source.ElementType}}> {{enumeratorVar}}={{sourceVar}}.GetEnumerator()){
                    while({{enumeratorVar}}.MoveNext()){
                        var {{itemVar}}={{enumeratorVar}}.Current;
                        if(comparer.Equals({{itemVar}},{{targetVar}})){
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
        return this.RenderSpanInline(source,destVar,destType,span,spanName =>
            $"{destVar}=global::Seamless.Linq.Enumerable.ContainsSpan({spanName},{value});");
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
                {{range.RenderArgumentPreparation(start,count)}}
                var {{target}}={{value}};
                {{destVar}}=(uint)({{target}} - {{start}}) < (uint){{count}};
            }
            """;
    }
    // TryGetSpan は静的に CanProvideSpan=false な source(型消去された IEnumerable<T> 等)でも
    // 実行時に T[]/List<T> なら成功する(Root.cs の GetEnumerableInterfaceStruct 参照)。
    // ArrayRoot/ListRoot 等 CanProvideSpan=true な source では TryGetSpan は常に true を返す
    // 実装なので、JIT がこの分岐を定数畳み込みでき、追加コストは生じない。
    protected override string ComputeMembers => $$"""
        {{AggressiveInliningAttribute}}
        private static {{resultType}} Compute(ref TEnum source, {{source.ElementType}} value){
            if(source.TryGetSpan(out var __span)){
                return global::Seamless.Linq.Enumerable.ContainsSpan(__span,value);
            }
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
    protected override string ComputeExpression => $"Compute(ref {this.e}, value, comparer)";
    public override string? RenderInlineComputation(string destVar,string destType) =>
        this.RenderDirectSpanContainsComparerInline(destVar,destType)
        ??this.RenderConsumeBasedContainsComparerInline(destVar,destType)
        ??this.RenderComputeInline(destVar,destType,value,comparer);

    private string? RenderConsumeBasedContainsComparerInline(string destVar,string destType) {
        if(source is not (Join or Zip or Zip_resultSelector or GroupJoin or SelectManyBase)) {
            return null;
        }
        var placeholder = $"__FLINQ_CONTAINS_CMP_{番号}__";
        string consumeStatement;
        try {
            consumeStatement=source.GetConsumeExecutionStatement(placeholder);
        } catch(NotSupportedException) {
            return null;
        }
        if(string.IsNullOrWhiteSpace(consumeStatement)
            ||consumeStatement.Contains("TryGetNext")
            ||consumeStatement.Contains("GetRemainingEnumerator")
            ||!consumeStatement.Contains($"{placeholder}.Consume(")) {
            return null;
        }
        var endLabel = $"__containsCmpEnd{番号}";
        var escaped = global::System.Text.RegularExpressions.Regex.Escape(placeholder);
        var pattern = $@"(?<indent>[ \t]*){escaped}\.Consume\((?<arg>.+?)\);";
        string comparerExpr;
        if(comparer=="null") {
            comparerExpr=$"global::System.Collections.Generic.EqualityComparer<{source.ElementType}>.Default";
        } else {
            comparerExpr=$"({comparer}??global::System.Collections.Generic.EqualityComparer<{source.ElementType}>.Default)";
        }
        var replaced = global::System.Text.RegularExpressions.Regex.Replace(consumeStatement,pattern,match => {
            var indent = match.Groups["indent"].Value;
            var arg = match.Groups["arg"].Value;
            return $"{indent}if({comparerExpr}.Equals({arg}, {value})) {{\n{indent}    {destVar}=true;\n{indent}    goto {endLabel};\n{indent}}}";
        });
        return $$"""
            {{destType}} {{destVar}}=false;
            {{replaced}}
            {{endLabel}}:;
            """ ;
    }
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
    protected override string ComputeMembers => $$"""
        {{AggressiveInliningAttribute}}
        private static {{resultType}} Compute(ref TEnum source, {{source.ElementType}} value, global::System.Collections.Generic.IEqualityComparer<{{source.ElementType}}>? comparer){
            comparer??=global::System.Collections.Generic.EqualityComparer<{{source.ElementType}}>.Default;
            if(source.TryGetSpan(out var __span)){
                foreach(var item in __span){
                    if(comparer.Equals(item,value)) return true;
                }
                return false;
            }
            while(source.TryGetNext(out var __mn17)){
                if(comparer.Equals(__mn17,value)) return true;
            }
            return false;
        }
        """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}
