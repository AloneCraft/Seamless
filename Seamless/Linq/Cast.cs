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
        public static ValueEnumerable<Cast<TEnumerator,TSource,TResult>,TResult> Cast<TEnumerator,TSource,TResult>(
            this ValueEnumerable<TEnumerator,TSource> source)
            where TEnumerator : struct,IValueEnumerator<TSource>
            => new(new(source.Enumerator));
    }
}

namespace Seamless.Linq {
    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct Cast<TEnumerator, TSource, TResult>(TEnumerator source)
        :IValueEnumerator<TResult>
        where TEnumerator : struct, IValueEnumerator<TSource> {
        private TEnumerator source = source;

        public bool TryGetNonEnumeratedCount(out int count) {
            return this.source.TryGetNonEnumeratedCount(out count);
        }

        public bool TryGetSpan(out ReadOnlySpan<TResult> span) {
            span=default;
            return false;
        }

        public bool TryCopyTo(scoped Span<TResult> destination,Index offset) => false;

        public bool TryGetNext(out TResult current) {
            while(this.source.TryGetNext(out var value)) {
                current=(TResult)(object)value!;
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
