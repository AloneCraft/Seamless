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
        public static Boolean All<TEnumerator, TSource>(this ValueEnumerable<TEnumerator,TSource> source,Func<TSource,Boolean> predicate)
            where TEnumerator : struct, IValueEnumerator<TSource> {
            ArgumentNullException.ThrowIfNull(predicate);
            using var enumerator = source.Enumerator;
            if(enumerator.TryGetSpan(out var span)) {
                foreach(var item in span) {
                    if(!predicate(item)) {
                        return false;
                    }
                }
            } else {
                while(enumerator.TryGetNext(out var item)) {
                    if(!predicate(item)) {
                        return false;
                    }
                }
            }

            return true;
            }

        public static Boolean All<TSource>(this ValueEnumerable<FromArray<TSource>,TSource> source,Func<TSource,Boolean> predicate) {
            ArgumentNullException.ThrowIfNull(predicate);

            var array = source.Enumerator.GetSource();
            if(array.GetType()!=typeof(TSource[])) {
                return All(array,predicate);
            }

            // The assembly output differs slightly when iterating through array and span.
            // According to microbenckmark results, iterating through span was faster for the logic passed to predicate.
            var span = (ReadOnlySpan<TSource>)array;
            for(int i = 0;(uint)i<(uint)span.Length;i++) {
                if(!predicate(span[i])) {
                    return false;
                }
            }
            return true;

            [MethodImpl(MethodImplOptions.NoInlining)]
            static Boolean All(TSource[] array,Func<TSource,Boolean> predicate) {
                for(int i = 0;(uint)i<(uint)array.Length;i++) {
                    if(!predicate(array[i])) {
                        return false;
                    }
                }
                return true;
            }
        }
    }
}
