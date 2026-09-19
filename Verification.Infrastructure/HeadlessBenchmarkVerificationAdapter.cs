using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Verification.Core;

namespace Verification.Infrastructure;

public sealed class HeadlessBenchmarkVerificationAdapter:IVerificationAdapter{
    private static readonly object PreparationGate=new();
    private static readonly HashSet<string> Prepared=[];
    private readonly DotnetVerificationAdapter builder=new();
    public string Name=>"headless-benchmark";

    public async Task PrepareAsync(VerificationCase item,string repositoryRoot,string artifactDirectory,CancellationToken cancellationToken){
        var key=repositoryRoot+'\0'+string.Join('\0',item.PrepareArguments);
        lock(PreparationGate){if(Prepared.Contains(key)){return;}}
        await this.builder.PrepareAsync(item,repositoryRoot,artifactDirectory,cancellationToken).ConfigureAwait(false);
        lock(PreparationGate){Prepared.Add(key);}
    }

    public async Task<CaseResult> ExecuteAsync(VerificationCase item,string repositoryRoot,string artifactDirectory,CancellationToken cancellationToken){
        if(item.Arguments.Length!=3){throw new InvalidDataException("A headless benchmark case requires tab, type, and method arguments.");}
        var executable=Path.Combine(repositoryRoot,"Benchmark","Benchmark.Headless","bin","Release","net10.0-windows10.0.19041","win-x64","Benchmark.Headless.dll");
        if(!File.Exists(executable)){throw new FileNotFoundException("Benchmark.Headless Release binary was not built.",executable);}
        var arguments=new[]{"exec",executable,"--run-tab",item.Arguments[0],"--type",item.Arguments[1],"--method",item.Arguments[2],"--operator-harness"};
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
        if(process.ExitCode!=0){return new(item.Id,CaseStatus.Failed,item.Conditions,[],null,null,"Benchmark.Headless exited with code "+process.ExitCode+": "+error,peak);}
        var json=output.Split(['\r','\n'],StringSplitOptions.RemoveEmptyEntries).LastOrDefault(static line=>line.StartsWith('{'));
        if(json is null){return new(item.Id,CaseStatus.Failed,item.Conditions,[],null,null,"Benchmark.Headless did not return a JSON measurement.",peak);}
        await File.WriteAllTextAsync(Path.Combine(artifactDirectory,"measurement.json"),json,CancellationToken.None).ConfigureAwait(false);
        return Parse(item,json,peak);
    }

    internal static CaseResult Parse(VerificationCase item,string json,long peak){
        using var document=JsonDocument.Parse(json);
        var root=document.RootElement;
        var fTimes=ReadDoubles(root,"FLinqSamples");
        var fAllocations=ReadLongs(root,"FLinqAllocationSamples");
        var bTimes=ReadDoubles(root,"BaselineSamples");
        var bAllocations=ReadLongs(root,"BaselineAllocationSamples");
        if(fTimes.Length<5||bTimes.Length!=fTimes.Length||fAllocations.Length!=fTimes.Length||bAllocations.Length!=fTimes.Length
            ||fTimes.Any(static value=>!double.IsFinite(value)||value<0)||bTimes.Any(static value=>!double.IsFinite(value)||value<0)){
            return new(item.Id,CaseStatus.Failed,item.Conditions,[],null,null,"Headless benchmark returned invalid or insufficient raw samples.",peak);
        }
        var measurements=new List<RawMeasurement>(fTimes.Length*2);
        for(var index=0;index<fTimes.Length;index++){
            measurements.Add(new("flinq",index,Round(fTimes[index]),1,fAllocations[index]));
            measurements.Add(new("baseline",index,Round(bTimes[index]),1,bAllocations[index]));
        }
        var fMedian=Median(fTimes);
        var bMedian=Median(bTimes);
        double? ratio=null;
        if(bMedian!=0){ratio=fMedian/bMedian;}
        var diagnostic="FLinq median="+fMedian.ToString("0.###",CultureInfo.InvariantCulture)
            +" ns; paired baseline median="+bMedian.ToString("0.###",CultureInfo.InvariantCulture)
            +" ns; ratio="+(ratio?.ToString("0.###",CultureInfo.InvariantCulture)??"n/a");
        return new(item.Id,CaseStatus.Passed,item.Conditions,fTimes,checked((long)Math.Round(Median(fAllocations.Select(static value=>(double)value).ToArray()),MidpointRounding.AwayFromZero)),null,diagnostic,peak,measurements.ToArray());
    }

    private static double[] ReadDoubles(JsonElement root,string name)=>root.GetProperty(name).EnumerateArray().Select(static value=>value.GetDouble()).ToArray();
    private static long[] ReadLongs(JsonElement root,string name)=>root.GetProperty(name).EnumerateArray().Select(static value=>value.GetInt64()).ToArray();
    private static long Round(double value)=>checked((long)Math.Round(value,MidpointRounding.AwayFromZero));
    private static double Median(double[] values){
        var copy=(double[])values.Clone();
        Array.Sort(copy);
        return copy[copy.Length/2];
    }
}
