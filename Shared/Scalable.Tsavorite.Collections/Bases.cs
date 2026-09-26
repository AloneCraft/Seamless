using Scalable.Collections;

using Seamless.Execution;
namespace Shared.Scalable.Tsavorite.Collections;

public abstract class TsavoriteSetScalable<TStrategy, T>
    :ScalableBenchmarkBase,IDisposable
    where TStrategy : IHashSetTestStrategy<T>
    where T : IEquatable<T> {
    // インスタンスが全スレッドの値を所有する。終了したスレッドの値も Dispose まで追跡する。
    private readonly ThreadLocal<global::System.Collections.Generic.Dictionary<string,TunedHashSetHandle<T>>> containsSets = new(
        static () => new(StringComparer.Ordinal),trackAllValues: true);
    private int disposed;

    [Benchmark, BenchTheory]
#if BENCH_AS_TEST
    [BenchArguments(65536,64,4096,4096,8388608,1.0)]
    [BenchArguments(65536,64,4096,4096,67108864,1.0)]
    [BenchArguments(65536,512,4096,4096,8388608,1.0)]
    [BenchArguments(65536,512,4096,4096,67108864,1.0)]
#else
    [BenchArguments(65536,64,4096,4096,8388608,1.0)]
    [BenchArguments(65536,64,4096,4096,67108864,1.0)]
    [BenchArguments(65536,512,4096,4096,8388608,1.0)]
    [BenchArguments(65536,512,4096,4096,67108864,1.0)]
    [BenchArguments(131072,64,4096,4096,16777216,1.0)]
    [BenchArguments(131072,64,4096,4096,134217728,1.0)]
    [BenchArguments(131072,512,4096,4096,16777216,1.0)]
    [BenchArguments(131072,512,4096,4096,134217728,1.0)]
    [BenchArguments(262144,64,4096,4096,33554432,1.0)]
    [BenchArguments(262144,64,4096,4096,268435456,1.0)]
    [BenchArguments(262144,512,4096,4096,33554432,1.0)]
    [BenchArguments(262144,512,4096,4096,268435456,1.0)]
    [BenchArguments(524288,64,4096,4096,67108864,1.0)]
    [BenchArguments(524288,64,4096,4096,536870912,1.0)]
    [BenchArguments(524288,512,4096,4096,67108864,1.0)]
    [BenchArguments(524288,512,4096,4096,536870912,1.0)]
    [BenchArguments(1048576,64,4096,4096,134217728,1.0)]
    [BenchArguments(1048576,64,4096,4096,1073741824,1.0)]
    [BenchArguments(1048576,512,4096,4096,134217728,1.0)]
    [BenchArguments(1048576,512,4096,4096,1073741824,1.0)]
#endif
    public long Add(long Count,long Index,long Page,long Segment,long Memory,double Fraction) {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref this.disposed)!=0,this);
        var count = checked((int)Count);
        var values = GetSharedValues<TsavoriteSetScalable<TStrategy,T>,T>(count,static index => TStrategy.Elem(index));
        using var handle = TStrategy.CreateTunedSet(
            ScalableTsavoriteConfiguration.CreateTuning(Index,Page,Segment,Memory,Fraction),
            ScalableTsavoriteConfiguration.CreateLimits(Index,Page,Memory));
        foreach(var value in values) {
            handle.Set.Add(value);
        }
        Assert.Equal(count,handle.Set.Count);
        return handle.Set.Count;
    }

    [Benchmark, BenchTheory]
#if BENCH_AS_TEST
    [BenchArguments(65536,64,4096,4096,8388608,1.0)]
    [BenchArguments(65536,64,4096,4096,67108864,1.0)]
    [BenchArguments(65536,512,4096,4096,8388608,1.0)]
    [BenchArguments(65536,512,4096,4096,67108864,1.0)]
