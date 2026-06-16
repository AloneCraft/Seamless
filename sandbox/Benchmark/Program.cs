using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Filters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;
using Perfolizer.Horology;
using Microsoft.VisualBasic.FileIO;
using System.Globalization;
using System.Text;

namespace Benchmark;

internal static class Program {
    // ビルド時に切り替える定数（編集して再ビルドすることで実行対象を制御）
    private const bool RUN_LINQ = true;
    private const bool RUN_FLINQ = true;
    private const bool RUN_ZLINQ = true;
    private const bool RUN_TSAVORITE = true;
    private const int IterationTime=1000;
    private const int IterationCount=1;
    private const int WarmupCount=3;

    public static void Main(string[] args) {
        // ダブルクリック起動時など CWD が不定の場合、BDN が .csproj を広範検索して
        // macOS の .Trash 等アクセス拒否ディレクトリに当たるのを防ぐ
        for(var d = new DirectoryInfo(AppContext.BaseDirectory); d!=null; d=d.Parent) {
            if(d.GetFiles("*.csproj").Length>0) {
                Directory.SetCurrentDirectory(d.FullName);
                break;
            }
        }
        // compile-time const で実行対象（メソッド名）を決める。
        // CLI の --filter で組み込むと args が非空になり対話メニューが出なくなるため、
        // ここでは BenchmarkDotNet の IFilter として後段の config に追加し、
        // メニュー（型選択・single-file モード選択）は常に表示されるようにする。
        var includeMethods = new List<string>();
        if(RUN_LINQ) includeMethods.Add("Linq");
        if(RUN_FLINQ) includeMethods.Add("FLinq");
        if(RUN_ZLINQ) includeMethods.Add("ZLinq");
        if(RUN_TSAVORITE) includeMethods.Add("Tsavorite");
        var singleFileMode = IsSingleFileMode(args);
        var applyCategoryFilter = false;
        if(args.Length==0) {
            var selection = PromptInteractiveSelection();
            args=selection.Args;
            singleFileMode=singleFileMode||selection.SingleFileMode;
            applyCategoryFilter = selection.UseCategoryFilter;
        }
        args=RemoveSingleFileSwitch(args);
        var job = Job.Default
            .WithIterationTime(TimeInterval.Millisecond*IterationTime)
            .WithIterationCount(IterationCount)                           
            .WithWarmupCount(WarmupCount);
        if(Environment.GetEnvironmentVariable("SEAMLESS_BENCHMARK_INPROCESS")=="1") {
            job=job.WithToolchain(InProcessNoEmitToolchain.Instance);
        }
        var config = ManualConfig.Create(DefaultConfig.Instance)
            .AddJob(job);
#if DEBUG
        // Debug構成でも実行できるように最適化必須バリデータを無効化する
        config=config.WithOptions(ConfigOptions.DisableOptimizationsValidator);
#endif
        if(applyCategoryFilter&&includeMethods.Count>0&&includeMethods.Count<4) {
            config=config.AddFilter(new MethodNameFilter(includeMethods));
        }
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args,config);
        if(singleFileMode) {
            WriteSingleFileReport();
        }
    }
    static InteractiveSelection PromptInteractiveSelection() {
        Console.WriteLine("=== 実行モード選択 ===");
        Console.WriteLine("1: RunAllBenchmark");
        Console.WriteLine("2: RunAll1FileBenchmark");
        Console.Write("\n選択 > ");
        Console.Out.Flush();
        var modeInput = Console.ReadLine()?.Trim()??string.Empty;
        var singleFileMode = string.Equals(modeInput,"2",StringComparison.OrdinalIgnoreCase);
        Console.WriteLine();
        Console.WriteLine("=== ベンチマーク選択 ===");
        Console.WriteLine(singleFileMode
            ? "RunAll1FileBenchmark を選びました。ここで番号を指定できます。"
            : "RunAllBenchmark を選びました。ここで番号を指定できます。");
        var args = SelectBenchmarks();
        var useCategoryFilter = args.Length>=2
            && string.Equals(args[0],"--filter",StringComparison.Ordinal)
            && args[1]=="*";
        return new InteractiveSelection(args,singleFileMode,useCategoryFilter);
    }

    readonly record struct InteractiveSelection(string[] Args,bool SingleFileMode,bool UseCategoryFilter);

    static bool IsSingleFileMode(string[] args) {
        if(Environment.GetEnvironmentVariable("SEAMLESS_BENCHMARK_SINGLE_FILE")=="1") {
            return true;
        }
        return args.Any(static arg => string.Equals(arg,"--single-file",StringComparison.OrdinalIgnoreCase)
            ||string.Equals(arg,"--ai-summary",StringComparison.OrdinalIgnoreCase));
    }
    static string[] RemoveSingleFileSwitch(string[] args) {
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
    // ピボット列の並び。0=LINQ,1=FLinq,2=ZLinq に対応。
    static readonly string[] MethodKinds = ["Linq","FLinq","ZLinq","Tsavorite"];
    // 表示順: LINQ, ZLinq, FLinq。MethodKinds のインデックスへの対応。
    static readonly int[] DisplayOrder = [0,2,1,3];
    static readonly string[] DisplayLabels = ["LINQ","ZLinq","FLinq","Tsavorite"];
    static void WriteSingleFileReport() {
        var resultsDir = FindResultsDirectory();
        if(!Directory.Exists(resultsDir)) {
            return;
        }
        var csvFiles = Directory.GetFiles(resultsDir,"*-report.csv").OrderBy(static path => path,StringComparer.OrdinalIgnoreCase).ToArray();
        if(csvFiles.Length==0) {
            return;
        }
        var rows = BuildSummary(csvFiles);
        if(rows.Count==0) {
            return;
        }
        var htmlPath = Path.Combine(resultsDir,"Benchmark.html");
        File.WriteAllText(htmlPath,BuildHtml(rows),Encoding.UTF8);
        var byNamePath = Path.Combine(resultsDir,"Summary-byName.csv");
        WriteSummaryCsv(rows.OrderBy(static r => r.RowKey,StringComparer.Ordinal),byNamePath);
        var byRatioPath = Path.Combine(resultsDir,"Summary-byRatio.csv");
        WriteSummaryCsv(rows.OrderBy(static r => r.SlowestNs),byRatioPath);
        Console.WriteLine($"Single file summary written to {htmlPath}");
        Console.WriteLine($"Pivot CSV written to {byNamePath}");
        Console.WriteLine($"Pivot CSV written to {byRatioPath}");
    }
    // クラス(Type)＋末尾番号を行キーに、LINQ/FLinq/ZLinq の Mean を横に集約する。
    static List<SummaryRow> BuildSummary(IEnumerable<string> csvFiles) {
        var map = new Dictionary<string,SummaryRow>(StringComparer.Ordinal);
        foreach(var csvFile in csvFiles) {
            var csv = ReadCsv(csvFile);
            if(csv.Count<=1) {
                continue;
            }
            var header = csv[0];
            var typeIndex = Array.IndexOf(header,"Type");
            var methodIndex = Array.IndexOf(header,"Method");
            var meanIndex = Array.IndexOf(header,"Mean");
            var methodNameIndex = Array.IndexOf(header,"MethodName");
            if(typeIndex<0||methodIndex<0||meanIndex<0) {
                continue;
            }
            for(var rowIndex = 1; rowIndex<csv.Count; rowIndex++) {
                var row = csv[rowIndex];
                if(row.Length<=typeIndex||row.Length<=methodIndex||row.Length<=meanIndex) {
                    continue;
                }
                var method = row[methodIndex];
                var kindIndex = SplitMethod(method,out var suffix);
                if(kindIndex<0) {
                    continue;
                }
                var type = row[typeIndex];
                var parameterSuffix = methodNameIndex>=0&&row.Length>methodNameIndex&&!string.IsNullOrWhiteSpace(row[methodNameIndex])
                    ? "["+row[methodNameIndex]+"]"
                    : string.Empty;
                var rowKey = type+suffix+parameterSuffix;
                if(!map.TryGetValue(rowKey,out var summary)) {
                    summary=new SummaryRow(type,suffix+parameterSuffix);
                    map[rowKey]=summary;
                }
                summary.MeanText[kindIndex]=row[meanIndex];
                summary.MeanNs[kindIndex]=ParseMeanNs(row[meanIndex]);
            }
        }
        var rows = map.Values.ToList();
        foreach(var summary in rows) {
            summary.ComputeRatios();
        }
        return rows;
    }
    // "LINQ3" → (0,"3"), "FLinq" → (1,""), 不一致 → -1。
    static int SplitMethod(string method,out string suffix) {
        var end = method.Length;
        while(end>0&&char.IsDigit(method[end-1])) {
            end--;
        }
        suffix=method[end..];
        var kind = method[..end];
        return Array.IndexOf(MethodKinds,kind);
    }
    // "237,160.8127 ns" → 237160.8127 (ns換算)。解析不能なら null。
    static double? ParseMeanNs(string value) {
        var text = value.Trim();
        if(text.Length==0) {
            return null;
        }
        var spaceIndex = text.LastIndexOf(' ');
        var numberPart = (spaceIndex>=0 ? text[..spaceIndex] : text).Replace(",","",StringComparison.Ordinal);
        var unitPart = spaceIndex>=0 ? text[(spaceIndex+1)..].Trim() : "ns";
        if(!double.TryParse(numberPart,NumberStyles.Float,CultureInfo.InvariantCulture,out var number)) {
            return null;
        }
        var scale = unitPart switch {
            "s" => 1e9,
            "ms" => 1e6,
            "us" or "µs" or "μs" => 1e3,
            _ => 1.0
        };
        return number*scale;
    }
    static string BuildHtml(List<SummaryRow> rows) {
        var writer = new StringBuilder();
        writer.AppendLine("<!DOCTYPE html>");
        writer.AppendLine("<html lang=\"ja\">");
        writer.AppendLine("<head>");
        writer.AppendLine("  <meta charset=\"utf-8\">");
        writer.AppendLine("  <meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        writer.AppendLine("  <title>Benchmark Summary</title>");
        writer.AppendLine("  <style>");
        writer.AppendLine("    body{font-family:ui-monospace,SFMono-Regular,Menlo,Consolas,monospace;margin:24px;line-height:1.5;color:#222;background:#fff;}");
        writer.AppendLine("    h1{margin:0 0 12px;}");
        writer.AppendLine("    p{margin:0 0 16px;}");
        writer.AppendLine("    .controls{margin:0 0 16px;}");
        writer.AppendLine("    button{font:inherit;padding:6px 12px;margin-right:8px;cursor:pointer;}");
        writer.AppendLine("    table{border-collapse:collapse;width:100%;}");
        writer.AppendLine("    th,td{border:1px solid #ccc;padding:6px 8px;text-align:left;vertical-align:top;}");
        writer.AppendLine("    th{background:#f5f5f5;}");
        writer.AppendLine("    thead th{position:sticky;top:0;z-index:1;}");
        writer.AppendLine("    td.num,th.num{text-align:right;white-space:nowrap;}");
        writer.AppendLine("  </style>");
        writer.AppendLine("</head>");
        writer.AppendLine("<body>");
        writer.AppendLine("  <h1>Benchmark Summary</h1>");
        writer.AppendLine($"  <p>Generated: {EscapeHtml(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz",CultureInfo.InvariantCulture))}</p>");
        writer.AppendLine("  <div class=\"controls\">");
        writer.AppendLine("    <button onclick=\"sortRows('name')\">名前順</button>");
        writer.AppendLine("    <button onclick=\"sortRows('ratio')\">比率順（最大比 昇順）</button>");
        writer.AppendLine("  </div>");
        writer.AppendLine("  <table id=\"summary\">");
        writer.AppendLine("    <thead>");
        writer.Append("      <tr><th>Type</th><th>Method</th>");
        foreach(var label in DisplayLabels) {
            writer.Append($"<th class=\"num\">{label}Time</th>");
        }
        foreach(var label in DisplayLabels) {
            writer.Append($"<th class=\"num\">{label}Ratio</th>");
        }
        writer.AppendLine("</tr>");
        writer.AppendLine("    </thead>");
        writer.AppendLine("    <tbody>");
        foreach(var summary in rows.OrderBy(static r => r.RowKey,StringComparer.Ordinal)) {
            writer.Append($"      <tr data-name=\"{EscapeHtml(summary.RowKey)}\" data-maxratio=\"{summary.SlowestNs.ToString(CultureInfo.InvariantCulture)}\"><td>");
            writer.Append(EscapeHtml(summary.Type));
            writer.Append("</td><td>");
            writer.Append(EscapeHtml(summary.Method));
            writer.Append("</td>");
            foreach(var i in DisplayOrder) {
                writer.Append("<td class=\"num\">");
                writer.Append(EscapeHtml(summary.MeanText[i]??string.Empty));
                writer.Append("</td>");
            }
            foreach(var i in DisplayOrder) {
                writer.Append("<td class=\"num\">");
                writer.Append(summary.Ratio[i] is { } ratio ? ratio.ToString("0.00",CultureInfo.InvariantCulture) : string.Empty);
                writer.Append("</td>");
            }
            writer.AppendLine("</tr>");
        }
        writer.AppendLine("    </tbody>");
        writer.AppendLine("  </table>");
        writer.AppendLine("  <script>");
        writer.AppendLine("    function sortRows(mode){");
        writer.AppendLine("      const tbody=document.querySelector('#summary tbody');");
        writer.AppendLine("      const rows=Array.from(tbody.querySelectorAll('tr'));");
        writer.AppendLine("      rows.sort((a,b)=>mode==='name'");
        writer.AppendLine("        ?a.dataset.name.localeCompare(b.dataset.name)");
        writer.AppendLine("        :parseFloat(a.dataset.maxratio)-parseFloat(b.dataset.maxratio));");
        writer.AppendLine("      rows.forEach(r=>tbody.appendChild(r));");
        writer.AppendLine("    }");
        writer.AppendLine("  </script>");
        writer.AppendLine("</body>");
        writer.AppendLine("</html>");
        return writer.ToString();
    }
    static void WriteSummaryCsv(IEnumerable<SummaryRow> rows,string path) {
        var writer = new StringBuilder();
        writer.Append("Type,Method");
        foreach(var label in DisplayLabels) {
            writer.Append(',').Append(label).Append("Time");
        }
        foreach(var label in DisplayLabels) {
            writer.Append(',').Append(label).Append("Ratio");
        }
        writer.AppendLine();
        foreach(var summary in rows) {
            writer.Append(CsvField(summary.Type));
            writer.Append(',');
            writer.Append(CsvField(summary.Method));
            foreach(var i in DisplayOrder) {
                writer.Append(',');
                writer.Append(CsvField(summary.MeanText[i]??string.Empty));
            }
            foreach(var i in DisplayOrder) {
                writer.Append(',');
                writer.Append(summary.Ratio[i] is { } ratio ? ratio.ToString("0.00",CultureInfo.InvariantCulture) : string.Empty);
            }
            writer.AppendLine();
        }
        File.WriteAllText(path,writer.ToString(),Encoding.UTF8);
    }
    // カンマやクオートを含む値はダブルクオート囲み（RFC4180）。
    static string CsvField(string value) {
        if(value.IndexOfAny([',','"','\n','\r'])<0) {
            return value;
        }
        return "\""+value.Replace("\"","\"\"",StringComparison.Ordinal)+"\"";
    }
    sealed class SummaryRow(string type,string method) {
        public string Type { get; } = type;
        public string Method { get; } = method;
        public string RowKey { get; } = type+method;
        public string?[] MeanText { get; } = new string?[4];
        public double?[] MeanNs { get; } = new double?[4];
        public double?[] Ratio { get; } = new double?[4];
        // 最も遅い手法の絶対時間(ns)。Ratioの基準値であり、byRatio出力のソートキーにも使う。
        public double SlowestNs { get; private set; }
        // Ratioは最遅手法を1.0とした相対値（他は1.0以下）。
        public void ComputeRatios() {
            var max = 0.0;
            foreach(var value in this.MeanNs) {
                if(value is { } v&&v>max) {
                    max=v;
                }
            }
            if(max<=0) {
                return;
            }
            for(var i = 0; i<this.MeanNs.Length; i++) {
                if(this.MeanNs[i] is { } v&&v>0) {
                    this.Ratio[i]=v/max;
                }
            }
            this.SlowestNs=max;
        }
    }
    static string FindResultsDirectory() {
        for(var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory!=null; directory=directory.Parent) {
            var candidate = Path.Combine(directory.FullName,"BenchmarkDotNet.Artifacts","results");
            if(Directory.Exists(candidate)) {
                return candidate;
            }
        }
        for(var directory = new DirectoryInfo(AppContext.BaseDirectory); directory!=null; directory=directory.Parent) {
            var candidate = Path.Combine(directory.FullName,"BenchmarkDotNet.Artifacts","results");
            if(Directory.Exists(candidate)) {
                return candidate;
            }
        }
        return Path.Combine(Directory.GetCurrentDirectory(),"BenchmarkDotNet.Artifacts","results");
    }
    static List<string[]> ReadCsv(string path) {
        var rows = new List<string[]>();
        using var parser = new TextFieldParser(path,Encoding.UTF8) {
            TextFieldType=FieldType.Delimited,
            Delimiters= [","],
            HasFieldsEnclosedInQuotes=true,
            TrimWhiteSpace=false
        };
        while(!parser.EndOfData) {
            rows.Add(parser.ReadFields()??[]);
        }
        return rows;
    }
    static string EscapeHtml(string value) {
        return value
            .Replace("&","&amp;",StringComparison.Ordinal)
            .Replace("<","&lt;",StringComparison.Ordinal)
            .Replace(">","&gt;",StringComparison.Ordinal)
            .Replace("\"","&quot;",StringComparison.Ordinal);
    }
    static string[] SelectBenchmarks() {
        var benchmarkTypes = typeof(Program).Assembly.GetTypes()
            .Where(t => t.GetMethods()
                .Any(m => m.GetCustomAttributes(typeof(BenchmarkAttribute),false).Length>0))
            .OrderBy(t => t.Name)
            .ToList();
        if(benchmarkTypes.Count==0) {
            return ["--filter","*"];
        }
        Console.WriteLine("番号を選択してください (カンマ区切りで複数選択可、Enterで全て実行):");
        Console.WriteLine("全ベンチ実行は `*`、個別は番号指定です。\n");
        for(int i = 0; i<benchmarkTypes.Count; i++) {
            Console.WriteLine($"{i+1,2}: {benchmarkTypes[i].Name}");
        }
        Console.Write("\n選択 > ");
        Console.Out.Flush();
        var input = Console.ReadLine()?.Trim()??string.Empty;

        if(string.IsNullOrEmpty(input)||input=="*") {
            return ["--filter","*","--join"];
        }
        var selected = new List<string>();
        var indices = input.Split(',');
        foreach(var idx in indices) {
            var token = idx.Trim();
            var dashIndex = token.IndexOf('-');
            if(dashIndex>0) {
                if(int.TryParse(token[..dashIndex],out var start)&&int.TryParse(token[(dashIndex+1)..],out var end)&&start<=end) {
                    for(var n = start; n<=end; n++) {
                        if(n>=1&&n<=benchmarkTypes.Count) {
                            selected.Add(benchmarkTypes[n-1].Name);
                        }
                    }
                }
            }else if(int.TryParse(token,out var n)&&n>=1&&n<=benchmarkTypes.Count) {
                selected.Add(benchmarkTypes[n-1].Name);
            }
        }
        selected=selected.Distinct().ToList();
        if(selected.Count==0) {
            return ["--filter","*","--join"];
        }
        var patterns = selected.Select(s => $"*{s}*").ToArray();
        if(selected.Count>1) {
            return ["--filter",..patterns,"--join"];
        }
        return ["--filter",patterns[0]];
    }
}
sealed class MethodNameFilter(IReadOnlyCollection<string> methodNames):IFilter {
    readonly HashSet<string> names = new(methodNames,StringComparer.Ordinal);
    public bool Predicate(BenchmarkCase benchmarkCase) => this.names.Contains(benchmarkCase.Descriptor.WorkloadMethod.Name);
}
