namespace Seamless.Collections;
using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Tsavorite.core;

/// <summary>
/// Tsavorite をバックエンドに使うオンメモリ Dictionary。
/// <see cref="IDictionary{TKey,TValue}"/> を実装し、利用者に Tsavorite の詳細を見せない。
/// 静的ポリモーフィズムにより仮想ディスパッチを回避する。
/// </summary>
[DebuggerDisplay("Count = {Count}")]
[DebuggerTypeProxy(typeof(DictionaryDebugView<,>))]
public sealed class Dictionary<TKey, TValue>:Collection<TKey,TValue>, IDictionary<TKey,TValue>,IReadOnlyDictionary<TKey, TValue>
	where TKey :IEquatable<TKey>
	where TValue : IEquatable<TValue> {
	private readonly TRemove_TKey_TValue_ClientSession Remove_TKey_TValue_ClientSession;
	private readonly TRemove_KeyValuePair_ClientSession Remove_KeyValuePair_ClientSession;
	internal Dictionary(TsavoriteKV<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>,ObjectAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>> Store,long LogMemorySize = 0,double MutableFraction = 1.0) : base(Store,LogMemorySize,MutableFraction) {
		this.Remove_TKey_TValue_ClientSession=Store.NewSession<Garnet.common.FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,Remove_TKey_TValue_SessionFunctions>(new ());
		this.Remove_KeyValuePair_ClientSession=Store.NewSession<Garnet.common.FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,Remove_KeyValuePair_SessionFunctions>(new ());
	}
	public bool IsReadOnly => false;
    public bool TryGetValue(TKey Key,[MaybeNullWhen(false)] out TValue Value) {
        SpanByteAndMemory Output = default;
        var Status = this.Read(Key,ref Output);
        if(Status.NotFound) {
            Value=default;
            return false;
        }
        Value=Deserialize<TValue>(Output.ReadOnlySpan);
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
    //public bool Contains(T item) =>
    //    this.PrivateContainsKey(item);

	public bool ContainsKey(TKey Key)=>
		this.Contains(Key);
    /// <summary>
    /// Key,Value両方が一致するエントリを削除する。Keyのみが存在する場合は削除しない。
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    public bool Remove(KeyValuePair<TKey,TValue> item) {
        var Session = this.Remove_KeyValuePair_ClientSession;
        var KeyBytes = Serialize(item.Key);
        var ValueBytes = Serialize(item.Value);
        var FixedKey = (Garnet.common.FixedSpanByteKey)(ReadOnlySpan<byte>)KeyBytes;
        var Input = PinnedSpanByte.FromPinnedSpan(ValueBytes);
        SpanByteAndMemory Output = default;
        var Status = Session.BasicContext.RMW(FixedKey,ref Input,ref Output);
        Status=Pending(Session,Status,ref Output);
        return !Output.ReadOnlySpan.IsEmpty;
    }
    public bool Remove(TKey key,[MaybeNullWhen(false)]out TValue value) {
        var Session = this.Remove_TKey_TValue_ClientSession;
        var KeyBytes = Serialize(key);
        var FixedKey = (Garnet.common.FixedSpanByteKey)(ReadOnlySpan<byte>)KeyBytes;
        var Input = default(PinnedSpanByte);
        SpanByteAndMemory Output = default;
        var Status = Session.BasicContext.RMW(FixedKey,ref Input,ref Output);
        Status=Pending(Session,Status,ref Output);
        if(Status.Found) {
            value=Deserialize<TValue>(Output.ReadOnlySpan);
            return true;
        }
        value=default;
        return false;
    }
    //public bool Remove(TKey key) {
    //    var KeyBytes = Serialize(key);
    //    var FixedKey = (FixedSpanByteKey)KeyBytes;
    //    var Input = default(PinnedSpanByte);
    //    SpanByteAndMemory Output = default;
    //    var DeleteSession = this.DeleteSession;
    //    var Status = DeleteSession.BasicContext.RMW(FixedKey,ref Input,ref Output);
    //    if(Status.IsPending) {
    //        DeleteSession.BasicContext.CompletePendingWithOutputs(out var CompletedOutputs,wait: true);
    //        CompletedOutputs.Next();
    //        Status=CompletedOutputs.Current.Status;
    //        CompletedOutputs.Dispose();
    //    } else {
    //    }
    //    return !Status.NotFound;
    //}
    public void CopyTo(KeyValuePair<TKey,TValue>[] array,int arrayIndex){
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
    IEnumerable<TKey> IReadOnlyDictionary<TKey,TValue>.Keys=>this.Keys;
    IEnumerable<TValue> IReadOnlyDictionary<TKey,TValue>.Values=>this.Values;
    public ICollection<TValue> Values => new List<TValue>(this.GetValueEnumerable());
    public TValue this[TKey Key] {
        get {
            if(this.TryGetValue(Key,out var Value)) {
                return Value;
            } else {
                throw new KeyNotFoundException();
            }
        }
        set {
            var KeyBytes = Serialize(Key);
            var ValueBytes = Serialize(value);
            if(this.WouldOverflowMutable(KeyBytes.Length,ValueBytes.Length)) {
                throw this.ThrowLogFull();
            } else {
            }
            var FixedKey = (Garnet.common.FixedSpanByteKey)(ReadOnlySpan<byte>)KeyBytes;
            var Session=this.ClientSession;
            var Status = Session.BasicContext.Upsert(FixedKey,ValueBytes);
            Pending(Session,Status);
        }
    }
	public override bool IsDisposed=>this.disposed;
	private bool disposed;
	protected override void Dispose(bool disposing) {
		if(this.disposed) return;
		this.disposed=true;
		if(disposing) {
			this.Remove_TKey_TValue_ClientSession.Dispose();
			this.Remove_KeyValuePair_ClientSession.Dispose();
		}
		base.Dispose(disposing);
	}
}
