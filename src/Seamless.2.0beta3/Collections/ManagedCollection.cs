using Seamless.ScanIteratorFunctions;
using Seamless.SessionFunctions;
using Seamless.Store;
namespace Seamless.Collections;
public abstract partial class ManagedCollection<TKey,TValue,TKeySerializer,TValueSerializer>:Collection<
    TKey, 
    TValue,
    SpanByteAndMemory,
    PinnedSpanByte, 
    ManagedRead,
    ManagedAdd,
    ContainsDelete,
    ManagedStoreFunctions, 
    ManagedAllocator
>{
    public override bool Contains(TKey Key) {
        var KeyBytes = this.SerializeKey(ref Key);
        return this.ContainsKey(KeyBytes);
    }

    /// <summary>
    /// シリアライズ済みキーバイト列で直接存在判定する。
    /// </summary>
    internal bool ContainsKey(ReadOnlySpan<byte> KeyBytes) {
        var Session = this.DeleteClientSession;
        var FixedKey = (FixedSpanByteKey)KeyBytes;
        var Output = false;
        var Status = Session.BasicContext.Read(FixedKey, ref Output);
        Status = Pending(Session, Status, ref Output);
        return Status.Found;
    }
    private protected override Status Read(TKey Key,out TValue Value){
        var KeyBytes = this.SerializeKey(ref Key);
        var Session = this.ReadClientSession;
        var FixedKey = (FixedSpanByteKey)KeyBytes;
        var Output = default(SpanByteAndMemory);
        var Status = Session.BasicContext.Read(FixedKey, ref Output);
        Status=Pending(Session, Status, ref Output);
        if(Status.Found){
            Value=TValueSerializer.Deserialize(Output.ReadOnlySpan);
        } else{
            //Unsafe.SkipInit(out Value);
            Value =default!;
        }
        return Status;
    }

    //private protected override TValue ConvertOutput(ref TValue output) => output;

    public override long GetLongCount() {
        var Iterator = new CounterIterator();
        long Cursor = 0;
        this.ReadClientSession.IterateLookup(ref Iterator, ref Cursor, resetCursor: false);
        return Iterator.Counter;
    }

    private protected override TryAddResult PrivateTryAdd(TKey Key, TValue Value) {
        var KeyBytes = this.SerializeKey(ref Key);
        var ValueBytes = this.SerializeValue(ref Value);
        if(this.WouldOverflowMutable(KeyBytes.Length, ValueBytes.Length)) {
            return TryAddResult.LogFull;
        }
        var Session = this.AddClientSession;
        var FixedKey = (FixedSpanByteKey)KeyBytes;
        var FixedInput = PinnedSpanByte.FromPinnedSpan(ValueBytes);
        var Output = false;
        var Status = Session.BasicContext.RMW(FixedKey, ref FixedInput, ref Output);
        Status = Pending(Session, Status, ref Output);
        return Output ? TryAddResult.Success : TryAddResult.KeyAlreadyExists;
    }

    public override bool Remove(TKey key){
        var DeleteClientSession = this.DeleteClientSession;
        var KeyBytes = this.SerializeKey(ref key);
        var FixedKey = (FixedSpanByteKey)KeyBytes;
        var Input = default(EqualityEmpty);
        var Output = false;
        var Status = DeleteClientSession.BasicContext.RMW(FixedKey, ref Input, ref Output);
        Status = Pending(DeleteClientSession, Status, ref Output);
        return Output;
    }

    }
