using System.Linq;
using NonGenericEnumerable = System.Collections.IEnumerable;

namespace Test.Seamless.Intercept.System.Linq;

public sealed class LinqChain1_LinqChain1 {
    [Fact, Trait("Category","Unit")]
    public static void Select_InnerEnumerableMethods() {
        Select_InnerEnumerableMethodsImpl0();
        Select_InnerEnumerableMethodsImpl();
    }

    private static void Select_InnerEnumerableMethodsImpl0() {
        var actual = Source().Select(p => Source().Aggregate((l,r) => l+r)).ToArray();
        Assert.Equal([10, 10, 10, 10], actual);
    }

    private static void Select_InnerEnumerableMethodsImpl(){
#pragma warning disable CS0219
        var r00 = Source().Select(p=>Source().Aggregate((l,r)=>l+r)).ToArray();
        var r01 = Source().Select(p=>Source().Aggregate((l,r)=>l+r+p)).ToArray();
        var r02 = Source().Select(p=>Source().Aggregate(0,(sum,q)=>sum+q)).ToArray();
        var r03 = Source().Select(p=>Source().Aggregate(0,(sum,q)=>sum+q+p)).ToArray();
        var r04 = Source().Select(p=>Source().Aggregate(0,(sum,q)=>sum+q,sum=>sum*2)).ToArray();
        var r05 = Source().Select(p=>Source().Aggregate(0,(sum,q)=>sum+q+p,sum=>sum+p)).ToArray();
        var r06 = Source().Select(p=>Source().AggregateBy(q=>q%2,0,(sum,q)=>sum+q)).ToArray();
        var r07 = Source().Select(p=>Source().AggregateBy(q=>(q+p)%2,0,(sum,q)=>sum+q+p)).ToArray();
        var r08 = Source().Select(p=>Source().All(q=>q>0)).ToArray();
        var r09 = Source().Select(p=>Source().All(q=>q>=p)).ToArray();
        var r10 = Source().Select(p=>Source().Any()).ToArray();
        var r11 = Source().Select(p=>Source().Any(q=>q>0)).ToArray();
        var r12 = Source().Select(p=>Source().Any(q=>q==p)).ToArray();
        var r13 = Source().Select(p=>Source().Append(5)).ToArray();
        var r14 = Source().Select(p=>Source().Append(p)).ToArray();
        var r15 = Source().Select(p=>Source().AsEnumerable()).ToArray();
        var r16 = Source().Select(p=>Source().Average()).ToArray();
        var r17 = Source().Select(p=>Rows().Average(row=>row.Value)).ToArray();
        var r18 = Source().Select(p=>Rows().Average(row=>row.Value+p)).ToArray();
        var r19 = Source().Select(p=>CastObjects().Cast<int>()).ToArray();
        var r20 = Source().Select(p=>Source().Chunk(2)).ToArray();
        var r21 = Source().Select(p=>Source().Chunk(p)).ToArray();
        var r22 = Source().Select(p=>Source().Concat(Second())).ToArray();
        var r23 = Source().Select(p=>Source().Contains(3)).ToArray();
        var r24 = Source().Select(p=>Source().Contains(p)).ToArray();
        var r25 = Source().Select(p=>Source().Count()).ToArray();
        var r26 = Source().Select(p=>Source().Count(q=>q>2)).ToArray();
        var r27 = Source().Select(p=>Source().Count(q=>q>=p)).ToArray();
        var r28 = Source().Select(p=>Duplicates().Distinct()).ToArray();
        var r29 = Source().Select(p=>Rows().DistinctBy(row=>row.Key)).ToArray();
        var r30 = Source().Select(p=>Rows().DistinctBy(row=>row.Key+p)).ToArray();
        var r31 = Source().Select(p=>Source().First()).ToArray();
        var r32 = Source().Select(p=>Source().First(q=>q>2)).ToArray();
        var r33 = Source().Select(p=>Source().First(q=>q>=p)).ToArray();
        var r34 = Source().Select(p=>Rows().GroupBy(row=>row.Key)).ToArray();
        var r35 = Source().Select(p=>Rows().GroupBy(row=>row.Key+p)).ToArray();
        var r36 = Source().Select(p=>Source().Join(Second(),outerValue=>outerValue,innerValue=>innerValue,(outerValue,innerValue)=>outerValue+innerValue)).ToArray();
        var r37 = Source().Select(p=>Source().Join(Second(),outerValue=>outerValue+p,innerValue=>innerValue+p,(outerValue,innerValue)=>outerValue+innerValue+p)).ToArray();
        var r38 = Source().Select(p=>Rows().Max(row=>row.Value)).ToArray();
        var r39 = Source().Select(p=>Rows().Max(row=>row.Value+p)).ToArray();
        var r40 = Source().Select(p=>CastObjects().OfType<int>()).ToArray();
        var r41 = Source().Select(p=>Rows().OrderBy(row=>row.Key)).ToArray();
        var r42 = Source().Select(p=>Rows().OrderBy(row=>row.Key+p)).ToArray();
        var r43 = Source().Select(p=>Source().Select(q=>q+q)).ToArray();
        var r44 = Source().Select(p=>Source().Select(q=>p+q)).ToArray();
        var r45 = Source().Select(p=>Source().Select((q,index)=>q+index)).ToArray();
        var r46 = Source().Select(p=>Source().Select((q,index)=>p+q+index)).ToArray();
        var r47 = Source().Select(p=>Source().SelectMany(q=>new[] { q,q+10 })).ToArray();
        var r48 = Source().Select(p=>Source().SelectMany(q=>new[] { p,q })).ToArray();
        var r49 = Source().Select(p=>Source().SelectMany((q,index)=>new[] { q,index })).ToArray();
        var r50 = Source().Select(p=>Source().SelectMany((q,index)=>new[] { p+q,index })).ToArray();
        var r51 = Source().Select(p=>Source().SelectMany(q=>new[] { q,q+10 },(q,sub)=>q+sub)).ToArray();
        var r52 = Source().Select(p=>Source().SelectMany(q=>new[] { p,q },(q,sub)=>p+q+sub)).ToArray();
        var r53 = Source().Select(p=>Source().SelectMany((q,index)=>new[] { q,index },(q,sub)=>q+sub)).ToArray();
        var r54 = Source().Select(p=>Source().SelectMany((q,index)=>new[] { p+q,index },(q,sub)=>p+q+sub)).ToArray();
        var r55 = Source().Select(p=>Source().SkipWhile(q=>q<3)).ToArray();
        var r56 = Source().Select(p=>Source().SkipWhile(q=>q<p)).ToArray();
        var r57 = Source().Select(p=>Rows().Sum(row=>row.Value)).ToArray();
        var r58 = Source().Select(p=>Rows().Sum(row=>row.Value+p)).ToArray();
        var r59 = Source().Select(p=>Source().Take(3)).ToArray();
        var r60 = Source().Select(p=>Source().Take(p)).ToArray();
        var r61 = Source().Select(p=>Source().ToArray()).ToArray();
        var r62 = Source().Select(p=>Source().ToList()).ToArray();
        var r63 = Source().Select(p=>Source().Union(Second())).ToArray();
        var r64 = Source().Select(p=>Source().Where(q=>q==q)).ToArray();
        var r65 = Source().Select(p=>Source().Where(q=>p==q)).ToArray();
        var r66 = Source().Select(p=>Source().Where((q,index)=>q>index)).ToArray();
        var r67 = Source().Select(p=>Source().Where((q,index)=>p+q>index)).ToArray();
        var r68 = Source().Select(p=>Source().Zip(Second(),(l,r)=>l+r)).ToArray();
        var r69 = Source().Select(p=>Source().Zip(Second(),(l,r)=>p+l+r)).ToArray();
#pragma warning restore CS0219
    }


