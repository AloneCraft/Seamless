using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Tsavorite.Unit.Linq;

public sealed class EnumerableIntercept {
    [Fact, Trait("Category","Unit")]
    public void Aggregate() {
        var values = DuplicateNumbers();
        AssertSameValue(() => values.Aggregate(static (total,item) => total+item),() => SystemEnumerable.Aggregate(values,static (total,item) => total+item));
        AssertSameValue(() => values.Aggregate(100,static (total,item) => total-item),() => SystemEnumerable.Aggregate(values,100,static (total,item) => total-item));
        AssertSameValue(() => values.Aggregate(0,static (total,item) => total+item,static total => $"sum:{total}"),() => SystemEnumerable.Aggregate(values,0,static (total,item) => total+item,static total => $"sum:{total}"));
    }

    [Fact, Trait("Category","Unit")]
    public void AggregateBy() {
        var rows = RowsWithDuplicates();
        IEqualityComparer<string> comparer = StringComparer.OrdinalIgnoreCase;
        AssertSameSequence(() => rows.AggregateBy(static row => row.Category,0,static (total,row) => total+row.Score),() => SystemEnumerable.AggregateBy(rows,static row => row.Category,0,static (total,row) => total+row.Score));
        AssertSameSequence(() => rows.AggregateBy(static row => row.Category,static category => category.Length,static (total,row) => total+row.Score,comparer),() => SystemEnumerable.AggregateBy(rows,static row => row.Category,static category => category.Length,static (total,row) => total+row.Score,comparer));
    }

    [Fact, Trait("Category","Unit")]
    public void All() {
        var rows = RowsWithDuplicates();
        AssertSameValue(() => rows.All(static row => row.Score>=-5),() => SystemEnumerable.All(rows,static row => row.Score>=-5));
        AssertSameValue(() => rows.All(static row => row.Category=="alpha"),() => SystemEnumerable.All(rows,static row => row.Category=="alpha"));
    }

    [Fact, Trait("Category","Unit")]
    public void Any() {
        var rows = RowsWithDuplicates();
        AssertSameValue(() => rows.Any(),                                               () => SystemEnumerable.Any(rows));
        AssertSameValue(() => Array.Empty<Row>().Any(),                                 () => SystemEnumerable.Any(Array.Empty<Row>()));
        AssertSameValue(() => rows.Any(static row => row.Note is null),() => SystemEnumerable.Any(rows,static row => row.Note is null));
    }

    [Fact, Trait("Category","Unit")]
    public void Append() {
        AssertSameSequence(() => DuplicateNumbers().Append(9),() => SystemEnumerable.Append(DuplicateNumbers(),9));
    }

    [Fact, Trait("Category","Unit")]
    public void AsEnumerable() {
        var values = DuplicateNumbers();
        Assert.Same(values.AsEnumerable(),SystemEnumerable.AsEnumerable(values));
    }

    [Fact, Trait("Category","Unit")]
    public void Average() {
        var rows = RowsWithDuplicates();
        var ints = new[] { 1,2,2,5,-1 };
        var longs = new[] { 1L,4L,4L,10L };
        var floats = new[] { 1.5f,2.5f,2.5f,5.5f };
        var doubles = new[] { 1.5d,2.5d,2.5d,5.5d };
        var decimals = new[] { 1.5m,2.5m,2.5m,5.5m };
        AssertSameValue(() => ints.Average(),  () => SystemEnumerable.Average(ints));
        AssertSameValue(() => longs.Average(), () => SystemEnumerable.Average(longs));
        AssertSameValue(() => floats.Average(),() => SystemEnumerable.Average(floats));
        AssertSameValue(() => doubles.Average(),                                () =>{
            var sum=0D;
            var count=0L;
            foreach(var item in doubles){
                sum+=item;
                checked {
                    count++;
                }
            }
            if(count==0)
                throw global::Tsavorite.GeneratedCodeHelpers.NoElements();
            return sum/count;
        });
        AssertSameValue(() => decimals.Average(),                               () =>{
            var sum=0m;
            var count=0L;
            foreach(var item in decimals) {
                checked {
                    sum+=item;
                    count++;
                }
            }
            if(count==0)
                throw global::Tsavorite.GeneratedCodeHelpers.NoElements();
            return sum/count;
        });
        AssertSameValue(() => new int?[] { 1,null,2,2 }.Average(),    () => SystemEnumerable.Average([1,null,2,2]));
        AssertSameValue(() => new long?[] { 1L,null,4L,4L }.Average(),() => SystemEnumerable.Average([1L,null,4L,4L]));
        AssertSameValue(() => new float?[] { 1.5f,null,2.5f,2.5f }.Average(),   () =>{
            var sum=0D;
            var count=0L;
            foreach(var item in new float?[] { 1.5f,null,2.5f,2.5f }){
                if(item.HasValue){
                    sum+=item.GetValueOrDefault();
                    checked {
                        count++;
                    }
                }
            }
            return count==0?null:(float)(sum/count);
        });
        AssertSameValue(() => new double?[] { 1.5d,null,2.5d,2.5d }.Average(),                  () => SystemEnumerable.Average(
            [1.5d,null,2.5d,2.5d]));
        AssertSameValue(() => new decimal?[] { 1.5m,null,2.5m,2.5m }.Average(),                 () => SystemEnumerable.Average(
            [1.5m,null,2.5m,2.5m]));
        AssertSameValue(() => rows.Average(static row => row.Amount),          () => SystemEnumerable.Average(rows,static row => row.Amount));
        AssertSameValue(() => rows.Average(static row => (double)row.Amount),  () => SystemEnumerable.Average(rows,static row => (double)row.Amount));
        AssertSameValue(() => rows.Average(static row => row.Score),           () => SystemEnumerable.Average(rows,static row => row.Score));
        AssertSameValue(() => rows.Average(static row => (long)row.Score),     () => SystemEnumerable.Average(rows,static row => (long)row.Score));
        AssertSameValue(() => rows.Average(static row => (float)row.Score),    () => SystemEnumerable.Average(rows,static row => (float)row.Score));
        AssertSameValue(() => rows.Average(static row => (decimal?)row.Amount),() => SystemEnumerable.Average(rows,static row => (decimal?)row.Amount));
        AssertSameValue(() => rows.Average(static row => (double?)row.Score),  () => SystemEnumerable.Average(rows,static row => (double?)row.Score));
        AssertSameValue(() => rows.Average(static row => (int?)row.Score),     () => SystemEnumerable.Average(rows,static row => (int?)row.Score));
        AssertSameValue(() => rows.Average(static row => (long?)row.Score),    () => SystemEnumerable.Average(rows,static row => (long?)row.Score));
        AssertSameValue(() => rows.Average(static row => (float?)row.Score),   () => SystemEnumerable.Average(rows,static row => (float?)row.Score));
    }

    [Fact, Trait("Category","Unit")]
    public void Cast() {
        var values = new ArrayList { 1,2,2,5 };
        AssertSameSequence(() => values.Cast<int>(),() => SystemEnumerable.Cast<int>(values));
    }

    [Fact, Trait("Category","Unit")]
    public void Chunk() {
        AssertSameSequence(() => ChunkText(DuplicateNumbers().Chunk(3)),() => ChunkText(SystemEnumerable.Chunk(DuplicateNumbers(),3)));
    }

    [Fact, Trait("Category","Unit")]
    public void Concat() {
        AssertSameSequence(() => DuplicateNumbers().Concat(UniqueNumbers()),() => SystemEnumerable.Concat(DuplicateNumbers(),UniqueNumbers()));
    }

    [Fact, Trait("Category","Unit")]
    public void Contains() {
        var values = DuplicateNumbers();
        AssertSameValue(() => SystemEnumerable.Contains(values,4),() => SystemEnumerable.Contains(values,4));
        AssertSameValue(() => SystemEnumerable.Contains(values,10),() => SystemEnumerable.Contains(values,10));
        AssertSameValue(() => SystemEnumerable.Contains(values,7,new ModuloComparer(3)),() =>{
            IEqualityComparer<int>? comparer=new ModuloComparer(3);
            comparer??=EqualityComparer<int>.Default;
            foreach(var item in values) {
                if(comparer.Equals(item,7)) {
                    return true;
                }
            }
            return false;
        });
    }

