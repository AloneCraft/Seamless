
using MemoryPack;
using System.Buffers;
using Tsavorite.core;
using OA = Tsavorite.core.SpanByteAllocator<Tsavorite.core.StoreFunctions<Tsavorite.core.SpanByte,Tsavorite.core.SpanByte,Tsavorite.core.SpanByteComparer,Tsavorite.core.SpanByteRecordDisposer>>;
using StoreFunc = Tsavorite.core.StoreFunctions<Tsavorite.core.SpanByte,Tsavorite.core.SpanByte,Tsavorite.core.SpanByteComparer,Tsavorite.core.SpanByteRecordDisposer>;
public class CheckpointRecoveryTests{
    public static class Const{
        public const int IndexSizeBits = 6; 
        public const int PageSizeBits = 12;
        public const int MemorySizeBits = PageSizeBits+1;
        public const long IndexSize =1L<<IndexSizeBits;
        public const long LogMemorySize=1L<<MemorySizeBits;
        public const long PageSize  =1L<<PageSizeBits;
        public const long SegmentSize=PageSize*1;
        
    }
    public struct Add:ISessionFunctions<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Empty> {
        public bool SingleReader(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref SpanByteAndMemory dst,ref ReadInfo readInfo) {
            var span = value.AsReadOnlySpan();
            if(!span.IsEmpty) dst.CopyFrom(span,MemoryPool<byte>.Shared);
            return true;
        }
        public bool ConcurrentReader(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref SpanByteAndMemory dst,ref ReadInfo readInfo,ref RecordInfo recordInfo) {
            var span = value.AsReadOnlySpan();
            if(!span.IsEmpty) dst.CopyFrom(span,MemoryPool<byte>.Shared);
            return true;
        }
        public void ReadCompletionCallback(ref SpanByte key,ref SpanByte input,ref SpanByteAndMemory output,Empty ctx,Status status,RecordMetadata recordMetadata) { }
        public bool SingleWriter(ref SpanByte key,ref SpanByte input,ref SpanByte src,ref SpanByte dst,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo,WriteReason reason,ref RecordInfo recordInfo) {
            src.CopyTo(ref dst);
            return true;
        }
        public void PostSingleWriter(ref SpanByte key,ref SpanByte input,ref SpanByte src,ref SpanByte dst,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo,WriteReason reason) { }
        public bool ConcurrentWriter(ref SpanByte key,ref SpanByte input,ref SpanByte src,ref SpanByte dst,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo,ref RecordInfo recordInfo) {
            if(src.Length>dst.Length) return false;
            src.CopyTo(ref dst);
            return true;
        }
        void ISessionFunctions<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Empty>.PostUpsertOperation<TEpochAccessor>(ref SpanByte key,ref SpanByte input,ref SpanByte src,ref UpsertInfo upsertInfo,TEpochAccessor epoch) { }
        public int GetUpsertValueLength(ref SpanByte value,ref SpanByte input) => value.Length+4;
        bool ISessionFunctions<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Empty>.NeedInitialUpdate(ref SpanByte key,ref SpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo) => throw new NotSupportedException();
        bool ISessionFunctions<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Empty>.InitialUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref SpanByteAndMemory output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo) => throw new NotSupportedException();
        void ISessionFunctions<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Empty>.PostInitialUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref SpanByteAndMemory output,ref RMWInfo rmwInfo) => throw new NotSupportedException();
        bool ISessionFunctions<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Empty>.NeedCopyUpdate(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref SpanByteAndMemory output,ref RMWInfo rmwInfo) => throw new NotSupportedException();
        bool ISessionFunctions<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Empty>.CopyUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte oldValue,ref SpanByte newValue,ref SpanByteAndMemory output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo) => throw new NotSupportedException();
        bool ISessionFunctions<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Empty>.PostCopyUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte oldValue,ref SpanByte newValue,ref SpanByteAndMemory output,ref RMWInfo rmwInfo) => throw new NotSupportedException();
        bool ISessionFunctions<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Empty>.InPlaceUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref SpanByteAndMemory output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo) => throw new NotSupportedException();
        int ISessionFunctions<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Empty>.GetRMWModifiedValueLength(ref SpanByte value,ref SpanByte input) => throw new NotSupportedException();
        int ISessionFunctions<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Empty>.GetRMWInitialValueLength(ref SpanByte input) => throw new NotSupportedException();
        void ISessionFunctions<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Empty>.PostRMWOperation<TEpochAccessor>(ref SpanByte key,ref SpanByte input,ref RMWInfo rmwInfo,TEpochAccessor epoch) => throw new NotSupportedException();
        void ISessionFunctions<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Empty>.RMWCompletionCallback(ref SpanByte key,ref SpanByte input,ref SpanByteAndMemory output,Empty ctx,Status status,RecordMetadata recordMetadata) => throw new NotSupportedException();
        bool ISessionFunctions<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Empty>.SingleDeleter(ref SpanByte key,ref SpanByte value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo) => throw new NotSupportedException();
        void ISessionFunctions<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Empty>.PostSingleDeleter(ref SpanByte key,ref DeleteInfo deleteInfo) => throw new NotSupportedException();
        bool ISessionFunctions<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Empty>.ConcurrentDeleter(ref SpanByte key,ref SpanByte value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo) => throw new NotSupportedException();
        void ISessionFunctions<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Empty>.PostDeleteOperation<TEpochAccessor>(ref SpanByte key,ref DeleteInfo deleteInfo,TEpochAccessor epoch) => throw new NotSupportedException();
        public void ConvertOutputToHeap(ref SpanByte input,ref SpanByteAndMemory output) { }
    }
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    private async ValueTask SaveLoad(int LoadCount){
        var ct=TestContext.Current.CancellationToken;
        var folder=Path.Combine(Environment.CurrentDirectory,"Test111");
        var checkpointDir=folder+"_checkpoint";
        const int Key=999;
        const int expected=111;
        Guid token;
        {
            var dir=Path.GetDirectoryName(folder)!;
            var name=Path.GetFileName(folder);
            foreach(var f in Directory.GetFiles(dir,name+".*")) File.Delete(f);
            foreach(var f in Directory.GetFiles(dir,name+"_obj.*")) File.Delete(f);
            if(Directory.Exists(checkpointDir)) Directory.Delete(checkpointDir,true);
            using var LogDevice=new ManagedLocalStorageDevice(folder);
            using var ObjectLogDevice=new ManagedLocalStorageDevice(folder+"_obj");
            using var Store=CreateStore(folder,checkpointDir,LogDevice,ObjectLogDevice);
            {
                var KeyBytes=MemoryPackSerializer.Serialize(Key);
                var ValueBytes=MemoryPackSerializer.Serialize(expected);
                var Session=Store.NewSession<SpanByte,SpanByteAndMemory,Empty,Add>(new());
                SpanByte.FromPinnedSpan(KeyBytes);
                var FixedKey=SpanByte.FromPinnedSpan(KeyBytes);
                var FixedValue=SpanByte.FromPinnedSpan(ValueBytes);
                var Status=Session.BasicContext.Upsert(ref FixedKey,ref FixedValue);
                var result=Session.BasicContext.CompletePending(true);
            }
            Read(Store);
            (_, token)=await Store.TakeFullCheckpointAsync(CheckpointType.FoldOver);
        }
        {
            using var LogDevice1=new ManagedLocalStorageDevice(folder);
            using var ObjectLogDevice1=new ManagedLocalStorageDevice(folder+"_obj");
            using var Store1=CreateStore(folder,checkpointDir,LogDevice1,ObjectLogDevice1);
            for(var a=0;a<LoadCount;a++){
                await Store1.RecoverAsync(default,token,cancellationToken:ct);
            }
            Read(Store1);
        }
        static TsavoriteKV<SpanByte,SpanByte,StoreFunc,OA> CreateStore(string folder,string checkpointDir,ManagedLocalStorageDevice logDevice,ManagedLocalStorageDevice objDevice){
            var Settings = new KVSettings<SpanByte,SpanByte> {
                IndexSize=Const.IndexSize,
                MemorySize=Const.LogMemorySize,
                PageSize=Const.PageSize,
                SegmentSize=Const.SegmentSize,
                MutableFraction=1,
                LogDevice=logDevice,
                ObjectLogDevice=objDevice,
                CheckpointDir=checkpointDir,
            };
            var sf=new StoreFunc(
                new SpanByteComparer(),
                () => null,
                () => null,
                new SpanByteRecordDisposer()
            );
            return new TsavoriteKV<SpanByte,SpanByte,StoreFunc,OA>(
                Settings,
                sf,
                static (AllocSettings,StoreFuncs) => new OA(AllocSettings,StoreFuncs));
        }
        static void Read(TsavoriteKV<SpanByte,SpanByte,StoreFunc,OA> Store){
            var Session=Store.NewSession<SpanByte,SpanByteAndMemory,Empty,Add>(new());
            var KeyBytes = MemoryPackSerializer.Serialize(Key);
            SpanByte.FromPinnedSpan(KeyBytes);
            var FixedKey   =SpanByte.FromPinnedSpan(KeyBytes);
            SpanByteAndMemory Value = default;
            var Status = Session.BasicContext.Read(ref FixedKey,ref Value);
            if(Status.IsPending){
                Session.BasicContext.CompletePendingWithOutputs(out var completedOutputs,wait:true);
                completedOutputs.Next();
                (Status,Value)=(completedOutputs.Current.Status,completedOutputs.Current.Output);
                completedOutputs.Dispose();
            }
            Assert.True(Status.Found);
            var actual = MemoryPackSerializer.Deserialize<int>(Value.AsReadOnlySpan());
            Assert.Equal(expected,actual);
        }
    }
    [Fact,Trait("Category","Coverage")]
    async ValueTask CheckpointAndRecoveryTest_UpsertDelete1(){
        await SaveLoad(1);
    }
    [Fact,Trait("Category","Coverage")]
    async ValueTask CheckpointAndRecoveryTest_UpsertDelete2(){
        await SaveLoad(2);
    }
}