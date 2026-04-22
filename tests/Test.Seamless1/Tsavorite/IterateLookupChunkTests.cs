using System.Diagnostics.CodeAnalysis;
using Seamless.Collections;
using Tsavorite.core;

// ファイルスコープ型エイリアス (Tsavorite 1.1.1)
using StoreFunc = Tsavorite.core.StoreFunctions<
    int,
    int,
    Seamless.Collections.UnmanagedKeyComparer<int>,
    Tsavorite.core.DefaultRecordDisposer<int, int>>;
using OA = Tsavorite.core.BlittableAllocator<
    int,
    int,
    Tsavorite.core.StoreFunctions<
        int,
        int,
        Seamless.Collections.UnmanagedKeyComparer<int>,
        Tsavorite.core.DefaultRecordDisposer<int, int>>>;

namespace Test.Seamless.TsavoriteLimits;

/// <summary>
/// IterateLookup で Chunk 配列にレコードを蓄積し、バッチ単位で後処理するパターン。
/// Tsavorite 1.1.1 API のみで検証するテスト。
///
/// <para>設計方針</para>
/// <list type="bullet">
///   <item>固定長 int/int をそのまま格納する TsavoriteKV を使い、ChunkScanFunctions でも
///   key/value をそのまま Entry に積む。</item>
///   <item>IterateLookup 終了後に Chunk を逐次取得し処理する。Reader 側に副作用を持ち込まない。</item>
///   <item>各バッチ後に CompletePending(wait: true) で Pending を完結させる。</item>
///   <item>Tsavorite 1.1.1: IterateLookup は ref TScanFunctions で受け取るため
///   struct のままボクシングなしで Count が正しく更新される。</item>
///   <item>このサンプルの key/value は int 固定長なので、SpanByte 可変長ログは使わない。
///   呼び出し側のシリアライズや pin 管理も不要になる。</item>
/// </list>
/// </summary>
[Collection("TsavoriteTests")]
public class IterateLookupChunkTests(ITestOutputHelper output)
{
    private const int ChunkSize = 10;   // 1 バッチで処理するレコード数
    private const int TotalKeys = 25;   // 挿入するキー総数
    private readonly record struct Entry(int Key, int Value);

    // ----------------------------------------------------------------
    // セッション関数: RMW で int 値を固定長で保存する
    // ----------------------------------------------------------------
    private struct SimpleUpsertFunctions : ISessionFunctions<int, int, int, int, Empty>
    {
        // ---- Read ----
        public bool SingleReader(ref int key, ref int input, ref int value, ref int dst, ref ReadInfo readInfo)
        {
            dst = value;
            return true;
        }
        public bool ConcurrentReader(ref int key, ref int input, ref int value, ref int dst, ref ReadInfo readInfo, ref RecordInfo recordInfo)
        {
            dst = value;
            return true;
        }
        public void ReadCompletionCallback(ref int key, ref int input, ref int output, Empty ctx, Status status, RecordMetadata recordMetadata)
        {
            if (!status.Found)
            {
                output = default;
            }
        }

        // ---- Upsert ----
        public int GetUpsertValueLength(ref int value, ref int input) => sizeof(int);
        public bool SingleWriter(ref int key, ref int input, ref int src, ref int dst,
            ref int output, ref UpsertInfo upsertInfo, WriteReason reason, ref RecordInfo recordInfo)
        {
            dst = src;
            output = src;
            return true;
        }
        public void PostSingleWriter(ref int key, ref int input, ref int src, ref int dst,
            ref int output, ref UpsertInfo upsertInfo, WriteReason reason)
        { }
        public bool ConcurrentWriter(ref int key, ref int input, ref int src, ref int dst,
            ref int output, ref UpsertInfo upsertInfo, ref RecordInfo recordInfo)
        {
            dst = src;
            output = src;
            return true;
        }
        void ISessionFunctions<int, int, int, int, Empty>.PostUpsertOperation<TEpochAccessor>(
            ref int key, ref int input, ref int src, ref UpsertInfo upsertInfo, TEpochAccessor epoch)
        { }
        public void ConvertOutputToHeap(ref int input, ref int output) { }

