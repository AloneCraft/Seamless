namespace Seamless.Collections;

using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using Tsavorite.core;
/// <summary>
/// ISessionFunctions      :の呼び出しタイミング一覧(DefaultSessionFunctions)。
/// Reader                 :Read/ReadAtAddress/ReadModifyWrite 内部の読み取りで呼ばれる。
/// InPlaceWriter          :(全オーバーロード): Upsert で既存レコードを同一エントリ内で更新できる場合に呼ばれる。
/// InitialWriter          :(全オーバーロード): Upsert で対象キーが未存在、または新規レコード作成が必要な場合に呼ばれる。
/// PostUpsertOperation    :(全オーバーロード): Upsert の書き込み確定後に呼ばれる。
/// PostInitialWriter      :(全オーバーロード): InitialWriter 実行直後に呼ばれる。
/// NeedInitialUpdate      :RMW で対象キーが未存在側の分岐判定時に呼ばれる。
/// InitialUpdater         :RMW の未存在分岐で新規値を作る際に呼ばれる。
/// InPlaceUpdater         :RMW で既存レコードをその場更新できる場合に呼ばれる。
/// PostInitialUpdater     :InitialUpdater 実行直後に呼ばれる。
/// NeedCopyUpdate         :RMW でコピー更新(新レコード作成)が必要か判定するときに呼ばれる。
/// CopyUpdater            :RMW のコピー更新実行時に呼ばれる。
/// PostCopyUpdater        :CopyUpdater 実行直後に呼ばれる。
/// PostRMWOperation       :RMW の最終確定後に呼ばれる。
/// RMWCompletionCallback  :RMW が非同期I/O完了(ディスクフェッチ完了)したときに呼ばれる。
/// InitialDeleter         :Delete でキー未存在側の初期削除パスに入ったときに呼ばれる。
/// PostInitialDeleter     :InitialDeleter 実行直後に呼ばれる。
/// InPlaceDeleter         :Delete で既存レコードをその場削除(トゥームストーン化)するときに呼ばれる。
/// PostDeleteOperation    :Delete の最終確定後に呼ばれる。
/// ReadCompletionCallback :Read が非同期I/O完了(ディスクフェッチ完了)したときに呼ばれる。
/// GetRMWModifiedFieldInfo:RMW のコピー更新時、更新後レコードのサイズ見積もりで呼ばれる。
/// GetRMWInitialFieldInfo :RMW の未存在分岐で新規レコードサイズ見積もり時に呼ばれる。
/// GetUpsertFieldInfo     :(全オーバーロード): Upsert の書き込み前にレコードサイズ見積もりで呼ばれる。
/// ConvertOutputToHeap    :出力をヒープに昇格/固定化する必要があるときに呼ばれる。
/// </summary>
internal readonly struct DefaultSessionFunctions():ISessionFunctions<PinnedSpanByte,SpanByteAndMemory,Empty> {
    private readonly MemoryPool<byte> memoryPool = MemoryPool<byte>.Shared;
    public bool Reader<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref ReadInfo readInfo)
        where TSourceLogRecord : ISourceLogRecord {
        srcLogRecord.ValueSpan.CopyTo(ref output,this.memoryPool);
        return true;
    }
    public bool InPlaceWriter(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ReadOnlySpan<byte> srcValue,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo) {
        return logRecord.TrySetValueSpanAndPrepareOptionals(srcValue,in sizeInfo);
    }

    [ExcludeFromCodeCoverage]
    public bool InPlaceWriter(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,IHeapObject srcValue,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo) {
        return logRecord.TrySetValueObjectAndPrepareOptionals(srcValue,in sizeInfo);
    }
    [ExcludeFromCodeCoverage]
    public bool InPlaceWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,in TSourceLogRecord inputLogRecord,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo) where TSourceLogRecord : ISourceLogRecord {
        return dstLogRecord.TryCopyFrom(in inputLogRecord,in sizeInfo);
    }

    public bool InitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ReadOnlySpan<byte> srcValue,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo) {
        return dstLogRecord.TrySetValueSpanAndPrepareOptionals(srcValue,in sizeInfo);
    }
    [ExcludeFromCodeCoverage]
    public bool InitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,IHeapObject srcValue,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo) {
        return dstLogRecord.TrySetValueObjectAndPrepareOptionals(srcValue,in sizeInfo);
    }
    [ExcludeFromCodeCoverage]
    public bool InitialWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,in TSourceLogRecord inputLogRecord,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo)
        where TSourceLogRecord : ISourceLogRecord {
        return dstLogRecord.TryCopyFrom(in inputLogRecord,in sizeInfo);
    }
    public void PostUpsertOperation<TKey, TEpochAccessor>(TKey key,ref PinnedSpanByte input,ReadOnlySpan<byte> valueSpan,ref UpsertInfo upsertInfo,TEpochAccessor epochAccessor)
        where TKey : IKey, allows ref struct
        where TEpochAccessor : IEpochAccessor { }
    public void PostUpsertOperation<TKey, TEpochAccessor>(TKey key,ref PinnedSpanByte input,IHeapObject valueObject,ref UpsertInfo upsertInfo,TEpochAccessor epochAccessor)
        where TKey : IKey, allows ref struct
        where TEpochAccessor : IEpochAccessor { }
    public void PostInitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ReadOnlySpan<byte> srcValue,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo) { }
    public void PostInitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,IHeapObject srcValue,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo) { }
    public void PostInitialWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,in TSourceLogRecord inputLogRecord,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo)
        where TSourceLogRecord : ISourceLogRecord { }









    public bool NeedInitialUpdate<TKey>(TKey key,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo)
        where TKey : IKey, allows ref struct {
        return true;
    }
    public bool InitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo) {
        return dstLogRecord.TrySetValueSpanAndPrepareOptionals(input.ReadOnlySpan,in sizeInfo);
    }









    public bool InPlaceUpdater(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo) {
        return logRecord.TrySetValueSpanAndPrepareOptionals(input.ReadOnlySpan,in sizeInfo);
    }

    public void PostInitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo) { }









    public bool NeedCopyUpdate<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo)
        where TSourceLogRecord : ISourceLogRecord {
        return true;
    }
    public bool CopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo)
        where TSourceLogRecord : ISourceLogRecord {
        return dstLogRecord.TryCopyFrom(in srcLogRecord,in sizeInfo);
    }

    public bool PostCopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo)
        where TSourceLogRecord : ISourceLogRecord {
        return true;
    }
    public void PostRMWOperation<TKey, TEpochAccessor>(TKey key,ref PinnedSpanByte input,ref RMWInfo rmwInfo,TEpochAccessor epochAccessor)
        where TKey : IKey, allows ref struct
        where TEpochAccessor : IEpochAccessor {
    }

    public void RMWCompletionCallback(ref DiskLogRecord diskLogRecord,ref PinnedSpanByte input,ref SpanByteAndMemory output,Empty ctx,Status status,RecordMetadata recordMetadata) { }
    public bool InitialDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) {
        return true;
    }


    public void PostInitialDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) { }
    public bool InPlaceDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) {
        return true;
    }
    public void PostDeleteOperation<TKey, TEpochAccessor>(TKey key,ref DeleteInfo deleteInfo,TEpochAccessor epochAccessor)
        where TKey : IKey, allows ref struct
        where TEpochAccessor : IEpochAccessor { }


    public void ReadCompletionCallback(ref DiskLogRecord diskLogRecord,ref PinnedSpanByte input,ref SpanByteAndMemory output,Empty ctx,Status status,RecordMetadata recordMetadata) { }
    public RecordFieldInfo GetRMWModifiedFieldInfo<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref PinnedSpanByte input)
        where TSourceLogRecord : ISourceLogRecord {
        return new() { KeySize=srcLogRecord.Key.Length,ValueSize=Math.Max(srcLogRecord.ValueSpan.Length,input.Length) };
    }
    public RecordFieldInfo GetRMWInitialFieldInfo<TKey>(TKey key,ref PinnedSpanByte input)
        where TKey : IKey, allows ref struct {
        return new() { KeySize=key.KeyBytes.Length,ValueSize=input.Length };
    }

    public RecordFieldInfo GetUpsertFieldInfo<TKey>(TKey key,ReadOnlySpan<byte> value,ref PinnedSpanByte input)
        where TKey : IKey, allows ref struct {
        return new() { KeySize=key.KeyBytes.Length,ValueSize=value.Length };
    }

    public RecordFieldInfo GetUpsertFieldInfo<TKey>(TKey key,IHeapObject value,ref PinnedSpanByte input)
        where TKey : IKey, allows ref struct {
        return new() { KeySize=key.KeyBytes.Length,ValueSize=ObjectIdMap.ObjectIdSize,ValueIsObject=true };
    }

    public RecordFieldInfo GetUpsertFieldInfo<TKey, TSourceLogRecord>(TKey key,in TSourceLogRecord inputLogRecord,ref PinnedSpanByte input)
        where TKey : IKey, allows ref struct
        where TSourceLogRecord : ISourceLogRecord {
        return new() { KeySize=key.KeyBytes.Length,ValueSize=inputLogRecord.Info.ValueIsObject ? ObjectIdMap.ObjectIdSize : inputLogRecord.ValueSpan.Length,ValueIsObject=inputLogRecord.Info.ValueIsObject };
    }




    public void ConvertOutputToHeap(ref PinnedSpanByte input,ref SpanByteAndMemory output) { }
}