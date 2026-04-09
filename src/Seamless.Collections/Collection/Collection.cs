using MemoryPack;
using System.Diagnostics;
using Seamless.Store;
namespace Seamless.Collections;
#pragma warning disable CS8618
public abstract partial class Collection<
    TKey,
    TValue,
#if TSAVORITE1_1_1
    TSessionKey,
    TSessionValue,
    TInput,
    TOutput,
#endif
    TAddFunctions,
    TDeleteFunctions,
    TStoreFunctions,
    TAllocator
>:IDisposable{
    private protected static ReadOnlySpan<byte>Serialize<T>(T value) => MemoryPackSerializer.Serialize(value);
    private protected static T Deserialize<T>(ReadOnlySpan<byte> bytes) => MemoryPackSerializer.Deserialize<T>(bytes)!;
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

    #if TSAVORITE1_1_1
        private protected readonly CollectionStore<TKey, TValue, TSessionKey, TSessionValue, TStoreFunctions, TAllocator> CollectionStore;
    #else
        private protected readonly CollectionStore<TKey, TValue, TStoreFunctions, TAllocator> CollectionStore;
    #endif

    public abstract bool Remove(TKey key);

    public bool Contains(TKey Key) {
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
            this.ClientSession.Dispose();
        }
        this.disposed = true;
    }

    ~Collection() => this.Dispose(false);
}
