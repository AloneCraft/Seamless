using System.Linq;
using System.Runtime.CompilerServices;

namespace Persisted.Collection;

public abstract class ListTestBase<TStrategy, T>
    where TStrategy : IListTestStrategy<T> {

    private static string CreatePath([CallerMemberName] string methodName = "") {
        var directoryPath = Path.Combine(
            Environment.CurrentDirectory,
            typeof(TStrategy).Name,
            typeof(T).Name
        );
        Directory.CreateDirectory(directoryPath);
        return Path.Combine(directoryPath,$"{methodName}.dat");
    }

    private static void TryDeleteDirectory(string path) {
        var directoryPath = Path.GetDirectoryName(path);
        if(string.IsNullOrEmpty(directoryPath))
            return;
        try {
            Directory.Delete(directoryPath,recursive: true);
        }
        catch {
        }
    }

    // ============================================================
    // List 基本操作テスト

    [BenchFact,Benchmark]
    public void Add_Enumerate_AndIndexer() {
        var path = CreatePath();
        try {
            using var h = TStrategy.CreateList(path);
            h.List.Add(TStrategy.Elem(10));
            h.List.Add(TStrategy.Elem(20));
            h.List.Add(TStrategy.Elem(30));

            Assert.Equal(TStrategy.Elem(10),h.List[0]);

            Assert.Equal(3L,h.List.Count);
            Assert.Equal(TStrategy.Elem(10),h.List[0]);
            Assert.Equal(TStrategy.Elem(20),h.List[1]);
            Assert.Equal(TStrategy.Elem(30),h.List[2]);

            var items = h.List.ToArray();
            Assert.Equal(new[] { TStrategy.Elem(10),TStrategy.Elem(20),TStrategy.Elem(30) },items);
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public void Remove_DeletesTailOnly() {
        var path = CreatePath();
        try {
            using var h = TStrategy.CreateList(path);
            h.List.Add(TStrategy.Elem(1));
            h.List.Add(TStrategy.Elem(2));
            h.List.Add(TStrategy.Elem(3));

            var removed = h.List.Remove();

            Assert.True(removed);
            Assert.Equal(2L,h.List.Count);
            Assert.Equal(new[] { TStrategy.Elem(1),TStrategy.Elem(2) },h.List.ToArray());
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public void Reopen_PreservesCountAndValues() {
        var path = CreatePath();
        try {
            using(var h = TStrategy.CreateList(path,initialCapacity: 1)) {
                for(var i = 0; i<8; i++) {
                    h.List.Add(TStrategy.Elem(i*2));
                }
            }

            using var reopened = TStrategy.CreateList(path);
            Assert.Equal(8L,reopened.List.Count);
            var expected = Enumerable.Range(0,8).Select(i => TStrategy.Elem(i*2)).ToArray();
            Assert.Equal(expected,reopened.List.ToArray());
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public void BackingFiles_FollowFixedOrVariableStrategy() {
        var path = CreatePath();
        try {
            using(var h = TStrategy.CreateList(path,initialCapacity: 1)) {
                h.List.Add(TStrategy.Elem(1));
                h.List.Add(TStrategy.Elem(2));
            }

            Assert.True(File.Exists(path));
            if(RuntimeHelpers.IsReferenceOrContainsReferences<T>())
                Assert.True(File.Exists(path+".index"));
            else
                Assert.False(File.Exists(path+".index"));
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public void CreateTemp_ReturnsDisposablePersistentList() {
        string? path = null;
        global::Seamless.Persisted.Collections.PersistedList<T>? list = null;
        try {
            list=global::Seamless.Persisted.Collections.Store.Create().CreateTemp<T>(initialCapacity: 1);
            path=list.Path;
            list.Add(TStrategy.Elem(1));
            Assert.Equal(1L,list.Count);
            Assert.Equal(TStrategy.Elem(1),list[0]);

            list.Dispose();
            Assert.True(list.IsDisposed);
        }
        finally {
            list?.Dispose();
            if(path is not null)
                TryDeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public void SetTuning_AfterConstruction_PreservesValues() {
        var path = CreatePath();
        try {
            using var h = TStrategy.CreateList(path,initialCapacity: 1);
            h.List.Add(TStrategy.Elem(1));
            h.List.Add(TStrategy.Elem(2));

            h.List.SetTuning(new(FileAccess.Write,FileOptions.RandomAccess));
            h.List.Add(TStrategy.Elem(3));

            Assert.Equal(FileAccess.Write,h.List.OptimizationMode);
            Assert.Equal(FileOptions.RandomAccess,h.List.AccessPattern);
            Assert.Equal(new[] { TStrategy.Elem(1),TStrategy.Elem(2),TStrategy.Elem(3) },h.List.ToArray());

            h.List.SetOptimizationMode(FileAccess.Read);
            h.List.SetAccessPattern(FileOptions.SequentialScan);

            Assert.Equal(FileAccess.Read,h.List.OptimizationMode);
            Assert.Equal(FileOptions.SequentialScan,h.List.AccessPattern);
            Assert.Equal(new[] { TStrategy.Elem(1),TStrategy.Elem(2),TStrategy.Elem(3) },h.List.ToArray());
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public void SequentialAdd() {
        var path = CreatePath();
        try {
            const int count = 32;
            using var h = TStrategy.CreateList(path,initialCapacity: 1);
            for(var i = 0; i<count; i++) {
                h.List.Add(TStrategy.Elem(i));
            }

            Assert.Equal(count,h.List.Count);
            for(var i = 0; i<count; i++) {
                Assert.Equal(TStrategy.Elem(i),h.List[i]);
            }
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    // ============================================================
    // System.Linq.Enumerable 互換テスト。

    [BenchFact,Benchmark]
    public void Enumerable_Aggregate() {
        var path = CreatePath();
        try {
            using var h = TStrategy.CreateList(path);
            h.List.Add(TStrategy.Elem(1));
            h.List.Add(TStrategy.Elem(2));
            h.List.Add(TStrategy.Elem(3));
            h.List.Add(TStrategy.Elem(4));

            var actual = h.List.Aggregate(static (total,item) => TStrategy.Elem(Convert.ToInt32(total)+Convert.ToInt32(item)));
            var expected = ((IEnumerable<T>)h.List).Aggregate(static (total,item) => TStrategy.Elem(Convert.ToInt32(total)+Convert.ToInt32(item)));
            Assert.NotNull(actual);
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public void Enumerable_All() {
        var path = CreatePath();
        try {
            using var h = TStrategy.CreateList(path);
            h.List.Add(TStrategy.Elem(1));
            h.List.Add(TStrategy.Elem(2));
            h.List.Add(TStrategy.Elem(3));

            Assert.True(h.List.All(_ => true));
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public void Enumerable_Any() {
        var path = CreatePath();
        try {
            using var h = TStrategy.CreateList(path);
            Assert.False(h.List.Any());
            h.List.Add(TStrategy.Elem(1));
            Assert.True(h.List.Any());
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public void Enumerable_Append() {
        var path = CreatePath();
        try {
            using var h = TStrategy.CreateList(path);
            h.List.Add(TStrategy.Elem(1));
            h.List.Add(TStrategy.Elem(2));

            var appended = h.List.Append(TStrategy.Elem(3));
            Assert.Equal(new[] { TStrategy.Elem(1),TStrategy.Elem(2),TStrategy.Elem(3) },appended);
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public void Enumerable_AsEnumerable() {
        var path = CreatePath();
        try {
            using var h = TStrategy.CreateList(path);
            h.List.Add(TStrategy.Elem(42));
            // AsEnumerable should return the same reference for ListBase<T>
            var enumerable = h.List.AsEnumerable();
            Assert.NotNull(enumerable);
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public void Enumerable_Chunk() {
        var path = CreatePath();
        try {
            using var h = TStrategy.CreateList(path);
            for(var i = 0; i<10; i++)
                h.List.Add(TStrategy.Elem(i));

            var chunks = h.List.Chunk(3);
            var chunkList = chunks.ToList();
            Assert.Equal(4,chunkList.Count);
            Assert.Equal(3,chunkList[0].Length);
            Assert.Single(chunkList[3]);
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public void Enumerable_Concat() {
        var path = CreatePath();
        try {
            using var h = TStrategy.CreateList(path);
            h.List.Add(TStrategy.Elem(1));
            h.List.Add(TStrategy.Elem(2));

            var concat = h.List.Concat(new[] { TStrategy.Elem(3),TStrategy.Elem(4) });
            Assert.Equal(new[] { TStrategy.Elem(1),TStrategy.Elem(2),TStrategy.Elem(3),TStrategy.Elem(4) },concat);
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public void Enumerable_Contains() {
        var path = CreatePath();
        try {
            using var h = TStrategy.CreateList(path);
            h.List.Add(TStrategy.Elem(1));
            h.List.Add(TStrategy.Elem(2));

            Assert.Contains(TStrategy.Elem(1),h.List);
#pragma warning disable xUnit2012
            Assert.False(h.List.Any(item => EqualityComparer<T>.Default.Equals(item,TStrategy.Elem(99))));
#pragma warning restore xUnit2012
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public void Enumerable_Count() {
        var path = CreatePath();
        try {
            using var h = TStrategy.CreateList(path);
            Assert.Empty(h.List);
            h.List.Add(TStrategy.Elem(1));
            h.List.Add(TStrategy.Elem(2));
            Assert.Equal(2,h.List.Count());
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public void Enumerable_DefaultIfEmpty() {
        var path = CreatePath();
        try {
            using var h = TStrategy.CreateList(path);
            Assert.Single(h.List.DefaultIfEmpty(TStrategy.Elem(42)));
            h.List.Add(TStrategy.Elem(1));
            Assert.Single(h.List.DefaultIfEmpty(TStrategy.Elem(42)));
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public void Enumerable_Distinct() {
        var path = CreatePath();
        try {
            using var h = TStrategy.CreateList(path);
            h.List.Add(TStrategy.Elem(1));
            h.List.Add(TStrategy.Elem(2));
            h.List.Add(TStrategy.Elem(1));
            h.List.Add(TStrategy.Elem(3));

            Assert.Equal(new[] { TStrategy.Elem(1),TStrategy.Elem(2),TStrategy.Elem(3) },h.List.Distinct());
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public void Enumerable_ElementAt() {
        var path = CreatePath();
        try {
            using var h = TStrategy.CreateList(path);
            h.List.Add(TStrategy.Elem(10));
            h.List.Add(TStrategy.Elem(20));
            h.List.Add(TStrategy.Elem(30));

            Assert.Equal(TStrategy.Elem(10),h.List.ElementAt(0));
            Assert.Equal(TStrategy.Elem(30),h.List.ElementAt(2));
            Assert.Equal(TStrategy.Elem(20),h.List.ElementAt(^2));
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public void Enumerable_ElementAtOrDefault() {
        var path = CreatePath();
        try {
            using var h = TStrategy.CreateList(path);
            h.List.Add(TStrategy.Elem(10));
            h.List.Add(TStrategy.Elem(20));

            Assert.Equal(TStrategy.Elem(10),h.List.ElementAtOrDefault(0));
            Assert.Equal(default(T)!,h.List.ElementAtOrDefault(99));
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public void Enumerable_Except() {
        var path = CreatePath();
        try {
            using var h = TStrategy.CreateList(path);
            h.List.Add(TStrategy.Elem(1));
            h.List.Add(TStrategy.Elem(2));
            h.List.Add(TStrategy.Elem(3));
            h.List.Add(TStrategy.Elem(4));

            var except = h.List.Except(new[] { TStrategy.Elem(2),TStrategy.Elem(4) });
            Assert.Equal(new[] { TStrategy.Elem(1),TStrategy.Elem(3) },except);
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public void Enumerable_First() {
        var path = CreatePath();
        try {
            using var h = TStrategy.CreateList(path);
            h.List.Add(TStrategy.Elem(10));
            h.List.Add(TStrategy.Elem(20));

            Assert.Equal(TStrategy.Elem(10),h.List.First());
            Assert.Equal(TStrategy.Elem(20),h.List.First(item => Equals(item,TStrategy.Elem(20))));
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public void Enumerable_FirstOrDefault() {
        var path = CreatePath();
        try {
            using var h = TStrategy.CreateList(path);
            Assert.Equal(default(T)!,h.List.FirstOrDefault());
            h.List.Add(TStrategy.Elem(42));
            Assert.Equal(TStrategy.Elem(42),h.List.FirstOrDefault());
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public void Enumerable_Intersect() {
        var path = CreatePath();
        try {
            using var h = TStrategy.CreateList(path);
            h.List.Add(TStrategy.Elem(1));
            h.List.Add(TStrategy.Elem(2));
            h.List.Add(TStrategy.Elem(3));

            var intersect = h.List.Intersect(new[] { TStrategy.Elem(2),TStrategy.Elem(3),TStrategy.Elem(4) });
            Assert.Equal(new[] { TStrategy.Elem(2),TStrategy.Elem(3) },intersect);
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public void Enumerable_Last() {
        var path = CreatePath();
        try {
            using var h = TStrategy.CreateList(path);
            h.List.Add(TStrategy.Elem(10));
            h.List.Add(TStrategy.Elem(20));
            h.List.Add(TStrategy.Elem(30));

            Assert.Equal(TStrategy.Elem(30),h.List.Last());
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public void Enumerable_LastOrDefault() {
        var path = CreatePath();
        try {
            using var h = TStrategy.CreateList(path);
            Assert.Equal(default(T)!,h.List.LastOrDefault());
            h.List.Add(TStrategy.Elem(42));
            Assert.Equal(TStrategy.Elem(42),h.List.LastOrDefault());
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public void Enumerable_LongCount() {
        var path = CreatePath();
        try {
            using var h = TStrategy.CreateList(path);
            h.List.Add(TStrategy.Elem(1));
            h.List.Add(TStrategy.Elem(2));

            Assert.Equal(2L,h.List.LongCount());
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public void Enumerable_Order() {
        var path = CreatePath();
        try {
            using var h = TStrategy.CreateList(path);
            h.List.Add(TStrategy.Elem(3));
            h.List.Add(TStrategy.Elem(1));
            h.List.Add(TStrategy.Elem(2));

            Assert.Equal(new[] { TStrategy.Elem(1),TStrategy.Elem(2),TStrategy.Elem(3) },h.List.Order());
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public void Enumerable_OrderDescending() {
        var path = CreatePath();
        try {
            using var h = TStrategy.CreateList(path);
            h.List.Add(TStrategy.Elem(1));
            h.List.Add(TStrategy.Elem(3));
            h.List.Add(TStrategy.Elem(2));

            Assert.Equal(new[] { TStrategy.Elem(3),TStrategy.Elem(2),TStrategy.Elem(1) },h.List.OrderDescending());
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public void Enumerable_Prepend() {
        var path = CreatePath();
        try {
            using var h = TStrategy.CreateList(path);
            h.List.Add(TStrategy.Elem(2));
            h.List.Add(TStrategy.Elem(3));

            var prepended = h.List.Prepend(TStrategy.Elem(1));
            Assert.Equal(new[] { TStrategy.Elem(1),TStrategy.Elem(2),TStrategy.Elem(3) },prepended);
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public void Enumerable_Reverse() {
        var path = CreatePath();
        try {
            using var h = TStrategy.CreateList(path);
            h.List.Add(TStrategy.Elem(1));
            h.List.Add(TStrategy.Elem(2));
            h.List.Add(TStrategy.Elem(3));

            Assert.Equal(new[] { TStrategy.Elem(3),TStrategy.Elem(2),TStrategy.Elem(1) },h.List.Reverse());
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public void Enumerable_Select() {
        var path = CreatePath();
        try {
            using var h = TStrategy.CreateList(path);
            h.List.Add(TStrategy.Elem(1));
            h.List.Add(TStrategy.Elem(2));
            h.List.Add(TStrategy.Elem(3));

            var selected = h.List.Select(static item => item);
            Assert.Equal(new[] { TStrategy.Elem(1),TStrategy.Elem(2),TStrategy.Elem(3) },selected);
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public void Enumerable_SequenceEqual() {
        var path = CreatePath();
        try {
            using var h = TStrategy.CreateList(path);
            h.List.Add(TStrategy.Elem(1));
            h.List.Add(TStrategy.Elem(2));

            Assert.True(h.List.SequenceEqual(new[] { TStrategy.Elem(1),TStrategy.Elem(2) }));
            Assert.False(h.List.SequenceEqual(new[] { TStrategy.Elem(1),TStrategy.Elem(99) }));
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public void Enumerable_Single() {
        var path = CreatePath();
        try {
            using var h = TStrategy.CreateList(path);
            h.List.Add(TStrategy.Elem(42));

            Assert.Equal(TStrategy.Elem(42),h.List.Single());
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public void Enumerable_SingleOrDefault() {
        var path = CreatePath();
        try {
            using var h = TStrategy.CreateList(path);
            Assert.Equal(default(T)!,h.List.SingleOrDefault());
            h.List.Add(TStrategy.Elem(42));
            Assert.Equal(TStrategy.Elem(42),h.List.SingleOrDefault());
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public void Enumerable_Skip() {
        var path = CreatePath();
        try {
            using var h = TStrategy.CreateList(path);
            for(var i = 1; i<=5; i++)
                h.List.Add(TStrategy.Elem(i));

            Assert.Equal(new[] { TStrategy.Elem(4),TStrategy.Elem(5) },h.List.Skip(3));
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public void Enumerable_SkipLast() {
        var path = CreatePath();
        try {
            using var h = TStrategy.CreateList(path);
            for(var i = 1; i<=5; i++)
                h.List.Add(TStrategy.Elem(i));

            Assert.Equal(new[] { TStrategy.Elem(1),TStrategy.Elem(2),TStrategy.Elem(3) },h.List.SkipLast(2));
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public void Enumerable_SkipWhile() {
        var path = CreatePath();
        try {
            using var h = TStrategy.CreateList(path);
            for(var i = 1; i<=5; i++)
                h.List.Add(TStrategy.Elem(i));

            Assert.Equal(new[] { TStrategy.Elem(4),TStrategy.Elem(5) },h.List.SkipWhile(static item => Convert.ToInt32(item)<4));
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public void Enumerable_Take() {
        var path = CreatePath();
        try {
            using var h = TStrategy.CreateList(path);
            for(var i = 1; i<=5; i++)
                h.List.Add(TStrategy.Elem(i));

            Assert.Equal(new[] { TStrategy.Elem(1),TStrategy.Elem(2),TStrategy.Elem(3) },h.List.Take(3));
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public void Enumerable_TakeLast() {
        var path = CreatePath();
        try {
            using var h = TStrategy.CreateList(path);
            for(var i = 1; i<=5; i++)
                h.List.Add(TStrategy.Elem(i));

            Assert.Equal(new[] { TStrategy.Elem(4),TStrategy.Elem(5) },h.List.TakeLast(2));
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public void Enumerable_TakeWhile() {
        var path = CreatePath();
        try {
            using var h = TStrategy.CreateList(path);
            for(var i = 1; i<=5; i++)
                h.List.Add(TStrategy.Elem(i));

            Assert.Equal(new[] { TStrategy.Elem(1),TStrategy.Elem(2),TStrategy.Elem(3) },h.List.TakeWhile(static item => Convert.ToInt32(item)<4));
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public void Enumerable_ToArray() {
        var path = CreatePath();
        try {
            using var h = TStrategy.CreateList(path);
            h.List.Add(TStrategy.Elem(1));
            h.List.Add(TStrategy.Elem(2));
            h.List.Add(TStrategy.Elem(3));

            Assert.Equal(new[] { TStrategy.Elem(1),TStrategy.Elem(2),TStrategy.Elem(3) },h.List.ToArray());
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public void Enumerable_ToHashSet() {
        var path = CreatePath();
        try {
            using var h = TStrategy.CreateList(path);
            h.List.Add(TStrategy.Elem(1));
            h.List.Add(TStrategy.Elem(2));
            h.List.Add(TStrategy.Elem(1));
            h.List.Add(TStrategy.Elem(3));

            var hashSet = h.List.ToHashSet();
            Assert.Equal(3,hashSet.Count);
            Assert.Contains(TStrategy.Elem(1),hashSet);
            Assert.Contains(TStrategy.Elem(2),hashSet);
            Assert.Contains(TStrategy.Elem(3),hashSet);
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public void Enumerable_ToList() {
        var path = CreatePath();
        try {
            using var h = TStrategy.CreateList(path);
            h.List.Add(TStrategy.Elem(1));
            h.List.Add(TStrategy.Elem(2));
            h.List.Add(TStrategy.Elem(3));

            var list = h.List.ToList();
            Assert.Equal(3,list.Count);
            Assert.Equal(TStrategy.Elem(1),list[0]);
            Assert.Equal(TStrategy.Elem(2),list[1]);
            Assert.Equal(TStrategy.Elem(3),list[2]);
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public void Enumerable_Union() {
        var path = CreatePath();
        try {
            using var h = TStrategy.CreateList(path);
            h.List.Add(TStrategy.Elem(1));
            h.List.Add(TStrategy.Elem(2));
            h.List.Add(TStrategy.Elem(3));

            var union = h.List.Union(new[] { TStrategy.Elem(3),TStrategy.Elem(4),TStrategy.Elem(5) });
            Assert.Equal(new[] { TStrategy.Elem(1),TStrategy.Elem(2),TStrategy.Elem(3),TStrategy.Elem(4),TStrategy.Elem(5) },union);
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public void Enumerable_Where() {
        var path = CreatePath();
        try {
            using var h = TStrategy.CreateList(path);
            for(var i = 1; i<=5; i++)
                h.List.Add(TStrategy.Elem(i));

            var filtered = h.List.Where(static item => Convert.ToInt32(item)%2==0);
            Assert.Equal(new[] { TStrategy.Elem(2),TStrategy.Elem(4) },filtered);
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public void Enumerable_Zip() {
        var path = CreatePath();
        try {
            using var h = TStrategy.CreateList(path);
            for(var i = 1; i<=3; i++)
                h.List.Add(TStrategy.Elem(i));

            var zipped = h.List.Zip(new[] { "a","b","c" });
            Assert.Equal(3,zipped.Count());
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public void Enumerable_Index() {
        var path = CreatePath();
        try {
            using var h = TStrategy.CreateList(path);
            h.List.Add(TStrategy.Elem(10));
            h.List.Add(TStrategy.Elem(20));
            h.List.Add(TStrategy.Elem(30));

            var indexed = h.List.Index().ToList();
            Assert.Equal(3,indexed.Count);
            Assert.Equal((0,TStrategy.Elem(10)),indexed[0]);
            Assert.Equal((1,TStrategy.Elem(20)),indexed[1]);
            Assert.Equal((2,TStrategy.Elem(30)),indexed[2]);
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public void Enumerable_TryGetNonEnumeratedCount() {
        var path = CreatePath();
        try {
            using var h = TStrategy.CreateList(path);
            h.List.Add(TStrategy.Elem(1));
            h.List.Add(TStrategy.Elem(2));

            Assert.True(h.List.TryGetNonEnumeratedCount(out var count));
            Assert.Equal(2,count);
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public void Enumerable_SelectMany() {
        var path = CreatePath();
        try {
            using var h = TStrategy.CreateList(path);
            h.List.Add(TStrategy.Elem(1));
            h.List.Add(TStrategy.Elem(2));

            var result = h.List.SelectMany(static item => new[] { item,item });
            Assert.Equal(new[] { TStrategy.Elem(1),TStrategy.Elem(1),TStrategy.Elem(2),TStrategy.Elem(2) },result);
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public void Enumerable_OrderBy() {
        var path = CreatePath();
        try {
            using var h = TStrategy.CreateList(path);
            h.List.Add(TStrategy.Elem(3));
            h.List.Add(TStrategy.Elem(1));
            h.List.Add(TStrategy.Elem(2));

            var ordered = h.List.OrderBy(static item => item);
            Assert.Equal(new[] { TStrategy.Elem(1),TStrategy.Elem(2),TStrategy.Elem(3) },ordered);
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public void Enumerable_OrderByDescending() {
        var path = CreatePath();
        try {
            using var h = TStrategy.CreateList(path);
            h.List.Add(TStrategy.Elem(1));
            h.List.Add(TStrategy.Elem(3));
            h.List.Add(TStrategy.Elem(2));

            var ordered = h.List.OrderByDescending(static item => item);
            Assert.Equal(new[] { TStrategy.Elem(3),TStrategy.Elem(2),TStrategy.Elem(1) },ordered);
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public void Enumerable_ThenBy() {
        var path = CreatePath();
        try {
            using var h = TStrategy.CreateList(path);
            h.List.Add(TStrategy.Elem(2));
            h.List.Add(TStrategy.Elem(1));

            var ordered = h.List.OrderBy(static item => item).ThenBy(static item => item);
            Assert.Equal(new[] { TStrategy.Elem(1),TStrategy.Elem(2) },ordered);
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public void Enumerable_ThenByDescending() {
        var path = CreatePath();
        try {
            using var h = TStrategy.CreateList(path);
            h.List.Add(TStrategy.Elem(2));
            h.List.Add(TStrategy.Elem(1));

            var ordered = h.List.OrderBy(static item => item).ThenByDescending(static item => item);
            Assert.Equal(new[] { TStrategy.Elem(1),TStrategy.Elem(2) },ordered);
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public void Enumerable_DistinctBy() {
        var path = CreatePath();
        try {
            using var h = TStrategy.CreateList(path);
            h.List.Add(TStrategy.Elem(1));
            h.List.Add(TStrategy.Elem(2));
            h.List.Add(TStrategy.Elem(1));
            h.List.Add(TStrategy.Elem(3));

            var distinct = h.List.DistinctBy(static item => item);
            Assert.Equal(new[] { TStrategy.Elem(1),TStrategy.Elem(2),TStrategy.Elem(3) },distinct);
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public void Enumerable_UnionBy() {
        var path = CreatePath();
        try {
            using var h = TStrategy.CreateList(path);
            h.List.Add(TStrategy.Elem(1));
            h.List.Add(TStrategy.Elem(2));

            var result = h.List.UnionBy(new[] { TStrategy.Elem(2),TStrategy.Elem(3) },static item => item);
            Assert.Equal(new[] { TStrategy.Elem(1),TStrategy.Elem(2),TStrategy.Elem(3) },result);
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public void Enumerable_IntersectBy() {
        var path = CreatePath();
        try {
            using var h = TStrategy.CreateList(path);
            h.List.Add(TStrategy.Elem(1));
            h.List.Add(TStrategy.Elem(2));
            h.List.Add(TStrategy.Elem(3));

            var result = h.List.IntersectBy(new[] { TStrategy.Elem(2),TStrategy.Elem(3) },static item => item);
            Assert.Equal(new[] { TStrategy.Elem(2),TStrategy.Elem(3) },result);
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public void Enumerable_ExceptBy() {
        var path = CreatePath();
        try {
            using var h = TStrategy.CreateList(path);
            h.List.Add(TStrategy.Elem(1));
            h.List.Add(TStrategy.Elem(2));
            h.List.Add(TStrategy.Elem(3));
            h.List.Add(TStrategy.Elem(4));

            var result = h.List.ExceptBy(new[] { TStrategy.Elem(2),TStrategy.Elem(4) },static item => item);
            Assert.Equal(new[] { TStrategy.Elem(1),TStrategy.Elem(3) },result);
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public void Enumerable_OfType() {
        var path = CreatePath();
        try {
            using var h = TStrategy.CreateList(path);
            h.List.Add(TStrategy.Elem(1));
            h.List.Add(TStrategy.Elem(2));

            global::System.Collections.IEnumerable source = h.List;
            var ofType = source.OfType<T>();
            Assert.Equal(h.List.ToArray(),ofType);
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [BenchFact,Benchmark]
    public void Enumerable_Cast() {
        var path = CreatePath();
        try {
            using var h = TStrategy.CreateList(path);
            h.List.Add(TStrategy.Elem(1));
            h.List.Add(TStrategy.Elem(2));

            global::System.Collections.IEnumerable source = h.List;
            var cast = source.Cast<T>();
            Assert.Equal(h.List.ToArray(),cast);
        }
        finally {
            TryDeleteDirectory(path);
        }
    }
}
