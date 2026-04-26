using Seamless.Stores;

using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

using static System.Collections.Specialized.BitVector32;

namespace Seamless.Collections;

[DebuggerDisplay("EntryCount={EntryCount}")]
public abstract class Collection<
    TKey,
    TValue,
    TSessionKey,
    TSessionValue,
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
        TSessionKey,
        TSessionValue,
        EqualityEmpty,
        TReadOutput,
        EqualityEmpty
    >,
    ICreate<TReadFunctions>
    where TAddFunctions : struct, ISessionFunctions<
        TSessionKey,
        TSessionValue,
        TAddInput,
        bool,
        EqualityEmpty
    >,
    ICreate<TAddFunctions>
    where TContainsDeleteFunctions : struct, ISessionFunctions<
        TSessionKey,
        TSessionValue,
        EqualityEmpty,
        bool,
        EqualityEmpty
    >,
    ICreate<TContainsDeleteFunctions>
    where TStoreFunctions : IStoreFunctions<
        TSessionKey,
        TSessionValue
    >
    where TAllocator : IAllocator<
        TSessionKey,
        TSessionValue,
        TStoreFunctions
    > {
    //public abstract class ScanIteratorFunctions_Chunk<T>:IScanIteratorFunctions<TSessionKey,TSessionValue> {
    //    public readonly T[] Chunk = new T[Const.Chunk_Length];
    //    public int Count;
    //    public long LastAcceptedAddress;
    //    public bool SkipLastAcceptedAddressOnce;
    //    public bool ストレージにデータがあるか;
    //    private protected abstract T Deserialize(ref TSessionKey key,ref TSessionValue value);
    //    public bool SingleReader(ref TSessionKey key,ref TSessionValue value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
    //        if(this.Count>=Const.Chunk_Length) {
    //            //メモリで完結する場合はここは通らない。
    //            //ここを通るのは、ストレージを伴う場合で、かつChunkの上限に達した場合。
    //            //this.LastAcceptedAddress=recordMetadata.Address;//次回の走査でこのレコードを再度読むため、RetryLastRecord を返す。
    //            if(this.Count==Const.Chunk_Length){
    //                this.LastAcceptedAddress=recordMetadata.Address;
    //                this.ストレージにデータがあるか=true;
    //            }else{
    //            }
    //            cursorRecordResult=CursorRecordResult.Skip|CursorRecordResult.EndBatch|CursorRecordResult.RetryLastRecord;//ストレージの場合はここは無視されるが、念のため EndBatch も返す。
    //            return false;
    //        }
    //        if(this.SkipLastAcceptedAddressOnce&&recordMetadata.Address==this.LastAcceptedAddress) {
    //            this.SkipLastAcceptedAddressOnce=false;
    //            cursorRecordResult=CursorRecordResult.Skip;
    //            return true;
    //        }
    //        // 重複読み込み防止（以前の議論のロジック）
    //        if(recordMetadata.Address==this.LastAcceptedAddress) {
    //            cursorRecordResult=CursorRecordResult.Skip;
    //            return true;
    //        }
    //        // データの処理
    //        this.Chunk[this.Count]=this.Deserialize(ref key,ref value);
    //        this.Count++;
    //        cursorRecordResult=CursorRecordResult.Accept;
    //        return true;
    //        //Debug.Assert(this.Count<=Const.Chunk_Length);
    //        //var Index = this.Count;
    //        //this.Chunk[Index]=this.Deserialize(ref key,ref value);
    //        //Index++;
    //        //if(Index==Const.Chunk_Length) {
    //        //    cursorRecordResult=CursorRecordResult.Accept|CursorRecordResult.EndBatch;
    //        //}
    //        //else {
    //        //    cursorRecordResult=CursorRecordResult.Accept;
    //        //}
    //        //this.Count=Index;
    //        //return true;
    //        //Debug.Assert(!recordMetadata.RecordInfo.Tombstone);
    //        //var index = Interlocked.Increment(ref this.Count)-1;
    //        //if(index>=Const.Chunk_Length) {
    //        //    Volatile.Write(ref this.Count,Const.Chunk_Length);
    //        //    var lastAcceptedAddress = Volatile.Read(ref this.LastAcceptedAddress);
    //        //    //trueを返す条件はrecordMetadata.Address==lastAcceptedAddressのときのみで、これはPendingの影響で重複して読まされる最後のレコードに対してだけ起こりうる。
    //        //    if(recordMetadata.Address==lastAcceptedAddress)
    //        //        cursorRecordResult=CursorRecordResult.Skip|CursorRecordResult.EndBatch;//Pendingの影響で重複 → RetryLastRecord 付けない
    //        //    else
    //        //        cursorRecordResult=CursorRecordResult.Skip|CursorRecordResult.EndBatch|CursorRecordResult.RetryLastRecord;
    //        //    return true;
    //        //}
    //        //this.Chunk[index]=this.Deserialize(ref key,ref value);
    //        //Volatile.Write(ref this.LastAcceptedAddress,recordMetadata.Address);
    //        //cursorRecordResult=CursorRecordResult.Accept;
    //        //return true;
    //    }
    //    public bool ConcurrentReader(ref TSessionKey key,ref TSessionValue value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
    //        lock(this)
    //            return this.SingleReader(ref key,ref value,recordMetadata,numberOfRecords,out cursorRecordResult);
    //    }
    //    public bool OnStart(long beginAddress,long endAddress) {
    //        Volatile.Write(ref this.Count,0);
    //        return true;
    //    }
    //    public void OnStop(bool completed,long numberOfRecords) {
    //    }
    //    public void OnException(Exception exception,long numberOfRecords) {
    //    }
    //}
    public abstract class ScanIteratorFunctions_List<T, TList>:IScanIteratorFunctions<TSessionKey,TSessionValue>
        where TList : List<T> {
        private TList? List;
        private readonly object Sync = new();
        public void SetList(TList list) {
            this.List=list;
        }
        private protected abstract T Deserialize(ref TSessionKey key,ref TSessionValue value);
        public bool SingleReader(ref TSessionKey key,ref TSessionValue value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
            if(recordMetadata.RecordInfo.Tombstone) {
                cursorRecordResult=CursorRecordResult.Skip;
                return true;
            }
            this.List!.Add(this.Deserialize(ref key,ref value));
            cursorRecordResult=CursorRecordResult.Accept;
            return true;
        }
        public bool ConcurrentReader(ref TSessionKey key,ref TSessionValue value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
            lock(this.Sync)
                return this.SingleReader(ref key,ref value,recordMetadata,numberOfRecords,out cursorRecordResult);
        }
        public bool OnStart(long beginAddress,long endAddress) {
            return true;
        }
        public void OnStop(bool completed,long numberOfRecords) {
        }
        public void OnException(Exception exception,long numberOfRecords) {
        }
    }
    private protected static string CreateTemporaryWorkListPath(string operationName) {
        var directoryPath = global::System.IO.Path.Combine(
            global::System.IO.Path.GetTempPath(),
            "Seamless",
            "worklists",
            operationName,
            Guid.NewGuid().ToString("N"));
        global::System.IO.Directory.CreateDirectory(directoryPath);
        return global::System.IO.Path.Combine(directoryPath,"items.dat");
    }
    private protected static void DeleteTemporaryWorkListPath(string path) {
        var directoryPath = global::System.IO.Path.GetDirectoryName(path);
        if(string.IsNullOrEmpty(directoryPath))
            return;
        try {
            global::System.IO.Directory.Delete(directoryPath,recursive: true);
        }
        catch {
        }
    }
    private protected void IterateLookupAll<TIterator>(
        ClientSession<
            TSessionKey,
            TSessionValue,
            EqualityEmpty,
            TReadOutput,
            EqualityEmpty,
            TReadFunctions,
            TStoreFunctions,
            TAllocator
        > session,
        ref TIterator iterator
    )
        where TIterator : IScanIteratorFunctions<TSessionKey,TSessionValue> {
        var cursor = 0L;
        {
            var hasMore = session.IterateLookup(ref iterator,ref cursor,resetCursor: false);
            session.BasicContext.CompletePending(wait: true);
        }
    }
    //public struct Enumerator<TIterator,T,TList>(
    //    ClientSession<
    //        TSessionKey,
    //        TSessionValue,
    //        EqualityEmpty,
    //        TReadOutput,
    //        EqualityEmpty,
    //        TReadFunctions,
    //        TStoreFunctions,
    //        TAllocator
    //    > Session,
    //    TIterator Iterator,
    //    TList List
    //):IEnumerator<T>
    //    where TIterator : ScanIteratorFunctions_List<T,TList>
    //    where TList:List<T>{
    //    private long Cursor,Count;
    //    private int Index = -1;
    //    private bool 最後のデータ配列か;
    //    public readonly T Current => Iterator.Chunk[this.Index];
    //    readonly object? IEnumerator.Current => this.Current;
    //    public bool MoveNext() {
    //        if(++this.Index<this.Count)
    //            return true;
    //        if(this.最後のデータ配列か) {
    //            return false;
    //        }
    //        var まだデータがある=Session.IterateLookup(ref Iterator,ref this.Cursor,resetCursor: false);
    //        //Session.BasicContext.CompletePending(wait: true);
    //        var Count = Iterator.Count;
    //        if(Iterator.LastAcceptedAddress>0)
    //            this.Cursor=Iterator.LastAcceptedAddress;
    //        this.Count=Count;
    //        if(!まだデータがある&&Count==0)
    //            this.最後のデータ配列か=true;
    //        if(Count>0) {
    //            this.Index=0;
    //            return true;
    //        }
    //        return false;
    //    }
    //    public void Reset() {
    //        this.Cursor=0;
    //        this.Count=0;
    //        this.Index=-1;
    //        this.最後のデータ配列か=false;
    //    }
    //    public readonly void Dispose() {
    //    }
    //}
    private protected ClientSession<
        TSessionKey,
        TSessionValue,
        EqualityEmpty,
        TReadOutput,
        EqualityEmpty,
        TReadFunctions,
        TStoreFunctions,
        TAllocator
    > ReadSession { get; private set; } = null!;
    private protected ClientSession<
        TSessionKey,
        TSessionValue,
        EqualityEmpty,
        TReadOutput,
        EqualityEmpty,
        TReadFunctions,
        TStoreFunctions,
        TAllocator
    > ReadMemoryClientSession { get; private set; } = null!;
    private protected ClientSession<
        TSessionKey,
        TSessionValue,
        TAddInput,
        bool,
        EqualityEmpty,
        TAddFunctions,
        TStoreFunctions,
        TAllocator
    > Add_ClientSession { get; private set; } = null!;
    private protected ClientSession<
        TSessionKey,
        TSessionValue,
        EqualityEmpty,
        bool,
        EqualityEmpty,
        TContainsDeleteFunctions,
        TStoreFunctions,
        TAllocator
    > DeleteSession { get; private set; } = null!;
    private readonly CollectionStore<
        TKey,
        TValue,
        TSessionKey,
        TSessionValue,
        TStoreFunctions,
        TAllocator
    > CollectionStore;
    private protected TsavoriteKV<TSessionKey,TSessionValue,TStoreFunctions,TAllocator> Store => this.CollectionStore.Store;
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
    public sealed class ScanIteratorFunctions_Counter:IScanIteratorFunctions<TSessionKey,TSessionValue> {
        public long Counter;
        public bool SingleReader(ref TSessionKey key,ref TSessionValue value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
            cursorRecordResult=CursorRecordResult.Accept;
            return true;
        }
        public bool ConcurrentReader(ref TSessionKey key,ref TSessionValue value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
            cursorRecordResult=CursorRecordResult.Accept;
            return true;
        }
        public bool OnStart(long beginAddress,long endAddress) {
            return true;
        }
        public void OnException(Exception exception,long numberOfRecords) {
        }
        public void OnStop(bool completed,long numberOfRecords) {
            this.Counter=numberOfRecords;
        }
    }
    public long GetLongCount() {
        var Iterator = new ScanIteratorFunctions_Counter();
        var Cursor = 0L;
        this.ReadSession.IterateLookup(ref Iterator,ref Cursor,resetCursor: false);
        this.ReadSession.BasicContext.CompletePending(wait: true);
        return Iterator.Counter;
    }
    private protected bool PrivateProtectedContainsKey(ref TSessionKey SessionKey) {
        var ReadSession = this.ReadSession;
        var Output = default(TReadOutput)!;
        var Status = ReadSession.BasicContext.Read(ref SessionKey,ref Output);
        Status=Pending(ReadSession,Status,ref Output);
        return Status.Found;
    }
    protected Collection(
        CollectionStore<
            TKey,
            TValue,
            TSessionKey,
            TSessionValue,
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
            EqualityEmpty,

            TReadOutput,
            EqualityEmpty,
            TReadFunctions
        >(TReadFunctions.Create());
        this.ReadMemoryClientSession=Store.NewSession<
            EqualityEmpty,

            TReadOutput,
            EqualityEmpty,
            TReadFunctions
        >(TReadFunctions.Create());
        this.Add_ClientSession=Store.NewSession<

            TAddInput,
            bool,
            EqualityEmpty,
            TAddFunctions
        >(TAddFunctions.Create());
        this.DeleteSession=Store.NewSession<

            EqualityEmpty,
            bool,
            EqualityEmpty,
            TContainsDeleteFunctions
        >(TContainsDeleteFunctions.Create());
    }
    private void DisposeCoreSessions() {
        CompletePendingAndDispose(this.ReadSession);
        CompletePendingAndDispose(this.ReadMemoryClientSession);
        CompletePendingAndDispose(this.Add_ClientSession);
        CompletePendingAndDispose(this.DeleteSession);
    }
    private protected virtual void DisposeAdditionalSessions() {
    }
    private protected virtual void RecreateAdditionalSessions() {
    }
    private protected static void CompletePendingAndDispose<TInput0, TOutput0, TContext0, TSessionFunctions0>(
        ClientSession<
            TSessionKey,
            TSessionValue,
            TInput0,
            TOutput0,
            TContext0,
            TSessionFunctions0,
            TStoreFunctions,
            TAllocator
        > session)
        where TSessionFunctions0 : struct, ISessionFunctions<
            TSessionKey,
            TSessionValue,
            TInput0,
            TOutput0,
            TContext0
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
        // Tsavorite 1.1.1 でも古い session は旧世代の epoch 参照を引きずりうるため、
        // session を張り替えて pending を解消し、log truncation を進めやすくする。
        this.DisposeAdditionalSessions();
        this.DisposeCoreSessions();
        this.RecreateCoreSessions();
        this.RecreateAdditionalSessions();
    }
    public virtual bool TryGetValue(TKey Key,[MaybeNullWhen(false)] out TValue Value) {
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
        TContext0,
        TSessionFunctions0
    >(
        ClientSession<
            TSessionKey,
            TSessionValue,
            TInput0,
            TOutput0,
            TContext0,
            TSessionFunctions0,
            TStoreFunctions,
            TAllocator
        > Session,
        Status Status
    )
    where TSessionFunctions0 : struct, ISessionFunctions<
        TSessionKey,
        TSessionValue,
        TInput0,
        TOutput0,
        TContext0
    > {
        if(Status.IsPending) {
            Session.BasicContext.CompletePendingWithOutputs(out var CompletedOutputs,wait: true);
            using(CompletedOutputs) {
                if(!CompletedOutputs.Next())
                    return Status;
                Status=CompletedOutputs.Current.Status;
            }
        }
        else {
        }
        return Status;
    }
    private protected static Status Pending<
        TInput0,
        TOutput0,
        TContext0,
        TSessionFunctions0
    >(
        ClientSession<
            TSessionKey,
            TSessionValue,
            TInput0,
            TOutput0,
            TContext0,
            TSessionFunctions0,
            TStoreFunctions,
            TAllocator
        > Session,
        Status Status,
        ref TOutput0 Output
    )
    where TSessionFunctions0 : struct, ISessionFunctions<
        TSessionKey,
        TSessionValue,
        TInput0,
        TOutput0,
        TContext0
    > {
        if(Status.IsPending) {
            Session.BasicContext.CompletePendingWithOutputs(out var CompletedOutputs,wait: true);
            using(CompletedOutputs) {
                if(!CompletedOutputs.Next())
                    return Status;
                Status=CompletedOutputs.Current.Status;
                Output=CompletedOutputs.Current.Output;
            }
        }
        else {
        }
        return Status;
    }
    private long LogMemorySize => this.CollectionStore.LogMemorySize;
    private double MutableFraction => this.CollectionStore.MutableFraction;
    private protected bool IsEmpty {
        get {
            var Log = this.CollectionStore.Store.Log;
            return Log.TailAddress<=Log.BeginAddress;
        }
    }
    private protected OutOfMemoryException ThrowLogFull() {
        var Log = this.CollectionStore.Store.Log;
        return new OutOfMemoryException($"Tsavorite mutable log is full. BeginAddress: {Log.BeginAddress}, HeadAddress: {Log.HeadAddress}, TailAddress: {Log.TailAddress}");
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private protected bool WouldOverflowMutable(int KeyLength,int ValueLength) {
        return false;
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
    private protected abstract Status Read(TKey Key,out TValue Output);
    public abstract bool IsDisposed { get; }
    private bool disposed;
    public void Dispose() {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }
    protected virtual void Dispose(bool disposing) {
        if(this.disposed) {
            return;
        }
        if(disposing) {
            this.DisposeAdditionalSessions();
            this.DisposeCoreSessions();
        }
        this.disposed=true;
    }
    ~Collection() => this.Dispose(false);
    //Collection
    private struct ScanIteratorFunctions_IsSubsetOf<TCollection>(TCollection other,object syncRoot):IScanIteratorFunctions<TSessionKey,TSessionValue>
        where TCollection : Collection<
            TKey,
            TValue,
            TSessionKey,
            TSessionValue,
            TReadOutput,
            TAddInput,
            TReadFunctions,
            TAddFunctions,
            TContainsDeleteFunctions,
            TStoreFunctions,
            TAllocator
        > {
        public bool IsSubset = true;
        private bool ReaderCore(ref TSessionKey key,ref TSessionValue value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
            if(!this.IsSubset) {
                cursorRecordResult=CursorRecordResult.Skip|CursorRecordResult.EndBatch;
                return true;
            }
            // IsSubset itself is only a monotonic true -> false flag, so the assignment below is not
            // why syncRoot exists. The lock is for the Tsavorite 1.1.1 case where ConcurrentReader and
            // pending-completion SingleReader can interleave while both touch Other's session state.
            // As an alternative design, we could prepare another contains/delete session
            // (for example ContainsDeleteSession1) and split reader paths by session.
            if(other.PrivateProtectedContainsKey(ref key)) {
                cursorRecordResult=CursorRecordResult.Accept;
                return true;
            }
            this.IsSubset=false;
            cursorRecordResult=CursorRecordResult.Skip|CursorRecordResult.EndBatch;
            return true;
        }
        public bool SingleReader(ref TSessionKey key,ref TSessionValue value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
            lock(syncRoot) {
                return this.ReaderCore(ref key,ref value,recordMetadata,numberOfRecords,out cursorRecordResult);
            }
        }
        public bool ConcurrentReader(ref TSessionKey key,ref TSessionValue value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
            lock(syncRoot) {
                return this.ReaderCore(ref key,ref value,recordMetadata,numberOfRecords,out cursorRecordResult);
            }
        }
        public bool OnStart(long beginAddress,long endAddress) => true;
        public void OnStop(bool completed,long numberOfRecords) { }
        public void OnException(Exception exception,long numberOfRecords) { }
    }
    private struct ScanIteratorFunctions_Overlaps<TCollection>(TCollection self,object syncRoot):IScanIteratorFunctions<TSessionKey,TSessionValue>
        where TCollection : Collection<
            TKey,
            TValue,
            TSessionKey,
            TSessionValue,
            TReadOutput,
            TAddInput,
            TReadFunctions,
            TAddFunctions,
            TContainsDeleteFunctions,
            TStoreFunctions,
            TAllocator
        > {
        public bool FoundOverlap = false;
        private bool ReaderCore(ref TSessionKey key,ref TSessionValue value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
            if(this.FoundOverlap) {
                Debug.Fail("ここは呼ばれることはないはず");
                //cursorRecordResult=CursorRecordResult.Skip|CursorRecordResult.EndBatch;
                //return true;
            }
            if(self.PrivateProtectedContainsKey(ref key)) {
                this.FoundOverlap=true;
                cursorRecordResult=CursorRecordResult.Skip|CursorRecordResult.EndBatch;
            }
            else {
                cursorRecordResult=CursorRecordResult.Accept;
            }
            return true;
        }
        public bool SingleReader(ref TSessionKey key,ref TSessionValue value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
            lock(syncRoot) {
                return this.ReaderCore(ref key,ref value,recordMetadata,numberOfRecords,out cursorRecordResult);
            }
        }
        public bool ConcurrentReader(ref TSessionKey key,ref TSessionValue value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
            lock(syncRoot) {
                return this.ReaderCore(ref key,ref value,recordMetadata,numberOfRecords,out cursorRecordResult);
            }
        }
        public bool OnStart(long beginAddress,long endAddress) => true;
        public void OnStop(bool completed,long numberOfRecords) { }
        public void OnException(Exception exception,long numberOfRecords) { }
    }
    private protected static bool IsSubsetOfSet<TCollection>(TCollection self,TCollection Other)
        where TCollection : Collection<
            TKey,
            TValue,
            TSessionKey,
            TSessionValue,
            TReadOutput,
            TAddInput,
            TReadFunctions,
            TAddFunctions,
            TContainsDeleteFunctions,
            TStoreFunctions,
            TAllocator
        > {
        ArgumentNullException.ThrowIfNull(Other);
        var ReadSession = self.ReadSession;
        var Iterator = new ScanIteratorFunctions_IsSubsetOf<TCollection>(Other,new object());
        var Cursor = 0L;
        ReadSession.IterateLookup(ref Iterator,ref Cursor,resetCursor: false);
        return Iterator.IsSubset;
    }
    private protected static bool IsSupersetOfSet<TCollection>(TCollection Self,TCollection Other)
        where TCollection : Collection<
            TKey,
            TValue,
            TSessionKey,
            TSessionValue,
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
            TSessionKey,
            TSessionValue,
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
            TSessionKey,
            TSessionValue,
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
    private protected static bool OverlapsSet<TCollection>(TCollection Self,TCollection other)
        where TCollection : Collection<
            TKey,
            TValue,
            TSessionKey,
            TSessionValue,
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
        var Iterator = new ScanIteratorFunctions_Overlaps<TCollection>(Self,new object());
        var Cursor = 0L;
        ReadSession.IterateLookup(ref Iterator,ref Cursor,resetCursor: false);
        ReadSession.BasicContext.CompletePending(wait: true);
        return Iterator.FoundOverlap;
    }
    private protected static bool SetEqualsSet<TCollection>(TCollection Self,TCollection Other)
        where TCollection : Collection<
            TKey,
            TValue,
            TSessionKey,
            TSessionValue,
            TReadOutput,
            TAddInput,
            TReadFunctions,
            TAddFunctions,
            TContainsDeleteFunctions,
            TStoreFunctions,
            TAllocator
        > {
        ArgumentNullException.ThrowIfNull(Other);
        if(Self.GetLongCount()!=Other.GetLongCount())
            return false;
        return IsSubsetOfSet(Self,Other);
    }
}
