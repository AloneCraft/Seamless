using Seamless.Stores;
using HashSetSample = Test.Seamless.Intersept.Seamless.Linq.HashSetSample.HashSet;
using SeamlessEnumerableSample = Test.Seamless.Intersept.Seamless.Linq.Enumerable;
using SystemEnumerableSample = Test.Seamless.Intersept.System.Linq.Enumerable;

namespace Test.Seamless.Unit.Linq;

public sealed class InterceptorSamples {
    [Fact, Trait("Category","Unit")]
    public void System_Enumerable_Smoke() {
        var container = new SystemEnumerableSample();

        Assert.True(container.Smoke()>0);
    }

    [Fact, Trait("Category","Unit")]
    public void Seamless_Enumerable_Smoke() {
        var container = new SeamlessEnumerableSample();

        Assert.True(container.Smoke()>0);
    }

    [Fact, Trait("Category","Unit")]
    public void Seamless_HashSet_Smoke() {
        using var sourceStore = HashStore.Create().CreateManagedHashSet<int>();
        using var secondStore = HashStore.Create().CreateManagedHashSet<int>();
        using var singleStore = HashStore.Create().CreateManagedHashSet<int>();
        using var emptyStore = HashStore.Create().CreateManagedHashSet<int>();
        using var source = sourceStore.Create();
        using var second = secondStore.Create();
        using var single = singleStore.Create();
        using var empty = emptyStore.Create();
        AddRange(source,new[] { 1,2,3,4 });
        AddRange(second,new[] { 2,3,5 });
        single.Add(2);
        var container = new HashSetSample();

        Assert.True(container.Smoke(source,second,single,empty)>0);
    }

    private static void AddRange(global::Seamless.Collections.IHashSet<int> set,IEnumerable<int> values) {
        foreach(var value in values) {
            set.Add(value);
        }
    }
}
