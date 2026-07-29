using Benchmark.Dynamic.Core;
using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;

namespace Benchmark.Dynamic.Worker;

public sealed record MetadataReferenceSnapshot(
    ImmutableArray<MetadataReference> References,
    long StartupNanoseconds);

public sealed class MetadataReferenceProvider {
    private readonly Lazy<MetadataReferenceSnapshot> スナップショット;

    public MetadataReferenceProvider() {
        this.スナップショット=new Lazy<MetadataReferenceSnapshot>(作成,LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public MetadataReferenceSnapshot Get() {
        return this.スナップショット.Value;
    }

    private static MetadataReferenceSnapshot 作成() {
        var 開始=Stopwatch.GetTimestamp();
        var パス一覧=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var TPA=AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string??string.Empty;
        foreach(var パス in TPA.Split(Path.PathSeparator,StringSplitOptions.RemoveEmptyEntries)) {
            if(File.Exists(パス)) {
                パス一覧.Add(Path.GetFullPath(パス));
            }
        }
        var 必須アセンブリ=new List<Assembly> {
            typeof(DynamicQueryValue).Assembly,
            typeof(global::Seamless.Linq.GeneratedCodeHelpers).Assembly,
            typeof(global::Seamless.Collections.Persisted.Store).Assembly,
            typeof(global::Seamless.Collections.Tsavorite.Store).Assembly,
            typeof(global::Seamless.FLinq.FLinqIncrementalGenerator).Assembly,
        };
        try {
            必須アセンブリ.Add(Assembly.Load(new AssemblyName("ZLinq")));
        } catch(FileNotFoundException) {
        }
        foreach(var アセンブリ in 必須アセンブリ.Concat(AppDomain.CurrentDomain.GetAssemblies())) {
            if(!アセンブリ.IsDynamic&&!string.IsNullOrWhiteSpace(アセンブリ.Location)&&File.Exists(アセンブリ.Location)) {
                パス一覧.Add(Path.GetFullPath(アセンブリ.Location));
            }
        }
        var 参照=パス一覧
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(パス=>(MetadataReference)MetadataReference.CreateFromFile(パス))
            .ToImmutableArray();
        return new(参照,経過ナノ秒(開始));
    }

    private static long 経過ナノ秒(long 開始) {
        return (long)((Stopwatch.GetTimestamp()-開始)*(1_000_000_000d/Stopwatch.Frequency));
    }
}