    [Fact, Trait("Category","Unit")]
    public void Count() {
        var rows = RowsWithDuplicates();
        AssertSameValue(() => rows.Count(),() => SystemEnumerable.Count(rows));
        AssertSameValue(() => rows.Count(static row => row.Category=="beta"),() => SystemEnumerable.Count(rows,static row => row.Category=="beta"));
    }

    [Fact, Trait("Category","Unit")]
    public void CountBy() {
        var rows = RowsWithDuplicates();
        AssertSameSequence(() => rows.CountBy(static row => row.Category),() => SystemEnumerable.CountBy(rows,static row => row.Category));
        AssertSameSequence(() => new[] { "A","a","B","b","b" }.CountBy(static value => value,StringComparer.OrdinalIgnoreCase),() => SystemEnumerable.CountBy(
            ["A","a","B","b","b"],static value => value,StringComparer.OrdinalIgnoreCase));
    }

    [Fact, Trait("Category","Unit")]
    public void DefaultIfEmpty() {
        AssertSameSequence(() => DuplicateNumbers().DefaultIfEmpty(),                   () => SystemEnumerable.DefaultIfEmpty(DuplicateNumbers()));
        AssertSameSequence(() => Array.Empty<int>().DefaultIfEmpty(),                   () => SystemEnumerable.DefaultIfEmpty(Array.Empty<int>()));
        AssertSameSequence(() => Array.Empty<int>().DefaultIfEmpty(42),() => SystemEnumerable.DefaultIfEmpty([],42));
    }

    [Fact, Trait("Category","Unit")]
    public void DistinctBy() {
        var rows = RowsWithDuplicates();
        AssertSameSequence(() => rows.DistinctBy(static row => row.Category),() => SystemEnumerable.DistinctBy(rows,static row => row.Category));
        AssertSameSequence(() => rows.DistinctBy(static row => row.Category,StringComparer.OrdinalIgnoreCase),() =>{
            Func<Row,string> keySelector=static row => row.Category;
            return rows.DistinctBy(keySelector,StringComparer.OrdinalIgnoreCase);
        });
    }

    [Fact, Trait("Category","Unit")]
    public void Distinct() {
        AssertSameSequence(() => DuplicateNumbers().Distinct(),() => SystemEnumerable.Distinct(DuplicateNumbers()));
        AssertSameSequence(() => UniqueNumbers().Distinct(),   () => SystemEnumerable.Distinct(UniqueNumbers()));
        AssertSameSequence(() => DuplicateNumbers().Distinct(new ModuloComparer(3)),() =>{
            IEqualityComparer<int>? comparer=new ModuloComparer(3);
            return DuplicateNumbers().Distinct(comparer);
        });
    }

    [Fact, Trait("Category","Unit")]
    public void ElementAtOrDefault() {
        var values = DuplicateNumbers();
        AssertSameValue(() => values.ElementAtOrDefault(3),() => SystemEnumerable.ElementAtOrDefault(values,3));
        AssertSameValue(() => values.ElementAtOrDefault(99),() => SystemEnumerable.ElementAtOrDefault(values,99));
        AssertSameValue(() => values.ElementAtOrDefault(^2),() => SystemEnumerable.ElementAtOrDefault(values,^2));
    }

    [Fact, Trait("Category","Unit")]
    public void ElementAt() {
        var values = DuplicateNumbers();
        AssertSameValue(() => values.ElementAt(3),() => SystemEnumerable.ElementAt(values,3));
        AssertSameValue(() => values.ElementAt(^2),() => SystemEnumerable.ElementAt(values,^2));
    }

    [Fact, Trait("Category","Unit")]
    public void Empty() {
        AssertSameSequence(() => [],() => SystemEnumerable.Empty<Row>());
    }

    [Fact, Trait("Category","Unit")]
    public void ExceptBy() {
        var rows = RowsWithDuplicates();
        AssertSameSequence(() => rows.ExceptBy([1,4],static row => row.Id),() => SystemEnumerable.ExceptBy(rows,[1,4],static row => row.Id));
        AssertSameSequence(() => rows.ExceptBy(["ALPHA"],static row => row.Category,StringComparer.OrdinalIgnoreCase),() =>{
            IEnumerable<string> second     =["ALPHA"];
            Func<Row,string>    keySelector=static row => row.Category;
            return rows.ExceptBy(second,keySelector,StringComparer.OrdinalIgnoreCase);
        });
    }

    [Fact, Trait("Category","Unit")]
    public void Except() {
        AssertSameSequence(() => DuplicateNumbers().Except([1,6]),() => SystemEnumerable.Except(DuplicateNumbers(),[1,6
        ]));
        AssertSameSequence(() => DuplicateNumbers().Except([7],new ModuloComparer(3)),() =>{
            IEnumerable<int>        second  =[7];
            IEqualityComparer<int>? comparer=new ModuloComparer(3);
            return DuplicateNumbers().Except(second,comparer);
        });
    }

    [Fact, Trait("Category","Unit")]
    public void FirstOrDefault() {
        var values = DuplicateNumbers();
        AssertSameValue(() => values.FirstOrDefault(),() => SystemEnumerable.FirstOrDefault(values));
        AssertSameValue(() => Array.Empty<int>().FirstOrDefault(42),() => SystemEnumerable.FirstOrDefault([],42));
        AssertSameValue(() => values.FirstOrDefault(static value => value>3),() => SystemEnumerable.FirstOrDefault(values,static value => value>3));
        AssertSameValue(() => values.FirstOrDefault(static value => value>20,42),() => SystemEnumerable.FirstOrDefault(values,static value => value>20,42));
    }

    [Fact, Trait("Category","Unit")]
    public void First() {
        var values = DuplicateNumbers();
        AssertSameValue(() => values.First(),() => SystemEnumerable.First(values));
        AssertSameValue(() => values.First(static value => value>3),() => SystemEnumerable.First(values,static value => value>3));
    }

    [Fact, Trait("Category","Unit")]
    public void GroupBy() {
        var rows = RowsWithDuplicates();
        AssertSameSequence(() => NormalizeGroups(rows.GroupBy(static row => row.Category)),                                                                                                     () => NormalizeGroups(SystemEnumerable.GroupBy(rows,static row => row.Category)));
        AssertSameSequence(() => NormalizeGroups(rows.GroupBy(static row => row.Category,StringComparer.OrdinalIgnoreCase)),                                                                    () => NormalizeGroups(SystemEnumerable.GroupBy(rows,static row => row.Category,StringComparer.OrdinalIgnoreCase)));
        AssertSameSequence(() => NormalizeGroups(rows.GroupBy(static row => row.Category,static row => row.Score)),                                                                             () => NormalizeGroups(SystemEnumerable.GroupBy(rows,static row => row.Category,static row => row.Score)));
        AssertSameSequence(() => NormalizeGroups(rows.GroupBy(static row => row.Category,static row => row.Score,StringComparer.OrdinalIgnoreCase)),                                            () =>{
            Func<Row,string> keySelector    =static row => row.Category;
            Func<Row,int>    elementSelector=static row => row.Score;
            return NormalizeGroups(rows.GroupBy(keySelector,elementSelector,StringComparer.OrdinalIgnoreCase));
        });
        AssertSameSequence(() => rows.GroupBy(static row => row.Category,static (key,                        items) => $"{key}:{items.Count()}"),                               () => SystemEnumerable.GroupBy(rows,static row => row.Category,static (key,                        items) => $"{key}:{items.Count()}"));
        AssertSameSequence(() => rows.GroupBy(static row => row.Category,static row => row.Score,static (key,items) => $"{key}:{items.Sum()}",StringComparer.OrdinalIgnoreCase),() => SystemEnumerable.GroupBy(rows,static row => row.Category,static row => row.Score,static (key,items) => $"{key}:{items.Sum()}",StringComparer.OrdinalIgnoreCase));
    }

