using BindingFlags = System.Reflection.BindingFlags;
using SeamlessEnumerable = Seamless.Linq.Enumerable;
using SystemEnumerable = System.Linq.Enumerable;
using Test.Seamless.Collections;

namespace Test.Seamless.Intercept.Seamless.Linq;

public sealed class EnumerableMethod2 {
    [Fact, Trait("Category","Intercept")]
    public static void Select_Select_ToArray() => AssertChain(
        SystemEnumerable.Select(SystemEnumerable.Select(Source(),static value => value+1),static value => value*10),
        new ChainSamples().Select_Select(),
        typeof(global::Seamless.Collections.UnmanagedList<int>)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Select_Select_index_ToArray() => AssertChain(
        SystemEnumerable.Select(SystemEnumerable.Select(Source(),static value => value+1),static (value,index) => value+index),
        new ChainSamples().Select_Select_index(),
        typeof(global::Seamless.Collections.UnmanagedList<int>)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Select_Where_ToArray() => AssertChain(
        SystemEnumerable.Where(SystemEnumerable.Select(Source(),static value => value*10),static value => value>20),
        new ChainSamples().Select_Where(),
        typeof(global::Seamless.Collections.UnmanagedList<int>)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Select_Where_index_ToArray() => AssertChain(
        SystemEnumerable.Where(SystemEnumerable.Select(Source(),static value => value*10),static (value,index) => value>20&&index>0),
        new ChainSamples().Select_Where_index(),
        typeof(global::Seamless.Collections.UnmanagedList<int>)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Select_index_Select_ToArray() => AssertChain(
        SystemEnumerable.Select(SystemEnumerable.Select(Source(),static (value,index) => value+index),static value => value*10),
        new ChainSamples().Select_index_Select(),
        typeof(global::Seamless.Collections.UnmanagedList<int>)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Select_index_Select_index_ToArray() => AssertChain(
        SystemEnumerable.Select(SystemEnumerable.Select(Source(),static (value,index) => value+index),static (value,index) => value+index),
        new ChainSamples().Select_index_Select_index(),
        typeof(global::Seamless.Collections.UnmanagedList<int>)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Select_index_Where_ToArray() => AssertChain(
        SystemEnumerable.Where(SystemEnumerable.Select(Source(),static (value,index) => value+index),static value => value>4),
        new ChainSamples().Select_index_Where(),
        typeof(global::Seamless.Collections.UnmanagedList<int>)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Select_index_Where_index_ToArray() => AssertChain(
        SystemEnumerable.Where(SystemEnumerable.Select(Source(),static (value,index) => value+index),static (value,index) => value>3&&index>1),
        new ChainSamples().Select_index_Where_index(),
        typeof(global::Seamless.Collections.UnmanagedList<int>)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Where_Select_ToArray() => AssertChain(
        SystemEnumerable.Select(SystemEnumerable.Where(Source(),static value => value>1),static value => value*10),
        new ChainSamples().Where_Select(),
        typeof(global::Seamless.Collections.UnmanagedList<int>)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Where_Select_index_ToArray() => AssertChain(
        SystemEnumerable.Select(SystemEnumerable.Where(Source(),static value => value>1),static (value,index) => value+index),
        new ChainSamples().Where_Select_index(),
        typeof(global::Seamless.Collections.UnmanagedList<int>)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Where_Where_ToArray() => AssertChain(
        SystemEnumerable.Where(SystemEnumerable.Where(Source(),static value => value>1),static value => value<4),
        new ChainSamples().Where_Where(),
        typeof(global::Seamless.Collections.UnmanagedList<int>)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Where_Where_index_ToArray() => AssertChain(
        SystemEnumerable.Where(SystemEnumerable.Where(Source(),static value => value>1),static (value,index) => value<4&&index<2),
        new ChainSamples().Where_Where_index(),
        typeof(global::Seamless.Collections.UnmanagedList<int>)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Where_index_Select_ToArray() => AssertChain(
        SystemEnumerable.Select(SystemEnumerable.Where(Source(),static (value,index) => value>index),static value => value*10),
        new ChainSamples().Where_index_Select(),
        typeof(global::Seamless.Collections.UnmanagedList<int>)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Where_index_Select_index_ToArray() => AssertChain(
        SystemEnumerable.Select(SystemEnumerable.Where(Source(),static (value,index) => value>index),static (value,index) => value+index),
        new ChainSamples().Where_index_Select_index(),
        typeof(global::Seamless.Collections.UnmanagedList<int>)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Where_index_Where_ToArray() => AssertChain(
        SystemEnumerable.Where(SystemEnumerable.Where(Source(),static (value,index) => value>index),static value => value<4),
        new ChainSamples().Where_index_Where(),
        typeof(global::Seamless.Collections.UnmanagedList<int>)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Where_index_Where_index_ToArray() => AssertChain(
        SystemEnumerable.Where(SystemEnumerable.Where(Source(),static (value,index) => value>index),static (value,index) => value<4&&index<2),
        new ChainSamples().Where_index_Where_index(),
        typeof(global::Seamless.Collections.UnmanagedList<int>)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Select_Where_TypeVariants_ToArray() {
        AssertChain(
            SystemEnumerable.Where(SystemEnumerable.Select(LongSource(),static value => value*10L),static value => value>20L),
            new ChainSamples().Select_Where_Int64(),
            typeof(global::Seamless.Collections.UnmanagedList<long>)
        );
        AssertChain(
            SystemEnumerable.Where(SystemEnumerable.Select(StringSource(),static value => value.ToUpperInvariant()),static value => value.Length>1),
            new ChainSamples().Select_Where_String(),
            typeof(global::Seamless.Collections.ManagedList<string>)
        );
        AssertChain(
            SystemEnumerable.Where(SystemEnumerable.Select(ManagedStructSource(),static value => CreateManagedStruct(value)),static value => value.Name.Length>2),
            new ChainSamples().Select_Where_ManagedStruct(),
            typeof(global::Seamless.Collections.ManagedList<ManagedStruct>)
        );
    }

    private static void AssertChain<T>(IEnumerable<T> expected,IEnumerable<T> actual,Type expectedStorageType) {
        try {
            AssertStorageType(actual,expectedStorageType);
            Assert.Equal(expected,actual);
        }
        finally {
            if(actual is IDisposable disposable) {
                disposable.Dispose();
            }
        }
    }

    private static void AssertStorageType<T>(IEnumerable<T> actual,Type expectedStorageType) {
        var field = actual.GetType().GetField("list",BindingFlags.Instance|BindingFlags.NonPublic);
        Assert.NotNull(field);
        var list = field.GetValue(actual);
        Assert.NotNull(list);
        Assert.Equal(expectedStorageType,list.GetType());
    }

    internal sealed class ChainSamples {
        public IEnumerable<int> Select_Select() =>
            SeamlessEnumerable.Select(SeamlessEnumerable.Select(global::Test.Seamless.Intercept.Seamless.Linq.EnumerableMethod2.Source(),static value => value+1),static value => value*10);

        public IEnumerable<int> Select_Select_index() =>
            SeamlessEnumerable.Select(SeamlessEnumerable.Select(global::Test.Seamless.Intercept.Seamless.Linq.EnumerableMethod2.Source(),static value => value+1),static (value,index) => value+index);

        public IEnumerable<int> Select_Where() =>
            SeamlessEnumerable.Where(SeamlessEnumerable.Select(global::Test.Seamless.Intercept.Seamless.Linq.EnumerableMethod2.Source(),static value => value*10),static value => value>20);

        public IEnumerable<int> Select_Where_index() =>
            SeamlessEnumerable.Where(SeamlessEnumerable.Select(global::Test.Seamless.Intercept.Seamless.Linq.EnumerableMethod2.Source(),static value => value*10),static (value,index) => value>20&&index>0);

        public IEnumerable<int> Select_index_Select() =>
            SeamlessEnumerable.Select(SeamlessEnumerable.Select(global::Test.Seamless.Intercept.Seamless.Linq.EnumerableMethod2.Source(),static (value,index) => value+index),static value => value*10);

        public IEnumerable<int> Select_index_Select_index() =>
            SeamlessEnumerable.Select(SeamlessEnumerable.Select(global::Test.Seamless.Intercept.Seamless.Linq.EnumerableMethod2.Source(),static (value,index) => value+index),static (value,index) => value+index);

        public IEnumerable<int> Select_index_Where() =>
            SeamlessEnumerable.Where(SeamlessEnumerable.Select(global::Test.Seamless.Intercept.Seamless.Linq.EnumerableMethod2.Source(),static (value,index) => value+index),static value => value>4);

        public IEnumerable<int> Select_index_Where_index() =>
            SeamlessEnumerable.Where(SeamlessEnumerable.Select(global::Test.Seamless.Intercept.Seamless.Linq.EnumerableMethod2.Source(),static (value,index) => value+index),static (value,index) => value>3&&index>1);

        public IEnumerable<int> Where_Select() =>
            SeamlessEnumerable.Select(SeamlessEnumerable.Where(global::Test.Seamless.Intercept.Seamless.Linq.EnumerableMethod2.Source(),static value => value>1),static value => value*10);

        public IEnumerable<int> Where_Select_index() =>
            SeamlessEnumerable.Select(SeamlessEnumerable.Where(global::Test.Seamless.Intercept.Seamless.Linq.EnumerableMethod2.Source(),static value => value>1),static (value,index) => value+index);

        public IEnumerable<int> Where_Where() =>
            SeamlessEnumerable.Where(SeamlessEnumerable.Where(global::Test.Seamless.Intercept.Seamless.Linq.EnumerableMethod2.Source(),static value => value>1),static value => value<4);

        public IEnumerable<int> Where_Where_index() =>
            SeamlessEnumerable.Where(SeamlessEnumerable.Where(global::Test.Seamless.Intercept.Seamless.Linq.EnumerableMethod2.Source(),static value => value>1),static (value,index) => value<4&&index<2);

        public IEnumerable<int> Where_index_Select() =>
            SeamlessEnumerable.Select(SeamlessEnumerable.Where(global::Test.Seamless.Intercept.Seamless.Linq.EnumerableMethod2.Source(),static (value,index) => value>index),static value => value*10);

        public IEnumerable<int> Where_index_Select_index() =>
            SeamlessEnumerable.Select(SeamlessEnumerable.Where(global::Test.Seamless.Intercept.Seamless.Linq.EnumerableMethod2.Source(),static (value,index) => value>index),static (value,index) => value+index);

        public IEnumerable<int> Where_index_Where() =>
            SeamlessEnumerable.Where(SeamlessEnumerable.Where(global::Test.Seamless.Intercept.Seamless.Linq.EnumerableMethod2.Source(),static (value,index) => value>index),static value => value<4);

        public IEnumerable<int> Where_index_Where_index() =>
            SeamlessEnumerable.Where(SeamlessEnumerable.Where(global::Test.Seamless.Intercept.Seamless.Linq.EnumerableMethod2.Source(),static (value,index) => value>index),static (value,index) => value<4&&index<2);

        public IEnumerable<long> Select_Where_Int64() =>
            SeamlessEnumerable.Where(SeamlessEnumerable.Select(global::Test.Seamless.Intercept.Seamless.Linq.EnumerableMethod2.LongSource(),static value => value*10L),static value => value>20L);

        public IEnumerable<string> Select_Where_String() =>
            SeamlessEnumerable.Where(SeamlessEnumerable.Select(global::Test.Seamless.Intercept.Seamless.Linq.EnumerableMethod2.StringSource(),static value => value.ToUpperInvariant()),static value => value.Length>1);

        public IEnumerable<ManagedStruct> Select_Where_ManagedStruct() =>
            SeamlessEnumerable.Where(SeamlessEnumerable.Select(global::Test.Seamless.Intercept.Seamless.Linq.EnumerableMethod2.ManagedStructSource(),static value => global::Test.Seamless.Intercept.Seamless.Linq.EnumerableMethod2.CreateManagedStruct(value)),static value => value.Name.Length>2);
    }

    internal static int[] Source() => [1,2,3,4];
    internal static long[] LongSource() => [1L,2L,3L,4L];
    internal static string[] StringSource() => ["a","bb","ccc"];
    internal static ManagedStruct[] ManagedStructSource() => [new() { Name="a" },new() { Name="bb" },new() { Name="ccc" }];
    internal static ManagedStruct CreateManagedStruct(ManagedStruct value) => new() { Name=value.Name+"!" };
}
