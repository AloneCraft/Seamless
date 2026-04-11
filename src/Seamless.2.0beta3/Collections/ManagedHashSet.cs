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
public abstract partial class ManagedHashSet<T>where T : IEquatable<T>{
	private readonly ClientSession<FixedSpanByteKey,EqualityEmpty,EqualityEmpty,EqualityEmpty,IntersectWith,StoreFunctions<SpanByteComparer,DefaultRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>> IntersectWith_ClientSession;
	private readonly ClientSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,EqualityEmpty,SymmetricExceptWith,StoreFunctions<SpanByteComparer,DefaultRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>> SymmetricExceptWith_ClientSession;
	internal ManagedHashSet(ManagedCollectionStore<T,EqualityEmpty> CollectionStore):base(CollectionStore){
		//var empty = EqualityEmpty.Default;
		var Store=CollectionStore.Store;
		this.IntersectWith_ClientSession=Store.NewSession<FixedSpanByteKey,EqualityEmpty,EqualityEmpty,EqualityEmpty,IntersectWith>(new());
		this.SymmetricExceptWith_ClientSession=Store.NewSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,EqualityEmpty,SymmetricExceptWith>(new());
	}
	public Enumerator<
		KeyIterator<T,ManagedSerializer<T>>,
		T
	> GetEnumerator()=>new (this.ReadClientSession,new());
    private Enumerator<
		SpanByteKeyIterator,
		byte[]
	> GetSpanByteEnumerator()=>new (this.ReadClientSession,new());
	public partial void IntersectWith(ManagedHashSet<T> other){
		ArgumentNullException.ThrowIfNull(other);
		var IntersectWith_ClientSession = this.IntersectWith_ClientSession;
		var Input = default(EqualityEmpty);
		var Output = default(EqualityEmpty);
		var Enumerator = this.GetSpanByteEnumerator();
		while(Enumerator.MoveNext()) {
			var KeyBytes = Enumerator.Current;
			if(other.ContainsKey(KeyBytes)) continue;
			if(this.WouldOverflowMutable(KeyBytes.Length,1)) throw this.ThrowLogFull();
			var FixedKey = (FixedSpanByteKey)(ReadOnlySpan<byte>)KeyBytes;
			var Status = IntersectWith_ClientSession.BasicContext.RMW(FixedKey,ref Input,ref Output);
		}
		IntersectWith_ClientSession.BasicContext.CompletePending(wait: true);
	}
	public partial void SymmetricExceptWith(ManagedHashSet<T> other) {
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
