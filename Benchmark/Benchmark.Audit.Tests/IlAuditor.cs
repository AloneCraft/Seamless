using Target = Benchmark.Audit;

namespace Benchmark.Audit.Tests;

/// <summary>監査対象のベンチ本体を1度だけ読み、全テストで共有する。</summary>
public sealed class AuditedBenchmarks:IDisposable {
    /// <summary>メモリ上4種の FLinq ベンチ本体と、全最適化の複合連鎖ベンチ。</summary>
    public static IReadOnlyList<string> AssemblyNames { get; } = [
        "Benchmark.System.Linq.Enumerable.FLinq",
        "Benchmark.System.Linq.Enumerable.List.FLinq",
        "Benchmark.System.Linq.Enumerable.IEnumerable.FLinq",
        "Benchmark.System.Linq.Enumerable.Range.FLinq",
        "Benchmark.Optimization.All",
    ];

    private readonly Target.MetadataCatalog catalog = new();

    public AuditedBenchmarks() {
        var auditor = new Target.IlAuditor(this.catalog);
        var rows = new List<Target.AuditRow>();
        foreach(var name in AssemblyNames) {
            rows.AddRange(auditor.Audit(this.catalog.Load(BenchmarkAssemblyPath(name))));
        }
        this.Rows=rows;
        this.MemoryRows=rows.Where(static row => row.Source is Target.SourceKind.Array or Target.SourceKind.List or Target.SourceKind.IEnumerable or Target.SourceKind.Range).ToArray();
    }

    internal IReadOnlyList<Target.AuditRow> Rows { get; }
    internal IReadOnlyList<Target.AuditRow> MemoryRows { get; }

    /// <summary>テストと同じ構成(Debug/Release)でビルドされたベンチ本体の出力を、リポジトリのルートから探す。</summary>
    internal static string BenchmarkAssemblyPath(string name) {
        var baseDirectory = AppContext.BaseDirectory;
        var configuration = "Debug";
        if(baseDirectory.Contains(Path.DirectorySeparatorChar+"Release"+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) {
            configuration="Release";
        }
        var directory = new DirectoryInfo(baseDirectory);
        while(directory is not null&&!File.Exists(Path.Combine(directory.FullName,"Seamless.slnx"))) {
            directory=directory.Parent;
        }
        if(directory is null) {
            throw new DirectoryNotFoundException("Seamless.slnx のあるリポジトリのルートが見つかりません: "+baseDirectory);
        }
        var path = Path.Combine(directory.FullName,"Benchmark",name,"bin",configuration,"net10.0",name+".dll");
        if(!File.Exists(path)) {
            throw new FileNotFoundException("監査対象のベンチ本体がありません。先にビルドしてください。",path);
        }
        return path;
    }

    public void Dispose() => this.catalog.Dispose();
}

/// <summary>
/// FLinq の置き換え結果に関する不変条件。違反は <see cref="KnownExceptions"/> に理由付きで載せたものだけ許す。
/// </summary>
public sealed class IlAuditor(AuditedBenchmarks benchmarks):IClassFixture<AuditedBenchmarks> {
    /// <summary>件数が O(1) で分かるかの確認。System.Linq 自身も行う確認で、要素を列挙しないので許す。</summary>
    private static readonly HashSet<string> nonEnumeratingLinq = new(StringComparer.Ordinal) {
        "System.Linq.Enumerable::TryGetNonEnumeratedCount",
    };

    [Fact,Trait("Category","Unit")]
    public void Audit() {
        {
            //監査対象の行が揃っていること(ベンチ定義の変更で監査が空振りしていない)
            Assert.Equal(97+97+97+81,benchmarks.Rows.Count(static row => row.Assembly.StartsWith("Benchmark.System.",StringComparison.Ordinal)));
            Assert.True(benchmarks.MemoryRows.Count(static row => row.Assembly=="Benchmark.Optimization.All")>=80);
        }
        {
            //置き換え漏れが既知例外以外に無いこと
            var violations = benchmarks.MemoryRows.Where(static row => !row.Intercepted||row.ImplCalledDirectly).ToArray();
            AssertOnlyKnown(AuditInvariant.置き換え漏れ,violations);
        }
        {
            //置き換え後に要素を列挙する外部LINQ呼び出しが既知例外以外に無いこと(置き換え漏れの行は上で扱う)
            var violations = benchmarks.MemoryRows.Where(static row => row.Intercepted&&!row.ImplCalledDirectly&&row.ExternalLinqTargets.Any(static target => !nonEnumeratingLinq.Contains(target))).ToArray();
            AssertOnlyKnown(AuditInvariant.外部LINQ,violations);
        }
        {
            //展開不可(FLINQ002)で例外を投げるだけの置き換えメソッドが無いこと
            var stubs = benchmarks.MemoryRows.Where(static row => row.ExpansionStub).Select(Describe).ToArray();
            Assert.True(stubs.Length==0,"展開不可の置き換えメソッドがあります:\n"+string.Join("\n",stubs));
        }
    }

    private static void AssertOnlyKnown(AuditInvariant invariant,IReadOnlyList<Target.AuditRow> violations) {
        var known = KnownExceptions.All.Where(item => item.Invariant==invariant).ToArray();
        var unexpected = violations
            .Where(row => !known.Any(item => item.Assembly==row.Assembly&&item.DeclaringType==row.DeclaringType&&item.Method==row.Method))
            .Select(Describe)
            .ToArray();
        var resolved = known
            .Where(item => !violations.Any(row => item.Assembly==row.Assembly&&item.DeclaringType==row.DeclaringType&&item.Method==row.Method))
            .Select(static item => item.Assembly+" | "+item.DeclaringType+" | "+item.Method)
            .ToArray();
        Assert.True(unexpected.Length==0,invariant+" の新しい違反があります:\n"+string.Join("\n",unexpected));
        Assert.True(resolved.Length==0,invariant+" の既知例外のうち解消済みの項目があります。KnownExceptions から消してください:\n"+string.Join("\n",resolved));
    }

    private static string Describe(Target.AuditRow row) =>
        row.Assembly+" | "+row.DeclaringType+" | "+row.Method+" | 外部LINQ="+string.Join(",",row.ExternalLinqTargets);
}
