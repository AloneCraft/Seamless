using Garnet.common;
using MemoryPack;
using System.Buffers;
using Tsavorite.core;
using OA = Tsavorite.core.SpanByteAllocator<Tsavorite.core.StoreFunctions<Tsavorite.core.SpanByteComparer,Tsavorite.core.DefaultRecordDisposer>>;
using StoreFunc = Tsavorite.core.StoreFunctions<Tsavorite.core.SpanByteComparer,Tsavorite.core.DefaultRecordDisposer>;
public class CheckpointRecoveryTests{
    public static class Const{
        public const int IndexSizeBits = 6; 
        public const int PageSizeBits = 12;
        public const int MemorySizeBits = PageSizeBits+1;
        public const long IndexSize =1L<<IndexSizeBits;
        public const long LogMemorySize=1L<<MemorySizeBits;
        public const long PageSize  =1L<<PageSizeBits;
        public const long SegmentSize=PageSize*1;
        public const long MinObjectLogCapacity=1L<<22; 
    }
    public readonly struct Add():ISessionFunctions<PinnedSpanByte,SpanByteAndMemory,Empty> {
        private readonly MemoryPool<byte> memoryPool = MemoryPool<byte>.Shared;
        public bool Reader<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref ReadInfo readInfo)
            where TSourceLogRecord:ISourceLogRecord {
            srcLogRecord.ValueSpan.CopyTo(ref output,this.memoryPool);
            return true;
        }
        public bool InPlaceWriter(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ReadOnlySpan<byte> srcValue,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo)
            => logRecord.TrySetValueSpanAndPrepareOptionals(srcValue,in sizeInfo);
        public bool InPlaceWriter(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,IHeapObject srcValue,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo)
            => logRecord.TrySetValueObjectAndPrepareOptionals(srcValue,in sizeInfo);
        public bool InPlaceWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,in TSourceLogRecord inputLogRecord,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo)
            where TSourceLogRecord:ISourceLogRecord
            => dstLogRecord.TryCopyFrom(in inputLogRecord,in sizeInfo);
        public bool InitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ReadOnlySpan<byte> srcValue,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo)
            => dstLogRecord.TrySetValueSpanAndPrepareOptionals(srcValue,in sizeInfo);
        public bool InitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,IHeapObject srcValue,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo)
            => dstLogRecord.TrySetValueObjectAndPrepareOptionals(srcValue,in sizeInfo);
        public bool InitialWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,in TSourceLogRecord inputLogRecord,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo)
            where TSourceLogRecord:ISourceLogRecord
            => dstLogRecord.TryCopyFrom(in inputLogRecord,in sizeInfo);
        public void PostUpsertOperation<TKey,TEpochAccessor>(TKey key,ref PinnedSpanByte input,ReadOnlySpan<byte> valueSpan,ref UpsertInfo upsertInfo,TEpochAccessor epochAccessor)
            where TKey:IKey,allows ref struct where TEpochAccessor:IEpochAccessor {}
        public void PostUpsertOperation<TKey,TEpochAccessor>(TKey key,ref PinnedSpanByte input,IHeapObject valueObject,ref UpsertInfo upsertInfo,TEpochAccessor epochAccessor)
            where TKey:IKey,allows ref struct where TEpochAccessor:IEpochAccessor {}
        public void PostInitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ReadOnlySpan<byte> srcValue,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo) {}
        public void PostInitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,IHeapObject srcValue,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo) {}
        public void PostInitialWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,in TSourceLogRecord inputLogRecord,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo)
            where TSourceLogRecord:ISourceLogRecord {}
        public RecordFieldInfo GetUpsertFieldInfo<TKey>(TKey key,ReadOnlySpan<byte> value,ref PinnedSpanByte input)
            where TKey:IKey,allows ref struct
            => new(){KeySize=key.KeyBytes.Length,ValueSize=value.Length};
        public RecordFieldInfo GetUpsertFieldInfo<TKey>(TKey key,IHeapObject value,ref PinnedSpanByte input)
            where TKey:IKey,allows ref struct
            => new(){KeySize=key.KeyBytes.Length,ValueSize=ObjectIdMap.ObjectIdSize,ValueIsObject=true};
        public RecordFieldInfo GetUpsertFieldInfo<TKey,TSourceLogRecord>(TKey key,in TSourceLogRecord inputLogRecord,ref PinnedSpanByte input)
            where TKey:IKey,allows ref struct where TSourceLogRecord:ISourceLogRecord
            => new(){KeySize=key.KeyBytes.Length,ValueSize=inputLogRecord.Info.ValueIsObject?ObjectIdMap.ObjectIdSize:inputLogRecord.ValueSpan.Length,ValueIsObject=inputLogRecord.Info.ValueIsObject};
        bool ISessionFunctions<PinnedSpanByte,SpanByteAndMemory,Empty>.NeedInitialUpdate<TKey>(TKey key,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo) => throw new NotSupportedException();
        bool ISessionFunctions<PinnedSpanByte,SpanByteAndMemory,Empty>.InitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo) => throw new NotSupportedException();
        bool ISessionFunctions<PinnedSpanByte,SpanByteAndMemory,Empty>.InPlaceUpdater(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo) => throw new NotSupportedException();
        void ISessionFunctions<PinnedSpanByte,SpanByteAndMemory,Empty>.PostInitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo) => throw new NotSupportedException();
        bool ISessionFunctions<PinnedSpanByte,SpanByteAndMemory,Empty>.NeedCopyUpdate<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo) => throw new NotSupportedException();
        bool ISessionFunctions<PinnedSpanByte,SpanByteAndMemory,Empty>.CopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo) => throw new NotSupportedException();
        bool ISessionFunctions<PinnedSpanByte,SpanByteAndMemory,Empty>.PostCopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo) => throw new NotSupportedException();
        void ISessionFunctions<PinnedSpanByte,SpanByteAndMemory,Empty>.PostRMWOperation<TKey,TEpochAccessor>(TKey key,ref PinnedSpanByte input,ref RMWInfo rmwInfo,TEpochAccessor epochAccessor) => throw new NotSupportedException();
        void ISessionFunctions<PinnedSpanByte,SpanByteAndMemory,Empty>.RMWCompletionCallback(ref DiskLogRecord diskLogRecord,ref PinnedSpanByte input,ref SpanByteAndMemory output,Empty ctx,Status status,RecordMetadata recordMetadata) => throw new NotSupportedException();
        RecordFieldInfo IVariableLengthInput<PinnedSpanByte>.GetRMWModifiedFieldInfo<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref PinnedSpanByte input) => throw new NotSupportedException();
        RecordFieldInfo IVariableLengthInput<PinnedSpanByte>.GetRMWInitialFieldInfo<TKey>(TKey key,ref PinnedSpanByte input) => throw new NotSupportedException();
        bool ISessionFunctions<PinnedSpanByte,SpanByteAndMemory,Empty>.InitialDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) => throw new NotSupportedException();
        void ISessionFunctions<PinnedSpanByte,SpanByteAndMemory,Empty>.PostInitialDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) => throw new NotSupportedException();
        bool ISessionFunctions<PinnedSpanByte,SpanByteAndMemory,Empty>.InPlaceDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) => throw new NotSupportedException();
        void ISessionFunctions<PinnedSpanByte,SpanByteAndMemory,Empty>.PostDeleteOperation<TKey,TEpochAccessor>(TKey key,ref DeleteInfo deleteInfo,TEpochAccessor epochAccessor) => throw new NotSupportedException();
        public void ReadCompletionCallback(ref DiskLogRecord diskLogRecord,ref PinnedSpanByte input,ref SpanByteAndMemory output,Empty ctx,Status status,RecordMetadata recordMetadata) {}
        public void ConvertOutputToHeap(ref PinnedSpanByte input,ref SpanByteAndMemory output) {}
    }
    private async ValueTask SaveLoad(CheckpointType　CheckpointType,int LoadCount){
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
                var Session=Store.NewSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,Add>(new());
                var FixedKey=(FixedSpanByteKey)(ReadOnlySpan<byte>)KeyBytes;
                var Status=Session.BasicContext.Upsert(FixedKey,ValueBytes);
                var result=Session.BasicContext.CompletePending(true);
            }


            Read(Store);
            (_, token)=Store.TakeFullCheckpointAsync(CheckpointType).GetAwaiter().GetResult();
            //(_, token)=await Store.TakeFullCheckpointAsync(CheckpointType.FoldOver);
        }
        {
            using var LogDevice1=new ManagedLocalStorageDevice(folder);
            using var ObjectLogDevice1=new ManagedLocalStorageDevice(folder+"_obj");
            using var Store1=CreateStore(folder,checkpointDir,LogDevice1,ObjectLogDevice1);
            for(var a=0;a<LoadCount;a++){
                //await Store1.RecoverAsync(default,token,cancellationToken:ct);
                Store1.Recover(Guid.Empty,token);
            }
            Read(Store1);
        }
        static TsavoriteKV<StoreFunc,OA> CreateStore(string folder,string checkpointDir,ManagedLocalStorageDevice logDevice,ManagedLocalStorageDevice objDevice){
            var Settings=new KVSettings {
                IndexSize=Const.IndexSize,
                LogMemorySize=Const.LogMemorySize,
                PageSize=Const.PageSize,
                SegmentSize=Const.SegmentSize,
                ObjectLogSegmentSize=Const.MinObjectLogCapacity,
                MutableFraction=1,
                LogDevice=logDevice,
                ObjectLogDevice=objDevice,
                CheckpointDir=checkpointDir,
            };
            var sf=new StoreFunctions<SpanByteComparer,DefaultRecordDisposer>(
                new SpanByteComparer(),
                () => null,
                new DefaultRecordDisposer()
            );
            return new TsavoriteKV<StoreFunc,OA>(
                Settings,
                sf,
                static (AllocSettings,StoreFuncs) => new SpanByteAllocator<StoreFunc>(AllocSettings,StoreFuncs));
        }
        static void Read(TsavoriteKV<StoreFunc,OA> Store){
            var Session=Store.NewSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,Add>(new());
            var KeyBytes=MemoryPackSerializer.Serialize(Key);
            var FixedKey=(FixedSpanByteKey)(ReadOnlySpan<byte>)KeyBytes;
            SpanByteAndMemory Value=default;
            var Status=Session.BasicContext.Read(FixedKey,ref Value);
            if(Status.IsPending){
                Session.BasicContext.CompletePendingWithOutputs(out var completedOutputs,wait:true);
                completedOutputs.Next();
                (Status,Value)=(completedOutputs.Current.Status,completedOutputs.Current.Output);
                completedOutputs.Dispose();
            }
            Assert.True(Status.Found);
            var actual=MemoryPackSerializer.Deserialize<int>(Value.ReadOnlySpan);
            Assert.Equal(expected,actual);
        }
    }
    [Fact,Trait("Category","Coverage")]async ValueTask SaveLoad_FoldOver1(){
        await SaveLoad(CheckpointType.FoldOver,1);
    }
    [Fact,Trait("Category","Coverage")]async ValueTask SaveLoad_FoldOver2(){
        await SaveLoad(CheckpointType.FoldOver,2);
    }
    [Fact,Trait("Category","Coverage")]async ValueTask SaveLoad_Snapshot1(){
        await SaveLoad(CheckpointType.Snapshot,1);
    }
    [Fact,Trait("Category","Coverage")]async ValueTask SaveLoad_Snapshot2(){
        await SaveLoad(CheckpointType.Snapshot,2);
    }
    //[Fact,Trait("Category","Coverage")]async ValueTask SaveLoad_StreamingSnapshot1(){
    //    await SaveLoad(CheckpointType.StreamingSnapshot,1);
    //}
    //[Fact,Trait("Category","Coverage")]async ValueTask SaveLoad_StreamingSnapshot2(){
    //    await SaveLoad(CheckpointType.StreamingSnapshot,2);
    //}
    //[Fact,Trait("Category","Coverage")]
    //async ValueTask CheckpointAndRecoveryTest_UpsertDelete1(){
    //    await SaveLoad(1);
    //}
    //[Fact,Trait("Category","Coverage")]
    //async ValueTask CheckpointAndRecoveryTest_UpsertDelete2(){
    //    await SaveLoad(2);
    //}
}