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
    /// IScanIteratorFunctions 実装です。
    /// IterateLookup から読んだレコードをデシリアライズしてチャンクへ格納します。
    /// 列挙時は ResetChunk() で Count を初期化して再利用します。
    /// </summary>
    public abstract class IterateLookupIteratorFunctions_List<T>:IScanIteratorFunctions {
        private const int MinimumScanChunkLength = 4096;
        public readonly T[] Chunk = new T[Math.Max(Const.Chunk_Length,MinimumScanChunkLength)];
        public int Count;
        private long LastAcceptedAddress;
        public void ResetChunk() {
            this.Count=0;
            this.LastAcceptedAddress=0;
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
            if(this.Count>=this.Chunk.Length) {
                if(recordMetadata.Address==this.LastAcceptedAddress) {
                    cursorRecordResult=CursorRecordResult.Skip|CursorRecordResult.EndBatch;
                } else {
                    cursorRecordResult=CursorRecordResult.Skip|CursorRecordResult.EndBatch|CursorRecordResult.RetryLastRecord;
                }
                return true;
            }

            this.Chunk[this.Count++]=this.Deserialize(logRecord);
            this.LastAcceptedAddress=recordMetadata.Address;
            cursorRecordResult=CursorRecordResult.Accept;
            return true;
        }
        public bool OnStart(long beginAddress,long endAddress) => true;
        public void OnStop(bool completed,long numberOfRecords) {
        }
        public void OnException(Exception exception,long numberOfRecords) {
        }
    }

    public abstract class MemoryIterateLookupIteratorFunctions<T>:IterateLookupIteratorFunctions_List<T> {
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
    private readonly int ThreadOwnerManagedThreadId = Environment.CurrentManagedThreadId;
    private int ThreadAffinityWarningRaised;
    private readonly CollectionStore<
        TKey,
        TValue,
        TStoreFunctions,
        TAllocator
    > CollectionStore;
    private protected TsavoriteKV<TStoreFunctions,TAllocator> Store => this.CollectionStore.Store;
    public TryAddResult TryAdd(TKey Key,TValue Value) {
        this.VerifyThreadAffinity();
        return this.PrivateTryAdd(Key,Value);
    }
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
    internal sealed class IterateLookupCounter:IScanIteratorFunctions {
        public long Counter;
        public bool Reader<TSourceLogRecord>(in TSourceLogRecord sourceLogRecord,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult)
            where TSourceLogRecord : ISourceLogRecord {
            if(sourceLogRecord.Info.Tombstone) {
                cursorRecordResult=CursorRecordResult.Skip;
            } else {
                cursorRecordResult=CursorRecordResult.Accept;
                this.Counter++;
            }
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
        this.VerifyThreadAffinity();
        var iterator = new IterateLookupCounter();
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
        this.VerifyThreadAffinity();
        this.DisposeAdditionalSessions();
        this.DisposeCoreSessions();
        this.CollectionStore.Store.Reset();
        this.RecreateCoreSessions();
        this.RecreateAdditionalSessions();
    }
    public virtual void Refresh() {
        this.VerifyThreadAffinity();
        // Refresh() performs log truncation/segment deletion, then recreates sessions.
        // Dispose active sessions, reset store, then recreate session objects.
        this.DisposeAdditionalSessions();
        this.DisposeCoreSessions();
        this.RecreateCoreSessions();
        this.RecreateAdditionalSessions();
    }
    public bool TryGetValue(TKey Key,[MaybeNullWhen(false)] out TValue Value) {
        this.VerifyThreadAffinity();
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
                } else {
                    Status=CompletedOutputs.Current.Status;
                }
            } finally {
                DisposeCompletedOutputs(ref CompletedOutputs);
            }
        } else {
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
                } else {
                    Status=CompletedOutputs.Current.Status;
                    Output=CompletedOutputs.Current.Output;
                }
            } finally {
                DisposeCompletedOutputs(ref CompletedOutputs);
            }
        } else {
        }
        return Status;
    }
    private static void DisposeCompletedOutputs<TInput0, TOutput0>(
        ref CompletedOutputIterator<TInput0,TOutput0,EqualityEmpty> completedOutputs
    ) {
        try {
            completedOutputs.Dispose();
        } catch(NullReferenceException) {
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
        } catch {
        }
    }
    private protected interface IIterateLookupForEachStrategy<TSelf>
        where TSelf : struct, IIterateLookupForEachStrategy<TSelf> {
        static abstract bool Reader<TSourceLogRecord>(
            ref TSelf self,
            in TSourceLogRecord logRecord,
            RecordMetadata recordMetadata,
            long numberOfRecords,
            out CursorRecordResult cursorRecordResult)
            where TSourceLogRecord : ISourceLogRecord;
    }
    private protected struct IterateLookupForEachIterator<TStrategy>(TStrategy strategy):IScanIteratorFunctions
        where TStrategy : struct, IIterateLookupForEachStrategy<TStrategy> {
        public TStrategy Strategy = strategy;
        public bool Reader<TSourceLogRecord>(in TSourceLogRecord logRecord,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult)
            where TSourceLogRecord : ISourceLogRecord {
            if(logRecord.Info.Tombstone) {
                cursorRecordResult=CursorRecordResult.Skip;
                return true;
            }
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
        where TStrategy : struct, IIterateLookupForEachStrategy<TStrategy> {
        ArgumentNullException.ThrowIfNull(collection);
        collection.VerifyThreadAffinity(nameof(ForEach));
        var readSession = collection.ReadSession;
        var iterator = new IterateLookupForEachIterator<TStrategy>(strategy);
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
            > session):IIterateLookupForEachStrategy<DeleteSessionForEachStrategy<TSessionFunctions>>
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
            > session):IIterateLookupForEachStrategy<RmwSessionForEachStrategy<TSessionFunctions>>
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
            > session):IIterateLookupForEachStrategy<UpsertLogRecordForEachStrategy<TSessionFunctions>>
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
        GC.SuppressFinalize(this);
    }
    protected virtual void Dispose(bool disposing) {
        if(this.disposed)
            return;
        if(disposing) {
            this.DisposeAdditionalSessions();
            this.DisposeCoreSessions();
        } else {
        }
        this.disposed=true;
    }
    ~Collection() => this.Dispose(false);
    [global::System.Diagnostics.Conditional("DEBUG")]
    internal void VerifyThreadAffinity([global::System.Runtime.CompilerServices.CallerMemberName] string operation = "") {
        if(this.ThreadOwnerManagedThreadId==Environment.CurrentManagedThreadId)
            return;
        if(global::System.Threading.Interlocked.CompareExchange(ref this.ThreadAffinityWarningRaised,1,0)!=0)
            return;
        Debug.WriteLine(
            $"[Tsavorite] Thread-affinity warning: {this.GetType().Name} should be used from thread {this.ThreadOwnerManagedThreadId}, "+
            $"current thread {Environment.CurrentManagedThreadId} at {operation}. "+
            "Create a new collection instance from Store.Create() for each thread.");
    }
    private struct IterateLookupIsSubsetOf<TCollection>(TCollection other):IScanIteratorFunctions
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
            if(logRecord.Info.Tombstone) {
                cursorRecordResult=CursorRecordResult.Skip;
                return true;
            }
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
    private struct IterateLookupOverlaps<TCollection>(TCollection self):IScanIteratorFunctions
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
            if(logRecord.Info.Tombstone) {
                cursorRecordResult=CursorRecordResult.Skip;
                return true;
            }
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
        self.VerifyThreadAffinity(nameof(IsSubsetOfSet));
        other.VerifyThreadAffinity(nameof(IsSubsetOfSet));
        var ReadSession = self.ReadSession;
        var Iterator = new IterateLookupIsSubsetOf<TCollection>(other);
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
        self.VerifyThreadAffinity(nameof(OverlapsSet));
        other.VerifyThreadAffinity(nameof(OverlapsSet));
        var ReadSession = other.ReadSession;
        var Iterator = new IterateLookupOverlaps<TCollection>(self);
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
        Self.VerifyThreadAffinity(nameof(SetEqualsSet));
        Other.VerifyThreadAffinity(nameof(SetEqualsSet));
        if(Self.GetLongCount()!=Other.GetLongCount()) {
            return false;
        }
        return IsSubsetOfSet(Self,Other);
    }
    /// <summary>
    /// Intersects this collection with another collection in place.
    /// </summary>
    /// <typeparam name="TCollection">The collection type.</typeparam>
    /// <param name="self">The collection to modify.</param>
    /// <param name="other">The collection to intersect with.</param>
    /// <param name="Session">The session used to tombstone keys.</param>
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
        self.VerifyThreadAffinity(nameof(this.IntersectWith));
        other.VerifyThreadAffinity(nameof(this.IntersectWith));
        var ReadSession = self.ReadSession;
        var workListPath = CreateTemporaryWorkListPath("intersect-with");
        try {
            using var keysToExpire = new SerializedList<byte[],RawByteArraySerializer>(workListPath,initialCapacity: Const.Chunk_Length,initialDataCapacity: 4096);
            var Iterator = new IterateLookupIntersectWith<TCollection>(other,keysToExpire);
            // IntersectWith first collects keys to expire, then tombstones them with RMW.
            // This keeps the scan separate from the delete phase and avoids advancing TailAddress early.
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
        } finally {
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
        other.VerifyThreadAffinity(nameof(this.SymmetricExceptWith));
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
        other.VerifyThreadAffinity(nameof(this.ExceptWith));
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
        other.VerifyThreadAffinity(nameof(this.UnionWith));
        var strategy = new UpsertLogRecordForEachStrategy<TSessionFunctions>(Session);
        ForEach(other,ref strategy);
        Session.BasicContext.CompletePending(wait: true);
    }
    private readonly struct IterateLookupIntersectWith<TCollection>(
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
            if(logRecord.Info.Tombstone) {
                cursorRecordResult=CursorRecordResult.Skip;
                return true;
            }
            ReadOnlySpan<byte> KeyBytes = logRecord.KeyBytes;
            if(other.PrivateProtectedContainsKey(KeyBytes)) {
            } else {
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



public abstract class Collection<TKey, TValue, TKeySerializer, TValueSerializer>:Collection<
    TKey,//TKey
    TValue,//TValue
    TValue,//TReadOutput
    TValue,//TAddInput
    Read<TValue,TValueSerializer>,//TReadFunctions
    Upsert<TValue,TValueSerializer>,//TAddFunctions
    ContainsDelete,//TDeleteFunctions
    SpanByteStoreFunctions,//TStoreFunctions
    SpanByteStoreAllocator//TAllocator
> where TKeySerializer : ISerializer<TKey>
    where TValueSerializer : ISerializer<TValue> {
    public sealed class KeyIterator:IterateLookupIteratorFunctions_List<TKey>, ICreate<KeyIterator> {
        public static KeyIterator Create() => new();
        private protected override TKey Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord) =>
            TKeySerializer.Deserialize(logRecord.Key);
    }
    private protected sealed class MemoryKeyIterator:MemoryIterateLookupIteratorFunctions<TKey> {
        private protected override TKey Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord) =>
            TKeySerializer.Deserialize(logRecord.Key);
    }
    private protected sealed class ValueIterator:IterateLookupIteratorFunctions_List<TValue>, ICreate<ValueIterator> {
        public static ValueIterator Create() => new();
        private protected override TValue Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord) =>
            TValueSerializer.Deserialize(logRecord.ValueSpan);
    }
    private protected sealed class MemoryValueIterator:MemoryIterateLookupIteratorFunctions<TValue> {
        private protected override TValue Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord) =>
            TValueSerializer.Deserialize(logRecord.ValueSpan);
    }
    public sealed class KeyValueIterator:IterateLookupIteratorFunctions_List<KeyValuePair<TKey,TValue>> {
        private protected override KeyValuePair<TKey,TValue> Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord) {
            var Key = TKeySerializer.Deserialize(logRecord.Key);
            var Value = TValueSerializer.Deserialize(logRecord.ValueSpan);
            return new(Key,Value);
        }
    }
    private protected sealed class MemoryKeyValueIterator:MemoryIterateLookupIteratorFunctions<KeyValuePair<TKey,TValue>> {
        private protected override KeyValuePair<TKey,TValue> Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord) {
            var Key = TKeySerializer.Deserialize(logRecord.Key);
            var Value = TValueSerializer.Deserialize(logRecord.ValueSpan);
            return new(Key,Value);
        }
    }
    public struct Enumerator<TIterator, T>:IEnumerator<T>
        where TIterator : IterateLookupIteratorFunctions_List<T> {
        private readonly Collection<
            TKey,
            TValue,
            TValue,
            TValue,
            Read<TValue,TValueSerializer>,
            Upsert<TValue,TValueSerializer>,
            ContainsDelete,
            SpanByteStoreFunctions,
            SpanByteStoreAllocator
        > CollectionInstance;
        private long Cursor;
        private int Index = -1;
        private int Count;
        private bool Done;
        private T CurrentValue = default!;
        private readonly ClientSession<
            FixedSpanByteKey,//TKey
            EqualityEmpty,//TInput
            TValue,//TOutput
            EqualityEmpty,//TContext
            Read<TValue,TValueSerializer>,
            SpanByteStoreFunctions,
            SpanByteStoreAllocator
        > _session;
        private readonly long _untilAddress;
        private TIterator _iterator;
        public Enumerator(ClientSession<
            FixedSpanByteKey,//TKey
            EqualityEmpty,//TInput
            TValue,//TOutput
            EqualityEmpty,//TContext
            Read<TValue,TValueSerializer>,
            SpanByteStoreFunctions,
            SpanByteStoreAllocator
        > session,
        TIterator iterator,
        long untilAddress,
        Collection<
            TKey,
            TValue,
            TValue,
            TValue,
            Read<TValue,TValueSerializer>,
            Upsert<TValue,TValueSerializer>,
            ContainsDelete,
            SpanByteStoreFunctions,
            SpanByteStoreAllocator
        > collection) {
            this.CollectionInstance=collection;
            this._session=session;
            this._untilAddress=untilAddress;
            this._iterator=iterator;
            this.Cursor=0;
            this.Index=-1;
            this.Count=0;
            this.Done=false;
            this.CurrentValue=default!;
        }
        public readonly T Current => this.CurrentValue;
        readonly object? IEnumerator.Current => this.Current;
        public bool MoveNext() {
            this.CollectionInstance.VerifyThreadAffinity();
            var nextIndex = this.Index+1;
            if(nextIndex<this.Count) {
                this.Index=nextIndex;
                this.CurrentValue=this._iterator.Chunk[this.Index];
                return true;
            }

            if(this.Done) {
                this.Index=-1;
                this.CurrentValue=default!;
                return false;
            }

            while(true) {
                this._iterator.ResetChunk();
                var mayHaveMore = this._session.IterateLookup(ref this._iterator,ref this.Cursor,resetCursor: false);
                this._session.BasicContext.CompletePending(wait: true);
                this.Count=this._iterator.Count;

                if(!mayHaveMore)
                    this.Done=true;

                if(this.Count==0) {
                    if(this.Done) {
                        this.Index=-1;
                        this.CurrentValue=default!;
                        return false;
                    }
                    continue;
                }

                this.Index=0;
                this.CurrentValue=this._iterator.Chunk[0];
                return true;
            }
        }
        public void Reset() {
            this.Cursor=0;
            this.Count=0;
            this.Index=-1;
            this.Done=false;
            this.CurrentValue=default!;
            this._iterator.ResetChunk();
        }
        public void Dispose() {
            this.Done=true;
            this.Index=-1;
            this.Count=0;
            this.CurrentValue=default!;
        }
    }
    private protected Collection(CollectionStore<TKey,TValue> CollectionStore) : base(CollectionStore) {
    }
    public bool ContainsKey(TKey Key) {
        this.VerifyThreadAffinity(nameof(this.ContainsKey));
        return this.PrivateProtectedContainsKey(TKeySerializer.Serialize(ref Key));
    }
    public bool Contains(TKey Key) {
        this.VerifyThreadAffinity(nameof(this.Contains));
        return this.PrivateProtectedContainsKey(TKeySerializer.Serialize(ref Key));
    }
    private protected override unsafe Status Read(TKey Key,out TValue Value) {
        this.VerifyThreadAffinity(nameof(this.Read));
        var KeyBytes = TKeySerializer.Serialize(ref Key);
        var Session = this.ReadSession;
        TValue Output = default!;
        Status Status;
        fixed(byte* _ = KeyBytes) {
            var FixedKey = (FixedSpanByteKey)KeyBytes;
            Status=Session.BasicContext.Read(FixedKey,ref Output);
            Status=Pending(Session,Status,ref Output);
        }
        Debug.Assert(Status.IsCompleted);
        if(Status.Found)
            Value=Output;
        else
            Value=default!;
        return Status;
    }
    public unsafe bool Remove(TKey key) {
        this.VerifyThreadAffinity();
        var ClientSession = this.ContainsDeleteSession;
        var KeyBytes = TKeySerializer.Serialize(ref key);
        var Input = default(EqualityEmpty);
        var Output = false;
        Status Status;
        fixed(byte* _ = KeyBytes) {
            Status=ClientSession.BasicContext.RMW((FixedSpanByteKey)KeyBytes,ref Input,ref Output);
            Status=Pending(ClientSession,Status,ref Output);
        }
        Debug.Assert(Status.IsCompleted);
        return Output;
    }
    private protected override unsafe TryAddResult PrivateTryAdd(TKey key,TValue value) {
        this.VerifyThreadAffinity(nameof(this.PrivateTryAdd));
        var KeyBytes = TKeySerializer.Serialize(ref key);
        var serializedValueSize = TValueSerializer.Serialize(ref value).Length;
        if(this.WouldOverflowMutable(KeyBytes.Length,serializedValueSize))
            return TryAddResult.LogFull;
        var ClientSession = this.Add_ClientSession;
        var Output = false;
        Status Status;
        fixed(byte* _ = KeyBytes) {
            var FixedKey = (FixedSpanByteKey)KeyBytes;
            Status=ClientSession.BasicContext.RMW(FixedKey,ref value,ref Output);
            Status=Pending(ClientSession,Status,ref Output);
        }
        Debug.Assert(Status.IsCompleted);
        if(Output)
            return TryAddResult.Success;
        return TryAddResult.KeyAlreadyExists;
    }

    private protected unsafe void PrivateUpsert(TKey key,TValue value) {
        this.VerifyThreadAffinity(nameof(this.PrivateUpsert));
        var KeyBytes = TKeySerializer.Serialize(ref key);
        var ValueBytes = TValueSerializer.Serialize(ref value);
        if(this.WouldOverflowMutable(KeyBytes.Length,ValueBytes.Length))
            throw this.ThrowLogFull();
        var ClientSession = this.Add_ClientSession;
        Status Status;
        fixed(byte* _ = KeyBytes) {
            var FixedKey = (FixedSpanByteKey)KeyBytes;
            Status=ClientSession.BasicContext.Upsert(FixedKey,ValueBytes);
            Pending(ClientSession,Status);
        }
    }
}
