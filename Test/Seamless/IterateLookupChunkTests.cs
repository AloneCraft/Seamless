using Garnet.common;

using global::System.Diagnostics.CodeAnalysis;

using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Tsavorite.core;

using TAlloc = Tsavorite.core.SpanByteAllocator<
    Tsavorite.core.StoreFunctions<
        Tsavorite.core.SpanByteComparer,
        Tsavorite.core.DefaultRecordTriggers>>;
// ファイルスコープ型エイリアス
using TStoreFuncs = Tsavorite.core.StoreFunctions<
    Tsavorite.core.SpanByteComparer,
    Tsavorite.core.DefaultRecordTriggers>;

namespace Test;

/// <summary>
/// IterateLookup で Tsavorite API のみを使ったチャンク 10 件単位のスキャンと
/// Pending（ディスク I/O 由来の非同期完了）を考慮したパターンを検証するテスト。
///
/// <para>ポイント</para>
/// <list type="bullet">
///   <item>IScanIteratorFunctions.Reader 内で <see cref="CursorRecordResult.EndBatch"/> |
///   <see cref="CursorRecordResult.RetryLastRecord"/> を返すと、IterateLookup は cursor を
///   更新して <c>true</c> を返す。次のループで同じ cursor から再開できる。</item>
///   <item>ChunkScanFunctions は key/value をその場で int/int にデシリアライズして
///   Chunk へ格納する。Span をバッチ外へ持ち出さない。</item>
///   <item>値のシリアライズ/デシリアライズは SimpleUpsertFunctions が担当する。
///   キーは <see cref="FixedSpanByteKey"/> として Tsavorite に渡す必要があるため、
///   呼び出し側でシリアライズし、その場で Pending を解消する。</item>
/// </list>
/// </summary>
[Collection("TsavoriteTests")]
public class IterateLookupChunkTests(ITestOutputHelper output) {
    private const int ChunkSize = 10;   // 1 バッチで処理するレコード数
    private const int TotalKeys = 25;   // 挿入するキー総数

    private static void SerializeKey(int key,Span<byte> destination) =>
        BinaryPrimitives.WriteInt32LittleEndian(destination,key);

    private static int DeserializeKey(ReadOnlySpan<byte> source) =>
        BinaryPrimitives.ReadInt32LittleEndian(source);

    private static ReadOnlySpan<byte> SerializeValue(ref int value) =>
        MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref value,1));

    private static int DeserializeValue(ReadOnlySpan<byte> source) =>
        BinaryPrimitives.ReadInt32LittleEndian(source);

    private static class Const {
        public const int Chunk_Length = ChunkSize;
    }

    // ----------------------------------------------------------------
    // セッション関数: RMW で int 値をログへ保存し、Read で int に戻す
    // ----------------------------------------------------------------
    private readonly struct SimpleUpsertFunctions:
        ISessionFunctions<int,int,Empty> {

        [DoesNotReturn]
        private static void ThrowRmwOnlyEntryPoint() =>
            throw new NotSupportedException();

        [DoesNotReturn]
        private static T ThrowRmwOnlyEntryPoint<T>() =>
            throw new NotSupportedException();

        public bool Reader<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref int input,ref int output,ref ReadInfo readInfo)
            where TSourceLogRecord : ISourceLogRecord {
            output=DeserializeValue(srcLogRecord.ValueSpan);
            return true;
        }

        public void ReadCompletionCallback(ref DiskLogRecord diskLogRecord,ref int input,ref int output,Empty ctx,Status status,RecordMetadata recordMetadata) {
            output=status.Found ? DeserializeValue(diskLogRecord.ValueSpan) : default;
        }

        public bool NeedInitialUpdate<TKey>(TKey key,ref int input,ref int output,ref RMWInfo rmwInfo)
#if NET10_0_OR_GREATER
            where TKey : IKey, allows ref struct => true;
#else
            where TKey : IKey => true;
