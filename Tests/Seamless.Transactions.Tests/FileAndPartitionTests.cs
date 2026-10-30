using Seamless.Transactions;
namespace Seamless.Transactions.Tests;
public sealed class FileAndPartitionTests{
    private static byte[] ReadShared(string path){using var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);using var memory=new MemoryStream();file.CopyTo(memory);return memory.ToArray();}
    private static WalDraft Draft(int[] parts)=>new(Guid.NewGuid(),parts,[new(parts[0],1,WalMutation.Upsert,"i32",[1],[2])],[]);
    [Fact]public async Task UnrelatedPartitionsExecuteWhileSyncIsBlockedAndReadWaits(){
        using var device=new FakeWalDevice();device.BlockSync();await using var writer=new WalWriter(device);
        await using var scheduler=new PartitionExecutor(writer,[0,0]);
        using var executed=new CountdownEvent(2);
        var first=scheduler.ExecuteAsync([0],()=>{executed.Signal();return new PartitionExecution<int>(1,Draft([0]));});
        var second=scheduler.ExecuteAsync([1],()=>{executed.Signal();return new PartitionExecution<int>(2,Draft([1]));});
        try{
            Assert.True(executed.Wait(TimeSpan.FromSeconds(5)));
            using var readExecuted=new ManualResetEventSlim();
            var read=scheduler.ExecuteAsync([0],()=>{readExecuted.Set();return new PartitionExecution<int>(3,null);});
            Assert.True(readExecuted.Wait(TimeSpan.FromSeconds(5)));Assert.False(first.IsCompleted);Assert.False(second.IsCompleted);Assert.False(read.IsCompleted);
            device.ReleaseSync();await Task.WhenAll(first,second,read).WaitAsync(TimeSpan.FromSeconds(5));
        }finally{device.ReleaseSync();}
    }
    [Fact]public async Task AcknowledgedSectorsAreNeverRewrittenAndEpochsContinue(){
        var path=Path.Combine(Path.GetTempPath(),"Seamless.C12.Wal",Guid.NewGuid().ToString("N"));
        try{
            byte[] prefix;string first;
            using(var device=new WalFileDevice(path)){
                device.Write(WalFormat.Encode(new(1,Draft([0]))));device.Sync();first=Directory.GetFiles(path,"segment-*.wal").Single();prefix=ReadShared(first);
                device.Write(WalFormat.Encode(new(2,Draft([0]))));device.Sync();Assert.Equal(prefix,ReadShared(first).Take(prefix.Length));
            }
            using(var device=new WalFileDevice(path)){
                Assert.Equal(2,device.RecoveredLsn);device.Write(WalFormat.Encode(new(3,Draft([0]))));device.Sync();Assert.Equal(new long[]{1,2,3},device.ReadTransactions().Select(static t=>t.Lsn));
            }
        }finally{if(Directory.Exists(path)){Directory.Delete(path,true);}}
    }
    [Fact]public void TruncatedLastFrameIsRepairedButFullCrcCorruptionFails(){
        var path=Path.Combine(Path.GetTempPath(),"Seamless.C12.Wal",Guid.NewGuid().ToString("N"));
        try{
            using(var device=new WalFileDevice(path)){device.Write(WalFormat.Encode(new(1,Draft([0]))));device.Sync();}
            var last=Directory.GetFiles(path,"segment-*.wal").Order().Last();
            using(var stream=new FileStream(last,FileMode.Append)){var frame=WalFormat.Encode(new(2,Draft([0])));stream.Write(frame.AsSpan(0,frame.Length-1));}
            using(var device=new WalFileDevice(path)){Assert.Equal(1,device.RecoveredLsn);device.Write(WalFormat.Encode(new(2,Draft([0]))));device.Sync();}
            last=Directory.GetFiles(path,"segment-*.wal").Order().Last();
            using(var stream=new FileStream(last,FileMode.Open,FileAccess.ReadWrite)){stream.Position=4096+32+1;var value=stream.ReadByte();stream.Position--;stream.WriteByte((byte)(value^1));}
            Assert.Throws<InvalidDataException>(()=>new WalFileDevice(path));
        }finally{if(Directory.Exists(path)){Directory.Delete(path,true);}}
    }
    [Fact]public async Task StreamingReaderCanResumeOnAnotherThread(){
        var path=Path.Combine(Path.GetTempPath(),"Seamless.C12.Stream",Guid.NewGuid().ToString("N"));
        try{
            using var device=new WalFileDevice(path);device.Write(WalFormat.Encode(new(1,Draft([0]))));device.Sync();
            var completion=new TaskCompletionSource<IEnumerator<WalTransaction>>(TaskCreationOptions.RunContinuationsAsynchronously);
            var thread=new Thread(()=>{
                try{var iterator=device.ReadTransactions().GetEnumerator();Assert.True(iterator.MoveNext());completion.SetResult(iterator);}catch(Exception error){completion.SetException(error);}
            });thread.Start();
            using var reader=await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));thread.Join();Assert.False(reader.MoveNext());
        }finally{if(Directory.Exists(path)){Directory.Delete(path,true);}}
    }
    [Fact]public async Task ShutdownDrainsAnAcceptedCheckpointBeforeDisposingPartitionResources(){
        using var device=new FakeWalDevice();await using var writer=new WalWriter(device);await using var scheduler=new PartitionExecutor(writer,[0]);
        using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
        var checkpoint=Task.Run(()=>scheduler.CheckpointAsync(0,_=>{entered.Set();release.Wait();}));
        try{
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));var stop=scheduler.DisposeAsync().AsTask();Assert.False(stop.IsCompleted);
            await Assert.ThrowsAsync<ObjectDisposedException>(()=>scheduler.CheckpointAsync(0,static _=>{}));
            release.Set();await Task.WhenAll(checkpoint,stop).WaitAsync(TimeSpan.FromSeconds(5));
        }finally{release.Set();}
    }
}
