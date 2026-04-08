using System.Diagnostics.CodeAnalysis;
namespace Seamless.SessionFunctions;

internal readonly struct IntersectWith:
    ISessionFunctions<Collections.Empty,Collections.Empty,Collections.Empty>
{
	[ExcludeFromCodeCoverage]public bool Reader<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref Collections.Empty input,ref Collections.Empty output,ref ReadInfo readInfo)
		where TSourceLogRecord : ISourceLogRecord {
        throw new NotSupportedException("Read");
	}
    [ExcludeFromCodeCoverage]public bool InPlaceWriter(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref Collections.Empty input,ReadOnlySpan<byte> srcValue,ref Collections.Empty output,ref UpsertInfo upsertInfo) {
		throw new NotSupportedException("Upsert");
	}
    [ExcludeFromCodeCoverage]public bool InPlaceWriter(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref Collections.Empty input,IHeapObject srcValue,ref Collections.Empty output,ref UpsertInfo upsertInfo) {
		throw new NotSupportedException("Upsert");
	}
    [ExcludeFromCodeCoverage]public bool InPlaceWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref Collections.Empty input,in TSourceLogRecord inputLogRecord,ref Collections.Empty output,ref UpsertInfo upsertInfo)
		where TSourceLogRecord : ISourceLogRecord {
		throw new NotSupportedException("Upsert");
	}
    [ExcludeFromCodeCoverage]public bool InitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref Collections.Empty input,ReadOnlySpan<byte> srcValue,ref Collections.Empty output,ref UpsertInfo upsertInfo) {
        throw new NotSupportedException("Upsert");
	}
    [ExcludeFromCodeCoverage]public bool InitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref Collections.Empty input,IHeapObject srcValue,ref Collections.Empty output,ref UpsertInfo upsertInfo) {
        throw new NotSupportedException("Upsert");
	}
    [ExcludeFromCodeCoverage]public bool InitialWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref Collections.Empty input,in TSourceLogRecord inputLogRecord,ref Collections.Empty output,ref UpsertInfo upsertInfo)
		where TSourceLogRecord : ISourceLogRecord {
        throw new NotSupportedException("Upsert");
	}
    [ExcludeFromCodeCoverage]public void PostUpsertOperation<TKey,TEpochAccessor>(TKey key,ref Collections.Empty input,ReadOnlySpan<byte> valueSpan,ref UpsertInfo upsertInfo,TEpochAccessor epochAccessor)
        where TKey:IKey,allows ref struct
        where TEpochAccessor:IEpochAccessor{
        throw new NotSupportedException("Upsert");
    }
    [ExcludeFromCodeCoverage]public void PostUpsertOperation<TKey,TEpochAccessor>(TKey key,ref Collections.Empty input,IHeapObject valueObject,ref UpsertInfo upsertInfo,TEpochAccessor epochAccessor)
        where TKey:IKey,allows ref struct
        where TEpochAccessor:IEpochAccessor{
        throw new NotSupportedException("Upsert");
    }
    [ExcludeFromCodeCoverage]public void PostInitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref Collections.Empty input,ReadOnlySpan<byte> srcValue,ref Collections.Empty output,ref UpsertInfo upsertInfo){
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]public void PostInitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref Collections.Empty input,IHeapObject srcValue,ref Collections.Empty output,ref UpsertInfo upsertInfo){
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]public void PostInitialWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref Collections.Empty input,in TSourceLogRecord inputLogRecord,ref Collections.Empty output,ref UpsertInfo upsertInfo)
        where TSourceLogRecord:ISourceLogRecord{
        throw new NotSupportedException();
    }
	[ExcludeFromCodeCoverage]public bool NeedInitialUpdate<TKey>(TKey key,ref Collections.Empty input,ref Collections.Empty output,ref RMWInfo rmwInfo)
		where TKey : IKey, allows ref struct {
        throw new NotSupportedException("Upsert");
	}
	[ExcludeFromCodeCoverage]public bool InitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref Collections.Empty input,ref Collections.Empty output,ref RMWInfo rmwInfo) {
        throw new NotSupportedException("Upsert");
	}
	public bool InPlaceUpdater(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref Collections.Empty input,ref Collections.Empty output,ref RMWInfo rmwInfo) {
		rmwInfo.Action=RMWAction.ExpireAndStop;
		return false;
	}
	[ExcludeFromCodeCoverage]public void PostInitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref Collections.Empty input,ref Collections.Empty output,ref RMWInfo rmwInfo){
        throw new NotSupportedException("Upsert");
	}
	public bool NeedCopyUpdate<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref Collections.Empty input,ref Collections.Empty output,ref RMWInfo rmwInfo)
		where TSourceLogRecord : ISourceLogRecord {
		return true;//ReadOnly領域に見つかった際Mutableにコピーして更新プロセスを続行するか
	}
	public bool CopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref Collections.Empty input,ref Collections.Empty output,ref RMWInfo rmwInfo)
		where TSourceLogRecord : ISourceLogRecord {
		rmwInfo.Action=RMWAction.ExpireAndStop;
		return false;
	}
    [ExcludeFromCodeCoverage]
    public bool PostCopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref Collections.Empty input,ref Collections.Empty output,ref RMWInfo rmwInfo)
		where TSourceLogRecord : ISourceLogRecord {
        throw new NotSupportedException("CopyUpdaterがfalseを返すのでここは呼ばれない");
	}
	public void PostRMWOperation<TKey, TEpochAccessor>(TKey key,ref Collections.Empty input,ref RMWInfo rmwInfo,TEpochAccessor epochAccessor)
		where TKey : IKey, allows ref struct
		where TEpochAccessor : IEpochAccessor {
        //RMWが成功しても失敗しても、両方のケースで呼ばれる。SymmetricExceptWith では RMW の結果に関わらず何もしない。
	}
    [ExcludeFromCodeCoverage]public void RMWCompletionCallback(ref DiskLogRecord diskLogRecord,ref Collections.Empty input,ref Collections.Empty output,Collections.Empty ctx,Status status,RecordMetadata recordMetadata){
        throw new NotSupportedException("RMW の非同期I/O完了時に呼ばれる。");
    }
    [ExcludeFromCodeCoverage]public bool InitialDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) {
		throw new NotSupportedException("Delete");
	}
    [ExcludeFromCodeCoverage]public void PostInitialDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo){
		throw new NotSupportedException("Delete");
    }
    [ExcludeFromCodeCoverage]public bool InPlaceDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) {
		throw new NotSupportedException("Delete");
	}
    [ExcludeFromCodeCoverage]public void PostDeleteOperation<TKey, TEpochAccessor>(TKey key,ref DeleteInfo deleteInfo,TEpochAccessor epochAccessor)
		where TKey : IKey, allows ref struct
		where TEpochAccessor : IEpochAccessor {
		throw new NotSupportedException("Delete");
	}
    [ExcludeFromCodeCoverage]public void ReadCompletionCallback(ref DiskLogRecord diskLogRecord,ref Collections.Empty input,ref Collections.Empty output,Collections.Empty ctx,Status status,RecordMetadata recordMetadata){
        throw new NotSupportedException("Read非同期で呼ばれる");
    }
	public RecordFieldInfo GetRMWModifiedFieldInfo<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref Collections.Empty input)
		where TSourceLogRecord : ISourceLogRecord {
		return new() { KeySize=srcLogRecord.Key.Length,ValueSize=0 };//IntersectWithは更新後の値が存在しない（= 削除）ため、Value部分は空（長さ0）だが、RecordHeaderのDeletedフラグを立てるために4バイト必要。
	}
    [ExcludeFromCodeCoverage]public RecordFieldInfo GetRMWInitialFieldInfo<TKey>(TKey key,ref Collections.Empty input)
		where TKey : IKey, allows ref struct {
        throw new NotSupportedException("IntersectWith RMWで新規レコードを作ることはないので、IntersectWith ではこのメソッドは呼ばれない。");
	}
    [ExcludeFromCodeCoverage]public RecordFieldInfo GetUpsertFieldInfo<TKey>(TKey key,ReadOnlySpan<byte> value,ref Collections.Empty input)
		where TKey : IKey, allows ref struct {
		throw new NotSupportedException("Upsert");
	}
    [ExcludeFromCodeCoverage]public RecordFieldInfo GetUpsertFieldInfo<TKey>(TKey key,IHeapObject value,ref Collections.Empty input)
		where TKey : IKey, allows ref struct {
		throw new NotSupportedException("Upsert");
	}
    [ExcludeFromCodeCoverage]public RecordFieldInfo GetUpsertFieldInfo<TKey, TSourceLogRecord>(TKey key,in TSourceLogRecord inputLogRecord,ref Collections.Empty input)
		where TKey : IKey, allows ref struct
		where TSourceLogRecord : ISourceLogRecord {
		throw new NotSupportedException("Upsert");
	}
    [ExcludeFromCodeCoverage]public void ConvertOutputToHeap(ref Collections.Empty input,ref Collections.Empty output){
        throw new NotSupportedException(@"
            **なぜカバーできないか**
            `IntersectWith` は `GetKeyEnumerable()` → `IterateLookup` でスキャンしたキー（= 必ずメモリ上、HeadAddress以上）に対して RMW を呼ぶ。一方 `ConvertOutputToHeap` が呼ばれる条件は「RMW が IsPending になった時」だけ（= レコードがディスク上、HeadAddress未満）。この2条件は同一レコードに対して同時に成立できないため、`IntersectWith` 経由では構造的に到達不可能。
            `Delete` がカバーできる理由は `dict.Remove(knownKey)` が利用者指定のキーで直接 RMW を呼ぶため（IterateLookup を経由しない）。
        ");
    }
}
