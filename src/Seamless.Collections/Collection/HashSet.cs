using System.Collections;
using System.Diagnostics;
using Garnet.common;
using Tsavorite.core;
namespace Seamless.Collections;
//public static class Extensions0{
//    public static bool PrivateTryAdd0<TKey, TValue>(this Collection<TKey,TValue>Collection,TKey Key,TValue Value) where TKey : IEquatable<TKey> where TValue : IEquatable<TValue> {
//        var KeyBytes = MemoryPack.MemoryPackSerializer.Serialize(Key);
//        var ValueBytes = MemoryPack.MemoryPackSerializer.Serialize(Key);
//    }
//}
/// <summary>
/// Tsavorite をバックエンドに使うオンメモリ HashSet。
/// <see cref="ISet{T}"/> を実装し、利用者に Tsavorite の詳細を見せない。
/// 重複を許さず、キーのみを格納する。値は Empty 構造体を使用する。
/// </summary>
[DebuggerDisplay("Count = {Count}")]
[DebuggerTypeProxy(typeof(HashSetDebugView<>))]
public sealed class HashSet<T>:Collection<T,Empty>, ISet<T> where T : IEquatable<T> {
	private readonly byte[] EmptyBytes;
	private readonly ClientSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,IntersectSessionFunctions,StoreFunctions<SpanByteComparer,DefaultRecordDisposer>,ObjectAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>> IntersectSession;
	private readonly ClientSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,SymmetricExceptSessionFunctions,StoreFunctions<SpanByteComparer,DefaultRecordDisposer>,ObjectAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>> SymmetricExceptSession;
	private bool SetOperationSessionsDisposed;
	internal HashSet(TsavoriteKV<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>,ObjectAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>> Store,long LogMemorySize = 0,double MutableFraction = 1.0) : base(Store,LogMemorySize,MutableFraction) {
		this.EmptyBytes=Serialize(Empty.Default);
		this.IntersectSession=Store.NewSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,IntersectSessionFunctions>(new IntersectSessionFunctions());
		this.SymmetricExceptSession=Store.NewSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,SymmetricExceptSessionFunctions>(new SymmetricExceptSessionFunctions());
	}
	public bool IsReadOnly => false;
	public bool Add(T item) {
		var Result = this.PrivateTryAdd(item,Empty.Default);
		switch(Result) {
			case TryAddResult.Success:
				return true;
			case TryAddResult.KeyAlreadyExists:
				return false;
			default:
                Debug.Assert(TryAddResult.LogFull==Result);
                throw this.ThrowLogFull();
		}
	}
	void ICollection<T>.Add(T item) => this.Add(item);
	//public bool Contains(T item) =>
	//	this.Contains(item);
	public void CopyTo(T[] array,int arrayIndex) {
		ArgumentNullException.ThrowIfNull(array);
		ArgumentOutOfRangeException.ThrowIfNegative(arrayIndex);
		if(array.Length-arrayIndex<this.GetLongCount())
			throw new ArgumentException("The destination array is too small.");
		foreach(var item in this) {
			array[arrayIndex++]=item;
		}
	}
	public void ExceptWith(HashSet<T> other) {
		ArgumentNullException.ThrowIfNull(other);
		foreach(var item in other) {
			this.Remove(item);
		}
	}
	IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
	public IEnumerator<T> GetEnumerator() => this.GetKeyEnumerable().GetEnumerator();
	public void IntersectWith(HashSet<T> other) {
		ArgumentNullException.ThrowIfNull(other);
		var Input = PinnedSpanByte.FromPinnedSpan(this.EmptyBytes);
		var Session = this.IntersectSession;
		foreach(var Key in this.GetKeyEnumerable()) {
			if(other.Contains(Key)) {
				continue;
			}
			var KeyBytes = Serialize(Key);
			var FixedKey = (FixedSpanByteKey)(ReadOnlySpan<byte>)KeyBytes;
			SpanByteAndMemory output = default;
			var _=Session.BasicContext.RMW(FixedKey,ref Input,ref output);
		}
        Session.BasicContext.CompletePending(true);
    }
	public bool IsProperSubsetOf(HashSet<T> other) {
		ArgumentNullException.ThrowIfNull(other);
		if(this.GetLongCount()>=other.GetLongCount())
			return false;
		foreach(var Key in this.GetKeyEnumerable()) {
			if(!other.Contains(Key)) {
				return false;
			}
		}
		return true;
	}
	public bool IsProperSupersetOf(HashSet<T> other) {
		ArgumentNullException.ThrowIfNull(other);
		if(this.GetLongCount()<=other.GetLongCount())
			return false;
		foreach(var item in other) {
            if(this.Contains(item))
                continue;
            return false;
        }
        return true;
	}
	public bool IsSubsetOf(HashSet<T> other) {
		ArgumentNullException.ThrowIfNull(other);
		foreach(var Key in this.GetKeyEnumerable()) {
			if(!other.Contains(Key)) {
				return false;
			}
		}
		return true;
	}
	public bool IsSupersetOf(HashSet<T> other) {
		ArgumentNullException.ThrowIfNull(other);
		foreach(var item in other) {
			if(!this.Contains(item)) {
				return false;
			}
		}
		return true;
	}
	public bool Overlaps(HashSet<T> other) {
		ArgumentNullException.ThrowIfNull(other);
		foreach(var item in other) {
			if(this.Contains(item)) {
				return true;
			}
		}
		return false;
	}
	public bool SetEquals(HashSet<T> other) {
		ArgumentNullException.ThrowIfNull(other);
		if(this.GetLongCount()!=other.GetLongCount())
			return false;
		foreach(var Key in this.GetKeyEnumerable()) {
			if(!other.Contains(Key)) {
				return false;
			}
		}
		return true;
	}
	public void SymmetricExceptWith(HashSet<T> other) {
		ArgumentNullException.ThrowIfNull(other);
		var Input = PinnedSpanByte.FromPinnedSpan(this.EmptyBytes);
		var Session = this.SymmetricExceptSession;
		foreach(var item in other) {
			var KeyBytes = Serialize(item);
			var Key = (FixedSpanByteKey)(ReadOnlySpan<byte>)KeyBytes;
			SpanByteAndMemory Output = default;
			var _=Session.BasicContext.RMW(Key,ref Input,ref Output);
		}
        Session.BasicContext.CompletePending(wait: true);
	}
	public void UnionWith(HashSet<T> other) {
		ArgumentNullException.ThrowIfNull(other);
		foreach(var item in other) {
			this.Add(item);
		}
	}
	protected override void Dispose(bool disposing) {
		if(!this.SetOperationSessionsDisposed) {
			this.SetOperationSessionsDisposed=true;
			if(disposing) {
				this.IntersectSession.Dispose();
				this.SymmetricExceptSession.Dispose();
			}
		}
		base.Dispose(disposing);
	}
}
