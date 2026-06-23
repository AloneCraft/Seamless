using Seamless.Persisted.Collections;
using Seamless.Tsavorite.SessionFunctions;
using Seamless.Tsavorite.Stores;
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
    /// IterateLookup 驍ｵ・�E�繝ｻ・�E�驛｢・�E�繝ｻ・�E�驛｢譎�E樟邵�E�繝ｻ・�E�・�E�繝ｻ・�E�髯�E�闌ｨ・�E�・�E�驛｢譎｢・�E�・�E�驛｢・�E�繝ｻ・�E�驛｢譎｢・�E�・�E�驛｢譎擾�E��E�・�E�繝ｻ繝ｻ1 驛｢譎｢・�E�・�E�驛｢・�E�繝ｻ・�E�驛｢譏ｴ繝ｻ(= 髯�E�・�E�繝ｻ・�E�驍ｵ・�E�繝ｻ・�E�髯�E�繝ｻ・�E�・�E�驍ｵ・�E�隲�E�諛翫・驍ｵ・�E�隴�E�・�E�繝ｻ・�E�繝ｻ・�E�驍ｵ・�E�陷�E�・�E�繝ｻ・�E�・つ髫�E�蠑ｱ繝ｻList/List) 驍ｵ・�E�繝ｻ・�E�髮趣�E��E�遶丞､�E�・�E�・�E�鬮�E�雜｣・�E�・�E�驛｢・�E�・つ驍ｵ・�E�雋�E∞�E�E�竏ｫ・�E�・�E�繝ｻ・�E�
    /// IScanIteratorFunctions 髯橸�E��E�雋�E�E��E�繝ｻ・�E�郢晢�E��E�郢晢�E��E�驛｢・�E�繝ｻ・�E�驛｢譏ｴ繝ｻ・取ｨ抵�E��E�譎｢・�E�・�E�驛｢・�E�繝ｻ・�E�髯憺屮・�E�・�E�髯滓�E・�E�譚ｿ�E�郢�E�繝ｻ閠�E� *Iterator 髯懷姓�E�E�譏ｴ繝ｻ驍ｵ・�E�髦�E�蜷�E�繝ｻ驛｢・�E�繝ｻ・�E�驛｢譎｢・�E�・�E�驛｢・�E�繝ｻ・�E�驛｢・�E�陜｣・�E�繝ｻ・�E�陷�E�諠�E�・�E�・�E�驍ｵ・�E�陷会ｽ�E�遯�E�・�E�
    /// <see cref="Deserialize{TSourceLogRecord}"/> 驛｢・�E�髮区�E��E�・�E�・�E�雋�E�E��E�繝ｻ・�E�郢晢�E��E�隨倥・・�E�・�E�闕ｵ謨鳴郢晢�E��E�numerator 髫�E�蝣�E�霍昴・・�E�騾匁E���E�陷・�E��E�驍ｵ・�E�繝ｻ・�E�
    /// <see cref="SetList"/> 驍ｵ・�E�繝ｻ・�E�髯�E�繝ｻ・�E�・�E�髯�E�迚呻�E��E�蟶吶・驛｢譎｢・�E�・�E�驛｢・�E�繝ｻ・�E�驛｢譎�E樟繝ｻ螳壽�E��E�繝ｻ・�E�驍ｵ・�E�髣埼屮・�E�・�E�繝ｻ・�E�驛｢・�E�・つ驍ｵ・�E�郢晢�E��E�
    /// </summary>
    public abstract class ScanIteratorFunctions_List<T, TList>:IScanIteratorFunctions
        where TList : PersistedList<T> {
        private const int MinimumScanChunkLength = 4096;
        public readonly T[] Chunk = new T[Math.Max(Const.Chunk_Length,MinimumScanChunkLength)];
        public int Count;
        public bool HasPendingItem => false;
        private TList? List;
        private readonly Lock Sync = new();
        public void SetList(TList list) => List=list;
        public void ResetChunk() {
            lock(Sync)
                Count=0;
        }
        private protected virtual bool ShouldRead<TSourceLogRecord>(in TSourceLogRecord logRecord)
            where TSourceLogRecord : ISourceLogRecord => true;
        private protected abstract T Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord)
            where TSourceLogRecord : ISourceLogRecord;
        public bool Reader<TSourceLogRecord>(in TSourceLogRecord logRecord,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult)
            where TSourceLogRecord : ISourceLogRecord {
            if(!ShouldRead(logRecord)||logRecord.Info.Tombstone) {
                cursorRecordResult=CursorRecordResult.Skip;
                return true;
            }
            if(List is { } list) {
                lock(Sync)
                    list.Add(Deserialize(logRecord));
                cursorRecordResult=CursorRecordResult.Accept;
                return true;
            }

            lock(Sync) {
                if(Count>=Chunk.Length) {
                    cursorRecordResult=CursorRecordResult.Skip|CursorRecordResult.EndBatch;
                    return true;
                }

                Chunk[Count++]=Deserialize(logRecord);
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
    private protected TsavoriteKV<TStoreFunctions,TAllocator> Store => CollectionStore.Store;
    public TryAddResult TryAdd(TKey Key,TValue Value) => PrivateTryAdd(Key,Value);
    public void Add(TKey Key,TValue Value) {
        var Result = TryAdd(Key,Value);
        switch(Result) {
            case TryAddResult.LogFull:
                throw ThrowLogFull();
            case TryAddResult.KeyAlreadyExists:
                throw new ArgumentException($"An item with the same key has already been added. Key: {Key}");
        }
        Debug.Assert(Result==TryAddResult.Success);
    }
    //todo 霑ｴ・�E�陜ｨ・�E�邵�E�・�E�郢晁�E繝ｻ郢�E�・�E�郢晢�E��E�郢晢�E��E�邵�E�・�E�邵�E�・�E�struct邵�E�・�E�邵�E�・�E�隴厁E���E�隴・�E��E�邵�E�・�E�邵�E�髦�E�竊�E�E��E�繝ｻ�E�繝ｻ
    internal sealed class CounterIterator:IScanIteratorFunctions {
        public long Counter;
        public bool Reader<TSourceLogRecord>(in TSourceLogRecord sourceLogRecord,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult)
            where TSourceLogRecord : ISourceLogRecord {
            cursorRecordResult=CursorRecordResult.Accept;
            Counter++;
            return true;
        }
        public bool OnStart(long beginAddress,long endAddress) {
            Counter=0;
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
        ReadSession.IterateLookup(ref iterator,ref cursor,resetCursor: false);
        ReadSession.BasicContext.CompletePending(wait: true);
        return iterator.Counter;
    }
    private protected unsafe bool PrivateProtectedContainsKey(ReadOnlySpan<byte> KeyBytes) {
        var Session = ContainsDeleteSession;
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
        RecreateCoreSessions();
    }
    private void RecreateCoreSessions() {
        var Store = CollectionStore.Store;
        ReadSession=Store.NewSession<
            FixedSpanByteKey,
            EqualityEmpty,
            TReadOutput,
            EqualityEmpty,
            TReadFunctions
        >(TReadFunctions.Create());
        Add_ClientSession=Store.NewSession<
            FixedSpanByteKey,
            TAddInput,
            bool,
            EqualityEmpty,
            TAddFunctions
        >(TAddFunctions.Create());
        ContainsDeleteSession=Store.NewSession<
            FixedSpanByteKey,
            EqualityEmpty,
            bool,
            EqualityEmpty,
            TContainsDeleteFunctions
        >(TContainsDeleteFunctions.Create());
    }
    private void DisposeCoreSessions() {
        CompletePendingAndDispose(ReadSession);
        CompletePendingAndDispose(Add_ClientSession);
        CompletePendingAndDispose(ContainsDeleteSession);
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
        DisposeAdditionalSessions();
        DisposeCoreSessions();
        CollectionStore.Store.Reset();
        RecreateCoreSessions();
        RecreateAdditionalSessions();
    }
    public virtual void Refresh() {
        // Tsavorite 驍ｵ・�E�繝ｻ・�E� log truncation/segment deletion 驍ｵ・�E�繝ｻ・�E�驍ｵ・�E�遶乗剌閹夐し・�E�郢晢�E��E�session 驍ｵ・�E�陟包�E��E�繝ｻ・�E�隴弱・莠憺し�E�E�陷�E�・�E�繝ｻ繝ｻepoch 髣匁E���E�驍丞�E・�E�・�E�繝ｻ・�E�髯�E�・�E�郢�E�蟲�E�繝ｻ驍ｵ・�E�繝ｻ・�E�髯溷私�E�E�・�E�鬯�E�・�E�繝ｻ・�E�驍ｵ・�E�髴郁E���E�・�E�讙趣�E��E�・�E�闕ｵ謨鳴郢晢�E��E�
        // session 驛｢・�E�髮区�E��E�・�E�・�E�繝ｻ・�E�驛｢・�E�鬯・�E��E�・�E�蟶�E�・�E�・�E�陋ｹ・�E�遯�E�・�E� pending 驛｢・�E�陞ｳ螟ｲ・�E�・�E�繝ｻ・�E�髮趣�E��E�陋ｹ・�E�繝ｻ・�E�驍ｵ・�E�遶乗剌閹夐し・�E�郢晢�E��E�繝ｻ・�E�驍丞�E・�E�・�E�繝ｻ・�E�驛｢・�E�陷�E�閧�E�繝ｻ髫�E�・�E�繝ｻ・�E�驍ｵ・�E�陷�E�・�E�・つ郢晢�E��E�
        DisposeAdditionalSessions();
        DisposeCoreSessions();
        RecreateCoreSessions();
        RecreateAdditionalSessions();
    }
    public bool TryGetValue(TKey Key,[MaybeNullWhen(false)] out TValue Value) {
        var Status = Read(Key,out Value);
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
            return TStrategy.Reader(ref Strategy,in logRecord,recordMetadata,numberOfRecords,out cursorRecordResult);
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
    private long LogMemorySize => CollectionStore.LogMemorySize;
    private double MutableFraction => CollectionStore.MutableFraction;
    private protected OutOfMemoryException ThrowLogFull() {
        var Log = CollectionStore.Store.Log;
        return new OutOfMemoryException($"Tsavorite mutable log is full. BeginAddress: {Log.BeginAddress}, HeadAddress: {Log.HeadAddress}, TailAddress: {Log.TailAddress}");
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private protected bool WouldOverflowMutable(int KeyLength,int ValueLength) {
        Debug.Assert(LogMemorySize>=8192);
        var Log = CollectionStore.Store.Log;
        return CollectionStore.OverflowMutableStrategyKind switch {
            OverflowMutableStrategyKind.Memory => MemoryOverflowMutableStrategy.WouldOverflowMutable(LogMemorySize,MutableFraction,Log.BeginAddress,Log.TailAddress,KeyLength,ValueLength),
            OverflowMutableStrategyKind.Storage => CollectionStore.StorageOverflowMutableStrategy.WouldOverflowMutable(Log.TailAddress,KeyLength,ValueLength),
            _ => throw new UnreachableException(),
        };
    }
    public int Count => checked((int)GetLongCount());
    public long EntryCount => CollectionStore.Store.EntryCount;
    private protected abstract Status Read(TKey Key,out TValue Output);
    public abstract bool IsDisposed { get; }
    private bool disposed;
    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    protected virtual void Dispose(bool disposing) {
        if(disposed)
            return;
        if(disposing) {
            DisposeAdditionalSessions();
            DisposeCoreSessions();
        }
        else {
        }
        disposed=true;
    }
    ~Collection() => Dispose(false);
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
            if(!IsSubset) {
                cursorRecordResult=CursorRecordResult.Skip|CursorRecordResult.EndBatch;
                return true;
            }
            if(other.PrivateProtectedContainsKey(logRecord.Key)) {
                cursorRecordResult=CursorRecordResult.Accept;
                return true;
            }
            IsSubset=false;
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
            if(FoundOverlap) {
                cursorRecordResult=CursorRecordResult.Skip|CursorRecordResult.EndBatch;
                return true;
            }
            if(self.PrivateProtectedContainsKey(logRecord.Key)) {
                FoundOverlap=true;
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
    /// HashSet,HashSet驍ｵ・�E�陟包�E��E�繝ｻ・�E�繝ｻ・�E�驍ｵ・�E�郢晢�E��E�
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
            // IntersectWith 驍ｵ・�E�繝ｻ・�E�髫俶誓�E�E�・�E�髫�E�貊ゑ�E��E�・�E�驍ｵ・�E�陷会ｽ�E�遶企�E・�E�・�E�陟暮�E�会ｽ�E�繝ｻRMW 驍ｵ・�E�繝ｻ・�E� tombstone 驛｢・�E�陞ｳ螟ｲ・�E�・�E�繝ｻ・�E�鬮�E�・�E�陋滂ｽ�E�隨倥・・�E�・�E�闕ｵ譏ｶ陞ｺ驛｢・�E�遶丞仰郢晢�E��E�
            // 髫俶誓�E�E�・�E�髫�E�貊ゑ�E��E�・�E�鬯�E�・�E�陷�E�・�E�繝ｻ・�E�陋ｹ・�E�陷・�E��E�驍ｵ・�E�繝ｻ・�E� TailAddress 驛｢・�E�陷代・・�E�・�E�闔ｨ竏晏�E�憺し�E�E�繝ｻ・�E�髯懈圜・�E�・�E�髯橸�E��E�陞｢・�E�繝ｻ・�E�驍ｵ・�E�繝ｻ・�E�髫�E�繝ｻ・�E�・�E�鬮�E�蜍溯㊥繝ｻ・�E�繝ｻ・�E�鬮�E�・�E�闔ｨ諛翫・驛｢・�E�髮区�E��E�繝ｻ髫俶誓�E�E�・�E�髫�E�貊ゑ�E��E�・�E�驍ｵ・�E�陷会ｽ�E�遶企�E・�E�・�E�郢晢�E��E�・つ郢晢�E��E�
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
>    where TKeySerializer : ISerializer<TKey>
    where TValueSerializer : ISerializer<TValue> {
    public sealed class KeyIterator:ScanIteratorFunctions_List<TKey,PersistedList<TKey>>,ICreate<KeyIterator> {
        public static KeyIterator Create() => new();
        private protected override TKey Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord) =>
            TKeySerializer.Deserialize(logRecord.Key);
    }
    private protected sealed class MemoryKeyIterator:MemoryScanIteratorFunctions<TKey,PersistedList<TKey>> {
        private protected override TKey Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord) =>
            TKeySerializer.Deserialize(logRecord.Key);
    }
    private protected sealed class ValueIterator:ScanIteratorFunctions_List<TValue,PersistedList<TValue>>,ICreate<ValueIterator> {
        public static ValueIterator Create() => new();
        private protected override TValue Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord) =>
            TValueSerializer.Deserialize(logRecord.ValueSpan);
    }
    private protected sealed class MemoryValueIterator:MemoryScanIteratorFunctions<TValue,PersistedList<TValue>> {
        private protected override TValue Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord) =>
            TValueSerializer.Deserialize(logRecord.ValueSpan);
    }
    public sealed class KeyValueIterator:ScanIteratorFunctions_List<KeyValuePair<TKey,TValue>,PersistedList<KeyValuePair<TKey,TValue>>> {
        private protected override KeyValuePair<TKey,TValue> Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord) {
            var Key = TKeySerializer.Deserialize(logRecord.Key);
            var Value = TValueSerializer.Deserialize(logRecord.ValueSpan);
            return new(Key,Value);
        }
    }
    private protected sealed class MemoryKeyValueIterator:MemoryScanIteratorFunctions<KeyValuePair<TKey,TValue>,PersistedList<KeyValuePair<TKey,TValue>>> {
        private protected override KeyValuePair<TKey,TValue> Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord) {
            var Key = TKeySerializer.Deserialize(logRecord.Key);
            var Value = TValueSerializer.Deserialize(logRecord.ValueSpan);
            return new(Key,Value);
        }
    }
    public struct Enumerator<TIterator, T>:IEnumerator<T>
        where TIterator : ScanIteratorFunctions_List<T,PersistedList<T>> {
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
        private readonly TIterator _iterator;
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
        long untilAddress) {
            _session=session;
            _untilAddress=untilAddress;
            _iterator=iterator;
            Cursor=0;
            Index=-1;
            Count=0;
            Done=false;
            CurrentValue=default!;
        }
        public readonly T Current => CurrentValue;
        readonly object? IEnumerator.Current => Current;
        public bool MoveNext() {
            var nextIndex = Index+1;
            if(nextIndex<Count) {
                Index=nextIndex;
                CurrentValue=_iterator.Chunk[Index];
                return true;
            }

            if(Done) {
                Index=-1;
                CurrentValue=default!;
                return false;
            }

            while(true) {
                _iterator.ResetChunk();
                var scanFunctions = _iterator;
                var mayHaveMore = _session.ScanCursor(
                    ref Cursor,
                    _iterator.Chunk.Length,
                    scanFunctions,
                    _untilAddress,
                    true,
                    _untilAddress,
                    false,
                    false);
                _session.BasicContext.CompletePending(wait: true);
                Count=_iterator.Count;

                if(!mayHaveMore&&!_iterator.HasPendingItem)
                    Done=true;

                if(Count==0) {
                    Done=true;
                    Index=-1;
                    CurrentValue=default!;
                    return false;
                }

                Index=0;
                CurrentValue=_iterator.Chunk[0];
                return true;
            }
        }
        public void Reset() {
            Cursor=0;
            Count=0;
            Index=-1;
            Done=false;
            CurrentValue=default!;
            _iterator.ResetChunk();
        }
        public void Dispose() {
            Done=true;
            Index=-1;
            Count=0;
            CurrentValue=default!;
        }
    }
    private protected Collection(CollectionStore<TKey,TValue> CollectionStore) : base(CollectionStore) {
    }
    public bool ContainsKey(TKey Key) {
        return PrivateProtectedContainsKey(TKeySerializer.Serialize(ref Key));
    }
    public bool Contains(TKey Key) {
        return PrivateProtectedContainsKey(TKeySerializer.Serialize(ref Key));
    }
    private protected override unsafe Status Read(TKey Key,out TValue Value) {
        var KeyBytes = TKeySerializer.Serialize(ref Key);
        var Session = ReadSession;
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
        var ClientSession = ContainsDeleteSession;
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
        var KeyBytes = TKeySerializer.Serialize(ref key);
        var serializedValueSize = TValueSerializer.Serialize(ref value).Length;
        if(WouldOverflowMutable(KeyBytes.Length,serializedValueSize))
            return TryAddResult.LogFull;
        var ClientSession = Add_ClientSession;
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
        var KeyBytes = TKeySerializer.Serialize(ref key);
        var ValueBytes = TValueSerializer.Serialize(ref value);
        if(WouldOverflowMutable(KeyBytes.Length,ValueBytes.Length))
            throw ThrowLogFull();
        var ClientSession = Add_ClientSession;
        Status Status;
        fixed(byte* _ = KeyBytes) {
            var FixedKey = (FixedSpanByteKey)KeyBytes;
            Status=ClientSession.BasicContext.Upsert(FixedKey,ValueBytes);
            Pending(ClientSession,Status);
        }
    }
}
