using System.Collections;
using System.Diagnostics;
using Seamless.SessionFunctions;
using Seamless.Stores;
namespace Seamless.Collections;

public abstract class ManagedCollection<TKey, TValue, TKeySerializer, TValueSerializer>:Collection<
    TKey,//TKey
    TValue,//TValue
    SpanByte,//TSessionKey
    SpanByte,//TSessionValue
    SpanByteAndMemory,//TReadOutput
    SpanByte,//TAddInput
    ManagedRead,//TReadFunctions
    ManagedAdd,//TAddFunctions
    ManagedContainsDelete,//TContainsDeleteFunction
    ManagedStoreFunctions,//TStoreFunctions
    ManagedAllocator//TAllocator
>
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue>
    where TKeySerializer : ISerializer<TKey>
    where TValueSerializer : ISerializer<TValue> {
    public sealed class KeyIterator:ScanIteratorFunctions_List<TKey,ManagedList<TKey>> {
        private protected override TKey Deserialize(ref SpanByte key,ref SpanByte value) =>
            TKeySerializer.Deserialize(key.AsReadOnlySpan());
    }
    internal sealed class ValueIterator:ScanIteratorFunctions_List<TValue,ManagedList<TValue>> {
        private protected override TValue Deserialize(ref SpanByte key,ref SpanByte value) =>
            TValueSerializer.Deserialize(value.AsReadOnlySpan());
    }
    public sealed class KeyValueIterator:ScanIteratorFunctions_List<KeyValuePair<TKey,TValue>,ManagedList<KeyValuePair<TKey,TValue>>> {
        private protected override KeyValuePair<TKey,TValue> Deserialize(ref SpanByte key,ref SpanByte value) {
            var Key = TKeySerializer.Deserialize(key.AsReadOnlySpan());
            var Value = TValueSerializer.Deserialize(value.AsReadOnlySpan());
            return new(Key,Value);
        }
    }
    private protected ManagedCollection(ManagedCollectionStore<TKey,TValue> CollectionStore) : base(CollectionStore) {
    }
    public unsafe bool ContainsKey(TKey Key) {
        var KeyBytes = TKeySerializer.Serialize(ref Key);
        fixed(byte* _ = KeyBytes) {
            var SessionKey = SpanByte.FromPinnedSpan(KeyBytes);
            return this.PrivateProtectedContainsKey(ref SessionKey);
        }
    }
    public bool Contains(TKey Key) {
        return this.ContainsKey(Key);
    }
    private protected override unsafe Status Read(TKey Key,out TValue Value) {
        var ClientSession = this.ReadSession;
        var KeyBytes = TKeySerializer.Serialize(ref Key);
        var Output = default(SpanByteAndMemory);
        Status Status;
        fixed(byte* _ = KeyBytes) {
            var FixedKey = SpanByte.FromPinnedSpan(KeyBytes);
            Status=ClientSession.BasicContext.Read(ref FixedKey,ref Output);
            Status=Pending(ClientSession,Status,ref Output);
        }
        Debug.Assert(Status.IsCompleted);
        if(Status.Found)
            Value=TValueSerializer.Deserialize(Output.AsReadOnlySpan());
        else
            Value=default!;
        return Status;
    }
    public unsafe bool Remove(TKey key) {
        var ClientSession = this.DeleteSession;
        var KeyBytes = TKeySerializer.Serialize(ref key);
        var Input = default(EqualityEmpty);
        var Output = false;
        Status Status;
        fixed(byte* _ = KeyBytes) {
            var FixedKey = SpanByte.FromPinnedSpan(KeyBytes);
            Status=ClientSession.BasicContext.RMW(ref FixedKey,ref Input,ref Output);
            Status=Pending(ClientSession,Status,ref Output);
        }
        Debug.Assert(Status.IsCompleted);
        return Output;
    }
    private protected override unsafe TryAddResult PrivateTryAdd(TKey Key,TValue Value) {
        var KeyBytes = TKeySerializer.Serialize(ref Key);
        var ValueBytes = TValueSerializer.Serialize(ref Value);
        if(this.WouldOverflowMutable(KeyBytes.Length,ValueBytes.Length))
            return TryAddResult.LogFull;
        var ClientSession = this.Add_ClientSession;
        var Output = false;
        Status Status;
        fixed(byte* _ = KeyBytes)
        fixed(byte* __ = ValueBytes) {
            var FixedKey = SpanByte.FromPinnedSpan(KeyBytes);
            var FixedInput = SpanByte.FromPinnedSpan(ValueBytes);
            Status=ClientSession.BasicContext.RMW(ref FixedKey,ref FixedInput,ref Output);
            Status=Pending(ClientSession,Status,ref Output);
        }
        Debug.Assert(Status.IsCompleted);
        if(Output)
            return TryAddResult.Success;
        return TryAddResult.KeyAlreadyExists;
    }
}
