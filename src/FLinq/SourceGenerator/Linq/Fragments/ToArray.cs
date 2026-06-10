namespace Tsavorite.SourceGenerator.Linq.Fragments;

public class ToArray(int 番号, Enumerable source, string resultType) : BufferedScalar(番号, resultType) {
    // スタック上で受ける InlineArray の要素数。これ以内なら ArrayPool に一切触れない。
    private const int InlineCapacity = 16;
    protected override Enumerable PrimarySource => source;
    // InlineArray バッファ型を補助 struct として出力する。
    // {Struct}_Buf 命名は補助クラス退避の正規表現（Struct\d+_接尾辞）にマッチしリネーム衝突を防ぐ。
    public override string StructMethod => $$"""
        [global::System.Runtime.CompilerServices.InlineArray({{InlineCapacity}})]
        private struct {{this.Struct}}_Buf {
            private {{source.ElementType}} _element0;
        }
        """;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}";
    protected override string ComputeExpression => $"Compute({this.e})";
    protected override string ComputeMembers => $$"""
        {{AggressiveInliningAttribute}}
        private static {{resultType}} Compute(TEnum source) {
            using var enumerator = source;
            // 要素数が InlineCapacity 以内ならスタック上の InlineArray だけで受け、ArrayPool/List の
            // ヒープ確保もスレッドセーフな Rent/Return も一切回避する。溢れたときだけ ArrayPool へスピルし、
            // 最後に正確なサイズの配列へ1回だけコピーする。MoveNext/Current のみで TEnum 制約に依存しない。
            {{this.Struct}}_Buf __inline = default;
            global::System.Span<{{source.ElementType}}> __stack = __inline;
            {{source.ElementType}}[]? __heap = null;
            var n = 0;
            try {
                while(enumerator.MoveNext()) {
                    if(__heap is null) {
                        if(n < {{InlineCapacity}}) {
                            __stack[n++] = enumerator.Current;
                            continue;
                        }
                        // スタックバッファが満杯になったので ArrayPool へ移行する。
                        __heap = global::System.Buffers.ArrayPool<{{source.ElementType}}>.Shared.Rent({{InlineCapacity}} * 2);
                        __stack.CopyTo(__heap);
                    } else if(n == __heap.Length) {
                        var __next = global::System.Buffers.ArrayPool<{{source.ElementType}}>.Shared.Rent(__heap.Length * 2);
                        global::System.Array.Copy(__heap, __next, n);
                        global::System.Buffers.ArrayPool<{{source.ElementType}}>.Shared.Return(__heap, clearArray: true);
                        __heap = __next;
                    }
                    __heap[n++] = enumerator.Current;
                }
                if(n == 0) {
                    return global::System.Array.Empty<{{source.ElementType}}>();
                }
                var result = new {{source.ElementType}}[n];
                if(__heap is null) {
                    __stack.Slice(0, n).CopyTo(result);
                } else {
                    global::System.Array.Copy(__heap, result, n);
                }
                return result;
            } finally {
                if(__heap is not null) {
                    global::System.Buffers.ArrayPool<{{source.ElementType}}>.Shared.Return(__heap, clearArray: true);
                }
            }
        }
        """;
    protected override string GetEnumeratorDisposeBody => string.Empty;
}
