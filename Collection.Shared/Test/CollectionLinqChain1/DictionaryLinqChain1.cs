using BenchmarkDotNet.Attributes;
using Collection.Shared.Strategy.CollectionLinq;

namespace Collection.Shared.Test.CollectionLinqChain1;

public abstract class DictionaryLinqChain1<TStrategy,TSource,TElement,TKey,TValue>
    :CollectionLinqChain1<TStrategy,TSource,TElement,TKey,TValue>
    where TStrategy : IDictionaryLinqStrategy<TSource,TElement,TKey,TValue>
    where TSource : IDisposable
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue>
{
#if BENCH_AS_TEST
    private const int ElementCount = 128;
#else
    private const int ElementCount = 4096;
#endif

    [BenchFact,Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void KeyValuePairEnumeration() {
        using var source = TStrategy.Create(ElementCount);
        var pairs = TStrategy.AsEnumerable(source)
            .Select(static element => new KeyValuePair<int,int>(
                TStrategy.KeyNumber(TStrategy.Key(element)),
                TStrategy.ValueNumber(TStrategy.Value(element))))
            .ToDictionary(static pair => pair.Key,static pair => pair.Value);
        AssertDictionaryRange(pairs);
    }

    [BenchFact,Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Keys() {
        using var source = TStrategy.Create(ElementCount);
        var keys = TStrategy.Keys(source)
            .Select(static key => TStrategy.KeyNumber(key))
            .ToHashSet();
        AssertNumberRange(keys,1,ElementCount);
    }

    [BenchFact,Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Values() {
        using var source = TStrategy.Create(ElementCount);
        var values = TStrategy.Values(source)
            .Select(static value => TStrategy.ValueNumber(value))
            .ToHashSet();
        AssertNumberRange(values,1,ElementCount);
    }

    [BenchFact,Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void KeyProjection() {
        using var source = TStrategy.Create(ElementCount);
        var keys = TStrategy.AsEnumerable(source)
            .Select(static element => TStrategy.Key(element))
            .Select(static key => TStrategy.KeyNumber(key))
            .ToHashSet();
        AssertNumberRange(keys,1,ElementCount);
    }

    [BenchFact,Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void ValueProjection() {
        using var source = TStrategy.Create(ElementCount);
        var values = TStrategy.AsEnumerable(source)
            .Select(static element => TStrategy.Value(element))
            .Select(static value => TStrategy.ValueNumber(value))
            .ToHashSet();
        AssertNumberRange(values,1,ElementCount);
    }

    [BenchFact,Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void ToDictionary() {
        using var source = TStrategy.Create(ElementCount);
        var dictionary = TStrategy.AsEnumerable(source)
            .ToDictionary(
                static element => TStrategy.KeyNumber(TStrategy.Key(element)),
                static element => TStrategy.ValueNumber(TStrategy.Value(element)));
        AssertDictionaryRange(dictionary);
    }

    private static void AssertDictionaryRange(global::System.Collections.Generic.Dictionary<int,int> dictionary) {
        Assert.Equal(ElementCount,dictionary.Count);
        for(var offset = 0;offset<ElementCount;offset++) {
            var number = offset+1;
            Assert.Contains(number,dictionary.Keys);
            Assert.Equal(number,dictionary[number]);
        }
    }

    private static void AssertNumberRange(global::System.Collections.Generic.HashSet<int> numbers,int start,int count) {
        Assert.Equal(count,numbers.Count);
        for(var offset = 0;offset<count;offset++) {
            Assert.Contains(start+offset,numbers);
        }
    }
}
