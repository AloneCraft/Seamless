using System.Collections;
using System.Diagnostics;
using Garnet.common;
using Seamless.ScanIteratorFunctions;
using Seamless.SessionFunctions;
using Tsavorite.core;
namespace Seamless.Collections;
/// <summary>
/// Tsavorite をバックエンドに使うオンメモリ HashSet。
/// <see cref="ISet{T}"/> を実装し、利用者に Tsavorite の詳細を見せない。
/// 重複を許さず、キーのみを格納する。値は Empty 構造体を使用する。
/// </summary>
[DebuggerDisplay("Count = {Count}")]
[DebuggerTypeProxy(typeof(HashSetDebugView<>))]
public abstract class HashSet<T>:Collection<T,Empty>, ISet<T> where T : IEquatable<T>{
	private readonly byte[] EmptyBytes;
#if TSAVORITE2_0BETA3
	private readonly ClientSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,IntersectWith          ,StoreFunctions<SpanByteComparer,DefaultRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>>IntersectWith_ClientSession;
	private readonly ClientSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,SymmetricExceptWith    ,StoreFunctions<SpanByteComparer,DefaultRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>>SymmetricExceptWith_ClientSession;
	internal HashSet(CollectionStore<T,Empty> CollectionStore):base(CollectionStore){
		this.EmptyBytes=Serialize(Empty.Default);
        var Store=CollectionStore.Store;
        this.IntersectWith_ClientSession=Store.NewSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,IntersectWith>(new());
		this.SymmetricExceptWith_ClientSession=Store.NewSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,SymmetricExceptWith>(new());
	}
    public Enumerator<T,Empty,Add,StoreFunctions<SpanByteComparer,DefaultRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>,KeyIterator<T>,T> GetEnumerator()=>new (this.ClientSession,new());
    public void IntersectWith(HashSet<T> other){
        ArgumentNullException.ThrowIfNull(other);
        //var Session2 = this.Intersect_ClientSession;
        var Session = this.IntersectWith_ClientSession;
        var Input = PinnedSpanByte.FromPinnedSpan(this.EmptyBytes);
        foreach(var Key in this){
            if(other.Contains(Key)) {
                continue;
            }
            var KeyBytes = Serialize(Key);
            var FixedKey = (FixedSpanByteKey)(ReadOnlySpan<byte>)KeyBytes;
            SpanByteAndMemory Output = default;
            Session.BasicContext.RMW(FixedKey,ref Input,ref Output);

        }
        Session.BasicContext.CompletePending(true);
    }
#elif TSAVORITE1_1_1
    private readonly ClientSession<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Empty,IntersectWith,StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>>> IntersectWith_ClientSession;
    private readonly ClientSession<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Empty,SymmetricExceptWith,StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>>> SymmetricExceptWith_ClientSession;
    internal HashSet(CollectionStore<T,Empty> CollectionStore) : base(CollectionStore) {
        this.EmptyBytes=Serialize(Empty.Default);
        var Store=CollectionStore.Store;
        this.IntersectWith_ClientSession=Store.NewSession<SpanByte,SpanByteAndMemory,Empty,IntersectWith>(new());
        this.SymmetricExceptWith_ClientSession=Store.NewSession<SpanByte,SpanByteAndMemory,Empty,SymmetricExceptWith>(new());
    }

	public Enumerator<
		T,
		Empty,
		Add,StoreFunctions<
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
	    >,
	    KeyIterator<T>,
		T
	> GetEnumerator() =>new (this.ClientSession,new());
	//public Enumerator<T,Empty,Add,GStoreFunctions,GAllocator                                                                                                                                                                                                                                                                                                                  ,KeyIterator<T>,T> GetEnumerator()=>new(this.ClientSession,new());

    public void IntersectWith(HashSet<T> other){
		ArgumentNullException.ThrowIfNull(other);
        //var Session2 = this.Intersect_ClientSession;
        var Session = this.IntersectWith_ClientSession;
        var InputBytes = this.EmptyBytes;
        foreach(var Key0 in this) {
            if(other.Contains(Key0)) {
                continue;
            }
            var KeyBytes = Serialize(Key0);
            var Key = SpanByte.FromPinnedSpan(KeyBytes);
            var Input = SpanByte.FromPinnedSpan(InputBytes);
            var Output = default(SpanByteAndMemory);
            var Status = Session.BasicContext.RMW(ref Key,ref Input,ref Output);
        }
        Session.BasicContext.CompletePending(wait: true);
    }
#endif
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
	IEnumerator<T> IEnumerable<T>.GetEnumerator() => this.GetEnumerator();
	public bool IsProperSubsetOf(HashSet<T> other) {
		ArgumentNullException.ThrowIfNull(other);
		if(this.GetLongCount()>=other.GetLongCount())
			return false;
		foreach(var Key in this) {
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
		foreach(var Key in this) {
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
		foreach(var Key in this) {
			if(!other.Contains(Key)) {
				return false;
			}
		}
		return true;
	}
	public void SymmetricExceptWith(HashSet<T> other) {
		ArgumentNullException.ThrowIfNull(other);
        var Session = this.SymmetricExceptWith_ClientSession;
#if TSAVORITE2_0BETA3
		var Input = PinnedSpanByte.FromPinnedSpan(this.EmptyBytes);
		foreach(var Item in other) {
			var KeyBytes = Serialize(Item);
			var Key = (FixedSpanByteKey)(ReadOnlySpan<byte>)KeyBytes;
			SpanByteAndMemory Output = default;
			var Status=Session.BasicContext.RMW(Key,ref Input,ref Output);
		}
#elif TSAVORITE1_1_1
        var InputBytes = this.EmptyBytes;
        foreach(var Item in other) {
            var KeyBytes = Serialize(Item);
            var Key = SpanByte.FromPinnedSpan(KeyBytes);
            var Input = SpanByte.FromPinnedSpan(InputBytes);
            SpanByteAndMemory Output = default;
            var Status=Session.BasicContext.RMW(ref Key,ref Input,ref Output);
            //unsafe {
            //    fixed(byte* PKey = KeyBytes, PInput = InputBytes) {
            //        var Key = SpanByte.FromPinnedPointer(PKey,KeyBytes.Length);
            //        var Input = SpanByte.FromPinnedPointer(PInput,InputBytes.Length);
            //        SpanByteAndMemory Output = default;
            //        Session.BasicContext.RMW(ref Key,ref Input,ref Output);
            //    }
            //}
        }
#endif
        Session.BasicContext.CompletePending(wait: true);
	}
	public void UnionWith(HashSet<T> other) {
		ArgumentNullException.ThrowIfNull(other);
		foreach(var item in other) {
			this.Add(item);
		}
	}
    public override bool IsDisposed=>this.disposed;
    private bool disposed;
	protected override void Dispose(bool disposing) {
        if(this.disposed) return;
        if(disposing){
            this.IntersectWith_ClientSession.Dispose();
            this.SymmetricExceptWith_ClientSession.Dispose();
        }else{

        }
        this.disposed=true;
        base.Dispose(disposing);
	}
}
