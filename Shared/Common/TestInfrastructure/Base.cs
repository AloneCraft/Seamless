namespace Common;

[AttributeUsage(AttributeTargets.Method,AllowMultiple=false)]
public sealed class LinqInterceptAttribute(bool optimizeLinq):Attribute {
    public bool OptimizeLinq { get; } = optimizeLinq;
}

[AttributeUsage(AttributeTargets.Class)]
public sealed class BenchmarkTestIterationsAttribute(int count):Attribute {
    public int Count { get; } = count;
}

[Flags]
public enum BenchmarkSourceUsage {
    None = 0,
    Src = 1,
    Src2 = 2,
    SrcOne = 4,
    Nested = 8,
    SourceShape = 16,
    All = Src|Src2|SrcOne|Nested|SourceShape,
}

[AttributeUsage(AttributeTargets.Method,AllowMultiple=false)]
public sealed class BenchmarkSourceUsageAttribute(BenchmarkSourceUsage usage):Attribute {
    public BenchmarkSourceUsage Usage { get; } = usage;
}

public static class BenchmarkExecutionMode {
    private const string TestModeKey = "Common.TestMode";
    private const string ElementScaleKey = "SEAMLESS_BENCHMARK_ELEMENT_SCALE";
    private const string ElementCountKey = "SEAMLESS_BENCHMARK_ELEMENT_COUNT";
    private const string PreparedElementCountsKey = "SEAMLESS_BENCHMARK_PREPARED_ELEMENT_COUNTS";
    private const string RandomSourceSeedKey = "SEAMLESS_BENCHMARK_RANDOM_SOURCE_SEED";
    private static readonly AsyncLocal<int?> ComparisonElementLimit = new();
    private static readonly AsyncLocal<int?> ElementCountOverride = new();
    private static readonly AsyncLocal<double?> ElementScaleOverride = new();
    private static readonly AsyncLocal<int?> RandomSourceSeedOverride = new();

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

    public static IDisposable UseElementCount(int elementCount,string? elementScale) {
        var previousCount = ElementCountOverride.Value;
        var previousScale = ElementScaleOverride.Value;
        ElementCountOverride.Value=elementCount>0 ? elementCount : null;
        ElementScaleOverride.Value=ReadElementScale(elementScale);
        return new ElementCountScope(previousCount,previousScale);
    }

    public static IDisposable UseRandomSourceSeed(int seed) {
        var previousSeed = RandomSourceSeedOverride.Value;
        RandomSourceSeedOverride.Value=seed;
        return new RandomSourceSeedScope(previousSeed);
    }

    internal static int? ReadRandomSourceSeed() {
        if(RandomSourceSeedOverride.Value is { } scopedSeed) {
            return scopedSeed;
        }

        var text = Environment.GetEnvironmentVariable(RandomSourceSeedKey);
        return int.TryParse(text,System.Globalization.NumberStyles.Integer,System.Globalization.CultureInfo.InvariantCulture,out var seed)
            ? seed
            : null;
    }

    internal static int LimitForComparison(int elementCount) =>
        ComparisonElementLimit.Value is > 0 and var limit
            ? Math.Min(elementCount,limit)
            : elementCount;

    internal static int ScaleElementCount(int elementCount) {
        if(elementCount<=0) {
            return elementCount;
        }
        if(ElementCountOverride.Value is { } scopedCount) {
            return scopedCount;
        }
        if(ReadElementCount() is { } absoluteCount) {
            return absoluteCount;
        }

        var scaled = elementCount*(ElementScaleOverride.Value??ReadElementScale());
        if(!double.IsFinite(scaled)||scaled<=1) {
            return 1;
        }

        return scaled>=int.MaxValue
            ? int.MaxValue
            : Math.Max(1,(int)Math.Round(scaled,MidpointRounding.AwayFromZero));
    }

