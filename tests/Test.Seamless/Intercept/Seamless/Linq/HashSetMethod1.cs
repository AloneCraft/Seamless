using System.Collections.Generic;
using System.Linq;
using Seamless.Stores;
using e = System.Linq.Enumerable;
using a= Seamless.Linq.HashSet;
namespace Test.Seamless.Intercept.Seamless.Linq.HashSetSample;
public sealed class HashSetMethod1 {
    [Fact, Trait("Category","Intercept")]private static void Aggregate_func()=>Assert.Equal(
        e.Aggregate(eSource(),static (left,right) => left+right),
        a.Aggregate(aSource(),static (left,right) => left+right)
    );
    [Fact, Trait("Category","Intercept")]private static void Aggregate_seed_func()=>Assert.Equal(
        e.Aggregate(eSource(),10,static (sum,value) => sum+value),
        a.Aggregate(aSource(),10,static (sum,value) => sum+value)
    );
    [Fact, Trait("Category","Intercept")]private static void Aggregate_seed_func_resultSelector()=>Assert.Equal(
        e.Aggregate(eSource(),10,static (sum,value) => sum+value,static sum => sum*2),
        a.Aggregate(aSource(),10,static (sum,value) => sum+value,static sum => sum*2)
    );
    [Fact,Trait("Category","Intercept")]private static void Aggregate_seed_func_resultSelector3(){
        eSource().Aggregate(3,(a,p)=>p+1);
        eSource().Select(p=>p+1);
    }
    private static global::System.Collections.Generic.HashSet<int>eSource() => [1,2,3,4];
    private static global::Seamless.Collections.IHashSet<int> aSource(){
        var r=HashStore.Create().Create<int>().Create();
        for(var a=1;a<=4;a++){
            r.Add(a);
        }
        return r;
    }
    private static HashSet<int> Second() => [3,4,5];
    private static HashSet<int> Third() => [10,20,30];
    private static HashSet<int> Duplicates() => [1,2,2,3,3,4];
    private static HashSet<int> SingleSource() => [2];
    private static global::System.Collections.IEnumerable CastObjects() => new HashSet<int>{ 1,2,3 };
    private static HashSet<object> MixedObjects() => [1,"skip",2,3.0,4];
    private static HashSet<Row>Rows() => [new(1,10,"beta"),new(2,20,"alpha"),new(3,30,"gamma")];
    private static HashSet<Row>OtherRows() => [new(3,300,"gamma-2"),new(4,40,"delta")];
    private sealed record Row(int Key,int Value,string Name);
}
