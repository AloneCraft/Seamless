using Tsavorite.Stores;

namespace Test.Tsavorite.Collections.Unit.Linq;

public sealed class EnumerableHashSet {
    [Fact, Trait("Category","Unit")]
    public void ScalarOperators_ScanConcreteHashSet() {
        using var source = CreateSource();
        var set = source.Set;
        for(var i = 1; i<=5; i++) {
            set.Add(i);
        }

        Assert.Equal(5,global::Tsavorite.Linq.Enumerable.Count(set));
        Assert.Equal(5L,global::Tsavorite.Linq.Enumerable.LongCount(set));
        Assert.True(global::Tsavorite.Linq.Enumerable.Any(set));
        Assert.True(global::Tsavorite.Linq.Enumerable.Any(set,static x => x==3));
        Assert.False(global::Tsavorite.Linq.Enumerable.Any(set,static x => x==9));
        Assert.True(global::Tsavorite.Linq.Enumerable.All(set,static x => x>0));
        Assert.False(global::Tsavorite.Linq.Enumerable.All(set,static x => x<5));
        Assert.Equal(2,global::Tsavorite.Linq.Enumerable.Count(set,static x => x%2==0));
        Assert.Equal(3L,global::Tsavorite.Linq.Enumerable.LongCount(set,static x => x>=3));
        Assert.True(global::Tsavorite.Linq.Enumerable.Contains(set,4));
        Assert.False(global::Tsavorite.Linq.Enumerable.Contains(set,8));
        Assert.Equal(15,global::Tsavorite.Linq.Enumerable.Aggregate(set,static (acc,x) => acc+x));
        Assert.Equal(115,global::Tsavorite.Linq.Enumerable.Aggregate(set,100,static (acc,x) => acc+x));
        Assert.Equal("15",global::Tsavorite.Linq.Enumerable.Aggregate(set,0,static (acc,x) => acc+x,static x => x.ToString()));
    }

    [Fact, Trait("Category","Unit")]
    public void FirstOperators_ReturnMatchingValueOrDefault() {
        using var source = CreateSource();
        var set = source.Set;
        set.Add(10);
        set.Add(20);

        Assert.Contains(global::Tsavorite.Linq.Enumerable.First(set),new[] { 10,20 });
        Assert.Equal(20,global::Tsavorite.Linq.Enumerable.First(set,static x => x==20));
        Assert.Contains(global::Tsavorite.Linq.Enumerable.FirstOrDefault(set),new[] { 10,20 });
        Assert.Equal(20,global::Tsavorite.Linq.Enumerable.FirstOrDefault(set,static x => x==20));
        Assert.Equal(-1,global::Tsavorite.Linq.Enumerable.FirstOrDefault(set,static x => x==30,-1));
        Assert.Throws<InvalidOperationException>(() => global::Tsavorite.Linq.Enumerable.First(set,static x => x==30));
    }

    [Fact, Trait("Category","Unit")]
    public void EmptyHashSet_UsesEnumerableSemantics() {
        using var source = CreateSource();
        var set = source.Set;

        Assert.False(global::Tsavorite.Linq.Enumerable.Any(set));
        Assert.True(global::Tsavorite.Linq.Enumerable.All(set,static _ => false));
        Assert.Equal(0,global::Tsavorite.Linq.Enumerable.Count(set));
        Assert.Equal(-1,global::Tsavorite.Linq.Enumerable.FirstOrDefault(set,-1));
        Assert.Throws<InvalidOperationException>(() => global::Tsavorite.Linq.Enumerable.First(set));
        Assert.Throws<InvalidOperationException>(() => global::Tsavorite.Linq.Enumerable.Aggregate(set,static (acc,x) => acc+x));
    }

    [Fact, Trait("Category","Unit")]
    public void RemovedItems_AreNotScanned() {
        using var source = CreateSource();
        var set = source.Set;
        set.Add(1);
        set.Add(2);
        set.Add(3);
        set.Remove(2);

        Assert.Equal(2,global::Tsavorite.Linq.Enumerable.Count(set));
        Assert.False(global::Tsavorite.Linq.Enumerable.Contains(set,2));
        Assert.Equal(4,global::Tsavorite.Linq.Enumerable.Aggregate(set,static (acc,x) => acc+x));
    }

    private static Source CreateSource() {
        var folder = TestPath.Directory<EnumerableHashSet>();
        var objectFolder = folder+"_obj";
        var logDevice = global::Tsavorite.core.Devices.CreateLogDevice(
            Path.Combine(folder,"log.data"),
            deleteOnClose: true,
            capacity: global::Tsavorite.Const.MinObjectLogCapacity);
        var objectLogDevice = global::Tsavorite.core.Devices.CreateLogDevice(
            Path.Combine(objectFolder,"obj.data"),
            deleteOnClose: true,
            capacity: global::Tsavorite.Const.MinObjectLogCapacity);
        var store = HashStore.Create(CheckpointDir: folder).Create<int>(logDevice,objectLogDevice);
        return new Source(folder,objectFolder,logDevice,objectLogDevice,store,store.Create());
    }

    private sealed class Source(
        string folder,
        string objectFolder,
        IDisposable logDevice,
        IDisposable objectLogDevice,
        IDisposable store,
        global::Tsavorite.Collections.HashSet<int> set):IDisposable {
        public global::Tsavorite.Collections.HashSet<int> Set { get; } = set;

        public void Dispose() {
            this.Set.Dispose();
            store.Dispose();
            objectLogDevice.Dispose();
            logDevice.Dispose();
            DeleteDirectory(objectFolder);
            DeleteDirectory(folder);
        }

        private static void DeleteDirectory(string path) {
            try {
                Directory.Delete(path,recursive: true);
            }
            catch {
            }
        }
    }
}