#endif

        public bool InitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref int input,ref int output,ref RMWInfo rmwInfo) {
            output=input;
            return dstLogRecord.TrySetValueSpanAndPrepareOptionals(SerializeValue(ref input),in sizeInfo);
        }

        public void PostInitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref int input,ref int output,ref RMWInfo rmwInfo) {
            output=input;
        }

        public bool InPlaceUpdater(ref LogRecord logRecord,ref int input,ref int output,ref RMWInfo rmwInfo) {
            return false;
        }

        public bool NeedCopyUpdate<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref int input,ref int output,ref RMWInfo rmwInfo)
            where TSourceLogRecord : ISourceLogRecord {
            return true;
        }

        public bool CopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref int input,ref int output,ref RMWInfo rmwInfo)
            where TSourceLogRecord : ISourceLogRecord {
            output=input;
            return dstLogRecord.TrySetValueSpanAndPrepareOptionals(SerializeValue(ref input),in sizeInfo);
        }

        public bool PostCopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref int input,ref int output,ref RMWInfo rmwInfo)
            where TSourceLogRecord : ISourceLogRecord {
            output=input;
            return true;
        }

        public void PostRMWOperation<TKey, TEpochAccessor>(TKey key,ref int input,ref RMWInfo rmwInfo,TEpochAccessor epochAccessor)
#if NET10_0_OR_GREATER
            where TKey : IKey, allows ref struct
#else
            where TKey : IKey
#endif
            where TEpochAccessor : IEpochAccessor {
        }

        public void RMWCompletionCallback(ref DiskLogRecord diskLogRecord,ref int input,ref int output,Empty ctx,Status status,RecordMetadata recordMetadata) {
        }

        public RecordFieldInfo GetRMWModifiedFieldInfo<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref int input)
            where TSourceLogRecord : ISourceLogRecord {
            return new() { KeySize=srcLogRecord.Key.Length,ValueSize=Unsafe.SizeOf<int>() };
        }

        public RecordFieldInfo GetRMWInitialFieldInfo<TKey>(TKey key,ref int input)
#if NET10_0_OR_GREATER
            where TKey : IKey, allows ref struct {
#else
            where TKey : IKey {
#endif
            return new() { KeySize=key.KeyBytes.Length,ValueSize=Unsafe.SizeOf<int>() };
        }

        public bool InitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref int input,ReadOnlySpan<byte> srcValue,ref int output,ref UpsertInfo upsertInfo) =>
            ThrowRmwOnlyEntryPoint<bool>();

        public bool InitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref int input,IHeapObject srcValue,ref int output,ref UpsertInfo upsertInfo) =>
            ThrowRmwOnlyEntryPoint<bool>();

        public bool InitialWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref int input,in TSourceLogRecord inputLogRecord,ref int output,ref UpsertInfo upsertInfo)
            where TSourceLogRecord : ISourceLogRecord => ThrowRmwOnlyEntryPoint<bool>();

        public bool InPlaceWriter(ref LogRecord logRecord,ref int input,ReadOnlySpan<byte> srcValue,ref int output,ref UpsertInfo upsertInfo) =>
            ThrowRmwOnlyEntryPoint<bool>();

        public bool InPlaceWriter(ref LogRecord logRecord,ref int input,IHeapObject srcValue,ref int output,ref UpsertInfo upsertInfo) =>
            ThrowRmwOnlyEntryPoint<bool>();

        public bool InPlaceWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,ref int input,in TSourceLogRecord inputLogRecord,ref int output,ref UpsertInfo upsertInfo)
            where TSourceLogRecord : ISourceLogRecord => ThrowRmwOnlyEntryPoint<bool>();

        public void PostUpsertOperation<TKey, TEpochAccessor>(TKey key,ref int input,ReadOnlySpan<byte> valueSpan,ref UpsertInfo upsertInfo,TEpochAccessor epochAccessor)
#if NET10_0_OR_GREATER
            where TKey : IKey, allows ref struct
#else
            where TKey : IKey
#endif
            where TEpochAccessor : IEpochAccessor {
            ThrowRmwOnlyEntryPoint();
        }

        public void PostUpsertOperation<TKey, TEpochAccessor>(TKey key,ref int input,IHeapObject valueObject,ref UpsertInfo upsertInfo,TEpochAccessor epochAccessor)
