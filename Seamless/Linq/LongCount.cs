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
        public static Int64 LongCount<TEnumerator, TSource>(this ValueEnumerable<TEnumerator,TSource> source)
            where TEnumerator : struct, IValueEnumerator<TSource> {
                using var enumerator = source.Enumerator;
                if(enumerator.TryGetNonEnumeratedCount(out var count)) {
                    return count;
                }

                var longCount = 0L;
                while(enumerator.TryGetNext(out _)) {
                    checked { longCount++; }
                }
                return longCount;
            }

        public static Int64 LongCount<TEnumerator, TSource>(this ValueEnumerable<TEnumerator,TSource> source,Func<TSource,Boolean> predicate)
            where TEnumerator : struct, IValueEnumerator<TSource> {
            ArgumentNullException.ThrowIfNull(predicate);
            using var enumerator = source.Enumerator;
            if(enumerator.TryGetSpan(out var span)) {
                var longCount = 0L;
                foreach(var current in span) {
                    if(predicate(current)) {
                        checked { longCount++; }
                    }
                }
                return longCount;
            } else {
                var longCount = 0L;
                while(enumerator.TryGetNext(out var current)) {
                    if(predicate(current)) {
                        checked { longCount++; }
                    }
                }
                return longCount;
            }
            }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Int64 LongCount<TSource>(this ValueEnumerable<FromArray<TSource>,TSource> source,Func<TSource,Boolean> predicate) {
            ArgumentNullException.ThrowIfNull(predicate);

            var array = source.Enumerator.GetSource();
            if(array.GetType()!=typeof(TSource[])) {
                return LongCount(array,predicate);
            }

            var longCount = 0;

            var span = (ReadOnlySpan<TSource>)array;
            for(int i = 0;(uint)i<(uint)span.Length;i++) {
                if(predicate(span[i])) {
                    longCount++;
                }
            }

            return longCount;

            [MethodImpl(MethodImplOptions.NoInlining)]
            static Int64 LongCount(TSource[] array,Func<TSource,Boolean> predicate) {
                var longCount = 0;
                for(int i = 0;(uint)i<(uint)array.Length;i++) {
                    if(predicate(array[i])) {
                        longCount++;
                    }
                }
                return longCount;
            }
        }

    }
}
