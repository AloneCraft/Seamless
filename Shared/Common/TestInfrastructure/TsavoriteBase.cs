namespace Common;

#if BENCH_AS_TEST
#pragma warning disable xUnit1013
#endif

public class TsavoriteBase:Base,IDisposable {
    private readonly int ElementCount;
    private readonly Dictionary<SourceCacheKey,SourceHandle<int>> srcCache = [];
    private readonly Dictionary<SourceCacheKey,SourceHandle<int>> src2Cache = [];
    private readonly Dictionary<SourceCacheKey,SourceHandle<int>> srcOneCache = [];

    public TsavoriteBase(int Count = 80_000):base(NormalizeCount(Count)) {
        this.ElementCount=Count;
    }

    protected new global::Seamless.Tsavorite.Collections.HashSet<int> src(int scale) =>
        this.GetOrCreate(scale,this.srcCache,static length => CreateRange(1,length));

    protected new global::Seamless.Tsavorite.Collections.HashSet<int> srcCount(int elementCount) =>
        this.GetOrCreateByLength(elementCount,this.srcCache,static length => CreateRange(1,length));

    protected new global::Seamless.Tsavorite.Collections.HashSet<int> src2(int scale) =>
        this.GetOrCreate(scale,this.src2Cache,static length => CreateRange(length/2+1,length/2));

    protected new global::Seamless.Tsavorite.Collections.HashSet<int> src2Count(int elementCount) =>
        this.GetOrCreateByLength(elementCount,this.src2Cache,static length => CreateRange(length/2+1,length/2));

    protected new global::Seamless.Tsavorite.Collections.HashSet<int> srcOne(int scale) =>
        this.GetOrCreate(scale,this.srcOneCache,static _ => [0]);

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

    private global::Seamless.Tsavorite.Collections.HashSet<int> GetOrCreate(
        int scale,
        Dictionary<SourceCacheKey,SourceHandle<int>> cache,
        Func<int,int[]> factory) {
        var length= this.CountForComparison(this.CountForTestTiming(this.ElementCount*scale));
        var key = new SourceCacheKey(SourceKind.Src,length,BenchmarkExecutionMode.ReadRandomSourceSeed());
        if(cache.TryGetValue(key,out var handle)) {
            return handle.Set;
        }

        var values = factory(length);
        handle=CreateSet(values);
        cache.Add(key,handle);
        return handle.Set;
    }

    private global::Seamless.Tsavorite.Collections.HashSet<int> GetOrCreateByLength(
        int length,
        Dictionary<SourceCacheKey,SourceHandle<int>> cache,
        Func<int,int[]> factory) {
        var effectiveLength= this.CountForComparison(this.CountForTestTiming(length));
        var key = new SourceCacheKey(SourceKind.SrcCount,effectiveLength,BenchmarkExecutionMode.ReadRandomSourceSeed());
        if(cache.TryGetValue(key,out var handle)) {
            return handle.Set;
        }

        var values = factory(effectiveLength);
        handle=CreateSet(values);
        cache.Add(key,handle);
        return handle.Set;
    }

    private static int[] CreateRange(int start,int length) {
        var result = new int[length];
        for(var index = 0;index<result.Length;index++) {
            result[index]=start+index;
        }
        if(BenchmarkExecutionMode.ReadRandomSourceSeed() is { } seed&&result.Length>1) {
            var random = new Random(seed^start^length);
            for(var index=result.Length-1;index>0;index--) {
                var next = random.Next(index+1);
                (result[index],result[next])=(result[next],result[index]);
            }
        }
        return result;
    }

    private static SourceHandle<T> CreateSet<T>(IEnumerable<T> values)
        where T : unmanaged, IEquatable<T> {
        var count = values.TryGetNonEnumeratedCount(out var knownCount) ? knownCount : 80_000;
        var store = Seamless.Tsavorite.Collections.Store.Create(
            IndexSize: Seamless.Tsavorite.Collections.Store.GetIndexSizeForCapacity(count),
            MemorySize: GetMemorySize(count))
            .Create<T>();
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

    private static void DisposeAll<T>(Dictionary<SourceCacheKey,SourceHandle<T>> cache)
        where T : unmanaged, IEquatable<T> {
        foreach(var handle in cache.Values) {
            handle.Dispose();
        }
        cache.Clear();
    }

    public sealed class SourceHandle<T>(
        IDisposable store,
        global::Seamless.Tsavorite.Collections.HashSet<T> set):IDisposable
        where T : unmanaged, IEquatable<T> {
        public global::Seamless.Tsavorite.Collections.HashSet<T> Set { get; } = set;

        public void Dispose() {
            this.Set.Dispose();
            store.Dispose();
        }
    }
}

#if BENCH_AS_TEST
#pragma warning restore xUnit1013
#endif
