using SystemEnumerable = System.Linq.Enumerable;

using Test.Tsavorite.Collections;

using Tsavorite.Collections;
using Tsavorite.Stores;

using System.Linq;

namespace Test.Tsavorite.Collections.Intercept.Tsavorite.Linq;

[Collection("HashSetIntercept")]
public sealed class HashSetMethod2 {
    public class Select_Select_ToArray {
        [Fact,Trait("Category","Intercept")]
        public static void Test() {
            using var source=CreateUnmanagedSource(Source());
            Source().Select(static value => value+1).Select(static value => value*10);
        }

        public static IEnumerable<int> Selectキャリア0(IEnumerable<int>  source,Func<int,int > selector){
            return source;
        }

        struct Struct0{
            public static int selector(int input) => input+1;
        }
        struct Struct1{
            public static int selector(int input) => input*10;
        }
        public static IEnumerable<int> Selectインライン1(IEnumerable<int>  source,Func<int,int > selector) {
            var result=new global::System.Collections.Generic.List<int>();
            var e=source.GetEnumerator();
            goto Start;
            Head:
            var item0=Struct0.selector(e.Current);
            var item1=Struct1.selector(item0);
            result.Add(item1);
            Start:
            if(e.MoveNext()) goto Head;
            return result;
        }
        public static void このようなキャリアと末端インラインの見本() {
            _=Selectキャリア0(
                Selectインライン1(
                    Source(),
                    input => input+1
                ),
                input=>input*10
            );
        }
    }
    public class SelectStatic_SelectStatic_ToArray {
        [Fact,Trait("Category","Intercept")]
        public static void Test() {
            var x=0;
            _=Source().Select(value => value+x).Select(value => value*x+10);
        }
        public static (IEnumerable<int>source,Func<int,int>selector) Selectキャリア0(IEnumerable<int>  source,Func<int,int > selector){
            return (source,selector);
        }
        public static IEnumerable<int> Selectインライン1(
            (IEnumerable<int>source,Func<int,int>selector)source,
            Func<int,int > selector) {
            var result=new global::System.Collections.Generic.List<int>();
            using var e=source.source.GetEnumerator();
            goto Start;
            Head:
            var item0=source.selector(e.Current);
            var item1=selector(item0);
            result.Add(item1);
            Start:
            if(e.MoveNext()) goto Head;
            return result;
        }
        public static void このようなキャリアと末端インラインの見本() {
            _=Selectインライン1(
                Selectキャリア0(
                    Source(),
                    p => p+1
                ),
                p=>p*10
            );
        }
    }