    [Fact, Trait("Category","Unit")]
    public void ExceptインラインOuter() {
        var x4 = new int[3].Select(p => p+1).Except(new int[2].Select(p => p+2));
    }
    [Fact, Trait("Category","Unit")]
    public void UnionインラインOuter() {
        var x4 = new int[3].Select(p => p+1).Union(new int[2].Select(p => p+2));
    }
    [Fact, Trait("Category","Unit")]
    public void GroupJoinインラインOuter() {
        var rows = RowsWithDuplicates();
        var orders = Orders();
        var x0 = rows.Select(p => new { p.Id,p.Amount,p.Category,p.Score });
        var x1 = rows.Select(p => new { p.Id,p.Amount,p.Category,p.Score }).ToArray();
        var x4 = new int[3].Select(p =>p+1).Union(new int[2].Select(p => p+2));
        var x2 = new int[3].Select(p =>new { p });
        var x3 = new int[3].Select(p => p+1);
        var x =rows.Select(p=>new{p.Id,p.Amount,p.Category,p.Score}).GroupJoin(
            orders,
            static row => row.Id,
            static order => order.RowId,
            static (row,items) => $"{row.Id}:{items.Count()}"
        ).ToArray();
        AssertSameSequence(() => rows.GroupJoin(orders,static row => row.Id,static order => order.RowId,static (row,items) => $"{row.Id}:{items.Sum(static item => item.Quantity)}",EqualityComparer<int>.Default),() => SystemEnumerable.GroupJoin(rows,orders,static row => row.Id,static order => order.RowId,static (row,items) => $"{row.Id}:{items.Sum(static item => item.Quantity)}",EqualityComparer<int>.Default));
    }
    [Fact, Trait("Category","Unit")]
    public void GroupJoin() {
        var rows = RowsWithDuplicates();
        var orders = Orders();
        AssertSameSequence(() => rows.GroupJoin(orders,static row => row.Id,static order => order.RowId,static (row,                 items) => $"{row.Id}:{items.Count()}"),                                                                         () => SystemEnumerable.GroupJoin(rows,orders,static row => row.Id,static order => order.RowId,static (row,items) => $"{row.Id}:{items.Count()}"));
        AssertSameSequence(() => rows.GroupJoin(orders,static row => row.Id,static order => order.RowId,static (row,items) => $"{row.Id}:{items.Sum(static item => item.Quantity)}",EqualityComparer<int>.Default),() => SystemEnumerable.GroupJoin(rows,orders,static row => row.Id,static order => order.RowId,static (row,items) => $"{row.Id}:{items.Sum(static item => item.Quantity)}",EqualityComparer<int>.Default));
    }

    [Fact, Trait("Category","Unit")]
    public void Index() {
        AssertSameSequence(() => DuplicateNumbers().Index(),() => SystemEnumerable.Index(DuplicateNumbers()));
    }

    [Fact, Trait("Category","Unit")]
    public void InfiniteSequence() {
        AssertSameSequence(() => SystemEnumerable.InfiniteSequence(3,4).Take(6),() => SystemEnumerable.InfiniteSequence(3,4).Take(6));
    }

    [Fact, Trait("Category","Unit")]
    public void IntersectBy() {
        var rows = RowsWithDuplicates();
        AssertSameSequence(() => rows.IntersectBy([1,4,9],static row => row.Id),() => SystemEnumerable.IntersectBy(rows,
            [1,4,9],static row => row.Id));
        AssertSameSequence(() => rows.IntersectBy(["ALPHA","missing"],static row => row.Category,StringComparer.OrdinalIgnoreCase),() => SystemEnumerable.IntersectBy(rows,
            ["ALPHA","missing"],static row => row.Category,StringComparer.OrdinalIgnoreCase));
    }

    [Fact, Trait("Category","Unit")]
    public void Intersect() {
        AssertSameSequence(() => DuplicateNumbers().Intersect([1,4,9]),() => SystemEnumerable.Intersect(DuplicateNumbers(),
            [1,4,9]));
        AssertSameSequence(() => DuplicateNumbers().Intersect([7,8],new ModuloComparer(3)),() => SystemEnumerable.Intersect(DuplicateNumbers(),
            [7,8],new ModuloComparer(3)));
    }

    [Fact, Trait("Category","Unit")]
    public void Join() {
        var rows = RowsWithDuplicates();
        var orders = Orders();
        AssertSameSequence(() => rows.Join(orders,static row => row.Id,static order => order.RowId,static (row,order) => $"{row.Id}:{order.Code}"),() => SystemEnumerable.Join(rows,orders,static row => row.Id,static order => order.RowId,static (row,order) => $"{row.Id}:{order.Code}"));
        AssertSameSequence(() => rows.Join(orders,static row => row.Id,static order => order.RowId,static (row,order) => $"{row.Id}:{order.Quantity}",EqualityComparer<int>.Default),() => {
            Func<Row,int> outerKeySelector = static row => row.Id;
            Func<TestOrder,int> innerKeySelector = static order => order.RowId;
            Func<Row,TestOrder,string> resultSelector = static (row,order) => $"{row.Id}:{order.Quantity}";
            var result = new List<string>();
            IEnumerable<TestOrder> source=(IEnumerable<TestOrder>)orders;
            Func<TestOrder,TestOrder> elementSelector=static item => item;
            var lookup1=new global::Tsavorite.Linq.LookupImpl<int,TestOrder>(EqualityComparer<int>.Default);
            foreach(var item in source) {
                lookup1.Add(innerKeySelector(item),elementSelector(item));
            }
            var lookup = (ILookup<int,TestOrder>)lookup1;
            foreach(var outerItem in rows) {
                foreach(var innerItem in lookup[outerKeySelector(outerItem)]) {
                    result.Add(resultSelector(outerItem,innerItem));
                }
            }
            return result;
        });
    }

    [Fact, Trait("Category","Unit")]
    public void LastOrDefault() {
        var values = DuplicateNumbers();
        AssertSameValue(() => values.LastOrDefault(),() => SystemEnumerable.LastOrDefault(values));
        AssertSameValue(() => Array.Empty<int>().LastOrDefault(42),() => SystemEnumerable.LastOrDefault([],42));
        AssertSameValue(() => values.LastOrDefault(static value => value<3),() => SystemEnumerable.LastOrDefault(values,static value => value<3));
        AssertSameValue(() => values.LastOrDefault(static value => value>20,42),() => SystemEnumerable.LastOrDefault(values,static value => value>20,42));
    }

    [Fact, Trait("Category","Unit")]
    public void Last() {
        var values = DuplicateNumbers();
        AssertSameValue(() => values.Last(),() => SystemEnumerable.Last(values));
        AssertSameValue(() => values.Last(static value => value<3),() => SystemEnumerable.Last(values,static value => value<3));
    }

    [Fact, Trait("Category","Unit")]
    public void LeftJoin() {
        var rows = RowsWithDuplicates();
        var orders = Orders();
        AssertSameSequence(() => rows.LeftJoin(orders,static row => row.Id,static order => order.RowId,static (row,order) => $"{row.Id}:{order?.Code??"none"}"),() => SystemEnumerable.LeftJoin(rows,orders,static row => row.Id,static order => order.RowId,static (row,order) => $"{row.Id}:{order?.Code??"none"}"));
        AssertSameSequence(() => rows.LeftJoin(orders,static row => row.Id,static order => order.RowId,static (row,order) => $"{row.Id}:{order?.Quantity??0}",EqualityComparer<int>.Default),() => SystemEnumerable.LeftJoin(rows,orders,static row => row.Id,static order => order.RowId,static (row,order) => $"{row.Id}:{order?.Quantity??0}",EqualityComparer<int>.Default));
    }

    [Fact, Trait("Category","Unit")]
    public void LongCount() {
        var rows = RowsWithDuplicates();
        AssertSameValue(() => rows.LongCount(),() => SystemEnumerable.LongCount(rows));
        AssertSameValue(() => rows.LongCount(static row => row.Score>=10),() => SystemEnumerable.LongCount(rows,static row => row.Score>=10));
    }

