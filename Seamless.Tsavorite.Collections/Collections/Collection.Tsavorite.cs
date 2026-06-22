using Seamless.Persisted.Collections;
using Seamless.Tsavorite.Collections.SessionFunctions;
using Seamless.Tsavorite.Collections.Stores;
namespace Seamless.Tsavorite.Collections;

[DebuggerDisplay("EntryCount={EntryCount}")]
public abstract class Collection<
    TKey,
    TValue,
    TReadOutput,
    TAddInput,
    TReadFunctions,
    TAddFunctions,
    TContainsDeleteFunctions,
    TStoreFunctions,
    TAllocator
>:IDisposable where TReadFunctions : struct, ISessionFunctions<
        EqualityEmpty,
        TReadOutput,
        EqualityEmpty
    >,
    ICreate<TReadFunctions>
    where TAddFunctions : struct, ISessionFunctions<
        TAddInput,
        bool,
        EqualityEmpty
    >,
    ICreate<TAddFunctions>
    where TContainsDeleteFunctions : struct, ISessionFunctions<
        EqualityEmpty,
        bool,
        EqualityEmpty
    >,
    ICreate<TContainsDeleteFunctions>
    where TStoreFunctions : IStoreFunctions
    where TAllocator : IAllocator<
        TStoreFunctions
    > {
    /// <summary>
    /// IterateLookup 縺ｧ繧ｹ繝医い縺ｮ蜈ｨ繝ｬ繧ｳ繝ｼ繝峨ｒ 1 繝ｪ繧ｹ繝・(= 蜻ｼ縺ｳ蜃ｺ縺怜・縺梧ｸ｡縺吩ｸ譎・List/List) 縺ｫ豬√＠霎ｼ繧縺溘ａ縺ｮ
    /// IScanIteratorFunctions 螳溯｣・・繧､繝・Ξ繝ｼ繧ｿ蝓ｺ蠎輔ょ推 *Iterator 蝙九・縺薙・繧ｯ繝ｩ繧ｹ繧堤ｶ呎価縺励※
    /// <see cref="Deserialize{TSourceLogRecord}"/> 繧貞ｮ溯｣・☆繧九・numerator 讒狗ｯ画凾縺ｫ
    /// <see cref="SetList"/> 縺ｧ蜃ｺ蜉帛・繝ｪ繧ｹ繝医ｒ蟾ｮ縺苓ｾｼ繧縲・
    /// </summary>
    public abstract class ScanIteratorFunctions_List<T, TList>:IScanIteratorFunctions
        where TList : PersistedList<T> {
        private const int MinimumScanChunkLength = 4096;
        public readonly T[] Chunk = new T[Math.Max(Const.Chunk_Length,MinimumScanChunkLength)];
        public int Count;
        public bool HasPendingItem => false;
        private TList? List;
        private readonly Lock Sync = new();
        public void SetList(TList list) => this.List=list;
        public void ResetChunk() {
            lock(this.Sync)
                this.Count=0;
        }
        private protected virtual bool ShouldRead<TSourceLogRecord>(in TSourceLogRecord logRecord)
            where TSourceLogRecord : ISourceLogRecord => true;
        private protected abstract T Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord)
            where TSourceLogRecord : ISourceLogRecord;
        public bool Reader<TSourceLogRecord>(in TSourceLogRecord logRecord,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult)
            where TSourceLogRecord : ISourceLogRecord {
            if(!this.ShouldRead(logRecord)||logRecord.Info.Tombstone) {
                cursorRecordResult=CursorRecordResult.Skip;
                return true;
            }
            if(this.List is { } list) {
                lock(this.Sync)
                    list.Add(this.Deserialize(logRecord));
                cursorRecordResult=CursorRecordResult.Accept;
                return true;
            }

            lock(this.Sync) {
                if(this.Count>=this.Chunk.Length) {
                    cursorRecordResult=CursorRecordResult.Skip|CursorRecordResult.EndBatch;
                    return true;
                }

                this.Chunk[this.Count++]=this.Deserialize(logRecord);
                cursorRecordResult=CursorRecordResult.Accept;
                return true;
            }
        }
        public bool OnStart(long beginAddress,long endAddress) => true;
        public void OnStop(bool completed,long numberOfRecords) {
        }
        public void OnException(Exception exception,long numberOfRecords) {
        }
    }

    public abstract class MemoryScanIteratorFunctions<T, TList>:ScanIteratorFunctions_List<T,TList>
        where TList : PersistedList<T> {
        private protected sealed override bool ShouldRead<TSourceLogRecord>(in TSourceLogRecord logRecord) =>
            typeof(TSourceLogRecord)!=typeof(DiskLogRecord);
    }
    private protected ClientSession<
        FixedSpanByteKey,
        EqualityEmpty,
        TReadOutput,
        EqualityEmpty,
        TReadFunctions,
        TStoreFunctions,
        TAllocator
    > ReadSession { get; private set; } = null!;
    private protected ClientSession<
        FixedSpanByteKey,
        TAddInput,
        bool,
        EqualityEmpty,
        TAddFunctions,
        TStoreFunctions,
        TAllocator
    > Add_ClientSession { get; private set; } = null!;
    private protected ClientSession<
        FixedSpanByteKey,
        EqualityEmpty,
        bool,
        EqualityEmpty,
        TContainsDeleteFunctions,
        TStoreFunctions,
        TAllocator
    > ContainsDeleteSession { get; private set; } = null!;
    private readonly CollectionStore<
        TKey,
        TValue,
        TStoreFunctions,
        TAllocator
    > CollectionStore;
    private protected TsavoriteKV<TStoreFunctions,TAllocator> Store => this.CollectionStore.Store;
    public TryAddResult TryAdd(TKey Key,TValue Value) => this.PrivateTryAdd(Key,Value);
    public void Add(TKey Key,TValue Value) {
        var Result = this.TryAdd(Key,Value);
        switch(Result) {
            case TryAddResult.LogFull:
                throw this.ThrowLogFull();
            case TryAddResult.KeyAlreadyExists:
                throw new ArgumentException($"An item with the same key has already been added. Key: {Key}");
        }
        Debug.Assert(Result==TryAddResult.Success);
    }
    //todo 現在のバージョンではstructでは更新できない。
    internal sealed class CounterIterator:IScanIteratorFunctions {
        public long Counter;
        public bool Reader<TSourceLogRecord>(in TSourceLogRecord sourceLogRecord,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult)
            where TSourceLogRecord : ISourceLogRecord {
            cursorRecordResult=CursorRecordResult.Accept;
            this.Counter++;
            return true;
        }
        public bool OnStart(long beginAddress,long endAddress) {
            this.Counter=0;
            return true;
        }
        public void OnException(Exception exception,long numberOfRecords) {
        }
        public void OnStop(bool completed,long numberOfRecords) {
        }
    }
    public long GetLongCount() {
        var iterator = new CounterIterator();
        var cursor = 0L;
        this.ReadSession.IterateLookup(ref iterator,ref cursor,resetCursor: false);
        this.ReadSession.BasicContext.CompletePending(wait: true);
        return iterator.Counter;
    }
    private protected unsafe bool PrivateProtectedContainsKey(ReadOnlySpan<byte> KeyBytes) {
        var Session = this.ContainsDeleteSession;
        var Output = false;
        Status Status;
        // FixedSpanByteKey requires the key span to stay fixed through any pending completion.
        // Tsavorite2 resolves Pending before leaving the block, so fixed avoids extra copies/GCHandle.
        fixed(byte* _ = KeyBytes) {
            Status=Session.BasicContext.Read((FixedSpanByteKey)KeyBytes,ref Output);
            Status=Pending(Session,Status,ref Output);
        }
        return Status.Found;
    }
    protected Collection(
        CollectionStore<
            TKey,
            TValue,
            TStoreFunctions,
            TAllocator
        > CollectionStore
    ) {
        this.CollectionStore=CollectionStore;
        this.RecreateCoreSessions();
    }
    private void RecreateCoreSessions() {
        var Store = this.CollectionStore.Store;
        this.ReadSession=Store.NewSession<
            FixedSpanByteKey,
            EqualityEmpty,
            TReadOutput,
            EqualityEmpty,
            TReadFunctions
        >(TReadFunctions.Create());
        this.Add_ClientSession=Store.NewSession<
            FixedSpanByteKey,
            TAddInput,
            bool,
            EqualityEmpty,
            TAddFunctions
        >(TAddFunctions.Create());
        this.ContainsDeleteSession=Store.NewSession<
            FixedSpanByteKey,
            EqualityEmpty,
            bool,
            EqualityEmpty,
            TContainsDeleteFunctions
        >(TContainsDeleteFunctions.Create());
    }
    private void DisposeCoreSessions() {
        CompletePendingAndDispose(this.ReadSession);
        CompletePendingAndDispose(this.Add_ClientSession);
        CompletePendingAndDispose(this.ContainsDeleteSession);
    }
    private protected virtual void DisposeAdditionalSessions() {
    }
    private protected virtual void RecreateAdditionalSessions() {
    }
    private protected static void CompletePendingAndDispose<TInput0, TOutput0, TSessionFunctions0>(
        ClientSession<
            FixedSpanByteKey,
            TInput0,
            TOutput0,
            EqualityEmpty,
            TSessionFunctions0,
            TStoreFunctions,
            TAllocator
        > session)
        where TSessionFunctions0 : struct, ISessionFunctions<
            TInput0,
            TOutput0,
            EqualityEmpty
        > {
        session.BasicContext.CompletePending(wait: true);
        session.Dispose();
    }
    public void Clear() {
        this.DisposeAdditionalSessions();
        this.DisposeCoreSessions();
        this.CollectionStore.Store.Reset();
        this.RecreateCoreSessions();
        this.RecreateAdditionalSessions();
    }
    public virtual void Refresh() {
        // Tsavorite 縺ｮ log truncation/segment deletion 縺ｯ縲∝商縺・session 縺御ｿ晄戟縺吶ｋ epoch 荳紋ｻ｣蜿ら・縺ｫ蠖ｱ髻ｿ縺輔ｌ繧九・
        // session 繧貞ｼｵ繧頑崛縺医※ pending 繧定ｧ｣豸医＠縲∝商縺・ｸ紋ｻ｣繧呈焔謾ｾ縺吶・
        this.DisposeAdditionalSessions();
        this.DisposeCoreSessions();
        this.RecreateCoreSessions();
        this.RecreateAdditionalSessions();
    }
    public bool TryGetValue(TKey Key,[MaybeNullWhen(false)] out TValue Value) {
        var Status = this.Read(Key,out Value);
        if(Status.NotFound) {
            Value=default;
            return false;
        }
        return true;
    }
    private protected abstract TryAddResult PrivateTryAdd(TKey Key,TValue Value);
    private protected static Status Pending<
        TInput0,
        TOutput0,
        TSessionFunctions0
    >(
        ClientSession<
            FixedSpanByteKey,
            TInput0,
            TOutput0,
            EqualityEmpty,
            TSessionFunctions0,
            TStoreFunctions,
            TAllocator
        > Session,
        Status Status
    )
    where TSessionFunctions0 : struct, ISessionFunctions<
        TInput0,
        TOutput0,
        EqualityEmpty
    > {
        if(Status.IsPending) {
            Session.BasicContext.CompletePendingWithOutputs(out var CompletedOutputs,wait: true);
            try {
                if(!CompletedOutputs.Next()) {
                }
                else {
                    Status=CompletedOutputs.Current.Status;
                }
            }
            finally {
                DisposeCompletedOutputs(ref CompletedOutputs);
            }
        }
        else {
        }
        return Status;
    }
    private protected static Status Pending<
        TInput0,
        TOutput0,
        TSessionFunctions0
    >(
        ClientSession<
            FixedSpanByteKey,
            TInput0,
            TOutput0,
            EqualityEmpty,
            TSessionFunctions0,
            TStoreFunctions,
            TAllocator
        > Session,
        Status Status,
        ref TOutput0 Output
    )
    where TSessionFunctions0 : struct, ISessionFunctions<
        TInput0,
        TOutput0,
        EqualityEmpty
    > {
        if(Status.IsPending) {
            Session.BasicContext.CompletePendingWithOutputs(out var CompletedOutputs,wait: true);
            try {
                if(!CompletedOutputs.Next()) {
                }
                else {
                    Status=CompletedOutputs.Current.Status;
                    Output=CompletedOutputs.Current.Output;
                }
            }
            finally {
                DisposeCompletedOutputs(ref CompletedOutputs);
            }
        }
        else {
        }
        return Status;
    }
    private static void DisposeCompletedOutputs<TInput0, TOutput0>(
        ref CompletedOutputIterator<TInput0,TOutput0,EqualityEmpty> completedOutputs
    ) {
        try {
            completedOutputs.Dispose();
        }
        catch(NullReferenceException) {
            // Tsavorite 2.0 beta can throw while clearing heap fields for a completed
            // disk record whose heap object was never populated. The operation result
            // has already been consumed; do not turn that cleanup bug into an Add/Read failure.
        }
    }
    private protected static string CreateTemporaryWorkListPath(string operationName) {
        var directoryPath = Path.Combine(
            Path.GetTempPath(),
            "Tsavorite",
            "worklists",
            operationName,
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directoryPath);
        return Path.Combine(directoryPath,"items.dat");
    }
    private protected static void DeleteTemporaryWorkListPath(string path) {
        var directoryPath = Path.GetDirectoryName(path);
        if(string.IsNullOrEmpty(directoryPath))
            return;
        try {
            Directory.Delete(directoryPath,recursive: true);
        }
        catch {
        }
    }
    private protected interface IForEachStrategy<TSelf>
        where TSelf : struct, IForEachStrategy<TSelf> {
        static abstract bool Reader<TSourceLogRecord>(
            ref TSelf self,
            in TSourceLogRecord logRecord,
            RecordMetadata recordMetadata,
            long numberOfRecords,
            out CursorRecordResult cursorRecordResult)
            where TSourceLogRecord : ISourceLogRecord;
    }
    private protected struct ForEachIterator<TStrategy>(TStrategy strategy):IScanIteratorFunctions
        where TStrategy : struct, IForEachStrategy<TStrategy> {
        public TStrategy Strategy = strategy;
        public bool Reader<TSourceLogRecord>(in TSourceLogRecord logRecord,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult)
            where TSourceLogRecord : ISourceLogRecord {
            return TStrategy.Reader(ref this.Strategy,in logRecord,recordMetadata,numberOfRecords,out cursorRecordResult);
        }
        public bool OnStart(long beginAddress,long endAddress) => true;
        public void OnStop(bool completed,long numberOfRecords) {
        }
        public void OnException(Exception exception,long numberOfRecords) {
        }
    }
    private protected static void ForEach<TCollection, TStrategy>(TCollection collection,ref TStrategy strategy)
        where TCollection : Collection<
            TKey,
            TValue,
            TReadOutput,
            TAddInput,
            TReadFunctions,
            TAddFunctions,
            TContainsDeleteFunctions,
            TStoreFunctions,
            TAllocator
        >
        where TStrategy : struct, IForEachStrategy<TStrategy> {
        ArgumentNullException.ThrowIfNull(collection);
        var readSession = collection.ReadSession;
        var iterator = new ForEachIterator<TStrategy>(strategy);
        var cursor = 0L;
        readSession.IterateLookup(ref iterator,ref cursor,resetCursor: false);
        readSession.BasicContext.CompletePending(wait: true);
        strategy=iterator.Strategy;
    }
    private protected readonly struct DeleteSessionForEachStrategy<TSessionFunctions>(
        ClientSession<
                FixedSpanByteKey,
                EqualityEmpty,
                EqualityEmpty,
                EqualityEmpty,
                TSessionFunctions,
                TStoreFunctions,
                TAllocator
            > session):IForEachStrategy<DeleteSessionForEachStrategy<TSessionFunctions>>
        where TSessionFunctions : struct, ISessionFunctions<
            EqualityEmpty,
            EqualityEmpty,
            EqualityEmpty
        > {
        private readonly ClientSession<
            FixedSpanByteKey,
            EqualityEmpty,
            EqualityEmpty,
            EqualityEmpty,
            TSessionFunctions,
            TStoreFunctions,
            TAllocator
        > _session = session;

        public static unsafe bool Reader<TSourceLogRecord>(
            ref DeleteSessionForEachStrategy<TSessionFunctions> self,
            in TSourceLogRecord logRecord,
            RecordMetadata recordMetadata,
            long numberOfRecords,
            out CursorRecordResult cursorRecordResult)
            where TSourceLogRecord : ISourceLogRecord {
            ReadOnlySpan<byte> keyBytes = logRecord.KeyBytes;
            fixed(byte* _ = keyBytes) {
                self._session.BasicContext.Delete((FixedSpanByteKey)keyBytes);
            }
            cursorRecordResult=CursorRecordResult.Accept;
            return true;
        }
    }
    private protected readonly struct RmwSessionForEachStrategy<TSessionFunctions>(
        ClientSession<
                FixedSpanByteKey,
                EqualityEmpty,
                EqualityEmpty,
                EqualityEmpty,
                TSessionFunctions,
                TStoreFunctions,
                TAllocator
            > session):IForEachStrategy<RmwSessionForEachStrategy<TSessionFunctions>>
        where TSessionFunctions : struct, ISessionFunctions<
            EqualityEmpty,
            EqualityEmpty,
            EqualityEmpty
        > {
        private readonly ClientSession<
            FixedSpanByteKey,
            EqualityEmpty,
            EqualityEmpty,
            EqualityEmpty,
            TSessionFunctions,
            TStoreFunctions,
            TAllocator
        > _session = session;

        public static unsafe bool Reader<TSourceLogRecord>(
            ref RmwSessionForEachStrategy<TSessionFunctions> self,
            in TSourceLogRecord logRecord,
            RecordMetadata recordMetadata,
            long numberOfRecords,
            out CursorRecordResult cursorRecordResult)
            where TSourceLogRecord : ISourceLogRecord {
            var input = default(EqualityEmpty);
            var output = default(EqualityEmpty);
            ReadOnlySpan<byte> keyBytes = logRecord.KeyBytes;
            Status status;
            fixed(byte* _ = keyBytes) {
                status=self._session.BasicContext.RMW((FixedSpanByteKey)keyBytes,ref input,ref output);
            }
            if(status.IsPending)
                self._session.BasicContext.CompletePending(wait: true);
            cursorRecordResult=CursorRecordResult.Accept;
            return true;
        }
    }
    private protected readonly struct UpsertLogRecordForEachStrategy<TSessionFunctions>(
        ClientSession<
                FixedSpanByteKey,
                TAddInput,
                bool,
                EqualityEmpty,
                TSessionFunctions,
                TStoreFunctions,
                TAllocator
            > session):IForEachStrategy<UpsertLogRecordForEachStrategy<TSessionFunctions>>
        where TSessionFunctions : struct, ISessionFunctions<
            TAddInput,
            bool,
            EqualityEmpty
        > {
        private readonly ClientSession<
            FixedSpanByteKey,
            TAddInput,
            bool,
            EqualityEmpty,
            TSessionFunctions,
            TStoreFunctions,
            TAllocator
        > _session = session;

        public static unsafe bool Reader<TSourceLogRecord>(
            ref UpsertLogRecordForEachStrategy<TSessionFunctions> self,
            in TSourceLogRecord logRecord,
            RecordMetadata recordMetadata,
            long numberOfRecords,
            out CursorRecordResult cursorRecordResult)
            where TSourceLogRecord : ISourceLogRecord {
            ReadOnlySpan<byte> keyBytes = logRecord.KeyBytes;
            Status status;
            fixed(byte* _ = keyBytes) {
                status=self._session.BasicContext.Upsert((FixedSpanByteKey)keyBytes,logRecord);
            }
            if(status.IsPending)
                self._session.BasicContext.CompletePending(wait: true);
            cursorRecordResult=CursorRecordResult.Accept;
            return true;
        }
    }
    private long LogMemorySize => this.CollectionStore.LogMemorySize;
    private double MutableFraction => this.CollectionStore.MutableFraction;
    private protected OutOfMemoryException ThrowLogFull() {
        var Log = this.CollectionStore.Store.Log;
        return new OutOfMemoryException($"Tsavorite mutable log is full. BeginAddress: {Log.BeginAddress}, HeadAddress: {Log.HeadAddress}, TailAddress: {Log.TailAddress}");
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private protected bool WouldOverflowMutable(int KeyLength,int ValueLength) {
        Debug.Assert(this.LogMemorySize>=8192);
        var Log = this.CollectionStore.Store.Log;
        return this.CollectionStore.OverflowMutableStrategyKind switch {
            OverflowMutableStrategyKind.Memory => MemoryOverflowMutableStrategy.WouldOverflowMutable(this.LogMemorySize,this.MutableFraction,Log.BeginAddress,Log.TailAddress,KeyLength,ValueLength),
            OverflowMutableStrategyKind.Storage => this.CollectionStore.StorageOverflowMutableStrategy.WouldOverflowMutable(Log.TailAddress,KeyLength,ValueLength),
            _ => throw new UnreachableException(),
        };
    }
    public int Count => checked((int)this.GetLongCount());
    public long EntryCount => this.CollectionStore.Store.EntryCount;
    private protected abstract Status Read(TKey Key,out TValue Output);
    public abstract bool IsDisposed { get; }
    private bool disposed;
    public void Dispose() {
        this.Dispose(true);
        System.GC.SuppressFinalize(this);
    }
    protected virtual void Dispose(bool disposing) {
        if(this.disposed)
            return;
        if(disposing) {
            this.DisposeAdditionalSessions();
            this.DisposeCoreSessions();
        }
        else {
        }
        this.disposed=true;
    }
    ~Collection() => this.Dispose(false);
    private struct ScanIteratorFunctions_IsSubsetOf<TCollection>(TCollection other):IScanIteratorFunctions
        where TCollection : Collection<
            TKey,
            TValue,
            TReadOutput,
            TAddInput,
            TReadFunctions,
            TAddFunctions,
            TContainsDeleteFunctions,
            TStoreFunctions,
            TAllocator
        > {
        public bool IsSubset = true;
        public bool Reader<TSourceLogRecord>(in TSourceLogRecord logRecord,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult)
            where TSourceLogRecord : ISourceLogRecord {
            if(!this.IsSubset) {
                cursorRecordResult=CursorRecordResult.Skip|CursorRecordResult.EndBatch;
                return true;
            }
            if(other.PrivateProtectedContainsKey(logRecord.Key)) {
                cursorRecordResult=CursorRecordResult.Accept;
                return true;
            }
            this.IsSubset=false;
            cursorRecordResult=CursorRecordResult.Skip|CursorRecordResult.EndBatch;
            return true;
        }
        public bool OnStart(long beginAddress,long endAddress) => true;
        public void OnException(Exception exception,long numberOfRecords) { }
        public void OnStop(bool completed,long numberOfRecords) { }
    }
    private struct ScanIteratorFunctions_Overlaps<TCollection>(TCollection self):IScanIteratorFunctions
        where TCollection : Collection<
            TKey,
            TValue,
            TReadOutput,
            TAddInput,
            TReadFunctions,
            TAddFunctions,
            TContainsDeleteFunctions,
            TStoreFunctions,
            TAllocator
        > {
        public bool FoundOverlap = false;
        public bool Reader<TSourceLogRecord>(in TSourceLogRecord logRecord,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult)
            where TSourceLogRecord : ISourceLogRecord {
            if(this.FoundOverlap) {
                cursorRecordResult=CursorRecordResult.Skip|CursorRecordResult.EndBatch;
                return true;
            }
            if(self.PrivateProtectedContainsKey(logRecord.Key)) {
                this.FoundOverlap=true;
                cursorRecordResult=CursorRecordResult.Skip|CursorRecordResult.EndBatch;
                return true;
            }
            cursorRecordResult=CursorRecordResult.Accept;
            return true;
        }
        public bool OnStart(long beginAddress,long endAddress) => true;
        public void OnStop(bool completed,long numberOfRecords) { }
        public void OnException(Exception exception,long numberOfRecords) { }
    }
    private protected static bool IsSubsetOfSet<TCollection>(TCollection self,TCollection other)
        where TCollection : Collection<
            TKey,
            TValue,
            TReadOutput,
            TAddInput,
            TReadFunctions,
            TAddFunctions,
            TContainsDeleteFunctions,
            TStoreFunctions,
            TAllocator
        > {
        ArgumentNullException.ThrowIfNull(other);
        var ReadSession = self.ReadSession;
        var Iterator = new ScanIteratorFunctions_IsSubsetOf<TCollection>(other);
        var Cursor = 0L;
        ReadSession.IterateLookup(ref Iterator,ref Cursor,resetCursor: false);
        ReadSession.BasicContext.CompletePending(wait: true);
        return Iterator.IsSubset;
    }
    private protected static bool IsSupersetOfSet<TCollection>(TCollection Self,TCollection Other)
        where TCollection : Collection<
            TKey,
            TValue,
            TReadOutput,
            TAddInput,
            TReadFunctions,
            TAddFunctions,
            TContainsDeleteFunctions,
            TStoreFunctions,
            TAllocator
        > {
        ArgumentNullException.ThrowIfNull(Other);
        return IsSubsetOfSet(Other,Self);
    }
    private protected static bool IsProperSubsetOfSet<TCollection>(TCollection Self,TCollection Other)
        where TCollection : Collection<
            TKey,
            TValue,
            TReadOutput,
            TAddInput,
            TReadFunctions,
            TAddFunctions,
            TContainsDeleteFunctions,
            TStoreFunctions,
            TAllocator
        > {
        ArgumentNullException.ThrowIfNull(Other);
        if(Self.GetLongCount()>=Other.GetLongCount()) {
            return false;
        }
        return IsSubsetOfSet(Self,Other);
    }
    private protected static bool IsProperSupersetOfSet<TCollection>(TCollection Self,TCollection Other)
        where TCollection : Collection<
            TKey,
            TValue,
            TReadOutput,
            TAddInput,
            TReadFunctions,
            TAddFunctions,
            TContainsDeleteFunctions,
            TStoreFunctions,
            TAllocator
        > {
        ArgumentNullException.ThrowIfNull(Other);
        if(Self.GetLongCount()<=Other.GetLongCount()) {
            return false;
        }
        return IsSupersetOfSet(Self,Other);
    }
    private protected static bool OverlapsSet<TCollection>(TCollection self,TCollection other)
        where TCollection : Collection<
            TKey,
            TValue,
            TReadOutput,
            TAddInput,
            TReadFunctions,
            TAddFunctions,
            TContainsDeleteFunctions,
            TStoreFunctions,
            TAllocator
        > {
        ArgumentNullException.ThrowIfNull(other);
        var ReadSession = other.ReadSession;
        var Iterator = new ScanIteratorFunctions_Overlaps<TCollection>(self);
        var Cursor = 0L;
        ReadSession.IterateLookup(ref Iterator,ref Cursor,resetCursor: false);
        ReadSession.BasicContext.CompletePending(wait: true);
        return Iterator.FoundOverlap;
    }
    private protected static bool SetEqualsSet<TCollection>(TCollection Self,TCollection Other)
        where TCollection : Collection<
            TKey,
            TValue,
            TReadOutput,
            TAddInput,
            TReadFunctions,
            TAddFunctions,
            TContainsDeleteFunctions,
            TStoreFunctions,
            TAllocator
        > {
        ArgumentNullException.ThrowIfNull(Other);
        if(Self.GetLongCount()!=Other.GetLongCount()) {
            return false;
        }
        return IsSubsetOfSet(Self,Other);
    }
    /// <summary>
    /// HashSet,HashSet縺御ｽｿ縺・
    /// </summary>
    /// <typeparam name="TCollection"></typeparam>
    /// <param name="self"></param>
    /// <param name="other"></param>
    /// <param name="Session"></param>
    private protected void IntersectWith<TCollection>(
        TCollection self,
        TCollection other,
        ClientSession<
            FixedSpanByteKey,
            EqualityEmpty,
            EqualityEmpty,
            EqualityEmpty,
            IntersectWith,
            TStoreFunctions,
            TAllocator
        > Session
    )
        where TCollection : Collection<
            TKey,
            TValue,
            TReadOutput,
            TAddInput,
            TReadFunctions,
            TAddFunctions,
            TContainsDeleteFunctions,
            TStoreFunctions,
            TAllocator
        > {
        ArgumentNullException.ThrowIfNull(other);
        var ReadSession = self.ReadSession;
        var workListPath = CreateTemporaryWorkListPath("intersect-with");
        try {
            using var keysToExpire = new SerializedList<byte[],RawByteArraySerializer>(workListPath,initialCapacity: Const.Chunk_Length,initialDataCapacity: 4096);
            var Iterator = new ScanIteratorFunctions_IntersectWith<TCollection>(other,keysToExpire);
            // IntersectWith 縺ｯ襍ｰ譟ｻ縺励↑縺後ｉ RMW 縺ｧ tombstone 繧定ｿｽ險倥☆繧九◆繧√・
            // 襍ｰ譟ｻ髢句ｧ区凾縺ｮ TailAddress 繧剃ｸ企剞縺ｫ蝗ｺ螳壹＠縺ｦ譁ｰ隕剰ｿｽ險伜・繧貞・襍ｰ譟ｻ縺励↑縺・・
            var Cursor = 0L;
            ReadSession.IterateLookup(ref Iterator,ref Cursor,resetCursor: false);
            ReadSession.BasicContext.CompletePending(wait: true);
            foreach(var keyBytes in keysToExpire) {
                var input = default(EqualityEmpty);
                var output = default(EqualityEmpty);
                ReadOnlySpan<byte> keyBytesSpan = keyBytes;
                unsafe {
                    fixed(byte* _ = keyBytesSpan) {
                        var status = Session.BasicContext.RMW((FixedSpanByteKey)keyBytesSpan,ref input,ref output);
                        Pending(Session,status,ref output);
                    }
                }
            }
            Session.BasicContext.CompletePending(wait: true);
        }
        finally {
            DeleteTemporaryWorkListPath(workListPath);
        }
    }
    private protected void SymmetricExceptWith<TCollection, TSessionFunctions>(
        TCollection other,
        ClientSession<
            FixedSpanByteKey,
            EqualityEmpty,
            EqualityEmpty,
            EqualityEmpty,
            TSessionFunctions,
            TStoreFunctions,
            TAllocator
        > Session
    )
        where TCollection : Collection<
            TKey,
            TValue,
            TReadOutput,
            TAddInput,
            TReadFunctions,
            TAddFunctions,
            TContainsDeleteFunctions,
            TStoreFunctions,
            TAllocator
        >
        where TSessionFunctions : struct, ISessionFunctions<
            EqualityEmpty,
            EqualityEmpty,
            EqualityEmpty
        > {
        var strategy = new RmwSessionForEachStrategy<TSessionFunctions>(Session);
        ForEach(other,ref strategy);
        Session.BasicContext.CompletePending(wait: true);
    }
    private protected void ExceptWith<TCollection, TSessionFunctions>(
        TCollection other,
        ClientSession<
            FixedSpanByteKey,
            EqualityEmpty,
            EqualityEmpty,
            EqualityEmpty,
            TSessionFunctions,
            TStoreFunctions,
            TAllocator
        > Session)
        where TCollection : Collection<
            TKey,
            TValue,
            TReadOutput,
            TAddInput,
            TReadFunctions,
            TAddFunctions,
            TContainsDeleteFunctions,
            TStoreFunctions,
            TAllocator
        >
        where TSessionFunctions : struct, ISessionFunctions<
            EqualityEmpty,
            EqualityEmpty,
            EqualityEmpty
        > {
        var strategy = new DeleteSessionForEachStrategy<TSessionFunctions>(Session);
        ForEach(other,ref strategy);
        Session.BasicContext.CompletePending(wait: true);
    }
    private protected void UnionWith<TCollection, TSessionFunctions>(
        TCollection other,
        ClientSession<
            FixedSpanByteKey,
            TAddInput,
            bool,
            EqualityEmpty,
            TSessionFunctions,
            TStoreFunctions,
            TAllocator
        > Session)
        where TCollection : Collection<
            TKey,
            TValue,
            TReadOutput,
            TAddInput,
            TReadFunctions,
            TAddFunctions,
            TContainsDeleteFunctions,
            TStoreFunctions,
            TAllocator
        >
        where TSessionFunctions : struct, ISessionFunctions<
            TAddInput,
            bool,
            EqualityEmpty
        > {
        var strategy = new UpsertLogRecordForEachStrategy<TSessionFunctions>(Session);
        ForEach(other,ref strategy);
        Session.BasicContext.CompletePending(wait: true);
    }
    private readonly struct ScanIteratorFunctions_IntersectWith<TCollection>(
        TCollection other,
        SerializedList<byte[],RawByteArraySerializer> keysToExpire
    ):IScanIteratorFunctions
        where TCollection : Collection<
            TKey,
            TValue,
            TReadOutput,
            TAddInput,
            TReadFunctions,
            TAddFunctions,
            TContainsDeleteFunctions,
            TStoreFunctions,
            TAllocator
        > {
        public bool Reader<TSourceLogRecord>(in TSourceLogRecord logRecord,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult)
            where TSourceLogRecord : ISourceLogRecord {
            ReadOnlySpan<byte> KeyBytes = logRecord.KeyBytes;
            if(other.PrivateProtectedContainsKey(KeyBytes)) {
            }
            else {
                keysToExpire.AddRawBytes(KeyBytes);
            }
            cursorRecordResult=CursorRecordResult.Accept;
            return true;
        }
        public bool OnStart(long beginAddress,long endAddress) => true;
        public void OnException(Exception exception,long numberOfRecords) { }
        public void OnStop(bool completed,long numberOfRecords) { }
    }
}
