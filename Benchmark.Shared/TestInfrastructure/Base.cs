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
    private const string ElementScaleKey = "SEAMLESS_BENCHMARK_ELEMENT_SCALE";
    private static readonly AsyncLocal<int?> ComparisonElementLimit = new();
    private static readonly Lazy<double> ElementScale = new(ReadElementScale);

    public static bool IsTestMode =>
#if BENCH_AS_TEST
        true
#else
        AppContext.GetData(TestModeKey) is true
        ||Environment.GetEnvironmentVariable("SEAMLESS_BENCHMARK_TEST_MODE")=="1"
#endif
        ;

    public static bool IsComparisonMode => ComparisonElementLimit.Value is > 0;

    public static void EnableTestMode() => AppContext.SetData(TestModeKey,true);

    public static void EnableComparisonMode(int maxElementCount) {
        if(maxElementCount<=0) {
            throw new ArgumentOutOfRangeException(nameof(maxElementCount));
        }

        ComparisonElementLimit.Value=maxElementCount;
    }

    public static void DisableComparisonMode() => ComparisonElementLimit.Value=null;

    internal static int LimitForComparison(int elementCount) =>
        ComparisonElementLimit.Value is > 0 and var limit
            ? Math.Min(elementCount,limit)
            : elementCount;

    internal static int ScaleElementCount(int elementCount) {
        if(elementCount<=0) {
            return elementCount;
        }

        var scaled = elementCount*ElementScale.Value;
        if(!double.IsFinite(scaled)||scaled<=1) {
            return 1;
        }

        return scaled>=int.MaxValue
            ? int.MaxValue
            : Math.Max(1,(int)Math.Round(scaled,MidpointRounding.AwayFromZero));
    }

    private static double ReadElementScale() {
        var text = Environment.GetEnvironmentVariable(ElementScaleKey);
        return double.TryParse(text,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var scale)
            &&double.IsFinite(scale)
            &&scale>0
            ? scale
            : 1;
    }
}

public sealed class Consumer {
    private const int DigestSampleCount = 8;
    private readonly global::BenchmarkDotNet.Engines.Consumer inner = new();
    private readonly List<object?> captured = [];
    private bool captureEnabled;
    private DigestCapture? digestCapture;

    public void Consume<T>(T value) {
        if(captureEnabled) {
            captured.Add(value);
        }

        digestCapture?.Add(value);
        inner.Consume(value);
    }

    internal void BeginCapture() {
        captured.Clear();
        captureEnabled=true;
    }

    internal object?[] EndCapture() {
        captureEnabled=false;
        return captured.ToArray();
    }

    internal void BeginDigestCapture(Func<object?,string> normalize) {
        if(digestCapture is not null) {
            throw new InvalidOperationException("Digest capture is already active.");
        }

        digestCapture=new DigestCapture(normalize);
    }

    internal string EndDigestCapture() {
        var capture=digestCapture??throw new InvalidOperationException("Digest capture is not active.");
        digestCapture=null;
        return capture.End();
    }

    internal static string CreateSingleDigest(string normalized) {
        var capture=new DigestCapture(static value => value?.ToString()??string.Empty);
        capture.Add(normalized);
        return capture.End();
    }

    private sealed class DigestCapture(Func<object?,string> normalize) {
        private readonly System.Security.Cryptography.IncrementalHash hash =
            System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
        private readonly List<string> first = [];
        private readonly Queue<string> last = [];
        private long count;

        public void Add(object? value) {
            var normalized=normalize(value);
            count++;
            if(first.Count<DigestSampleCount) {
                first.Add(normalized);
            }
            if(last.Count==DigestSampleCount) {
                last.Dequeue();
            }
            last.Enqueue(normalized);

            var bytes=System.Text.Encoding.UTF8.GetBytes(normalized);
            var lengthBytes=new byte[4];
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(lengthBytes,bytes.Length);
            hash.AppendData(lengthBytes);
            hash.AppendData(bytes);
        }

        public string End() {
            var hashText=Convert.ToHexString(hash.GetHashAndReset());
            hash.Dispose();
            return "count="+count
                +";sha256="+hashText
                +";first="+Format(first)
                +";last="+Format(last);
        }

        private static string Format(IEnumerable<string> values) =>
            "["+string.Join(",",values)+"]";
    }
}

public enum SourceKind {
    Src,
    SrcCount,
    Src2,
    Src2Count,
    SrcOne
}

public readonly record struct SourceCacheKey(SourceKind Kind, int Length);

public class Base {
    private const int BenchmarkDefaultCount = 10_000_000;
    private const int TestDefaultCount = 80_000;
    private static readonly object MissingResult = new();
    private static readonly object SourceCacheSync = new();
    private static readonly Dictionary<SourceCacheKey,int[]> SourceCache = [];