    [Fact, Trait("Category","Unit")]
    public void Max() {
        var rows = RowsWithDuplicates();
        AssertNumericMax(rows);
        AssertSameValue(() => DuplicateNumbers().Max(),                       () => SystemEnumerable.Max(DuplicateNumbers()));
        AssertSameValue<int>(() => DuplicateNumbers().Max(Comparer<int>.Default),() =>{
            IEnumerable<int> source=DuplicateNumbers();
            IComparer<int>? comparer1=Comparer<int>.Default;
            return SystemEnumerable.Max(source,comparer1);
        });
        AssertSameValue(() => rows.Max(static row => row.Note),() => SystemEnumerable.Max(rows,static row => row.Note));
    }

    [Fact, Trait("Category","Unit")]
    public void MaxBy() {
        var rows = RowsWithDuplicates();
        AssertSameValue(() => rows.MaxBy(static row => row.Score),() => SystemEnumerable.MaxBy(rows,static row => row.Score));
        AssertSameValue(() => rows.MaxBy(static row => row.Score,Comparer<int>.Default),() => SystemEnumerable.MaxBy(rows,static row => row.Score,Comparer<int>.Default));
    }

    [Fact, Trait("Category","Unit")]
    public void Min() {
        var rows = RowsWithDuplicates();
        AssertNumericMin(rows);
        AssertSameValue(() => DuplicateNumbers().Min(),() => SystemEnumerable.Min(DuplicateNumbers()));
        AssertSameValue(() => DuplicateNumbers().Min(Comparer<int>.Default),() => SystemEnumerable.Min(DuplicateNumbers(),Comparer<int>.Default));
        AssertSameValue(() => rows.Min(static row => row.Note),() => SystemEnumerable.Min(rows,static row => row.Note));
    }

    [Fact, Trait("Category","Unit")]
    public void MinBy() {
        var rows = RowsWithDuplicates();
        AssertSameValue(() => rows.MinBy(static row => row.Score),() => SystemEnumerable.MinBy(rows,static row => row.Score));
        AssertSameValue(() => rows.MinBy(static row => row.Score,Comparer<int>.Default),() => SystemEnumerable.MinBy(rows,static row => row.Score,Comparer<int>.Default));
    }

    [Fact, Trait("Category","Unit")]
    public void OfType() {
        var values = new ArrayList { 1,"two",2,"three",2 };
        AssertSameSequence(() => values.OfType<int>(),() => SystemEnumerable.OfType<int>(values));
    }

    [Fact, Trait("Category","Unit")]
    public void OrderByDescending() {
        var rows = RowsWithDuplicates();
        AssertSameSequence(() => rows.OrderByDescending(static row => row.Score),() => SystemEnumerable.OrderByDescending(rows,static row => row.Score));
        AssertSameSequence(() => rows.OrderByDescending(static row => row.Score,Comparer<int>.Default),() => SystemEnumerable.OrderByDescending(rows,static row => row.Score,Comparer<int>.Default));
    }

    [Fact, Trait("Category","Unit")]
    public void OrderBy() {
        var rows = RowsWithDuplicates();
        AssertSameSequence(() => rows.OrderBy(static row => row.Category),() => SystemEnumerable.OrderBy(rows,static row => row.Category));
        AssertSameSequence(() => rows.OrderBy(static row => row.Category,StringComparer.OrdinalIgnoreCase),() => SystemEnumerable.OrderBy(rows,static row => row.Category,StringComparer.OrdinalIgnoreCase));
    }

    [Fact, Trait("Category","Unit")]
    public void OrderDescending() {
        AssertSameSequence(() => DuplicateNumbers().OrderDescending(),() => SystemEnumerable.OrderDescending(DuplicateNumbers()));
        AssertSameSequence(() => DuplicateNumbers().OrderDescending(Comparer<int>.Default),() => SystemEnumerable.OrderDescending(DuplicateNumbers(),Comparer<int>.Default));
    }

    [Fact, Trait("Category","Unit")]
    public void Order() {
        AssertSameSequence(() => DuplicateNumbers().Order(),() => SystemEnumerable.Order(DuplicateNumbers()));
        AssertSameSequence(() => DuplicateNumbers().Order(new DescendingIntComparer()),() => SystemEnumerable.Order(DuplicateNumbers(),new DescendingIntComparer()));
    }

    [Fact, Trait("Category","Unit")]
    public void Prepend() {
        AssertSameSequence(() => DuplicateNumbers().Prepend(9),() => SystemEnumerable.Prepend(DuplicateNumbers(),9));
    }

    [Fact, Trait("Category","Unit")]
    public void Range() {
        AssertSameSequence(() => SystemEnumerable.Range(-3,7),() => SystemEnumerable.Range(-3,7));
    }

    [Fact, Trait("Category","Unit")]
    public void Repeat() {
        AssertSameSequence(() => SystemEnumerable.Repeat("repeat",4),() => SystemEnumerable.Repeat("repeat",4));
    }

    [Fact, Trait("Category","Unit")]
    public void Reverse() {
        AssertSameSequence(() => DuplicateNumbers().Reverse(),() => SystemEnumerable.Reverse(DuplicateNumbers()));
        AssertSameSequence(() => UniqueNumbers().Reverse(),   () => SystemEnumerable.Reverse(UniqueNumbers()));
    }

    [Fact, Trait("Category","Unit")]
    public void RightJoin() {
        var rows = RowsWithDuplicates();
        var orders = Orders();
        AssertSameSequence(() => rows.RightJoin(orders,static row => row.Id,static order => order.RowId,static (row,order) => $"{row?.Id??0}:{order.Code}"),() => SystemEnumerable.RightJoin(rows,orders,static row => row.Id,static order => order.RowId,static (row,order) => $"{row?.Id??0}:{order.Code}"));
        AssertSameSequence(() => rows.RightJoin(orders,static row => row.Id,static order => order.RowId,static (row,order) => $"{row?.Id??0}:{order.Quantity}",EqualityComparer<int>.Default),() => SystemEnumerable.RightJoin(rows,orders,static row => row.Id,static order => order.RowId,static (row,order) => $"{row?.Id??0}:{order.Quantity}",EqualityComparer<int>.Default));
    }

    [Fact, Trait("Category","Unit")]
    public void SelectMany() {
        var rows = RowsWithDuplicates();
        AssertSameSequence(() => rows.SelectMany(static row => new[] { row.Id,row.Score }),() => SystemEnumerable.SelectMany(rows,static row => new[] { row.Id,row.Score }));
        AssertSameSequence(() => rows.SelectMany(static (row,index) => new[] { row.Id+index,row.Score-index }),() => SystemEnumerable.SelectMany(rows,static (row,index) => new[] { row.Id+index,row.Score-index }));
        AssertSameSequence(() => rows.SelectMany(static row => new[] { row.Id,row.Score },static (row,value) => $"{row.Category}:{value}"),() => SystemEnumerable.SelectMany(rows,static row => new[] { row.Id,row.Score },static (row,value) => $"{row.Category}:{value}"));
        AssertSameSequence(() => rows.SelectMany(static (row,index) => new[] { row.Id+index,row.Score-index },static (row,value) => $"{row.Category}:{value}"),() => SystemEnumerable.SelectMany(rows,static (row,index) => new[] { row.Id+index,row.Score-index },static (row,value) => $"{row.Category}:{value}"));
    }

    [Fact, Trait("Category","Unit")]
    public void Select() {
        var rows = RowsWithDuplicates();
        AssertSameSequence(() => rows.Select(static row => $"{row.Category}:{row.Score}"),() => SystemEnumerable.Select(rows,static row => $"{row.Category}:{row.Score}"));
        AssertSameSequence(() => rows.Select(static (row,index) => $"{index}:{row.Id}:{row.Score}"),() => SystemEnumerable.Select(rows,static (row,index) => $"{index}:{row.Id}:{row.Score}"));
        AssertSameException<ArgumentNullException>(() => { _=((IEnumerable<int>)null!).Select(static value => value); },() => { _=SystemEnumerable.Select((IEnumerable<int>)null!,static value => value); });
        AssertSameException<ArgumentNullException>(() => { _=new[] { 1 }.Select((Func<int,int>)null!); },() => { _=SystemEnumerable.Select(
            [1],(Func<int,int>)null!); });
    }

