using Garnet.common;
using Seamless.SessionFunctions;
namespace Seamless.Collections;
using ScanIteratorFunctions;

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
public abstract class ManagedDictionary<TKey, TValue>:ManagedCollection<TKey,TValue>, IDictionary<TKey,TValue>,IReadOnlyDictionary<TKey, TValue>
	where TKey :IEquatable<TKey>
	where TValue : IEquatable<TValue> 
{
	//private readonly ClientSession<Garnet.common.FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,Remove_TKey_TValue ,GStoreFunctions,GAllocator>Remove_TKey_TValue_ClientSession;
	//private readonly ClientSession<Garnet.common.FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,Remove_KeyValuePair ,GStoreFunctions,GAllocator>Remove_KeyValuePair_ClientSession;
#if TSAVORITE2_0BETA3
    private ClientSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,Remove_TKey_TValue ,StoreFunctions<SpanByteComparer,DefaultRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>> Remove_TKey_TValue_ClientSession{get;set;}
    private ClientSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,Remove_KeyValuePair ,StoreFunctions<SpanByteComparer,DefaultRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>>Remove_KeyValuePair_ClientSession{get;set;}
	internal ManagedDictionary(ManagedCollectionStore<TKey,TValue>CollectionStore) : base(CollectionStore) {
        var Store=CollectionStore.Store;
        this.Remove_TKey_TValue_ClientSession=Store.NewSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,Remove_TKey_TValue>(new ());
        this.Remove_KeyValuePair_ClientSession=Store.NewSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,Remove_KeyValuePair>(new ());
    }
#elif TSAVORITE1_1_1
    private ClientSession<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Empty,Remove_TKey_TValue,StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>>> Remove_TKey_TValue_ClientSession{get;set;}
    private ClientSession<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Empty,Remove_KeyValuePair   ,StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>>>Remove_KeyValuePair_ClientSession{get;set;}
	internal ManagedDictionary(ManagedCollectionStore<TKey,TValue>CollectionStore) : base(CollectionStore) { 
        var Store=CollectionStore.Store;
        this.Remove_TKey_TValue_ClientSession=Store.NewSession<SpanByte,SpanByteAndMemory,Empty,Remove_TKey_TValue>(new ());
        this.Remove_KeyValuePair_ClientSession=Store.NewSession<SpanByte,SpanByteAndMemory,Empty,Remove_KeyValuePair>(new ());
    }
#endif
	public bool IsReadOnly => false;
    public bool TryGetValue(TKey Key,[MaybeNullWhen(false)] out TValue Value) {
        var Status = this.Read(Key,out var Output);
        if(Status.NotFound) {
            Value=default;
            return false;
        }
        Value=Deserialize<TValue>(GetReadOnlySpan(Output));
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
        var Status = this.Read(item.Key,out _);
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
#if TSAVORITE2_0BETA3
    public bool Remove(KeyValuePair<TKey,TValue> item) {
        var Session = this.Remove_KeyValuePair_ClientSession;
        var KeyBytes = Serialize(item.Key);
        var ValueBytes = Serialize(item.Value);
        var FixedKey = (FixedSpanByteKey)(ReadOnlySpan<byte>)KeyBytes;
        var Input = PinnedSpanByte.FromPinnedSpan(ValueBytes);
        var Output = default(SpanByteAndMemory);
        var Status = Session.BasicContext.RMW(FixedKey,ref Input,ref Output);
        Status=Pending(Session,Status,ref Output);
        //RMWの結果がFoundかNotFoundかをOutputの長さで判定。
        //Remove_KeyValuePairはキー存在時でも値が一致しない場合は削除しないため、Output.Length>0ならばキーと値の両方が一致して削除されたと判断できる。
        return Output.Length>0;
    }
    public bool Remove(TKey key,out TValue value) {
        var Session = this.Remove_TKey_TValue_ClientSession;
        var KeyBytes = Serialize(key);
        var FixedKey = (FixedSpanByteKey)(ReadOnlySpan<byte>)KeyBytes;
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
    public Enumerator<TKey,TValue,StoreFunctions<SpanByteComparer,DefaultRecordDisposer>,KeyValueIterator<TKey,TValue>,KeyValuePair<TKey,TValue>> GetEnumerator()=>new (this.ClientSession,new());
    private List<T> CreateCollection<TIterator,T>(TIterator Iterator)where TIterator : ScanIteratorFunctions<T>{
        // Tsavoriteのセッションから列挙を開始
        // 列挙数が不明なため、Listの初期容量はデフォルトに任せる
        var Collection = new List<T>();
        using var Enumerator = new Enumerator<TKey,TValue,StoreFunctions<SpanByteComparer,DefaultRecordDisposer>,TIterator,T>(this.ClientSession, Iterator);
        while (Enumerator.MoveNext()){
            Collection.Add(Enumerator.Current);
        }

        return Collection;
    }    
    private IEnumerable<T> Enumerate<TIterator,T>(TIterator Iterator)
        where TIterator:ScanIteratorFunctions<T>{
        using var e=new Enumerator<TKey,TValue,StoreFunctions<SpanByteComparer,DefaultRecordDisposer>,TIterator,T>(this.ClientSession,Iterator);
        while(e.MoveNext())
            yield return e.Current;
    }
#elif TSAVORITE1_1_1
    public bool Remove(KeyValuePair<TKey,TValue> item) {
        var Session = this.Remove_KeyValuePair_ClientSession;
        var KeyBytes = Serialize(item.Key);
        var ValueBytes = Serialize(item.Value);
        var FixedKey =SpanByte.FromPinnedSpan(KeyBytes);
        var Input =SpanByte.FromPinnedSpan(ValueBytes);
        var Status = Session.BasicContext.RMW(FixedKey,Input,out var Output);
        Status=Pending(Session,Status,ref Output);
        //RMWの結果がFoundかNotFoundかをOutputの長さで判定。
        //Remove_KeyValuePairはキー存在時でも値が一致しない場合は削除しないため、Output.Length>0ならばキーと値の両方が一致して削除されたと判断できる。
        return Output.Length>0;
    }
    public bool Remove(TKey key,[MaybeNullWhen(false)]out TValue value) {
        var Session = this.Remove_TKey_TValue_ClientSession;
        var KeyBytes = Serialize(key);
        var FixedKey =SpanByte.FromPinnedSpan(KeyBytes);
        var Input = default(SpanByte);
        var Status = Session.BasicContext.RMW(FixedKey,Input,out var Output);
        Status=Pending(Session,Status,ref Output);
        if(Status.Found) {
            value=Deserialize<TValue>(Output.AsReadOnlySpan());
            return true;
        }
        value =default;
        return false;
    }
    public Enumerator<TKey,TValue,StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>,KeyValueIterator<TKey,TValue>,KeyValuePair<TKey,TValue>> GetEnumerator()=>new (this.ClientSession,new());
    private List<T> CreateCollection<TIterator,T>(TIterator Iterator)where TIterator : ScanIteratorFunctions<T>{
        // Tsavoriteのセッションから列挙を開始
        // 列挙数が不明なため、Listの初期容量はデフォルトに任せる
        var Collection = new List<T>();
        using var Enumerator = new Enumerator<TKey, TValue,StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>,TIterator,T>(this.ClientSession, Iterator);
        while (Enumerator.MoveNext()){
            Collection.Add(Enumerator.Current);
        }

        return Collection;
    }    
    private IEnumerable<T> Enumerate<TIterator,T>(TIterator Iterator)
        where TIterator:ScanIteratorFunctions<T>{
        using var e=new Enumerator<TKey,TValue,StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>,TIterator,T>(this.ClientSession,Iterator);
        while(e.MoveNext()){
            yield return e.Current;
        }
    }
#endif
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
	IEnumerator<KeyValuePair<TKey,TValue>> IEnumerable<KeyValuePair<TKey,TValue>>.GetEnumerator() => this.GetEnumerator();
	public List<KeyValuePair<TKey,TValue>> ToList(){
        using var Enumerator=this.GetEnumerator();
        var List=new List<KeyValuePair<TKey,TValue>>();
        while(Enumerator.MoveNext()){
            List.Add(Enumerator.Current);
        }
        return List;
    }
    public ICollection<TKey> Keys=>this.CreateCollection<KeyIterator<TKey>,TKey>(new());
    public ICollection<TValue> Values=>this.CreateCollection<ValueIterator<TValue>,TValue>(new());
    IEnumerable<TKey> IReadOnlyDictionary<TKey,TValue>.Keys=>this.Enumerate<KeyIterator<TKey>,TKey>(new());
    IEnumerable<TValue> IReadOnlyDictionary<TKey,TValue>.Values=>this.Enumerate<ValueIterator<TValue>,TValue>(new());
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
            var Session=this.ClientSession;
#if TSAVORITE2_0BETA3
            var FixedKey=(FixedSpanByteKey)(ReadOnlySpan<byte>)KeyBytes;
            var Input=ValueBytes;
#elif TSAVORITE1_1_1
            var FixedKey = SpanByte.FromPinnedSpan(KeyBytes);
            var Input = SpanByte.FromPinnedSpan(ValueBytes);
#endif
            var Status = Session.BasicContext.Upsert(FixedKey,Input);
            Status=Pending(Session,Status);
            //var FixedKey = (Garnet.common.FixedSpanByteKey)(ReadOnlySpan<byte>)KeyBytes;
            //var Session=this.ClientSession;
            //var Status = Session.BasicContext.Upsert(FixedKey,ValueBytes);
            //Pending(Session,Status);
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
