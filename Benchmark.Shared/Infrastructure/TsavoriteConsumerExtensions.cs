namespace Benchmark.Shared;

static class TsavoriteConsumerExtensions{
    public static void Consume<TEnumerator,T>(this global::Seamless.Linq.ValueEnumerable<TEnumerator,T> source,Consumer consumer)
        where TEnumerator:struct,global::Seamless.Linq.IValueEnumerator<T>{
        var e=source.Enumerator;
        while(e.TryGetNext(out var item)){
            consumer.Consume(item);
        }
        e.Dispose();
    }
}