    [Fact, Trait("Category","Unit")]
    public void Sequence() {
        AssertSameSequence(() => SystemEnumerable.Sequence(1,9,2),() => SystemEnumerable.Sequence(1,9,2));
        AssertSameSequence(() => SystemEnumerable.Sequence(5,1,-2),() => SystemEnumerable.Sequence(5,1,-2));
        AssertSameSequence(() => SystemEnumerable.Sequence(1.5m,4.5m,1.5m),() => SystemEnumerable.Sequence(1.5m,4.5m,1.5m));
    }

    [Fact, Trait("Category","Unit")]
    public void SequenceEqual() {
        AssertSameValue(() => SystemEnumerable.SequenceEqual(DuplicateNumbers(),DuplicateNumbers()),() => SystemEnumerable.SequenceEqual(DuplicateNumbers(),DuplicateNumbers()));
        AssertSameValue(() => SystemEnumerable.SequenceEqual(DuplicateNumbers(),UniqueNumbers()),() => SystemEnumerable.SequenceEqual(DuplicateNumbers(),UniqueNumbers()));
        AssertSameValue(() => SystemEnumerable.SequenceEqual([1,4,7],[7,10,13],new ModuloComparer(3)),() => SystemEnumerable.SequenceEqual(
            [1,4,7],[7,10,13],new ModuloComparer(3)));
    }

    [Fact, Trait("Category","Unit")]
    public void Shuffle() {
        var values = DuplicateNumbers();
        Assert.Equal(values.Shuffle().Order().ToArray(),SystemEnumerable.Shuffle(values).Order().ToArray());
    }

    [Fact, Trait("Category","Unit")]
    public void SingleOrDefault() {
        AssertSameValue(() => new[] { 7 }.SingleOrDefault(),() => SystemEnumerable.SingleOrDefault([7]));
        AssertSameValue(() => Array.Empty<int>().SingleOrDefault(42),() => SystemEnumerable.SingleOrDefault([],42));
        AssertSameValue(() => DuplicateNumbers().SingleOrDefault(static value => value==6),() => SystemEnumerable.SingleOrDefault(DuplicateNumbers(),static value => value==6));
        AssertSameValue(() => DuplicateNumbers().SingleOrDefault(static value => value==99,42),() => SystemEnumerable.SingleOrDefault(DuplicateNumbers(),static value => value==99,42));
    }

    [Fact, Trait("Category","Unit")]
    public void Single() {
        AssertSameValue(() => new[] { 7 }.Single(),() => SystemEnumerable.Single([7]));
        AssertSameValue(() => DuplicateNumbers().Single(static value => value==6),() => SystemEnumerable.Single(DuplicateNumbers(),static value => value==6));
    }

    [Fact, Trait("Category","Unit")]
    public void SkipLast() {
        AssertSameSequence(() => DuplicateNumbers().SkipLast(2),() => SystemEnumerable.SkipLast(DuplicateNumbers(),2));
    }

    [Fact, Trait("Category","Unit")]
    public void SkipWhile() {
        AssertSameSequence(() => DuplicateNumbers().SkipWhile(static value => value<5),() => SystemEnumerable.SkipWhile(DuplicateNumbers(),static value => value<5));
        AssertSameSequence(() => DuplicateNumbers().SkipWhile(static (value,index) => value+index<7),() => SystemEnumerable.SkipWhile(DuplicateNumbers(),static (value,index) => value+index<7));
    }

    [Fact, Trait("Category","Unit")]
    public void Skip() {
        AssertSameSequence(() => DuplicateNumbers().Skip(3),() => SystemEnumerable.Skip(DuplicateNumbers(),3));
    }

    [Fact, Trait("Category","Unit")]
    public void Sum() {
        var rows = RowsWithDuplicates();
        AssertNumericSum(rows);
    }

    [Fact, Trait("Category","Unit")]
    public void TakeLast() {
        AssertSameSequence(() => DuplicateNumbers().TakeLast(3),() => SystemEnumerable.TakeLast(DuplicateNumbers(),3));
    }

    [Fact, Trait("Category","Unit")]
    public void TakeWhile() {
        AssertSameSequence(() => DuplicateNumbers().TakeWhile(static value => value<5),() => SystemEnumerable.TakeWhile(DuplicateNumbers(),static value => value<5));
        AssertSameSequence(() => DuplicateNumbers().TakeWhile(static (value,index) => value+index<8),() => SystemEnumerable.TakeWhile(DuplicateNumbers(),static (value,index) => value+index<8));
    }

    [Fact, Trait("Category","Unit")]
    public void Take() {
        AssertSameSequence(() => DuplicateNumbers().Take(5),() => SystemEnumerable.Take(DuplicateNumbers(),5));
        AssertSameSequence(() => DuplicateNumbers().Take(2..^1),() => SystemEnumerable.Take(DuplicateNumbers(),2..^1));
    }

    [Fact, Trait("Category","Unit")]
    public void ThenByDescending() {
        var rows = RowsWithDuplicates();
        AssertSameSequence(() => rows.OrderBy(static row => row.Category).ThenByDescending(static row => row.Score),() => SystemEnumerable.ThenByDescending(SystemEnumerable.OrderBy(rows,static row => row.Category),static row => row.Score));
        AssertSameSequence(() => rows.OrderBy(static row => row.Category).ThenByDescending(static row => row.Score,Comparer<int>.Default),() => SystemEnumerable.ThenByDescending(SystemEnumerable.OrderBy(rows,static row => row.Category),static row => row.Score,Comparer<int>.Default));
    }

    [Fact, Trait("Category","Unit")]
    public void ThenBy() {
        var rows = RowsWithDuplicates();
        AssertSameSequence(() => rows.OrderBy(static row => row.Category).ThenBy(static row => row.Score),() => SystemEnumerable.ThenBy(SystemEnumerable.OrderBy(rows,static row => row.Category),static row => row.Score));
        AssertSameSequence(() => rows.OrderBy(static row => row.Category).ThenBy(static row => row.Score,Comparer<int>.Default),() => SystemEnumerable.ThenBy(SystemEnumerable.OrderBy(rows,static row => row.Category),static row => row.Score,Comparer<int>.Default));
    }

    [Fact, Trait("Category","Unit")]
    public void ToArray() {
        AssertSameSequence(() => DuplicateNumbers().ToArray(),() => SystemEnumerable.ToArray(DuplicateNumbers()));
    }

    [Fact, Trait("Category","Unit")]
    public void ToDictionary() {
        var rows = RowsWithoutDuplicateKeys();
        AssertSameSequence(() => NormalizeDictionary(new[] { new KeyValuePair<string,int>("a",1),new KeyValuePair<string,int>("b",2) }.ToDictionary()),                                () => NormalizeDictionary(SystemEnumerable.ToDictionary(
            [new KeyValuePair<string,int>("a",1),new KeyValuePair<string,int>("b",2)])));
        AssertSameSequence(() => NormalizeDictionary(new[] { new KeyValuePair<string,int>("a",1),new KeyValuePair<string,int>("B",2) }.ToDictionary(StringComparer.OrdinalIgnoreCase)),() => NormalizeDictionary(SystemEnumerable.ToDictionary(
            [new KeyValuePair<string,int>("a",1),new KeyValuePair<string,int>("B",2)],StringComparer.OrdinalIgnoreCase)));
        AssertSameSequence(() => NormalizeDictionary(new[] { ("a",1),("b",2) }.ToDictionary()),                                                                                        () => NormalizeDictionary(SystemEnumerable.ToDictionary(
            [("a",1),("b",2)])));
        AssertSameSequence(() => NormalizeDictionary(new[] { ("a",1),("B",2) }.ToDictionary(StringComparer.OrdinalIgnoreCase)),                                                        () => NormalizeDictionary(SystemEnumerable.ToDictionary(
            [("a",1),("B",2)],StringComparer.OrdinalIgnoreCase)));
        AssertSameSequence(() => NormalizeDictionary(rows.ToDictionary(static row => row.Id)),                                                                                         () => NormalizeDictionary(SystemEnumerable.ToDictionary(rows,                     static row => row.Id)));
        AssertSameSequence(() => NormalizeDictionary(rows.ToDictionary(static row => row.Category,StringComparer.OrdinalIgnoreCase)),                                                  () => NormalizeDictionary(SystemEnumerable.ToDictionary(rows,                     static row => row.Category,StringComparer.OrdinalIgnoreCase)));
        AssertSameSequence(() => NormalizeDictionary(rows.ToDictionary(static row => row.Id,      static row => row.Score)),                                                           () => NormalizeDictionary(SystemEnumerable.ToDictionary(rows,                     static row => row.Id,      static row => row.Score)));
        AssertSameSequence(() => NormalizeDictionary(rows.ToDictionary(static row => row.Category,static row => row.Score,StringComparer.OrdinalIgnoreCase)),                          () => NormalizeDictionary(SystemEnumerable.ToDictionary(rows,                     static row => row.Category,static row => row.Score,StringComparer.OrdinalIgnoreCase)));
        AssertSameException<ArgumentException>(() => { _=RowsWithDuplicates().ToDictionary(static row => row.Id); },() => { _=SystemEnumerable.ToDictionary(RowsWithDuplicates(),                                                                       static row => row.Id); });
    }

