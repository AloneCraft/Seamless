using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Seamless.Collections;
using Tsavorite.core;

// ファイルスコープ型エイリアス (Tsavorite 1.1.1)
using StoreFunc = Tsavorite.core.StoreFunctions<
    int,
    int,
    Seamless.Collections.UnmanagedKeyComparer<int>,
    Tsavorite.core.DefaultRecordDisposer<int,int>>;
using OA = Tsavorite.core.BlittableAllocator<
    int,
    int,
    Tsavorite.core.StoreFunctions<
        int,
        int,
        Seamless.Collections.UnmanagedKeyComparer<int>,
        Tsavorite.core.DefaultRecordDisposer<int,int>>>;

namespace Test.Seamless.Tsavorite;

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
public class TIterateLookupChunkTests(ITestOutputHelper output) {
    private const int ChunkSize = 10;   // 1 バッチで処理するレコード数
    private const int TotalKeys = 25;   // 挿入するキー総数
    private readonly record struct Entry(int Key,int Value);

    // ----------------------------------------------------------------
    // セッション関数: RMW で int 値を固定長で保存する
    // ----------------------------------------------------------------
    private struct SimpleUpsertFunctions:ISessionFunctions<int,int,int,int,Empty> {
        // ---- Read ----
        public bool SingleReader(ref int key,ref int input,ref int value,ref int dst,ref ReadInfo readInfo) {
            dst=value;
            return true;
        }
        public bool ConcurrentReader(ref int key,ref int input,ref int value,ref int dst,ref ReadInfo readInfo,ref RecordInfo recordInfo) {
            dst=value;
            return true;
        }
        public void ReadCompletionCallback(ref int key,ref int input,ref int output,Empty ctx,Status status,RecordMetadata recordMetadata) {
            if(!status.Found) {
                output=default;
            }
        }

        // ---- Upsert ----
        public int GetUpsertValueLength(ref int value,ref int input) => sizeof(int);
        public bool SingleWriter(ref int key,ref int input,ref int src,ref int dst,
            ref int output,ref UpsertInfo upsertInfo,WriteReason reason,ref RecordInfo recordInfo) {
            dst=src;
            output=src;
            return true;
        }
        public void PostSingleWriter(ref int key,ref int input,ref int src,ref int dst,
            ref int output,ref UpsertInfo upsertInfo,WriteReason reason) { }
        public bool ConcurrentWriter(ref int key,ref int input,ref int src,ref int dst,
            ref int output,ref UpsertInfo upsertInfo,ref RecordInfo recordInfo) {
            dst=src;
            output=src;
            return true;
        }
        void ISessionFunctions<int,int,int,int,Empty>.PostUpsertOperation<TEpochAccessor>(
            ref int key,ref int input,ref int src,ref UpsertInfo upsertInfo,TEpochAccessor epoch) { }
        public void ConvertOutputToHeap(ref int input,ref int output) { }

        // ---- RMW ----
        bool ISessionFunctions<int,int,int,int,Empty>.NeedInitialUpdate(ref int key,ref int input,ref int output,ref RMWInfo rmwInfo) => true;
        bool ISessionFunctions<int,int,int,int,Empty>.InitialUpdater(ref int key,ref int input,ref int value,ref int output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo) {
            value=input;
            return true;
        }
        void ISessionFunctions<int,int,int,int,Empty>.PostInitialUpdater(ref int key,ref int input,ref int value,ref int output,ref RMWInfo rmwInfo) {
            output=input;
        }
        bool ISessionFunctions<int,int,int,int,Empty>.NeedCopyUpdate(ref int key,ref int input,ref int value,ref int output,ref RMWInfo rmwInfo) => false;
        bool ISessionFunctions<int,int,int,int,Empty>.CopyUpdater(ref int key,ref int input,ref int oldValue,ref int newValue,ref int output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo) {
            newValue=input;
            output=input;
            return true;
        }
        bool ISessionFunctions<int,int,int,int,Empty>.PostCopyUpdater(ref int key,ref int input,ref int oldValue,ref int newValue,ref int output,ref RMWInfo rmwInfo) {
            output=input;
            return true;
        }
        bool ISessionFunctions<int,int,int,int,Empty>.InPlaceUpdater(ref int key,ref int input,ref int value,ref int output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo) {
            value=input;
            output=input;
            return true;
        }
        int ISessionFunctions<int,int,int,int,Empty>.GetRMWModifiedValueLength(ref int value,ref int input) => sizeof(int);
        int ISessionFunctions<int,int,int,int,Empty>.GetRMWInitialValueLength(ref int input) => sizeof(int);
        void ISessionFunctions<int,int,int,int,Empty>.PostRMWOperation<TEpochAccessor>(ref int key,ref int input,ref RMWInfo rmwInfo,TEpochAccessor epoch) { }
        void ISessionFunctions<int,int,int,int,Empty>.RMWCompletionCallback(ref int key,ref int input,ref int output,Empty ctx,Status status,RecordMetadata recordMetadata) { }

