using Seamless.FLinq;

using System.Collections;
using System.Globalization;

using ZLinq;

using SystemEnumerable = System.Linq.Enumerable;

namespace DynamicLinq;

public sealed record EnumerableComparisonRow(int Key,int Value,string Name);

public static class EnumerableComparisonCatalog {
    private static readonly int[] source = [1,2,3,4];
    private static readonly int[] sourceWithDuplicates = [1,2,2,3,3,4];
    private static readonly int[] second = [3,4,5];
    private static readonly object?[] mixedObjects = [1,"hello",2,null,3.0,4];
    private static readonly object[] castObjects = [1,2,3];
    private static readonly EnumerableComparisonRow[] rows = [new(1,10,"beta"),new(2,20,"alpha"),new(3,30,"gamma"),new(1,40,"delta")];
    private static readonly EnumerableComparisonRow[] otherRows = [new(1,100,"one"),new(2,200,"two"),new(4,400,"four")];
    private static readonly string[] benchmarkMethodNames =
    [
        "Aggregate",
        "AggregateBy",
        "All",
        "Any",
        "Append",
        "Average",
        "Cast",
        "Chunk",
        "Concat",
        "Contains",
        "Count",
        "CountBy",
        "DefaultIfEmpty",
        "Distinct",
        "DistinctBy",
        "ElementAt",
        "ElementAtOrDefault",
        "Empty",
        "Except",
        "ExceptBy",
        "First",
        "FirstOrDefault",
        "GroupBy",
        "GroupJoin",
        "Index",
        "Intersect",
        "IntersectBy",
        "Join",
        "Last",
        "LastOrDefault",
        "LongCount",
        "Max",
        "MaxBy",
        "Min",
        "MinBy",
        "OfType",
        "Order",
        "OrderBy",
        "OrderByDescending",
        "OrderDescending",
        "Prepend",
        "Range",
        "Repeat",
        "Reverse",
        "Select",
        "SelectMany",
        "SequenceEqual",
        "Single",
        "SingleOrDefault",
        "Skip",
        "SkipLast",
        "SkipWhile",
        "Sum",
        "Take",
        "TakeLast",
        "TakeWhile",
        "ThenBy",
        "ThenByDescending",
        "ToArray",
        "ToDictionary",
        "ToHashSet",
        "ToList",
        "ToLookup",
        "Union",
        "UnionBy",
        "Where",
        "Zip",
    ];
    private static readonly global::System.Collections.Generic.HashSet<string> chain1ExcludedMethodNames =
    [
        "AsEnumerable",
        "AsQueryable",
        "InfiniteSequence",
        "LeftJoin",
        "RightJoin",
        "Sequence",
        "Shuffle",
        "TryGetNonEnumeratedCount",
    ];

    public static IReadOnlyList<string> BenchmarkMethodNames => benchmarkMethodNames;
    public static IReadOnlySet<string> Chain1ExcludedMethodNames => chain1ExcludedMethodNames;

