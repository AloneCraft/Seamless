using Seamless.ScanIteratorFunctions;
namespace Seamless.Collections;
using SessionFunctions;

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
public sealed partial class Dictionary<TKey, TValue>
	where TKey : IEquatable<TKey>
	where TValue : IEquatable<TValue> {
    private partial void Initialize(){
        var Store=this.Store;
        this.Remove_TKey_TValue_ClientSession=Store.NewSession<SpanByte,SpanByteAndMemory,Empty,Remove_TKey_TValue>(new ());
        this.Remove_KeyValuePair_ClientSession=Store.NewSession<SpanByte,SpanByteAndMemory,Empty,Remove_KeyValuePair>(new ());
    }
	//internal Dictionary(TsavoriteKV<SpanByte,SpanByte,GStoreFunctions,GAllocator> Store,long LogMemorySize = 0,double MutableFraction = 1.0) : base(Store,LogMemorySize,MutableFraction) { }
	//public bool IsReadOnly => false;
 //   public bool TryGetValue(TKey Key,[MaybeNullWhen(false)] out TValue Value) {
 //       var Status = this.Read(Key,out var Output);
 //       if(Status.NotFound) {
 //           Value=default;
 //           return false;
 //       }
 //       Value=Deserialize<TValue>(Output.AsReadOnlySpan());
 //       return true;
 //   }
	//public TryAddResult TryAdd(TKey Key,TValue Value) =>
 //       this.PrivateTryAdd(Key,Value);
	//public void Add(TKey Key,TValue Value) {
	//	var Result = this.TryAdd(Key,Value);
	//	switch(Result) {
 //           case TryAddResult.LogFull:
 //               throw this.ThrowLogFull();
 //           case TryAddResult.KeyAlreadyExists: throw new ArgumentException($"An item with the same key has already been added. Key: {Key}");
	//	}
	//}
	//public void Add(KeyValuePair<TKey,TValue> item) => this.Add(item.Key,item.Value);
 //   bool ICollection<KeyValuePair<TKey,TValue>>.Contains(KeyValuePair<TKey,TValue> item) {
 //       var Status = this.Read(item.Key,out _);
 //       Debug.Assert(Status.Found==!Status.NotFound);
 //       return Status.Found;
 //   }

    
    
 //   public bool ContainsKey(TKey Key)=>
 //       this.Contains(Key);
    //private static SpanByte GetFixedKey(byte[]KeyBytes){
    //    return SpanByte.FromPinnedSpan(KeyBytes);
    //}
    //private static SpanByte GetFixedInput(byte[]ValueBytes) {
    //    return SpanByte.FromPinnedSpan(ValueBytes);
    //}
    //private static ReadOnlySpan<byte> GetReadOnlySpan(SpanByteAndMemory Output) {
    //    return Output.AsReadOnlySpan();
    //}
    //private static SpanByte GetDefaultInput()=>default;
	/// <summary>
	/// Key,Value両方が一致するエントリを削除する。Keyのみが存在する場合は削除しない。
	/// </summary>
	/// <param name="item"></param>
	/// <returns></returns>
	//public partial bool Remove(KeyValuePair<TKey,TValue> item) {
 //       var Session = this.Remove_KeyValuePair_ClientSession;
 //       var KeyBytes = Serialize(item.Key);
 //       var ValueBytes = Serialize(item.Value);
 //       var FixedKey =SpanByte.FromPinnedSpan(KeyBytes);
 //       var Input =SpanByte.FromPinnedSpan(ValueBytes);
 //       var Status = Session.BasicContext.RMW(FixedKey,Input,out var Output);
	//	Status=Pending(Session,Status,ref Output);
	//	return !Output.AsReadOnlySpan().IsEmpty;
 //       //var DeleteSession = this.DeleteClientSession;
 //       //var KeyBytes = Serialize(Key);
 //       //var Input = default(SpanByte);
 //       //var Output = default(SpanByteAndMemory);
 //       //var FixedKey = SpanByte.FromPinnedSpan(KeyBytes);
 //       //var Status = DeleteSession.BasicContext.RMW(ref FixedKey,ref Input,ref Output);
 //       //Status=Pending(DeleteSession,Status);
 //       //Debug.Assert(Status.Found!=Status.NotFound);
 //       //return Status.Found||Status.Record.Created||Status.Record.InPlaceUpdated;
 //   }
 //   public partial bool Remove(TKey key,out TValue value) {
 //       var Session = this.Remove_TKey_TValue_ClientSession;
 //       var KeyBytes = Serialize(key);
 //       var FixedKey =SpanByte.FromPinnedSpan(KeyBytes);
 //       var Input = default(SpanByte);
 //       var Status = Session.BasicContext.RMW(FixedKey,Input,out var Output);
 //       Status=Pending(Session,Status,ref Output);
 //       if(Status.Found) {
 //           value=Deserialize<TValue>(Output.AsReadOnlySpan());
 //           return true;
 //       }
 //       value=default;
 //       return false;
 //   }
	//public void CopyTo(KeyValuePair<TKey,TValue>[] array,int arrayIndex) {
	//	ArgumentNullException.ThrowIfNull(array);
	//	ArgumentOutOfRangeException.ThrowIfNegative(arrayIndex);
	//	if(array.Length-arrayIndex<this.GetLongCount())
	//		throw new ArgumentException("The destination array is too small.");
	//	foreach(var kvp in this) {
	//		array[arrayIndex++]=kvp;
	//	}
	//}
	//IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
	//IEnumerator<KeyValuePair<TKey,TValue>> IEnumerable<KeyValuePair<TKey,TValue>>.GetEnumerator() => this.GetEnumerator();
 //   //public Enumerator<T,Empty,Add,GStoreFunctions,
 //   //GAllocator,
 //   //KeyIterator<T>,T> GetEnumerator()=>new(this.ClientSession,new());

	//public Enumerator<TKey,TValue,Add,GStoreFunctions,GAllocator,KeyValueIterator<TKey,TValue>,KeyValuePair<TKey,TValue>> GetEnumerator()=>new (this.ClientSession,new());
	//public List<KeyValuePair<TKey,TValue>> ToList(){
 //       using var Enumerator=this.GetEnumerator();
 //       var List=new List<KeyValuePair<TKey,TValue>>();
 //       while(Enumerator.MoveNext()){
 //           List.Add(Enumerator.Current);
 //       }
 //       return List;
 //   }
 //   private List<T> CreateCollection<TIterator,T>(TIterator Iterator)where TIterator : ScanIteratorFunctions<T>{
 //       // Tsavoriteのセッションから列挙を開始
 //       // 列挙数が不明なため、Listの初期容量はデフォルトに任せる
 //       var Collection = new List<T>();
 //       using var Enumerator = new Enumerator<
 //           TKey, TValue, Add,
 //           GStoreFunctions,
 //           GAllocator,
 //           TIterator, T
 //       >(this.ClientSession, Iterator);
 //       while (Enumerator.MoveNext()){
 //           Collection.Add(Enumerator.Current);
 //       }

 //       return Collection;
 //   }    
 //   public ICollection<TKey> Keys=>this.CreateCollection<KeyIterator<TKey>,TKey>(new());
 //   public ICollection<TValue> Values=>this.CreateCollection<ValueIterator<TValue>,TValue>(new());
 //   private IEnumerable<T> Enumerate<TIterator,T>(TIterator Iterator)
 //       where TIterator:ScanIteratorFunctions<T>{
 //       using var e=new Enumerator<
 //           TKey,TValue,Add,
 //           GStoreFunctions,
 //           GAllocator,
 //           TIterator,T
 //       >(this.ClientSession,Iterator);
 //       while(e.MoveNext())
 //           yield return e.Current;
 //   }
 //   IEnumerable<TKey> IReadOnlyDictionary<TKey,TValue>.Keys=>this.Enumerate<KeyIterator<TKey>,TKey>(new());
 //   IEnumerable<TValue> IReadOnlyDictionary<TKey,TValue>.Values=>this.Enumerate<ValueIterator<TValue>,TValue>(new());
    private partial void set_Item(byte[]KeyBytes,byte[]ValueBytes){
        var Session=this.ClientSession;
        var FixedKey = SpanByte.FromPinnedSpan(KeyBytes);
        var KeyValue = SpanByte.FromPinnedSpan(ValueBytes);
        var Status = Session.BasicContext.Upsert(FixedKey,KeyValue);
        Status=Pending(Session,Status);
    }
	//public TValue this[TKey Key] {
	//	get {
	//		var KeyBytes = Serialize(Key);
 //           var Session=this.ClientSession;
 //           var FixedKey = SpanByte.FromPinnedSpan(KeyBytes);
 //           SpanByteAndMemory ValueBytes = default;
 //           var Status = Session.BasicContext.Read(ref FixedKey,ref ValueBytes);
 //           Status=Pending(Session,Status,ref ValueBytes);

 //           //if(Status.IsPending) {
 //           //	this.Session.BasicContext.CompletePendingWithOutputs(out var CompletedOutputs,wait: true);
 //           //	CompletedOutputs.Next();
 //           //	Status=CompletedOutputs.Current.Status;
 //           //	ValueBytes=CompletedOutputs.Current.Output;
 //           //	CompletedOutputs.Dispose();
 //           //} else {
 //           //}
 //           if(Status.NotFound)
 //               throw new KeyNotFoundException();
 //           return Deserialize<TValue>(ValueBytes.AsReadOnlySpan());
	//		unsafe {
	//			fixed(byte* pKey = KeyBytes) {
	//			}
	//		}
	//	}
	//	set {
	//		var KeyBytes = Serialize(Key);
	//		var ValueBytes = Serialize(value);
	//		if(this.WouldOverflowMutable(KeyBytes.Length,ValueBytes.Length)) {
	//			throw this.ThrowLogFull();
	//		}
 //           var Session=this.ClientSession;
 //           var FixedKey = SpanByte.FromPinnedSpan(KeyBytes);
 //           var KeyValue = SpanByte.FromPinnedSpan(ValueBytes);
 //           var Status = Session.BasicContext.Upsert(FixedKey,KeyValue);
 //           Pending(Session,Status);
	//	}
	//}
 //   public override bool IsDisposed=>this.disposed;
 //   private bool disposed;
 //   protected override void Dispose(bool disposing) {
 //       if(this.disposed) return;
 //       this.disposed=true;
 //       base.Dispose(disposing);
 //   }
}