        // ---- Delete (使用しない) ----
        bool ISessionFunctions<int,int,int,int,Empty>.SingleDeleter(ref int key,ref int value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo) => throw new NotSupportedException();
        void ISessionFunctions<int,int,int,int,Empty>.PostSingleDeleter(ref int key,ref DeleteInfo deleteInfo) => throw new NotSupportedException();
        bool ISessionFunctions<int,int,int,int,Empty>.ConcurrentDeleter(ref int key,ref int value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo) => throw new NotSupportedException();
        void ISessionFunctions<int,int,int,int,Empty>.PostDeleteOperation<TEpochAccessor>(ref int key,ref DeleteInfo deleteInfo,TEpochAccessor epoch) => throw new NotSupportedException();
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
    private sealed class ScanIteratorFunctions_Chunk:IScanIteratorFunctions<int,int> {
        public readonly Entry[] Chunk;
        public int Count;

        public ScanIteratorFunctions_Chunk(int chunkSize) {
            this.Chunk=new Entry[chunkSize];
            this.Count=0;
        }

        public bool SingleReader(ref int key,ref int value,RecordMetadata recordMetadata,
            long numberOfRecords,out CursorRecordResult cursorRecordResult) {
            if(this.Count>=this.Chunk.Length) {
                cursorRecordResult=CursorRecordResult.Skip|CursorRecordResult.EndBatch|CursorRecordResult.RetryLastRecord;
                return true;
            }
            this.Chunk[this.Count++]=new Entry(key,value);
            cursorRecordResult=CursorRecordResult.Accept;
            return true;
        }

        // Mutable 領域のレコードは ConcurrentReader で処理する (内容は SingleReader と同じ)
        public bool ConcurrentReader(ref int key,ref int value,RecordMetadata recordMetadata,
            long numberOfRecords,out CursorRecordResult cursorRecordResult)
            =>
                this.SingleReader(ref key,ref value,recordMetadata,numberOfRecords,out cursorRecordResult);

        public bool OnStart(long beginAddress,long endAddress) => true;
        public void OnStop(bool completed,long numberOfRecords) { }
        public void OnException(Exception exception,long numberOfRecords) { }
    }

    private class KeyScanIteratorFunctions_Chunk:IScanIteratorFunctions<int,int> {
        public readonly int[] Chunk;
        public int Count;
        private long LastAcceptedAddress;

        public KeyScanIteratorFunctions_Chunk(int chunkSize) {
            this.Chunk=new int[chunkSize];
            this.Count=0;
        }

        public bool SingleReader(ref int key,ref int value,RecordMetadata recordMetadata,
            long numberOfRecords,out CursorRecordResult cursorRecordResult) {
            var index = Interlocked.Increment(ref this.Count)-1;
            if(index>=this.Chunk.Length) {
                Volatile.Write(ref this.Count,this.Chunk.Length);
                var lastAcceptedAddress = Volatile.Read(ref this.LastAcceptedAddress);
                if(recordMetadata.Address==lastAcceptedAddress)
                    cursorRecordResult=CursorRecordResult.Skip|CursorRecordResult.EndBatch;
                else
                    cursorRecordResult=CursorRecordResult.Skip|CursorRecordResult.EndBatch|CursorRecordResult.RetryLastRecord;
                return true;
            }
            this.Chunk[index]=key;
            Volatile.Write(ref this.LastAcceptedAddress,recordMetadata.Address);
            cursorRecordResult=CursorRecordResult.Accept;
            return true;
        }

