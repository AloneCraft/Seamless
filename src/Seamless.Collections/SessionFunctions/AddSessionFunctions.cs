namespace Seamless.Collections;

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using Tsavorite.core;
/// <summary>
/// RMWでキーの重複を検知するセッション関数。
/// キーが存在しない場合のみ新規レコードを作成し、
/// キーが存在する場合はInPlaceUpdater/NeedCopyUpdateでfalseを返して更新を拒否する。
/// RMWのStatusで重複を判定できる:
///   Status.NotFound=true → 新規挿入成功
///   Status.NotFound=false → 既存キーのため更新拒否（重複検知）
/// </summary>
// ISessionFunctions の呼び出しタイミング一覧(AddSessionFunctions)。
// Reader: Read/ReadAtAddress/ReadModifyWrite 内部の読み取りで呼ばれる。
// InPlaceWriter(全オーバーロード): Upsert の既存キー更新時に呼ばれる。
// InitialWriter(全オーバーロード): Upsert の新規キー挿入時に呼ばれる。
// PostUpsertOperation(全オーバーロード): Upsert 確定後に呼ばれる。
// PostInitialWriter(全オーバーロード): InitialWriter 直後に呼ばれる。
// NeedInitialUpdate: RMW でキー未存在側の判定時に呼ばれる。
// InitialUpdater: RMW の未存在分岐(新規作成)で呼ばれる。
// InPlaceUpdater: RMW の既存レコードその場更新時に呼ばれる(この実装では重複拒否で false)。
// PostInitialUpdater: InitialUpdater 直後に呼ばれる。
// NeedCopyUpdate: RMW のコピー更新可否判定時に呼ばれる(この実装では重複拒否で false)。
// CopyUpdater: NeedCopyUpdate が true の場合にコピー更新で呼ばれる。
// PostCopyUpdater: CopyUpdater 直後に呼ばれる。
// PostRMWOperation: RMW 確定後に呼ばれる。
// RMWCompletionCallback: RMW の非同期I/O完了時に呼ばれる。
// InitialDeleter: Delete の未存在側初期削除パスで呼ばれる。
// PostInitialDeleter: InitialDeleter 直後に呼ばれる。
// InPlaceDeleter: Delete の既存レコード削除時に呼ばれる。
// PostDeleteOperation: Delete 確定後に呼ばれる。
// ReadCompletionCallback: Read の非同期I/O完了時に呼ばれる。
// GetRMWModifiedFieldInfo: RMW コピー更新のサイズ見積もり時に呼ばれる。
// GetRMWInitialFieldInfo: RMW 未存在分岐のサイズ見積もり時に呼ばれる。
// GetUpsertFieldInfo(全オーバーロード): Upsert 前のサイズ見積もり時に呼ばれる。
// ConvertOutputToHeap: 出力のヒープ化が必要なときに呼ばれる。
public readonly struct AddSessionFunctions():ISessionFunctions<PinnedSpanByte,SpanByteAndMemory,Empty> {
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
    /// <summary>
    /// NeedInitialUpdate が true で、キー未存在時は InitialUpdater で値を書き込み
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <param name="key"></param>
    /// <param name="input"></param>
    /// <param name="output"></param>
    /// <param name="rmwInfo"></param>
    /// <returns></returns>
    public bool NeedInitialUpdate<TKey>(TKey key,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo)
        where TKey : IKey, allows ref struct {
        return true;
    }
    public bool InitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo) {
        return dstLogRecord.TrySetValueSpanAndPrepareOptionals(input.ReadOnlySpan,in sizeInfo);
    }
    /// <summary>
    /// キー既存時は NeedCopyUpdate=false かつ InPlaceUpdater=false で更新を拒否
    /// </summary>
    /// <param name="logRecord"></param>
    /// <param name="sizeInfo"></param>
    /// <param name="input"></param>
    /// <param name="output"></param>
    /// <param name="rmwInfo"></param>
    /// <returns></returns>
    public bool InPlaceUpdater(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo) {
        return false;
    }

    public void PostInitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo) { }
    /// <summary>
    /// キー既存時は NeedCopyUpdate=false かつ InPlaceUpdater=false で更新を拒否
    /// </summary>
    /// <typeparam name="TSourceLogRecord"></typeparam>
    /// <param name="srcLogRecord"></param>
    /// <param name="input"></param>
    /// <param name="output"></param>
    /// <param name="rmwInfo"></param>
    /// <returns></returns>
    public bool NeedCopyUpdate<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo)
        where TSourceLogRecord : ISourceLogRecord {
        return false;
    }
    public bool CopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo)
        where TSourceLogRecord : ISourceLogRecord {
        return true;
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
        return new() { KeySize=srcLogRecord.Key.Length,ValueSize=input.Length };
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


