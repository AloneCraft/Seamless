using System.Diagnostics;
using Seamless.SessionFunctions;
using Seamless.Stores;
namespace Seamless.Collections;
/// <summary>
/// Tsavorite をバックエンドに使うオンメモリ HashSet。
/// <see cref="ISet{T}"/> を実装し、利用者に Tsavorite の詳細を見せない。
/// 重複を許さず、キーのみを格納する。値は EqualityEmpty 構造体を使用する。
/// </summary>
[DebuggerDisplay("Count = {Count}")]
[DebuggerTypeProxy(typeof(DebugView<>))]
public abstract partial class ManagedHashSet<TKey>where TKey : IEquatable<TKey>{
    private readonly ClientSession<
        SpanByte,//TKey
        SpanByte,//TValue
        EqualityEmpty,//TInput
        EqualityEmpty,//TOutput
        EqualityEmpty,//TContext
        ManagedIntersectWith,
        StoreFunctions<
            SpanByte,//TKey
            SpanByte,//TValue
            SpanByteComparer,//TKeyComparer
            SpanByteRecordDisposer//TRecordDisposer
        >,
        SpanByteAllocator<
            StoreFunctions<
                SpanByte,//TKey
                SpanByte,//TValue
                SpanByteComparer,//TKeyComparer
                SpanByteRecordDisposer//TRecordDisposer
            >
        >
    > IntersectWith_ClientSession;
    private readonly ClientSession<
        SpanByte,//TKey
        SpanByte,//TValue
        SpanByte,//TInput
        SpanByteAndMemory,//TOutput
        EqualityEmpty,//TContext
        SymmetricExceptWith,
        StoreFunctions<
            SpanByte,//TKey
            SpanByte,//TValue
            SpanByteComparer,//TKeyComparer
            SpanByteRecordDisposer//TRecordDisposer
        >,
        SpanByteAllocator<
            StoreFunctions<
                SpanByte,//TKey
                SpanByte,//TValue
                SpanByteComparer,//TKeyComparer
                SpanByteRecordDisposer//TRecordDisposer
            >
        >
    > SymmetricExceptWith_ClientSession;
    internal ManagedHashSet(ManagedCollectionStore<TKey,EqualityEmpty> CollectionStore) : base(CollectionStore) {
        var Store=CollectionStore.Store;
        this.IntersectWith_ClientSession=Store.NewSession<
            EqualityEmpty,//TKey
            
            EqualityEmpty,//TOutput
            EqualityEmpty,//TContext
            ManagedIntersectWith//TFunctions
        >(new());
        this.SymmetricExceptWith_ClientSession=Store.NewSession<
            SpanByte,//TKey
            
            SpanByteAndMemory,//TOutput
            EqualityEmpty,//TContext
            SymmetricExceptWith//TFunctions
        >(new());
    }
    public Enumerator<
        KeyIterator,
        TKey
    > GetEnumerator()=>new(this.ReadClientSession,new());
    
    
    
    




    public partial void IntersectWith(ManagedHashSet<TKey> other){
        ArgumentNullException.ThrowIfNull(other);
        var Session = this.IntersectWith_ClientSession;
        var Input=default(EqualityEmpty);
        var Output=default(EqualityEmpty);

        foreach(var Key in this) {
            if(other.Contains(Key)) continue;
            var Key0 = Key;
            var KeyBytes = ManagedSerializer<TKey>.Serialize(ref Key0);
            var Key1 = SpanByte.FromPinnedSpan(KeyBytes);
            Session.BasicContext.RMW(ref Key1, ref Input, ref Output);
        }
        Session.BasicContext.CompletePending(wait: true);
    }
    public partial void SymmetricExceptWith(ManagedHashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        var Session = this.SymmetricExceptWith_ClientSession;
        foreach(var Item in other){
            var Item0=Item;
            var KeyBytes = ManagedSerializer<TKey>.Serialize(ref Item0);
            var Key = SpanByte.FromPinnedSpan(KeyBytes);
            var Input = default(SpanByte);
            SpanByteAndMemory Output = default;
            var Status=Session.BasicContext.RMW(ref Key,ref Input,ref Output);
        }
        Session.BasicContext.CompletePending(wait: true);
    }
}