#if NET10_0_OR_GREATER
            where TKey : IKey, allows ref struct
#else
            where TKey : IKey
#endif
            where TEpochAccessor : IEpochAccessor {
            ThrowRmwOnlyEntryPoint();
        }

        public void PostInitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref int input,ReadOnlySpan<byte> srcValue,ref int output,ref UpsertInfo upsertInfo) =>
            ThrowRmwOnlyEntryPoint();

        public void PostInitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref int input,IHeapObject srcValue,ref int output,ref UpsertInfo upsertInfo) =>
            ThrowRmwOnlyEntryPoint();

        public void PostInitialWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref int input,in TSourceLogRecord inputLogRecord,ref int output,ref UpsertInfo upsertInfo)
            where TSourceLogRecord : ISourceLogRecord => ThrowRmwOnlyEntryPoint();

        public bool InitialDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) =>
            ThrowRmwOnlyEntryPoint<bool>();

        public void PostInitialDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) =>
            ThrowRmwOnlyEntryPoint();

        public bool InPlaceDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) =>
            ThrowRmwOnlyEntryPoint<bool>();

        public void PostDeleteOperation<TKey, TEpochAccessor>(TKey key,ref DeleteInfo deleteInfo,TEpochAccessor epochAccessor)
#if NET10_0_OR_GREATER
            where TKey : IKey, allows ref struct
#else
            where TKey : IKey
#endif
            where TEpochAccessor : IEpochAccessor => ThrowRmwOnlyEntryPoint();

        public RecordFieldInfo GetUpsertFieldInfo<TKey>(TKey key,ReadOnlySpan<byte> value,ref int input)
