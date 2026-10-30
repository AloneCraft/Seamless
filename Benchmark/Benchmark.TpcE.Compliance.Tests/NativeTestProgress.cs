using System.Diagnostics;
using System.Text.Json;
namespace Benchmark.TpcE.Compliance.Tests;
/// <summary>実 EGen 試験の段階と資源量を専用試験領域へ記録する。</summary>
internal sealed class NativeTestProgress:IDisposable{
    private static readonly object Gate=new();
    private readonly string phase;
    private readonly Stopwatch timer=Stopwatch.StartNew();
    private NativeTestProgress(string phase){this.phase=phase;this.Record("begin");}
    internal static long SegmentBytes{
        get{
            var setting=Environment.GetEnvironmentVariable("TPCE_TEST_SEGMENT_BYTES");
            if(setting is null){return 16L*1024*1024;}
            return long.Parse(setting,System.Globalization.CultureInfo.InvariantCulture);
        }
    }
    internal static NativeTestProgress Measure(string phase)=>new(phase);
    internal static void StoreFiles(string backend,int attempt,Seamless.Execution.Store store){
        var count=Directory.EnumerateFiles(store.DirectoryPath!,"*",SearchOption.AllDirectories).LongCount();
        Write(new{time=DateTimeOffset.UtcNow,phase="recovery-store-files",backend,attempt,files=count});
    }
    private void Record(string state){
        using var process=Process.GetCurrentProcess();
        Write(new{time=DateTimeOffset.UtcNow,this.phase,state,seconds=this.timer.Elapsed.TotalSeconds,cpuSeconds=process.TotalProcessorTime.TotalSeconds,handles=process.HandleCount,workingSet=process.WorkingSet64,segmentBytes=SegmentBytes});
    }
    private static void Write(object value){
        var root=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT");
        if(root is null){return;}
        lock(Gate){File.AppendAllText(Path.Combine(root,"tpce-phases.jsonl"),JsonSerializer.Serialize(value)+Environment.NewLine);}
    }
    public void Dispose()=>this.Record("end");
}
