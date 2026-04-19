using Seamless.SessionFunctions;
using Seamless.Stores;
namespace Seamless.Collections;
/// <summary>
/// Tsavorite をバックエンドに使うオンメモリ HashSet。
/// <see cref="ISet{T}"/> を実装し、利用者に Tsavorite の詳細を見せない。
/// 重複を許さず、キーのみを格納する。値は EqualityEmpty 構造体を使用する。
/// </summary>
public partial class ManagedHashSet<TKey>
    where TKey : IEquatable<TKey> {
    private readonly ClientSession<
        FixedSpanByteKey,//TKey
                         //
        EqualityEmpty,//TInput
        EqualityEmpty,//TOutput
        EqualityEmpty,//TContext
        IntersectWith,//TFunctions
        ManagedStoreFunctions,//TStoreFunctions
        ManagedAllocator
    > IntersectWith_ClientSession;
    private readonly ClientSession<
        FixedSpanByteKey,//TKey
                         //
        EqualityEmpty,//TInput
        EqualityEmpty,//TOutput
        EqualityEmpty,//TContext
        SymmetricExceptWith,//TFunctions
        ManagedStoreFunctions,
        UnmanagedAllocator
    > SymmetricExceptWith_ClientSession;
    internal ManagedHashSet(ManagedCollectionStore<TKey,EqualityEmpty> CollectionStore) : base(CollectionStore) {
        var Store = CollectionStore.Store;
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
    > GetEnumerator() => new(this.ReadClientSession,new());
    IEnumerator<TKey> IMemoryCollection<TKey>.GetEnumerator() => new Enumerator<MemoryKeyIterator,TKey>(this.ReadMemoryClientSession,new(),this.HeadAddress);
    private void ExceptWithZeroCopy(ManagedHashSet<TKey> other) {
        ExceptWith(this,other,other.IntersectWith_ClientSession,this.ContainsDeleteClientSession);
    }
    private void UnionWithZeroCopy(ManagedHashSet<TKey> other) {
        UnionWith(this,other,other.IntersectWith_ClientSession,this.AddClientSession);
    }
    public partial void IntersectWith(ManagedHashSet<TKey> other) {
        //2.0はMaagedもUnmanagedも共通なのでCollectionで定義している
        IntersectWith(this,other,this.IntersectWith_ClientSession);
        //
        //
        //
        //
    }
    public partial void SymmetricExceptWith(ManagedHashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        SymmetricExceptWith(this,other,other.IntersectWith_ClientSession,this.SymmetricExceptWith_ClientSession);
    }
}
