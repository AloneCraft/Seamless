using System.Buffers.Binary;
using System.IO.Hashing;
using Seamless.Transactions;
namespace Seamless.Transactions.Tests;
public sealed class DurabilityBoundaries{
    private static WalDraft Draft()=>new(Guid.NewGuid(),[0],[new(0,1,WalMutation.Upsert,"i32",[1],[2])],[]);
    [Theory][InlineData(1,4194304,67108864L)][InlineData(256,128,128L)]public async Task FullQueueBackpressuresAndStopDrainsAcceptedRequests(int capacity,int maxFrame,long bytes){
        using var device=new FakeWalDevice();device.BlockSync();await using var writer=new WalWriter(device,new WalOptions{QueueCapacity=capacity,MaxFrameBytes=maxFrame,QueueBytes=bytes,MaxBatch=1});
        var first=await writer.AppendAsync(Draft());Assert.True(device.SyncEntered.Wait(TimeSpan.FromSeconds(5)));
        var second=await writer.AppendAsync(Draft());var waiting=writer.AppendAsync(Draft()).AsTask();Assert.False(waiting.IsCompleted);
        var stop=writer.StopAsync().AsTask();await Assert.ThrowsAsync<ObjectDisposedException>(()=>waiting);Assert.False(stop.IsCompleted);
        device.ReleaseSync();await Task.WhenAll(first.Durable,second.Durable,stop).WaitAsync(TimeSpan.FromSeconds(5));Assert.Equal(2,writer.DurableLsn);
    }
    [Fact]public async Task EncodingFailureTerminatesAcceptedAndFutureRequests(){
        using var device=new FakeWalDevice();device.BlockSync();await using var writer=new WalWriter(device);
        var first=await writer.AppendAsync(Draft());Assert.True(device.SyncEntered.Wait(TimeSpan.FromSeconds(5)));
        var bad=Draft() with{Id=Guid.Empty};await Assert.ThrowsAsync<InvalidDataException>(async()=>await writer.AppendAsync(bad));
        Assert.True(first.Durable.IsCompleted);device.ReleaseSync();await Assert.ThrowsAsync<InvalidDataException>(()=>first.Durable);
        await Assert.ThrowsAsync<InvalidDataException>(async()=>await writer.AppendAsync(Draft()));
    }
    [Fact]public async Task ForgottenOutcomeDoesNotReappearAfterPruneOrInterruptedDeletion(){
        var path=Path.Combine(Path.GetTempPath(),"Seamless.C12.Retention",Guid.NewGuid().ToString("N"));
        var options=new WalOptions{SectorBytes=4096,MaxFrameBytes=1024,SegmentBytes=16384};var ids=new List<Guid>();string obsolete;byte[] obsoleteBytes;
        try{
            await using(var journal=new TransactionJournal(path,options)){
                for(var index=0;index<12;index++){var draft=Draft();ids.Add(draft.Id);var receipt=await journal.AppendAsync(draft);await receipt.Durable;}
                await journal.ForgetThroughAsync(1);await journal.CheckpointAndPruneAsync(12);
                obsolete=Directory.GetFiles(path,"segment-*.wal").Order().First();obsoleteBytes=File.ReadAllBytes(obsolete);
            }
            await using(var journal=new TransactionJournal(path,options)){
                await journal.CheckpointAndPruneAsync(12);Assert.False(File.Exists(obsolete));
            }
            // 保持台帳の置換後・旧ファイル削除前の停止を再現する。
            File.WriteAllBytes(obsolete,obsoleteBytes);
            await using(var journal=new TransactionJournal(path,options)){
                Assert.Equal(TransactionOutcomeKind.Unknown,journal.GetOutcome(ids[0]).Kind);
                Assert.Equal(TransactionOutcomeKind.Committed,journal.GetOutcome(ids[^1]).Kind);
            }
            var snapshot=Path.Combine(path,"outcomes.snapshot");var bytes=File.ReadAllBytes(snapshot);bytes[24]^=1;File.WriteAllBytes(snapshot,bytes);
            Assert.Throws<InvalidDataException>(()=>new TransactionJournal(path,options));
        }finally{if(Directory.Exists(path)){Directory.Delete(path,true);}}
    }
    [Fact]public void HeaderGoldenAndLsnGapAreValidated(){
        var path=Path.Combine(Path.GetTempPath(),"Seamless.C12.Gap",Guid.NewGuid().ToString("N"));
        try{
            var frame=WalFormat.Encode(new(1,Draft()));Assert.Equal(0x32465753U,BinaryPrimitives.ReadUInt32LittleEndian(frame));Assert.Equal(1,BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(4)));Assert.Equal(frame.Length,BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(8)));Assert.Equal(1,BinaryPrimitives.ReadInt64LittleEndian(frame.AsSpan(16)));
            using(var device=new WalFileDevice(path)){device.Write(frame);device.Sync();}
            var file=Directory.GetFiles(path,"segment-*.wal").Single();
            using(var stream=new FileStream(file,FileMode.Open,FileAccess.ReadWrite)){
                var header=new byte[32];stream.Position=4096;stream.ReadExactly(header);BinaryPrimitives.WriteInt64LittleEndian(header.AsSpan(16),2);BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(28),Crc32.HashToUInt32(header.AsSpan(0,28)));stream.Position=4096;stream.Write(header);
            }
            Assert.Throws<InvalidDataException>(()=>new WalFileDevice(path));
        }finally{if(Directory.Exists(path)){Directory.Delete(path,true);}}
    }
    [Fact]public async Task OutcomeSnapshotFailureStopsFurtherCommitsWithoutLosingKnownResult(){
        var path=Path.Combine(Path.GetTempPath(),"Seamless.C12.OutcomeFailure",Guid.NewGuid().ToString("N"));
        try{
            await using var journal=new TransactionJournal(path);var draft=Draft();var receipt=await journal.AppendAsync(draft);await receipt.Durable;
            Directory.CreateDirectory(Path.Combine(path,"outcomes.snapshot.new"));
            await Assert.ThrowsAnyAsync<Exception>(async()=>await journal.CheckpointAndPruneAsync(receipt.Lsn));
            Assert.NotNull(journal.Writer.Failure.Error);Assert.Equal(TransactionOutcomeKind.Committed,journal.GetOutcome(draft.Id).Kind);
            await Assert.ThrowsAnyAsync<Exception>(async()=>await journal.AppendAsync(Draft()));
        }finally{if(Directory.Exists(path)){Directory.Delete(path,true);}}
    }
    [Fact]public async Task PrunedPrefixRequiresACoveredCheckpointAndItsOutcomeCatalog(){
        var path=Path.Combine(Path.GetTempPath(),"Seamless.C12.Coverage",Guid.NewGuid().ToString("N"));
        var options=new WalOptions{SectorBytes=4096,MaxFrameBytes=1024,SegmentBytes=16384};
        try{
            await using(var journal=new TransactionJournal(path,options)){
                for(var index=0;index<12;index++){var receipt=await journal.AppendAsync(Draft());await receipt.Durable;}
                await journal.CheckpointAndPruneAsync(12);
            }
            await using(var journal=new TransactionJournal(path,options)){
                await journal.CheckpointAndPruneAsync(12);Assert.True(journal.Device.FirstAvailableLsn>1);
                await using var coordinator=new TransactionCoordinator(journal);coordinator.Register(new DiscardParticipant());
                Assert.Throws<InvalidDataException>(()=>coordinator.Recover(0));
            }
            File.Delete(Path.Combine(path,"outcomes.snapshot"));Assert.Throws<InvalidDataException>(()=>new TransactionJournal(path,options));
        }finally{if(Directory.Exists(path)){Directory.Delete(path,true);}}
    }
    private sealed class DiscardParticipant:ITransactionParticipant,ITransactionChanges{
        public int Partition=>0;public int TableId=>1;public string CodecIdentity=>"i32";
        public ITransactionChanges CreateChanges()=>this;public void Replay(WalEntry entry){}public IReadOnlyList<WalEntry> Encode()=>[];public void Apply(){}public void Dispose(){}
    }
}
