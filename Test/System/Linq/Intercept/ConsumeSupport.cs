using Seamless.Linq;
namespace Test.System.Linq.Intercept;

// Chain1/Chain2 用の記録 Consumer。`.ToList()` だと LINQ メソッドが 1 個増えてしまうため、
// System.Linq ではない `Consume(consumer)` で列挙を駆動する（= チェーンは LINQ メソッド単体）。
// ソースジェネレータは `expr.Consume(consumer);` という文形を認識し、`consumer.Consume(item)`
// ループへ展開する（intercept される actual 側）。expected 側は完全修飾 SystemEnumerable 呼び出しの
// ため最適化されず、下の IEnumerable 拡張が使われる。両者の Items を比較する。
internal sealed class Consumer<T> {
    public List<T> Items { get; } = [];
    public void Consume(T item) {
        this.Items.Add(item);
    }
}

// 割当計測用の Consumer。Items を溜めないので、計測対象そのものの割当だけを見られる。
// Consumer<T> は List へ蓄積するため、割当を測る用途には使えない。
internal sealed class CountingConsumer {
    public int Count;
    public void Consume(int item) {
        this.Count++;
    }
}

internal static class ConsumeSupport {
    public static void Consume(this IEnumerable<int> source,CountingConsumer consumer) {
        foreach(var item in source) {
            consumer.Consume(item);
        }
    }

    public static void Consume<T>(this IEnumerable<T> source,Consumer<T> consumer) {
        foreach(var item in source) {
            consumer.Consume(item);
        }
    }

    public static void Consume<TEnumerator, T>(this ValueEnumerable<TEnumerator,T> source,Consumer<T> consumer)
        where TEnumerator : struct, IValueEnumerator<T> {
        var e = source.Enumerator;
        while(e.TryGetNext(out var item)) {
            consumer.Consume(item);
        }
        e.Dispose();
    }
}
