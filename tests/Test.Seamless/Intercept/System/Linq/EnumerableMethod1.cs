using System.Linq;
using NonGenericEnumerable = System.Collections.IEnumerable;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq;

public sealed class EnumerableMethod1 {
    [Fact,Trait("Category","Unit")]
    public void LinqWhereSelectStringJoin() => //this.LinqWhereSelectStringJoinImpl();
    string.Join(",",LinqWhereSelectStringJoinImpl());
    public IEnumerable<string> LinqWhereSelectStringJoinImpl() => new[]{"abc"}.Where(x => x!=null).Select(x => x.ToString());
    [Fact,Trait("Category","Unit")]
    public void LinqWhereSelectStringJoin2() => //this.LinqWhereSelectStringJoinImpl();
    LinqWhereSelectStringJoinImpl2();
    public IEnumerable<string> LinqWhereSelectStringJoinImpl2() => new[]{"abc"}.Where(x => x!=null).Select(x => x.ToString());
    [Fact,Trait("Category","Unit")]
    public void InstanceAggregate() {
        this.InstanceAggregate_func_Static();
    }
    private void InstanceAggregate_func_Static() {
        var expected=SystemEnumerable.Aggregate(this.InstanceSource(),static (l,r) => l+r);
        var actual=this.InstanceSource().Aggregate(static (l,r) => l+r);
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public static void Aggregate_func() {
        Aggregate_func_Static();
        Aggregate_func_Lambda();
        Aggregate_func_Captured();
        Aggregate_func_DelegateVariable();
        Aggregate_func_Delegate();
        Aggregate_func_DelegateTryCatch();
    }
    private static void Aggregate_func_Static() {
        var expected=SystemEnumerable.Aggregate(Source(),static (l,r) => l+r);
        var actual=Source().Aggregate(static (l,r) => l+r);
        Assert.Equal(expected,actual);
    }
    private static void Aggregate_func_Lambda() {
        var expected=SystemEnumerable.Aggregate(Source(),(l,r) => l+r);
        var actual=Source().Aggregate((l,r) => l+r);
        Assert.Equal(expected,actual);
    }
    private static void Aggregate_func_Captured() {
        var offset=0;
        var expected=SystemEnumerable.Aggregate(Source(),(l,r) => l+r+offset);
        var actual=Source().Aggregate((l,r) => l+r+offset);
        Assert.Equal(expected,actual);
    }
    private static void Aggregate_func_DelegateVariable() {
        Func<int,int,int> func=(l,r) => l+r;
        var expected=SystemEnumerable.Aggregate(Source(),func);
        var actual=Source().Aggregate(func);
        Assert.Equal(expected,actual);
    }
    private static void Aggregate_func_Delegate() {
        var expected=SystemEnumerable.Aggregate(Source(),delegate(int l,int r) { return l+r; });
        var actual=Source().Aggregate(delegate(int l,int r) { return l+r; });
        Assert.Equal(expected,actual);
    }
    private static void Aggregate_func_DelegateTryCatch() {
        var expected=SystemEnumerable.Aggregate(Source(),delegate(int l,int r) { try { return l+r; } catch { return 0; } });
        var actual=Source().Aggregate(delegate(int l,int r) { try { return l+r; } catch { return 0; } });
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void Aggregate_seed_func() {
        Aggregate_seed_func_Static();
        Aggregate_seed_func_Lambda();
        Aggregate_seed_func_Captured();
        Aggregate_seed_func_DelegateVariable();
        Aggregate_seed_func_Delegate();
        Aggregate_seed_func_DelegateTryCatch();
    }
    private static void Aggregate_seed_func_Static() {
        var expected=SystemEnumerable.Aggregate(Source(),0,static (sum,v) => sum+v);
        var actual=Source().Aggregate(0,static (sum,v) => sum+v);
        Assert.Equal(expected,actual);
    }
    private static void Aggregate_seed_func_Lambda() {
        var expected=SystemEnumerable.Aggregate(Source(),0,(sum,v) => sum+v);
        var actual=Source().Aggregate(0,(sum,v) => sum+v);
        Assert.Equal(expected,actual);
    }
    private static void Aggregate_seed_func_Captured() {
        var offset=0;
        var expected=SystemEnumerable.Aggregate(Source(),0,(sum,v) => sum+v+offset);
        var actual=Source().Aggregate(0,(sum,v) => sum+v+offset);
        Assert.Equal(expected,actual);
    }
    private static void Aggregate_seed_func_DelegateVariable() {
        Func<int,int,int> func=(sum,v) => sum+v;
        var expected=SystemEnumerable.Aggregate(Source(),0,func);
        var actual=Source().Aggregate(0,func);
        Assert.Equal(expected,actual);
    }
    private static void Aggregate_seed_func_Delegate() {
        var expected=SystemEnumerable.Aggregate(Source(),0,delegate(int sum,int v) { return sum+v; });
        var actual=Source().Aggregate(0,delegate(int sum,int v) { return sum+v; });
        Assert.Equal(expected,actual);
    }
    private static void Aggregate_seed_func_DelegateTryCatch() {
        var expected=SystemEnumerable.Aggregate(Source(),0,delegate(int sum,int v) { try { return sum+v; } catch { return 0; } });
        var actual=Source().Aggregate(0,delegate(int sum,int v) { try { return sum+v; } catch { return 0; } });
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void Aggregate_seed_func_resultSelector() {
        Aggregate_seed_func_resultSelector_Scalar_Static();
        Aggregate_seed_func_resultSelector_Scalar_Lambda();
        Aggregate_seed_func_resultSelector_Scalar_Captured();
        Aggregate_seed_func_resultSelector_Scalar_DelegateVariable();
        Aggregate_seed_func_resultSelector_Scalar_Delegate();
        Aggregate_seed_func_resultSelector_Scalar_DelegateTryCatch();
        Aggregate_seed_func_resultSelector_ValueTuple_Static();
        Aggregate_seed_func_resultSelector_ValueTuple_Lambda();
        Aggregate_seed_func_resultSelector_ValueTuple_Captured();
        Aggregate_seed_func_resultSelector_ValueTuple_DelegateVariable();
        Aggregate_seed_func_resultSelector_ValueTuple_Delegate();
        Aggregate_seed_func_resultSelector_ValueTuple_DelegateTryCatch();
    }
    private static void Aggregate_seed_func_resultSelector_Scalar_Static() {
        var expected=SystemEnumerable.Aggregate(Source(),0,static (sum,v) => sum+v,static sum => sum*2);
        var actual=Source().Aggregate(0,static (sum,v) => sum+v,static sum => sum*2);
        Assert.Equal(expected,actual);
    }
    private static void Aggregate_seed_func_resultSelector_Scalar_Lambda() {
        var expected=SystemEnumerable.Aggregate(Source(),0,(sum,v) => sum+v,sum => sum*2);
        var actual=Source().Aggregate(0,(sum,v) => sum+v,sum => sum*2);
        Assert.Equal(expected,actual);
    }
    private static void Aggregate_seed_func_resultSelector_Scalar_Captured() {
        var offset=0;
        var expected=SystemEnumerable.Aggregate(Source(),0,(sum,v) => sum+v+offset,sum => sum*2+offset);
        var actual=Source().Aggregate(0,(sum,v) => sum+v+offset,sum => sum*2+offset);
        Assert.Equal(expected,actual);
    }
    private static void Aggregate_seed_func_resultSelector_Scalar_DelegateVariable() {
        Func<int,int,int> func=(sum,v) => sum+v;
        Func<int,int> scalarSelector=sum => sum*2;
        var expected=SystemEnumerable.Aggregate(Source(),0,func,scalarSelector);
        var actual=Source().Aggregate(0,func,scalarSelector);
        Assert.Equal(expected,actual);
    }
    private static void Aggregate_seed_func_resultSelector_Scalar_Delegate() {
        var expected=SystemEnumerable.Aggregate(Source(),0,delegate(int sum,int v) { return sum+v; },delegate(int sum) { return sum*2; });
        var actual=Source().Aggregate(0,delegate(int sum,int v) { return sum+v; },delegate(int sum) { return sum*2; });
        Assert.Equal(expected,actual);
    }
    private static void Aggregate_seed_func_resultSelector_Scalar_DelegateTryCatch() {
        var expected=SystemEnumerable.Aggregate(Source(),0,delegate(int sum,int v) { try { return sum+v; } catch { return 0; } },delegate(int sum) { try { return sum*2; } catch { return 0; } });
        var actual=Source().Aggregate(0,delegate(int sum,int v) { try { return sum+v; } catch { return 0; } },delegate(int sum) { try { return sum*2; } catch { return 0; } });
        Assert.Equal(expected,actual);
    }
    private static void Aggregate_seed_func_resultSelector_ValueTuple_Static() {
        var expected=SystemEnumerable.Aggregate(Source(),0,static (sum,v) => sum+v,static sum => (sum,sum*2));
        var actual=Source().Aggregate(0,static (sum,v) => sum+v,static sum => (sum,sum*2));
        Assert.Equal(expected,actual);
    }
    private static void Aggregate_seed_func_resultSelector_ValueTuple_Lambda() {
        var expected=SystemEnumerable.Aggregate(Source(),0,(sum,v) => sum+v,sum => (sum,sum*2));
        var actual=Source().Aggregate(0,(sum,v) => sum+v,sum => (sum,sum*2));
        Assert.Equal(expected,actual);
    }
    private static void Aggregate_seed_func_resultSelector_ValueTuple_Captured() {
        var offset=0;
        var expected=SystemEnumerable.Aggregate(Source(),0,(sum,v) => sum+v+offset,sum => (sum,sum*2+offset));
        var actual=Source().Aggregate(0,(sum,v) => sum+v+offset,sum => (sum,sum*2+offset));
        Assert.Equal(expected,actual);
    }
    private static void Aggregate_seed_func_resultSelector_ValueTuple_DelegateVariable() {
        Func<int,int,int> func=(sum,v) => sum+v;
        var expected=SystemEnumerable.Aggregate(Source(),0,func,sum => (sum,sum*2));
        var actual=Source().Aggregate(0,func,sum => (sum,sum*2));
        Assert.Equal(expected,actual);
    }
    private static void Aggregate_seed_func_resultSelector_ValueTuple_Delegate() {
        var expected=SystemEnumerable.Aggregate(Source(),0,delegate(int sum,int v) { return sum+v; },delegate(int sum) { return (sum,sum*2); });
        var actual=Source().Aggregate(0,delegate(int sum,int v) { return sum+v; },delegate(int sum) { return (sum,sum*2); });
        Assert.Equal(expected,actual);
    }
    private static void Aggregate_seed_func_resultSelector_ValueTuple_DelegateTryCatch() {
        var expected=SystemEnumerable.Aggregate(Source(),0,delegate(int sum,int v) { try { return sum+v; } catch { return 0; } },delegate(int sum) { return (sum,sum*2); });
        var actual=Source().Aggregate(0,delegate(int sum,int v) { try { return sum+v; } catch { return 0; } },delegate(int sum) { return (sum,sum*2); });
        Assert.Equal(expected,actual);
    }

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
    public void All() {
        All_Static();
        All_Lambda();
        All_Captured();
        All_Delegate();
        All_Variable();
    }
    private static void All_Static() {
        var expected=SystemEnumerable.All(Source(),static v => v>0);
        var actual=Source().All(static v => v>0);
        Assert.Equal(expected,actual);
    }
    private static void All_Lambda() {
        var expected=SystemEnumerable.All(Source(),v => v>0);
        var actual=Source().All(v => v>0);
        Assert.Equal(expected,actual);
    }
    private static void All_Captured() {
        var threshold=0;
        var expected=SystemEnumerable.All(Source(),v => v>threshold);
        var actual=Source().All(v => v>threshold);
        Assert.Equal(expected,actual);
    }
    private static void All_Delegate() {
        var expected=SystemEnumerable.All(Source(),delegate(int v) { return v>0; });
        var actual=Source().All(delegate(int v) { return v>0; });
        Assert.Equal(expected,actual);
    }
    private static void All_Variable() {
        Func<int,bool> predicate=v => v>0;
        var expected=SystemEnumerable.All(Source(),predicate);
        var actual=Source().All(predicate);
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void Any() {
        Any_NoDelegate();
        Any_predicate_Static();
        Any_predicate_Lambda();
        Any_predicate_Captured();
        Any_predicate_Delegate();
        Any_predicate_Variable();
    }
    private static void Any_NoDelegate() {
        var expected=SystemEnumerable.Any(Source());
        var actual=Source().Any();
        Assert.Equal(expected,actual);
    }
    private static void Any_predicate_Static() {
        var expected=SystemEnumerable.Any(Source(),static v => v>3);
        var actual=Source().Any(static v => v>3);
        Assert.Equal(expected,actual);
    }
    private static void Any_predicate_Lambda() {
        var expected=SystemEnumerable.Any(Source(),v => v>3);
        var actual=Source().Any(v => v>3);
        Assert.Equal(expected,actual);
    }
    private static void Any_predicate_Captured() {
        var threshold=3;
        var expected=SystemEnumerable.Any(Source(),v => v>threshold);
        var actual=Source().Any(v => v>threshold);
        Assert.Equal(expected,actual);
    }
    private static void Any_predicate_Delegate() {
        var expected=SystemEnumerable.Any(Source(),delegate(int v) { return v>3; });
        var actual=Source().Any(delegate(int v) { return v>3; });
        Assert.Equal(expected,actual);
    }
    private static void Any_predicate_Variable() {
        Func<int,bool> predicate=v => v>3;
        var expected=SystemEnumerable.Any(Source(),predicate);
        var actual=Source().Any(predicate);
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
    public void Average() {
        Average_int_NoDelegate();
        Average_selector_Static();
        Average_selector_Lambda();
        Average_selector_Captured();
        Average_selector_Delegate();
        Average_selector_Variable();
    }
    private static void Average_int_NoDelegate() {
        var expected=SystemEnumerable.Average(Source());
        var actual=Source().Average();
        Assert.Equal(expected,actual);
    }
    private static void Average_selector_Static() {
        var expected=SystemEnumerable.Average(Rows(),static row => row.Value);
        var actual=Rows().Average(static row => row.Value);
        Assert.Equal(expected,actual);
    }
    private static void Average_selector_Lambda() {
        var expected=SystemEnumerable.Average(Rows(),row => row.Value);
        var actual=Rows().Average(row => row.Value);
        Assert.Equal(expected,actual);
    }
    private static void Average_selector_Captured() {
        var offset=0;
        var expected=SystemEnumerable.Average(Rows(),row => row.Value+offset);
        var actual=Rows().Average(row => row.Value+offset);
        Assert.Equal(expected,actual);
    }
    private static void Average_selector_Delegate() {
        var expected=SystemEnumerable.Average(Rows(),delegate(Row row) { return row.Value; });
        var actual=Rows().Average(delegate(Row row) { return row.Value; });
        Assert.Equal(expected,actual);
    }
    private static void Average_selector_Variable() {
        Func<Row,int> selector=row => row.Value;
        var expected=SystemEnumerable.Average(Rows(),selector);
        var actual=Rows().Average(selector);
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
    public void Contains() {
        Contains_NoDelegate();
    }
    private static void Contains_NoDelegate() {
        var expected=SystemEnumerable.Contains(Source(),3);
        var actual=Source().Contains(3);
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void Count() {
        Count_NoDelegate();
        Count_predicate_Static();
        Count_predicate_Lambda();
        Count_predicate_Captured();
        Count_predicate_Delegate();
        Count_predicate_Variable();
    }
    private static void Count_NoDelegate() {
        var expected=SystemEnumerable.Count(Source());
        var actual=Source().Count();
        Assert.Equal(expected,actual);
    }
    private static void Count_predicate_Static() {
        var expected=SystemEnumerable.Count(Source(),static v => v>2);
        var actual=Source().Count(static v => v>2);
        Assert.Equal(expected,actual);
    }
    private static void Count_predicate_Lambda() {
        var expected=SystemEnumerable.Count(Source(),v => v>2);
        var actual=Source().Count(v => v>2);
        Assert.Equal(expected,actual);
    }
    private static void Count_predicate_Captured() {
        var threshold=2;
        var expected=SystemEnumerable.Count(Source(),v => v>threshold);
        var actual=Source().Count(v => v>threshold);
        Assert.Equal(expected,actual);
    }
    private static void Count_predicate_Delegate() {
        var expected=SystemEnumerable.Count(Source(),delegate(int v) { return v>2; });
        var actual=Source().Count(delegate(int v) { return v>2; });
        Assert.Equal(expected,actual);
    }
    private static void Count_predicate_Variable() {
        Func<int,bool> predicate=v => v>2;
        var expected=SystemEnumerable.Count(Source(),predicate);
        var actual=Source().Count(predicate);
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
    public void ElementAt() {
        ElementAt_NoDelegate();
    }
    private static void ElementAt_NoDelegate() {
        var expected=SystemEnumerable.ElementAt(Source(),1);
        var actual=Source().ElementAt(1);
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void ElementAtOrDefault() {
        ElementAtOrDefault_NoDelegate();
        ElementAtOrDefault_outOfRange_NoDelegate();
    }
    private static void ElementAtOrDefault_NoDelegate() {
        var expected=SystemEnumerable.ElementAtOrDefault(Source(),1);
        var actual=Source().ElementAtOrDefault(1);
        Assert.Equal(expected,actual);
    }
    private static void ElementAtOrDefault_outOfRange_NoDelegate() {
        var expected=SystemEnumerable.ElementAtOrDefault(Source(),99);
        var actual=Source().ElementAtOrDefault(99);
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
    public void First() {
        First_NoDelegate();
        First_predicate_Static();
        First_predicate_Lambda();
        First_predicate_Captured();
        First_predicate_Delegate();
        First_predicate_Variable();
    }
    private static void First_NoDelegate() {
        var expected=SystemEnumerable.First(Source());
        var actual=Source().First();
        Assert.Equal(expected,actual);
    }
    private static void First_predicate_Static() {
        var expected=SystemEnumerable.First(Source(),static v => v>2);
        var actual=Source().First(static v => v>2);
        Assert.Equal(expected,actual);
    }
    private static void First_predicate_Lambda() {
        var expected=SystemEnumerable.First(Source(),v => v>2);
        var actual=Source().First(v => v>2);
        Assert.Equal(expected,actual);
    }
    private static void First_predicate_Captured() {
        var threshold=2;
        var expected=SystemEnumerable.First(Source(),v => v>threshold);
        var actual=Source().First(v => v>threshold);
        Assert.Equal(expected,actual);
    }
    private static void First_predicate_Delegate() {
        var expected=SystemEnumerable.First(Source(),delegate(int v) { return v>2; });
        var actual=Source().First(delegate(int v) { return v>2; });
        Assert.Equal(expected,actual);
    }
    private static void First_predicate_Variable() {
        Func<int,bool> predicate=v => v>2;
        var expected=SystemEnumerable.First(Source(),predicate);
        var actual=Source().First(predicate);
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void FirstOrDefault() {
        FirstOrDefault_NoDelegate();
        FirstOrDefault_predicate_Static();
        FirstOrDefault_predicate_Lambda();
        FirstOrDefault_predicate_Captured();
        FirstOrDefault_predicate_Delegate();
        FirstOrDefault_predicate_Variable();
    }
    private static void FirstOrDefault_NoDelegate() {
        var expected=SystemEnumerable.FirstOrDefault(Source());
        var actual=Source().FirstOrDefault();
        Assert.Equal(expected,actual);
    }
    private static void FirstOrDefault_predicate_Static() {
        var expected=SystemEnumerable.FirstOrDefault(Source(),static v => v>2);
        var actual=Source().FirstOrDefault(static v => v>2);
        Assert.Equal(expected,actual);
    }
    private static void FirstOrDefault_predicate_Lambda() {
        var expected=SystemEnumerable.FirstOrDefault(Source(),v => v>2);
        var actual=Source().FirstOrDefault(v => v>2);
        Assert.Equal(expected,actual);
    }
    private static void FirstOrDefault_predicate_Captured() {
        var threshold=2;
        var expected=SystemEnumerable.FirstOrDefault(Source(),v => v>threshold);
        var actual=Source().FirstOrDefault(v => v>threshold);
        Assert.Equal(expected,actual);
    }
    private static void FirstOrDefault_predicate_Delegate() {
        var expected=SystemEnumerable.FirstOrDefault(Source(),delegate(int v) { return v>2; });
        var actual=Source().FirstOrDefault(delegate(int v) { return v>2; });
        Assert.Equal(expected,actual);
    }
    private static void FirstOrDefault_predicate_Variable() {
        Func<int,bool> predicate=v => v>2;
        var expected=SystemEnumerable.FirstOrDefault(Source(),predicate);
        var actual=Source().FirstOrDefault(predicate);
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
    public void Last() {
        Last_NoDelegate();
        Last_predicate_Static();
        Last_predicate_Lambda();
        Last_predicate_Captured();
        Last_predicate_Delegate();
        Last_predicate_Variable();
    }
    private static void Last_NoDelegate() {
        var expected=SystemEnumerable.Last(Source());
        var actual=Source().Last();
        Assert.Equal(expected,actual);
    }
    private static void Last_predicate_Static() {
        var expected=SystemEnumerable.Last(Source(),static v => v<3);
        var actual=Source().Last(static v => v<3);
        Assert.Equal(expected,actual);
    }
    private static void Last_predicate_Lambda() {
        var expected=SystemEnumerable.Last(Source(),v => v<3);
        var actual=Source().Last(v => v<3);
        Assert.Equal(expected,actual);
    }
    private static void Last_predicate_Captured() {
        var threshold=3;
        var expected=SystemEnumerable.Last(Source(),v => v<threshold);
        var actual=Source().Last(v => v<threshold);
        Assert.Equal(expected,actual);
    }
    private static void Last_predicate_Delegate() {
        var expected=SystemEnumerable.Last(Source(),delegate(int v) { return v<3; });
        var actual=Source().Last(delegate(int v) { return v<3; });
        Assert.Equal(expected,actual);
    }
    private static void Last_predicate_Variable() {
        Func<int,bool> predicate=v => v<3;
        var expected=SystemEnumerable.Last(Source(),predicate);
        var actual=Source().Last(predicate);
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void LastOrDefault() {
        LastOrDefault_NoDelegate();
        LastOrDefault_predicate_Static();
        LastOrDefault_predicate_Lambda();
        LastOrDefault_predicate_Captured();
        LastOrDefault_predicate_Delegate();
        LastOrDefault_predicate_Variable();
    }
    private static void LastOrDefault_NoDelegate() {
        var expected=SystemEnumerable.LastOrDefault(Source());
        var actual=Source().LastOrDefault();
        Assert.Equal(expected,actual);
    }
    private static void LastOrDefault_predicate_Static() {
        var expected=SystemEnumerable.LastOrDefault(Source(),static v => v<3);
        var actual=Source().LastOrDefault(static v => v<3);
        Assert.Equal(expected,actual);
    }
    private static void LastOrDefault_predicate_Lambda() {
        var expected=SystemEnumerable.LastOrDefault(Source(),v => v<3);
        var actual=Source().LastOrDefault(v => v<3);
        Assert.Equal(expected,actual);
    }
    private static void LastOrDefault_predicate_Captured() {
        var threshold=3;
        var expected=SystemEnumerable.LastOrDefault(Source(),v => v<threshold);
        var actual=Source().LastOrDefault(v => v<threshold);
        Assert.Equal(expected,actual);
    }
    private static void LastOrDefault_predicate_Delegate() {
        var expected=SystemEnumerable.LastOrDefault(Source(),delegate(int v) { return v<3; });
        var actual=Source().LastOrDefault(delegate(int v) { return v<3; });
        Assert.Equal(expected,actual);
    }
    private static void LastOrDefault_predicate_Variable() {
        Func<int,bool> predicate=v => v<3;
        var expected=SystemEnumerable.LastOrDefault(Source(),predicate);
        var actual=Source().LastOrDefault(predicate);
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void LongCount() {
        LongCount_NoDelegate();
        LongCount_predicate_Static();
        LongCount_predicate_Lambda();
        LongCount_predicate_Captured();
        LongCount_predicate_Delegate();
        LongCount_predicate_Variable();
    }
    private static void LongCount_NoDelegate() {
        var expected=SystemEnumerable.LongCount(Source());
        var actual=Source().LongCount();
        Assert.Equal(expected,actual);
    }
    private static void LongCount_predicate_Static() {
        var expected=SystemEnumerable.LongCount(Source(),static v => v>2);
        var actual=Source().LongCount(static v => v>2);
        Assert.Equal(expected,actual);
    }
    private static void LongCount_predicate_Lambda() {
        var expected=SystemEnumerable.LongCount(Source(),v => v>2);
        var actual=Source().LongCount(v => v>2);
        Assert.Equal(expected,actual);
    }
    private static void LongCount_predicate_Captured() {
        var threshold=2;
        var expected=SystemEnumerable.LongCount(Source(),v => v>threshold);
        var actual=Source().LongCount(v => v>threshold);
        Assert.Equal(expected,actual);
    }
    private static void LongCount_predicate_Delegate() {
        var expected=SystemEnumerable.LongCount(Source(),delegate(int v) { return v>2; });
        var actual=Source().LongCount(delegate(int v) { return v>2; });
        Assert.Equal(expected,actual);
    }
    private static void LongCount_predicate_Variable() {
        Func<int,bool> predicate=v => v>2;
        var expected=SystemEnumerable.LongCount(Source(),predicate);
        var actual=Source().LongCount(predicate);
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void Max() {
        Max_int_NoDelegate();
        Max_selector_Static();
        Max_selector_Lambda();
        Max_selector_Captured();
        Max_selector_Delegate();
        Max_selector_Variable();
    }
    private static void Max_int_NoDelegate() {
        var expected=SystemEnumerable.Max(Source());
        var actual=Source().Max();
        Assert.Equal(expected,actual);
    }
    private static void Max_selector_Static() {
        var expected=SystemEnumerable.Max(Rows(),static row => row.Value);
        var actual=Rows().Max(static row => row.Value);
        Assert.Equal(expected,actual);
    }
    private static void Max_selector_Lambda() {
        var expected=SystemEnumerable.Max(Rows(),row => row.Value);
        var actual=Rows().Max(row => row.Value);
        Assert.Equal(expected,actual);
    }
    private static void Max_selector_Captured() {
        var offset=0;
        var expected=SystemEnumerable.Max(Rows(),row => row.Value+offset);
        var actual=Rows().Max(row => row.Value+offset);
        Assert.Equal(expected,actual);
    }
    private static void Max_selector_Delegate() {
        var expected=SystemEnumerable.Max(Rows(),delegate(Row row) { return row.Value; });
        var actual=Rows().Max(delegate(Row row) { return row.Value; });
        Assert.Equal(expected,actual);
    }
    private static void Max_selector_Variable() {
        Func<Row,int> selector=row => row.Value;
        var expected=SystemEnumerable.Max(Rows(),selector);
        var actual=Rows().Max(selector);
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void MaxBy() {
        MaxBy_Static();
        MaxBy_Lambda();
        MaxBy_Captured();
        MaxBy_Delegate();
        MaxBy_Variable();
    }
    private static void MaxBy_Static() {
        var expected=SystemEnumerable.MaxBy(Rows(),static row => row.Value);
        var actual=Rows().MaxBy(static row => row.Value);
        Assert.Equal(expected,actual);
    }
    private static void MaxBy_Lambda() {
        var expected=SystemEnumerable.MaxBy(Rows(),row => row.Value);
        var actual=Rows().MaxBy(row => row.Value);
        Assert.Equal(expected,actual);
    }
    private static void MaxBy_Captured() {
        var offset=0;
        var expected=SystemEnumerable.MaxBy(Rows(),row => row.Value+offset);
        var actual=Rows().MaxBy(row => row.Value+offset);
        Assert.Equal(expected,actual);
    }
    private static void MaxBy_Delegate() {
        var expected=SystemEnumerable.MaxBy(Rows(),delegate(Row row) { return row.Value; });
        var actual=Rows().MaxBy(delegate(Row row) { return row.Value; });
        Assert.Equal(expected,actual);
    }
    private static void MaxBy_Variable() {
        Func<Row,int> keySelector=row => row.Value;
        var expected=SystemEnumerable.MaxBy(Rows(),keySelector);
        var actual=Rows().MaxBy(keySelector);
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void Min() {
        Min_int_NoDelegate();
        Min_selector_Static();
        Min_selector_Lambda();
        Min_selector_Captured();
        Min_selector_Delegate();
        Min_selector_Variable();
    }
    private static void Min_int_NoDelegate() {
        var expected=SystemEnumerable.Min(Source());
        var actual=Source().Min();
        Assert.Equal(expected,actual);
    }
    private static void Min_selector_Static() {
        var expected=SystemEnumerable.Min(Rows(),static row => row.Value);
        var actual=Rows().Min(static row => row.Value);
        Assert.Equal(expected,actual);
    }
    private static void Min_selector_Lambda() {
        var expected=SystemEnumerable.Min(Rows(),row => row.Value);
        var actual=Rows().Min(row => row.Value);
        Assert.Equal(expected,actual);
    }
    private static void Min_selector_Captured() {
        var offset=0;
        var expected=SystemEnumerable.Min(Rows(),row => row.Value+offset);
        var actual=Rows().Min(row => row.Value+offset);
        Assert.Equal(expected,actual);
    }
    private static void Min_selector_Delegate() {
        var expected=SystemEnumerable.Min(Rows(),delegate(Row row) { return row.Value; });
        var actual=Rows().Min(delegate(Row row) { return row.Value; });
        Assert.Equal(expected,actual);
    }
    private static void Min_selector_Variable() {
        Func<Row,int> selector=row => row.Value;
        var expected=SystemEnumerable.Min(Rows(),selector);
        var actual=Rows().Min(selector);
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void MinBy() {
        MinBy_Static();
        MinBy_Lambda();
        MinBy_Captured();
        MinBy_Delegate();
        MinBy_Variable();
    }
    private static void MinBy_Static() {
        var expected=SystemEnumerable.MinBy(Rows(),static row => row.Value);
        var actual=Rows().MinBy(static row => row.Value);
        Assert.Equal(expected,actual);
    }
    private static void MinBy_Lambda() {
        var expected=SystemEnumerable.MinBy(Rows(),row => row.Value);
        var actual=Rows().MinBy(row => row.Value);
        Assert.Equal(expected,actual);
    }
    private static void MinBy_Captured() {
        var offset=0;
        var expected=SystemEnumerable.MinBy(Rows(),row => row.Value+offset);
        var actual=Rows().MinBy(row => row.Value+offset);
        Assert.Equal(expected,actual);
    }
    private static void MinBy_Delegate() {
        var expected=SystemEnumerable.MinBy(Rows(),delegate(Row row) { return row.Value; });
        var actual=Rows().MinBy(delegate(Row row) { return row.Value; });
        Assert.Equal(expected,actual);
    }
    private static void MinBy_Variable() {
        Func<Row,int> keySelector=row => row.Value;
        var expected=SystemEnumerable.MinBy(Rows(),keySelector);
        var actual=Rows().MinBy(keySelector);
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void OfType() {
        OfType_NoDelegate();
        OfType_mixedTypes_NoDelegate();
    }
    private static void OfType_NoDelegate() {
        var expected=SystemEnumerable.OfType<int>(CastObjects());
        var actual=CastObjects().OfType<int>();
        Assert.Equal(expected,actual);
    }
    private static void OfType_mixedTypes_NoDelegate() {
        var mixed=new object[] { 1,"hello",2,null!,3.0,4 };
        var expected=SystemEnumerable.OfType<int>(mixed);
        var actual=((global::System.Collections.IEnumerable)mixed).OfType<int>();
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
    public void SequenceEqual() {
        SequenceEqual_NoDelegate();
    }
    private static void SequenceEqual_NoDelegate() {
        var expected=SystemEnumerable.SequenceEqual(Source(),Source());
        var actual=Source().SequenceEqual(Source());
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void Single() {
        Single_NoDelegate();
        Single_predicate_Static();
        Single_predicate_Lambda();
        Single_predicate_Captured();
        Single_predicate_Delegate();
        Single_predicate_Variable();
    }
    private static void Single_NoDelegate() {
        var expected=SystemEnumerable.Single(new[]{42});
        var actual=new[]{42}.Single();
        Assert.Equal(expected,actual);
    }
    private static void Single_predicate_Static() {
        var expected=SystemEnumerable.Single(Source(),static v => v==2);
        var actual=Source().Single(static v => v==2);
        Assert.Equal(expected,actual);
    }
    private static void Single_predicate_Lambda() {
        var expected=SystemEnumerable.Single(Source(),v => v==2);
        var actual=Source().Single(v => v==2);
        Assert.Equal(expected,actual);
    }
    private static void Single_predicate_Captured() {
        var target=2;
        var expected=SystemEnumerable.Single(Source(),v => v==target);
        var actual=Source().Single(v => v==target);
        Assert.Equal(expected,actual);
    }
    private static void Single_predicate_Delegate() {
        var expected=SystemEnumerable.Single(Source(),delegate(int v) { return v==2; });
        var actual=Source().Single(delegate(int v) { return v==2; });
        Assert.Equal(expected,actual);
    }
    private static void Single_predicate_Variable() {
        Func<int,bool> predicate=v => v==2;
        var expected=SystemEnumerable.Single(Source(),predicate);
        var actual=Source().Single(predicate);
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void SingleOrDefault() {
        SingleOrDefault_NoDelegate();
        SingleOrDefault_predicate_Static();
        SingleOrDefault_predicate_Lambda();
        SingleOrDefault_predicate_Captured();
        SingleOrDefault_predicate_Delegate();
        SingleOrDefault_predicate_Variable();
    }
    private static void SingleOrDefault_NoDelegate() {
        var expected=SystemEnumerable.SingleOrDefault(new[]{42});
        var actual=new[]{42}.SingleOrDefault();
        Assert.Equal(expected,actual);
    }
    private static void SingleOrDefault_predicate_Static() {
        var expected=SystemEnumerable.SingleOrDefault(Source(),static v => v==2);
        var actual=Source().SingleOrDefault(static v => v==2);
        Assert.Equal(expected,actual);
    }
    private static void SingleOrDefault_predicate_Lambda() {
        var expected=SystemEnumerable.SingleOrDefault(Source(),v => v==2);
        var actual=Source().SingleOrDefault(v => v==2);
        Assert.Equal(expected,actual);
    }
    private static void SingleOrDefault_predicate_Captured() {
        var target=2;
        var expected=SystemEnumerable.SingleOrDefault(Source(),v => v==target);
        var actual=Source().SingleOrDefault(v => v==target);
        Assert.Equal(expected,actual);
    }
    private static void SingleOrDefault_predicate_Delegate() {
        var expected=SystemEnumerable.SingleOrDefault(Source(),delegate(int v) { return v==2; });
        var actual=Source().SingleOrDefault(delegate(int v) { return v==2; });
        Assert.Equal(expected,actual);
    }
    private static void SingleOrDefault_predicate_Variable() {
        Func<int,bool> predicate=v => v==2;
        var expected=SystemEnumerable.SingleOrDefault(Source(),predicate);
        var actual=Source().SingleOrDefault(predicate);
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
    public void Sum() {
        Sum_int_NoDelegate();
        Sum_selector_Static();
        Sum_selector_Lambda();
        Sum_selector_Captured();
        Sum_selector_Delegate();
        Sum_selector_Variable();
    }
    private static void Sum_int_NoDelegate() {
        var expected=SystemEnumerable.Sum(Source());
        var actual=Source().Sum();
        Assert.Equal(expected,actual);
    }
    private static void Sum_selector_Static() {
        var expected=SystemEnumerable.Sum(Rows(),static row => row.Value);
        var actual=Rows().Sum(static row => row.Value);
        Assert.Equal(expected,actual);
    }
    private static void Sum_selector_Lambda() {
        var expected=SystemEnumerable.Sum(Rows(),row => row.Value);
        var actual=Rows().Sum(row => row.Value);
        Assert.Equal(expected,actual);
    }
    private static void Sum_selector_Captured() {
        var offset=0;
        var expected=SystemEnumerable.Sum(Rows(),row => row.Value+offset);
        var actual=Rows().Sum(row => row.Value+offset);
        Assert.Equal(expected,actual);
    }
    private static void Sum_selector_Delegate() {
        var expected=SystemEnumerable.Sum(Rows(),delegate(Row row) { return row.Value; });
        var actual=Rows().Sum(delegate(Row row) { return row.Value; });
        Assert.Equal(expected,actual);
    }
    private static void Sum_selector_Variable() {
        Func<Row,int> selector=row => row.Value;
        var expected=SystemEnumerable.Sum(Rows(),selector);
        var actual=Rows().Sum(selector);
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
    public void TryGetNonEnumeratedCount() {
        TryGetNonEnumeratedCount_NoDelegate();
    }
    private static void TryGetNonEnumeratedCount_NoDelegate() {
        var expectedResult=SystemEnumerable.TryGetNonEnumeratedCount(Source(),out var expectedCount);
        var actualResult=Source().TryGetNonEnumeratedCount(out var actualCount);
        Assert.Equal(expectedResult,actualResult);
        Assert.Equal(expectedCount,actualCount);
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
