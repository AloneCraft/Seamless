using System.Reflection;
using System.Runtime.InteropServices;
using Seamless.Collections.Tsavorite;
using Seamless.Execution;
using Store=Seamless.Execution.Store;
namespace Seamless.Transactions.Tests;

public sealed partial class TsavoriteHistoryTests{
    [Fact]
    public void NativeAllocatorRejectsBeyondItsConfiguredCapacityWithoutConsumingMoreAddresses(){
        // 本番と同じ境界拒否をbyteの2ページで確認する。巨大な索引は確保しない。
        const int capacity=2*65536;
        using var allocator=new global::Tsavorite.core.MallocFixedPageSize<byte>(capacity);
        var expected=capacity-global::Tsavorite.core.MallocFixedPageSize<byte>.AllocateChunkSize;long last=0;
        for(var index=0;index<expected;index++){last=allocator.Allocate();}
        for(var attempt=0;attempt<8;attempt++){
            Assert.Throws<global::Tsavorite.core.TsavoriteException>(()=>allocator.Allocate());
            Assert.Equal(expected,allocator.NumAllocations);
        }
        byte marker=42;allocator.Set(last,ref marker);Assert.Equal(marker,allocator.Get(last));
        allocator.Free(last);Assert.Equal(last,allocator.Allocate());
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void NativeAllocatedPoolsAndPagesFitTheOwnerReservation(bool disk){
        var path=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!,Guid.NewGuid().ToString("N"));
        using var owner=disk?Store.Open(path,new(){MemoryBytes=64L<<20,DiskBytes=16L<<20}):Store.CreateMemory(new(){MemoryBytes=64L<<20,DiskBytes=0});
        var table=owner.CreateTsavoriteHistory("native-budget",Tuning);
        CheckAllocatedMemory(owner,table);
        using(var region=table.BeginParallel()){
            using var access=region.Create();
            for(var version=1;version<=8;version++){
                for(var key=0;key<128;key++){access.AppendCommittedVersion(BitConverter.GetBytes(key),BitConverter.GetBytes((long)key+version),version,false);}
                CheckAllocatedMemory(owner,table);
            }
            Assert.Equal(1,BitConverter.ToInt64(access.ReadVisibleRecord(BitConverter.GetBytes(0),1)!.Value.Row));
        }
        table.Reclaim();CheckAllocatedMemory(owner,table);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void StoreCreationRejectsNativePoolBeforeSpendingTooSmallBudget(bool parentLimited){
        var parent=new ResourceBudget(1L<<20,0);using var sibling=parent.ReserveMemory(4096);
        using var owner=Store.CreateMemory(new(){MemoryBytes=parentLimited?64L<<20:1L<<20,DiskBytes=0},parentBudget:parentLimited?parent:null);
        Assert.Throws<ResourceLimitException>(()=>owner.CreateTsavoriteHistory("too-small",Tuning));
        Assert.Equal(0,owner.Budget.MemoryBytes);Assert.Equal(4096,parent.MemoryBytes);
    }
    [Fact]
    public void RepeatedQuiescentReclaimReturnsOldNativePagesInsteadOfGrowingWithTheGlobalTail(){
        using var owner=Store.CreateMemory(new(){MemoryBytes=64L<<20,DiskBytes=0});var table=owner.CreateTsavoriteHistory("reclaim-pages",Tuning);var key=BitConverter.GetBytes(1);long version=0;
        for(var cycle=0;cycle<24;cycle++){
            using(var access=table.Create()){for(var update=0;update<128;update++){version++;access.AppendCommittedVersion(key,BitConverter.GetBytes(version),version,false);}}
            table.Reclaim();
            using(var access=table.Create()){Assert.Equal(version,BitConverter.ToInt64(access.ReadRecord(key)!.Value.Row));}
            var allocated=table.Store.Log.MemorySizeBytesIncludingOverflowPages;
            Assert.True(allocated<=table.LogMemorySize+4*table.PageBytes,$"After reclaim cycle {cycle}, native log payload {allocated} grows with the discarded global tail.");
            CheckAllocatedMemory(owner,table);
        }
    }
    private static void CheckAllocatedMemory(Store owner,HistoryStore table){
        // 採用DLLの実配列を調べる試験専用の観測。使用中bucket数を確保量へ読み替えない。
        var allocator=NativeField(table.Store,"overflowBucketsAllocator");var arrays=(Array)NativeField(allocator,"values");long overflowBytes=0;
        foreach(var value in arrays){if(value is Array page){overflowBytes=checked(overflowBytes+page.LongLength*Marshal.SizeOf(page.GetType().GetElementType()!));}}
        // Garnet 2.2.3以降は使用中bucket数ではなく、先読みとalignmentを含む確保済みpayloadを報告する。
        Assert.Equal(overflowBytes,table.Store.IndexOverflowSizeBytes);
        var lowerBound=checked(overflowBytes+table.Store.IndexSizeBytes+table.Store.Log.MemorySizeBytesIncludingOverflowPages);
        Assert.True(owner.Budget.MemoryBytes>=lowerBound,$"Reservation {owner.Budget.MemoryBytes} does not cover allocated overflow/index/log payload {lowerBound}.");
    }
    private static object NativeField(object value,string name){
        for(var type=value.GetType();type is not null;type=type.BaseType){if(type.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.DeclaredOnly) is FieldInfo field){return field.GetValue(value)??throw new InvalidDataException("Native observation field is null: "+name);}}
        throw new InvalidDataException("Native observation field is missing: "+name);
    }
}
