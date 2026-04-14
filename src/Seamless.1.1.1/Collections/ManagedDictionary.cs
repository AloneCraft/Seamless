using Seamless.SessionFunctions;
using Seamless.ScanIteratorFunctions;
using System.Diagnostics.CodeAnalysis;
using Seamless.Stores;
namespace Seamless.Collections;
public partial class ManagedDictionary<TKey, TValue, TKeySerializer, TValueSerializer> {
    private ClientSession<
        SpanByte,//TKey
        SpanByte,//TValue
        EqualityEmpty,//TInput
        SpanByteAndMemory,//TOutput
        EqualityEmpty,//TContext
        ManagedRemove_TKey_TValue,//TFunctions
        ManagedStoreFunctions,//TStoreFunctions
        ManagedAllocator//TAllocator
    > Remove_TKey_TValue_ClientSession { get; set; }
    private ClientSession<
        SpanByte,
        SpanByte,
        SpanByte,
        bool,
        EqualityEmpty,
        ManagedRemove_KeyValuePair,
        ManagedStoreFunctions,
        ManagedAllocator
    > Remove_KeyValuePair_ClientSession { get; }
    internal ManagedDictionary(ManagedCollectionStore<TKey,TValue> CollectionStore):base(CollectionStore){
        var Store=CollectionStore.Store;
        this.Remove_TKey_TValue_ClientSession=Store.NewSession<
            EqualityEmpty,//TKey
            SpanByteAndMemory,//TValue

            EqualityEmpty,//TContext
            ManagedRemove_TKey_TValue//TFunctions
        >(new());
        this.Remove_KeyValuePair_ClientSession=Store.NewSession<
            SpanByte,//TInput

            bool,//TOutput
            EqualityEmpty,//TContext
            ManagedRemove_KeyValuePair//TFunctions
        >(new());
    }
    public bool Remove(KeyValuePair<TKey,TValue> item){
        var Session=this.Remove_KeyValuePair_ClientSession;
        var Key0=item.Key;
        var Value0=item.Value;
        var KeyBytes=TKeySerializer.Serialize(ref Key0);
        var ValueBytes=TValueSerializer.Serialize(ref Value0);
        var FixedKey=SpanByte.FromPinnedSpan(KeyBytes);
        var Input=SpanByte.FromPinnedSpan(ValueBytes);
        var Output=false;
        var Status=Session.BasicContext.RMW(ref FixedKey,ref Input,ref Output);
        Status=Pending(Session,Status,ref Output);
        return Output;
    }
    public bool Remove(TKey key,[MaybeNullWhen(false)] out TValue value){
        var Session=this.Remove_TKey_TValue_ClientSession;
        var KeyBytes=TKeySerializer.Serialize(ref key);
        var FixedKey=SpanByte.FromPinnedSpan(KeyBytes);
        var Input=default(EqualityEmpty);
        SpanByteAndMemory Output=default;
        var Status=Session.BasicContext.RMW(ref FixedKey,ref Input,ref Output);
        Status=Pending(Session,Status,ref Output);
        // output.Length>0 は InPlaceUpdater/CopyUpdater で値がコピーされた＝成功
        if(Output.Length>0){
            value=TValueSerializer.Deserialize(Output.AsReadOnlySpan());
            return true;
        }
        value=default;
        return false;
    }
    public Enumerator<
        KeyValueIterator,
        KeyValuePair<TKey,TValue>
    > GetEnumerator() => new(this.ReadClientSession,new());
    private List<T> CreateCollection<TIterator, T>(TIterator Iterator) where TIterator:ScanIteratorFunctions<T,SpanByte,SpanByte> {
        var Collection=new List<T>();
        using var e=new Enumerator<TIterator,T>(this.ReadClientSession,Iterator);
        while(e.MoveNext()){
            Collection.Add(e.Current);
        }
        return Collection;
    }
    private IEnumerable<T> Enumerate<TIterator, T>(TIterator Iterator)
        where TIterator:ScanIteratorFunctions<T,SpanByte,SpanByte> {
        using var e=new Enumerator<TIterator,T>(this.ReadClientSession,Iterator);
        while(e.MoveNext())
            yield return e.Current;
    }
    public TValue this[TKey Key] {
        get {
            if(this.TryGetValue(Key,out var Value))
                return Value;
            throw new KeyNotFoundException();
        }
        set {
            var KeyBytes=TKeySerializer.Serialize(ref Key);
            var ValueBytes=TValueSerializer.Serialize(ref value);
            if(this.WouldOverflowMutable(KeyBytes.Length,ValueBytes.Length))
                throw this.ThrowLogFull();
            var Session=this.AddClientSession;
            var FixedKey=SpanByte.FromPinnedSpan(KeyBytes);
            var Status=Session.BasicContext.Upsert(FixedKey,SpanByte.FromPinnedSpan(ValueBytes));
            Status=Pending(Session,Status);
        }
    }
}
