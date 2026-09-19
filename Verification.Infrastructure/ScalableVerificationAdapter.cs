using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Verification.Core;

namespace Verification.Infrastructure;

public sealed class ScalableVerificationAdapter:IVerificationAdapter{
    private static readonly object PreparationGate=new();
    private static readonly HashSet<string> Prepared=[];
    private readonly DotnetVerificationAdapter builder=new();
    public string Name=>"scalable-worker";

    public async Task PrepareAsync(VerificationCase item,string repositoryRoot,string artifactDirectory,CancellationToken cancellationToken){
        var key=repositoryRoot+'\0'+string.Join('\0',item.PrepareArguments);
        lock(PreparationGate){if(Prepared.Contains(key)){return;}}
        await this.builder.PrepareAsync(item,repositoryRoot,artifactDirectory,cancellationToken).ConfigureAwait(false);
        lock(PreparationGate){Prepared.Add(key);}
    }

    public async Task<CaseResult> ExecuteAsync(VerificationCase item,string repositoryRoot,string artifactDirectory,CancellationToken cancellationToken){
        if(item.Arguments.Length!=1){throw new InvalidDataException("A scalable benchmark case requires one serialized request.");}
        var request=Path.Combine(artifactDirectory,"request.json");
        await File.WriteAllTextAsync(request,item.Arguments[0],cancellationToken).ConfigureAwait(false);
        var executable=Path.Combine(repositoryRoot,"Benchmark","Benchmark.Scalable.Headless","bin","Release","net10.0","Benchmark.Scalable.Headless.dll");
        if(!File.Exists(executable)){throw new FileNotFoundException("Scalable Headless Release binary was not built.",executable);}
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
        if(process.ExitCode!=0||json is null){return new(item.Id,CaseStatus.Failed,item.Conditions,[],null,null,"Scalable Headless failed with code "+process.ExitCode+": "+error,peak);}
        return Parse(item,json,item.Arguments[0],peak);
    }

    internal static CaseResult Parse(VerificationCase item,string json,string requestJson,long wrapperPeak){
        using var resultDocument=JsonDocument.Parse(json);
        using var requestDocument=JsonDocument.Parse(requestJson);
        var root=resultDocument.RootElement;
        if(root.GetProperty("状態").GetString()!="Completed"){
            return new(item.Id,CaseStatus.Failed,item.Conditions,[],null,null,root.GetProperty("ログ").GetString(),wrapperPeak);
        }
        var loopCount=requestDocument.RootElement.GetProperty("LoopCount").GetInt32();
        if(loopCount<=0){throw new InvalidDataException("Scalable loop count must be positive.");}
        var raw=root.GetProperty("サンプル一覧").EnumerateArray().Select(static value=>value.GetDouble()).ToArray();
        if(raw.Length<5||raw.Any(static value=>!double.IsFinite(value)||value<0)){
            return new(item.Id,CaseStatus.Failed,item.Conditions,[],null,null,"Scalable Worker returned invalid or insufficient samples.",wrapperPeak);
        }
        var times=raw.Select(value=>value/loopCount).ToArray();
        var allocated=root.GetProperty("割り当てバイト数").GetInt64();
        allocated=checked((long)Math.Round(allocated/(double)loopCount,MidpointRounding.AwayFromZero));
        var gc=root.GetProperty("GC収集回数一覧").EnumerateArray().Select(static value=>value.GetInt32()).ToArray();
        var samples=new RawMeasurement[times.Length];
        for(var index=0;index<times.Length;index++){
            var gen0=0;
            var gen1=0;
            var gen2=0;
            if(index==0&&gc.Length>=3){gen0=gc[0];gen1=gc[1];gen2=gc[2];}
            samples[index]=new("scalable",index,checked((long)Math.Round(raw[index],MidpointRounding.AwayFromZero)),loopCount,allocated,gen0,gen1,gen2);
        }
        var workerPeak=root.GetProperty("ProcessPeakPrivateBytes").GetInt64();
        var peak=Math.Max(wrapperPeak,workerPeak);
        var digest=root.GetProperty("結果ダイジェスト").GetString();
        var diagnostic="median="+Median(times).ToString("0.###",CultureInfo.InvariantCulture)+" ns; loop-count="+loopCount.ToString(CultureInfo.InvariantCulture)+"; worker-peak="+workerPeak.ToString(CultureInfo.InvariantCulture);
        return new(item.Id,CaseStatus.Passed,item.Conditions,times,allocated,digest,diagnostic,peak,samples);
    }

    private static double Median(double[] values){
        var copy=(double[])values.Clone();
        Array.Sort(copy);
        return copy[copy.Length/2];
    }
}
