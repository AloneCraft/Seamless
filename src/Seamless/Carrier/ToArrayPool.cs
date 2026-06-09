namespace Seamless.Carrier;

partial class ValueEnumerableExtensions {
    /// <summary>
    /// Converts to an array borrowed from ArrayPool&lt;T&gt;.Shared.
    /// For performance considerations, PooledArray is a struct, so
    /// copying or boxing it risks returning to the ArrayPool multiple times.
    /// Always use it simply with using and do not keep it for long periods.
    /// </summary>
    public static PooledArray<TSource> ToArrayPool<TEnumerator, TSource>(this ValueEnumerable<TEnumerator,TSource> source)
        where TEnumerator : struct, IValueEnumerator<TSource> {
        using var enumerator = source.Enumerator;

        if(enumerator.TryGetNonEnumeratedCount(out var count)) {
            // when count == 0 but always return rental array
            var array = ArrayPool<TSource>.Shared.Rent(count);

            if(array.Length==0) {
                return new(array,count);
            }

            if(enumerator.TryCopyTo(array.AsSpan(0,count),0)) {
                return new(array,count);
            }

            var i = 0;
            while(enumerator.TryGetNext(out var item)) {
                array[i]=item;
                i++;
            }

            return new(array,i);
        }
        else {
#if NETSTANDARD2_0
            Span<TSource> initialBufferSpan = default;
#elif NET8_0_OR_GREATER
            var initialBuffer = default(InlineArray16<TSource>);
            Span<TSource> initialBufferSpan = initialBuffer;
#else
            var initialBuffer = default(InlineArray16<TSource>);
            Span<TSource> initialBufferSpan = initialBuffer.AsSpan();
#endif
            var arrayBuilder = new SegmentedArrayProvider<TSource>(initialBufferSpan);
            var span = arrayBuilder.GetSpan();
            var i = 0;
            while(enumerator.TryGetNext(out var item)) {
                if(i==span.Length) {
                    arrayBuilder.Advance(i);
                    span=arrayBuilder.GetSpan();
                    i=0;
                }

                span[i]=item;
                i++;
            }
            arrayBuilder.Advance(i);

            var array = ArrayPool<TSource>.Shared.Rent(arrayBuilder.Count);
            arrayBuilder.CopyToAndClear(array);
            return new(array,arrayBuilder.Count);

        }
    }
}

/// <summary>
/// Holds an array borrowed from ArrayPool&lt;T&gt;.Shared.Rent.
/// When Disposed, it will Return the array to ArrayPool&lt;T&gt;.Shared.
/// If boxed or passed by copy, there's a risk of multiple Returns.
/// Please use it as is and avoid long-term retention.
/// </summary>
public struct PooledArray<TSource>:IDisposable {
    TSource[] array;
    int size;

    internal PooledArray(TSource[] array,int size) {
        this.array=array;
        this.size=size;
    }

    // for compatibility, we choose Size instead of Length/Count.
    public int Size {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.size;
    }

    public Span<TSource> Span {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.array.AsSpan(0,this.size);
    }

    public Memory<TSource> Memory {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.array.AsMemory(0,this.size);
    }

    public ArraySegment<TSource> ArraySegment {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new ArraySegment<TSource>(this.array,0,this.size);
    }

    public IEnumerable<TSource> AsEnumerable() {
        return this.ArraySegment.AsEnumerable();
    }

#if NET9_0_OR_GREATER
    public ValueEnumerable<FromMemory<TSource>,TSource> AsValueEnumerable() {
        return this.Memory.AsValueEnumerable();
    }
#endif

    // for compatibility

    // get raw array.
    public TSource[] Array => this.array;

    public void Deconstruct(out TSource[] array,out int size) {
        array=this.array;
        size=this.size;

        // when deconstructed once, don't hold array.
        this.array=null!;
    }

    public void Dispose() {
        if(this.array!=null) {
            ArrayPool<TSource>.Shared.Return(this.array,clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<TSource>());
            this.array=null!;
        }
    }
}