        public bool ConcurrentReader(ref int key,ref int value,RecordMetadata recordMetadata,
            long numberOfRecords,out CursorRecordResult cursorRecordResult)
            =>
                this.SingleReader(ref key,ref value,recordMetadata,numberOfRecords,out cursorRecordResult);

        public bool OnStart(long beginAddress,long endAddress) {
            Volatile.Write(ref this.Count,0);
            Volatile.Write(ref this.LastAcceptedAddress,0);
            return true;
        }

        public void OnStop(bool completed,long numberOfRecords) { }
        public void OnException(Exception exception,long numberOfRecords) { }
    }

    private struct Enumerator(
        ClientSession<int,int,int,int,Empty,SimpleUpsertFunctions,StoreFunc,OA> session,
        KeyScanIteratorFunctions_Chunk iterator
    ):IEnumerator<int> {
        private long Cursor = 0, Count;
        private int Index = -1;
        private bool Done;
        public int PendingCompletionCount;
        public readonly int Current => iterator.Chunk[this.Index];
        readonly object? IEnumerator.Current => this.Current;

        public bool MoveNext() {
            if(++this.Index<this.Count)
                return true;
            if(this.Done) {
                this.Index=-1;
                return false;
            }

            iterator.Count=0;
            var まだデータがある = session.IterateLookup(ref iterator,ref this.Cursor,resetCursor: false);
            if(session.BasicContext.CompletePending(wait: true))
                this.PendingCompletionCount++;
            this.Count=iterator.Count;
            if(this.Count==0) {
                this.Done=true;
                this.Index=-1;
                return false;
            }

            this.Done=!まだデータがある;

            this.Index=0;
            return true;
        }

        public void Reset() {
            this.Cursor=0;
            this.Count=0;
            this.Index=-1;
            this.Done=false;
            this.PendingCompletionCount=0;
        }

        public readonly void Dispose() {
        }
    }
    //private class ChunkScanFunctions:IScanIteratorFunctions<SpanByte,SpanByte> {
    //    public readonly Entry[] Chunk;
    //    public int Count;

    //    public ChunkScanFunctions(int chunkSize) {
    //        this.Chunk=new Entry[chunkSize];
    //        this.Count=0;
    //    }

    //    public bool SingleReader(ref SpanByte key,ref SpanByte value,RecordMetadata recordMetadata,
    //        long numberOfRecords,out CursorRecordResult cursorRecordResult) {
    //        // Chunk.Length == ChunkSize で上限管理。remaining は Count と冗長なので不要。
    //        if(this.Count>=this.Chunk.Length) {
    //            cursorRecordResult=CursorRecordResult.Skip|CursorRecordResult.EndBatch|CursorRecordResult.RetryLastRecord;
    //            return true;
    //        }
    //        // Pending の問題を防ぐため: key/value の SpanByte はコールバック内でのみ有効。
    //        // Span を保持せず、ここで型へデシリアライズして値コピーしておく。
    //        this.Chunk[this.Count++]=new Entry(
    //            DeserializeKey(key.AsReadOnlySpan()),
    //            DeserializeValue(value.AsReadOnlySpan()));
    //        cursorRecordResult=CursorRecordResult.Accept;
    //        return true;
    //    }

    //    // Mutable 領域のレコードは ConcurrentReader で処理する (内容は SingleReader と同じ)
    //    public bool ConcurrentReader(ref SpanByte key,ref SpanByte value,RecordMetadata recordMetadata,
    //        long numberOfRecords,out CursorRecordResult cursorRecordResult)
    //        =>
    //            this.SingleReader(ref key,ref value,recordMetadata,numberOfRecords,out cursorRecordResult);

    //    public bool OnStart(long beginAddress,long endAddress) => true;
    //    public void OnStop(bool completed,long numberOfRecords) { }
    //    public void OnException(Exception exception,long numberOfRecords) { }
    //}