        // ---- RMW ----
        bool ISessionFunctions<int, int, int, int, Empty>.NeedInitialUpdate(ref int key, ref int input, ref int output, ref RMWInfo rmwInfo) => true;
        bool ISessionFunctions<int, int, int, int, Empty>.InitialUpdater(ref int key, ref int input, ref int value, ref int output, ref RMWInfo rmwInfo, ref RecordInfo recordInfo)
        {
            value = input;
            return true;
        }
        void ISessionFunctions<int, int, int, int, Empty>.PostInitialUpdater(ref int key, ref int input, ref int value, ref int output, ref RMWInfo rmwInfo)
        {
            output = input;
        }
        bool ISessionFunctions<int, int, int, int, Empty>.NeedCopyUpdate(ref int key, ref int input, ref int value, ref int output, ref RMWInfo rmwInfo) => false;
        bool ISessionFunctions<int, int, int, int, Empty>.CopyUpdater(ref int key, ref int input, ref int oldValue, ref int newValue, ref int output, ref RMWInfo rmwInfo, ref RecordInfo recordInfo)
        {
            newValue = input;
            output = input;
            return true;
        }
        bool ISessionFunctions<int, int, int, int, Empty>.PostCopyUpdater(ref int key, ref int input, ref int oldValue, ref int newValue, ref int output, ref RMWInfo rmwInfo)
        {
            output = input;
            return true;
        }
        bool ISessionFunctions<int, int, int, int, Empty>.InPlaceUpdater(ref int key, ref int input, ref int value, ref int output, ref RMWInfo rmwInfo, ref RecordInfo recordInfo)
        {
            value = input;
            output = input;
            return true;
        }
        int ISessionFunctions<int, int, int, int, Empty>.GetRMWModifiedValueLength(ref int value, ref int input) => sizeof(int);
        int ISessionFunctions<int, int, int, int, Empty>.GetRMWInitialValueLength(ref int input) => sizeof(int);
        void ISessionFunctions<int, int, int, int, Empty>.PostRMWOperation<TEpochAccessor>(ref int key, ref int input, ref RMWInfo rmwInfo, TEpochAccessor epoch) { }
        void ISessionFunctions<int, int, int, int, Empty>.RMWCompletionCallback(ref int key, ref int input, ref int output, Empty ctx, Status status, RecordMetadata recordMetadata) { }

        // ---- Delete (使用しない) ----
        bool ISessionFunctions<int, int, int, int, Empty>.SingleDeleter(ref int key, ref int value, ref DeleteInfo deleteInfo, ref RecordInfo recordInfo) => throw new NotSupportedException();
        void ISessionFunctions<int, int, int, int, Empty>.PostSingleDeleter(ref int key, ref DeleteInfo deleteInfo) => throw new NotSupportedException();
        bool ISessionFunctions<int, int, int, int, Empty>.ConcurrentDeleter(ref int key, ref int value, ref DeleteInfo deleteInfo, ref RecordInfo recordInfo) => throw new NotSupportedException();
        void ISessionFunctions<int, int, int, int, Empty>.PostDeleteOperation<TEpochAccessor>(ref int key, ref DeleteInfo deleteInfo, TEpochAccessor epoch) => throw new NotSupportedException();
    }

    // ----------------------------------------------------------------
    // IScanIteratorFunctions: key+value をそのまま Entry に蓄積する
    //
    // Seamless1 の ScanIteratorFunctions と同じ構造:
    //   public readonly T[] Chunk = new T[Const.ChunkLength];
    //   public int Count;
    //   SingleReader / ConcurrentReader でデータを Chunk に格納
    //
    // Tsavorite 1.1.1 では IterateLookup は ref TScanFunctions で受け取るため
    // struct のままボクシングなしで Count が正しく更新される。
    // (ただし Tsavorite 内部でインターフェースキャストが生じる場合は class にすること)
    //
    // SingleReader  = ReadOnly またはディスク領域のレコード
    // ConcurrentReader = Mutable 領域のレコード
    // ----------------------------------------------------------------
    private class ScanIteratorFunctions_Chunk : IScanIteratorFunctions<int, int>
    {
        public readonly Entry[] Chunk;
        public int Count;

        public ScanIteratorFunctions_Chunk(int chunkSize)
        {
            this.Chunk = new Entry[chunkSize];
            this.Count = 0;
        }

