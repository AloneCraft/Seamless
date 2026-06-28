using System;
using System.Collections.Generic;

namespace Seamless.Collections;

public static class SetEnumerableExtensions {
    public static IEnumerable<TResult> Select<TSource, TResult>(this ISet<TSource> source, Func<TSource, TResult> selector) {
        ArgumentNullException.ThrowIfNull(source);
        return Enumerable.Select(source, selector);
    }

    public static IEnumerable<TResult> Select<TSource, TResult>(this ISet<TSource> source, Func<TSource, int, TResult> selector) {
        ArgumentNullException.ThrowIfNull(source);
        return Enumerable.Select(source, selector);
    }

    public static IEnumerable<TSource> Where<TSource>(this ISet<TSource> source, Func<TSource, bool> predicate) {
        ArgumentNullException.ThrowIfNull(source);
        return Enumerable.Where(source, predicate);
    }

    public static IEnumerable<TSource> Where<TSource>(this ISet<TSource> source, Func<TSource, int, bool> predicate) {
        ArgumentNullException.ThrowIfNull(source);
        return Enumerable.Where(source, predicate);
    }
}
