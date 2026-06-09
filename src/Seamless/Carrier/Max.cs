namespace Seamless.Carrier;

partial class ValueEnumerableExtensions {
    public static TResult? Max<TEnumerator, TSource, TResult>(this ValueEnumerable<TEnumerator,TSource> source,Func<TSource,TResult> selector)
        where TEnumerator : struct, IValueEnumerator<TSource> {
        ArgumentNullException.ThrowIfNull(selector);

        // If inlined, we could expect a slight performance improvement,
        // but since ZLinq's iteration is already sufficiently fast with no allocations, we'll use Select as is.
        return source.Select(selector).Max();
    }

    // already nullable supported.

    public static TSource? Max<TEnumerator, TSource>(this ValueEnumerable<TEnumerator,TSource> source)
        where TEnumerator : struct, IValueEnumerator<TSource> {
        return Max<TEnumerator,TSource>(source,null);
    }

    public static TSource? Max<TEnumerator, TSource>(this ValueEnumerable<TEnumerator,TSource> source,IComparer<TSource>? comparer)
        where TEnumerator : struct, IValueEnumerator<TSource> {
        comparer??=Comparer<TSource>.Default;

        using(var enumerator = source.Enumerator) {
#if NET8_0_OR_GREATER
            if(enumerator.TryGetSpan(out var span)) {
                return MaxSpan(span,comparer);
            }
#endif
            TSource? value = default;
            if(value is null) {
                // reference type, allows return null
                // We will compare both left and right as non-null using a Comparer.
                // Therefore, We will first loop until right(value) becomes non-null.
                do {
                    if(!enumerator.TryGetNext(out var current)) {
                        return value;
                    }
                    value=current;
                }
                while(value is null);

                while(enumerator.TryGetNext(out var current)) {
                    // compare both(left, right) non-null
                    if(current is not null&&comparer.Compare(current,value)>0) {
                        value=current;
                    }
                }
                return value;
            }
            else {
                // value type
                if(!enumerator.TryGetNext(out value)) {
                    Throws.NoElements();
                }

                // optimize for default comparer
                if(comparer==Comparer<TSource>.Default) {
                    while(enumerator.TryGetNext(out var current)) {
                        if(Comparer<TSource>.Default.Compare(current,value)>0) {
                            value=current;
                        }
                    }

                    return value;
                }
                else {
                    while(enumerator.TryGetNext(out var current)) {
                        if(comparer.Compare(current,value)>0) {
                            value=current;
                        }
                    }

                    return value;
                }
            }
        }
    }

#if NET8_0_OR_GREATER

