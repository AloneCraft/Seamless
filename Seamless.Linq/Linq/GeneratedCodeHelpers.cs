namespace Seamless.Linq;

public static class GeneratedCodeHelpers {
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

}
