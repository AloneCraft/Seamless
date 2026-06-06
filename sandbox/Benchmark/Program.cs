using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;
using Perfolizer.Horology;
using Microsoft.VisualBasic.FileIO;
using System.Globalization;
using System.Text;

namespace Benchmark;

internal static class Program{
    public static void Main(string[] args){
        // ダブルクリック起動時など CWD が不定の場合、BDN が .csproj を広範検索して
        // macOS の .Trash 等アクセス拒否ディレクトリに当たるのを防ぐ
        for(var d=new DirectoryInfo(AppContext.BaseDirectory);d!=null;d=d.Parent){
            if(d.GetFiles("*.csproj").Length>0){
                Directory.SetCurrentDirectory(d.FullName);
                break;
            }
        }
        var singleFileMode=IsSingleFileMode(args);
        if(args.Length==0){
            var selection=PromptInteractiveSelection();
            args=selection.Args;
            singleFileMode=singleFileMode||selection.SingleFileMode;
        }
        args=RemoveSingleFileSwitch(args);
        var job=Job.Default
            .WithIterationTime(TimeInterval.Millisecond*100)  // WorkloadPilot の収束目標
            .WithIterationCount(16)                           // WorkloadActual = 16
            .WithWarmupCount(4);                              // WorkloadWarmup = 4
        if(Environment.GetEnvironmentVariable("SEAMLESS_BENCHMARK_INPROCESS")=="1"){
            job=job.WithToolchain(InProcessNoEmitToolchain.Instance);
        }
        var config=ManualConfig.Create(DefaultConfig.Instance)
            .AddJob(job);
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args,config);
        if(singleFileMode){
            WriteSingleFileReport();
        }
    }

    static InteractiveSelection PromptInteractiveSelection(){
        Console.WriteLine("=== 実行モード選択 ===");
        Console.WriteLine("1: RunAllBenchmark");
        Console.WriteLine("2: RunAll1FileBenchmark");
        Console.Write("\n選択 > ");
        Console.Out.Flush();
        var modeInput=Console.ReadLine()?.Trim()??string.Empty;
        var singleFileMode=string.Equals(modeInput,"2",StringComparison.OrdinalIgnoreCase);
        Console.WriteLine();
        Console.WriteLine("=== ベンチマーク選択 ===");
        Console.WriteLine(singleFileMode
            ? "RunAll1FileBenchmark を選びました。ここで番号を指定できます。"
            : "RunAllBenchmark を選びました。ここで番号を指定できます。");
        var args=SelectBenchmarks();
        return new InteractiveSelection(args,singleFileMode);
    }

    readonly record struct InteractiveSelection(string[] Args,bool SingleFileMode);

    static bool IsSingleFileMode(string[] args){
        if(Environment.GetEnvironmentVariable("SEAMLESS_BENCHMARK_SINGLE_FILE")=="1"){
            return true;
        }
        return args.Any(static arg=>string.Equals(arg,"--single-file",StringComparison.OrdinalIgnoreCase)
            ||string.Equals(arg,"--ai-summary",StringComparison.OrdinalIgnoreCase));
    }

    static string[] RemoveSingleFileSwitch(string[] args){
        var filtered=new List<string>(args.Length);
        foreach(var arg in args){
            if(string.Equals(arg,"--single-file",StringComparison.OrdinalIgnoreCase)
                ||string.Equals(arg,"--ai-summary",StringComparison.OrdinalIgnoreCase)){
                continue;
            }
            filtered.Add(arg);
        }
        return filtered.ToArray();
    }

    static void WriteSingleFileReport(){
        var resultsDir=FindResultsDirectory();
        if(!Directory.Exists(resultsDir)){
            return;
        }
        var csvFiles=Directory.GetFiles(resultsDir,"Benchmark.*-report.csv").OrderBy(static path=>path,StringComparer.OrdinalIgnoreCase).ToArray();
        if(csvFiles.Length==0){
            return;
        }
        var outputPath=Path.Combine(resultsDir,"Benchmark.html");
        var writer=new StringBuilder();
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
        foreach(var csvFile in csvFiles){
            AppendCsvReport(writer,csvFile);
        }
        writer.AppendLine("</body>");
        writer.AppendLine("</html>");
        File.WriteAllText(outputPath,writer.ToString(),Encoding.UTF8);
        Console.WriteLine($"Single file summary written to {outputPath}");
    }

    static string FindResultsDirectory(){
        for(var directory=new DirectoryInfo(Directory.GetCurrentDirectory());directory!=null;directory=directory.Parent){
            var candidate=Path.Combine(directory.FullName,"BenchmarkDotNet.Artifacts","results");
            if(Directory.Exists(candidate)){
                return candidate;
            }
        }
        for(var directory=new DirectoryInfo(AppContext.BaseDirectory);directory!=null;directory=directory.Parent){
            var candidate=Path.Combine(directory.FullName,"BenchmarkDotNet.Artifacts","results");
            if(Directory.Exists(candidate)){
                return candidate;
            }
        }
        return Path.Combine(Directory.GetCurrentDirectory(),"BenchmarkDotNet.Artifacts","results");
    }

    static void AppendCsvReport(StringBuilder writer,string csvFile){
        var rows=ReadCsv(csvFile);
        if(rows.Count<=1){
            return;
        }
        var header=rows[0];
        var methodIndex=Array.IndexOf(header,"Method");
        var meanIndex=Array.IndexOf(header,"Mean");
        var errorIndex=Array.IndexOf(header,"Error");
        var stdDevIndex=Array.IndexOf(header,"StdDev");
        var ratioIndex=Array.IndexOf(header,"Ratio");
        if(methodIndex<0||meanIndex<0){
            return;
        }
        writer.AppendLine("  <section>");
        writer.AppendLine($"    <h2>{EscapeHtml(Path.GetFileNameWithoutExtension(csvFile))}</h2>");
        writer.AppendLine("    <table>");
        writer.AppendLine("      <thead><tr><th>Method</th><th>Mean</th><th>Error</th><th>StdDev</th><th>Ratio</th></tr></thead>");
        writer.AppendLine("      <tbody>");
        for(var rowIndex=1;rowIndex<rows.Count;rowIndex++){
            var row=rows[rowIndex];
            if(row.Length<=methodIndex||row.Length<=meanIndex){
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

    static List<string[]> ReadCsv(string path){
        var rows=new List<string[]>();
        using var parser=new TextFieldParser(path,Encoding.UTF8){
            TextFieldType=FieldType.Delimited,
            Delimiters=[","],
            HasFieldsEnclosedInQuotes=true,
            TrimWhiteSpace=false
        };
        while(!parser.EndOfData){
            rows.Add(parser.ReadFields()??Array.Empty<string>());
        }
        return rows;
    }

    static string EscapeHtml(string value){
        return value
            .Replace("&","&amp;",StringComparison.Ordinal)
            .Replace("<","&lt;",StringComparison.Ordinal)
            .Replace(">","&gt;",StringComparison.Ordinal)
            .Replace("\"","&quot;",StringComparison.Ordinal);
    }

    static string GetCell(string[] row,int index){
        if(index<0||index>=row.Length){
            return string.Empty;
        }
        return row[index];
    }

    static string[] SelectBenchmarks(){
        var benchmarkTypes=typeof(Program).Assembly.GetTypes()
            .Where(t=>t.GetMethods()
                .Any(m=>m.GetCustomAttributes(typeof(BenchmarkAttribute),false).Length>0))
            .OrderBy(t=>t.Name)
            .ToList();

        if(benchmarkTypes.Count==0){
            return ["--filter","*"];
        }

        Console.WriteLine("番号を選択してください (カンマ区切りで複数選択可、Enterで全て実行):");
        Console.WriteLine("全ベンチ実行は `*`、個別は番号指定です。\n");

        for(int i=0;i<benchmarkTypes.Count;i++){
            Console.WriteLine($"{i+1,2}: {benchmarkTypes[i].Name}");
        }

        Console.Write("\n選択 > ");
        Console.Out.Flush();
        var input=Console.ReadLine()?.Trim()??string.Empty;

        if(string.IsNullOrEmpty(input)||input=="*"){
            return ["--filter","*"];
        }

        var selected=new List<string>();
        var indices=input.Split(',');
        foreach(var idx in indices){
            if(int.TryParse(idx.Trim(),out var n)&&n>=1&&n<=benchmarkTypes.Count){
                selected.Add(benchmarkTypes[n-1].Name);
            }
        }

        if(selected.Count==0){
            return ["--filter","*"];
        }

        var pattern=string.Join("|",selected.Select(s=>$"*{s}*"));
        return ["--filter",pattern];
    }
}
