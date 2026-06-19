namespace Benchmark.Shared;

static class TsavoriteConsumerExtensions{
    public static void Consume<TEnumerator,T>(this global::Tsavorite.Carrier.ValueEnumerable<TEnumerator,T> source,Consumer consumer)
        where TEnumerator:struct,global::Tsavorite.Carrier.IValueEnumerator<T>{
        var e=source.Enumerator;
        while(e.TryGetNext(out var item)){
            consumer.Consume(item);
        }
        e.Dispose();
    }
}