    public static object? RunSystem(string methodName) {
        return methodName switch {
            "Aggregate" => source.Aggregate(static (left,right) => left+right),
            "AggregateBy" => source.AggregateBy(static value => value%2,0,static (total,value) => total+value).ToArray(),
            "All" => source.All(static value => value>0),
            "Any" => source.Any(static value => value>3),
            "Append" => source.Append(5).ToArray(),
            "Average" => source.Average(),
            "Cast" => castObjects.Cast<int>().ToArray(),
            "Chunk" => source.Chunk(2),
            "Concat" => source.Concat(second).ToArray(),
            "Contains" => source.Contains(3),
            "Count" => source.Count(static value => value>2),
            "CountBy" => sourceWithDuplicates.CountBy(static value => value).ToArray(),
            "DefaultIfEmpty" => Array.Empty<int>().DefaultIfEmpty(42).ToArray(),
            "Distinct" => sourceWithDuplicates.Distinct().ToArray(),
            "DistinctBy" => rows.DistinctBy(static row => row.Key).Select(static row => row.Name).ToArray(),
            "ElementAt" => source.ElementAt(2),
            "ElementAtOrDefault" => source.ElementAtOrDefault(10),
            "Empty" => SystemEnumerable.Empty<int>().ToArray(),
            "Except" => source.Except(second).ToArray(),
            "ExceptBy" => rows.ExceptBy(second,static row => row.Key).Select(static row => row.Name).ToArray(),
            "First" => source.First(static value => value>2),
            "FirstOrDefault" => source.FirstOrDefault(static value => value>10),
            "GroupBy" => ProjectGroups(rows.GroupBy(static row => row.Key)),
            "GroupJoin" => source.GroupJoin(second,static outerValue => outerValue,static innerValue => innerValue,static (outerValue,group) => outerValue+group.Sum()).ToArray(),
            "Index" => source.Index().ToArray(),
            "Intersect" => source.Intersect(second).ToArray(),
            "IntersectBy" => rows.IntersectBy(second,static row => row.Key).Select(static row => row.Name).ToArray(),
            "Join" => rows.Join(otherRows,static row => row.Key,static other => other.Key,static (row,other) => row.Name+":"+other.Name).ToArray(),
            "Last" => source.Last(static value => value>2),
            "LastOrDefault" => source.LastOrDefault(static value => value>10),
            "LongCount" => source.LongCount(static value => value>1),
            "Max" => source.Max(),
            "MaxBy" => rows.MaxBy(static row => row.Value)?.Name,
            "Min" => source.Min(),
            "MinBy" => rows.MinBy(static row => row.Value)?.Name,
            "OfType" => mixedObjects.OfType<int>().ToArray(),
            "Order" => sourceWithDuplicates.Order().ToArray(),
            "OrderBy" => rows.OrderBy(static row => row.Name).Select(static row => row.Name).ToArray(),
            "OrderByDescending" => rows.OrderByDescending(static row => row.Name).Select(static row => row.Name).ToArray(),
            "OrderDescending" => sourceWithDuplicates.OrderDescending().ToArray(),
            "Prepend" => source.Prepend(0).ToArray(),
            "Range" => SystemEnumerable.Range(3,4).ToArray(),
            "Repeat" => SystemEnumerable.Repeat(7,3).ToArray(),
            "Reverse" => source.Reverse().ToArray(),
            "Select" => source.Select(static value => value*2).ToArray(),
            "SelectMany" => source.SelectMany(static outerValue => second,static (outerValue,innerValue) => outerValue+innerValue).ToArray(),
            "SequenceEqual" => source.SequenceEqual([1,2,3,4]),
            "Single" => source.Single(static value => value==3),
            "SingleOrDefault" => source.SingleOrDefault(static value => value==10),
            "Skip" => source.Skip(2).ToArray(),
            "SkipLast" => source.SkipLast(2).ToArray(),
            "SkipWhile" => source.SkipWhile(static value => value<3).ToArray(),
            "Sum" => source.Sum(),
            "Take" => source.Take(2).ToArray(),
            "TakeLast" => source.TakeLast(2).ToArray(),
            "TakeWhile" => source.TakeWhile(static value => value<3).ToArray(),
            "ThenBy" => rows.OrderBy(static row => row.Key).ThenBy(static row => row.Name).Select(static row => row.Name).ToArray(),
            "ThenByDescending" => rows.OrderBy(static row => row.Key).ThenByDescending(static row => row.Name).Select(static row => row.Name).ToArray(),
            "ToArray" => source.ToArray(),
            "ToDictionary" => ProjectDictionary(rows.Take(3).ToDictionary(static row => row.Key,static row => row.Name)),
            "ToHashSet" => sourceWithDuplicates.ToHashSet().Order().ToArray(),
            "ToList" => source.ToList(),
            "ToLookup" => ProjectLookup(rows.ToLookup(static row => row.Key,static row => row.Name)),
            "Union" => source.Union(second).ToArray(),
            "UnionBy" => rows.UnionBy(otherRows,static row => row.Key).Select(static row => row.Name).ToArray(),
            "Where" => source.Where(static value => value%2==0).ToArray(),
            "Zip" => source.Zip(second,static (left,right) => left+right).ToArray(),
            _ => throw CreateNotSupported(methodName),
        };
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    public static object? RunFLinq(string methodName) {
        return methodName switch {
            "Aggregate" => source.Aggregate(static (left,right) => left+right),
            "AggregateBy" => source.AggregateBy(static value => value%2,0,static (total,value) => total+value).ToArray(),
            "All" => source.All(static value => value>0),
            "Any" => source.Any(static value => value>3),
            "Append" => source.Append(5).ToArray(),
            "Average" => source.Average(),
            "Cast" => castObjects.Cast<int>().ToArray(),
            "Chunk" => source.Chunk(2),
            "Concat" => source.Concat(second).ToArray(),
            "Contains" => source.Contains(3),
            "Count" => source.Count(static value => value>2),
            "CountBy" => sourceWithDuplicates.CountBy(static value => value).ToArray(),
            "DefaultIfEmpty" => Array.Empty<int>().DefaultIfEmpty(42).ToArray(),
            "Distinct" => sourceWithDuplicates.Distinct().ToArray(),
            "DistinctBy" => rows.DistinctBy(static row => row.Key).Select(static row => row.Name).ToArray(),
            "ElementAt" => source.ElementAt(2),
            "ElementAtOrDefault" => source.ElementAtOrDefault(10),
            "Empty" => Enumerable.Empty<int>().ToArray(),
            "Except" => source.Except(second).ToArray(),
            "ExceptBy" => rows.ExceptBy(second,static row => row.Key).Select(static row => row.Name).ToArray(),
            "First" => source.First(static value => value>2),
            "FirstOrDefault" => source.FirstOrDefault(static value => value>10),
            "GroupBy" => ProjectGroups(rows.GroupBy(static row => row.Key)),
            "GroupJoin" => source.GroupJoin(second,static outerValue => outerValue,static innerValue => innerValue,static (outerValue,group) => outerValue+group.Sum()).ToArray(),
            "Index" => source.Index().ToArray(),
            "Intersect" => source.Intersect(second).ToArray(),
            "IntersectBy" => rows.IntersectBy(second,static row => row.Key).Select(static row => row.Name).ToArray(),
            "Join" => rows.Join(otherRows,static row => row.Key,static other => other.Key,static (row,other) => row.Name+":"+other.Name).ToArray(),
            "Last" => source.Last(static value => value>2),
            "LastOrDefault" => source.LastOrDefault(static value => value>10),
            "LongCount" => source.LongCount(static value => value>1),
            "Max" => source.Max(),
            "MaxBy" => rows.MaxBy(static row => row.Value)?.Name,
            "Min" => source.Min(),
            "MinBy" => rows.MinBy(static row => row.Value)?.Name,
            "OfType" => mixedObjects.OfType<int>().ToArray(),
            "Order" => sourceWithDuplicates.Order().ToArray(),
            "OrderBy" => rows.OrderBy(static row => row.Name).Select(static row => row.Name).ToArray(),
            "OrderByDescending" => rows.OrderByDescending(static row => row.Name).Select(static row => row.Name).ToArray(),
            "OrderDescending" => sourceWithDuplicates.OrderDescending().ToArray(),
            "Prepend" => source.Prepend(0).ToArray(),
            "Range" => Enumerable.Range(3,4).ToArray(),
            "Repeat" => Enumerable.Repeat(7,3).ToArray(),
            "Reverse" => source.Reverse().ToArray(),
            "Select" => source.Select(static value => value*2).ToArray(),
            "SelectMany" => source.SelectMany(static outerValue => second,static (outerValue,innerValue) => outerValue+innerValue).ToArray(),
            "SequenceEqual" => source.SequenceEqual([1,2,3,4]),
            "Single" => source.Single(static value => value==3),
            "SingleOrDefault" => source.SingleOrDefault(static value => value==10),
            "Skip" => source.Skip(2).ToArray(),
            "SkipLast" => source.SkipLast(2).ToArray(),
            "SkipWhile" => source.SkipWhile(static value => value<3).ToArray(),
            "Sum" => source.Sum(),
            "Take" => source.Take(2).ToArray(),
            "TakeLast" => source.TakeLast(2).ToArray(),
            "TakeWhile" => source.TakeWhile(static value => value<3).ToArray(),
            "ThenBy" => rows.OrderBy(static row => row.Key).ThenBy(static row => row.Name).Select(static row => row.Name).ToArray(),
            "ThenByDescending" => rows.OrderBy(static row => row.Key).ThenByDescending(static row => row.Name).Select(static row => row.Name).ToArray(),
            "ToArray" => source.ToArray(),
            "ToDictionary" => ProjectDictionary(rows.Take(3).ToDictionary(static row => row.Key,static row => row.Name)),
            "ToHashSet" => sourceWithDuplicates.ToHashSet().Order().ToArray(),
            "ToList" => source.ToList(),
            "ToLookup" => ProjectLookup(rows.ToLookup(static row => row.Key,static row => row.Name)),
            "Union" => source.Union(second).ToArray(),
            "UnionBy" => rows.UnionBy(otherRows,static row => row.Key).Select(static row => row.Name).ToArray(),
            "Where" => source.Where(static value => value%2==0).ToArray(),
            "Zip" => source.Zip(second,static (left,right) => left+right).ToArray(),
            _ => throw CreateNotSupported(methodName),
        };
    }

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    public static object? RunFLinqNoOptimized(string methodName) {
        return methodName switch {
            "Aggregate" => source.Aggregate(static (left,right) => left+right),
            "AggregateBy" => source.AggregateBy(static value => value%2,0,static (total,value) => total+value).ToArray(),
            "All" => source.All(static value => value>0),
            "Any" => source.Any(static value => value>3),
            "Append" => source.Append(5).ToArray(),
            "Average" => source.Average(),
            "Cast" => castObjects.Cast<int>().ToArray(),
            "Chunk" => source.Chunk(2),
            "Concat" => source.Concat(second).ToArray(),
            "Contains" => source.Contains(3),
            "Count" => source.Count(static value => value>2),
            "CountBy" => sourceWithDuplicates.CountBy(static value => value).ToArray(),
            "DefaultIfEmpty" => Array.Empty<int>().DefaultIfEmpty(42).ToArray(),
            "Distinct" => sourceWithDuplicates.Distinct().ToArray(),
            "DistinctBy" => rows.DistinctBy(static row => row.Key).Select(static row => row.Name).ToArray(),
            "ElementAt" => source.ElementAt(2),
            "ElementAtOrDefault" => source.ElementAtOrDefault(10),
            "Empty" => Enumerable.Empty<int>().ToArray(),
            "Except" => source.Except(second).ToArray(),
            "ExceptBy" => rows.ExceptBy(second,static row => row.Key).Select(static row => row.Name).ToArray(),
            "First" => source.First(static value => value>2),
            "FirstOrDefault" => source.FirstOrDefault(static value => value>10),
            "GroupBy" => ProjectGroups(rows.GroupBy(static row => row.Key)),
            "GroupJoin" => source.GroupJoin(second,static outerValue => outerValue,static innerValue => innerValue,static (outerValue,group) => outerValue+group.Sum()).ToArray(),
            "Index" => source.Index().ToArray(),
            "Intersect" => source.Intersect(second).ToArray(),
            "IntersectBy" => rows.IntersectBy(second,static row => row.Key).Select(static row => row.Name).ToArray(),
            "Join" => rows.Join(otherRows,static row => row.Key,static other => other.Key,static (row,other) => row.Name+":"+other.Name).ToArray(),
            "Last" => source.Last(static value => value>2),
            "LastOrDefault" => source.LastOrDefault(static value => value>10),
            "LongCount" => source.LongCount(static value => value>1),
            "Max" => source.Max(),
            "MaxBy" => rows.MaxBy(static row => row.Value)?.Name,
            "Min" => source.Min(),
            "MinBy" => rows.MinBy(static row => row.Value)?.Name,
            "OfType" => mixedObjects.OfType<int>().ToArray(),
            "Order" => sourceWithDuplicates.Order().ToArray(),
            "OrderBy" => rows.OrderBy(static row => row.Name).Select(static row => row.Name).ToArray(),
            "OrderByDescending" => rows.OrderByDescending(static row => row.Name).Select(static row => row.Name).ToArray(),
            "OrderDescending" => sourceWithDuplicates.OrderDescending().ToArray(),
            "Prepend" => source.Prepend(0).ToArray(),
            "Range" => Enumerable.Range(3,4).ToArray(),
            "Repeat" => Enumerable.Repeat(7,3).ToArray(),
            "Reverse" => source.Reverse().ToArray(),
            "Select" => source.Select(static value => value*2).ToArray(),
            "SelectMany" => source.SelectMany(static outerValue => second,static (outerValue,innerValue) => outerValue+innerValue).ToArray(),
            "SequenceEqual" => source.SequenceEqual([1,2,3,4]),
            "Single" => source.Single(static value => value==3),
            "SingleOrDefault" => source.SingleOrDefault(static value => value==10),
            "Skip" => source.Skip(2).ToArray(),
            "SkipLast" => source.SkipLast(2).ToArray(),
            "SkipWhile" => source.SkipWhile(static value => value<3).ToArray(),
            "Sum" => source.Sum(),
            "Take" => source.Take(2).ToArray(),
            "TakeLast" => source.TakeLast(2).ToArray(),
            "TakeWhile" => source.TakeWhile(static value => value<3).ToArray(),
            "ThenBy" => rows.OrderBy(static row => row.Key).ThenBy(static row => row.Name).Select(static row => row.Name).ToArray(),
            "ThenByDescending" => rows.OrderBy(static row => row.Key).ThenByDescending(static row => row.Name).Select(static row => row.Name).ToArray(),
            "ToArray" => source.ToArray(),
            "ToDictionary" => ProjectDictionary(rows.Take(3).ToDictionary(static row => row.Key,static row => row.Name)),
            "ToHashSet" => sourceWithDuplicates.ToHashSet().Order().ToArray(),
            "ToList" => source.ToList(),
            "ToLookup" => ProjectLookup(rows.ToLookup(static row => row.Key,static row => row.Name)),
            "Union" => source.Union(second).ToArray(),
            "UnionBy" => rows.UnionBy(otherRows,static row => row.Key).Select(static row => row.Name).ToArray(),
            "Where" => source.Where(static value => value%2==0).ToArray(),
            "Zip" => source.Zip(second,static (left,right) => left+right).ToArray(),
            _ => throw CreateNotSupported(methodName),
        };
    }

