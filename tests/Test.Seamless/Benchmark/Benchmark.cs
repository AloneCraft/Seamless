using System.Numerics;

using Test.Seamless.Collections;

namespace Benchmarks;

[Collection("Benchmark")]
public abstract class Benchmark<TKey,TValue>(ITestOutputHelper output):Test<TKey,TValue>(output)
    where TKey:IEquatable<TKey>
    where TValue:IEquatable<TValue>{
    protected override double MutableFraction=>1.0;
    private protected const int Count=100_000;
    private protected readonly int IndexSize=(int)BitOperations.RoundUpToPowerOf2((uint)Count);
    //private protected const int Loop=100;
}