    [Fact, Trait("Category","Unit")]
    public void ToHashSet() {
        AssertSameSequence(() => DuplicateNumbers().ToHashSet().Order(),() => SystemEnumerable.ToHashSet(DuplicateNumbers()).Order());
        AssertSameSequence(() => DuplicateNumbers().ToHashSet(new ModuloComparer(3)).Order(),() =>{
            IEnumerable<int> source=DuplicateNumbers();
            var set=new HashSet<int>(new ModuloComparer(3));
            foreach(var item in source) {
                set.Add(item);
            }
            return set.Order();
        });
    }

    [Fact, Trait("Category","Unit")]
    public void ToList() {
        AssertSameSequence(() => DuplicateNumbers().ToList(),() =>{
            IEnumerable<int> source=DuplicateNumbers();
            var result=new List<int>();
            foreach(var item in source){
                result.Add(item);
            }
            return result;
        });
    }

    [Fact, Trait("Category","Unit")]
    public void ToLookup() {
        var rows = RowsWithDuplicates();
        AssertSameSequence(() => NormalizeLookup(rows.ToLookup(static row => row.Category)),                                 () => NormalizeLookup(SystemEnumerable.ToLookup(rows,static row => row.Category)));
        AssertSameSequence(() => NormalizeLookup(rows.ToLookup(static row => row.Category,StringComparer.OrdinalIgnoreCase)),() =>{
            Func<Row,string> keySelector=static row => row.Category;
            IEqualityComparer<string>? comparer=StringComparer.OrdinalIgnoreCase;
            comparer??=EqualityComparer<string>.Default;
            var lookup=new global::Tsavorite.Linq.LookupImpl<string,Row>(comparer);
            foreach(var item in rows) {
                lookup.Add(keySelector(item),item);
            }
            return NormalizeLookup(lookup);
        });
        AssertSameSequence(() => NormalizeLookup(rows.ToLookup(static row => row.Category,static row => row.Score)),         () => NormalizeLookup(SystemEnumerable.ToLookup(rows,static row => row.Category,static row => row.Score)));
        AssertSameSequence(() => NormalizeLookup(rows.ToLookup(static row => row.Category,static row => row.Score,StringComparer.OrdinalIgnoreCase)),() =>{
            Func<Row,string> keySelector=static row => row.Category;
            Func<Row,int> elementSelector=static row => row.Score;
            var lookup=new global::Tsavorite.Linq.LookupImpl<string,int>(StringComparer.OrdinalIgnoreCase);
            foreach(var item in rows) {
                lookup.Add(keySelector(item),elementSelector(item));
            }
            return NormalizeLookup(lookup);
        });
    }

    [Fact, Trait("Category","Unit")]
    public void TryGetNonEnumeratedCount() {
        AssertSameCountResult(DuplicateNumbers(),DuplicateNumbers());
    }

    [Fact, Trait("Category","Unit")]
    public void UnionBy() {
        var first = RowsWithDuplicates();
        var second = new[] { new Row(10,"alpha",100,10m,"new"),new Row(11,"delta",110,11m,"new") };
        AssertSameSequence(() => first.UnionBy(second,static row => row.Category),() => SystemEnumerable.UnionBy(first,second,static row => row.Category));
        AssertSameSequence(() => first.UnionBy(second,static row => row.Category,StringComparer.OrdinalIgnoreCase),() => SystemEnumerable.UnionBy(first,second,static row => row.Category,StringComparer.OrdinalIgnoreCase));
    }

    [Fact, Trait("Category","Unit")]
    public void Union() {
        AssertSameSequence(() => DuplicateNumbers().Union([1,7,8]),() => SystemEnumerable.Union(DuplicateNumbers(),[1,7,8
        ]));
        AssertSameSequence(() => DuplicateNumbers().Union([7,8],new ModuloComparer(3)),() => SystemEnumerable.Union(DuplicateNumbers(),
            [7,8],new ModuloComparer(3)));
    }

    [Fact, Trait("Category","Unit")]
    public void Where() {
        AssertSameSequence(() => DuplicateNumbers().Where(static value => value%2==0),() => SystemEnumerable.Where(DuplicateNumbers(),static value => value%2==0));
        AssertSameSequence(() => DuplicateNumbers().Where(static (value,index) => (value+index)%2==0),() => SystemEnumerable.Where(DuplicateNumbers(),static (value,index) => (value+index)%2==0));
    }

    [Fact, Trait("Category","Unit")]
    public void Zip() {
        AssertSameSequence(() => new[] { 1,2,3 }.Zip(["a","b"]),() => SystemEnumerable.Zip([1,2,3],["a","b"]));
        AssertSameSequence(() => new[] { 1,2,3 }.Zip(["a","b","c"],[10m,20m]),() => SystemEnumerable.Zip([1,2,3],["a","b","c"
        ],[10m,20m]));
        AssertSameSequence(() => new[] { 1,2,3 }.Zip([10,20],static (left,right) => left+right),() => SystemEnumerable.Zip(
            [1,2,3],[10,20],static (left,right) => left+right));
    }

    [Fact, Trait("Category","Unit")]
    public void SequenceOperatorsCoverRepresentativeTypes() {
        AssertCoreSequenceOperators(["beta","alpha","beta","gamma"],"omega",static value => value.Length>4,static value => value.ToUpperInvariant());
        AssertCoreSequenceOperators([2.5m,1.5m,2.5m,4.5m],9.5m,static value => value>2m,static value => $"{value:0.0}");
        AssertCoreSequenceOperators([2.5d,1.5d,2.5d,4.5d],9.5d,static value => value>2d,static value => $"{value:0.0}");
        AssertCoreSequenceOperators(
            [
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Guid.Parse("11111111-1111-1111-1111-111111111111")
            ],
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            static value => value.ToString("N")[0]=='1',
            static value => value.ToString("N"));
        AssertCoreSequenceOperators(
            [new DateTime(2026,4,1),new DateTime(2026,4,2),new DateTime(2026,4,1)],
            new DateTime(2026,4,30),
            static value => value.Day>1,
            static value => value.ToString("yyyyMMdd"));
        AssertCoreSequenceOperators(
            [new Row(1,"alpha",10,1.5m,"one"),new Row(2,"beta",20,2.5m,null),new Row(1,"alpha",10,1.5m,"one")],
            new Row(3,"gamma",30,3.5m,"three"),
            static value => value.Score>=10,
            static value => $"{value.Category}:{value.Amount}");
    }

    //[Fact, Trait("Category","Unit")]
    //public void ExplicitStorageEnumerableVariants() {
    //    var source = new[] { 1,2,3,4 };