    internal static IReadOnlyList<int> ReadPreparedElementCounts() {
        var text = Environment.GetEnvironmentVariable(PreparedElementCountsKey);
        if(string.IsNullOrWhiteSpace(text)) {
            return [];
        }

        return text
            .Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries)
            .Select(static value => int.TryParse(value,System.Globalization.NumberStyles.Integer,System.Globalization.CultureInfo.InvariantCulture,out var count) ? count : 0)
            .Where(static count => count>0)
            .Distinct()
            .Order()
            .ToArray();
    }

    private static double ReadElementScale() =>
        ReadElementScale(Environment.GetEnvironmentVariable(ElementScaleKey));

    private static double ReadElementScale(string? text) {
        return double.TryParse(text,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var scale)
            &&double.IsFinite(scale)
            &&scale>0
            ? scale
            : 1;
    }

    private static int? ReadElementCount() {
        var text = Environment.GetEnvironmentVariable(ElementCountKey);
        return int.TryParse(text,System.Globalization.NumberStyles.Integer,System.Globalization.CultureInfo.InvariantCulture,out var count)
            &&count>0
            ? count
            : null;
    }

    private sealed class ElementCountScope(int? previousCount,double? previousScale):IDisposable {
        public void Dispose() {
            ElementCountOverride.Value=previousCount;
            ElementScaleOverride.Value=previousScale;
        }
    }

    private sealed class RandomSourceSeedScope(int? previousSeed):IDisposable {
        public void Dispose() {
            RandomSourceSeedOverride.Value=previousSeed;
        }
    }
}

public sealed class Consumer {
    private const int DigestSampleCount = 8;
    private readonly global::BenchmarkDotNet.Engines.Consumer inner = new();
    private readonly List<object?> captured = [];
    private bool captureEnabled;
    private DigestCapture? digestCapture;

