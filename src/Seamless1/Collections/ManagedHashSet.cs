using Seamless.Stores;
namespace Seamless.Collections;
/// <summary>
/// Tsavorite をバックエンドに使うオンメモリ HashSet。
/// <see cref="System.Collections.Generic.ISet{T}"/> を実装し、利用者に Tsavorite の詳細を見せない。
/// 重複を許さず、キーのみを格納する。値は EqualityEmpty 構造体を使用する。
/// </summary>
public partial class ManagedHashSet<TKey> {
    private ClientSession<
        SpanByte,//TKey
        SpanByte,//TValue
        EqualityEmpty,//TInput
        EqualityEmpty,//TOutput
        EqualityEmpty,//TContext
        SessionFunctions.ExceptWith,
        ManagedStoreFunctions,
        ManagedAllocator
    > ExceptWith_ClientSession = null!;
    private ClientSession<
        SpanByte,//TKey
        SpanByte,//TValue
        EqualityEmpty,//TInput
        EqualityEmpty,//TOutput
        EqualityEmpty,//TContext
        SessionFunctions.ManagedIntersectWith,
        ManagedStoreFunctions,
        ManagedAllocator
    > IntersectWith_ClientSession = null!;
    private ClientSession<
        SpanByte,//TKey
        SpanByte,//TValue
        SpanByte,//TInput
        EqualityEmpty,//TOutput
        EqualityEmpty,//TContext
        SessionFunctions.SymmetricExceptWith,
        ManagedStoreFunctions,
        ManagedAllocator
    > SymmetricExceptWith_ClientSession = null!;
    internal ManagedHashSet(ManagedCollectionStore<TKey,EqualityEmpty> CollectionStore,IDisposable? ownedStore = null) : base(CollectionStore) {
        this.ownedStore=ownedStore;
        this.CreateAdditionalSessions();
    }
    private protected override void DisposeAdditionalSessions() {
        CompletePendingAndDispose(this.ExceptWith_ClientSession);
        CompletePendingAndDispose(this.IntersectWith_ClientSession);
        CompletePendingAndDispose(this.SymmetricExceptWith_ClientSession);
    }
    private protected override void RecreateAdditionalSessions() {
        this.CreateAdditionalSessions();
    }
    private void CreateAdditionalSessions() {
        var Store = this.Store;
        this.ExceptWith_ClientSession=Store.NewSession<
            EqualityEmpty,//TInput
            EqualityEmpty,//TOutput
            EqualityEmpty,//TContext
            SessionFunctions.ExceptWith//TFunctions
        >(new());
        this.IntersectWith_ClientSession=Store.NewSession<
            EqualityEmpty,//TKey
                          //
            EqualityEmpty,//TOutput
            EqualityEmpty,//TContext
            SessionFunctions.ManagedIntersectWith//TFunctions
        >(new());
        this.SymmetricExceptWith_ClientSession=Store.NewSession<
            SpanByte,//TKey
                     //
            EqualityEmpty,//TOutput
            EqualityEmpty,//TContext
            SessionFunctions.SymmetricExceptWith//TFunctions
        >(new());
    }
    public ManagedList<TKey>.Enumerator GetEnumerator() {
        var items = new InMemoryList<TKey>(Const.Chunk_Length);
        var iterator = new KeyIterator();
        iterator.SetList(items);
        this.IterateLookupAll(this.ReadSession,ref iterator);
        return new ManagedList<TKey>.Enumerator(items,ownsList: true);
    }