    //    AssertSequenceAndDispose(new[] { 2,4 },global::Tsavorite.Linq.UnmanagedEnumerable.Where(source,static value => value%2==0));
    //    AssertSequenceAndDispose(new[] { 2,4 },global::Tsavorite.Linq.UnmanagedEnumerable.Where<int,EvenStrategy>(source));
    //    AssertSequenceAndDispose(new[] { 2,4,6,8 },global::Tsavorite.Linq.UnmanagedEnumerable.Select<int,int,DoubleStrategy>(source));
    //    AssertSequenceAndDispose(new[] { "v1","v2","v3","v4" },global::Tsavorite.Linq.ManagedEnumerable.Select(source,static value => "v"+value));
    //    AssertSequenceAndDispose(new[] { (1,10),(1,20),(2,10),(2,20) },global::Tsavorite.Linq.ManagedEnumerable.SelectMany(new[] { 1,2 },static _ => new[] { 10,20 },static (outer,inner) => (outer,inner)));
    //}

    private static void AssertCoreSequenceOperators<T>(T[] values,T extra,Func<T,bool> predicate,Func<T,string> selector) {
        AssertSameSequence(() => values.Where(predicate),                                                  () => SystemEnumerable.Where(values,predicate));
        AssertSameSequence(() => values.Select(selector),                                                  () => SystemEnumerable.Select(values,selector));
        AssertSameSequence(() => values.SelectMany(value => new[] { value,extra }),                        () => SystemEnumerable.SelectMany(values,value => new[] { value,extra }));
        AssertSameSequence(() => values.Concat([extra]),                                           () => SystemEnumerable.Concat(values,
            [extra]));
        AssertSameSequence(() => values.Append(extra),                                                     () => SystemEnumerable.Append(values,extra));
        AssertSameSequence(() => values.Prepend(extra),                                                    () => SystemEnumerable.Prepend(values,extra));
        AssertSameSequence(() => values.Distinct(),                                                        () => SystemEnumerable.Distinct(values));
        AssertSameSequence(() => values.Skip(1),                                          () => SystemEnumerable.Skip(values,1));
        AssertSameSequence(() => values.Take(3),                                          () => SystemEnumerable.Take(values,3));
        AssertSameSequence(() => Array.Empty<T>().DefaultIfEmpty(extra),                  () => SystemEnumerable.DefaultIfEmpty([],extra));
        AssertSameSequence(() => values.Zip(SystemEnumerable.Repeat(extra,values.Length)),() => SystemEnumerable.Zip(values,SystemEnumerable.Repeat(extra,values.Length)));
    }

    private static void AssertSequenceAndDispose<T>(IEnumerable<T> expected,IEnumerable<T> actual) {
        try {
            Assert.Equal(expected,actual);
        }
        finally {
            (actual as IDisposable)?.Dispose();
        }
    }

    private static void AssertNumericMax(Row[] rows) {
        AssertSameValue(() => new[] { 1.5m,2.5m,2.5m }.Max(),                               () => SystemEnumerable.Max([
            1.5m,2.5m,2.5m
        ]));
        AssertSameValue(() => new[] { 1.5d,2.5d,2.5d }.Max(),                               () => SystemEnumerable.Max([
            1.5d,2.5d,2.5d
        ]));
        AssertSameValue(() => new[] { 1,2,2 }.Max(),                                        () => SystemEnumerable.Max([
            1,2,2
        ]));
        AssertSameValue(() => new[] { 1L,2L,2L }.Max(),                                     () => SystemEnumerable.Max([
            1L,2L,2L
        ]));
        AssertSameValue(() => new[] { 1.5f,2.5f,2.5f }.Max(),                               () => SystemEnumerable.Max([
            1.5f,2.5f,2.5f
        ]));
        AssertSameValue(() => new decimal?[] { 1.5m,null,2.5m }.Max(),                      () => SystemEnumerable.Max([
            1.5m,null,2.5m
        ]));
        AssertSameValue(() => new double?[] { 1.5d,null,2.5d }.Max(),                       () => SystemEnumerable.Max([
            1.5d,null,2.5d
        ]));
        AssertSameValue(() => new int?[] { 1,null,2 }.Max(),                                () => SystemEnumerable.Max([
            1,null,2
        ]));
        AssertSameValue(() => new long?[] { 1L,null,2L }.Max(),                             () => SystemEnumerable.Max([
            1L,null,2L
        ]));
        AssertSameValue(() => new float?[] { 1.5f,null,2.5f }.Max(),                        () => SystemEnumerable.Max([
            1.5f,null,2.5f
        ]));
        AssertSameValue(() => rows.Max(static row => row.Amount),          () => SystemEnumerable.Max(rows,static row => row.Amount));
        AssertSameValue(() => rows.Max(static row => (double)row.Score),   () => SystemEnumerable.Max(rows,static row => (double)row.Score));
        AssertSameValue(() => rows.Max(static row => row.Score),           () => SystemEnumerable.Max(rows,static row => row.Score));
        AssertSameValue(() => rows.Max(static row => (long)row.Score),     () => SystemEnumerable.Max(rows,static row => (long)row.Score));
        AssertSameValue(() => rows.Max(static row => (float)row.Score),    () => SystemEnumerable.Max(rows,static row => (float)row.Score));
        AssertSameValue(() => rows.Max(static row => (decimal?)row.Amount),() => SystemEnumerable.Max(rows,static row => (decimal?)row.Amount));
        AssertSameValue(() => rows.Max(static row => (double?)row.Score),  () => SystemEnumerable.Max(rows,static row => (double?)row.Score));
        AssertSameValue(() => rows.Max(static row => (int?)row.Score),     () => SystemEnumerable.Max(rows,static row => (int?)row.Score));
        AssertSameValue(() => rows.Max(static row => (long?)row.Score),    () => SystemEnumerable.Max(rows,static row => (long?)row.Score));
        AssertSameValue(() => rows.Max(static row => (float?)row.Score),   () => SystemEnumerable.Max(rows,static row => (float?)row.Score));
    }

    private static void AssertNumericMin(Row[] rows) {
        AssertSameValue(() => new[] { 1.5m,2.5m,2.5m }.Min(),                               () => SystemEnumerable.Min([
            1.5m,2.5m,2.5m
        ]));
        AssertSameValue(() => new[] { 1.5d,2.5d,2.5d }.Min(),                               () => SystemEnumerable.Min([
            1.5d,2.5d,2.5d
        ]));
        AssertSameValue(() => new[] { 1,2,2 }.Min(),                                        () => SystemEnumerable.Min([
            1,2,2
        ]));
        AssertSameValue(() => new[] { 1L,2L,2L }.Min(),                                     () => SystemEnumerable.Min([
            1L,2L,2L
        ]));
        AssertSameValue(() => new[] { 1.5f,2.5f,2.5f }.Min(),                               () => SystemEnumerable.Min([
            1.5f,2.5f,2.5f
        ]));
        AssertSameValue(() => new decimal?[] { 1.5m,null,2.5m }.Min(),                      () => SystemEnumerable.Min([
            1.5m,null,2.5m
        ]));
        AssertSameValue(() => new double?[] { 1.5d,null,2.5d }.Min(),                       () => SystemEnumerable.Min([
            1.5d,null,2.5d
        ]));
        AssertSameValue(() => new int?[] { 1,null,2 }.Min(),                                () => SystemEnumerable.Min([
            1,null,2
        ]));
        AssertSameValue(() => new long?[] { 1L,null,2L }.Min(),                             () => SystemEnumerable.Min([
            1L,null,2L
        ]));
        AssertSameValue(() => new float?[] { 1.5f,null,2.5f }.Min(),                        () => SystemEnumerable.Min([
            1.5f,null,2.5f
        ]));
        AssertSameValue(() => rows.Min(static row => row.Amount),          () => SystemEnumerable.Min(rows,static row => row.Amount));
        AssertSameValue(() => rows.Min(static row => (double)row.Score),   () => SystemEnumerable.Min(rows,static row => (double)row.Score));
        AssertSameValue(() => rows.Min(static row => row.Score),           () => SystemEnumerable.Min(rows,static row => row.Score));
        AssertSameValue(() => rows.Min(static row => (long)row.Score),     () => SystemEnumerable.Min(rows,static row => (long)row.Score));
        AssertSameValue(() => rows.Min(static row => (float)row.Score),    () => SystemEnumerable.Min(rows,static row => (float)row.Score));
        AssertSameValue(() => rows.Min(static row => (decimal?)row.Amount),() => SystemEnumerable.Min(rows,static row => (decimal?)row.Amount));
        AssertSameValue(() => rows.Min(static row => (double?)row.Score),  () => SystemEnumerable.Min(rows,static row => (double?)row.Score));
        AssertSameValue(() => rows.Min(static row => (int?)row.Score),     () => SystemEnumerable.Min(rows,static row => (int?)row.Score));
        AssertSameValue(() => rows.Min(static row => (long?)row.Score),    () => SystemEnumerable.Min(rows,static row => (long?)row.Score));
        AssertSameValue(() => rows.Min(static row => (float?)row.Score),   () => SystemEnumerable.Min(rows,static row => (float?)row.Score));
    }

