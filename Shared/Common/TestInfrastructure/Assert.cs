#if !BENCH_AS_TEST
using System.Collections;

namespace Common;

public static class Assert {
    public static void True(bool condition,string? message = null) {
        if(!condition) {
            throw new InvalidOperationException(message??"Expected true.");
        }
    }

    public static void False(bool condition,string? message = null) {
        if(condition) {
            throw new InvalidOperationException(message??"Expected false.");
        }
    }

    public static void Equal<T>(T expected,T actual) {
        if(expected is IEnumerable expectedItems&&actual is IEnumerable actualItems&&expected is not string&&actual is not string) {
            EqualSequence(expectedItems,actualItems);
            return;
        }

        if(!EqualityComparer<T>.Default.Equals(expected,actual)) {
            throw new InvalidOperationException($"Expected {expected}, actual {actual}.");
        }
    }

    public static void Equal<T>(IEnumerable<T> expected,IEnumerable<T> actual) =>
        EqualSequence(expected,actual);

    public static void Same(object expected,object actual) {
        if(!ReferenceEquals(expected,actual)) {
            throw new InvalidOperationException("Expected same instance.");
        }
    }

    public static void NotNull(object? value) {
        if(value is null) {
            throw new InvalidOperationException("Expected non-null value.");
        }
    }

    public static T IsType<T>(object? value) {
        if(value is T typed&&value.GetType()==typeof(T)) {
            return typed;
        }

        throw new InvalidOperationException($"Expected type {typeof(T).FullName}, actual {value?.GetType().FullName??"<null>"}.");
    }

    public static void IsNotType<T>(object? value) {
        if(value?.GetType()==typeof(T)) {
            throw new InvalidOperationException($"Did not expect type {typeof(T).FullName}.");
        }
    }

    public static void Contains<T>(T expected,IEnumerable<T> collection) {
        foreach(var item in collection) {
            if(EqualityComparer<T>.Default.Equals(expected,item)) {
                return;
            }
        }

        throw new InvalidOperationException($"Collection did not contain {expected}.");
    }

    public static void Contains(string expectedSubstring,string actualString) {
        if(!actualString.Contains(expectedSubstring,StringComparison.Ordinal)) {
            throw new InvalidOperationException($"String did not contain {expectedSubstring}.");
        }
    }

    public static void Empty<T>(IEnumerable<T> collection) {
        if(collection.Any()) {
            throw new InvalidOperationException("Expected empty collection.");
        }
    }

    public static void Single<T>(IEnumerable<T> collection) {
        using var enumerator = collection.GetEnumerator();
        if(!enumerator.MoveNext()) {
            throw new InvalidOperationException("Expected single item, found none.");
        }

        if(enumerator.MoveNext()) {
            throw new InvalidOperationException("Expected single item, found multiple.");
        }
    }

    public static void All<T>(IEnumerable<T> collection,Action<T> action) {
        foreach(var item in collection) {
            action(item);
        }
    }

    public static TException Throws<TException>(Action action)
        where TException : Exception {
        try {
            action();
        }
        catch(TException exception) {
            return exception;
        }
        catch(Exception exception) {
            throw new InvalidOperationException($"Expected exception {typeof(TException).FullName}, actual {exception.GetType().FullName}.",exception);
        }

        throw new InvalidOperationException($"Expected exception {typeof(TException).FullName}.");
    }

    public static async Task<TException> ThrowsAsync<TException>(Func<Task> action)
        where TException : Exception {
        try {
            await action().ConfigureAwait(false);
        }
        catch(TException exception) {
            return exception;
        }
        catch(Exception exception) {
            throw new InvalidOperationException($"Expected exception {typeof(TException).FullName}, actual {exception.GetType().FullName}.",exception);
        }

        throw new InvalidOperationException($"Expected exception {typeof(TException).FullName}.");
    }

    public static async Task<TException> ThrowsAnyAsync<TException>(Func<Task> action)
        where TException : Exception {
        try {
            await action().ConfigureAwait(false);
        }
        catch(TException exception) {
            return exception;
        }
        catch(Exception exception) when(exception is TException) {
            return (TException)exception;
        }
        catch(Exception exception) {
            throw new InvalidOperationException($"Expected exception assignable to {typeof(TException).FullName}, actual {exception.GetType().FullName}.",exception);
        }

        throw new InvalidOperationException($"Expected exception assignable to {typeof(TException).FullName}.");
    }

    private static void EqualSequence(IEnumerable expected,IEnumerable actual) {
        var expectedEnumerator = expected.GetEnumerator();
        var actualEnumerator = actual.GetEnumerator();
        var index = 0;
        while(true) {
            var expectedHasItem = expectedEnumerator.MoveNext();
            var actualHasItem = actualEnumerator.MoveNext();
            if(!expectedHasItem&&!actualHasItem) {
                return;
            }

            if(expectedHasItem!=actualHasItem||!object.Equals(expectedEnumerator.Current,actualEnumerator.Current)) {
                throw new InvalidOperationException($"Sequences differ at index {index}.");
            }

            index++;
        }
    }
}
#endif