        public bool SingleReader(ref int key, ref int value, RecordMetadata recordMetadata,
            long numberOfRecords, out CursorRecordResult cursorRecordResult)
        {
            // Chunk.Length == ChunkSize で上限管理。remaining は Count と冗長なので不要。
            if (this.Count >= this.Chunk.Length)
            {
                cursorRecordResult = CursorRecordResult.Skip | CursorRecordResult.EndBatch | CursorRecordResult.RetryLastRecord;
                return true;
            }
            this.Chunk[this.Count++] = new Entry(key, value);
            cursorRecordResult = CursorRecordResult.Accept;
            return true;
        }

        // Mutable 領域のレコードは ConcurrentReader で処理する (内容は SingleReader と同じ)
        public bool ConcurrentReader(ref int key, ref int value, RecordMetadata recordMetadata,
            long numberOfRecords, out CursorRecordResult cursorRecordResult)
            =>
                this.SingleReader(ref key, ref value, recordMetadata, numberOfRecords, out cursorRecordResult);

        public bool OnStart(long beginAddress, long endAddress) => true;
        public void OnStop(bool completed, long numberOfRecords) { }
        public void OnException(Exception exception, long numberOfRecords) { }
    }

    // ----------------------------------------------------------------
    // ストア / セッション生成ヘルパー
    // ----------------------------------------------------------------
    private static TsavoriteKV<int, int, StoreFunc, OA> CreateStore(KVSettings<int, int> settings)
    {
        var sf = new StoreFunc(
            new UnmanagedKeyComparer<int>(),
            () => null,
            () => null,
            DefaultRecordDisposer<int, int>.Instance);
        return new TsavoriteKV<int, int, StoreFunc, OA>(
            settings, sf,
            static (a, f) => new OA(a, f));
    }

    private static ClientSession<int, int, int, int, Empty, SimpleUpsertFunctions, StoreFunc, OA>
        CreateSession(TsavoriteKV<int, int, StoreFunc, OA> store) =>
        store.NewSession<int, int, Empty, SimpleUpsertFunctions>(new SimpleUpsertFunctions());

    private static Status CompletePending(
        ClientSession<int, int, int, int, Empty, SimpleUpsertFunctions, StoreFunc, OA> session,
        Status status,
        ref int output)
    {
        if (!status.IsPending)
        {
            return status;
        }
        session.BasicContext.CompletePendingWithOutputs(out var completedOutputs, wait: true);
        using (completedOutputs)
        {
            if (!completedOutputs.Next())
            {
                return status;
            }
            status = completedOutputs.Current.Status;
            output = completedOutputs.Current.Output;
        }
        return status;
    }

    private static void UpsertValue(
        ClientSession<int, int, int, int, Empty, SimpleUpsertFunctions, StoreFunc, OA> session,
        int keyValue,
        int value)
    {
        var key = keyValue;
        var input = value;
        var output = default(int);
        var status = session.BasicContext.RMW(ref key, ref input, ref output);
        status = CompletePending(session, status, ref output);
        Assert.True(status.IsCompleted);
    }


    // ----------------------------------------------------------------
    // IterateLookup チャンクループ本体
    //   戻り値: 訪問できた総レコード数
    //
    // IterateLookup 戻り値の意味 (Tsavorite 1.1.1):
    //   true  = EndBatch 打ち切り (cursor 更新済み、続きあり)
    //   false = スキャン自然終了 (untilAddress 到達)
    //
    // store.Log.TailAddress を呼び出し時に一度だけキャプチャして untilAddress に渡す。
    // スキャン中に Upsert でログが伸びても元の範囲のみスキャンできる。
    // ----------------------------------------------------------------
    private int RunChunkedIterateLookup(
        TsavoriteKV<int, int, StoreFunc, OA> store,
        ClientSession<int, int, int, int, Empty, SimpleUpsertFunctions, StoreFunc, OA> session,
        Action<Entry[], int>? chunkProcessor = null)
    {
        var scanUntil = store.Log.TailAddress;   // スキャン範囲の上限をキャプチャ
        var cursor = 0L;
        var totalVisited = 0;
        var batchIndex = 0;

        while (true)
        {
            var funcs = new ScanIteratorFunctions_Chunk(ChunkSize);
            var completed = session.IterateLookup(ref funcs, ref cursor, scanUntil, resetCursor: false);
            // Pending の問題を防ぐため: IterateLookup がディスク I/O を発行した場合、
            // ここで Pending を完結させないと次バッチで不正なコールバックが発生しうる。
            session.BasicContext.CompletePending(wait: true);

            totalVisited += funcs.Count;
            batchIndex++;
            output.WriteLine($"batch={batchIndex} count={funcs.Count} cursor={cursor} completed={completed}");

            if (funcs.Count > 0)
            {
                chunkProcessor?.Invoke(funcs.Chunk, funcs.Count);
            }

            // Tsavorite 1.1.1 の IterateLookup 戻り値:
            //   true  = EndBatch 打ち切り → ループ継続 (cursor は次バッチ先頭)
            //   false = スキャン自然終了 (untilAddress 到達) → ループ終了
            if (funcs.Count == 0) break;    // 安全網: 空バッチ
            if (!completed) break;          // 自然終了
        }
        return totalVisited;
    }

