using Seamless.Collections.Persisted;
using Seamless.Execution;
using CommonStore=Seamless.Execution.Store;
namespace Test.Persisted.Collections.Work;
public class SortedSet{
    [Fact]public void CompleteAdding(){
        using var owner=CommonStore.CreateTemporary();
        using var set=owner.CreatePersistedSortedSet<int>("ids",sortMemoryBytes:64);
        Assert.False(set.IsReady);
        Assert.Throws<InvalidOperationException>(()=>set.Contains(1));
        Assert.Throws<InvalidOperationException>(()=>set.Range(0,2));
        foreach(var value in new[]{3,1,2,1,3}){set.Add(value);}
        Assert.Equal(new[]{3,1,2,1,3},System.Linq.Enumerable.ToArray(set));
        Assert.Equal(5,set.Count);
        set.CompleteAdding();set.CompleteAdding();
        Assert.True(set.IsReady);
        Assert.Equal(3,set.Count);Assert.Equal(5,set.AddedCount);
        Assert.Equal(new[]{1,2,3},System.Linq.Enumerable.ToArray(set));
        Assert.True(set.Contains(2));Assert.False(set.Contains(4));
        Assert.Equal(new[]{1,2},System.Linq.Enumerable.ToArray(set.Range(1,3)));
        set.Add(0);Assert.False(set.IsReady);
        Assert.Throws<InvalidOperationException>(()=>set.Contains(0));
        set.CompleteAdding();Assert.Equal(new[]{0,1,2,3},System.Linq.Enumerable.ToArray(set));
    }
    [Fact]public void EmptyAndLifetime(){
        var owner=CommonStore.CreateTemporary();
        var path=owner.DirectoryPath!;
        var set=owner.CreatePersistedSortedSet<int>("ids");
        set.CompleteAdding();Assert.Empty(set);Assert.False(set.Contains(0));
        owner.Dispose();owner.Dispose();
        Assert.Throws<ObjectDisposedException>(()=>set.Add(0));
        Assert.Equal(0,owner.Budget.MemoryBytes);Assert.Equal(0,owner.Budget.TemporaryStorageBytes);
        Assert.False(Directory.Exists(path));
    }
    [Fact]public void BoundaryValuesUseSemanticOrder(){
        using var owner=CommonStore.CreateTemporary();
        using var numbers=owner.CreatePersistedSortedSet<long>("numbers");
        foreach(var value in new[]{long.MaxValue,0,-1,long.MinValue}){numbers.Add(value);}
        numbers.CompleteAdding();
        Assert.Equal(new[]{long.MinValue,-1,0,long.MaxValue},System.Linq.Enumerable.ToArray(numbers));
        Assert.True(numbers.Contains(long.MinValue));Assert.True(numbers.Contains(long.MaxValue));Assert.False(numbers.Contains(1));
        using var strings=owner.CreatePersistedSortedSet<string?>("strings");
        strings.Add(null);strings.CompleteAdding();Assert.Single(strings);Assert.True(strings.Contains(null));
        strings.Add("a");strings.Add("Z");strings.CompleteAdding();
        Assert.Equal(new string?[]{null,"Z","a"},System.Linq.Enumerable.ToArray(strings));
    }
    [Fact]public void SaveLoad(){
        var path=Path.Combine(Path.GetTempPath(),"seamless-sorted-test-"+Guid.NewGuid().ToString("N"));
        try{
            using(var owner=CommonStore.Open(path)){
                using var set=owner.CreatePersistedSortedSet<string>("ids",StringComparer.OrdinalIgnoreCase,"ordinal-ignore-case-v1");
                set.Add("Beta");set.Add("alpha");set.Add("ALPHA");
                var building=owner.Save();
                set.CompleteAdding();var complete=owner.Save();
                owner.Load(building);Assert.False(set.IsReady);Assert.Equal(3,set.Count);
                Assert.Throws<InvalidOperationException>(()=>set.Contains("alpha"));
                set.Add("Gamma");set.CompleteAdding();owner.Save();
                owner.Load(complete);Assert.True(set.IsReady);Assert.Equal(2,set.Count);Assert.False(set.Contains("gamma"));
                Assert.Equal(new[]{"ALPHA","Beta"},System.Linq.Enumerable.ToArray(set));
                owner.Save();
            }
            using(var owner=CommonStore.Open(path)){
                Assert.Throws<InvalidDataException>(()=>owner.CreatePersistedSortedSet<string>("ids",StringComparer.OrdinalIgnoreCase,"other"));
                Assert.Throws<InvalidDataException>(()=>owner.CreatePersistedSortedList<string>("ids",StringComparer.OrdinalIgnoreCase,"ordinal-ignore-case-v1"));
                using var set=owner.CreatePersistedSortedSet<string>("ids",StringComparer.OrdinalIgnoreCase,"ordinal-ignore-case-v1");
                Assert.True(set.IsReady);Assert.True(set.Contains("ALPHA"));Assert.Equal(2,set.Count);
            }
        }finally{if(Directory.Exists(path)){Directory.Delete(path,true);}}
    }
    [Fact]public void ExternalSortAndBinarySearch(){
        using var owner=CommonStore.CreateTemporary(new(){MemoryBytes=8192,DiskBytes=8L<<20});
        var comparer=new CountingComparer();
        using var set=owner.CreatePersistedSortedSet<int>("ids",comparer,sortMemoryBytes:512);
        for(var i=2047;i>=0;i--){set.Add(i);set.Add(i);}
        set.CompleteAdding();
        Assert.Equal(2048,set.Count);
        comparer.Calls=0;Assert.True(set.Contains(1900));Assert.InRange(comparer.Calls,1,13);
        var expected=0;foreach(var value in set){Assert.Equal(expected++,value);}
        Assert.True(owner.Budget.PeakMemoryBytes<=8192);
        Assert.Equal(0,owner.Budget.MemoryBytes);
        Assert.Empty(Directory.EnumerateFiles(owner.GetComponentDirectory("ids"),"*.hindex",SearchOption.AllDirectories));
        Assert.Empty(Directory.EnumerateFiles(owner.GetComponentDirectory("ids"),"run-*.idx",SearchOption.AllDirectories));
        Assert.Empty(Directory.EnumerateFiles(owner.GetComponentDirectory("ids"),"merge-*.idx",SearchOption.AllDirectories));
    }
    [Fact]public void FailureKeepsBuildingInput(){
        const long limit=1L<<20;
        using var owner=CommonStore.CreateTemporary(new(){MemoryBytes=8192,DiskBytes=limit});
        var comparer=new CountingComparer();
        using var set=owner.CreatePersistedSortedSet<int>("ids",comparer,sortMemoryBytes:64);
        foreach(var value in new[]{4,3,2,1}){set.Add(value);}
        var bytes=owner.Budget.TemporaryStorageBytes;
        comparer.Throw=true;
        Assert.ThrowsAny<Exception>(()=>set.CompleteAdding());
        Assert.False(set.IsReady);Assert.Equal(bytes,owner.Budget.TemporaryStorageBytes);
        Assert.Equal(new[]{4,3,2,1},System.Linq.Enumerable.ToArray(set));
        comparer.Throw=false;
        using(var blocker=owner.Budget.ReserveTemporaryStorage(limit-bytes-8)){
            Assert.Throws<ResourceLimitException>(()=>set.CompleteAdding());
            Assert.False(set.IsReady);Assert.Equal(limit-8,owner.Budget.TemporaryStorageBytes);
        }
        Assert.Equal(0,owner.Budget.MemoryBytes);
        set.CompleteAdding();Assert.Equal(new[]{1,2,3,4},System.Linq.Enumerable.ToArray(set));
    }
    private sealed class CountingComparer:IComparer<int>{
        public int Calls;
        public bool Throw;
        public int Compare(int left,int right){this.Calls++;if(this.Throw){throw new InvalidOperationException("Injected comparison failure.");}return left.CompareTo(right);}
    }
}
