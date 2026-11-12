using System.Security.Cryptography;
using System.Text.Json;
using Seamless.Collections;
using Seamless.Collections.Tsavorite;
using Seamless.Execution;
using Seamless.Execution.Growth;
using ExecutionStore=Seamless.Execution.Store;

namespace Management.Tests;

/// <summary>明示指定した隔離Fルートだけで実行する、保存・故障挙動の観測プローブ。</summary>
public sealed class HistoryPeriodProbe{
    private const int Rows=2048;
    private static readonly TsavoriteTuning Tuning=new(){IndexSize=1024,PageSize=4096,SegmentSize=16384,LogMemorySize=8192,Growth=new Policy<Fixed>()};
    private static readonly StoreLimits Limits=new(){MemoryBytes=32L<<20,DiskBytes=16L<<20};
    [Fact]public void Probe(){
        var configured=Environment.GetEnvironmentVariable("SEAMLESS_HISTORY_PERIOD_PROBE_ROOT");
        if(configured is null){return;}
        var root=Path.GetFullPath(configured);
        Assert.StartsWith(Path.GetFullPath(@"F:\Seamless\history-period-probe-"),root,StringComparison.OrdinalIgnoreCase);
        var mode=Environment.GetEnvironmentVariable("SEAMLESS_HISTORY_PERIOD_PROBE_CASE")??throw new InvalidOperationException("Specify an isolated probe case.");
        Directory.CreateDirectory(root);var output=Path.Combine(root,mode);Directory.CreateDirectory(output);
        var source=Path.Combine(root,"source-period");
        if(mode=="create"){
            Assert.False(Directory.Exists(source));
            Write(source,output);
            return;
        }
        Assert.True(File.Exists(Path.Combine(source,"store.json")),"The confirmed source checkpoint is required before fault injection.");
        if(mode=="reopen"){
            var baseline=Read(source,output);Assert.Equal("Read",baseline.Outcome);Assert.Equal(Rows,baseline.ActualRows);Assert.Equal(0,baseline.Mismatches);return;
        }
        Assert.True(File.Exists(Path.Combine(root,"reopen","outcome.json")),"Confirm a separate-process reopen before fault injection.");
        var copy=Path.Combine(output,"period-1");Assert.False(Directory.Exists(copy));Copy(source,copy);
        var target=Mutate(mode,copy,output);
        var result=Read(copy,output);File.WriteAllText(Path.Combine(output,"mutation-result.json"),JsonSerializer.Serialize(new{Mode=mode,Target=target,Result=result},new JsonSerializerOptions{WriteIndented=true}));
        if(mode=="independent"){
            var healthy=Path.Combine(output,"period-2");Copy(source,healthy);var health=Read(healthy,Path.Combine(output,"healthy"));
            Assert.Equal("Read",health.Outcome);Assert.Equal(Rows,health.ActualRows);Assert.Equal(0,health.Mismatches);
        }
    }
    private static void Write(string path,string output){
        using(var owner=ExecutionStore.Open(path,Limits)){
            var data=owner.CreateTsavoriteSet<long,Row>("frames",Tuning);
            using(var rows=data.Create()){
                for(var key=0;key<Rows;key++){Assert.True(rows.Add(Expected(key)));}
                Assert.Equal(Rows,rows.Count);
            }
            Stage(output,"BeforeSave");var checkpoint=owner.Save();
            File.WriteAllText(Path.Combine(output,"save.json"),JsonSerializer.Serialize(new{Checkpoint=checkpoint,DataStoreType=data.GetType().AssemblyQualifiedName,NativeStoreType=data.Store.GetType().AssemblyQualifiedName,
                PageSize=Tuning.PageSize,data.LogMemorySize,TailAddress=data.Store.Log.TailAddress,BeginAddress=data.Store.Log.BeginAddress,MemoryBytes=owner.Budget.MemoryBytes,DiskBytes=owner.Budget.TemporaryStorageBytes,Count=Rows}));
        }
        var inventory=Inventory(path);File.WriteAllText(Path.Combine(output,"inventory.json"),JsonSerializer.Serialize(inventory,new JsonSerializerOptions{WriteIndented=true}));
        Assert.Contains(inventory,item=>item.Path=="store.json"&&item.Bytes>0);Assert.Contains(inventory,item=>item.Path.Contains("index-checkpoints",StringComparison.Ordinal)&&item.Bytes>0);
        Assert.Contains(inventory,item=>item.Path.Contains("cpr-checkpoints",StringComparison.Ordinal)&&item.Bytes>0);
        Assert.Contains(inventory,item=>LogSegment(item.Path)&&item.Bytes>0);Stage(output,"CreatedAndClosed");
    }
    private static ReadOutcome Read(string path,string output){
        Directory.CreateDirectory(output);Stage(output,"BeforeOpen");ReadOutcome result;
        try{
            using var owner=ExecutionStore.Open(path,Limits);Stage(output,"BeforeRegister");var data=owner.CreateTsavoriteSet<long,Row>("frames",Tuning);
            Stage(output,"BeforeEnumerate");using var rows=data.Create();var read=rows.ToArray();var expected=Enumerable.Range(0,Rows).Select(key=>Expected(key)).ToArray();
            var wrong=read.Count(row=>row.Key<0||row.Key>=Rows||row!=Expected(row.Key));
            var missing=expected.Count(row=>!read.Contains(row));
            result=new("Read",read.Length,wrong+missing,rows.GetLongCount(),Digest(read),null,null,owner.CurrentCheckpoint?.ToString());
        }catch(Exception exception){result=new("Exception",0,0,null,null,exception.GetType().FullName,exception.ToString(),null);}
        File.WriteAllText(Path.Combine(output,"outcome.json"),JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));Stage(output,"Completed");return result;
    }
    private static string Mutate(string mode,string copy,string output){
        var files=Inventory(copy);string target;var action="DeleteFile";
        switch(mode){
            case "delete-index":target=Path.Combine(copy,files.Where(item=>item.Path.Contains("index-checkpoints",StringComparison.Ordinal)&&item.Bytes>0).MaxBy(item=>item.Bytes)!.Path);break;
            case "delete-checkpoint":
            case "independent":target=Directory.EnumerateDirectories(copy,"cpr-checkpoints",SearchOption.AllDirectories).Single();action="DeleteDirectory";break;
            case "delete-log":target=Path.Combine(copy,files.Where(item=>LogSegment(item.Path)&&item.Bytes>0).OrderBy(item=>item.Path,StringComparer.Ordinal).First().Path);break;
            case "corrupt-log":target=Path.Combine(copy,files.Where(item=>LogSegment(item.Path)&&item.Bytes>0).OrderBy(item=>item.Path,StringComparer.Ordinal).First().Path);action="CorruptByte";break;
            case "corrupt-checkpoint":target=Path.Combine(copy,files.Where(item=>item.Path.Contains("cpr-checkpoints",StringComparison.Ordinal)&&item.Bytes>0).MaxBy(item=>item.Bytes)!.Path);action="CorruptByte";break;
            case "delete-manifest":target=Path.Combine(copy,"store.json");break;
            default:throw new InvalidOperationException("Unknown isolated fault case.");
        }
        target=Path.GetFullPath(target);var prefix=Path.GetFullPath(copy)+Path.DirectorySeparatorChar;
        Assert.StartsWith(prefix,target,StringComparison.OrdinalIgnoreCase);
        File.WriteAllText(Path.Combine(output,"mutation.json"),JsonSerializer.Serialize(new{Action=action,AbsoluteTarget=target,VerifiedCopyRoot=prefix,Before=files},new JsonSerializerOptions{WriteIndented=true}));
        if(action=="DeleteDirectory"){Directory.Delete(target,true);}
        else if(action=="DeleteFile"){File.Delete(target);}
        else{
            using var file=new FileStream(target,FileMode.Open,FileAccess.ReadWrite,FileShare.None);var offset=file.Length/2;file.Position=offset;var before=file.ReadByte();Assert.True(before>=0);
            file.Position=offset;file.WriteByte((byte)(before^0x5a));file.Flush(true);
            File.WriteAllText(Path.Combine(output,"corruption.json"),JsonSerializer.Serialize(new{Offset=offset,Before=before,After=before^0x5a,Length=file.Length}));
        }
        Stage(output,"MutationApplied");return target;
    }
    private static void Copy(string source,string target){
        Directory.CreateDirectory(target);
        foreach(var path in Directory.EnumerateFiles(source,"*",SearchOption.AllDirectories)){
            var destination=Path.Combine(target,Path.GetRelativePath(source,path));Directory.CreateDirectory(Path.GetDirectoryName(destination)!);File.Copy(path,destination,false);
        }
    }
    private static FileEntry[] Inventory(string root)=>Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories).Select(path=>new FileEntry(Path.GetRelativePath(root,path),new FileInfo(path).Length,Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))))).OrderBy(item=>item.Path,StringComparer.Ordinal).ToArray();
    private static bool LogSegment(string path)=>!path.Contains("checkpoints",StringComparison.Ordinal)&&Path.GetFileName(path).Contains("hlog",StringComparison.Ordinal);
    private static string Digest(IEnumerable<Row> rows){using var buffer=new MemoryStream();using var writer=new BinaryWriter(buffer);foreach(var row in rows.OrderBy(row=>row.Key)){writer.Write(row.Key);writer.Write(row.Value);writer.Write(row.A);writer.Write(row.B);writer.Write(row.C);writer.Write(row.D);}writer.Flush();return Convert.ToHexString(SHA256.HashData(buffer.ToArray()));}
    private static Row Expected(long key)=>new(key,key*3,key^0x42,~key,key+101,key-101);
    private static void Stage(string output,string stage)=>File.WriteAllText(Path.Combine(output,"stage.json"),JsonSerializer.Serialize(new{Stage=stage,PID=Environment.ProcessId,At=DateTimeOffset.UtcNow}));
    public readonly record struct Row(long Key,long Value,long A,long B,long C,long D):IRecord<long>;
    private sealed record FileEntry(string Path,long Bytes,string SHA256);
    private sealed record ReadOutcome(string Outcome,int ActualRows,int Mismatches,long? ReportedCount,string? Digest,string? ExceptionType,string? Exception,string? Checkpoint);
}
