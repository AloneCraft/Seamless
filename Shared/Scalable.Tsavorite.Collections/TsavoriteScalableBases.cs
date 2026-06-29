using Collection.Helper.CollectionLinq;
using Scalable.Collections;
namespace Scalable.Tsavorite.Collections;

public abstract class TsavoriteSetScalable<TStrategy,T>
    where TStrategy : IHashSetTestStrategy<T>
    where T : IEquatable<T> {
    [BenchFact,Benchmark]
    public long Add() {
        var count = ScalableWorkload.ElementCount;
        using var handle = TStrategy.CreateMemorySet(memorySize: CollectionLinqSourceSizing.GetMemorySize(count));
        for(var i=0;i<count;i++) {
            handle.Set.Add(TStrategy.Elem(i));
        }
        Assert.Equal(count,handle.Set.Count);
        return handle.Set.Count;
    }

    [BenchFact,Benchmark]
    public int Contains() {
        var count = ScalableWorkload.ElementCount;
        using var handle = TStrategy.CreateMemorySet(memorySize: CollectionLinqSourceSizing.GetMemorySize(count));
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
    where TStrategy : IDictTestStrategy<TKey,TValue>
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue> {
    [BenchFact,Benchmark]
    public long Add() {
        var count = ScalableWorkload.ElementCount;
        using var store = TStrategy.CreateMemoryStore(memorySize: CollectionLinqSourceSizing.GetMemorySize(count));
        using var dictionary = store.NewDict();
        AddRange(dictionary,count);
        Assert.Equal(count,dictionary.Count);
        return dictionary.Count;
    }

    [BenchFact,Benchmark]
    public int TryGetValue() {
        var count = ScalableWorkload.ElementCount;
        using var store = TStrategy.CreateMemoryStore(memorySize: CollectionLinqSourceSizing.GetMemorySize(count));
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
