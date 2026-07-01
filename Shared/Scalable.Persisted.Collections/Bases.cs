using Scalable.Collections;
using Seamless.Serialization;
using System.Collections.Concurrent;

namespace Scalable.Persisted.Collections;

public abstract class PersistedSetScalable<TProvider,T>
    :ScalableBenchmarkBase
    where TProvider : IHashSetValueProvider<T>
    where T : IEquatable<T> {
    private static readonly ConcurrentDictionary<string,Lazy<global::Seamless.Persisted.Collections.Set<T>>> SharedContainsSets = new(StringComparer.Ordinal);

    [Benchmark,BenchTheory]
    [BenchArguments(65536,1,4096)]
    [BenchArguments(131072,1,4096)]
    [BenchArguments(262144,1,4096)]
    [BenchArguments(524288,1,4096)]
    [BenchArguments(1048576,1,4096)]
    [BenchArguments(2097152,1,4096)]
    [BenchArguments(4194304,1,4096)]
    [BenchArguments(8388608,1,4096)]
    public long Add(long Count,long Initial,long Data) {
        var count = checked((int)Count);
        var values = GetSharedValues<PersistedSetScalable<TProvider,T>,T>(count,static index => TProvider.Create(index));
        var path = ScalablePath.Create<TProvider,T>(nameof(Add));
        try {
            using var set = global::Seamless.Persisted.Collections.Store.CreateSet<T>(path,InitialCapacity: Initial,InitialDataCapacity: Data);
            foreach(var value in values) {
                set.Add(value);
            }
            Assert.Equal(count,set.GetLongCount());
            return set.GetLongCount();
        } finally {
            ScalablePath.DeleteDirectory(path);
        }
    }

    [Benchmark,BenchTheory]
    [BenchArguments(65536,1,4096)]
    [BenchArguments(131072,1,4096)]
    [BenchArguments(262144,1,4096)]
    [BenchArguments(524288,1,4096)]
    [BenchArguments(1048576,1,4096)]
    [BenchArguments(2097152,1,4096)]
    [BenchArguments(4194304,1,4096)]
    [BenchArguments(8388608,1,4096)]
    public int Contains(long Count,long Initial,long Data) {
        var count = checked((int)Count);
        var values = GetSharedValues<PersistedSetScalable<TProvider,T>,T>(count,static index => TProvider.Create(index));
        var set = GetSharedContainsSet(count,Initial,Data);
        var found = 0;
        foreach(var value in values) {
            if(set.Contains(value)) {
                found++;
            }
        }
        Assert.Equal(count,found);
        return found;
    }

    private static global::Seamless.Persisted.Collections.Set<T> GetSharedContainsSet(int count,long initial,long data) {
        var key = count.ToString(System.Globalization.CultureInfo.InvariantCulture)
            +"|"+initial.ToString(System.Globalization.CultureInfo.InvariantCulture)
            +"|"+data.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return SharedContainsSets.GetOrAdd(
            key,
            _ => new Lazy<global::Seamless.Persisted.Collections.Set<T>>(
                () => CreateContainsSet(count,initial,data),
                LazyThreadSafetyMode.ExecutionAndPublication)).Value;
    }

    private static global::Seamless.Persisted.Collections.Set<T> CreateContainsSet(int count,long initial,long data) {
        var path = ScalablePath.Create<TProvider,T>(nameof(Contains));
        var set = global::Seamless.Persisted.Collections.Store.CreateSet<T>(path,InitialCapacity: initial,InitialDataCapacity: data);
        foreach(var value in GetSharedValues<PersistedSetScalable<TProvider,T>,T>(count,static index => TProvider.Create(index))) {
            set.Add(value);
        }
        set.Compact();
        ScalablePath.RegisterProcessCleanup(path,set);
        return set;
    }
}

