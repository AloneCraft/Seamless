namespace Tsavorite.Carrier.Internal;

[StructLayout(LayoutKind.Auto)]
internal class RentedRingBuffer<T>(int capacity) : IDisposable
{
    public T[]? Buffer = ArrayPool<T>.Shared.Rent(4);
    public readonly int Capacity = capacity;
    int head = 0;
    public int Count = 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Enqueue(T item)
    {
        if (this.head ==this.Buffer!.Length &&this.Count !=this.Capacity)
        {
            this.Expand();
        }


        this.Buffer![this.head] = item;
        var newHead = (this.head + 1);
        this.head = newHead %this.Capacity;
        this.Count = Math.Min(this.Count + 1,this.Capacity);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryDequeue(out T item)
    {
        if (this.Count == 0)
        {
            item = default!;
            return false;
        }

        var tail = ((long)this.head -this.Count +this.Capacity) %this.Capacity;
        ref var tailRef = ref this.Buffer![tail];
        item = tailRef;
        tailRef = default!;
        this.Count--;
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    void Expand()
    {
        var newBuffer = ArrayPool<T>.Shared.Rent(this.Buffer!.Length * 2);
        var prevSpan = this.Buffer.AsSpan();
        prevSpan.CopyTo(newBuffer);
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            prevSpan.Clear();
        }

        ArrayPool<T>.Shared.Return(this.Buffer);
        this.Buffer = newBuffer;
    }

    public void Dispose()
    {
        if (this.Buffer != null)
        {
            // Clear the buffer if T contains references
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                this.Buffer.AsSpan(0,this.Count).Clear();
            }

            ArrayPool<T>.Shared.Return(this.Buffer);
            this.Buffer = null;
        }
    }
}
