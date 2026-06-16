using System.Linq;

using Tsavorite.Linq;
namespace Test.Tsavorite.Collections.Intercept.Tsavorite.Linq;

[Collection("HashSetIntercept")]
public sealed class HashSetScan {
    [Fact, Trait("Category","Intercept")]
    public static void Count_Where_Select() {
        Assert.Equal(2,CountWhereSelectOptimized());
        Assert.Equal(2,CountWhereSelectNoOptimized());
    }

    [Fact, Trait("Category","Intercept")]
    public static void Any_Select_Where() {
        Assert.True(AnySelectWhereOptimized());
        Assert.True(AnySelectWhereNoOptimized());
    }

    [Fact, Trait("Category","Intercept")]
    public static void First_Where() {
        Assert.Equal(3,FirstWhereOptimized());
        Assert.Equal(3,FirstWhereNoOptimized());
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static int CountWhereSelectOptimized() {
        using var source = global::Tsavorite.Stores.HashStore.Create().Create<int>().CreateOwned();
        source.Add(1);
        source.Add(2);
        source.Add(3);
        source.Add(4);
        return source.Where(static value => value>1)
            .Select(static value => value*10)
            .Count(static value => value>20);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static int CountWhereSelectNoOptimized() {
        using var source = global::Tsavorite.Stores.HashStore.Create().Create<int>().CreateOwned();
        source.Add(1);
        source.Add(2);
        source.Add(3);
        source.Add(4);
        return source.Where(static value => value>1)
            .Select(static value => value*10)
            .Count(static value => value>20);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static bool AnySelectWhereOptimized() {
        using var source = global::Tsavorite.Stores.HashStore.Create().Create<int>().CreateOwned();
        source.Add(1);
        source.Add(2);
        source.Add(3);
        source.Add(4);
        return source.Select(static value => value*2)
            .Any(static value => value==8);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static bool AnySelectWhereNoOptimized() {
        using var source = global::Tsavorite.Stores.HashStore.Create().Create<int>().CreateOwned();
        source.Add(1);
        source.Add(2);
        source.Add(3);
        source.Add(4);
        return source.Select(static value => value*2)
            .Any(static value => value==8);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static int FirstWhereOptimized() {
        using var source = global::Tsavorite.Stores.HashStore.Create().Create<int>().CreateOwned();
        source.Add(1);
        source.Add(2);
        source.Add(3);
        source.Add(4);
        return source.Where(static value => value==3).First();
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static int FirstWhereNoOptimized() {
        using var source = global::Tsavorite.Stores.HashStore.Create().Create<int>().CreateOwned();
        source.Add(1);
        source.Add(2);
        source.Add(3);
        source.Add(4);
        return source.Where(static value => value==3).First();
    }
}
