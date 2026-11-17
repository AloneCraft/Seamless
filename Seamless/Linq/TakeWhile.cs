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
    partial class Enumerable {
        public static ValueEnumerable<TakeWhile<TEnumerator,TSource>,TSource> TakeWhile<TEnumerator, TSource>(this ValueEnumerable<TEnumerator,TSource> source,Func<TSource,Boolean> predicate)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator,Throws.IfNull(predicate)));

        public static ValueEnumerable<TakeWhile2<TEnumerator,TSource>,TSource> TakeWhile<TEnumerator, TSource>(this ValueEnumerable<TEnumerator,TSource> source,Func<TSource,Int32,Boolean> predicate)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator,Throws.IfNull(predicate)));

    }
}

namespace Seamless.Linq {
    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct TakeWhile<TEnumerator, TSource>(TEnumerator source,Func<TSource,Boolean> predicate)
        :IValueEnumerator<TSource>
        where TEnumerator : struct, IValueEnumerator<TSource> {
        private TEnumerator source = source;

        public bool TryGetNonEnumeratedCount(out int count) {
            count=0;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<TSource> span) {
            span=default;
            return false;
        }

        public bool TryCopyTo(scoped Span<TSource> destination,Index offset) => false;

        public bool TryGetNext(out TSource current) {
            if(this.source.TryGetNext(out current)&&predicate(current)) {
                return true;
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose() {
            this.source.Dispose();
        }
    }

    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct TakeWhile2<TEnumerator, TSource>(TEnumerator source,Func<TSource,Int32,Boolean> predicate)
        :IValueEnumerator<TSource>
        where TEnumerator : struct, IValueEnumerator<TSource> {
        private TEnumerator source = source;
        private int index = 0;

        public bool TryGetNonEnumeratedCount(out int count) {
            count=0;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<TSource> span) {
            span=default;
            return false;
        }

        public bool TryCopyTo(scoped Span<TSource> destination,Index offset) => false;

        public bool TryGetNext(out TSource current) {
            if(this.source.TryGetNext(out current)&&predicate(current,this.index++)) {
                return true;
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose() {
            this.source.Dispose();
        }
    }
}
