using MemoryPack;
using Seamless.SessionFunctions;
using Tsavorite.core;
using Empty = Seamless.Collections.Empty;
namespace Test.Seamless.Collections111;
public class CheckpointRecoveryTests{
    private async ValueTask SaveLoad(int LoadCount){
        var ct=TestContext.Current.CancellationToken;
        var folder=Path.Combine(Environment.CurrentDirectory,"Test111");
        var checkpointDir=folder+"_checkpoint";
        const int Key=999;
        const int expected=111;
        {
            var dir=Path.GetDirectoryName(folder)!;
            var name=Path.GetFileName(folder);
            foreach(var f in Directory.GetFiles(dir,name+".*")) File.Delete(f);
            foreach(var f in Directory.GetFiles(dir,name+"_obj.*")) File.Delete(f);
            if(Directory.Exists(checkpointDir)) Directory.Delete(checkpointDir,true);
            using var LogDevice=new ManagedLocalStorageDevice(folder);
            using var ObjectLogDevice=new ManagedLocalStorageDevice(folder+"_obj");
            var Store=CreateStore(folder,checkpointDir,LogDevice,ObjectLogDevice);
            {
                var KeyBytes=MemoryPackSerializer.Serialize(Key);
                var ValueBytes=MemoryPackSerializer.Serialize(expected);
                var Session=Store.NewSession<SpanByte,SpanByteAndMemory,Empty,Add>(new Add());
                SpanByte.FromPinnedSpan(KeyBytes);
                var FixedKey=SpanByte.FromPinnedSpan(KeyBytes);
                var FixedValue=SpanByte.FromPinnedSpan(ValueBytes);
                var Status=Session.BasicContext.Upsert(ref FixedKey,ref FixedValue);
                var result=Session.BasicContext.CompletePending(true);
            }
            Read(Store);
            await Store.TakeFullCheckpointAsync(CheckpointType.FoldOver);
        }
        {
            using var LogDevice1=new ManagedLocalStorageDevice(folder);
            using var ObjectLogDevice1=new ManagedLocalStorageDevice(folder+"_obj");
            var Store1=CreateStore(folder,checkpointDir,LogDevice1,ObjectLogDevice1);
            for(var a = 0; a < LoadCount; a++) { 
                await Store1.RecoverAsync(numPagesToPreload: -1,undoNextVersion: false,cancellationToken: ct);
            }
            Read(Store1);
        }
        static TsavoriteKV<SpanByte,SpanByte,StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>>>CreateStore(string folder,string checkpointDir,ManagedLocalStorageDevice logDevice,ManagedLocalStorageDevice objDevice){
            var Settings = new KVSettings<SpanByte,SpanByte> {
                IndexSize=Common.Const.IndexSize,
                MemorySize=Common.Const.LogMemorySize,
                PageSize=Common.Const.PageSize,
                SegmentSize=Common.Const.SegmentSize,
                MutableFraction=1,
                LogDevice=logDevice,
                ObjectLogDevice=objDevice,
                CheckpointDir=checkpointDir,
            };
            var StoreFunctions = new StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>(
                new SpanByteComparer(),
                () => null,
                () => null,
                new SpanByteRecordDisposer()
            );
            return new TsavoriteKV<SpanByte,SpanByte,StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>>>(
                Settings,
                StoreFunctions,
                static (AllocSettings,StoreFuncs) => new SpanByteAllocator<StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>>(AllocSettings,StoreFuncs));
        }
        static void Read(TsavoriteKV<SpanByte,SpanByte,StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>>> Store){
            var Session=Store.NewSession<SpanByte,SpanByteAndMemory,Empty,Add>(new Add());
            var KeyBytes = MemoryPackSerializer.Serialize(Key);
            SpanByte.FromPinnedSpan(KeyBytes);
            var FixedKey   =SpanByte.FromPinnedSpan(KeyBytes);
            SpanByteAndMemory Value = default;
            var Status = Session.BasicContext.Read(ref FixedKey,ref Value);
            Session.BasicContext.CompletePending(true);
            var actual = MemoryPackSerializer.Deserialize<int>(Value.AsReadOnlySpan());
            Assert.Equal(expected,actual);
        }
    }
    [Fact,Trait("Category","Coverage")]
    async ValueTask CheckpointAndRecoveryTest_UpsertDelete繰り返し後に復元結果が一致する1(){
        await SaveLoad(1);
    }
    [Fact,Trait("Category","Coverage")]
    async ValueTask CheckpointAndRecoveryTest_UpsertDelete繰り返し後に復元結果が一致する2(){
        await SaveLoad(2);
    }

}