    protected readonly Consumer consumer = new();
    private protected object? Result;
    private protected readonly int count;

    protected Base(int count = -1) =>
        this.count=count>=0?count:(BenchmarkExecutionMode.IsTestMode?TestDefaultCount:BenchmarkDefaultCount);

    private protected int CountForTestTiming(int benchmarkCount) =>
        BenchmarkExecutionMode.ScaleElementCount(BenchmarkExecutionMode.IsTestMode?count:benchmarkCount);

    private protected int CountForComparison(int elementCount) =>
        BenchmarkExecutionMode.LimitForComparison(elementCount);

    public string CaptureBenchmarkMethodResult(string methodName,bool invokeImpl) {
        if(string.IsNullOrWhiteSpace(methodName)) {
            throw new ArgumentException("Method name is required.",nameof(methodName));
        }

        var method=invokeImpl
            ? FindInstanceMethodInHierarchy(GetType(),methodName+"_Impl",nonPublic:true)
            : FindBenchmarkMethod(GetType(),methodName);

        Result=MissingResult;
        consumer.BeginDigestCapture(Normalize);
        object? returnValue=null;
        Exception? invocationError=null;
        var consumed=string.Empty;
        try {
            returnValue=method.Invoke(this,null);
        } catch(System.Reflection.TargetInvocationException ex) when(ex.InnerException is not null) {
            invocationError=ex.InnerException;
        } finally {
            consumed=consumer.EndDigestCapture();
        }

        if(invocationError is not null) {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(invocationError).Throw();
            throw new InvalidOperationException("Unreachable.");
        }

        if(!ReferenceEquals(MissingResult,Result)) {
            return Consumer.CreateSingleDigest(Normalize(Result));
        }
        if(method.ReturnType!=typeof(void)) {
            return Consumer.CreateSingleDigest(Normalize(returnValue));
        }
        return consumed;
    }

    protected void AssertResults(Action linq,Action flinq,Action noOptimized,Action zlinq) {
        var expected = CaptureResult(linq);
#if TSAVORITE_LINQ_COUNTER
        using var optimizedScope = global::Seamless.Linq.LinqCounterScope.Start();
#endif
        var flinqResult = CaptureResult(flinq);
#if TSAVORITE_LINQ_COUNTER
        using var noOptimizedScope = global::Seamless.Linq.LinqCounterScope.Start();
#endif
        var noOptimizedResult = CaptureResult(noOptimized);
        var zlinqResult = CaptureResult(zlinq);

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
        var expected = CaptureResult(linq);
#if TSAVORITE_LINQ_COUNTER
        using var optimizedScope = global::Seamless.Linq.LinqCounterScope.Start();
#endif
        var flinqResult = CaptureResult(flinq);
#if TSAVORITE_LINQ_COUNTER
        using var noOptimizedScope = global::Seamless.Linq.LinqCounterScope.Start();
#endif
        var noOptimizedResult = CaptureResult(noOptimized);

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
        Result=MissingResult;
        consumer.BeginCapture();
        action();
        var consumed=consumer.EndCapture();
        return !ReferenceEquals(MissingResult,Result)
            ? Normalize(Result)
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
        if(TryGetKey(value,out var key)&&value is System.Collections.IEnumerable keyedEnumerable) {
            return "{"+Normalize(key)+":"+NormalizeEnumerable(keyedEnumerable)+"}";
        }
        if(value is System.Collections.IDictionary dictionary) {
            return NormalizeDictionary(dictionary);
        }
        if(value is System.Collections.IEnumerable enumerable) {
            return NormalizeEnumerable(enumerable);
        }
        if(value is IFormattable formattable) {
            return formattable.ToString(null,System.Globalization.CultureInfo.InvariantCulture);
        }
        return value.ToString()??string.Empty;
    }

    private static string NormalizeEnumerable(System.Collections.IEnumerable enumerable) {
        var values=new List<string>();
        foreach(var item in enumerable) {
            values.Add(Normalize(item));
        }
        return "["+string.Join(",",values)+"]";
    }

