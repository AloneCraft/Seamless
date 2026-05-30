using System.Linq;

namespace Test.Seamless.Intercept.System.Linq;

public sealed class EnumerableMethod2 {
    [Fact, Trait("Category","Unit")]public static void Select内部Any外出し不可() {
        Select内部Any外出し不可Impl();
    }
    private static void Select内部Any外出し不可Impl() {
        var actual=Source().Select(
            p=>Source().Any(q=>q!=p)
        ).ToArray();
        Assert.Equal([true,true,true,true],actual);
    }
    [Fact, Trait("Category","Unit")]public static void Where内部Where外出し不可() {
        Where内部Where外出し不可Impl();
    }
    private static void Where内部Where外出し不可Impl() {
        var actual=Source().Where(
            p=>Source().Where(q=>q!=p).Any()
        ).ToArray();
        Assert.Equal([1,2,3,4],actual);
    }
    [Fact, Trait("Category","Unit")]public static void Where内部Where外出し可() {
        Where内部Where外出し可Impl();
    }
    private static void Where内部Where外出し可Impl() {
        var actual=Source().Where(
            p=>Source().Where(q=>q!=3).Any()
        ).ToArray();
        Assert.Equal([1,2,3,4],actual);
    }
    [Fact, Trait("Category","Unit")]public static void Where内部Any外出し不可() {
        Where内部Any外出し不可Impl();
    }
    private static void Where内部Any外出し不可Impl() {
        var actual=Source().Where(
            p=>Source().Any(q=>q!=p)
        ).ToArray();
        Assert.Equal([1,2,3,4],actual);
    }
    [Fact, Trait("Category","Unit")]public static void Where内部Any外出し可() {
        Where内部Any外出し可Impl();
    }
    private static void Where内部Any外出し可Impl() {
        var actual=Source().Where(
            p=>Source().Any(q=>q!=3)
        ).ToArray();
        Assert.Equal([1,2,3,4],actual);
    }
    [Fact, Trait("Category","Unit")]public static void Where内部Selectインライン() {
        Where内部Select外出し不可Impl();
        Where内部Select外出し可Impl();
    }
    private static void Where内部Select外出し不可Impl() {
        var actual=Source().Where(
            p=>Source().Select(q=>q!=p).Any()
        ).ToArray();
        Assert.Equal([1,2,3,4],actual);
    }
    private static void Where内部Select外出し可Impl() {
        var actual=Source().Where(
            p=>Source().Select(q=>q!=3).Any()
        ).ToArray();
        Assert.Equal([1,2,3,4],actual);
    }






    [Fact, Trait("Category","Unit")]public static void Select内部Selectインライン() {
        Select内部Select外出し不可Impl();
        Select内部Select外出し可Impl();
    }
    private static void Select内部Select外出し不可Impl() {
        var actual=Source().Select(
            p=>Source().Select(q=>q!=p).Any()
        ).ToArray();
        Assert.Equal([true,true,true,true],actual);
    }
    private static void Select内部Select外出し可Impl() {
        var actual=Source().Select(
            p=>Source().Select(q=>q!=3).Any()
        ).ToArray();
        Assert.Equal([true,true,true,true],actual);
    }
    [Fact, Trait("Category","Unit")]public static void SelectManyインライン() {
        SelectManyImpl();
    }
    private static void SelectManyImpl() {
        var actual=Source().SelectMany(
            p=>Source()
        ).ToList();
    }
    [Fact, Trait("Category","Unit")]public static void SelectMany内部Where外出し不可() {
        SelectMany内部Where外出し不可Impl();
    }
    private static void SelectMany内部Where外出し不可Impl() {
        var actual=Source().SelectMany(
            p=>Source().Where(q=>q!=p)
        ).ToList();
    }
    [Fact, Trait("Category","Unit")]public static void SelectMany内部Where() {
        SelectMany内部WhereImpl();
    }
    private static void SelectMany内部WhereImpl() {
        var actual=Source().SelectMany(
            p=>Source().Where(q=>q==1)
        ).ToList();
    }
    [Fact, Trait("Category","Unit")]public static void SelectMany内部WhereをDictionaryにして外出し() {
        SelectMany内部WhereをDictionaryにして外出しImpl();
    }
    private static void SelectMany内部WhereをDictionaryにして外出しImpl() {
        /*
        var actual=Source().SelectMany(
            p=>Source().CreateDictionary(q=>q/2).Equal(p)
        );
        ↓
        var Dictionary=Source().CreateDictionary(q=>q/2);
        var actual=Source().SelectMany(
            p=>Dictionary.Equal(p)
        );
        ↓CreateDictionaryをインライン展開
        var Dictionary=new Dictionary<TKey,List<T>>();
        foreach(var a in Source()){
            if(Dictionary.TryGetValue(a/2,out var List)){
                List.Add(a);
            }else{
                Dictionary.Add(new List<T>{a});
            }
        }
        var actual=Source().SelectMany(
            p=>Dictionary.Equal(p)
        );
         */
        var actual=Source().SelectMany(
            p=>Source().Where(q=>q==p)
        ).ToList();
    }
    [Fact, Trait("Category","Unit")]public void Join_Where() {
        Join_WhereImpl();
    }
    private void Join_WhereImpl() {
        var actual=Source().Join(Source(),o=>o+1,i=>i/1,(o,i) => o+i).Where(p => p%2==0).ToList();
    }
    [Fact, Trait("Category","Unit")]public static void Join_keySelector内部Linqインライン() {
        Join_keySelector内部LinqインラインImpl();
    }
    private static void Join_keySelector内部LinqインラインImpl() {
        var actual=Source()
            .Join(
                Source(),
                outerValue=>Source().Where(value=>value<outerValue).Count(),
                innerValue=>Source().Where(value=>value<=innerValue).Count()-1,
                (outerValue,innerValue)=>outerValue*10+innerValue)
            .ToArray();
        Assert.Equal([11,22,33,44],actual);
    }
    [Fact, Trait("Category","Unit")]public static void GroupJoin_keySelector内部Linqインライン() {
        GroupJoin_keySelector内部LinqインラインImpl();
    }
    private static void GroupJoin_keySelector内部LinqインラインImpl() {
        var actual=Source()
            .GroupJoin(
                Source(),
                outerValue=>Source().Where(value=>value<outerValue).Count(),
                innerValue=>Source().Where(value=>value<=innerValue).Count()-1,
                (outerValue,group)=>group.Sum())
            .ToArray();
        Assert.Equal([1,2,3,4],actual);
    }
    [Fact, Trait("Category","Unit")]
    public void Select_Select() {
        Select_SelectImpl();
    }
    private void Select_SelectImpl() {
        var actual=Source().Select(static value => value*10).Where(static value => value>20).ToList();
    }

