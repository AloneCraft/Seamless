namespace Seamless.FLinq;

internal class SequenceEqual(int number,Enumerable source,Enumerable second,string resultType):BufferedScalar(number,resultType) {
    // Explicit Fragment child visitor
    protected override IEnumerable<Fragment> GetChildFragments() {
        foreach(var child in base.GetChildFragments()) {
                yield return child;
        }
        if(source is { } __visitorChild_source) {
            yield return __visitorChild_source;
        }
        if(second is { } __visitorChild_second) {
            yield return __visitorChild_second;
        }
    }

    private static bool IsVectorizedSequenceEqualElementType(string elementType) => elementType is
        "bool" or "byte" or "sbyte" or "short" or "ushort" or "int" or "uint" or "long" or "ulong" or "nint" or "nuint" or "char"
        or "global::System.Boolean" or "global::System.Byte" or "global::System.SByte" or "global::System.Int16" or "global::System.UInt16"
        or "global::System.Int32" or "global::System.UInt32" or "global::System.Int64" or "global::System.UInt64" or "global::System.IntPtr" or "global::System.UIntPtr" or "global::System.Char";

    private string SpanEqualityExpression => source.ElementType==second.ElementType&&source.CanProvideSpan&&second.CanProvideSpan
        ? "global::System.MemoryExtensions.SequenceEqual(__spanL,__spanR)"
        : "global::Seamless.Linq.Enumerable.SequenceEqualSpan(__spanL,__spanR)";

    public override string StructMethod => string.Empty;
    protected override string StructTypeParameters => this.StructTypeParametersWithOuter("TEnum1","TEnum2");
    protected override string StructTypeConstraints =>
        $"where TEnum1 : struct, global::Seamless.Linq.IValueEnumerator<{source.ElementType}>\n    where TEnum2 : struct, global::Seamless.Linq.IValueEnumerator<{second.ElementType}>";
    public override string EnumeratorTypeName => this.EnumeratorTypeNameWithOuter(source.EnumeratorTypeName,second.EnumeratorTypeName);
    protected override string StructSources => source.GetEnumeratorStruct+"\n"+second.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => RenderEnumeratorExpression(this.EnumeratorTypeName,source.GetEnumeratorExpression,second.GetEnumeratorExpression);
    public override string GetEnumeratorPrimaryConstructorParameters => "TEnum1 source,TEnum2 second";
    protected override string ComputeExpression => "Compute(source, second)";
    private bool CanInlineCompute {
        get {
            if(source is RangeRoot&&second is RangeRoot) {
                return false;
            }
            try {
                _=source.GetEnumeratorExpression;
                _=second.GetEnumeratorExpression;
                return true;
            } catch(NotSupportedException) {
                return false;
            }
        }
    }

