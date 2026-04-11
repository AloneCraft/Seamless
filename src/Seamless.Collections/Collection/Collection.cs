using System.Diagnostics;
namespace Seamless.Collections;
#pragma warning disable CS8618
public abstract partial class Collection<
    TKey,
    TValue,
#if TSAVORITE1_1_1
    TSessionKey,
    TSessionValue,
#endif
    TReadOutput,
    TAddInput,
    TReadFunctions,
    TAddFunctions,
    TDeleteFunctions,
    TStoreFunctions,
    TAllocator
>:IDisposable{
    //private protected static partial ReadOnlySpan<byte> GetReadOnlySpan(SpanByteAndMemory a);
    //private protected static ReadOnlySpan<byte> GetReadOnlySpan(SpanByteAndMemory a) => a.ReadOnlySpan;

    private protected abstract ReadOnlySpan<byte>SerializeKey(ref TKey key);
    private protected abstract ReadOnlySpan<byte>SerializeValue(ref TValue value);

    private protected bool PrivateProtectedWouldOverflowMutable(int KeyLength, int ValueLength) {
        Debug.Assert(this.LogMemorySize >= 8192);
        var MutableBytes = (long)(this.LogMemorySize * this.MutableFraction);
        if(MutableBytes <= 0) {
            MutableBytes = this.LogMemorySize;
        }
        var Log = this.CollectionStore.Store.Log;
        var Used = Log.TailAddress - Log.BeginAddress;
        var Required = (long)KeyLength + ValueLength + RecordOverheadBytes;
        return Used + Required > MutableBytes;
    }

    private protected const int RecordOverheadBytes = 128;
    private protected long LogMemorySize => this.CollectionStore.LogMemorySize;
    private protected double MutableFraction => this.CollectionStore.MutableFraction;

    private protected OutOfMemoryException ThrowLogFull() {
        var Log = this.CollectionStore.Store.Log;
        return new OutOfMemoryException($"Tsavorite mutable log is full. BeginAddress: {Log.BeginAddress}, HeadAddress: {Log.HeadAddress}, TailAddress: {Log.TailAddress}");
    }

    private protected abstract bool WouldOverflowMutable(int KeyLength, int ValueLength);

    public int Count => (int)this.GetLongCount();
    public long EntryCount => this.CollectionStore.Store.EntryCount;

    private protected abstract Status Read(TKey Key,out TValue Output);

    public abstract bool Remove(TKey key);

    public virtual bool Contains(TKey Key) {
        var Status = this.Read(Key, out _);
        Debug.Assert(Status.Found == !Status.NotFound);
        return Status.Found;
    }

    public abstract bool IsDisposed { get; }

    private bool disposed;

    public void Dispose() {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing) {
        if(this.disposed) {
            return;
        }
        if(disposing) {
            this.AddClientSession.Dispose();
        }
        this.disposed = true;
    }

    ~Collection() => this.Dispose(false);
}
