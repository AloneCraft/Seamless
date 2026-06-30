namespace Seamless.Tsavorite.Collections;
/// <summary>
/// これはSystem.Collections.Generic.List<T>に使えるならそれに置き換えてこれは削除して
/// </summary>
/// <typeparam name="T"></typeparam>
public sealed class InMemoryList<T>:PersistedList<T> {
    private readonly List<T> items;
    private StorageTuning tuning = StorageTuning.Default;

    public override StorageTuning Tuning => this.tuning;

    public InMemoryList(long initialCapacity = 16) {
        if(initialCapacity<1)
            throw new ArgumentOutOfRangeException(nameof(initialCapacity));
        if(initialCapacity>int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(initialCapacity));

        this.items=new(checked((int)initialCapacity));
        this.Capacity=this.items.Capacity;
    }

    public override void Add(T item) {
        this.ThrowIfDisposed();

        this.items.Add(item);
        this.Count=this.items.Count;
        this.Capacity=this.items.Capacity;
    }

    public override void SetTuning(StorageTuning tuning) {
        this.ThrowIfDisposed();
        this.tuning=tuning;
    }

    public new ref T this[int index] => ref this[checked((long)index)];

    public new ref T this[long index] {
        get {
            this.ThrowIfDisposed();
            if((ulong)index>=(ulong)this.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            return ref CollectionsMarshal.AsSpan(this.items)[checked((int)index)];
        }
    }

    public override ReadOnlySpan<T> AsSpan(long start,int length) {
        this.ThrowIfDisposed();
        this.ValidateSpanRange(start,length);
        return CollectionsMarshal.AsSpan(this.items).Slice(checked((int)start),length);
    }

    protected override T GetItem(long index) =>
        this.items[checked((int)index)];

    protected override void OnCountChanged() {
        var targetCount = checked((int)this.Count);
        if(targetCount<this.items.Count) {
            this.items.RemoveRange(targetCount,this.items.Count-targetCount);
        }
        this.Capacity=Math.Max(this.Capacity,this.items.Capacity);
    }

    protected override void DisposeCore() {
        this.items.Clear();
        this.Count=0;
    }
}
