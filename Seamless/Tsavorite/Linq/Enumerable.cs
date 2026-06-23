using Seamless.Linq;

namespace Seamless.Tsavorite.Linq;

public static partial class Enumerable {
    public static Select<TKey, TResult> Select<TKey, TResult>(this global::Seamless.Tsavorite.Collections.HashSet<TKey> source, Func<TKey, TResult> selector) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return new Select<TKey, TResult>(source, selector);
    }

    public static Select<TKey, TResult> Select<TKey, TResult>(this global::Seamless.Tsavorite.Collections.HashSet<TKey> source, Func<TKey, int, TResult> selector) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return new Select<TKey, TResult>(source, selector);
    }

    public static Where<TKey> Where<TKey>(this global::Seamless.Tsavorite.Collections.HashSet<TKey> source, Func<TKey, bool> predicate) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        return new Where<TKey>(source, predicate);
    }

    public static Where<TKey> Where<TKey>(this global::Seamless.Tsavorite.Collections.HashSet<TKey> source, Func<TKey, int, bool> predicate) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        return new Where<TKey>(source, predicate);
    }

    public static Select<TRecord, TResult> Select<TKey, TRecord, TResult>(this global::Seamless.Tsavorite.Collections.Set<TKey, TRecord> source, Func<TRecord, TResult> selector)
        where TRecord : global::Seamless.Collections.IRecord<TKey> {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return new Select<TRecord, TResult>(source, selector);
    }

    public static Select<TRecord, TResult> Select<TKey, TRecord, TResult>(this global::Seamless.Tsavorite.Collections.Set<TKey, TRecord> source, Func<TRecord, int, TResult> selector)
        where TRecord : global::Seamless.Collections.IRecord<TKey> {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return new Select<TRecord, TResult>(source, selector);
    }

    public static Where<TRecord> Where<TKey, TRecord>(this global::Seamless.Tsavorite.Collections.Set<TKey, TRecord> source, Func<TRecord, bool> predicate)
        where TRecord : global::Seamless.Collections.IRecord<TKey> {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        return new Where<TRecord>(source, predicate);
    }

    public static Where<TRecord> Where<TKey, TRecord>(this global::Seamless.Tsavorite.Collections.Set<TKey, TRecord> source, Func<TRecord, int, bool> predicate)
        where TRecord : global::Seamless.Collections.IRecord<TKey> {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        return new Where<TRecord>(source, predicate);
    }

    public static Select<TSource, TResult> Select<TInput, TSource, TResult>(this Select<TInput, TSource> source, Func<TSource, TResult> selector) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return new Select<TSource, TResult>(source, selector);
    }

    public static Select<TSource, TResult> Select<TInput, TSource, TResult>(this Select<TInput, TSource> source, Func<TSource, int, TResult> selector) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return new Select<TSource, TResult>(source, selector);
    }

    public static Where<TSource> Where<TInput, TSource>(this Select<TInput, TSource> source, Func<TSource, bool> predicate) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        return new Where<TSource>(source, predicate);
    }

    public static Where<TSource> Where<TInput, TSource>(this Select<TInput, TSource> source, Func<TSource, int, bool> predicate) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        return new Where<TSource>(source, predicate);
    }

    public static Select<TSource, TResult> Select<TSource, TResult>(this Where<TSource> source, Func<TSource, TResult> selector) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return new Select<TSource, TResult>(source, selector);
    }

    public static Select<TSource, TResult> Select<TSource, TResult>(this Where<TSource> source, Func<TSource, int, TResult> selector) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return new Select<TSource, TResult>(source, selector);
    }

    public static Where<TSource> Where<TSource>(this Where<TSource> source, Func<TSource, bool> predicate) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        return new Where<TSource>(source, predicate);
    }

    public static Where<TSource> Where<TSource>(this Where<TSource> source, Func<TSource, int, bool> predicate) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        return new Where<TSource>(source, predicate);
    }
}
