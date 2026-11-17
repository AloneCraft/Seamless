using System.Buffers.Binary;
using Seamless.Collections.Tsavorite;
using Seamless.Execution;
using Store=Seamless.Execution.Store;
namespace Seamless.Transactions.Tests;

public sealed partial class TsavoriteHistoryTests{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void CollidingWideKeysReturnTheirActualRecordAddressAndDeletedHeadsAreReclaimed(int storage){
        var directory=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!,"wide-address-"+Guid.NewGuid().ToString("N"));
        var limits=new StoreLimits{MemoryBytes=32L<<20,DiskBytes=64L<<20};var options=new StoreOptions{Growth=new global::Seamless.Execution.Growth.Policy<global::Seamless.Execution.Growth.Fixed>()};
        Store owner;if(storage==0){owner=Store.CreateMemory(limits,options);}else{owner=Store.Open(directory,limits,options);}
        using(owner){
            var table=owner.CreateTsavoriteHistory("wide-address",Tuning with{LogMemorySize=8L<<20},"wide-32-row-32");
            using(var access=table.Create()){
                var row=new byte[32];
                for(var version=1;version<=8;version++){
                    for(var id=0;id<1024;id++){BinaryPrimitives.WriteInt64LittleEndian(row,id);BinaryPrimitives.WriteInt64LittleEndian(row.AsSpan(8),version);access.AppendCommittedVersion(WideAddressKey(id),row,version,version==8&&id%19==0);}
                }
                if(storage==2){table.Store.Log.FlushAndEvict(true);}
                // キー検索で返されたアドレスから同じキー/値が読める。callbackのhash-chain先頭と取り違えない。
                for(var id=0;id<1024;id++){
                    var key=WideAddressKey(id);var latest=access.ReadRecord(key)!.Value;var roundTrip=access.ReadRecordAtAddress(latest.Address);
                    Assert.Equal(key,roundTrip.Key);Assert.Equal(latest.Row,roundTrip.Row);Assert.Equal(latest.CommitVersion,roundTrip.CommitVersion);
                    var original=access.ReadVisibleRecord(key,1)!.Value;Assert.Equal(1,original.CommitVersion);Assert.Equal(key,access.ReadRecordAtAddress(original.Address).Key);
                }
            }
            table.Reclaim();if(storage!=0){owner.Save();}
            using var current=table.Create();
            for(var id=0;id<1024;id++){
                var record=current.ReadRecord(WideAddressKey(id));if(id%19==0){Assert.Null(record);}else{Assert.NotNull(record);Assert.Equal(8,record.Value.CommitVersion);Assert.False(record.Value.Deleted);Assert.Equal(id,BinaryPrimitives.ReadInt64LittleEndian(record.Value.Row));}
            }
        }
    }
    private static byte[] WideAddressKey(long id){
        var key=new byte[32];BinaryPrimitives.WriteInt64LittleEndian(key,id);BinaryPrimitives.WriteInt64LittleEndian(key.AsSpan(8),123456789);BinaryPrimitives.WriteInt64LittleEndian(key.AsSpan(16),987654321);BinaryPrimitives.WriteInt64LittleEndian(key.AsSpan(24),id^0x123456789AB);return key;
    }
}
