#if NET8_0_OR_GREATER

using Seamless.Linq;

namespace Seamless.System.Carrier {
    public static partial class ValueEnumerable {
        public static ValueEnumerable<FromInfiniteSequence<T>,T> InfiniteSequence<T>(T start,T step)
            where T : IAdditionOperators<T,T,T> {
            if(start is null) Throws.Null(nameof(start));
            if(step is null) Throws.Null(nameof(step));

            return new(new(start,step));
        }
        //public static ValueEnumerable<FromInfiniteSequence<T>,T> InfiniteSequence<T>(T start,T step)
        //    where T : IAdditionOperators<T,T,T> {

        //    return new(new(start,step));
        //}
        public static ValueEnumerable<FromUInt16InfiniteSequence,UInt16> InfiniteSequence(UInt16 start,UInt16 step) {
            return new(new(start,step));
        }
        public static ValueEnumerable<FromInt16InfiniteSequence,Int16> InfiniteSequence(Int16 start,Int16 step) {
            return new(new(start,step));
        }

        public static ValueEnumerable<FromInfiniteSequenceDateTime,DateTime> InfiniteSequence(DateTime start,
            TimeSpan step) {
            return new(new(start,step));
        }

        public static ValueEnumerable<FromInfiniteSequenceDateTimeOffset,DateTimeOffset> InfiniteSequence(
            DateTimeOffset start,TimeSpan step) {
            return new(new(start,step));
        }

        public static ValueEnumerable<FromByteInfiniteSequence,Byte> InfiniteSequence(Byte start,Byte step) {
            return new(new(start,step));
        }
        public static ValueEnumerable<FromSByteInfiniteSequence,SByte> InfiniteSequence(SByte start,SByte step) {
            return new(new(start,step));
        }
    }
    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct FromUInt16InfiniteSequence(UInt16 start,UInt16 step):IValueEnumerator<UInt16> {
        bool calledGetNext;

        public bool TryGetNonEnumeratedCount(out int count) {
            count=0;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<UInt16> span) {
            span=default;
            return false;
        }

        public bool TryCopyTo(scoped Span<UInt16> destination,Index offset) {
            return false;
        }

        public bool TryGetNext(out UInt16 current) {
            if(!this.calledGetNext) {
                this.calledGetNext=true;
                current=start;
                return true;
            }

            current=start+=step;
            return true;
        }

        public void Dispose() {
        }
    }
    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct FromSByteInfiniteSequence(SByte start,SByte step):IValueEnumerator<SByte> {
        bool calledGetNext;

        public bool TryGetNonEnumeratedCount(out int count) {
            count=0;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<SByte> span) {
            span=default;
            return false;
        }

        public bool TryCopyTo(scoped Span<SByte> destination,Index offset) {
            return false;
        }

        public bool TryGetNext(out SByte current) {
            if(!this.calledGetNext) {
                this.calledGetNext=true;
                current=start;
                return true;
            }

            current=start+=step;
            return true;
        }

        public void Dispose() {
        }
    }
    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct FromByteInfiniteSequence(Byte start,Byte step):IValueEnumerator<Byte> {
        bool calledGetNext;

        public bool TryGetNonEnumeratedCount(out int count) {
            count=0;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<Byte> span) {
            span=default;
            return false;
        }

        public bool TryCopyTo(scoped Span<Byte> destination,Index offset) {
            return false;
        }

        public bool TryGetNext(out Byte current) {
            if(!this.calledGetNext) {
                this.calledGetNext=true;
                current=start;
                return true;
            }

            current=start+=step;
            return true;
        }

        public void Dispose() {
        }
    }
}

