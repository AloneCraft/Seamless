using MemoryPack;

using Seamless.Collections;
using Seamless.SessionFunctions;

using System.Buffers;

using Tsavorite.core;

using Empty = Seamless.Collections.Empty;
namespace Test.Seamless.Collections111;

/// <summary>
/// Upsert/Delete繰り返し → チェックポイント保存 → リカバリー → データ検証 のテスト。
/// </summary>
public class CheckpointRecoveryTests{
    private static string CreateFolder()=>Path.Combine(Environment.CurrentDirectory,$"Test111{Guid.NewGuid()}");
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
            //await Store.CompleteCheckpointAsync(ct);
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
            //var Session=Store.NewSession<SpanByte,SpanByteAndMemory,Empty,Add>(new Add());
            //var Session=dict.ClientSession;
            SpanByte.FromPinnedSpan(KeyBytes);
            var FixedKey   =SpanByte.FromPinnedSpan(KeyBytes);
            SpanByteAndMemory Value = default;
            var Status = Session.BasicContext.Read(ref FixedKey,ref Value);
            Session.BasicContext.CompletePending(true);
            var actual = MemoryPackSerializer.Deserialize<int>(Value.AsReadOnlySpan());
            Assert.Equal(expected,actual);
        }
    }
    static Span<byte> a0(SpanByteAndMemory b)=>b.AsSpan();
    static ReadOnlySpan<byte> a1(SpanByteAndMemory b)=>b.AsMemoryReadOnlySpan();
    static ReadOnlySpan<byte> a2(SpanByteAndMemory b)=>b.AsReadOnlySpan();
    [Fact,Trait("Category","Coverage")]
    async ValueTask CheckpointAndRecoveryTest_UpsertDelete繰り返し後に復元結果が一致する1(){
        await SaveLoad(1);
    }
    [Fact,Trait("Category","Coverage")]
    async ValueTask CheckpointAndRecoveryTest_UpsertDelete繰り返し後に復元結果が一致する2(){
        await SaveLoad(2);
    }
    [Fact,Trait("Category","Coverage")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification="<保留中>")]
    public async Task CheckpointAndRecoveryTest_UpsertDelete繰り返し後に復元結果が一致する(){
        const int count=200;
        var ct=TestContext.Current.CancellationToken;
        var folder=CreateFolder();
        var checkpointDir=folder+"_checkpoint";
        CheckpointType checkpointType=CheckpointType.FoldOver;
        Guid checkpointToken;
        {
            using var logDevice=new ManagedLocalStorageDevice(folder);
            using var objDevice=new ManagedLocalStorageDevice(folder+"_obj");
            using var store=Store.Create<int,int>(logDevice,objDevice,CheckpointDir:checkpointDir);
            using var dict=store.Create();
            for(var i=0;i<count;i++){
                dict[i]=i;
            }
            for(var i=0;i<count;i+=2){
                _=dict.Remove(i);
            }
            for(var i=1;i<count;i+=2){
                dict[i]=i*10;
            }
            var (success,token)=await store.Store.TakeFullCheckpointAsync(checkpointType);
            Assert.True(success,$"[{checkpointType}] チェックポイントの開始に失敗しました");
            checkpointToken=token;
            //await store.SaveAsync(ct);
        }
        {
            using var logDevice=new ManagedLocalStorageDevice(folder,recoverDevice:true);
            using var objDevice=new ManagedLocalStorageDevice(folder+"_obj",recoverDevice:true);
            using var store=Store.Create<int,int>(logDevice,objDevice,CheckpointDir:checkpointDir);
            store.Store.Recover(checkpointToken);
            //await store.LoadAsync(ct);
            using var dict=store.Create();

            for(var i=0;i<count;i+=2){
                Assert.False(dict.ContainsKey(i),$"偶数キー {i} は削除済みのはずですが見つかりました");
            }

            for(var i=1;i<count;i+=2){
                Assert.True(dict.ContainsKey(i),$"奇数キー {i} はリカバリー後に見つかるはずです");
                Assert.Equal(i*10,dict[i]);
            }
        }
    }
    public static class Const{
        public const int Chunk_Length=2;
        public const int Count=253;
        public const int IndexSizeBits=6;
        public const int PageSizeBits=12;
        public const int MemorySizeBits=PageSizeBits+1;
        public const long IndexSize=1L<<IndexSizeBits;
        public const long LogMemorySize=1L<<MemorySizeBits;
        public const long PageSize=1L<<PageSizeBits;
        public const long SegmentSize=PageSize*1;
        public const long MinLogCapacity=SegmentSize*2;
        public const long MinObjectLogCapacity=1L<<22;
        public const long MaxObjectLogCapacity=1L<<62;
    }
    //[Fact,Trait("Category","Coverage")]
    //[System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification="<保留中>")]
    //public async ValueTask  単純なSaveLoad(){
    //    const int count=200;
    //    var ct=TestContext.Current.CancellationToken;
    //    var folder=CreateFolder();
    //    var checkpointDir=folder+"_checkpoint";
    //    var checkpointType=CheckpointType.FoldOver;
    //    Guid checkpointToken;
    //    {
    //        using var LogDevice=new ManagedLocalStorageDevice(folder);
    //        using var ObjectLogDevice=new ManagedLocalStorageDevice(folder+"_obj");
    //        //long IndexSize=Const.IndexSize,long PageSize=Const.PageSize,long SegmentSize=Const.SegmentSize,long MemorySize=Const.LogMemorySize,double MutableFraction=1.0,string? CheckpointDir=null
    //        var Settings=new KVSettings<SpanByte,SpanByte>{
    //            IndexSize=Const.IndexSize,
    //            MemorySize= Const.LogMemorySize,
    //            PageSize=Const.PageSize,
    //            SegmentSize=Const.SegmentSize,
    //            MutableFraction=1,
    //            LogDevice=LogDevice,
    //            ObjectLogDevice=ObjectLogDevice,
    //            CheckpointDir=checkpointDir,
    //        };
    //        var StoreFunctions=new StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>(
    //            new SpanByteComparer(),
    //            ()=>null,
    //            ()=>null,
    //            new SpanByteRecordDisposer()
    //        );
    //        var Store=new TsavoriteKV<SpanByte,SpanByte,StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>>>(
    //            Settings,
    //            StoreFunctions,
    //            static (AllocSettings,StoreFuncs)=>new SpanByteAllocator<StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>>(AllocSettings,StoreFuncs));
    //        var Session = Store.NewSession<SpanByte, SpanByteAndMemory, Empty, Add>(new());
    //        for (int Key=0;Key<count;Key++){
    //            var key=TestSpanByteKey.FromPinnedSpan(SpanByte.FromPinnedVariable(ref keyNum));
    //            var value=SpanByte.FromPinnedVariable(ref valueNum);
    //            var KeyBytes =MemoryPack.MemoryPackSerializer.Serialize(Key);
    //            var ValueBytes =MemoryPack.MemoryPackSerializer.Serialize(Key);
    //            SpanByte Value0 = default;
    //            fixed (byte* pKey=KeyBytes,pValue=ValueBytes){
    //                var FixedKey = SpanByte.FromPinnedPointer(pKey  , KeyBytes.Length);
    //                var ValueKey = SpanByte.FromPinnedPointer(pValue,ValueBytes.Length);
    //                var status = Session.BasicContext.Upsert(ref FixedKey, ref Value0);
    //                //Assert.Equal(status,Status);
    //            }
    //        }
    //        var (success,token)=await Store.TakeFullCheckpointAsync(checkpointType);
    //        Assert.True(success,$"[{checkpointType}] チェックポイントの開始に失敗しました");
    //        checkpointToken=token;
    //        //await store.SaveAsync(ct);
    //    }
    //    {
    //        using var LogDevice=new ManagedLocalStorageDevice(folder,recoverDevice:true);
    //        using var ObjectLogDevice=new ManagedLocalStorageDevice(folder+"_obj",recoverDevice:true);
    //        var Settings=new KVSettings<SpanByte,SpanByte>{
    //            IndexSize=Const.IndexSize,
    //            MemorySize= Const.LogMemorySize,
    //            PageSize=Const.PageSize,
    //            SegmentSize=Const.SegmentSize,
    //            MutableFraction=1,
    //            LogDevice=LogDevice,
    //            ObjectLogDevice=ObjectLogDevice,
    //            CheckpointDir=checkpointDir,
    //        };
    //        var StoreFunctions=new StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>(
    //            new SpanByteComparer(),
    //            ()=>null,
    //            ()=>null,
    //            new SpanByteRecordDisposer()
    //        );
    //        var Store=new TsavoriteKV<SpanByte,SpanByte,StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>>>(
    //            Settings,
    //            StoreFunctions,
    //            static (AllocSettings,StoreFuncs)=>new SpanByteAllocator<StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>>(AllocSettings,StoreFuncs));
    //        Store.Recover(checkpointToken);
    //        //var (success,token)=Store.Recover(checkpointToken,ct).AsValueTask().GetAwaiter().GetResult();;
    //        var Session = Store.NewSession<SpanByte, SpanByteAndMemory, Empty, Add>(new());
    //        for (int Key=0;Key<count;Key++){
    //            var KeyBytes =MemoryPack.MemoryPackSerializer.Serialize(Key);
    //            SpanByteAndMemory Value0 = default;
    //            fixed (byte* pKey = KeyBytes){
    //                var FixedKey = SpanByte.FromPinnedPointer(pKey, KeyBytes.Length);
    //                var status = Session.BasicContext.Read(ref FixedKey, ref Value0);
    //                var Value=MemoryPack.MemoryPackSerializer.Deserialize<int>(Value0.AsReadOnlySpan());

    //                //Assert.Equal(status,Status);
    //            }
    //        }
    //    }
    //}
}
//public class CheckpointRecoveryTests{
//    private static string CreateFolder()=>Path.Combine(Environment.CurrentDirectory,"Test111");
//    private async ValueTask SaveLoad(int LoadCount){
//        var ct=TestContext.Current.CancellationToken;
//        var folder=CreateFolder();
//        var checkpointDir=folder+"_checkpoint";
//        const int Key=999;
//        const int expected=111;
//        {
//            using var LogDevice=new ManagedLocalStorageDevice(folder);
//            using var ObjectLogDevice=new ManagedLocalStorageDevice(folder+"_obj");
//            var Store=CreateStore(folder,checkpointDir,LogDevice,ObjectLogDevice);
//            {
//                var KeyBytes=MemoryPackSerializer.Serialize(Key);
//                var ValueBytes=MemoryPackSerializer.Serialize(expected);
//                var Session=Store.NewSession<SpanByte,SpanByteAndMemory,Tsavorite.core.Empty,Add1>(new());
//                SpanByte.FromPinnedSpan(KeyBytes);
//                var FixedKey=SpanByte.FromPinnedSpan(KeyBytes);
//                var FixedValue=SpanByte.FromPinnedSpan(ValueBytes);
//                var Status=Session.BasicContext.Upsert(ref FixedKey,ref FixedValue);
//                //Assert.True(Status.Found);
//                var result=Session.BasicContext.CompletePending(true);
//            }
//            Read(Store);
//            await Store.TakeFullCheckpointAsync(CheckpointType.FoldOver);
//            await Store.CompleteCheckpointAsync(ct);
//        }
//        {
//            using var LogDevice1=new ManagedLocalStorageDevice(folder,recoverDevice:true);
//            using var ObjectLogDevice1=new ManagedLocalStorageDevice(folder+"_obj",recoverDevice:true);
//            var Store1=CreateStore(folder,checkpointDir,LogDevice1,ObjectLogDevice1);
//            for(var a = 0; a < LoadCount; a++) { 
//                await Store1.RecoverAsync(numPagesToPreload: -1,undoNextVersion: false,cancellationToken: ct);
//            }
//            Read(Store1);
//        }
//        static TsavoriteKV<SpanByte,SpanByte,StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>>>CreateStore(string folder,string checkpointDir,ManagedLocalStorageDevice logDevice,ManagedLocalStorageDevice objDevice){
//            var Settings = new KVSettings<SpanByte,SpanByte> {
//                IndexSize=Common.Const.IndexSize,
//                MemorySize=Common.Const.LogMemorySize,
//                PageSize=Common.Const.PageSize,
//                SegmentSize=Common.Const.SegmentSize,
//                MutableFraction=1,
//                LogDevice=logDevice,
//                ObjectLogDevice=objDevice,
//                CheckpointDir=checkpointDir,
//            };
//            var StoreFunctions = new StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>(
//                new SpanByteComparer(),
//                () => null,
//                () => null,
//                new SpanByteRecordDisposer()
//            );
//            return new TsavoriteKV<SpanByte,SpanByte,StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>>>(
//                Settings,
//                StoreFunctions,
//                static (AllocSettings,StoreFuncs) => new SpanByteAllocator<StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>>(AllocSettings,StoreFuncs));
//        }
//        static void Read(TsavoriteKV<SpanByte,SpanByte,StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>>> Store){
//            var Session=Store.NewSession<SpanByte,SpanByteAndMemory,Tsavorite.core.Empty,Add1>(new());
//            var KeyBytes = MemoryPackSerializer.Serialize(Key);
//            //var Session=Store.NewSession<SpanByte,SpanByteAndMemory,Empty,Add>(new Add());
//            //var Session=dict.ClientSession;
//            SpanByte.FromPinnedSpan(KeyBytes);
//            var FixedKey   =SpanByte.FromPinnedSpan(KeyBytes);
//            SpanByteAndMemory Value = default;
//            var Status = Session.BasicContext.Read(ref FixedKey,ref Value);
//            Session.BasicContext.CompletePending(true);
//            var actual = MemoryPackSerializer.Deserialize<int>(Value.AsReadOnlySpan());
//            Assert.Equal(expected,actual);
//        }
//    }
//    static Span<byte> a0(SpanByteAndMemory b)=>b.AsSpan();
//    static ReadOnlySpan<byte> a1(SpanByteAndMemory b)=>b.AsMemoryReadOnlySpan();
//    static ReadOnlySpan<byte> a2(SpanByteAndMemory b)=>b.AsReadOnlySpan();
//    [Fact,Trait("Category","Coverage")]
//    async ValueTask CheckpointAndRecoveryTest_UpsertDelete繰り返し後に復元結果が一致する1(){
//        await SaveLoad(1);
//    }
//    [Fact,Trait("Category","Coverage")]
//    async ValueTask CheckpointAndRecoveryTest_UpsertDelete繰り返し後に復元結果が一致する2(){
//        await SaveLoad(2);
//    }
//    public static class Const{
//        public const int Chunk_Length=2;
//        public const int Count=253;
//        public const int IndexSizeBits=6;
//        public const int PageSizeBits=12;
//        public const int MemorySizeBits=PageSizeBits+1;
//        public const long IndexSize=1L<<IndexSizeBits;
//        public const long LogMemorySize=1L<<MemorySizeBits;
//        public const long PageSize=1L<<PageSizeBits;
//        public const long SegmentSize=PageSize*1;
//        public const long MinLogCapacity=SegmentSize*2;
//        public const long MinObjectLogCapacity=1L<<22;
//        public const long MaxObjectLogCapacity=1L<<62;
//    }
//    private struct Add1:ISessionFunctions<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Tsavorite.core.Empty> {
//        public bool SingleReader(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref SpanByteAndMemory dst,ref ReadInfo readInfo) {
//            var span = value.AsReadOnlySpan();
//            if(!span.IsEmpty) dst.CopyFrom(span,MemoryPool<byte>.Shared);
//            return true;
//        }
//        public bool ConcurrentReader(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref SpanByteAndMemory dst,ref ReadInfo readInfo,ref RecordInfo recordInfo) {
//            var span = value.AsReadOnlySpan();
//            if(!span.IsEmpty) dst.CopyFrom(span,MemoryPool<byte>.Shared);
//            return true;
//        }
//        public void ReadCompletionCallback(ref SpanByte key,ref SpanByte input,ref SpanByteAndMemory output,Tsavorite.core.Empty ctx,Status status,RecordMetadata recordMetadata) { }

