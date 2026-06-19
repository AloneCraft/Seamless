using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Microsoft.VisualBasic.FileIO;
using System.Globalization;
using System.Reflection;
using System.Text;
using Tsavorite.Benchmark.Benchmarks;

namespace Tsavorite.Benchmark;

public static class Program {
    private const string BenchmarkSourceAssemblyMetadataKey = "BenchmarkSourceAssembly";

    public static void Main(string[] args) {
        SetCurrentDirectoryToProject();
        var benchmarkAssembly = ResolveBenchmarkAssembly();

        var singleFileMode = IsSingleFileMode(args);
        if(args.Length==0&&!Console.IsInputRedirected) {
            var selection = PromptInteractiveSelection(benchmarkAssembly);
            args=selection.Args;
            singleFileMode=singleFileMode||selection.SingleFileMode;
        }

        args=RemoveSingleFileSwitch(args);

        var switcher = BenchmarkSwitcher.FromAssembly(benchmarkAssembly);
        if(BenchmarkOutputLayout.ShouldSplitByCategory(args)) {
            foreach(var category in BenchmarkOutputLayout.KnownCategories) {
                switcher.Run(BenchmarkOutputLayout.WithCategoryFilter(args,category),BenchmarkOutputLayout.CreateRuntimeConfig(category));
            }
        }
        else {
            var category = BenchmarkOutputLayout.TryGetCategoryFromArgs(args)??BenchmarkOutputLayout.AllCategory;
            switcher.Run(args,BenchmarkOutputLayout.CreateRuntimeConfig(category));
        }

        if(singleFileMode) {
            WriteSingleFileReport();
        }
    }

    private static Assembly ResolveBenchmarkAssembly() {
        var entryAssembly = Assembly.GetEntryAssembly()??typeof(Program).Assembly;
        var sourceAssemblyName = entryAssembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(static attribute => string.Equals(attribute.Key,BenchmarkSourceAssemblyMetadataKey,StringComparison.Ordinal))
            ?.Value;

        if(string.IsNullOrWhiteSpace(sourceAssemblyName)) {
            return typeof(Program).Assembly;
        }

        return AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(assembly => string.Equals(assembly.GetName().Name,sourceAssemblyName,StringComparison.OrdinalIgnoreCase))
            ??Assembly.Load(sourceAssemblyName);
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
            return new InteractiveSelection([],singleFileMode);
        }

        Console.WriteLine(singleFileMode
            ? "Select benchmark (single file report) を選びました。"
            : "Select benchmark を選びました。");
        return new InteractiveSelection(SelectBenchmarks(benchmarkAssembly),singleFileMode);
    }

    private static string[] SelectBenchmarks(Assembly benchmarkAssembly) {
        var benchmarkTypes = benchmarkAssembly.GetTypes()
            .Where(static type => type.GetMethods()
                .Any(static method => method.GetCustomAttributes(typeof(BenchmarkAttribute),false).Length>0))
            .OrderBy(static type => type.Name,StringComparer.OrdinalIgnoreCase)
            .ToList();

        if(benchmarkTypes.Count==0) {
            return [];
        }

        Console.WriteLine("番号を選択してください。");
        Console.WriteLine("カンマ区切りで複数選択可、Enter または * で全て実行します。\n");

        for(var i = 0;i<benchmarkTypes.Count;i++) {
            Console.WriteLine($"{i+1,2}: {benchmarkTypes[i].Name}");
        }

        Console.Write("\n選択 > ");
        Console.Out.Flush();
        var input = Console.ReadLine()?.Trim()??string.Empty;

        if(string.IsNullOrEmpty(input)||input=="*") {
            return [];
        }

        var selected = new List<string>();
        foreach(var indexText in input.Split(',')) {
            if(int.TryParse(indexText.Trim(),out var index)&&index>=1&&index<=benchmarkTypes.Count) {
                selected.Add(benchmarkTypes[index-1].Name);
            }
        }

        if(selected.Count==0) {
            Console.WriteLine("有効な番号がなかったため、全て実行します。");
            return [];
        }

        var pattern = string.Join("|",selected.Select(static name => $"*{name}*"));
        Console.WriteLine($"Filter: {pattern}");
        return ["--filter",pattern];
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
}
