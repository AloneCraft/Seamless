using System;
using System.Collections.Generic;
using Seamless.Linq;
using Xunit;

namespace Seamless.TestKit;

/// <summary>
/// クエリ実行においてフォールバックゼロと期待値一致をアサートするテストヘルパー。
/// </summary>
public static class AssertQuery
{
    public static T MatchesWithZeroFallback<T>(Func<T> query, T expected)
    {
        using var observation = LinqFallbackObservation.Start();
        using var scope = LinqCounterScope.Start();
        var actual = query();
        Assert.Equal(0, observation.Count);
        Assert.Equal(expected, actual);
        return actual;
    }

    public static void MatchesSequenceWithZeroFallback<T>(Func<IEnumerable<T>> query, IEnumerable<T> expected)
    {
        using var observation = LinqFallbackObservation.Start();
        using var scope = LinqCounterScope.Start();
        var actual = query();
        Assert.Equal(0, observation.Count);
        Assert.Equal(expected, actual);
    }

    public static void MatchesWithZeroFallback<T>(Func<T> query, Action<T> assert)
    {
        using var observation = LinqFallbackObservation.Start();
        using var scope = LinqCounterScope.Start();
        var actual = query();
        Assert.Equal(0, observation.Count);
        assert(actual);
    }

    public static TException ThrowsWithZeroFallback<TException>(Action query) where TException : Exception
    {
        using var observation = LinqFallbackObservation.Start();
        using var scope = LinqCounterScope.Start();
        var ex = Assert.Throws<TException>(query);
        Assert.Equal(0, observation.Count);
        return ex;
    }
}
