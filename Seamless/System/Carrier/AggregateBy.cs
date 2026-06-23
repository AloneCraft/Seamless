namespace Seamless.System.Carrier {
    partial class ValueEnumerableExtensions {
        // The original dotnet implementation uses a Dictionary so it has TKey : notnull constraints.
        // However, I think it would be better without any null constraints.
        // https://github.com/dotnet/runtime/issues/98259
        // I'm going to use DictionarySlim instead of Dictionary so removed TKey : notnull constraints.

        public static ValueEnumerable<AggregateBy<TEnumerator,TSource,TKey,TAccumulate>,KeyValuePair<TKey,TAccumulate>> AggregateBy<TEnumerator, TSource, TKey, TAccumulate>(this ValueEnumerable<TEnumerator,TSource> source,Func<TSource,TKey> keySelector,TAccumulate seed,Func<TAccumulate,TSource,TAccumulate> func)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator,Throws.IfNull(keySelector),seed,Throws.IfNull(func),null));

        public static ValueEnumerable<AggregateBy<TEnumerator,TSource,TKey,TAccumulate>,KeyValuePair<TKey,TAccumulate>> AggregateBy<TEnumerator, TSource, TKey, TAccumulate>(this ValueEnumerable<TEnumerator,TSource> source,Func<TSource,TKey> keySelector,TAccumulate seed,Func<TAccumulate,TSource,TAccumulate> func,IEqualityComparer<TKey>? keyComparer)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator,Throws.IfNull(keySelector),seed,Throws.IfNull(func),keyComparer));

        public static ValueEnumerable<AggregateBy2<TEnumerator,TSource,TKey,TAccumulate>,KeyValuePair<TKey,TAccumulate>> AggregateBy<TEnumerator, TSource, TKey, TAccumulate>(this ValueEnumerable<TEnumerator,TSource> source,Func<TSource,TKey> keySelector,Func<TKey,TAccumulate> seedSelector,Func<TAccumulate,TSource,TAccumulate> func)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator,Throws.IfNull(keySelector),Throws.IfNull(seedSelector),Throws.IfNull(func),null));

        public static ValueEnumerable<AggregateBy2<TEnumerator,TSource,TKey,TAccumulate>,KeyValuePair<TKey,TAccumulate>> AggregateBy<TEnumerator, TSource, TKey, TAccumulate>(this ValueEnumerable<TEnumerator,TSource> source,Func<TSource,TKey> keySelector,Func<TKey,TAccumulate> seedSelector,Func<TAccumulate,TSource,TAccumulate> func,IEqualityComparer<TKey>? keyComparer)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator,Throws.IfNull(keySelector),Throws.IfNull(seedSelector),Throws.IfNull(func),keyComparer));
    }
}

namespace Seamless.System.Carrier {
    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct AggregateBy<TEnumerator, TSource, TKey, TAccumulate>(TEnumerator source,Func<TSource,TKey> keySelector,TAccumulate seed,Func<TAccumulate,TSource,TAccumulate> func,IEqualityComparer<TKey>? keyComparer)
        :IValueEnumerator<KeyValuePair<TKey,TAccumulate>>
        where TEnumerator : struct, IValueEnumerator<TSource> {
        TEnumerator source = source;

        DictionarySlim<TKey,TAccumulate>? dictionary;
        DictionarySlim<TKey,TAccumulate>.Enumerator enumerator;

        public bool TryGetNonEnumeratedCount(out int count) {
            count=0;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<KeyValuePair<TKey,TAccumulate>> span) {
            span=default;
            return false;
        }

        public bool TryCopyTo(scoped Span<KeyValuePair<TKey,TAccumulate>> destination,Index offset) => false;

        public bool TryGetNext(out KeyValuePair<TKey,TAccumulate> current) {
            if(this.dictionary==null) {
                // if empty, don't create dictionary.
                if(this.source.TryGetNonEnumeratedCount(out var count)&&count==0) {
                    Unsafe.SkipInit(out current);
                    return false;
                }
                this.Initialize();
            }

            if(this.enumerator.TryGetNext(out current)) {
                return true;
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        void Initialize() {
            var dict = keyComparer!=null
                ? new DictionarySlim<TKey,TAccumulate>(keyComparer)
                : new DictionarySlim<TKey,TAccumulate>();

            using(this.source) {
                if(this.source.TryGetSpan(out var span)) {
                    foreach(var item in span) {
                        var key = keySelector(item);
                        ref var accumulate = ref dict.GetValueRefOrAddDefault(key,out var exists);
                        accumulate=func(exists ? accumulate! : seed,item);
                    }
                }
                else {
                    while(this.source.TryGetNext(out var item)) {
                        var key = keySelector(item);
                        ref var accumulate = ref dict.GetValueRefOrAddDefault(key,out var exists);
                        accumulate=func(exists ? accumulate! : seed,item);
                    }
                }
            }

            this.dictionary=dict;
            this.enumerator=dict.GetEnumerator();
        }

        public void Dispose() {
            this.dictionary?.Dispose();
            this.source.Dispose();
        }
    }

    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct AggregateBy2<TEnumerator, TSource, TKey, TAccumulate>(TEnumerator source,Func<TSource,TKey> keySelector,Func<TKey,TAccumulate> seedSelector,Func<TAccumulate,TSource,TAccumulate> func,IEqualityComparer<TKey>? keyComparer)
        :IValueEnumerator<KeyValuePair<TKey,TAccumulate>>
        where TEnumerator : struct, IValueEnumerator<TSource> {
        TEnumerator source = source;

        DictionarySlim<TKey,TAccumulate>? dictionary;
        DictionarySlim<TKey,TAccumulate>.Enumerator enumerator;

        public bool TryGetNonEnumeratedCount(out int count) {
            count=0;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<KeyValuePair<TKey,TAccumulate>> span) {
            span=default;
            return false;
        }

        public bool TryCopyTo(scoped Span<KeyValuePair<TKey,TAccumulate>> destination,Index offset) => false;

        public bool TryGetNext(out KeyValuePair<TKey,TAccumulate> current) {
            if(this.dictionary==null) {
                // if empty, don't create dictionary.
                if(this.source.TryGetNonEnumeratedCount(out var count)&&count==0) {
                    Unsafe.SkipInit(out current);
                    return false;
                }

                this.Initialize();
            }

            if(this.enumerator.TryGetNext(out current)) {
                return true;
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        void Initialize() {
            var dict = keyComparer!=null
                ? new DictionarySlim<TKey,TAccumulate>(keyComparer)
                : new DictionarySlim<TKey,TAccumulate>();

            using(this.source) {
                if(this.source.TryGetSpan(out var span)) {
                    foreach(var item in span) {
                        var key = keySelector(item);
                        ref var accumulate = ref dict.GetValueRefOrAddDefault(key,out var exists);
                        accumulate=func(exists ? accumulate! : seedSelector(key),item);
                    }
                }
                else {
                    while(this.source.TryGetNext(out var item)) {
                        var key = keySelector(item);
                        ref var accumulate = ref dict.GetValueRefOrAddDefault(key,out var exists);
                        accumulate=func(exists ? accumulate! : seedSelector(key),item);
                    }
                }
            }

            this.dictionary=dict;
            this.enumerator=dict.GetEnumerator();
        }

        public void Dispose() {
            this.dictionary?.Dispose();
            this.source.Dispose();
        }
    }
}