#else
    [BenchArguments(65536,64,4096,4096,8388608,1.0)]
    [BenchArguments(65536,64,4096,4096,67108864,1.0)]
    [BenchArguments(65536,512,4096,4096,8388608,1.0)]
    [BenchArguments(65536,512,4096,4096,67108864,1.0)]
    [BenchArguments(131072,64,4096,4096,16777216,1.0)]
    [BenchArguments(131072,64,4096,4096,134217728,1.0)]
    [BenchArguments(131072,512,4096,4096,16777216,1.0)]
    [BenchArguments(131072,512,4096,4096,134217728,1.0)]
    [BenchArguments(262144,64,4096,4096,33554432,1.0)]
    [BenchArguments(262144,64,4096,4096,268435456,1.0)]
    [BenchArguments(262144,512,4096,4096,33554432,1.0)]
    [BenchArguments(262144,512,4096,4096,268435456,1.0)]
    [BenchArguments(524288,64,4096,4096,67108864,1.0)]
    [BenchArguments(524288,64,4096,4096,536870912,1.0)]
    [BenchArguments(524288,512,4096,4096,67108864,1.0)]
    [BenchArguments(524288,512,4096,4096,536870912,1.0)]
    [BenchArguments(1048576,64,4096,4096,134217728,1.0)]
    [BenchArguments(1048576,64,4096,4096,1073741824,1.0)]
    [BenchArguments(1048576,512,4096,4096,134217728,1.0)]
    [BenchArguments(1048576,512,4096,4096,1073741824,1.0)]
#endif
    public int Contains(long Count,long Index,long Page,long Segment,long Memory,double Fraction) {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref this.disposed)!=0,this);
        var count = checked((int)Count);
        var values = GetSharedValues<TsavoriteSetScalable<TStrategy,T>,T>(count,static index => TStrategy.Elem(index));
        var handle = this.GetContainsSet(count,Index,Page,Segment,Memory,Fraction);
        var found = 0;
        foreach(var value in values) {
            if(handle.Set.Contains(value)) {
                found++;
            }
        }
        Assert.Equal(count,found);
        return found;
    }

    private TunedHashSetHandle<T> GetContainsSet(int count,long index,long page,long segment,long memory,double fraction) {
        var key = count.ToString(System.Globalization.CultureInfo.InvariantCulture)
            +"|"+index.ToString(System.Globalization.CultureInfo.InvariantCulture)
            +"|"+page.ToString(System.Globalization.CultureInfo.InvariantCulture)
            +"|"+segment.ToString(System.Globalization.CultureInfo.InvariantCulture)
            +"|"+memory.ToString(System.Globalization.CultureInfo.InvariantCulture)
            +"|"+fraction.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var sets = this.containsSets.Value!;
        if(!sets.TryGetValue(key,out var handle)) {
            handle=CreateContainsSet(count,index,page,segment,memory,fraction);
            sets.Add(key,handle);
        }
        return handle;
    }

    private static TunedHashSetHandle<T> CreateContainsSet(int count,long index,long page,long segment,long memory,double fraction) {
        var handle = TStrategy.CreateTunedSet(
            ScalableTsavoriteConfiguration.CreateTuning(index,page,segment,memory,fraction),
            ScalableTsavoriteConfiguration.CreateLimits(index,page,memory));
        try {
            foreach(var value in GetSharedValues<TsavoriteSetScalable<TStrategy,T>,T>(count,static sourceIndex => TStrategy.Elem(sourceIndex))) {
                handle.Set.Add(value);
            }
            return handle;
        } catch {
            handle.Dispose();
            throw;
        }
    }

    /// <summary>全スレッドの操作終了後に呼ぶ。終了済みスレッドのアクセサもストアより先に破棄する。</summary>
    [GlobalCleanup]
    public void Dispose() {
        if(Interlocked.Exchange(ref this.disposed,1)==0) {
            ScalableTsavoriteConfiguration.DisposeCache(this.containsSets);
        }
    }
}

