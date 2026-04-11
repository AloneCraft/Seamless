using System.Diagnostics;
using Seamless.ScanIteratorFunctions;
using Seamless.SessionFunctions;
using Seamless.Store;
namespace Seamless.Collections;
/// <summary>
/// UnmanagedHashSet の 2.0beta3 固有部分。IntersectWith/SymmetricExceptWith セッションとイテレータ。
/// </summary>
[DebuggerDisplay("Count = {Count}")]
public abstract partial class UnmanagedHashSet<TKey>:UnmanagedCollection<TKey,EqualityEmpty,UnmanagedSerializer<TKey>,UnmanagedSerializer<EqualityEmpty>>
    where TKey:unmanaged,IEquatable<TKey> {
    private readonly ClientSession<FixedSpanByteKey,EqualityEmpty,EqualityEmpty,EqualityEmpty,IntersectWith,StoreFunctions<UnmanagedKeyComparer<TKey>,SpanByteRecordDisposer>,SpanByteAllocator<StoreFunctions<UnmanagedKeyComparer<TKey>,SpanByteRecordDisposer>>> IntersectWith_ClientSession;
    private readonly ClientSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,EqualityEmpty,SymmetricExceptWith,StoreFunctions<UnmanagedKeyComparer<TKey>,SpanByteRecordDisposer>,SpanByteAllocator<StoreFunctions<UnmanagedKeyComparer<TKey>,SpanByteRecordDisposer>>> SymmetricExceptWith_ClientSession;
    internal UnmanagedHashSet(UnmanagedCollectionStore<TKey,EqualityEmpty> CollectionStore):base(CollectionStore){
        var Store=CollectionStore.Store;
        this.IntersectWith_ClientSession=Store.NewSession<FixedSpanByteKey,EqualityEmpty,EqualityEmpty,EqualityEmpty,IntersectWith>(new());
        this.SymmetricExceptWith_ClientSession=Store.NewSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,EqualityEmpty,SymmetricExceptWith>(new());
    }
    public UnmanagedEnumerator<
        TKey,
        EqualityEmpty,
        KeyIterator,
        TKey
    > GetEnumerator()=>new(this.ReadClientSession,new());
    internal UnmanagedEnumerator<
        TKey,
        EqualityEmpty,
        SpanByteKeyIterator,
        byte[]
    > GetSpanByteEnumerator()=>new(this.ReadClientSession,new());
    public partial void IntersectWith(UnmanagedHashSet<TKey> other){
        ArgumentNullException.ThrowIfNull(other);
        var IntersectWith_ClientSession = this.IntersectWith_ClientSession;
        var Input = default(EqualityEmpty);
        var Output = default(EqualityEmpty);
        var Enumerator = this.GetSpanByteEnumerator();
        while(Enumerator.MoveNext()) {
            var KeyBytes = Enumerator.Current;
            if(other.ContainsKey(KeyBytes)) {
                continue;
            }
            if(this.WouldOverflowMutable(KeyBytes.Length,1)) {
                throw this.ThrowLogFull();
            }
            var FixedKey = (FixedSpanByteKey)(ReadOnlySpan<byte>)KeyBytes;
            var Status = IntersectWith_ClientSession.BasicContext.RMW(FixedKey,ref Input,ref Output);
        }
        IntersectWith_ClientSession.BasicContext.CompletePending(wait: true);
    }
    public partial void SymmetricExceptWith(UnmanagedHashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        var Session = this.SymmetricExceptWith_ClientSession;
        var Input = default(PinnedSpanByte);
        var Enumerator = other.GetSpanByteEnumerator();
        while(Enumerator.MoveNext()){
            var KeyBytes = Enumerator.Current;
            var Key = (FixedSpanByteKey)(ReadOnlySpan<byte>)KeyBytes;
            SpanByteAndMemory Output = default;
            var Status=Session.BasicContext.RMW(Key,ref Input,ref Output);
        }
        Session.BasicContext.CompletePending(wait: true);
    }
}
