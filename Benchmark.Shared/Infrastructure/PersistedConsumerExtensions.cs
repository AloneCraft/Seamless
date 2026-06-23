namespace Benchmark.Shared;

static class PersistedConsumerExtensions {
    public static void Consume<TEnumerator,T>(this Seamless.Linq.ValueEnumerable<TEnumerator,T> source,Consumer consumer)
        where TEnumerator:struct,Seamless.Linq.IValueEnumerator<T> {
        var e=source.Enumerator;
        while(e.TryGetNext(out var item)) {
            consumer.Consume(item);
        }
        e.Dispose();
    }
}
