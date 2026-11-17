using Seamless.Collections.Tsavorite;
using Seamless.Execution;
using Store=Seamless.Execution.Store;
namespace Seamless.Transactions.Tests;

public sealed partial class TsavoriteHistoryTests{
    private static readonly TsavoriteTuning Tuning=new(){IndexSize=64,PageSize=4096,SegmentSize=1<<16,LogMemorySize=1<<16};
    [Fact]
    public void CopiesPreserveValuesAndLogicalDeletionKeepsTheHistoryHead(){
        using var owner=Store.CreateMemory();var table=owner.CreateTsavoriteHistory("accounts",Tuning);
        using var region=table.BeginParallel();using var access=region.Create();var key=BitConverter.GetBytes(1);
        access.AppendCommittedVersion(key,BitConverter.GetBytes(100L),1,false);var original=access.ReadRecord(key)!.Value;
        access.AppendCommittedVersion(key,BitConverter.GetBytes(90L),2,false);
        access.AppendCommittedVersion(key,[],3,true);
        access.AppendCommittedVersion(key,BitConverter.GetBytes(200L),4,false);
        Assert.Equal(100,BitConverter.ToInt64(access.ReadVisibleRecord(key,1)!.Value.Row));
        Assert.Equal(90,BitConverter.ToInt64(access.ReadVisibleRecord(key,2)!.Value.Row));
        Assert.True(access.ReadVisibleRecord(key,3)!.Value.Deleted);
        Assert.Equal(200,BitConverter.ToInt64(access.ReadVisibleRecord(key,4)!.Value.Row));
        Assert.Equal(original.Address,access.ReadVisibleRecord(key,1)!.Value.Address);
        original.Row[0]=0;Assert.Equal(100,BitConverter.ToInt64(access.ReadRecordAtAddress(original.Address).Row));
    }

    [Fact]
    public void CollidingKeysAreSkippedWhileWalkingTheNativeHashChain(){
        using var owner=Store.CreateMemory();var table=owner.CreateTsavoriteHistory("collisions",Tuning);
        using var region=table.BeginParallel();using var access=region.Create();
        for(var key=0;key<128;key++){access.AppendCommittedVersion(BitConverter.GetBytes(key),BitConverter.GetBytes((long)key),1,false);}
        for(var key=0;key<128;key++){access.AppendCommittedVersion(BitConverter.GetBytes(key),BitConverter.GetBytes((long)key+1000),2,false);}
        for(var key=0;key<128;key++){
            var value=access.ReadVisibleRecord(BitConverter.GetBytes(key),1)!.Value;
            Assert.Equal(key,BitConverter.ToInt32(value.Key));Assert.Equal(key,BitConverter.ToInt64(value.Row));Assert.Equal(1,value.CommitVersion);
        }
        Assert.Null(access.ReadVisibleRecord(BitConverter.GetBytes(200),1));
    }

    [Fact]
    public void ReclamationRejectsLiveAccessorsAndInvalidatesOldAddressTokens(){
        using var owner=Store.CreateMemory();var table=owner.CreateTsavoriteHistory("generation",Tuning);HistoryAddress old;
        using(var access=table.Create()){
            access.AppendCommittedVersion(BitConverter.GetBytes(1),BitConverter.GetBytes(100L),1,false);old=access.ReadRecord(BitConverter.GetBytes(1))!.Value.Address;
            Assert.Throws<InvalidOperationException>(()=>table.Reclaim());
        }
        table.Reclaim();using var current=table.Create();
        Assert.Throws<InvalidOperationException>(()=>current.ReadRecordAtAddress(old));
        Assert.Equal(100,BitConverter.ToInt64(current.ReadRecord(BitConverter.GetBytes(1))!.Value.Row));
    }

