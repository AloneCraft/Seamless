namespace Benchmarks;

[Collection("Benchmark")]
public abstract class HashSetBenchmark<TKey>(ITestOutputHelper output):Benchmark<TKey,int>(output)
    where TKey : IEquatable<TKey> {
    protected override double MutableFraction => 1.0;
    protected override int CreateValue(int seed) => seed;
}
