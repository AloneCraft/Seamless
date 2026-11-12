using Seamless.Execution;
using Seamless.Management;
using Seamless.Management.Server;
using Tsavorite.core;

namespace Management.Tests;

[Collection("AuditStorage")]
public sealed class CheckpointPathCompatibility{
    [Fact]public async Task DeepPeriodPathsReopenAndLegacyPeriodNamesKeepTheirRetentionOrder(){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"checkpoint-path-"+Guid.NewGuid().ToString("N"));
        if(root.Length<160){root=Path.Combine(root,new string('d',159-root.Length));}
        var budget=new ResourceBudget(64L<<20,128L<<20);var periods=new ExecutionHistoryPeriods(root,budget);
        var observation=new QueryExecutionObservation(Guid.NewGuid(),"deep-definition","Value",1,new('A',64),true,DateTimeOffset.UnixEpoch,DateTimeOffset.UnixEpoch,TimeSpan.Zero,TimeSpan.Zero,null,0,0,false,"Succeeded","Execution",null);
        var first=new QueryExecutionHistoryEntry(Guid.NewGuid(),1,observation);var current=periods.Write([first]);
        Assert.Equal(39,Path.GetFileName(current).Length);
        Assert.Contains(Directory.EnumerateFiles(current,"info.dat.*",SearchOption.AllDirectories),path=>path.Length>260);
        Assert.Equal(first,Assert.Single(periods.Read(current,CancellationToken.None)));
        var legacy=current+new string('a',20);Directory.Move(current,legacy);
        Assert.Equal(first,Assert.Single(periods.Read(legacy,CancellationToken.None)));
        var second=first with{Sequence=2,Observation=observation with{ExecutionId=Guid.NewGuid()}};var newer=periods.Write([second]);
        Assert.True(StringComparer.Ordinal.Compare(Path.GetFileName(legacy),Path.GetFileName(newer))<0);
        await using(var reader=new QueryExecutionHistoryStore(root,budget)){
            var result=await reader.ReadAsync(new("deep-definition","Value"),CancellationToken.None);Assert.Equal(2,result.Items.Length);Assert.Equal(0,result.InvalidFiles);
        }
        await using(var recorder=new QueryExecutionHistoryStore(root,budget,new(MaximumFiles:1))){
            Assert.True(recorder.TryRecord(observation with{ExecutionId=Guid.NewGuid()}));await recorder.DisposeAsync();
            Assert.Single(Directory.GetDirectories(root,"period-*"));Assert.False(Directory.Exists(legacy));Assert.False(Directory.Exists(newer));
            var result=await recorder.ReadAsync(new("deep-definition","Value"),CancellationToken.None);Assert.Single(result.Items);Assert.Equal(0,result.InvalidFiles);
        }
        Assert.Equal(0,budget.MemoryBytes);
    }
    [Fact]public void NativeCheckpointMetadataRemainsReadableWithExplicitRandomAccess(){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"native-metadata-"+Guid.NewGuid().ToString("N"));
        var token=Guid.NewGuid();byte[] index=[1,3,5,7,9];byte[] log=[2,4,6,8,10];byte[] previousIndex,previousLog;
        using(var previous=new DeviceLogCommitCheckpointManager(new LocalStorageNamedDeviceFactoryCreator(disableFileBuffering:false,deviceType:DeviceType.Native),new DefaultCheckpointNamingScheme(root),removeOutdated:false)){
            previous.CommitIndexCheckpoint(token,index);previous.CommitLogCheckpointMetadata(token,log);
            previousIndex=previous.GetIndexCheckpointMetadata(token);previousLog=previous.GetLogCheckpointMetadata(token);
        }
        Assert.Equal(index,previousIndex.Take(index.Length));Assert.Equal(log,previousLog.Take(log.Length));
        Assert.All(previousIndex.Skip(index.Length),value=>Assert.Equal((byte)0,value));Assert.All(previousLog.Skip(log.Length),value=>Assert.Equal((byte)0,value));
        using var current=new DeviceLogCommitCheckpointManager(new LocalStorageNamedDeviceFactoryCreator(disableFileBuffering:false,deviceType:DeviceType.RandomAccess),new DefaultCheckpointNamingScheme(root),removeOutdated:false);
        Assert.Equal(previousIndex,current.GetIndexCheckpointMetadata(token));Assert.Equal(previousLog,current.GetLogCheckpointMetadata(token));
    }
}
