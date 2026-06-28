using Seamless.Linq;

namespace Seamless.Persisted.Linq;

public static partial class Enumerable {
    public static Select<TSource, TResult> Select<TSource, TResult>(this Collections.Set<TSource> source, Func<TSource, TResult> selector) {
        return new Select<TSource, TResult>(source,(Item,Index)=>selector(Item));
    }

    public static Select<TSource, TResult> Select<TSource, TResult>(this Collections.Set<TSource> source, Func<TSource, int, TResult> selector) {
        return new Select<TSource, TResult>(source, selector);
    }

    public static Where<TSource> Where<TSource>(this Collections.Set<TSource> source, Func<TSource, bool> predicate) {
        return new Where<TSource>(source,(Item,Index)=>predicate(Item));
    }

    public static Where<TSource> Where<TSource>(this Collections.Set<TSource> source, Func<TSource, int, bool> predicate) {
        return new Where<TSource>(source, predicate);
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
