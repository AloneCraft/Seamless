namespace Seamless.Collections.Tsavorite;

using global::System.Diagnostics.CodeAnalysis;

/// <summary>
/// 辞書の代入(<c>dictionary[key]=value</c>)用のセッション関数。RMW で値を書き、キーが新しく入ったときだけ output=true を返す。
/// 挿入か上書きかが分かるので、データ本体の件数を正確に保てる(Upsert では分からない)。
/// その場で書き換えられないとき(値が伸びた・読み取り専用の領域・ディスク上のレコード)は、末尾へ書き直す(RCU)。
/// </summary>
public readonly struct SetValue<TValue, TStrategy>:
    ISessionFunctions<TValue,bool,EqualityEmpty>,
    ICreate<SetValue<TValue,TStrategy>> where TStrategy : ISerializer<TValue> {
    public static SetValue<TValue,TStrategy> Create() => new();
    [ExcludeFromCodeCoverage]
    [DoesNotReturn]
    private static T ThrowNotRmwEntryPoint<T>() {
        // SetValue は RMW にだけ使うので、Read・Upsert・Delete のコールバックには到達しない。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    [DoesNotReturn]
    private static void ThrowNotRmwEntryPoint() {
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool Reader<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref TValue input,ref bool output,ref ReadInfo readInfo)
        where TSourceLogRecord : ISourceLogRecord => ThrowNotRmwEntryPoint<bool>();
    [ExcludeFromCodeCoverage]
    public void ReadCompletionCallback(ref DiskLogRecord diskLogRecord,ref TValue input,ref bool output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata) => ThrowNotRmwEntryPoint();

    // キーが無い(削除の印を含む)ときは新しく入れる。
    public bool NeedInitialUpdate<TKey>(TKey key,ref TValue input,ref bool output,ref RMWInfo rmwInfo)
#if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct {
#else
        where TKey : IKey{
#endif
        return true;
    }
    public bool InitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,ref bool output,ref RMWInfo rmwInfo) {
        return dstLogRecord.TrySetValueSpanAndPrepareOptionals(TStrategy.Serialize(ref input),in sizeInfo);
    }
    public void PostInitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,ref bool output,ref RMWInfo rmwInfo) {
        output=true;
    }
    // キーがあるときは値を上書きする(新規ではないので output は false のまま)。
    public bool InPlaceUpdater(ref LogRecord logRecord,ref TValue input,ref bool output,ref RMWInfo rmwInfo) {
        var value = TStrategy.Serialize(ref input);
        RecordSizeInfo sizeInfo = new() { FieldInfo=new() { KeySize=logRecord.KeyBytes.Length,ValueSize=value.Length } };
        logRecord.PopulateRecordSizeInfoForIPU(ref sizeInfo);
        // 入りきらなければ false を返し、末尾への書き直し(CopyUpdater)に回る。
        return logRecord.TrySetValueSpanAndPrepareOptionals(value,in sizeInfo);
    }
    public bool NeedCopyUpdate<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref TValue input,ref bool output,ref RMWInfo rmwInfo)
        where TSourceLogRecord : ISourceLogRecord {
        return true;
    }
    public bool CopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,ref bool output,ref RMWInfo rmwInfo)
        where TSourceLogRecord : ISourceLogRecord {
        return dstLogRecord.TrySetValueSpanAndPrepareOptionals(TStrategy.Serialize(ref input),in sizeInfo);
    }
    public bool PostCopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,ref bool output,ref RMWInfo rmwInfo)
        where TSourceLogRecord : ISourceLogRecord {
        return true;
    }
    public void PostRMWOperation<TKey, TEpochAccessor>(TKey key,ref TValue input,ref RMWInfo rmwInfo,TEpochAccessor epochAccessor)
#if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct
#else
        where TKey : IKey
#endif
        where TEpochAccessor : IEpochAccessor {
    }
    // ディスク上のレコードを読んでから書いたとき。output は PostInitialUpdater/PostCopyUpdater が決めたものを残す。
    public void RMWCompletionCallback(ref DiskLogRecord diskLogRecord,ref TValue input,ref bool output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata) {
    }
    public RecordFieldInfo GetRMWModifiedFieldInfo<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref TValue input)
        where TSourceLogRecord : ISourceLogRecord {
        return new() { KeySize=srcLogRecord.KeyBytes.Length,ValueSize=TStrategy.Serialize(ref input).Length };
    }
    public RecordFieldInfo GetRMWInitialFieldInfo<TKey>(TKey key,ref TValue input)
