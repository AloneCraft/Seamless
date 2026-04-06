using System.Diagnostics;
using Seamless.Collections;
using Tsavorite.core;
namespace Test.Seamless.Benchmark;


public class HashSet {
    [Fact, Trait("Category",Unit)]
    public void ログファイルが作られる(){
        const long Count=1L<<12;
        var CurrentDirectory=Environment.CurrentDirectory;
        Console.WriteLine($"CurrentDirectory: {CurrentDirectory}");
        var CheckpointDir=@$"{CurrentDirectory}\Checkpoint";
        var Log=@$"{CurrentDirectory}\Log\Table1";
        var ObjectLog=@$"{CurrentDirectory}\ObjectLog\Table1";
        Console.WriteLine($"Log: {Log}");
        Console.WriteLine($"ObjectLog: {ObjectLog}");
        using var LogDevice=Devices.CreateLogDevice(Log,DeviceType.Default,false,false,-1,false,false,true,false);
        using var ObjectLogDevice=Devices.CreateLogDevice(ObjectLog,DeviceType.Default,false,false,-1,false,false,true,false);
        const int PageSize=4096,MemorySize=8192;
        const double MutableFraction=(double)PageSize/MemorySize;
        using var store = Store.Create<int>(LogDevice:LogDevice,ObjectLogDevice:ObjectLogDevice,CheckpointDir:CheckpointDir,MutableFraction:MutableFraction,IndexSize:Count,PageSize:PageSize,MemorySize:MemorySize);
            //$$\text{MutableFraction} = \frac{\text{PageSize}}{\text{LogMemorySize}}$$/
        using var h= store.Create();
        var s=Stopwatch.StartNew();
        var ms=s.ElapsedMilliseconds;
        var List=new List<long>();
        for(var a=0;a<Count;a++){
            if(a%(1<<12)==0){
                var next=s.ElapsedMilliseconds;
                List.Add(next-ms);
                Trace.WriteLine($"Elapsed: {next-ms}ms,a:{a},{(double)a/Count}");
                ms =next;
            }
            h.Add(a);
        }
    }
}
