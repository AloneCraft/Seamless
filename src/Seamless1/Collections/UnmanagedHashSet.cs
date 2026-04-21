using System.Runtime.CompilerServices;
using Seamless.SessionFunctions;
using Seamless.Stores;
namespace Seamless.Collections;

/// <summary>
/// UnmanagedHashSet の 1.1.1 固有部分。IntersectWith/SymmetricExceptWith 実装とイテレータ。
/// </summary>
public partial class UnmanagedHashSet<TKey>
    where TKey : unmanaged, IEquatable<TKey> {
    private readonly ClientSession<
        TKey,
        EqualityEmpty,
        EqualityEmpty,//TInput
        EqualityEmpty,//TOutput
        EqualityEmpty,//TContext
        UnmanagedIntersectWith<
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
    > IntersectWith_ClientSession;
    private readonly ClientSession<
        TKey,
        EqualityEmpty,
        EqualityEmpty,//TInput
        EqualityEmpty,//TOutput
        EqualityEmpty,//TContext
        UnmanagedSymmetricExceptWith<
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
    > SymmetricExceptWith_ClientSession;
    internal UnmanagedHashSet(UnmanagedCollectionStore<TKey,EqualityEmpty> CollectionStore) : base(CollectionStore) {
        var Store = CollectionStore.Store;
        this.IntersectWith_ClientSession=Store.NewSession<

            EqualityEmpty,
            EqualityEmpty,
            EqualityEmpty,
            UnmanagedIntersectWith<
                TKey
            >
        >(new());
        this.SymmetricExceptWith_ClientSession=Store.NewSession<

            EqualityEmpty,
            EqualityEmpty,
            EqualityEmpty,
            UnmanagedSymmetricExceptWith<
                TKey,
                EqualityEmpty
            >
        >(new());
    }
    public Enumerator<
        KeyIterator,
        TKey
    > GetEnumerator() => new(this.ReadSession,new());
    IEnumerator<TKey> IMemoryCollection<TKey>.GetEnumerator() => new Enumerator<KeyIterator,TKey>(this.ReadMemoryClientSession,new(),this.HeadAddress);
    private readonly struct ExceptWithFunctions(
        ClientSession<
            TKey,
            EqualityEmpty,
            EqualityEmpty,
            bool,
            EqualityEmpty,
            UnmanagedContainsDelete<TKey,EqualityEmpty>,
            StoreFunctions<TKey,EqualityEmpty,UnmanagedKeyComparer<TKey>,DefaultRecordDisposer<TKey,EqualityEmpty>>,
            BlittableAllocator<TKey,EqualityEmpty,StoreFunctions<TKey,EqualityEmpty,UnmanagedKeyComparer<TKey>,DefaultRecordDisposer<TKey,EqualityEmpty>>>
        > Session
    ):IScanIteratorFunctions<TKey,EqualityEmpty> {
        public bool SingleReader(ref TKey key,ref EqualityEmpty value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
            Session.BasicContext.Upsert(ref key,ref value);
            cursorRecordResult=CursorRecordResult.Accept;
            return true;
        }
        public bool ConcurrentReader(ref TKey key,ref EqualityEmpty value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult)
            => this.SingleReader(ref key,ref value,recordMetadata,numberOfRecords,out cursorRecordResult);
        public bool OnStart(long beginAddress,long endAddress) => true;
        public void OnStop(bool completed,long numberOfRecords) { }
        public void OnException(Exception exception,long numberOfRecords) { }
    }
    private struct IntersectWithFunctions(
        UnmanagedHashSet<TKey> other,
        ClientSession<
            TKey,
            EqualityEmpty,
            EqualityEmpty,
            EqualityEmpty,
            EqualityEmpty,
            UnmanagedIntersectWith<TKey>,
            StoreFunctions<TKey,EqualityEmpty,UnmanagedKeyComparer<TKey>,DefaultRecordDisposer<TKey,EqualityEmpty>>,
            BlittableAllocator<TKey,EqualityEmpty,StoreFunctions<TKey,EqualityEmpty,UnmanagedKeyComparer<TKey>,DefaultRecordDisposer<TKey,EqualityEmpty>>>
        > Session,
        int RemainingCount
    ):IScanIteratorFunctions<TKey,EqualityEmpty> {
        public bool SingleReader(ref TKey key,ref EqualityEmpty value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
            if(RemainingCount<=0) {
                cursorRecordResult=CursorRecordResult.Skip|CursorRecordResult.EndBatch|CursorRecordResult.RetryLastRecord;
                return true;
            }
            RemainingCount--;
            if(other.PrivateProtectedContainsKey(ref key)) {
            }
            else {
                var input = default(EqualityEmpty);
                var output = default(EqualityEmpty);
                var Status = Session.BasicContext.RMW(ref key,ref input,ref output);
                var Output = default(EqualityEmpty);
                Pending(Session,Status,ref Output);
            }
            cursorRecordResult=CursorRecordResult.Accept;
            return true;
        }
        public bool ConcurrentReader(ref TKey key,ref EqualityEmpty value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult)
            => this.SingleReader(ref key,ref value,recordMetadata,numberOfRecords,out cursorRecordResult);
        public bool OnStart(long beginAddress,long endAddress) => true;
        public void OnStop(bool completed,long numberOfRecords) { }
        public void OnException(Exception exception,long numberOfRecords) { }
    }
    private struct IntersectWithFunctions0(
    ):IScanIteratorFunctions<TKey,EqualityEmpty> {
        public bool SingleReader(ref TKey key,ref EqualityEmpty value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
            cursorRecordResult=CursorRecordResult.Accept;
            return true;
        }
        public bool ConcurrentReader(ref TKey key,ref EqualityEmpty value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult)
            => this.SingleReader(ref key,ref value,recordMetadata,numberOfRecords,out cursorRecordResult);
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
            UnmanagedSymmetricExceptWith<TKey,EqualityEmpty>,
            StoreFunctions<TKey,EqualityEmpty,UnmanagedKeyComparer<TKey>,DefaultRecordDisposer<TKey,EqualityEmpty>>,
            BlittableAllocator<TKey,EqualityEmpty,StoreFunctions<TKey,EqualityEmpty,UnmanagedKeyComparer<TKey>,DefaultRecordDisposer<TKey,EqualityEmpty>>>
        > Session
    ):IScanIteratorFunctions<TKey,EqualityEmpty> {
        public bool SingleReader(ref TKey key,ref EqualityEmpty value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
            //ここでif(this.self.WouldOverflowMutable(key.Length,1))
            //    throw this.self.ThrowLogFull();
            var input = default(EqualityEmpty);
            var output = default(EqualityEmpty);
            Session.BasicContext.RMW(ref key,ref input,ref output);
            cursorRecordResult=CursorRecordResult.Accept;
            return true;
        }
        public bool ConcurrentReader(ref TKey key,ref EqualityEmpty value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult)
            => this.SingleReader(ref key,ref value,recordMetadata,numberOfRecords,out cursorRecordResult);
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
            UnmanagedAdd<TKey,EqualityEmpty>,
            StoreFunctions<TKey,EqualityEmpty,UnmanagedKeyComparer<TKey>,DefaultRecordDisposer<TKey,EqualityEmpty>>,
            BlittableAllocator<TKey,EqualityEmpty,StoreFunctions<TKey,EqualityEmpty,UnmanagedKeyComparer<TKey>,DefaultRecordDisposer<TKey,EqualityEmpty>>>
        > Session
    ):IScanIteratorFunctions<TKey,EqualityEmpty> {
        public bool SingleReader(ref TKey key,ref EqualityEmpty value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
            var input = default(EqualityEmpty);
            var output = false;
            Session.BasicContext.RMW(ref key,ref input,ref output);
            cursorRecordResult=CursorRecordResult.Accept;
            return true;
        }
        public bool ConcurrentReader(ref TKey key,ref EqualityEmpty value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult)
            => this.SingleReader(ref key,ref value,recordMetadata,numberOfRecords,out cursorRecordResult);
        public bool OnStart(long beginAddress,long endAddress) => true;
        public void OnStop(bool completed,long numberOfRecords) { }
        public void OnException(Exception exception,long numberOfRecords) { }
    }
    public partial void ExceptWith(UnmanagedHashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        var ReadSession = other.ReadSession;
        var ContainsDeleteSession = this.ContainsDeleteSession;
        var cursor = 0L;
        var Iterator = new ExceptWithFunctions(ContainsDeleteSession);
        ReadSession.IterateLookup(ref Iterator,ref cursor,resetCursor: false);
        ReadSession.BasicContext.CompletePending(wait: true);
        ContainsDeleteSession.BasicContext.CompletePending(wait: true);
    }
    public void 動作確認(UnmanagedHashSet<TKey> other) {
        var ReadSession = other.ReadSession;
        var IntersectWithSession = this.IntersectWith_ClientSession;
        foreach(var a in other) {

        }
        var cursor = 0L;
        while(true) {
            var Iterator = new KeyIterator();
            var 最後まで読み切ったか = ReadSession.IterateLookup(ref Iterator,ref cursor,resetCursor: false);
            ReadSession.BasicContext.CompletePending(wait: true);
            IntersectWithSession.BasicContext.CompletePending(wait: true);

            var Count = Iterator.Count;
            // this.Count=Count;
            // if(最後まで読み切ったか) {
            // }
            // else {
            //     this.Done=true;
            // }
            // if(Count>0) {
            //     this.Index=0;
            //     return true;
            // }
            // this.Index=-1;



            if(最後まで読み切ったか) {
                return;
            }
        }
    }
    public partial void IntersectWith(UnmanagedHashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        //this.動作確認(other);
        var cursor = 0L;
        var ReadSession = other.ReadSession;
        var IntersectWithSession = this.IntersectWith_ClientSession;
        var Iterator = new IntersectWithFunctions(this,IntersectWithSession,SetOperationBatchSize);
        //var Iterator = new IntersectWithFunctions0();
        //var Iterator = new KeyIterator();
        var 最後まで読み切ったか = ReadSession.IterateLookup(ref Iterator,ref cursor,resetCursor: false);
        ReadSession.BasicContext.CompletePending(wait: true);
        IntersectWithSession.BasicContext.CompletePending(wait: true);
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
        var cursor = 0L;
        var Iterator = new ScanIteratorFunctions_SymmetricExceptWith(this.SymmetricExceptWith_ClientSession);
        var 最後まで読み切ったか = ReadSession.IterateLookup(ref Iterator,ref cursor,resetCursor: false);
        ReadSession.BasicContext.CompletePending(wait: true);
        this.SymmetricExceptWith_ClientSession.BasicContext.CompletePending(wait: true);
    }
    public partial void UnionWith(UnmanagedHashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        var ReadSession = other.ReadSession;
        var cursor = 0L;
        var Iterator = new ScanIteratorFunctions_UnionWith(this.Add_ClientSession);
        var 最後まで読み切ったか = ReadSession.IterateLookup(ref Iterator,ref cursor,resetCursor: false);
        ReadSession.BasicContext.CompletePending(wait: true);
        this.Add_ClientSession.BasicContext.CompletePending(wait: true);
    }
}