    [Fact]
    public async Task SnapshotBatchesProtectStructureWithoutRetainingANativeEpochAcrossAwait(){
        using var owner=Store.CreateMemory();var table=owner.CreateTsavoriteHistory("batches",Tuning);
        using var region=table.BeginParallel();using var access=region.Create();
        for(var key=0;key<128;key++){access.AppendCommittedVersion(BitConverter.GetBytes(key),BitConverter.GetBytes((long)key),1,false);}
        using var snapshot=access.OpenSnapshot(1);var rows=new List<HistoryRecord>();rows.AddRange(snapshot.ReadBatch(16));
        var started=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var update=Task.Run(()=>{using var writer=region.Create();started.SetResult();for(var key=0;key<128;key++){writer.AppendCommittedVersion(BitConverter.GetBytes(key),BitConverter.GetBytes((long)key+1000),2,false);}});
        try{
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));Assert.False(update.IsCompleted);
            while(true){var batch=snapshot.ReadBatch(16);if(batch.Count==0){break;}rows.AddRange(batch);}
        }finally{snapshot.Dispose();await update.WaitAsync(TimeSpan.FromSeconds(5));}
        Assert.Equal(128,rows.Count);Assert.Equal(128,rows.Select(static row=>BitConverter.ToInt32(row.Key)).Distinct().Count());
        Assert.All(rows,static row=>Assert.Equal(BitConverter.ToInt32(row.Key),BitConverter.ToInt64(row.Row)));
        using var after=region.Create();var sum=new Sum();after.IterateRecords(ref sum,1);Assert.Equal(128,sum.Count);Assert.Equal(8128,sum.Value);
    }

    [Fact]
    public void DiskReadsReopenWithTheSameFormatAndRejectOrdinaryOrDifferentSchemaProfiles(){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!,Guid.NewGuid().ToString("N"));
        using(var owner=Store.Open(root)){
            var table=owner.CreateTsavoriteHistory("disk",Tuning,"accounts-codec");
            using(var access=table.Create()){
                for(var version=1;version<=8;version++){access.AppendCommittedVersion(BitConverter.GetBytes(1),BitConverter.GetBytes((long)version),version,false);}
                table.Store.Log.FlushAndEvict(true);
                Assert.Equal(2,BitConverter.ToInt64(access.ReadVisibleRecord(BitConverter.GetBytes(1),2)!.Value.Row));
                access.AppendCommittedVersion(BitConverter.GetBytes(1),[],9,true);
            }
            table.Reclaim();owner.Save();Assert.Equal(9,table.MaximumCommittedVersion);
            using var empty=table.Create();Assert.Null(empty.ReadRecord(BitConverter.GetBytes(1)));
        }
        using(var owner=Store.Open(root)){
            Assert.Throws<InvalidDataException>(()=>owner.CreateTsavorite<int,long>("disk",Tuning));
            Assert.Throws<InvalidDataException>(()=>owner.CreateTsavoriteHistory("disk",Tuning,"different-codec"));
            var table=owner.CreateTsavoriteHistory("disk",Tuning,"accounts-codec");Assert.Equal(9,table.MaximumCommittedVersion);
            using var access=table.Create();Assert.Null(access.ReadRecord(BitConverter.GetBytes(1)));
        }
    }

    private struct Sum:IHistoryStrategy<Sum>{
        internal int Count;internal long Value;
        public static bool Invoke(ref Sum self,ReadOnlySpan<byte> key,ReadOnlySpan<byte> row,long version,bool deleted){if(!deleted){self.Count++;self.Value+=BitConverter.ToInt64(row);}return true;}
    }
    [Fact]
    public void AFixedMemoryRingRejectsAppendBeforeOverwritingRetainedHistory(){
        using var owner=Store.CreateMemory();var table=owner.CreateTsavoriteHistory("fixed-ring",Tuning with{Growth=new Seamless.Execution.Growth.Policy<Seamless.Execution.Growth.Fixed>()});
        using var region=table.BeginParallel();using var access=region.Create();var key=BitConverter.GetBytes(1);
        access.AppendCommittedVersion(key,BitConverter.GetBytes(100L),1,false);var original=access.ReadRecord(key)!.Value;var rejected=false;
        for(var version=2;version<=5000;version++){
            try{access.AppendCommittedVersion(key,BitConverter.GetBytes((long)version),version,false);}
            catch(ResourceLimitException){rejected=true;break;}
        }
        Assert.True(rejected);var retained=access.ReadVisibleRecord(key,1)!.Value;
        Assert.Equal(100,BitConverter.ToInt64(retained.Row));Assert.Equal(original.Address,retained.Address);
    }
}
