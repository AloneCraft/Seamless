using Seamless.Collections.Persisted;
using Seamless.Execution;
using CommonStore=Seamless.Execution.Store;
namespace Test.Persisted.Collections.Work;

public class HashSet{
    [Fact]public void Add(){
        using(var owner=CommonStore.CreateTemporary()){
            using var set=owner.CreatePersistedHashSet<string>("case",StringComparer.OrdinalIgnoreCase,"ordinal-ignore-case-v1");
            Assert.True(set.Add("Alpha"));
            Assert.True(set.Contains("ALPHA"));
            Assert.False(set.Add("alpha"));
            Assert.True(set.Add("Beta"));
            Assert.Equal(2,set.Count);
            Assert.Equal(2,set.GetLongCount());
            Assert.False(set.Contains("missing"));
            Assert.Equal(new[]{"Alpha","Beta"},System.Linq.Enumerable.ToArray(set));
        }
        {
            // 同一 hash 値の別キーを完全比較し、追加中も検索できる。
            using var owner=CommonStore.CreateTemporary();
            using var set=owner.CreatePersistedHashSet<int>("collisions",new CollidingComparer(),"int-v1");
            for(var i=0;i<256;i++){
                Assert.True(set.Add(i));
                Assert.True(set.Contains(i));
            }
            for(var i=0;i<256;i++){
                Assert.False(set.Add(i));
            }
            Assert.Equal(256,set.Count);
            Assert.False(set.Contains(256));
        }
    }

    [Fact]public void GetEnumerator(){
        using var owner=CommonStore.CreateTemporary(new(){MemoryBytes=4096,DiskBytes=8L<<20});
        using var set=owner.CreatePersistedHashSet<int>("large");
        for(var i=0;i<10_000;i++){
            set.Add(i);
        }
        var count=0;
        foreach(var value in set){
            Assert.Equal(count++,value);
        }
        Assert.Equal(10_000,count);
        Assert.True(owner.Budget.PeakMemoryBytes<=4096);
        Assert.Equal(0,owner.Budget.MemoryBytes);
    }

    [Fact]public void SaveLoad(){
        var directory=Path.Combine(Path.GetTempPath(),"seamless-work-hash-test-"+Guid.NewGuid().ToString("N"));
        try{
            using(var owner=CommonStore.Open(directory)){
                using var set=owner.CreatePersistedHashSet<int>("ids",new SeedComparer(1),"integer-equality-v1");
                set.Add(1);
                var first=owner.Save();
                set.Add(2);
                var second=owner.Save();
                owner.Load(first);
                Assert.False(set.Contains(2));
                set.Add(3);
                owner.Save();
                owner.Load(second);
                Assert.True(set.Contains(1));
                Assert.True(set.Contains(2));
                Assert.False(set.Contains(3));
                owner.Save();
            }
            using(var owner=CommonStore.Open(directory)){
                // 同じ等価性で hash seed が変わっても索引を再構築する。
                using var set=owner.CreatePersistedHashSet<int>("ids",new SeedComparer(997),"integer-equality-v1");
                Assert.Equal(2,set.Count);
                Assert.True(set.Contains(1));
                Assert.True(set.Contains(2));
                Assert.Throws<InvalidDataException>(()=>owner.CreatePersistedHashSet<int>("ids",new SeedComparer(997),"different-rule"));
            }
        }finally{
            if(Directory.Exists(directory)){
                Directory.Delete(directory,true);
            }
        }
    }

    [Fact]public void ResourceLimits(){
        {
            // 索引拡張の旧+作業ファイル容量も先に予約し、失敗した追加は件数を変えない。
            const long limit=4L<<20;
            using var owner=CommonStore.CreateTemporary(new(){MemoryBytes=4096,DiskBytes=limit});
            using var set=owner.CreatePersistedHashSet<int>("ids");
            for(var i=0;i<10;i++){
                set.Add(i);
            }
            using(var blocker=owner.Budget.ReserveTemporaryStorage(limit-owner.Budget.TemporaryStorageBytes-400)){
                var error=Assert.Throws<ResourceLimitException>(()=>set.Add(10));
                Assert.Equal(ResourceLimitKind.TemporaryStorage,error.Kind);
                Assert.Equal(10,set.Count);
                Assert.False(set.Contains(10));
                for(var i=0;i<10;i++){
                    Assert.True(set.Contains(i));
                }
                Assert.Empty(Directory.EnumerateFiles(owner.GetComponentDirectory("ids"),"*.tmp-*",SearchOption.AllDirectories));
            }
            Assert.True(set.Add(10));
            Assert.True(owner.Budget.PeakTemporaryStorageBytes<=limit);
        }
        {
            using var owner=CommonStore.CreateTemporary(new(){MemoryBytes=128,DiskBytes=1L<<20});
            using var set=owner.CreatePersistedHashSet<int>("ids");
            Assert.Throws<ResourceLimitException>(()=>set.Add(1));
            Assert.Empty(set);
            Assert.Equal(0,owner.Budget.MemoryBytes);
        }
        {
            // 保存コピーの容量不足でもデータは使え、作りかけ世代は残さない。
            const long limit=4L<<20;
            using var owner=CommonStore.CreateTemporary(new(){MemoryBytes=4096,DiskBytes=limit});
            using var set=owner.CreatePersistedHashSet<int>("ids");
            set.Add(1);
            using var blocker=owner.Budget.ReserveTemporaryStorage(limit-owner.Budget.TemporaryStorageBytes-1);
            Assert.Throws<ResourceLimitException>(()=>owner.Save());
            Assert.True(set.Contains(1));
            Assert.Empty(owner.Checkpoints);
            Assert.Equal(0,owner.Budget.MemoryBytes);
            Assert.False(Directory.Exists(Path.Combine(owner.GetComponentDirectory("ids"),"snapshots")));
        }
    }

    [Fact]public void Dispose(){
        var owner=CommonStore.CreateTemporary();
        var directory=owner.DirectoryPath!;
        var set=owner.CreatePersistedHashSet<int>("ids");
        set.Add(1);
        owner.Save();
        owner.Dispose();
        owner.Dispose();
        Assert.True(set.IsDisposed);
        Assert.Throws<ObjectDisposedException>(()=>set.Contains(1));
        Assert.Throws<ObjectDisposedException>(()=>set.Add(2));
        Assert.Equal(0,owner.Budget.MemoryBytes);
        Assert.Equal(0,owner.Budget.TemporaryStorageBytes);
        Assert.False(Directory.Exists(directory));
    }

    private sealed class CollidingComparer:IEqualityComparer<int>{
        public bool Equals(int x,int y)=>x==y;
        public int GetHashCode(int value)=>0;
    }
    private sealed class SeedComparer(int seed):IEqualityComparer<int>{
        public bool Equals(int x,int y)=>x==y;
        public int GetHashCode(int value)=>value^seed;
    }
}
