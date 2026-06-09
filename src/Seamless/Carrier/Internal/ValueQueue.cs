namespace Seamless.Carrier.Internal;

internal struct ValueQueue<T>(int capacity): IDisposable
{
    private T[] items = ArrayPool<T>.Shared.Rent(capacity);
    private int head = 0;
    private int tail = 0;
    private int size = 0;

    public int Count => this.size;

    public void Enqueue(T item)
    {
        if (this.items.Length ==this.size)
        {
            this.Grow();
        }

        this.items[this.tail] = item;
        this.tail = (this.tail + 1) %this.items.Length;
        this.size++;
    }

    public T Dequeue()
    {
        if (this.size == 0) Throw();

        T removed = this.items[this.head];
        this.items[this.head] = default!;
        this.head = (this.head + 1) %this.items.Length;
        this.size--;
        return removed;
    }

    static void Throw() => throw new InvalidOperationException("Queue is empty.");

    void Grow()
    {
        T[] newArray = ArrayPool<T>.Shared.Rent(this.items.Length * 2);
        if (this.size > 0)
        {
            if (this.head <this.tail)
            {
                Array.Copy(this.items,this.head, newArray, 0,this.size);
            }
            else
            {
                Array.Copy(this.items,this.head, newArray, 0,this.items.Length -this.head);
                Array.Copy(this.items, 0, newArray,this.items.Length -this.head,this.tail);
            }
        }
        ArrayPool<T>.Shared.Return(this.items, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        this.items = newArray;
        this.head = 0;
        this.tail =this.size;
    }

    public void Dispose()
    {
        if (this.items != null)
        {
            ArrayPool<T>.Shared.Return(this.items, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
            this.items = null!;
        }
    }
}
