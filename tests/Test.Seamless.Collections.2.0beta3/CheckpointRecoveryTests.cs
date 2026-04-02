using System.Diagnostics;
using AdventureWorks2008R2.Tables;

using Garnet.common;

using Newtonsoft.Json.Linq;

using Seamless.Collections;
using Seamless.SessionFunctions;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Tsavorite.core;

using Xunit.Internal;

using Empty = Seamless.Collections.Empty;
using Store=Seamless.Collections.Store;
namespace Test.Seamless.Collections111;
using IntAllocator=SpanByteAllocator<StoreFunctions<CheckpointRecoveryTests.IntKeyComparer,SpanByteRecordDisposer>>;
using IntStoreFunctions=StoreFunctions<CheckpointRecoveryTests.IntKeyComparer,SpanByteRecordDisposer>;
public class CheckpointRecoveryTests{
    private static string CreateFolder()=>Path.Combine(Environment.CurrentDirectory,"SeamlessTest111_"+Guid.NewGuid().ToString("N"));
    private async ValueTask SaveLoad(int Load回数){
        const int count=200;
        var ct=TestContext.Current.CancellationToken;
        var folder=CreateFolder();
        var checkpointDir=folder+"_checkpoint";
        {
            using var LogDevice=new ManagedLocalStorageDevice(folder);
            using var objDevice=new ManagedLocalStorageDevice(folder+"_obj");
            using var store=Store.Create<int,int>(CheckpointDir:checkpointDir,LogDevice:LogDevice,ObjectLogDevice:objDevice);
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
            await store.SaveAsync(ct);
        }
        {
            using var LogDevice=new ManagedLocalStorageDevice(folder,recoverDevice:true);
            using var objDevice=new ManagedLocalStorageDevice(folder+"_obj",recoverDevice:true);
            using var store=Store.Create<int,int>(CheckpointDir:checkpointDir,LogDevice:LogDevice,ObjectLogDevice:objDevice);
            for(var a = 0; a < Load回数; a++) { 
                await store.LoadAsync(ct);
            }
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
    [Fact,Trait("Category","Coverage")]
    async ValueTask CheckpointAndRecoveryTest_UpsertDelete繰り返し後に復元結果が一致する1(){
        await SaveLoad(1);
    }
    [Fact,Trait("Category","Coverage")]
    async ValueTask CheckpointAndRecoveryTest_UpsertDelete繰り返し後に復元結果が一致する2(){
        await SaveLoad(2);
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
    private static async ValueTask SaveLoad(CheckpointType checkpointType,long IndexSize,int Count=2000){
        var folder=CreateFolder();
        var checkpointDir=folder+"_checkpoint";
        Guid checkpointToken;
        var StoreFunctions=new StoreFunctions<SpanByteComparer,DefaultRecordDisposer>(
            new SpanByteComparer(),
            ()=>null,
            new DefaultRecordDisposer()
        );
        {
            using var LogDevice=new ManagedLocalStorageDevice(folder);
            using var ObjectLogDevice=new ManagedLocalStorageDevice(folder+"_obj");
            var Settings=new KVSettings{
                IndexSize=IndexSize,
                LogMemorySize=Const.LogMemorySize,
                PageSize=Const.PageSize,
                SegmentSize=Const.SegmentSize,
                MutableFraction=1,
                LogDevice=LogDevice,
                ObjectLogDevice=ObjectLogDevice,
                CheckpointDir=checkpointDir,
            };
            // ① SpanByteRecordDisposer → DefaultRecordDisposer に修正
            using var Store=new TsavoriteKV<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>>(
                Settings,
                StoreFunctions,
                static (AllocSettings,StoreFuncs)=>new SpanByteAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>(AllocSettings,StoreFuncs));
            using var Session=Store.NewSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,Add>(new());
            var bContext=Session.BasicContext;
            for(int Key=0;Key<Count;Key++){
                var KeyBytes=MemoryPack.MemoryPackSerializer.Serialize(Key);
                var ValueBytes=MemoryPack.MemoryPackSerializer.Serialize(Key);
                // ② byte[] → FixedSpanByteKey 変換、Upsert の引数修正
                var key=(FixedSpanByteKey)(ReadOnlySpan<byte>)KeyBytes;
                var status=Session.BasicContext.Upsert(key,ValueBytes);
            }
            bContext.CompletePending(wait:true);
            var(success,token)=await Store.TakeFullCheckpointAsync(checkpointType);
            Assert.True(success,$"[{checkpointType}] チェックポイントの開始に失敗しました");
            checkpointToken=token;
        }
        {
            using var LogDevice=new ManagedLocalStorageDevice(folder,recoverDevice:true);
            using var ObjectLogDevice=new ManagedLocalStorageDevice(folder+"_obj",recoverDevice:true);
            var Settings=new KVSettings{
                IndexSize=IndexSize<<1,
                LogMemorySize=Const.LogMemorySize,
                PageSize=Const.PageSize,
                SegmentSize=Const.SegmentSize,
                MutableFraction=1,
                LogDevice=LogDevice,
                ObjectLogDevice=ObjectLogDevice,
                CheckpointDir=checkpointDir,
            };
            // ③ 4引数 StoreFunctions → 2引数 StoreFunctions<SpanByteComparer,DefaultRecordDisposer> に修正
            using var Store=new TsavoriteKV<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>>(
                Settings,
                StoreFunctions,
                static (AllocSettings,StoreFuncs)=>new SpanByteAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>(AllocSettings,StoreFuncs));
            await Store.RecoverAsync(fullCheckpointToken:checkpointToken);
            using var Session=Store.NewSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,Add>(new());
            for(int Key=0;Key<Count;Key++){
                var KeyBytes=MemoryPack.MemoryPackSerializer.Serialize(Key);
                // ④ SpanByte → FixedSpanByteKey、PinnedSpanByte → SpanByteAndMemory に修正
                var key=(FixedSpanByteKey)(ReadOnlySpan<byte>)KeyBytes;
                SpanByteAndMemory Value0=default;
                var status=Session.BasicContext.Read(key,ref Value0);
                if(status.IsPending){
                    Session.BasicContext.CompletePendingWithOutputs(out var completedOutputs,wait:true);
                    using(completedOutputs){
                        Assert.True(completedOutputs.Next(),$"キー {Key} の非同期Readが完了しませんでした");
                        Assert.True(completedOutputs.Current.Status.Found,$"キー {Key} はリカバリー後に見つかるはずです");
                    }
                } else {
                    Assert.True(status.Found,$"キー {Key} はリカバリー後に見つかるはずです");
                }
            }
        }
    }
    private static async Task SaveLoadで取得できないx(long Const){
        await SaveLoad(CheckpointType.Snapshot, Const);
        await SaveLoad(CheckpointType.FoldOver, Const);
        await SaveLoad(CheckpointType.StreamingSnapshot, Const);
    }
    [Fact,Trait("Category","Coverage")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification="<保留中>")]
    public async Task SaveLoadで取得できない9(){
        await SaveLoadで取得できないx(1L<<9);
    }
    [Fact,Trait("Category","Coverage")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification="<保留中>")]
    public async Task SaveLoadで取得できない6(){
        await SaveLoad(CheckpointType.Snapshot,1L<<6,1);
        await SaveLoadで取得できないx(1L<<6);
    }
    public readonly unsafe struct TestSpanByteKey : IKey
    {
        private readonly byte[] arr;
        private readonly void* ptr;
        private readonly int len;

        public readonly bool IsPinned => arr == null;

        public readonly ReadOnlySpan<byte> KeyBytes => arr == null ? new(ptr, len) : arr.AsSpan();

        /// <inheritdoc/>
        public bool HasNamespace => false;

        /// <inheritdoc/>
        public ReadOnlySpan<byte> NamespaceBytes => [];

        private TestSpanByteKey(byte[] arr, void* ptr, int len)
        {
            this.arr = arr;
            this.ptr = ptr;
            this.len = len;
        }

        public static TestSpanByteKey FromPinnedSpan(ReadOnlySpan<byte> key)
        {
            var ptr = Unsafe.AsPointer(ref MemoryMarshal.GetReference(key));
            var len = key.Length;
            return new(null, ptr, len);
        }

        public static TestSpanByteKey CopySpan(ReadOnlySpan<byte> key)
        {
            var arr = key.ToArray();
            return new(arr, null, arr.Length);
        }

        public static TestSpanByteKey FromPointer(byte* ptr, int len) => new(null, ptr, len);

        public static TestSpanByteKey FromArray(byte[] array) => new(array, null, array.Length);
    }
    public struct StoreFunctions
    {
        /// <summary>
        /// Construct a StoreFunctions instance with all types specified and contained instances passed, e.g. for custom objects.
        /// </summary>
        public static StoreFunctions<TKeyComparer, TRecordDisposer> Create<TKeyComparer, TRecordDisposer>
                (TKeyComparer keyComparer, Func<IObjectSerializer<IHeapObject>> valueSerializerCreator, TRecordDisposer recordDisposer)
            where TKeyComparer : IKeyComparer
            where TRecordDisposer : IRecordDisposer
            => new(keyComparer, valueSerializerCreator, recordDisposer);

        /// <summary>
        /// Construct a StoreFunctions instance with all types specified and contained instances passed, e.g. for custom objects.
        /// </summary>
        public static StoreFunctions<TKeyComparer, DefaultRecordDisposer> Create<TKeyComparer>(TKeyComparer keyComparer, Func<IObjectSerializer<IHeapObject>> valueSerializerCreator)
            where TKeyComparer : IKeyComparer
            => new(keyComparer, valueSerializerCreator, DefaultRecordDisposer.Instance);

        /// <summary>
        /// Construct a StoreFunctions instance with all types specified and contained instances passed, e.g. for custom objects.
        /// </summary>
        public static StoreFunctions<TKeyComparer, TRecordDisposer> Create<TKeyComparer, TRecordDisposer>(TKeyComparer keyComparer, TRecordDisposer recordDisposer)
            where TKeyComparer : IKeyComparer
            where TRecordDisposer : IRecordDisposer
            => new(keyComparer, valueSerializerCreator: null, recordDisposer);

        /// <summary>
        /// Store functions that take only the <paramref name="keyComparer"/>
        /// </summary>
        public static StoreFunctions<TKeyComparer, DefaultRecordDisposer> Create<TKeyComparer>(TKeyComparer keyComparer)
            where TKeyComparer : IKeyComparer
            => new(keyComparer, valueSerializerCreator: null, DefaultRecordDisposer.Instance);

        /// <summary>
        /// Store functions for <see cref="Span{_byte_}"/> Key and Value
        /// </summary>
        public static StoreFunctions<SpanByteComparer, DefaultRecordDisposer> Create()
            => new(SpanByteComparer.Instance, valueSerializerCreator: null, DefaultRecordDisposer.Instance);
    }
    public sealed class IntKeyComparer : IKeyComparer
    {
        public static readonly IntKeyComparer Instance = new();

        public bool Equals<TFirstKey, TSecondKey>(TFirstKey k1, TSecondKey k2)
            where TFirstKey : IKey
#if NET9_0_OR_GREATER
                , allows ref struct
#endif
            where TSecondKey : IKey
#if NET9_0_OR_GREATER
                , allows ref struct
#endif
            => k1.KeyBytes.AsRef<int>() == k2.KeyBytes.AsRef<int>();

        public long GetHashCode64<TKey>(TKey k)
            where TKey : IKey
#if NET9_0_OR_GREATER
                , allows ref struct
#endif
            => Utility.GetHashCode(k.KeyBytes.AsRef<int>());
    }

    public class SimpleIntSimpleFunctions : SessionFunctionsBase<int, int, Empty>
    {
        public override bool Reader<TSourceLogRecord>(in TSourceLogRecord srcLogRecord, ref int input, ref int output, ref ReadInfo readInfo)
        {
            output = srcLogRecord.ValueSpan.AsRef<int>();
            return true;
        }

        public override bool InitialWriter(ref LogRecord dstLogRecord, in RecordSizeInfo sizeInfo, ref int input, ReadOnlySpan<byte> srcValue, ref int output, ref UpsertInfo upsertInfo)
        {
            var result = base.InitialWriter(ref dstLogRecord, in sizeInfo, ref input, srcValue, ref output, ref upsertInfo);
            if (result)
                output = srcValue.AsRef<int>();
            return result;
        }

        public override bool InPlaceWriter(ref LogRecord logRecord, in RecordSizeInfo sizeInfo, ref int input, ReadOnlySpan<byte> srcValue, ref int output, ref UpsertInfo upsertInfo)
        {
            var result = base.InPlaceWriter(ref logRecord, in sizeInfo, ref input, srcValue, ref output, ref upsertInfo);
            if (result)
                output = srcValue.AsRef<int>();
            return result;
        }

        public override bool InitialUpdater(ref LogRecord dstLogRecord, in RecordSizeInfo sizeInfo, ref int input, ref int output, ref RMWInfo rmwInfo)
        {
            var ok = dstLogRecord.TrySetValueSpanAndPrepareOptionals(SpanByte.FromPinnedVariable(ref input), in sizeInfo);
            if (ok)
                output = input;
            return ok;
        }

        public override bool CopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord, ref LogRecord dstLogRecord, in RecordSizeInfo sizeInfo, ref int input, ref int output, ref RMWInfo rmwInfo)
        {
            Assert.True(dstLogRecord.TryCopyFrom(in srcLogRecord, in sizeInfo), "Failed TryCopyRecordValues");
            var result = output = input;
            return dstLogRecord.TrySetValueSpanAndPrepareOptionals(SpanByte.FromPinnedVariable(ref result), in sizeInfo);
        }

        public override bool InPlaceUpdater(ref LogRecord logRecord, in RecordSizeInfo sizeInfo, ref int input, ref int output, ref RMWInfo rmwInfo)
        {
            var result = output = input;
            return logRecord.TrySetValueSpanAndPrepareOptionals(SpanByte.FromPinnedVariable(ref result), in sizeInfo);
        }

        public override unsafe RecordFieldInfo GetRMWModifiedFieldInfo<TSourceLogRecord>(in TSourceLogRecord srcLogRecord, ref int input)
        {
            return new() { KeySize = srcLogRecord.Key.Length, ValueSize = sizeof(int) };
        }

        public override unsafe RecordFieldInfo GetRMWInitialFieldInfo<TKey>(TKey key, ref int input)
        {
            return new() { KeySize = key.KeyBytes.Length, ValueSize = sizeof(int) };
        }

        public override unsafe RecordFieldInfo GetUpsertFieldInfo<TKey>(TKey key, ReadOnlySpan<byte> value, ref int input)
        {
            return new() { KeySize = key.KeyBytes.Length, ValueSize = sizeof(int) };
        }
    }
    [Fact,Trait("Category","Coverage")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification="<保留中>")]
    public async ValueTask SaveLoadで取得できる(){
        const int count=200;
        var ct=TestContext.Current.CancellationToken;
        var folder=CreateFolder();
        var checkpointDir=folder+"_checkpoint";
        var checkpointType=CheckpointType.FoldOver;
        Guid checkpointToken;
        {
            using var LogDevice=new ManagedLocalStorageDevice(folder);
            using var store=new TsavoriteKV<IntStoreFunctions,IntAllocator>(
                new KVSettings{
                    IndexSize=1L<<16,
                    LogDevice=LogDevice,
                    LogMemorySize=1L<<22,// 4 MB — 全レコードがメモリに収まるサイズ
                    PageSize=1L<<12,
                    SegmentSize=1L<<22,
                    CheckpointDir=checkpointDir
                },
                StoreFunctions.Create(IntKeyComparer.Instance,SpanByteRecordDisposer.Instance),
                (allocatorSettings,storeFunctions)=>new(allocatorSettings,storeFunctions));
            using var session=store.NewSession<TestSpanByteKey,int,int,Empty,SimpleIntSimpleFunctions>(
                new SimpleIntSimpleFunctions());
            var bContext=session.BasicContext;
            // Step 1: 全キー (0..count-1) を value=key でUpsert
            for(int i=0;i<count;i++){
                var keyNum=i;
                var valueNum=i;
                var key=TestSpanByteKey.FromPinnedSpan(SpanByte.FromPinnedVariable(ref keyNum));
                var value=SpanByte.FromPinnedVariable(ref valueNum);
                _=bContext.Upsert(key,value);
            }
            // Step 2: 偶数キーを全てDelete
            for(int i=0;i<count;i+=2){
                var keyNum=i;
                var key=TestSpanByteKey.FromPinnedSpan(SpanByte.FromPinnedVariable(ref keyNum));
                _=bContext.Delete(key);
            }
            // Step 3: 奇数キーを value=key*10 でUpsert (上書き)
            for(int i=1;i<count;i+=2){
                var keyNum=i;
                var valueNum=i*10;
                var key=TestSpanByteKey.FromPinnedSpan(SpanByte.FromPinnedVariable(ref keyNum));
                var value=SpanByte.FromPinnedVariable(ref valueNum);
                _=bContext.Upsert(key,value);
            }
            _=bContext.CompletePending(wait:true);
            // フルチェックポイント (インデックス + ログ) を取得
            var (success,token)=await store.TakeFullCheckpointAsync(checkpointType);
            Assert.True(success,$"[{checkpointType}] チェックポイントの開始に失敗しました");
            checkpointToken=token;
        }
        // ─── フェーズ 2: リカバリーとデータ検証 ──────────────────────────────────
        {
            // recoverDevice: true で既存ログセグメントを認識させる
            using var LogDevice=new ManagedLocalStorageDevice(folder,recoverDevice:true);
            using var store=new TsavoriteKV<IntStoreFunctions,IntAllocator>(
                new KVSettings{
                    IndexSize=1L<<16,
                    LogDevice=LogDevice,
                    LogMemorySize=1L<<22,// 4 MB — 全レコードがメモリに収まるサイズ
                    PageSize=1L<<12,
                    SegmentSize=1L<<22,
                    CheckpointDir=checkpointDir
                },
                StoreFunctions.Create(IntKeyComparer.Instance,SpanByteRecordDisposer.Instance),
                (allocatorSettings,storeFunctions)=>new(allocatorSettings,storeFunctions));
            // フルチェックポイントはインデックスとログが同一トークン
            _=store.Recover(checkpointToken);
            using var session=store.NewSession<TestSpanByteKey,int,int,Empty,SimpleIntSimpleFunctions>(
                new SimpleIntSimpleFunctions());
            var bContext=session.BasicContext;
            // 検証 1: 偶数キーはDeleteされているため NotFound であること
            for(int i=0;i<count;i+=2){
                var Key=i;
                int output=default;
                var KeySpan=TestSpanByteKey.FromPinnedSpan(SpanByte.FromPinnedVariable(ref Key));
                var status=bContext.Read(KeySpan,ref output);
                if(status.IsPending){
                    _=bContext.CompletePendingWithOutputs(out var completedOutputs,wait:true);
                    using(completedOutputs){
                        // 削除済みレコードは Found にならない
                        if(completedOutputs.Next()){
                            Assert.False(completedOutputs.Current.Status.Found,
                                $"[{checkpointType}] 偶数キー {i} は削除済みのはずですが Found になっています");
                        }
                    }
                } else{
                    Assert.False(status.Found,
                        $"[{checkpointType}] 偶数キー {i} は削除済みのはずですが Found になっています");
                }
            }
            // 検証 2: 奇数キーは value = key * 10 で保存されていること
            for(int i=1;i<count;i+=2){
                var Key=i;
                int output=default;
                var KeySpan=TestSpanByteKey.FromPinnedSpan(SpanByte.FromPinnedVariable(ref Key));
                var status=bContext.Read(KeySpan,ref output);

                if(status.IsPending){
                    _=bContext.CompletePendingWithOutputs(out var completedOutputs,wait:true);
                    using(completedOutputs){
                        Assert.True(completedOutputs.Next(),
                            $"[{checkpointType}] 奇数キー {i} の非同期Readが完了しませんでした");
                        Assert.True(completedOutputs.Current.Status.Found,
                            $"[{checkpointType}] 奇数キー {i} はリカバリー後に見つかるはずです");
                        //Assert.Equal(i*10,completedOutputs.Current.Output,
                        //    $"[{checkpointType}] 奇数キー {i} の値が不正です (期待値={i*10})");
                    }
                } else{
                    Assert.True(status.Found,
                        $"[{checkpointType}] 奇数キー {i} はリカバリー後に見つかるはずです");
                    //ClassicAssert.AreEqual(i*10,output,
                    //    $"[{checkpointType}] 奇数キー {i} の値が不正です (期待値={i*10})");
                }
            }
            _=bContext.CompletePending(wait:true);
        }
    }
}
internal static class SpanByteExtensions
{
    /// <summary>For use with stack-based single T variable.</summary>
    public static ref T AsRef<T>(this Span<byte> spanByte) where T : unmanaged
    {
        Debug.Assert(spanByte.Length >= Unsafe.SizeOf<T>(), $"Span<byte> length expected to be >= {Unsafe.SizeOf<T>()} but was {spanByte.Length}");
        return ref Unsafe.As<byte, T>(ref spanByte[0]);
    }

    /// <summary>For use with stack-based byte vector indexed as a vector of T; usually just the 0th item</summary>
    public static ref readonly T AsRef<T>(this ReadOnlySpan<byte> spanByte) where T : unmanaged
    {
        Debug.Assert(spanByte.Length >= Unsafe.SizeOf<T>(), $"ReadOnlySpan<byte> length expected to be >= {Unsafe.SizeOf<T>()} but was {spanByte.Length}");
        return ref MemoryMarshal.Cast<byte, T>(spanByte)[0];
    }
}
