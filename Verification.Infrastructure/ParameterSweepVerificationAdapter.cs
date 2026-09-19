using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Verification.Core;

namespace Verification.Infrastructure;

public sealed class ParameterSweepVerificationAdapter:IVerificationAdapter{
    private static readonly object PreparationGate=new();
    private static readonly HashSet<string> Prepared=[];
    private readonly DotnetVerificationAdapter builder=new();
    public string Name=>"parameter-sweep";

    public async Task PrepareAsync(VerificationCase item,string repositoryRoot,string artifactDirectory,CancellationToken cancellationToken){
        var key=repositoryRoot+'\0'+string.Join('\0',item.PrepareArguments);
        lock(PreparationGate){if(Prepared.Contains(key)){return;}}
        await this.builder.PrepareAsync(item,repositoryRoot,artifactDirectory,cancellationToken).ConfigureAwait(false);
        lock(PreparationGate){Prepared.Add(key);}
    }

    public async Task<CaseResult> ExecuteAsync(VerificationCase item,string repositoryRoot,string artifactDirectory,CancellationToken cancellationToken){
        if(item.Arguments.Length!=1){throw new InvalidDataException("A parameter sweep case requires one serialized request.");}
        var request=Path.Combine(artifactDirectory,"request.json");
        await File.WriteAllTextAsync(request,item.Arguments[0],cancellationToken).ConfigureAwait(false);
        var executable=Path.Combine(repositoryRoot,"Benchmark","Benchmark.Tsavorite.ParameterSweep.Headless","bin","Release","net10.0","Benchmark.Tsavorite.ParameterSweep.Headless.dll");
        if(!File.Exists(executable)){throw new FileNotFoundException("Parameter Sweep Headless Release binary was not built.",executable);}
        var arguments=new[]{"exec",executable,"--request",request,"--working",artifactDirectory};
        await File.WriteAllTextAsync(Path.Combine(artifactDirectory,"command.json"),JsonSerializer.Serialize(arguments),cancellationToken).ConfigureAwait(false);
        using var process=new Process{StartInfo=new("dotnet"){
            WorkingDirectory=repositoryRoot,
            UseShellExecute=false,
            RedirectStandardOutput=true,
            RedirectStandardError=true,
            CreateNoWindow=true,
        }};
        foreach(var argument in arguments){process.StartInfo.ArgumentList.Add(argument);}
        process.Start();
        var outputTask=process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var errorTask=process.StandardError.ReadToEndAsync(CancellationToken.None);
        long peak=0;
        try{
            while(true){
                cancellationToken.ThrowIfCancellationRequested();
                try{
                    process.Refresh();
                    if(process.HasExited){break;}
                    peak=Math.Max(peak,process.PrivateMemorySize64);
                }catch(InvalidOperationException){break;}
                await Task.Delay(25,cancellationToken).ConfigureAwait(false);
            }
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        }catch{
            if(!process.HasExited){process.Kill(entireProcessTree:true);}
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        var output=await outputTask.ConfigureAwait(false);
        var error=await errorTask.ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(artifactDirectory,"stdout.log"),output,CancellationToken.None).ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(artifactDirectory,"stderr.log"),error,CancellationToken.None).ConfigureAwait(false);
        var json=output.Split(['\r','\n'],StringSplitOptions.RemoveEmptyEntries).LastOrDefault(static line=>line.StartsWith('{'));
        if(process.ExitCode!=0||json is null){return new(item.Id,CaseStatus.Failed,item.Conditions,[],null,null,"Parameter Sweep Headless failed with code "+process.ExitCode+": "+error,peak);}
        return Parse(item,json,peak);
    }

    internal static CaseResult Parse(VerificationCase item,string json,long peak){
        using var document=JsonDocument.Parse(json);
        var root=document.RootElement;
        var failure=root.GetProperty("失敗種別").GetInt32();
        var maxCount=root.GetProperty("最大成功Count").GetInt64();
        var entries=root.GetProperty("エントリCount").GetInt64();
        var addMilliseconds=root.GetProperty("追加経過ミリ秒").GetDouble();
        double? searchNanoseconds=null;
        if(root.GetProperty("検索平均ナノ秒").ValueKind==JsonValueKind.Number){searchNanoseconds=root.GetProperty("検索平均ナノ秒").GetDouble();}
        if(maxCount<0||entries<0||!double.IsFinite(addMilliseconds)||addMilliseconds<0){
            return new(item.Id,CaseStatus.Failed,item.Conditions,[],null,null,"Parameter sweep returned invalid measurements.",peak);
        }
        var status=CaseStatus.Passed;
        if(failure>=4){status=CaseStatus.Failed;}
        var measurements=new List<RawMeasurement>();
        if(maxCount>0){measurements.Add(new("sweep-add",0,checked((long)Math.Round(addMilliseconds*1_000_000d,MidpointRounding.AwayFromZero)),maxCount,0));}
        if(searchNanoseconds is { } search&&double.IsFinite(search)&&search>=0){measurements.Add(new("sweep-search",0,checked((long)Math.Round(search,MidpointRounding.AwayFromZero)),1,0));}
        var metrics=new Dictionary<string,double>(StringComparer.Ordinal){
            ["maximum-count"]=maxCount,
            ["entry-count"]=entries,
            ["failure-kind"]=failure,
        };
        if(maxCount>0){metrics["add-nanoseconds-per-operation"]=addMilliseconds*1_000_000d/maxCount;}
        if(searchNanoseconds is { } searchValue){metrics["search-nanoseconds-per-operation"]=searchValue;}
        var message=root.GetProperty("失敗メッセージ").GetString()??string.Empty;
        var diagnostic="maximum-count="+maxCount.ToString(CultureInfo.InvariantCulture)+"; entries="+entries.ToString(CultureInfo.InvariantCulture)+"; failure-kind="+failure.ToString(CultureInfo.InvariantCulture);
        if(message.Length>0){diagnostic+="; "+message;}
        return new(item.Id,status,item.Conditions,[],null,null,diagnostic,peak,measurements.ToArray(),Metrics:metrics);
    }
}