    internal static TSource? MaxSpan<TSource>(ReadOnlySpan<TSource> span,IComparer<TSource> comparer) {
        #region generate from FileGen.Commands.Max
        if(typeof(TSource)==typeof(byte)) {
            if(comparer!=Comparer<TSource>.Default) return MaxSpanComparer(span,comparer);
            var result = SimdMaxBinaryInteger(UnsafeSpanBitCast<TSource,byte>(span));
            return Unsafe.As<byte,TSource>(ref result);
        }
        else if(typeof(TSource)==typeof(sbyte)) {
            if(comparer!=Comparer<TSource>.Default) return MaxSpanComparer(span,comparer);
            var result = SimdMaxBinaryInteger(UnsafeSpanBitCast<TSource,sbyte>(span));
            return Unsafe.As<sbyte,TSource>(ref result);
        }
        else if(typeof(TSource)==typeof(short)) {
            if(comparer!=Comparer<TSource>.Default) return MaxSpanComparer(span,comparer);
            var result = SimdMaxBinaryInteger(UnsafeSpanBitCast<TSource,short>(span));
            return Unsafe.As<short,TSource>(ref result);
        }
        else if(typeof(TSource)==typeof(ushort)) {
            if(comparer!=Comparer<TSource>.Default) return MaxSpanComparer(span,comparer);
            var result = SimdMaxBinaryInteger(UnsafeSpanBitCast<TSource,ushort>(span));
            return Unsafe.As<ushort,TSource>(ref result);
        }
        else if(typeof(TSource)==typeof(int)) {
            if(comparer!=Comparer<TSource>.Default) return MaxSpanComparer(span,comparer);
            var result = SimdMaxBinaryInteger(UnsafeSpanBitCast<TSource,int>(span));
            return Unsafe.As<int,TSource>(ref result);
        }
        else if(typeof(TSource)==typeof(uint)) {
            if(comparer!=Comparer<TSource>.Default) return MaxSpanComparer(span,comparer);
            var result = SimdMaxBinaryInteger(UnsafeSpanBitCast<TSource,uint>(span));
            return Unsafe.As<uint,TSource>(ref result);
        }
        else if(typeof(TSource)==typeof(long)) {
            if(comparer!=Comparer<TSource>.Default) return MaxSpanComparer(span,comparer);
            var result = SimdMaxBinaryInteger(UnsafeSpanBitCast<TSource,long>(span));
            return Unsafe.As<long,TSource>(ref result);
        }
        else if(typeof(TSource)==typeof(ulong)) {
            if(comparer!=Comparer<TSource>.Default) return MaxSpanComparer(span,comparer);
            var result = SimdMaxBinaryInteger(UnsafeSpanBitCast<TSource,ulong>(span));
            return Unsafe.As<ulong,TSource>(ref result);
        }
        else if(typeof(TSource)==typeof(nint)) {
            if(comparer!=Comparer<TSource>.Default) return MaxSpanComparer(span,comparer);
            var result = SimdMaxBinaryInteger(UnsafeSpanBitCast<TSource,nint>(span));
            return Unsafe.As<nint,TSource>(ref result);
        }
        else if(typeof(TSource)==typeof(nuint)) {
            if(comparer!=Comparer<TSource>.Default) return MaxSpanComparer(span,comparer);
            var result = SimdMaxBinaryInteger(UnsafeSpanBitCast<TSource,nuint>(span));
            return Unsafe.As<nuint,TSource>(ref result);
        }
        else if(typeof(TSource)==typeof(Int128)) {
            if(comparer!=Comparer<TSource>.Default) return MaxSpanComparer(span,comparer);
            var result = SimdMaxBinaryInteger(UnsafeSpanBitCast<TSource,Int128>(span));
            return Unsafe.As<Int128,TSource>(ref result);
        }
        else if(typeof(TSource)==typeof(UInt128)) {
            if(comparer!=Comparer<TSource>.Default) return MaxSpanComparer(span,comparer);
            var result = SimdMaxBinaryInteger(UnsafeSpanBitCast<TSource,UInt128>(span));
            return Unsafe.As<UInt128,TSource>(ref result);
        }
        #endregion
        // float/double: NaN は全順序で最小（System.Linq.Max と同じく NaN は無視。全要素 NaN のときのみ NaN）。
        else if(typeof(TSource)==typeof(double)) {
            if(comparer!=Comparer<TSource>.Default) return MaxSpanComparer(span,comparer);
            var result = SimdMaxFloat(UnsafeSpanBitCast<TSource,double>(span));
            return Unsafe.As<double,TSource>(ref result);
        }
        else if(typeof(TSource)==typeof(float)) {
            if(comparer!=Comparer<TSource>.Default) return MaxSpanComparer(span,comparer);
            var result = SimdMaxFloat(UnsafeSpanBitCast<TSource,float>(span));
            return Unsafe.As<float,TSource>(ref result);
        }
        else {
            return MaxSpanComparer(span,comparer);
        }
    }

    static TSource? MaxSpanComparer<TSource>(ReadOnlySpan<TSource> span,IComparer<TSource> comparer) {
        TSource? value = default;
        if(value is null) {
            // reference type
            var index = 0;
            do {
                if(!((uint)index<(uint)span.Length)) {
                    return value;
                }
                value=span[index++];
            }
            while(value is null);

            while((uint)index<(uint)span.Length) {
                // compare both(left, right) non-null
                if(span[index] is not null&&comparer.Compare(span[index],value)>0) {
                    value=span[index];
                }
                index++;
            }
            return value;
        }
        else {
            // value type
            if(span.Length==0) {
                Throws.NoElements();
            }

            var index = 1;
            value=span[0];

            // optimize for default comparer
            if(comparer==Comparer<TSource>.Default) {
                while((uint)index<(uint)span.Length) {
                    if(Comparer<TSource>.Default.Compare(span[index],value)>0) {
                        value=span[index];
                    }
                    index++;
                }

                return value;
            }
            else {
                while((uint)index<(uint)span.Length) {
                    if(comparer.Compare(span[index],value)>0) {
                        value=span[index];
                    }
                    index++;
                }

                return value;
            }
        }
    }

