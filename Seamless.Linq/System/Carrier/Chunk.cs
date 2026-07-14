namespace Seamless.System.Carrier {
    partial class ValueEnumerableExtensions {
        public static ValueEnumerable<Chunk<TEnumerator,TSource>,TSource[]> Chunk<TEnumerator, TSource>(this ValueEnumerable<TEnumerator,TSource> source,Int32 size)
            where TEnumerator : struct, IValueEnumerator<TSource> {
            const int ArrayMaxLength = 0X7FFFFFC7;

            if(size<1) Throws.ArgumentOutOfRange(nameof(size));

            // I think it would be better to throw an exception, but adjusting to Array.MaxLength.
            // Related discussion in https://github.com/dotnet/runtime/issues/67132
            size=Math.Min(size,ArrayMaxLength);

            return new(new(source.Enumerator,size));
        }
    }
}

namespace Seamless.System.Carrier {
    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct Chunk<TEnumerator, TSource>(TEnumerator source,Int32 size)
        :IValueEnumerator<TSource[]>
        where TEnumerator : struct, IValueEnumerator<TSource> {
        private TEnumerator source = source;
        private int index;
        private bool isInitialized;
        private bool isCompleted;
        private bool isCanGetSpan;

        public bool TryGetNonEnumeratedCount(out int count) {
            if(this.source.TryGetNonEnumeratedCount(out var sourceCount)) {
                count=(sourceCount+size-1)/size; // Calculate the total number of chunks
                return true;
            }

            count=0;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<TSource[]> span) {
            span=default;
            return false;
        }

        public bool TryCopyTo(scoped Span<TSource[]> destination,Index offset) => false;

        public bool TryGetNext(out TSource[] current) {
            if(this.isCompleted) {
                current=default!;
                return false;
            }

            if(!this.isInitialized) {
                this.isInitialized=true;

                if(this.source.TryGetNonEnumeratedCount(out var count)) {
                    size=Math.Min(size,count); // no needs large buffer
                }

                if(this.source.TryGetSpan(out var span)) {
                    this.isCanGetSpan=true;
                    if(span.Length==0) {
                        this.isCompleted=true;
                        current=default!;
                        return false;
                    }
                }
            }

            if(this.isCanGetSpan) {
                if(!this.source.TryGetSpan(out var span)) {
                    current=default!;
                    this.isCompleted=true;
                    return false;
                }
                var currentSpan = span.Slice(this.index,Math.Min(size,span.Length-this.index));
                this.index+=currentSpan.Length;
                current=currentSpan.ToArray();
                if(this.index==span.Length) {
                    this.isCompleted=true;
                }
                return true;
            } else // noSpan
              {
                this.index=0;
                current=null!;
                while(this.source.TryGetNext(out var value)) {
                    if(current==null) current=new TSource[size];

                    current[this.index++]=value;
                    if(this.index==size) {
                        this.index=0;
                        return true;
                    }
                }

                this.isCompleted=true;
                if(current==null) return false;
                if(current.Length!=this.index) {
                    Array.Resize(ref current,this.index);
                }
                return true;
            }
        }

        public void Dispose() {
            this.source.Dispose();
        }
    }

}
