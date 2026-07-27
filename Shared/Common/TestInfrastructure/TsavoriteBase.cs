namespace Common;

#if BENCH_AS_TEST
#pragma warning disable xUnit1013
#endif

public class TsavoriteBase(int count=80_000):TsavoriteBase<int>(count);

public class TsavoriteBase<T>(int elementCount=80_000):Base<T>(NormalizeCount(elementCount)),IDisposable
where T : notnull {
    private readonly Dictionary<SourceCacheKey,SourceHandle<T>> srcCache    = [];
    private readonly Dictionary<SourceCacheKey,SourceHandle<T>> src2Cache   = [];
    private readonly Dictionary<SourceCacheKey,SourceHandle<T>> srcOneCache = [];
    protected internal new global::Seamless.Collections.Tsavorite.HashSet<T> src(int scale) =>
        this.GetOrCreate(scale,this.srcCache,static length => CreateRange(1,length));

    protected internal new global::Seamless.Collections.Tsavorite.HashSet<T> srcCount(int elementCount) =>
        this.GetOrCreateByLength(elementCount,this.srcCache,static length => CreateRange(1,length));

    protected internal new global::Seamless.Collections.Tsavorite.HashSet<T> src2(int scale) =>
        this.GetOrCreate(scale,this.src2Cache,static length => CreateRange(length/2+1,length/2));

    protected internal new global::Seamless.Collections.Tsavorite.HashSet<T> src2Count(int elementCount) =>
        this.GetOrCreateByLength(elementCount,this.src2Cache,static length => CreateRange(length/2+1,length/2));

    protected internal new global::Seamless.Collections.Tsavorite.HashSet<T> srcOne(int scale) =>
        this.GetOrCreate(scale,this.srcOneCache,static _ => [FromInt(0)]);

    [GlobalCleanup]
    public void Cleanup() {
        DisposeAll(this.srcCache);
        DisposeAll(this.src2Cache);
        DisposeAll(this.srcOneCache);
    }

    public void Dispose() => this.Cleanup();

    private static int NormalizeCount(int count) {
#if BENCH_AS_TEST
        return Math.Min(count,8_000);
#else
        return count;
#endif
    }

    private global::Seamless.Collections.Tsavorite.HashSet<T> GetOrCreate(
        int scale,
        Dictionary<SourceCacheKey,SourceHandle<T>> cache,
        Func<int,T[]> factory) {
        var length = this.CountForComparison(this.CountForTestTiming(elementCount*scale));
        var key = new SourceCacheKey(SourceKind.Src,length,BenchmarkExecutionMode.ReadRandomSourceSeed());
        if(cache.TryGetValue(key,out var handle)) {
            return handle.Set;
        }

        var values = factory(length);
        handle=CreateSet(values);
        cache.Add(key,handle);
        return handle.Set;
    }

    private global::Seamless.Collections.Tsavorite.HashSet<T> GetOrCreateByLength(
        int length,
        Dictionary<SourceCacheKey,SourceHandle<T>> cache,
        Func<int,T[]> factory) {
        var effectiveLength = this.CountForComparison(this.CountForTestTiming(length));
        var key = new SourceCacheKey(SourceKind.SrcCount,effectiveLength,BenchmarkExecutionMode.ReadRandomSourceSeed());
        if(cache.TryGetValue(key,out var handle)) {
            return handle.Set;
        }

        var values = factory(effectiveLength);
        handle=CreateSet(values);
        cache.Add(key,handle);
        return handle.Set;
    }

    private static T[] CreateRange(int start,int length) {
        var result = new T[length];
        for(var index = 0;index<result.Length;index++) {
            result[index]=FromInt(start+index);
        }
        if(BenchmarkExecutionMode.ReadRandomSourceSeed() is { } seed&&result.Length>1) {
            var random = new Random(seed^start^length);
            for(var index = result.Length-1;index>0;index--) {
                var next = random.Next(index+1);
                (result[index],result[next])=(result[next],result[index]);
            }
        }
        return result;
    }

    [LinqIntercept(false)]
    private static SourceHandle<T> CreateSet(IEnumerable<T> values) {
        var count = values.TryGetNonEnumeratedCount(out var knownCount) ? knownCount : 80_000;
        var store = Seamless.Collections.Tsavorite.Store.Create(
            IndexSize: Seamless.Collections.Tsavorite.Store.GetIndexSizeForCapacity(count),
            MemorySize: GetMemorySize(count))
            .Create<T>(new Seamless.Collections.MemoryPackSerializerStrategy());
        var set = store.Create();
        foreach(var value in values) {
            set.Add(value);
        }
        return new SourceHandle<T>(store,set);
    }

    private static long GetMemorySize(int count) {
        var required = Math.Max(8192L,count*128L);
        var result = 1L;
        while(result<required) {
            result<<=1;
        }
        return result;
    }

    private static void DisposeAll<TItem>(Dictionary<SourceCacheKey,SourceHandle<TItem>> cache) {
        foreach(var handle in cache.Values) {
            handle.Dispose();
        }
        cache.Clear();
    }

    public sealed class SourceHandle<TItem>(
        IDisposable store,
        global::Seamless.Collections.Tsavorite.HashSet<TItem> set):IDisposable {
        public global::Seamless.Collections.Tsavorite.HashSet<TItem> Set { get; } = set;

        public void Dispose() {
            this.Set.Dispose();
            store.Dispose();
        }
    }
}

#if BENCH_AS_TEST
#pragma warning restore xUnit1013
#endif
