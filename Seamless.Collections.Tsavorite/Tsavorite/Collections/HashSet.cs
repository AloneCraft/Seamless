namespace Seamless.Tsavorite.Collections;

/// <summary>
/// Tsavorite のハッシュセット実装。
/// <see cref="System.Collections.Generic.ISet{T}"/> を実装し、
/// Tsavorite 永続ストア上で高速な追加・削除・検索を行うコレクションです。
/// </summary>
[DebuggerTypeProxy(typeof(HashSetDebugView<>))]
public partial class HashSet<TKey>:Collection<TKey,EqualityEmpty,AutoSerializer<TKey>,MemoryPackSerializer<EqualityEmpty>>, IHashSet<TKey> {
    //private readonly byte[] EmptyBytes;
    private readonly IDisposable? ownedStore;
    public bool IsReadOnly => false;
    public bool Add(TKey item) {
        if(this.Contains(item))
            return false;
        this.PrivateUpsert(item,EqualityEmpty.Default);
        return true;
    }
    void ICollection<TKey>.Add(TKey item) => this.Add(item);
    public void CopyTo(TKey[] array,int arrayIndex) {
        ArgumentNullException.ThrowIfNull(array);
        ArgumentOutOfRangeException.ThrowIfNegative(arrayIndex);
        if(array.Length-arrayIndex<this.GetLongCount())
            throw new ArgumentException("The destination array is too small.");
        foreach(var item in this) {
            array[arrayIndex++]=item;
        }
    }
    public partial void ExceptWith(HashSet<TKey> other);
    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    IEnumerator<TKey> IEnumerable<TKey>.GetEnumerator() => this.GetEnumerator();
    public partial bool IsProperSubsetOf(HashSet<TKey> other);
    public partial bool IsProperSupersetOf(HashSet<TKey> other);
    public partial bool IsSubsetOf(HashSet<TKey> other);
    public partial bool IsSupersetOf(HashSet<TKey> other);
    public partial bool Overlaps(HashSet<TKey> other);
    public partial bool SetEquals(HashSet<TKey> other);
    public partial void IntersectWith(HashSet<TKey> other);
    public partial void SymmetricExceptWith(HashSet<TKey> other);
    public partial void UnionWith(HashSet<TKey> other);

    public void ExceptWith(Seamless.Collections.ISet<TKey> other) => TsavoriteSetOperations.ExceptWith(this,other);

    public void IntersectWith(Seamless.Collections.ISet<TKey> other) => TsavoriteSetOperations.IntersectWith(this,other);

    public bool IsSubsetOf(Seamless.Collections.ISet<TKey> other) => TsavoriteSetOperations.IsSubsetOf(this,other);

    public bool IsSupersetOf(Seamless.Collections.ISet<TKey> other) => TsavoriteSetOperations.IsSupersetOf(this,other);

    public bool IsProperSubsetOf(Seamless.Collections.ISet<TKey> other) => TsavoriteSetOperations.IsProperSubsetOf(this,other);

    public bool IsProperSupersetOf(Seamless.Collections.ISet<TKey> other) => TsavoriteSetOperations.IsProperSupersetOf(this,other);

    public bool Overlaps(Seamless.Collections.ISet<TKey> other) => TsavoriteSetOperations.Overlaps(this,other);

    public bool SetEquals(Seamless.Collections.ISet<TKey> other) => TsavoriteSetOperations.SetEquals(this,other);

    public void SymmetricExceptWith(Seamless.Collections.ISet<TKey> other) => TsavoriteSetOperations.SymmetricExceptWith(this,other);

    public void UnionWith(Seamless.Collections.ISet<TKey> other) => TsavoriteSetOperations.UnionWith(this,other);

    public void ExceptWith(global::System.Collections.Generic.ISet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        foreach(var item in other)
            this.Remove(item);
    }

    public void ExceptWith(IEnumerable<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        foreach(var item in other)
            this.Remove(item);
    }

    public void IntersectWith(global::System.Collections.Generic.ISet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        var workListPath = CreateTemporaryWorkListPath("hashset-intersect-ienumerable");
        try {
            using var keysToRemove = new SerializedList<TKey,AutoSerializer<TKey>>(workListPath,initialCapacity: Const.Chunk_Length,initialDataCapacity: 4096);
            foreach(var item in this) {
                if(!other.Contains(item))
                    keysToRemove.Add(item);
            }
            foreach(var item in keysToRemove)
                this.Remove(item);
        }
        finally {
            DeleteTemporaryWorkListPath(workListPath);
        }
    }

    public void IntersectWith(IEnumerable<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        if(other is global::System.Collections.Generic.ISet<TKey> set) {
            this.IntersectWith(set);
            return;
        }
        var materialized = new global::System.Collections.Generic.HashSet<TKey>(other);
        this.IntersectWith(materialized);
    }

