using System.Text.Json;
using Benchmark.Tsavorite.ParameterSweep;

var requestPath=Required(args,"--request");
var workingDirectory=Path.GetFullPath(Required(args,"--working"));
Directory.CreateDirectory(workingDirectory);
var request=JsonSerializer.Deserialize<SweepRequest>(await File.ReadAllTextAsync(requestPath))
    ??throw new InvalidDataException("Parameter sweep request is missing.");
var options=CreateOptions(request.Profile,workingDirectory);
var result=await new パラメータスイープ実行器().ケースを実行Async(request.Case,options,CancellationToken.None);
var json=JsonSerializer.Serialize(result);
await File.WriteAllTextAsync(Path.Combine(workingDirectory,"result.json"),json);
Console.WriteLine(json);
if(result.失敗種別 is パラメータスイープ失敗種別.完了
    or パラメータスイープ失敗種別.ログ満杯
    or パラメータスイープ失敗種別.メモリ不足
    or パラメータスイープ失敗種別.タイムアウト
    or パラメータスイープ失敗種別.メモリ上限超過
    or パラメータスイープ失敗種別.ディスク上限超過){return 0;}
return 1;

static string Required(string[] arguments,string name){
    var index=Array.IndexOf(arguments,name);
    if(index<0||index+1>=arguments.Length){throw new ArgumentException("Missing argument: "+name);}
    return arguments[index+1];
}

static パラメータスイープ実行オプション CreateOptions(string profile,string workingDirectory){
    if(profile=="quick"){
        return new(){
            最大スイープ手順数=2,
            最大成功Count=128,
            検索試行Count=32,
            ケース別タイムアウト=TimeSpan.FromSeconds(2),
            ストレージデバイス容量=16L<<20,
            作業ディレクトリ=workingDirectory,
        };
    }
    if(profile=="full"){
        return new(){作業ディレクトリ=workingDirectory};
    }
    throw new ArgumentException("Unknown parameter sweep profile: "+profile);
}

internal sealed record SweepRequest(string Profile,パラメータスイープケース Case);
