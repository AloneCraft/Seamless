using System.Linq;

using Test.Tsavorite.Collections;

using Tsavorite.Collections;
using Tsavorite.Linq;
using Tsavorite.Stores;

namespace Test.Tsavorite.Linq.Intercept;

[Collection("HashSetIntercept")]
public sealed class HashSetMethod2 {
    public class Select_Select_ToArray {
        [Fact,Trait("Category","Intercept")]
        public static void Test() => AssertChain(
            Source().Select(static value => value+1).Select(static value => value*10),
            Select_Select_ToArrayOptimized,
            Select_Select_ToArrayNoOptimized
        );
    }

    public class SelectStatic_SelectStatic_ToArray {
        [Fact,Trait("Category","Intercept")]
        public static void Test() {
            var x=0;
            AssertChain(
                Source().Select(value => value+x).Select(value => value*x+10),
                SelectStatic_SelectStatic_ToArrayOptimized,
                SelectStatic_SelectStatic_ToArrayNoOptimized
            );
        }
    }

    [Fact, Trait("Category","Intercept")]
    public static void Select_Select_index_ToArray() => AssertChain(
        Source().Select(static value => value+1).Select(static (value,index) => value+index),
        Select_Select_index_ToArrayOptimized,
        Select_Select_index_ToArrayNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void Select_Where_ToArray() => AssertChain(
        Source().Select(static value => value*10).Where(static value => value>20),
        Select_Where_ToArrayOptimized,
        Select_Where_ToArrayNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void Select_Where_index_ToArray() => AssertChain(
        Source().Select(static value => value*10).Where(static (value,index) => value>20&&index>0),
        Select_Where_index_ToArrayOptimized,
        Select_Where_index_ToArrayNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void Select_index_Select_ToArray() => AssertChain(
        Source().Select(static (value,index) => value+index).Select(static value => value*10),
        Select_index_Select_ToArrayOptimized,
        Select_index_Select_ToArrayNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void Select_index_Select_index_ToArray() => AssertChain(
        Source().Select(static (value,index) => value+index).Select(static (value,index) => value+index),
        Select_index_Select_index_ToArrayOptimized,
        Select_index_Select_index_ToArrayNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void Select_index_Where_ToArray() => AssertChain(
        Source().Select(static (value,index) => value+index).Where(static value => value>4),
        Select_index_Where_ToArrayOptimized,
        Select_index_Where_ToArrayNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void Select_index_Where_index_ToArray() => AssertChain(
        Source().Select(static (value,index) => value+index).Where(static (value,index) => value>3&&index>1),
        Select_index_Where_index_ToArrayOptimized,
        Select_index_Where_index_ToArrayNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void Where_Select_ToArray() => AssertChain(
        Source().Where(static value => value>1).Select(static value => value*10),
        Where_Select_ToArrayOptimized,
        Where_Select_ToArrayNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void Where_Select_index_ToArray() => AssertChain(
        Source().Where(static value => value>1).Select(static (value,index) => value+index),
        Where_Select_index_ToArrayOptimized,
        Where_Select_index_ToArrayNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void Where_Where_ToArray() => AssertChain(
        Source().Where(static value => value>1).Where(static value => value<4),
        Where_Where_ToArrayOptimized,
        Where_Where_ToArrayNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void Where_Where_index_ToArray() => AssertChain(
        Source().Where(static value => value>1).Where(static (value,index) => value<4&&index<2),
        Where_Where_index_ToArrayOptimized,
        Where_Where_index_ToArrayNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void Where_index_Select_ToArray() => AssertChain(
        Source().Where(static (value,index) => value>index).Select(static value => value*10),
        Where_index_Select_ToArrayOptimized,
        Where_index_Select_ToArrayNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void Where_index_Select_index_ToArray() => AssertChain(
        Source().Where(static (value,index) => value>index).Select(static (value,index) => value+index),
        Where_index_Select_index_ToArrayOptimized,
        Where_index_Select_index_ToArrayNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void Where_index_Where_ToArray() => AssertChain(
        Source().Where(static (value,index) => value>index).Where(static value => value<4),
        Where_index_Where_ToArrayOptimized,
        Where_index_Where_ToArrayNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void Where_index_Where_index_ToArray() => AssertChain(
        Source().Where(static (value,index) => value>index).Where(static (value,index) => value<4&&index<2),
        Where_index_Where_index_ToArrayOptimized,
        Where_index_Where_index_ToArrayNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void Select_Where_TypeVariants_ToArray() {
        AssertChain(
            LongSource().Select(static value => value*10L).Where(static value => value>20L),
            Select_Where_Long_ToArrayOptimized,
            Select_Where_Long_ToArrayNoOptimized
        );
        AssertChain(
            StringSource().Select(static value => value.ToUpperInvariant()).Where(static value => value.Length>1),
            Select_Where_String_ToArrayOptimized,
            Select_Where_String_ToArrayNoOptimized
        );
        AssertChain(
            ManagedStructSource().Select(static value => CreateManagedStruct(value)).Where(static value => value.Name.Length>2),
            Select_Where_ManagedStruct_ToArrayOptimized,
            Select_Where_ManagedStruct_ToArrayNoOptimized
        );
    }

    private static void AssertChain<T>(IEnumerable<T> expected,Func<IEnumerable<T>> optimized,Func<IEnumerable<T>> noOptimized)
        where T : IComparable<T> {
        var expectedArray=expected.OrderBy(static value => value).ToArray();
        Assert.Equal(expectedArray,optimized().OrderBy(static value => value));
        Assert.Equal(expectedArray,noOptimized().OrderBy(static value => value));
    }


    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> Select_Select_ToArrayOptimized() {
            using var source=CreateUnmanagedSource(Source());
            return source.Select(static value => value+1).Select(static value => value*10).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> SelectStatic_SelectStatic_ToArrayOptimized() {
            var x=0;
            using var source=CreateUnmanagedSource(Source());
            return source.Select(value => value+x).Select(value => value*x+10).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> Select_Select_index_ToArrayOptimized() {
            using var source = CreateUnmanagedSource(Source());
            return source.Select(static value => value+1).Select(static (value,index) => value+index).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> Select_Where_ToArrayOptimized() {
            using var source = CreateUnmanagedSource(Source());
            return source.Select(static value => value*10).Where(static value => value>20).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> Select_Where_index_ToArrayOptimized() {
            using var source = CreateUnmanagedSource(Source());
            return source.Select(static value => value*10).Where(static (value,index) => value>20&&index>0).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> Select_index_Select_ToArrayOptimized() {
            using var source = CreateUnmanagedSource(Source());
            return source.Select(static (value,index) => value+index).Select(static value => value*10).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> Select_index_Select_index_ToArrayOptimized() {
            using var source = CreateUnmanagedSource(Source());
            return source.Select(static (value,index) => value+index).Select(static (value,index) => value+index).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> Select_index_Where_ToArrayOptimized() {
            using var source = CreateUnmanagedSource(Source());
            return source.Select(static (value,index) => value+index).Where(static value => value>4).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> Select_index_Where_index_ToArrayOptimized() {
            using var source = CreateUnmanagedSource(Source());
            return source.Select(static (value,index) => value+index).Where(static (value,index) => value>3&&index>1).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> Where_Select_ToArrayOptimized() {
            using var source = CreateUnmanagedSource(Source());
            return source.Where(static value => value>1).Select(static value => value*10).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> Where_Select_index_ToArrayOptimized() {
            using var source = CreateUnmanagedSource(Source());
            return source.Where(static value => value>1).Select(static (value,index) => value+index).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> Where_Where_ToArrayOptimized() {
            using var source = CreateUnmanagedSource(Source());
            return source.Where(static value => value>1).Where(static value => value<4).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> Where_Where_index_ToArrayOptimized() {
            using var source = CreateUnmanagedSource(Source());
            return source.Where(static value => value>1).Where(static (value,index) => value<4&&index<2).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> Where_index_Select_ToArrayOptimized() {
            using var source = CreateUnmanagedSource(Source());
            return source.Where(static (value,index) => value>index).Select(static value => value*10).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> Where_index_Select_index_ToArrayOptimized() {
            using var source = CreateUnmanagedSource(Source());
            return source.Where(static (value,index) => value>index).Select(static (value,index) => value+index).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> Where_index_Where_ToArrayOptimized() {
            using var source = CreateUnmanagedSource(Source());
            return source.Where(static (value,index) => value>index).Where(static value => value<4).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> Where_index_Where_index_ToArrayOptimized() {
            using var source = CreateUnmanagedSource(Source());
            return source.Where(static (value,index) => value>index).Where(static (value,index) => value<4&&index<2).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<long> Select_Where_Long_ToArrayOptimized() {
            using var source = CreateUnmanagedSource(LongSource());
            return source.Select(static value => value*10L).Where(static value => value>20L).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<string> Select_Where_String_ToArrayOptimized() {
            using var source = CreateManagedSource(StringSource());
            return source.Select(static value => value.ToUpperInvariant()).Where(static value => value.Length>1).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<ManagedStruct> Select_Where_ManagedStruct_ToArrayOptimized() {
            using var source = CreateManagedSource(ManagedStructSource());
            return source.Select(static value => CreateManagedStruct(value)).Where(static value => value.Name.Length>2).ToArray();
        }


    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> Select_Select_ToArrayNoOptimized() {
            using var source=CreateUnmanagedSource(Source());
            return source.Select(static value => value+1).Select(static value => value*10).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> SelectStatic_SelectStatic_ToArrayNoOptimized() {
            var x=0;
            using var source=CreateUnmanagedSource(Source());
            return source.Select(value => value+x).Select(value => value*x+10).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> Select_Select_index_ToArrayNoOptimized() {
            using var source = CreateUnmanagedSource(Source());
            return source.Select(static value => value+1).Select(static (value,index) => value+index).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> Select_Where_ToArrayNoOptimized() {
            using var source = CreateUnmanagedSource(Source());
            return source.Select(static value => value*10).Where(static value => value>20).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> Select_Where_index_ToArrayNoOptimized() {
            using var source = CreateUnmanagedSource(Source());
            return source.Select(static value => value*10).Where(static (value,index) => value>20&&index>0).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> Select_index_Select_ToArrayNoOptimized() {
            using var source = CreateUnmanagedSource(Source());
            return source.Select(static (value,index) => value+index).Select(static value => value*10).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> Select_index_Select_index_ToArrayNoOptimized() {
            using var source = CreateUnmanagedSource(Source());
            return source.Select(static (value,index) => value+index).Select(static (value,index) => value+index).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> Select_index_Where_ToArrayNoOptimized() {
            using var source = CreateUnmanagedSource(Source());
            return source.Select(static (value,index) => value+index).Where(static value => value>4).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> Select_index_Where_index_ToArrayNoOptimized() {
            using var source = CreateUnmanagedSource(Source());
            return source.Select(static (value,index) => value+index).Where(static (value,index) => value>3&&index>1).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> Where_Select_ToArrayNoOptimized() {
            using var source = CreateUnmanagedSource(Source());
            return source.Where(static value => value>1).Select(static value => value*10).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> Where_Select_index_ToArrayNoOptimized() {
            using var source = CreateUnmanagedSource(Source());
            return source.Where(static value => value>1).Select(static (value,index) => value+index).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> Where_Where_ToArrayNoOptimized() {
            using var source = CreateUnmanagedSource(Source());
            return source.Where(static value => value>1).Where(static value => value<4).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> Where_Where_index_ToArrayNoOptimized() {
            using var source = CreateUnmanagedSource(Source());
            return source.Where(static value => value>1).Where(static (value,index) => value<4&&index<2).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> Where_index_Select_ToArrayNoOptimized() {
            using var source = CreateUnmanagedSource(Source());
            return source.Where(static (value,index) => value>index).Select(static value => value*10).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> Where_index_Select_index_ToArrayNoOptimized() {
            using var source = CreateUnmanagedSource(Source());
            return source.Where(static (value,index) => value>index).Select(static (value,index) => value+index).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> Where_index_Where_ToArrayNoOptimized() {
            using var source = CreateUnmanagedSource(Source());
            return source.Where(static (value,index) => value>index).Where(static value => value<4).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> Where_index_Where_index_ToArrayNoOptimized() {
            using var source = CreateUnmanagedSource(Source());
            return source.Where(static (value,index) => value>index).Where(static (value,index) => value<4&&index<2).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<long> Select_Where_Long_ToArrayNoOptimized() {
            using var source = CreateUnmanagedSource(LongSource());
            return source.Select(static value => value*10L).Where(static value => value>20L).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<string> Select_Where_String_ToArrayNoOptimized() {
            using var source = CreateManagedSource(StringSource());
            return source.Select(static value => value.ToUpperInvariant()).Where(static value => value.Length>1).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<ManagedStruct> Select_Where_ManagedStruct_ToArrayNoOptimized() {
            using var source = CreateManagedSource(ManagedStructSource());
            return source.Select(static value => CreateManagedStruct(value)).Where(static value => value.Name.Length>2).ToArray();
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
