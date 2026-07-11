using Collection.Helper.CollectionLinq;
using Scalable.Collections;
using System.Collections.Concurrent;
#if BENCHMARK_PROJECT
namespace Benchmark.Scalable.Tsavorite.Collections;
#else
namespace Scalable.Tsavorite.Collections;
#endif

public abstract class TsavoriteSetScalable<TStrategy,T>
    :ScalableBenchmarkBase
    where TStrategy : IHashSetTestStrategy<T>
    where T : IEquatable<T> {
    private static readonly ConcurrentDictionary<string,Lazy<HashSetHandle<T>>> SharedContainsSets = new(StringComparer.Ordinal);

    [Benchmark,BenchTheory]
    [BenchArguments(65536,131072,4096,4096,8388608,1.0)]
    [BenchArguments(131072,131072,4096,4096,16777216,1.0)]
    [BenchArguments(262144,131072,4096,4096,33554432,1.0)]
    [BenchArguments(524288,131072,4096,4096,67108864,1.0)]
    [BenchArguments(1048576,131072,4096,4096,134217728,1.0)]
    [BenchArguments(2097152,131072,4096,4096,268435456,1.0)]
    [BenchArguments(4194304,131072,4096,4096,536870912,1.0)]
    [BenchArguments(8388608,131072,4096,4096,1073741824,1.0)]
    public long Add(long Count,long Index,long Page,long Segment,long Memory,double Fraction) {
        var count = checked((int)Count);
        var values = GetSharedValues<TsavoriteSetScalable<TStrategy,T>,T>(count,static index => TStrategy.Elem(index));
        using var handle = TStrategy.CreateMemorySet(memorySize: Memory,mutableFraction: Fraction,indexSize: Index,pageSize: Page,segmentSize: Segment);
        foreach(var value in values) {
            handle.Set.Add(value);
        }
        Assert.Equal(count,handle.Set.Count);
        return handle.Set.Count;
    }

    [Benchmark,BenchTheory]
    [BenchArguments(65536,131072,4096,4096,8388608,1.0)]
    [BenchArguments(131072,131072,4096,4096,16777216,1.0)]
    [BenchArguments(262144,131072,4096,4096,33554432,1.0)]
    [BenchArguments(524288,131072,4096,4096,67108864,1.0)]
    [BenchArguments(1048576,131072,4096,4096,134217728,1.0)]
    [BenchArguments(2097152,131072,4096,4096,268435456,1.0)]
    [BenchArguments(4194304,131072,4096,4096,536870912,1.0)]
    [BenchArguments(8388608,131072,4096,4096,1073741824,1.0)]
    public int Contains(long Count,long Index,long Page,long Segment,long Memory,double Fraction) {
        var count = checked((int)Count);
        var values = GetSharedValues<TsavoriteSetScalable<TStrategy,T>,T>(count,static index => TStrategy.Elem(index));
        var handle = GetSharedContainsSet(count,Index,Page,Segment,Memory,Fraction);
        var found = 0;
        foreach(var value in values) {
            if(handle.Set.Contains(value)) {
                found++;
            }
        }
        Assert.Equal(count,found);
        return found;
    }

    private static HashSetHandle<T> GetSharedContainsSet(int count,long index,long page,long segment,long memory,double fraction) {
        var key = count.ToString(System.Globalization.CultureInfo.InvariantCulture)
            +"|"+index.ToString(System.Globalization.CultureInfo.InvariantCulture)
            +"|"+page.ToString(System.Globalization.CultureInfo.InvariantCulture)
            +"|"+segment.ToString(System.Globalization.CultureInfo.InvariantCulture)
            +"|"+memory.ToString(System.Globalization.CultureInfo.InvariantCulture)
            +"|"+fraction.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return SharedContainsSets.GetOrAdd(
            key,
            _ => new Lazy<HashSetHandle<T>>(
                () => CreateContainsSet(count,index,page,segment,memory,fraction),
                LazyThreadSafetyMode.ExecutionAndPublication)).Value;
    }

    private static HashSetHandle<T> CreateContainsSet(int count,long index,long page,long segment,long memory,double fraction) {
        var handle = TStrategy.CreateMemorySet(memorySize: memory,mutableFraction: fraction,indexSize: index,pageSize: page,segmentSize: segment);
        foreach(var value in GetSharedValues<TsavoriteSetScalable<TStrategy,T>,T>(count,static sourceIndex => TStrategy.Elem(sourceIndex))) {
            handle.Set.Add(value);
        }
        return handle;
    }
}

