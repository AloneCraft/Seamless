using System.Text.RegularExpressions;

using Seamless.FLinq;

using Test.System.Linq.Intercept;

using CommonStore=global::Seamless.Execution.Store;

namespace Test.Seamless.Tsavorite.Linq.Enumerable;

/// <summary>
/// マルチ用(並列区間の操作用オブジェクト)を起点にした FLinq の仕様。件数を持たないので、件数で先取りせずに集め、
/// 素の Count は要素を取り出さずに数える ScanCount に任せ、TryGetNonEnumeratedCount は false を返す。
/// </summary>
public class FLinqParallelSourceTests {
    private static ParallelHashSet<int>? hashSetSource;
    private static ParallelSet<int,Score>? setSource;
    private static readonly int[] values=[.. global::System.Linq.Enumerable.Range(1,1000)];
    // 生成コードが根の件数を読んでいる形(Source().Count や 根の変数.Count)
    private static readonly Regex sourceCount=new(@"(Source\(\)|IterateSource\d+)\.Count\b");

    [Fact,Trait("Category","Unit")]
    public void ParallelHashSet(){
        using var store=CommonStore.CreateMemory();
        using var data=store.CreateTsavoriteHashSet<int>("ids");
        using var region=data.BeginParallel(values.Length);
        using var set=region.Create();
        foreach(var value in values){
            set.Add(value);
        }
        hashSetSource=set;
        try{
            //結果は配列で求めたものと同じ
            Assert.Equal(values.Length,HashSetCountImpl());
            Assert.Equal(values.Count(static value=>value%3==0),HashSetCountWhereImpl());
            Assert.Equal(values.Where(static value=>value%2!=0).Order(),HashSetToListImpl().Order());
            Assert.Equal(values.Where(static value=>value%2==0).Select(static value=>value*10).Order(),HashSetToArrayImpl().Order());
            Assert.Equal(values.Length,HashSetToArrayAllImpl().Length);
            Assert.True(values.ToHashSet().SetEquals(HashSetToHashSetImpl()));
            var dictionary=HashSetToDictionaryImpl();
            Assert.Equal(values.Length,dictionary.Count);
            Assert.Equal(20,dictionary[10]);
            Assert.True(HashSetContainsImpl(500));
            Assert.False(HashSetContainsImpl(5000));
            Assert.True(HashSetAnyWhereImpl());
            Assert.Equal(values.Sum(),HashSetSumImpl());
            //件数を持たないので、列挙せずに件数は分からない(System.Linq と同じ)
            Assert.False(HashSetTryGetNonEnumeratedCountImpl(out var count));
            Assert.Equal(0,count);

            //生成コードはどこでも根の件数を読まない(マルチ用は Count を持たないので、読めばコンパイルできない)
            Assert.DoesNotMatch(sourceCount,GeneratedInterceptorAssert.SourceFor<FLinqParallelSourceTests>(nameof(HashSetCountImpl)));
            //素の Count は ScanCount、段があれば Iterate で数える。ToArray は伸ばしながら集める。Contains は索引の直接検索
            Assert.Contains(".ScanCount()",InterceptorBody(nameof(HashSetCountImpl)));
            Assert.DoesNotContain("ScanCount",InterceptorBody(nameof(HashSetCountWhereImpl)));
            Assert.Contains(".Iterate(ref __TsavoriteParallelHashSetIterateState",InterceptorBody(nameof(HashSetToListImpl)));
            Assert.Contains("List=new global::System.Collections.Generic.List<int>();",InterceptorBody(nameof(HashSetToListImpl)));
            Assert.Contains("Array=global::System.Array.Empty<int>();",InterceptorBody(nameof(HashSetToArrayImpl)));
            Assert.Contains("GrowArray",GeneratedInterceptorAssert.SourceFor<FLinqParallelSourceTests>(nameof(HashSetToArrayImpl)));
            var contains=InterceptorBody(nameof(HashSetContainsImpl));
            Assert.Contains(").Contains(",contains);
            Assert.DoesNotContain(".Iterate(ref",contains);
        }finally{
            hashSetSource=null;
        }
        {
            //空のときも同じ
            using var emptyStore=CommonStore.CreateMemory();
            using var emptyData=emptyStore.CreateTsavoriteHashSet<int>("ids");
            using var emptyRegion=emptyData.BeginParallel();
            using var empty=emptyRegion.Create();
            hashSetSource=empty;
            try{
                Assert.Equal(0,HashSetCountImpl());
                Assert.Empty(HashSetToArrayAllImpl());
                Assert.Empty(HashSetToListImpl());
                Assert.Empty(HashSetToDictionaryImpl());
                Assert.False(HashSetAnyWhereImpl());
            }finally{
                hashSetSource=null;
            }
        }
    }

