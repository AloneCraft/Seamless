namespace Common;

#if BENCH_AS_TEST
#pragma warning disable xUnit1013
#endif

public class PersistedBase(int count = 80_000):Base(NormalizeCount(count)),IDisposable {
    private readonly Dictionary<SourceCacheKey,SourceHandle<int>> srcCache = [];
    private readonly Dictionary<SourceCacheKey,SourceHandle<int>> src2Cache = [];
    private readonly Dictionary<SourceCacheKey,SourceHandle<int>> srcOneCache = [];
    private readonly string rootPath = Path.Combine(AppContext.BaseDirectory,"persisted-benchmark",Guid.NewGuid().ToString("N"));

    protected new Seamless.Persisted.Collections.Set<int> src(int scale) =>
        this.GetOrCreate(scale,this.srcCache,static length => CreateRange(1,length));

    protected new Seamless.Persisted.Collections.Set<int> srcCount(int elementCount) =>
        this.GetOrCreateByLength(elementCount,this.srcCache,static length => CreateRange(1,length));

    protected new Seamless.Persisted.Collections.Set<int> src2(int scale) =>
        this.GetOrCreate(scale,this.src2Cache,static length => CreateRange(length/2+1,length/2));

    protected new Seamless.Persisted.Collections.Set<int> src2Count(int elementCount) =>
        this.GetOrCreateByLength(elementCount,this.src2Cache,static length => CreateRange(length/2+1,length/2));

    protected new Seamless.Persisted.Collections.Set<int> srcOne(int scale) =>
        this.GetOrCreate(scale,this.srcOneCache,static _ => [0]);

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

    private Seamless.Persisted.Collections.Set<int> GetOrCreate(
        int scale,
        Dictionary<SourceCacheKey,SourceHandle<int>> cache,
        Func<int,int[]> factory) {
        var length= this.CountForComparison(this.CountForTestTiming(this.count*scale));
        var key = new SourceCacheKey(SourceKind.Src,length,BenchmarkExecutionMode.ReadRandomSourceSeed());
        if(cache.TryGetValue(key,out var handle)) {
            return handle.Set;
        }

        handle=this.CreateSet(factory(length));
        cache.Add(key,handle);
        return handle.Set;
    }

    private Seamless.Persisted.Collections.Set<int> GetOrCreateByLength(
        int length,
        Dictionary<SourceCacheKey,SourceHandle<int>> cache,
        Func<int,int[]> factory) {
        var effectiveLength= this.CountForComparison(this.CountForTestTiming(length));
        var key = new SourceCacheKey(SourceKind.SrcCount,effectiveLength,BenchmarkExecutionMode.ReadRandomSourceSeed());
        if(cache.TryGetValue(key,out var handle)) {
            return handle.Set;
        }

        handle=this.CreateSet(factory(effectiveLength));
        cache.Add(key,handle);
        return handle.Set;
    }

    private SourceHandle<T> CreateSet<T>(IEnumerable<T> values) {
        var path = Path.Combine(this.rootPath,Guid.NewGuid().ToString("N"),"items.dat");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var count = values.TryGetNonEnumeratedCount(out var knownCount) ? knownCount : 80_000;
        var set = new Seamless.Persisted.Collections.Set<T>(
            path,
            initialCapacity: Math.Max(1,count),
            initialDataCapacity: Math.Max(4096,count*16));
        foreach(var value in values) {
            set.Add(value);
        }
        return new SourceHandle<T>(path,set);
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

    private static void DisposeAll<T>(Dictionary<SourceCacheKey,SourceHandle<T>> cache) {
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
        }
        catch(IOException) {
        }
        catch(UnauthorizedAccessException) {
        }
    }

    public sealed class SourceHandle<T>(
        string path,
        Seamless.Persisted.Collections.Set<T> set):IDisposable {
        public Seamless.Persisted.Collections.Set<T> Set { get; } = set;

        public void Dispose() {
            this.Set.Dispose();
            TryDeleteDirectory(Path.GetDirectoryName(path)!);
        }
    }
}

#if BENCH_AS_TEST
#pragma warning restore xUnit1013
#endif
