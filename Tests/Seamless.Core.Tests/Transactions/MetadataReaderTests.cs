using Seamless.Execution;

namespace Seamless.Transactions.Tests;

public sealed class MetadataReaderTests{
    private static string Root()=>Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??throw new InvalidOperationException("Missing isolated root."),"metadata-reader-"+Guid.NewGuid().ToString("N"));
    private static WalOptions Options()=>MetadataRetentionTests.Options() with{MaxFrameBytes=4<<20,SegmentBytes=8<<20,QueueBytes=4<<20};
    private static async Task Append(TransactionJournal journal,byte value,bool preparation=false,int payloadBytes=128){var payload=new byte[payloadBytes];if(payloadBytes>0){payload[0]=value;}var receipt=await journal.AppendAsync(new(Guid.NewGuid(),[0],[],payload){IsPreparation=preparation});await receipt.Durable;}
    [Fact]public async Task PagesRepeatBeforeAckAndContinueWithoutRescanning(){
        await using var journal=new TransactionJournal(Root(),Options());for(byte i=1;i<=5;i++){await Append(journal,i);}
        using(var page=journal.ReadMetadataBatch(maximumRecords:2)){Assert.Equal(new long[]{1,2},page.Records.Select(static row=>row.Lsn));Assert.Equal(2,page.ThroughLsn);Assert.True(page.HasMore);Assert.Equal(0,journal.MetadataAcknowledgedLsn);}
        using(var replay=journal.ReadMetadataBatch(maximumRecords:2)){Assert.Equal(new long[]{1,2},replay.Records.Select(static row=>row.Lsn));journal.AcknowledgeMetadataThrough(replay.ThroughLsn);}
        using(var next=journal.ReadMetadataBatch(maximumRecords:2)){Assert.Equal(new long[]{3,4},next.Records.Select(static row=>row.Lsn));Assert.True(next.ScannedFrames<=4);journal.AcknowledgeMetadataThrough(next.ThroughLsn);}
        using var last=journal.ReadMetadataBatch();Assert.Equal(5,Assert.Single(last.Records).Lsn);Assert.False(last.HasMore);Assert.Equal(4,journal.MetadataAcknowledgedLsn);
    }
    [Fact]public async Task ReopenedPositionMakesProgressWithOneFrameScanBudget(){
        var root=Root();await using(var journal=new TransactionJournal(root,Options())){for(byte i=1;i<=5;i++){await Append(journal,i);}journal.AcknowledgeMetadataThrough(4);}
        await using var reopened=new TransactionJournal(root,Options());var received=new List<long>();var pages=0;do{using var page=reopened.ReadMetadataBatch(maximumFrames:1);received.AddRange(page.Records.Select(static row=>row.Lsn));reopened.AcknowledgeMetadataThrough(page.ThroughLsn);pages++;if(!page.HasMore){break;}Assert.True(pages<20,"Reader repeated the acknowledged prefix.");}while(true);Assert.Equal(new long[]{5},received);
    }
    [Fact]public async Task PayloadAndMemoryLimitsDoNotAcknowledgeOrLeakReservations(){
        var budget=new ResourceBudget(128<<10,32<<20);await using var journal=new TransactionJournal(Root(),Options(),budget);await Append(journal,1,payloadBytes:1024);var before=budget.MemoryBytes;
        Assert.Throws<InvalidDataException>(()=>journal.ReadMetadataBatch(maximumPayloadBytes:512));Assert.Equal(before,budget.MemoryBytes);Assert.Equal(0,journal.MetadataAcknowledgedLsn);
        using(var page=journal.ReadMetadataBatch()){Assert.Equal(1024,Assert.Single(page.Records).Payload.Length);Assert.True(budget.MemoryBytes>before);}Assert.Equal(before,budget.MemoryBytes);
    }
    [Fact]public async Task RawDeviceFramesBeyondWriterDurabilityAreNotVisible(){
        await using var journal=new TransactionJournal(Root(),Options());journal.Device.Write(WalFormat.Encode(new(1,new(Guid.NewGuid(),[0],[],[1]))));journal.Device.Sync();Assert.Equal(0,journal.Writer.DurableLsn);using var page=journal.ReadMetadataBatch();Assert.Empty(page.Records);Assert.Equal(0,page.ThroughLsn);Assert.False(page.HasMore);
    }
    [Fact]public async Task PreparationAndEmptyMetadataAreNotDeliveredAsEvents(){
        await using var journal=new TransactionJournal(Root(),Options());await Append(journal,1,true);await Append(journal,2,payloadBytes:0);await Append(journal,3);using var page=journal.ReadMetadataBatch();Assert.Equal(3,Assert.Single(page.Records).Payload.Span[0]);Assert.Equal(3,page.ThroughLsn);Assert.False(page.HasMore);
    }
    [Fact]public async Task DefaultProfileAndInvalidBoundsAreRejected(){
        await using var original=new TransactionJournal(Root());Assert.Throws<NotSupportedException>(()=>original.ReadMetadataBatch());await using var journal=new TransactionJournal(Root(),Options());Assert.Throws<ArgumentOutOfRangeException>(()=>journal.ReadMetadataBatch(maximumRecords:0));Assert.Throws<ArgumentOutOfRangeException>(()=>journal.ReadMetadataBatch(maximumFrames:0));
    }
    [Fact]public async Task PayloadQuotaSplitsPagesWithoutSkippingTheNextFrame(){
        await using var journal=new TransactionJournal(Root(),Options());await Append(journal,1,payloadBytes:700);await Append(journal,2,payloadBytes:700);using(var first=journal.ReadMetadataBatch(maximumPayloadBytes:1024)){Assert.Equal(1,Assert.Single(first.Records).Lsn);Assert.True(first.HasMore);journal.AcknowledgeMetadataThrough(first.ThroughLsn);}using var second=journal.ReadMetadataBatch(maximumPayloadBytes:1024);Assert.Equal(2,Assert.Single(second.Records).Lsn);Assert.False(second.HasMore);
    }
    [Fact]public async Task BodyCorruptionIsRejectedWithoutAdvancingAcknowledgement(){
        var root=Root();await using var journal=new TransactionJournal(root,MetadataRetentionTests.Options());for(byte i=1;i<=5;i++){await Append(journal,i,payloadBytes:3000);}var first=Directory.GetFiles(root,"segment-*.wal").Order(StringComparer.Ordinal).First();using(var file=new FileStream(first,FileMode.Open,FileAccess.ReadWrite,FileShare.ReadWrite)){file.Position=4096+100;var value=file.ReadByte();file.Position--;file.WriteByte((byte)(value^1));file.Flush(true);}Assert.Throws<InvalidDataException>(()=>journal.ReadMetadataBatch());Assert.Equal(0,journal.MetadataAcknowledgedLsn);
    }
    [Fact]public async Task ResourceFailureReleasesThePageAndDoesNotConsumeTheFrame(){
        var budget=new ResourceBudget(128<<10,32<<20);await using var journal=new TransactionJournal(Root(),Options(),budget);await Append(journal,1,payloadBytes:1024);var before=budget.MemoryBytes;using(var occupied=budget.ReserveMemory(budget.AvailableMemoryBytes-600)){Assert.Throws<ResourceLimitException>(()=>journal.ReadMetadataBatch());}Assert.Equal(before,budget.MemoryBytes);Assert.Equal(0,journal.MetadataAcknowledgedLsn);using var page=journal.ReadMetadataBatch();Assert.Single(page.Records);
    }
    [Fact]public async Task ScanByteLimitMakesProgressAcrossPaddingAndSegments(){
        await using var journal=new TransactionJournal(Root(),MetadataRetentionTests.Options());for(byte i=1;i<=9;i++){await Append(journal,i,payloadBytes:700);}var rows=new List<long>();var pages=0;
        do{using var page=journal.ReadMetadataBatch(maximumScanBytes:8192);Assert.True(page.ScannedBytes<=8192);rows.AddRange(page.Records.Select(static row=>row.Lsn));journal.AcknowledgeMetadataThrough(page.ThroughLsn);if(!page.HasMore){break;}Assert.True(++pages<30,"Byte quota prevented forward progress.");}while(true);Assert.Equal(Enumerable.Range(1,9).Select(static value=>(long)value),rows);
    }
}
