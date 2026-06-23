using System;
using global::System.Collections.Generic;

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
        this.selectorWithIndex = selector;
    }

    public IEnumerator<TResult> GetEnumerator() =>
        new Enumerator(this.source, this.selector, this.selectorWithIndex);

    global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator() => this.GetEnumerator();

public struct Enumerator(IEnumerable<TSource> source, Func<TSource, TResult>? selector, Func<TSource, int, TResult>? selectorWithIndex = null): IEnumerator<TResult> {
    IEnumerator<TSource> source = source.GetEnumerator();
    Func<TSource, TResult>? selector = selector;
    Func<TSource, int, TResult>? selectorWithIndex = selectorWithIndex;
    int index;
    TResult current;

        public TResult Current => this.current!;

        object global::System.Collections.IEnumerator.Current => this.current!;

    public bool MoveNext() {
        while(this.source.MoveNext()) {
            var value = this.source.Current;
            this.current = this.selectorWithIndex is not null
                ? this.selectorWithIndex(value, this.index++)
                : this.selector!.Invoke(value);
            return true;
        }
        return false;
    }

        public void Dispose() => this.source.Dispose();
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
        new Enumerator(this.source, this.predicate, this.predicateWithIndex);

    global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator() => this.GetEnumerator();

public struct Enumerator(IEnumerable<TSource> source, Func<TSource, bool>? predicate, Func<TSource, int, bool>? predicateWithIndex = null): IEnumerator<TSource> {
    IEnumerator<TSource> source = source.GetEnumerator();
    Func<TSource, bool>? predicate = predicate;
    Func<TSource, int, bool>? predicateWithIndex = predicateWithIndex;
    int index;
    TSource current;

        public TSource Current => this.current!;

        object global::System.Collections.IEnumerator.Current => this.current!;

    public bool MoveNext() {
        while(this.source.MoveNext()) {
            var item = this.source.Current;
            var currentIndex = this.index++;
            if((this.predicateWithIndex is not null ? this.predicateWithIndex(item, currentIndex) : this.predicate!(item))) {
                this.current=item;
                return true;
            }
        }
        return false;
    }

        public void Dispose() => this.source.Dispose();
        public void Reset() => throw new NotSupportedException();
    }
}