    public static void Where_InnerEnumerableMethodsImpl() {
        Source().Where(p=>{
            Source().Aggregate((l,r)=>l+r);
            Source().Aggregate((l,r)=>l+r+p);
            Source().Aggregate(0,(sum,q)=>sum+q);
            Source().Aggregate(0,(sum,q)=>sum+q+p);
            Source().Aggregate(0,(sum,q)=>sum+q,sum=>sum*2);
            Source().Aggregate(0,(sum,q)=>sum+q+p,sum=>sum+p);
            Source().AggregateBy(q=>q%2,0,(sum,q)=>sum+q);
            Source().AggregateBy(q=>(q+p)%2,0,(sum,q)=>sum+q+p);
            Source().All(q=>q>0);
            Source().All(q=>q>=p);
            Source().Any();
            Source().Any(q=>q>0);
            Source().Any(q=>q==p);
            Source().Append(5);
            Source().Append(p);
            Source().AsEnumerable();
            Source().Average();
            Rows().Average(row=>row.Value);
            Rows().Average(row=>row.Value+p);
            CastObjects().Cast<int>();
            Source().Chunk(2);
            Source().Chunk(p);
            Source().Concat(Second());
            Source().Contains(3);
            Source().Contains(p);
            Source().Count();
            Source().Count(q=>q>2);
            Source().Count(q=>q>=p);
            Duplicates().Distinct();
            Rows().DistinctBy(row=>row.Key);
            Rows().DistinctBy(row=>row.Key+p);
            Source().First();
            Source().First(q=>q>2);
            Source().First(q=>q>=p);
            Rows().GroupBy(row=>row.Key);
            Rows().GroupBy(row=>row.Key+p);
            Source().Join(Second(),outerValue=>outerValue,innerValue=>innerValue,(outerValue,innerValue)=>outerValue+innerValue);
            Source().Join(Second(),outerValue=>outerValue+p,innerValue=>innerValue+p,(outerValue,innerValue)=>outerValue+innerValue+p);
            Rows().Max(row=>row.Value);
            Rows().Max(row=>row.Value+p);
            CastObjects().OfType<int>();
            Rows().OrderBy(row=>row.Key);
            Rows().OrderBy(row=>row.Key+p);
            Source().Select(q=>q==q);
            Source().Select(q=>p==q);
            Source().Select((q,index)=>q==index);
            Source().Select((q,index)=>p+q==index);
            Source().SelectMany(q=>new[] { q,q+10 });
            Source().SelectMany(q=>new[] { p,q });
            Source().SelectMany((q,index)=>new[] { q,index });
            Source().SelectMany((q,index)=>new[] { p+q,index });
            Source().SelectMany(q=>new[] { q,q+10 },(q,sub)=>q+sub);
            Source().SelectMany(q=>new[] { p,q },(q,sub)=>p+q+sub);
            Source().SelectMany((q,index)=>new[] { q,index },(q,sub)=>q+sub);
            Source().SelectMany((q,index)=>new[] { p+q,index },(q,sub)=>p+q+sub);
            Source().SkipWhile(q=>q<3);
            Source().SkipWhile(q=>q<p);
            Rows().Sum(row=>row.Value);
            Rows().Sum(row=>row.Value+p);
            Source().Take(3);
            Source().Take(p);
            Source().ToArray();
            Source().ToList();
            Source().Union(Second());
            Source().Where(q=>q==q);
            Source().Where(q=>p==q);
            Source().Where((q,index)=>q>index);
            Source().Where((q,index)=>p+q>index);
            Source().Zip(Second(),(l,r)=>l+r);
            Source().Zip(Second(),(l,r)=>p+l+r);
            return Source().Any();
        }).ToArray();
    }

    [Fact, Trait("Category","Unit")]
    public static void Where_InnerEnumerableMethods() {
        Where_InnerEnumerableMethodsImpl();
    }

    private static int[] Source() => [1,2,3,4];
    private static int[] Second() => [3,4,5];
    private static int[] Duplicates() => [1,2,2,3,3,4];
    private static NonGenericEnumerable CastObjects() => new object[] { 1,2,3 };
    private static Row[] Rows() => [new(1,10,"beta"),new(2,20,"alpha"),new(3,30,"gamma")];

    internal sealed record Row(int Key,int Value,string Name);
}
