using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using Microsoft.VisualBasic.FileIO;

internal static class Program {
    public static void Main(string[] args) {
        SetCurrentDirectoryToProject();

        var benchmarkAssembly = typeof(global::Labo.Shared.Linq.Enumerable.NoopLinqInterceptAttribute).Assembly;
        if(IsListJsonMode(args)) {
            WriteBenchmarkListJson(benchmarkAssembly);
            return;
        }

        var singleFileMode = IsSingleFileMode(args);
        if(args.Length==0&&!Console.IsInputRedirected) {
            var selection = PromptInteractiveSelection(benchmarkAssembly);
            args=selection.Args;
            singleFileMode=singleFileMode||selection.SingleFileMode;
        }

        args=RemoveSingleFileSwitch(args);
        var category = BenchmarkOutputLayout.TryGetCategoryFromArgs(args)??BenchmarkOutputLayout.AllCategory;
        BenchmarkSwitcher
            .FromAssembly(benchmarkAssembly)
            .Run(args,BenchmarkOutputLayout.CreateRuntimeConfig(category));

        if(singleFileMode) {
            WriteSingleFileReport();
        }
    }

    private static void SetCurrentDirectoryToProject() {
        for(var directory = new DirectoryInfo(AppContext.BaseDirectory);directory is not null;directory=directory.Parent) {
            if(directory.GetFiles("*.csproj").Length>0) {
                Directory.SetCurrentDirectory(directory.FullName);
                return;
            }
        }
    }

    private static InteractiveSelection PromptInteractiveSelection(Assembly benchmarkAssembly) {
        Console.WriteLine("=== ベンチマーク実行モード ===");
        Console.WriteLine("1: All benchmark");
        Console.WriteLine("2: Select benchmark");
        Console.WriteLine("3: All benchmark (single file report)");
        Console.WriteLine("4: Select benchmark (single file report)");
        Console.Write("\n選択 > ");
        Console.Out.Flush();

        var modeInput = Console.ReadLine()?.Trim()??string.Empty;
        var singleFileMode = string.Equals(modeInput,"3",StringComparison.OrdinalIgnoreCase)
            ||string.Equals(modeInput,"4",StringComparison.OrdinalIgnoreCase);
        var selectBenchmark = string.Equals(modeInput,"2",StringComparison.OrdinalIgnoreCase)
            ||string.Equals(modeInput,"4",StringComparison.OrdinalIgnoreCase);

        Console.WriteLine();
        if(!selectBenchmark) {
            Console.WriteLine(singleFileMode
                ? "All benchmark (single file report) を実行します。"
                : "All benchmark を実行します。");
            return new InteractiveSelection(["--filter","*"],singleFileMode);
        }

        Console.WriteLine(singleFileMode
            ? "Select benchmark (single file report) を選びました。"
            : "Select benchmark を選びました。");
        return new InteractiveSelection(SelectBenchmarks(benchmarkAssembly),singleFileMode);
    }

    private static string[] SelectBenchmarks(Assembly benchmarkAssembly) {
        var benchmarkMethods = GetBenchmarkMethods(benchmarkAssembly);

        if(benchmarkMethods.Count==0) {
            return ["--filter","*"];
        }

        Console.WriteLine("番号を選択してください。");
        Console.WriteLine("カンマ区切りで複数選択可、Enter または * で全て実行します。\n");

        for(var i = 0;i<benchmarkMethods.Count;i++) {
            Console.WriteLine($"{i+1,3}: {benchmarkMethods[i].DisplayName}");
        }

        Console.Write("\n選択 > ");
        Console.Out.Flush();
        var input = Console.ReadLine()?.Trim()??string.Empty;

        if(string.IsNullOrEmpty(input)||input=="*") {
            return ["--filter","*"];
        }

        var selected = new List<BenchmarkMethod>();
        foreach(var indexText in input.Split(',')) {
            if(int.TryParse(indexText.Trim(),NumberStyles.Integer,CultureInfo.InvariantCulture,out var index)
                &&index>=1
                &&index<=benchmarkMethods.Count) {
                selected.Add(benchmarkMethods[index-1]);
            }
        }

        if(selected.Count==0) {
            Console.WriteLine("有効な番号がなかったため、全て実行します。");
            return ["--filter","*"];
        }

        var pattern = string.Join("|",selected.Select(static method => $"*{method.MethodName}*"));
        Console.WriteLine($"Filter: {pattern}");
        return ["--filter",pattern];
    }

    private static bool IsListJsonMode(string[] args) {
        return args.Any(static arg => string.Equals(arg,"--list-json",StringComparison.OrdinalIgnoreCase));
    }

    private static void WriteBenchmarkListJson(Assembly benchmarkAssembly) {
        var benchmarkMethods = GetBenchmarkMethods(benchmarkAssembly)
            .Select(static method => new BenchmarkListItem(
                TypeName: method.TypeName,
                TypeFullName: method.TypeFullName,
                MethodName: method.MethodName,
                DisplayName: method.DisplayName,
                Filter: method.Filter,
                Categories: method.Categories))
            .ToArray();

        Console.WriteLine(JsonSerializer.Serialize(benchmarkMethods));
    }