    // ================================================================
    // テスト1: オンメモリ ─ 全 25 件をチャンク 10 件で走査
    // MutableFraction=1.0 なので ConcurrentReader コールバックが呼ばれる。
    // Chunk から key をデシリアライズして全件が取得できることを確認する。
    // ================================================================
    [Fact]
    public void InMemory_Chunk10_VisitsAll25Records()
    {
        var folder = Path.Combine(Path.GetTempPath(), "IterateLookupInMem1_" + Guid.NewGuid().ToString("N"));
        using var logDevice = Devices.CreateLogDevice(Path.Combine(folder, "log.data"), deleteOnClose: true);

        using var settings = new KVSettings<int, int>
        {
            IndexSize = 1L << 16,   // 65536 bytes = 1024 hash buckets (衝突なし)
            PageSize = 1L << 12,   // 4 KB
            SegmentSize = 1L << 20,
            MemorySize = 1L << 20,   // 1 MB
            MutableFraction = 1.0,
            LogDevice = logDevice,
        };
        using var store = CreateStore(settings);
        using var session = CreateSession(store);

        // key/value とも固定長なので、呼び出し側のシリアライズは不要。
        for (var i = 0; i < TotalKeys; i++)
        {
            UpsertValue(session, i, i * 100);
        }

        var visitedKeys = new List<int>();
        var visitedValues = new List<int>();
        void CollectKeys(Entry[] chunk, int count)
        {
            for (var ci = 0; ci < count; ci++)
            {
                visitedKeys.Add(chunk[ci].Key);
                visitedValues.Add(chunk[ci].Value);
            }
        }

        var total = this.RunChunkedIterateLookup(store, session, CollectKeys);

        Assert.Equal(TotalKeys, total);
        // 全 25 件のキー (0..24) が過不足なく取得できていることを確認する。
        Assert.Equal(TotalKeys, visitedKeys.Distinct().Count());
        Assert.Equal(visitedKeys.OrderBy(static key => key).Select(static key => key * 100), visitedValues.OrderBy(static value => value));

        store.Log.FlushAndEvict(wait: true);
    }