    // ----------------------------------------------------------------
    // ストア / セッション生成ヘルパー
    // ----------------------------------------------------------------
    private static TsavoriteKV<int,int,StoreFunc,OA> CreateStore(KVSettings<int,int> settings) {
        var sf = new StoreFunc(
            new UnmanagedKeyComparer<int>(),
            () => null,
            () => null,
            DefaultRecordDisposer<int,int>.Instance);
        return new TsavoriteKV<int,int,StoreFunc,OA>(
            settings,sf,
            static (a,f) => new OA(a,f));
    }

    private static ClientSession<int,int,int,int,Empty,SimpleUpsertFunctions,StoreFunc,OA>
        CreateSession(TsavoriteKV<int,int,StoreFunc,OA> store) =>
        store.NewSession<int,int,Empty,SimpleUpsertFunctions>(new SimpleUpsertFunctions());

    private static Status CompletePending(
        ClientSession<int,int,int,int,Empty,SimpleUpsertFunctions,StoreFunc,OA> session,
        Status status,
        ref int output) {
        if(!status.IsPending) {
            return status;
        }
        session.BasicContext.CompletePendingWithOutputs(out var completedOutputs,wait: true);
        using(completedOutputs) {
            if(!completedOutputs.Next()) {
                return status;
            }
            status=completedOutputs.Current.Status;
            output=completedOutputs.Current.Output;
        }
        return status;
    }

    private static void UpsertValue(
        ClientSession<int,int,int,int,Empty,SimpleUpsertFunctions,StoreFunc,OA> session,
        int keyValue,
        int value) {
        var key = keyValue;
        var input = value;
        var output = default(int);
        var status = session.BasicContext.RMW(ref key,ref input,ref output);
        status=CompletePending(session,status,ref output);
        Assert.True(status.IsCompleted);
    }

    private static int ReadValue(
        ClientSession<int,int,int,int,Empty,SimpleUpsertFunctions,StoreFunc,OA> session,
        int keyValue) {
        var key = keyValue;
        var output = default(int);
        var status = session.BasicContext.Read(ref key,ref output);
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
        TsavoriteKV<int,int,StoreFunc,OA> store,
        ClientSession<int,int,int,int,Empty,SimpleUpsertFunctions,StoreFunc,OA> session,
        Action<Entry[],int>? chunkProcessor = null) {
        var scanUntil = store.Log.TailAddress;   // スキャン範囲の上限をキャプチャ
        var cursor = 0L;
        var totalVisited = 0;
        var batchIndex = 0;

        while(true) {
            var funcs = new ScanIteratorFunctions_Chunk(ChunkSize);
            var まだデータがある = session.IterateLookup(ref funcs,ref cursor,scanUntil,resetCursor: false);
            // Pending の問題を防ぐため: IterateLookup がディスク I/O を発行した場合、
            // ここで Pending を完結させないと次バッチで不正なコールバックが発生しうる。
            session.BasicContext.CompletePending(wait: true);

            totalVisited+=funcs.Count;
            batchIndex++;
            output.WriteLine($"batch={batchIndex} count={funcs.Count} cursor={cursor} completed={まだデータがある}");

            if(funcs.Count>0) {
                chunkProcessor?.Invoke(funcs.Chunk,funcs.Count);
            }

            // Tsavorite 1.1.1 の IterateLookup 戻り値:
            //   true  = EndBatch 打ち切り → ループ継続 (cursor は次バッチ先頭)
            //   false = スキャン自然終了 (untilAddress 到達) → ループ終了
            if(funcs.Count==0) break;    // 安全網: 空バッチ
            if(!まだデータがある) break;          // 自然終了
        }
        return totalVisited;
    }

    // ================================================================
    // テスト1: オンメモリ ─ 全 25 件をチャンク 10 件で走査
    // MutableFraction=1.0 なので ConcurrentReader コールバックが呼ばれる。
    // Chunk から key をデシリアライズして全件が取得できることを確認する。
    // ================================================================
    [Fact]
    public void InMemory_Chunk10_VisitsAll25Records() {
        var folder = global::Test.Seamless.TestPath.Directory(this);
        using var logDevice = Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true);

        using var settings = new KVSettings<int,int> {
            IndexSize=1L<<16,   // 65536 bytes = 1024 hash buckets (衝突なし)
            PageSize=1L<<12,   // 4 KB
            SegmentSize=1L<<20,
            MemorySize=1L<<20,   // 1 MB
            MutableFraction=1.0,
            LogDevice=logDevice,
        };
        using var store = CreateStore(settings);
        using var session = CreateSession(store);

