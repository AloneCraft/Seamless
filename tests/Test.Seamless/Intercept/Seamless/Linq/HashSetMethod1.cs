using Seamless.Stores;
using e = System.Linq.Enumerable;
using a = Seamless.Linq.HashSet;
namespace Test.Seamless.Intercept.Seamless.Linq.HashSetSample;

public sealed class HashSetMethod1 {
    [Fact, Trait("Category","Intercept")]
    private static void Aggregate_func() => Assert.Equal(
        e.Aggregate(eSource(),static (left,right) => left+right),
        a.Aggregate(aSource(),static (left,right) => left+right)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Aggregate_seed_func() => Assert.Equal(
        e.Aggregate(eSource(),10,static (sum,value) => sum+value),
        a.Aggregate(aSource(),10,static (sum,value) => sum+value)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Aggregate_seed_func_resultSelector() => Assert.Equal(
        e.Aggregate(eSource(),10,static (sum,value) => sum+value,static sum => sum*2),
        a.Aggregate(aSource(),10,static (sum,value) => sum+value,static sum => sum*2)
    );
    private static global::System.Collections.Generic.HashSet<int> eSource() => [1,2,3,4];
    private static global::Seamless.Collections.IHashSet<int> aSource() {
        var r = HashStore.Create().Create<int>().Create();
        for(var a = 1; a<=4; a++) {
            r.Add(a);
        }
        return r;
    }
}