    [Fact, Trait("Category","Unit")]
    public void Select_Select_index() {
        Select_Select_indexImpl();
    }
    private void Select_Select_indexImpl(){
        var actual=Source().Select(static value => value+1).Select(static (value,index) => value+index).ToList();
    }

    [Fact, Trait("Category","Unit")]
    public void Select_Where_index() {
        Select_Where_indexImpl();
    }
    private void Select_Where_indexImpl() {
        var actual=Source().Select(static value => value*10).Where(static (value,index) => value>20&&index>0).ToList();
    }

    [Fact, Trait("Category","Unit")]
    public void Select_index_Select() {
        Select_index_SelectImpl();
    }
    private void Select_index_SelectImpl() {
        var actual=Source().Select(static (value,index) => value+index).Select(static value => value*10).ToList();
    }

    [Fact, Trait("Category","Unit")]
    public void Select_index_Select_index() {
        Select_index_Select_indexImpl();
    }
    private void Select_index_Select_indexImpl() {
        var actual=Source().Select(static (value,index) => value+index).Select(static (value,index) => value+index).ToList();
    }

    [Fact, Trait("Category","Unit")]
    public void Select_index_Where() {
        Select_index_WhereImpl();
    }
    private void Select_index_WhereImpl() {
        var actual=Source().Select(static (value,index) => value+index).Where(static value => value>4).ToList();
    }

    [Fact, Trait("Category","Unit")]
    public void Select_index_Where_index() {
        Select_index_Where_indexImpl();
    }
    private void Select_index_Where_indexImpl() {
        var actual=Source().Select(static (value,index) => value+index).Where(static (value,index) => value>3&&index>1).ToList();
    }

    [Fact, Trait("Category","Unit")]
    public void Where_Select() {
        Where_SelectImpl();
    }
    private void Where_SelectImpl() {
        var actual=Source().Where(static value => value>1).Select(static value => value*10).ToList();
    }

    [Fact, Trait("Category","Unit")]
    public void Where_Select_index() {
        Where_Select_indexImpl();
    }
    private void Where_Select_indexImpl() {
        var actual=Source().Where(static value => value>1).Select(static (value,index) => value+index).ToList();
    }

    [Fact, Trait("Category","Unit")]
    public void Where_Where() {
        Where_WhereImpl();
    }
    private void Where_WhereImpl() {
        var actual=Source().Where(static value => value>1).Where(static value => value<4).ToList();
    }

    [Fact, Trait("Category","Unit")]
    public void Where_Where_index() {
        Where_Where_indexImpl();
    }
    private void Where_Where_indexImpl() {
        var actual=Source().Where(static value => value>1).Where(static (value,index) => value<4&&index<2).ToList();
    }

    [Fact, Trait("Category","Unit")]
    public void Where_index_Select() {
        Where_index_SelectImpl();
    }
    private void Where_index_SelectImpl() {
        var actual=Source().Where(static (value,index) => value>index).Select(static value => value*10).ToList();
    }

    [Fact, Trait("Category","Unit")]
    public void Where_index_Select_index() {
        Where_index_Select_indexImpl();
    }
    private void Where_index_Select_indexImpl() {
        var actual=Source().Where(static (value,index) => value>index).Select(static (value,index) => value+index).ToList();
    }

    [Fact, Trait("Category","Unit")]
    public void Where_index_Where() {
        Where_index_WhereImpl();
    }
    private void Where_index_WhereImpl() {
        var actual=Source().Where(static (value,index) => value>index).Where(static value => value<4).ToList();
    }

    [Fact, Trait("Category","Unit")]
    public void Where_index_Where_index() {
        Where_index_Where_indexImpl();
    }
    private void Where_index_Where_indexImpl() {
        var actual=Source().Where(static (value,index) => value>index).Where(static (value,index) => value<4&&index<2).ToList();
    }

    [Fact, Trait("Category","Unit")]
    public void Select_Take_Select() {
        Select_Take_SelectImpl();
    }
    private void Select_Take_SelectImpl() {
        var actual=Source().Select(static value => value+1).Take(2).Select(static value => value*10).ToList();
    }

    [Fact, Trait("Category","Unit")]
    public void Take_zero_Select_does_not_enumerate_source() {
        Take_zero_Select_does_not_enumerate_sourceImpl();
    }
    private void Take_zero_Select_does_not_enumerate_sourceImpl() {
        ThrowingSource().Take(0).Select(static value => value*10);
    }

    private static int[] Source() => [1,2,3,4];
    private static IEnumerable<int> ThrowingSource() => new ThrowingEnumerable();

    private sealed class ThrowingEnumerable:IEnumerable<int> {
        public IEnumerator<int> GetEnumerator() => throw new InvalidOperationException("Take(0) must not enumerate the source.");
        global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
