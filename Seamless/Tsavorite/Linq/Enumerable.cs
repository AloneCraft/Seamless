using Seamless.Linq;

namespace Seamless.Tsavorite.Linq;

public static partial class Enumerable {
    public static Select<TKey, TResult> Select<TKey, TResult>(this Collections.HashSet<TKey> source, Func<TKey, TResult> selector) {
        return new Select<TKey, TResult>(source,(Item,Index)=>selector(Item));
    }

    public static Select<TKey, TResult> Select<TKey, TResult>(this Collections.HashSet<TKey> source, Func<TKey, int, TResult> selector) {
        return new Select<TKey, TResult>(source, selector);
    }

    public static Where<TKey> Where<TKey>(this Collections.HashSet<TKey> source, Func<TKey, bool> predicate) {
        return new Where<TKey>(source,(Item,Index)=>predicate(Item));
    }

    public static Where<TKey> Where<TKey>(this Collections.HashSet<TKey> source, Func<TKey, int, bool> predicate) {
        return new Where<TKey>(source, predicate);
    }

    public static Select<TRecord, TResult> Select<TKey, TRecord, TResult>(this Set<TKey, TRecord> source, Func<TRecord, TResult> selector)
        where TRecord : IRecord<TKey> {
        return new Select<TRecord, TResult>(source,(Item,Index)=>selector(Item));
    }

    public static Select<TRecord, TResult> Select<TKey, TRecord, TResult>(this Set<TKey, TRecord> source, Func<TRecord, int, TResult> selector)
        where TRecord : IRecord<TKey> {
        return new Select<TRecord, TResult>(source, selector);
    }

    public static Where<TRecord> Where<TKey, TRecord>(this Set<TKey, TRecord> source, Func<TRecord, bool> predicate)
        where TRecord : IRecord<TKey> {
        return new Where<TRecord>(source,(Item,Index)=>predicate(Item));
    }

    public static Where<TRecord> Where<TKey, TRecord>(this Set<TKey, TRecord> source, Func<TRecord, int, bool> predicate)
        where TRecord : IRecord<TKey> {
        return new Where<TRecord>(source, predicate);
    }

    public static Select<TSource, TResult> Select<TInput, TSource, TResult>(this Select<TInput, TSource> source, Func<TSource, TResult> selector) {
        return new Select<TSource, TResult>(source,(Item,Index)=>selector(Item));
    }

    public static Select<TSource, TResult> Select<TInput, TSource, TResult>(this Select<TInput, TSource> source, Func<TSource, int, TResult> selector) {
        return new Select<TSource, TResult>(source, selector);
    }

    public static Where<TSource> Where<TInput, TSource>(this Select<TInput, TSource> source, Func<TSource, bool> predicate) {
        return new Where<TSource>(source,(Item,Index)=>predicate(Item));
    }

    public static Where<TSource> Where<TInput, TSource>(this Select<TInput, TSource> source, Func<TSource, int, bool> predicate) {
        return new Where<TSource>(source, predicate);
    }

    public static Select<TSource, TResult> Select<TSource, TResult>(this Where<TSource> source, Func<TSource, TResult> selector) {
        return new Select<TSource, TResult>(source,(Item,Index)=>selector(Item));
    }

    public static Select<TSource, TResult> Select<TSource, TResult>(this Where<TSource> source, Func<TSource, int, TResult> selector) {
        return new Select<TSource, TResult>(source, selector);
    }

    public static Where<TSource> Where<TSource>(this Where<TSource> source, Func<TSource, bool> predicate) {
        return new Where<TSource>(source,(Item,Index)=>predicate(Item));
    }

    public static Where<TSource> Where<TSource>(this Where<TSource> source, Func<TSource, int, bool> predicate) {
        return new Where<TSource>(source, predicate);
    }
}
