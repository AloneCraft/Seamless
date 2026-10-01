using Target = Benchmark.Audit;
using System.Reflection;

namespace Benchmark.Audit.Tests;

/// <summary>比べる3者(FLinq・ZLinq・手書き理想)のベンチ本体と、その具象型名の接頭辞。</summary>
public sealed record IdealComparisonTab(string Name,string FLinqAssembly,string ZLinqAssembly,string IdealAssembly,IReadOnlyList<string> TypeSuffixes);

/// <summary>ベンチ本体を読み込み、要素数とシードを固定して結果ダイジェストを取る。Worker の取り方と同じ。</summary>
internal static class BenchmarkDigests {
    public static IReadOnlyList<IdealComparisonTab> Tabs { get; } = [
        new("Array","Benchmark.System.Linq.Enumerable.FLinq","Benchmark.System.Linq.Enumerable.ZLinq","Benchmark.System.Linq.Enumerable.Ideal",["Int32","ClassInt32"]),
        new("List","Benchmark.System.Linq.Enumerable.List.FLinq","Benchmark.System.Linq.Enumerable.List.ZLinq","Benchmark.System.Linq.Enumerable.List.Ideal",["Int32","ClassInt32"]),
        new("IEnumerable","Benchmark.System.Linq.Enumerable.IEnumerable.FLinq","Benchmark.System.Linq.Enumerable.IEnumerable.ZLinq","Benchmark.System.Linq.Enumerable.IEnumerable.Ideal",["Int32","ClassInt32"]),
        new("Range","Benchmark.System.Linq.Enumerable.Range.FLinq","Benchmark.System.Linq.Enumerable.Range.ZLinq","Benchmark.System.Linq.Enumerable.Range.Ideal",["Int32"]),
    ];

    public static Assembly Load(string assemblyName) {
        var loaded = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(assembly => assembly.GetName().Name==assemblyName);
        if(loaded is not null) {
            return loaded;
        }
        return Assembly.LoadFrom(AuditedBenchmarks.BenchmarkAssemblyPath(assemblyName));
    }

    public static Type ConcreteType(Assembly assembly,string prefix,string suffix) {
        var name = assembly.GetName().Name+"."+prefix+suffix;
        return assembly.GetType(name,throwOnError: true)!;
    }

    /// <summary>[BenchFact] の付いた公開メソッド名。</summary>
    public static IReadOnlyList<string> BenchmarkMethods(Type type) =>
        type.GetMethods(BindingFlags.Public|BindingFlags.Instance)
            .Where(static method => method.GetCustomAttributes().Any(static attribute => attribute.GetType().Name=="BenchFactAttribute"))
            .Select(static method => method.Name)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

    public static string Digest(Type type,string method,int? seed) {
        var mode = type.Assembly.GetType("Common.BenchmarkExecutionMode",throwOnError: true)!;
        using var count = (IDisposable)mode.GetMethod("UseElementCount",[typeof(int),typeof(string)])!.Invoke(null,[4096,"1"])!;
        IDisposable? seedScope = null;
        if(seed is { } value) {
            seedScope=(IDisposable)mode.GetMethod("UseRandomSourceSeed",[typeof(int)])!.Invoke(null,[value])!;
        }
        var instance = Activator.CreateInstance(type)!;
        try {
            var capture = type.GetMethod("CaptureBenchmarkMethodResult",[typeof(string),typeof(bool),typeof(object[])])!;
            try {
                return (string)capture.Invoke(instance,[method,false,Array.Empty<object?>()])!;
            } catch(TargetInvocationException exception) when(exception.InnerException is not null) {
                return "例外:"+exception.InnerException.GetType().Name+":"+exception.InnerException.Message;
            }
        } finally {
            (instance as IDisposable)?.Dispose();
            seedScope?.Dispose();
        }
    }
}

/// <summary>
/// 手書き理想ループ版(Shared.Linq.Ideal.IdealBase)が FLinq・ZLinq と同じ仕事をしていることの検査。
/// 結果ダイジェスト(件数+sha256+先頭末尾)が一致し、ベンチの定数の写しがずれていないこと。
/// </summary>
public sealed class IdealBase {
    [Fact,Trait("Category","Unit")]
    public void Digest() {
        foreach(var tab in BenchmarkDigests.Tabs) {
            var flinq = BenchmarkDigests.Load(tab.FLinqAssembly);
            var zlinq = BenchmarkDigests.Load(tab.ZLinqAssembly);
            var ideal = BenchmarkDigests.Load(tab.IdealAssembly);
            foreach(var suffix in tab.TypeSuffixes) {
                var flinqType = BenchmarkDigests.ConcreteType(flinq,"FLinq",suffix);
                var zlinqType = BenchmarkDigests.ConcreteType(zlinq,"ZLinq",suffix);
                var idealType = BenchmarkDigests.ConcreteType(ideal,"Ideal",suffix);
                {
                    //理想版と FLinq のベンチ(行)が過不足なく対応していること
                    Assert.Equal(BenchmarkDigests.BenchmarkMethods(flinqType),BenchmarkDigests.BenchmarkMethods(idealType));
                }
                {
                    //シードなし・シード1の両方で、理想版の結果が ZLinq と一致すること(FLinq との不一致は別に報告する)
                    var mismatches = new List<string>();
                    foreach(var method in BenchmarkDigests.BenchmarkMethods(idealType)) {
                        foreach(int? seed in new int?[] { null,1 }) {
                            var expected = BenchmarkDigests.Digest(zlinqType,method,seed);
                            var actual = BenchmarkDigests.Digest(idealType,method,seed);
                            if(!expected.StartsWith("count=",StringComparison.Ordinal)) {
                                mismatches.Add(tab.Name+" "+suffix+" "+method+" seed="+seed+" の ZLinq が結果を返しません: "+expected);
                            }
                            if(expected!=actual) {
                                mismatches.Add(tab.Name+" "+suffix+" "+method+" seed="+seed+"\n  ZLinq="+expected+"\n  Ideal="+actual);
                            }
                        }
                    }
                    Assert.True(mismatches.Count==0,"理想版の結果が ZLinq と異なります:\n"+string.Join("\n",mismatches));
                }
            }
        }
    }