    IEnumerator<TKey> IMemoryCollection<TKey>.GetEnumerator() {
        var items = new InMemoryList<TKey>(Const.Chunk_Length);
        var iterator = new KeyIterator();
        iterator.SetList(items);
        this.IterateLookupAll(this.ReadMemoryClientSession,ref iterator);
        return new ManagedList<TKey>.Enumerator(items,ownsList: true);
    }
    private readonly struct ScanIteratorFunctions_ExceptWith(
        ClientSession<
            SpanByte,
            SpanByte,
            EqualityEmpty,
            EqualityEmpty,
            EqualityEmpty,
            SessionFunctions.ExceptWith,
            ManagedStoreFunctions,
            ManagedAllocator
        > Session,
        object syncRoot
    ):IScanIteratorFunctions<SpanByte,SpanByte> {
        private bool ReaderCore(ref SpanByte key,ref SpanByte value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
            Session.BasicContext.Delete(ref key);
            cursorRecordResult=CursorRecordResult.Accept;
            return true;
        }
        public bool SingleReader(ref SpanByte key,ref SpanByte value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
            lock(syncRoot) {
                return this.ReaderCore(ref key,ref value,recordMetadata,numberOfRecords,out cursorRecordResult);
            }
        }
        public bool ConcurrentReader(ref SpanByte key,ref SpanByte value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
            lock(syncRoot) {
                return this.ReaderCore(ref key,ref value,recordMetadata,numberOfRecords,out cursorRecordResult);
            }
        }
        public bool OnStart(long beginAddress,long endAddress) => true;
        public void OnStop(bool completed,long numberOfRecords) { }
        public void OnException(Exception exception,long numberOfRecords) { }
    }
    private readonly struct ScanIteratorFunctions_IntersectWith(
        ManagedHashSet<TKey> other,
        ClientSession<
            SpanByte,
            SpanByte,
            EqualityEmpty,
            EqualityEmpty,
            EqualityEmpty,
            SessionFunctions.ManagedIntersectWith,
            ManagedStoreFunctions,
            ManagedAllocator
        > Session,
        object syncRoot
    ):IScanIteratorFunctions<SpanByte,SpanByte> {
        private bool ReaderCore(ref SpanByte key,ref SpanByte value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
            if(other.PrivateProtectedContainsKey(ref key)) {
            }
            else {
                var input = default(EqualityEmpty);
                var output = default(EqualityEmpty);
                Session.BasicContext.RMW(ref key,ref input,ref output);
            }
            cursorRecordResult=CursorRecordResult.Accept;
            return true;
        }
        public bool SingleReader(ref SpanByte key,ref SpanByte value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
            lock(syncRoot) {
                return this.ReaderCore(ref key,ref value,recordMetadata,numberOfRecords,out cursorRecordResult);
            }
        }
        public bool ConcurrentReader(ref SpanByte key,ref SpanByte value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
            lock(syncRoot) {
                return this.ReaderCore(ref key,ref value,recordMetadata,numberOfRecords,out cursorRecordResult);
            }
        }
        public bool OnStart(long beginAddress,long endAddress) => true;
        public void OnStop(bool completed,long numberOfRecords) { }
        public void OnException(Exception exception,long numberOfRecords) { }
    }
    private readonly struct ScanIteratorFunctions_SymmetricExceptWith(
        ClientSession<
            SpanByte,
            SpanByte,
            SpanByte,
            EqualityEmpty,
            EqualityEmpty,
            SessionFunctions.SymmetricExceptWith,
            ManagedStoreFunctions,
            ManagedAllocator
        > Session,
        object syncRoot
    ):IScanIteratorFunctions<SpanByte,SpanByte> {
        private bool ReaderCore(ref SpanByte key,ref SpanByte value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
            var input = default(SpanByte);
            var output = default(EqualityEmpty);
            Session.BasicContext.RMW(ref key,ref input,ref output);
            cursorRecordResult=CursorRecordResult.Accept;
            return true;
        }
        public bool SingleReader(ref SpanByte key,ref SpanByte value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
            lock(syncRoot) {
                return this.ReaderCore(ref key,ref value,recordMetadata,numberOfRecords,out cursorRecordResult);
            }
        }
        public bool ConcurrentReader(ref SpanByte key,ref SpanByte value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
            lock(syncRoot) {
                return this.ReaderCore(ref key,ref value,recordMetadata,numberOfRecords,out cursorRecordResult);
            }
        }
        public bool OnStart(long beginAddress,long endAddress) => true;
        public void OnStop(bool completed,long numberOfRecords) { }
        public void OnException(Exception exception,long numberOfRecords) { }
    }
    private readonly struct ScanIteratorFunctions_UnionWith(
        ClientSession<
            SpanByte,
            SpanByte,
            SpanByte,
            bool,
            EqualityEmpty,
            SessionFunctions.ManagedAdd,
            ManagedStoreFunctions,
            ManagedAllocator
        > Session,
        object syncRoot
    ):IScanIteratorFunctions<SpanByte,SpanByte> {
        private bool ReaderCore(ref SpanByte key,ref SpanByte value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
            Session.BasicContext.Upsert(ref key,ref value);
            cursorRecordResult=CursorRecordResult.Accept;
            return true;
        }
        public bool SingleReader(ref SpanByte key,ref SpanByte value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
            lock(syncRoot) {
                return this.ReaderCore(ref key,ref value,recordMetadata,numberOfRecords,out cursorRecordResult);
            }
        }
        public bool ConcurrentReader(ref SpanByte key,ref SpanByte value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
            lock(syncRoot) {
                return this.ReaderCore(ref key,ref value,recordMetadata,numberOfRecords,out cursorRecordResult);
            }
        }
        public bool OnStart(long beginAddress,long endAddress) => true;
        public void OnStop(bool completed,long numberOfRecords) { }
        public void OnException(Exception exception,long numberOfRecords) { }
    }
    public partial void ExceptWith(ManagedHashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        var ReadSession = other.ReadSession;
        var Iterator = new ScanIteratorFunctions_ExceptWith(this.ExceptWith_ClientSession,new object());
        var Cursor = 0L;
        ReadSession.IterateLookup(ref Iterator,ref Cursor,resetCursor: false);
        ReadSession.BasicContext.CompletePending(wait: true);
        this.ExceptWith_ClientSession.BasicContext.CompletePending(wait: true);
    }
    public partial void IntersectWith(ManagedHashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        var ReadSession = this.ReadSession;
        var Iterator = new ScanIteratorFunctions_IntersectWith(other,this.IntersectWith_ClientSession,new object());
        var Cursor = 0L;
        ReadSession.IterateLookup(ref Iterator,ref Cursor,resetCursor: false);
        ReadSession.BasicContext.CompletePending(wait: true);
        this.IntersectWith_ClientSession.BasicContext.CompletePending(wait: true);
    }
    public partial bool IsProperSubsetOf(ManagedHashSet<TKey> other) => IsProperSubsetOfSet(this,other);
    public partial bool IsProperSupersetOf(ManagedHashSet<TKey> other) => IsProperSupersetOfSet(this,other);
    public partial bool IsSubsetOf(ManagedHashSet<TKey> other) => IsSubsetOfSet(this,other);
    public partial bool IsSupersetOf(ManagedHashSet<TKey> other) => IsSupersetOfSet(this,other);
    public partial bool Overlaps(ManagedHashSet<TKey> other) => OverlapsSet(this,other);
    public partial bool SetEquals(ManagedHashSet<TKey> other) => SetEqualsSet(this,other);
    public partial void SymmetricExceptWith(ManagedHashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        var ReadSession = other.ReadSession;
        var Iterator = new ScanIteratorFunctions_SymmetricExceptWith(this.SymmetricExceptWith_ClientSession,new object());
        var Cursor = 0L;
        ReadSession.IterateLookup(ref Iterator,ref Cursor,resetCursor: false);
        ReadSession.BasicContext.CompletePending(wait: true);
        this.SymmetricExceptWith_ClientSession.BasicContext.CompletePending(wait: true);
    }
    public partial void UnionWith(ManagedHashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        var ReadSession = other.ReadSession;
        var Iterator = new ScanIteratorFunctions_UnionWith(this.Add_ClientSession,new object());
        var Cursor = 0L;
        ReadSession.IterateLookup(ref Iterator,ref Cursor,resetCursor: false);
        ReadSession.BasicContext.CompletePending(wait: true);
        this.Add_ClientSession.BasicContext.CompletePending(wait: true);
    }
}
