using Seamless.SessionFunctions;
using Seamless.ScanIteratorFunctions;
using System.Diagnostics.CodeAnalysis;
using Seamless.Stores;
namespace Seamless.Collections;
public partial class ManagedDictionary<TKey, TValue, TKeySerializer, TValueSerializer>{
    private ClientSession<
        FixedSpanByteKey,//TKey

        EqualityEmpty,//TInput
        SpanByteAndMemory,//TOutput
        EqualityEmpty,//TContext
        Remove_TKey_TValue,//TFunctions
        ManagedStoreFunctions,//TStoreFunctions
        ManagedAllocator//TAllocator
    > Remove_TKey_TValue_ClientSession { get; }
    private ClientSession<
        FixedSpanByteKey,
        //
        PinnedSpanByte,
        bool,
        EqualityEmpty,
        Remove_KeyValuePair,
        ManagedStoreFunctions,
        UnmanagedAllocator
    > Remove_KeyValuePair_ClientSession{get;}
    internal ManagedDictionary(ManagedCollectionStore<TKey,TValue> CollectionStore):base(CollectionStore){
        var Store=CollectionStore.Store;
        this.Remove_TKey_TValue_ClientSession=Store.NewSession<
            FixedSpanByteKey,//TKey
            EqualityEmpty,//TInput
            SpanByteAndMemory,//TOutput
            EqualityEmpty,//TContext
            Remove_TKey_TValue//TFunctions
        >(new());
        this.Remove_KeyValuePair_ClientSession=Store.NewSession<
            FixedSpanByteKey,//TKey
            PinnedSpanByte,//TInput
            bool,//TOutput
            EqualityEmpty,//TContext
            Remove_KeyValuePair//TFunctions
        >(new());
    }
    public bool Remove(KeyValuePair<TKey,TValue> item){
        var Session=this.Remove_KeyValuePair_ClientSession;
        var Key0=item.Key;
        var Value0=item.Value;
        var KeyBytes=TKeySerializer.Serialize(ref Key0);
        var ValueBytes=TValueSerializer.Serialize(ref Value0);
        var FixedKey=(FixedSpanByteKey)KeyBytes;
        var Input=PinnedSpanByte.FromPinnedSpan(ValueBytes);
        var Output=false;
        var Status=Session.BasicContext.RMW(FixedKey,ref Input,ref Output);
        Status=Pending(Session,Status,ref Output);
        return Output;
    }
    public bool Remove(TKey key,[MaybeNullWhen(false)] out TValue value){
        var Session=this.Remove_TKey_TValue_ClientSession;
        var KeyBytes=TKeySerializer.Serialize(ref key);
        var FixedKey=(FixedSpanByteKey)KeyBytes;
        var Input=default(EqualityEmpty);
        SpanByteAndMemory Output=default;
        var Status=Session.BasicContext.RMW(FixedKey,ref Input,ref Output);
        Status=Pending(Session,Status,ref Output);
        // output.Length>0 は InPlaceUpdater/CopyUpdater で値がコピーされた＝成功
        if(Output.Length>0){
            value=TValueSerializer.Deserialize(Output.ReadOnlySpan);
            return true;
        }
        value=default;
        return false;
    }
    public Enumerator<
        KeyValueIterator,
        KeyValuePair<TKey,TValue>
    > GetEnumerator()=>new(this.ReadClientSession,new());
    private List<T> CreateCollection<TIterator,T>(TIterator Iterator) where TIterator:ScanIteratorFunctions<T>{
        var Collection=new List<T>();
        using var e=new Enumerator<TIterator,T>(this.ReadClientSession,Iterator);
        while(e.MoveNext()){
            Collection.Add(e.Current);
        }
        return Collection;
    }
    private IEnumerable<T> Enumerate<TIterator,T>(TIterator Iterator)
        where TIterator:ScanIteratorFunctions<T>{
        using var e=new Enumerator<TIterator,T>(this.ReadClientSession,Iterator);
        while(e.MoveNext())
            yield return e.Current;
    }
    public TValue this[TKey Key]{
        get{
            if(this.TryGetValue(Key,out var Value))
                return Value;
            throw new KeyNotFoundException();
        }
        set{
            var KeyBytes=TKeySerializer.Serialize(ref Key);
            var ValueBytes=TValueSerializer.Serialize(ref value);
            if(this.WouldOverflowMutable(KeyBytes.Length,ValueBytes.Length))
                throw this.ThrowLogFull();
            var Session=this.AddClientSession;
            var FixedKey=(FixedSpanByteKey)KeyBytes;
            var Status=Session.BasicContext.Upsert(FixedKey,ValueBytes);
            Status=Pending(Session,Status);
        }
    }
}
