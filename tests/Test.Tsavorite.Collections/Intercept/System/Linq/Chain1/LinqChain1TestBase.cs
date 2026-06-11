using System.Linq;

namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain1;

public sealed record Chain1Row(int Key,int Value,string Name);

public abstract class LinqChain1TestBase {
    public static int[] Source() => [1,2,3,4];
    public static int[] SourceWithDuplicates() => [1,2,2,3,3,4];
    public static int[] SourceWithDefault() => [0,1,2,3];
    public static int[] EmptySource() => [];
    public static int[] Second() => [3,4,5];
    public static int[] EqualSource() => [1,2,3,4];
    public static IEnumerable CastObjects() => new object[] { 1,2,3 };
    public static IEnumerable MixedObjects() => new object?[] { 1,"hello",2,null,3.0,4 };
    public static Chain1Row[] Rows() => [new(1,10,"beta"),new(2,20,"alpha"),new(3,30,"gamma"),new(1,40,"delta")];
    public static Chain1Row[] OtherRows() => [new(1,100,"one"),new(2,200,"two"),new(4,400,"four")];

    public readonly struct IntIndexPredicate(int offset) {
        public bool LessThanThreshold(int value,int index) => value+offset>index;
    }

    public static void AssertSequenceEqual<T>(IEnumerable<T> expected,IEnumerable<T> actual) {
        Assert.Equal(expected,actual);
    }

    public static void AssertNestedSequenceEqual<T>(IEnumerable<IEnumerable<T>> expected,IEnumerable<IEnumerable<T>> actual) {
        using var expectedEnumerator=expected.GetEnumerator();
        using var actualEnumerator=actual.GetEnumerator();
        while(true) {
            var hasExpected=expectedEnumerator.MoveNext();
            var hasActual=actualEnumerator.MoveNext();
            Assert.Equal(hasExpected,hasActual);
            if(!hasExpected) {
                return;
            }

            Assert.Equal(expectedEnumerator.Current,actualEnumerator.Current);
        }
    }

    public static void AssertGroupSequenceEqual<TKey,TElement>(IEnumerable<IGrouping<TKey,TElement>> expected,IEnumerable<IGrouping<TKey,TElement>> actual) {
        using var expectedEnumerator=expected.GetEnumerator();
        using var actualEnumerator=actual.GetEnumerator();
        while(true) {
            var hasExpected=expectedEnumerator.MoveNext();
            var hasActual=actualEnumerator.MoveNext();
            Assert.Equal(hasExpected,hasActual);
            if(!hasExpected) {
                return;
            }

            Assert.Equal(expectedEnumerator.Current.Key,actualEnumerator.Current.Key);
            Assert.Equal(expectedEnumerator.Current,actualEnumerator.Current);
        }
    }

    public static void AssertDictionaryEqual<TKey,TValue>(IDictionary<TKey,TValue> expected,IDictionary<TKey,TValue> actual)
        where TKey:notnull {
        Assert.Equal(expected.Count,actual.Count);
        foreach(var (key,value) in expected) {
            Assert.True(actual.TryGetValue(key,out var actualValue));
            Assert.Equal(value,actualValue);
        }
    }

    public static void AssertLookupEqual<TKey,TElement>(ILookup<TKey,TElement> expected,ILookup<TKey,TElement> actual) {
        Assert.Equal(expected.Count,actual.Count);
        foreach(var group in expected) {
            Assert.Equal(group,actual[group.Key]);
        }
    }
}
