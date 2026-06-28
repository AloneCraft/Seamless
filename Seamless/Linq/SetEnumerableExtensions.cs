using System;
using System.Collections.Generic;

namespace Seamless.Linq;

public static class SetEnumerableExtensions {
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

public readonly struct Select<TSource, TResult> : IEnumerable<TResult> {
    private readonly IEnumerable<TSource> source;
    private readonly Func<TSource, int, TResult> selector;
    public Select(IEnumerable<TSource> source, Func<TSource, TResult> selector)
        : this(source,(Item,Index)=>selector(Item)) {
    }

    public Select(IEnumerable<TSource> source, Func<TSource, int, TResult> selector) {
        this.source = source;
        this.selector = selector;
    }

    public IEnumerator<TResult> GetEnumerator() =>
        new Enumerator(source, selector);

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

public struct Enumerator(IEnumerable<TSource> source, Func<TSource, int, TResult> selector): IEnumerator<TResult> {
    IEnumerator<TSource> source = source.GetEnumerator();
    Func<TSource, int, TResult> selector = selector;
    int index;
    TResult current = default!;

        public TResult Current => current!;

        object IEnumerator.Current => current!;

    public bool MoveNext() {
        while(source.MoveNext()) {
            var value = source.Current;
            current = selector(value, index++);
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
    private readonly Func<TSource, int, bool> predicate;
    public Where(IEnumerable<TSource> source, Func<TSource, bool> predicate)
        : this(source,(Item,Index)=>predicate(Item)) {
    }

    public Where(IEnumerable<TSource> source, Func<TSource, int, bool> predicate) {
        this.source = source;
        this.predicate = predicate;
    }

    public IEnumerator<TSource> GetEnumerator() =>
        new Enumerator(source, predicate);

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

public struct Enumerator(IEnumerable<TSource> source, Func<TSource, int, bool> predicate): IEnumerator<TSource> {
    IEnumerator<TSource> source = source.GetEnumerator();
    Func<TSource, int, bool> predicate = predicate;
    int index;
    TSource current = default!;

        public TSource Current => current!;

        object IEnumerator.Current => current!;

    public bool MoveNext() {
        while(source.MoveNext()) {
            var item = source.Current;
            var currentIndex = index++;
            if(predicate(item, currentIndex)) {
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
