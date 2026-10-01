using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Benchmark.Audit;

// 使い方:
//   Benchmark.Audit audit <アセンブリ>... [--json <出力>] [--md <出力>]
//   Benchmark.Audit compare --flinq <FLinq のアセンブリ>... --ideal <理想版のアセンブリ>... [--json <出力>] [--md <出力>]
if(args.Length>0&&args[0]=="noise") {
    return Noise(args);
}
if(args.Length==0||(args[0]!="audit"&&args[0]!="compare")) {
    Console.Error.WriteLine("使い方: Benchmark.Audit audit <アセンブリ.dll>... [--json <出力.json>] [--md <出力.md>]");
    Console.Error.WriteLine("        Benchmark.Audit compare --flinq <アセンブリ.dll>... --ideal <アセンブリ.dll>... [--json <出力.json>] [--md <出力.md>]");
    Console.Error.WriteLine("        Benchmark.Audit noise --first <1回目.jsonl>... --second <2回目.jsonl>... [--json <出力.json>] [--md <出力.md>]");
    return 2;
}
var command = args[0];
var flinqAssemblies = new List<string>();
var idealAssemblies = new List<string>();
string? jsonPath = null;
string? markdownPath = null;
var target = flinqAssemblies;
for(var index = 1;index<args.Length;index++) {
    if(args[index]=="--json") {
        jsonPath=args[++index];
    } else if(args[index]=="--md") {
        markdownPath=args[++index];
    } else if(args[index]=="--flinq") {
        target=flinqAssemblies;
    } else if(args[index]=="--ideal") {
        target=idealAssemblies;
    } else {
        target.Add(args[index]);
    }
}
using var catalog = new MetadataCatalog();
var options = new JsonSerializerOptions {
    WriteIndented=true,
    Encoder=JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    Converters={ new JsonStringEnumConverter() },
};
var generated = new IlAuditor(catalog);
var rows = new List<AuditRow>();
foreach(var path in flinqAssemblies) {
    rows.AddRange(generated.Audit(catalog.Load(path)));
}
if(command=="audit") {
    Write(jsonPath,JsonSerializer.Serialize(rows,options));
    if(markdownPath is not null) {
        File.WriteAllText(markdownPath,AuditReport.ToMarkdown(rows),new UTF8Encoding(false));
    }
    Console.Error.WriteLine($"rows={rows.Count} intercepted={rows.Count(static row => row.Intercepted)} implDirect={rows.Count(static row => row.ImplCalledDirectly)}");
    return 0;
}
var handwritten = new IlAuditor(catalog) { Handwritten=true };
var idealRows = new List<AuditRow>();
foreach(var path in idealAssemblies) {
    idealRows.AddRange(handwritten.Audit(catalog.Load(path)));
}
var comparison = AuditComparison.Compare(rows,idealRows);
Write(jsonPath,JsonSerializer.Serialize(comparison,options));
if(markdownPath is not null) {
    File.WriteAllText(markdownPath,AuditComparison.ToMarkdown(comparison),new UTF8Encoding(false));
}
Console.Error.WriteLine($"flinq={rows.Count} ideal={idealRows.Count} matched={comparison.Count} worse={comparison.Count(static row => row.WorseThanIdeal.Count>0)}");
return 0;

// 同じビルドを2回測った multi-run の結果から、行ごとのぶれと理想比を求めて分類する。
static int Noise(string[] arguments) {
    var first = new List<string>();
    var second = new List<string>();
    string? json = null;
    string? markdown = null;
    var target = first;
    for(var index = 1;index<arguments.Length;index++) {
        if(arguments[index]=="--first") {
            target=first;
        } else if(arguments[index]=="--second") {
            target=second;
        } else if(arguments[index]=="--json") {
            json=arguments[++index];
        } else if(arguments[index]=="--md") {
            markdown=arguments[++index];
        } else {
            target.Add(arguments[index]);
        }
    }
    var rows = MeasurementAnalysis.Classify(first.SelectMany(MeasurementAnalysis.Read).ToArray(),second.SelectMany(MeasurementAnalysis.Read).ToArray());
    var options = new JsonSerializerOptions { WriteIndented=true,Encoder=JavaScriptEncoder.UnsafeRelaxedJsonEscaping,Converters={ new JsonStringEnumConverter() } };
    Write(json,JsonSerializer.Serialize(rows,options));
    if(markdown is not null) {
        File.WriteAllText(markdown,MeasurementAnalysis.ToMarkdown(rows),new UTF8Encoding(false));
    }
    foreach(var group in rows.GroupBy(static row => row.Class).OrderBy(static group => group.Key)) {
        Console.Error.WriteLine($"{group.Key}: {group.Count()}");
    }
    return 0;
}

static void Write(string? path,string json) {
    if(path is null) {
        Console.WriteLine(json);
    } else {
        File.WriteAllText(path,json,new UTF8Encoding(false));
    }
}