public abstract class PersistedListScalable<TStrategy,T>
    :ScalableBenchmarkBase
    where TStrategy : IListTestStrategy<T> {
    private static readonly ConcurrentDictionary<string,Lazy<ListHandle<T>>> SharedIndexerLists = new(StringComparer.Ordinal);

    [Benchmark,BenchTheory]
    [BenchArguments(65536,1,4096)]
    [BenchArguments(131072,1,4096)]
    [BenchArguments(262144,1,4096)]
    [BenchArguments(524288,1,4096)]
    [BenchArguments(1048576,1,4096)]
    [BenchArguments(2097152,1,4096)]
    [BenchArguments(4194304,1,4096)]
    [BenchArguments(8388608,1,4096)]
    public long Add(long Count,long Initial,long Data) {
        var count = checked((int)Count);
        var values = GetSharedValues<PersistedListScalable<TStrategy,T>,T>(count,static index => TStrategy.Elem(index));
        var path = ScalablePath.Create<TStrategy,T>(nameof(Add));
        try {
            using var handle = TStrategy.CreateList(path,initialCapacity: Initial,initialDataCapacity: Data);
            foreach(var value in values) {
                handle.List.Add(value);
            }
            Assert.Equal(count,handle.List.GetLongCount());
            return handle.List.GetLongCount();
        } finally {
            ScalablePath.DeleteDirectory(path);
        }
    }

    [Benchmark,BenchTheory]
    [BenchArguments(65536,1,4096)]
    [BenchArguments(131072,1,4096)]
    [BenchArguments(262144,1,4096)]
    [BenchArguments(524288,1,4096)]
    [BenchArguments(1048576,1,4096)]
    [BenchArguments(2097152,1,4096)]
    [BenchArguments(4194304,1,4096)]
    [BenchArguments(8388608,1,4096)]
    public long Remove(long Count,long Initial,long Data) {
        var count = checked((int)Count);
        var values = GetSharedValues<PersistedListScalable<TStrategy,T>,T>(count,static index => TStrategy.Elem(index));
        var path = ScalablePath.Create<TStrategy,T>(nameof(Remove));
        try {
            using var handle = TStrategy.CreateList(path,initialCapacity: Initial,initialDataCapacity: Data);
            foreach(var value in values) {
                handle.List.Add(value);
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

    [Benchmark,BenchTheory]
    [BenchArguments(65536,1,4096)]
    [BenchArguments(131072,1,4096)]
    [BenchArguments(262144,1,4096)]
    [BenchArguments(524288,1,4096)]
    [BenchArguments(1048576,1,4096)]
    [BenchArguments(2097152,1,4096)]
    [BenchArguments(4194304,1,4096)]
    [BenchArguments(8388608,1,4096)]
    public long Indexer(long Count,long Initial,long Data) {
        var count = checked((int)Count);
        var values = GetSharedValues<PersistedListScalable<TStrategy,T>,T>(count,static index => TStrategy.Elem(index));
        var handle = GetSharedIndexerList(count,Initial,Data);
        var index = ScalableWorkload.ReadIndex(count);
        Assert.Equal(values[index],handle.List[index]);
        Assert.Equal(values[count-1],handle.List[count-1]);
        return handle.List.GetLongCount();
    }

    private static ListHandle<T> GetSharedIndexerList(int count,long initial,long data) {
        var key = count.ToString(System.Globalization.CultureInfo.InvariantCulture)
            +"|"+initial.ToString(System.Globalization.CultureInfo.InvariantCulture)
            +"|"+data.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return SharedIndexerLists.GetOrAdd(
            key,
            _ => new Lazy<ListHandle<T>>(
                () => CreateIndexerList(count,initial,data),
                LazyThreadSafetyMode.ExecutionAndPublication)).Value;
    }

    private static ListHandle<T> CreateIndexerList(int count,long initial,long data) {
        var path = ScalablePath.Create<TStrategy,T>(nameof(Indexer));
        var handle = TStrategy.CreateList(path,initialCapacity: initial,initialDataCapacity: data);
        foreach(var value in GetSharedValues<PersistedListScalable<TStrategy,T>,T>(count,static index => TStrategy.Elem(index))) {
            handle.List.Add(value);
        }
        ScalablePath.RegisterProcessCleanup(path,handle);
        return handle;
    }
}

public abstract class PersistedBlittableDictionaryScalable<TProvider,TKey,TValue>
    :ScalableBenchmarkBase
    where TProvider : IDictTestStrategy<TKey,TValue>
    where TKey : unmanaged, IEquatable<TKey>
    where TValue : unmanaged, IEquatable<TValue> {
    private static readonly ConcurrentDictionary<string,Lazy<global::Seamless.Persisted.Collections.Dictionary<TKey,TValue>>> SharedTryGetValueDictionaries = new(StringComparer.Ordinal);

    [Benchmark,BenchTheory]
    [BenchArguments(65536,1,4096)]
    [BenchArguments(131072,1,4096)]
    [BenchArguments(262144,1,4096)]
    [BenchArguments(524288,1,4096)]
    [BenchArguments(1048576,1,4096)]
    [BenchArguments(2097152,1,4096)]
    [BenchArguments(4194304,1,4096)]
    [BenchArguments(8388608,1,4096)]
    public long Add(long Count,long Initial,long Data) {
        var count = checked((int)Count);
        var path = ScalablePath.Create<TProvider,KeyValuePair<TKey,TValue>>(nameof(Add));
        try {
            using var dictionary = global::Seamless.Persisted.Collections.Store.CreateDictionary<TKey,TValue>(path,InitialCapacity: Initial,InitialDataCapacity: Data);
            AddRange(dictionary,count);
            Assert.Equal(count,dictionary.Count);
            return dictionary.Count;
        } finally {
            ScalablePath.DeleteDirectory(path);
        }
    }

    [Benchmark,BenchTheory]
    [BenchArguments(65536,1,4096)]
    [BenchArguments(131072,1,4096)]
    [BenchArguments(262144,1,4096)]
    [BenchArguments(524288,1,4096)]
    [BenchArguments(1048576,1,4096)]
    [BenchArguments(2097152,1,4096)]
    [BenchArguments(4194304,1,4096)]
    [BenchArguments(8388608,1,4096)]
    public int TryGetValue(long Count,long Initial,long Data) {
        var count = checked((int)Count);
        var pairs = GetSharedPairs<PersistedBlittableDictionaryScalable<TProvider,TKey,TValue>,TKey,TValue>(
            count,
            static index => TProvider.K(index),
            static index => TProvider.V(index));
        var dictionary = GetSharedTryGetValueDictionary(count,Initial,Data);
        var found = 0;
        foreach(var pair in pairs) {
            if(dictionary.TryGetValue(pair.Key,out _)) {
                found++;
            }
        }
        Assert.Equal(count,found);
        return found;
    }

    private static void AddRange(global::Seamless.Persisted.Collections.Dictionary<TKey,TValue> dictionary,int count) {
        foreach(var pair in GetSharedPairs<PersistedBlittableDictionaryScalable<TProvider,TKey,TValue>,TKey,TValue>(
            count,
            static index => TProvider.K(index),
            static index => TProvider.V(index))) {
            dictionary.Add(pair.Key,pair.Value);
        }
    }

    private static global::Seamless.Persisted.Collections.Dictionary<TKey,TValue> GetSharedTryGetValueDictionary(int count,long initial,long data) {
        var key = count.ToString(System.Globalization.CultureInfo.InvariantCulture)
            +"|"+initial.ToString(System.Globalization.CultureInfo.InvariantCulture)
            +"|"+data.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return SharedTryGetValueDictionaries.GetOrAdd(
            key,
            _ => new Lazy<global::Seamless.Persisted.Collections.Dictionary<TKey,TValue>>(
                () => CreateTryGetValueDictionary(count,initial,data),
                LazyThreadSafetyMode.ExecutionAndPublication)).Value;
    }

    private static global::Seamless.Persisted.Collections.Dictionary<TKey,TValue> CreateTryGetValueDictionary(int count,long initial,long data) {
        var path = ScalablePath.Create<TProvider,KeyValuePair<TKey,TValue>>(nameof(TryGetValue));
        var dictionary = global::Seamless.Persisted.Collections.Store.CreateDictionary<TKey,TValue>(path,InitialCapacity: initial,InitialDataCapacity: data);
        AddRange(dictionary,count);
        dictionary.Compact();
        ScalablePath.RegisterProcessCleanup(path,dictionary);
        return dictionary;
    }
}

public abstract class PersistedSerializedDictionaryScalable<TProvider,TKey,TValue>
    :ScalableBenchmarkBase
    where TProvider : IDictTestStrategy<TKey,TValue>
    where TKey : notnull, IEquatable<TKey>
    where TValue : IEquatable<TValue> {
    private static readonly ConcurrentDictionary<string,Lazy<global::Seamless.Persisted.Collections.Dictionary<TKey,TValue>>> SharedTryGetValueDictionaries = new(StringComparer.Ordinal);

    [Benchmark,BenchTheory]
    [BenchArguments(65536,1,4096)]
    [BenchArguments(131072,1,4096)]
    [BenchArguments(262144,1,4096)]
    [BenchArguments(524288,1,4096)]
    [BenchArguments(1048576,1,4096)]
    [BenchArguments(2097152,1,4096)]
    [BenchArguments(4194304,1,4096)]
    [BenchArguments(8388608,1,4096)]
    public long Add(long Count,long Initial,long Data) {
        var count = checked((int)Count);
        var path = ScalablePath.Create<TProvider,KeyValuePair<TKey,TValue>>(nameof(Add));
        try {
            using var dictionary = global::Seamless.Persisted.Collections.Store.CreateDictionary<TKey,TValue>(path,InitialCapacity: Initial,InitialDataCapacity: Data);
            AddRange(dictionary,count);
            Assert.Equal(count,dictionary.Count);
            return dictionary.Count;
        } finally {
            ScalablePath.DeleteDirectory(path);
        }
    }

    [Benchmark,BenchTheory]
    [BenchArguments(65536,1,4096)]
    [BenchArguments(131072,1,4096)]
    [BenchArguments(262144,1,4096)]
    [BenchArguments(524288,1,4096)]
    [BenchArguments(1048576,1,4096)]
    [BenchArguments(2097152,1,4096)]
    [BenchArguments(4194304,1,4096)]
    [BenchArguments(8388608,1,4096)]
    public int TryGetValue(long Count,long Initial,long Data) {
        var count = checked((int)Count);
        var pairs = GetSharedPairs<PersistedSerializedDictionaryScalable<TProvider,TKey,TValue>,TKey,TValue>(
            count,
            static index => TProvider.K(index),
            static index => TProvider.V(index));
        var dictionary = GetSharedTryGetValueDictionary(count,Initial,Data);
        var found = 0;
        foreach(var pair in pairs) {
            if(dictionary.TryGetValue(pair.Key,out _)) {
                found++;
            }
        }
        Assert.Equal(count,found);
        return found;
    }

    private static void AddRange(global::Seamless.Persisted.Collections.Dictionary<TKey,TValue> dictionary,int count) {
        foreach(var pair in GetSharedPairs<PersistedSerializedDictionaryScalable<TProvider,TKey,TValue>,TKey,TValue>(
            count,
            static index => TProvider.K(index),
            static index => TProvider.V(index))) {
            dictionary.Add(pair.Key,pair.Value);
        }
    }

    private static global::Seamless.Persisted.Collections.Dictionary<TKey,TValue> GetSharedTryGetValueDictionary(int count,long initial,long data) {
        var key = count.ToString(System.Globalization.CultureInfo.InvariantCulture)
            +"|"+initial.ToString(System.Globalization.CultureInfo.InvariantCulture)
            +"|"+data.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return SharedTryGetValueDictionaries.GetOrAdd(
            key,
            _ => new Lazy<global::Seamless.Persisted.Collections.Dictionary<TKey,TValue>>(
                () => CreateTryGetValueDictionary(count,initial,data),
                LazyThreadSafetyMode.ExecutionAndPublication)).Value;
    }

    private static global::Seamless.Persisted.Collections.Dictionary<TKey,TValue> CreateTryGetValueDictionary(int count,long initial,long data) {
        var path = ScalablePath.Create<TProvider,KeyValuePair<TKey,TValue>>(nameof(TryGetValue));
        var dictionary = global::Seamless.Persisted.Collections.Store.CreateDictionary<TKey,TValue>(path,InitialCapacity: initial,InitialDataCapacity: data);
        AddRange(dictionary,count);
        dictionary.Compact();
        ScalablePath.RegisterProcessCleanup(path,dictionary);
        return dictionary;
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

    public static void RegisterProcessCleanup(string path,IDisposable disposable) {
        AppDomain.CurrentDomain.ProcessExit+=(_,_) => {
            try {
                disposable.Dispose();
            } catch(ObjectDisposedException) {
            }
            DeleteDirectory(path);
        };
    }
}
