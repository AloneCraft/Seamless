using System.Linq;

using Test.System.Linq.Intercept;


namespace Test.System.Linq.Intercept.Optimization;

public sealed class SelectManyFragmentBackedBoundary:OptimizationTestBase{
    [Fact,Trait("Category","Unit")]
    public static void SelectManyPrependJoinKeepsPrependedElement() {
        var expected=StringOuter().SelectMany(static x => new[] { x,x+x }).Prepend("zz").Join(StringInner(),static x => x.Length%7,static y => y.Length%7,static (x,y) => x+":"+y).ToList();
        SelectManyPrependJoinKeepsPrependedElementOptimized(expected);
        SelectManyPrependJoinKeepsPrependedElementNoOptimized(expected);
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
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

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
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
