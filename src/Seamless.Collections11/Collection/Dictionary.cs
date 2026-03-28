namespace Seamless.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Collections;
using System.Diagnostics;
using Tsavorite.core;
/// <summary>
/// Tsavorite をバックエンドに使うオンメモリ Dictionary。
/// <see cref="IDictionary{TKey,TValue}"/> を実装し、利用者に Tsavorite の詳細を見せない。
/// 静的ポリモーフィズムにより仮想ディスパッチを回避する。
/// </summary>
[DebuggerDisplay("Count = {Count}")]
[DebuggerTypeProxy(typeof(DictionaryDebugView<,>))]
public sealed class Dictionary<TKey, TValue>:Collection<TKey,TValue>, IDictionary<TKey,TValue>
	where TKey : IEquatable<TKey>
	where TValue : IEquatable<TValue> {
	internal Dictionary(TsavoriteKV<SpanByte,SpanByte,StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>>> Store,long LogMemorySize = 0,double MutableFraction = 1.0) : base(Store,LogMemorySize,MutableFraction) { }
	public bool IsReadOnly => false;
    public bool TryGetValue(TKey Key,[MaybeNullWhen(false)] out TValue Value) {
        SpanByteAndMemory Output = default;
        var Status = this.Read(Key,ref Output);
        if(Status.NotFound) {
            Value=default;
            return false;
        }
        Value=Deserialize<TValue>(Output.AsReadOnlySpan());
        return true;
    }
	public TryAddResult TryAdd(TKey Key,TValue Value) =>
        this.PrivateTryAdd(Key,Value);
	public void Add(TKey Key,TValue Value) {
		var Result = this.TryAdd(Key,Value);
		switch(Result) {
            case TryAddResult.LogFull:
                throw this.ThrowLogFull();
            case TryAddResult.KeyAlreadyExists: throw new ArgumentException($"An item with the same key has already been added. Key: {Key}");
		}
	}
	public void Add(KeyValuePair<TKey,TValue> item) => this.Add(item.Key,item.Value);
    bool ICollection<KeyValuePair<TKey,TValue>>.Contains(KeyValuePair<TKey,TValue> item) {
        SpanByteAndMemory Output = default;
        var Status = this.Read(item.Key,ref Output);
        Debug.Assert(Status.Found==!Status.NotFound);
        return Status.Found;
    }

    
    
    public bool ContainsKey(TKey Key)=>
        this.Contains(Key);
	/// <summary>
	/// Key,Value両方が一致するエントリを削除する。Keyのみが存在する場合は削除しない。
	/// </summary>
	/// <param name="item"></param>
	/// <returns></returns>
	public bool Remove(KeyValuePair<TKey,TValue> item) {
		var Key = item.Key;
        if(this.TryGetValue(Key,out var existing)){
            if(EqualityComparer<TValue>.Default.Equals(existing,item.Value)){
                return this.Remove(Key);
            } else{
            }
        }
        return false;
    }
    public bool Remove(TKey key,[MaybeNullWhen(false)]out TValue value) {
        if(this.TryGetValue(key,out value)) {
            return this.Remove(key);
        } else {
        }
        return false;
    }
	public void CopyTo(KeyValuePair<TKey,TValue>[] array,int arrayIndex) {
		ArgumentNullException.ThrowIfNull(array);
		ArgumentOutOfRangeException.ThrowIfNegative(arrayIndex);
		if(array.Length-arrayIndex<this.GetLongCount())
			throw new ArgumentException("The destination array is too small.");
		foreach(var kvp in this) {
			array[arrayIndex++]=kvp;
		}
	}
	IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
	public IEnumerator<KeyValuePair<TKey,TValue>> GetEnumerator() => this.GetKeyValueEnumerable().GetEnumerator();
	public List<KeyValuePair<TKey,TValue>> ToList() => [.. this.GetKeyValueEnumerable()];
	public ICollection<TKey> Keys => new List<TKey>(this.GetKeyEnumerable());
	public ICollection<TValue> Values => new List<TValue>(this.GetValueEnumerable());
	public TValue this[TKey Key] {
		get {
			var KeyBytes = Serialize(Key);
			unsafe {
				fixed(byte* pKey = KeyBytes) {
                    var Session=this.Session;
					var FixedKey = SpanByte.FromPinnedPointer(pKey,KeyBytes.Length);
					SpanByteAndMemory ValueBytes = default;
					var Status = Session.BasicContext.Read(ref FixedKey,ref ValueBytes);
                    Status=Pending(Session,Status,ref ValueBytes);

					//if(Status.IsPending) {
					//	this.Session.BasicContext.CompletePendingWithOutputs(out var CompletedOutputs,wait: true);
					//	CompletedOutputs.Next();
					//	Status=CompletedOutputs.Current.Status;
					//	ValueBytes=CompletedOutputs.Current.Output;
					//	CompletedOutputs.Dispose();
					//} else {
					//}
					if(Status.NotFound)
						throw new KeyNotFoundException();
					return Deserialize<TValue>(ValueBytes.AsReadOnlySpan());
				}
			}
		}
		set {
			var KeyBytes = Serialize(Key);
			var ValueBytes = Serialize(value);
			if(this.WouldOverflowMutable(KeyBytes.Length,ValueBytes.Length)) {
				throw this.ThrowLogFull();
			}
			unsafe {
				fixed(byte* pKey = KeyBytes, pValue = ValueBytes){
                    var Session=this.Session;
					var FixedKey = SpanByte.FromPinnedPointer(pKey,KeyBytes.Length);
					var KeyValue = SpanByte.FromPinnedPointer(pValue,ValueBytes.Length);
					var Status = Session.BasicContext.Upsert(FixedKey,KeyValue);
                    Pending(Session,Status);
					//if(Status.IsPending) {
					//	this.Session.BasicContext.CompletePendingWithOutputs(out var CompletedOutputs,wait: true);
					//	CompletedOutputs.Next();
					//	Status=CompletedOutputs.Current.Status;
					//	CompletedOutputs.Dispose();
					//} else{
     //               }
				}
			}
		}
	}
    ~Dictionary() => this.Dispose(false);
}
