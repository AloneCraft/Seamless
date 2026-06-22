namespace Benchmark.Shared;

#if BENCH_AS_TEST
#pragma warning disable xUnit1013
#endif

public class PersistedBase(int count = 80_000):Base(NormalizeCount(count)),IDisposable {
    private readonly Dictionary<int,SourceHandle<int>> srcCache = [];
    private readonly Dictionary<int,SourceHandle<int>> src2Cache = [];
    private readonly Dictionary<int,SourceHandle<int>> srcOneCache = [];
    private readonly string rootPath = Path.Combine(AppContext.BaseDirectory,"persisted-benchmark",Guid.NewGuid().ToString("N"));

    protected new global::Seamless.Persisted.Collections.Set<int> src(int scale) =>
        this.GetOrCreate(scale,this.srcCache,static length => CreateRange(1,length));

    protected new global::Seamless.Persisted.Collections.Set<int> srcCount(int elementCount) =>
        this.GetOrCreateByLength(elementCount,this.srcCache,static length => CreateRange(1,length));

    protected new global::Seamless.Persisted.Collections.Set<int> src2(int scale) =>
        this.GetOrCreate(scale,this.src2Cache,static length => {
            var result = new int[length/2];
            var start = length/2+1;
            for(var index = 0;index<result.Length;index++) {
                result[index]=start+index;
            }
            return result;
        });

    protected new global::Seamless.Persisted.Collections.Set<int> src2Count(int elementCount) =>
        this.GetOrCreateByLength(elementCount,this.src2Cache,static length => {
            var result = new int[length/2];
            var start = length/2+1;
            for(var index = 0;index<result.Length;index++) {
                result[index]=start+index;
            }
            return result;
        });

    protected new global::Seamless.Persisted.Collections.Set<int> srcOne(int scale) =>
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

    private global::Seamless.Persisted.Collections.Set<int> GetOrCreate(
        int scale,
        Dictionary<int,SourceHandle<int>> cache,
        Func<int,int[]> factory) {
        var length=this.CountForComparison(this.CountForTestTiming(this.count*scale));
        if(cache.TryGetValue(length,out var handle)) {
            return handle.Set;
        }

        handle=this.CreateSet(factory(length));
        cache.Add(length,handle);
        return handle.Set;
    }

    private global::Seamless.Persisted.Collections.Set<int> GetOrCreateByLength(
        int length,
        Dictionary<int,SourceHandle<int>> cache,
        Func<int,int[]> factory) {
        var effectiveLength=this.CountForComparison(this.CountForTestTiming(length));
        if(cache.TryGetValue(effectiveLength,out var handle)) {
            return handle.Set;
        }

        handle=this.CreateSet(factory(effectiveLength));
        cache.Add(effectiveLength,handle);
        return handle.Set;
    }

    private SourceHandle<T> CreateSet<T>(IEnumerable<T> values) {
        var path = Path.Combine(this.rootPath,Guid.NewGuid().ToString("N"),"items.dat");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var count = values.TryGetNonEnumeratedCount(out var knownCount) ? knownCount : 80_000;
        var set = new global::Seamless.Persisted.Collections.Set<T>(
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
        return result;
    }

    private static void DisposeAll<T>(Dictionary<int,SourceHandle<T>> cache) {
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
        global::Seamless.Persisted.Collections.Set<T> set):IDisposable {
        public global::Seamless.Persisted.Collections.Set<T> Set { get; } = set;

        public void Dispose() {
            this.Set.Dispose();
            TryDeleteDirectory(Path.GetDirectoryName(path)!);
        }
    }
}

#if BENCH_AS_TEST
#pragma warning restore xUnit1013
#endif
