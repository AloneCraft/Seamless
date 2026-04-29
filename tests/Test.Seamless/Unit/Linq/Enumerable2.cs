#if TSAVORITE1_1_1
using Test.Seamless.Benchmarks.Simple;
using SampleContainer = Test.Seamless.Benchmarks.Simple.Container;
using SampleOptimizedContainer = Test.Seamless.Benchmarks.Simple.OptimizedContainer;
#elif TSAVORITE2_0BETA3
using Test.Seamless.Benchmarks.Simple2;
using SampleContainer = Test.Seamless.Benchmarks.Simple2.SimpleDatabase;
using SampleOptimizedContainer = Test.Seamless.Benchmarks.Simple2.OptimizedSimpleDatabase;
#endif

#if TSAVORITE1_1_1 || TSAVORITE2_0BETA3

namespace Test.Seamless.Unit.Linq;

public sealed class Enumerable2 {
    [Fact, Trait("Category","Unit")]
    public void QueryOverridesMatchOriginalMethods() {
        var container = new SampleContainer();
        var optimized = new SampleOptimizedContainer();
        var outer = new[] { 1,2,3 };
        var inner = new[] { "a","b" };

        Assert.Equal(
            container.SelectManyQuery(outer,inner),
            optimized.SelectManyQuery(outer,inner));
        Assert.Equal(
            container.WhereSelectQuery(outer,2),
            optimized.WhereSelectQuery(outer,2));
    }

    [Fact, Trait("Category","Unit")]
    public void MethodChainOverridesMatchOriginalMethods() {
        var container = new SampleContainer();
        var optimized = new SampleOptimizedContainer();
        var outer = new[] { 1,2,3,2 };
        var inner = new[] { 10,20 };

        Assert.Equal(
            container.SelectManyMethod(outer,inner),
            optimized.SelectManyMethod(outer,inner));
        Assert.Equal(
            container.WhereSelectMethod(outer,2),
            optimized.WhereSelectMethod(outer,2));
        Assert.Equal(
            container.AggregateMethod(outer),
            optimized.AggregateMethod(outer));
        Assert.Equal(
            container.WhereAfterAnonymousSelect(20),
            optimized.WhereAfterAnonymousSelect(20));
        Assert.Equal(
            container.WhereAfterTupleSelect(20),
            optimized.WhereAfterTupleSelect(20));
        Assert.Equal(
            container.WhereAfterJoin(20),
            optimized.WhereAfterJoin(20));
        Assert.Equal(
            container.WhereAfterExcept(20),
            optimized.WhereAfterExcept(20));
    }
}
#endif
