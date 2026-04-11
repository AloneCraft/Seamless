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
[DebuggerTypeProxy(typeof(HashSetDebugView<>))]
public abstract partial class ManagedHashSet<T>
    where T : IEquatable<T>{
    private readonly ClientSession<
        SpanByte,
        SpanByte,
        EqualityEmpty,
        EqualityEmpty,
        EqualityEmpty,
        IntersectWith,
        StoreFunctions<
            SpanByte,
            SpanByte,
            SpanByteComparer,
            SpanByteRecordDisposer
        >,
        SpanByteAllocator<
            StoreFunctions<
                SpanByte,
                SpanByte,
                SpanByteComparer,
                SpanByteRecordDisposer
            >
        >
    > IntersectWith_ClientSession;
    private readonly ClientSession<
        SpanByte,
        SpanByte,
        SpanByte,//TInput
        SpanByteAndMemory,//TOutput
        EqualityEmpty,//TContext
        SymmetricExceptWith,
        StoreFunctions<
            SpanByte,
            SpanByte,
            SpanByteComparer,
            SpanByteRecordDisposer
        >,
        SpanByteAllocator<
            StoreFunctions<
                SpanByte,
                SpanByte,
                SpanByteComparer,
                SpanByteRecordDisposer
            >
        >
    > SymmetricExceptWith_ClientSession;
	internal ManagedHashSet(ManagedCollectionStore<T,EqualityEmpty> CollectionStore) : base(CollectionStore) {
        //this.EmptyBytes=Serialize(EqualityEmpty.Default).ToArray();
        var Store=CollectionStore.Store;
        this.IntersectWith_ClientSession=Store.NewSession<EqualityEmpty,EqualityEmpty,EqualityEmpty,IntersectWith>(new());
        this.SymmetricExceptWith_ClientSession=Store.NewSession<SpanByte,SpanByteAndMemory,EqualityEmpty,SymmetricExceptWith>(new());
    }

	public Enumerator<
		KeyIterator<T,ManagedSerializer<T>>,
		T
	> GetEnumerator() =>new(this.ReadClientSession,new());
	public partial void IntersectWith(ManagedHashSet<T> other){
		ArgumentNullException.ThrowIfNull(other);
		var Session = this.IntersectWith_ClientSession;
		EqualityEmpty Input = default;
		EqualityEmpty Output = default;
		foreach(var Key in this) {
			if(other.Contains(Key)) continue;
			var Key0 = Key;
			var KeyBytes = this.SerializeKey(ref Key0);
			var Key1 = SpanByte.FromPinnedSpan(KeyBytes);
			Session.BasicContext.RMW(ref Key1, ref Input, ref Output);
		}
		Session.BasicContext.CompletePending(wait: true);
	}
	public partial void SymmetricExceptWith(ManagedHashSet<T> other) {
		ArgumentNullException.ThrowIfNull(other);
        var Session = this.SymmetricExceptWith_ClientSession;
        foreach(var Item in other){
            var Item0=Item;
            var KeyBytes = this.SerializeKey(ref Item0);
            var Key = SpanByte.FromPinnedSpan(KeyBytes);
            var Input = default(SpanByte);
            SpanByteAndMemory Output = default;
            var Status=Session.BasicContext.RMW(ref Key,ref Input,ref Output);
        }
        Session.BasicContext.CompletePending(wait: true);
	}
}
