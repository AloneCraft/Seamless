using System.Linq;
using NonGenericEnumerable = System.Collections.IEnumerable;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq;

public sealed class IEnumerable_ToList {
    [Fact,Trait("Category","Unit")]
    public void AggregateBy() {
        AggregateBy_Static();
        AggregateBy_Lambda();
        AggregateBy_Captured();
        AggregateBy_Delegate();
        AggregateBy_Variable();
    }
    private static void AggregateBy_Static() {
        var expected=SystemEnumerable.AggregateBy(Source(),static v => v%2,0,static (sum,v) => sum+v).ToList();
        var actual=Source().AggregateBy(static v => v%2,0,static (sum,v) => sum+v).ToList();
        Assert.Equal(expected,actual);
    }
    private static void AggregateBy_Lambda() {
        var expected=SystemEnumerable.AggregateBy(Source(),v => v%2,0,(sum,v) => sum+v).ToList();
        var actual=Source().AggregateBy(v => v%2,0,(sum,v) => sum+v).ToList();
        Assert.Equal(expected,actual);
    }
    private static void AggregateBy_Captured() {
        var mod=2;
        var expected=SystemEnumerable.AggregateBy(Source(),v => v%mod,0,(sum,v) => sum+v).ToList();
        var actual=Source().AggregateBy(v => v%mod,0,(sum,v) => sum+v).ToList();
        Assert.Equal(expected,actual);
    }
    private static void AggregateBy_Delegate() {
        var expected=SystemEnumerable.AggregateBy(Source(),delegate(int v) { return v%2; },0,delegate(int sum,int v) { return sum+v; }).ToList();
        var actual=Source().AggregateBy(delegate(int v) { return v%2; },0,delegate(int sum,int v) { return sum+v; }).ToList();
        Assert.Equal(expected,actual);
    }
    private static void AggregateBy_Variable() {
        Func<int,int> keySelector=v => v%2;
        Func<int,int,int> func=(sum,v) => sum+v;
        var expected=SystemEnumerable.AggregateBy(Source(),keySelector,0,func).ToList();
        var actual=Source().AggregateBy(keySelector,0,func).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void Append() {
        Append_NoDelegate();
    }
    private static void Append_NoDelegate() {
        var expected=SystemEnumerable.Append(Source(),5).ToList();
        var actual=Source().Append(5).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void AsEnumerable() {
        AsEnumerable_NoDelegate();
    }
    private static void AsEnumerable_NoDelegate() {
        var expected=SystemEnumerable.AsEnumerable(Source()).ToList();
        var actual=Source().AsEnumerable().ToList();
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void Cast() {
        Cast_NoDelegate();
    }
    private static void Cast_NoDelegate() {
        var expected=SystemEnumerable.Cast<int>(CastObjects()).ToList();
        var actual=CastObjects().Cast<int>().ToList();
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void Chunk() {
        Chunk_NoDelegate();
    }
    private static void Chunk_NoDelegate() {
        var expected=SystemEnumerable.Chunk(Source(),2).ToList();
        var actual=Source().Chunk(2).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void Concat() {
        Concat_NoDelegate();
    }
    private static void Concat_NoDelegate() {
        var expected=SystemEnumerable.Concat(Source(),Second()).ToList();
        var actual=Source().Concat(Second()).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void CountBy() {
        CountBy_Static();
        CountBy_Lambda();
        CountBy_Captured();
        CountBy_Delegate();
        CountBy_Variable();
    }
    private static void CountBy_Static() {
        var expected=SystemEnumerable.CountBy(Source(),static v => v%2).ToList();
        var actual=Source().CountBy(static v => v%2).ToList();
        Assert.Equal(expected,actual);
    }
    private static void CountBy_Lambda() {
        var expected=SystemEnumerable.CountBy(Source(),v => v%2).ToList();
        var actual=Source().CountBy(v => v%2).ToList();
        Assert.Equal(expected,actual);
    }
    private static void CountBy_Captured() {
        var mod=2;
        var expected=SystemEnumerable.CountBy(Source(),v => v%mod).ToList();
        var actual=Source().CountBy(v => v%mod).ToList();
        Assert.Equal(expected,actual);
    }
    private static void CountBy_Delegate() {
        var expected=SystemEnumerable.CountBy(Source(),delegate(int v) { return v%2; }).ToList();
        var actual=Source().CountBy(delegate(int v) { return v%2; }).ToList();
        Assert.Equal(expected,actual);
    }
    private static void CountBy_Variable() {
        Func<int,int> keySelector=v => v%2;
        var expected=SystemEnumerable.CountBy(Source(),keySelector).ToList();
        var actual=Source().CountBy(keySelector).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void DefaultIfEmpty() {
        DefaultIfEmpty_NoDelegate();
        DefaultIfEmpty_defaultValue_NoDelegate();
    }
    private static void DefaultIfEmpty_NoDelegate() {
        var expected=SystemEnumerable.DefaultIfEmpty(Source()).ToList();
        var actual=Source().DefaultIfEmpty().ToList();
        Assert.Equal(expected,actual);
    }
    private static void DefaultIfEmpty_defaultValue_NoDelegate() {
        var empty=new int[0];
        var expected=SystemEnumerable.DefaultIfEmpty(empty,99).ToList();
        var actual=empty.DefaultIfEmpty(99).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void Distinct() {
        Distinct_NoDelegate();
    }
    private static void Distinct_NoDelegate() {
        var expected=SystemEnumerable.Distinct(Duplicates()).ToList();
        var actual=Duplicates().Distinct().ToList();
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void DistinctBy() {
        DistinctBy_Static();
        DistinctBy_Lambda();
        DistinctBy_Captured();
        DistinctBy_Delegate();
        DistinctBy_Variable();
    }
    private static void DistinctBy_Static() {
        var expected=SystemEnumerable.DistinctBy(Rows(),static row => row.Key).ToList();
        var actual=Rows().DistinctBy(static row => row.Key).ToList();
        Assert.Equal(expected,actual);
    }
    private static void DistinctBy_Lambda() {
        var expected=SystemEnumerable.DistinctBy(Rows(),row => row.Key).ToList();
        var actual=Rows().DistinctBy(row => row.Key).ToList();
        Assert.Equal(expected,actual);
    }
    private static void DistinctBy_Captured() {
        var offset=0;
        var expected=SystemEnumerable.DistinctBy(Rows(),row => row.Key+offset).ToList();
        var actual=Rows().DistinctBy(row => row.Key+offset).ToList();
        Assert.Equal(expected,actual);
    }
    private static void DistinctBy_Delegate() {
        var expected=SystemEnumerable.DistinctBy(Rows(),delegate(Row row) { return row.Key; }).ToList();
        var actual=Rows().DistinctBy(delegate(Row row) { return row.Key; }).ToList();
        Assert.Equal(expected,actual);
    }
    private static void DistinctBy_Variable() {
        Func<Row,int> keySelector=row => row.Key;
        var expected=SystemEnumerable.DistinctBy(Rows(),keySelector).ToList();
        var actual=Rows().DistinctBy(keySelector).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void Except() {
        Except_NoDelegate();
    }
    private static void Except_NoDelegate() {
        var expected=SystemEnumerable.Except(Source(),Second()).ToList();
        var actual=Source().Except(Second()).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void ExceptBy() {
        ExceptBy_Static();
        ExceptBy_Lambda();
        ExceptBy_Captured();
        ExceptBy_Delegate();
        ExceptBy_Variable();
    }
    private static void ExceptBy_Static() {
        var expected=SystemEnumerable.ExceptBy(Rows(),new[]{1},static row => row.Key).ToList();
        var actual=Rows().ExceptBy(new[]{1},static row => row.Key).ToList();
        Assert.Equal(expected,actual);
    }
    private static void ExceptBy_Lambda() {
        var expected=SystemEnumerable.ExceptBy(Rows(),new[]{1},row => row.Key).ToList();
        var actual=Rows().ExceptBy(new[]{1},row => row.Key).ToList();
        Assert.Equal(expected,actual);
    }
    private static void ExceptBy_Captured() {
        var offset=0;
        var expected=SystemEnumerable.ExceptBy(Rows(),new[]{1},row => row.Key+offset).ToList();
        var actual=Rows().ExceptBy(new[]{1},row => row.Key+offset).ToList();
        Assert.Equal(expected,actual);
    }
    private static void ExceptBy_Delegate() {
        var expected=SystemEnumerable.ExceptBy(Rows(),new[]{1},delegate(Row row) { return row.Key; }).ToList();
        var actual=Rows().ExceptBy(new[]{1},delegate(Row row) { return row.Key; }).ToList();
        Assert.Equal(expected,actual);
    }
    private static void ExceptBy_Variable() {
        Func<Row,int> keySelector=row => row.Key;
        var expected=SystemEnumerable.ExceptBy(Rows(),new[]{1},keySelector).ToList();
        var actual=Rows().ExceptBy(new[]{1},keySelector).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void GroupBy_keySelector() {
        GroupBy_keySelector_Static();
        GroupBy_keySelector_Lambda();
        GroupBy_keySelector_Captured();
        GroupBy_keySelector_Delegate();
        GroupBy_keySelector_Variable();
    }
    private static void GroupBy_keySelector_Static() {
        var expected=SystemEnumerable.GroupBy(Rows(),static row => row.Key).ToList();
        var actual=Rows().GroupBy(static row => row.Key).ToList();
        Assert.Equal(expected,actual);
    }
    private static void GroupBy_keySelector_Lambda() {
        var expected=SystemEnumerable.GroupBy(Rows(),row => row.Key).ToList();
        var actual=Rows().GroupBy(row => row.Key).ToList();
        Assert.Equal(expected,actual);
    }
    private static void GroupBy_keySelector_Captured() {
        var offset=0;
        var expected=SystemEnumerable.GroupBy(Rows(),row => row.Key+offset).ToList();
        var actual=Rows().GroupBy(row => row.Key+offset).ToList();
        Assert.Equal(expected,actual);
    }
    private static void GroupBy_keySelector_Delegate() {
        var expected=SystemEnumerable.GroupBy(Rows(),delegate(Row row) { return row.Key; }).ToList();
        var actual=Rows().GroupBy(delegate(Row row) { return row.Key; }).ToList();
        Assert.Equal(expected,actual);
    }
    private static void GroupBy_keySelector_Variable() {
        Func<Row,int> keySelector=row => row.Key;
        var expected=SystemEnumerable.GroupBy(Rows(),keySelector).ToList();
        var actual=Rows().GroupBy(keySelector).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void GroupBy_keySelector_elementSelector() {
        GroupBy_keySelector_elementSelector_Static();
        GroupBy_keySelector_elementSelector_Lambda();
        GroupBy_keySelector_elementSelector_Captured();
        GroupBy_keySelector_elementSelector_Delegate();
        GroupBy_keySelector_elementSelector_Variable();
    }
    private static void GroupBy_keySelector_elementSelector_Static() {
        var expected=SystemEnumerable.GroupBy(Rows(),static row => row.Key,static row => row.Value).ToList();
        var actual=Rows().GroupBy(static row => row.Key,static row => row.Value).ToList();
        Assert.Equal(expected,actual);
    }
    private static void GroupBy_keySelector_elementSelector_Lambda() {
        var expected=SystemEnumerable.GroupBy(Rows(),row => row.Key,row => row.Value).ToList();
        var actual=Rows().GroupBy(row => row.Key,row => row.Value).ToList();
        Assert.Equal(expected,actual);
    }
    private static void GroupBy_keySelector_elementSelector_Captured() {
        var offset=0;
        var expected=SystemEnumerable.GroupBy(Rows(),row => row.Key+offset,row => row.Value+offset).ToList();
        var actual=Rows().GroupBy(row => row.Key+offset,row => row.Value+offset).ToList();
        Assert.Equal(expected,actual);
    }
    private static void GroupBy_keySelector_elementSelector_Delegate() {
        var expected=SystemEnumerable.GroupBy(Rows(),delegate(Row row) { return row.Key; },delegate(Row row) { return row.Value; }).ToList();
        var actual=Rows().GroupBy(delegate(Row row) { return row.Key; },delegate(Row row) { return row.Value; }).ToList();
        Assert.Equal(expected,actual);
    }
    private static void GroupBy_keySelector_elementSelector_Variable() {
        Func<Row,int> keySelector=row => row.Key;
        Func<Row,int> elementSelector=row => row.Value;
        var expected=SystemEnumerable.GroupBy(Rows(),keySelector,elementSelector).ToList();
        var actual=Rows().GroupBy(keySelector,elementSelector).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void GroupBy_keySelector_resultSelector() {
        GroupBy_keySelector_resultSelector_Static();
        GroupBy_keySelector_resultSelector_Lambda();
        GroupBy_keySelector_resultSelector_Captured();
        GroupBy_keySelector_resultSelector_Delegate();
        GroupBy_keySelector_resultSelector_Variable();
    }
    private static void GroupBy_keySelector_resultSelector_Static() {
        var expected=SystemEnumerable.GroupBy(Rows(),static row => row.Key,static (key,elems) => key+elems.Sum(r => r.Value)).ToList();
        var actual=Rows().GroupBy(static row => row.Key,static (key,elems) => key+elems.Sum(r => r.Value)).ToList();
        Assert.Equal(expected,actual);
    }
    private static void GroupBy_keySelector_resultSelector_Lambda() {
        var expected=SystemEnumerable.GroupBy(Rows(),row => row.Key,(key,elems) => key+elems.Sum(r => r.Value)).ToList();
        var actual=Rows().GroupBy(row => row.Key,(key,elems) => key+elems.Sum(r => r.Value)).ToList();
        Assert.Equal(expected,actual);
    }
    private static void GroupBy_keySelector_resultSelector_Captured() {
        var offset=0;
        var expected=SystemEnumerable.GroupBy(Rows(),row => row.Key+offset,(key,elems) => key+elems.Sum(r => r.Value)+offset).ToList();
        var actual=Rows().GroupBy(row => row.Key+offset,(key,elems) => key+elems.Sum(r => r.Value)+offset).ToList();
        Assert.Equal(expected,actual);
    }
    private static void GroupBy_keySelector_resultSelector_Delegate() {
        var expected=SystemEnumerable.GroupBy(Rows(),delegate(Row row) { return row.Key; },delegate(int key,IEnumerable<Row> elems) { return key+elems.Sum(r => r.Value); }).ToList();
        var actual=Rows().GroupBy(delegate(Row row) { return row.Key; },delegate(int key,IEnumerable<Row> elems) { return key+elems.Sum(r => r.Value); }).ToList();
        Assert.Equal(expected,actual);
    }
    private static void GroupBy_keySelector_resultSelector_Variable() {
        Func<Row,int> keySelector=row => row.Key;
        Func<int,IEnumerable<Row>,int> resultSelector=(key,elems) => key+elems.Sum(r => r.Value);
        var expected=SystemEnumerable.GroupBy(Rows(),keySelector,resultSelector).ToList();
        var actual=Rows().GroupBy(keySelector,resultSelector).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void GroupJoin() {
        GroupJoin_Static();
        GroupJoin_Lambda();
        GroupJoin_Captured();
        GroupJoin_Delegate();
        GroupJoin_Variable();
    }
    private static void GroupJoin_Static() {
        var expected=SystemEnumerable.GroupJoin(Source(),Second(),static o => o,static i => i,static (o,matches) => o+matches.Sum()).ToList();
        var actual=Source().GroupJoin(Second(),static o => o,static i => i,static (o,matches) => o+matches.Sum()).ToList();
        Assert.Equal(expected,actual);
    }
    private static void GroupJoin_Lambda() {
        var expected=SystemEnumerable.GroupJoin(Source(),Second(),o => o,i => i,(o,matches) => o+matches.Sum()).ToList();
        var actual=Source().GroupJoin(Second(),o => o,i => i,(o,matches) => o+matches.Sum()).ToList();
        Assert.Equal(expected,actual);
    }
    private static void GroupJoin_Captured() {
        var offset=0;
        var expected=SystemEnumerable.GroupJoin(Source(),Second(),o => o+offset,i => i+offset,(o,matches) => o+matches.Sum()+offset).ToList();
        var actual=Source().GroupJoin(Second(),o => o+offset,i => i+offset,(o,matches) => o+matches.Sum()+offset).ToList();
        Assert.Equal(expected,actual);
    }
    private static void GroupJoin_Delegate() {
        var expected=SystemEnumerable.GroupJoin(Source(),Second(),delegate(int o) { return o; },delegate(int i) { return i; },delegate(int o,IEnumerable<int> matches) { return o+matches.Sum(); }).ToList();
        var actual=Source().GroupJoin(Second(),delegate(int o) { return o; },delegate(int i) { return i; },delegate(int o,IEnumerable<int> matches) { return o+matches.Sum(); }).ToList();
        Assert.Equal(expected,actual);
    }
    private static void GroupJoin_Variable() {
        Func<int,int> outerKeySelector=o => o;
        Func<int,int> innerKeySelector=i => i;
        Func<int,IEnumerable<int>,int> resultSelector=(o,matches) => o+matches.Sum();
        var expected=SystemEnumerable.GroupJoin(Source(),Second(),outerKeySelector,innerKeySelector,resultSelector).ToList();
        var actual=Source().GroupJoin(Second(),outerKeySelector,innerKeySelector,resultSelector).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void Index() {
        Index_NoDelegate();
    }
    private static void Index_NoDelegate() {
        var expected=SystemEnumerable.Index(Source()).ToList();
        var actual=Source().Index().ToList();
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void Intersect() {
        Intersect_NoDelegate();
    }
    private static void Intersect_NoDelegate() {
        var expected=SystemEnumerable.Intersect(Source(),Second()).ToList();
        var actual=Source().Intersect(Second()).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void IntersectBy() {
        IntersectBy_Static();
        IntersectBy_Lambda();
        IntersectBy_Captured();
        IntersectBy_Delegate();
        IntersectBy_Variable();
    }
    private static void IntersectBy_Static() {
        var expected=SystemEnumerable.IntersectBy(Rows(),new[]{1,2},static row => row.Key).ToList();
        var actual=Rows().IntersectBy(new[]{1,2},static row => row.Key).ToList();
        Assert.Equal(expected,actual);
    }
    private static void IntersectBy_Lambda() {
        var expected=SystemEnumerable.IntersectBy(Rows(),new[]{1,2},row => row.Key).ToList();
        var actual=Rows().IntersectBy(new[]{1,2},row => row.Key).ToList();
        Assert.Equal(expected,actual);
    }
    private static void IntersectBy_Captured() {
        var offset=0;
        var expected=SystemEnumerable.IntersectBy(Rows(),new[]{1,2},row => row.Key+offset).ToList();
        var actual=Rows().IntersectBy(new[]{1,2},row => row.Key+offset).ToList();
        Assert.Equal(expected,actual);
    }
    private static void IntersectBy_Delegate() {
        var expected=SystemEnumerable.IntersectBy(Rows(),new[]{1,2},delegate(Row row) { return row.Key; }).ToList();
        var actual=Rows().IntersectBy(new[]{1,2},delegate(Row row) { return row.Key; }).ToList();
        Assert.Equal(expected,actual);
    }
    private static void IntersectBy_Variable() {
        Func<Row,int> keySelector=row => row.Key;
        var expected=SystemEnumerable.IntersectBy(Rows(),new[]{1,2},keySelector).ToList();
        var actual=Rows().IntersectBy(new[]{1,2},keySelector).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void Join() {
        Join_Static();
        Join_Lambda();
        Join_Captured();
        Join_Delegate();
        Join_Variable();
    }
    private static void Join_Static() {
        var expected=SystemEnumerable.Join(Source(),Second(),static o => o,static i => i,static (o,i) => o+i).ToList();
        var actual=Source().Join(Second(),static o => o,static i => i,static (o,i) => o+i).ToList();
        Assert.Equal(expected,actual);
    }
    private static void Join_Lambda() {
        var expected=SystemEnumerable.Join(Source(),Second(),o => o,i => i,(o,i) => o+i).ToList();
        var actual=Source().Join(Second(),o => o,i => i,(o,i) => o+i).ToList();
        Assert.Equal(expected,actual);
    }
    private static void Join_Captured() {
        var offset=0;
        var expected=SystemEnumerable.Join(Source(),Second(),o => o+offset,i => i+offset,(o,i) => o+i+offset).ToList();
        var actual=Source().Join(Second(),o => o+offset,i => i+offset,(o,i) => o+i+offset).ToList();
        Assert.Equal(expected,actual);
    }
    private static void Join_Delegate() {
        var expected=SystemEnumerable.Join(Source(),Second(),delegate(int o) { return o; },delegate(int i) { return i; },delegate(int o,int i) { return o+i; }).ToList();
        var actual=Source().Join(Second(),delegate(int o) { return o; },delegate(int i) { return i; },delegate(int o,int i) { return o+i; }).ToList();
        Assert.Equal(expected,actual);
    }
    private static void Join_Variable() {
        Func<int,int> outerKeySelector=o => o;
        Func<int,int> innerKeySelector=i => i;
        Func<int,int,int> resultSelector=(o,i) => o+i;
        var expected=SystemEnumerable.Join(Source(),Second(),outerKeySelector,innerKeySelector,resultSelector).ToList();
        var actual=Source().Join(Second(),outerKeySelector,innerKeySelector,resultSelector).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void Order() {
        Order_NoDelegate();
    }
    private static void Order_NoDelegate() {
        var expected=SystemEnumerable.Order(Source()).ToList();
        var actual=Source().Order().ToList();
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void OrderBy() {
        OrderBy_Static();
        OrderBy_Lambda();
        OrderBy_Captured();
        OrderBy_Delegate();
        OrderBy_Variable();
    }
    private static void OrderBy_Static() {
        var expected=SystemEnumerable.OrderBy(Rows(),static row => row.Key).ToList();
        var actual=Rows().OrderBy(static row => row.Key).ToList();
        Assert.Equal(expected,actual);
    }
    private static void OrderBy_Lambda() {
        var expected=SystemEnumerable.OrderBy(Rows(),row => row.Key).ToList();
        var actual=Rows().OrderBy(row => row.Key).ToList();
        Assert.Equal(expected,actual);
    }
    private static void OrderBy_Captured() {
        var offset=0;
        var expected=SystemEnumerable.OrderBy(Rows(),row => row.Key+offset).ToList();
        var actual=Rows().OrderBy(row => row.Key+offset).ToList();
        Assert.Equal(expected,actual);
    }
    private static void OrderBy_Delegate() {
        var expected=SystemEnumerable.OrderBy(Rows(),delegate(Row row) { return row.Key; }).ToList();
        var actual=Rows().OrderBy(delegate(Row row) { return row.Key; }).ToList();
        Assert.Equal(expected,actual);
    }
    private static void OrderBy_Variable() {
        Func<Row,int> keySelector=row => row.Key;
        var expected=SystemEnumerable.OrderBy(Rows(),keySelector).ToList();
        var actual=Rows().OrderBy(keySelector).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void OrderByDescending() {
        OrderByDescending_Static();
        OrderByDescending_Lambda();
        OrderByDescending_Captured();
        OrderByDescending_Delegate();
        OrderByDescending_Variable();
    }
    private static void OrderByDescending_Static() {
        var expected=SystemEnumerable.OrderByDescending(Rows(),static row => row.Key).ToList();
        var actual=Rows().OrderByDescending(static row => row.Key).ToList();
        Assert.Equal(expected,actual);
    }
    private static void OrderByDescending_Lambda() {
        var expected=SystemEnumerable.OrderByDescending(Rows(),row => row.Key).ToList();
        var actual=Rows().OrderByDescending(row => row.Key).ToList();
        Assert.Equal(expected,actual);
    }
    private static void OrderByDescending_Captured() {
        var offset=0;
        var expected=SystemEnumerable.OrderByDescending(Rows(),row => row.Key+offset).ToList();
        var actual=Rows().OrderByDescending(row => row.Key+offset).ToList();
        Assert.Equal(expected,actual);
    }
    private static void OrderByDescending_Delegate() {
        var expected=SystemEnumerable.OrderByDescending(Rows(),delegate(Row row) { return row.Key; }).ToList();
        var actual=Rows().OrderByDescending(delegate(Row row) { return row.Key; }).ToList();
        Assert.Equal(expected,actual);
    }
    private static void OrderByDescending_Variable() {
        Func<Row,int> keySelector=row => row.Key;
        var expected=SystemEnumerable.OrderByDescending(Rows(),keySelector).ToList();
        var actual=Rows().OrderByDescending(keySelector).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void Prepend() {
        Prepend_NoDelegate();
    }
    private static void Prepend_NoDelegate() {
        var expected=SystemEnumerable.Prepend(Source(),0).ToList();
        var actual=Source().Prepend(0).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void Reverse() {
        Reverse_NoDelegate();
    }
    private static void Reverse_NoDelegate() {
        var expected=SystemEnumerable.Reverse(Source()).ToList();
        var actual=Source().Reverse().ToList();
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void Select_selector() {
        Select_selector_Static();
        Select_selector_Lambda();
        Select_selector_Captured();
        Select_selector_Delegate();
        Select_selector_Variable();
    }
    private static void Select_selector_Static() {
        var expected=SystemEnumerable.Select(Source(),static v => v*2).ToList();
        var actual=Source().Select(static v => v*2).ToList();
        Assert.Equal(expected,actual);
    }
    private static void Select_selector_Lambda() {
        var expected=SystemEnumerable.Select(Source(),v => v*2).ToList();
        var actual=Source().Select(v => v*2).ToList();
        Assert.Equal(expected,actual);
    }
    private static void Select_selector_Captured() {
        var multiplier=2;
        var expected=SystemEnumerable.Select(Source(),v => v*multiplier).ToList();
        var actual=Source().Select(v => v*multiplier).ToList();
        Assert.Equal(expected,actual);
    }
    private static void Select_selector_Delegate() {
        var expected=SystemEnumerable.Select(Source(),delegate(int v) { return v*2; }).ToList();
        var actual=Source().Select(delegate(int v) { return v*2; }).ToList();
        Assert.Equal(expected,actual);
    }
    private static void Select_selector_Variable() {
        Func<int,int> selector=v => v*2;
        var expected=SystemEnumerable.Select(Source(),selector).ToList();
        var actual=Source().Select(selector).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void Select_indexSelector() {
        Select_indexSelector_Static();
        Select_indexSelector_Lambda();
        Select_indexSelector_Captured();
        Select_indexSelector_Delegate();
        Select_indexSelector_Variable();
    }
    private static void Select_indexSelector_Static() {
        var expected=SystemEnumerable.Select(Source(),static (v,i) => v+i).ToList();
        var actual=Source().Select(static (v,i) => v+i).ToList();
        Assert.Equal(expected,actual);
    }
    private static void Select_indexSelector_Lambda() {
        var expected=SystemEnumerable.Select(Source(),(v,i) => v+i).ToList();
        var actual=Source().Select((v,i) => v+i).ToList();
        Assert.Equal(expected,actual);
    }
    private static void Select_indexSelector_Captured() {
        var offset=0;
        var expected=SystemEnumerable.Select(Source(),(v,i) => v+i+offset).ToList();
        var actual=Source().Select((v,i) => v+i+offset).ToList();
        Assert.Equal(expected,actual);
    }
    private static void Select_indexSelector_Delegate() {
        var expected=SystemEnumerable.Select(Source(),delegate(int v,int i) { return v+i; }).ToList();
        var actual=Source().Select(delegate(int v,int i) { return v+i; }).ToList();
        Assert.Equal(expected,actual);
    }
    private static void Select_indexSelector_Variable() {
        Func<int,int,int> selector=(v,i) => v+i;
        var expected=SystemEnumerable.Select(Source(),selector).ToList();
        var actual=Source().Select(selector).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void SelectMany() {
        SelectMany_selector_Static();
        SelectMany_selector_Lambda();
        SelectMany_selector_Captured();
        SelectMany_selector_Delegate();
        SelectMany_selector_Variable();
        SelectMany_selector_index_Static();
        SelectMany_selector_index_Lambda();
        SelectMany_selector_index_Captured();
        SelectMany_selector_index_Delegate();
        SelectMany_selector_index_Variable();
        SelectMany_collectionSelector_resultSelector_Static();
        SelectMany_collectionSelector_resultSelector_Lambda();
        SelectMany_collectionSelector_resultSelector_Captured();
        SelectMany_collectionSelector_resultSelector_Delegate();
        SelectMany_collectionSelector_resultSelector_Variable();
        SelectMany_collectionSelector_index_resultSelector_Static();
        SelectMany_collectionSelector_index_resultSelector_Lambda();
        SelectMany_collectionSelector_index_resultSelector_Captured();
        SelectMany_collectionSelector_index_resultSelector_Delegate();
        SelectMany_collectionSelector_index_resultSelector_Variable();
    }
    private static void SelectMany_selector_Static() {
        var expected=SystemEnumerable.SelectMany(Source(),static v => new[] { v,v+10 }).ToList();
        var actual=Source().SelectMany(static v => new[] { v,v+10 }).ToList();
        Assert.Equal(expected,actual);
    }
    private static void SelectMany_selector_Lambda() {
        var expected=SystemEnumerable.SelectMany(Source(),v => new[] { v,v+10 }).ToList();
        var actual=Source().SelectMany(v => new[] { v,v+10 }).ToList();
        Assert.Equal(expected,actual);
    }
    private static void SelectMany_selector_Captured() {
        var offset=10;
        var expected=SystemEnumerable.SelectMany(Source(),v => new[] { v,v+offset }).ToList();
        var actual=Source().SelectMany(v => new[] { v,v+offset }).ToList();
        Assert.Equal(expected,actual);
    }
    private static void SelectMany_selector_Delegate() {
        var expected=SystemEnumerable.SelectMany(Source(),delegate(int v) { return new[] { v,v+10 }; }).ToList();
        var actual=Source().SelectMany(delegate(int v) { return new[] { v,v+10 }; }).ToList();
        Assert.Equal(expected,actual);
    }
    private static void SelectMany_selector_Variable() {
        Func<int,int[]> selector=v => new[] { v,v+10 };
        var expected=SystemEnumerable.SelectMany(Source(),selector).ToList();
        var actual=Source().SelectMany(selector).ToList();
        Assert.Equal(expected,actual);
    }
    private static void SelectMany_selector_index_Static() {
        var expected=SystemEnumerable.SelectMany(Source(),static (v,i) => new[] { v,v+10 }).ToList();
        var actual=Source().SelectMany(static (v,i) => new[] { v,v+10 }).ToList();
        Assert.Equal(expected,actual);
    }
    private static void SelectMany_selector_index_Lambda() {
        var expected=SystemEnumerable.SelectMany(Source(),(v,i) => new[] { v,v+10 }).ToList();
        var actual=Source().SelectMany((v,i) => new[] { v,v+10 }).ToList();
        Assert.Equal(expected,actual);
    }
    private static void SelectMany_selector_index_Captured() {
        var offset=10;
        var expected=SystemEnumerable.SelectMany(Source(),(v,i) => new[] { v,v+offset }).ToList();
        var actual=Source().SelectMany((v,i) => new[] { v,v+offset }).ToList();
        Assert.Equal(expected,actual);
    }
    private static void SelectMany_selector_index_Delegate() {
        var expected=SystemEnumerable.SelectMany(Source(),delegate(int v,int i) { return new[] { v,v+10 }; }).ToList();
        var actual=Source().SelectMany(delegate(int v,int i) { return new[] { v,v+10 }; }).ToList();
        Assert.Equal(expected,actual);
    }
    private static void SelectMany_selector_index_Variable() {
        Func<int,int,int[]> selector=(v,i) => new[] { v,v+10 };
        var expected=SystemEnumerable.SelectMany(Source(),selector).ToList();
        var actual=Source().SelectMany(selector).ToList();
        Assert.Equal(expected,actual);
    }
    private static void SelectMany_collectionSelector_resultSelector_Static() {
        var expected=SystemEnumerable.SelectMany(Source(),static v => new[] { v,v+10 },static (v,sub) => sub).ToList();
        var actual=Source().SelectMany(static v => new[] { v,v+10 },static (v,sub) => sub).ToList();
        Assert.Equal(expected,actual);
    }
    private static void SelectMany_collectionSelector_resultSelector_Lambda() {
        var expected=SystemEnumerable.SelectMany(Source(),v => new[] { v,v+10 },(v,sub) => sub).ToList();
        var actual=Source().SelectMany(v => new[] { v,v+10 },(v,sub) => sub).ToList();
        Assert.Equal(expected,actual);
    }
    private static void SelectMany_collectionSelector_resultSelector_Captured() {
        var offset=10;
        var expected=SystemEnumerable.SelectMany(Source(),v => new[] { v,v+offset },(v,sub) => sub+offset).ToList();
        var actual=Source().SelectMany(v => new[] { v,v+offset },(v,sub) => sub+offset).ToList();
        Assert.Equal(expected,actual);
    }
    private static void SelectMany_collectionSelector_resultSelector_Delegate() {
        var expected=SystemEnumerable.SelectMany(Source(),delegate(int v) { return new[] { v,v+10 }; },delegate(int v,int sub) { return sub; }).ToList();
        var actual=Source().SelectMany(delegate(int v) { return new[] { v,v+10 }; },delegate(int v,int sub) { return sub; }).ToList();
        Assert.Equal(expected,actual);
    }
    private static void SelectMany_collectionSelector_resultSelector_Variable() {
        Func<int,int[]> collectionSelector=v => new[] { v,v+10 };
        Func<int,int,int> resultSelector=(v,sub) => sub;
        var expected=SystemEnumerable.SelectMany(Source(),collectionSelector,resultSelector).ToList();
        var actual=Source().SelectMany(collectionSelector,resultSelector).ToList();
        Assert.Equal(expected,actual);
    }
    private static void SelectMany_collectionSelector_index_resultSelector_Static() {
        var expected=SystemEnumerable.SelectMany(Source(),static (v,i) => new[] { v,v+10 },static (v,sub) => sub).ToList();
        var actual=Source().SelectMany(static (v,i) => new[] { v,v+10 },static (v,sub) => sub).ToList();
        Assert.Equal(expected,actual);
    }
    private static void SelectMany_collectionSelector_index_resultSelector_Lambda() {
        var expected=SystemEnumerable.SelectMany(Source(),(v,i) => new[] { v,v+10 },(v,sub) => sub).ToList();
        var actual=Source().SelectMany((v,i) => new[] { v,v+10 },(v,sub) => sub).ToList();
        Assert.Equal(expected,actual);
    }
    private static void SelectMany_collectionSelector_index_resultSelector_Captured() {
        var offset=10;
        var expected=SystemEnumerable.SelectMany(Source(),(v,i) => new[] { v,v+offset },(v,sub) => sub+offset).ToList();
        var actual=Source().SelectMany((v,i) => new[] { v,v+offset },(v,sub) => sub+offset).ToList();
        Assert.Equal(expected,actual);
    }
    private static void SelectMany_collectionSelector_index_resultSelector_Delegate() {
        var expected=SystemEnumerable.SelectMany(Source(),delegate(int v,int i) { return new[] { v,v+10 }; },delegate(int v,int sub) { return sub; }).ToList();
        var actual=Source().SelectMany(delegate(int v,int i) { return new[] { v,v+10 }; },delegate(int v,int sub) { return sub; }).ToList();
        Assert.Equal(expected,actual);
    }
    private static void SelectMany_collectionSelector_index_resultSelector_Variable() {
        Func<int,int,int[]> collectionSelector=(v,i) => new[] { v,v+10 };
        Func<int,int,int> resultSelector=(v,sub) => sub;
        var expected=SystemEnumerable.SelectMany(Source(),collectionSelector,resultSelector).ToList();
        var actual=Source().SelectMany(collectionSelector,resultSelector).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void Skip() {
        Skip_NoDelegate();
    }
    private static void Skip_NoDelegate() {
        var expected=SystemEnumerable.Skip(Source(),2).ToList();
        var actual=Source().Skip(2).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void SkipLast() {
        SkipLast_NoDelegate();
    }
    private static void SkipLast_NoDelegate() {
        var expected=SystemEnumerable.SkipLast(Source(),1).ToList();
        var actual=Source().SkipLast(1).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void SkipWhile_predicate() {
        SkipWhile_predicate_Static();
        SkipWhile_predicate_Lambda();
        SkipWhile_predicate_Captured();
        SkipWhile_predicate_Delegate();
        SkipWhile_predicate_Variable();
    }
    private static void SkipWhile_predicate_Static() {
        var expected=SystemEnumerable.SkipWhile(Source(),static v => v<3).ToList();
        var actual=Source().SkipWhile(static v => v<3).ToList();
        Assert.Equal(expected,actual);
    }
    private static void SkipWhile_predicate_Lambda() {
        var expected=SystemEnumerable.SkipWhile(Source(),v => v<3).ToList();
        var actual=Source().SkipWhile(v => v<3).ToList();
        Assert.Equal(expected,actual);
    }
    private static void SkipWhile_predicate_Captured() {
        var threshold=3;
        var expected=SystemEnumerable.SkipWhile(Source(),v => v<threshold).ToList();
        var actual=Source().SkipWhile(v => v<threshold).ToList();
        Assert.Equal(expected,actual);
    }
    private static void SkipWhile_predicate_Delegate() {
        var expected=SystemEnumerable.SkipWhile(Source(),delegate(int v) { return v<3; }).ToList();
        var actual=Source().SkipWhile(delegate(int v) { return v<3; }).ToList();
        Assert.Equal(expected,actual);
    }
    private static void SkipWhile_predicate_Variable() {
        Func<int,bool> predicate=v => v<3;
        var expected=SystemEnumerable.SkipWhile(Source(),predicate).ToList();
        var actual=Source().SkipWhile(predicate).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void SkipWhile_indexPredicate() {
        SkipWhile_indexPredicate_Static();
        SkipWhile_indexPredicate_Lambda();
        SkipWhile_indexPredicate_Captured();
        SkipWhile_indexPredicate_Delegate();
        SkipWhile_indexPredicate_Variable();
    }
    private static void SkipWhile_indexPredicate_Static() {
        var expected=SystemEnumerable.SkipWhile(Source(),static (v,i) => i<2).ToList();
        var actual=Source().SkipWhile(static (v,i) => i<2).ToList();
        Assert.Equal(expected,actual);
    }
    private static void SkipWhile_indexPredicate_Lambda() {
        var expected=SystemEnumerable.SkipWhile(Source(),(v,i) => i<2).ToList();
        var actual=Source().SkipWhile((v,i) => i<2).ToList();
        Assert.Equal(expected,actual);
    }
    private static void SkipWhile_indexPredicate_Captured() {
        var threshold=2;
        var expected=SystemEnumerable.SkipWhile(Source(),(v,i) => i<threshold).ToList();
        var actual=Source().SkipWhile((v,i) => i<threshold).ToList();
        Assert.Equal(expected,actual);
    }
    private static void SkipWhile_indexPredicate_Delegate() {
        var expected=SystemEnumerable.SkipWhile(Source(),delegate(int v,int i) { return i<2; }).ToList();
        var actual=Source().SkipWhile(delegate(int v,int i) { return i<2; }).ToList();
        Assert.Equal(expected,actual);
    }
    private static void SkipWhile_indexPredicate_Variable() {
        Func<int,int,bool> predicate=(v,i) => i<2;
        var expected=SystemEnumerable.SkipWhile(Source(),predicate).ToList();
        var actual=Source().SkipWhile(predicate).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void Take() {
        Take_NoDelegate();
    }
    private static void Take_NoDelegate() {
        var expected=SystemEnumerable.Take(Source(),3).ToList();
        var actual=Source().Take(3).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void TakeLast() {
        TakeLast_NoDelegate();
    }
    private static void TakeLast_NoDelegate() {
        var expected=SystemEnumerable.TakeLast(Source(),2).ToList();
        var actual=Source().TakeLast(2).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void TakeWhile_predicate() {
        TakeWhile_predicate_Static();
        TakeWhile_predicate_Lambda();
        TakeWhile_predicate_Captured();
        TakeWhile_predicate_Delegate();
        TakeWhile_predicate_Variable();
    }
    private static void TakeWhile_predicate_Static() {
        var expected=SystemEnumerable.TakeWhile(Source(),static v => v<3).ToList();
        var actual=Source().TakeWhile(static v => v<3).ToList();
        Assert.Equal(expected,actual);
    }
    private static void TakeWhile_predicate_Lambda() {
        var expected=SystemEnumerable.TakeWhile(Source(),v => v<3).ToList();
        var actual=Source().TakeWhile(v => v<3).ToList();
        Assert.Equal(expected,actual);
    }
    private static void TakeWhile_predicate_Captured() {
        var threshold=3;
        var expected=SystemEnumerable.TakeWhile(Source(),v => v<threshold).ToList();
        var actual=Source().TakeWhile(v => v<threshold).ToList();
        Assert.Equal(expected,actual);
    }
    private static void TakeWhile_predicate_Delegate() {
        var expected=SystemEnumerable.TakeWhile(Source(),delegate(int v) { return v<3; }).ToList();
        var actual=Source().TakeWhile(delegate(int v) { return v<3; }).ToList();
        Assert.Equal(expected,actual);
    }
    private static void TakeWhile_predicate_Variable() {
        Func<int,bool> predicate=v => v<3;
        var expected=SystemEnumerable.TakeWhile(Source(),predicate).ToList();
        var actual=Source().TakeWhile(predicate).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void TakeWhile_indexPredicate() {
        TakeWhile_indexPredicate_Static();
        TakeWhile_indexPredicate_Lambda();
        TakeWhile_indexPredicate_Captured();
        TakeWhile_indexPredicate_Delegate();
        TakeWhile_indexPredicate_Variable();
    }
    private static void TakeWhile_indexPredicate_Static() {
        var expected=SystemEnumerable.TakeWhile(Source(),static (v,i) => i<2).ToList();
        var actual=Source().TakeWhile(static (v,i) => i<2).ToList();
        Assert.Equal(expected,actual);
    }
    private static void TakeWhile_indexPredicate_Lambda() {
        var expected=SystemEnumerable.TakeWhile(Source(),(v,i) => i<2).ToList();
        var actual=Source().TakeWhile((v,i) => i<2).ToList();
        Assert.Equal(expected,actual);
    }
    private static void TakeWhile_indexPredicate_Captured() {
        var threshold=2;
        var expected=SystemEnumerable.TakeWhile(Source(),(v,i) => i<threshold).ToList();
        var actual=Source().TakeWhile((v,i) => i<threshold).ToList();
        Assert.Equal(expected,actual);
    }
    private static void TakeWhile_indexPredicate_Delegate() {
        var expected=SystemEnumerable.TakeWhile(Source(),delegate(int v,int i) { return i<2; }).ToList();
        var actual=Source().TakeWhile(delegate(int v,int i) { return i<2; }).ToList();
        Assert.Equal(expected,actual);
    }
    private static void TakeWhile_indexPredicate_Variable() {
        Func<int,int,bool> predicate=(v,i) => i<2;
        var expected=SystemEnumerable.TakeWhile(Source(),predicate).ToList();
        var actual=Source().TakeWhile(predicate).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void ThenBy() {
        ThenBy_Static();
        ThenBy_Lambda();
        ThenBy_Captured();
        ThenBy_Delegate();
        ThenBy_Variable();
    }
    private static void ThenBy_Static() {
        var expected=SystemEnumerable.ThenBy(SystemEnumerable.OrderBy(Rows(),row => row.Key%2),static row => row.Key).ToList();
        var actual=Rows().OrderBy(row => row.Key%2).ThenBy(static row => row.Key).ToList();
        Assert.Equal(expected,actual);
    }
    private static void ThenBy_Lambda() {
        var expected=SystemEnumerable.ThenBy(SystemEnumerable.OrderBy(Rows(),row => row.Key%2),row => row.Key).ToList();
        var actual=Rows().OrderBy(row => row.Key%2).ThenBy(row => row.Key).ToList();
        Assert.Equal(expected,actual);
    }
    private static void ThenBy_Captured() {
        var offset=0;
        var expected=SystemEnumerable.ThenBy(SystemEnumerable.OrderBy(Rows(),row => row.Key%2),row => row.Key+offset).ToList();
        var actual=Rows().OrderBy(row => row.Key%2).ThenBy(row => row.Key+offset).ToList();
        Assert.Equal(expected,actual);
    }
    private static void ThenBy_Delegate() {
        var expected=SystemEnumerable.ThenBy(SystemEnumerable.OrderBy(Rows(),row => row.Key%2),delegate(Row row) { return row.Key; }).ToList();
        var actual=Rows().OrderBy(row => row.Key%2).ThenBy(delegate(Row row) { return row.Key; }).ToList();
        Assert.Equal(expected,actual);
    }
    private static void ThenBy_Variable() {
        Func<Row,int> keySelector=row => row.Key;
        var expected=SystemEnumerable.ThenBy(SystemEnumerable.OrderBy(Rows(),row => row.Key%2),keySelector).ToList();
        var actual=Rows().OrderBy(row => row.Key%2).ThenBy(keySelector).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void ThenByDescending() {
        ThenByDescending_Static();
        ThenByDescending_Lambda();
        ThenByDescending_Captured();
        ThenByDescending_Delegate();
        ThenByDescending_Variable();
    }
    private static void ThenByDescending_Static() {
        var expected=SystemEnumerable.ThenByDescending(SystemEnumerable.OrderBy(Rows(),row => row.Key%2),static row => row.Key).ToList();
        var actual=Rows().OrderBy(row => row.Key%2).ThenByDescending(static row => row.Key).ToList();
        Assert.Equal(expected,actual);
    }
    private static void ThenByDescending_Lambda() {
        var expected=SystemEnumerable.ThenByDescending(SystemEnumerable.OrderBy(Rows(),row => row.Key%2),row => row.Key).ToList();
        var actual=Rows().OrderBy(row => row.Key%2).ThenByDescending(row => row.Key).ToList();
        Assert.Equal(expected,actual);
    }
    private static void ThenByDescending_Captured() {
        var offset=0;
        var expected=SystemEnumerable.ThenByDescending(SystemEnumerable.OrderBy(Rows(),row => row.Key%2),row => row.Key+offset).ToList();
        var actual=Rows().OrderBy(row => row.Key%2).ThenByDescending(row => row.Key+offset).ToList();
        Assert.Equal(expected,actual);
    }
    private static void ThenByDescending_Delegate() {
        var expected=SystemEnumerable.ThenByDescending(SystemEnumerable.OrderBy(Rows(),row => row.Key%2),delegate(Row row) { return row.Key; }).ToList();
        var actual=Rows().OrderBy(row => row.Key%2).ThenByDescending(delegate(Row row) { return row.Key; }).ToList();
        Assert.Equal(expected,actual);
    }
    private static void ThenByDescending_Variable() {
        Func<Row,int> keySelector=row => row.Key;
        var expected=SystemEnumerable.ThenByDescending(SystemEnumerable.OrderBy(Rows(),row => row.Key%2),keySelector).ToList();
        var actual=Rows().OrderBy(row => row.Key%2).ThenByDescending(keySelector).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void ToArray() {
        ToArray_NoDelegate();
    }
    private static void ToArray_NoDelegate() {
        var expected=SystemEnumerable.ToArray(Source());
        var actual=Source().ToArray();
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void ToDictionary_keySelector() {
        ToDictionary_keySelector_Static();
        ToDictionary_keySelector_Lambda();
        ToDictionary_keySelector_Captured();
        ToDictionary_keySelector_Delegate();
        ToDictionary_keySelector_Variable();
    }
    private static void ToDictionary_keySelector_Static() {
        var expected=SystemEnumerable.ToDictionary(Rows(),static row => row.Key);
        var actual=Rows().ToDictionary(static row => row.Key);
        Assert.Equal(expected,actual);
    }
    private static void ToDictionary_keySelector_Lambda() {
        var expected=SystemEnumerable.ToDictionary(Rows(),row => row.Key);
        var actual=Rows().ToDictionary(row => row.Key);
        Assert.Equal(expected,actual);
    }
    private static void ToDictionary_keySelector_Captured() {
        var offset=0;
        var expected=SystemEnumerable.ToDictionary(Rows(),row => row.Key+offset);
        var actual=Rows().ToDictionary(row => row.Key+offset);
        Assert.Equal(expected,actual);
    }
    private static void ToDictionary_keySelector_Delegate() {
        var expected=SystemEnumerable.ToDictionary(Rows(),delegate(Row row) { return row.Key; });
        var actual=Rows().ToDictionary(delegate(Row row) { return row.Key; });
        Assert.Equal(expected,actual);
    }
    private static void ToDictionary_keySelector_Variable() {
        Func<Row,int> keySelector=row => row.Key;
        var expected=SystemEnumerable.ToDictionary(Rows(),keySelector);
        var actual=Rows().ToDictionary(keySelector);
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void ToDictionary_keySelector_elementSelector() {
        ToDictionary_keySelector_elementSelector_Static();
        ToDictionary_keySelector_elementSelector_Lambda();
        ToDictionary_keySelector_elementSelector_Captured();
        ToDictionary_keySelector_elementSelector_Delegate();
        ToDictionary_keySelector_elementSelector_Variable();
    }
    private static void ToDictionary_keySelector_elementSelector_Static() {
        var expected=SystemEnumerable.ToDictionary(Rows(),static row => row.Key,static row => row.Value);
        var actual=Rows().ToDictionary(static row => row.Key,static row => row.Value);
        Assert.Equal(expected,actual);
    }
    private static void ToDictionary_keySelector_elementSelector_Lambda() {
        var expected=SystemEnumerable.ToDictionary(Rows(),row => row.Key,row => row.Value);
        var actual=Rows().ToDictionary(row => row.Key,row => row.Value);
        Assert.Equal(expected,actual);
    }
    private static void ToDictionary_keySelector_elementSelector_Captured() {
        var offset=0;
        var expected=SystemEnumerable.ToDictionary(Rows(),row => row.Key+offset,row => row.Value+offset);
        var actual=Rows().ToDictionary(row => row.Key+offset,row => row.Value+offset);
        Assert.Equal(expected,actual);
    }
    private static void ToDictionary_keySelector_elementSelector_Delegate() {
        var expected=SystemEnumerable.ToDictionary(Rows(),delegate(Row row) { return row.Key; },delegate(Row row) { return row.Value; });
        var actual=Rows().ToDictionary(delegate(Row row) { return row.Key; },delegate(Row row) { return row.Value; });
        Assert.Equal(expected,actual);
    }
    private static void ToDictionary_keySelector_elementSelector_Variable() {
        Func<Row,int> keySelector=row => row.Key;
        Func<Row,int> elementSelector=row => row.Value;
        var expected=SystemEnumerable.ToDictionary(Rows(),keySelector,elementSelector);
        var actual=Rows().ToDictionary(keySelector,elementSelector);
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void ToHashSet() {
        ToHashSet_NoDelegate();
    }
    private static void ToHashSet_NoDelegate() {
        var expected=SystemEnumerable.ToHashSet(Duplicates()).OrderBy(v => v).ToList();
        var actual=Duplicates().ToHashSet().OrderBy(v => v).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void ToList() {
        ToList_NoDelegate();
    }
    private static void ToList_NoDelegate() {
        var expected=SystemEnumerable.ToList(Source());
        var actual=Source().ToList();
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void ToLookup_keySelector() {
        ToLookup_keySelector_Static();
        ToLookup_keySelector_Lambda();
        ToLookup_keySelector_Captured();
        ToLookup_keySelector_Delegate();
        ToLookup_keySelector_Variable();
    }
    private static void ToLookup_keySelector_Static() {
        var expected=SystemEnumerable.ToLookup(Source(),static v => v%2).OrderBy(g => g.Key).SelectMany(g => g).ToList();
        var actual=Source().ToLookup(static v => v%2).OrderBy(g => g.Key).SelectMany(g => g).ToList();
        Assert.Equal(expected,actual);
    }
    private static void ToLookup_keySelector_Lambda() {
        var expected=SystemEnumerable.ToLookup(Source(),v => v%2).OrderBy(g => g.Key).SelectMany(g => g).ToList();
        var actual=Source().ToLookup(v => v%2).OrderBy(g => g.Key).SelectMany(g => g).ToList();
        Assert.Equal(expected,actual);
    }
    private static void ToLookup_keySelector_Captured() {
        var mod=2;
        var expected=SystemEnumerable.ToLookup(Source(),v => v%mod).OrderBy(g => g.Key).SelectMany(g => g).ToList();
        var actual=Source().ToLookup(v => v%mod).OrderBy(g => g.Key).SelectMany(g => g).ToList();
        Assert.Equal(expected,actual);
    }
    private static void ToLookup_keySelector_Delegate() {
        var expected=SystemEnumerable.ToLookup(Source(),delegate(int v) { return v%2; }).OrderBy(g => g.Key).SelectMany(g => g).ToList();
        var actual=Source().ToLookup(delegate(int v) { return v%2; }).OrderBy(g => g.Key).SelectMany(g => g).ToList();
        Assert.Equal(expected,actual);
    }
    private static void ToLookup_keySelector_Variable() {
        Func<int,int> keySelector=v => v%2;
        var expected=SystemEnumerable.ToLookup(Source(),keySelector).OrderBy(g => g.Key).SelectMany(g => g).ToList();
        var actual=Source().ToLookup(keySelector).OrderBy(g => g.Key).SelectMany(g => g).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void ToLookup_keySelector_elementSelector() {
        ToLookup_keySelector_elementSelector_Static();
        ToLookup_keySelector_elementSelector_Lambda();
        ToLookup_keySelector_elementSelector_Captured();
        ToLookup_keySelector_elementSelector_Delegate();
        ToLookup_keySelector_elementSelector_Variable();
    }
    private static void ToLookup_keySelector_elementSelector_Static() {
        var expected=SystemEnumerable.ToLookup(Rows(),static row => row.Key%2,static row => row.Value).OrderBy(g => g.Key).SelectMany(g => g).ToList();
        var actual=Rows().ToLookup(static row => row.Key%2,static row => row.Value).OrderBy(g => g.Key).SelectMany(g => g).ToList();
        Assert.Equal(expected,actual);
    }
    private static void ToLookup_keySelector_elementSelector_Lambda() {
        var expected=SystemEnumerable.ToLookup(Rows(),row => row.Key%2,row => row.Value).OrderBy(g => g.Key).SelectMany(g => g).ToList();
        var actual=Rows().ToLookup(row => row.Key%2,row => row.Value).OrderBy(g => g.Key).SelectMany(g => g).ToList();
        Assert.Equal(expected,actual);
    }
    private static void ToLookup_keySelector_elementSelector_Captured() {
        var offset=0;
        var expected=SystemEnumerable.ToLookup(Rows(),row => row.Key%2,row => row.Value+offset).OrderBy(g => g.Key).SelectMany(g => g).ToList();
        var actual=Rows().ToLookup(row => row.Key%2,row => row.Value+offset).OrderBy(g => g.Key).SelectMany(g => g).ToList();
        Assert.Equal(expected,actual);
    }
    private static void ToLookup_keySelector_elementSelector_Delegate() {
        var expected=SystemEnumerable.ToLookup(Rows(),delegate(Row row) { return row.Key%2; },delegate(Row row) { return row.Value; }).OrderBy(g => g.Key).SelectMany(g => g).ToList();
        var actual=Rows().ToLookup(delegate(Row row) { return row.Key%2; },delegate(Row row) { return row.Value; }).OrderBy(g => g.Key).SelectMany(g => g).ToList();
        Assert.Equal(expected,actual);
    }
    private static void ToLookup_keySelector_elementSelector_Variable() {
        Func<Row,int> keySelector=row => row.Key%2;
        Func<Row,int> elementSelector=row => row.Value;
        var expected=SystemEnumerable.ToLookup(Rows(),keySelector,elementSelector).OrderBy(g => g.Key).SelectMany(g => g).ToList();
        var actual=Rows().ToLookup(keySelector,elementSelector).OrderBy(g => g.Key).SelectMany(g => g).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void Union() {
        Union_NoDelegate();
    }
    private static void Union_NoDelegate() {
        var expected=Source().Union(Second()).ToList();
        var actual=Source().Union(Second(),EqualityComparer<int>.Default).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void UnionBy() {
        UnionBy_Static();
        UnionBy_Lambda();
        UnionBy_Captured();
        UnionBy_Delegate();
        UnionBy_Variable();
    }
    private static void UnionBy_Static() {
        var expected=SystemEnumerable.UnionBy(Source(),Second(),static v => v).ToList();
        var actual=Source().UnionBy(Second(),static v => v).ToList();
        Assert.Equal(expected,actual);
    }
    private static void UnionBy_Lambda() {
        var expected=SystemEnumerable.UnionBy(Source(),Second(),v => v).ToList();
        var actual=Source().UnionBy(Second(),v => v).ToList();
        Assert.Equal(expected,actual);
    }
    private static void UnionBy_Captured() {
        var offset=0;
        var expected=SystemEnumerable.UnionBy(Source(),Second(),v => v+offset).ToList();
        var actual=Source().UnionBy(Second(),v => v+offset).ToList();
        Assert.Equal(expected,actual);
    }
    private static void UnionBy_Delegate() {
        var expected=SystemEnumerable.UnionBy(Source(),Second(),delegate(int v) { return v; }).ToList();
        var actual=Source().UnionBy(Second(),delegate(int v) { return v; }).ToList();
        Assert.Equal(expected,actual);
    }
    private static void UnionBy_Variable() {
        Func<int,int> keySelector=v => v;
        var expected=SystemEnumerable.UnionBy(Source(),Second(),keySelector).ToList();
        var actual=Source().UnionBy(Second(),keySelector).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void Where_predicate() {
        Where_predicate_Static();
        Where_predicate_Lambda();
        Where_predicate_Captured();
        Where_predicate_Delegate();
        Where_predicate_Variable();
    }
    private static void Where_predicate_Static() {
        var expected=SystemEnumerable.Where(Source(),static v => v>2).ToList();
        var actual=Source().Where(static v => v>2).ToList();
        Assert.Equal(expected,actual);
    }
    private static void Where_predicate_Lambda() {
        var expected=SystemEnumerable.Where(Source(),v => v>2).ToList();
        var actual=Source().Where(v => v>2).ToList();
        Assert.Equal(expected,actual);
    }
    private static void Where_predicate_Captured() {
        var threshold=2;
        var expected=SystemEnumerable.Where(Source(),v => v>threshold).ToList();
        var actual=Source().Where(v => v>threshold).ToList();
        Assert.Equal(expected,actual);
    }
    private static void Where_predicate_Delegate() {
        var expected=SystemEnumerable.Where(Source(),delegate(int v) { return v>2; }).ToList();
        var actual=Source().Where(delegate(int v) { return v>2; }).ToList();
        Assert.Equal(expected,actual);
    }
    private static void Where_predicate_Variable() {
        Func<int,bool> predicate=v => v>2;
        var expected=SystemEnumerable.Where(Source(),predicate).ToList();
        var actual=Source().Where(predicate).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void Where_indexPredicate() {
        Where_indexPredicate_Static();
        Where_indexPredicate_Lambda();
        Where_indexPredicate_Captured();
        Where_indexPredicate_Delegate();
        Where_indexPredicate_Variable();
    }
    private static void Where_indexPredicate_Static() {
        var expected=SystemEnumerable.Where(Source(),static (v,i) => v>2&&i>0).ToList();
        var actual=Source().Where(static (v,i) => v>2&&i>0).ToList();
        Assert.Equal(expected,actual);
    }
    private static void Where_indexPredicate_Lambda() {
        var expected=SystemEnumerable.Where(Source(),(v,i) => v>2&&i>0).ToList();
        var actual=Source().Where((v,i) => v>2&&i>0).ToList();
        Assert.Equal(expected,actual);
    }
    private static void Where_indexPredicate_Captured() {
        var threshold=2;
        var expected=SystemEnumerable.Where(Source(),(v,i) => v>threshold&&i>0).ToList();
        var actual=Source().Where((v,i) => v>threshold&&i>0).ToList();
        Assert.Equal(expected,actual);
    }
    private static void Where_indexPredicate_Delegate() {
        var expected=SystemEnumerable.Where(Source(),delegate(int v,int i) { return v>2&&i>0; }).ToList();
        var actual=Source().Where(delegate(int v,int i) { return v>2&&i>0; }).ToList();
        Assert.Equal(expected,actual);
    }
    private static void Where_indexPredicate_Variable() {
        Func<int,int,bool> predicate=(v,i) => v>2&&i>0;
        var expected=SystemEnumerable.Where(Source(),predicate).ToList();
        var actual=Source().Where(predicate).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void Zip_resultSelector() {
        Zip_resultSelector_Static();
        Zip_resultSelector_Lambda();
        Zip_resultSelector_Captured();
        Zip_resultSelector_Delegate();
        Zip_resultSelector_Variable();
    }
    private static void Zip_resultSelector_Static() {
        var expected=SystemEnumerable.Zip(Source(),Second(),static (a,b) => a+b).ToList();
        var actual=Source().Zip(Second(),static (a,b) => a+b).ToList();
        Assert.Equal(expected,actual);
    }
    private static void Zip_resultSelector_Lambda() {
        var expected=SystemEnumerable.Zip(Source(),Second(),(a,b) => a+b).ToList();
        var actual=Source().Zip(Second(),(a,b) => a+b).ToList();
        Assert.Equal(expected,actual);
    }
    private static void Zip_resultSelector_Captured() {
        var offset=0;
        var expected=SystemEnumerable.Zip(Source(),Second(),(a,b) => a+b+offset).ToList();
        var actual=Source().Zip(Second(),(a,b) => a+b+offset).ToList();
        Assert.Equal(expected,actual);
    }
    private static void Zip_resultSelector_Delegate() {
        var expected=SystemEnumerable.Zip(Source(),Second(),delegate(int a,int b) { return a+b; }).ToList();
        var actual=Source().Zip(Second(),delegate(int a,int b) { return a+b; }).ToList();
        Assert.Equal(expected,actual);
    }
    private static void Zip_resultSelector_Variable() {
        Func<int,int,int> resultSelector=(a,b) => a+b;
        var expected=SystemEnumerable.Zip(Source(),Second(),resultSelector).ToList();
        var actual=Source().Zip(Second(),resultSelector).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void Zip_tuple() {
        Zip_tuple_NoDelegate();
    }
    private static void Zip_tuple_NoDelegate() {
        var expected=SystemEnumerable.Zip(Source(),Second()).ToList();
        var actual=Source().Zip(Second()).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void Zip_triple() {
        Zip_triple_NoDelegate();
    }
    private static void Zip_triple_NoDelegate() {
        var expected=SystemEnumerable.Zip(Source(),Second(),Source()).ToList();
        var actual=Source().Zip(Second(),Source()).ToList();
        Assert.Equal(expected,actual);
    }

    public int[] InstanceSource() => [1,2,3,4];
    public static int[] Source() => [1,2,3,4];
    public static int[] Second() => [3,4,5];
    public static int[] Duplicates() => [1,2,2,3,3,4];
    public static NonGenericEnumerable CastObjects() => new object[] { 1,2,3 };
    public static Row[] Rows() => [new(1,10,"beta"),new(2,20,"alpha"),new(3,30,"gamma")];

    public sealed record Row(int Key,int Value,string Name);
}
