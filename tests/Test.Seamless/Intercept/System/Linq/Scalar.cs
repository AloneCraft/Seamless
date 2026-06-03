using System.Linq;
using NonGenericEnumerable = System.Collections.IEnumerable;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq;

public sealed class Scalar {
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
    public void TryGetNonEnumeratedCount() {
        TryGetNonEnumeratedCount_NoDelegate();
    }
    private static void TryGetNonEnumeratedCount_NoDelegate() {
        var expectedResult=SystemEnumerable.TryGetNonEnumeratedCount(Source(),out var expectedCount);
        var actualResult=Source().TryGetNonEnumeratedCount(out var actualCount);
        Assert.Equal(expectedResult,actualResult);
        Assert.Equal(expectedCount,actualCount);
    }

    public int[] InstanceSource() => [1,2,3,4];
    public static int[] Source() => [1,2,3,4];
    public static int[] Second() => [3,4,5];
    public static int[] Duplicates() => [1,2,2,3,3,4];
    public static NonGenericEnumerable CastObjects() => new object[] { 1,2,3 };
    public static Row[] Rows() => [new(1,10,"beta"),new(2,20,"alpha"),new(3,30,"gamma")];

    public sealed record Row(int Key,int Value,string Name);
}
