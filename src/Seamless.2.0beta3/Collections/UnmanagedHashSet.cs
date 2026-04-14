using System.Diagnostics;
using Seamless.SessionFunctions;
using Seamless.Stores;
namespace Seamless.Collections;

/// <summary>
/// UnmanagedHashSet の 2.0beta3 固有部分。IntersectWith/SymmetricExceptWith セッションとイテレータ。
/// </summary>
[DebuggerDisplay("Count = {Count}")]
public abstract partial class UnmanagedHashSet<TKey>
    where TKey : unmanaged, IEquatable<TKey>
{
    private readonly ClientSession<
        FixedSpanByteKey,
        EqualityEmpty,
        EqualityEmpty,
        EqualityEmpty,
        IntersectWith,
        UnmanagedStoreFunctions,
        UnmanagedAllocator
    > IntersectWith_ClientSession;
    private readonly ClientSession<
        FixedSpanByteKey,
        PinnedSpanByte,
        SpanByteAndMemory,
        EqualityEmpty,
        SymmetricExceptWith,
        UnmanagedStoreFunctions,
        UnmanagedAllocator
    > SymmetricExceptWith_ClientSession;
    internal UnmanagedHashSet(UnmanagedCollectionStore<TKey, EqualityEmpty> CollectionStore) : base(CollectionStore)
    {
        var Store = CollectionStore.Store;
        this.IntersectWith_ClientSession = Store.NewSession<
            FixedSpanByteKey,
            EqualityEmpty,
            EqualityEmpty,
            EqualityEmpty,
            IntersectWith
        >(new());
        this.SymmetricExceptWith_ClientSession = Store.NewSession<
            FixedSpanByteKey,
            PinnedSpanByte,
            SpanByteAndMemory,
            EqualityEmpty,
            SymmetricExceptWith
        >(new());
    }
    public Enumerator<
        KeyIterator,
        TKey
    > GetEnumerator() => new(this.ReadClientSession, new());
    public partial void IntersectWith(UnmanagedHashSet<TKey> other)
    {
        this.IntersectWith(this, other, this.IntersectWith_ClientSession);
    }

    public partial void SymmetricExceptWith(UnmanagedHashSet<TKey> other)
    {
        this.SymmetricExceptWith(this, other, this.SymmetricExceptWith_ClientSession);
    }
}
