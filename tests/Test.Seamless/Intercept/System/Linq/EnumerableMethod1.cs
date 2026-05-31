using System.Linq;
using NonGenericEnumerable = System.Collections.IEnumerable;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq;

public sealed class EnumerableMethod1 {
    [Fact,Trait("Category","Unit")]
    public void Aggregate_func() {
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

    [Fact, Trait("Category","Unit")]
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

    [Fact, Trait("Category","Unit")]
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

    [Fact, Trait("Category","Unit")]
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

    [Fact, Trait("Category","Unit")]
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

    [Fact, Trait("Category","Unit")]
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

    [Fact, Trait("Category","Unit")]
    public void Append() {
        Append_NoDelegate();
    }
    private static void Append_NoDelegate() {
        var expected=SystemEnumerable.Append(Source(),5).ToList();
        var actual=Source().Append(5).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void AsEnumerable() {
        AsEnumerable_NoDelegate();
    }
    private static void AsEnumerable_NoDelegate() {
        var expected=SystemEnumerable.AsEnumerable(Source()).ToList();
        var actual=Source().AsEnumerable().ToList();
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
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

    [Fact, Trait("Category","Unit")]
    public void Cast() {
        Cast_NoDelegate();
    }
    private static void Cast_NoDelegate() {
        var expected=SystemEnumerable.Cast<int>(CastObjects()).ToList();
        var actual=CastObjects().Cast<int>().ToList();
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Chunk() {
        Chunk_NoDelegate();
    }
    private static void Chunk_NoDelegate() {
        var expected=SystemEnumerable.Chunk(Source(),2).ToList();
        var actual=Source().Chunk(2).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Concat() {
        Concat_NoDelegate();
    }
    private static void Concat_NoDelegate() {
        var expected=SystemEnumerable.Concat(Source(),Second()).ToList();
        var actual=Source().Concat(Second()).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Contains() {
        Contains_NoDelegate();
    }
    private static void Contains_NoDelegate() {
        var expected=SystemEnumerable.Contains(Source(),3);
        var actual=Source().Contains(3);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
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

    [Fact, Trait("Category","Unit")]
    public void Distinct() {
        Distinct_NoDelegate();
    }
    private static void Distinct_NoDelegate() {
        var expected=SystemEnumerable.Distinct(Duplicates()).ToList();
        var actual=Duplicates().Distinct().ToList();
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
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

    [Fact, Trait("Category","Unit")]
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

    [Fact, Trait("Category","Unit")]
    public void GroupBy() {
        GroupBy_Static();
        GroupBy_Lambda();
        GroupBy_Captured();
        GroupBy_Delegate();
        GroupBy_Variable();
    }
    private static void GroupBy_Static() {
        var expected=SystemEnumerable.GroupBy(Rows(),static row => row.Key).ToList();
        var actual=Rows().GroupBy(static row => row.Key).ToList();
        Assert.Equal(expected,actual);
    }
    private static void GroupBy_Lambda() {
        var expected=SystemEnumerable.GroupBy(Rows(),row => row.Key).ToList();
        var actual=Rows().GroupBy(row => row.Key).ToList();
        Assert.Equal(expected,actual);
    }
    private static void GroupBy_Captured() {
        var offset=0;
        var expected=SystemEnumerable.GroupBy(Rows(),row => row.Key+offset).ToList();
        var actual=Rows().GroupBy(row => row.Key+offset).ToList();
        Assert.Equal(expected,actual);
    }
    private static void GroupBy_Delegate() {
        var expected=SystemEnumerable.GroupBy(Rows(),delegate(Row row) { return row.Key; }).ToList();
        var actual=Rows().GroupBy(delegate(Row row) { return row.Key; }).ToList();
        Assert.Equal(expected,actual);
    }
    private static void GroupBy_Variable() {
        Func<Row,int> keySelector=row => row.Key;
        var expected=SystemEnumerable.GroupBy(Rows(),keySelector).ToList();
        var actual=Rows().GroupBy(keySelector).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
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

    [Fact, Trait("Category","Unit")]
    public void Max() {
        Max_selector_Static();
        Max_selector_Lambda();
        Max_selector_Captured();
        Max_selector_Delegate();
        Max_selector_Variable();
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

    [Fact, Trait("Category","Unit")]
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

    [Fact, Trait("Category","Unit")]
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
    [Fact, Trait("Category","Unit")]
    public void Select1() {
        Select_selector_Static1();
    }
    private static void Select_selector_Static1() {
        var expected=SystemEnumerable.Select(Source(),static v => v*2).ToList();
        var actual=Source().Select(static v => v*2).ToList();
        Assert.Equal(expected,actual);
    }
    [Fact, Trait("Category","Unit")]
    public void Select() {
        Select_selector_Static();
        Select_selector_Lambda();
        Select_selector_Captured();
        Select_selector_Delegate();
        Select_selector_Variable();
        Select_selector_index_Static();
        Select_selector_index_Lambda();
        Select_selector_index_Captured();
        Select_selector_index_Delegate();
        Select_selector_index_Variable();
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
    private static void Select_selector_index_Static() {
        var expected=SystemEnumerable.Select(Source(),static (v,i) => v+i).ToList();
        var actual=Source().Select(static (v,i) => v+i).ToList();
        Assert.Equal(expected,actual);
    }
    private static void Select_selector_index_Lambda() {
        var expected=SystemEnumerable.Select(Source(),(v,i) => v+i).ToList();
        var actual=Source().Select((v,i) => v+i).ToList();
        Assert.Equal(expected,actual);
    }
    private static void Select_selector_index_Captured() {
        var offset=0;
        var expected=SystemEnumerable.Select(Source(),(v,i) => v+i+offset).ToList();
        var actual=Source().Select((v,i) => v+i+offset).ToList();
        Assert.Equal(expected,actual);
    }
    private static void Select_selector_index_Delegate() {
        var expected=SystemEnumerable.Select(Source(),delegate(int v,int i) { return v+i; }).ToList();
        var actual=Source().Select(delegate(int v,int i) { return v+i; }).ToList();
        Assert.Equal(expected,actual);
    }
    private static void Select_selector_index_Variable() {
        Func<int,int,int> selector=(v,i) => v+i;
        var expected=SystemEnumerable.Select(Source(),selector).ToList();
        var actual=Source().Select(selector).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
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

    [Fact, Trait("Category","Unit")]
    public void SkipWhile() {
        SkipWhile_Static();
        SkipWhile_Lambda();
        SkipWhile_Captured();
        SkipWhile_Delegate();
        SkipWhile_Variable();
    }
    private static void SkipWhile_Static() {
        var expected=SystemEnumerable.SkipWhile(Source(),static v => v<3).ToList();
        var actual=Source().SkipWhile(static v => v<3).ToList();
        Assert.Equal(expected,actual);
    }
    private static void SkipWhile_Lambda() {
        var expected=SystemEnumerable.SkipWhile(Source(),v => v<3).ToList();
        var actual=Source().SkipWhile(v => v<3).ToList();
        Assert.Equal(expected,actual);
    }
    private static void SkipWhile_Captured() {
        var threshold=3;
        var expected=SystemEnumerable.SkipWhile(Source(),v => v<threshold).ToList();
        var actual=Source().SkipWhile(v => v<threshold).ToList();
        Assert.Equal(expected,actual);
    }
    private static void SkipWhile_Delegate() {
        var expected=SystemEnumerable.SkipWhile(Source(),delegate(int v) { return v<3; }).ToList();
        var actual=Source().SkipWhile(delegate(int v) { return v<3; }).ToList();
        Assert.Equal(expected,actual);
    }
    private static void SkipWhile_Variable() {
        Func<int,bool> predicate=v => v<3;
        var expected=SystemEnumerable.SkipWhile(Source(),predicate).ToList();
        var actual=Source().SkipWhile(predicate).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Sum() {
        Sum_selector_Static();
        Sum_selector_Lambda();
        Sum_selector_Captured();
        Sum_selector_Delegate();
        Sum_selector_Variable();
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

    [Fact, Trait("Category","Unit")]
    public void Take() {
        Take_NoDelegate();
    }
    private static void Take_NoDelegate() {
        var expected=SystemEnumerable.Take(Source(),3).ToList();
        var actual=Source().Take(3).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void ToArray() {
        ToArray_NoDelegate();
    }
    private static void ToArray_NoDelegate() {
        var expected=SystemEnumerable.ToArray(Source());
        var actual=Source().ToArray();
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void ToList() {
        ToList_NoDelegate();
    }
    private static void ToList_NoDelegate() {
        var expected=SystemEnumerable.ToList(Source());
        var actual=Source().ToList();
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Union() {
        Union_NoDelegate();
    }
    private static void Union_NoDelegate() {
        var expected=Source().Union(Second()).ToList();
        var actual=Source().Union(Second(),EqualityComparer<int>.Default).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Where() {
        Where_predicate_Static();
        Where_predicate_Lambda();
        Where_predicate_Captured();
        Where_predicate_Delegate();
        Where_predicate_Variable();
        Where_predicate_index_Static();
        Where_predicate_index_Lambda();
        Where_predicate_index_Captured();
        Where_predicate_index_Delegate();
        Where_predicate_index_Variable();
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
    private static void Where_predicate_index_Static() {
        var expected=SystemEnumerable.Where(Source(),static (v,i) => v>2&&i>0).ToList();
        var actual=Source().Where(static (v,i) => v>2&&i>0).ToList();
        Assert.Equal(expected,actual);
    }
    private static void Where_predicate_index_Lambda() {
        var expected=SystemEnumerable.Where(Source(),(v,i) => v>2&&i>0).ToList();
        var actual=Source().Where((v,i) => v>2&&i>0).ToList();
        Assert.Equal(expected,actual);
    }
    private static void Where_predicate_index_Captured() {
        var threshold=2;
        var expected=SystemEnumerable.Where(Source(),(v,i) => v>threshold&&i>0).ToList();
        var actual=Source().Where((v,i) => v>threshold&&i>0).ToList();
        Assert.Equal(expected,actual);
    }
    private static void Where_predicate_index_Delegate() {
        var expected=SystemEnumerable.Where(Source(),delegate(int v,int i) { return v>2&&i>0; }).ToList();
        var actual=Source().Where(delegate(int v,int i) { return v>2&&i>0; }).ToList();
        Assert.Equal(expected,actual);
    }
    private static void Where_predicate_index_Variable() {
        Func<int,int,bool> predicate=(v,i) => v>2&&i>0;
        var expected=SystemEnumerable.Where(Source(),predicate).ToList();
        var actual=Source().Where(predicate).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Zip() {
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

    public static int[] Source() => [1,2,3,4];
    public static int[] Second() => [3,4,5];
    public static int[] Duplicates() => [1,2,2,3,3,4];
    public static NonGenericEnumerable CastObjects() => new object[] { 1,2,3 };
    public static Row[] Rows() => [new(1,10,"beta"),new(2,20,"alpha"),new(3,30,"gamma")];

    public sealed record Row(int Key,int Value,string Name);
}