//        public bool SingleWriter(ref SpanByte key,ref SpanByte input,ref SpanByte src,ref SpanByte dst,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo,WriteReason reason,ref RecordInfo recordInfo) {
//            src.CopyTo(ref dst);
//            return true;
//        }

//        public void PostSingleWriter(ref SpanByte key,ref SpanByte input,ref SpanByte src,ref SpanByte dst,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo,WriteReason reason) { }
//        public bool ConcurrentWriter(ref SpanByte key,ref SpanByte input,ref SpanByte src,ref SpanByte dst,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo,ref RecordInfo recordInfo) {
//            if(src.Length>dst.Length) return false;
//            src.CopyTo(ref dst);
//            return true;
//        }


//        void ISessionFunctions<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Tsavorite.core.Empty>.PostUpsertOperation<TEpochAccessor>(ref SpanByte key,ref SpanByte input,ref SpanByte src,ref UpsertInfo upsertInfo,TEpochAccessor epoch) { }

//        public bool NeedInitialUpdate(ref SpanByte key,ref SpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo) => true;
//        public bool InitialUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref SpanByteAndMemory output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo) {
//            input.CopyTo(ref value);
//            return true;
//        }
//        public void PostInitialUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref SpanByteAndMemory output,ref RMWInfo rmwInfo) { }
//        public bool NeedCopyUpdate(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref SpanByteAndMemory output,ref RMWInfo rmwInfo) => false;
//        public bool CopyUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte oldValue,ref SpanByte newValue,ref SpanByteAndMemory output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo) {
//            oldValue.CopyTo(ref newValue);
//            return true;
//        }