namespace Seamless.System.Carrier {
    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct FromInfiniteSequence<T>(T start,T step):IValueEnumerator<T>
        where T : IAdditionOperators<T,T,T> {
        bool calledGetNext;

        public bool TryGetNonEnumeratedCount(out int count) {
            count=0;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<T> span) {
            span=default;
            return false;
        }

        public bool TryCopyTo(scoped Span<T> destination,Index offset) {
            return false;
        }

        public bool TryGetNext(out T current) {
            if(!this.calledGetNext) {
                this.calledGetNext=true;
                current=start;
                return true;
            }

            current=start+=step;
            return true;
        }

        public void Dispose() {
        }
    }

    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct FromInfiniteSequenceDateTime(DateTime start,TimeSpan step):IValueEnumerator<DateTime> {
        bool calledGetNext;

        public bool TryGetNonEnumeratedCount(out int count) {
            count=0;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<DateTime> span) {
            span=default;
            return false;
        }

        public bool TryCopyTo(scoped Span<DateTime> destination,Index offset) {
            return false;
        }

        public bool TryGetNext(out DateTime current) {
            if(!this.calledGetNext) {
                this.calledGetNext=true;
                current=start;
                return true;
            }

            current=start+=step;
            return true;
        }

        public void Dispose() {
        }
    }

    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct FromInfiniteSequenceDateTimeOffset(DateTimeOffset start,TimeSpan step)
        :IValueEnumerator<DateTimeOffset> {
        bool calledGetNext;

        public bool TryGetNonEnumeratedCount(out int count) {
            count=0;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<DateTimeOffset> span) {
            span=default;
            return false;
        }

        public bool TryCopyTo(scoped Span<DateTimeOffset> destination,Index offset) {
            return false;
        }

        public bool TryGetNext(out DateTimeOffset current) {
            if(!this.calledGetNext) {
                this.calledGetNext=true;
                current=start;
                return true;
            }

            current=start+=step;
            return true;
        }

        public void Dispose() {
        }
    }
}

#endif

namespace Seamless.System.Carrier {
    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct FromInt16InfiniteSequence(Int16 start,Int16 step):IValueEnumerator<Int16> {
        bool calledGetNext;

        public bool TryGetNonEnumeratedCount(out int count) {
            count=0;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<Int16> span) {
            span=default;
            return false;
        }

        public bool TryCopyTo(scoped Span<Int16> destination,Index offset) {
            return false;
        }

        public bool TryGetNext(out Int16 current) {
            if(!this.calledGetNext) {
                this.calledGetNext=true;
                current=start;
                return true;
            }

            current=start+=step;
            return true;
        }

        public void Dispose() {
        }
    }

    public static partial class ValueEnumerable {
        public static ValueEnumerable<FromUInt32InfiniteSequence,UInt32> InfiniteSequence(UInt32 start,UInt32 step) {
            return new(new(start,step));
        }
    }

    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct FromUInt32InfiniteSequence(UInt32 start,UInt32 step):IValueEnumerator<UInt32> {
        bool calledGetNext;

        public bool TryGetNonEnumeratedCount(out int count) {
            count=0;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<UInt32> span) {
            span=default;
            return false;
        }

        public bool TryCopyTo(scoped Span<UInt32> destination,Index offset) {
            return false;
        }

        public bool TryGetNext(out UInt32 current) {
            if(!this.calledGetNext) {
                this.calledGetNext=true;
                current=start;
                return true;
            }

            current=start+=step;
            return true;
        }

        public void Dispose() {
        }
    }

    public static partial class ValueEnumerable {
        public static ValueEnumerable<FromInt32InfiniteSequence,Int32> InfiniteSequence(Int32 start,Int32 step) {
            return new(new(start,step));
        }
    }

    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct FromInt32InfiniteSequence(Int32 start,Int32 step):IValueEnumerator<Int32> {
        bool calledGetNext;

        public bool TryGetNonEnumeratedCount(out int count) {
            count=0;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<Int32> span) {
            span=default;
            return false;
        }

        public bool TryCopyTo(scoped Span<Int32> destination,Index offset) {
            return false;
        }

        public bool TryGetNext(out Int32 current) {
            if(!this.calledGetNext) {
                this.calledGetNext=true;
                current=start;
                return true;
            }

            current=start+=step;
            return true;
        }

        public void Dispose() {
        }
    }

    public static partial class ValueEnumerable {
        public static ValueEnumerable<FromUInt64InfiniteSequence,UInt64> InfiniteSequence(UInt64 start,UInt64 step) {
            return new(new(start,step));
        }
    }

    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct FromUInt64InfiniteSequence(UInt64 start,UInt64 step):IValueEnumerator<UInt64> {
        bool calledGetNext;

        public bool TryGetNonEnumeratedCount(out int count) {
            count=0;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<UInt64> span) {
            span=default;
            return false;
        }

        public bool TryCopyTo(scoped Span<UInt64> destination,Index offset) {
            return false;
        }

        public bool TryGetNext(out UInt64 current) {
            if(!this.calledGetNext) {
                this.calledGetNext=true;
                current=start;
                return true;
            }

            current=start+=step;
            return true;
        }

        public void Dispose() {
        }
    }

