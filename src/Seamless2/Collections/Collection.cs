using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Seamless.SessionFunctions;
using Seamless.Stores;
namespace Seamless.Collections;

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
>:IDisposable
    where TKey : IEquatable<TKey>
    where TReadFunctions : struct, ISessionFunctions<
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
    public abstract class ScanIteratorFunctions<T>:IScanIteratorFunctions {
        public readonly T[] Chunk = new T[Const.Chunk_Length];
        public int Count;
        private long LastAcceptedAddress;
        private protected virtual bool ShouldRead<TSourceLogRecord>(in TSourceLogRecord logRecord)
            where TSourceLogRecord : ISourceLogRecord => true;
        private protected abstract T Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord) where TSourceLogRecord : ISourceLogRecord;
        public bool Reader<TSourceLogRecord>(in TSourceLogRecord logRecord,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) where TSourceLogRecord : ISourceLogRecord {
            if(!this.ShouldRead(logRecord)) {
                cursorRecordResult=CursorRecordResult.Skip;
                return true;
            }
            Debug.Assert(!logRecord.Info.Tombstone);
            var Index = Interlocked.Increment(ref this.Count)-1;
            if(Index>=Const.Chunk_Length) {
                Volatile.Write(ref this.Count,Const.Chunk_Length);
                var lastAcceptedAddress = Volatile.Read(ref this.LastAcceptedAddress);
                if(recordMetadata.Address==lastAcceptedAddress)
                    cursorRecordResult=CursorRecordResult.Skip|CursorRecordResult.EndBatch;//Pendingの影響で重複 → RetryLastRecord 付けない
                else
                    cursorRecordResult=CursorRecordResult.Skip|CursorRecordResult.EndBatch|CursorRecordResult.RetryLastRecord;
                return true;
            }
            this.Chunk[Index]=this.Deserialize(logRecord);
            Volatile.Write(ref this.LastAcceptedAddress,recordMetadata.Address);
            cursorRecordResult=CursorRecordResult.Accept;
            return true;
        }
        public bool OnStart(long beginAddress,long endAddress) {
            Volatile.Write(ref this.Count,0);
            Volatile.Write(ref this.LastAcceptedAddress,0);
            return true;
        }
        public void OnStop(bool completed,long numberOfRecords) {
        }
        public void OnException(Exception exception,long numberOfRecords) {
        }
    }

    public abstract class MemoryScanIteratorFunctions<T>:ScanIteratorFunctions<T> {
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
            this.Counter=numberOfRecords;
        }
    }
    private readonly CounterIterator _counterIterator = new();
    public long GetLongCount() {
        var Iterator = this._counterIterator;
        var Cursor = 0L;
        this.ReadSession.IterateLookup(ref Iterator,ref Cursor,resetCursor: false);
        this.ReadSession.BasicContext.CompletePending(wait: true);
        return _counterIterator.Counter;
    }
    private protected unsafe bool PrivateProtectedContainsKey(ReadOnlySpan<byte> KeyBytes) {
        var Session = this.ContainsDeleteSession;
        var Output = false;
        Status Status;
        // FixedSpanByteKey requires the key span to stay fixed through any pending completion.
        // Seamless2 resolves Pending before leaving the block, so fixed avoids extra copies/GCHandle.
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
        // Tsavorite の log truncation/segment deletion は、古い session が保持する epoch 世代参照に影響される。
        // session を張り替えて pending を解消し、古い世代を手放す。
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
            using(CompletedOutputs) {
                if(!CompletedOutputs.Next()) {
                }
                else {
                    Status=CompletedOutputs.Current.Status;
                }
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
            using(CompletedOutputs) {
                if(!CompletedOutputs.Next()) {
                }
                else {
                    Status=CompletedOutputs.Current.Status;
                    Output=CompletedOutputs.Current.Output;
                }
            }
        }
        else {
        }
        return Status;
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
            OverflowMutableStrategyKind.Memory => this.WouldOverflowMutable<MemoryOverflowMutableStrategy>(Log.BeginAddress,Log.TailAddress,KeyLength,ValueLength),
            OverflowMutableStrategyKind.Storage => this.WouldOverflowMutable<StorageOverflowMutableStrategy>(Log.BeginAddress,Log.TailAddress,KeyLength,ValueLength),
            _ => throw new UnreachableException(),
        };
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool WouldOverflowMutable<TOverflowMutableStrategy>(long BeginAddress,long TailAddress,int KeyLength,int ValueLength)
        where TOverflowMutableStrategy : struct, IOverflowMutableStrategy<TOverflowMutableStrategy> {
        return TOverflowMutableStrategy.WouldOverflowMutable(this.LogMemorySize,this.MutableFraction,BeginAddress,TailAddress,KeyLength,ValueLength);
    }
    public int Count => (int)this.GetLongCount();
    public long EntryCount => this.CollectionStore.Store.EntryCount;
    private protected long HeadAddress => this.CollectionStore.Address.HeadAddress;
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
        var ReadSession=other.ReadSession;
        var Iterator=new ScanIteratorFunctions_Overlaps<TCollection>(self);
        var Cursor=0L;
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
    /// ManagedHashSet,UnmanagedHashSetが使う
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
        var keysToExpire = new List<byte[]>();
        var Iterator = new ScanIteratorFunctions_IntersectWith<TCollection>(other,keysToExpire);
        var untilAddress = self.CollectionStore.Address.TailAddress;
        // IntersectWith は走査しながら RMW で tombstone を追記するため、
        // 走査開始時の TailAddress を上限に固定して新規追記分を再走査しない。
        var Cursor = 0L;
        ReadSession.IterateLookup(ref Iterator,ref Cursor,untilAddress,resetCursor: false);
        ReadSession.BasicContext.CompletePending(wait: true);
        foreach(var keyBytes in keysToExpire) {
            var input = default(EqualityEmpty);
            var output = default(EqualityEmpty);
            Status status;
            unsafe {
                fixed(byte* _ = keyBytes) {
                    status=Session.BasicContext.RMW((FixedSpanByteKey)keyBytes,ref input,ref output);
                    Pending(Session,status,ref output);
                }
            }
        }
        Session.BasicContext.CompletePending(wait: true);
    }
    private protected void SymmetricExceptWith<TCollection>(
        TCollection other,
        ClientSession<
            FixedSpanByteKey,
            EqualityEmpty,
            EqualityEmpty,
            EqualityEmpty,
            SymmetricExceptWith,
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
        var ReadSession = other.ReadSession;
        var keysToToggle = new List<byte[]>();
        var Iterator = new ScanIteratorFunctions_SymmetricExceptWith(keysToToggle);
        var Cursor = 0L;
        ReadSession.IterateLookup(ref Iterator,ref Cursor,resetCursor: false);
        ReadSession.BasicContext.CompletePending(wait: true);
        foreach(var keyBytes in keysToToggle) {
            var input = default(EqualityEmpty);
            var output = default(EqualityEmpty);
            Status status;
            unsafe {
                fixed(byte* _ = keyBytes) {
                    status=Session.BasicContext.RMW((FixedSpanByteKey)keyBytes,ref input,ref output);
                    Pending(Session,status,ref output);
                }
            }
        }
        Session.BasicContext.CompletePending(wait: true);
    }
    private protected void ExceptWith<TCollection>(TCollection other)
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
        var Iterator = new ScanIteratorFunctions_ExceptWith(this.ContainsDeleteSession);
        var Cursor = 0L;
        ReadSession.IterateLookup(ref Iterator,ref Cursor,resetCursor: false);
        ReadSession.BasicContext.CompletePending(wait: true);
        this.ContainsDeleteSession.BasicContext.CompletePending(wait: true);
    }
    private readonly struct ScanIteratorFunctions_IntersectWith<TCollection>(
        TCollection other,
        List<byte[]> keysToExpire
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
            var KeyBytes = logRecord.Key;
            if(other.PrivateProtectedContainsKey(KeyBytes)) {
            }else{
                keysToExpire.Add(KeyBytes.ToArray());
            }
            cursorRecordResult=CursorRecordResult.Accept;
            return true;
        }
        public bool OnStart(long beginAddress,long endAddress) => true;
        public void OnException(Exception exception,long numberOfRecords) { }
        public void OnStop(bool completed,long numberOfRecords) { }
    }
    private sealed class ScanIteratorFunctions_ExceptWith(
        ClientSession<
            FixedSpanByteKey,
            EqualityEmpty,
            bool,
            EqualityEmpty,
            TContainsDeleteFunctions,
            TStoreFunctions,
            TAllocator
        > Session
    ):IScanIteratorFunctions {
        public int Count;
        public bool Reader<TSourceLogRecord>(in TSourceLogRecord logRecord,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult)
            where TSourceLogRecord : ISourceLogRecord {
            if(this.Count>=Const.Chunk_Length) {
                cursorRecordResult=CursorRecordResult.Skip|CursorRecordResult.EndBatch|CursorRecordResult.RetryLastRecord;
                return true;
            }
            var Input = default(EqualityEmpty);
            var Output = false;
            var status = Session.BasicContext.RMW((FixedSpanByteKey)logRecord.Key,ref Input,ref Output);
            if(status.IsPending)
                Session.BasicContext.CompletePending(wait: true);
            this.Count++;
            cursorRecordResult=CursorRecordResult.Accept;
            return true;
        }
        public bool OnStart(long beginAddress,long endAddress) {
            this.Count=0;
            return true;
        }
        public void OnException(Exception exception,long numberOfRecords) { }
        public void OnStop(bool completed,long numberOfRecords) { }
    }
    private protected readonly struct ScanIteratorFunctions_UnionWith(
        ClientSession<
            FixedSpanByteKey,
            TAddInput,
            bool,
            EqualityEmpty,
            TAddFunctions,
            TStoreFunctions,
            TAllocator
        > Session
    ):IScanIteratorFunctions {
        public bool Reader<TSourceLogRecord>(in TSourceLogRecord logRecord,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult)
            where TSourceLogRecord : ISourceLogRecord {
            var status = Session.BasicContext.Upsert((FixedSpanByteKey)logRecord.Key,logRecord);
            if(status.IsPending)
                Session.BasicContext.CompletePending(wait: true);
            cursorRecordResult=CursorRecordResult.Accept;
            return true;
        }
        public bool OnStart(long beginAddress,long endAddress) => true;
        public void OnException(Exception exception,long numberOfRecords) { }
        public void OnStop(bool completed,long numberOfRecords) { }
    }
    private protected readonly struct ScanIteratorFunctions_SymmetricExceptWith(
        List<byte[]> keysToToggle
    ):IScanIteratorFunctions {
        public bool Reader<TSourceLogRecord>(in TSourceLogRecord logRecord,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult)
            where TSourceLogRecord : ISourceLogRecord {
            keysToToggle.Add(logRecord.Key.ToArray());
            cursorRecordResult=CursorRecordResult.Accept;
            return true;
        }
        public bool OnStart(long beginAddress,long endAddress) => true;
        public void OnException(Exception exception,long numberOfRecords) { }
        public void OnStop(bool completed,long numberOfRecords) { }
    }
}
