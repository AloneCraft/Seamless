using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

Console.InputEncoding=Encoding.UTF8;
Console.OutputEncoding=Encoding.UTF8;
var requestPath=Required(args,"--request");
var workingDirectory=Path.GetFullPath(Required(args,"--working"));
Directory.CreateDirectory(workingDirectory);
var request=JsonSerializer.Deserialize<ScalableRequest>(await File.ReadAllTextAsync(requestPath))
    ??throw new InvalidDataException("Scalable benchmark request is missing.");
var workerPath=Path.Combine(AppContext.BaseDirectory,"Worker","Worker.dll");
if(!File.Exists(workerPath)){throw new FileNotFoundException("Benchmark Worker was not copied.",workerPath);}
using var process=new Process{StartInfo=new("dotnet"){
    UseShellExecute=false,
    RedirectStandardInput=true,
    RedirectStandardOutput=true,
    RedirectStandardError=true,
    CreateNoWindow=true,
}};
process.StartInfo.ArgumentList.Add("exec");
process.StartInfo.ArgumentList.Add(workerPath);
process.StartInfo.ArgumentList.Add("--logical-cpu");
process.StartInfo.ArgumentList.Add(request.LogicalCpu.ToString(System.Globalization.CultureInfo.InvariantCulture));
process.StartInfo.ArgumentList.Add("--run-root");
process.StartInfo.ArgumentList.Add(workingDirectory);
process.Start();
var errorTask=process.StandardError.ReadToEndAsync(CancellationToken.None);
try{
    var ready=await process.StandardOutput.ReadLineAsync();
    if(ready!="ready"){throw new InvalidOperationException("Benchmark Worker did not become ready.");}
    var task=new{
        Command="fast-run",
        コマンド="fast-run",
        タスクID=Guid.NewGuid().ToString("N"),
        アセンブリ名=request.AssemblyName,
        型完全名=request.TypeName,
        メソッド名=request.MethodName,
        引数キー=string.Join(",",request.Arguments.Select(static pair=>pair.Key+"="+pair.Value)),
        引数=request.Arguments.Select(static pair=>new{名前=pair.Key,値=pair.Value}).ToArray(),
        ソースタブ=0,
        準備済み要素数=Array.Empty<int>(),
        要素数=request.ElementCount,
        ループ回数=request.LoopCount,
        要素倍率="1",
        成果物フォルダー=workingDirectory,
        割り当て論理CPU=request.LogicalCpu,
        開放ループ較正か=false,
        較正閾値ナノ秒=(double?)null,
        ベンチマーク反復回数=1,
        ウォームアップ目標ナノ秒=300_000_000d,
    };
    await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(task));
    await process.StandardInput.FlushAsync();
    var resultTask=process.StandardOutput.ReadLineAsync();
    long peak=0;
    while(!resultTask.IsCompleted){
        try{process.Refresh();peak=Math.Max(peak,process.PrivateMemorySize64);}catch(InvalidOperationException){break;}
        await Task.Delay(25);
    }
    var result=await resultTask??throw new InvalidOperationException("Benchmark Worker ended without a result.");
    var node=JsonNode.Parse(result)?.AsObject()??throw new InvalidDataException("Benchmark Worker returned invalid JSON.");
    node["ProcessPeakPrivateBytes"]=peak;
    result=node.ToJsonString();
    await File.WriteAllTextAsync(Path.Combine(workingDirectory,"result.json"),result);
    Console.WriteLine(result);
    await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new{Command="shutdown",コマンド="shutdown"}));
    await process.StandardInput.FlushAsync();
    await process.WaitForExitAsync();
    using var document=JsonDocument.Parse(result);
    return document.RootElement.GetProperty("終了コード").GetInt32();
}finally{
    if(!process.HasExited){process.Kill(entireProcessTree:true);await process.WaitForExitAsync();}
    await File.WriteAllTextAsync(Path.Combine(workingDirectory,"worker-stderr.log"),await errorTask);
}

static string Required(string[] arguments,string name){
    var index=Array.IndexOf(arguments,name);
    if(index<0||index+1>=arguments.Length){throw new ArgumentException("Missing argument: "+name);}
    return arguments[index+1];
}

internal sealed record ScalableRequest(
    string AssemblyName,
    string TypeName,
    string MethodName,
    IReadOnlyDictionary<string,string> Arguments,
    int ElementCount,
    int LoopCount,
    int LogicalCpu);