    private static List<BenchmarkMethod> GetBenchmarkMethods(Assembly benchmarkAssembly) {
        return benchmarkAssembly.GetTypes()
            .Where(static type => !type.IsAbstract)
            .SelectMany(static type => type
                .GetMethods(BindingFlags.Instance|BindingFlags.Public)
                .Where(static method => method.GetCustomAttributes(typeof(BenchmarkAttribute),false).Length>0)
                .Select(method => new BenchmarkMethod(type,method)))
            .OrderBy(static method => method.MethodName,StringComparer.OrdinalIgnoreCase)
            .ThenBy(static method => method.TypeName,StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsSingleFileMode(string[] args) {
        if(Environment.GetEnvironmentVariable("SEAMLESS_BENCHMARK_SINGLE_FILE")=="1") {
            return true;
        }

        return args.Any(static arg => string.Equals(arg,"--single-file",StringComparison.OrdinalIgnoreCase)
            ||string.Equals(arg,"--ai-summary",StringComparison.OrdinalIgnoreCase));
    }

    private static string[] RemoveSingleFileSwitch(string[] args) {
        var filtered = new List<string>(args.Length);
        foreach(var arg in args) {
            if(string.Equals(arg,"--single-file",StringComparison.OrdinalIgnoreCase)
                ||string.Equals(arg,"--ai-summary",StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            filtered.Add(arg);
        }

        return filtered.ToArray();
    }

    private static void WriteSingleFileReport() {
        var csvFiles = FindReportFiles();
        if(csvFiles.Length==0) {
            return;
        }

        var outputPath = Path.Combine(Path.GetDirectoryName(csvFiles[0])!,"Benchmark.html");
        var writer = new StringBuilder();
        writer.AppendLine("<!DOCTYPE html>");
        writer.AppendLine("<html lang=\"ja\">");
        writer.AppendLine("<head>");
        writer.AppendLine("  <meta charset=\"utf-8\">");
        writer.AppendLine("  <meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        writer.AppendLine("  <title>Benchmark Summary</title>");
        writer.AppendLine("  <style>");
        writer.AppendLine("    body{font-family:ui-monospace,SFMono-Regular,Menlo,Consolas,monospace;margin:24px;line-height:1.5;color:#222;background:#fff;}");
        writer.AppendLine("    h1,h2{margin:0 0 12px;}");
        writer.AppendLine("    p{margin:0 0 16px;}");
        writer.AppendLine("    section{margin:0 0 28px;}");
        writer.AppendLine("    table{border-collapse:collapse;width:100%;margin:0 0 16px;}");
        writer.AppendLine("    th,td{border:1px solid #ccc;padding:6px 8px;text-align:left;vertical-align:top;}");
        writer.AppendLine("    th{background:#f5f5f5;}");
        writer.AppendLine("    td.num{text-align:right;white-space:nowrap;}");
        writer.AppendLine("  </style>");
        writer.AppendLine("</head>");
        writer.AppendLine("<body>");
        writer.AppendLine("  <h1>Benchmark Summary</h1>");
        writer.AppendLine($"  <p>Generated: {EscapeHtml(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz",CultureInfo.InvariantCulture))}</p>");
        foreach(var csvFile in csvFiles) {
            AppendCsvReport(writer,csvFile);
        }
        writer.AppendLine("</body>");
        writer.AppendLine("</html>");
        File.WriteAllText(outputPath,writer.ToString(),Encoding.UTF8);
        Console.WriteLine($"Single file summary written to {outputPath}");
    }

    private static string[] FindReportFiles() {
        var roots = BenchmarkOutputLayout.KnownCategories
            .Prepend(BenchmarkOutputLayout.AllCategory)
            .Select(BenchmarkOutputLayout.GetCategoryRoot)
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return roots
            .SelectMany(static root => Directory.GetFiles(root,"*-report.csv",System.IO.SearchOption.AllDirectories))
            .OrderBy(static path => path,StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void AppendCsvReport(StringBuilder writer,string csvFile) {
        var rows = ReadCsv(csvFile);
        if(rows.Count<=1) {
            return;
        }

        var header = rows[0];
        var methodIndex = Array.IndexOf(header,"Method");
        var meanIndex = Array.IndexOf(header,"Mean");
        var errorIndex = Array.IndexOf(header,"Error");
        var stdDevIndex = Array.IndexOf(header,"StdDev");
        var ratioIndex = Array.IndexOf(header,"Ratio");
        if(methodIndex<0||meanIndex<0) {
            return;
        }

        writer.AppendLine("  <section>");
        writer.AppendLine($"    <h2>{EscapeHtml(Path.GetFileNameWithoutExtension(csvFile))}</h2>");
        writer.AppendLine("    <table>");
        writer.AppendLine("      <thead><tr><th>Method</th><th>Mean</th><th>Error</th><th>StdDev</th><th>Ratio</th></tr></thead>");
        writer.AppendLine("      <tbody>");
        for(var rowIndex = 1;rowIndex<rows.Count;rowIndex++) {
            var row = rows[rowIndex];
            if(row.Length<=methodIndex||row.Length<=meanIndex) {
                continue;
            }

            writer.Append("        <tr><td>");
            writer.Append(EscapeHtml(row[methodIndex]));
            writer.Append("</td><td class=\"num\">");
            writer.Append(EscapeHtml(row[meanIndex]));
            writer.Append("</td><td class=\"num\">");
            writer.Append(EscapeHtml(GetCell(row,errorIndex)));
            writer.Append("</td><td class=\"num\">");
            writer.Append(EscapeHtml(GetCell(row,stdDevIndex)));
            writer.Append("</td><td class=\"num\">");
            writer.Append(EscapeHtml(GetCell(row,ratioIndex)));
            writer.AppendLine("</td></tr>");
        }
        writer.AppendLine("      </tbody>");
        writer.AppendLine("    </table>");
        writer.AppendLine("  </section>");
    }

    private static List<string[]> ReadCsv(string path) {
        var rows = new List<string[]>();
        using var parser = new TextFieldParser(path,Encoding.UTF8) {
            TextFieldType=FieldType.Delimited,
            Delimiters=[","],
            HasFieldsEnclosedInQuotes=true,
            TrimWhiteSpace=false,
        };

        while(!parser.EndOfData) {
            rows.Add(parser.ReadFields()??[]);
        }

        return rows;
    }

    private static string EscapeHtml(string value) {
        return value
            .Replace("&","&amp;",StringComparison.Ordinal)
            .Replace("<","&lt;",StringComparison.Ordinal)
            .Replace(">","&gt;",StringComparison.Ordinal)
            .Replace("\"","&quot;",StringComparison.Ordinal);
    }

    private static string GetCell(string[] row,int index) {
        if(index<0||index>=row.Length) {
            return string.Empty;
        }

        return row[index];
    }

    private readonly record struct InteractiveSelection(string[] Args,bool SingleFileMode);
    private sealed record BenchmarkMethod(Type Type,MethodInfo Method) {
        public string TypeName { get; } = Type.Name;
        public string TypeFullName { get; } = Type.FullName??Type.Name;
        public string MethodName { get; } = Method.Name;
        public string DisplayName { get; } = $"{Type.Name}.{Method.Name}";
        public string Filter { get; } = $"*{Type.FullName??Type.Name}.{Method.Name}*";
        public string[] Categories { get; } = Type.GetCustomAttributes<BenchmarkCategoryAttribute>(false)
            .Concat(Method.GetCustomAttributes<BenchmarkCategoryAttribute>(false))
            .SelectMany(static attribute => attribute.Categories)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static category => category,StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private sealed record BenchmarkListItem(
        string TypeName,
        string TypeFullName,
        string MethodName,
        string DisplayName,
        string Filter,
        string[] Categories);
}

internal static class BenchmarkOutputLayout {
    public const string AllCategory = "All";

    private static readonly string[] categories = ["LINQ","FLinq","ZLinq","Tsavorite","Array"];
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

    public static string GetCategoryRoot(string category) {
        var current = metadata.Value;
        var path = Path.Combine(
            current.ProjectDir,
            "Benchmark",
            current.BaseConfiguration,
            current.FrameworkLabel,
            category);
        Directory.CreateDirectory(path);
        return path;
    }

    private static BenchmarkMetadata LoadMetadata() {
        var assembly = typeof(BenchmarkOutputLayout).Assembly;
        return new BenchmarkMetadata(
            ProjectDir: Environment.CurrentDirectory,
            BaseConfiguration: GetConfiguration(AppContext.BaseDirectory),
            FrameworkLabel: GetFrameworkLabel(AppContext.BaseDirectory)??GetAssemblyMetadata(assembly,"TargetFramework")??"net10.0");
    }

    private static string GetConfiguration(string baseDirectory) {
        var parts = baseDirectory.Split(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
        var binIndex = Array.FindIndex(parts,static part => string.Equals(part,"bin",StringComparison.OrdinalIgnoreCase));
        if(binIndex>=0&&binIndex+1<parts.Length) {
            return parts[binIndex+1];
        }

        return "Debug";
    }

    private static string? GetFrameworkLabel(string baseDirectory) {
        var parts = baseDirectory.Split(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
        var binIndex = Array.FindIndex(parts,static part => string.Equals(part,"bin",StringComparison.OrdinalIgnoreCase));
        if(binIndex>=0&&binIndex+2<parts.Length) {
            return parts[binIndex+2];
        }

        return null;
    }

    private static string? GetAssemblyMetadata(Assembly assembly,string key) {
        return assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => string.Equals(attribute.Key,key,StringComparison.Ordinal))
            ?.Value;
    }

    private sealed record BenchmarkMetadata(string ProjectDir,string BaseConfiguration,string FrameworkLabel);
}