public abstract class TsavoriteDictionaryScalable<TStrategy, TKey, TValue>
    :ScalableBenchmarkBase,IDisposable
    where TStrategy : IDictTestStrategy<TKey,TValue>
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue> {
    // 操作用オブジェクトはスレッドごとに作り、データ本体とともにこのインスタンスで破棄する。
    private readonly ThreadLocal<global::System.Collections.Generic.Dictionary<string,SharedDictionary>> tryGetValueDictionaries = new(
        static () => new(StringComparer.Ordinal),trackAllValues: true);
    private int disposed;

    [Benchmark, BenchTheory]
#if BENCH_AS_TEST
    [BenchArguments(65536,64,4096,4096,8388608,1.0)]
    [BenchArguments(65536,64,4096,4096,67108864,1.0)]
    [BenchArguments(65536,512,4096,4096,8388608,1.0)]
    [BenchArguments(65536,512,4096,4096,67108864,1.0)]
#else
    [BenchArguments(65536,64,4096,4096,8388608,1.0)]
    [BenchArguments(65536,64,4096,4096,67108864,1.0)]
    [BenchArguments(65536,512,4096,4096,8388608,1.0)]
    [BenchArguments(65536,512,4096,4096,67108864,1.0)]
    [BenchArguments(131072,64,4096,4096,16777216,1.0)]
    [BenchArguments(131072,64,4096,4096,134217728,1.0)]
    [BenchArguments(131072,512,4096,4096,16777216,1.0)]
    [BenchArguments(131072,512,4096,4096,134217728,1.0)]
    [BenchArguments(262144,64,4096,4096,33554432,1.0)]
    [BenchArguments(262144,64,4096,4096,268435456,1.0)]
    [BenchArguments(262144,512,4096,4096,33554432,1.0)]
    [BenchArguments(262144,512,4096,4096,268435456,1.0)]
    [BenchArguments(524288,64,4096,4096,67108864,1.0)]
    [BenchArguments(524288,64,4096,4096,536870912,1.0)]
    [BenchArguments(524288,512,4096,4096,67108864,1.0)]
    [BenchArguments(524288,512,4096,4096,536870912,1.0)]
    [BenchArguments(1048576,64,4096,4096,134217728,1.0)]
    [BenchArguments(1048576,64,4096,4096,1073741824,1.0)]
    [BenchArguments(1048576,512,4096,4096,134217728,1.0)]
    [BenchArguments(1048576,512,4096,4096,1073741824,1.0)]
#endif
    public long Add(long Count,long Index,long Page,long Segment,long Memory,double Fraction) {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref this.disposed)!=0,this);
        var count = checked((int)Count);
        using var store = TStrategy.CreateTunedStore(
            ScalableTsavoriteConfiguration.CreateTuning(Index,Page,Segment,Memory,Fraction),
            ScalableTsavoriteConfiguration.CreateLimits(Index,Page,Memory));
        using var dictionary = store.NewDict();
        AddRange(dictionary,count);
        Assert.Equal(count,dictionary.Count);
        return dictionary.Count;
    }

    [Benchmark, BenchTheory]
#if BENCH_AS_TEST
    [BenchArguments(65536,64,4096,4096,8388608,1.0)]
    [BenchArguments(65536,64,4096,4096,67108864,1.0)]
    [BenchArguments(65536,512,4096,4096,8388608,1.0)]
    [BenchArguments(65536,512,4096,4096,67108864,1.0)]
#else
    [BenchArguments(65536,64,4096,4096,8388608,1.0)]
    [BenchArguments(65536,64,4096,4096,67108864,1.0)]
    [BenchArguments(65536,512,4096,4096,8388608,1.0)]
    [BenchArguments(65536,512,4096,4096,67108864,1.0)]
    [BenchArguments(131072,64,4096,4096,16777216,1.0)]
    [BenchArguments(131072,64,4096,4096,134217728,1.0)]
    [BenchArguments(131072,512,4096,4096,16777216,1.0)]
    [BenchArguments(131072,512,4096,4096,134217728,1.0)]
    [BenchArguments(262144,64,4096,4096,33554432,1.0)]
    [BenchArguments(262144,64,4096,4096,268435456,1.0)]
    [BenchArguments(262144,512,4096,4096,33554432,1.0)]
    [BenchArguments(262144,512,4096,4096,268435456,1.0)]
    [BenchArguments(524288,64,4096,4096,67108864,1.0)]
    [BenchArguments(524288,64,4096,4096,536870912,1.0)]
    [BenchArguments(524288,512,4096,4096,67108864,1.0)]
    [BenchArguments(524288,512,4096,4096,536870912,1.0)]
    [BenchArguments(1048576,64,4096,4096,134217728,1.0)]
    [BenchArguments(1048576,64,4096,4096,1073741824,1.0)]
    [BenchArguments(1048576,512,4096,4096,134217728,1.0)]
    [BenchArguments(1048576,512,4096,4096,1073741824,1.0)]
