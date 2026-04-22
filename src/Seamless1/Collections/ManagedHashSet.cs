//using Seamless.SessionFunctions;
using Seamless.Stores;
namespace Seamless.Collections;
/// <summary>
/// Tsavorite をバックエンドに使うオンメモリ HashSet。
/// <see cref="ISet{T}"/> を実装し、利用者に Tsavorite の詳細を見せない。
/// 重複を許さず、キーのみを格納する。値は EqualityEmpty 構造体を使用する。
/// </summary>
public partial class ManagedHashSet<TKey>
    where TKey : IEquatable<TKey> {
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
    internal ManagedHashSet(ManagedCollectionStore<TKey,EqualityEmpty> CollectionStore) : base(CollectionStore) {
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
    public Enumerator<
        KeyIterator,
        TKey
    > GetEnumerator() => new(this.ReadSession,new());
    IEnumerator<TKey> IMemoryCollection<TKey>.GetEnumerator() => new Enumerator<KeyIterator,TKey>(this.ReadMemoryClientSession,new(),this.HeadAddress);
    private readonly struct ExceptWithScanIteratorFunctions:IScanIteratorFunctions<SpanByte,SpanByte> {
        private readonly ClientSession<
            SpanByte,
            SpanByte,
            EqualityEmpty,
            EqualityEmpty,
            EqualityEmpty,
            SessionFunctions.ExceptWith,
            ManagedStoreFunctions,
            ManagedAllocator
        > session;

        public ExceptWithScanIteratorFunctions(
            ClientSession<
                SpanByte,
                SpanByte,
                EqualityEmpty,
                EqualityEmpty,
                EqualityEmpty,
                SessionFunctions.ExceptWith,
                ManagedStoreFunctions,
                ManagedAllocator
            > session
        ) {
            this.session=session;
        }

        public bool SingleReader(ref SpanByte key,ref SpanByte value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
            this.session.BasicContext.Delete(ref key);
            cursorRecordResult=CursorRecordResult.Accept;
            return true;
        }
        public bool ConcurrentReader(ref SpanByte key,ref SpanByte value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult)
           => this.SingleReader(ref key,ref value,recordMetadata,numberOfRecords,out cursorRecordResult);
        public bool OnStart(long beginAddress,long endAddress) => true;
        public void OnStop(bool completed,long numberOfRecords) { }
        public void OnException(Exception exception,long numberOfRecords) { }
    }
    private struct IntersectWithScanIteratorFunctions:IScanIteratorFunctions<SpanByte,SpanByte> {
        private readonly ManagedHashSet<TKey> other;
        private readonly ClientSession<
            SpanByte,
            SpanByte,
            EqualityEmpty,
            EqualityEmpty,
            EqualityEmpty,
            SessionFunctions.ManagedIntersectWith,
            ManagedStoreFunctions,
            ManagedAllocator
        > session;
        private int remainingCount;

        public IntersectWithScanIteratorFunctions(
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
            > session,
            int remainingCount
        ) {
            this.other=other;
            this.session=session;
            this.remainingCount=remainingCount;
        }

        public bool SingleReader(ref SpanByte key,ref SpanByte value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
            if(this.remainingCount<=0) {
                cursorRecordResult=CursorRecordResult.Skip|CursorRecordResult.EndBatch|CursorRecordResult.RetryLastRecord;
                return true;
            }
            this.remainingCount--;
            if(this.other.PrivateProtectedContainsKey(ref key)) {
            }
            else {
                var input = default(EqualityEmpty);
                var output = default(EqualityEmpty);
                this.session.BasicContext.RMW(ref key,ref input,ref output);
            }
            cursorRecordResult=CursorRecordResult.Accept;
            return true;
        }
        public bool ConcurrentReader(ref SpanByte key,ref SpanByte value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult)
           => this.SingleReader(ref key,ref value,recordMetadata,numberOfRecords,out cursorRecordResult);
        public bool OnStart(long beginAddress,long endAddress) => true;
        public void OnStop(bool completed,long numberOfRecords) { }
        public void OnException(Exception exception,long numberOfRecords) { }
    }
    private struct SymmetricExceptWithScanIteratorFunctions:IScanIteratorFunctions<SpanByte,SpanByte> {
        private readonly ManagedHashSet<TKey> self;
        private readonly ClientSession<
            SpanByte,
            SpanByte,
            SpanByte,
            EqualityEmpty,
            EqualityEmpty,
            SessionFunctions.SymmetricExceptWith,
            ManagedStoreFunctions,
            ManagedAllocator
        > session;
        private int remainingCount;

        public SymmetricExceptWithScanIteratorFunctions(
            ManagedHashSet<TKey> self,
            ClientSession<
                SpanByte,
                SpanByte,
                SpanByte,
                EqualityEmpty,
                EqualityEmpty,
                SessionFunctions.SymmetricExceptWith,
                ManagedStoreFunctions,
                ManagedAllocator
            > session,
            int remainingCount
        ) {
            this.self=self;
            this.session=session;
            this.remainingCount=remainingCount;
        }

        public bool SingleReader(ref SpanByte key,ref SpanByte value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
            if(this.remainingCount<=0) {
                cursorRecordResult=CursorRecordResult.Skip|CursorRecordResult.EndBatch|CursorRecordResult.RetryLastRecord;
                return true;
            }
            this.remainingCount--;
            if(this.self.WouldOverflowMutable(key.Length,1))
                throw this.self.ThrowLogFull();
            var input = default(SpanByte);
            var output = default(EqualityEmpty);
            this.session.BasicContext.RMW(ref key,ref input,ref output);
            cursorRecordResult=CursorRecordResult.Accept;
            return true;
        }
        public bool ConcurrentReader(ref SpanByte key,ref SpanByte value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult)
           => this.SingleReader(ref key,ref value,recordMetadata,numberOfRecords,out cursorRecordResult);
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
        > Session
    ):IScanIteratorFunctions<SpanByte,SpanByte> {
        public bool SingleReader(ref SpanByte key,ref SpanByte value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
            Session.BasicContext.Upsert(ref key,ref value);
            cursorRecordResult=CursorRecordResult.Accept;
            return true;
        }
        public bool ConcurrentReader(ref SpanByte key,ref SpanByte value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult)
           => this.SingleReader(ref key,ref value,recordMetadata,numberOfRecords,out cursorRecordResult);
        public bool OnStart(long beginAddress,long endAddress) => true;
        public void OnStop(bool completed,long numberOfRecords) { }
        public void OnException(Exception exception,long numberOfRecords) { }
    }
    public partial void ExceptWith(ManagedHashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        if(ReferenceEquals(this,other)) {
            this.Clear();
            return;
        }
        var ReadSession = other.ReadSession;
        var cursor = 0L;
        var iterator = new ExceptWithScanIteratorFunctions(this.ExceptWith_ClientSession);
        ReadSession.IterateLookup(ref iterator,ref cursor,resetCursor: false);
        ReadSession.BasicContext.CompletePending(wait: true);
        this.ExceptWith_ClientSession.BasicContext.CompletePending(wait: true);
    }
    public partial void IntersectWith(ManagedHashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        var ReadSession = this.ReadSession;
        var cursor = 0L;
        var iterator = new IntersectWithScanIteratorFunctions(other,this.IntersectWith_ClientSession,SetOperationBatchSize);
        var 最後まで読み切ったか = ReadSession.IterateLookup(ref iterator,ref cursor,resetCursor: false);
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
        var cursor = 0L;
        var Iterator = new SymmetricExceptWithScanIteratorFunctions(this,this.SymmetricExceptWith_ClientSession,SetOperationBatchSize);
        ReadSession.IterateLookup(ref Iterator,ref cursor,resetCursor: false);
        ReadSession.BasicContext.CompletePending(wait: true);
        this.SymmetricExceptWith_ClientSession.BasicContext.CompletePending(wait: true);
        //if(ReferenceEquals(this,other)) {
        //    this.Clear();
        //    return;
        //}
        //var scanSession = other.ReadSession;
        //var session = this.SymmetricExceptWith_ClientSession;
        //var cursor = 0L;
        //while(true) {
        //    var iterator = new SymmetricExceptWithScanIteratorFunctions(this,session,SetOperationBatchSize);
        //    var 最後まで読み切ったか = scanSession.IterateLookup(ref iterator,ref cursor,resetCursor: false);
        //    scanSession.BasicContext.CompletePending(wait: true);
        //    session.BasicContext.CompletePending(wait: true);
        //    if(最後まで読み切ったか) {
        //        return;
        //    }
        //}
    }
    public partial void UnionWith(ManagedHashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        var ReadSession = other.ReadSession;
        var cursor = 0L;
        var Iterator = new ScanIteratorFunctions_UnionWith(this.Add_ClientSession);
        ReadSession.IterateLookup(ref Iterator,ref cursor,resetCursor: false);
        ReadSession.BasicContext.CompletePending(wait: true);
        this.Add_ClientSession.BasicContext.CompletePending(wait: true);
        //if(ReferenceEquals(this,other)) {
        //    return;
        //}
        //var scanSession = other.ReadSession;
        //var session = this.Add_ClientSession;
        //var cursor = 0L;
        //while(true) {
        //    var iterator = new ScanIteratorFunctions_UnionWith(this,session,SetOperationBatchSize);
        //    var 最後まで読み切ったか = scanSession.IterateLookup(ref iterator,ref cursor,resetCursor: false);
        //    scanSession.BasicContext.CompletePending(wait: true);
        //    session.BasicContext.CompletePending(wait: true);
        //    if(最後まで読み切ったか) {
        //        return;
        //    }
        //}
    }
}
