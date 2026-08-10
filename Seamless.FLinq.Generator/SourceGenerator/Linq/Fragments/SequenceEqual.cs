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

    public override string StructMethod => string.Empty;
    protected override string StructTypeParameters => this.StructTypeParametersWithOuter("TEnum1","TEnum2");
    protected override string StructTypeConstraints =>
        $"where TEnum1 : struct, global::Seamless.Linq.IValueEnumerator<{source.ElementType}>\n    where TEnum2 : struct, global::Seamless.Linq.IValueEnumerator<{second.ElementType}>";
    public override string EnumeratorTypeName => this.EnumeratorTypeNameWithOuter(source.EnumeratorTypeName,second.EnumeratorTypeName);
    protected override string StructSources => source.GetEnumeratorStruct+"\n"+second.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => RenderEnumeratorExpression(this.EnumeratorTypeName,source.GetEnumeratorExpression,second.GetEnumeratorExpression);
    public override string GetEnumeratorPrimaryConstructorParameters => "TEnum1 source,TEnum2 second";
    protected override string ComputeExpression => "Compute(source, second)";
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
        return null;
    }
    public override string InlineSupportingMembers => source.GetEnumeratorStruct+"\n"+second.GetEnumeratorStruct;
    protected override string ComputeMembers => source.CanProvideSpan&&second.CanProvideSpan
        ? (source.ElementType==second.ElementType&&IsVectorizedSequenceEqualElementType(source.ElementType)
            ? $$"""
                {{AggressiveInliningAttribute}}
                private static {{resultType}} Compute(TEnum1 source,TEnum2 second){
                    source.TryGetSpan(out var __spanL);
                    second.TryGetSpan(out var __spanR);
                    return global::System.MemoryExtensions.SequenceEqual(__spanL, __spanR);
                }
                """
            : $$"""
                {{AggressiveInliningAttribute}}
                private static {{resultType}} Compute(TEnum1 source,TEnum2 second){
                    var __eq=global::System.Collections.Generic.EqualityComparer<{{source.ElementType}}>.Default;
                    source.TryGetSpan(out var __spanL);
                    second.TryGetSpan(out var __spanR);
                    if(__spanL.Length!=__spanR.Length) return false;
                    for(int __i=0;(uint)__i<(uint)__spanL.Length;__i++){
                        if(!__eq.Equals(__spanL[__i],({{source.ElementType}})(object)__spanR[__i]!)) return false;
                    }
                    return true;
                }
                """)
        : $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum1 source,TEnum2 second){
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

    public override string StructMethod => string.Empty;
    protected override string StructTypeParameters => this.StructTypeParametersWithOuter("TEnum1","TEnum2");
    protected override string StructTypeConstraints =>
        $"where TEnum1 : struct, global::Seamless.Linq.IValueEnumerator<{source.ElementType}>\n    where TEnum2 : struct, global::Seamless.Linq.IValueEnumerator<{second.ElementType}>";
    public override string EnumeratorTypeName => this.EnumeratorTypeNameWithOuter(source.EnumeratorTypeName,second.EnumeratorTypeName);
    protected override string StructSources => source.GetEnumeratorStruct+"\n"+second.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => RenderEnumeratorExpression(this.EnumeratorTypeName,source.GetEnumeratorExpression,second.GetEnumeratorExpression,comparer);
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
    // comparer=="null" かつ両ソースが span を出せるときは Seamless.Linq.Enumerable.SequenceEqualSpan
    // を呼ぶ。要素型は T のベンチマーク基底クラス等では構文時点で "T" としてしか見えず、
    // 生成器側の型名ホワイトリストでは int 等の具象呼び出しでもベクトル化 memcmp に振り分けられない。
    // SequenceEqualSpan 側で typeof(T) による実行時分岐にしているため（Min/Max/Sum と同じ方式）、
    // 生成器はソース型を知らなくても常にこの1関数を呼べばよい。
    protected override string ComputeMembers => source.CanProvideSpan&&second.CanProvideSpan
        ? (source.ElementType==second.ElementType
            ? (comparer=="null"
                ? $$"""
                    {{AggressiveInliningAttribute}}
                    private static {{resultType}} Compute(TEnum1 source, TEnum2 second, global::System.Collections.Generic.IEqualityComparer<{{source.ElementType}}>? comparer){
                        source.TryGetSpan(out var __spanL);
                        second.TryGetSpan(out var __spanR);
                        return global::Seamless.Linq.Enumerable.SequenceEqualSpan(__spanL, __spanR);
                    }
                    """
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
}
