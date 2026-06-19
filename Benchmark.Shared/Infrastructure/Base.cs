namespace Benchmark.Shared;

[AttributeUsage(AttributeTargets.Method,AllowMultiple=false)]
public sealed class LinqInterceptAttribute(bool optimizeLinq):Attribute {
    public bool OptimizeLinq { get; } = optimizeLinq;
}

[AttributeUsage(AttributeTargets.Class)]
public sealed class BenchmarkTestIterationsAttribute(int count):Attribute {
    public int Count { get; } = count;
}

public static class BenchmarkExecutionMode {
    private const string TestModeKey = "Benchmark.Shared.TestMode";

    public static bool IsTestMode =>
        AppContext.GetData(TestModeKey) is true
        ||Environment.GetEnvironmentVariable("SEAMLESS_BENCHMARK_TEST_MODE")=="1";

    public static void EnableTestMode() => AppContext.SetData(TestModeKey,true);
}

public sealed class Consumer {
    private readonly global::BenchmarkDotNet.Engines.Consumer inner = new();
    private readonly List<object?> captured = [];
    private bool captureEnabled;

    public void Consume<T>(T value) {
        if(this.captureEnabled) {
            this.captured.Add(value);
        }

        this.inner.Consume(value);
    }

    internal void BeginCapture() {
        this.captured.Clear();
        this.captureEnabled=true;
    }

    internal object?[] EndCapture() {
        this.captureEnabled=false;
        return this.captured.ToArray();
    }
}

public class Base {
    private const int BenchmarkDefaultCount = 10_000_000;
    private const int TestDefaultCount = 80_000;
    private static readonly object MissingResult = new();

    protected readonly Consumer consumer = new();
    private protected object? Result;
    private protected readonly int count;
    private readonly Dictionary<int,int[]> srcCache = [];
    private readonly Dictionary<int,int[]> src2Cache = [];

    protected Base(int count = -1) =>
        this.count=count>=0?count:(BenchmarkExecutionMode.IsTestMode?TestDefaultCount:BenchmarkDefaultCount);

    private protected int CountForTestTiming(int benchmarkCount) =>
        BenchmarkExecutionMode.IsTestMode?this.count:benchmarkCount;

    protected void AssertResults(Action linq,Action flinq,Action noOptimized,Action zlinq) {
        var expected = this.CaptureResult(linq);
#if TSAVORITE_LINQ_COUNTER
        using var optimizedScope = global::Tsavorite.LinqCounterScope.Start();
#endif
        var flinqResult = this.CaptureResult(flinq);
#if TSAVORITE_LINQ_COUNTER
        using var noOptimizedScope = global::Tsavorite.LinqCounterScope.Start();
#endif
        var noOptimizedResult = this.CaptureResult(noOptimized);
        var zlinqResult = this.CaptureResult(zlinq);

        AssertEqual(expected,flinqResult,"FLinq result differs from LINQ.");
        AssertEqual(expected,noOptimizedResult,"Non-optimized FLinq result differs from LINQ.");
        AssertEqual(expected,zlinqResult,"ZLinq result differs from LINQ.");
#if TSAVORITE_LINQ_COUNTER
        AssertTrue(optimizedScope.Counter.PhysicalDelegate<=noOptimizedScope.Counter.PhysicalDelegate,
            "Optimized FLinq should not use more physical delegates than the non-optimized path.");
        AssertEqual(noOptimizedScope.Counter.LogicalDelegate,noOptimizedScope.Counter.PhysicalDelegate,
            "Non-optimized FLinq logical and physical delegate counts should match.");
#endif
    }

    protected void AssertResults(Action linq,Action flinq,Action noOptimized) {
        var expected = this.CaptureResult(linq);
#if TSAVORITE_LINQ_COUNTER
        using var optimizedScope = global::Tsavorite.LinqCounterScope.Start();
#endif
        var flinqResult = this.CaptureResult(flinq);
#if TSAVORITE_LINQ_COUNTER
        using var noOptimizedScope = global::Tsavorite.LinqCounterScope.Start();
#endif
        var noOptimizedResult = this.CaptureResult(noOptimized);

        AssertEqual(expected,flinqResult,"FLinq result differs from LINQ.");
        AssertEqual(expected,noOptimizedResult,"Non-optimized FLinq result differs from LINQ.");
#if TSAVORITE_LINQ_COUNTER
        AssertTrue(optimizedScope.Counter.PhysicalDelegate<=noOptimizedScope.Counter.PhysicalDelegate,
            "Optimized FLinq should not use more physical delegates than the non-optimized path.");
        AssertEqual(noOptimizedScope.Counter.LogicalDelegate,noOptimizedScope.Counter.PhysicalDelegate,
            "Non-optimized FLinq logical and physical delegate counts should match.");
#endif
    }

    private string CaptureResult(Action action) {
        this.Result=MissingResult;
        this.consumer.BeginCapture();
        action();
        var consumed=this.consumer.EndCapture();
        return !ReferenceEquals(MissingResult,this.Result)
            ? Normalize(this.Result)
            : Normalize(consumed);
    }