    [Fact, Trait("Category","Intercept")]
    public static void Select_Select_index_ToArray() {
        using var source = CreateUnmanagedSource(Source());
        AssertChain(
            Source().Select(static value => value+1).Select(static (value,index) => value+index),
            source.Select(static value => value+1).Select(static (value,index) => value+index)
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void Select_Where_ToArray() {
        using var source = CreateUnmanagedSource(Source());
        AssertChain(
            Source().Select(static value => value*10).Where(static value => value>20),
            source.Select(static value => value*10).Where(static value => value>20)
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void Select_Where_index_ToArray() {
        using var source = CreateUnmanagedSource(Source());
        AssertChain(
            Source().Select(static value => value*10).Where(static (value,index) => value>20&&index>0),
            source.Select(static value => value*10).Where(static (value,index) => value>20&&index>0)
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void Select_index_Select_ToArray() {
        using var source = CreateUnmanagedSource(Source());
        AssertChain(
            Source().Select(static (value,index) => value+index).Select(static value => value*10),
            source.Select(static (value,index) => value+index).Select(static value => value*10)
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void Select_index_Select_index_ToArray() {
        using var source = CreateUnmanagedSource(Source());
        AssertChain(
            Source().Select(static (value,index) => value+index).Select(static (value,index) => value+index),
            source.Select(static (value,index) => value+index).Select(static (value,index) => value+index)
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void Select_index_Where_ToArray() {
        using var source = CreateUnmanagedSource(Source());
        AssertChain(
            Source().Select(static (value,index) => value+index).Where(static value => value>4),
            source.Select(static (value,index) => value+index).Where(static value => value>4)
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void Select_index_Where_index_ToArray() {
        using var source = CreateUnmanagedSource(Source());
        AssertChain(
            Source().Select(static (value,index) => value+index).Where(static (value,index) => value>3&&index>1),
            source.Select(static (value,index) => value+index).Where(static (value,index) => value>3&&index>1)
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void Where_Select_ToArray() {
        using var source = CreateUnmanagedSource(Source());
        AssertChain(
            Source().Where(static value => value>1).Select(static value => value*10),
            source.Where(static value => value>1).Select(static value => value*10)
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void Where_Select_index_ToArray() {
        using var source = CreateUnmanagedSource(Source());
        AssertChain(
            Source().Where(static value => value>1).Select(static (value,index) => value+index),
            source.Where(static value => value>1).Select(static (value,index) => value+index)
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void Where_Where_ToArray() {
        using var source = CreateUnmanagedSource(Source());
        AssertChain(
            Source().Where(static value => value>1).Where(static value => value<4),
            source.Where(static value => value>1).Where(static value => value<4)
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void Where_Where_index_ToArray() {
        using var source = CreateUnmanagedSource(Source());
        AssertChain(
            Source().Where(static value => value>1).Where(static (value,index) => value<4&&index<2),
            source.Where(static value => value>1).Where(static (value,index) => value<4&&index<2)
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void Where_index_Select_ToArray() {
        using var source = CreateUnmanagedSource(Source());
        AssertChain(
            Source().Where(static (value,index) => value>index).Select(static value => value*10),
            source.Where(static (value,index) => value>index).Select(static value => value*10)
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void Where_index_Select_index_ToArray() {
        using var source = CreateUnmanagedSource(Source());
        AssertChain(
            Source().Where(static (value,index) => value>index).Select(static (value,index) => value+index),
            source.Where(static (value,index) => value>index).Select(static (value,index) => value+index)
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void Where_index_Where_ToArray() {
        using var source = CreateUnmanagedSource(Source());
        AssertChain(
            Source().Where(static (value,index) => value>index).Where(static value => value<4),
            source.Where(static (value,index) => value>index).Where(static value => value<4)
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void Where_index_Where_index_ToArray() {
        using var source = CreateUnmanagedSource(Source());
        AssertChain(
            Source().Where(static (value,index) => value>index).Where(static (value,index) => value<4&&index<2),
            source.Where(static (value,index) => value>index).Where(static (value,index) => value<4&&index<2)
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void Select_Where_TypeVariants_ToArray() {
        using var longSource = CreateUnmanagedSource(LongSource());
        using var stringSource = CreateManagedSource(StringSource());
        using var managedStructSource = CreateManagedSource(ManagedStructSource());

        AssertChain(
            LongSource().Select(static value => value*10L).Where(static value => value>20L),
            longSource.Select(static value => value*10L).Where(static value => value>20L)
        );
        AssertChain(
            StringSource().Select(static value => value.ToUpperInvariant()).Where(static value => value.Length>1),
            stringSource.Select(static value => value.ToUpperInvariant()).Where(static value => value.Length>1)
        );
        AssertChain(
            ManagedStructSource().Select(static value => CreateManagedStruct(value)).Where(static value => value.Name.Length>2),
            managedStructSource.Select(static value => CreateManagedStruct(value)).Where(static value => value.Name.Length>2)
        );
    }

    private static void AssertChain<T>(IEnumerable<T> expected,IEnumerable<T> actual)
        where T : IComparable<T> {
        try {
            Assert.Equal(expected.OrderBy(static value => value),actual.OrderBy(static value => value));
        }
        finally {
            if(actual is IDisposable disposable) {
                disposable.Dispose();
            }
        }
    }

    internal static global::System.Collections.Generic.HashSet<int> Source() => [1,2,3,4];
    internal static global::System.Collections.Generic.HashSet<long> LongSource() => [1L,2L,3L,4L];
    internal static global::System.Collections.Generic.HashSet<string> StringSource() => ["a","bb","ccc"];
    internal static global::System.Collections.Generic.HashSet<ManagedStruct> ManagedStructSource() => [new() { Name="a" },new() { Name="bb" },new() { Name="ccc" }];
    internal static ManagedStruct CreateManagedStruct(ManagedStruct value) => new() { Name=value.Name+"!" };

    private static IHashSet<T> CreateUnmanagedSource<T>(IEnumerable<T> values)
        where T : unmanaged, IEquatable<T> {
        var store = HashStore.Create().Create<T>();
        var result = store.CreateOwned();
        foreach(var value in values) {
            result.Add(value);
        }
        return result;
    }

    private static IHashSet<T> CreateManagedSource<T>(IEnumerable<T> values)
        where T : IEquatable<T> {
        var store = HashStore.Create().Create<T>(new MemoryPackSerializerStrategy());
        var result = store.CreateOwned();
        foreach(var value in values) {
            result.Add(value);
        }
        return result;
    }
}