    // In case of float/double containing NaN, SIMD behavior might be inconsistent, so float/double shouldn't be processed with SIMD.
    // The current implementation correctly handles NaN through IComparer<T>.
    static T SimdMaxBinaryInteger<T>(ReadOnlySpan<T> span)
        where T : struct, IBinaryInteger<T> {
        if(span.Length==0) Throws.NoElements();

        if(Vector.IsHardwareAccelerated&&Vector<T>.IsSupported&&span.Length>=Vector<T>.Count) {
            ref var current = ref MemoryMarshal.GetReference(span);
            ref var end = ref Unsafe.Add(ref current,span.Length);
            ref var to = ref Unsafe.Subtract(ref end,Vector<T>.Count);

            var vectorResult = Vector.LoadUnsafe(ref current);
            current=ref Unsafe.Add(ref current,Vector<T>.Count);
            while(Unsafe.IsAddressLessThan(ref current,ref to)) // exclude last
            {
                var data = Vector.LoadUnsafe(ref current);
                vectorResult=Vector.Max(data,vectorResult);
                current=ref Unsafe.Add(ref current,Vector<T>.Count);
            }

            // overlap load
            var lastOverlap = Vector.LoadUnsafe(ref to);
            vectorResult=Vector.Max(lastOverlap,vectorResult);

            var result = vectorResult[0];
            for(int i = 1; i<Vector<T>.Count; i++) {
                if(vectorResult[i]>result) {
                    result=vectorResult[i];
                }
            }
            return result;
        }
        else {
            var result = span[0];
            for(int i = 1; (uint)i<(uint)span.Length; i++) {
                if(span[i]>result) {
                    result=span[i];
                }
            }
            return result;
        }
    }

    // float/double 用の SIMD Max。
    // NaN は全順序で最小なので無視する（System.Linq.Max と同じく、全要素 NaN のときのみ NaN を返す）。
    // ベクトル計算では NaN レーンを -Inf に置換して数値 Max を求め、非 NaN が 1 つでもあったかを追跡する。
    static T SimdMaxFloat<T>(ReadOnlySpan<T> span)
        where T : struct, IFloatingPointIeee754<T> {
        if(span.Length==0) Throws.NoElements();

        if(Vector.IsHardwareAccelerated&&Vector<T>.IsSupported&&span.Length>=Vector<T>.Count) {
            ref var current = ref MemoryMarshal.GetReference(span);
            ref var end = ref Unsafe.Add(ref current,span.Length);
            ref var to = ref Unsafe.Subtract(ref end,Vector<T>.Count);

            var negInf = new Vector<T>(T.NegativeInfinity);
            var hasNonNaN = false;

            var first = Vector.LoadUnsafe(ref current);
            hasNonNaN|=Vector.EqualsAny(first,first); // 非 NaN レーンが存在するか
            var vectorResult = Vector.ConditionalSelect(Vector.Equals(first,first),first,negInf);
            current=ref Unsafe.Add(ref current,Vector<T>.Count);

            while(Unsafe.IsAddressLessThan(ref current,ref to)) // exclude last
            {
                var data = Vector.LoadUnsafe(ref current);
                hasNonNaN|=Vector.EqualsAny(data,data);
                var cleaned = Vector.ConditionalSelect(Vector.Equals(data,data),data,negInf);
                vectorResult=Vector.Max(vectorResult,cleaned);
                current=ref Unsafe.Add(ref current,Vector<T>.Count);
            }

            // overlap load for the tail
            var lastData = Vector.LoadUnsafe(ref to);
            hasNonNaN|=Vector.EqualsAny(lastData,lastData);
            var lastCleaned = Vector.ConditionalSelect(Vector.Equals(lastData,lastData),lastData,negInf);
            vectorResult=Vector.Max(vectorResult,lastCleaned);

            if(!hasNonNaN) return T.NaN; // 全要素 NaN

            var result = vectorResult[0];
            for(int i = 1; i<Vector<T>.Count; i++) {
                if(vectorResult[i]>result) {
                    result=vectorResult[i];
                }
            }
            return result;
        }
        else {
            var result = span[0];
            for(int i = 1; (uint)i<(uint)span.Length; i++) {
                var x = span[i];
                // result が NaN のときは x で置き換える（NaN を無視）。
                if(x>result||T.IsNaN(result)) result=x;
            }
            return result;
        }
    }

#endif
}