    public void Consume<T>(T value) {
        if(this.captureEnabled) {
            this.captured.Add(value);
        }

        this.digestCapture?.Add(value);
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

    internal void BeginDigestCapture(Func<object?,string> normalize) {
        if(this.digestCapture is not null) {
            throw new InvalidOperationException("Digest capture is already active.");
        }

        this.digestCapture=new DigestCapture(normalize);
    }

    internal string EndDigestCapture() {
        var capture= this.digestCapture??throw new InvalidOperationException("Digest capture is not active.");
        this.digestCapture=null;
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
            this.count++;
            if(this.first.Count<DigestSampleCount) {
                this.first.Add(normalized);
            }
            if(this.last.Count==DigestSampleCount) {
                this.last.Dequeue();
            }
            this.last.Enqueue(normalized);

            var bytes=System.Text.Encoding.UTF8.GetBytes(normalized);
            var lengthBytes=new byte[4];
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(lengthBytes,bytes.Length);
            this.hash.AppendData(lengthBytes);
            this.hash.AppendData(bytes);
        }

        public string End() {
            var hashText=Convert.ToHexString(this.hash.GetHashAndReset());
            this.hash.Dispose();
            return "count="+this.count
                +";sha256="+hashText
                +";first="+Format(this.first)
                +";last="+Format(this.last);
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

public readonly record struct SourceCacheKey(SourceKind Kind,int Length,int? RandomSeed);

public class Base {
    private const int BenchmarkDefaultCount = 10_000_000;
    private const int TestDefaultCount = 80_000;
    private static readonly object MissingResult = new();
    private static readonly object SourceCacheSync = new();
    private static readonly Dictionary<SourceCacheKey,int[]> SourceCache = [];
    private static readonly Dictionary<SourceCacheKey,List<int>> ListSourceCache = [];

    protected readonly Consumer consumer = new();
    private protected object? Result;
    private protected readonly int count;

    protected Base(int count = -1) =>
        this.count=count>=0?count:(BenchmarkExecutionMode.IsTestMode?TestDefaultCount:BenchmarkDefaultCount);

    [GlobalSetup]
    public void PrepareBenchmarkSources() {
#if SYSTEM_RANGE
        return;
#else
        var preparedElementCounts = BenchmarkExecutionMode.ReadPreparedElementCounts();
        foreach(var elementCount in preparedElementCounts) {
            _ = this.srcCount(elementCount);
            _ = this.src2Count(elementCount);
        }
        _ = this.srcOne(1);
#endif
    }

    private protected int CountForTestTiming(int benchmarkCount) =>
        BenchmarkExecutionMode.ScaleElementCount(BenchmarkExecutionMode.IsTestMode? this.count :benchmarkCount);

    private protected int CountForComparison(int elementCount) =>
        BenchmarkExecutionMode.LimitForComparison(elementCount);

    protected int EffectiveSourceCount(int elementCount) =>
        this.CountForComparison(this.CountForTestTiming(elementCount));

    public string CaptureBenchmarkMethodResult(string methodName,bool invokeImpl) {
        if(string.IsNullOrWhiteSpace(methodName)) {
            throw new ArgumentException("Method name is required.",nameof(methodName));
        }

        var method=invokeImpl
            ? FindInstanceMethodInHierarchy(this.GetType(),methodName+"_Impl",nonPublic:true)
            : FindBenchmarkMethod(this.GetType(),methodName);

        this.Result=MissingResult;
        this.consumer.BeginDigestCapture(Normalize);
        object? returnValue=null;
        Exception? invocationError=null;
        var consumed=string.Empty;
        try {
            returnValue=method.Invoke(this,null);
        } catch(System.Reflection.TargetInvocationException ex) when(ex.InnerException is not null) {
            invocationError=ex.InnerException;
        } finally {
            consumed=this.consumer.EndDigestCapture();
        }

        if(invocationError is not null) {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(invocationError).Throw();
            throw new InvalidOperationException("Unreachable.");
        }

        if(!ReferenceEquals(MissingResult,this.Result)) {
            return Consumer.CreateSingleDigest(Normalize(this.Result));
        }
        if(method.ReturnType!=typeof(void)) {
            return Consumer.CreateSingleDigest(Normalize(returnValue));
        }
        return consumed;
    }

    protected void AssertResults(Action linq,Action flinq,Action noOptimized,Action zlinq) {
        var expected = this.CaptureResult(linq);
#if TSAVORITE_LINQ_COUNTER
        using var optimizedScope = global::Seamless.Linq.LinqCounterScope.Start();
#endif
        var flinqResult = this.CaptureResult(flinq);
#if TSAVORITE_LINQ_COUNTER
        using var noOptimizedScope = global::Seamless.Linq.LinqCounterScope.Start();
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
        using var optimizedScope = global::Seamless.Linq.LinqCounterScope.Start();
#endif
        var flinqResult = this.CaptureResult(flinq);
#if TSAVORITE_LINQ_COUNTER
        using var noOptimizedScope = global::Seamless.Linq.LinqCounterScope.Start();
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
        var consumed= this.consumer.EndCapture();
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
        var length= this.CountForComparison(BenchmarkExecutionMode.ScaleElementCount(this.count*scale));
        for(var a = 0;a<length;a+=2) {
            yield return -a;
            yield return +a;
        }
    }

    protected IEnumerable<int> srcMinusPlusCount(int elementCount) {
        var length= this.CountForComparison(BenchmarkExecutionMode.ScaleElementCount(elementCount));
        for(var a = 0;a<length;a+=2) {
            yield return -a;
            yield return +a;
        }
    }

#if SYSTEM_RANGE
    [LinqIntercept(false)]
    protected IEnumerable<int> src(int scale) =>
        System.Linq.Enumerable.Range(1,this.CountForComparison(BenchmarkExecutionMode.ScaleElementCount(this.count*scale)));

    [LinqIntercept(false)]
    protected IEnumerable<int> srcCount(int elementCount) =>
        System.Linq.Enumerable.Range(1,this.CountForComparison(BenchmarkExecutionMode.ScaleElementCount(elementCount)));

    [LinqIntercept(false)]
    protected IEnumerable<int> src2(int scale) {
        var length = this.CountForComparison(BenchmarkExecutionMode.ScaleElementCount(this.count*scale));
        return System.Linq.Enumerable.Range(length/2+1,length/2);
    }

    [LinqIntercept(false)]
    protected IEnumerable<int> src2Count(int elementCount) {
        var length = this.CountForComparison(BenchmarkExecutionMode.ScaleElementCount(elementCount));
        return System.Linq.Enumerable.Range(length/2+1,length/2);
    }

    [LinqIntercept(false)]
    protected IEnumerable<int> srcOne(int scale) => System.Linq.Enumerable.Range(0,1);
#elif SYSTEM_LIST
    [LinqIntercept(false)]
    protected List<int> src(int scale) => this.GetOrCreateList(scale,SourceKind.Src,CreateSourceList);

    [LinqIntercept(false)]
    protected List<int> srcCount(int elementCount) => this.GetOrCreateListByLength(elementCount,SourceKind.SrcCount,CreateSourceList);

    [LinqIntercept(false)]
    protected List<int> src2(int scale) => this.GetOrCreateList(scale,SourceKind.Src2,CreateSecondSourceList);

    [LinqIntercept(false)]
    protected List<int> src2Count(int elementCount) => this.GetOrCreateListByLength(elementCount,SourceKind.Src2Count,CreateSecondSourceList);

    [LinqIntercept(false)]
    protected List<int> srcOne(int scale) =>
        this.GetOrCreateListByLength(1,SourceKind.SrcOne,static _ => [0]);
#else
    [LinqIntercept(false)]
    protected int[] src(int scale) => this.GetOrCreate(scale,SourceKind.Src,CreateSourceArray);

    [LinqIntercept(false)]
    protected int[] srcCount(int elementCount) => this.GetOrCreateByLength(elementCount,SourceKind.SrcCount,CreateSourceArray);

    [LinqIntercept(false)]
    protected int[] src2(int scale) => this.GetOrCreate(scale,SourceKind.Src2,CreateSecondSourceArray);

    [LinqIntercept(false)]
    protected int[] src2Count(int elementCount) => this.GetOrCreateByLength(elementCount,SourceKind.Src2Count,CreateSecondSourceArray);

    [LinqIntercept(false)]
    protected int[] srcOne(int scale) => GetOrCreate(
        new SourceCacheKey(SourceKind.SrcOne,1,BenchmarkExecutionMode.ReadRandomSourceSeed()),
        static _ => [0]);
#endif

    private int[] GetOrCreate(int scale,SourceKind kind,Func<int,int[]> factory) {
        var length= this.CountForComparison(BenchmarkExecutionMode.ScaleElementCount(this.count*scale));
        return GetOrCreate(new SourceCacheKey(kind,length,BenchmarkExecutionMode.ReadRandomSourceSeed()),factory);
    }

    private int[] GetOrCreateByLength(int elementCount,SourceKind kind,Func<int,int[]> factory) {
        var effectiveLength= this.CountForComparison(BenchmarkExecutionMode.ScaleElementCount(elementCount));
        return GetOrCreate(new SourceCacheKey(kind,effectiveLength,BenchmarkExecutionMode.ReadRandomSourceSeed()),factory);
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

    private List<int> GetOrCreateList(int scale,SourceKind kind,Func<int,List<int>> factory) {
        var length= this.CountForComparison(BenchmarkExecutionMode.ScaleElementCount(this.count*scale));
        return GetOrCreateList(new SourceCacheKey(kind,length,BenchmarkExecutionMode.ReadRandomSourceSeed()),factory);
    }

    private List<int> GetOrCreateListByLength(int elementCount,SourceKind kind,Func<int,List<int>> factory) {
        var effectiveLength= this.CountForComparison(BenchmarkExecutionMode.ScaleElementCount(elementCount));
        return GetOrCreateList(new SourceCacheKey(kind,effectiveLength,BenchmarkExecutionMode.ReadRandomSourceSeed()),factory);
    }

    private static List<int> GetOrCreateList(SourceCacheKey key,Func<int,List<int>> factory) {
        lock(SourceCacheSync) {
            if(ListSourceCache.TryGetValue(key,out var values)) {
                return values;
            }

            values=factory(key.Length);
            ListSourceCache.Add(key,values);
            return values;
        }
    }

    private static int[] CreateSourceArray(int length) =>
        CreateIntegerRange(1,length,SourceKind.Src);

    private static int[] CreateSecondSourceArray(int length) =>
        CreateIntegerRange(length/2+1,length/2,SourceKind.Src2);

    private static List<int> CreateSourceList(int length) =>
        CreateSourceArray(length).ToList();

    private static List<int> CreateSecondSourceList(int length) =>
        CreateSecondSourceArray(length).ToList();

    private static int[] CreateIntegerRange(int start,int length,SourceKind kind) {
        var values = new int[length];
        for(var i = 0;i<values.Length;i++) {
            values[i]=start+i;
        }
        if(BenchmarkExecutionMode.ReadRandomSourceSeed() is { } seed&&values.Length>1) {
            Shuffle(values,CreateRandomSeed(seed,kind,start,length));
        }
        return values;
    }

    private static int CreateRandomSeed(int seed,SourceKind kind,int start,int length) {
        unchecked {
            var value=seed;
            value=(value*397)^(int)kind;
            value=(value*397)^start;
            value=(value*397)^length;
            return value;
        }
    }

    private static void Shuffle(int[] values,int seed) {
        var random = new Random(seed);
        for(var i=values.Length-1;i>0;i--) {
            var j = random.Next(i+1);
            (values[i],values[j])=(values[j],values[i]);
        }
    }

    [LinqIntercept(false)]
    protected IEnumerable<IEnumerable<int>> srcNested(int scale) {
        var side = (int)Math.Sqrt(this.CountForComparison(BenchmarkExecutionMode.ScaleElementCount(this.count*scale)));
        return System.Linq.Enumerable.Range(1,side).Select(_ => System.Linq.Enumerable.Range(1,side));
    }

    [LinqIntercept(false)]
    protected IEnumerable<IEnumerable<int>> srcNestedCount(int elementCount) {
        var side = (int)Math.Sqrt(this.CountForComparison(BenchmarkExecutionMode.ScaleElementCount(elementCount)));
        return System.Linq.Enumerable.Range(1,side).Select(_ => System.Linq.Enumerable.Range(1,side));
    }
}

