using Seamless.ScanIteratorFunctions;
using Seamless.SessionFunctions;
using System.Runtime.CompilerServices;
using Seamless.Stores;
namespace Seamless.Collections;

public partial class UnmanagedDictionary<
    TKey,
    TValue,
    TKeySerializer,
    TValueSerializer
>
    where TKey : unmanaged, IEquatable<TKey>
    where TValue : unmanaged, IEquatable<TValue>
    where TKeySerializer : ISerializer<TKey>
    where TValueSerializer : ISerializer<TValue> {

    private ClientSession<
        TKey,
        TValue,
        EqualityEmpty,
        TValue,
        EqualityEmpty,
        UnmanagedRemove_TKey_TValue<TKey,TValue>,
        StoreFunctions<
            TKey,
            TValue,
            UnmanagedKeyComparer<TKey>,
            DefaultRecordDisposer<TKey,TValue>
        >,
        BlittableAllocator<
            TKey,
            TValue,
            StoreFunctions<
                TKey,
                TValue,
                UnmanagedKeyComparer<TKey>,
                DefaultRecordDisposer<TKey,TValue>
            >
        >
    > Remove_TKey_TValue_ClientSession { get; set; }
    private ClientSession<
        TKey,
        TValue,
        TValue,
        bool,
        EqualityEmpty,
        UnmanagedRemove_KeyValuePair<TKey,TValue>,
        StoreFunctions<
            TKey,
            TValue,
            UnmanagedKeyComparer<TKey>,
            DefaultRecordDisposer<TKey,TValue>
        >,
        BlittableAllocator<
            TKey,
            TValue,
            StoreFunctions<
                TKey,
                TValue,
                UnmanagedKeyComparer<TKey>,
                DefaultRecordDisposer<TKey,TValue>
            >
        >
    > Remove_KeyValuePair_ClientSession { get; set; }
    internal UnmanagedDictionary(UnmanagedCollectionStore<TKey,TValue> CollectionStore) : base(CollectionStore){
        var Store = CollectionStore.Store;
        this.Remove_TKey_TValue_ClientSession=Store.NewSession<
            
            EqualityEmpty,
            TValue,
            EqualityEmpty,
            UnmanagedRemove_TKey_TValue<
                TKey,
                TValue
            >
        >(new());
        this.Remove_KeyValuePair_ClientSession=Store.NewSession<
            
            TValue,
            bool,
            EqualityEmpty,
            UnmanagedRemove_KeyValuePair<
                TKey,
                TValue
            >
        >(new());
    }
    public Enumerator<
        KeyValueIterator,
        KeyValuePair<
            TKey,
            TValue
        >
    > GetEnumerator() => new(this.ReadClientSession,new());
    private IEnumerable<T> Enumerate<TIterator, T>(TIterator iterator)
    where TIterator : ScanIteratorFunctions<T,TKey,TValue> {
        using var e = new Enumerator<TIterator,T>(this.ReadClientSession,iterator);
        while(e.MoveNext()){
            yield return e.Current;
        }
    }
    private List<T> CreateCollection<TIterator, T>(TIterator iterator) 
        where TIterator : ScanIteratorFunctions<T,TKey,TValue> {
        var collection = new List<T>();
        using var e = new Enumerator<TIterator,T>(this.ReadClientSession,iterator);
        while(e.MoveNext()){
            collection.Add(e.Current);
        }
        return collection;
    }
    public partial bool Remove(KeyValuePair<TKey,TValue> item){
        var Session = this.Remove_KeyValuePair_ClientSession;
        var Key = item.Key;
        
        
        
        
        var Input = item.Value;
        var Output = false;
        var Status = Session.BasicContext.RMW(ref Key,ref Input,ref Output);
        Pending(Session,Status,ref Output);
        return Output;
    }
    public partial bool Remove(TKey key,out TValue value){
        var Session = this.Remove_TKey_TValue_ClientSession;
        
        
        var Input = default(EqualityEmpty);
        var Output = default(TValue);
        var Status = Session.BasicContext.RMW(ref key,ref Input,ref Output);
        Status=Pending(Session,Status,ref Output);
        
        if(Status.Found){
            value=Output;
            return true;
        }
        //Unsafe.SkipInit(out value);
        value=default;
        return false;
    }
    private partial void SetValue(TKey key,TValue value){


        if(this.WouldOverflowMutable(Unsafe.SizeOf<TKey>(),Unsafe.SizeOf<TValue>()))
            throw this.ThrowLogFull();
        var session = this.AddClientSession;


        var status = session.BasicContext.Upsert(ref key,ref value);
        status=Pending(session,status);
    }
}
