using System.Collections;
using System.Diagnostics;
using Tsavorite.core;
namespace Seamless.Collections;
/// <summary>
/// Tsavorite をバックエンドに使うオンメモリ HashSet。
/// <see cref="ISet{T}"/> を実装し、利用者に Tsavorite の詳細を見せない。
/// 重複を許さず、キーのみを格納する。値は Empty 構造体を使用する。
/// </summary>
[DebuggerDisplay("Count = {Count}")]
[DebuggerTypeProxy(typeof(HashSetDebugView<>))]
public sealed class HashSet<T>:Collection<T,Empty>, ISet<T> where T : IEquatable<T> {
	private readonly byte[] EmptyBytes;
	private readonly ClientSession<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Empty,IntersectSessionFunctions,StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>>> IntersectSession;
	private readonly ClientSession<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Empty,SymmetricExceptSessionFunctions,StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>>> SymmetricExceptSession;
	private bool SetOperationSessionsDisposed;
	internal HashSet(TsavoriteKV<SpanByte,SpanByte,StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>>> Store,long LogMemorySize = 0,double MutableFraction = 1.0) : base(Store,LogMemorySize,MutableFraction) {
		this.EmptyBytes=Serialize(default(Empty));
		this.IntersectSession=this.Store.NewSession<SpanByte,SpanByteAndMemory,Empty,IntersectSessionFunctions>(new IntersectSessionFunctions());
		this.SymmetricExceptSession=this.Store.NewSession<SpanByte,SpanByteAndMemory,Empty,SymmetricExceptSessionFunctions>(new SymmetricExceptSessionFunctions());
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
		var InputBytes = this.EmptyBytes;
		var Session = this.IntersectSession;
		foreach(var Key0 in this.GetKeyEnumerable()) {
			if(other.Contains(Key0)) {
				continue;
			}
			var KeyBytes = Serialize(Key0);
			unsafe {
				fixed(byte* PKey = KeyBytes, PInput = InputBytes) {
					var Key = SpanByte.FromPinnedPointer(PKey,KeyBytes.Length);
					var Input = SpanByte.FromPinnedPointer(PInput,InputBytes.Length);
					SpanByteAndMemory Output = default;
					var Status = Session.BasicContext.RMW(ref Key,ref Input,ref Output);

				}
			}
		}
        Session.BasicContext.CompletePending(wait: true);
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
			if(!this.Contains(item)) {
				return false;
			}
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
		var InputBytes = this.EmptyBytes;
		var Session = this.SymmetricExceptSession;
		foreach(var Item in other) {
			var KeyBytes = Serialize(Item);
			unsafe {
				fixed(byte* PKey = KeyBytes, PInput = InputBytes) {
					var Key = SpanByte.FromPinnedPointer(PKey,KeyBytes.Length);
					var Input = SpanByte.FromPinnedPointer(PInput,InputBytes.Length);
					SpanByteAndMemory Output = default;
					Session.BasicContext.RMW(ref Key,ref Input,ref Output);
				}
			}
		}
        Session.BasicContext.CompletePending(wait: true);
        this.SymmetricExceptSession.Dispose();
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