    public void SymmetricExceptWith(global::System.Collections.Generic.ISet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        foreach(var item in other) {
            if(!this.Remove(item))
                this.Add(item);
        }
    }

    public void SymmetricExceptWith(IEnumerable<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        if(other is global::System.Collections.Generic.ISet<TKey> set) {
            this.SymmetricExceptWith(set);
            return;
        }
        var materialized = new global::System.Collections.Generic.HashSet<TKey>(other);
        this.SymmetricExceptWith(materialized);
    }

    public void UnionWith(global::System.Collections.Generic.ISet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        foreach(var item in other)
            this.Add(item);
    }

    public void UnionWith(IEnumerable<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        foreach(var item in other)
            this.Add(item);
    }
    public override bool IsDisposed => this.disposed;
    private bool disposed;
    protected override void Dispose(bool disposing) {
        if(this.disposed) return;
        if(disposing) {
            this.IntersectWith_ClientSession.Dispose();
            this.SymmetricExceptWith_ClientSession.Dispose();
        }
        else {
        }
        this.disposed=true;
        base.Dispose(disposing);
        if(disposing)
            this.ownedStore?.Dispose();
    }
}



internal interface IHashSetScanStrategy<TSelf, T>
    where TSelf : struct, IHashSetScanStrategy<TSelf,T> {
    static abstract bool Invoke(ref TSelf self,T item);
}

public partial class HashSet<TKey> {
    internal void Scan<TStrategy>(ref TStrategy strategy)
        where TStrategy : struct, IHashSetScanStrategy<TStrategy,TKey> {
        var state = new ScanState<TStrategy>(strategy);
        var adapter = new ScanAdapter<TStrategy>(state);
        this.RunScanCursor(adapter,()=>adapter.Stop);
        strategy=state.Strategy;
    }

    internal void ScanUnsafeDelegate(Func<object?,bool> body) {
        var adapter = new UnsafeDelegateScanAdapter(body);
        this.RunScanCursor(adapter,()=>adapter.Stop);
    }

    private void RunScanCursor(IScanIteratorFunctions adapter,Func<bool> shouldStop) {
        this.VerifyThreadAffinity();
        var cursor = 0L;
        var untilAddress = this.Store.Log.TailAddress;
        while(!shouldStop()) {
            var mayHaveMore = this.ReadSession.ScanCursor(
                ref cursor,
                Const.Chunk_Length,
                adapter,
                untilAddress,
                true,
                untilAddress,
                false,
                false);
            this.ReadSession.BasicContext.CompletePending(wait: true);
            if(!mayHaveMore) {
                break;
            }
        }
    }

    private sealed class ScanState<TStrategy>(TStrategy strategy) {
        public TStrategy Strategy = strategy;
    }

    private sealed class ScanAdapter<TStrategy>(ScanState<TStrategy> state):IScanIteratorFunctions
        where TStrategy : struct, IHashSetScanStrategy<TStrategy,TKey> {
        private readonly ScanState<TStrategy> state = state;
        public bool Stop { get; private set; }

        public bool Reader<TSourceLogRecord>(
            in TSourceLogRecord logRecord,
            RecordMetadata recordMetadata,
            long numberOfRecords,
            out CursorRecordResult cursorRecordResult)
            where TSourceLogRecord : ISourceLogRecord {
            var item = AutoSerializer<TKey>.Deserialize(logRecord.Key);
            if(TStrategy.Invoke(ref this.state.Strategy,item)) {
                cursorRecordResult=CursorRecordResult.Accept;
                return true;
            }

            this.Stop=true;
            cursorRecordResult=CursorRecordResult.Accept;
            return false;
        }

        public bool OnStart(long beginAddress,long endAddress) => true;

        public void OnStop(bool completed,long numberOfRecords) {
        }

        public void OnException(Exception exception,long numberOfRecords) {
        }
    }

    private sealed class UnsafeDelegateScanAdapter(Func<object?,bool> body):IScanIteratorFunctions {
        private readonly Func<object?,bool> body = body;
        public bool Stop { get; private set; }

        public bool Reader<TSourceLogRecord>(
            in TSourceLogRecord logRecord,
            RecordMetadata recordMetadata,
            long numberOfRecords,
            out CursorRecordResult cursorRecordResult)
            where TSourceLogRecord : ISourceLogRecord {
            var item = AutoSerializer<TKey>.Deserialize(logRecord.Key);
            if(this.body(item)) {
                cursorRecordResult=CursorRecordResult.Accept;
                return true;
            }

            this.Stop=true;
            cursorRecordResult=CursorRecordResult.Accept;
            return false;
        }

