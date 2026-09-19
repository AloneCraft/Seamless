using Seamless.Execution;
using Scope=Seamless.Execution.ExecutionScope;
namespace Execution.Tests;
public sealed class ExecutionScope{
    [Fact]public void Enter(){
        using var scope=new Scope(new(){MemoryLimitBytes=1024,TemporaryStorageLimitBytes=1024});
        using(var operation=scope.Enter()){
            Assert.Throws<InvalidOperationException>(()=>scope.Enter());
        }
        using(var operation=scope.Enter()){}
        scope.Dispose();
        Assert.Throws<ObjectDisposedException>(()=>scope.Enter());
    }
    [Fact]public void ReserveMemory(){
        using var scope=new Scope(new(){MemoryLimitBytes=1024,TemporaryStorageLimitBytes=1024});
        using(var first=scope.Budget.ReserveMemory(700)){
            Assert.Equal(700,scope.Budget.MemoryBytes);
            Assert.Throws<ResourceLimitException>(()=>scope.Budget.ReserveMemory(400));
            using var second=scope.Budget.ReserveMemory(300);
            Assert.Equal(1000,scope.Budget.MemoryBytes);
        }
        Assert.Equal(0,scope.Budget.MemoryBytes);
        Assert.Equal(1000,scope.Budget.PeakMemoryBytes);
    }
    [Fact]public void CreateTemporaryFile(){
        var directory=Path.Combine(Path.GetTempPath(),"seamless-execution-tests",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try{
            var scope=new Scope(new(){MemoryLimitBytes=1024,TemporaryStorageLimitBytes=10,TemporaryDirectory=directory});
            var file=scope.CreateTemporaryFile();
            file.Write([1,2,3,4]);
            Assert.Equal(4,scope.Budget.TemporaryStorageBytes);
            file.Position=1;file.Write([8]);
            Assert.Equal(4,scope.Budget.TemporaryStorageBytes);
            Assert.Throws<ResourceLimitException>(()=>file.SetLength(11));
            Assert.Equal(4,file.Length);
            file.SetLength(2);
            Assert.Equal(2,scope.Budget.TemporaryStorageBytes);
            scope.Dispose();
            Assert.Equal(0,scope.Budget.TemporaryStorageBytes);
            Assert.Empty(Directory.EnumerateFileSystemEntries(directory));
            Assert.Throws<ObjectDisposedException>(()=>file.ReadByte());
        }finally{Directory.Delete(directory,true);}
    }
    [Fact]public void RecoverAbandoned_IgnoresUntrustedMetadataAndRemovesOwnedDirectory(){
        var root=Path.Combine(Path.GetTempPath(),"seamless-execution-recovery-tests",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try{
            var invalid=Path.Combine(root,"scope-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(invalid);
            File.WriteAllText(Path.Combine(invalid,"owner.json"),"{");
            File.WriteAllText(Path.Combine(invalid,"owner.lock"),string.Empty);
            var validName="scope-"+Guid.NewGuid().ToString("N");
            var valid=Path.Combine(root,validName);
            Directory.CreateDirectory(valid);
            File.WriteAllText(Path.Combine(valid,"owner.json"),global::System.Text.Json.JsonSerializer.Serialize(new{Format="seamless-execution-v1",Id=validName}));
            File.WriteAllText(Path.Combine(valid,"owner.lock"),string.Empty);
            File.WriteAllText(Path.Combine(valid,"data.tmp"),"owned");

            var recovered=TemporaryResourceRecovery.RecoverAbandoned(root);

            Assert.Equal([valid],recovered);
            Assert.False(Directory.Exists(valid));
            Assert.True(Directory.Exists(invalid));
        }finally{Directory.Delete(root,true);}
    }
}