    private static string NormalizeDictionary(System.Collections.IDictionary dictionary) {
        var values=new List<string>();
        foreach(System.Collections.DictionaryEntry entry in dictionary) {
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

    private static System.Reflection.MethodInfo FindBenchmarkMethod(Type type,string methodName) {
        var method=FindInstanceMethodInHierarchy(type,methodName,nonPublic:false);
        if(!HasAttribute(method,"BenchmarkAttribute")) {
            throw new MissingMethodException(type.FullName,methodName+" [Benchmark]");
        }
        return method;
    }

    private static System.Reflection.MethodInfo FindInstanceMethodInHierarchy(Type type,string methodName,bool nonPublic) {
        var flags=System.Reflection.BindingFlags.Instance
            |System.Reflection.BindingFlags.DeclaredOnly
            |(nonPublic?System.Reflection.BindingFlags.NonPublic:System.Reflection.BindingFlags.Public);
        for(var current=type;current is not null;current=current.BaseType!) {
            var method=current.GetMethod(methodName,flags);
            if(method is null) {
                continue;
            }
            if(method.GetParameters().Length!=0) {
                throw new InvalidOperationException($"{current.FullName}.{methodName} must not declare parameters.");
            }
            return method;
        }

        throw new MissingMethodException(type.FullName,methodName);
    }

    private static bool HasAttribute(System.Reflection.MemberInfo member,string attributeName) {
        foreach(var attribute in member.CustomAttributes) {
            if(attribute.AttributeType.Name==attributeName) {
                return true;
            }
        }
        return false;
    }

    protected IEnumerable<int> srcMinusPlus(int scale) {
        var length=CountForComparison(BenchmarkExecutionMode.ScaleElementCount(count*scale));
        for(var a = 0;a<length;a+=2) {
            yield return -a;
            yield return +a;
        }
    }

    protected IEnumerable<int> srcMinusPlusCount(int elementCount) {
        var length=CountForComparison(BenchmarkExecutionMode.ScaleElementCount(elementCount));
        for(var a = 0;a<length;a+=2) {
            yield return -a;
            yield return +a;
        }
    }

    [LinqIntercept(false)]
    protected IEnumerable<int> src(int scale) => System.Linq.Enumerable.AsEnumerable(GetOrCreate(scale,SourceKind.Src,static length => {
        var values = new int[length];
        for(var i = 0;i<values.Length;i++) {
            values[i]=i+1;
        }
        return values;
    }));

    [LinqIntercept(false)]
    protected IEnumerable<int> srcCount(int elementCount) => System.Linq.Enumerable.AsEnumerable(GetOrCreateByLength(elementCount,SourceKind.SrcCount,static length => {
        var values = new int[length];
        for(var i = 0;i<values.Length;i++) {
            values[i]=i+1;
        }
        return values;
    }));

    [LinqIntercept(false)]
    protected IEnumerable<int> src2(int scale) => System.Linq.Enumerable.AsEnumerable(GetOrCreate(scale,SourceKind.Src2,static length => {
        var values = new int[length/2];
        var start = length/2+1;
        for(var i = 0;i<values.Length;i++) {
            values[i]=start+i;
        }
        return values;
    }));

    [LinqIntercept(false)]
    protected IEnumerable<int> src2Count(int elementCount) => System.Linq.Enumerable.AsEnumerable(GetOrCreateByLength(elementCount,SourceKind.Src2Count,static length => {
        var values = new int[length/2];
        var start = length/2+1;
        for(var i = 0;i<values.Length;i++) {
            values[i]=start+i;
        }
        return values;
    }));

    [LinqIntercept(false)]
    protected IEnumerable<int> srcOne(int scale) => System.Linq.Enumerable.AsEnumerable(GetOrCreate(
        new SourceCacheKey(SourceKind.SrcOne,1),
        static _ => [0]));

    private int[] GetOrCreate(int scale,SourceKind kind,Func<int,int[]> factory) {
        var length=CountForComparison(BenchmarkExecutionMode.ScaleElementCount(count*scale));
        return GetOrCreate(new SourceCacheKey(kind,length),factory);
    }

    private int[] GetOrCreateByLength(int elementCount,SourceKind kind,Func<int,int[]> factory) {
        var effectiveLength=CountForComparison(BenchmarkExecutionMode.ScaleElementCount(elementCount));
        return GetOrCreate(new SourceCacheKey(kind,effectiveLength),factory);
    }

    private static int[] GetOrCreate(SourceCacheKey key,Func<int,int[]> factory) {
        lock(SourceCacheSync) {
            if(SourceCache.TryGetValue(key,out var values)) {
                return values;
            }

            values=factory(key.Length);
            SourceCache.Add(key,values);
            return values;
        }
    }

    [LinqIntercept(false)]
    protected IEnumerable<IEnumerable<int>> srcNested(int scale) {
        var side = (int)Math.Sqrt(CountForComparison(BenchmarkExecutionMode.ScaleElementCount(count*scale)));
        return System.Linq.Enumerable.Range(1,side).Select(_ => System.Linq.Enumerable.Range(1,side));
    }

    [LinqIntercept(false)]
    protected IEnumerable<IEnumerable<int>> srcNestedCount(int elementCount) {
        var side = (int)Math.Sqrt(CountForComparison(BenchmarkExecutionMode.ScaleElementCount(elementCount)));
        return System.Linq.Enumerable.Range(1,side).Select(_ => System.Linq.Enumerable.Range(1,side));
    }
}

