using Seamless.Persisted.Collections;
namespace Seamless.Tsavorite.Collections;
/// <summary>
/// これはSystem.Collections.Generic.List<T>に使えるならそれに置き換えてこれは削除して
/// </summary>
/// <typeparam name="T"></typeparam>
public sealed class InMemoryList<T>:PersistedList<T> {
    private readonly List<T> items;
    private TempListTuning tuning = new(FileAccess.ReadWrite,FileOptions.SequentialScan);

    public override FileAccess OptimizationMode => tuning.OptimizationMode;
    public override FileOptions AccessPattern => tuning.AccessPattern;

    public InMemoryList(long initialCapacity = 16) {
        if(initialCapacity<1)
            throw new ArgumentOutOfRangeException(nameof(initialCapacity));
        if(initialCapacity>int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(initialCapacity));

        items=new(checked((int)initialCapacity));
        Capacity=items.Capacity;
    }

    public override void Add(T item) {
        ThrowIfDisposed();

        items.Add(item);
        Count=items.Count;
        Capacity=items.Capacity;
    }

    public override void SetTuning(TempListTuning tuning) {
        ThrowIfDisposed();
        this.tuning=tuning;
    }

    public new ref T this[int index] => ref this[checked((long)index)];

    public new ref T this[long index] {
        get {
            ThrowIfDisposed();
            if((ulong)index>=(ulong)Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            return ref CollectionsMarshal.AsSpan(items)[checked((int)index)];
        }
    }

    public override ReadOnlySpan<T> AsSpan(long start,int length) {
        ThrowIfDisposed();
        ValidateSpanRange(start,length);
        return CollectionsMarshal.AsSpan(items).Slice(checked((int)start),length);
    }

    protected override T GetItem(long index) =>
        items[checked((int)index)];

    protected override void OnCountChanged() {
        var targetCount = checked((int)Count);
        if(targetCount<items.Count) {
            items.RemoveRange(targetCount,items.Count-targetCount);
        }
        Capacity=Math.Max(Capacity,items.Capacity);
    }

    protected override void DisposeCore() {
        items.Clear();
        Count=0;
    }
}
