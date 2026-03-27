namespace Seamless.Collections;

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
	public TryAddResult TryAdd(TKey Key,TValue Value) {
		var KeyBytes = Serialize(Key);
		var ValueBytes = Serialize(Value);
		if(this.WouldOverflowMutable(KeyBytes.Length,ValueBytes.Length)) {
			return TryAddResult.LogFull;
		}
		unsafe {
			fixed(byte* pKey = KeyBytes, pValue = ValueBytes) {
				var Key0 = SpanByte.FromPinnedPointer(pKey,KeyBytes.Length);
				var input = SpanByte.FromPinnedPointer(pValue,ValueBytes.Length);
				SpanByteAndMemory output = default;
				var status = this.Session.BasicContext.RMW(ref Key0,ref input,ref output);
				if(status.IsPending) {
					this.Session.BasicContext.CompletePendingWithOutputs(out var completedOutputs,wait: true);
					completedOutputs.Next();
					status=completedOutputs.Current.Status;
					completedOutputs.Dispose();
				} else {
				}
				if(!status.NotFound) {
					return TryAddResult.KeyAlreadyExists;
				}
			}
		}
		return TryAddResult.Success;
	}
	public void Add(TKey Key,TValue Value) {
		var Result = this.TryAdd(Key,Value);
		switch(Result) {
			case TryAddResult.LogFull:
				var Log = this.Store.Log;
				throw new OutOfMemoryException($"Tsavorite object log is full. Cannot add new entries. BeginAddress: {Log.BeginAddress}, HeadAddress: {Log.HeadAddress}, Available capacity exhausted.");
			case TryAddResult.KeyAlreadyExists: throw new ArgumentException($"An item with the same key has already been added. Key: {Key}");
		}
	}
	public void Add(KeyValuePair<TKey,TValue> item) => this.Add(item.Key,item.Value);
	public bool ContainsKey(TKey Key) {
		var KeyBytes = Serialize(Key);
		unsafe {
			fixed(byte* pKey = KeyBytes) {
				var FixedKey = SpanByte.FromPinnedPointer(pKey,KeyBytes.Length);
				SpanByteAndMemory output = default;
				var Status = this.Session.BasicContext.Read(ref FixedKey,ref output);
				if(Status.IsPending) {
					this.Session.BasicContext.CompletePendingWithOutputs(out var completedOutputs,wait: true);
					completedOutputs.Next();
					Status=completedOutputs.Current.Status;
					completedOutputs.Dispose();
				} else {
				}
				return !Status.NotFound;
			}
		}
	}
	public bool Contains(KeyValuePair<TKey,TValue> item) => this.TryGetValue(item.Key,out var value)&&EqualityComparer<TValue>.Default.Equals(value,item.Value);
	/// <summary>
	/// Key,Value両方が一致するエントリを削除する。Keyのみが存在する場合は削除しない。
	/// </summary>
	/// <param name="item"></param>
	/// <returns></returns>
	public bool Remove(KeyValuePair<TKey,TValue> item) {
		var Key = item.Key;
		if(!this.TryGetValue(Key,out var existing)||!EqualityComparer<TValue>.Default.Equals(existing,item.Value)) {
			return false;
		}
		return this.Remove(Key);
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
					var FixedKey = SpanByte.FromPinnedPointer(pKey,KeyBytes.Length);
					SpanByteAndMemory ValueBytes = default;
					var Status = this.Session.BasicContext.Read(ref FixedKey,ref ValueBytes);
					if(Status.IsPending) {
						this.Session.BasicContext.CompletePendingWithOutputs(out var CompletedOutputs,wait: true);
						CompletedOutputs.Next();
						Status=CompletedOutputs.Current.Status;
						ValueBytes=CompletedOutputs.Current.Output;
						CompletedOutputs.Dispose();
					} else {
					}
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
				this.ThrowLogFull();
			}
			unsafe {
				fixed(byte* pKey = KeyBytes, pValue = ValueBytes) {
					var FixedKey = SpanByte.FromPinnedPointer(pKey,KeyBytes.Length);
					var KeyValue = SpanByte.FromPinnedPointer(pValue,ValueBytes.Length);
					var Status = this.Session.BasicContext.Upsert(FixedKey,KeyValue);
					if(Status.IsPending) {
						this.Session.BasicContext.CompletePendingWithOutputs(out var CompletedOutputs,wait: true);
						CompletedOutputs.Next();
						Status=CompletedOutputs.Current.Status;
						CompletedOutputs.Dispose();
					} else{
                    }
				}
			}
		}
	}
    ~Dictionary() => this.Dispose(false);
}
