using System.Linq;
using SeamlessEnumerable = Seamless.Linq.Enumerable;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Unit.Linq;

public sealed class EnumerableIntercept {
    [Fact, Trait("Category","Unit")]
    public void Aggregate() {
        var values = DuplicateNumbers();
        AssertSameValue(() => values.Aggregate(static (total,item) => total+item),() => SeamlessEnumerable.Aggregate(values,static (total,item) => total+item));
        AssertSameValue(() => values.Aggregate(100,static (total,item) => total-item),() => SeamlessEnumerable.Aggregate(values,100,static (total,item) => total-item));
        AssertSameValue(() => values.Aggregate(0,static (total,item) => total+item,static total => $"sum:{total}"),() => SeamlessEnumerable.Aggregate(values,0,static (total,item) => total+item,static total => $"sum:{total}"));
    }

    [Fact, Trait("Category","Unit")]
    public void AggregateBy() {
        var rows = RowsWithDuplicates();
        IEqualityComparer<string> comparer = StringComparer.OrdinalIgnoreCase;
        AssertSameSequence(() => rows.AggregateBy(static row => row.Category,0,static (total,row) => total+row.Score),() => SeamlessEnumerable.AggregateBy(rows,static row => row.Category,0,static (total,row) => total+row.Score));
        AssertSameSequence(() => rows.AggregateBy(static row => row.Category,static category => category.Length,static (total,row) => total+row.Score,comparer),() => SeamlessEnumerable.AggregateBy(rows,static row => row.Category,static category => category.Length,static (total,row) => total+row.Score,comparer));
    }

    [Fact, Trait("Category","Unit")]
    public void All() {
        var rows = RowsWithDuplicates();
        AssertSameValue(() => rows.All(static row => row.Score>=-5),() => SeamlessEnumerable.All(rows,static row => row.Score>=-5));
        AssertSameValue(() => rows.All(static row => row.Category=="alpha"),() => SeamlessEnumerable.All(rows,static row => row.Category=="alpha"));
    }

    [Fact, Trait("Category","Unit")]
    public void Any() {
        var rows = RowsWithDuplicates();
        AssertSameValue(() => rows.Any(),                                               () => SeamlessEnumerable.Any(rows));
        AssertSameValue(() => Array.Empty<Row>().Any(),                                 () => SeamlessEnumerable.Any(Array.Empty<Row>()));
        AssertSameValue(() => rows.Any(static row => row.Note is null),() => SeamlessEnumerable.Any(rows,static row => row.Note is null));
    }

    [Fact, Trait("Category","Unit")]
    public void Append() {
        AssertSameSequence(() => DuplicateNumbers().Append(9),() => SeamlessEnumerable.Append(DuplicateNumbers(),9));
    }

    [Fact, Trait("Category","Unit")]
    public void AsEnumerable() {
        var values = DuplicateNumbers();
        Assert.Same(values.AsEnumerable(),SeamlessEnumerable.AsEnumerable(values));
    }

