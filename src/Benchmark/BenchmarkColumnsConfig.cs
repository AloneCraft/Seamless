using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Reports;
using System.Reflection;

namespace Tsavorite.Benchmark.Benchmarks;

public sealed class BenchmarkColumnsConfig:ManualConfig {
    public BenchmarkColumnsConfig() {
        this.AddColumnProvider(new PreferredFirstColumnProvider());
    }

    private sealed class PreferredFirstColumnProvider:IColumnProvider {
        private static readonly string[] Preferred = ["Method","Mean","Allocated","Gen0","Gen1","Gen2"];

        public IEnumerable<IColumn> GetColumns(Summary summary) {
            var columns = DefaultColumnProviders.Instance
                .SelectMany(p => p.GetColumns(summary))
                .ToList();
            var preferred = new List<IColumn>();

            foreach(var name in Preferred) {
                preferred.AddRange(columns.Where(c => string.Equals(c.ColumnName,name,StringComparison.OrdinalIgnoreCase)));
            }

            var rest = columns.Where(c => !preferred.Contains(c)).ToList();
            return preferred.Concat(rest);
        }
    }
}

internal static class BenchmarkOutputLayout {
    public const string AllCategory = "All";

    private static readonly string[] categories = ["Dictionary","HashSet","List"];
    private static readonly Lazy<BenchmarkMetadata> metadata = new(LoadMetadata);

    public static IReadOnlyList<string> KnownCategories => categories;

    public static IConfig CreateRuntimeConfig(string category) {
#if DEBUG
        IConfig baseConfig = new DebugInProcessConfig();
#else
        IConfig baseConfig = DefaultConfig.Instance;
#endif
        return ManualConfig.Create(baseConfig)
            .WithArtifactsPath(GetCategoryRoot(category));
    }

    public static bool ShouldSplitByCategory(string[] args) {
        if(TryGetCategoryFromArgs(args) is not null) {
            return false;
        }

        foreach(var arg in args) {
            if(string.Equals(arg,"--list",StringComparison.OrdinalIgnoreCase)
                ||string.Equals(arg,"-l",StringComparison.OrdinalIgnoreCase)
                ||string.Equals(arg,"--help",StringComparison.OrdinalIgnoreCase)
                ||string.Equals(arg,"-h",StringComparison.OrdinalIgnoreCase)
                ||string.Equals(arg,"-?",StringComparison.OrdinalIgnoreCase)
                ||string.Equals(arg,"--info",StringComparison.OrdinalIgnoreCase)
                ||string.Equals(arg,"--version",StringComparison.OrdinalIgnoreCase)) {
                return false;
            }
        }

        return true;
    }

    public static string[] WithCategoryFilter(string[] args,string category) {
        return args.Concat(["--filter",$"*{category}*"]).ToArray();
    }

    public static string? TryGetCategoryFromArgs(IEnumerable<string> args) {
        string? filter = null;
        using var enumerator = args.GetEnumerator();
        while(enumerator.MoveNext()) {
            var arg = enumerator.Current;
            if(string.Equals(arg,"--filter",StringComparison.OrdinalIgnoreCase)) {
                if(enumerator.MoveNext()) {
                    filter=enumerator.Current;
                }
                break;
            }

            if(arg.StartsWith("--filter=",StringComparison.OrdinalIgnoreCase)) {
                filter=arg[9..];
                break;
            }
        }

        if(string.IsNullOrWhiteSpace(filter)) {
            return null;
        }

        foreach(var category in categories) {
            if(filter.Contains(category,StringComparison.OrdinalIgnoreCase)) {
                return category;
            }
        }

        return null;
    }

    public static string CreateCategoryWorkspaceRoot(string category) {
        var path = Path.Combine(GetCategoryRoot(category),"work",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    public static string GetCategoryRoot(string category) {
        var current = metadata.Value;
        var path = Path.Combine(
            current.ProjectDir,
            "Benchmark",
            current.BaseConfiguration,
            current.TargetLabel,
            current.FrameworkLabel,
            category);
        Directory.CreateDirectory(path);
        return path;
    }

    private static BenchmarkMetadata LoadMetadata() {
        var assembly = typeof(BenchmarkOutputLayout).Assembly;
        return new BenchmarkMetadata(
            ProjectDir: GetAssemblyMetadata(assembly,"BenchmarkProjectDir")??Environment.CurrentDirectory,
            TargetLabel: GetResolvedGarnetPackageVersion(assembly)
                ??GetAssemblyMetadata(assembly,"SeamlessBenchmarkOutputLabel")
                ??"unknown-target",
            BaseConfiguration: GetAssemblyMetadata(assembly,"SeamlessBaseConfiguration")??"Debug",
            FrameworkLabel: GetAssemblyMetadata(assembly,"SeamlessBenchmarkOutputFrameworkLabel")??"net10");
    }

    private static string? GetResolvedGarnetPackageVersion(Assembly benchmarkAssembly) {
        foreach(var reference in benchmarkAssembly.GetReferencedAssemblies()) {
            if(reference.Name is null||!reference.Name.StartsWith("Tsavorite",StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            try {
                var referencedAssembly = Assembly.Load(reference);
                var packageVersion = GetAssemblyMetadata(referencedAssembly,"TsavoriteGarnetPackageVersion");
                if(!string.IsNullOrWhiteSpace(packageVersion)) {
                    return packageVersion;
                }
            }
            catch {
            }
        }

        return null;
    }

    private static string? GetAssemblyMetadata(Assembly assembly,string key) {
        return assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => string.Equals(attribute.Key,key,StringComparison.Ordinal))
            ?.Value;
    }

    private sealed record BenchmarkMetadata(string ProjectDir,string TargetLabel,string BaseConfiguration,string FrameworkLabel);
}
