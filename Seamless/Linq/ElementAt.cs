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
        public static TSource ElementAt<TEnumerator, TSource>(this ValueEnumerable<TEnumerator,TSource> source,Int32 index)
            where TEnumerator : struct, IValueEnumerator<TSource> {
            var enumerator = source.Enumerator;
            try {
                return TryGetElementAt<TEnumerator,TSource>(ref enumerator,index,out var value)
                    ? value
                    : Throws.ArgumentOutOfRange<TSource>(nameof(index));
            } finally {
                enumerator.Dispose();
            }
        }

        public static TSource ElementAt<TEnumerator, TSource>(this ValueEnumerable<TEnumerator,TSource> source,Index index)
            where TEnumerator : struct, IValueEnumerator<TSource> {
            var enumerator = source.Enumerator;
            try {
                return TryGetElementAt<TEnumerator,TSource>(ref enumerator,index,out var value)
                    ? value
                    : Throws.ArgumentOutOfRange<TSource>(nameof(index));
            } finally {
                enumerator.Dispose();
            }
        }

        public static TSource? ElementAtOrDefault<TEnumerator, TSource>(this ValueEnumerable<TEnumerator,TSource> source,Int32 index)
          where TEnumerator : struct, IValueEnumerator<TSource> {
            if(index<0) return default; // allows negative index

            var enumerator = source.Enumerator;
            try {
                return TryGetElementAt<TEnumerator,TSource>(ref enumerator,index,out var value)
                ? value
                : default!;
            } finally {
                enumerator.Dispose();
            }
        }

        public static TSource? ElementAtOrDefault<TEnumerator, TSource>(this ValueEnumerable<TEnumerator,TSource> source,Index index)
            where TEnumerator : struct, IValueEnumerator<TSource> {
            var enumerator = source.Enumerator;
            try {
                return TryGetElementAt<TEnumerator,TSource>(ref enumerator,index,out var value)
                ? value
                : default!;
            } finally {
                enumerator.Dispose();
            }
        }
        private static bool TryGetElementAt<TEnumerator, TSource>(ref TEnumerator source,Index index,out TSource value)
           where TEnumerator : struct, IValueEnumerator<TSource> {
            var current = default(TSource)!;
#if NETSTANDARD2_0
            var span = SingleSpan.Create<TSource>();
            if (source.TryCopyTo(span, index))
            {
                value = span[0];
                span.Clear();
                return true;
            }
#else
            if(source.TryCopyTo(SingleSpan.Create(ref current),index)) {
                value=current;
                return true;
            }
#endif
            else if(EnumeratorHelper.TryConsumeGetAt<TEnumerator,TSource>(ref source,index,out current)) {
                value=current!;
                return true;
            }

            value=default!;
            return false;
        }
    }
}