    private static void AssertNumericSum(Row[] rows) {
        AssertSameValue(() => new[] { 1.5m,2.5m,2.5m }.Sum(),                               () => SystemEnumerable.Sum([
            1.5m,2.5m,2.5m
        ]));
        AssertSameValue(() => new[] { 1.5d,2.5d,2.5d }.Sum(),                               () => SystemEnumerable.Sum([
            1.5d,2.5d,2.5d
        ]));
        AssertSameValue(() => new[] { 1,2,2 }.Sum(),                                        () => SystemEnumerable.Sum([
            1,2,2
        ]));
        AssertSameValue(() => new[] { 1L,2L,2L }.Sum(),                                     () => SystemEnumerable.Sum([
            1L,2L,2L
        ]));
        AssertSameValue(() => new[] { 1.5f,2.5f,2.5f }.Sum(),                               () => SystemEnumerable.Sum([
            1.5f,2.5f,2.5f
        ]));
        AssertSameValue(() => new decimal?[] { 1.5m,null,2.5m }.Sum(),                      () => SystemEnumerable.Sum([
            1.5m,null,2.5m
        ]));
        AssertSameValue(() => new double?[] { 1.5d,null,2.5d }.Sum(),                       () => SystemEnumerable.Sum([
            1.5d,null,2.5d
        ]));
        AssertSameValue(() => new int?[] { 1,null,2 }.Sum(),                                () => SystemEnumerable.Sum([
            1,null,2
        ]));
        AssertSameValue(() => new long?[] { 1L,null,2L }.Sum(),                             () => SystemEnumerable.Sum([
            1L,null,2L
        ]));
        AssertSameValue(() => new float?[] { 1.5f,null,2.5f }.Sum(),                        () => SystemEnumerable.Sum([
            1.5f,null,2.5f
        ]));
        AssertSameValue(() => rows.Sum(static row => row.Amount),          () => SystemEnumerable.Sum(rows,static row => row.Amount));
        AssertSameValue(() => rows.Sum(static row => (double)row.Score),   () => SystemEnumerable.Sum(rows,static row => (double)row.Score));
        AssertSameValue(() => rows.Sum(static row => row.Score),           () => SystemEnumerable.Sum(rows,static row => row.Score));
        AssertSameValue(() => rows.Sum(static row => (long)row.Score),     () => SystemEnumerable.Sum(rows,static row => (long)row.Score));
        AssertSameValue(() => rows.Sum(static row => (float)row.Score),    () => SystemEnumerable.Sum(rows,static row => (float)row.Score));
        AssertSameValue(() => rows.Sum(static row => (decimal?)row.Amount),() => SystemEnumerable.Sum(rows,static row => (decimal?)row.Amount));
        AssertSameValue(() => rows.Sum(static row => (double?)row.Score),  () => SystemEnumerable.Sum(rows,static row => (double?)row.Score));
        AssertSameValue(() => rows.Sum(static row => (int?)row.Score),     () => SystemEnumerable.Sum(rows,static row => (int?)row.Score));
        AssertSameValue(() => rows.Sum(static row => (long?)row.Score),    () => SystemEnumerable.Sum(rows,static row => (long?)row.Score));
        AssertSameValue(() => rows.Sum(static row => (float?)row.Score),   () => SystemEnumerable.Sum(rows,static row => (float?)row.Score));
    }

    private static void AssertSameSequence<T>(Func<IEnumerable<T>> expectedFactory,Func<IEnumerable<T>> actualFactory) {
        Assert.Equal(SystemEnumerable.ToArray(expectedFactory()),SystemEnumerable.ToArray(actualFactory()));
    }

    private static void AssertSameValue<T>(Func<T> expectedFactory,Func<T> actualFactory) {
        Assert.Equal(expectedFactory(),actualFactory());
    }

    private static void AssertSameException<TException>(Action expectedAction,Action actualAction)
        where TException : Exception {
        var expected = Assert.Throws<TException>(expectedAction);
        var actual = Assert.Throws<TException>(actualAction);
        Assert.Equal(expected.GetType(),actual.GetType());
        if(expected is ArgumentException expectedArgument&&actual is ArgumentException actualArgument) {
            Assert.Equal(expectedArgument.ParamName,actualArgument.ParamName);
        }
    }

    private static void AssertSameCountResult<T>(IEnumerable<T> expectedSource,IEnumerable<T> actualSource) {
        var expectedResult = expectedSource.TryGetNonEnumeratedCount(out var expectedCount);
        var actualResult = SystemEnumerable.TryGetNonEnumeratedCount(actualSource,out var actualCount);
        Assert.Equal(expectedResult,actualResult);
        Assert.Equal(expectedCount,actualCount);
    }

    private static IEnumerable<string> ChunkText<T>(IEnumerable<T[]> chunks) => chunks.Select(static chunk => string.Join(",",chunk));

    private static IEnumerable<string> NormalizeGroups<TKey, TElement>(IEnumerable<IGrouping<TKey,TElement>> groups) =>
        groups.Select(static group => $"{group.Key}:{string.Join("|",group)}");

    private static IEnumerable<string> NormalizeLookup<TKey, TElement>(ILookup<TKey,TElement> lookup) =>
        lookup.Select(static group => $"{group.Key}:{string.Join("|",group)}");

    private static IEnumerable<string> NormalizeDictionary<TKey, TValue>(Dictionary<TKey,TValue> dictionary)
        where TKey : notnull =>
        dictionary.OrderBy(static pair => pair.Key.ToString()).Select(static pair => $"{pair.Key}:{pair.Value}");

    private static int[] DuplicateNumbers() => [4,1,2,4,3,2,5,1,6];

    private static int[] UniqueNumbers() => [-3,-1,0,2,5,8];

    private static Row[] RowsWithDuplicates() => [
        new Row(1,"alpha",10,1.5m,"one"),
        new Row(2,"beta",20,2.5m,null),
        new Row(1,"alpha",15,1.5m,"duplicate-id"),
        new Row(2,"beta",20,2.5m,null),
        new Row(3,"gamma",-5,3.5m,"three"),
        new Row(4,"gamma",0,4.5m,"four")
    ];

    private static Row[] RowsWithoutDuplicateKeys() => [
        new Row(1,"alpha",10,1.5m,"one"),
        new Row(2,"beta",20,2.5m,null),
        new Row(3,"gamma",-5,3.5m,"three"),
        new Row(4,"delta",0,4.5m,"four")
    ];

    private static TestOrder[] Orders() => [
        new TestOrder(1,"A",2),
        new TestOrder(1,"B",3),
        new TestOrder(2,"C",5),
        new TestOrder(5,"missing",7)
    ];

    private sealed record Row(int Id,string Category,int Score,decimal Amount,string? Note);

    private sealed record TestOrder(int RowId,string Code,int Quantity);

    //private readonly struct DoubleStrategy:global::Tsavorite.Linq.IFunction<int,int> {
    //    public static int Invoke(int value) => value*2;
    //}

    //private readonly struct EvenStrategy:global::Tsavorite.Linq.IFunction<int,bool> {
    //    public static bool Invoke(int value) => value%2==0;
    //}

    private sealed class ModuloComparer(int divisor):IEqualityComparer<int> {
        public bool Equals(int left,int right) => left%divisor==right%divisor;
        public int GetHashCode(int value) => value%divisor;
    }

    private sealed class DescendingIntComparer:IComparer<int> {
        public int Compare(int left,int right) => right.CompareTo(left);
    }
}



