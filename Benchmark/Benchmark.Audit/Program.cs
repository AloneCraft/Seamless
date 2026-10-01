using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Benchmark.Audit;

// 使い方: Benchmark.Audit audit <アセンブリ>... [--json <出力>] [--md <出力>]
if(args.Length==0||args[0]!="audit") {
    Console.Error.WriteLine("使い方: Benchmark.Audit audit <アセンブリ.dll>... [--json <出力.json>] [--md <出力.md>]");
    return 2;
}
var assemblies = new List<string>();
string? jsonPath = null;
string? markdownPath = null;
for(var index = 1;index<args.Length;index++) {
    if(args[index]=="--json") {
        jsonPath=args[++index];
    } else if(args[index]=="--md") {
        markdownPath=args[++index];
    } else {
        assemblies.Add(args[index]);
    }
}
using var catalog = new MetadataCatalog();
var auditor = new IlAuditor(catalog);
var rows = new List<AuditRow>();
foreach(var path in assemblies) {
    rows.AddRange(auditor.Audit(catalog.Load(path)));
}
var options = new JsonSerializerOptions {
    WriteIndented=true,
    Encoder=JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    Converters={ new JsonStringEnumConverter() },
};
var json = JsonSerializer.Serialize(rows,options);
if(jsonPath is null) {
    Console.WriteLine(json);
} else {
    File.WriteAllText(jsonPath,json,new UTF8Encoding(false));
}
if(markdownPath is not null) {
    File.WriteAllText(markdownPath,AuditReport.ToMarkdown(rows),new UTF8Encoding(false));
}
Console.Error.WriteLine($"rows={rows.Count} intercepted={rows.Count(static row => row.Intercepted)} implDirect={rows.Count(static row => row.ImplCalledDirectly)}");
return 0;
