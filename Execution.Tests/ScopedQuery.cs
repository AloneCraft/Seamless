using Seamless.Execution;
using Seamless.Linq;
using Enumerable=System.Linq.Enumerable;
using Scope=Seamless.Execution.ExecutionScope;
namespace Execution.Tests;
public sealed class ScopedQuery{
    [Fact]public void Query(){
        using var scope=new Scope();
        var query=from number in scope.Query(Enumerable.Range(0,20)) where number%2==0 select number*3;
        Assert.Equal(10,query.LongCount());
        Assert.Equal(0,query.First());
        using(var iterator=query.GetEnumerator()){
            Assert.True(iterator.MoveNext());
            Assert.Throws<InvalidOperationException>(()=>query.Count());
        }
        Assert.Equal(10,query.Count());
        scope.Dispose();
        Assert.Throws<ObjectDisposedException>(()=>query.Count());
    }
    [Fact]public void GroupBy(){
        foreach(var limit in new[]{32768L,16L*1024*1024}){
            using var scope=new Scope(new(){MemoryLimitBytes=limit,TemporaryStorageLimitBytes=16*1024*1024});
            var input=Enumerable.Range(0,4000).Select(x=>new{Key=x%5,Value=x});
            var groups=System.Linq.Enumerable.ToArray(scope.Query(input).GroupBy(x=>x.Key,x=>x.Value));
            Assert.Equal([0,1,2,3,4],groups.Select(x=>x.Key));
            foreach(var group in groups){Assert.Equal(Enumerable.Range(0,4000).Where(x=>x%5==group.Key),group);}
            Assert.InRange(scope.Budget.PeakMemoryBytes,1,limit);
            if(limit==32768){Assert.True(scope.Budget.PeakTemporaryStorageBytes>0);}
            else{Assert.Equal(0,scope.Budget.PeakTemporaryStorageBytes);}
            scope.Dispose();
            Assert.Throws<ObjectDisposedException>(()=>System.Linq.Enumerable.Count(groups[0]));
        }
        {
            using var scope=new Scope(new(){MemoryLimitBytes=32768});
            var groups=System.Linq.Enumerable.ToArray(scope.Query(Enumerable.Range(0,256)).GroupBy(x=>x,x=>x,new CollisionComparer()));
            Assert.Equal(256,groups.Length);
            for(var i=0;i<groups.Length;i++){Assert.Equal(i,groups[i].Key);Assert.Equal(i,Assert.Single(groups[i]));}
        }
    }
    [Fact]public void Join(){
        using var scope=new Scope(new(){MemoryLimitBytes=32768});
        var outer=Enumerable.Range(0,40).ToArray();var inner=Enumerable.Range(0,2000).ToArray();
        var expected=outer.Join(inner,x=>x%5,x=>x%5,(x,y)=>(x,y));
        var query=scope.Query(outer).Join(inner,x=>x%5,x=>x%5,(x,y)=>(x,y));
        Assert.Equal(expected,query);
        Assert.Equal(0,scope.Budget.MemoryBytes);
        Assert.Equal(0,scope.Budget.TemporaryStorageBytes);
    }
    [Fact]public void Distinct(){
        {
            using var scope=new Scope(new(){MemoryLimitBytes=32768});
            var input=new[]{0f,BitConverter.Int32BitsToSingle(unchecked((int)0x80000000)),0f};
            Assert.Equal(2,scope.Query(input).Distinct().Count());
            Assert.Equal(1,scope.Query(input).Distinct(EqualityComparer<float>.Default).Count());
        }
        {
            using var scope=new Scope(new(){MemoryLimitBytes=32768});
            string?[] input=[null,"A","a",null,"A"];
            Assert.Equal(new string?[]{null,"A","a"},scope.Query(input).Distinct());
            Assert.Equal(new string?[]{null,"A"},scope.Query(input).Distinct(StringComparer.OrdinalIgnoreCase));
            Assert.Equal([2,4],scope.Query(new[]{1,2,2,3,4}).Intersect(new[]{2,4}));
            Assert.Equal([1,3],scope.Query(new[]{1,2,2,3,4}).Except(new[]{2,4}));
        }
        {
            using var scope=new Scope(new(){MemoryLimitBytes=32768});
            var objects=new[]{new object(),new object()};
            Assert.Equal(2,scope.Query(objects).Distinct(ReferenceEqualityComparer.Instance).Count());
        }
    }
    [Fact]public void ScopedByteEquality_DoesNotChangeFLinqSystemEquality(){
        var input=new[]{0f,BitConverter.Int32BitsToSingle(unchecked((int)0x80000000)),0f};
        Assert.Equal(1,FLinqDistinctCount(input));
        using var scope=new Scope(new(){MemoryLimitBytes=32768});
        Assert.Equal(2,scope.Query(input).Distinct().Count());
    }
    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int FLinqDistinctCount(float[] input)=>input.Distinct().Count();
    [Fact]public void ToStorage(){
        using var scope=new Scope(new(){MemoryLimitBytes=32768});
        using var storage=scope.Query(Enumerable.Range(0,10000)).Select(x=>x*2).ToStorage();
        Assert.Equal(10000,storage.LongCount);
        Assert.Equal(Enumerable.Range(0,10000).Select(x=>x*2),storage);
        Assert.Throws<ResourceLimitException>(()=>scope.Query(Enumerable.Range(0,10000)).ToArray());
        Assert.InRange(scope.Budget.PeakMemoryBytes,1,32768);
    }
    [Fact]public void Where(){
        using var cancellation=new CancellationTokenSource();
        using var scope=new Scope(new(){CancellationToken=cancellation.Token});
        var query=scope.Query(Enumerable.Range(0,1000)).Where(x=>{if(x==42){cancellation.Cancel();}return false;});
        Assert.Throws<OperationCanceledException>(()=>query.Count());
    }
    private sealed class CollisionComparer:IEqualityComparer<int>{
        public bool Equals(int x,int y)=>x==y;
        public int GetHashCode(int value)=>0;
    }
    [Fact]public void OrderBy(){
        {
            using var scope=new Scope(new(){MemoryLimitBytes=32768,TemporaryStorageLimitBytes=32*1024*1024});
            var input=Enumerable.Range(0,1000).Select(i=>new{Key=i%7,Position=i}).ToArray();
            var query=scope.Query(input).OrderBy(x=>x.Key).ThenByDescending(x=>x.Position%3);
            Assert.Equal(input.OrderBy(x=>x.Key).ThenByDescending(x=>x.Position%3),System.Linq.Enumerable.ToArray(query));
            Assert.True(scope.Budget.PeakTemporaryStorageBytes>0);
            Assert.InRange(scope.Budget.PeakMemoryBytes,1,32768);
            Assert.Equal(0,scope.Budget.TemporaryStorageBytes);
            Assert.Equal(0,scope.Budget.MemoryBytes);
        }
        {
            using var scope=new Scope(new(){MemoryLimitBytes=32768});
            var calls=0;
            var query=scope.Query(Enumerable.Range(0,1000)).OrderBy(x=>{calls++;return-x;});
            using(var iterator=query.GetEnumerator()){Assert.True(iterator.MoveNext());Assert.Equal(999,iterator.Current);}
            Assert.Equal(1000,calls);
            Assert.Equal(0,scope.Budget.MemoryBytes);
            Assert.Equal(0,scope.Budget.TemporaryStorageBytes);
        }
    }
}
