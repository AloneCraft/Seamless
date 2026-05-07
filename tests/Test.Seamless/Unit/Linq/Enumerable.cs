using System.Linq;
using SeamlessEnumerable = Seamless.Linq.Enumerable;
using SystemEnumerable = System.Linq.Enumerable;
// ReSharper disable InvokeAsExtensionMember

namespace Test.Seamless.Unit.Linq;

public sealed class Enumerable {
    [Fact, Trait("Category","Unit")]
    public void Aggregate() {
        var values = DuplicateNumbers();
        AssertSameValue(() => SystemEnumerable.Aggregate(values,static (total,item) => total+item),() => SeamlessEnumerable.Aggregate(values,static (total,item) => total+item));
        AssertSameValue(() => SystemEnumerable.Aggregate(values,100,static (total,item) => total-item),() => SeamlessEnumerable.Aggregate(values,100,static (total,item) => total-item));
        AssertSameValue(() => SystemEnumerable.Aggregate(values,0,static (total,item) => total+item,static total => $"sum:{total}"),() => SeamlessEnumerable.Aggregate(values,0,static (total,item) => total+item,static total => $"sum:{total}"));
    }

    [Fact, Trait("Category","Unit")]
    public void AggregateBy() {
        var rows = RowsWithDuplicates();
        IEqualityComparer<string> comparer = StringComparer.OrdinalIgnoreCase;
        AssertSameSequence(() => SystemEnumerable.AggregateBy(rows,static row => row.Category,0,static (total,row) => total+row.Score),() => SeamlessEnumerable.AggregateBy(rows,static row => row.Category,0,static (total,row) => total+row.Score));
        AssertSameSequence(() => SystemEnumerable.AggregateBy(rows,static row => row.Category,static category => category.Length,static (total,row) => total+row.Score,comparer),() => SeamlessEnumerable.AggregateBy(rows,static row => row.Category,static category => category.Length,static (total,row) => total+row.Score,comparer));
    }

    [Fact, Trait("Category","Unit")]
    public void All() {
        var rows = RowsWithDuplicates();
        AssertSameValue(() => SystemEnumerable.All(rows,static row => row.Score>=-5),() => SeamlessEnumerable.All(rows,static row => row.Score>=-5));
        AssertSameValue(() => SystemEnumerable.All(rows,static row => row.Category=="alpha"),() => SeamlessEnumerable.All(rows,static row => row.Category=="alpha"));
    }

    [Fact, Trait("Category","Unit")]
    public void Any() {
        var rows = RowsWithDuplicates();
        AssertSameValue(() => SystemEnumerable.Any(rows),() => SeamlessEnumerable.Any(rows));
        AssertSameValue(() => SystemEnumerable.Any(Array.Empty<Row>()),() => SeamlessEnumerable.Any(Array.Empty<Row>()));
        AssertSameValue(() => SystemEnumerable.Any(rows,static row => row.Note is null),() => SeamlessEnumerable.Any(rows,static row => row.Note is null));
    }

    [Fact, Trait("Category","Unit")]
    public void Append() {
        AssertSameSequence(() => SystemEnumerable.Append(DuplicateNumbers(),9),() => SeamlessEnumerable.Append(DuplicateNumbers(),9));
    }

    [Fact, Trait("Category","Unit")]
    public void AsEnumerable() {
        var values = DuplicateNumbers();
        Assert.Same(SystemEnumerable.AsEnumerable(values),SeamlessEnumerable.AsEnumerable(values));
    }