        // key/value とも固定長なので、呼び出し側のシリアライズは不要。
        for(var i = 0; i<TotalKeys; i++) {
            UpsertValue(session,i,i*100);
        }

        var visitedKeys = new global::System.Collections.Generic.List<int>();
        var visitedValues = new global::System.Collections.Generic.List<int>();
        void CollectKeys(Entry[] chunk,int count) {
            for(var ci = 0; ci<count; ci++) {
                visitedKeys.Add(chunk[ci].Key);
                visitedValues.Add(chunk[ci].Value);
            }
        }

        var total = this.RunChunkedIterateLookup(store,session,CollectKeys);

        Assert.Equal(TotalKeys,total);
        // 全 25 件のキー (0..24) が過不足なく取得できていることを確認する。
        Assert.Equal(TotalKeys,visitedKeys.Distinct().Count());
        Assert.Equal(visitedKeys.OrderBy(static key => key).Select(static key => key*100),visitedValues.OrderBy(static value => value));

        store.Log.FlushAndEvict(wait: true);
    }

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
        var folder = global::Test.Seamless.TestPath.Directory(this,totalKeys.ToString());
        try {
            using var logDevice = Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true);
            using var settings = new KVSettings<int,int> {
                IndexSize=64,
                PageSize=1L<<12,
                SegmentSize=1L<<12,
                MemorySize=1L<<15,
                MutableFraction=0.05,
                LogDevice=logDevice,
            };
            using var store = CreateStore(settings);
            using var session = CreateSession(store);
            var expected = new Dictionary<int,int>(totalKeys);
            for(var i = 0; i<totalKeys; i++) {
                var value = i+1;
                UpsertValue(session,i,value);
                expected.Add(i,value);
            }

            store.Log.ShiftReadOnlyAddress(store.Log.TailAddress,wait: true);
            store.Log.Flush(wait: true);

            var visited = new Dictionary<int,int>(totalKeys);
            using var scanEnumerator = new Enumerator(session,new(ChunkSize));
            var total = 0;
            var safetyLimit = totalKeys*4;
            while(scanEnumerator.MoveNext()) {
                total++;
                Assert.True(total<=safetyLimit,$"Enumerator did not terminate at totalKeys={totalKeys}.");
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
        finally {
            try { Directory.Delete(folder,true); } catch { }
        }
    }

