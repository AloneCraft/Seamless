using Seamless.Stores;

using System.Diagnostics;
namespace Seamless.Collections;

/// <summary>
/// UnmanagedHashSet の 1.1.1 固有部分。IntersectWith/SymmetricExceptWith 実装とイテレータ。
/// </summary>
public partial class UnmanagedHashSet<TKey>
    where TKey : unmanaged, IEquatable<TKey> {
    private ClientSession<
        TKey,
        EqualityEmpty,
        EqualityEmpty,//TInput
        EqualityEmpty,//TOutput
        EqualityEmpty,//TContext
        SessionFunctions.UnmanagedExceptWith<
            TKey
        >,
        StoreFunctions<
            TKey,
            EqualityEmpty,
            UnmanagedKeyComparer<
                TKey
            >,
            DefaultRecordDisposer<
                TKey,
                EqualityEmpty
            >
        >,
        BlittableAllocator<
            TKey,
            EqualityEmpty,
            StoreFunctions<
                TKey,
                EqualityEmpty,
                UnmanagedKeyComparer<
                    TKey
                >,
                DefaultRecordDisposer<
                    TKey,
                    EqualityEmpty
                >
            >
        >
    > ExceptWith_ClientSession = null!;
    private ClientSession<
        TKey,
        EqualityEmpty,
        EqualityEmpty,//TInput
        EqualityEmpty,//TOutput
        EqualityEmpty,//TContext
        SessionFunctions.UnmanagedIntersectWith<
            TKey
        >,
        StoreFunctions<
            TKey,
            EqualityEmpty,
            UnmanagedKeyComparer<
                TKey
            >,
            DefaultRecordDisposer<
                TKey,
                EqualityEmpty
            >
        >,
        BlittableAllocator<
            TKey,
            EqualityEmpty,
            StoreFunctions<
                TKey
                ,EqualityEmpty,
                UnmanagedKeyComparer<
                    TKey
                >,
                DefaultRecordDisposer<
                    TKey,
                    EqualityEmpty
                >
            >
        >
    > IntersectWith_ClientSession = null!;
    private ClientSession<
        TKey,
        EqualityEmpty,
        EqualityEmpty,//TInput
        EqualityEmpty,//TOutput
        EqualityEmpty,//TContext
        SessionFunctions.UnmanagedSymmetricExceptWith<
            TKey,
            EqualityEmpty
        >,
        StoreFunctions<
            TKey,
            EqualityEmpty,
            UnmanagedKeyComparer<
                TKey
            >,
            DefaultRecordDisposer<
                TKey,
                EqualityEmpty
            >
        >,
        BlittableAllocator<
            TKey,
            EqualityEmpty,
            StoreFunctions<
                TKey,
                EqualityEmpty,
                UnmanagedKeyComparer<
                    TKey
                >,
                DefaultRecordDisposer<
                    TKey,
                    EqualityEmpty
                >
            >
        >
    > SymmetricExceptWith_ClientSession = null!;
    internal UnmanagedHashSet(UnmanagedCollectionStore<TKey,EqualityEmpty> CollectionStore) : base(CollectionStore) {
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

            EqualityEmpty,
            EqualityEmpty,
            EqualityEmpty,
            SessionFunctions.UnmanagedExceptWith<
                TKey
            >
        >(new());
        this.IntersectWith_ClientSession=Store.NewSession<

            EqualityEmpty,
            EqualityEmpty,
            EqualityEmpty,
            SessionFunctions.UnmanagedIntersectWith<
                TKey
            >
        >(new());
        this.SymmetricExceptWith_ClientSession=Store.NewSession<

            EqualityEmpty,
            EqualityEmpty,
            EqualityEmpty,
            SessionFunctions.UnmanagedSymmetricExceptWith<
                TKey,
                EqualityEmpty
            >
        >(new());
    }
    public Enumerator<
        KeyIterator,
        TKey
    > GetEnumerator() => new(this.ReadSession,new());
    IEnumerator<TKey> IMemoryCollection<TKey>.GetEnumerator() => new Enumerator<KeyIterator,TKey>(this.ReadMemoryClientSession,new());
    private readonly struct ScanIteratorFunctions_ExceptWith(
        ClientSession<
            TKey,
            EqualityEmpty,
            EqualityEmpty,
            EqualityEmpty,
            EqualityEmpty,
            SessionFunctions.UnmanagedExceptWith<
                TKey
            >,
            StoreFunctions<
                TKey,
                EqualityEmpty,
                UnmanagedKeyComparer<
                    TKey
                >,
                DefaultRecordDisposer<
                    TKey,
                    EqualityEmpty
                >
            >,
            BlittableAllocator<
                TKey,
                EqualityEmpty,
                StoreFunctions<
                    TKey,
                    EqualityEmpty,
                    UnmanagedKeyComparer<
                        TKey
                    >,
                    DefaultRecordDisposer<
                        TKey,
                        EqualityEmpty
                    >
                >
            >
        > Session
        ,object syncRoot
    ):IScanIteratorFunctions<TKey,EqualityEmpty> {
        private bool ReaderCore(ref TKey key,ref EqualityEmpty value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
            Session.BasicContext.Delete(ref key);
            cursorRecordResult=CursorRecordResult.Accept;
            return true;
        }
        public bool SingleReader(ref TKey key,ref EqualityEmpty value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
            lock(syncRoot) {
                return this.ReaderCore(ref key,ref value,recordMetadata,numberOfRecords,out cursorRecordResult);
            }
        }
        public bool ConcurrentReader(ref TKey key,ref EqualityEmpty value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
            lock(syncRoot) {
                return this.ReaderCore(ref key,ref value,recordMetadata,numberOfRecords,out cursorRecordResult);
            }
        }
        public bool OnStart(long beginAddress,long endAddress) => true;
        public void OnStop(bool completed,long numberOfRecords) { }
        public void OnException(Exception exception,long numberOfRecords) { }
    }
    private readonly struct ScanIteratorFunctions_IntersectWith(
        UnmanagedHashSet<TKey> other,
        ClientSession<
            TKey,
            EqualityEmpty,
            EqualityEmpty,
            EqualityEmpty,
            EqualityEmpty,
            SessionFunctions.UnmanagedIntersectWith<
                TKey
            >,
            StoreFunctions<
                TKey,
                EqualityEmpty,
                UnmanagedKeyComparer<
                    TKey
                >,
                DefaultRecordDisposer<
                    TKey,
                    EqualityEmpty
                >
            >,
            BlittableAllocator<
                TKey,
                EqualityEmpty,
                StoreFunctions<
                    TKey,
                    EqualityEmpty,
                    UnmanagedKeyComparer<
                        TKey
                    >,
                    DefaultRecordDisposer<
                        TKey,
                        EqualityEmpty
                    >
                >
            >
        > Session
        ,object syncRoot
    ):IScanIteratorFunctions<TKey,EqualityEmpty> {
        private bool ReaderCore(ref TKey key,ref EqualityEmpty value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
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
        public bool SingleReader(ref TKey key,ref EqualityEmpty value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
            lock(syncRoot) {
                return this.ReaderCore(ref key,ref value,recordMetadata,numberOfRecords,out cursorRecordResult);
            }
        }
        public bool ConcurrentReader(ref TKey key,ref EqualityEmpty value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
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
            TKey,
            EqualityEmpty,
            EqualityEmpty,
            EqualityEmpty,
            EqualityEmpty,
            SessionFunctions.UnmanagedSymmetricExceptWith<
                TKey,
                EqualityEmpty
            >,
            StoreFunctions<
                TKey,
                EqualityEmpty,
                UnmanagedKeyComparer<
                    TKey
                >,
                DefaultRecordDisposer<
                    TKey,
                    EqualityEmpty
                >
            >,
            BlittableAllocator<
                TKey,
                EqualityEmpty,
                StoreFunctions<
                    TKey,
                    EqualityEmpty,
                    UnmanagedKeyComparer<
                        TKey
                    >,
                    DefaultRecordDisposer<
                        TKey,
                        EqualityEmpty
                    >
                >
            >
        > Session
        ,object syncRoot
    ):IScanIteratorFunctions<TKey,EqualityEmpty> {
        private bool ReaderCore(ref TKey key,ref EqualityEmpty value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
            //ここでif(this.self.WouldOverflowMutable(key.Length,1))
            //    throw this.self.ThrowLogFull();
            var input = default(EqualityEmpty);
            var output = default(EqualityEmpty);
            Session.BasicContext.RMW(ref key,ref input,ref output);
            cursorRecordResult=CursorRecordResult.Accept;
            return true;
        }
        public bool SingleReader(ref TKey key,ref EqualityEmpty value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
            lock(syncRoot) {
                return this.ReaderCore(ref key,ref value,recordMetadata,numberOfRecords,out cursorRecordResult);
            }
        }
        public bool ConcurrentReader(ref TKey key,ref EqualityEmpty value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
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
            TKey,
            EqualityEmpty,
            EqualityEmpty,
            bool,
            EqualityEmpty,
            SessionFunctions.UnmanagedAdd<
                TKey,
                EqualityEmpty
            >,
            StoreFunctions<
                TKey,
                EqualityEmpty,
                UnmanagedKeyComparer<
                    TKey
                >,
                DefaultRecordDisposer<
                    TKey,
                    EqualityEmpty
                >
            >,
            BlittableAllocator<
                TKey,
                EqualityEmpty,
                StoreFunctions<
                    TKey,
                    EqualityEmpty,
                    UnmanagedKeyComparer<
                        TKey
                    >,
                    DefaultRecordDisposer<
                        TKey,
                        EqualityEmpty
                    >
                >
            >
        > Session
        ,object syncRoot
    ):IScanIteratorFunctions<TKey,EqualityEmpty> {
        private bool ReaderCore(ref TKey key,ref EqualityEmpty value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
            var input = default(EqualityEmpty);
            var output = false;
            Session.BasicContext.RMW(ref key,ref input,ref output);
            cursorRecordResult=CursorRecordResult.Accept;
            return true;
        }
        public bool SingleReader(ref TKey key,ref EqualityEmpty value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
            lock(syncRoot) {
                return this.ReaderCore(ref key,ref value,recordMetadata,numberOfRecords,out cursorRecordResult);
            }
        }
        public bool ConcurrentReader(ref TKey key,ref EqualityEmpty value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
            lock(syncRoot) {
                return this.ReaderCore(ref key,ref value,recordMetadata,numberOfRecords,out cursorRecordResult);
            }
        }
        public bool OnStart(long beginAddress,long endAddress) => true;
        public void OnStop(bool completed,long numberOfRecords) { }
        public void OnException(Exception exception,long numberOfRecords) { }
    }
    public partial void ExceptWith(UnmanagedHashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        var ReadSession = other.ReadSession;
        var Iterator = new ScanIteratorFunctions_ExceptWith(this.ExceptWith_ClientSession,new object());
        var Cursor = 0L;
        ReadSession.IterateLookup(ref Iterator,ref Cursor,resetCursor: false);
        ReadSession.BasicContext.CompletePending(wait: true);
        this.ExceptWith_ClientSession.BasicContext.CompletePending(wait: true);
    }
    public partial void IntersectWith(UnmanagedHashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        var ReadSession = this.ReadSession;
        var Iterator = new ScanIteratorFunctions_IntersectWith(other,this.IntersectWith_ClientSession,new object());
        var Cursor = 0L;
        ReadSession.IterateLookup(ref Iterator,ref Cursor,resetCursor: false);
        ReadSession.BasicContext.CompletePending(wait: true);
        this.IntersectWith_ClientSession.BasicContext.CompletePending(wait: true);
    }
    public partial bool IsSubsetOf(UnmanagedHashSet<TKey> other) => IsSubsetOfSet(this,other);
    public partial bool IsSupersetOf(UnmanagedHashSet<TKey> other) => IsSupersetOfSet(this,other);
    public partial bool IsProperSubsetOf(UnmanagedHashSet<TKey> other) => IsProperSubsetOfSet(this,other);
    public partial bool IsProperSupersetOf(UnmanagedHashSet<TKey> other) => IsProperSupersetOfSet(this,other);
    public partial bool Overlaps(UnmanagedHashSet<TKey> other) => OverlapsSet(this,other);
    public partial bool SetEquals(UnmanagedHashSet<TKey> other) => SetEqualsSet(this,other);
    public partial void SymmetricExceptWith(UnmanagedHashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        var ReadSession = other.ReadSession;
        var Iterator = new ScanIteratorFunctions_SymmetricExceptWith(this.SymmetricExceptWith_ClientSession,new object());
        var Cursor = 0L;
        ReadSession.IterateLookup(ref Iterator,ref Cursor,resetCursor: false);
        ReadSession.BasicContext.CompletePending(wait: true);
        this.SymmetricExceptWith_ClientSession.BasicContext.CompletePending(wait: true);
    }
    public partial void UnionWith(UnmanagedHashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        var ReadSession = other.ReadSession;
        var Iterator = new ScanIteratorFunctions_UnionWith(this.Add_ClientSession,new object());
        var Cursor = 0L;
        ReadSession.IterateLookup(ref Iterator,ref Cursor,resetCursor: false);
        ReadSession.BasicContext.CompletePending(wait: true);
        this.Add_ClientSession.BasicContext.CompletePending(wait: true);
    }
}
