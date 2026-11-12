using Seamless.Execution;
using Seamless.Management;
using Seamless.Management.Server;

namespace Management.Tests;

[Collection("AuditStorage")]
public sealed class ExecutionHistoryIntegrity{
    [Theory][InlineData("directory")][InlineData("manifest")]
    public void APeriodRemovedAfterValidationIsNotRecreatedByOpeningTheStore(string removal){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"period-open-race-"+Guid.NewGuid().ToString("N"));
        var budget=new ResourceBudget(64L<<20,128L<<20);var writer=new ExecutionHistoryPeriods(root,budget);
        var observation=new QueryExecutionObservation(Guid.NewGuid(),"definition","Value",1,new('A',64),true,DateTimeOffset.UnixEpoch,DateTimeOffset.UnixEpoch,TimeSpan.Zero,TimeSpan.Zero,null,0,0,false,"Succeeded","Execution",null);
        var entry=new QueryExecutionHistoryEntry(Guid.NewGuid(),1,observation);var period=writer.Write([entry]);
        var manifest=Path.Combine(period,"store.json");
        var reader=new ExecutionHistoryPeriods(root,budget,beforeOpen:()=>{
            if(removal=="directory"){Directory.Delete(period,true);}else{File.Delete(manifest);}
        });
        if(removal=="directory"){
            Assert.Throws<DirectoryNotFoundException>(()=>reader.Read(period,CancellationToken.None));Assert.False(Directory.Exists(period));
        }else{
            Assert.Throws<FileNotFoundException>(()=>reader.Read(period,CancellationToken.None));Assert.False(File.Exists(manifest));
            File.Delete(Path.Combine(period,"store.lock"));Assert.Throws<FileNotFoundException>(()=>Store.OpenExisting(period));Assert.False(File.Exists(Path.Combine(period,"store.lock")));
        }
        var normal=Path.Combine(root,"normal-open");using(var owner=Store.Open(normal)){Assert.True(Directory.Exists(normal));Assert.Null(owner.CurrentCheckpoint);}
        Assert.Equal(0,budget.MemoryBytes);
    }
    [Fact]public async Task IncompleteStagingStopsOnlyNewWritesAndHealthyPeriodsRemainReadable(){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"period-incomplete-"+Guid.NewGuid().ToString("N"));
        var budget=new ResourceBudget(64L<<20,128L<<20);
        var observation=new QueryExecutionObservation(Guid.NewGuid(),"definition","Value",1,new('A',64),true,DateTimeOffset.UnixEpoch,DateTimeOffset.UnixEpoch,TimeSpan.Zero,TimeSpan.Zero,null,0,0,false,"Succeeded","Execution",null);
        var entry=new QueryExecutionHistoryEntry(Guid.NewGuid(),1,observation);_=new ExecutionHistoryPeriods(root,budget).Write([entry]);
        var incomplete=Path.Combine(root,"writing-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(incomplete);File.WriteAllText(Path.Combine(incomplete,"partial"),"interrupted");
        await using var recorder=new QueryExecutionHistoryStore(root,budget);Assert.True(recorder.TryRecord(observation with{ExecutionId=Guid.NewGuid()}));await recorder.DisposeAsync();
        var page=await recorder.ReadAsync(new("definition","Value"),CancellationToken.None);
        Assert.Equal(entry,Assert.Single(page.Items));Assert.Equal("IncompletePeriod",page.Status.ErrorCode);Assert.Equal(1,page.Status.Dropped);Assert.Equal(1,page.InvalidFiles);
        Assert.Single(Directory.GetDirectories(root,"writing-*"));Assert.Single(Directory.GetDirectories(root,"period-*"));Assert.Equal("interrupted",File.ReadAllText(Path.Combine(incomplete,"partial")));
        Assert.Equal(0,budget.MemoryBytes);
    }
    [Fact]public async Task ReadBudgetExhaustionIsLimitedWithoutReportingAValidPeriodAsCorrupt(){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"period-budget-"+Guid.NewGuid().ToString("N"));
        var writeBudget=new ResourceBudget(64L<<20,128L<<20);
        var observation=new QueryExecutionObservation(Guid.NewGuid(),"definition","Value",1,new('A',64),true,DateTimeOffset.UnixEpoch,DateTimeOffset.UnixEpoch,TimeSpan.Zero,TimeSpan.Zero,null,0,0,false,"Succeeded","Execution",null);
        _=new ExecutionHistoryPeriods(root,writeBudget).Write([new(Guid.NewGuid(),1,observation)]);
        var readBudget=new ResourceBudget(1L<<20,128L<<20);await using var reader=new QueryExecutionHistoryStore(root,readBudget);
        var page=await reader.ReadAsync(new("definition","Value"),CancellationToken.None);
        Assert.Empty(page.Items);Assert.True(page.ScanLimited);Assert.Equal(0,page.InvalidFiles);Assert.Equal("ReadMemoryBudget",page.Status.ErrorCode);
        Assert.Equal(0,writeBudget.MemoryBytes);Assert.Equal(0,readBudget.MemoryBytes);
    }
    [Fact]public async Task SealedPeriodStaysReadableAcrossRepeatedNativeRecoveriesAndNewRecorderInstances(){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"period-reopen-"+Guid.NewGuid().ToString("N"));
        var budget=new ResourceBudget(64L<<20,128L<<20);var periods=new ExecutionHistoryPeriods(root,budget);
        var observation=new QueryExecutionObservation(Guid.NewGuid(),"definition","Value",1,new('A',64),true,DateTimeOffset.UnixEpoch,DateTimeOffset.UnixEpoch,TimeSpan.Zero,TimeSpan.Zero,null,0,0,false,"Succeeded","Execution",null);
        var entry=new QueryExecutionHistoryEntry(Guid.NewGuid(),1,observation);var period=periods.Write([entry]);
        for(var index=0;index<3;index++){Assert.Equal(entry,Assert.Single(periods.Read(period,CancellationToken.None)));}
        Assert.Equal(entry,Assert.Single(new ExecutionHistoryPeriods(root,budget).Read(period,CancellationToken.None)));
        await using var reader=new QueryExecutionHistoryStore(root,budget);
        var page=await reader.ReadAsync(new("definition","Value"),CancellationToken.None);Assert.Equal(entry,Assert.Single(page.Items));Assert.Equal(0,page.InvalidFiles);
        await reader.DisposeAsync();Assert.Equal(0,budget.MemoryBytes);
    }
    [Fact]public void MemoryPackRowIntegrityCoversKeySessionAndSemanticPayload(){
        var observation=new QueryExecutionObservation(Guid.NewGuid(),"definition","Value",1,new('A',64),true,DateTimeOffset.UnixEpoch,DateTimeOffset.UnixEpoch,TimeSpan.Zero,TimeSpan.Zero,null,0,0,false,"Succeeded","Execution",null);
        var entry=new QueryExecutionHistoryEntry(Guid.NewGuid(),7,observation);var row=ExecutionHistoryPeriods.Encode(entry);
        Assert.Equal(entry,ExecutionHistoryPeriods.Decode(row));
        Assert.Throws<InvalidDataException>(()=>ExecutionHistoryPeriods.Decode(row with{Key=8}));
        Assert.Throws<InvalidDataException>(()=>ExecutionHistoryPeriods.Decode(row with{SessionId=Guid.NewGuid()}));
        var changed=row.Payload.ToArray();changed[^1]^=0x5a;
        Assert.Throws<InvalidDataException>(()=>ExecutionHistoryPeriods.Decode(row with{Payload=changed}));
    }
    [Theory][InlineData("data")][InlineData("checkpoint")][InlineData("seal")]
    public void CorruptPeriodIsRejectedBeforeNativeRecoveryAndOtherPeriodsStayReadable(string corruption){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"integrity-"+Guid.NewGuid().ToString("N"));
        var budget=new ResourceBudget(64L<<20,128L<<20);var periods=new ExecutionHistoryPeriods(root,budget);
        var observation=new QueryExecutionObservation(Guid.NewGuid(),"definition","Value",1,new('A',64),true,DateTimeOffset.UnixEpoch,DateTimeOffset.UnixEpoch,TimeSpan.Zero,TimeSpan.Zero,null,0,0,false,"Succeeded","Execution",null);
        var entry=new QueryExecutionHistoryEntry(Guid.NewGuid(),1,observation);
        var bad=periods.Write(Enumerable.Range(1,32).Select(index=>entry with{Sequence=index,Observation=observation with{ExecutionId=Guid.NewGuid()}}).ToArray());
        var good=periods.Write([entry with{Sequence=33,Observation=observation with{ExecutionId=Guid.NewGuid()}}]);
        string target;
        if(corruption=="data"){target=Directory.EnumerateFiles(bad,"hlog.*",SearchOption.AllDirectories).First(path=>new FileInfo(path).Length>0);}
        else if(corruption=="checkpoint"){target=Directory.EnumerateFiles(bad,"snapshot.dat.*",SearchOption.AllDirectories).First();}
        else{target=Path.Combine(bad,"period.seal");}
        using(var file=new FileStream(target,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){var offset=file.Length/2;file.Position=offset;var value=file.ReadByte();Assert.True(value>=0);file.Position=offset;file.WriteByte((byte)(value^0x5a));file.Flush(true);}
        Assert.Throws<InvalidDataException>(()=>periods.Read(bad,CancellationToken.None));Assert.Single(periods.Read(good,CancellationToken.None));Assert.Equal(0,budget.MemoryBytes);
    }
}
