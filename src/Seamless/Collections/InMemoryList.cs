namespace Seamless.Collections;

public sealed class InMemoryList<T>:List<T> {
    private readonly global::System.Collections.Generic.List<T> items;

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