    public static object? RunZLinq(string methodName) {
        return methodName switch {
            "Aggregate" => source.AsValueEnumerable().Aggregate(static (left,right) => left+right),
            "AggregateBy" => source.AsValueEnumerable().AggregateBy(static value => value%2,0,static (total,value) => total+value).ToArray(),
            "All" => source.AsValueEnumerable().All(static value => value>0),
            "Any" => source.AsValueEnumerable().Any(static value => value>3),
            "Append" => source.AsValueEnumerable().Append(5).ToArray(),
            "Average" => source.AsValueEnumerable().Average(),
            "Cast" => castObjects.AsValueEnumerable().Cast<int>().ToArray(),
            "Chunk" => source.AsValueEnumerable().Chunk(2).ToArray(),
            "Concat" => source.AsValueEnumerable().Concat(second).ToArray(),
            "Contains" => source.AsValueEnumerable().Contains(3),
            "Count" => source.AsValueEnumerable().Count(static value => value>2),
            "CountBy" => sourceWithDuplicates.AsValueEnumerable().CountBy(static value => value).ToArray(),
            "DefaultIfEmpty" => Array.Empty<int>().AsValueEnumerable().DefaultIfEmpty(42).ToArray(),
            "Distinct" => sourceWithDuplicates.AsValueEnumerable().Distinct().ToArray(),
            "DistinctBy" => rows.AsValueEnumerable().DistinctBy(static row => row.Key).Select(static row => row.Name).ToArray(),
            "ElementAt" => source.AsValueEnumerable().ElementAt(2),
            "ElementAtOrDefault" => source.AsValueEnumerable().ElementAtOrDefault(10),
            "Empty" => ValueEnumerable.Empty<int>().ToArray(),
            "Except" => source.AsValueEnumerable().Except(second).ToArray(),
            "ExceptBy" => rows.AsValueEnumerable().ExceptBy(second,static row => row.Key).Select(static row => row.Name).ToArray(),
            "First" => source.AsValueEnumerable().First(static value => value>2),
            "FirstOrDefault" => source.AsValueEnumerable().FirstOrDefault(static value => value>10),
            "GroupBy" => ProjectGroups(rows.AsValueEnumerable().GroupBy(static row => row.Key).ToArray()),
            "GroupJoin" => source.AsValueEnumerable().GroupJoin(second,static outerValue => outerValue,static innerValue => innerValue,static (outerValue,group) => outerValue+group.Sum()).ToArray(),
            "Index" => source.AsValueEnumerable().Index().ToArray(),
            "Intersect" => source.AsValueEnumerable().Intersect(second).ToArray(),
            "IntersectBy" => rows.AsValueEnumerable().IntersectBy(second,static row => row.Key).Select(static row => row.Name).ToArray(),
            "Join" => rows.AsValueEnumerable().Join(otherRows,static row => row.Key,static other => other.Key,static (row,other) => row.Name+":"+other.Name).ToArray(),
            "Last" => source.AsValueEnumerable().Last(static value => value>2),
            "LastOrDefault" => source.AsValueEnumerable().LastOrDefault(static value => value>10),
            "LongCount" => source.AsValueEnumerable().LongCount(static value => value>1),
            "Max" => source.AsValueEnumerable().Max(),
            "MaxBy" => rows.AsValueEnumerable().MaxBy(static row => row.Value)!.Name,
            "Min" => source.AsValueEnumerable().Min(),
            "MinBy" => rows.AsValueEnumerable().MinBy(static row => row.Value)!.Name,
            "OfType" => mixedObjects.AsValueEnumerable().OfType<int>().ToArray(),
            "Order" => sourceWithDuplicates.AsValueEnumerable().Order().ToArray(),
            "OrderBy" => rows.AsValueEnumerable().OrderBy(static row => row.Name).Select(static row => row.Name).ToArray(),
            "OrderByDescending" => rows.AsValueEnumerable().OrderByDescending(static row => row.Name).Select(static row => row.Name).ToArray(),
            "OrderDescending" => sourceWithDuplicates.AsValueEnumerable().OrderDescending().ToArray(),
            "Prepend" => source.AsValueEnumerable().Prepend(0).ToArray(),
            "Range" => ValueEnumerable.Range(3,4).ToArray(),
            "Repeat" => ValueEnumerable.Repeat(7,3).ToArray(),
            "Reverse" => source.AsValueEnumerable().Reverse().ToArray(),
            "Select" => source.AsValueEnumerable().Select(static value => value*2).ToArray(),
            "SelectMany" => source.AsValueEnumerable().SelectMany(static outerValue => second,static (outerValue,innerValue) => outerValue+innerValue).ToArray(),
            "SequenceEqual" => source.AsValueEnumerable().SequenceEqual([1,2,3,4]),
            "Single" => source.AsValueEnumerable().Single(static value => value==3),
            "SingleOrDefault" => source.AsValueEnumerable().SingleOrDefault(static value => value==10),
            "Skip" => source.AsValueEnumerable().Skip(2).ToArray(),
            "SkipLast" => source.AsValueEnumerable().SkipLast(2).ToArray(),
            "SkipWhile" => source.AsValueEnumerable().SkipWhile(static value => value<3).ToArray(),
            "Sum" => source.AsValueEnumerable().Sum(),
            "Take" => source.AsValueEnumerable().Take(2).ToArray(),
            "TakeLast" => source.AsValueEnumerable().TakeLast(2).ToArray(),
            "TakeWhile" => source.AsValueEnumerable().TakeWhile(static value => value<3).ToArray(),
            "ThenBy" => rows.AsValueEnumerable().OrderBy(static row => row.Key).ThenBy(static row => row.Name).Select(static row => row.Name).ToArray(),
            "ThenByDescending" => rows.AsValueEnumerable().OrderBy(static row => row.Key).ThenByDescending(static row => row.Name).Select(static row => row.Name).ToArray(),
            "ToArray" => source.AsValueEnumerable().ToArray(),
            "ToDictionary" => ProjectDictionary(rows.AsValueEnumerable().Take(3).ToDictionary(static row => row.Key,static row => row.Name)),
            "ToHashSet" => sourceWithDuplicates.AsValueEnumerable().ToHashSet().Order().ToArray(),
            "ToList" => source.AsValueEnumerable().ToList(),
            "ToLookup" => ProjectLookup(rows.AsValueEnumerable().ToLookup(static row => row.Key,static row => row.Name)),
            "Union" => source.AsValueEnumerable().Union(second).ToArray(),
            "UnionBy" => rows.AsValueEnumerable().UnionBy(otherRows,static row => row.Key).Select(static row => row.Name).ToArray(),
            "Where" => source.AsValueEnumerable().Where(static value => value%2==0).ToArray(),
            "Zip" => source.AsValueEnumerable().Zip(second,static (left,right) => left+right).ToArray(),
            _ => throw CreateNotSupported(methodName),
        };
    }

