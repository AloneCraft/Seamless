using System;
using System.Buffers;
using System.Collections;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Seamless.Linq;
namespace Seamless.Linq {
    public static partial class Enumerable {
        public static ValueEnumerable<FromRepeat<T>,T> Repeat<T>(T element,int count) {
            if(count<0) {
                Throws.ArgumentOutOfRange(nameof(count));
            }

            return new(new(element,count));
        }
    }
}

namespace Seamless.Linq {
    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct FromRepeat<T>(T _element,int _count):IValueEnumerator<T> {
        private int index;

        public bool TryGetNonEnumeratedCount(out int count) {
            count=_count;
            return true;
        }

        public bool TryGetSpan(out ReadOnlySpan<T> span) {
            span=default!;
            return false;
        }

        public bool TryCopyTo(Span<T> destination,Index offset) {
            if(EnumeratorHelper.TryGetSliceRange(_count,offset,destination.Length,out var _,out var fillCount)) {
                destination[..fillCount].Fill(_element); // Span.Fill using SIMD
                return true;
            }

            return false;
        }

        public bool TryGetNext(out T current) {
            if(this.index++<_count) {
                current=_element;
                return true;
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose() {
        }
    }
}
