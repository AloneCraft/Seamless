using Seamless.Database;
using Seamless.Collections.Tsavorite;

namespace Test.Database.Compilation;

public sealed class WorkStoreTests{
    private sealed class WorkDatabase(DatabaseContainerOptions options):DatabaseContainer(options){}
    [Fact]public void DatabaseOwnsWorkStoresAndSharesItsBudget(){
        var root=Path.Combine(Path.GetTempPath(),"database-work-store-tests",Guid.NewGuid().ToString("N"));
        using var database=new WorkDatabase(DatabaseContainerOptions.InMemory with{TemporaryDirectory=root});
        var first=database.CreateWorkStore();var firstDirectory=first.DirectoryPath!;
        var data=first.CreateTsavorite<int,long>();using(var rows=data.Create()){rows[1]=10;rows[1]=20;Assert.Equal(20,rows[1]);}
        Assert.True(database.ResourceUsage.MemoryBytes>0);
        first.Dispose();Assert.False(Directory.Exists(firstDirectory));Assert.Equal(0,database.ResourceUsage.MemoryBytes);
        var second=database.CreateWorkStore();var secondDirectory=second.DirectoryPath!;
        database.Dispose();Assert.False(Directory.Exists(secondDirectory));second.Dispose();
        Assert.Equal(0,database.ResourceUsage.MemoryBytes);Assert.Equal(0,database.ResourceUsage.TemporaryStorageBytes);
        Assert.Throws<ObjectDisposedException>(()=>database.CreateWorkStore());
    }
}