    // ================================================================
    // テスト2: ディスクスピル ─ ReadOnly 移動後の全 25 件をチャンク 10 件で走査
    //
    // ShiftReadOnlyAddress でレコードを ReadOnly に移動してから IterateLookup を呼ぶ。
    // SingleReader コールバック (ReadOnly 領域) が呼ばれ、Chunk 配列に蓄積される。
    // CompletePending を正しい位置で呼ぶ Pending 処理パターンを示す。
    // ================================================================
    [Fact]
    [SuppressMessage("Interoperability", "CA1416:プラットフォームの互換性", Justification = "<互換性>")]
    public void DiskSpill_Chunk10_VisitsAll25Records_PendingPattern()
    {
        var folder = Path.Combine(
            Path.GetTempPath(),
            "IterateLookupTest1_" + Guid.NewGuid().ToString("N"));

        using var logDevice = Devices.CreateLogDevice(
            Path.Combine(folder, "log.data"), deleteOnClose: true);

        using var settings = new KVSettings<int, int>
        {
            IndexSize = 64,
            PageSize = 1L << 12,   // 4 KB
            SegmentSize = 1L << 22,
            MemorySize = 1L << 22,   // 4 MB
            MutableFraction = 1.0,
            LogDevice = logDevice,
        };
        using var store = CreateStore(settings);
        using var session = CreateSession(store);

        for (var i = 0; i < TotalKeys; i++)
        {
            UpsertValue(session, i, i * 100);
        }
        // Note: FlushAndEvict は HeadAddress=TailAddress にしてしまい、
        //       IterateLookup がスキャン対象を見つけられなくなるため使わない
        store.Log.ShiftReadOnlyAddress(store.Log.TailAddress, wait: true);

        var visitedKeys = new List<int>();
        var visitedValues = new List<int>();
        void CollectKeys(Entry[] chunk, int count)
        {
            for (var ci = 0; ci < count; ci++)
            {
                visitedKeys.Add(chunk[ci].Key);
                visitedValues.Add(chunk[ci].Value);
            }
        }

        var total = this.RunChunkedIterateLookup(store, session, CollectKeys);
        output.WriteLine($"DiskSpill total={total} distinct={visitedKeys.Distinct().Count()}");

        Assert.Equal(TotalKeys, total);
        Assert.Equal(TotalKeys, visitedKeys.Distinct().Count());
        Assert.Equal(visitedKeys.OrderBy(static key => key).Select(static key => key * 100), visitedValues.OrderBy(static value => value));

        store.Log.FlushAndEvict(wait: true);
    }

    // ================================================================
    // テスト3: ディスクスピル ─ Chunk に蓄積したキーを別セッションで Upsert
    //          Pending 処理パターンが適切に実行されることを確認する。
    //
    // 設計
    //   - scanSession で IterateLookup → Chunk に Entry を蓄積 (SingleReader)
    //   - バッチ後に Chunk から key を取り出し、upsertSession で value=1 に上書き
    //   - UpsertChunk パターン:
    //       fixed-length key/value のまま RMW → Pending をその場で解消
    //     のシンプルな実装を確認する。
    // ================================================================
    [Fact]
    [SuppressMessage("Interoperability", "CA1416:プラットフォームの互換性", Justification = "<互換性>")]
    public void DiskSpill_UpsertFromChunk_PendingHandled()
    {
        var folder = Path.Combine(
            Path.GetTempPath(),
            "IterateLookupUpsert1_" + Guid.NewGuid().ToString("N"));

        using var logDevice = Devices.CreateLogDevice(
            Path.Combine(folder, "log.data"), deleteOnClose: true);

        using var settings = new KVSettings<int, int>
        {
            IndexSize = 64,
            PageSize = 1L << 12,   // 4 KB
            SegmentSize = 1L << 22,
            MemorySize = 1L << 22,   // 4 MB
            MutableFraction = 1.0,
            LogDevice = logDevice,
        };
        using var store = CreateStore(settings);
        using var scanSession = CreateSession(store);
        using var upsertSession = CreateSession(store);

        // 初期値 0 で 25 件挿入
        for (var i = 0; i < TotalKeys; i++)
        {
            UpsertValue(scanSession, i, 0);
        }

        // 全レコードを ReadOnly 領域へ移動
        store.Log.ShiftReadOnlyAddress(store.Log.TailAddress, wait: true);

        var totalUpserted = 0;

        // バッチ処理:
        //   1. IterateLookup で Chunk に Entry を蓄積 (SingleReader)
        //   2. CompletePending で IterateLookup の Pending を完結
        //   3. 各キーをそのまま RMW し、Pending をその場で解消
        // RunChunkedIterateLookup が store.Log.TailAddress をキャプチャするため、
        // Upsert でログが伸びても初期 TailAddress までのスキャンに限定される。
        var total = this.RunChunkedIterateLookup(store, scanSession, (chunk, count) =>
        {
            for (var ci = 0; ci < count; ci++)
            {
                UpsertValue(upsertSession, chunk[ci].Key, 1);
            }
            totalUpserted += count;
        });

        output.WriteLine($"DiskSpill+Upsert total={total} upserted={totalUpserted}");
        Assert.Equal(TotalKeys, total);
        Assert.Equal(TotalKeys, totalUpserted);

        store.Log.FlushAndEvict(wait: true);
    }
}
