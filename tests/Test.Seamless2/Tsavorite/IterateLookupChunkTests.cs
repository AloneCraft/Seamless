using System.Buffers.Binary;
using Garnet.common;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Tsavorite.core;

// ファイルスコープ型エイリアス
using TStoreFuncs = Tsavorite.core.StoreFunctions<
    Tsavorite.core.SpanByteComparer,
    Tsavorite.core.DefaultRecordDisposer>;
using TAlloc = Tsavorite.core.SpanByteAllocator<
    Tsavorite.core.StoreFunctions<
        Tsavorite.core.SpanByteComparer,
        Tsavorite.core.DefaultRecordDisposer>>;

namespace Test.Seamless.TsavoriteLimits;

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
    private readonly record struct Entry(int Key,int Value);

    private static void SerializeKey(int key,Span<byte> destination) =>
        BinaryPrimitives.WriteInt32LittleEndian(destination,key);

    private static int DeserializeKey(ReadOnlySpan<byte> source) =>
        BinaryPrimitives.ReadInt32LittleEndian(source);

    private static ReadOnlySpan<byte> SerializeValue(ref int value) =>
        MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref value,1));

    private static int DeserializeValue(ReadOnlySpan<byte> source) =>
        BinaryPrimitives.ReadInt32LittleEndian(source);

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
            where TKey : IKey, allows ref struct => true;

        public bool InitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref int input,ref int output,ref RMWInfo rmwInfo) {
            output=input;
            return dstLogRecord.TrySetValueSpanAndPrepareOptionals(SerializeValue(ref input),in sizeInfo);
        }

        public void PostInitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref int input,ref int output,ref RMWInfo rmwInfo) {
            output=input;
        }

        public bool InPlaceUpdater(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref int input,ref int output,ref RMWInfo rmwInfo) {
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
            where TKey : IKey, allows ref struct
            where TEpochAccessor : IEpochAccessor {
        }

        public void RMWCompletionCallback(ref DiskLogRecord diskLogRecord,ref int input,ref int output,Empty ctx,Status status,RecordMetadata recordMetadata) {
        }

        public RecordFieldInfo GetRMWModifiedFieldInfo<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref int input)
            where TSourceLogRecord : ISourceLogRecord {
            return new() { KeySize=srcLogRecord.Key.Length,ValueSize=Unsafe.SizeOf<int>() };
        }

        public RecordFieldInfo GetRMWInitialFieldInfo<TKey>(TKey key,ref int input)
            where TKey : IKey, allows ref struct {
            return new() { KeySize=key.KeyBytes.Length,ValueSize=Unsafe.SizeOf<int>() };
        }

        public bool InitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref int input,ReadOnlySpan<byte> srcValue,ref int output,ref UpsertInfo upsertInfo) =>
            ThrowRmwOnlyEntryPoint<bool>();

        public bool InitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref int input,IHeapObject srcValue,ref int output,ref UpsertInfo upsertInfo) =>
            ThrowRmwOnlyEntryPoint<bool>();

        public bool InitialWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref int input,in TSourceLogRecord inputLogRecord,ref int output,ref UpsertInfo upsertInfo)
            where TSourceLogRecord : ISourceLogRecord => ThrowRmwOnlyEntryPoint<bool>();

        public bool InPlaceWriter(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref int input,ReadOnlySpan<byte> srcValue,ref int output,ref UpsertInfo upsertInfo) =>
            ThrowRmwOnlyEntryPoint<bool>();

        public bool InPlaceWriter(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref int input,IHeapObject srcValue,ref int output,ref UpsertInfo upsertInfo) =>
            ThrowRmwOnlyEntryPoint<bool>();

        public bool InPlaceWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref int input,in TSourceLogRecord inputLogRecord,ref int output,ref UpsertInfo upsertInfo)
            where TSourceLogRecord : ISourceLogRecord => ThrowRmwOnlyEntryPoint<bool>();

        public void PostUpsertOperation<TKey, TEpochAccessor>(TKey key,ref int input,ReadOnlySpan<byte> valueSpan,ref UpsertInfo upsertInfo,TEpochAccessor epochAccessor)
            where TKey : IKey, allows ref struct
            where TEpochAccessor : IEpochAccessor {
            ThrowRmwOnlyEntryPoint();
        }

        public void PostUpsertOperation<TKey, TEpochAccessor>(TKey key,ref int input,IHeapObject valueObject,ref UpsertInfo upsertInfo,TEpochAccessor epochAccessor)
            where TKey : IKey, allows ref struct
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
            where TKey : IKey, allows ref struct
            where TEpochAccessor : IEpochAccessor => ThrowRmwOnlyEntryPoint();

        public RecordFieldInfo GetUpsertFieldInfo<TKey>(TKey key,ReadOnlySpan<byte> value,ref int input)
            where TKey : IKey, allows ref struct {
            return new() { KeySize=key.KeyBytes.Length,ValueSize=value.Length };
        }

        public RecordFieldInfo GetUpsertFieldInfo<TKey>(TKey key,IHeapObject value,ref int input)
            where TKey : IKey, allows ref struct {
            return new() { KeySize=key.KeyBytes.Length,ValueSize=Unsafe.SizeOf<int>(),ValueIsObject=true };
        }

        public RecordFieldInfo GetUpsertFieldInfo<TKey, TSourceLogRecord>(TKey key,in TSourceLogRecord inputLogRecord,ref int input)
            where TKey : IKey, allows ref struct
            where TSourceLogRecord : ISourceLogRecord {
            return new() { KeySize=key.KeyBytes.Length,ValueSize=inputLogRecord.ValueSpan.Length,ValueIsObject=inputLogRecord.Info.ValueIsObject };
        }

        public void ConvertOutputToHeap(ref int input,ref int output) {
        }
    }

    // ----------------------------------------------------------------
    // IScanIteratorFunctions: ChunkSize 件ごとにバッチを打ち切り、Entry を格納
    //
    // Tsavorite 2.0 では IterateLookup は ref TScanFunctions で受け取り、
    // Reader<TSourceLogRecord> 汎用メソッドで各レコードを処理する。
    // Count は ref struct 経由で正しく更新される。
    //
    // ISourceLogRecord から Key / ValueSpan を取得できる。
    // 各バッチ (ChunkSize 件) 後に EndBatch を返して IterateLookup を中断できる。
    // ----------------------------------------------------------------
    private sealed class ChunkScanFunctions:IScanIteratorFunctions {
        public readonly Entry[] Chunk;
        public int Count;

        public ChunkScanFunctions(int chunkSize) {
            this.Chunk=new Entry[chunkSize];
            this.Count=0;
        }

        public bool Reader<TSourceLogRecord>(
            in TSourceLogRecord logRecord,
            RecordMetadata recordMetadata,
            long numberOfRecords,
            out CursorRecordResult cursorRecordResult)
            where TSourceLogRecord : ISourceLogRecord {
            // Chunk.Length == ChunkSize で上限管理。remaining は Count と冗長なので不要。
            if(this.Count>=this.Chunk.Length) {
                // チャンク上限到達。cursor をこのレコードの直前に巻き戻して終了。
                // RetryLastRecord により次回の IterateLookup が同じレコードから再開する。
                cursorRecordResult=
                    CursorRecordResult.Skip|
                    CursorRecordResult.EndBatch|
                    CursorRecordResult.RetryLastRecord;
                return true;
            }
            // Pending の問題を防ぐため: Key / ValueSpan はコールバック内でのみ有効。
            // Span を保持せず、ここで型へデシリアライズして値コピーしておく。
            this.Chunk[this.Count++]=new Entry(
                DeserializeKey(logRecord.Key),
                DeserializeValue(logRecord.ValueSpan));
            cursorRecordResult=CursorRecordResult.Accept;
            return true;
        }

        public bool OnStart(long beginAddress,long endAddress) => true;
        public void OnStop(bool completed,long numberOfRecords) { }
        public void OnException(Exception exception,long numberOfRecords) { }
    }

    // ----------------------------------------------------------------
    // ストア生成ヘルパー
    // ----------------------------------------------------------------
    private static TsavoriteKV<TStoreFuncs,TAlloc> CreateStore(KVSettings settings) {
        var storeFunctions = new TStoreFuncs(
            new SpanByteComparer(),
            static () => null,
            new DefaultRecordDisposer());
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
        if(!status.IsPending) {
            return status;
        }
        session.BasicContext.CompletePendingWithOutputs(out var completedOutputs,wait: true);
        using(completedOutputs) {
            if(completedOutputs.Next()) {
                status=completedOutputs.Current.Status;
                output=completedOutputs.Current.Output;
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

    // ----------------------------------------------------------------
    // IterateLookup チャンクループ本体
    //   戻り値: 訪問できた総レコード数
    //
    // IterateLookup 戻り値の意味 (Tsavorite 2.0):
    //   false = スキャン自然終了 (scanUntil 到達) → ループ終了
    //   true  = EndBatch 打ち切り → ループ継続 (cursor は次バッチ先頭)
    //
    // store.Log.TailAddress を呼び出し時に一度だけキャプチャして scanUntil に渡す。
    // スキャン中に Upsert でログが伸びても元の範囲のみスキャンできる。
    // ----------------------------------------------------------------
    private int RunChunkedIterateLookup(
        TsavoriteKV<TStoreFuncs,TAlloc> store,
        ClientSession<
            FixedSpanByteKey,
            int,
            int,
            Empty,
            SimpleUpsertFunctions,
            TStoreFuncs,
            TAlloc> session,
        Action<Entry[],int>? chunkProcessor = null) {
        var scanUntil = store.Log.TailAddress;   // スキャン範囲の上限をキャプチャ
        var cursor = 0L;
        var totalVisited = 0;
        var batchIndex = 0;

        while(true) {
            var funcs = new ChunkScanFunctions(ChunkSize);
            var completed = session.IterateLookup(ref funcs,ref cursor,scanUntil,resetCursor: false);
            var pendingCompleted = session.BasicContext.CompletePending(wait: true);
            Assert.True(pendingCompleted);

            totalVisited+=funcs.Count;
            batchIndex++;
            output.WriteLine($"batch={batchIndex} count={funcs.Count} cursor={cursor} completed={completed} pendingCompleted={pendingCompleted}");

            if(funcs.Count>0) {
                chunkProcessor?.Invoke(funcs.Chunk,funcs.Count);
            }

            // Tsavorite 2.0 の IterateLookup 戻り値:
            //   true  = EndBatch 打ち切り → ループ継続 (cursor は次バッチ先頭)
            //   false = スキャン自然終了 (scanUntil 到達) → ループ終了
            if(funcs.Count==0) break;    // 安全網: 空バッチ
            if(!completed) break;          // 自然終了
        }
        return totalVisited;
    }

    // ================================================================
    // テスト1: オンメモリ ─ 全 25 件をチャンク 10 件で走査
    // MutableFraction=1.0 なので Reader<TSourceLogRecord> が呼ばれる。
    // Chunk から key をデシリアライズして全件が取得できることを確認する。
    // ================================================================
    [Fact]
    public void InMemory_Chunk10_VisitsAll25Records() {
        var folder = Path.Combine(Path.GetTempPath(),"IterateLookupInMem2_"+Guid.NewGuid().ToString("N"));
        using var logDevice = Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true);

        using var settings = new KVSettings {
            IndexSize=1L<<16,   // 65536 bytes
            PageSize=1L<<12,    // 4 KB
            SegmentSize=1L<<20,
            LogMemorySize=1L<<20,   // 1 MB
            MutableFraction=1.0,
            LogDevice=logDevice,
        };
        using var store = CreateStore(settings);
        using var session = CreateSession(store);

        // キーは呼び出し側で stackalloc シリアライズし、そのヘルパー内で Pending を解消する。
        // 値のシリアライズは SimpleUpsertFunctions 内で行う。
        for(var i = 0; i<TotalKeys; i++) {
            UpsertValue(session,i,i*100);
        }

        var visitedKeys = new List<int>();
        var visitedValues = new List<int>();
        void CollectKeys(Entry[] chunk,int count) {
            for(var ci = 0; ci<count; ci++) {
                visitedKeys.Add(chunk[ci].Key);
                visitedValues.Add(chunk[ci].Value);
            }
        }

        var total = this.RunChunkedIterateLookup(store,session,CollectKeys);

        Assert.Equal(TotalKeys,total);
        // 全 25 件のキー (0..24) が過不足なく取得できていることを確認
        Assert.Equal(TotalKeys,visitedKeys.Distinct().Count());
        Assert.Equal(visitedKeys.OrderBy(static key => key).Select(static key => key*100),visitedValues.OrderBy(static value => value));

        store.Log.FlushAndEvict(wait: true);
    }

    // ================================================================
    // テスト2: ディスクスピル ─ ReadOnly 移動後の全 25 件をチャンク 10 件で走査
    //
    // ShiftReadOnlyAddress でレコードを ReadOnly に移動してから IterateLookup を呼ぶ。
    // Reader<TSourceLogRecord> (ReadOnly 領域) が呼ばれ、Chunk 配列に蓄積される。
    // CompletePending を正しい位置で呼ぶ Pending 処理パターンを示す。
    // ================================================================
    [Fact]
    [SuppressMessage("Interoperability","CA1416:プラットフォームの互換性",Justification = "<互換性>")]
    public void DiskSpill_Chunk10_VisitsAll25Records_PendingPattern() {
        var folder = Path.Combine(
            Path.GetTempPath(),
            "IterateLookupTest2_"+Guid.NewGuid().ToString("N"));

        using var logDevice = Devices.CreateLogDevice(
            Path.Combine(folder,"log.data"),deleteOnClose: true);

        using var settings = new KVSettings {
            IndexSize=64,
            PageSize=1L<<12,    // 4 KB
            SegmentSize=1L<<22,
            LogMemorySize=1L<<22,   // 4 MB
            MutableFraction=1.0,
            LogDevice=logDevice,
        };
        using var store = CreateStore(settings);
        using var session = CreateSession(store);

        for(var i = 0; i<TotalKeys; i++) {
            UpsertValue(session,i,i*100);
        }

        // 全レコードを ReadOnly 領域へ移動 (メモリ上に留まるが ReadOnly)
        // Note: FlushAndEvict は HeadAddress=TailAddress にしてしまい
        //       IterateLookup がスキャン対象を見つけられなくなるため使わない
        store.Log.ShiftReadOnlyAddress(store.Log.TailAddress,wait: true);

        var visitedKeys = new List<int>();
        var visitedValues = new List<int>();
        void CollectKeys(Entry[] chunk,int count) {
            for(var ci = 0; ci<count; ci++) {
                visitedKeys.Add(chunk[ci].Key);
                visitedValues.Add(chunk[ci].Value);
            }
        }

        var total = this.RunChunkedIterateLookup(store,session,CollectKeys);
        output.WriteLine($"DiskSpill total={total} distinct={visitedKeys.Distinct().Count()}");

        Assert.Equal(TotalKeys,total);
        Assert.Equal(TotalKeys,visitedKeys.Distinct().Count());
        Assert.Equal(visitedKeys.OrderBy(static key => key).Select(static key => key*100),visitedValues.OrderBy(static value => value));

        store.Log.FlushAndEvict(wait: true);
    }

    // ================================================================
    // テスト3: ディスクスピル ─ Chunk に蓄積したキーを別セッションで Upsert
    //          Pending 処理パターンが適切に実行されることを確認する。
    //
    // 設計
    //   - scanSession で IterateLookup → Chunk に Entry を蓄積 (Reader<TSourceLogRecord>)
    //   - バッチ後に Chunk から key を取り出し、upsertSession で value=1 に上書き
    //   - UpsertChunk パターン:
    //       key を stackalloc シリアライズ → RMW → Pending をその場で解消
    //     で key/value の責務を分離する。
    // ================================================================
    [Fact]
    [SuppressMessage("Interoperability","CA1416:プラットフォームの互換性",Justification = "<互換性>")]
    public void DiskSpill_UpsertFromChunk_PendingHandled() {
        var folder = Path.Combine(
            Path.GetTempPath(),
            "IterateLookupUpsert2_"+Guid.NewGuid().ToString("N"));

        using var logDevice = Devices.CreateLogDevice(
            Path.Combine(folder,"log.data"),deleteOnClose: true);

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

        // 初期値 0 で 25 件挿入
        for(var i = 0; i<TotalKeys; i++) {
            UpsertValue(scanSession,i,0);
        }

        // 全レコードを ReadOnly 領域へ移動
        store.Log.ShiftReadOnlyAddress(store.Log.TailAddress,wait: true);

        var totalUpserted = 0;

        var total = this.RunChunkedIterateLookup(store,scanSession,(chunk,count) => {
            // Chunk 配列から取り出したキーを別セッションで Upsert する
            for(var ci = 0; ci<count; ci++) {
                UpsertValue(upsertSession,chunk[ci].Key,1);
            }
            totalUpserted+=count;
        });

        output.WriteLine($"DiskSpill+Upsert total={total} upserted={totalUpserted}");
        Assert.Equal(TotalKeys,total);
        Assert.Equal(TotalKeys,totalUpserted);

        store.Log.FlushAndEvict(wait: true);
    }
}
