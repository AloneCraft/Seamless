namespace Seamless.FLinq.Linq.Fragments;

internal class SequenceEqual(int 番号,Enumerable source,Enumerable second,string resultType)
    :SequenceEqual_comparer(番号,source,second,"null",resultType);

internal class SequenceEqual_comparer(int 番号,Enumerable source,Enumerable second,string comparer,string resultType):BufferedScalar(番号,resultType) {
    public override string StructMethod => string.Empty;
    protected override string StructTypeParameters => this.StructTypeParametersWithOuter("TEnum1","TEnum2");
    protected override string StructTypeConstraints =>
        $"where TEnum1 : struct, global::Seamless.Linq.IValueEnumerator<{source.ElementType}>\n    where TEnum2 : struct, global::Seamless.Linq.IValueEnumerator<{second.ElementType}>";
    public override string EnumeratorTypeName => this.EnumeratorTypeNameWithOuter(source.EnumeratorTypeName,second.EnumeratorTypeName);
    protected override string StructSources => source.GetEnumeratorStruct+"\n"+second.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}, {second.GetEnumeratorExpression}, {comparer})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum1 source, TEnum2 second, global::System.Collections.Generic.IEqualityComparer<{source.ElementType}>? comparer";
    protected override string ComputeExpression => "Compute(source, second, comparer)";
    public override string? RenderInlineComputation(string destVar,string destType) {
        if(comparer=="null"
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
        return this.RenderComputeInline(destVar,destType,$"{comparer}");
    }
    public override string InlineSupportingMembers => source.GetEnumeratorStruct+"\n"+second.GetEnumeratorStruct;
    // comparer=="null"（呼び出し側が比較子を指定していない）のとき、生成される Compute は
    // IEqualityComparer<T>? 引数を受け取ってはいても実行時には常に null しか渡らない。
    // それにも関わらず comparer 経由で .Equals を呼ぶと、JIT は「T が閉じたジェネリック引数だと
    // わかっている」という情報を活かせず、インターフェース越しの間接呼び出しのままになる
    // （このコード全体に AggressiveOptimization が付いており tiered PGO を通らないため、
    // 実行時の投機的 devirtualization にも期待できない）。
    // comparer を経由せず EqualityComparer<T>.Default.Equals を直接書けば、T は生成時点で
    // 確定した具象型なので JIT が devirtualize できる。参照型・値型を問わず安全（IEquatable<T>
    // 未実装でも EqualityComparer<T>.Default は必ずコンパイルできる）。
    // さらに、要素型が既知のプリミティブ値型なら 2 引数版 MemoryExtensions.SequenceEqual に
    // 乗せてベクトル化 memcmp を使う（IsKnownEquatableSpanType は OrderLoop の
    // CanUseDirectDefaultOrderCompare と同じ型名ホワイトリスト方式）。
    protected override string ComputeMembers => source.CanProvideSpan&&second.CanProvideSpan
        ? (source.ElementType==second.ElementType
            ? (comparer=="null"
                ? (IsKnownEquatableSpanType(source.ElementType)
                    ? $$"""
                        {{AggressiveInliningAttribute}}
                        private static {{resultType}} Compute(TEnum1 source, TEnum2 second, global::System.Collections.Generic.IEqualityComparer<{{source.ElementType}}>? comparer){
                            source.TryGetSpan(out var __spanL);
                            second.TryGetSpan(out var __spanR);
                            return global::System.MemoryExtensions.SequenceEqual(__spanL, __spanR);
                        }
                        """
                    : $$"""
                        {{AggressiveInliningAttribute}}
                        private static {{resultType}} Compute(TEnum1 source, TEnum2 second, global::System.Collections.Generic.IEqualityComparer<{{source.ElementType}}>? comparer){
                            source.TryGetSpan(out var __spanL);
                            second.TryGetSpan(out var __spanR);
                            if(__spanL.Length!=__spanR.Length) return false;
                            var __eq=global::System.Collections.Generic.EqualityComparer<{{source.ElementType}}>.Default;
                            for(int __i=0;(uint)__i<(uint)__spanL.Length;__i++){
                                if(!__eq.Equals(__spanL[__i],__spanR[__i])) return false;
                            }
                            return true;
                        }
                        """)
                : $$"""
                    {{AggressiveInliningAttribute}}
                    private static {{resultType}} Compute(TEnum1 source, TEnum2 second, global::System.Collections.Generic.IEqualityComparer<{{source.ElementType}}>? comparer){
                        source.TryGetSpan(out var __spanL);
                        second.TryGetSpan(out var __spanR);
                        return global::System.MemoryExtensions.SequenceEqual(__spanL, __spanR, comparer);
                    }
                    """)
            : $$"""
                {{AggressiveInliningAttribute}}
                private static {{resultType}} Compute(TEnum1 source, TEnum2 second, global::System.Collections.Generic.IEqualityComparer<{{source.ElementType}}>? comparer){
                    comparer??=global::System.Collections.Generic.EqualityComparer<{{source.ElementType}}>.Default;
                    source.TryGetSpan(out var __spanL);
                    second.TryGetSpan(out var __spanR);
                    if(__spanL.Length!=__spanR.Length) return false;
                    for(int __i=0;(uint)__i<(uint)__spanL.Length;__i++){
                        if(!comparer.Equals(__spanL[__i],({{source.ElementType}})(object)__spanR[__i]!)) return false;
                    }
                    return true;
                }
                """)
        : (comparer=="null"
            ? $$"""
                {{AggressiveInliningAttribute}}
                private static {{resultType}} Compute(TEnum1 source, TEnum2 second, global::System.Collections.Generic.IEqualityComparer<{{source.ElementType}}>? comparer){
                    var __eq=global::System.Collections.Generic.EqualityComparer<{{source.ElementType}}>.Default;
                    while(true){
                        var hasLeft=source.TryGetNext(out var __mn51);
                        var hasRight=second.TryGetNext(out var __mn52);
                        if(hasLeft!=hasRight) return false;
                        if(!hasLeft) return true;
                        if(!__eq.Equals(__mn51,({{source.ElementType}})(object)__mn52!)) return false;
                    }
                }
                """
            : $$"""
                {{AggressiveInliningAttribute}}
                private static {{resultType}} Compute(TEnum1 source, TEnum2 second, global::System.Collections.Generic.IEqualityComparer<{{source.ElementType}}>? comparer){
                    comparer??=global::System.Collections.Generic.EqualityComparer<{{source.ElementType}}>.Default;
                    while(true){
                        var hasLeft=source.TryGetNext(out var __mn51);
                        var hasRight=second.TryGetNext(out var __mn52);
                        if(hasLeft!=hasRight) return false;
                        if(!hasLeft) return true;
                        if(!comparer.Equals(__mn51,({{source.ElementType}})(object)__mn52!)) return false;
                    }
                }
                """);
    protected override string GetEnumeratorDisposeBody => "source.Dispose();\nsecond.Dispose();";

    private static bool IsKnownEquatableSpanType(string typeName) =>
        typeName is "byte" or "sbyte"
            or "short" or "ushort"
            or "int" or "uint"
            or "long" or "ulong"
            or "float" or "double" or "decimal"
            or "char" or "string" or "bool"
            or "global::System.Byte" or "global::System.SByte"
            or "global::System.Int16" or "global::System.UInt16"
            or "global::System.Int32" or "global::System.UInt32"
            or "global::System.Int64" or "global::System.UInt64"
            or "global::System.Single" or "global::System.Double" or "global::System.Decimal"
            or "global::System.Char" or "global::System.String" or "global::System.Boolean";
}
