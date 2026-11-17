using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using Seamless.Execution;
using TestSeamless;
namespace HistoryStoreModes;

internal static partial class Program{
    private const int Samples=128;
    private static readonly JsonSerializerOptions Json=new(){WriteIndented=true};
    private static int Main(string[] args){
        try{
            if(args.Length is not(4 or 5)||args[0] is not("--verify" or "--run")||args.Length==5&&(args[0]!="--run"||args[4]!="--buffered")){throw new ArgumentException("--verify|--run F:data F:report source-manifest-sha256 [--buffered]");}
            var root=Path.GetFullPath(args[1]);var report=Path.GetFullPath(args[2]);
            if(Path.GetPathRoot(root)!="F:\\"||Path.GetPathRoot(report)!="F:\\"||Directory.Exists(root)&&Directory.EnumerateFileSystemEntries(root).Any()){throw new ArgumentException("Use a fresh private F root and F report.");}
            Directory.CreateDirectory(root);Directory.CreateDirectory(Path.GetDirectoryName(report)!);
            if(args[0]=="--verify"){Verify(root);File.WriteAllText(report,JsonSerializer.Serialize(new{Outcome="Passed",Format="history-store-modes-verification-v1",SourceManifestSha256=args[3],Checks=checks},Json));}
            else{Run(root,report,args[3],args.Length==5).GetAwaiter().GetResult();}
            return 0;
        }catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
    private static IBackend Open<TKey>(string root,Settings settings,bool dedicated) where TKey:unmanaged{
        if(dedicated){return new DedicatedHistory<TKey>(root,settings);}return new NativeHistory<TKey>(root,settings);
    }
    private static void Fill(Span<byte> row,long id,long version){
        BinaryPrimitives.WriteInt64LittleEndian(row,id);BinaryPrimitives.WriteInt64LittleEndian(row[8..],version);
        for(var index=16;index<row.Length;index++){row[index]=unchecked((byte)(id*31+version*17+index));}
    }
    private static bool Deleted(long id,int version,int versions)=>version>1&&((version==2&&id%17==0)||(version==versions&&id%19==0));
    private static void Assert([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)]bool condition,string message){if(!condition){throw new InvalidDataException(message);}}
    private static void Validate(Observation? value,long id,int version,Settings settings,bool reclaimed=false){
        var deleted=Deleted(id,version,settings.Versions);
        if(reclaimed&&deleted){Assert(value is null,$"Deleted head survived reclamation: id={id}, version={value?.Version}, deleted={value?.Deleted}");return;}
        Assert(value.HasValue,"Expected row/version is absent");var actual=value.Value;
        Assert(actual.Version==version&&actual.Deleted==deleted&&actual.Row.Length==settings.RowBytes,"Version/deletion/row size differs");
        Span<byte> expected=stackalloc byte[settings.RowBytes];Fill(expected,id,version);Assert(expected.SequenceEqual(actual.Row),"Payload differs from independent pattern");
    }
    private static void ValidateAll(IBackend backend,Settings settings,bool reclaimed=false){
        for(var id=0;id<settings.Keys;id++){Validate(backend.Read(id,long.MaxValue),id,settings.Versions,settings,reclaimed);}
        if(!reclaimed){for(var version=1;version<=settings.Versions;version++){for(var id=0;id<Math.Min(17,settings.Keys);id++){Validate(backend.Read(id,version),id,version,settings);}}}
    }
    private static (double Milliseconds,long Allocated) Measure(Action body){var allocated=GC.GetTotalAllocatedBytes(true);var started=Stopwatch.GetTimestamp();body();return(Stopwatch.GetElapsedTime(started).TotalMilliseconds,GC.GetTotalAllocatedBytes(true)-allocated);}
    private static object ReadSample(IBackend backend,Settings settings,int version){
        var latency=new double[Samples];long checksum=0;
        var measure=Measure(()=>{
            for(var index=0;index<Samples;index++){
                var id=(index*761+13)%settings.Keys;var started=Stopwatch.GetTimestamp();var row=backend.Read(id,version);latency[index]=Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                Validate(row,id,version,settings);checksum=checked(checksum+row!.Value.Row[16]+row.Value.Version);
            }
        });
        Array.Sort(latency);return new{Version=version,Count=Samples,measure.Milliseconds,AllocatedBytes=measure.Allocated,P50=latency[63],P95=latency[121],P99=latency[126],Checksum=checksum};
    }
    private static async Task Run(string root,string report,string sourceHash,bool buffered){
        var records=new List<object>();
        var ioMode="NativeDefault_ArenaBuffered";if(buffered){ioMode="BufferedRandomAccess";}
        async Task Save(){await File.WriteAllTextAsync(report,JsonSerializer.Serialize(new{Format="history-store-modes-v1",SourceManifestSha256=sourceHash,AssemblySha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Program).Assembly.Location))),TsavoriteSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(global::Tsavorite.core.KVSettings).Assembly.Location))),
            Affinity=Affinity(),ProcessorCount=Environment.ProcessorCount,IoMode=ioMode,Repeats=5,Keys=1024,ReadSamples=Samples,ParentMemoryLimit=64L<<20,ArenaLogicalLimit=8L<<20,
            Scope="sequential backend prototype; append is not WAL commit; no product replacement",CacheMeaning="head native log plus separate arena cache; total actual bytes reported",IoMeaning="OS process transfer counters; application eviction does not clear OS cache",ReadBatchMeaning="evict once before the batch; not a per-request cold-disk test",Records=records},Json));}
        foreach(var shape in new[]{(Row:32,Versions:1),(Row:32,Versions:8),(Row:32,Versions:64),(Row:512,Versions:1),(Row:512,Versions:8)}){
            foreach(var keyBytes in new[]{8,32}){
                var storages=new[]{"Memory","ResidentStorage","SmallCacheStorage"};if(buffered){storages=["ResidentStorage","SmallCacheStorage"];}
                foreach(var storage in storages){
                    foreach(var indexBytes in new[]{64L,32768L}){
                        var logBytes=8<<20;var persistent=storage!="Memory";if(storage=="SmallCacheStorage"){logBytes=64<<10;}
                        var settings=new Settings(persistent,indexBytes,logBytes,shape.Row,1024,shape.Versions,buffered);
                        for(var repeat=-1;repeat<5;repeat++){
                            for(var order=0;order<2;order++){
                                var mode=(order+repeat+2)%2;var dedicated=mode==1;var id=Guid.NewGuid().ToString("N");var directory=Path.Combine(root,id);Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"experiment-owner.txt"),id);
                                File.WriteAllText(Path.Combine(directory,"case.json"),JsonSerializer.Serialize(new{storage,keyBytes,settings,repeat,order,dedicated},Json));
                                object result;
                                if(keyBytes==8){result=await RunCase<long>(directory,settings,dedicated);}else{result=await RunCase<WideKey>(directory,settings,dedicated);}
                                if(repeat>=0){records.Add(new{Storage=storage,KeyBytes=keyBytes,settings.RowBytes,settings.Versions,settings.IndexBytes,settings.LogBytes,Repeat=repeat,Order=order,Dedicated=dedicated,Result=result});}
                                // 各caseの保存と再openを検証してから専用データを回収し、全case分の領域を累積しない。
                                var target=Path.GetFullPath(directory);if(!target.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||File.ReadAllText(Path.Combine(target,"experiment-owner.txt"))!=id){throw new InvalidOperationException("Refuse cleanup outside the owned case.");}Directory.Delete(target,true);
                            }
                        }
                        await Save();Console.WriteLine($"Complete: {storage}, key={keyBytes}, row={shape.Row}, versions={shape.Versions}, index={indexBytes}, samples={records.Count}");
                    }
                }
            }
        }
        await Save();
    }
    private static async Task<object> RunCase<TKey>(string directory,Settings settings,bool dedicated) where TKey:unmanaged{
        GC.Collect();GC.WaitForPendingFinalizers();
        using var process=Process.GetCurrentProcess();await using var metrics=new ProcessMetrics(process.Id,process.StartTime.ToUniversalTime());
        var backend=Open<TKey>(directory,settings,dedicated);
        object before,after,warmLatest,warmOld;object? evictedOld=null;double save,reclaim,reopen=0;object footprintBefore,footprintAfter;var row=new byte[settings.RowBytes];
        (double Milliseconds,long Allocated) append;
        try{
            append=Measure(()=>{for(var version=1;version<=settings.Versions;version++){for(var id=0;id<settings.Keys;id++){Fill(row,id,version);backend.Append(id,version,Deleted(id,version,settings.Versions),row);}}});
            ValidateAll(backend,settings);save=Measure(backend.Save).Milliseconds;warmLatest=ReadSample(backend,settings,settings.Versions);warmOld=ReadSample(backend,settings,1);
            if(settings.Persistent){backend.Evict();evictedOld=ReadSample(backend,settings,1);}
            before=backend.Metrics();footprintBefore=Footprint(directory);
            if(settings.Persistent){backend.Dispose();Assert(backend.MemoryBytes==0&&backend.StorageBytes==0,"Owner leaked resources");var started=Stopwatch.GetTimestamp();backend=Open<TKey>(directory,settings,dedicated);reopen=Stopwatch.GetElapsedTime(started).TotalMilliseconds;ValidateAll(backend,settings);}
            reclaim=Measure(backend.Reclaim).Milliseconds;ValidateAll(backend,settings,true);after=backend.Metrics();footprintAfter=Footprint(directory);
        }finally{backend.Dispose();}
        Assert(backend.MemoryBytes==0&&backend.StorageBytes==0,"Owner leaked resources");
        var resources=await metrics.FinishAsync();Assert(resources.Status=="Available","OS process counters unavailable: "+resources.Reason);
        return new{AppendMilliseconds=append.Milliseconds,AppendAllocatedBytes=append.Allocated,AppendedVersions=settings.Keys*settings.Versions,SaveMilliseconds=save,ReopenMilliseconds=reopen,ReclaimMilliseconds=reclaim,WarmLatest=warmLatest,WarmOld=warmOld,EvictedOld=evictedOld,Before=before,After=after,FootprintBefore=footprintBefore,FootprintAfter=footprintAfter,Resources=resources};
    }
    private static long Affinity(){if(OperatingSystem.IsWindows()||OperatingSystem.IsLinux()){return Process.GetCurrentProcess().ProcessorAffinity.ToInt64();}throw new PlatformNotSupportedException();}
    private static object Footprint(string root){
        long arena=0,other=0;var files=0;
        foreach(var file in Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories)){if(Path.GetFileName(file) is "experiment-owner.txt" or "case.json"){continue;}var length=new FileInfo(file).Length;files++;if(file.Contains(Path.DirectorySeparatorChar+"arena"+Path.DirectorySeparatorChar,StringComparison.Ordinal)){arena+=length;}else{other+=length;}}
        return new{ArenaFileBytes=arena,NativeAndManifestBytes=other,TotalBytes=arena+other,Files=files,Meaning="logical file lengths; not physical allocation or cumulative writes"};
    }
}