    public override string? RenderInlineComputation(string destVar,string destType) {
        if(source is RangeRoot leftRange
            &&second is RangeRoot rightRange
            &&source.ElementType=="int"
            &&second.ElementType=="int") {
            return $$"""
                {{destType}} {{destVar}};
                {
                    var __startL{{number}}={{leftRange.StartExpression}};
                    var __countL{{number}}={{leftRange.CountExpression}};
                    var __startR{{number}}={{rightRange.StartExpression}};
                    var __countR{{number}}={{rightRange.CountExpression}};
                    {{destVar}}=__startL{{number}}==__startR{{number}} && __countL{{number}}==__countR{{number}};
                }
                """;
        }

        if(!this.CanInlineCompute) {
            return null;
        }

        var sourceExpression=source.GetEnumeratorExpression;
        var secondExpression=second.GetEnumeratorExpression;
        var sourceVariable=$"__sl{number}";
        var secondVariable=$"__sr{number}";
        var scalarType=this.EnumeratorTypeNameWithOuter(source.EnumeratorTypeName,second.EnumeratorTypeName);
        return $$"""
            {{destType}} {{destVar}};
            using(var {{sourceVariable}}={{sourceExpression}})
            using(var {{secondVariable}}={{secondExpression}}){
                {{destVar}}={{scalarType}}.Compute({{sourceVariable}},{{secondVariable}});
            }
            """;
    }
    public override string InlineSupportingMembers => this.CanInlineCompute
        ? $$"""
            {{this.StructSources}}
            private struct {{this.Struct}}{{this.StructTypeParameters}}
                {{this.StructTypeConstraints}} {
            {{IndentLines(this.ComputeMembers.Replace("private static","internal static"),"    ")}}
            }
            """
        : source.GetEnumeratorStruct+"\n"+second.GetEnumeratorStruct;
    // TryGetSpan は静的に CanProvideSpan=false な source(型消去された IEnumerable<T> 等)でも
    // 実行時に T[]/List<T> なら成功する(Root.cs の GetEnumerableInterfaceStruct 参照)。
    // 以前は source.CanProvideSpan&&second.CanProvideSpan という静的判定だけで span 経路の
    // 生成有無を分けていたため、型消去された List<T> 等はこの実行時 fast path を一切使えず
    // 常に TryGetNext の逐次比較へ落ちていた(実測: List<int> 4096要素で約150倍遅い)。
    // TryGetSpan は失敗しても列挙位置を進めない(非破壊)ため、成功時に span 経路、
    // 失敗時に TryGetNext 経路へフォールバックする形へ常時統一できる。
    // 静的に CanProvideSpan=true な source(ArrayRoot/ListRoot 等)では TryGetSpan は常に
    // true を返す実装なので、JIT がこの分岐を定数畳み込みでき、追加コストは生じない。
    protected override string ComputeMembers => source.ElementType==second.ElementType&&IsVectorizedSequenceEqualElementType(source.ElementType)
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum1 source,TEnum2 second){
                if(source.TryGetSpan(out var __spanL)&&second.TryGetSpan(out var __spanR)){
                    return global::System.MemoryExtensions.SequenceEqual(__spanL, __spanR);
                }
                var __eq=global::System.Collections.Generic.EqualityComparer<{{source.ElementType}}>.Default;
                while(true){
                    var hasLeft=source.TryGetNext(out var __mn51);
                    var hasRight=second.TryGetNext(out var __mn52);
                    if(hasLeft!=hasRight) return false;
                    if(!hasLeft) return true;
                    if(!__eq.Equals(__mn51,__mn52)) return false;
                }
            }
            """
        : $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum1 source,TEnum2 second){
                if(source.TryGetSpan(out var __spanL)&&second.TryGetSpan(out var __spanR)){
                    return {{this.SpanEqualityExpression}};
                }
                var __eq=global::System.Collections.Generic.EqualityComparer<{{source.ElementType}}>.Default;
                while(true){
                    var hasLeft=source.TryGetNext(out var __mn51);
                    var hasRight=second.TryGetNext(out var __mn52);
                    if(hasLeft!=hasRight) return false;
                    if(!hasLeft) return true;
                    if(!__eq.Equals(__mn51,({{source.ElementType}})(object)__mn52!)) return false;
                }
            }
            """;
    protected override string GetEnumeratorDisposeBody => "source.Dispose();\nsecond.Dispose();";
}
internal class SequenceEqual_comparer(int 番号,Enumerable source,Enumerable second,string comparer,string resultType):BufferedScalar(番号,resultType) {
    // Explicit Fragment child visitor
    protected override IEnumerable<Fragment> GetChildFragments() {
        foreach(var child in base.GetChildFragments()) {
                yield return child;
        }
        if(source is { } __visitorChild_source) {
            yield return __visitorChild_source;
        }
        if(second is { } __visitorChild_second) {
            yield return __visitorChild_second;
        }
    }

    private string SpanEqualityExpression => source.ElementType==second.ElementType&&source.CanProvideSpan&&second.CanProvideSpan
        ? "global::System.MemoryExtensions.SequenceEqual(__spanL,__spanR)"
        : "global::Seamless.Linq.Enumerable.SequenceEqualSpan(__spanL,__spanR)";

