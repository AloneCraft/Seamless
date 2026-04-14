using Seamless.SessionFunctions;
using Seamless.Stores;

using System.Diagnostics;
namespace Seamless.Collections;

/// <summary>
/// Tsavorite をバックエンドに使うオンメモリ HashSet。
/// <see cref="ISet{T}"/> を実装し、利用者に Tsavorite の詳細を見せない。
/// 重複を許さず、キーのみを格納する。値は EqualityEmpty 構造体を使用する。
/// </summary>
[DebuggerDisplay("Count = {Count}")]
public abstract partial class ManagedHashSet<TKey>
    where TKey : IEquatable<TKey>
{
    private readonly ClientSession<
        FixedSpanByteKey,//TKey

        EqualityEmpty,//TInput
        EqualityEmpty,//TOutput
        EqualityEmpty,//TContext
        IntersectWith,//TFunctions


        StoreFunctions<
            SpanByteComparer,//TKeyComparer
            DefaultRecordDisposer//TRecordDisposer
        >,//TStoreFunctions


        SpanByteAllocator<
            StoreFunctions<
                SpanByteComparer,//TKeyComparer
                DefaultRecordDisposer//TRecordDisposer
            >
        >
    > IntersectWith_ClientSession;
    private readonly ClientSession<
        FixedSpanByteKey,//TKey

        PinnedSpanByte,//TInput
        SpanByteAndMemory,//TOutput
        EqualityEmpty,//TContext
        SymmetricExceptWith,//TFunctions
        StoreFunctions<
            SpanByteComparer,//TKeyComparer
            DefaultRecordDisposer//TRecordDisposer


        >,
        SpanByteAllocator<
            StoreFunctions<
                SpanByteComparer,//TKeyComparer
                DefaultRecordDisposer//TRecordDisposer


            >
        >
    > SymmetricExceptWith_ClientSession;
    internal ManagedHashSet(ManagedCollectionStore<TKey, EqualityEmpty> CollectionStore) : base(CollectionStore)
    {
        var Store = CollectionStore.Store;
        this.IntersectWith_ClientSession = Store.NewSession<
            FixedSpanByteKey,//TKey
            EqualityEmpty,//TInput
            EqualityEmpty,//TOutput
            EqualityEmpty,//TContext
            IntersectWith//TFunctions
        >(new());
        this.SymmetricExceptWith_ClientSession = Store.NewSession<
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
    > GetEnumerator() => new(this.ReadClientSession, new());
    public partial void IntersectWith(ManagedHashSet<TKey> other)
    {
        this.IntersectWith(this, other, this.IntersectWith_ClientSession);
    }
    public partial void SymmetricExceptWith(ManagedHashSet<TKey> other)
    {
        this.SymmetricExceptWith(this, other, this.SymmetricExceptWith_ClientSession);
    }
}
