using System;
using System.Collections.Generic;

namespace Seamless.Linq;

public static class SetEnumerableExtensions {
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

public readonly struct Select<TSource, TResult> : IEnumerable<TResult> {
    private readonly IEnumerable<TSource> source;
    private readonly Func<TSource, TResult>? selector;
    private readonly Func<TSource, int, TResult>? selectorWithIndex;
    public Select(IEnumerable<TSource> source, Func<TSource, TResult> selector) {
        this.source = source;
        this.selector = selector;
    }

    public Select(IEnumerable<TSource> source, Func<TSource, int, TResult> selector) {
        this.source = source;
        selectorWithIndex = selector;
    }

    public IEnumerator<TResult> GetEnumerator() =>
        new Enumerator(source, selector, selectorWithIndex);

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

public struct Enumerator(IEnumerable<TSource> source, Func<TSource, TResult>? selector, Func<TSource, int, TResult>? selectorWithIndex = null): IEnumerator<TResult> {
    IEnumerator<TSource> source = source.GetEnumerator();
    Func<TSource, TResult>? selector = selector;
    Func<TSource, int, TResult>? selectorWithIndex = selectorWithIndex;
    int index;
    TResult current;

        public TResult Current => current!;

        object IEnumerator.Current => current!;

    public bool MoveNext() {
        while(source.MoveNext()) {
            var value = source.Current;
            current = selectorWithIndex is not null
                ? selectorWithIndex(value, index++)
                : selector!.Invoke(value);
            return true;
        }
        return false;
    }

        public void Dispose() => source.Dispose();
        public void Reset() => throw new NotSupportedException();
    }
}

public readonly struct Where<TSource> : IEnumerable<TSource> {
    private readonly IEnumerable<TSource> source;
    private readonly Func<TSource, bool>? predicate;
    private readonly Func<TSource, int, bool>? predicateWithIndex;
    public Where(IEnumerable<TSource> source, Func<TSource, bool> predicate) {
        this.source = source;
        this.predicate = predicate;
    }

    public Where(IEnumerable<TSource> source, Func<TSource, int, bool> predicateWithIndex) {
        this.source = source;
        this.predicateWithIndex = predicateWithIndex;
    }

    public IEnumerator<TSource> GetEnumerator() =>
        new Enumerator(source, predicate, predicateWithIndex);

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

public struct Enumerator(IEnumerable<TSource> source, Func<TSource, bool>? predicate, Func<TSource, int, bool>? predicateWithIndex = null): IEnumerator<TSource> {
    IEnumerator<TSource> source = source.GetEnumerator();
    Func<TSource, bool>? predicate = predicate;
    Func<TSource, int, bool>? predicateWithIndex = predicateWithIndex;
    int index;
    TSource current;

        public TSource Current => current!;

        object IEnumerator.Current => current!;

    public bool MoveNext() {
        while(source.MoveNext()) {
            var item = source.Current;
            var currentIndex = index++;
            if((predicateWithIndex is not null ? predicateWithIndex(item, currentIndex) : predicate!(item))) {
                current=item;
                return true;
            }
        }
        return false;
    }

        public void Dispose() => source.Dispose();
        public void Reset() => throw new NotSupportedException();
    }
}
