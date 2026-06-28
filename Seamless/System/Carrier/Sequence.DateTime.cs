namespace Seamless.System.Carrier {
    public static partial class ValueEnumerable {
        public static ValueEnumerable<FromSequenceDateTime,DateTime> Sequence(DateTime start,DateTime endInclusive,TimeSpan step) {
            if(step==TimeSpan.Zero) // (T.IsZero(step))
            {
                if(start!=endInclusive) {
                    Throws.ArgumentOutOfRange(nameof(step));
                }

                // repeat one
                return new(new(start,endInclusive,step,isIncrement: true));
            }
            else if(step>=TimeSpan.Zero) // (T.IsPositive(step))
            {
                if(endInclusive<start) {
                    Throws.ArgumentOutOfRange(nameof(endInclusive));
                }

                // increment pattern
                return new(new(start,endInclusive,step,isIncrement: true));
            }
            else {
                if(endInclusive>start) {
                    Throws.ArgumentOutOfRange(nameof(endInclusive));
                }

                // decrement pattern
                return new(new(start,endInclusive,step,isIncrement: false));
            }
        }
    }
}

namespace Seamless.System.Carrier {
    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct FromSequenceDateTime(DateTime currentValue,DateTime endInclusive,TimeSpan step,bool isIncrement):IValueEnumerator<DateTime> {
        private bool calledGetNext;

        public bool TryGetNext(out DateTime current) {
            if(!this.calledGetNext) {
                this.calledGetNext=true;
                current=currentValue;
                return true;
            }

            if(isIncrement) {
                var next = unchecked((DateTime)(currentValue+step));

                if(next>=endInclusive||next<=currentValue) {
                    if(next==endInclusive&&currentValue!=next) {
                        current=currentValue=next;
                        return true;
                    }

                    current=default!;
                    return false;
                }

                current=currentValue=next;
                return true;
            }
            else {
                var next = unchecked((DateTime)(currentValue+step));

                if(next<=endInclusive||next>=currentValue) {
                    if(next==endInclusive&&currentValue!=next) {
                        current=currentValue=next;
                        return true;
                    }

                    current=default!;
                    return false;
                }

                current=currentValue=next;
                return true;
            }
        }

        public void Dispose() {
        }
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
    }
}

namespace Seamless.System.Carrier {
    public static partial class ValueEnumerable {
        public static ValueEnumerable<FromSequenceDateTimeOffset,DateTimeOffset> Sequence(DateTimeOffset start,DateTimeOffset endInclusive,TimeSpan step) {
            if(step==TimeSpan.Zero) // (T.IsZero(step))
            {
                if(start!=endInclusive) {
                    Throws.ArgumentOutOfRange(nameof(step));
                }

                // repeat one
                return new(new(start,endInclusive,step,isIncrement: true));
            }
            else if(step>=TimeSpan.Zero) // (T.IsPositive(step))
            {
                if(endInclusive<start) {
                    Throws.ArgumentOutOfRange(nameof(endInclusive));
                }

                // increment pattern
                return new(new(start,endInclusive,step,isIncrement: true));
            }
            else {
                if(endInclusive>start) {
                    Throws.ArgumentOutOfRange(nameof(endInclusive));
                }

                // decrement pattern
                return new(new(start,endInclusive,step,isIncrement: false));
            }
        }
    }
}

namespace Seamless.System.Carrier {
    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct FromSequenceDateTimeOffset(
        DateTimeOffset currentValue,
        DateTimeOffset endInclusive,
        TimeSpan step,
        bool isIncrement):IValueEnumerator<DateTimeOffset> {
        private bool calledGetNext;

        public bool TryGetNext(out DateTimeOffset current) {
            if(!this.calledGetNext) {
                this.calledGetNext=true;
                current=currentValue;
                return true;
            }

            if(isIncrement) {
                var next = unchecked((DateTimeOffset)(currentValue+step));

                if(next>=endInclusive||next<=currentValue) {
                    if(next==endInclusive&&currentValue!=next) {
                        current=currentValue=next;
                        return true;
                    }

                    current=default!;
                    return false;
                }

                current=currentValue=next;
                return true;
            }
            else {
                var next = unchecked((DateTimeOffset)(currentValue+step));

                if(next<=endInclusive||next>=currentValue) {
                    if(next==endInclusive&&currentValue!=next) {
                        current=currentValue=next;
                        return true;
                    }

                    current=default!;
                    return false;
                }

                current=currentValue=next;
                return true;
            }
        }

        public void Dispose() {
        }

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
    }
}
