namespace Seamless.System.Carrier {
    partial class ValueEnumerableExtensions {
        public static TSource Last<TEnumerator, TSource>(this ValueEnumerable<TEnumerator,TSource> source)
            where TEnumerator : struct, IValueEnumerator<TSource> {
            var enumerator = source.Enumerator;
            try {
                return TryGetLast<TEnumerator,TSource>(ref enumerator,out var value)
                ? value
                : Throws.NoElements<TSource>();
            }
            finally {
                enumerator.Dispose();
            }
        }

        public static TSource Last<TEnumerator, TSource>(this ValueEnumerable<TEnumerator,TSource> source,Func<TSource,Boolean> predicate)
            where TEnumerator : struct, IValueEnumerator<TSource> {
            ArgumentNullException.ThrowIfNull(predicate);
            var enumerator = source.Enumerator;
            try {
                return TryGetLast<TEnumerator,TSource>(ref enumerator,predicate,out var value)
                ? value
                : Throws.NoMatch<TSource>();
            }
            finally {
                enumerator.Dispose();
            }
        }

        public static TSource? LastOrDefault<TEnumerator, TSource>(this ValueEnumerable<TEnumerator,TSource> source)
    where TEnumerator : struct, IValueEnumerator<TSource> {
            var enumerator = source.Enumerator;
            try {
                return TryGetLast<TEnumerator,TSource>(ref enumerator,out var value)
                ? value
                : default;
            }
            finally {
                enumerator.Dispose();
            }
        }

        public static TSource LastOrDefault<TEnumerator, TSource>(this ValueEnumerable<TEnumerator,TSource> source,TSource defaultValue)
            where TEnumerator : struct, IValueEnumerator<TSource> {
            var enumerator = source.Enumerator;
            try {
                return TryGetLast<TEnumerator,TSource>(ref enumerator,out var value)
                ? value
                : defaultValue;
            }
            finally {
                enumerator.Dispose();
            }
        }

        public static TSource? LastOrDefault<TEnumerator, TSource>(this ValueEnumerable<TEnumerator,TSource> source,Func<TSource,Boolean> predicate)
            where TEnumerator : struct, IValueEnumerator<TSource> {
            ArgumentNullException.ThrowIfNull(predicate);
            var enumerator = source.Enumerator;
            try {
                return TryGetLast<TEnumerator,TSource>(ref enumerator,predicate,out var value)
                ? value
                : default;
            }
            finally {
                enumerator.Dispose();
            }
        }

        public static TSource LastOrDefault<TEnumerator, TSource>(this ValueEnumerable<TEnumerator,TSource> source,Func<TSource,Boolean> predicate,TSource defaultValue)
            where TEnumerator : struct, IValueEnumerator<TSource> {
            ArgumentNullException.ThrowIfNull(predicate);
            var enumerator = source.Enumerator;
            try {
                return TryGetLast<TEnumerator,TSource>(ref enumerator,predicate,out var value)
                ? value
                : defaultValue;
            }
            finally {
                enumerator.Dispose();
            }
        }
        private static bool TryGetLast<TEnumerator, TSource>(ref TEnumerator source,out TSource value)
           where TEnumerator : struct, IValueEnumerator<TSource> {

            var current = default(TSource)!;
#if NETSTANDARD2_0
            var span = SingleSpan.Create<TSource>();
            if (source.TryCopyTo(span, ^1))
            {
                value = span[0];
                span.Clear();
                return true;
            }
#else
            if(source.TryCopyTo(SingleSpan.Create(ref current),^1)) {
                value=current;
                return true;
            }
#endif
            else if(EnumeratorHelper.TryConsumeGetAt<TEnumerator,TSource>(ref source,^1,out current)) {
                value=current!;
                return true;
            }

            value=default!;
            return false;
        }
        private static bool TryGetLast<TEnumerator, TSource>(ref TEnumerator source,Func<TSource,Boolean> predicate,out TSource value)
            where TEnumerator : struct, IValueEnumerator<TSource> {
            if(source.TryGetSpan(out var span)) {
                // search from last
                for(var i = span.Length-1; i>=0; i--) {
                    ref readonly var v = ref span[i];
                    if(predicate(v)) {
                        value=v;
                        return true;
                    }
                }

                value=default!;
                return false;
            }

            while(source.TryGetNext(out var last)) {
                if(predicate(last)) // found
                {
                    while(source.TryGetNext(out var current)) {
                        if(predicate(current)) {
                            last=current;
                        }
                    }

                    value=last;
                    return true;
                }
            }

            value=default!;
            return false;
        }
    }
}
