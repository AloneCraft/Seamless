using System.Reflection;
using Seamless.Execution;
using Seamless.Collections.Tsavorite;
using StoreType=Seamless.Execution.Store;
using ScopeType=Seamless.Execution.ExecutionScope;

namespace Execution.Tests;

public sealed class WorkStoreOperations{
    private static string Root(){
        var path=Path.Combine(Path.GetTempPath(),"seamless-work-store-tests",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);return path;
    }
    private static ScopeType Scope(string root)=>new(new(){TemporaryDirectory=root,MemoryLimitBytes=64L<<20,TemporaryStorageLimitBytes=1L<<30});
    private static IEnumerable<IDisposable> Owned(ScopeType scope)=>(IEnumerable<IDisposable>)typeof(ScopeType).GetField("resources",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(scope)!;
    private readonly record struct Entry(long Key,long Value):Seamless.Collections.IRecord<long>;

    [Fact]public void AnonymousCollectionsAreIndependentAndReleaseTheirReservations(){
        using var scope=Scope(Root());
        using(var work=scope.CreateWorkStore()){
            Assert.Equal(StoreKind.Temporary,work.Kind);
            Assert.Same(scope.Budget,work.Budget.Parent);
            Assert.Equal(scope.Budget.MemoryLimitBytes,work.Limits.MemoryBytes);
            Assert.Equal(scope.Budget.TemporaryStorageLimitBytes,work.Limits.DiskBytes);
            var first=work.CreateTsavorite<int,long>();var second=work.CreateTsavorite<int,long>();
            using(var left=first.Create()){using var right=second.Create();left[1]=100;right[1]=200;Assert.Equal(100,left[1]);Assert.Equal(200,right[1]);}
            var set=work.CreateTsavoriteHashSet<int>();
            using(var values=set.Create()){
                Assert.True(values.Add(7));var found=values.Contains(7);Assert.True(found);
            }
            var keyed=work.CreateTsavoriteSet<long,Entry>();
            using(var values=keyed.Create()){
                Assert.True(values.Add(new(7,70)));
                // 列挙によるAssert.Containsではなく、公開Containsのレコード全体比較を検査する。
                var matching=values.Contains(new(7,70));var different=values.Contains(new(7,71));
                Assert.True(matching);Assert.False(different);
            }
            Assert.True(scope.Budget.MemoryBytes>0);
        }
        Assert.Equal(0,scope.Budget.MemoryBytes);Assert.Equal(0,scope.Budget.TemporaryStorageBytes);Assert.Empty(Owned(scope));
    }
    [Fact]public void WorkStoresShareTheParentBudget(){
        using var scope=new ScopeType(new(){TemporaryDirectory=Root(),MemoryLimitBytes=128,TemporaryStorageLimitBytes=256});
        using var first=scope.CreateWorkStore();using var second=scope.CreateWorkStore();
        using(var memory=first.Budget.ReserveMemory(80)){
            Assert.Equal(80,scope.Budget.MemoryBytes);
            Assert.Throws<ResourceLimitException>(()=>second.Budget.ReserveMemory(64));
        }
        using(var disk=first.Budget.ReserveTemporaryStorage(200)){
            Assert.Equal(200,scope.Budget.TemporaryStorageBytes);
            Assert.Throws<ResourceLimitException>(()=>second.Budget.ReserveTemporaryStorage(64));
        }
        Assert.Equal(0,scope.Budget.MemoryBytes);Assert.Equal(0,scope.Budget.TemporaryStorageBytes);
    }
    [Fact]public void ParentDisposalClosesAnUndisposedWorkStore(){
        var scope=Scope(Root());var work=scope.CreateWorkStore();var directory=work.DirectoryPath!;
        var data=work.CreateTsavorite<int,long>();using(var rows=data.Create()){rows[1]=42;}
        scope.Dispose();scope.Dispose();work.Dispose();
        Assert.False(Directory.Exists(directory));Assert.Equal(0,scope.Budget.MemoryBytes);Assert.Equal(0,scope.Budget.TemporaryStorageBytes);
        Assert.Throws<ObjectDisposedException>(()=>work.GetComponentDirectory("after-dispose"));
        Assert.Throws<ObjectDisposedException>(()=>scope.CreateWorkStore());
    }
    [Fact]public void FailedAndCancelledCreationLeaveNoOwnedStore(){
        var root=Root();using var cancellation=new CancellationTokenSource();
        using var scope=new ScopeType(new(){TemporaryDirectory=root,CancellationToken=cancellation.Token});
        Assert.Throws<ArgumentOutOfRangeException>(()=>scope.CreateWorkStore(new(){MemoryBytes=0}));
        Assert.Empty(Directory.EnumerateFileSystemEntries(root));Assert.Empty(Owned(scope));
        cancellation.Cancel();Assert.Throws<OperationCanceledException>(()=>scope.CreateWorkStore());
        Assert.Empty(Directory.EnumerateFileSystemEntries(root));Assert.Empty(Owned(scope));
    }
    [Fact]public void PersistentStoresKeepTheirNamedEntry(){
        using var store=StoreType.Open(Root());
        Assert.Throws<InvalidOperationException>(()=>store.CreateTsavorite<int,long>());
        Assert.Throws<InvalidOperationException>(()=>store.CreateTsavoriteHashSet<int>());
        Assert.Throws<InvalidOperationException>(()=>store.CreateTsavoriteSet<long,Entry>());
        var data=store.CreateTsavorite<int,long>("named");using var rows=data.Create();rows[1]=42;Assert.Equal(42,rows[1]);
    }
    [Fact]public async Task ExplicitAndParentDisposalCanFinishTogether(){
        for(var iteration=0;iteration<20;iteration++){
            var scope=Scope(Root());var work=scope.CreateWorkStore();var directory=work.DirectoryPath!;
            await Task.WhenAll(Task.Run(work.Dispose),Task.Run(scope.Dispose)).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(Directory.Exists(directory));Assert.Empty(Owned(scope));
        }
    }
}