    private static void AssertEqual<T>(T expected,T actual,string message) {
        if(EqualityComparer<T>.Default.Equals(expected,actual)) {
            return;
        }

        throw new InvalidOperationException($"{message}{Environment.NewLine}Expected: {expected}{Environment.NewLine}Actual: {actual}");
    }

    private static void AssertTrue(bool condition,string message) {
        if(!condition) {
            throw new InvalidOperationException(message);
        }
    }

    private static string Normalize(object? value) {
        if(value is null) {
            return "<null>";
        }
        if(value is string text) {
            return text;
        }
        if(TryGetKey(value,out var key)&&value is global::System.Collections.IEnumerable keyedEnumerable) {
            return "{"+Normalize(key)+":"+NormalizeEnumerable(keyedEnumerable)+"}";
        }
        if(value is global::System.Collections.IDictionary dictionary) {
            return NormalizeDictionary(dictionary);
        }
        if(value is global::System.Collections.IEnumerable enumerable) {
            return NormalizeEnumerable(enumerable);
        }
        if(value is IFormattable formattable) {
            return formattable.ToString(null,global::System.Globalization.CultureInfo.InvariantCulture);
        }
        return value.ToString()??string.Empty;
    }

    private static string NormalizeEnumerable(global::System.Collections.IEnumerable enumerable) {
        var values=new List<string>();
        foreach(var item in enumerable) {
            values.Add(Normalize(item));
        }
        return "["+string.Join(",",values)+"]";
    }

    private static string NormalizeDictionary(global::System.Collections.IDictionary dictionary) {
        var values=new List<string>();
        foreach(global::System.Collections.DictionaryEntry entry in dictionary) {
            values.Add(Normalize(entry.Key)+"="+Normalize(entry.Value));
        }
        values.Sort(StringComparer.Ordinal);
        return "["+string.Join(",",values)+"]";
    }

    private static bool TryGetKey(object instance,out object? key) {
        var property=instance.GetType().GetProperty("Key");
        if(property is null) {
            key=null;
            return false;
        }
        key=property.GetValue(instance);
        return true;
    }

    protected IEnumerable<int> srcMinusPlus(int scale) {
        for(var a = 0;a<count*scale;a+=2) {
            yield return -a;
            yield return +a;
        }
    }

    protected IEnumerable<int> srcMinusPlusCount(int elementCount) {
        for(var a = 0;a<elementCount;a+=2) {
            yield return -a;
            yield return +a;
        }
    }

    [LinqIntercept(false)]
    protected IEnumerable<int> src(int scale) => global::System.Linq.Enumerable.AsEnumerable(this.GetOrCreate(scale,this.srcCache,static length => {
        var values = new int[length];
        for(var i = 0;i<values.Length;i++) {
            values[i]=i+1;
        }
        return values;
    }));

    [LinqIntercept(false)]
    protected IEnumerable<int> srcCount(int elementCount) => global::System.Linq.Enumerable.AsEnumerable(this.GetOrCreateByLength(elementCount,this.srcCache,static length => {
        var values = new int[length];
        for(var i = 0;i<values.Length;i++) {
            values[i]=i+1;
        }
        return values;
    }));

    [LinqIntercept(false)]
    protected IEnumerable<int> src2(int scale) => global::System.Linq.Enumerable.AsEnumerable(this.GetOrCreate(scale,this.src2Cache,static length => {
        var values = new int[length/2];
        var start = length/2+1;
        for(var i = 0;i<values.Length;i++) {
            values[i]=start+i;
        }
        return values;
    }));

    [LinqIntercept(false)]
    protected IEnumerable<int> src2Count(int elementCount) => global::System.Linq.Enumerable.AsEnumerable(this.GetOrCreateByLength(elementCount,this.src2Cache,static length => {
        var values = new int[length/2];
        var start = length/2+1;
        for(var i = 0;i<values.Length;i++) {
            values[i]=start+i;
        }
        return values;
    }));

    [LinqIntercept(false)]
    protected IEnumerable<int> srcOne(int scale) => [0];

    private int[] GetOrCreate(int scale,Dictionary<int,int[]> cache,Func<int,int[]> factory) {
        if(cache.TryGetValue(scale,out var values)) {
            return values;
        }

        values=factory(count*scale);
        cache.Add(scale,values);
        return values;
    }

    private int[] GetOrCreateByLength(int length,Dictionary<int,int[]> cache,Func<int,int[]> factory) {
        if(cache.TryGetValue(length,out var values)) {
            return values;
        }

        values=factory(length);
        cache.Add(length,values);
        return values;
    }

    [LinqIntercept(false)]
    protected IEnumerable<IEnumerable<int>> srcNested(int scale) {
        var side = (int)Math.Sqrt(count*scale);
        return global::System.Linq.Enumerable.Range(1,side).Select(_ => global::System.Linq.Enumerable.Range(1,side));
    }

    [LinqIntercept(false)]
    protected IEnumerable<IEnumerable<int>> srcNestedCount(int elementCount) {
        var side = (int)Math.Sqrt(elementCount);
        return global::System.Linq.Enumerable.Range(1,side).Select(_ => global::System.Linq.Enumerable.Range(1,side));
    }
}
