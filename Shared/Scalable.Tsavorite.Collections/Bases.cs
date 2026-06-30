using Collection.Helper.CollectionLinq;
using Scalable.Collections;
namespace Scalable.Tsavorite.Collections;

public abstract class TsavoriteSetScalable<TStrategy,T>
    :ScalableBenchmarkBase
    where TStrategy : IHashSetTestStrategy<T>
    where T : IEquatable<T> {
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
        using var handle = TStrategy.CreateMemorySet(memorySize: Memory,mutableFraction: Fraction,indexSize: Index,pageSize: Page,segmentSize: Segment);
        for(var i=0;i<count;i++) {
            handle.Set.Add(TStrategy.Elem(i));
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
        using var handle = TStrategy.CreateMemorySet(memorySize: Memory,mutableFraction: Fraction,indexSize: Index,pageSize: Page,segmentSize: Segment);
        for(var i=0;i<count;i++) {
            handle.Set.Add(TStrategy.Elem(i));
        }
        var found = 0;
        for(var i=0;i<count;i++) {
            if(handle.Set.Contains(TStrategy.Elem(i))) {
                found++;
            }
        }
        Assert.Equal(count,found);
        return found;
    }
}

public abstract class TsavoriteDictionaryScalable<TStrategy,TKey,TValue>
    :ScalableBenchmarkBase
    where TStrategy : IDictTestStrategy<TKey,TValue>
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue> {
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
        using var store = TStrategy.CreateMemoryStore(memorySize: Memory,mutableFraction: Fraction,indexSize: Index,pageSize: Page,segmentSize: Segment);
        using var dictionary = store.NewDict();
        AddRange(dictionary,count);
        var found = 0;
        for(var i=0;i<count;i++) {
            if(dictionary.TryGetValue(TStrategy.K(i),out _)) {
                found++;
            }
        }
        Assert.Equal(count,found);
        return found;
    }

    private static void AddRange(global::Seamless.Tsavorite.Collections.IDictionary<TKey,TValue> dictionary,int count) {
        for(var i=0;i<count;i++) {
            dictionary.Add(TStrategy.K(i),TStrategy.V(i));
        }
    }
}
