namespace Seamless.System.Carrier {
    partial class ValueEnumerableExtensions {
        // The original dotnet implementation uses a Dictionary so it has TKey : notnull constraints.
        // However, I think it would be better without any null constraints.
        // https://github.com/dotnet/runtime/issues/98259
        // I'm going to use DictionarySlim instead of Dictionary so removed TKey : notnull constraints.

        public static ValueEnumerable<CountBy<TEnumerator,TSource,TKey>,KeyValuePair<TKey,int>> CountBy<TEnumerator, TSource, TKey>(this ValueEnumerable<TEnumerator,TSource> source,Func<TSource,TKey> keySelector)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator,Throws.IfNull(keySelector),null));

        public static ValueEnumerable<CountBy<TEnumerator,TSource,TKey>,KeyValuePair<TKey,int>> CountBy<TEnumerator, TSource, TKey>(this ValueEnumerable<TEnumerator,TSource> source,Func<TSource,TKey> keySelector,IEqualityComparer<TKey>? keyComparer)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator,Throws.IfNull(keySelector),keyComparer));
    }
}

namespace Seamless.System.Carrier {
    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct CountBy<TEnumerator, TSource, TKey>(TEnumerator source,Func<TSource,TKey> keySelector,IEqualityComparer<TKey>? keyComparer)
        :IValueEnumerator<KeyValuePair<TKey,int>>
        where TEnumerator : struct, IValueEnumerator<TSource> {
        private TEnumerator source = source;
        private DictionarySlim<TKey,int>? dictionary;
        private DictionarySlim<TKey,int>.Enumerator enumerator;

        public bool TryGetNonEnumeratedCount(out int count) {
            count=0;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<KeyValuePair<TKey,int>> span) {
            span=default;
            return false;
        }


        public bool TryCopyTo(scoped Span<KeyValuePair<TKey,int>> destination,Index offset) => false;

        public bool TryGetNext(out KeyValuePair<TKey,int> current) {
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
        private void Initialize() {
            using(this.source) {
                if(this.source.TryGetSpan(out var span)) {
                    var dict = keyComparer!=null
                        ? new DictionarySlim<TKey,int>(Math.Min(span.Length,1024),keyComparer)
                        : new DictionarySlim<TKey,int>(Math.Min(span.Length,1024));
                    foreach(var item in span) {
                        var key = keySelector(item);
                        ref var count = ref dict.GetValueRefOrAddDefault(key,out var exists);
                        checked { count+=1; }
                    }
                    this.dictionary=dict;
                } else {
                    var capacityHint = this.source.TryGetNonEnumeratedCount(out var sourceCount)
                        ? Math.Min(sourceCount,1024)
                        : 0;
                    var dict = keyComparer!=null
                        ? new DictionarySlim<TKey,int>(capacityHint,keyComparer)
                        : new DictionarySlim<TKey,int>(capacityHint);
                    while(this.source.TryGetNext(out var item)) {
                        var key = keySelector(item);
                        ref var count = ref dict.GetValueRefOrAddDefault(key,out var exists);
                        checked { count+=1; }
                    }
                    this.dictionary=dict;
                }
            }

            this.enumerator=this.dictionary.GetEnumerator();
        }

        public void Dispose() {
            this.dictionary?.Dispose();
            this.source.Dispose();
        }
    }
}
