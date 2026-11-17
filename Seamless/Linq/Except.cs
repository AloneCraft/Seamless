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
        public static ValueEnumerable<Except<TEnumerator,TEnumerator2,TSource>,TSource> Except<TEnumerator, TEnumerator2, TSource>(this ValueEnumerable<TEnumerator,TSource> source,ValueEnumerable<TEnumerator2,TSource> second)
            where TEnumerator : struct, IValueEnumerator<TSource>
            where TEnumerator2 : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator,second,null));

        public static ValueEnumerable<Except<TEnumerator,TEnumerator2,TSource>,TSource> Except<TEnumerator, TEnumerator2, TSource>(this ValueEnumerable<TEnumerator,TSource> source,ValueEnumerable<TEnumerator2,TSource> second,IEqualityComparer<TSource>? comparer)
            where TEnumerator : struct, IValueEnumerator<TSource>
            where TEnumerator2 : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator,second,comparer));


        public static ValueEnumerable<Except<TEnumerator,FromEnumerable<TSource>,TSource>,TSource> Except<TEnumerator, TSource>(this ValueEnumerable<TEnumerator,TSource> source,IEnumerable<TSource> second)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator,Throws.IfNull(second).AsValueEnumerable(),null));

        public static ValueEnumerable<Except<TEnumerator,FromEnumerable<TSource>,TSource>,TSource> Except<TEnumerator, TSource>(this ValueEnumerable<TEnumerator,TSource> source,IEnumerable<TSource> second,IEqualityComparer<TSource>? comparer)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator,Throws.IfNull(second).AsValueEnumerable(),comparer));
    }
}

namespace Seamless.Linq {
    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct Except<TEnumerator, TEnumerator2, TSource>(TEnumerator source,ValueEnumerable<TEnumerator2,TSource> second,IEqualityComparer<TSource>? comparer)
        :IValueEnumerator<TSource>
        where TEnumerator : struct, IValueEnumerator<TSource>
        where TEnumerator2 : struct, IValueEnumerator<TSource> {
        private TEnumerator source = source;
        private ValueEnumerable<TEnumerator2,TSource> second = second;
        private HashSetSlim<TSource>? set;

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
            if(this.set==null) {
                this.set=this.second.ToHashSetSlim(comparer??EqualityComparer<TSource>.Default);
            }

            while(this.source.TryGetNext(out var value)) {
                if(this.set.Add(value)) {
                    current=value;
                    return true;
                }
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose() {
            this.set?.Dispose();
            this.source.Dispose();
        }
    }
}
