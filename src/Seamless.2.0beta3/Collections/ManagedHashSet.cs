using System.Diagnostics;
using Seamless.ScanIteratorFunctions;
using Seamless.SessionFunctions;
using Seamless.Stores;
namespace Seamless.Collections;
/// <summary>
/// Tsavorite をバックエンドに使うオンメモリ HashSet。
/// <see cref="ISet{T}"/> を実装し、利用者に Tsavorite の詳細を見せない。
/// 重複を許さず、キーのみを格納する。値は EqualityEmpty 構造体を使用する。
/// </summary>
[DebuggerDisplay("Count = {Count}")]
public abstract partial class ManagedHashSet<TKey>where TKey : IEquatable<TKey>{
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
    internal ManagedHashSet(ManagedCollectionStore<TKey,EqualityEmpty> CollectionStore):base(CollectionStore){
        var Store=CollectionStore.Store;
        this.IntersectWith_ClientSession      =Store.NewSession<
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
    > GetEnumerator()=>new (this.ReadClientSession,new());
    private Enumerator<
        SpanByteKeyIterator,
        byte[]
    > GetSpanByteEnumerator()=>new (this.ReadClientSession,new());
    private sealed class SpanByteKeyIterator:ScanIteratorFunctions<byte[]> {
        private protected override byte[] Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord) =>
            logRecord.Key.ToArray();
    }
    public partial void IntersectWith(ManagedHashSet<TKey> other){
        ArgumentNullException.ThrowIfNull(other);
        var IntersectWith_ClientSession = this.IntersectWith_ClientSession;
        var Input = default(EqualityEmpty);
        var Output = default(EqualityEmpty);
        var Enumerator = this.GetSpanByteEnumerator();
        while(Enumerator.MoveNext()) {
            var KeyBytes = Enumerator.Current;
            if(other.PrivateProtectedContainsKey(KeyBytes)) continue;
            if(this.WouldOverflowMutable(KeyBytes.Length,1)) throw this.ThrowLogFull();
            var FixedKey = (FixedSpanByteKey)(ReadOnlySpan<byte>)KeyBytes;
            var Status = IntersectWith_ClientSession.BasicContext.RMW(FixedKey,ref Input,ref Output);
        }
        IntersectWith_ClientSession.BasicContext.CompletePending(wait: true);
    }
    public partial void SymmetricExceptWith(ManagedHashSet<TKey> other) {
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
