using Tsavorite.SourceGenerator;

namespace Benchmark.Shared;

[AttributeUsage(AttributeTargets.Class)]
public sealed class BenchmarkTestIterationsAttribute(int count):Attribute {
    public int Count { get; } = count;
}

public sealed class Consumer {
    private readonly global::BenchmarkDotNet.Engines.Consumer inner = new();
#if BENCH_AS_TEST
    private readonly List<object?> captured = [];
    private bool captureEnabled;
#endif

    public void Consume<T>(T value) {
#if BENCH_AS_TEST
        if(this.captureEnabled) {
            this.captured.Add(value);
        }
#endif
        this.inner.Consume(value);
    }

#if BENCH_AS_TEST
    internal void BeginCapture() {
        this.captured.Clear();
        this.captureEnabled=true;
    }

    internal object?[] EndCapture() {
        this.captureEnabled=false;
        return this.captured.ToArray();
    }
#endif
}

public class Base {
    protected readonly Consumer consumer = new();
    private protected object? Result;
    private readonly Dictionary<int,int[]> srcCache = [];
    private readonly Dictionary<int,int[]> src2Cache = [];
#if BENCH_AS_TEST
    private static readonly object MissingResult = new();

    // テスト（テストエクスプローラでの簡易タイミング計測）では要素数を
    // ベンチクラス毎に変更できる。既定1000万。変えたいクラスは
    // public Xxx():base(要素数){} で指定する。
    private protected readonly int count;
    protected Base(int count = 80_000) => this.count=count;
    private protected int CountForTestTiming(int benchmarkCount) => this.count;

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

        Xunit.Assert.Equal(expected,flinqResult);
        Xunit.Assert.Equal(expected,noOptimizedResult);
        Xunit.Assert.Equal(expected,zlinqResult);
#if TSAVORITE_LINQ_COUNTER
        Xunit.Assert.True(optimizedScope.Counter.PhysicalDelegate<=noOptimizedScope.Counter.PhysicalDelegate);
        Xunit.Assert.Equal(noOptimizedScope.Counter.LogicalDelegate,noOptimizedScope.Counter.PhysicalDelegate);
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

        Xunit.Assert.Equal(expected,flinqResult);
        Xunit.Assert.Equal(expected,noOptimizedResult);
#if TSAVORITE_LINQ_COUNTER
        Xunit.Assert.True(optimizedScope.Counter.PhysicalDelegate<=noOptimizedScope.Counter.PhysicalDelegate);
        Xunit.Assert.Equal(noOptimizedScope.Counter.LogicalDelegate,noOptimizedScope.Counter.PhysicalDelegate);
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
#else
    protected void AssertResults(Action linq,Action flinq,Action noOptimized,Action zlinq) =>
        throw new NotSupportedException("Result comparison is available only when BENCH_AS_TEST is defined.");

    protected void AssertResults(Action linq,Action flinq,Action noOptimized) =>
        throw new NotSupportedException("Result comparison is available only when BENCH_AS_TEST is defined.");

    // BDN 実ベンチは一律1000万（派生の :Base(N) 指定は無視）。
    // const にすることで生成器のラムダ定数畳み込みが効く。
    private protected readonly int count=10_000_000;//constにしないのはthisをなしにすると警告が出るから
    protected Base(int count=10_000_000){}
    private protected int CountForTestTiming(int benchmarkCount)=>benchmarkCount;
#endif
    protected IEnumerable<int> srcMinusPlus(int scale) {
        for(var a = 0;a<count*scale;a+=2) {
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
    protected IEnumerable<int> src2(int scale) => global::System.Linq.Enumerable.AsEnumerable(this.GetOrCreate(scale,this.src2Cache,static length => {
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

    [LinqIntercept(false)]
    protected IEnumerable<IEnumerable<int>> srcNested(int scale) {
        var side = (int)Math.Sqrt(count*scale);
        return global::System.Linq.Enumerable.Range(1,side).Select(_ => global::System.Linq.Enumerable.Range(1,side));
    }
}
