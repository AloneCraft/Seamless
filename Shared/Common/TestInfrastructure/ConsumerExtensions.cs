namespace Common;

internal static class ConsumerExtensions {
    public static void Consume<T>(this IEnumerable<T> source,Consumer consumer) {
        foreach(var item in source) {
            consumer.Consume(item);
        }
    }

    public static void Consume<TEnumerator, T>(this global::ZLinq.ValueEnumerable<TEnumerator,T> source,Consumer consumer)
        where TEnumerator : struct, global::ZLinq.IValueEnumerator<T>, allows ref struct {
        using var e = source.Enumerator;
        while(e.TryGetNext(out var item)) {
            consumer.Consume(item);
        }
    }
}
