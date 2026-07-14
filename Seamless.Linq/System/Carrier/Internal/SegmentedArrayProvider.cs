namespace Seamless.System.Carrier.Internal;

// similar as IBufferWriter style to avoid Add frequently.
internal ref struct SegmentedArrayProvider<T> {
    private const int ArrayMaxLength = 0X7FFFFFC7;
    private Span<T> currentSegment;
    private int countInCurrentSegment;
    private readonly Span<T> initialBuffer;
    private InlineArray27<T[]> segments; // rented buffers
    private int segmentsCount;
    private int countInFinishedSegments;

    public int Count => checked(this.countInFinishedSegments+this.countInCurrentSegment);

    public bool IsInitialBufferOnly => this.segmentsCount==0;

    public SegmentedArrayProvider(Span<T> initialBuffer) {
        this.initialBuffer=this.currentSegment=initialBuffer;
    }

    public Span<T> GetSpan() {
        var span = this.currentSegment;
        var index = this.countInCurrentSegment;
        if((uint)index<(uint)span.Length) {
            return span[index..];
        } else {
            this.Expand();
            return this.currentSegment;
        }
    }

    public void Advance(int count) {
        this.countInCurrentSegment+=count;
    }
    private void Expand() {
        var currentSegmentLength = this.currentSegment.Length;
        checked { this.countInFinishedSegments+=currentSegmentLength; }

        if(this.countInFinishedSegments>ArrayMaxLength) {
            throw new OutOfMemoryException();
        }

        var newSegmentLength = (int)Math.Min(Math.Max(16,currentSegmentLength)*2L,ArrayMaxLength);
        this.currentSegment=
#if NET8_0_OR_GREATER
            this.segments[this.segmentsCount]
#else
            InlineArrayMarshal.ElementRef<InlineArray27<T[]>, T[]>(ref segments, segmentsCount)
#endif
            =ArrayPool<T>.Shared.Rent(newSegmentLength);
        this.countInCurrentSegment=0;
        this.segmentsCount++;
    }

    public void CopyToAndClear(Span<T> destination) {
        int segmentIndex = this.segmentsCount;
        if(segmentIndex!=0) {
            var first = this.initialBuffer;
            first.CopyTo(destination);
            destination=destination[first.Length..];

            segmentIndex--;
            if(segmentIndex!=0) // skip for last
            {
#if NET8_0_OR_GREATER
                ReadOnlySpan<T[]> segmentSpan = ((ReadOnlySpan<T[]>)this.segments)[..segmentIndex];
#else
                ReadOnlySpan<T[]> segmentSpan = segments.AsSpan().Slice(0, segmentIndex);
#endif
                foreach(var array in segmentSpan) {
                    // copy full segments
                    var segment = array.AsSpan();
                    segment.CopyTo(destination);
                    destination=destination[segment.Length..];

                    // return to pool
                    ArrayPool<T>.Shared.Return(array,clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
                }
#if NETSTANDARD2_0
                segments.Clear();
#endif
            }

            // copy current(last) buffer
            var lastSegment = this.segments[segmentIndex];
            lastSegment.AsSpan(0,this.countInCurrentSegment).CopyTo(destination);
            ArrayPool<T>.Shared.Return(lastSegment,clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        } else {
            // only copy initial buffer
            this.currentSegment[..this.countInCurrentSegment].CopyTo(destination);
        }
    }
}

#if NET10_0_OR_GREATER
// .NET 10 or later supports build-in InlineArray16 implementation
// https://source.dot.net/#System.Private.CoreLib/src/libraries/System.Private.CoreLib/src/System/Runtime/CompilerServices/InlineArray.cs
#elif NET8_0_OR_GREATER

[InlineArray(16)]
internal struct InlineArray16<T>
{
    T item;
}

#else

#if! NETSTANDARD2_0

[StructLayout(LayoutKind.Sequential)]
internal struct InlineArray16<T>
{
    T item0;
    T item1;
    T item2;
    T item3;
    T item4;
    T item5;
    T item6;
    T item7;
    T item8;
    T item9;
    T item10;
    T item11;
    T item12;
    T item13;
    T item14;
    T item15;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [UnscopedRef]
    internal Span<T> AsSpan()
    {
        return InlineArrayMarshal.AsSpan<InlineArray16<T>, T>(ref this, 16);
    }

}

#endif

#endif


#if NET8_0_OR_GREATER

[InlineArray(27)]
internal struct InlineArray27<T> {
    private T item;
}

#else

[StructLayout(LayoutKind.Sequential)]
internal struct InlineArray27<T>
{
    T item0;
    T item1;
    T item2;
    T item3;
    T item4;
    T item5;
    T item6;
    T item7;
    T item8;
    T item9;
    T item10;
    T item11;
    T item12;
    T item13;
    T item14;
    T item15;
    T item16;
    T item17;
    T item18;
    T item19;
    T item20;
    T item21;
    T item22;
    T item23;
    T item24;
    T item25;
    T item26;

    public ref T this[int index]
    {
        [UnscopedRef]
        get => ref InlineArrayMarshal.ElementRef<InlineArray27<T>, T>(ref this, index);
    }

#if !NETSTANDARD2_0

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [UnscopedRef]
    internal Span<T> AsSpan()
    {
        return InlineArrayMarshal.AsSpan<InlineArray27<T>, T>(ref this, 27);
    }

#else

    // NetStandard2.0 version is not intended to performance, just for compatibility of implementation.

    [ThreadStatic]
    static T[]? threadStaticArray;

    // be careful, need to call Clear after use.
    public Span<T> AsSpan()
    {
        var array = threadStaticArray;
        if (array == null)
        {
            array = threadStaticArray = new T[27];
        }

        array[0] = item0;
        array[1] = item1;
        array[2] = item2;
        array[3] = item3;
        array[4] = item4;
        array[5] = item5;
        array[6] = item6;
        array[7] = item7;
        array[8] = item8;
        array[9] = item9;
        array[10] = item10;
        array[11] = item11;
        array[12] = item12;
        array[13] = item13;
        array[14] = item14;
        array[15] = item15;
        array[16] = item16;
        array[17] = item17;
        array[18] = item18;
        array[19] = item19;
        array[20] = item20;
        array[21] = item21;
        array[22] = item22;
        array[23] = item23;
        array[24] = item24;
        array[25] = item25;
        array[26] = item26;

        return threadStaticArray.AsSpan();
    }

    public void Clear()
    {
        var array = threadStaticArray;
        if (array != null)
        {
            Array.Clear(array, 0, array.Length);
        }
    }

#endif
}

#endif


#if !NET8_0_OR_GREATER

internal static class InlineArrayMarshal
{
#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Span<TElement> AsSpan<TBuffer, TElement>(ref TBuffer buffer, int length)
    {
        return MemoryMarshal.CreateSpan(ref Unsafe.As<TBuffer, TElement>(ref buffer), length);
    }
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ref TElement FirstElementRef<TBuffer, TElement>(ref TBuffer buffer)
    {
        return ref Unsafe.As<TBuffer, TElement>(ref buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ref TElement ElementRef<TBuffer, TElement>(ref TBuffer buffer, int index)
    {
        return ref Unsafe.Add(ref Unsafe.As<TBuffer, TElement>(ref buffer), index);
    }
}

#endif
