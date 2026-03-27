using System.Diagnostics;
using Garnet.common;
namespace Seamless.Collections;

using MemoryPack;
using System.Diagnostics.CodeAnalysis;
using Tsavorite.core;
public abstract class Collection<TKey, TValue, TSessionFunctions, TStoreFunctions, TAllocator>:IDisposable
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue>
    where TSessionFunctions : ISessionFunctions<PinnedSpanByte,SpanByteAndMemory,Empty>
    where TStoreFunctions : IStoreFunctions
    where TAllocator : IAllocator<TStoreFunctions> {
    private protected const int RecordOverheadBytes = 128;
    internal readonly TsavoriteKV<TStoreFunctions,TAllocator> Store;
    private protected readonly long LogMemorySize;
    private protected readonly double MutableFraction;
    private protected readonly TSessionFunctions SessionFunctions;
    public ClientSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,TSessionFunctions,TStoreFunctions,TAllocator> Session;
    private protected readonly ClientSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,DeleteSessionFunctions,TStoreFunctions,TAllocator> DeleteSession;
    private protected Collection(TsavoriteKV<TStoreFunctions,TAllocator> Store,TSessionFunctions SessionFunctions,long LogMemorySize = 0,double MutableFraction = 1.0) {
        this.Store=Store;
        this.LogMemorySize=LogMemorySize;
        this.MutableFraction=MutableFraction;
        this.SessionFunctions=SessionFunctions;
        this.Session=Store.NewSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,TSessionFunctions>(SessionFunctions);
        this.DeleteSession=Store.NewSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,DeleteSessionFunctions>(new DeleteSessionFunctions());
    }
    private protected OutOfMemoryException ThrowLogFull() {
        var Log = this.Store.Log;
        return new OutOfMemoryException($"Tsavorite mutable log is full. BeginAddress: {Log.BeginAddress}, HeadAddress: {Log.HeadAddress}, TailAddress: {Log.TailAddress}");
    }
    private protected bool WouldOverflowMutable(int KeyLength,int ValueLength) {
        //return false;
        if(this.LogMemorySize<=0) {
            return false;
        }
        var MutableBytes = (long)(this.LogMemorySize*this.MutableFraction);
        if(MutableBytes<=0) {
            MutableBytes=this.LogMemorySize;
        }
        var Log = this.Store.Log;
        var 使用しているバイト数 = Log.TailAddress-Log.BeginAddress;
        var Required = (long)KeyLength+ValueLength+RecordOverheadBytes;
        var 実行後の使用しているバイト数 = 使用しているバイト数+Required;
        return 実行後の使用しているバイト数>MutableBytes;
    }
    private protected static byte[] Serialize<T>(T value) => MemoryPackSerializer.Serialize(value);
    private protected static T Deserialize<T>(ReadOnlySpan<byte> bytes) => MemoryPackSerializer.Deserialize<T>(bytes)!;
    public long GetLongCount() {
        var Iterator = new CounterIterator();
        long Cursor = 0;
        this.Session.IterateLookup(ref Iterator,ref Cursor,resetCursor: false);
        return Iterator.Counter;
    }
    public int Count => (int)this.GetLongCount();
    public long EntryCount => this.Store.EntryCount;
    private protected Status Read(TKey Key,ref SpanByteAndMemory Output) {
        var KeyBytes = Serialize(Key);
        var FixedKey = (FixedSpanByteKey)(ReadOnlySpan<byte>)KeyBytes;
        var Status = this.Session.BasicContext.Read(FixedKey,ref Output);
        return Status;
    }
    private protected static Status Pending<TSessionFunctions0,TStoreFunctions0,TAllocator0>(ClientSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,TSessionFunctions0,TStoreFunctions0,TAllocator0>Session,Status Status)
        where TSessionFunctions0:ISessionFunctions<PinnedSpanByte,SpanByteAndMemory,Empty>
        where TStoreFunctions0  :IStoreFunctions
        where TAllocator0 : IAllocator<TStoreFunctions0>
    {
        if(Status.IsPending) {
            Session.BasicContext.CompletePendingWithOutputs(out var CompletedOutputs,wait: true);
            CompletedOutputs.Next();
            Status=CompletedOutputs.Current.Status;
            CompletedOutputs.Dispose();
        } else {
        }
        return Status;
    }
    private protected TryAddResult PrivateTryAdd(TKey Key,TValue Value){
        var KeyBytes = Serialize(Key);
        var ValueBytes = Serialize(Value);
        if(this.WouldOverflowMutable(KeyBytes.Length,ValueBytes.Length)) {
            return TryAddResult.LogFull;
        }
        var Input = PinnedSpanByte.FromPinnedSpan(ValueBytes);
        SpanByteAndMemory output = default;
        var FixedKey = (FixedSpanByteKey)(ReadOnlySpan<byte>)KeyBytes;
        var Status = this.Session.BasicContext.RMW(FixedKey,ref Input,ref output);
        Status=Pending(this.Session,Status);
        if(Status.NotFound) {
            return TryAddResult.Success;
        }
        return TryAddResult.KeyAlreadyExists;
    }
    public bool TryGetValue(TKey Key,[MaybeNullWhen(false)] out TValue Value) {
        SpanByteAndMemory Output = default;
        var Status = this.Read(Key,ref Output);
        if(Status.IsPending) {
            this.Session.BasicContext.CompletePendingWithOutputs(out var CompletedOutputs,wait: true);
            CompletedOutputs.Next();
            Status=CompletedOutputs.Current.Status;
            Output=CompletedOutputs.Current.Output;
            CompletedOutputs.Dispose();
        } else {
        }
        if(Status.NotFound) {
            Value=default;
            return false;
        }
        Value=Deserialize<TValue>(Output.ReadOnlySpan);
        return true;
    }
    private protected bool PrivateContainsKey(TKey Key) {
        SpanByteAndMemory Output = default;
        var Status = this.Read(Key,ref Output);
        Status=Pending(this.Session,Status);
        Debug.Assert(Status.Found==!Status.NotFound);
        return !Status.NotFound;
    }
    public bool Remove(TKey key) {
        var KeyBytes = Serialize(key);
        var FixedKey = (FixedSpanByteKey)KeyBytes;
        var Input = default(PinnedSpanByte);
        SpanByteAndMemory Output = default;
        var DeleteSession = this.DeleteSession;
        var Status = DeleteSession.BasicContext.RMW(FixedKey,ref Input,ref Output);
        Pending(DeleteSession,Status);
        //if(Status.IsPending) {
        //    DeleteSession.BasicContext.CompletePendingWithOutputs(out var CompletedOutputs,wait: true);
        //    CompletedOutputs.Next();
        //    Status=CompletedOutputs.Current.Status;
        //    CompletedOutputs.Dispose();
        //} else {
        //}
        Debug.Assert(Status.Found!=Status.NotFound);
        return !Status.NotFound;
    }
    //public bool Remove(TKey Key) {
    //    var KeyBytes = Serialize(Key);
    //    var FixedKey = (FixedSpanByteKey)KeyBytes;
    //    var Input = default(PinnedSpanByte);
    //    SpanByteAndMemory Output = default;
    //    var DeleteSession = this.DeleteSession;
    //    var Status = DeleteSession.BasicContext.RMW(FixedKey,ref Input,ref Output);
    //    if(Status.IsPending) {
    //        DeleteSession.BasicContext.CompletePendingWithOutputs(out var CompletedOutputs,wait: true);
    //        CompletedOutputs.Next();
    //        Status=CompletedOutputs.Current.Status;
    //        CompletedOutputs.Dispose();
    //    } else {
    //    }
    //    return !Status.NotFound;
    //}
    public void Clear() {
        var Session = this.Session;
        Session.BasicContext.CompletePending(wait: true);
        Session.Dispose();
        this.Store.Reset();
        this.Session=this.Store.NewSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,TSessionFunctions>(this.SessionFunctions);
    }









    private protected KeyEnumerable<TKey,TValue,TSessionFunctions,TStoreFunctions,TAllocator> GetKeyEnumerable() => new(this.Session);
    private protected ValueEnumerable<TKey,TValue,TSessionFunctions,TStoreFunctions,TAllocator> GetValueEnumerable() => new(this.Session);
    private protected KeyValueEnumerable<TKey,TValue,TSessionFunctions,TStoreFunctions,TAllocator> GetKeyValueEnumerable() => new(this.Session);
    public bool IsDisposed => this.disposed;
    private bool disposed;
    public void Dispose() {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }
    protected virtual void Dispose(bool disposing) {
        if(this.disposed) return;
        if(disposing) {
            //マネージドリソースの解放
            this.Session.Dispose();
        }
        //アンマネージドリソースの解放（必要なければ省略可）
        this.disposed=true;
    }
    [ExcludeFromCodeCoverage]~Collection() => this.Dispose(false);
}