    [Fact, Trait("Category","Unit")]
    public void Average() {
        var rows = RowsWithDuplicates();
        var ints = new[] { 1,2,2,5,-1 };
        var longs = new[] { 1L,4L,4L,10L };
        var floats = new[] { 1.5f,2.5f,2.5f,5.5f };
        var doubles = new[] { 1.5d,2.5d,2.5d,5.5d };
        var decimals = new[] { 1.5m,2.5m,2.5m,5.5m };
        AssertSameValue(() => ints.Average(),  () => SeamlessEnumerable.Average(ints));
        AssertSameValue(() => longs.Average(), () => SeamlessEnumerable.Average(longs));
        AssertSameValue(() => floats.Average(),() => SeamlessEnumerable.Average(floats));
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
                throw SeamlessEnumerable.NoElements();
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
                throw SeamlessEnumerable.NoElements();
            return sum/count;
        });
        AssertSameValue(() => new int?[] { 1,null,2,2 }.Average(),    () => SeamlessEnumerable.Average(new int?[] { 1,null,2,2 }));
        AssertSameValue(() => new long?[] { 1L,null,4L,4L }.Average(),() => SeamlessEnumerable.Average(new long?[] { 1L,null,4L,4L }));
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
        AssertSameValue(() => new double?[] { 1.5d,null,2.5d,2.5d }.Average(),                  () => SeamlessEnumerable.Average(new double?[] { 1.5d,null,2.5d,2.5d }));
        AssertSameValue(() => new decimal?[] { 1.5m,null,2.5m,2.5m }.Average(),                 () => SeamlessEnumerable.Average(new decimal?[] { 1.5m,null,2.5m,2.5m }));
        AssertSameValue(() => rows.Average(static row => row.Amount),          () => SeamlessEnumerable.Average(rows,static row => row.Amount));
        AssertSameValue(() => rows.Average(static row => (double)row.Amount),  () => SeamlessEnumerable.Average(rows,static row => (double)row.Amount));
        AssertSameValue(() => rows.Average(static row => row.Score),           () => SeamlessEnumerable.Average(rows,static row => row.Score));
        AssertSameValue(() => rows.Average(static row => (long)row.Score),     () => SeamlessEnumerable.Average(rows,static row => (long)row.Score));
        AssertSameValue(() => rows.Average(static row => (float)row.Score),    () => SeamlessEnumerable.Average(rows,static row => (float)row.Score));
        AssertSameValue(() => rows.Average(static row => (decimal?)row.Amount),() => SeamlessEnumerable.Average(rows,static row => (decimal?)row.Amount));
        AssertSameValue(() => rows.Average(static row => (double?)row.Score),  () => SeamlessEnumerable.Average(rows,static row => (double?)row.Score));
        AssertSameValue(() => rows.Average(static row => (int?)row.Score),     () => SeamlessEnumerable.Average(rows,static row => (int?)row.Score));
        AssertSameValue(() => rows.Average(static row => (long?)row.Score),    () => SeamlessEnumerable.Average(rows,static row => (long?)row.Score));
        AssertSameValue(() => rows.Average(static row => (float?)row.Score),   () => SeamlessEnumerable.Average(rows,static row => (float?)row.Score));
    }

    [Fact, Trait("Category","Unit")]
    public void Cast() {
        var values = new ArrayList { 1,2,2,5 };
        AssertSameSequence(() => values.Cast<int>(),() => SeamlessEnumerable.Cast<int>(values));
    }

    [Fact, Trait("Category","Unit")]
    public void Chunk() {
        AssertSameSequence(() => ChunkText(DuplicateNumbers().Chunk(3)),() => ChunkText(SeamlessEnumerable.Chunk(DuplicateNumbers(),3)));
    }

    [Fact, Trait("Category","Unit")]
    public void Concat() {
        AssertSameSequence(() => DuplicateNumbers().Concat(UniqueNumbers()),() => SeamlessEnumerable.Concat(DuplicateNumbers(),UniqueNumbers()));
    }

    [Fact, Trait("Category","Unit")]
    public void Contains() {
        var values = DuplicateNumbers();
        AssertSameValue(() => SystemEnumerable.Contains(values,4),() => SeamlessEnumerable.Contains(values,4));
        AssertSameValue(() => SystemEnumerable.Contains(values,10),() => SeamlessEnumerable.Contains(values,10));
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
        AssertSameValue(() => rows.Count(),() => SeamlessEnumerable.Count(rows));
        AssertSameValue(() => rows.Count(static row => row.Category=="beta"),() => SeamlessEnumerable.Count(rows,static row => row.Category=="beta"));
    }

    [Fact, Trait("Category","Unit")]
    public void CountBy() {
        var rows = RowsWithDuplicates();
        AssertSameSequence(() => rows.CountBy(static row => row.Category),() => SeamlessEnumerable.CountBy(rows,static row => row.Category));
        AssertSameSequence(() => new[] { "A","a","B","b","b" }.CountBy(static value => value,StringComparer.OrdinalIgnoreCase),() => SeamlessEnumerable.CountBy(new[] { "A","a","B","b","b" },static value => value,StringComparer.OrdinalIgnoreCase));
    }

    [Fact, Trait("Category","Unit")]
    public void DefaultIfEmpty() {
        AssertSameSequence(() => DuplicateNumbers().DefaultIfEmpty(),                   () => SeamlessEnumerable.DefaultIfEmpty(DuplicateNumbers()));
        AssertSameSequence(() => Array.Empty<int>().DefaultIfEmpty(),                   () => SeamlessEnumerable.DefaultIfEmpty(Array.Empty<int>()));
        AssertSameSequence(() => Array.Empty<int>().DefaultIfEmpty(42),() => SeamlessEnumerable.DefaultIfEmpty(Array.Empty<int>(),42));
    }

    [Fact, Trait("Category","Unit")]
    public void DistinctBy() {
        var rows = RowsWithDuplicates();
        AssertSameSequence(() => rows.DistinctBy(static row => row.Category),() => SeamlessEnumerable.DistinctBy(rows,static row => row.Category));
        AssertSameSequence(() => rows.DistinctBy(static row => row.Category,StringComparer.OrdinalIgnoreCase),() =>{
            Func<Row,string> keySelector=static row => row.Category;
            return rows.DistinctBy(keySelector,StringComparer.OrdinalIgnoreCase);
        });
    }

    [Fact, Trait("Category","Unit")]
    public void Distinct() {
        AssertSameSequence(() => DuplicateNumbers().Distinct(),() => SeamlessEnumerable.Distinct(DuplicateNumbers()));
        AssertSameSequence(() => UniqueNumbers().Distinct(),   () => SeamlessEnumerable.Distinct(UniqueNumbers()));
        AssertSameSequence(() => DuplicateNumbers().Distinct(new ModuloComparer(3)),() =>{
            IEqualityComparer<int>? comparer=new ModuloComparer(3);
            return DuplicateNumbers().Distinct(comparer);
        });
    }

    [Fact, Trait("Category","Unit")]
    public void ElementAtOrDefault() {
        var values = DuplicateNumbers();
        AssertSameValue(() => values.ElementAtOrDefault(3),() => SeamlessEnumerable.ElementAtOrDefault(values,3));
        AssertSameValue(() => values.ElementAtOrDefault(99),() => SeamlessEnumerable.ElementAtOrDefault(values,99));
        AssertSameValue(() => values.ElementAtOrDefault(^2),() => SeamlessEnumerable.ElementAtOrDefault(values,^2));
    }

    [Fact, Trait("Category","Unit")]
    public void ElementAt() {
        var values = DuplicateNumbers();
        AssertSameValue(() => values.ElementAt(3),() => SeamlessEnumerable.ElementAt(values,3));
        AssertSameValue(() => values.ElementAt(^2),() => SeamlessEnumerable.ElementAt(values,^2));
    }

    [Fact, Trait("Category","Unit")]
    public void Empty() {
        AssertSameSequence(() => SystemEnumerable.Empty<Row>(),() => SeamlessEnumerable.Empty<Row>());
    }

    [Fact, Trait("Category","Unit")]
    public void ExceptBy() {
        var rows = RowsWithDuplicates();
        AssertSameSequence(() => rows.ExceptBy(new[] { 1,4 },static row => row.Id),() => SeamlessEnumerable.ExceptBy(rows,new[] { 1,4 },static row => row.Id));
        AssertSameSequence(() => rows.ExceptBy(new[] { "ALPHA" },static row => row.Category,StringComparer.OrdinalIgnoreCase),() =>{
            IEnumerable<string> second     =new[] { "ALPHA" };
            Func<Row,string>    keySelector=static row => row.Category;
            return rows.ExceptBy(second,keySelector,StringComparer.OrdinalIgnoreCase);
        });
    }

    [Fact, Trait("Category","Unit")]
    public void Except() {
        AssertSameSequence(() => DuplicateNumbers().Except(new[] { 1,6 }),() => SeamlessEnumerable.Except(DuplicateNumbers(),new[] { 1,6 }));
        AssertSameSequence(() => DuplicateNumbers().Except(new[] { 7 },new ModuloComparer(3)),() =>{
            IEnumerable<int>        second  =new[] { 7 };
            IEqualityComparer<int>? comparer=new ModuloComparer(3);
            return DuplicateNumbers().Except(second,comparer);
        });
    }

    [Fact, Trait("Category","Unit")]
    public void FirstOrDefault() {
        var values = DuplicateNumbers();
        AssertSameValue(() => values.FirstOrDefault(),() => SeamlessEnumerable.FirstOrDefault(values));
        AssertSameValue(() => Array.Empty<int>().FirstOrDefault(42),() => SeamlessEnumerable.FirstOrDefault(Array.Empty<int>(),42));
        AssertSameValue(() => values.FirstOrDefault(static value => value>3),() => SeamlessEnumerable.FirstOrDefault(values,static value => value>3));
        AssertSameValue(() => values.FirstOrDefault(static value => value>20,42),() => SeamlessEnumerable.FirstOrDefault(values,static value => value>20,42));
    }

    [Fact, Trait("Category","Unit")]
    public void First() {
        var values = DuplicateNumbers();
        AssertSameValue(() => values.First(),() => SeamlessEnumerable.First(values));
        AssertSameValue(() => values.First(static value => value>3),() => SeamlessEnumerable.First(values,static value => value>3));
    }

    [Fact, Trait("Category","Unit")]
    public void GroupBy() {
        var rows = RowsWithDuplicates();
        AssertSameSequence(() => NormalizeGroups(rows.GroupBy(static row => row.Category)),                                                                                                     () => NormalizeGroups(SeamlessEnumerable.GroupBy(rows,static row => row.Category)));
        AssertSameSequence(() => NormalizeGroups(rows.GroupBy(static row => row.Category,StringComparer.OrdinalIgnoreCase)),                                                                    () => NormalizeGroups(SeamlessEnumerable.GroupBy(rows,static row => row.Category,StringComparer.OrdinalIgnoreCase)));
        AssertSameSequence(() => NormalizeGroups(rows.GroupBy(static row => row.Category,static row => row.Score)),                                                                             () => NormalizeGroups(SeamlessEnumerable.GroupBy(rows,static row => row.Category,static row => row.Score)));
        AssertSameSequence(() => NormalizeGroups(rows.GroupBy(static row => row.Category,static row => row.Score,StringComparer.OrdinalIgnoreCase)),                                            () =>{
            Func<Row,string> keySelector    =static row => row.Category;
            Func<Row,int>    elementSelector=static row => row.Score;
            return NormalizeGroups(rows.GroupBy(keySelector,elementSelector,StringComparer.OrdinalIgnoreCase));
        });
        AssertSameSequence(() => rows.GroupBy(static row => row.Category,static (key,                        items) => $"{key}:{items.Count()}"),                               () => SeamlessEnumerable.GroupBy(rows,static row => row.Category,static (key,                        items) => $"{key}:{items.Count()}"));
        AssertSameSequence(() => rows.GroupBy(static row => row.Category,static row => row.Score,static (key,items) => $"{key}:{items.Sum()}",StringComparer.OrdinalIgnoreCase),() => SeamlessEnumerable.GroupBy(rows,static row => row.Category,static row => row.Score,static (key,items) => $"{key}:{items.Sum()}",StringComparer.OrdinalIgnoreCase));
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
        AssertSameSequence(() => rows.GroupJoin(orders,static row => row.Id,static order => order.RowId,static (row,items) => $"{row.Id}:{items.Sum(static item => item.Quantity)}",EqualityComparer<int>.Default),() => SeamlessEnumerable.GroupJoin(rows,orders,static row => row.Id,static order => order.RowId,static (row,items) => $"{row.Id}:{items.Sum(static item => item.Quantity)}",EqualityComparer<int>.Default));
    }
    [Fact, Trait("Category","Unit")]
    public void GroupJoin() {
        var rows = RowsWithDuplicates();
        var orders = Orders();
        AssertSameSequence(() => rows.GroupJoin(orders,static row => row.Id,static order => order.RowId,static (row,                 items) => $"{row.Id}:{items.Count()}"),                                                                         () => SeamlessEnumerable.GroupJoin(rows,orders,static row => row.Id,static order => order.RowId,static (row,items) => $"{row.Id}:{items.Count()}"));
        AssertSameSequence(() => rows.GroupJoin(orders,static row => row.Id,static order => order.RowId,static (row,items) => $"{row.Id}:{items.Sum(static item => item.Quantity)}",EqualityComparer<int>.Default),() => SeamlessEnumerable.GroupJoin(rows,orders,static row => row.Id,static order => order.RowId,static (row,items) => $"{row.Id}:{items.Sum(static item => item.Quantity)}",EqualityComparer<int>.Default));
    }

    [Fact, Trait("Category","Unit")]
    public void Index() {
        AssertSameSequence(() => DuplicateNumbers().Index(),() => SeamlessEnumerable.Index(DuplicateNumbers()));
    }

    [Fact, Trait("Category","Unit")]
    public void InfiniteSequence() {
        AssertSameSequence(() => SystemEnumerable.InfiniteSequence(3,4).Take(6),() => SeamlessEnumerable.InfiniteSequence(3,4).Take(6));
    }

    [Fact, Trait("Category","Unit")]
    public void IntersectBy() {
        var rows = RowsWithDuplicates();
        AssertSameSequence(() => rows.IntersectBy(new[] { 1,4,9 },static row => row.Id),() => SeamlessEnumerable.IntersectBy(rows,new[] { 1,4,9 },static row => row.Id));
        AssertSameSequence(() => rows.IntersectBy(new[] { "ALPHA","missing" },static row => row.Category,StringComparer.OrdinalIgnoreCase),() => SeamlessEnumerable.IntersectBy(rows,new[] { "ALPHA","missing" },static row => row.Category,StringComparer.OrdinalIgnoreCase));
    }

    [Fact, Trait("Category","Unit")]
    public void Intersect() {
        AssertSameSequence(() => DuplicateNumbers().Intersect(new[] { 1,4,9 }),() => SeamlessEnumerable.Intersect(DuplicateNumbers(),new[] { 1,4,9 }));
        AssertSameSequence(() => DuplicateNumbers().Intersect(new[] { 7,8 },new ModuloComparer(3)),() => SeamlessEnumerable.Intersect(DuplicateNumbers(),new[] { 7,8 },new ModuloComparer(3)));
    }

    [Fact, Trait("Category","Unit")]
    public void Join() {
        var rows = RowsWithDuplicates();
        var orders = Orders();
        AssertSameSequence(() => rows.Join(orders,static row => row.Id,static order => order.RowId,static (row,order) => $"{row.Id}:{order.Code}"),() => SeamlessEnumerable.Join(rows,orders,static row => row.Id,static order => order.RowId,static (row,order) => $"{row.Id}:{order.Code}"));
        AssertSameSequence(() => rows.Join(orders,static row => row.Id,static order => order.RowId,static (row,order) => $"{row.Id}:{order.Quantity}",EqualityComparer<int>.Default),() => {
            Func<Row,int> outerKeySelector = static row => row.Id;
            Func<TestOrder,int> innerKeySelector = static order => order.RowId;
            Func<Row,TestOrder,string> resultSelector = static (row,order) => $"{row.Id}:{order.Quantity}";
            var result = new List<string>();
            IEnumerable<TestOrder> source=(IEnumerable<TestOrder>)orders;
            Func<TestOrder,TestOrder> elementSelector=static item => item;
            var lookup1=new SeamlessEnumerable.LookupImpl<int,TestOrder>(EqualityComparer<int>.Default);
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
        AssertSameValue(() => values.LastOrDefault(),() => SeamlessEnumerable.LastOrDefault(values));
        AssertSameValue(() => Array.Empty<int>().LastOrDefault(42),() => SeamlessEnumerable.LastOrDefault(Array.Empty<int>(),42));
        AssertSameValue(() => values.LastOrDefault(static value => value<3),() => SeamlessEnumerable.LastOrDefault(values,static value => value<3));
        AssertSameValue(() => values.LastOrDefault(static value => value>20,42),() => SeamlessEnumerable.LastOrDefault(values,static value => value>20,42));
    }

    [Fact, Trait("Category","Unit")]
    public void Last() {
        var values = DuplicateNumbers();
        AssertSameValue(() => values.Last(),() => SeamlessEnumerable.Last(values));
        AssertSameValue(() => values.Last(static value => value<3),() => SeamlessEnumerable.Last(values,static value => value<3));
    }

    [Fact, Trait("Category","Unit")]
    public void LeftJoin() {
        var rows = RowsWithDuplicates();
        var orders = Orders();
        AssertSameSequence(() => rows.LeftJoin(orders,static row => row.Id,static order => order.RowId,static (row,order) => $"{row.Id}:{order?.Code??"none"}"),() => SeamlessEnumerable.LeftJoin(rows,orders,static row => row.Id,static order => order.RowId,static (row,order) => $"{row.Id}:{order?.Code??"none"}"));
        AssertSameSequence(() => rows.LeftJoin(orders,static row => row.Id,static order => order.RowId,static (row,order) => $"{row.Id}:{order?.Quantity??0}",EqualityComparer<int>.Default),() => SeamlessEnumerable.LeftJoin(rows,orders,static row => row.Id,static order => order.RowId,static (row,order) => $"{row.Id}:{order?.Quantity??0}",EqualityComparer<int>.Default));
    }

    [Fact, Trait("Category","Unit")]
    public void LongCount() {
        var rows = RowsWithDuplicates();
        AssertSameValue(() => rows.LongCount(),() => SeamlessEnumerable.LongCount(rows));
        AssertSameValue(() => rows.LongCount(static row => row.Score>=10),() => SeamlessEnumerable.LongCount(rows,static row => row.Score>=10));
    }

    [Fact, Trait("Category","Unit")]
    public void Max() {
        var rows = RowsWithDuplicates();
        AssertNumericMax(rows);
        AssertSameValue(() => DuplicateNumbers().Max(),                       () => SeamlessEnumerable.Max(DuplicateNumbers()));
        AssertSameValue<int>(() => DuplicateNumbers().Max(Comparer<int>.Default),() =>{
            IEnumerable<int> source=DuplicateNumbers();
            IComparer<int>? comparer1=Comparer<int>.Default;
            return SeamlessEnumerable.Max(source,comparer1);
        });
        AssertSameValue(() => rows.Max(static row => row.Note),() => SeamlessEnumerable.Max(rows,static row => row.Note));
    }

    [Fact, Trait("Category","Unit")]
    public void MaxBy() {
        var rows = RowsWithDuplicates();
        AssertSameValue(() => rows.MaxBy(static row => row.Score),() => SeamlessEnumerable.MaxBy(rows,static row => row.Score));
        AssertSameValue(() => rows.MaxBy(static row => row.Score,Comparer<int>.Default),() => SeamlessEnumerable.MaxBy(rows,static row => row.Score,Comparer<int>.Default));
    }

    [Fact, Trait("Category","Unit")]
    public void Min() {
        var rows = RowsWithDuplicates();
        AssertNumericMin(rows);
        AssertSameValue(() => DuplicateNumbers().Min(),() => SeamlessEnumerable.Min(DuplicateNumbers()));
        AssertSameValue(() => DuplicateNumbers().Min(Comparer<int>.Default),() => SeamlessEnumerable.Min(DuplicateNumbers(),Comparer<int>.Default));
        AssertSameValue(() => rows.Min(static row => row.Note),() => SeamlessEnumerable.Min(rows,static row => row.Note));
    }

    [Fact, Trait("Category","Unit")]
    public void MinBy() {
        var rows = RowsWithDuplicates();
        AssertSameValue(() => rows.MinBy(static row => row.Score),() => SeamlessEnumerable.MinBy(rows,static row => row.Score));
        AssertSameValue(() => rows.MinBy(static row => row.Score,Comparer<int>.Default),() => SeamlessEnumerable.MinBy(rows,static row => row.Score,Comparer<int>.Default));
    }

    [Fact, Trait("Category","Unit")]
    public void OfType() {
        var values = new ArrayList { 1,"two",2,"three",2 };
        AssertSameSequence(() => values.OfType<int>(),() => SeamlessEnumerable.OfType<int>(values));
    }

    [Fact, Trait("Category","Unit")]
    public void OrderByDescending() {
        var rows = RowsWithDuplicates();
        AssertSameSequence(() => rows.OrderByDescending(static row => row.Score),() => SeamlessEnumerable.OrderByDescending(rows,static row => row.Score));
        AssertSameSequence(() => rows.OrderByDescending(static row => row.Score,Comparer<int>.Default),() => SeamlessEnumerable.OrderByDescending(rows,static row => row.Score,Comparer<int>.Default));
    }

    [Fact, Trait("Category","Unit")]
    public void OrderBy() {
        var rows = RowsWithDuplicates();
        AssertSameSequence(() => rows.OrderBy(static row => row.Category),() => SeamlessEnumerable.OrderBy(rows,static row => row.Category));
        AssertSameSequence(() => rows.OrderBy(static row => row.Category,StringComparer.OrdinalIgnoreCase),() => SeamlessEnumerable.OrderBy(rows,static row => row.Category,StringComparer.OrdinalIgnoreCase));
    }

    [Fact, Trait("Category","Unit")]
    public void OrderDescending() {
        AssertSameSequence(() => DuplicateNumbers().OrderDescending(),() => SeamlessEnumerable.OrderDescending(DuplicateNumbers()));
        AssertSameSequence(() => DuplicateNumbers().OrderDescending(Comparer<int>.Default),() => SeamlessEnumerable.OrderDescending(DuplicateNumbers(),Comparer<int>.Default));
    }

    [Fact, Trait("Category","Unit")]
    public void Order() {
        AssertSameSequence(() => DuplicateNumbers().Order(),() => SeamlessEnumerable.Order(DuplicateNumbers()));
        AssertSameSequence(() => DuplicateNumbers().Order(new DescendingIntComparer()),() => SeamlessEnumerable.Order(DuplicateNumbers(),new DescendingIntComparer()));
    }

    [Fact, Trait("Category","Unit")]
    public void Prepend() {
        AssertSameSequence(() => DuplicateNumbers().Prepend(9),() => SeamlessEnumerable.Prepend(DuplicateNumbers(),9));
    }

    [Fact, Trait("Category","Unit")]
    public void Range() {
        AssertSameSequence(() => SystemEnumerable.Range(-3,7),() => SeamlessEnumerable.Range(-3,7));
    }

    [Fact, Trait("Category","Unit")]
    public void Repeat() {
        AssertSameSequence(() => SystemEnumerable.Repeat("repeat",4),() => SeamlessEnumerable.Repeat("repeat",4));
    }

    [Fact, Trait("Category","Unit")]
    public void Reverse() {
        AssertSameSequence(() => DuplicateNumbers().Reverse(),() => SeamlessEnumerable.Reverse(DuplicateNumbers()));
        AssertSameSequence(() => UniqueNumbers().Reverse(),   () => SeamlessEnumerable.Reverse(UniqueNumbers()));
    }

    [Fact, Trait("Category","Unit")]
    public void RightJoin() {
        var rows = RowsWithDuplicates();
        var orders = Orders();
        AssertSameSequence(() => rows.RightJoin(orders,static row => row.Id,static order => order.RowId,static (row,order) => $"{row?.Id??0}:{order.Code}"),() => SeamlessEnumerable.RightJoin(rows,orders,static row => row.Id,static order => order.RowId,static (row,order) => $"{row?.Id??0}:{order.Code}"));
        AssertSameSequence(() => rows.RightJoin(orders,static row => row.Id,static order => order.RowId,static (row,order) => $"{row?.Id??0}:{order.Quantity}",EqualityComparer<int>.Default),() => SeamlessEnumerable.RightJoin(rows,orders,static row => row.Id,static order => order.RowId,static (row,order) => $"{row?.Id??0}:{order.Quantity}",EqualityComparer<int>.Default));
    }

    [Fact, Trait("Category","Unit")]
    public void SelectMany() {
        var rows = RowsWithDuplicates();
        AssertSameSequence(() => rows.SelectMany(static row => new[] { row.Id,row.Score }),() => SeamlessEnumerable.SelectMany(rows,static row => new[] { row.Id,row.Score }));
        AssertSameSequence(() => rows.SelectMany(static (row,index) => new[] { row.Id+index,row.Score-index }),() => SeamlessEnumerable.SelectMany(rows,static (row,index) => new[] { row.Id+index,row.Score-index }));
        AssertSameSequence(() => rows.SelectMany(static row => new[] { row.Id,row.Score },static (row,value) => $"{row.Category}:{value}"),() => SeamlessEnumerable.SelectMany(rows,static row => new[] { row.Id,row.Score },static (row,value) => $"{row.Category}:{value}"));
        AssertSameSequence(() => rows.SelectMany(static (row,index) => new[] { row.Id+index,row.Score-index },static (row,value) => $"{row.Category}:{value}"),() => SeamlessEnumerable.SelectMany(rows,static (row,index) => new[] { row.Id+index,row.Score-index },static (row,value) => $"{row.Category}:{value}"));
    }

    [Fact, Trait("Category","Unit")]
    public void Select() {
        var rows = RowsWithDuplicates();
        AssertSameSequence(() => rows.Select(static row => $"{row.Category}:{row.Score}"),() => SeamlessEnumerable.Select(rows,static row => $"{row.Category}:{row.Score}"));
        AssertSameSequence(() => rows.Select(static (row,index) => $"{index}:{row.Id}:{row.Score}"),() => SeamlessEnumerable.Select(rows,static (row,index) => $"{index}:{row.Id}:{row.Score}"));
        AssertSameException<ArgumentNullException>(() => { _=((IEnumerable<int>)null!).Select(static value => value); },() => { _=SeamlessEnumerable.Select((IEnumerable<int>)null!,static value => value); });
        AssertSameException<ArgumentNullException>(() => { _=new[] { 1 }.Select((Func<int,int>)null!); },() => { _=SeamlessEnumerable.Select(new[] { 1 },(Func<int,int>)null!); });
        var mutable = new List<int> { 1,2 };
        var actual = SeamlessEnumerable.Select(mutable,static value => value*10);
        mutable.Add(3);
        Assert.Equal(new[] { 10,20 },actual.ToArray());
        Assert.Equal(new[] { 10,20 },actual.ToArray());
    }

    [Fact, Trait("Category","Unit")]
    public void Sequence() {
        AssertSameSequence(() => SystemEnumerable.Sequence(1,9,2),() => SeamlessEnumerable.Sequence(1,9,2));
        AssertSameSequence(() => SystemEnumerable.Sequence(5,1,-2),() => SeamlessEnumerable.Sequence(5,1,-2));
        AssertSameSequence(() => SystemEnumerable.Sequence(1.5m,4.5m,1.5m),() => SeamlessEnumerable.Sequence(1.5m,4.5m,1.5m));
    }

    [Fact, Trait("Category","Unit")]
    public void SequenceEqual() {
        AssertSameValue(() => SystemEnumerable.SequenceEqual(DuplicateNumbers(),DuplicateNumbers()),() => SeamlessEnumerable.SequenceEqual(DuplicateNumbers(),DuplicateNumbers()));
        AssertSameValue(() => SystemEnumerable.SequenceEqual(DuplicateNumbers(),UniqueNumbers()),() => SeamlessEnumerable.SequenceEqual(DuplicateNumbers(),UniqueNumbers()));
        AssertSameValue(() => SystemEnumerable.SequenceEqual(new[] { 1,4,7 },new[] { 7,10,13 },new ModuloComparer(3)),() => SeamlessEnumerable.SequenceEqual(new[] { 1,4,7 },new[] { 7,10,13 },new ModuloComparer(3)));
    }

    [Fact, Trait("Category","Unit")]
    public void Shuffle() {
        var values = DuplicateNumbers();
        Assert.Equal(values.Shuffle().Order().ToArray(),SeamlessEnumerable.Shuffle(values).Order().ToArray());
    }

    [Fact, Trait("Category","Unit")]
    public void SingleOrDefault() {
        AssertSameValue(() => new[] { 7 }.SingleOrDefault(),() => SeamlessEnumerable.SingleOrDefault(new[] { 7 }));
        AssertSameValue(() => Array.Empty<int>().SingleOrDefault(42),() => SeamlessEnumerable.SingleOrDefault(Array.Empty<int>(),42));
        AssertSameValue(() => DuplicateNumbers().SingleOrDefault(static value => value==6),() => SeamlessEnumerable.SingleOrDefault(DuplicateNumbers(),static value => value==6));
        AssertSameValue(() => DuplicateNumbers().SingleOrDefault(static value => value==99,42),() => SeamlessEnumerable.SingleOrDefault(DuplicateNumbers(),static value => value==99,42));
    }

    [Fact, Trait("Category","Unit")]
    public void Single() {
        AssertSameValue(() => new[] { 7 }.Single(),() => SeamlessEnumerable.Single(new[] { 7 }));
        AssertSameValue(() => DuplicateNumbers().Single(static value => value==6),() => SeamlessEnumerable.Single(DuplicateNumbers(),static value => value==6));
    }

    [Fact, Trait("Category","Unit")]
    public void SkipLast() {
        AssertSameSequence(() => DuplicateNumbers().SkipLast(2),() => SeamlessEnumerable.SkipLast(DuplicateNumbers(),2));
    }

    [Fact, Trait("Category","Unit")]
    public void SkipWhile() {
        AssertSameSequence(() => DuplicateNumbers().SkipWhile(static value => value<5),() => SeamlessEnumerable.SkipWhile(DuplicateNumbers(),static value => value<5));
        AssertSameSequence(() => DuplicateNumbers().SkipWhile(static (value,index) => value+index<7),() => SeamlessEnumerable.SkipWhile(DuplicateNumbers(),static (value,index) => value+index<7));
    }

    [Fact, Trait("Category","Unit")]
    public void Skip() {
        AssertSameSequence(() => DuplicateNumbers().Skip(3),() => SeamlessEnumerable.Skip(DuplicateNumbers(),3));
    }

    [Fact, Trait("Category","Unit")]
    public void Sum() {
        var rows = RowsWithDuplicates();
        AssertNumericSum(rows);
    }

    [Fact, Trait("Category","Unit")]
    public void TakeLast() {
        AssertSameSequence(() => DuplicateNumbers().TakeLast(3),() => SeamlessEnumerable.TakeLast(DuplicateNumbers(),3));
    }

    [Fact, Trait("Category","Unit")]
    public void TakeWhile() {
        AssertSameSequence(() => DuplicateNumbers().TakeWhile(static value => value<5),() => SeamlessEnumerable.TakeWhile(DuplicateNumbers(),static value => value<5));
        AssertSameSequence(() => DuplicateNumbers().TakeWhile(static (value,index) => value+index<8),() => SeamlessEnumerable.TakeWhile(DuplicateNumbers(),static (value,index) => value+index<8));
    }

    [Fact, Trait("Category","Unit")]
    public void Take() {
        AssertSameSequence(() => DuplicateNumbers().Take(5),() => SeamlessEnumerable.Take(DuplicateNumbers(),5));
        AssertSameSequence(() => DuplicateNumbers().Take(2..^1),() => SeamlessEnumerable.Take(DuplicateNumbers(),2..^1));
    }

    [Fact, Trait("Category","Unit")]
    public void ThenByDescending() {
        var rows = RowsWithDuplicates();
        AssertSameSequence(() => rows.OrderBy(static row => row.Category).ThenByDescending(static row => row.Score),() => SeamlessEnumerable.ThenByDescending(SeamlessEnumerable.OrderBy(rows,static row => row.Category),static row => row.Score));
        AssertSameSequence(() => rows.OrderBy(static row => row.Category).ThenByDescending(static row => row.Score,Comparer<int>.Default),() => SeamlessEnumerable.ThenByDescending(SeamlessEnumerable.OrderBy(rows,static row => row.Category),static row => row.Score,Comparer<int>.Default));
    }

    [Fact, Trait("Category","Unit")]
    public void ThenBy() {
        var rows = RowsWithDuplicates();
        AssertSameSequence(() => rows.OrderBy(static row => row.Category).ThenBy(static row => row.Score),() => SeamlessEnumerable.ThenBy(SeamlessEnumerable.OrderBy(rows,static row => row.Category),static row => row.Score));
        AssertSameSequence(() => rows.OrderBy(static row => row.Category).ThenBy(static row => row.Score,Comparer<int>.Default),() => SeamlessEnumerable.ThenBy(SeamlessEnumerable.OrderBy(rows,static row => row.Category),static row => row.Score,Comparer<int>.Default));
    }

    [Fact, Trait("Category","Unit")]
    public void ToArray() {
        AssertSameSequence(() => DuplicateNumbers().ToArray(),() => SeamlessEnumerable.ToArray(DuplicateNumbers()));
    }

    [Fact, Trait("Category","Unit")]
    public void ToDictionary() {
        var rows = RowsWithoutDuplicateKeys();
        AssertSameSequence(() => NormalizeDictionary(new[] { new KeyValuePair<string,int>("a",1),new KeyValuePair<string,int>("b",2) }.ToDictionary()),                                () => NormalizeDictionary(SeamlessEnumerable.ToDictionary(new[] { new KeyValuePair<string,int>("a",1),new KeyValuePair<string,int>("b",2) })));
        AssertSameSequence(() => NormalizeDictionary(new[] { new KeyValuePair<string,int>("a",1),new KeyValuePair<string,int>("B",2) }.ToDictionary(StringComparer.OrdinalIgnoreCase)),() => NormalizeDictionary(SeamlessEnumerable.ToDictionary(new[] { new KeyValuePair<string,int>("a",1),new KeyValuePair<string,int>("B",2) },StringComparer.OrdinalIgnoreCase)));
        AssertSameSequence(() => NormalizeDictionary(new[] { ("a",1),("b",2) }.ToDictionary()),                                                                                        () => NormalizeDictionary(SeamlessEnumerable.ToDictionary(new[] { ("a",1),("b",2) })));
        AssertSameSequence(() => NormalizeDictionary(new[] { ("a",1),("B",2) }.ToDictionary(StringComparer.OrdinalIgnoreCase)),                                                        () => NormalizeDictionary(SeamlessEnumerable.ToDictionary(new[] { ("a",1),("B",2) },StringComparer.OrdinalIgnoreCase)));
        AssertSameSequence(() => NormalizeDictionary(rows.ToDictionary(static row => row.Id)),                                                                                         () => NormalizeDictionary(SeamlessEnumerable.ToDictionary(rows,                     static row => row.Id)));
        AssertSameSequence(() => NormalizeDictionary(rows.ToDictionary(static row => row.Category,StringComparer.OrdinalIgnoreCase)),                                                  () => NormalizeDictionary(SeamlessEnumerable.ToDictionary(rows,                     static row => row.Category,StringComparer.OrdinalIgnoreCase)));
        AssertSameSequence(() => NormalizeDictionary(rows.ToDictionary(static row => row.Id,      static row => row.Score)),                                                           () => NormalizeDictionary(SeamlessEnumerable.ToDictionary(rows,                     static row => row.Id,      static row => row.Score)));
        AssertSameSequence(() => NormalizeDictionary(rows.ToDictionary(static row => row.Category,static row => row.Score,StringComparer.OrdinalIgnoreCase)),                          () => NormalizeDictionary(SeamlessEnumerable.ToDictionary(rows,                     static row => row.Category,static row => row.Score,StringComparer.OrdinalIgnoreCase)));
        AssertSameException<ArgumentException>(() => { _=RowsWithDuplicates().ToDictionary(static row => row.Id); },() => { _=SeamlessEnumerable.ToDictionary(RowsWithDuplicates(),                                                                       static row => row.Id); });
    }

    [Fact, Trait("Category","Unit")]
    public void ToHashSet() {
        AssertSameSequence(() => DuplicateNumbers().ToHashSet().Order(),() => SeamlessEnumerable.ToHashSet(DuplicateNumbers()).Order());
        AssertSameSequence(() => DuplicateNumbers().ToHashSet(new ModuloComparer(3)).Order(),() =>{
            IEnumerable<int> source=DuplicateNumbers();
            var set=new global::System.Collections.Generic.HashSet<int>(new ModuloComparer(3));
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
        AssertSameSequence(() => NormalizeLookup(rows.ToLookup(static row => row.Category)),                                 () => NormalizeLookup(SeamlessEnumerable.ToLookup(rows,static row => row.Category)));
        AssertSameSequence(() => NormalizeLookup(rows.ToLookup(static row => row.Category,StringComparer.OrdinalIgnoreCase)),() =>{
            Func<Row,string> keySelector=static row => row.Category;
            IEqualityComparer<string>? comparer=StringComparer.OrdinalIgnoreCase;
            comparer??=EqualityComparer<string>.Default;
            var lookup=new SeamlessEnumerable.LookupImpl<string,Row>(comparer);
            foreach(var item in rows) {
                lookup.Add(keySelector(item),item);
            }
            return NormalizeLookup(lookup);
        });
        AssertSameSequence(() => NormalizeLookup(rows.ToLookup(static row => row.Category,static row => row.Score)),         () => NormalizeLookup(SeamlessEnumerable.ToLookup(rows,static row => row.Category,static row => row.Score)));
        AssertSameSequence(() => NormalizeLookup(rows.ToLookup(static row => row.Category,static row => row.Score,StringComparer.OrdinalIgnoreCase)),() =>{
            Func<Row,string> keySelector=static row => row.Category;
            Func<Row,int> elementSelector=static row => row.Score;
            var lookup=new SeamlessEnumerable.LookupImpl<string,int>(StringComparer.OrdinalIgnoreCase);
            foreach(var item in rows) {
                lookup.Add(keySelector(item),elementSelector(item));
            }
            return NormalizeLookup(lookup);
        });
    }

    [Fact, Trait("Category","Unit")]
    public void TryGetNonEnumeratedCount() {
        AssertSameCountResult(DuplicateNumbers(),DuplicateNumbers());
        var filtered = SeamlessEnumerable.Where(DuplicateNumbers(),static value => value>2);
        Assert.True(SeamlessEnumerable.TryGetNonEnumeratedCount(filtered,out var count));
        Assert.Equal(5,count);
    }

    [Fact, Trait("Category","Unit")]
    public void UnionBy() {
        var first = RowsWithDuplicates();
        var second = new[] { new Row(10,"alpha",100,10m,"new"),new Row(11,"delta",110,11m,"new") };
        AssertSameSequence(() => first.UnionBy(second,static row => row.Category),() => SeamlessEnumerable.UnionBy(first,second,static row => row.Category));
        AssertSameSequence(() => first.UnionBy(second,static row => row.Category,StringComparer.OrdinalIgnoreCase),() => SeamlessEnumerable.UnionBy(first,second,static row => row.Category,StringComparer.OrdinalIgnoreCase));
    }

    [Fact, Trait("Category","Unit")]
    public void Union() {
        AssertSameSequence(() => DuplicateNumbers().Union(new[] { 1,7,8 }),() => SeamlessEnumerable.Union(DuplicateNumbers(),new[] { 1,7,8 }));
        AssertSameSequence(() => DuplicateNumbers().Union(new[] { 7,8 },new ModuloComparer(3)),() => SeamlessEnumerable.Union(DuplicateNumbers(),new[] { 7,8 },new ModuloComparer(3)));
    }

    [Fact, Trait("Category","Unit")]
    public void Where() {
        AssertSameSequence(() => DuplicateNumbers().Where(static value => value%2==0),() => SeamlessEnumerable.Where(DuplicateNumbers(),static value => value%2==0));
        AssertSameSequence(() => DuplicateNumbers().Where(static (value,index) => (value+index)%2==0),() => SeamlessEnumerable.Where(DuplicateNumbers(),static (value,index) => (value+index)%2==0));
    }

    [Fact, Trait("Category","Unit")]
    public void Zip() {
        AssertSameSequence(() => new[] { 1,2,3 }.Zip(new[] { "a","b" }),() => SeamlessEnumerable.Zip(new[] { 1,2,3 },new[] { "a","b" }));
        AssertSameSequence(() => new[] { 1,2,3 }.Zip(new[] { "a","b","c" },new[] { 10m,20m }),() => SeamlessEnumerable.Zip(new[] { 1,2,3 },new[] { "a","b","c" },new[] { 10m,20m }));
        AssertSameSequence(() => new[] { 1,2,3 }.Zip(new[] { 10,20 },static (left,right) => left+right),() => SeamlessEnumerable.Zip(new[] { 1,2,3 },new[] { 10,20 },static (left,right) => left+right));
    }

    [Fact, Trait("Category","Unit")]
    public void SequenceOperatorsCoverRepresentativeTypes() {
        AssertCoreSequenceOperators(new[] { "beta","alpha","beta","gamma" },"omega",static value => value.Length>4,static value => value.ToUpperInvariant());
        AssertCoreSequenceOperators(new[] { 2.5m,1.5m,2.5m,4.5m },9.5m,static value => value>2m,static value => $"{value:0.0}");
        AssertCoreSequenceOperators(new[] { 2.5d,1.5d,2.5d,4.5d },9.5d,static value => value>2d,static value => $"{value:0.0}");
        AssertCoreSequenceOperators(
            new[] {
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Guid.Parse("11111111-1111-1111-1111-111111111111")
            },
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            static value => value.ToString("N")[0]=='1',
            static value => value.ToString("N"));
        AssertCoreSequenceOperators(
            new[] { new DateTime(2026,4,1),new DateTime(2026,4,2),new DateTime(2026,4,1) },
            new DateTime(2026,4,30),
            static value => value.Day>1,
            static value => value.ToString("yyyyMMdd"));
        AssertCoreSequenceOperators(
            new[] { new Row(1,"alpha",10,1.5m,"one"),new Row(2,"beta",20,2.5m,null),new Row(1,"alpha",10,1.5m,"one") },
            new Row(3,"gamma",30,3.5m,"three"),
            static value => value.Score>=10,
            static value => $"{value.Category}:{value.Amount}");
    }

    //[Fact, Trait("Category","Unit")]
    //public void ExplicitStorageEnumerableVariants() {
    //    var source = new[] { 1,2,3,4 };

    //    AssertSequenceAndDispose(new[] { 2,4 },global::Seamless.Linq.UnmanagedEnumerable.Where(source,static value => value%2==0));
    //    AssertSequenceAndDispose(new[] { 2,4 },global::Seamless.Linq.UnmanagedEnumerable.Where<int,EvenStrategy>(source));
    //    AssertSequenceAndDispose(new[] { 2,4,6,8 },global::Seamless.Linq.UnmanagedEnumerable.Select<int,int,DoubleStrategy>(source));
    //    AssertSequenceAndDispose(new[] { "v1","v2","v3","v4" },global::Seamless.Linq.ManagedEnumerable.Select(source,static value => "v"+value));
    //    AssertSequenceAndDispose(new[] { (1,10),(1,20),(2,10),(2,20) },global::Seamless.Linq.ManagedEnumerable.SelectMany(new[] { 1,2 },static _ => new[] { 10,20 },static (outer,inner) => (outer,inner)));
    //}

    private static void AssertCoreSequenceOperators<T>(T[] values,T extra,Func<T,bool> predicate,Func<T,string> selector) {
        AssertSameSequence(() => values.Where(predicate),                                                  () => SeamlessEnumerable.Where(values,predicate));
        AssertSameSequence(() => values.Select(selector),                                                  () => SeamlessEnumerable.Select(values,selector));
        AssertSameSequence(() => values.SelectMany(value => new[] { value,extra }),                        () => SeamlessEnumerable.SelectMany(values,value => new[] { value,extra }));
        AssertSameSequence(() => values.Concat(new[] { extra }),                                           () => SeamlessEnumerable.Concat(values,new[] { extra }));
        AssertSameSequence(() => values.Append(extra),                                                     () => SeamlessEnumerable.Append(values,extra));
        AssertSameSequence(() => values.Prepend(extra),                                                    () => SeamlessEnumerable.Prepend(values,extra));
        AssertSameSequence(() => values.Distinct(),                                                        () => SeamlessEnumerable.Distinct(values));
        AssertSameSequence(() => values.Skip(1),                                          () => SeamlessEnumerable.Skip(values,1));
        AssertSameSequence(() => values.Take(3),                                          () => SeamlessEnumerable.Take(values,3));
        AssertSameSequence(() => Array.Empty<T>().DefaultIfEmpty(extra),                  () => SeamlessEnumerable.DefaultIfEmpty(Array.Empty<T>(),extra));
        AssertSameSequence(() => values.Zip(SystemEnumerable.Repeat(extra,values.Length)),() => SeamlessEnumerable.Zip(values,SeamlessEnumerable.Repeat(extra,values.Length)));
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
        AssertSameValue(() => new[] { 1.5m,2.5m,2.5m }.Max(),                               () => SeamlessEnumerable.Max(new[] { 1.5m,2.5m,2.5m }));
        AssertSameValue(() => new[] { 1.5d,2.5d,2.5d }.Max(),                               () => SeamlessEnumerable.Max(new[] { 1.5d,2.5d,2.5d }));
        AssertSameValue(() => new[] { 1,2,2 }.Max(),                                        () => SeamlessEnumerable.Max(new[] { 1,2,2 }));
        AssertSameValue(() => new[] { 1L,2L,2L }.Max(),                                     () => SeamlessEnumerable.Max(new[] { 1L,2L,2L }));
        AssertSameValue(() => new[] { 1.5f,2.5f,2.5f }.Max(),                               () => SeamlessEnumerable.Max(new[] { 1.5f,2.5f,2.5f }));
        AssertSameValue(() => new decimal?[] { 1.5m,null,2.5m }.Max(),                      () => SeamlessEnumerable.Max(new decimal?[] { 1.5m,null,2.5m }));
        AssertSameValue(() => new double?[] { 1.5d,null,2.5d }.Max(),                       () => SeamlessEnumerable.Max(new double?[] { 1.5d,null,2.5d }));
        AssertSameValue(() => new int?[] { 1,null,2 }.Max(),                                () => SeamlessEnumerable.Max(new int?[] { 1,null,2 }));
        AssertSameValue(() => new long?[] { 1L,null,2L }.Max(),                             () => SeamlessEnumerable.Max(new long?[] { 1L,null,2L }));
        AssertSameValue(() => new float?[] { 1.5f,null,2.5f }.Max(),                        () => SeamlessEnumerable.Max(new float?[] { 1.5f,null,2.5f }));
        AssertSameValue(() => rows.Max(static row => row.Amount),          () => SeamlessEnumerable.Max(rows,static row => row.Amount));
        AssertSameValue(() => rows.Max(static row => (double)row.Score),   () => SeamlessEnumerable.Max(rows,static row => (double)row.Score));
        AssertSameValue(() => rows.Max(static row => row.Score),           () => SeamlessEnumerable.Max(rows,static row => row.Score));
        AssertSameValue(() => rows.Max(static row => (long)row.Score),     () => SeamlessEnumerable.Max(rows,static row => (long)row.Score));
        AssertSameValue(() => rows.Max(static row => (float)row.Score),    () => SeamlessEnumerable.Max(rows,static row => (float)row.Score));
        AssertSameValue(() => rows.Max(static row => (decimal?)row.Amount),() => SeamlessEnumerable.Max(rows,static row => (decimal?)row.Amount));
        AssertSameValue(() => rows.Max(static row => (double?)row.Score),  () => SeamlessEnumerable.Max(rows,static row => (double?)row.Score));
        AssertSameValue(() => rows.Max(static row => (int?)row.Score),     () => SeamlessEnumerable.Max(rows,static row => (int?)row.Score));
        AssertSameValue(() => rows.Max(static row => (long?)row.Score),    () => SeamlessEnumerable.Max(rows,static row => (long?)row.Score));
        AssertSameValue(() => rows.Max(static row => (float?)row.Score),   () => SeamlessEnumerable.Max(rows,static row => (float?)row.Score));
    }

    private static void AssertNumericMin(Row[] rows) {
        AssertSameValue(() => new[] { 1.5m,2.5m,2.5m }.Min(),                               () => SeamlessEnumerable.Min(new[] { 1.5m,2.5m,2.5m }));
        AssertSameValue(() => new[] { 1.5d,2.5d,2.5d }.Min(),                               () => SeamlessEnumerable.Min(new[] { 1.5d,2.5d,2.5d }));
        AssertSameValue(() => new[] { 1,2,2 }.Min(),                                        () => SeamlessEnumerable.Min(new[] { 1,2,2 }));
        AssertSameValue(() => new[] { 1L,2L,2L }.Min(),                                     () => SeamlessEnumerable.Min(new[] { 1L,2L,2L }));
        AssertSameValue(() => new[] { 1.5f,2.5f,2.5f }.Min(),                               () => SeamlessEnumerable.Min(new[] { 1.5f,2.5f,2.5f }));
        AssertSameValue(() => new decimal?[] { 1.5m,null,2.5m }.Min(),                      () => SeamlessEnumerable.Min(new decimal?[] { 1.5m,null,2.5m }));
        AssertSameValue(() => new double?[] { 1.5d,null,2.5d }.Min(),                       () => SeamlessEnumerable.Min(new double?[] { 1.5d,null,2.5d }));
        AssertSameValue(() => new int?[] { 1,null,2 }.Min(),                                () => SeamlessEnumerable.Min(new int?[] { 1,null,2 }));
        AssertSameValue(() => new long?[] { 1L,null,2L }.Min(),                             () => SeamlessEnumerable.Min(new long?[] { 1L,null,2L }));
        AssertSameValue(() => new float?[] { 1.5f,null,2.5f }.Min(),                        () => SeamlessEnumerable.Min(new float?[] { 1.5f,null,2.5f }));
        AssertSameValue(() => rows.Min(static row => row.Amount),          () => SeamlessEnumerable.Min(rows,static row => row.Amount));
        AssertSameValue(() => rows.Min(static row => (double)row.Score),   () => SeamlessEnumerable.Min(rows,static row => (double)row.Score));
        AssertSameValue(() => rows.Min(static row => row.Score),           () => SeamlessEnumerable.Min(rows,static row => row.Score));
        AssertSameValue(() => rows.Min(static row => (long)row.Score),     () => SeamlessEnumerable.Min(rows,static row => (long)row.Score));
        AssertSameValue(() => rows.Min(static row => (float)row.Score),    () => SeamlessEnumerable.Min(rows,static row => (float)row.Score));
        AssertSameValue(() => rows.Min(static row => (decimal?)row.Amount),() => SeamlessEnumerable.Min(rows,static row => (decimal?)row.Amount));
        AssertSameValue(() => rows.Min(static row => (double?)row.Score),  () => SeamlessEnumerable.Min(rows,static row => (double?)row.Score));
        AssertSameValue(() => rows.Min(static row => (int?)row.Score),     () => SeamlessEnumerable.Min(rows,static row => (int?)row.Score));
        AssertSameValue(() => rows.Min(static row => (long?)row.Score),    () => SeamlessEnumerable.Min(rows,static row => (long?)row.Score));
        AssertSameValue(() => rows.Min(static row => (float?)row.Score),   () => SeamlessEnumerable.Min(rows,static row => (float?)row.Score));
    }

    private static void AssertNumericSum(Row[] rows) {
        AssertSameValue(() => new[] { 1.5m,2.5m,2.5m }.Sum(),                               () => SeamlessEnumerable.Sum(new[] { 1.5m,2.5m,2.5m }));
        AssertSameValue(() => new[] { 1.5d,2.5d,2.5d }.Sum(),                               () => SeamlessEnumerable.Sum(new[] { 1.5d,2.5d,2.5d }));
        AssertSameValue(() => new[] { 1,2,2 }.Sum(),                                        () => SeamlessEnumerable.Sum(new[] { 1,2,2 }));
        AssertSameValue(() => new[] { 1L,2L,2L }.Sum(),                                     () => SeamlessEnumerable.Sum(new[] { 1L,2L,2L }));
        AssertSameValue(() => new[] { 1.5f,2.5f,2.5f }.Sum(),                               () => SeamlessEnumerable.Sum(new[] { 1.5f,2.5f,2.5f }));
        AssertSameValue(() => new decimal?[] { 1.5m,null,2.5m }.Sum(),                      () => SeamlessEnumerable.Sum(new decimal?[] { 1.5m,null,2.5m }));
        AssertSameValue(() => new double?[] { 1.5d,null,2.5d }.Sum(),                       () => SeamlessEnumerable.Sum(new double?[] { 1.5d,null,2.5d }));
        AssertSameValue(() => new int?[] { 1,null,2 }.Sum(),                                () => SeamlessEnumerable.Sum(new int?[] { 1,null,2 }));
        AssertSameValue(() => new long?[] { 1L,null,2L }.Sum(),                             () => SeamlessEnumerable.Sum(new long?[] { 1L,null,2L }));
        AssertSameValue(() => new float?[] { 1.5f,null,2.5f }.Sum(),                        () => SeamlessEnumerable.Sum(new float?[] { 1.5f,null,2.5f }));
        AssertSameValue(() => rows.Sum(static row => row.Amount),          () => SeamlessEnumerable.Sum(rows,static row => row.Amount));
        AssertSameValue(() => rows.Sum(static row => (double)row.Score),   () => SeamlessEnumerable.Sum(rows,static row => (double)row.Score));
        AssertSameValue(() => rows.Sum(static row => row.Score),           () => SeamlessEnumerable.Sum(rows,static row => row.Score));
        AssertSameValue(() => rows.Sum(static row => (long)row.Score),     () => SeamlessEnumerable.Sum(rows,static row => (long)row.Score));
        AssertSameValue(() => rows.Sum(static row => (float)row.Score),    () => SeamlessEnumerable.Sum(rows,static row => (float)row.Score));
        AssertSameValue(() => rows.Sum(static row => (decimal?)row.Amount),() => SeamlessEnumerable.Sum(rows,static row => (decimal?)row.Amount));
        AssertSameValue(() => rows.Sum(static row => (double?)row.Score),  () => SeamlessEnumerable.Sum(rows,static row => (double?)row.Score));
        AssertSameValue(() => rows.Sum(static row => (int?)row.Score),     () => SeamlessEnumerable.Sum(rows,static row => (int?)row.Score));
        AssertSameValue(() => rows.Sum(static row => (long?)row.Score),    () => SeamlessEnumerable.Sum(rows,static row => (long?)row.Score));
        AssertSameValue(() => rows.Sum(static row => (float?)row.Score),   () => SeamlessEnumerable.Sum(rows,static row => (float?)row.Score));
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
        var actualResult = SeamlessEnumerable.TryGetNonEnumeratedCount(actualSource,out var actualCount);
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

    private static int[] DuplicateNumbers() => new[] { 4,1,2,4,3,2,5,1,6 };

    private static int[] UniqueNumbers() => new[] { -3,-1,0,2,5,8 };

    private static Row[] RowsWithDuplicates() => new[]{
        new Row(1,"alpha",10,1.5m,"one"),
        new Row(2,"beta",20,2.5m,null),
        new Row(1,"alpha",15,1.5m,"duplicate-id"),
        new Row(2,"beta",20,2.5m,null),
        new Row(3,"gamma",-5,3.5m,"three"),
        new Row(4,"gamma",0,4.5m,"four")
    };

    private static Row[] RowsWithoutDuplicateKeys() => new[]{
        new Row(1,"alpha",10,1.5m,"one"),
        new Row(2,"beta",20,2.5m,null),
        new Row(3,"gamma",-5,3.5m,"three"),
        new Row(4,"delta",0,4.5m,"four")
    };

    private static TestOrder[] Orders() => new[]{
        new TestOrder(1,"A",2),
        new TestOrder(1,"B",3),
        new TestOrder(2,"C",5),
        new TestOrder(5,"missing",7)
    };

    private sealed record Row(int Id,string Category,int Score,decimal Amount,string? Note);

    private sealed record TestOrder(int RowId,string Code,int Quantity);

    //private readonly struct DoubleStrategy:global::Seamless.Linq.IFunction<int,int> {
    //    public static int Invoke(int value) => value*2;
    //}

    //private readonly struct EvenStrategy:global::Seamless.Linq.IFunction<int,bool> {
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