    public static partial class ValueEnumerable {
        public static ValueEnumerable<FromInt64InfiniteSequence,Int64> InfiniteSequence(Int64 start,Int64 step) {
            return new(new(start,step));
        }
    }

    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct FromInt64InfiniteSequence(Int64 start,Int64 step):IValueEnumerator<Int64> {
        bool calledGetNext;

        public bool TryGetNonEnumeratedCount(out int count) {
            count=0;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<Int64> span) {
            span=default;
            return false;
        }

        public bool TryCopyTo(scoped Span<Int64> destination,Index offset) {
            return false;
        }

        public bool TryGetNext(out Int64 current) {
            if(!this.calledGetNext) {
                this.calledGetNext=true;
                current=start;
                return true;
            }

            current=start+=step;
            return true;
        }

        public void Dispose() {
        }
    }

    public static partial class ValueEnumerable {
        public static ValueEnumerable<FromCharInfiniteSequence,Char> InfiniteSequence(Char start,Char step) {
            return new(new(start,step));
        }
    }

    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct FromCharInfiniteSequence(Char start,Char step):IValueEnumerator<Char> {
        bool calledGetNext;

        public bool TryGetNonEnumeratedCount(out int count) {
            count=0;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<Char> span) {
            span=default;
            return false;
        }

        public bool TryCopyTo(scoped Span<Char> destination,Index offset) {
            return false;
        }

        public bool TryGetNext(out Char current) {
            if(!this.calledGetNext) {
                this.calledGetNext=true;
                current=start;
                return true;
            }

            current=start+=step;
            return true;
        }

        public void Dispose() {
        }
    }

    public static partial class ValueEnumerable {
        public static ValueEnumerable<FromSingleInfiniteSequence,Single> InfiniteSequence(Single start,Single step) {
            return new(new(start,step));
        }
    }

    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct FromSingleInfiniteSequence(Single start,Single step):IValueEnumerator<Single> {
        bool calledGetNext;

        public bool TryGetNonEnumeratedCount(out int count) {
            count=0;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<Single> span) {
            span=default;
            return false;
        }

        public bool TryCopyTo(scoped Span<Single> destination,Index offset) {
            return false;
        }

        public bool TryGetNext(out Single current) {
            if(!this.calledGetNext) {
                this.calledGetNext=true;
                current=start;
                return true;
            }

            current=start+=step;
            return true;
        }

        public void Dispose() {
        }
    }

    public static partial class ValueEnumerable {
        public static ValueEnumerable<FromDoubleInfiniteSequence,Double> InfiniteSequence(Double start,Double step) {
            return new(new(start,step));
        }
    }

    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct FromDoubleInfiniteSequence(Double start,Double step):IValueEnumerator<Double> {
        bool calledGetNext;

        public bool TryGetNonEnumeratedCount(out int count) {
            count=0;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<Double> span) {
            span=default;
            return false;
        }

        public bool TryCopyTo(scoped Span<Double> destination,Index offset) {
            return false;
        }

        public bool TryGetNext(out Double current) {
            if(!this.calledGetNext) {
                this.calledGetNext=true;
                current=start;
                return true;
            }

            current=start+=step;
            return true;
        }

        public void Dispose() {
        }
    }

    public static partial class ValueEnumerable {
        public static ValueEnumerable<FromDecimalInfiniteSequence,Decimal> InfiniteSequence(Decimal start,Decimal step) {
            return new(new(start,step));
        }
    }

    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct FromDecimalInfiniteSequence(Decimal start,Decimal step):IValueEnumerator<Decimal> {
        bool calledGetNext;

        public bool TryGetNonEnumeratedCount(out int count) {
            count=0;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<Decimal> span) {
            span=default;
            return false;
        }

        public bool TryCopyTo(scoped Span<Decimal> destination,Index offset) {
            return false;
        }

        public bool TryGetNext(out Decimal current) {
            if(!this.calledGetNext) {
                this.calledGetNext=true;
                current=start;
                return true;
            }

            current=start+=step;
            return true;
        }

        public void Dispose() {
        }
    }
}