        public bool OnStart(long beginAddress,long endAddress) => true;

        public void OnStop(bool completed,long numberOfRecords) {
        }

        public void OnException(Exception exception,long numberOfRecords) {
        }
    }
}

internal static class HashSetScanUnsafe {
    internal static void Scan<T>(HashSet<T> source,Func<object?,bool> body) =>
        source.ScanUnsafeDelegate(body);

    internal static void ScanInt32(HashSet<int> source,Func<object?,bool> body) =>
        source.ScanUnsafeDelegate(body);
}


/// <summary>
/// Tsavorite の HashSet スキャン操作向けの内部ヘルパークラス。
/// <see cref="System.Collections.Generic.ISet{T}"/> の走査を非ジェネリック経由で実行するための
/// 補助メソッドを提供します。
/// </summary>
[DebuggerTypeProxy(typeof(HashSetDebugView<>))]
public partial class HashSet<TKey> {
    private ClientSession<
        FixedSpanByteKey,//TKey
        EqualityEmpty,//TInput
        EqualityEmpty,//TOutput
        EqualityEmpty,//TContext
        ExceptWith,//TFunctions
        SpanByteStoreFunctions,
        SpanByteStoreAllocator
    > ExceptWith_ClientSession = null!;
    private ClientSession<
        FixedSpanByteKey,//TKey
        EqualityEmpty,//TInput
        EqualityEmpty,//TOutput
        EqualityEmpty,//TContext
        IntersectWith,//TFunctions
        SpanByteStoreFunctions,//TStoreFunctions
        SpanByteStoreAllocator
    > IntersectWith_ClientSession = null!;
    private ClientSession<
        FixedSpanByteKey,//TKey
        EqualityEmpty,//TInput
        EqualityEmpty,//TOutput
        EqualityEmpty,//TContext
        SymmetricExceptWith,//TFunctions
        SpanByteStoreFunctions,
        SpanByteStoreAllocator
    > SymmetricExceptWith_ClientSession = null!;
    internal HashSet(CollectionStore<TKey,EqualityEmpty> CollectionStore,IDisposable? ownedStore = null) : base(CollectionStore) {
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
            FixedSpanByteKey,//TKey
            EqualityEmpty,//TInput
            EqualityEmpty,//TOutput
            EqualityEmpty,//TContext
            ExceptWith//TFunctions
        >(new());
        this.IntersectWith_ClientSession=Store.NewSession<
            FixedSpanByteKey,//TKey
            EqualityEmpty,//TInput
            EqualityEmpty,//TOutput
            EqualityEmpty,//TContext
            IntersectWith//TFunctions
        >(new());
        this.SymmetricExceptWith_ClientSession=Store.NewSession<
            FixedSpanByteKey,//TKey
            EqualityEmpty,//TInput
            EqualityEmpty,//TOutput
            EqualityEmpty,//TContext
            SymmetricExceptWith//TFunctions
        >(new());
    }
    public Enumerator<
        KeyIterator,
        TKey
    > GetEnumerator() {
        this.VerifyThreadAffinity();
        return new(this.ReadSession,new(),this.Store.Log.TailAddress,this);
    }
    IEnumerator<TKey> IMemoryCollection<TKey>.GetEnumerator() {
        this.VerifyThreadAffinity();
        return new Enumerator<MemoryKeyIterator,TKey>(this.ReadSession,new(),this.Store.Log.TailAddress,this);
    }
    public partial void ExceptWith(HashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        base.ExceptWith(other,this.ExceptWith_ClientSession);
    }
    public partial void IntersectWith(HashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        this.IntersectWith(this,other,this.IntersectWith_ClientSession);
    }
    public partial bool IsProperSubsetOf(HashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        return this.Count<other.Count&&this.IsSubsetOf(other);
    }
    public partial bool IsProperSupersetOf(HashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        return this.Count>other.Count&&this.IsSupersetOf(other);
    }
    public partial bool IsSubsetOf(HashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        foreach(var item in this) {
            if(!other.Contains(item))
                return false;
        }
        return true;
    }
    public partial bool IsSupersetOf(HashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        return other.IsSubsetOf(this);
    }
    public partial bool Overlaps(HashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        foreach(var item in this) {
            if(other.Contains(item))
                return true;
        }
        return false;
    }
    public partial bool SetEquals(HashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        return this.Count==other.Count&&this.IsSubsetOf(other);
    }
    public partial void SymmetricExceptWith(HashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        base.SymmetricExceptWith(other,this.SymmetricExceptWith_ClientSession);
    }
    public partial void UnionWith(HashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        base.UnionWith(other,this.Add_ClientSession);
    }
}