//        public bool PostCopyUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte oldValue,ref SpanByte newValue,ref SpanByteAndMemory output,ref RMWInfo rmwInfo) => true;
//        public bool InPlaceUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref SpanByteAndMemory output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo) => true;
//        public int GetRMWModifiedValueLength(ref SpanByte value,ref SpanByte input) => value.Length;
//        public int GetRMWInitialValueLength(ref SpanByte input) => input.Length+4;
//        public int GetUpsertValueLength(ref SpanByte value,ref SpanByte input) => value.Length+4;



//        void ISessionFunctions<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Tsavorite.core.Empty>.PostRMWOperation<TEpochAccessor>(ref SpanByte key,ref SpanByte input,ref RMWInfo rmwInfo,TEpochAccessor epoch) { }

//        public void RMWCompletionCallback(ref SpanByte key,ref SpanByte input,ref SpanByteAndMemory output,Tsavorite.core.Empty ctx,Status status,RecordMetadata recordMetadata) { }

//        public bool SingleDeleter(ref SpanByte key,ref SpanByte value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo) => true;
//        public void PostSingleDeleter(ref SpanByte key,ref DeleteInfo deleteInfo) { }
//        public bool ConcurrentDeleter(ref SpanByte key,ref SpanByte value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo) => true;

//        void ISessionFunctions<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Tsavorite.core.Empty>.PostDeleteOperation<TEpochAccessor>(ref SpanByte key,ref DeleteInfo deleteInfo,TEpochAccessor epoch) { }

//        public void ConvertOutputToHeap(ref SpanByte input,ref SpanByteAndMemory output) { }
//    }
//}