    public static string Normalize(object? value) {
        return value switch {
            null => "<null>",
            string s => s,
            IDictionary dictionary => NormalizeDictionary(dictionary),
            IEnumerable enumerable when value is not string => NormalizeEnumerable(enumerable),
            IFormattable formattable => formattable.ToString(null,CultureInfo.InvariantCulture),
            _ => value.ToString()??string.Empty,
        };
    }

    private static string[][] ProjectGroups(IEnumerable<IGrouping<int,EnumerableComparisonRow>> groups) {
        return groups.Select(static group => group.Select(static row => $"{row.Key}:{row.Name}:{row.Value}").ToArray()).ToArray();
    }

    private static string[][] ProjectLookup(ILookup<int,string> lookup) {
        return lookup.Select(static group => ProjectLookupGroup(group)).ToArray();
    }

    private static KeyValuePair<int,string>[] ProjectDictionary(Dictionary<int,string> dictionary) {
        return dictionary.OrderBy(static pair => pair.Key).ToArray();
    }

    private static string[] ProjectLookupGroup(IGrouping<int,string> group) {
        return group.Select(value => $"{group.Key}:{value}").ToArray();
    }

    private static string NormalizeDictionary(IDictionary dictionary) {
        return "["+string.Join(",",dictionary.Cast<DictionaryEntry>().Select(static entry => $"{Normalize(entry.Key)}={Normalize(entry.Value)}").Order())+"]";
    }

    private static string NormalizeEnumerable(IEnumerable enumerable) {
        var values = new List<string>();
        foreach(var item in enumerable) {
            if(item is IEnumerable nested&&item is not string&&TryGetKey(item,out var key)) {
                values.Add("{"+Normalize(key)+":"+NormalizeEnumerable(nested)+"}");
                continue;
            }

            values.Add(Normalize(item));
        }
        return "["+string.Join(",",values)+"]";
    }

    private static bool TryGetKey(object instance,out object? key) {
        var property = instance.GetType().GetProperty("Key");
        if(property is null) {
            key=null;
            return false;
        }

        key=property.GetValue(instance);
        return true;
    }

    private static Exception CreateNotSupported(string methodName) {
        return new ArgumentOutOfRangeException(nameof(methodName),methodName,"The method is not registered in EnumerableComparisonCatalog.");
    }
}