public abstract class TsavoriteDictionaryScalable<TStrategy,TKey,TValue>
    :ScalableBenchmarkBase
    where TStrategy : IDictTestStrategy<TKey,TValue>
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue> {
    private static readonly ConcurrentDictionary<string,Lazy<SharedDictionary>> SharedTryGetValueDictionaries = new(StringComparer.Ordinal);

    [Benchmark,BenchTheory]
    [BenchArguments(65536,131072,4096,4096,8388608,1.0)]
    [BenchArguments(131072,131072,4096,4096,16777216,1.0)]
    [BenchArguments(262144,131072,4096,4096,33554432,1.0)]
    [BenchArguments(524288,131072,4096,4096,67108864,1.0)]
    [BenchArguments(1048576,131072,4096,4096,134217728,1.0)]
    [BenchArguments(2097152,131072,4096,4096,268435456,1.0)]
    [BenchArguments(4194304,131072,4096,4096,536870912,1.0)]
    [BenchArguments(8388608,131072,4096,4096,1073741824,1.0)]
    public long Add(long Count,long Index,long Page,long Segment,long Memory,double Fraction) {
        var count = checked((int)Count);
        using var store = TStrategy.CreateMemoryStore(memorySize: Memory,mutableFraction: Fraction,indexSize: Index,pageSize: Page,segmentSize: Segment);
        using var dictionary = store.NewDict();
        AddRange(dictionary,count);
        Assert.Equal(count,dictionary.Count);
        return dictionary.Count;
    }

    [Benchmark,BenchTheory]
    [BenchArguments(65536,131072,4096,4096,8388608,1.0)]
    [BenchArguments(131072,131072,4096,4096,16777216,1.0)]
    [BenchArguments(262144,131072,4096,4096,33554432,1.0)]
    [BenchArguments(524288,131072,4096,4096,67108864,1.0)]
    [BenchArguments(1048576,131072,4096,4096,134217728,1.0)]
    [BenchArguments(2097152,131072,4096,4096,268435456,1.0)]
    [BenchArguments(4194304,131072,4096,4096,536870912,1.0)]
    [BenchArguments(8388608,131072,4096,4096,1073741824,1.0)]
    public int TryGetValue(long Count,long Index,long Page,long Segment,long Memory,double Fraction) {
        var count = checked((int)Count);
        var pairs = GetSharedPairs<TsavoriteDictionaryScalable<TStrategy,TKey,TValue>,TKey,TValue>(
            count,
            static index => TStrategy.K(index),
            static index => TStrategy.V(index));
        var dictionary = GetSharedTryGetValueDictionary(count,Index,Page,Segment,Memory,Fraction).Dictionary;
        var found = 0;
        foreach(var pair in pairs) {
            if(dictionary.TryGetValue(pair.Key,out _)) {
                found++;
            }
        }
        Assert.Equal(count,found);
        return found;
    }

    private static void AddRange(global::Seamless.Tsavorite.Collections.IDictionary<TKey,TValue> dictionary,int count) {
        foreach(var pair in GetSharedPairs<TsavoriteDictionaryScalable<TStrategy,TKey,TValue>,TKey,TValue>(
            count,
            static index => TStrategy.K(index),
            static index => TStrategy.V(index))) {
            dictionary.Add(pair.Key,pair.Value);
        }
    }

    private static SharedDictionary GetSharedTryGetValueDictionary(int count,long index,long page,long segment,long memory,double fraction) {
        var key = count.ToString(System.Globalization.CultureInfo.InvariantCulture)
            +"|"+index.ToString(System.Globalization.CultureInfo.InvariantCulture)
            +"|"+page.ToString(System.Globalization.CultureInfo.InvariantCulture)
            +"|"+segment.ToString(System.Globalization.CultureInfo.InvariantCulture)
            +"|"+memory.ToString(System.Globalization.CultureInfo.InvariantCulture)
            +"|"+fraction.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return SharedTryGetValueDictionaries.GetOrAdd(
            key,
            _ => new Lazy<SharedDictionary>(
                () => CreateTryGetValueDictionary(count,index,page,segment,memory,fraction),
                LazyThreadSafetyMode.ExecutionAndPublication)).Value;
    }

    private static SharedDictionary CreateTryGetValueDictionary(int count,long index,long page,long segment,long memory,double fraction) {
        var store = TStrategy.CreateMemoryStore(memorySize: memory,mutableFraction: fraction,indexSize: index,pageSize: page,segmentSize: segment);
        var dictionary = store.NewDict();
        AddRange(dictionary,count);
        return new SharedDictionary(store,dictionary);
    }

    private sealed class SharedDictionary(
        StoreHandle<TKey,TValue> store,
        global::Seamless.Tsavorite.Collections.IDictionary<TKey,TValue> dictionary) {
        public StoreHandle<TKey,TValue> Store { get; } = store;
        public global::Seamless.Tsavorite.Collections.IDictionary<TKey,TValue> Dictionary { get; } = dictionary;
    }
}
