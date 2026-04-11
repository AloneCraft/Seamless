using Seamless.ScanIteratorFunctions;
using Seamless.SessionFunctions;
using System.Diagnostics;
using System.Runtime.CompilerServices;
namespace Seamless.Collections;

public abstract partial class ManagedCollection<TKey,TValue,TKeySerializer,TValueSerializer>:Collection<
    TKey, 
    TValue,
    SpanByte,//TSessionKey
    SpanByte,//TSessionValue
    SpanByteAndMemory,//TReadOutput
    SpanByte,//TAddInput
    ManagedRead,
    ManagedAdd,
    ManagedDelete,
    ManagedStoreFunctions, 
    ManagedAllocator
>
{
    private protected override Status Read(TKey Key, out TValue Output) {
        var KeyBytes = this.SerializeKey(ref Key);
        var Session = this.ReadClientSession;
        var FixedKey = SpanByte.FromPinnedSpan(KeyBytes);
        SpanByteAndMemory value0 = default;
        var Status = Session.BasicContext.Read(ref FixedKey, ref value0);
        if (Status.IsPending) {
            Session.BasicContext.CompletePendingWithOutputs(out var CompletedOutputs, wait: true);
            using (CompletedOutputs) {
                Debug.Assert(CompletedOutputs.Next());
                Status = CompletedOutputs.Current.Status;
                value0 = CompletedOutputs.Current.Output;
            }
        }
        if(Status.Found){
            Output=TValueSerializer.Deserialize(value0.AsReadOnlySpan());
        } else{
            //Unsafe.SkipInit(out Output);
            Output=default!;
        }
        return Status;
    }

    public override long GetLongCount() {
        var Iterator = new CounterIterator<SpanByte,SpanByte>();
        long Cursor = 0;
        this.AddClientSession.IterateLookup(ref Iterator, ref Cursor, resetCursor: false);
        return Iterator.Counter;
    }

    private protected override TryAddResult PrivateTryAdd(TKey Key, TValue Value) {
        var KeyBytes = this.SerializeKey(ref Key);
        var ValueBytes = this.SerializeValue(ref Value);
        if (this.WouldOverflowMutable(KeyBytes.Length, ValueBytes.Length)) {
            return TryAddResult.LogFull;
        }
        var Session = this.AddClientSession;
        var FixedKey = SpanByte.FromPinnedSpan(KeyBytes);
        var FixedInput = SpanByte.FromPinnedSpan(ValueBytes);
        var Output = false;
        var Status = Session.BasicContext.RMW(ref FixedKey, ref FixedInput, ref Output, default(EqualityEmpty));
        if (Status.IsPending) {
            Session.BasicContext.CompletePendingWithOutputs(out var CompletedOutputs, wait: true);
            using (CompletedOutputs) {
                Debug.Assert(CompletedOutputs.Next());
                Status = CompletedOutputs.Current.Status;
                Output = CompletedOutputs.Current.Output;
            }
        }
        if (Output) {
            return TryAddResult.Success;
        }
        return TryAddResult.KeyAlreadyExists;
    }

    public override bool Remove(TKey key) {
        var DeleteClientSession = this.DeleteClientSession;
        var KeyBytes = this.SerializeKey(ref key);
        var FixedKey = SpanByte.FromPinnedSpan(KeyBytes);
        var Input = default(EqualityEmpty);
        var Output = false;
        var Status = DeleteClientSession.BasicContext.RMW(ref FixedKey, ref Input, ref Output, default(EqualityEmpty));
        if (Status.IsPending) {
            DeleteClientSession.BasicContext.CompletePendingWithOutputs(out var CompletedOutputs, wait: true);
            using (CompletedOutputs) {
                Debug.Assert(CompletedOutputs.Next());
                Status = CompletedOutputs.Current.Status;
                Output = CompletedOutputs.Current.Output;
            }
        }
        return Output;
    }
}
