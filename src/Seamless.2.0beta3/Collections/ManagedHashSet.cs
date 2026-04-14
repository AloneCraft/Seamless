using Seamless.SessionFunctions;
using Seamless.Stores;

namespace Seamless.Collections;

/// <summary>
/// Tsavorite をバックエンドに使うオンメモリ HashSet。
/// <see cref="ISet{T}"/> を実装し、利用者に Tsavorite の詳細を見せない。
/// 重複を許さず、キーのみを格納する。値は EqualityEmpty 構造体を使用する。
/// </summary>
public partial class ManagedHashSet<TKey>
    where TKey:IEquatable<TKey> {
    private readonly ClientSession<
        FixedSpanByteKey,//TKey

        EqualityEmpty,//TInput
        EqualityEmpty,//TOutput
        EqualityEmpty,//TContext
        IntersectWith,//TFunctions
        ManagedStoreFunctions,//TStoreFunctions
        ManagedAllocator
    > IntersectWith_ClientSession;
    private readonly ClientSession<
        FixedSpanByteKey,//TKey

        PinnedSpanByte,//TInput
        SpanByteAndMemory,//TOutput
        EqualityEmpty,//TContext
        SymmetricExceptWith,//TFunctions
        ManagedStoreFunctions,
        UnmanagedAllocator
    > SymmetricExceptWith_ClientSession;
    internal ManagedHashSet(ManagedCollectionStore<TKey,EqualityEmpty> CollectionStore):base(CollectionStore){
        var Store=CollectionStore.Store;
        this.IntersectWith_ClientSession=Store.NewSession<
            FixedSpanByteKey,//TKey
            EqualityEmpty,//TInput
            EqualityEmpty,//TOutput
            EqualityEmpty,//TContext
            IntersectWith//TFunctions
        >(new());
        this.SymmetricExceptWith_ClientSession=Store.NewSession<
            FixedSpanByteKey,//TKey
            PinnedSpanByte,//TInput
            SpanByteAndMemory,//TOutput
            EqualityEmpty,//TContext
            SymmetricExceptWith//TFunctions
        >(new());
    }
    public Enumerator<
        KeyIterator,
        TKey
    > GetEnumerator() => new(this.ReadClientSession,new());










































































    public partial void IntersectWith(ManagedHashSet<TKey> other){
        //2.0はMaagedもUnmanagedも共通なのでCollectionで定義している
        this.IntersectWith(this,other,this.IntersectWith_ClientSession);
    }




    public partial void SymmetricExceptWith(ManagedHashSet<TKey> other){
        this.SymmetricExceptWith(this,other,this.SymmetricExceptWith_ClientSession);
    }
}
