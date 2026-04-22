using Seamless.SessionFunctions;
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
    > Remove_TKey_TValue_ClientSession { get; set; } = null!;
    private ClientSession<
        SpanByte,
        SpanByte,
        SpanByte,
        bool,
        EqualityEmpty,
        ManagedRemove_KeyValuePair,
        ManagedStoreFunctions,
        ManagedAllocator
    > Remove_KeyValuePair_ClientSession { get; set; } = null!;
    internal ManagedDictionary(ManagedCollectionStore<TKey,TValue> CollectionStore) : base(CollectionStore) {
        this.CreateAdditionalSessions();
    }
    private protected override void DisposeAdditionalSessions() {
        CompletePendingAndDispose(this.Remove_TKey_TValue_ClientSession);
        CompletePendingAndDispose(this.Remove_KeyValuePair_ClientSession);
    }
    private protected override void RecreateAdditionalSessions() {
        this.CreateAdditionalSessions();
    }
    private void CreateAdditionalSessions() {
        var Store = this.Store;
        this.Remove_TKey_TValue_ClientSession=Store.NewSession<
            EqualityEmpty,//TKey
            SpanByteAndMemory,//TValue
                              //
            EqualityEmpty,//TContext
            ManagedRemove_TKey_TValue//TFunctions
        >(new());
        this.Remove_KeyValuePair_ClientSession=Store.NewSession<
            SpanByte,//TInput
                     //
            bool,//TOutput
            EqualityEmpty,//TContext
            ManagedRemove_KeyValuePair//TFunctions
        >(new());
    }
    public unsafe bool Remove(KeyValuePair<TKey,TValue> item) {
        var Session = this.Remove_KeyValuePair_ClientSession;
        var Key0 = item.Key;
        var Value0 = item.Value;
        var KeyBytes = TKeySerializer.Serialize(ref Key0);
        var ValueBytes = TValueSerializer.Serialize(ref Value0);
        var Output = false;
        Status Status;
        fixed(byte* _ = KeyBytes)
        fixed(byte* __ = ValueBytes) {
            var FixedKey = SpanByte.FromPinnedSpan(KeyBytes);
            var Input = SpanByte.FromPinnedSpan(ValueBytes);
            Status=Session.BasicContext.RMW(ref FixedKey,ref Input,ref Output);
            Pending(Session,Status,ref Output);
        }
        return Output;
    }
    public unsafe bool Remove(TKey key,[MaybeNullWhen(false)] out TValue value) {
        var Session = this.Remove_TKey_TValue_ClientSession;
        var KeyBytes = TKeySerializer.Serialize(ref key);
        var Input = default(EqualityEmpty);
        SpanByteAndMemory Output = default;
        Status Status;
        fixed(byte* _ = KeyBytes) {
            var FixedKey = SpanByte.FromPinnedSpan(KeyBytes);
            Status=Session.BasicContext.RMW(ref FixedKey,ref Input,ref Output);
            Pending(Session,Status,ref Output);
        }
        // output.Length>0 は InPlaceUpdater/CopyUpdater で値がコピーされた＝成功
        if(Output.Length>0) {
            value=TValueSerializer.Deserialize(Output.AsReadOnlySpan());
            return true;
        }
        value=default;
        return false;
    }
    public Enumerator<
        KeyValueIterator,
        KeyValuePair<TKey,TValue>
    > GetEnumerator() => new(this.ReadSession,new());
    IEnumerator<KeyValuePair<TKey,TValue>> IMemoryCollection<KeyValuePair<TKey,TValue>>.GetEnumerator() => new Enumerator<KeyValueIterator,KeyValuePair<TKey,TValue>>(this.ReadMemoryClientSession,new(),this.HeadAddress);
    private List<T> CreateCollection<TIterator, T>(TIterator Iterator) where TIterator : ScanIteratorFunctions<T>{
        var Collection = new List<T>();
        using var e = new Enumerator<TIterator,T>(this.ReadSession,Iterator);
        while(e.MoveNext()) {
            Collection.Add(e.Current);
        }
        return Collection;
    }
    private IEnumerable<T> Enumerate<TIterator, T>(TIterator Iterator)
        where TIterator : ScanIteratorFunctions<T> {
        using var e = new Enumerator<TIterator,T>(this.ReadSession,Iterator);
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
            var KeyBytes = TKeySerializer.Serialize(ref Key);
            var ValueBytes = TValueSerializer.Serialize(ref value);
            if(this.WouldOverflowMutable(KeyBytes.Length,ValueBytes.Length))
                throw this.ThrowLogFull();
            var ClientSession = this.Add_ClientSession;
            Status Status;
            unsafe {
                fixed(byte* _ = KeyBytes)
                fixed(byte* __ = ValueBytes) {
                    var FixedKey = SpanByte.FromPinnedSpan(KeyBytes);
                    var FixedValue = SpanByte.FromPinnedSpan(ValueBytes);
                    Status=ClientSession.BasicContext.Upsert(FixedKey,FixedValue);
                }
            }
            Pending(ClientSession,Status);
        }
    }
}
