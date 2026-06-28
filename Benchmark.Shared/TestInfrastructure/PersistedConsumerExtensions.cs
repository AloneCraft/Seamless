namespace Benchmark.Shared;
internal static class PersistedConsumerExtensions {
    public static void Consume<TEnumerator,T>(this Seamless.System.Carrier.ValueEnumerable<TEnumerator,T> source,Consumer consumer)
        where TEnumerator:struct,Seamless.System.Carrier.IValueEnumerator<T> {
        var e=source.Enumerator;
        while(e.TryGetNext(out var item)) {
            consumer.Consume(item);
        }
        e.Dispose();
    }
}