#endif
    public int TryGetValue(long Count,long Index,long Page,long Segment,long Memory,double Fraction) {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref this.disposed)!=0,this);
        var count = checked((int)Count);
        var pairs = GetSharedPairs<TsavoriteDictionaryScalable<TStrategy,TKey,TValue>,TKey,TValue>(
            count,
            static index => TStrategy.K(index),
            static index => TStrategy.V(index));
        var dictionary = this.GetTryGetValueDictionary(count,Index,Page,Segment,Memory,Fraction).Dictionary;
        var found = 0;
        foreach(var pair in pairs) {
            if(dictionary.TryGetValue(pair.Key,out _)) {
                found++;
            }
        }
        Assert.Equal(count,found);
        return found;
    }

    private static void AddRange(global::Seamless.Collections.Tsavorite.IDictionary<TKey,TValue> dictionary,int count) {
        foreach(var pair in GetSharedPairs<TsavoriteDictionaryScalable<TStrategy,TKey,TValue>,TKey,TValue>(
            count,
            static index => TStrategy.K(index),
            static index => TStrategy.V(index))) {
            dictionary.Add(pair.Key,pair.Value);
        }
    }

    private SharedDictionary GetTryGetValueDictionary(int count,long index,long page,long segment,long memory,double fraction) {
        var key = count.ToString(System.Globalization.CultureInfo.InvariantCulture)
            +"|"+index.ToString(System.Globalization.CultureInfo.InvariantCulture)
            +"|"+page.ToString(System.Globalization.CultureInfo.InvariantCulture)
            +"|"+segment.ToString(System.Globalization.CultureInfo.InvariantCulture)
            +"|"+memory.ToString(System.Globalization.CultureInfo.InvariantCulture)
            +"|"+fraction.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var dictionaries = this.tryGetValueDictionaries.Value!;
        if(!dictionaries.TryGetValue(key,out var dictionary)) {
            dictionary=CreateTryGetValueDictionary(count,index,page,segment,memory,fraction);
            dictionaries.Add(key,dictionary);
        }
        return dictionary;
    }

    private static SharedDictionary CreateTryGetValueDictionary(int count,long index,long page,long segment,long memory,double fraction) {
        var store = TStrategy.CreateTunedStore(
            ScalableTsavoriteConfiguration.CreateTuning(index,page,segment,memory,fraction),
            ScalableTsavoriteConfiguration.CreateLimits(index,page,memory));
        global::Seamless.Collections.Tsavorite.IDictionary<TKey,TValue>? dictionary = null;
        try {
            dictionary=store.NewDict();
            AddRange(dictionary,count);
            return new SharedDictionary(store,dictionary);
        } catch {
            try {
                dictionary?.Dispose();
            } finally {
                store.Dispose();
            }
            throw;
        }
    }

    /// <summary>全スレッドの操作終了後に呼ぶ。終了済みスレッドのアクセサもストアより先に破棄する。</summary>
    [GlobalCleanup]
    public void Dispose() {
        if(Interlocked.Exchange(ref this.disposed,1)==0) {
            ScalableTsavoriteConfiguration.DisposeCache(this.tryGetValueDictionaries);
        }
    }

    private sealed class SharedDictionary(
        TunedDictionaryHandle<TKey,TValue> store,
        global::Seamless.Collections.Tsavorite.IDictionary<TKey,TValue> dictionary):IDisposable {
        public global::Seamless.Collections.Tsavorite.IDictionary<TKey,TValue> Dictionary { get; } = dictionary;
        public void Dispose() {
            try {
                this.Dictionary.Dispose();
            } finally {
                store.Dispose();
            }
        }
    }
}

file static class ScalableTsavoriteConfiguration {
    public static TsavoriteTuning CreateTuning(long index,long page,long segment,long memory,double fraction) => new() {
        IndexSize=index,
        PageSize=page,
        SegmentSize=segment,
        LogMemorySize=memory,
        MutableFraction=fraction,
        AutoGrowIndex=false,
    };

    public static StoreLimits CreateLimits(long index,long page,long memory) => new() {
        MemoryBytes=checked(index+memory+2*page),
    };

    public static void DisposeCache<T>(ThreadLocal<global::System.Collections.Generic.Dictionary<string,T>> cache)
        where T:IDisposable {
        List<Exception>? errors = null;
        try {
            foreach(var values in cache.Values) {
                foreach(var value in values.Values) {
                    try {
                        value.Dispose();
                    } catch(Exception error) {
                        (errors??=[]).Add(error);
                    }
                }
                values.Clear();
            }
        } finally {
            cache.Dispose();
        }
        if(errors is not null) {
            throw new AggregateException("Scalable collection cleanup failed.",errors);
        }
    }
}
