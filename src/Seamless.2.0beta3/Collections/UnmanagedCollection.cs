using Seamless.ScanIteratorFunctions;
using Seamless.SessionFunctions;

using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
namespace Seamless.Collections;
public abstract partial class UnmanagedCollection<TKey,TValue,TKeySerializer,TValueSerializer>:Collection<
    TKey,
    TValue,
    TValue,//TReadOutput
    TValue,//AddInput
    UnmanagedRead<TKey,TValue>,
    UnmanagedAdd<TKey,TValue>,
    ContainsDelete,
    UnmanagedStoreFunctions,
    UnmanagedAllocator
>
    where TKey:unmanaged,IEquatable<TKey>
    where TValue:unmanaged,IEquatable<TValue>
    where TKeySerializer:ISerializer<TKey>
    where TValueSerializer:ISerializer<TValue> {
    public sealed class KeyIterator:ScanIteratorFunctions<TKey> {
        private protected override TKey Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord)=>
            TKeySerializer.Deserialize(logRecord.Key);
    }
    internal sealed class ValueIterator:ScanIteratorFunctions<TValue> {
        private protected override TValue Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord)=>
            TValueSerializer.Deserialize(logRecord.ValueSpan);
    }
    public sealed class KeyValueIterator:ScanIteratorFunctions<KeyValuePair<TKey,TValue>> {
        private protected override KeyValuePair<TKey,TValue> Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord)=>
            new(
                TKeySerializer.Deserialize(logRecord.Key),
                TValueSerializer.Deserialize(logRecord.ValueSpan)
            );
    }
    [StructLayout(LayoutKind.Auto)]
    public struct Enumerator<TIterator,T>(
        ClientSession<
            FixedSpanByteKey,
            EqualityEmpty,
            TValue,
            EqualityEmpty,
            UnmanagedRead<TKey,TValue>,
            UnmanagedStoreFunctions,
            UnmanagedAllocator
        > session,
        TIterator iterator
    ):IEnumerator<T>
        where TIterator:ScanIteratorFunctions<T> {
        private TIterator Iterator=iterator;
        private long Cursor=0,Count;
        private int Index=-1;
        private bool Done;
        public T Current=>this.Iterator.Chunk[this.Index];
        object? IEnumerator.Current=>this.Current;
        public bool MoveNext(){
            if(++this.Index<this.Count){
                return true;
            }
            if(this.Done){
                this.Index=-1;
                return false;
            }
            this.Iterator.Count=0;
            var r=session.IterateLookup(ref this.Iterator,ref this.Cursor,resetCursor:false);
            var Count=this.Iterator.Count;
            this.Count=Count;
            if(!r)
                this.Done=true;
            if(Count>0){
                this.Index=0;
                return true;
            }
            this.Index=-1;
            return false;
        }
        public void Reset(){
            this.Cursor=0;
            this.Count=0;
            this.Index=-1;
            this.Done=false;
        }
        public void Dispose(){
        }
    }
    /// <summary>
    /// キー値で存在判定する。
    /// </summary>
    public bool ContainsKey(TKey Key) => this.ContainsKey(TKeySerializer.Serialize(ref Key));

    public bool Contains(TKey Key) => this.ContainsKey(TKeySerializer.Serialize(ref Key));

    public bool Remove(TKey key) {
        var DeleteClientSession = this.DeleteClientSession;
        var KeyBytes = TKeySerializer.Serialize(ref key);
        var Input = default(EqualityEmpty);
        var Output = false;
        var Status = DeleteClientSession.BasicContext.RMW((FixedSpanByteKey)KeyBytes, ref Input, ref Output);
        Status = Pending(DeleteClientSession, Status, ref Output);
        return Output;
    }

    private protected override Status Read(TKey Key,out TValue Output) {
        var Session = this.ReadClientSession;
        Output = default;
        var Status = Session.BasicContext.Read((FixedSpanByteKey)TKeySerializer.Serialize(ref Key), ref Output);
        return Pending(Session, Status, ref Output);
    }

    private protected override TryAddResult PrivateTryAdd(TKey Key,TValue Value) {
        if(this.WouldOverflowMutable(Unsafe.SizeOf<TKey>(),Unsafe.SizeOf<TValue>())) {
            return TryAddResult.LogFull;
        }
        var KeyBytes = TKeySerializer.Serialize(ref Key);
        var Session = this.AddClientSession;
        var Output = false;
        var Status = Session.BasicContext.RMW((FixedSpanByteKey)KeyBytes, ref Value, ref Output);
        Status = Pending(Session, Status, ref Output);
        if(Output)
            return TryAddResult.Success;
        else
            return TryAddResult.KeyAlreadyExists;
    }
}