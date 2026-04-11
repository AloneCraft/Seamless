using Seamless.SessionFunctions;
using Seamless.ScanIteratorFunctions;
using System.Diagnostics.CodeAnalysis;
using Seamless.Store;
namespace Seamless.Collections;
public abstract partial class ManagedDictionary<TKey, TValue, TKeySerializer, TValueSerializer> {
	private ClientSession<SpanByte,SpanByte,EqualityEmpty,SpanByteAndMemory,EqualityEmpty,ManagedRemove_TKey_TValue,StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>>> Remove_TKey_TValue_ClientSession{get;set;}
	private ClientSession<SpanByte,SpanByte,SpanByte,bool,EqualityEmpty,ManagedRemove_KeyValuePair,StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>>>Remove_KeyValuePair_ClientSession{get;set;}
	internal ManagedDictionary(ManagedCollectionStore<TKey,TValue>CollectionStore) : base(CollectionStore) { 
		var Store=CollectionStore.Store;
		this.Remove_TKey_TValue_ClientSession=Store.NewSession<EqualityEmpty,SpanByteAndMemory,EqualityEmpty,ManagedRemove_TKey_TValue>(new ());
		this.Remove_KeyValuePair_ClientSession=Store.NewSession<SpanByte,bool,EqualityEmpty,ManagedRemove_KeyValuePair>(new ());
	}
    public bool Remove(KeyValuePair<TKey,TValue> item) {
        var Session = this.Remove_KeyValuePair_ClientSession;
        var Key0 = item.Key;
        var Value0 = item.Value;
        var KeyBytes =this.SerializeKey(ref Key0);
        var ValueBytes =this.SerializeValue(ref Value0);
        var FixedKey =SpanByte.FromPinnedSpan(KeyBytes);
        var Input =SpanByte.FromPinnedSpan(ValueBytes);
        var Output = false;
        var Status = Session.BasicContext.RMW(ref FixedKey,ref Input,ref Output,default(EqualityEmpty));
        Status=Pending(Session,Status,ref Output);
        return Output;
    }
    public bool Remove(TKey key,[MaybeNullWhen(false)]out TValue value) {
        var Session = this.Remove_TKey_TValue_ClientSession;
        var KeyBytes =this.SerializeKey(ref key);
        var FixedKey =SpanByte.FromPinnedSpan(KeyBytes);
        var Input = default(EqualityEmpty);
        SpanByteAndMemory Output = default;
        var Status = Session.BasicContext.RMW(ref FixedKey,ref Input,ref Output,default(EqualityEmpty));
        Status=Pending(Session,Status,ref Output);
        // output.Length>0 は InPlaceUpdater/CopyUpdater で値がコピーされた＝成功
        if(Output.Length>0) {
            value=TValueSerializer.Deserialize(Output.AsReadOnlySpan());
            return true;
        }
        value =default;
        return false;
    }
	public Enumerator<
		KeyValueIterator<TKey,TValue,TKeySerializer,TValueSerializer>,
		KeyValuePair<TKey,TValue>
	> GetEnumerator()=>new(this.ReadClientSession,new());
    private List<T> CreateCollection<TIterator,T>(TIterator Iterator)where TIterator : ScanIteratorFunctions<T>{
        var Collection = new List<T>();
        using var e = new Enumerator<TIterator, T>(this.ReadClientSession, Iterator);
        while (e.MoveNext()){
            Collection.Add(e.Current);
        }
        return Collection;
    }
    private IEnumerable<T> Enumerate<TIterator,T>(TIterator Iterator)
        where TIterator:ScanIteratorFunctions<T>{
        using var e=new Enumerator<TIterator, T>(this.ReadClientSession,Iterator);
        while(e.MoveNext()){
            yield return e.Current;
        }
    }
    public TValue this[TKey Key] {
        get {
            if(this.TryGetValue(Key,out var Value)) {
                return Value;
            } else {
                throw new KeyNotFoundException();
            }
        }
        set {
            var KeyBytes =this.SerializeKey(ref Key);
            var ValueBytes =this.SerializeValue(ref value);
            if(this.WouldOverflowMutable(KeyBytes.Length,ValueBytes.Length)) {
                throw this.ThrowLogFull();
            } else {
            }
            var Session=this.AddClientSession;
            var FixedKey = SpanByte.FromPinnedSpan(KeyBytes);
            var Input = SpanByte.FromPinnedSpan(ValueBytes);
            var Status = Session.BasicContext.Upsert(FixedKey,Input);
            Status=Pending(Session,Status);
        }
    }
}
