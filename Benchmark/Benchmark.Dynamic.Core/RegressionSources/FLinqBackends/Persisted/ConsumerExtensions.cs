namespace Test.FLinqBackends.Persisted;
internal static class ConsumerExtensions{
    public static void Consume<T>(this global::System.Collections.Generic.IEnumerable<T> source,global::BenchmarkDotNet.Engines.Consumer consumer){
        foreach(var item in source){consumer.Consume(item);}
    }
}