    [Fact, Trait("Category","Unit")]
    public void Average() {
        var rows = RowsWithDuplicates();
        var ints = new[] { 1,2,2,5,-1 };
        var longs = new[] { 1L,4L,4L,10L };
        var floats = new[] { 1.5f,2.5f,2.5f,5.5f };
        var doubles = new[] { 1.5d,2.5d,2.5d,5.5d };
        var decimals = new[] { 1.5m,2.5m,2.5m,5.5m };
        AssertSameValue(() => SystemEnumerable.Average(ints),                                   () => SeamlessEnumerable.Average(ints));
        AssertSameValue(() => SystemEnumerable.Average(longs),                                  () => SeamlessEnumerable.Average(longs));
        AssertSameValue(() => SystemEnumerable.Average(floats),                                 () => SeamlessEnumerable.Average(floats));
        AssertSameValue(() => SystemEnumerable.Average(doubles),                                () => SeamlessEnumerable.Average(doubles));
        AssertSameValue(() => SystemEnumerable.Average(decimals),                               () =>{
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
        AssertSameValue(() => SystemEnumerable.Average(new int?[] { 1,null,2,2 }),              () => SeamlessEnumerable.Average(new int?[] { 1,null,2,2 }));
        AssertSameValue(() => SystemEnumerable.Average(new long?[] { 1L,null,4L,4L }),          () => SeamlessEnumerable.Average(new long?[] { 1L,null,4L,4L }));
        AssertSameValue(() => SystemEnumerable.Average(new float?[] { 1.5f,null,2.5f,2.5f }),   () => SeamlessEnumerable.Average(new float?[] { 1.5f,null,2.5f,2.5f }));
        AssertSameValue(() => SystemEnumerable.Average(new double?[] { 1.5d,null,2.5d,2.5d }),  () => SeamlessEnumerable.Average(new double?[] { 1.5d,null,2.5d,2.5d }));
        AssertSameValue(() => SystemEnumerable.Average(new decimal?[] { 1.5m,null,2.5m,2.5m }), () => SeamlessEnumerable.Average(new decimal?[] { 1.5m,null,2.5m,2.5m }));
        AssertSameValue(() => SystemEnumerable.Average(rows,static row => row.Amount),          () => SeamlessEnumerable.Average(rows,static row => row.Amount));
        AssertSameValue(() => SystemEnumerable.Average(rows,static row => (double)row.Amount),  () => SeamlessEnumerable.Average(rows,static row => (double)row.Amount));
        AssertSameValue(() => SystemEnumerable.Average(rows,static row => row.Score),           () => SeamlessEnumerable.Average(rows,static row => row.Score));
        AssertSameValue(() => SystemEnumerable.Average(rows,static row => (long)row.Score),     () => SeamlessEnumerable.Average(rows,static row => (long)row.Score));
        AssertSameValue(() => SystemEnumerable.Average(rows,static row => (float)row.Score),    () => SeamlessEnumerable.Average(rows,static row => (float)row.Score));
        AssertSameValue(() => SystemEnumerable.Average(rows,static row => (decimal?)row.Amount),() => SeamlessEnumerable.Average(rows,static row => (decimal?)row.Amount));
        AssertSameValue(() => SystemEnumerable.Average(rows,static row => (double?)row.Score),  () => SeamlessEnumerable.Average(rows,static row => (double?)row.Score));
        AssertSameValue(() => SystemEnumerable.Average(rows,static row => (int?)row.Score),     () => SeamlessEnumerable.Average(rows,static row => (int?)row.Score));
        AssertSameValue(() => SystemEnumerable.Average(rows,static row => (long?)row.Score),    () => SeamlessEnumerable.Average(rows,static row => (long?)row.Score));
        AssertSameValue(() => SystemEnumerable.Average(rows,static row => (float?)row.Score),   () => SeamlessEnumerable.Average(rows,static row => (float?)row.Score));
    }

    [Fact, Trait("Category","Unit")]
    public void Cast() {
        var values = new ArrayList { 1,2,2,5 };
        AssertSameSequence(() => SystemEnumerable.Cast<int>(values),() => SeamlessEnumerable.Cast<int>(values));
    }

    [Fact, Trait("Category","Unit")]
    public void Chunk() {
        AssertSameSequence(() => ChunkText(SystemEnumerable.Chunk(DuplicateNumbers(),3)),() => ChunkText(SeamlessEnumerable.Chunk(DuplicateNumbers(),3)));
    }

    [Fact, Trait("Category","Unit")]
    public void Concat() {
        AssertSameSequence(() => SystemEnumerable.Concat(DuplicateNumbers(),UniqueNumbers()),() => SeamlessEnumerable.Concat(DuplicateNumbers(),UniqueNumbers()));
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
        AssertSameValue(() => SystemEnumerable.Count(rows),() => SeamlessEnumerable.Count(rows));
        AssertSameValue(() => SystemEnumerable.Count(rows,static row => row.Category=="beta"),() => SeamlessEnumerable.Count(rows,static row => row.Category=="beta"));
    }

    [Fact, Trait("Category","Unit")]
    public void CountBy() {
        var rows = RowsWithDuplicates();
        AssertSameSequence(() => SystemEnumerable.CountBy(rows,static row => row.Category),() => SeamlessEnumerable.CountBy(rows,static row => row.Category));
        AssertSameSequence(() => SystemEnumerable.CountBy(new[] { "A","a","B","b","b" },static value => value,StringComparer.OrdinalIgnoreCase),() => SeamlessEnumerable.CountBy(new[] { "A","a","B","b","b" },static value => value,StringComparer.OrdinalIgnoreCase));
    }

    [Fact, Trait("Category","Unit")]
    public void DefaultIfEmpty() {
        AssertSameSequence(() => SystemEnumerable.DefaultIfEmpty(DuplicateNumbers()),() => SeamlessEnumerable.DefaultIfEmpty(DuplicateNumbers()));
        AssertSameSequence(() => SystemEnumerable.DefaultIfEmpty(Array.Empty<int>()),() => SeamlessEnumerable.DefaultIfEmpty(Array.Empty<int>()));
        AssertSameSequence(() => SystemEnumerable.DefaultIfEmpty(Array.Empty<int>(),42),() => SeamlessEnumerable.DefaultIfEmpty(Array.Empty<int>(),42));
    }

    [Fact, Trait("Category","Unit")]
    public void DistinctBy() {
        var rows = RowsWithDuplicates();
        AssertSameSequence(() => SystemEnumerable.DistinctBy(rows,static row => row.Category),() => SeamlessEnumerable.DistinctBy(rows,static row => row.Category));
        AssertSameSequence(() => SystemEnumerable.DistinctBy(rows,static row => row.Category,StringComparer.OrdinalIgnoreCase),() =>{
            Func<Row,string> keySelector=static row => row.Category;
            return rows.DistinctBy(keySelector,StringComparer.OrdinalIgnoreCase);
        });
    }

    [Fact, Trait("Category","Unit")]
    public void Distinct() {
        AssertSameSequence(() => SystemEnumerable.Distinct(DuplicateNumbers()),() => SeamlessEnumerable.Distinct(DuplicateNumbers()));
        AssertSameSequence(() => SystemEnumerable.Distinct(UniqueNumbers()),() => SeamlessEnumerable.Distinct(UniqueNumbers()));
        AssertSameSequence(() => SystemEnumerable.Distinct(DuplicateNumbers(),new ModuloComparer(3)),() =>{
            IEqualityComparer<int>? comparer=new ModuloComparer(3);
            return DuplicateNumbers().Distinct(comparer);
        });
    }

    [Fact, Trait("Category","Unit")]
    public void ElementAtOrDefault() {
        var values = DuplicateNumbers();
        AssertSameValue(() => SystemEnumerable.ElementAtOrDefault(values,3),() => SeamlessEnumerable.ElementAtOrDefault(values,3));
        AssertSameValue(() => SystemEnumerable.ElementAtOrDefault(values,99),() => SeamlessEnumerable.ElementAtOrDefault(values,99));
        AssertSameValue(() => SystemEnumerable.ElementAtOrDefault(values,^2),() => SeamlessEnumerable.ElementAtOrDefault(values,^2));
    }

    [Fact, Trait("Category","Unit")]
    public void ElementAt() {
        var values = DuplicateNumbers();
        AssertSameValue(() => SystemEnumerable.ElementAt(values,3),() => SeamlessEnumerable.ElementAt(values,3));
        AssertSameValue(() => SystemEnumerable.ElementAt(values,^2),() => SeamlessEnumerable.ElementAt(values,^2));
    }

    [Fact, Trait("Category","Unit")]
    public void Empty() {
        AssertSameSequence(() => SystemEnumerable.Empty<Row>(),() => SeamlessEnumerable.Empty<Row>());
    }

    [Fact, Trait("Category","Unit")]
    public void ExceptBy() {
        var rows = RowsWithDuplicates();
        AssertSameSequence(() => SystemEnumerable.ExceptBy(rows,new[] { 1,4 },static row => row.Id),() => SeamlessEnumerable.ExceptBy(rows,new[] { 1,4 },static row => row.Id));
        AssertSameSequence(() => SystemEnumerable.ExceptBy(rows,new[] { "ALPHA" },static row => row.Category,StringComparer.OrdinalIgnoreCase),() =>{
            IEnumerable<string> second     =new[] { "ALPHA" };
            Func<Row,string>    keySelector=static row => row.Category;
            return rows.ExceptBy(second,keySelector,StringComparer.OrdinalIgnoreCase);
        });
    }

    [Fact, Trait("Category","Unit")]
    public void Except() {
        AssertSameSequence(() => SystemEnumerable.Except(DuplicateNumbers(),new[] { 1,6 }),() => SeamlessEnumerable.Except(DuplicateNumbers(),new[] { 1,6 }));
        AssertSameSequence(() => SystemEnumerable.Except(DuplicateNumbers(),new[] { 7 },new ModuloComparer(3)),() =>{
            IEnumerable<int>        second  =new[] { 7 };
            IEqualityComparer<int>? comparer=new ModuloComparer(3);
            return DuplicateNumbers().Except(second,comparer);
        });
    }

    [Fact, Trait("Category","Unit")]
    public void FirstOrDefault() {
        var values = DuplicateNumbers();
        AssertSameValue(() => SystemEnumerable.FirstOrDefault(values),() => SeamlessEnumerable.FirstOrDefault(values));
        AssertSameValue(() => SystemEnumerable.FirstOrDefault(Array.Empty<int>(),42),() => SeamlessEnumerable.FirstOrDefault(Array.Empty<int>(),42));
        AssertSameValue(() => SystemEnumerable.FirstOrDefault(values,static value => value>3),() => SeamlessEnumerable.FirstOrDefault(values,static value => value>3));
        AssertSameValue(() => SystemEnumerable.FirstOrDefault(values,static value => value>20,42),() => SeamlessEnumerable.FirstOrDefault(values,static value => value>20,42));
    }

    [Fact, Trait("Category","Unit")]
    public void First() {
        var values = DuplicateNumbers();
        AssertSameValue(() => SystemEnumerable.First(values),() => SeamlessEnumerable.First(values));
        AssertSameValue(() => SystemEnumerable.First(values,static value => value>3),() => SeamlessEnumerable.First(values,static value => value>3));
    }

    [Fact, Trait("Category","Unit")]
    public void GroupBy() {
        var rows = RowsWithDuplicates();
        AssertSameSequence(() => NormalizeGroups(SystemEnumerable.GroupBy(rows,static row => row.Category)),                                                                                                     () => NormalizeGroups(SeamlessEnumerable.GroupBy(rows,static row => row.Category)));
        AssertSameSequence(() => NormalizeGroups(SystemEnumerable.GroupBy(rows,static row => row.Category,StringComparer.OrdinalIgnoreCase)),                                                                    () => NormalizeGroups(SeamlessEnumerable.GroupBy(rows,static row => row.Category,StringComparer.OrdinalIgnoreCase)));
        AssertSameSequence(() => NormalizeGroups(SystemEnumerable.GroupBy(rows,static row => row.Category,static row => row.Score)),                                                                             () => NormalizeGroups(SeamlessEnumerable.GroupBy(rows,static row => row.Category,static row => row.Score)));
        AssertSameSequence(() => NormalizeGroups(SystemEnumerable.GroupBy(rows,static row => row.Category,static row => row.Score,StringComparer.OrdinalIgnoreCase)),                                            () =>{
            Func<Row,string> keySelector    =static row => row.Category;
            Func<Row,int>    elementSelector=static row => row.Score;
            return NormalizeGroups(rows.GroupBy(keySelector,elementSelector,StringComparer.OrdinalIgnoreCase));
        });
        AssertSameSequence(() => SystemEnumerable.GroupBy(rows,static row => row.Category,static (key,                        items) => $"{key}:{SystemEnumerable.Count(items)}"),                               () => SeamlessEnumerable.GroupBy(rows,static row => row.Category,static (key,                        items) => $"{key}:{SystemEnumerable.Count(items)}"));
        AssertSameSequence(() => SystemEnumerable.GroupBy(rows,static row => row.Category,static row => row.Score,static (key,items) => $"{key}:{SystemEnumerable.Sum(items)}",StringComparer.OrdinalIgnoreCase),() => SeamlessEnumerable.GroupBy(rows,static row => row.Category,static row => row.Score,static (key,items) => $"{key}:{SystemEnumerable.Sum(items)}",StringComparer.OrdinalIgnoreCase));
    }

    [Fact, Trait("Category","Unit")]
    public void GroupJoin() {
        var rows = RowsWithDuplicates();
        var orders = Orders();
        AssertSameSequence(() => SystemEnumerable.GroupJoin(rows,orders,static row => row.Id,static order => order.RowId,static (row,items) => $"{row.Id}:{SystemEnumerable.Count(items)}"),() => SeamlessEnumerable.GroupJoin(rows,orders,static row => row.Id,static order => order.RowId,static (row,items) => $"{row.Id}:{SystemEnumerable.Count(items)}"));
        AssertSameSequence(() => SystemEnumerable.GroupJoin(rows,orders,static row => row.Id,static order => order.RowId,static (row,items) => $"{row.Id}:{SystemEnumerable.Sum(items,static item => item.Quantity)}",EqualityComparer<int>.Default),() => SeamlessEnumerable.GroupJoin(rows,orders,static row => row.Id,static order => order.RowId,static (row,items) => $"{row.Id}:{SystemEnumerable.Sum(items,static item => item.Quantity)}",EqualityComparer<int>.Default));
    }

    [Fact, Trait("Category","Unit")]
    public void Index() {
        AssertSameSequence(() => SystemEnumerable.Index(DuplicateNumbers()),() => SeamlessEnumerable.Index(DuplicateNumbers()));
    }

    [Fact, Trait("Category","Unit")]
    public void InfiniteSequence() {
        AssertSameSequence(() => SystemEnumerable.Take(SystemEnumerable.InfiniteSequence(3,4),6),() => SystemEnumerable.Take(SeamlessEnumerable.InfiniteSequence(3,4),6));
    }

    [Fact, Trait("Category","Unit")]
    public void IntersectBy() {
        var rows = RowsWithDuplicates();
        AssertSameSequence(() => SystemEnumerable.IntersectBy(rows,new[] { 1,4,9 },static row => row.Id),() => SeamlessEnumerable.IntersectBy(rows,new[] { 1,4,9 },static row => row.Id));
        AssertSameSequence(() => SystemEnumerable.IntersectBy(rows,new[] { "ALPHA","missing" },static row => row.Category,StringComparer.OrdinalIgnoreCase),() => SeamlessEnumerable.IntersectBy(rows,new[] { "ALPHA","missing" },static row => row.Category,StringComparer.OrdinalIgnoreCase));
    }

    [Fact, Trait("Category","Unit")]
    public void Intersect() {
        AssertSameSequence(() => SystemEnumerable.Intersect(DuplicateNumbers(),new[] { 1,4,9 }),() => SeamlessEnumerable.Intersect(DuplicateNumbers(),new[] { 1,4,9 }));
        AssertSameSequence(() => SystemEnumerable.Intersect(DuplicateNumbers(),new[] { 7,8 },new ModuloComparer(3)),() => SeamlessEnumerable.Intersect(DuplicateNumbers(),new[] { 7,8 },new ModuloComparer(3)));
    }

    [Fact, Trait("Category","Unit")]
    public void Join() {
        var rows = RowsWithDuplicates();
        var orders = Orders();
        AssertSameSequence(() => SystemEnumerable.Join(rows,orders,static row => row.Id,static order => order.RowId,static (row,order) => $"{row.Id}:{order.Code}"),() => SeamlessEnumerable.Join(rows,orders,static row => row.Id,static order => order.RowId,static (row,order) => $"{row.Id}:{order.Code}"));
        AssertSameSequence(() => SystemEnumerable.Join(rows,orders,static row => row.Id,static order => order.RowId,static (row,order) => $"{row.Id}:{order.Quantity}",EqualityComparer<int>.Default),() => {
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
        AssertSameValue(() => SystemEnumerable.LastOrDefault(values),() => SeamlessEnumerable.LastOrDefault(values));
        AssertSameValue(() => SystemEnumerable.LastOrDefault(Array.Empty<int>(),42),() => SeamlessEnumerable.LastOrDefault(Array.Empty<int>(),42));
        AssertSameValue(() => SystemEnumerable.LastOrDefault(values,static value => value<3),() => SeamlessEnumerable.LastOrDefault(values,static value => value<3));
        AssertSameValue(() => SystemEnumerable.LastOrDefault(values,static value => value>20,42),() => SeamlessEnumerable.LastOrDefault(values,static value => value>20,42));
    }

    [Fact, Trait("Category","Unit")]
    public void Last() {
        var values = DuplicateNumbers();
        AssertSameValue(() => SystemEnumerable.Last(values),() => SeamlessEnumerable.Last(values));
        AssertSameValue(() => SystemEnumerable.Last(values,static value => value<3),() => SeamlessEnumerable.Last(values,static value => value<3));
    }

    [Fact, Trait("Category","Unit")]
    public void LeftJoin() {
        var rows = RowsWithDuplicates();
        var orders = Orders();
        AssertSameSequence(() => SystemEnumerable.LeftJoin(rows,orders,static row => row.Id,static order => order.RowId,static (row,order) => $"{row.Id}:{order?.Code??"none"}"),() => SeamlessEnumerable.LeftJoin(rows,orders,static row => row.Id,static order => order.RowId,static (row,order) => $"{row.Id}:{order?.Code??"none"}"));
        AssertSameSequence(() => SystemEnumerable.LeftJoin(rows,orders,static row => row.Id,static order => order.RowId,static (row,order) => $"{row.Id}:{order?.Quantity??0}",EqualityComparer<int>.Default),() => SeamlessEnumerable.LeftJoin(rows,orders,static row => row.Id,static order => order.RowId,static (row,order) => $"{row.Id}:{order?.Quantity??0}",EqualityComparer<int>.Default));
    }

    [Fact, Trait("Category","Unit")]
    public void LongCount() {
        var rows = RowsWithDuplicates();
        AssertSameValue(() => SystemEnumerable.LongCount(rows),() => SeamlessEnumerable.LongCount(rows));
        AssertSameValue(() => SystemEnumerable.LongCount(rows,static row => row.Score>=10),() => SeamlessEnumerable.LongCount(rows,static row => row.Score>=10));
    }

    [Fact, Trait("Category","Unit")]
    public void Max() {
        var rows = RowsWithDuplicates();
        AssertNumericMax(rows);
        AssertSameValue(() => SystemEnumerable.Max(DuplicateNumbers()),() => SeamlessEnumerable.Max(DuplicateNumbers()));
        AssertSameValue(() => SystemEnumerable.Max(DuplicateNumbers(),Comparer<int>.Default),() => SeamlessEnumerable.Max(DuplicateNumbers(),Comparer<int>.Default));
        AssertSameValue(() => SystemEnumerable.Max(rows,static row => row.Note),() => SeamlessEnumerable.Max(rows,static row => row.Note));
    }

    [Fact, Trait("Category","Unit")]
    public void MaxBy() {
        var rows = RowsWithDuplicates();
        AssertSameValue(() => SystemEnumerable.MaxBy(rows,static row => row.Score),() => SeamlessEnumerable.MaxBy(rows,static row => row.Score));
        AssertSameValue(() => SystemEnumerable.MaxBy(rows,static row => row.Score,Comparer<int>.Default),() => SeamlessEnumerable.MaxBy(rows,static row => row.Score,Comparer<int>.Default));
    }

    [Fact, Trait("Category","Unit")]
    public void Min() {
        var rows = RowsWithDuplicates();
        AssertNumericMin(rows);
        AssertSameValue(() => SystemEnumerable.Min(DuplicateNumbers()),() => SeamlessEnumerable.Min(DuplicateNumbers()));
        AssertSameValue(() => SystemEnumerable.Min(DuplicateNumbers(),Comparer<int>.Default),() => SeamlessEnumerable.Min(DuplicateNumbers(),Comparer<int>.Default));
        AssertSameValue(() => SystemEnumerable.Min(rows,static row => row.Note),() => SeamlessEnumerable.Min(rows,static row => row.Note));
    }

    [Fact, Trait("Category","Unit")]
    public void MinBy() {
        var rows = RowsWithDuplicates();
        AssertSameValue(() => SystemEnumerable.MinBy(rows,static row => row.Score),() => SeamlessEnumerable.MinBy(rows,static row => row.Score));
        AssertSameValue(() => SystemEnumerable.MinBy(rows,static row => row.Score,Comparer<int>.Default),() => SeamlessEnumerable.MinBy(rows,static row => row.Score,Comparer<int>.Default));
    }

    [Fact, Trait("Category","Unit")]
    public void OfType() {
        var values = new ArrayList { 1,"two",2,"three",2 };
        AssertSameSequence(() => SystemEnumerable.OfType<int>(values),() => SeamlessEnumerable.OfType<int>(values));
    }

    [Fact, Trait("Category","Unit")]
    public void OrderByDescending() {
        var rows = RowsWithDuplicates();
        AssertSameSequence(() => SystemEnumerable.OrderByDescending(rows,static row => row.Score),() => SeamlessEnumerable.OrderByDescending(rows,static row => row.Score));
        AssertSameSequence(() => SystemEnumerable.OrderByDescending(rows,static row => row.Score,Comparer<int>.Default),() => SeamlessEnumerable.OrderByDescending(rows,static row => row.Score,Comparer<int>.Default));
    }

    [Fact, Trait("Category","Unit")]
    public void OrderBy() {
        var rows = RowsWithDuplicates();
        AssertSameSequence(() => SystemEnumerable.OrderBy(rows,static row => row.Category),() => SeamlessEnumerable.OrderBy(rows,static row => row.Category));
        AssertSameSequence(() => SystemEnumerable.OrderBy(rows,static row => row.Category,StringComparer.OrdinalIgnoreCase),() => SeamlessEnumerable.OrderBy(rows,static row => row.Category,StringComparer.OrdinalIgnoreCase));
    }

    [Fact, Trait("Category","Unit")]
    public void OrderDescending() {
        AssertSameSequence(() => SystemEnumerable.OrderDescending(DuplicateNumbers()),() => SeamlessEnumerable.OrderDescending(DuplicateNumbers()));
        AssertSameSequence(() => SystemEnumerable.OrderDescending(DuplicateNumbers(),Comparer<int>.Default),() => SeamlessEnumerable.OrderDescending(DuplicateNumbers(),Comparer<int>.Default));
    }

    [Fact, Trait("Category","Unit")]
    public void Order() {
        AssertSameSequence(() => SystemEnumerable.Order(DuplicateNumbers()),() => SeamlessEnumerable.Order(DuplicateNumbers()));
        AssertSameSequence(() => SystemEnumerable.Order(DuplicateNumbers(),new DescendingIntComparer()),() => SeamlessEnumerable.Order(DuplicateNumbers(),new DescendingIntComparer()));
    }

    [Fact, Trait("Category","Unit")]
    public void Prepend() {
        AssertSameSequence(() => SystemEnumerable.Prepend(DuplicateNumbers(),9),() => SeamlessEnumerable.Prepend(DuplicateNumbers(),9));
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
        AssertSameSequence(() => SystemEnumerable.Reverse(DuplicateNumbers()),() => SeamlessEnumerable.Reverse(DuplicateNumbers()));
        AssertSameSequence(() => SystemEnumerable.Reverse(UniqueNumbers()),() => SeamlessEnumerable.Reverse(UniqueNumbers()));
    }

    [Fact, Trait("Category","Unit")]
    public void RightJoin() {
        var rows = RowsWithDuplicates();
        var orders = Orders();
        AssertSameSequence(() => SystemEnumerable.RightJoin(rows,orders,static row => row.Id,static order => order.RowId,static (row,order) => $"{row?.Id??0}:{order.Code}"),() => SeamlessEnumerable.RightJoin(rows,orders,static row => row.Id,static order => order.RowId,static (row,order) => $"{row?.Id??0}:{order.Code}"));
        AssertSameSequence(() => SystemEnumerable.RightJoin(rows,orders,static row => row.Id,static order => order.RowId,static (row,order) => $"{row?.Id??0}:{order.Quantity}",EqualityComparer<int>.Default),() => SeamlessEnumerable.RightJoin(rows,orders,static row => row.Id,static order => order.RowId,static (row,order) => $"{row?.Id??0}:{order.Quantity}",EqualityComparer<int>.Default));
    }

    [Fact, Trait("Category","Unit")]
    public void SelectMany() {
        var rows = RowsWithDuplicates();
        AssertSameSequence(() => SystemEnumerable.SelectMany(rows,static row => new[] { row.Id,row.Score }),() => SeamlessEnumerable.SelectMany(rows,static row => new[] { row.Id,row.Score }));
        AssertSameSequence(() => SystemEnumerable.SelectMany(rows,static (row,index) => new[] { row.Id+index,row.Score-index }),() => SeamlessEnumerable.SelectMany(rows,static (row,index) => new[] { row.Id+index,row.Score-index }));
        AssertSameSequence(() => SystemEnumerable.SelectMany(rows,static row => new[] { row.Id,row.Score },static (row,value) => $"{row.Category}:{value}"),() => SeamlessEnumerable.SelectMany(rows,static row => new[] { row.Id,row.Score },static (row,value) => $"{row.Category}:{value}"));
        AssertSameSequence(() => SystemEnumerable.SelectMany(rows,static (row,index) => new[] { row.Id+index,row.Score-index },static (row,value) => $"{row.Category}:{value}"),() => SeamlessEnumerable.SelectMany(rows,static (row,index) => new[] { row.Id+index,row.Score-index },static (row,value) => $"{row.Category}:{value}"));
    }

    [Fact, Trait("Category","Unit")]
    public void Select() {
        var rows = RowsWithDuplicates();
        AssertSameSequence(() => SystemEnumerable.Select(rows,static row => $"{row.Category}:{row.Score}"),() => SeamlessEnumerable.Select(rows,static row => $"{row.Category}:{row.Score}"));
        AssertSameSequence(() => SystemEnumerable.Select(rows,static (row,index) => $"{index}:{row.Id}:{row.Score}"),() => SeamlessEnumerable.Select(rows,static (row,index) => $"{index}:{row.Id}:{row.Score}"));
        AssertSameException<ArgumentNullException>(() => { _=SystemEnumerable.Select((IEnumerable<int>)null!,static value => value); },() => { _=SeamlessEnumerable.Select((IEnumerable<int>)null!,static value => value); });
        AssertSameException<ArgumentNullException>(() => { _=SystemEnumerable.Select(new[] { 1 },(Func<int,int>)null!); },() => { _=SeamlessEnumerable.Select(new[] { 1 },(Func<int,int>)null!); });
        var mutable = new List<int> { 1,2 };
        var actual = SeamlessEnumerable.Select(mutable,static value => value*10);
        mutable.Add(3);
        Assert.Equal(new[] { 10,20 },SystemEnumerable.ToArray(actual));
        Assert.Equal(new[] { 10,20 },SystemEnumerable.ToArray(actual));
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
        Assert.Equal(SystemEnumerable.ToArray(SystemEnumerable.Order(SystemEnumerable.Shuffle(values))),SystemEnumerable.ToArray(SystemEnumerable.Order(SeamlessEnumerable.Shuffle(values))));
    }

    [Fact, Trait("Category","Unit")]
    public void SingleOrDefault() {
        AssertSameValue(() => SystemEnumerable.SingleOrDefault(new[] { 7 }),() => SeamlessEnumerable.SingleOrDefault(new[] { 7 }));
        AssertSameValue(() => SystemEnumerable.SingleOrDefault(Array.Empty<int>(),42),() => SeamlessEnumerable.SingleOrDefault(Array.Empty<int>(),42));
        AssertSameValue(() => SystemEnumerable.SingleOrDefault(DuplicateNumbers(),static value => value==6),() => SeamlessEnumerable.SingleOrDefault(DuplicateNumbers(),static value => value==6));
        AssertSameValue(() => SystemEnumerable.SingleOrDefault(DuplicateNumbers(),static value => value==99,42),() => SeamlessEnumerable.SingleOrDefault(DuplicateNumbers(),static value => value==99,42));
    }

    [Fact, Trait("Category","Unit")]
    public void Single() {
        AssertSameValue(() => SystemEnumerable.Single(new[] { 7 }),() => SeamlessEnumerable.Single(new[] { 7 }));
        AssertSameValue(() => SystemEnumerable.Single(DuplicateNumbers(),static value => value==6),() => SeamlessEnumerable.Single(DuplicateNumbers(),static value => value==6));
    }

    [Fact, Trait("Category","Unit")]
    public void SkipLast() {
        AssertSameSequence(() => SystemEnumerable.SkipLast(DuplicateNumbers(),2),() => SeamlessEnumerable.SkipLast(DuplicateNumbers(),2));
    }

    [Fact, Trait("Category","Unit")]
    public void SkipWhile() {
        AssertSameSequence(() => SystemEnumerable.SkipWhile(DuplicateNumbers(),static value => value<5),() => SeamlessEnumerable.SkipWhile(DuplicateNumbers(),static value => value<5));
        AssertSameSequence(() => SystemEnumerable.SkipWhile(DuplicateNumbers(),static (value,index) => value+index<7),() => SeamlessEnumerable.SkipWhile(DuplicateNumbers(),static (value,index) => value+index<7));
    }

    [Fact, Trait("Category","Unit")]
    public void Skip() {
        AssertSameSequence(() => SystemEnumerable.Skip(DuplicateNumbers(),3),() => SeamlessEnumerable.Skip(DuplicateNumbers(),3));
    }

    [Fact, Trait("Category","Unit")]
    public void Sum() {
        var rows = RowsWithDuplicates();
        AssertNumericSum(rows);
    }

    [Fact, Trait("Category","Unit")]
    public void TakeLast() {
        AssertSameSequence(() => SystemEnumerable.TakeLast(DuplicateNumbers(),3),() => SeamlessEnumerable.TakeLast(DuplicateNumbers(),3));
    }

    [Fact, Trait("Category","Unit")]
    public void TakeWhile() {
        AssertSameSequence(() => SystemEnumerable.TakeWhile(DuplicateNumbers(),static value => value<5),() => SeamlessEnumerable.TakeWhile(DuplicateNumbers(),static value => value<5));
        AssertSameSequence(() => SystemEnumerable.TakeWhile(DuplicateNumbers(),static (value,index) => value+index<8),() => SeamlessEnumerable.TakeWhile(DuplicateNumbers(),static (value,index) => value+index<8));
    }

    [Fact, Trait("Category","Unit")]
    public void Take() {
        AssertSameSequence(() => SystemEnumerable.Take(DuplicateNumbers(),5),() => SeamlessEnumerable.Take(DuplicateNumbers(),5));
        AssertSameSequence(() => SystemEnumerable.Take(DuplicateNumbers(),2..^1),() => SeamlessEnumerable.Take(DuplicateNumbers(),2..^1));
    }

    [Fact, Trait("Category","Unit")]
    public void ThenByDescending() {
        var rows = RowsWithDuplicates();
        AssertSameSequence(() => SystemEnumerable.ThenByDescending(SystemEnumerable.OrderBy(rows,static row => row.Category),static row => row.Score),() => SeamlessEnumerable.ThenByDescending(SeamlessEnumerable.OrderBy(rows,static row => row.Category),static row => row.Score));
        AssertSameSequence(() => SystemEnumerable.ThenByDescending(SystemEnumerable.OrderBy(rows,static row => row.Category),static row => row.Score,Comparer<int>.Default),() => SeamlessEnumerable.ThenByDescending(SeamlessEnumerable.OrderBy(rows,static row => row.Category),static row => row.Score,Comparer<int>.Default));
    }

    [Fact, Trait("Category","Unit")]
    public void ThenBy() {
        var rows = RowsWithDuplicates();
        AssertSameSequence(() => SystemEnumerable.ThenBy(SystemEnumerable.OrderBy(rows,static row => row.Category),static row => row.Score),() => SeamlessEnumerable.ThenBy(SeamlessEnumerable.OrderBy(rows,static row => row.Category),static row => row.Score));
        AssertSameSequence(() => SystemEnumerable.ThenBy(SystemEnumerable.OrderBy(rows,static row => row.Category),static row => row.Score,Comparer<int>.Default),() => SeamlessEnumerable.ThenBy(SeamlessEnumerable.OrderBy(rows,static row => row.Category),static row => row.Score,Comparer<int>.Default));
    }

    [Fact, Trait("Category","Unit")]
    public void ToArray() {
        AssertSameSequence(() => SystemEnumerable.ToArray(DuplicateNumbers()),() => SeamlessEnumerable.ToArray(DuplicateNumbers()));
    }

    [Fact, Trait("Category","Unit")]
    public void ToDictionary() {
        var rows = RowsWithoutDuplicateKeys();
        AssertSameSequence(() => NormalizeDictionary(SystemEnumerable.ToDictionary(new[] { new KeyValuePair<string,int>("a",1),new KeyValuePair<string,int>("b",2) })),() => NormalizeDictionary(SeamlessEnumerable.ToDictionary(new[] { new KeyValuePair<string,int>("a",1),new KeyValuePair<string,int>("b",2) })));
        AssertSameSequence(() => NormalizeDictionary(SystemEnumerable.ToDictionary(new[] { new KeyValuePair<string,int>("a",1),new KeyValuePair<string,int>("B",2) },StringComparer.OrdinalIgnoreCase)),() => NormalizeDictionary(SeamlessEnumerable.ToDictionary(new[] { new KeyValuePair<string,int>("a",1),new KeyValuePair<string,int>("B",2) },StringComparer.OrdinalIgnoreCase)));
        AssertSameSequence(() => NormalizeDictionary(SystemEnumerable.ToDictionary(new[] { ("a",1),("b",2) })),() => NormalizeDictionary(SeamlessEnumerable.ToDictionary(new[] { ("a",1),("b",2) })));
        AssertSameSequence(() => NormalizeDictionary(SystemEnumerable.ToDictionary(new[] { ("a",1),("B",2) },StringComparer.OrdinalIgnoreCase)),() => NormalizeDictionary(SeamlessEnumerable.ToDictionary(new[] { ("a",1),("B",2) },StringComparer.OrdinalIgnoreCase)));
        AssertSameSequence(() => NormalizeDictionary(SystemEnumerable.ToDictionary(rows,static row => row.Id)),() => NormalizeDictionary(SeamlessEnumerable.ToDictionary(rows,static row => row.Id)));
        AssertSameSequence(() => NormalizeDictionary(SystemEnumerable.ToDictionary(rows,static row => row.Category,StringComparer.OrdinalIgnoreCase)),() => NormalizeDictionary(SeamlessEnumerable.ToDictionary(rows,static row => row.Category,StringComparer.OrdinalIgnoreCase)));
        AssertSameSequence(() => NormalizeDictionary(SystemEnumerable.ToDictionary(rows,static row => row.Id,static row => row.Score)),() => NormalizeDictionary(SeamlessEnumerable.ToDictionary(rows,static row => row.Id,static row => row.Score)));
        AssertSameSequence(() => NormalizeDictionary(SystemEnumerable.ToDictionary(rows,static row => row.Category,static row => row.Score,StringComparer.OrdinalIgnoreCase)),() => NormalizeDictionary(SeamlessEnumerable.ToDictionary(rows,static row => row.Category,static row => row.Score,StringComparer.OrdinalIgnoreCase)));
        AssertSameException<ArgumentException>(() => { _=SystemEnumerable.ToDictionary(RowsWithDuplicates(),static row => row.Id); },() => { _=SeamlessEnumerable.ToDictionary(RowsWithDuplicates(),static row => row.Id); });
    }

    [Fact, Trait("Category","Unit")]
    public void ToHashSet() {
        AssertSameSequence(() => SystemEnumerable.Order(SystemEnumerable.ToHashSet(DuplicateNumbers())),() => SystemEnumerable.Order(SeamlessEnumerable.ToHashSet(DuplicateNumbers())));
        AssertSameSequence(() => SystemEnumerable.Order(SystemEnumerable.ToHashSet(DuplicateNumbers(),new ModuloComparer(3))),() =>{
            IEnumerable<int> source=DuplicateNumbers();
            var set=new global::System.Collections.Generic.HashSet<int>(new ModuloComparer(3));
            foreach(var item in source) {
                set.Add(item);
            }
            return SystemEnumerable.Order(set);
        });
    }

    [Fact, Trait("Category","Unit")]
    public void ToList() {
        AssertSameSequence(() => SystemEnumerable.ToList(DuplicateNumbers()),() =>{
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
        AssertSameSequence(() => NormalizeLookup(SystemEnumerable.ToLookup(rows,static row => row.Category)),                                 () => NormalizeLookup(SeamlessEnumerable.ToLookup(rows,static row => row.Category)));
        AssertSameSequence(() => NormalizeLookup(SystemEnumerable.ToLookup(rows,static row => row.Category,StringComparer.OrdinalIgnoreCase)),() =>{
            Func<Row,string> keySelector=static row => row.Category;
            IEqualityComparer<string>? comparer=StringComparer.OrdinalIgnoreCase;
            comparer??=EqualityComparer<string>.Default;
            var lookup=new SeamlessEnumerable.LookupImpl<string,Row>(comparer);
            foreach(var item in rows) {
                lookup.Add(keySelector(item),item);
            }
            return NormalizeLookup(lookup);
        });
        AssertSameSequence(() => NormalizeLookup(SystemEnumerable.ToLookup(rows,static row => row.Category,static row => row.Score)),         () => NormalizeLookup(SeamlessEnumerable.ToLookup(rows,static row => row.Category,static row => row.Score)));
        AssertSameSequence(() => NormalizeLookup(SystemEnumerable.ToLookup(rows,static row => row.Category,static row => row.Score,StringComparer.OrdinalIgnoreCase)),() =>{
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
        AssertSameSequence(() => SystemEnumerable.UnionBy(first,second,static row => row.Category),() => SeamlessEnumerable.UnionBy(first,second,static row => row.Category));
        AssertSameSequence(() => SystemEnumerable.UnionBy(first,second,static row => row.Category,StringComparer.OrdinalIgnoreCase),() => SeamlessEnumerable.UnionBy(first,second,static row => row.Category,StringComparer.OrdinalIgnoreCase));
    }

    [Fact, Trait("Category","Unit")]
    public void Union() {
        AssertSameSequence(() => SystemEnumerable.Union(DuplicateNumbers(),new[] { 1,7,8 }),() => SeamlessEnumerable.Union(DuplicateNumbers(),new[] { 1,7,8 }));
        AssertSameSequence(() => SystemEnumerable.Union(DuplicateNumbers(),new[] { 7,8 },new ModuloComparer(3)),() => SeamlessEnumerable.Union(DuplicateNumbers(),new[] { 7,8 },new ModuloComparer(3)));
    }

    [Fact, Trait("Category","Unit")]
    public void Where() {
        AssertSameSequence(() => SystemEnumerable.Where(DuplicateNumbers(),static value => value%2==0),() => SeamlessEnumerable.Where(DuplicateNumbers(),static value => value%2==0));
        AssertSameSequence(() => SystemEnumerable.Where(DuplicateNumbers(),static (value,index) => (value+index)%2==0),() => SeamlessEnumerable.Where(DuplicateNumbers(),static (value,index) => (value+index)%2==0));
    }

    [Fact, Trait("Category","Unit")]
    public void Zip() {
        AssertSameSequence(() => SystemEnumerable.Zip(new[] { 1,2,3 },new[] { "a","b" }),() => SeamlessEnumerable.Zip(new[] { 1,2,3 },new[] { "a","b" }));
        AssertSameSequence(() => SystemEnumerable.Zip(new[] { 1,2,3 },new[] { "a","b","c" },new[] { 10m,20m }),() => SeamlessEnumerable.Zip(new[] { 1,2,3 },new[] { "a","b","c" },new[] { 10m,20m }));
        AssertSameSequence(() => SystemEnumerable.Zip(new[] { 1,2,3 },new[] { 10,20 },static (left,right) => left+right),() => SeamlessEnumerable.Zip(new[] { 1,2,3 },new[] { 10,20 },static (left,right) => left+right));
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
        AssertSameSequence(() => SystemEnumerable.Where(values,predicate),() => SeamlessEnumerable.Where(values,predicate));
        AssertSameSequence(() => SystemEnumerable.Select(values,selector),() => SeamlessEnumerable.Select(values,selector));
        AssertSameSequence(() => SystemEnumerable.SelectMany(values,value => new[] { value,extra }),() => SeamlessEnumerable.SelectMany(values,value => new[] { value,extra }));
        AssertSameSequence(() => SystemEnumerable.Concat(values,new[] { extra }),() => SeamlessEnumerable.Concat(values,new[] { extra }));
        AssertSameSequence(() => SystemEnumerable.Append(values,extra),() => SeamlessEnumerable.Append(values,extra));
        AssertSameSequence(() => SystemEnumerable.Prepend(values,extra),() => SeamlessEnumerable.Prepend(values,extra));
        AssertSameSequence(() => SystemEnumerable.Distinct(values),() => SeamlessEnumerable.Distinct(values));
        AssertSameSequence(() => SystemEnumerable.Skip(values,1),() => SeamlessEnumerable.Skip(values,1));
        AssertSameSequence(() => SystemEnumerable.Take(values,3),() => SeamlessEnumerable.Take(values,3));
        AssertSameSequence(() => SystemEnumerable.DefaultIfEmpty(Array.Empty<T>(),extra),() => SeamlessEnumerable.DefaultIfEmpty(Array.Empty<T>(),extra));
        AssertSameSequence(() => SystemEnumerable.Zip(values,SystemEnumerable.Repeat(extra,values.Length)),() => SeamlessEnumerable.Zip(values,SeamlessEnumerable.Repeat(extra,values.Length)));
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
        AssertSameValue(() => SystemEnumerable.Max(new[] { 1.5m,2.5m,2.5m }),() => SeamlessEnumerable.Max(new[] { 1.5m,2.5m,2.5m }));
        AssertSameValue(() => SystemEnumerable.Max(new[] { 1.5d,2.5d,2.5d }),() => SeamlessEnumerable.Max(new[] { 1.5d,2.5d,2.5d }));
        AssertSameValue(() => SystemEnumerable.Max(new[] { 1,2,2 }),() => SeamlessEnumerable.Max(new[] { 1,2,2 }));
        AssertSameValue(() => SystemEnumerable.Max(new[] { 1L,2L,2L }),() => SeamlessEnumerable.Max(new[] { 1L,2L,2L }));
        AssertSameValue(() => SystemEnumerable.Max(new[] { 1.5f,2.5f,2.5f }),() => SeamlessEnumerable.Max(new[] { 1.5f,2.5f,2.5f }));
        AssertSameValue(() => SystemEnumerable.Max(new decimal?[] { 1.5m,null,2.5m }),() => SeamlessEnumerable.Max(new decimal?[] { 1.5m,null,2.5m }));
        AssertSameValue(() => SystemEnumerable.Max(new double?[] { 1.5d,null,2.5d }),() => SeamlessEnumerable.Max(new double?[] { 1.5d,null,2.5d }));
        AssertSameValue(() => SystemEnumerable.Max(new int?[] { 1,null,2 }),() => SeamlessEnumerable.Max(new int?[] { 1,null,2 }));
        AssertSameValue(() => SystemEnumerable.Max(new long?[] { 1L,null,2L }),() => SeamlessEnumerable.Max(new long?[] { 1L,null,2L }));
        AssertSameValue(() => SystemEnumerable.Max(new float?[] { 1.5f,null,2.5f }),() => SeamlessEnumerable.Max(new float?[] { 1.5f,null,2.5f }));
        AssertSameValue(() => SystemEnumerable.Max(rows,static row => row.Amount),() => SeamlessEnumerable.Max(rows,static row => row.Amount));
        AssertSameValue(() => SystemEnumerable.Max(rows,static row => (double)row.Score),() => SeamlessEnumerable.Max(rows,static row => (double)row.Score));
        AssertSameValue(() => SystemEnumerable.Max(rows,static row => row.Score),() => SeamlessEnumerable.Max(rows,static row => row.Score));
        AssertSameValue(() => SystemEnumerable.Max(rows,static row => (long)row.Score),() => SeamlessEnumerable.Max(rows,static row => (long)row.Score));
        AssertSameValue(() => SystemEnumerable.Max(rows,static row => (float)row.Score),() => SeamlessEnumerable.Max(rows,static row => (float)row.Score));
        AssertSameValue(() => SystemEnumerable.Max(rows,static row => (decimal?)row.Amount),() => SeamlessEnumerable.Max(rows,static row => (decimal?)row.Amount));
        AssertSameValue(() => SystemEnumerable.Max(rows,static row => (double?)row.Score),() => SeamlessEnumerable.Max(rows,static row => (double?)row.Score));
        AssertSameValue(() => SystemEnumerable.Max(rows,static row => (int?)row.Score),() => SeamlessEnumerable.Max(rows,static row => (int?)row.Score));
        AssertSameValue(() => SystemEnumerable.Max(rows,static row => (long?)row.Score),() => SeamlessEnumerable.Max(rows,static row => (long?)row.Score));
        AssertSameValue(() => SystemEnumerable.Max(rows,static row => (float?)row.Score),() => SeamlessEnumerable.Max(rows,static row => (float?)row.Score));
    }

    private static void AssertNumericMin(Row[] rows) {
        AssertSameValue(() => SystemEnumerable.Min(new[] { 1.5m,2.5m,2.5m }),() => SeamlessEnumerable.Min(new[] { 1.5m,2.5m,2.5m }));
        AssertSameValue(() => SystemEnumerable.Min(new[] { 1.5d,2.5d,2.5d }),() => SeamlessEnumerable.Min(new[] { 1.5d,2.5d,2.5d }));
        AssertSameValue(() => SystemEnumerable.Min(new[] { 1,2,2 }),() => SeamlessEnumerable.Min(new[] { 1,2,2 }));
        AssertSameValue(() => SystemEnumerable.Min(new[] { 1L,2L,2L }),() => SeamlessEnumerable.Min(new[] { 1L,2L,2L }));
        AssertSameValue(() => SystemEnumerable.Min(new[] { 1.5f,2.5f,2.5f }),() => SeamlessEnumerable.Min(new[] { 1.5f,2.5f,2.5f }));
        AssertSameValue(() => SystemEnumerable.Min(new decimal?[] { 1.5m,null,2.5m }),() => SeamlessEnumerable.Min(new decimal?[] { 1.5m,null,2.5m }));
        AssertSameValue(() => SystemEnumerable.Min(new double?[] { 1.5d,null,2.5d }),() => SeamlessEnumerable.Min(new double?[] { 1.5d,null,2.5d }));
        AssertSameValue(() => SystemEnumerable.Min(new int?[] { 1,null,2 }),() => SeamlessEnumerable.Min(new int?[] { 1,null,2 }));
        AssertSameValue(() => SystemEnumerable.Min(new long?[] { 1L,null,2L }),() => SeamlessEnumerable.Min(new long?[] { 1L,null,2L }));
        AssertSameValue(() => SystemEnumerable.Min(new float?[] { 1.5f,null,2.5f }),() => SeamlessEnumerable.Min(new float?[] { 1.5f,null,2.5f }));
        AssertSameValue(() => SystemEnumerable.Min(rows,static row => row.Amount),() => SeamlessEnumerable.Min(rows,static row => row.Amount));
        AssertSameValue(() => SystemEnumerable.Min(rows,static row => (double)row.Score),() => SeamlessEnumerable.Min(rows,static row => (double)row.Score));
        AssertSameValue(() => SystemEnumerable.Min(rows,static row => row.Score),() => SeamlessEnumerable.Min(rows,static row => row.Score));
        AssertSameValue(() => SystemEnumerable.Min(rows,static row => (long)row.Score),() => SeamlessEnumerable.Min(rows,static row => (long)row.Score));
        AssertSameValue(() => SystemEnumerable.Min(rows,static row => (float)row.Score),() => SeamlessEnumerable.Min(rows,static row => (float)row.Score));
        AssertSameValue(() => SystemEnumerable.Min(rows,static row => (decimal?)row.Amount),() => SeamlessEnumerable.Min(rows,static row => (decimal?)row.Amount));
        AssertSameValue(() => SystemEnumerable.Min(rows,static row => (double?)row.Score),() => SeamlessEnumerable.Min(rows,static row => (double?)row.Score));
        AssertSameValue(() => SystemEnumerable.Min(rows,static row => (int?)row.Score),() => SeamlessEnumerable.Min(rows,static row => (int?)row.Score));
        AssertSameValue(() => SystemEnumerable.Min(rows,static row => (long?)row.Score),() => SeamlessEnumerable.Min(rows,static row => (long?)row.Score));
        AssertSameValue(() => SystemEnumerable.Min(rows,static row => (float?)row.Score),() => SeamlessEnumerable.Min(rows,static row => (float?)row.Score));
    }

    private static void AssertNumericSum(Row[] rows) {
        AssertSameValue(() => SystemEnumerable.Sum(new[] { 1.5m,2.5m,2.5m }),() => SeamlessEnumerable.Sum(new[] { 1.5m,2.5m,2.5m }));
        AssertSameValue(() => SystemEnumerable.Sum(new[] { 1.5d,2.5d,2.5d }),() => SeamlessEnumerable.Sum(new[] { 1.5d,2.5d,2.5d }));
        AssertSameValue(() => SystemEnumerable.Sum(new[] { 1,2,2 }),() => SeamlessEnumerable.Sum(new[] { 1,2,2 }));
        AssertSameValue(() => SystemEnumerable.Sum(new[] { 1L,2L,2L }),() => SeamlessEnumerable.Sum(new[] { 1L,2L,2L }));
        AssertSameValue(() => SystemEnumerable.Sum(new[] { 1.5f,2.5f,2.5f }),() => SeamlessEnumerable.Sum(new[] { 1.5f,2.5f,2.5f }));
        AssertSameValue(() => SystemEnumerable.Sum(new decimal?[] { 1.5m,null,2.5m }),() => SeamlessEnumerable.Sum(new decimal?[] { 1.5m,null,2.5m }));
        AssertSameValue(() => SystemEnumerable.Sum(new double?[] { 1.5d,null,2.5d }),() => SeamlessEnumerable.Sum(new double?[] { 1.5d,null,2.5d }));
        AssertSameValue(() => SystemEnumerable.Sum(new int?[] { 1,null,2 }),() => SeamlessEnumerable.Sum(new int?[] { 1,null,2 }));
        AssertSameValue(() => SystemEnumerable.Sum(new long?[] { 1L,null,2L }),() => SeamlessEnumerable.Sum(new long?[] { 1L,null,2L }));
        AssertSameValue(() => SystemEnumerable.Sum(new float?[] { 1.5f,null,2.5f }),() => SeamlessEnumerable.Sum(new float?[] { 1.5f,null,2.5f }));
        AssertSameValue(() => SystemEnumerable.Sum(rows,static row => row.Amount),() => SeamlessEnumerable.Sum(rows,static row => row.Amount));
        AssertSameValue(() => SystemEnumerable.Sum(rows,static row => (double)row.Score),() => SeamlessEnumerable.Sum(rows,static row => (double)row.Score));
        AssertSameValue(() => SystemEnumerable.Sum(rows,static row => row.Score),() => SeamlessEnumerable.Sum(rows,static row => row.Score));
        AssertSameValue(() => SystemEnumerable.Sum(rows,static row => (long)row.Score),() => SeamlessEnumerable.Sum(rows,static row => (long)row.Score));
        AssertSameValue(() => SystemEnumerable.Sum(rows,static row => (float)row.Score),() => SeamlessEnumerable.Sum(rows,static row => (float)row.Score));
        AssertSameValue(() => SystemEnumerable.Sum(rows,static row => (decimal?)row.Amount),() => SeamlessEnumerable.Sum(rows,static row => (decimal?)row.Amount));
        AssertSameValue(() => SystemEnumerable.Sum(rows,static row => (double?)row.Score),() => SeamlessEnumerable.Sum(rows,static row => (double?)row.Score));
        AssertSameValue(() => SystemEnumerable.Sum(rows,static row => (int?)row.Score),() => SeamlessEnumerable.Sum(rows,static row => (int?)row.Score));
        AssertSameValue(() => SystemEnumerable.Sum(rows,static row => (long?)row.Score),() => SeamlessEnumerable.Sum(rows,static row => (long?)row.Score));
        AssertSameValue(() => SystemEnumerable.Sum(rows,static row => (float?)row.Score),() => SeamlessEnumerable.Sum(rows,static row => (float?)row.Score));
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
        var expectedResult = SystemEnumerable.TryGetNonEnumeratedCount(expectedSource,out var expectedCount);
        var actualResult = SeamlessEnumerable.TryGetNonEnumeratedCount(actualSource,out var actualCount);
        Assert.Equal(expectedResult,actualResult);
        Assert.Equal(expectedCount,actualCount);
    }

    private static IEnumerable<string> ChunkText<T>(IEnumerable<T[]> chunks) => SystemEnumerable.Select(chunks,static chunk => string.Join(",",chunk));

    private static IEnumerable<string> NormalizeGroups<TKey, TElement>(IEnumerable<IGrouping<TKey,TElement>> groups) =>
        SystemEnumerable.Select(groups,static group => $"{group.Key}:{string.Join("|",group)}");

    private static IEnumerable<string> NormalizeLookup<TKey, TElement>(ILookup<TKey,TElement> lookup) =>
        SystemEnumerable.Select(lookup,static group => $"{group.Key}:{string.Join("|",group)}");

    private static IEnumerable<string> NormalizeDictionary<TKey, TValue>(Dictionary<TKey,TValue> dictionary)
        where TKey : notnull =>
        SystemEnumerable.Select(SystemEnumerable.OrderBy(dictionary,static pair => pair.Key.ToString()),static pair => $"{pair.Key}:{pair.Value}");

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
