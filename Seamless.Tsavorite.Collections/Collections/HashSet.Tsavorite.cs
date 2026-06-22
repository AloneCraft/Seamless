using Seamless.Tsavorite.Collections.SessionFunctions;
using Seamless.Tsavorite.Collections.Stores;
namespace Seamless.Tsavorite.Collections;
/// <summary>
/// Tsavorite ��o�b�N�G���h�Ɏg���I�������� HashSet�B
/// <see cref="System.Collections.Generic.ISet{T}"/> ��������A���p�҂� Tsavorite �̏ڍׂ�����Ȃ��B
/// �d����������A�L�[�݂̂�i�[����B�l�� EqualityEmpty �\���̂�g�p����B
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
    > GetEnumerator() => new(this.ReadSession,new(),this.Store.Log.TailAddress);
    IEnumerator<TKey> IMemoryCollection<TKey>.GetEnumerator() => new Enumerator<MemoryKeyIterator,TKey>(this.ReadSession,new(),this.Store.Log.TailAddress);
    public partial void ExceptWith(HashSet<TKey> other) {
        base.ExceptWith(other,this.ExceptWith_ClientSession);
    }
    public partial void IntersectWith(HashSet<TKey> other) {
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
        base.SymmetricExceptWith(other,this.SymmetricExceptWith_ClientSession);
    }
    public partial void UnionWith(HashSet<TKey> other) {
        base.UnionWith(other,this.Add_ClientSession);
    }
}
