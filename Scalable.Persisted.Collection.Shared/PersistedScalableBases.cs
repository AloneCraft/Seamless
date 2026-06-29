using BenchmarkDotNet.Attributes;
using Scalable.Collection;
using Seamless.Collections;
using Seamless.Serialization;

namespace Scalable.Persisted.Collection;

public abstract class PersistedSetScalable<TProvider,T>
    where TProvider : IHashSetValueProvider<T>
    where T : IEquatable<T> {
    [BenchFact,Benchmark]
    public long Add() {
        var count = ScalableWorkload.ElementCount;
        var path = ScalablePath.Create<TProvider,T>(nameof(Add));
        try {
            using var set = new global::Seamless.Persisted.Collections.Set<T>(path,initialCapacity: 1,initialDataCapacity: 4096);
            for(var i=0;i<count;i++) {
                set.Add(TProvider.Create(i));
            }
            Assert.Equal(count,set.GetLongCount());
            return set.GetLongCount();
        } finally {
            ScalablePath.DeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public int Contains() {
        var count = ScalableWorkload.ElementCount;
        var path = ScalablePath.Create<TProvider,T>(nameof(Contains));
        try {
            using var set = new global::Seamless.Persisted.Collections.Set<T>(path,initialCapacity: 1,initialDataCapacity: 4096);
            for(var i=0;i<count;i++) {
                set.Add(TProvider.Create(i));
            }
            var found = 0;
            for(var i=0;i<count;i++) {
                if(set.Contains(TProvider.Create(i))) {
                    found++;
                }
            }
            Assert.Equal(count,found);
            return found;
        } finally {
            ScalablePath.DeleteDirectory(path);
        }
    }
}

public abstract class PersistedListScalable<TStrategy,T>
    where TStrategy : IListTestStrategy<T> {
    [BenchFact,Benchmark]
    public long Add() {
        var count = ScalableWorkload.ElementCount;
        var path = ScalablePath.Create<TStrategy,T>(nameof(Add));
        try {
            using var handle = TStrategy.CreateList(path,initialCapacity: 1,initialDataCapacity: 4096);
            for(var i=0;i<count;i++) {
                handle.List.Add(TStrategy.Elem(i));
            }
            Assert.Equal(count,handle.List.GetLongCount());
            return handle.List.GetLongCount();
        } finally {
            ScalablePath.DeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public long Remove() {
        var count = ScalableWorkload.ElementCount;
        var path = ScalablePath.Create<TStrategy,T>(nameof(Remove));
        try {
            using var handle = TStrategy.CreateList(path,initialCapacity: 1,initialDataCapacity: 4096);
            for(var i=0;i<count;i++) {
                handle.List.Add(TStrategy.Elem(i));
            }
            for(var i=0;i<count;i++) {
                handle.List.Remove();
            }
            Assert.Equal(0,handle.List.GetLongCount());
            return handle.List.GetLongCount();
        } finally {
            ScalablePath.DeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public long Indexer() {
        var count = ScalableWorkload.ElementCount;
        var path = ScalablePath.Create<TStrategy,T>(nameof(Indexer));
        try {
            using var handle = TStrategy.CreateList(path,initialCapacity: 1,initialDataCapacity: 4096);
            for(var i=0;i<count;i++) {
                handle.List.Add(TStrategy.Elem(i));
            }
            var index = ScalableWorkload.ReadIndex(count);
            Assert.Equal(TStrategy.Elem(index),handle.List[index]);
            Assert.Equal(TStrategy.Elem(count-1),handle.List[count-1]);
            return handle.List.GetLongCount();
        } finally {
            ScalablePath.DeleteDirectory(path);
        }
    }
}

public abstract class PersistedBlittableDictionaryScalable<TProvider,TKey,TValue>
    where TProvider : IDictTestStrategy<TKey,TValue>
    where TKey : unmanaged, IEquatable<TKey>
    where TValue : unmanaged, IEquatable<TValue> {
    [BenchFact,Benchmark]
    public long Add() {
        var count = ScalableWorkload.ElementCount;
        var path = ScalablePath.Create<TProvider,KeyValuePair<TKey,TValue>>(nameof(Add));
        try {
            using var dictionary = new global::Seamless.Persisted.Collections.BlittableDictionary<TKey,TValue>(path,initialCapacity: 1,initialDataCapacity: 4096);
            AddRange(dictionary,count);
            Assert.Equal(count,dictionary.Count);
            return dictionary.Count;
        } finally {
            ScalablePath.DeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public int TryGetValue() {
        var count = ScalableWorkload.ElementCount;
        var path = ScalablePath.Create<TProvider,KeyValuePair<TKey,TValue>>(nameof(TryGetValue));
        try {
            using var dictionary = new global::Seamless.Persisted.Collections.BlittableDictionary<TKey,TValue>(path,initialCapacity: 1,initialDataCapacity: 4096);
            AddRange(dictionary,count);
            var found = 0;
            for(var i=0;i<count;i++) {
                if(dictionary.TryGetValue(TProvider.K(i),out _)) {
                    found++;
                }
            }
            Assert.Equal(count,found);
            return found;
        } finally {
            ScalablePath.DeleteDirectory(path);
        }
    }

    private static void AddRange(global::Seamless.Persisted.Collections.BlittableDictionary<TKey,TValue> dictionary,int count) {
        for(var i=0;i<count;i++) {
            dictionary.Add(TProvider.K(i),TProvider.V(i));
        }
    }
}

public abstract class PersistedSerializedDictionaryScalable<TProvider,TKey,TValue>
    where TProvider : IDictTestStrategy<TKey,TValue>
    where TKey : notnull, IEquatable<TKey>
    where TValue : IEquatable<TValue> {
    [BenchFact,Benchmark]
    public long Add() {
        var count = ScalableWorkload.ElementCount;
        var path = ScalablePath.Create<TProvider,KeyValuePair<TKey,TValue>>(nameof(Add));
        try {
            using var dictionary = new global::Seamless.Persisted.Collections.SerializedDictionary<TKey,TValue,MemoryPackSerializer<TKey>,MemoryPackSerializer<TValue>>(path,initialCapacity: 1,initialDataCapacity: 4096);
            AddRange(dictionary,count);
            Assert.Equal(count,dictionary.Count);
            return dictionary.Count;
        } finally {
            ScalablePath.DeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public int TryGetValue() {
        var count = ScalableWorkload.ElementCount;
        var path = ScalablePath.Create<TProvider,KeyValuePair<TKey,TValue>>(nameof(TryGetValue));
        try {
            using var dictionary = new global::Seamless.Persisted.Collections.SerializedDictionary<TKey,TValue,MemoryPackSerializer<TKey>,MemoryPackSerializer<TValue>>(path,initialCapacity: 1,initialDataCapacity: 4096);
            AddRange(dictionary,count);
            var found = 0;
            for(var i=0;i<count;i++) {
                if(dictionary.TryGetValue(TProvider.K(i),out _)) {
                    found++;
                }
            }
            Assert.Equal(count,found);
            return found;
        } finally {
            ScalablePath.DeleteDirectory(path);
        }
    }

    private static void AddRange(global::Seamless.Persisted.Collections.SerializedDictionary<TKey,TValue,MemoryPackSerializer<TKey>,MemoryPackSerializer<TValue>> dictionary,int count) {
        for(var i=0;i<count;i++) {
            dictionary.Add(TProvider.K(i),TProvider.V(i));
        }
    }
}

internal static class ScalablePath {
    public static string Create<TStrategy,TElement>(string tag) {
        var directory = Path.Combine(
            Environment.CurrentDirectory,
            "Scalable",
            typeof(TStrategy).Name,
            typeof(TElement).Name,
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory,tag+".dat");
    }

    public static void DeleteDirectory(string path) {
        var directory = Path.GetDirectoryName(path);
        if(string.IsNullOrWhiteSpace(directory)) {
            return;
        }
        try {
            Directory.Delete(directory,recursive:true);
        } catch(IOException) {
        } catch(UnauthorizedAccessException) {
        }
    }
}
