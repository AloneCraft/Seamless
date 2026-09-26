using Seamless.Collections.Persisted;
using Seamless.Execution;
using CommonStore=Seamless.Execution.Store;
namespace Test.Persisted.Collections.Work;

public class HashDictionary{
    [Fact]public void Add(){
        using var owner=CommonStore.CreateTemporary();
        using var dictionary=owner.CreatePersistedHashDictionary<string,int>("values",StringComparer.OrdinalIgnoreCase,"ignore-case-v1");
        dictionary.Add("Key",1);
        Assert.True(dictionary.ContainsKey("KEY"));
        Assert.True(dictionary.TryGetValue("key",out var first));
        Assert.Equal(1,first);
        dictionary.Add("KEY",2);
        Assert.Single(dictionary);
        Assert.Equal(2,dictionary["key"]);
        dictionary["other"]=3;
        Assert.Equal(2,dictionary.GetLongCount());
        Assert.False(dictionary.TryGetValue("missing",out _));
        Assert.Throws<KeyNotFoundException>(()=>dictionary["missing"]);
        Assert.Throws<ArgumentNullException>(()=>dictionary.Add(null!,4));
        Assert.Equal(new[]{new KeyValuePair<string,int>("KEY",2),new KeyValuePair<string,int>("other",3)},System.Linq.Enumerable.ToArray(dictionary));
    }

    [Fact]public void AddComparerFailurePreservesCommittedRows(){
        for(var failureCall=1;failureCall<=3;failureCall++){
            using var owner=CommonStore.CreateTemporary();
            var comparer=new FaultComparer();
            using var dictionary=owner.CreatePersistedHashDictionary<int,int>("values",comparer,"integer-equality-v1");
            dictionary.Add(1,10);
            dictionary.Add(2,20);
            comparer.Arm(failureCall);
            var error=Record.Exception(()=>dictionary.Add(2,200));
            comparer.Arm(0);
            var expected=200;
            if(error is not null){
                Assert.IsType<InvalidOperationException>(error);
                expected=20;
            }
            Assert.Equal(2,dictionary.Count);
            Assert.Equal(expected,dictionary[2]);
            Assert.Equal(new[]{new KeyValuePair<int,int>(1,10),new(2,expected)},System.Linq.Enumerable.ToArray(dictionary));
            // 失敗した追記が raw データへ残り、索引の再構築で復活しないことも確認する。
            var checkpoint=owner.Save();
            owner.Load(checkpoint);
            Assert.Equal(2,dictionary.Count);
            Assert.Equal(expected,dictionary[2]);
            Assert.Equal(new[]{new KeyValuePair<int,int>(1,10),new(2,expected)},System.Linq.Enumerable.ToArray(dictionary));
        }
    }
    [Fact]public void GetEnumerator(){
        using var owner=CommonStore.CreateTemporary(new(){MemoryBytes=4096,DiskBytes=8L<<20});
        using var dictionary=owner.CreatePersistedHashDictionary<int,int>("collisions",new CollidingComparer(),"integer-v1");
        for(var i=0;i<1000;i++){
            dictionary.Add(i,i);
            dictionary.Add(i,-i);
        }
        var count=0;
        foreach(var pair in dictionary){
            Assert.Equal(count,pair.Key);
            Assert.Equal(-count++,pair.Value);
        }
        Assert.Equal(1000,count);
        Assert.Equal(1000,dictionary.Count);
        Assert.False(dictionary.ContainsKey(1001));
        Assert.Equal(0,owner.Budget.MemoryBytes);
        Assert.True(owner.Budget.PeakMemoryBytes<=4096);
    }