    [Fact,Trait("Category","Unit")]
    public void OptimizationDigest() {
        {
            //複合連鎖の理想版の結果が、同じ型名の全最適化版(FLinq)と一致すること(シードなし・シード1)
            var all = BenchmarkDigests.Load("Benchmark.Optimization.All");
            var ideal = BenchmarkDigests.Load("Benchmark.Optimization.Ideal");
            var mismatches = new List<string>();
            var compared = 0;
            foreach(var idealType in ideal.GetTypes().Where(static type => type.IsClass&&!type.IsAbstract&&type.Namespace?.StartsWith("Benchmark.Optimization.",StringComparison.Ordinal)==true)) {
                var allType = all.GetType(idealType.FullName!,throwOnError: false);
                if(allType is null) {
                    mismatches.Add(idealType.FullName+" が全最適化版にありません");
                    continue;
                }
                var methods = BenchmarkDigests.BenchmarkMethods(idealType);
                if(!methods.SequenceEqual(BenchmarkDigests.BenchmarkMethods(allType))) {
                    mismatches.Add(idealType.FullName+" のベンチが全最適化版と対応していません");
                    continue;
                }
                foreach(var method in methods) {
                    var known = KnownExceptions.All.Any(item => item.Invariant==AuditInvariant.結果不一致&&item.DeclaringType==idealType.FullName&&item.Method==method);
                    var differs = false;
                    foreach(int? seed in new int?[] { null,1 }) {
                        var expected = BenchmarkDigests.Digest(allType,method,seed);
                        var actual = BenchmarkDigests.Digest(idealType,method,seed);
                        compared++;
                        if(expected!=actual) {
                            differs=true;
                            if(!known) {
                                mismatches.Add(idealType.FullName+"."+method+" seed="+seed+"\n  All="+expected+"\n  Ideal="+actual);
                            }
                        }
                    }
                    if(known&&!differs) {
                        mismatches.Add(idealType.FullName+"."+method+" は一致するようになりました。KnownExceptions から消してください");
                    }
                }
            }
            Assert.True(compared>=150,"比較した行が少なすぎます: "+compared);
            Assert.True(mismatches.Count==0,"複合連鎖の理想版の結果が全最適化版と異なります:\n"+string.Join("\n",mismatches));
        }
    }

    [Fact,Trait("Category","Unit")]
    public void NoLinqCalls() {
        {
            //理想版の本体(Shared.Linq.Ideal)が System.Linq・ZLinq・Seamless の LINQ 実装を呼ばないこと
            using var catalog = new Target.MetadataCatalog();
            var auditor = new Target.IlAuditor(catalog);
            var calls = new List<string>();
            foreach(var name in BenchmarkDigests.Tabs.Select(static tab => tab.IdealAssembly).Append("Benchmark.Optimization.Ideal")) {
                var assembly = catalog.Load(AuditedBenchmarks.BenchmarkAssemblyPath(name));
                calls.AddRange(auditor.FindCalls(
                    assembly,
                    static ns => ns.StartsWith("Shared.Linq.Ideal",StringComparison.Ordinal),
                    static type => type.StartsWith("System.Linq.",StringComparison.Ordinal)||type.StartsWith("ZLinq",StringComparison.Ordinal)||type.StartsWith("Seamless.",StringComparison.Ordinal)));
            }
            Assert.True(calls.Count==0,"理想版が LINQ 実装を呼んでいます:\n"+string.Join("\n",calls));
        }
    }

    [Fact,Trait("Category","Unit")]
    public void Constants() {
        {
            //理想版の基底の定数が Shared/Linq.Enumerable/Base.cs と同じ値であること
            var original = BenchmarkDigests.Load("Benchmark.System.Linq.Enumerable.FLinq").GetType("Shared.Linq.Base`1",throwOnError: true)!;
            var copy = BenchmarkDigests.Load("Benchmark.System.Linq.Enumerable.Ideal").GetType("Shared.Linq.Ideal.IdealBase`1",throwOnError: true)!;
            static IReadOnlyList<string> Literals(Type type) =>
                type.GetFields(BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Static)
                    .Where(static field => field.IsLiteral)
                    .Select(static field => field.Name+"="+field.GetRawConstantValue())
                    .Order(StringComparer.Ordinal)
                    .ToArray();
            Assert.Equal(Literals(original),Literals(copy));
            //Range 版の定数も Shared/Linq.Range/Range_start_count.cs と同じであること
            var rangeOriginal = BenchmarkDigests.Load("Benchmark.System.Linq.Enumerable.Range.FLinq").GetType("Shared.Linq.Range_start_count",throwOnError: true)!;
            var rangeCopy = BenchmarkDigests.Load("Benchmark.System.Linq.Enumerable.Range.Ideal").GetType("Shared.Linq.IdealRange.Range_start_count",throwOnError: true)!;
            Assert.Equal(Literals(rangeOriginal),Literals(rangeCopy));
        }
    }
}
