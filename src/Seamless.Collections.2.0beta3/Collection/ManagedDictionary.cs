using Seamless.SessionFunctions;
using Seamless.ScanIteratorFunctions;
using System.Diagnostics.CodeAnalysis;
namespace Seamless.Collections;
public abstract partial class ManagedDictionary<TKey, TValue, TKeySerializer, TValueSerializer> {
    private ClientSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,core_Empty,Remove_TKey_TValue ,StoreFunctions<SpanByteComparer,DefaultRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>> Remove_TKey_TValue_ClientSession{get;set;}
    private ClientSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,core_Empty,Remove_KeyValuePair ,StoreFunctions<SpanByteComparer,DefaultRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>>Remove_KeyValuePair_ClientSession{get;set;}
	internal ManagedDictionary(ManagedCollectionStore<TKey,TValue>CollectionStore) : base(CollectionStore) {
        var Store=CollectionStore.Store;
        this.Remove_TKey_TValue_ClientSession=Store.NewSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,core_Empty,Remove_TKey_TValue>(new ());
        this.Remove_KeyValuePair_ClientSession=Store.NewSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,core_Empty,Remove_KeyValuePair>(new ());
    }
    public bool Remove(KeyValuePair<TKey,TValue> item) {
        var Session = this.Remove_KeyValuePair_ClientSession;
        var Key0 = item.Key;
        var Value0 = item.Value;
        var KeyBytes = SerializeKey(ref Key0);
        var ValueBytes = SerializeValue(ref Value0);
        var FixedKey = (FixedSpanByteKey)(ReadOnlySpan<byte>)KeyBytes;
        var Input = PinnedSpanByte.FromPinnedSpan(ValueBytes);
        var Output = default(SpanByteAndMemory);
        var Status = Session.BasicContext.RMW(FixedKey,ref Input,ref Output);
        Status=Pending(Session,Status,ref Output);
        return Output.Length>0;
    }
    public bool Remove(TKey key,[MaybeNullWhen(false)]out TValue value) {
        var Session = this.Remove_TKey_TValue_ClientSession;
        var KeyBytes = SerializeKey(ref key);
        var FixedKey = (FixedSpanByteKey)(ReadOnlySpan<byte>)KeyBytes;
        var Input = default(PinnedSpanByte);
        SpanByteAndMemory Output = default;
        var Status = Session.BasicContext.RMW(FixedKey,ref Input,ref Output);
        Status=Pending(Session,Status,ref Output);
        if(Status.Found) {
            value=TValueSerializer.Deserialize(Output.ReadOnlySpan);
            return true;
        }
        value=default;
        return false;
    }
    public Enumerator<TKey,TValue,SessionFunctions.Add,StoreFunctions<SpanByteComparer,DefaultRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>,KeyValueIterator<TKey,TValue,TKeySerializer,TValueSerializer>,KeyValuePair<TKey,TValue>> GetEnumerator()=>new (this.ClientSession,new());
    private List<T> CreateCollection<TIterator,T>(TIterator Iterator)where TIterator : ScanIteratorFunctions<T>{
        var Collection = new List<T>();
        using var Enumerator = new Enumerator<TKey,TValue,SessionFunctions.Add,StoreFunctions<SpanByteComparer,DefaultRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>,TIterator,T>(this.ClientSession, Iterator);
        while (Enumerator.MoveNext()){
            Collection.Add(Enumerator.Current);
        }
        return Collection;
    }    
    private IEnumerable<T> Enumerate<TIterator,T>(TIterator Iterator)
        where TIterator:ScanIteratorFunctions<T>{
        using var e=new Enumerator<TKey,TValue,SessionFunctions.Add,StoreFunctions<SpanByteComparer,DefaultRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>,TIterator,T>(this.ClientSession,Iterator);
        while(e.MoveNext())
            yield return e.Current;
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
            var FixedKey=(FixedSpanByteKey)(ReadOnlySpan<byte>)KeyBytes;
            var Input=ValueBytes;
            var Status = Session.BasicContext.Upsert(FixedKey,Input);
            Status=Pending(Session,Status);
        }
    }
}