    [Fact,Trait("Category","Unit")]
    public void ParallelSet(){
        using var store=CommonStore.CreateMemory();
        using var data=store.CreateTsavoriteSet<int,Score>("scores");
        using var region=data.BeginParallel(values.Length);
        using var set=region.Create();
        foreach(var value in values){
            set.Add(new Score(value,value*2));
        }
        setSource=set;
        try{
            Assert.Equal(values.Length,SetCountImpl());
            Assert.Equal(values.Order(),SetToListImpl().Order());
            Assert.Equal(values.Where(static value=>value>900).Select(static value=>value*2).Order(),SetToArrayImpl().Order());
            Assert.True(SetContainsImpl(new Score(7,14)));
            Assert.False(SetContainsImpl(new Score(7000,1)));
            Assert.False(SetTryGetNonEnumeratedCountImpl(out var count));
            Assert.Equal(0,count);

            Assert.Contains(".ScanCount()",InterceptorBody(nameof(SetCountImpl)));
            Assert.Contains(".Iterate(ref __TsavoriteParallelSetIterateState",InterceptorBody(nameof(SetToListImpl)));
        }finally{
            setSource=null;
        }
    }

    [Fact,Trait("Category","Unit")]
    public void AsValueEnumerable(){
        //実行時ライブラリの入口も、件数なし・Contains は索引の直接検索
        using var store=CommonStore.CreateMemory();
        using var data=store.CreateTsavoriteHashSet<int>("ids");
        using var region=data.BeginParallel();
        using var set=region.Create();
        set.Add(1);
        set.Add(2);
        var enumerable=global::Seamless.Linq.Enumerable.AsValueEnumerable(set);
        Assert.False(enumerable.Enumerator.TryGetNonEnumeratedCount(out _));
        Assert.True(global::Seamless.Linq.Enumerable.Contains(enumerable,2));
        Assert.False(global::Seamless.Linq.Enumerable.Contains(enumerable,3));
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int HashSetCountImpl()=>HashSetSource().Count();

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int HashSetCountWhereImpl()=>HashSetSource().Where(static value=>value%3==0).Count();

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static List<int> HashSetToListImpl()=>HashSetSource().Where(static value=>value%2!=0).ToList();

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int[] HashSetToArrayImpl()=>HashSetSource().Where(static value=>value%2==0).Select(static value=>value*10).ToArray();

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int[] HashSetToArrayAllImpl()=>HashSetSource().ToArray();

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static global::System.Collections.Generic.HashSet<int> HashSetToHashSetImpl()=>HashSetSource().ToHashSet();

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static Dictionary<int,int> HashSetToDictionaryImpl()=>HashSetSource().ToDictionary(static value=>value,static value=>value*2);

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    // Source().Contains(x) は自身のインスタンスメソッドに束縛されて LINQ 介入が起きないので、AsEnumerable() を挟む
    private static bool HashSetContainsImpl(int value)=>HashSetSource().AsEnumerable().Contains(value);

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static bool HashSetAnyWhereImpl()=>HashSetSource().Where(static value=>value>999).Any();

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int HashSetSumImpl()=>HashSetSource().Sum();

    // TryGetNonEnumeratedCount は文の形(宣言・式・return)で書き換えられる
    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static bool HashSetTryGetNonEnumeratedCountImpl(out int count){
        var result=HashSetSource().TryGetNonEnumeratedCount(out count);
        return result;
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int SetCountImpl()=>SetSource().Count();

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static List<int> SetToListImpl()=>SetSource().Select(static score=>score.Key).ToList();

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int[] SetToArrayImpl()=>SetSource().Where(static score=>score.Key>900).Select(static score=>score.Value).ToArray();

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static bool SetContainsImpl(Score score)=>SetSource().AsEnumerable().Contains(score);

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static bool SetTryGetNonEnumeratedCountImpl(out int count){
        return SetSource().TryGetNonEnumeratedCount(out count);
    }

    // 生成ファイルのうち、指定したメソッドの介入部分(次の介入の説明まで)
    private static string InterceptorBody(string methodName){
        var generated=GeneratedInterceptorAssert.SourceFor<FLinqParallelSourceTests>(methodName);
        var start=generated.IndexOf($"<c>{methodName}()</c>",StringComparison.Ordinal);
        Assert.True(start>=0,$"Interceptor for {methodName} was not found in the generated source.");
        var end=generated.IndexOf("/// <summary>",start+1,StringComparison.Ordinal);
        if(end<0){
            end=generated.Length;
        }
        return generated[start..end];
    }

    private static ParallelHashSet<int> HashSetSource()=>
        hashSetSource??throw new InvalidOperationException("Source was not initialized.");

    private static ParallelSet<int,Score> SetSource()=>
        setSource??throw new InvalidOperationException("Source was not initialized.");

    public readonly record struct Score(int Key,int Value):global::Seamless.Collections.IRecord<int>;
}
