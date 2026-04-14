using Seamless.Collections.Tests;
using Xunit.Abstractions;
namespace Seamless.Collections.Benchmarks;
[Collection("Benchmark")]
public abstract class Benchmark<TKey,TValue>(ITestOutputHelper output):Test<TKey,TValue>(output)
    where TKey:IEquatable<TKey>
    where TValue:IEquatable<TValue>{
    protected override double MutableFraction=>1.0;
    private protected const int Loop=100;
}