#if NET10_0_OR_GREATER
            where TKey : IKey, allows ref struct {
#else
            where TKey : IKey {
#endif
            return new() { KeySize=key.KeyBytes.Length,ValueSize=value.Length };
        }

        public RecordFieldInfo GetUpsertFieldInfo<TKey>(TKey key,IHeapObject value,ref int input)
#if NET10_0_OR_GREATER
            where TKey : IKey, allows ref struct {
#else
            where TKey : IKey {
#endif
            return new() { KeySize=key.KeyBytes.Length,ValueSize=Unsafe.SizeOf<int>(),ValueIsObject=true };
        }

        public RecordFieldInfo GetUpsertFieldInfo<TKey, TSourceLogRecord>(TKey key,in TSourceLogRecord inputLogRecord,ref int input)
#if NET10_0_OR_GREATER
            where TKey : IKey, allows ref struct
#else
            where TKey : IKey
#endif
            where TSourceLogRecord : ISourceLogRecord {
            return new() { KeySize=key.KeyBytes.Length,ValueSize=inputLogRecord.ValueSpan.Length,ValueIsObject=inputLogRecord.DataHeader.ValueIsObject };
        }

        public void BeforeConsistentReadCallback(long hash) {
        }

        public void AfterConsistentReadKeyCallback() {
        }

        public void BeforeConsistentReadKeyBatchCallback(ReadOnlySpan<PinnedSpanByte> parameters) {
        }

        public bool AfterConsistentReadKeyBatchCallback(int keyCount) => true;

        public void ConvertOutputToHeap(ref int input,ref int output) {
        }
    }

    // ----------------------------------------------------------------
    // IScanIteratorFunctions: ChunkSize 件ごとにバッチを打ち切り、キーを格納
    //
    // Tsavorite 2.0 では IterateLookup は ref TScanFunctions で受け取り、
    // Reader<TSourceLogRecord> 汎用メソッドで各レコードを処理する。
    // Count は ref struct 経由で正しく更新される。
    //
    // ISourceLogRecord から Key / ValueSpan を取得できる。
    // 各バッチ (ChunkSize 件) 後に EndBatch を返して IterateLookup を中断できる。
    // ----------------------------------------------------------------
    private abstract class ScanIteratorFunctions<T>:IScanIteratorFunctions {
        public readonly T[] Chunk = new T[global::Seamless.Tsavorite.Const.Chunk_Length];
        public int Count;
        private long LastAcceptedAddress;

        private protected abstract T Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord)
            where TSourceLogRecord : ISourceLogRecord;

        public bool Reader<TSourceLogRecord>(
            in TSourceLogRecord logRecord,
            RecordMetadata recordMetadata,
            long numberOfRecords,
            out CursorRecordResult cursorRecordResult)
            where TSourceLogRecord : ISourceLogRecord {
            var index = Interlocked.Increment(ref this.Count)-1;
            if(index>=global::Seamless.Tsavorite.Const.Chunk_Length) {
                Volatile.Write(ref this.Count,global::Seamless.Tsavorite.Const.Chunk_Length);
                var lastAcceptedAddress = Volatile.Read(ref this.LastAcceptedAddress);
                if(recordMetadata.Address==lastAcceptedAddress) {
                    cursorRecordResult=
                        CursorRecordResult.Skip|
                        CursorRecordResult.EndBatch;
                } else {
                    cursorRecordResult=
                        CursorRecordResult.Skip|
                        CursorRecordResult.EndBatch|
                        CursorRecordResult.RetryLastRecord;
                }
                return true;
            }
            this.Chunk[index]=this.Deserialize(logRecord);
            Volatile.Write(ref this.LastAcceptedAddress,recordMetadata.Address);
            cursorRecordResult=CursorRecordResult.Accept;
            return true;
        }

        public bool OnStart(long beginAddress,long endAddress) {
            Volatile.Write(ref this.Count,0);
            Volatile.Write(ref this.LastAcceptedAddress,0);
            return true;
        }
        public void OnStop(bool completed,long numberOfRecords) { }
        public void OnException(Exception exception,long numberOfRecords) { }
    }

    private sealed class ScanIteratorFunctions_Chunk:ScanIteratorFunctions<int> {
        private protected override int Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord) =>
            DeserializeKey(logRecord.Key);
    }

    private struct Enumerator<TIterator, T>(
        ClientSession<FixedSpanByteKey,int,int,Empty,SimpleUpsertFunctions,TStoreFuncs,TAlloc> Session,
        TIterator Iterator
    ):IEnumerator<T>
        where TIterator : ScanIteratorFunctions<T> {
        private long Cursor = 0, Count;
        private int Index = -1;
        private bool Done;
        public readonly T Current => Iterator.Chunk[this.Index];
        readonly object? IEnumerator.Current => this.Current;
        public bool MoveNext() {
            if(++this.Index<this.Count)
                return true;
            if(this.Done) {
                this.Index=-1;
                return false;
            }
            Iterator.Count=0;
            var まだデータがある = Session.IterateLookup(ref Iterator,ref this.Cursor,resetCursor: false);
            Session.BasicContext.CompletePending(wait: true);
            this.Count=Iterator.Count;
            if(!まだデータがある&&this.Count==0)
                this.Done=true;
            if(this.Count==0) {
                this.Index=-1;
                return false;
            }
            this.Index=0;
            return true;
        }
        public void Reset() {
            this.Cursor=0;
            this.Count=0;
            this.Index=-1;
            this.Done=false;
        }
        public readonly void Dispose() {
        }
    }

    // ----------------------------------------------------------------
    // ストア生成ヘルパー
    // ----------------------------------------------------------------
    private static TsavoriteKV<TStoreFuncs,TAlloc> CreateStore(KVSettings settings) {
        var storeFunctions = new TStoreFuncs(
            new SpanByteComparer(),
            static () => null,
            DefaultRecordTriggers.Instance);
        return new TsavoriteKV<TStoreFuncs,TAlloc>(
            settings,
            storeFunctions,
            static (a,f) => new TAlloc(a,f));
    }

    // セッション型を変数で保持するためのヘルパー (型推論用)
    private static ClientSession<
        FixedSpanByteKey,
        int,
        int,
        Empty,
        SimpleUpsertFunctions,
        TStoreFuncs,
        TAlloc> CreateSession(TsavoriteKV<TStoreFuncs,TAlloc> store) =>
        store.NewSession<
            FixedSpanByteKey,
            int,
            int,
            Empty,
            SimpleUpsertFunctions>(new SimpleUpsertFunctions());

    private static Status CompletePending(
        ClientSession<
            FixedSpanByteKey,
            int,
            int,
            Empty,
            SimpleUpsertFunctions,
            TStoreFuncs,
            TAlloc> session,
        Status status,
        ref int output) {
        if(!status.IsPending)
            return status;
        session.BasicContext.CompletePendingWithOutputs(out var completedOutputs,wait: true);
        using(completedOutputs) {
            if(completedOutputs.Next()) {
                status=completedOutputs.Current.Status;
                output=completedOutputs.Current.Output;
            } else {
            }
        }
        return status;
    }

    private static void UpsertValue(
        ClientSession<
            FixedSpanByteKey,
            int,
            int,
            Empty,
            SimpleUpsertFunctions,
            TStoreFuncs,
            TAlloc> session,
        int keyValue,
        int value) {
        Span<byte> keyBytes = stackalloc byte[sizeof(int)];
        SerializeKey(keyValue,keyBytes);
        var key = (FixedSpanByteKey)(ReadOnlySpan<byte>)keyBytes;
        var input = value;
        var output = default(int);
        var status = session.BasicContext.RMW(key,ref input,ref output);
        status=CompletePending(session,status,ref output);
        Assert.True(status.IsCompleted);
    }

    private static int ReadValue(
        ClientSession<
            FixedSpanByteKey,
            int,
            int,
            Empty,
            SimpleUpsertFunctions,
            TStoreFuncs,
            TAlloc> session,
        int keyValue) {
        Span<byte> keyBytes = stackalloc byte[sizeof(int)];
        SerializeKey(keyValue,keyBytes);
        var key = (FixedSpanByteKey)(ReadOnlySpan<byte>)keyBytes;
        var output = default(int);
        var status = session.BasicContext.Read(key,ref output);
        status=CompletePending(session,status,ref output);
        Assert.True(status.Found,$"Key {keyValue} was not found.");
        return output;
    }

    private static void AssertEqualByKey(IReadOnlyDictionary<int,int> expected,IReadOnlyDictionary<int,int> actual) {
        Assert.Equal(expected.Count,actual.Count);
        Assert.Equal(
            expected.OrderBy(static pair => pair.Key).Select(static pair => pair.Key),
            actual.OrderBy(static pair => pair.Key).Select(static pair => pair.Key));
        Assert.Equal(
            expected.OrderBy(static pair => pair.Key).Select(static pair => pair.Value),
            actual.OrderBy(static pair => pair.Key).Select(static pair => pair.Value));
    }

    // ================================================================
    // テスト1b: データ件数を 1,2,4,8,... と増やして Enumerator 走査を検証
    //
    // 目的:
    //   - Enumerator + ScanIteratorFunctions_Chunk の組み合わせで
    //     ストレージ経路の列挙挙動を件数スケールごとに確認する
    // ================================================================
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(64)]
    [InlineData(128)]
    [InlineData(256)]
    [InlineData(512)]
    [InlineData(1024)]
    [SuppressMessage("Interoperability","CA1416:プラットフォームの互換性",Justification = "<互換性>")]
    public void DiskBacked_Enumerator_PowerOfTwoDataSizes(int totalKeys) {
        var       folder   = Path.Combine(Environment.CurrentDirectory,nameof(this.DiskBacked_Enumerator_PowerOfTwoDataSizes));
        using var logDevice= Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: false,preallocateFile: true);
        using var settings = new KVSettings {
            IndexSize=64,
            PageSize=1L<<12,       // 4 KB
            SegmentSize=1L<<12,    // 4 KB
            LogMemorySize=1L<<15,  // 32 KB: 1024 件ケースでも列挙ロジックのみを検証できる最小側設定
            MutableFraction=0.05,
            LogDevice=logDevice,
        };
        using var store = CreateStore(settings);
        using var session = CreateSession(store);
        var expected = new Dictionary<int,int>(totalKeys);
        for(var i = 0;i<totalKeys;i++) {
            var value = i+1;
            UpsertValue(session,i,value);
            expected.Add(i,value);
        }
        store.Log.ShiftReadOnlyAddress(store.Log.TailAddress,wait: true);
        store.Log.Flush(wait: true);
        var visited = new Dictionary<int,int>(totalKeys);
        using var scanEnumerator = new Enumerator<ScanIteratorFunctions_Chunk,int>(
            session,
            new());
        var total = 0;
        var safetyLimit = totalKeys*4;
        while(scanEnumerator.MoveNext()) {
            total++;
            Assert.True(total<=safetyLimit,
                $"Enumerator did not terminate at totalKeys={totalKeys}. "+
                "Likely cause: Iterator.Count is not reset before IterateLookup, "+
                "so Reader returns EndBatch immediately and stale chunk is replayed.");
            var key = scanEnumerator.Current;
            Assert.True(expected.TryGetValue(key,out var expectedValue),$"Unexpected key {key}.");
            var actualValue = ReadValue(session,key);
            Assert.Equal(expectedValue,actualValue);
            Assert.True(visited.TryAdd(key,actualValue),$"Duplicate key {key} was enumerated.");
        }

        Assert.Equal(totalKeys,total);
        AssertEqualByKey(expected,visited);

        store.Log.FlushAndEvict(wait: true);
    }

    // ================================================================
    // テスト3: ディスクスピル ─ Chunk に蓄積したキーを別セッションで Upsert
    //          Pending 処理パターンが適切に実行されることを確認する。
    //
    // 設計
    //   - scanSession で IterateLookup → Chunk にキーを蓄積 (Reader<TSourceLogRecord>)
    //   - バッチ後に Chunk から key を取り出し、upsertSession で value=1 に上書き
    //   - UpsertChunk パターン:
    //       key を stackalloc シリアライズ → RMW → Pending をその場で解消
    //     で key/value の責務を分離する。
    // ================================================================
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(64)]
    [InlineData(128)]
    [InlineData(256)]
    [InlineData(512)]
    [InlineData(1024)]
    [InlineData(2048)]
    [SuppressMessage("Interoperability","CA1416:プラットフォームの互換性",Justification = "<互換性>")]
    public void DiskSpill_UpsertFromChunk_PendingHandled(int Count) {
        var       folder   = Path.Combine(Environment.CurrentDirectory,nameof(this.DiskSpill_UpsertFromChunk_PendingHandled));
        using var logDevice= Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: false,preallocateFile: true);
        using var settings = new KVSettings {
            IndexSize=64,
            PageSize=1L<<12,    // 4 KB
            SegmentSize=1L<<22,
            LogMemorySize=1L<<22,   // 4 MB
            MutableFraction=1.0,
            LogDevice=logDevice,
        };
        using var store = CreateStore(settings);
        using var scanSession = CreateSession(store);
        using var upsertSession = CreateSession(store);
        var expected = new Dictionary<int,int>(Count);
        for(var i = 0;i<Count;i++) {
            var value = i+1;
            UpsertValue(scanSession,i,value);
            expected.Add(i,value);
        }
        // 全レコードを ReadOnly 領域へ移動
        store.Log.ShiftReadOnlyAddress(store.Log.TailAddress,wait: true);
        var totalUpserted = 0;
        var total = 0;
        var visited = new Dictionary<int,int>(Count);
        using var scanEnumerator = new Enumerator<ScanIteratorFunctions_Chunk,int>(
            scanSession,
            new());
        while(scanEnumerator.MoveNext()) {
            total++;
            var key = scanEnumerator.Current;
            Assert.True(expected.TryGetValue(key,out var expectedValue),$"Unexpected key {key}.");
            var actualValue = ReadValue(upsertSession,key);
            Assert.Equal(expectedValue,actualValue);
            Assert.True(visited.TryAdd(key,actualValue),$"Duplicate key {key} was enumerated.");
            totalUpserted++;
        }
        output.WriteLine($"DiskSpill+Upsert Count={Count} total={total} upserted={totalUpserted}");
        Assert.Equal(Count,total);
        Assert.Equal(Count,totalUpserted);
        AssertEqualByKey(expected,visited);
        store.Log.FlushAndEvict(wait: true);
    }
}
