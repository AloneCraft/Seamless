namespace Seamless.Carrier.Internal;

// storing struct enumerator, with pooling(stack itself is node)
internal sealed class RefStack<T> where T : IDisposable
{
    internal static readonly RefStack<T> DisposeSentinel = new(0);

    static volatile int gate = 0;
    static volatile RefStack<T>? Last = null;

    RefStack<T>? Prev = null; // pooling property

    public static RefStack<T> Rent()
    {
        if (Interlocked.CompareExchange(ref gate, 1, 0) == 0)
        {
            if (Last == null)
            {
                gate = 0;
                return new RefStack<T>(4);
            }

            var rent = Last;
            Last = Last.Prev;

            gate = 0;
            return rent;
        }

        return new RefStack<T>(4);
    }

    public static void Return(RefStack<T> stack)
    {
        stack.Reset();
        if (Interlocked.CompareExchange(ref gate, 1, 0) == 0)
        {
            stack.Prev = Last;
            Last = stack;
            gate = 0;
        }
    }

    // ---

    T[] array;
    int size = 0;

    RefStack(int initialSize)
    {
        this.array = initialSize == 0 ? [] : new T[initialSize];
        this.size = 0;
    }

    public void Push(T value)
    {
        if (this.size ==this.array.Length)
        {
            Array.Resize(ref this.array,this.array.Length * 2); // I don't care if the stack is not deep enough to overflow.
        }
        this.array[this.size++] = value;
    }

    public void Pop()
    {
        this.size--;
    }

    public ref T PeekRefOrNullRef()
    {
        if (this.size == 0)
        {
            return ref Unsafe.NullRef<T>();
        }
        return ref this.array[this.size - 1];
    }

    public void Reset()
    {
        for (int i = 0; i <this.size; i++)
        {
            this.array[i].Dispose();
        }

        this.size = 0;
    }
}
