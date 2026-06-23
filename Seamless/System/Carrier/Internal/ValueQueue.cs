namespace Seamless.System.Carrier.Internal;

internal struct ValueQueue<T>(int capacity): IDisposable
{
    private T[] items = ArrayPool<T>.Shared.Rent(capacity);
    private int head = 0;
    private int tail = 0;
    private int size = 0;

    public int Count => size;

    public void Enqueue(T item)
    {
        if (items.Length ==size)
        {
            Grow();
        }

        items[tail] = item;
        tail = (tail + 1) %items.Length;
        size++;
    }

    public T Dequeue()
    {
        if (size == 0) Throw();

        T removed = items[head];
        items[head] = default!;
        head = (head + 1) %items.Length;
        size--;
        return removed;
    }

    static void Throw() => throw new InvalidOperationException("Queue is empty.");

    void Grow()
    {
        T[] newArray = ArrayPool<T>.Shared.Rent(items.Length * 2);
        if (size > 0)
        {
            if (head <tail)
            {
                Array.Copy(items,head, newArray, 0,size);
            }
            else
            {
                Array.Copy(items,head, newArray, 0,items.Length -head);
                Array.Copy(items, 0, newArray,items.Length -head,tail);
            }
        }
        ArrayPool<T>.Shared.Return(items, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        items = newArray;
        head = 0;
        tail =size;
    }

    public void Dispose()
    {
        if (items != null)
        {
            ArrayPool<T>.Shared.Return(items, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
            items = null!;
        }
    }
}