    [Fact]public void EnumerationRejectsChanges(){
        using var owner=CommonStore.CreateTemporary();
        using var dictionary=owner.CreatePersistedHashDictionary<int,int>("values");
        dictionary.Add(1,10);
        var checkpoint=owner.Save();
        {
            // 同じキーへの追記も、最後の要素を返した直後に検出する。
            using var enumerator=dictionary.GetEnumerator();
            Assert.True(enumerator.MoveNext());
            dictionary.Add(1,20);
            Assert.Throws<InvalidOperationException>(()=>enumerator.MoveNext());
        }
        {
            using var enumerator=dictionary.GetEnumerator();
            Assert.True(enumerator.MoveNext());
            owner.Load(checkpoint);
            Assert.Throws<InvalidOperationException>(()=>enumerator.MoveNext());
        }
        Assert.Equal(10,dictionary[1]);
        Assert.Equal(0,owner.Budget.MemoryBytes);
    }
    [Fact]public void SaveLoad(){
        var directory=Path.Combine(Path.GetTempPath(),"seamless-work-dictionary-test-"+Guid.NewGuid().ToString("N"));
        try{
            using(var owner=CommonStore.Open(directory)){
                using var dictionary=owner.CreatePersistedHashDictionary<string,int>("values",StringComparer.OrdinalIgnoreCase,"ignore-case-v1");
                dictionary.Add("one",1);
                var first=owner.Save();
                dictionary.Add("ONE",2);
                dictionary.Add("two",20);
                var second=owner.Save();
                owner.Load(first);
                Assert.Equal(1,dictionary["one"]);
                Assert.False(dictionary.ContainsKey("two"));
                dictionary.Add("three",30);
                owner.Save();
                owner.Load(second);
                Assert.Equal(2,dictionary["one"]);
                Assert.Equal(20,dictionary["two"]);
                Assert.False(dictionary.ContainsKey("three"));
                owner.Save();
                dictionary.Add("unsaved",-1);
            }
            using(var owner=CommonStore.Open(directory)){
                Assert.Throws<InvalidDataException>(()=>owner.CreatePersistedHashDictionary<string,int>("values",StringComparer.Ordinal,"ignore-case-v1"));
                Assert.Throws<InvalidDataException>(()=>owner.CreatePersistedHashSet<string>("values",StringComparer.OrdinalIgnoreCase,"ignore-case-v1"));
                using var dictionary=owner.CreatePersistedHashDictionary<string,int>("values",StringComparer.OrdinalIgnoreCase,"ignore-case-v1");
                Assert.Equal(2,dictionary.Count);
                Assert.Equal(2,dictionary["ONE"]);
                Assert.False(dictionary.ContainsKey("unsaved"));
            }
        }finally{
            if(Directory.Exists(directory)){
                Directory.Delete(directory,true);
            }
        }
    }

    [Fact]public void ResourceLimits(){
        using var owner=CommonStore.CreateTemporary(new(){MemoryBytes=2048,DiskBytes=1L<<20});
        using var dictionary=owner.CreatePersistedHashDictionary<int,string>("values");
        var original=new string('a',200);
        dictionary.Add(1,original);
        using(var blocker=owner.Budget.ReserveMemory(1792)){
            Assert.Throws<ResourceLimitException>(()=>dictionary.Add(1,"replacement"));
        }
        Assert.Single(dictionary);
        Assert.Equal(original,dictionary[1]);
        Assert.Equal(new[]{new KeyValuePair<int,string>(1,original)},System.Linq.Enumerable.ToArray(dictionary));
        Assert.Equal(0,owner.Budget.MemoryBytes);
    }

    [Fact]public void Dispose(){
        using var owner=CommonStore.CreateTemporary();
        var dictionary=owner.CreatePersistedHashDictionary<int,int>("values");
        dictionary.Add(1,2);
        dictionary.Dispose();
        dictionary.Dispose();
        Assert.True(dictionary.IsDisposed);
        Assert.Throws<ObjectDisposedException>(()=>dictionary.Add(2,3));
        Assert.Throws<ObjectDisposedException>(()=>dictionary.ContainsKey(1));
        Assert.Equal(0,owner.Budget.MemoryBytes);
        Assert.Equal(0,owner.Budget.TemporaryStorageBytes);
    }

    private sealed class FaultComparer:IEqualityComparer<int>{
        private int failureCall;
        private int calls;
        internal void Arm(int failureCall){this.failureCall=failureCall;this.calls=0;}
        public bool Equals(int left,int right){
            this.calls++;
            if(this.calls==this.failureCall){throw new InvalidOperationException("Injected comparison failure.");}
            return left==right;
        }
        public int GetHashCode(int value)=>0;
    }
    private sealed class CollidingComparer:IEqualityComparer<int>{
        public bool Equals(int x,int y)=>x==y;
        public int GetHashCode(int value)=>0;
    }
}