    [Fact]
    [SuppressMessage("Interoperability","CA1416:プラットフォームの互換性",Justification = "<互換性>")]
    public void DiskBacked_Enumerator_ObservesPending() {
        const int totalKeys = 1024;
        var folder = global::Test.Seamless.TestPath.Directory(this);
        try {
            using var logDevice = Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true);
            using var settings = new KVSettings<int,int> {
                IndexSize=64,
                PageSize=1L<<12,
                SegmentSize=1L<<12,
                MemorySize=1L<<15,
                MutableFraction=0.05,
                LogDevice=logDevice,
            };
            using var store = CreateStore(settings);
            using var session = CreateSession(store);
            for(var i = 0; i<totalKeys; i++) {
                UpsertValue(session,i,i+1);
            }

            store.Log.ShiftReadOnlyAddress(store.Log.TailAddress,wait: true);
            store.Log.Flush(wait: true);

            using var scanEnumerator = new Enumerator(session,new(ChunkSize));
            var count = 0;
            var safetyLimit = totalKeys*4;
            while(scanEnumerator.MoveNext()) {
                _=scanEnumerator.Current;
                count++;
                Assert.True(count<=safetyLimit,
                    $"Enumerator did not terminate in pending-path check. count={count} totalKeys={totalKeys}");
            }

            Assert.Equal(totalKeys,count);
            output.WriteLine($"pending-count={scanEnumerator.PendingCompletionCount} total={count}");
            Assert.True(scanEnumerator.PendingCompletionCount>0,
                "Enumerator did not observe pending disk reads. Increase spill pressure if this regresses.");

            store.Log.FlushAndEvict(wait: true);
        }
        finally {
            try { Directory.Delete(folder,true); } catch { }
        }
    }

    // ================================================================
    // テスト2: ディスクスピル ─ ReadOnly 移動後の全 25 件をチャンク 10 件で走査
    //
    // ShiftReadOnlyAddress でレコードを ReadOnly に移動してから IterateLookup を呼ぶ。
    // SingleReader コールバック (ReadOnly 領域) が呼ばれ、Chunk 配列に蓄積される。
    // CompletePending を正しい位置で呼ぶ Pending 処理パターンを示す。
    // ================================================================
    [Fact]
    [SuppressMessage("Interoperability","CA1416:プラットフォームの互換性",Justification = "<互換性>")]
    public void DiskSpill_Chunk10_VisitsAll25Records_PendingPattern() {
        var folder = global::Test.Seamless.TestPath.Directory(this);

        using var logDevice = Devices.CreateLogDevice(
            Path.Combine(folder,"log.data"),deleteOnClose: true);

        using var settings = new KVSettings<int,int> {
            IndexSize=64,
            PageSize=1L<<12,   // 4 KB
            SegmentSize=1L<<22,
            MemorySize=1L<<22,   // 4 MB
            MutableFraction=1.0,
            LogDevice=logDevice,
        };
        using var store = CreateStore(settings);
        using var session = CreateSession(store);

        for(var i = 0; i<TotalKeys; i++) {
            UpsertValue(session,i,i*100);
        }
        // Note: FlushAndEvict は HeadAddress=TailAddress にしてしまい、
        //       IterateLookup がスキャン対象を見つけられなくなるため使わない
        store.Log.ShiftReadOnlyAddress(store.Log.TailAddress,wait: true);

        var visitedKeys = new global::System.Collections.Generic.List<int>();
        var visitedValues = new global::System.Collections.Generic.List<int>();
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
    //   - scanSession で IterateLookup → Chunk に Entry を蓄積 (SingleReader)
    //   - バッチ後に Chunk から key を取り出し、upsertSession で value=1 に上書き
    //   - UpsertChunk パターン:
    //       fixed-length key/value のまま RMW → Pending をその場で解消
    //     のシンプルな実装を確認する。
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
    [SuppressMessage("Interoperability","CA1416:プラットフォームの互換性",Justification = "<互換性>")]
    public void DiskSpill_UpsertFromChunk_PendingHandled(int Count) {
        var folder = global::Test.Seamless.TestPath.Directory(this,Count.ToString());
        using var logDevice = Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true);
        using var settings = new KVSettings<int,int> {
            IndexSize=64,
            PageSize=1L<<12,   // 4 KB
            SegmentSize=1L<<22,
            MemorySize=1L<<22,   // 4 MB
            MutableFraction=1.0,
            LogDevice=logDevice,
        };
        using var store = CreateStore(settings);
        using var scanSession = CreateSession(store);
        using var upsertSession = CreateSession(store);
        var expected = new Dictionary<int,int>(Count);

        for(var i = 0; i<Count; i++) {
            var value = i+1;
            UpsertValue(scanSession,i,value);
            expected.Add(i,value);
        }

        // 全レコードを ReadOnly 領域へ移動
        store.Log.ShiftReadOnlyAddress(store.Log.TailAddress,wait: true);
        store.Log.Flush(wait: true);

        var totalUpserted = 0;
        var visited = new Dictionary<int,int>(Count);

        using var scanEnumerator = new Enumerator(scanSession,new(ChunkSize));
        var total = 0;
        while(scanEnumerator.MoveNext()) {
            total++;
            var key = scanEnumerator.Current;
            Assert.True(expected.TryGetValue(key,out var expectedValue),$"Unexpected key {key}.");
            var actualValue = ReadValue(upsertSession,key);
            Assert.Equal(expectedValue,actualValue);
            Assert.True(visited.TryAdd(key,actualValue),$"Duplicate key {key} was enumerated.");
            UpsertValue(upsertSession,key,1);
            totalUpserted++;
        }

        output.WriteLine($"DiskSpill+Upsert Count={Count} total={total} upserted={totalUpserted} pending={scanEnumerator.PendingCompletionCount}");
        Assert.Equal(Count,total);
        Assert.Equal(Count,totalUpserted);
        AssertEqualByKey(expected,visited);

        store.Log.FlushAndEvict(wait: true);
    }
}
