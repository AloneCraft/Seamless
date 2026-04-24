using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Seamless.SessionFunctions;
using Seamless.Stores;
namespace Seamless.Collections;

public abstract partial class UnmanagedCollection<TKey, TValue, TKeySerializer, TValueSerializer>:Collection<
    TKey,
    TValue,
    TKey,//TSessionKey
    TValue,//TSessionValue
    TValue,//TReadOutput
    TValue,//TAddInput
    UnmanagedRead<
        TKey,
        TValue
    >,
    UnmanagedAdd<
        TKey,
        TValue
    >,
    UnmanagedContainsDelete<
        TKey,
        TValue
    >,
    StoreFunctions<
        TKey,
        TValue,
        UnmanagedKeyComparer<
            TKey
        >,
        DefaultRecordDisposer<
            TKey,
            TValue
        >
    >,
    BlittableAllocator<
        TKey,
        TValue,
        StoreFunctions<
            TKey,
            TValue,
            UnmanagedKeyComparer<
                TKey
            >,
            DefaultRecordDisposer<
                TKey,
                TValue
            >
        >
    >
>
    where TKey : unmanaged, IEquatable<TKey>
    where TValue : unmanaged, IEquatable<TValue>
    where TKeySerializer : ISerializer<TKey>
    where TValueSerializer : ISerializer<TValue> {
    public sealed partial class KeyIterator:ScanIteratorFunctions_Chunk<TKey> {
        private protected override TKey Deserialize(ref TKey key,ref TValue value) =>
            key;
    }
    internal sealed partial class ValueIterator:ScanIteratorFunctions_Chunk<TValue> {
        private protected override TValue Deserialize(ref TKey key,ref TValue value) =>
            value;
    }
    public sealed partial class KeyValueIterator:ScanIteratorFunctions_Chunk<KeyValuePair<TKey,TValue>> {
        private protected override KeyValuePair<TKey,TValue> Deserialize(ref TKey key,ref TValue value) =>
            new(
                key,
                value
            );
    }
    private protected UnmanagedCollection(UnmanagedCollectionStore<TKey,TValue> CollectionStore) : base(CollectionStore) {
    }
    public bool ContainsKey(TKey Key) {
        //
        //
        return this.PrivateProtectedContainsKey(ref Key);
    }
    public bool Contains(TKey Key) => this.ContainsKey(Key);
    private protected override Status Read(TKey Key,out TValue Output) {
        var ClientSession = this.ReadSession;


        Output=default;
        var Status = ClientSession.BasicContext.Read(ref Key,ref Output);
        Status=Pending(ClientSession,Status,ref Output);
        Debug.Assert(Status.IsCompleted);
        return Status;
    }






    public bool Remove(TKey key) {
        var ClientSession = this.ContainsDeleteSession;


        var Input = default(EqualityEmpty);
        var Output = false;
        var Status = ClientSession.BasicContext.RMW(ref key,ref Input,ref Output);
        Status=Pending(ClientSession,Status,ref Output);
        Debug.Assert(Status.IsCompleted);
        Debug.Assert(Status.Found!=Status.NotFound);
        return Output;
    }
    private protected override TryAddResult PrivateTryAdd(TKey key,TValue value) {

        if(this.WouldOverflowMutable(Unsafe.SizeOf<TKey>(),Unsafe.SizeOf<TValue>()))
            return TryAddResult.LogFull;
        var ClientSession = this.Add_ClientSession;
        var Output = false;
        var Status = ClientSession.BasicContext.RMW(ref key,ref value,ref Output);
        Status=Pending(ClientSession,Status,ref Output);
        Debug.Assert(Status.IsCompleted);
        Debug.Assert(Status.NotFound==Output);
        if(Output)
            return TryAddResult.Success;
        return TryAddResult.KeyAlreadyExists;
    }
}
