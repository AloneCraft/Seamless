using System.Linq;

using Test.Tsavorite.Collections.Intercept.System.Linq;


namespace Test.Tsavorite.Collections.Intercept.System.Linq.Optimization;

public sealed class SelectManyFragmentBackedBoundary:OptimizationTestBase{
    [Fact,Trait("Category","Unit")]
    public static void SelectManyPrependJoinKeepsPrependedElement() {
        var expected=StringOuter().SelectMany(static x => new[] { x,x+x }).Prepend("zz").Join(StringInner(),static x => x.Length%7,static y => y.Length%7,static (x,y) => x+":"+y).ToList();
        SelectManyPrependJoinKeepsPrependedElementOptimized(expected);
        SelectManyPrependJoinKeepsPrependedElementNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void SelectManyPrependJoinKeepsPrependedElementOptimized(dynamic expected) {
        var actual=new Consumer<string>();
                StringOuter()
                    .SelectMany(static x => new[] { x,x+x })
                    .Prepend("zz")
                    .Join(
                        StringInner(),
                        static x => x.Length%7,
                        static y => y.Length%7,
                        static (x,y) => x+":"+y)
                    .Consume(actual);
        
                Assert.Contains("zz:yy",actual.Items);
                AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void SelectManyPrependJoinKeepsPrependedElementNoOptimized(dynamic expected) {
        var actual=new Consumer<string>();
                StringOuter()
                    .SelectMany(static x => new[] { x,x+x })
                    .Prepend("zz")
                    .Join(
                        StringInner(),
                        static x => x.Length%7,
                        static y => y.Length%7,
                        static (x,y) => x+":"+y)
                    .Consume(actual);
        
                Assert.Contains("zz:yy",actual.Items);
                AssertSequenceEqual(expected,actual.Items);
    }

    private static string[] StringOuter() => ["a","bb","ccc"];

    private static string[] StringInner() => ["x","yy","zzz","wwww"];
}
