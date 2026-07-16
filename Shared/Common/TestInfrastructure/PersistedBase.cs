namespace Common;

#if BENCH_AS_TEST
#pragma warning disable xUnit1013
#endif

public class PersistedBase(int count=80_000):PersistedBase<int>(count);

public class PersistedBase<T>(int count = 80_000):Base<T>(NormalizeCount(count)), IDisposable where T : notnull {
    private readonly Dictionary<SourceCacheKey,SourceHandle<T>> srcCache = [];
    private readonly Dictionary<SourceCacheKey,SourceHandle<T>> src2Cache = [];
    private readonly Dictionary<SourceCacheKey,SourceHandle<T>> srcOneCache = [];
    private readonly string rootPath = Path.Combine(AppContext.BaseDirectory,"persisted-benchmark",Guid.NewGuid().ToString("N"));

    protected internal new Seamless.Persisted.Collections.Set<T> src(int scale) =>
        this.GetOrCreate(scale,this.srcCache,static length => CreateRange(1,length));

    protected internal new Seamless.Persisted.Collections.Set<T> srcCount(int elementCount) =>
        this.GetOrCreateByLength(elementCount,this.srcCache,static length => CreateRange(1,length));

    protected internal new Seamless.Persisted.Collections.Set<T> src2(int scale) =>
        this.GetOrCreate(scale,this.src2Cache,static length => CreateRange(length/2+1,length/2));

    protected internal new Seamless.Persisted.Collections.Set<T> src2Count(int elementCount) =>
        this.GetOrCreateByLength(elementCount,this.src2Cache,static length => CreateRange(length/2+1,length/2));

    protected internal new Seamless.Persisted.Collections.Set<T> srcOne(int scale) =>
        this.GetOrCreate(scale,this.srcOneCache,static _ => [FromInt(0)]);

    [GlobalCleanup]
    public void Cleanup() {
        DisposeAll(this.srcCache);
        DisposeAll(this.src2Cache);
        DisposeAll(this.srcOneCache);
        TryDeleteDirectory(this.rootPath);
    }

    public void Dispose() => this.Cleanup();

    private static int NormalizeCount(int count) {
#if BENCH_AS_TEST
        return Math.Min(count,8_000);
#else
        return count;
#endif
    }

    private Seamless.Persisted.Collections.Set<T> GetOrCreate(
        int scale,
        Dictionary<SourceCacheKey,SourceHandle<T>> cache,
        Func<int,T[]> factory) {
        var length = this.CountForComparison(this.CountForTestTiming(this.count*scale));
        var key = new SourceCacheKey(SourceKind.Src,length,BenchmarkExecutionMode.ReadRandomSourceSeed());
        if(cache.TryGetValue(key,out var handle)) {
            return handle.Set;
        }

        handle=this.CreateSet(factory(length));
        cache.Add(key,handle);
        return handle.Set;
    }

    private Seamless.Persisted.Collections.Set<T> GetOrCreateByLength(
        int length,
        Dictionary<SourceCacheKey,SourceHandle<T>> cache,
        Func<int,T[]> factory) {
        var effectiveLength = this.CountForComparison(this.CountForTestTiming(length));
        var key = new SourceCacheKey(SourceKind.SrcCount,effectiveLength,BenchmarkExecutionMode.ReadRandomSourceSeed());
        if(cache.TryGetValue(key,out var handle)) {
            return handle.Set;
        }

        handle=this.CreateSet(factory(effectiveLength));
        cache.Add(key,handle);
        return handle.Set;
    }

    [LinqIntercept(false)]
    private SourceHandle<TItem> CreateSet<TItem>(IEnumerable<TItem> values) {
        var path = Path.Combine(this.rootPath,Guid.NewGuid().ToString("N"),"items.dat");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var count = values.TryGetNonEnumeratedCount(out var knownCount) ? knownCount : 80_000;
        var set = Seamless.Persisted.Collections.Store.CreateSet<TItem>(
            path,
            InitialCapacity: Math.Max(1,count),
            InitialDataCapacity: Math.Max(4096,count*16));
        foreach(var value in values) {
            set.Add(value);
        }
        return new SourceHandle<TItem>(path,set);
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

    private static void DisposeAll<TItem>(Dictionary<SourceCacheKey,SourceHandle<TItem>> cache) {
        foreach(var handle in cache.Values) {
            handle.Dispose();
        }
        cache.Clear();
    }

    private static void TryDeleteDirectory(string path) {
        try {
            if(Directory.Exists(path)) {
                Directory.Delete(path,recursive: true);
            }
        } catch(IOException) {
        } catch(UnauthorizedAccessException) {
        }
    }

    public sealed class SourceHandle<TItem>(
        string path,
        Seamless.Persisted.Collections.Set<TItem> set):IDisposable {
        public Seamless.Persisted.Collections.Set<TItem> Set { get; } = set;

        public void Dispose() {
            this.Set.Dispose();
            TryDeleteDirectory(Path.GetDirectoryName(path)!);
        }
    }
}

#if BENCH_AS_TEST
#pragma warning restore xUnit1013
#endif