    public override string StructMethod => string.Empty;
    protected override string StructTypeParameters => this.StructTypeParametersWithOuter("TEnum1","TEnum2");
    protected override string StructTypeConstraints =>
        $"where TEnum1 : struct, global::Seamless.Linq.IValueEnumerator<{source.ElementType}>\n    where TEnum2 : struct, global::Seamless.Linq.IValueEnumerator<{second.ElementType}>";
    public override string EnumeratorTypeName => this.EnumeratorTypeNameWithOuter(source.EnumeratorTypeName,second.EnumeratorTypeName);
    protected override string StructSources => source.GetEnumeratorStruct+"\n"+second.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => RenderEnumeratorExpression(this.EnumeratorTypeName,source.GetEnumeratorExpression,second.GetEnumeratorExpression,comparer);
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum1 source, TEnum2 second, global::System.Collections.Generic.IEqualityComparer<{source.ElementType}>? comparer";
    protected override string ComputeExpression => "Compute(source, second, comparer)";
    private bool CanInlineCompute {
        get {
            if(source is RangeRoot&&second is RangeRoot) {
                return false;
            }
            try {
                _=source.GetEnumeratorExpression;
                _=second.GetEnumeratorExpression;
                return true;
            } catch(NotSupportedException) {
                return false;
            }
        }
    }
    public override string? RenderInlineComputation(string destVar,string destType) {
        if(ComparerExpressionSupport.IsDefaultEqualityComparer(comparer,source.ElementType)
            &&source is RangeRoot leftRange
            &&second is RangeRoot rightRange
            &&source.ElementType=="int"
            &&second.ElementType=="int") {
            return $$"""
                {{destType}} {{destVar}};
                {
                    var __startL{{番号}}={{leftRange.StartExpression}};
                    var __countL{{番号}}={{leftRange.CountExpression}};
                    var __startR{{番号}}={{rightRange.StartExpression}};
                    var __countR{{番号}}={{rightRange.CountExpression}};
                    {{destVar}}=__startL{{番号}}==__startR{{番号}} && __countL{{番号}}==__countR{{番号}};
                }
                """;
        }

        if(!this.CanInlineCompute) {
            return null;
        }

        var sourceExpression=source.GetEnumeratorExpression;
        var secondExpression=second.GetEnumeratorExpression;
        var sourceVariable=$"__sl{番号}";
        var secondVariable=$"__sr{番号}";
        var scalarType=this.EnumeratorTypeNameWithOuter(source.EnumeratorTypeName,second.EnumeratorTypeName);
        return $$"""
            {{destType}} {{destVar}};
            using(var {{sourceVariable}}={{sourceExpression}})
            using(var {{secondVariable}}={{secondExpression}}){
                {{destVar}}={{scalarType}}.Compute({{sourceVariable}},{{secondVariable}},{{comparer}});
            }
            """;
    }
    public override string InlineSupportingMembers => this.CanInlineCompute
        ? $$"""
            {{this.StructSources}}
            private struct {{this.Struct}}{{this.StructTypeParameters}}
                {{this.StructTypeConstraints}} {
            {{IndentLines(this.ComputeMembers.Replace("private static","internal static"),"    ")}}
            }
            """
        : source.GetEnumeratorStruct+"\n"+second.GetEnumeratorStruct;
    // comparer が null または EqualityComparer<T>.Default で、両ソースが span を出せるときは Seamless.Linq.Enumerable.SequenceEqualSpan
    // を呼ぶ。要素型は T のベンチマーク基底クラス等では構文時点で "T" としてしか見えず、
    // 生成器側の型名ホワイトリストでは int 等の具象呼び出しでもベクトル化 memcmp に振り分けられない。
    // SequenceEqualSpan 側で typeof(T) による実行時分岐にしているため（Min/Max/Sum と同じ方式）、
    // 生成器はソース型を知らなくても常にこの1関数を呼べばよい。
    // TryGetSpan は静的に CanProvideSpan=false な source(型消去された IEnumerable<T> 等)でも
    // 実行時に T[]/List<T> なら成功する(Root.cs の GetEnumerableInterfaceStruct 参照)。
    // 以前は source.CanProvideSpan&&second.CanProvideSpan という静的判定だけで span 経路の
    // 生成有無を分けていたため、型消去された List<T> 等はこの実行時 fast path を一切使えず
    // 常に TryGetNext の逐次比較へ落ちていた(実測: List<int> 4096要素で約150倍遅い)。
    // TryGetSpan は失敗しても列挙位置を進めない(非破壊)ため、成功時に span 経路、
    // 失敗時に TryGetNext 経路へフォールバックする形へ常時統一できる。
    // 静的に CanProvideSpan=true な source(ArrayRoot/ListRoot 等)では TryGetSpan は常に
    // true を返す実装なので、JIT がこの分岐を定数畳み込みでき、追加コストは生じない。
    protected override string ComputeMembers => source.ElementType==second.ElementType
        ? (ComparerExpressionSupport.IsDefaultEqualityComparer(comparer,source.ElementType)
            ? $$"""
                {{AggressiveInliningAttribute}}
                private static {{resultType}} Compute(TEnum1 source, TEnum2 second, global::System.Collections.Generic.IEqualityComparer<{{source.ElementType}}>? comparer){
                    if(source.TryGetSpan(out var __spanL)&&second.TryGetSpan(out var __spanR)){
                        return {{this.SpanEqualityExpression}};
                    }
                    var __eq=global::System.Collections.Generic.EqualityComparer<{{source.ElementType}}>.Default;
                    while(true){
                        var hasLeft=source.TryGetNext(out var __mn51);
                        var hasRight=second.TryGetNext(out var __mn52);
                        if(hasLeft!=hasRight) return false;
                        if(!hasLeft) return true;
                        if(!__eq.Equals(__mn51,__mn52)) return false;
                    }
                }
                """
            // comparer 明示時は MemoryExtensions.SequenceEqual(span,span,comparer) に委譲しない。
            // 実測で同一データに対し手書きループの2.6倍遅いことを確認済み(ClassInt32、4096要素:
            // 委譲32.7µs対して手書き12.4µs)。下の要素型不一致分岐と同じ手書きループに揃える。
            : $$"""
                {{AggressiveInliningAttribute}}
                private static {{resultType}} Compute(TEnum1 source, TEnum2 second, global::System.Collections.Generic.IEqualityComparer<{{source.ElementType}}>? comparer){
                    comparer??=global::System.Collections.Generic.EqualityComparer<{{source.ElementType}}>.Default;
                    if(source.TryGetSpan(out var __spanL)&&second.TryGetSpan(out var __spanR)){
                        if(__spanL.Length!=__spanR.Length) return false;
                        for(int __i=0;(uint)__i<(uint)__spanL.Length;__i++){
                            if(!comparer.Equals(__spanL[__i],__spanR[__i])) return false;
                        }
                        return true;
                    }
                    while(true){
                        var hasLeft=source.TryGetNext(out var __mn51);
                        var hasRight=second.TryGetNext(out var __mn52);
                        if(hasLeft!=hasRight) return false;
                        if(!hasLeft) return true;
                        if(!comparer.Equals(__mn51,__mn52)) return false;
                    }
                }
                """)
        : $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum1 source, TEnum2 second, global::System.Collections.Generic.IEqualityComparer<{{source.ElementType}}>? comparer){
                comparer??=global::System.Collections.Generic.EqualityComparer<{{source.ElementType}}>.Default;
                if(source.TryGetSpan(out var __spanL)&&second.TryGetSpan(out var __spanR)){
                    if(__spanL.Length!=__spanR.Length) return false;
                    for(int __i=0;(uint)__i<(uint)__spanL.Length;__i++){
                        if(!comparer.Equals(__spanL[__i],({{source.ElementType}})(object)__spanR[__i]!)) return false;
                    }
                    return true;
                }
                while(true){
                    var hasLeft=source.TryGetNext(out var __mn51);
                    var hasRight=second.TryGetNext(out var __mn52);
                    if(hasLeft!=hasRight) return false;
                    if(!hasLeft) return true;
                    if(!comparer.Equals(__mn51,({{source.ElementType}})(object)__mn52!)) return false;
                }
            }
            """;
    protected override string GetEnumeratorDisposeBody => "source.Dispose();\nsecond.Dispose();";
}
