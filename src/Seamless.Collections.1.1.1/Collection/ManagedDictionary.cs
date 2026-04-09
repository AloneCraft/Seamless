using Seamless.SessionFunctions;
using Seamless.ScanIteratorFunctions;
using System.Diagnostics.CodeAnalysis;
using Seamless.Store;
namespace Seamless.Collections;
public abstract partial class ManagedDictionary<TKey, TValue, TKeySerializer, TValueSerializer> {
	private ClientSession<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,core_Empty,Remove_TKey_TValue,StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>>> Remove_TKey_TValue_ClientSession{get;set;}
	private ClientSession<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,core_Empty,Remove_KeyValuePair   ,StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>>>Remove_KeyValuePair_ClientSession{get;set;}
	internal ManagedDictionary(ManagedCollectionStore<TKey,TValue>CollectionStore) : base(CollectionStore) { 
		var Store=CollectionStore.Store;
		this.Remove_TKey_TValue_ClientSession=Store.NewSession<SpanByte,SpanByteAndMemory,core_Empty,Remove_TKey_TValue>(new ());
		this.Remove_KeyValuePair_ClientSession=Store.NewSession<SpanByte,SpanByteAndMemory,core_Empty,Remove_KeyValuePair>(new ());
	}
    public bool Remove(KeyValuePair<TKey,TValue> item) {
        var Session = this.Remove_KeyValuePair_ClientSession;
        var Key0 = item.Key;
        var Value0 = item.Value;
        var KeyBytes = SerializeKey(ref Key0);
        var ValueBytes = SerializeValue(ref Value0);
        var FixedKey =SpanByte.FromPinnedSpan(KeyBytes);
        var Input =SpanByte.FromPinnedSpan(ValueBytes);
        var Status = Session.BasicContext.RMW(FixedKey,Input,out var Output);
        Status=Pending(Session,Status,ref Output);
        return Output.Length>0;
    }
    public bool Remove(TKey key,[MaybeNullWhen(false)]out TValue value) {
        var Session = this.Remove_TKey_TValue_ClientSession;
        var KeyBytes = SerializeKey(ref key);
        var FixedKey =SpanByte.FromPinnedSpan(KeyBytes);
        var Input = default(SpanByte);
        var Status = Session.BasicContext.RMW(FixedKey,Input,out var Output);
        Status=Pending(Session,Status,ref Output);
        if(Status.Found) {
            value=TValueSerializer.Deserialize(Output.AsReadOnlySpan());
            return true;
        }
        value =default;
        return false;
    }
    public Enumerator<
		TKey,
		TValue,
		Add,
		StoreFunctions<
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
		KeyValueIterator<TKey,TValue,TKeySerializer,TValueSerializer>,
		KeyValuePair<TKey,TValue>
	> GetEnumerator()=>new (this.ClientSession,new());
    private List<T> CreateCollection<TIterator,T>(TIterator Iterator)where TIterator : ScanIteratorFunctions<T>{
        var Collection = new List<T>();
        using var e = new Enumerator<
            TKey, 
            TValue,
            Add,
            StoreFunctions<
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
            TIterator,
            T
        >(this.ClientSession, Iterator);
        while (e.MoveNext()){
            Collection.Add(e.Current);
        }
        return Collection;
    }
    private IEnumerable<T> Enumerate<TIterator,T>(TIterator Iterator)
        where TIterator:ScanIteratorFunctions<T>{
        using var e=new Enumerator<
            TKey,
            TValue,
            Add,
            StoreFunctions<
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
            TIterator,
            T
        >(this.ClientSession,Iterator);
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
            var KeyBytes = SerializeKey(ref Key);
            var ValueBytes = SerializeValue(ref value);
            if(this.WouldOverflowMutable(KeyBytes.Length,ValueBytes.Length)) {
                throw this.ThrowLogFull();
            } else {
            }
            var Session=this.ClientSession;
            var FixedKey = SpanByte.FromPinnedSpan(KeyBytes);
            var Input = SpanByte.FromPinnedSpan(ValueBytes);
            var Status = Session.BasicContext.Upsert(FixedKey,Input);
            Status=Pending(Session,Status);
        }
    }
}
