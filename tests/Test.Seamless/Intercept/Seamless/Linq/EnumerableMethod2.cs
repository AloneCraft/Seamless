using Seamless.Linq;
using Test.Seamless.Collections;

using NonGenericEnumerable = System.Collections.IEnumerable;
using NonGenericEnumerator = System.Collections.IEnumerator;
using a = Seamless.Linq.Enumerable;
using e = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.Seamless.Linq;

public static class EnumerableMethod2 {
    [Fact, Trait("Category","Intercept")]
    public static void Seamless_Linq_Enumerableがインライン化されることを期待する0() {
        //インライン前
        {
            var expected = a.Select(
                a.Where(
                    Source,
                    static value => value%2==0
                ),
                static value => value+1
            );
        }
        //インライン後0
        {
            var selector0 = static (int value) => value+1;
            var result0 = new List<int>();
            foreach(var item in Source) {
                result0.Add(Inline0(item,selector0));
            }
            var expected0 = result0;
            static int Inline0(int item,Func<int,int> selector) {
                return selector(item);
            }
        }
        //こういう最適化したい。２重ループにならない内側メソッドはSelect,Where,Takeなどいくつかある
    }
    [Fact, Trait("Category","Intercept")]
    public static void Seamless_Linq_Enumerableがインライン化されることを期待する1() {
        //インライン前
        {
            var expected = a.Select(Source,static value => value+1);
        }
        //インライン後0
        {
            var selector0 = static (int value) => value+1;
            var result0 = new List<int>();
            foreach(var item in Source) {
                result0.Add(Inline0(item,selector0));
            }
            var expected0 = result0;
            static int Inline0(int item,Func<int,int> selector) {
                return selector(item);
            }
        }
        //こういう最適化したい。２重ループにならない内側メソッドはSelect,Where,Takeなどいくつかある
    }
    [Fact, Trait("Category","Intercept")]
    public static void Seamless_Linq_Enumerableがインライン化されることを期待する2() {
        //インライン前
        {
            var expected = a.Select(a.Where(Source,static value => value==1),static value => value*10);
        }
        //インライン後0
        {
            var selector0 = static (int value) => value+1;
            var result0 = new List<int>();
            foreach(var item in Source) {
                result0.Add(Inline0(item,selector0));
            }
            var expected0 = result0;
            static int Inline0(int item,Func<int,int> selector) {
                return selector(item);
            }
            var selector1 = static (int value) => value*10;
            var result1 = new List<int>();
            foreach(var item in result0) {
                result1.Add(Inline1(item,selector1));
            }
            var expected1 = result1;//これが結果
            static int Inline1(int item,Func<int,int> selector) {
                return selector(item);
            }
        }
        //インライン後1
        {
            var selector0 = static (int value) => value+1;
            var selector1 = static (int value) => value*10;
            var result0 = new List<int>();
            foreach(var item in Source) {
                result0.Add(Inline1(Inline0(item,selector0),selector1));
            }
            var expected0 = result0;//これが結果
            static int Inline0(int item,Func<int,int> selector) {
                return selector(item);
            }
            static int Inline1(int item,Func<int,int> selector) {
                return selector(item);
            }
        }
        //こういう最適化したい。２重ループにならない内側メソッドはSelect,Where,Takeなどいくつかある
    }
    [Fact, Trait("Category","Intercept")]
    public static void Select_Select_ToArray() => AssertChain(
        e.Select(e.Select(Source,static value => value+1),static value => value*10),
        a.Select(a.Select(Source,static value => value+1),static value => value*10)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Select_Select_index_ToArray() => AssertChain(
        e.Select(e.Select(Source,static value => value+1),static (value,index) => value+index),
        a.Select(a.Select(Source,static value => value+1),static (value,index) => value+index)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Select_Where_ToArray() => AssertChain(
        e.Where(e.Select(Source,static value => value*10),static value => value>20),
        a.Where(a.Select(Source,static value => value*10),static value => value>20)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Select_Where_index_ToArray() => AssertChain(
        e.Where(e.Select(Source,static value => value*10),static (value,index) => value>20&&index>0),
        a.Where(a.Select(Source,static value => value*10),static (value,index) => value>20&&index>0)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Select_index_Select_ToArray() => AssertChain(
        e.Select(e.Select(Source,static (value,index) => value+index),static value => value*10),
        a.Select(a.Select(Source,static (value,index) => value+index),static value => value*10)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Select_index_Select_index_ToArray() => AssertChain(
        e.Select(e.Select(Source,static (value,index) => value+index),static (value,index) => value+index),
        a.Select(a.Select(Source,static (value,index) => value+index),static (value,index) => value+index)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Select_index_Where_ToArray() => AssertChain(
        e.Where(e.Select(Source,static (value,index) => value+index),static value => value>4),
        a.Where(a.Select(Source,static (value,index) => value+index),static value => value>4)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Select_index_Where_index_ToArray() => AssertChain(
        e.Where(e.Select(Source,static (value,index) => value+index),static (value,index) => value>3&&index>1),
        a.Where(a.Select(Source,static (value,index) => value+index),static (value,index) => value>3&&index>1)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Where_Select_ToArray() => AssertChain(
        e.Select(e.Where(Source,static value => value>1),static value => value*10),
        a.Select(a.Where(Source,static value => value>1),static value => value*10)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Where_Select_index_ToArray() => AssertChain(
        e.Select(e.Where(Source,static value => value>1),static (value,index) => value+index),
        a.Select(a.Where(Source,static value => value>1),static (value,index) => value+index)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Where_Where_ToArray() => AssertChain(
        e.Where(e.Where(Source,static value => value>1),static value => value<4),
        a.Where(a.Where(Source,static value => value>1),static value => value<4)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Where_Where_index_ToArray() => AssertChain(
        e.Where(e.Where(Source,static value => value>1),static (value,index) => value<4&&index<2),
        a.Where(a.Where(Source,static value => value>1),static (value,index) => value<4&&index<2)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Where_index_Select_ToArray() => AssertChain(
        e.Select(e.Where(Source,static (value,index) => value>index),static value => value*10),
        a.Select(a.Where(Source,static (value,index) => value>index),static value => value*10)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Where_index_Select_index_ToArray() => AssertChain(
        e.Select(e.Where(Source,static (value,index) => value>index),static (value,index) => value+index),
        a.Select(a.Where(Source,static (value,index) => value>index),static (value,index) => value+index)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Where_index_Where_ToArray() => AssertChain(
        e.Where(e.Where(Source,static (value,index) => value>index),static value => value<4),
        a.Where(a.Where(Source,static (value,index) => value>index),static value => value<4)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Where_index_Where_index_ToArray() => AssertChain(
        e.Where(e.Where(Source,static (value,index) => value>index),static (value,index) => value<4&&index<2),
        a.Where(a.Where(Source,static (value,index) => value>index),static (value,index) => value<4&&index<2)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Select_Take_Select_ToArray() => AssertChain(
        e.Select(e.Take(e.Select(Source,static value => value+1),2),static value => value*10),
        a.Select(a.Take(a.Select(Source,static value => value+1),2),static value => value*10)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Take_zero_Select_does_not_enumerate_source_ToArray() => AssertChain(
        e.Select(e.Take(ThrowingSource(),0),static value => value*10),
        a.Select(a.Take(ThrowingSource(),0),static value => value*10)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Select_Where_TypeVariants_ToArray() {
        AssertChain(
            e.Where(e.Select(LongSource,static value => value*10L),static value => value>20L),
            a.Where(a.Select(LongSource,static value => value*10L),static value => value>20L)
        );
        AssertChain(
            e.Where(e.Select(StringSource,static value => value.ToUpperInvariant()),static value => value.Length>1),
            a.Where(a.Select(StringSource,static value => value.ToUpperInvariant()),static value => value.Length>1)
        );
        AssertChain(
            e.Where(e.Select(ManagedStructSource,static value => CreateManagedStruct(value)),static value => value.Name.Length>2),
            a.Where(a.Select(ManagedStructSource,static value => CreateManagedStruct(value)),static value => value.Name.Length>2)
        );
    }

    private static void AssertChain<T>(IEnumerable<T> expected,IEnumerable<T> actual) {
        try {
            Assert.Equal(expected,actual);
        }
        finally {
            if(actual is IDisposable disposable) {
                disposable.Dispose();
            }
        }
    }
    private static readonly int[] Source=[1,2,3,4];
    private static readonly long[] LongSource=[1L,2L,3L,4L];
    private static readonly string[] StringSource=["a","bb","ccc"];
    private static readonly ManagedStruct[] ManagedStructSource=[new(){Name="a"},new(){Name="bb"},new(){Name="ccc"}];
    private static IEnumerable<int> ThrowingSource() => new ThrowingEnumerable();

    private sealed class ThrowingEnumerable:IEnumerable<int> {
        public IEnumerator<int> GetEnumerator() => throw new InvalidOperationException("Take(0) must not enumerate the source.");
        NonGenericEnumerator NonGenericEnumerable.GetEnumerator() => GetEnumerator();
    }

    internal static ManagedStruct CreateManagedStruct(ManagedStruct value) => new() { Name=value.Name+"!" };
}
