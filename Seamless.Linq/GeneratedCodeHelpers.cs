namespace Seamless.Linq;

public static class GeneratedCodeHelpers {
    /// <summary>
    /// System.Linq の Take(Range) と同じ意味で、範囲が件数を超えても例外にせず [0, count] へ切り詰めた開始位置と長さを返す。
    /// <see cref="Range.GetOffsetAndLength(int)"/> は範囲が件数を超えると ArgumentOutOfRangeException になる。
    /// </summary>
    public static (int Offset, int Length) GetClampedOffsetAndLength(Range range, int count) {
        var start = range.Start.IsFromEnd ? Math.Max(0, count - range.Start.Value) : Math.Min(range.Start.Value, count);
        var end = range.End.IsFromEnd ? Math.Max(0, count - range.End.Value) : Math.Min(range.End.Value, count);
        return (start, Math.Max(0, end - start));
    }

    /// <summary>
    /// 生成インターセプターが展開に失敗したときの明示例外。
    /// 静かなフォールバック経路を作らない方針のため、構築時に実行時診断カウンタも加算する。
    /// </summary>
    public static InvalidOperationException LinqInterceptorFailure(string message) {
        LinqCounterScope.Counter.IncrementEnumerableFallback(message);
        return new(message);
    }
    public static InvalidOperationException UnsupportedLinqExpansion(string methodName,string reason) {
        var message = $"FLinq could not expand '{methodName}'. {reason}";
        LinqCounterScope.Counter.IncrementEnumerableFallback(message);
        return new(message);
    }
    public static InvalidOperationException NoElements() =>
        new("Sequence contains no elements");
    /// <summary>
    /// 件数の分からない源(Tsavorite のマルチ用など)から配列へ集めるとき、満杯になった配列を倍に伸ばす。
    /// 伸ばすのは log n 回だけなので、要素ごとに呼ぶ側を小さく保つためインライン化しない。
    /// </summary>
    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static void GrowArray<T>(ref T[]? array) {
        long length = 0;
        if(array is not null) {
            length=array.Length;
        }
        if(length>=Array.MaxLength) {
            throw new OutOfMemoryException("Array dimensions exceeded supported range.");
        }
        var newLength = (int)Math.Min(Math.Max(4L,length*2),Array.MaxLength);
        Array.Resize(ref array,newLength);
    }
    public static NotSupportedException UnorderedSourceOperation(string methodName,string sourceType) =>
        new($"'{methodName}' requires sequence order and is not supported directly on unordered source '{sourceType}'. Cast to IEnumerable<T>, call AsEnumerable(), or materialize to Array/List to opt in.");
    [global::System.Diagnostics.CodeAnalysis.DoesNotReturn]
    public static T ThrowUnorderedSourceOperation<T>(string methodName,string sourceType) =>
        throw UnorderedSourceOperation(methodName,sourceType);

    // インデックス間接ソート(OrderBy/ThenBy の安定ソート実装)向け。
    // Array.Sort(int[],int,int,IComparer<int>) は comparer をボクシングし、O(n log n) 回の
    // Compare がインターフェース越し呼び出しのまま devirtualize されない。
    // TComparer を struct 制約にして Span<int>.Sort<T,TComparer> へ渡すことで、
    // 呼び出し地点ごとに専用インスタンス化が作られ Compare が devirtualize + インライン化される。
    // struct 制約は「うっかりクラス comparer に戻す」回帰をコンパイルエラーで防ぐためのもの。
    public static void SortIndexes<TComparer>(int[] indexes,int count,TComparer comparer)
        where TComparer : struct, IComparer<int>
        => indexes.AsSpan(0,count).Sort(comparer);

    // 型情報を失った IEnumerable<T> ソースを materialize する共通ラダー。
    // 以前は生成器が is T[] / is List<T> / is ICollection<T> / foreach の分岐を
    // 各コール地点へフル展開していたため、1 箇所あたり 25〜36 行を消費していた。
    // 挙動は当時の展開と同一で、生成側は 1 行呼び出しになる。
    //
    // ValueEnumerable<TEnumerator,TSource> 入力向けの融合特殊化（ToArray.cs / ToList.cs）とは
    // 別経路であり重複しない。こちらは IEnumerable<T> 入力専用。
    public static TSource[] ToArrayFromEnumerable<TSource>(IEnumerable<TSource> source) {
        if(source is TSource[] array) {
            if(array.Length==0) {
                return [];
            }
            var result = GC.AllocateUninitializedArray<TSource>(array.Length);
            array.AsSpan().CopyTo(result.AsSpan());
            return result;
        }
        if(source is List<TSource> list) {
            var span = CollectionsMarshal.AsSpan(list);
            if(span.Length==0) {
                return [];
            }
            var result = GC.AllocateUninitializedArray<TSource>(span.Length);
            span.CopyTo(result.AsSpan());
            return result;
        }
        if(source is ICollection<TSource> collection) {
            var count = collection.Count;
            if(count==0) {
                return [];
            }
            var result = GC.AllocateUninitializedArray<TSource>(count);
            collection.CopyTo(result,0);
            return result;
        }
        var fallback = new List<TSource>();
        foreach(var item in source) {
            fallback.Add(item);
        }
        return fallback.ToArray();
    }

    // 件数も span も前もって出せないソース向けの ToList 実体。
    // List<T>.Add の倍々成長は容量が尽きるたびに倍サイズの配列を新規確保して全件コピーするため、
    // N 要素で合計およそ 2N の GC 確保になる（4096 要素・参照型で実測 65,504B、理想は 32,768B）。
    // ここではスタック上の InlineArray16 と ArrayPool のセグメント連結で受けて成長時コピーを無くし、
    // 総数が確定してから正確サイズの List を 1 本だけ確保する。
    // アルゴリズムは ToList(ValueEnumerable<,>) の非 count 経路と同一だが、呼び出し地点では
    // TryGetNonEnumeratedCount が false 確定なのでそのプローブは持ち込まない。
    //
    // enumerator は ref で受け、この中では Dispose しない。所有権は生成コード側の try-finally が持つ。
    // 値渡しにしてはならない: OrderBy のようにコンストラクタでプール配列を借りる enumerator では、
    // 呼び出し元のコピーと合わせて同じ配列を二重に Return してしまう。
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static List<TSource> ToListFromValueEnumerator<TEnumerator, TSource>(ref TEnumerator enumerator)
        where TEnumerator : struct, IValueEnumerator<TSource> {
        var initialBuffer = default(InlineArray16<TSource>);
        Span<TSource> initialBufferSpan = initialBuffer;
        var arrayBuilder = new SegmentedArrayProvider<TSource>(initialBufferSpan);
        var span = arrayBuilder.GetSpan();
        var i = 0;
        while(enumerator.TryGetNext(out var item)) {
            if(i==span.Length) {
                arrayBuilder.Advance(i);
                span=arrayBuilder.GetSpan();
                i=0;
            }
            span[i]=item;
            i++;
        }
        arrayBuilder.Advance(i);

        var count = arrayBuilder.Count;
        var list = new List<TSource>(count);
        CollectionsMarshal.SetCount(list,count);
        arrayBuilder.CopyToAndClear(CollectionsMarshal.AsSpan(list));
        return list;
    }
}