#if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct {
#else
        where TKey : IKey{
#endif
        return new() { KeySize=key.KeyBytes.Length,ValueSize=TStrategy.Serialize(ref input).Length };
    }

    [ExcludeFromCodeCoverage]
    public bool InitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,ReadOnlySpan<byte> srcValue,ref bool output,ref UpsertInfo upsertInfo) => ThrowNotRmwEntryPoint<bool>();
    [ExcludeFromCodeCoverage]
    public bool InitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,IHeapObject srcValue,ref bool output,ref UpsertInfo upsertInfo) => ThrowNotRmwEntryPoint<bool>();
    [ExcludeFromCodeCoverage]
    public bool InitialWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,in TSourceLogRecord inputLogRecord,ref bool output,ref UpsertInfo upsertInfo)
        where TSourceLogRecord : ISourceLogRecord => ThrowNotRmwEntryPoint<bool>();
    [ExcludeFromCodeCoverage]
    public void PostInitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,ReadOnlySpan<byte> srcValue,ref bool output,ref UpsertInfo upsertInfo) => ThrowNotRmwEntryPoint();
    [ExcludeFromCodeCoverage]
    public void PostInitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,IHeapObject srcValue,ref bool output,ref UpsertInfo upsertInfo) => ThrowNotRmwEntryPoint();
    [ExcludeFromCodeCoverage]
    public void PostInitialWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,in TSourceLogRecord inputLogRecord,ref bool output,ref UpsertInfo upsertInfo)
        where TSourceLogRecord : ISourceLogRecord => ThrowNotRmwEntryPoint();
    [ExcludeFromCodeCoverage]
    public bool InPlaceWriter(ref LogRecord logRecord,ref TValue input,ReadOnlySpan<byte> srcValue,ref bool output,ref UpsertInfo upsertInfo) => ThrowNotRmwEntryPoint<bool>();
    [ExcludeFromCodeCoverage]
    public bool InPlaceWriter(ref LogRecord logRecord,ref TValue input,IHeapObject srcValue,ref bool output,ref UpsertInfo upsertInfo) => ThrowNotRmwEntryPoint<bool>();
    [ExcludeFromCodeCoverage]
    public bool InPlaceWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,ref TValue input,in TSourceLogRecord inputLogRecord,ref bool output,ref UpsertInfo upsertInfo)
        where TSourceLogRecord : ISourceLogRecord => ThrowNotRmwEntryPoint<bool>();
    [ExcludeFromCodeCoverage]
    public void PostUpsertOperation<TKey, TEpochAccessor>(TKey key,ref TValue input,ReadOnlySpan<byte> valueSpan,ref UpsertInfo upsertInfo,TEpochAccessor epochAccessor)
#if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct
#else
        where TKey : IKey
#endif
        where TEpochAccessor : IEpochAccessor => ThrowNotRmwEntryPoint();
    [ExcludeFromCodeCoverage]
    public void PostUpsertOperation<TKey, TEpochAccessor>(TKey key,ref TValue input,IHeapObject valueObject,ref UpsertInfo upsertInfo,TEpochAccessor epochAccessor)
#if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct
#else
        where TKey : IKey
#endif
        where TEpochAccessor : IEpochAccessor => ThrowNotRmwEntryPoint();
    [ExcludeFromCodeCoverage]
    public bool InitialDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) => ThrowNotRmwEntryPoint<bool>();
    [ExcludeFromCodeCoverage]
    public void PostInitialDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) => ThrowNotRmwEntryPoint();
    [ExcludeFromCodeCoverage]
    public bool InPlaceDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) => ThrowNotRmwEntryPoint<bool>();
    [ExcludeFromCodeCoverage]
    public void PostDeleteOperation<TKey, TEpochAccessor>(TKey key,ref DeleteInfo deleteInfo,TEpochAccessor epochAccessor)
#if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct
#else
        where TKey : IKey
#endif
        where TEpochAccessor : IEpochAccessor => ThrowNotRmwEntryPoint();
    [ExcludeFromCodeCoverage]
    public RecordFieldInfo GetUpsertFieldInfo<TKey>(TKey key,ReadOnlySpan<byte> value,ref TValue input)
#if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct
#else
        where TKey : IKey
#endif
        => ThrowNotRmwEntryPoint<RecordFieldInfo>();
    [ExcludeFromCodeCoverage]
    public RecordFieldInfo GetUpsertFieldInfo<TKey>(TKey key,IHeapObject value,ref TValue input)
#if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct
#else
        where TKey : IKey
#endif
        => ThrowNotRmwEntryPoint<RecordFieldInfo>();
    [ExcludeFromCodeCoverage]
    public RecordFieldInfo GetUpsertFieldInfo<TKey, TSourceLogRecord>(TKey key,in TSourceLogRecord inputLogRecord,ref TValue input)
#if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct
#else
        where TKey : IKey
#endif
        where TSourceLogRecord : ISourceLogRecord => ThrowNotRmwEntryPoint<RecordFieldInfo>();
    public void PreSingleKeyConsistentRead(long hash) {
    }
    public void PostSingleKeyConsistentReadCallback() {
    }
    public void PreBatchKeyConsistentReadCallback(ReadOnlySpan<PinnedSpanByte> parameters) {
    }
    public bool PostBatchKeyConsistentReadCallback(int keyCount) {
        return true;
    }
    public void ConvertOutputToHeap(ref TValue input,ref bool output) {
    }
}
