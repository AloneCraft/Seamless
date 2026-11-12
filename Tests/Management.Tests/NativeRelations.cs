using Seamless.Management;
using Seamless.Management.Server;

namespace Management.Tests;

public sealed class NativeRelations{
    internal const string Source="""
        using System;
        using Seamless.Database;
        namespace Relations{
        internal static partial class DatabaseGenerator;
        public sealed partial class Db(DatabaseContainerOptions options):DatabaseContainer(options){
            [ForeignKey("AB","Bs",new[]{"Other"},new[]{"Id"})]
            public Table<Keys.Row,Records.Row> As=>this.Table<Keys.Row,Records.Row>(row=>new(row.Id));
            [ForeignKey("BA","As",new[]{"Other"},new[]{"Id"})]
            public Table<Keys.Row,Records.Row> Bs=>this.Table<Keys.Row,Records.Row>(row=>new(row.Id));
        }
        namespace Keys{public partial struct Row{public partial int Id{get;}}}
        namespace Records{public partial struct Row{public partial int Id{get;}public partial int Other{get;}}}
        }
        """;
    private static (System.Runtime.Loader.AssemblyLoadContext Context,System.Reflection.Assembly Assembly) Load(){
        var compiled=new RuntimeCompiler().Compile(new("1",[new("relations.cs",Source)],[]));
        Assert.DoesNotContain(compiled.Result.Diagnostics,item=>item.Severity=="Error");
        var context=new System.Runtime.Loader.AssemblyLoadContext("RelationTest_"+Guid.NewGuid(),true);
        return(context,context.LoadFromStream(new MemoryStream(compiled.AssemblyBytes)));
    }
    [Fact]public void CyclesValidateAtFinalCommit(){
        var loaded=Load();try{
            dynamic row(int id,int other)=>Activator.CreateInstance(loaded.Assembly.GetType("Relations.Records.Row")!,id,other)!;
            dynamic key(int id)=>Activator.CreateInstance(loaded.Assembly.GetType("Relations.Keys.Row")!,id)!;
            using var database=(Seamless.Database.DatabaseContainer)Activator.CreateInstance(loaded.Assembly.GetType("Relations.Db")!,Options())!;dynamic db=database;
            using(var tx=database.BeginTransaction()){db.As.Add(row(1,2));db.Bs.Add(row(2,1));tx.Commit();}
            Assert.Throws<Seamless.Database.ForeignKeyViolationException>(()=>{using var tx=database.BeginTransaction();db.As.Add(row(3,99));tx.Commit();});
            Assert.Equal(1,(int)db.As.Count);Assert.Equal(1,(int)db.Bs.Count);
            Assert.Throws<Seamless.Database.ForeignKeyViolationException>(()=>db.Bs.Remove(key(2)));
            using(var tx=database.BeginTransaction()){db.As.Remove(key(1));db.Bs.Remove(key(2));tx.Commit();}
            Assert.Equal(0,(int)db.As.Count+(int)db.Bs.Count);
        }finally{loaded.Context.Unload();}
    }
    [Fact]public async Task AsyncCyclesAndSavepoint(){
        var loaded=Load();try{
            dynamic row(int id,int other)=>Activator.CreateInstance(loaded.Assembly.GetType("Relations.Records.Row")!,id,other)!;
            dynamic key(int id)=>Activator.CreateInstance(loaded.Assembly.GetType("Relations.Keys.Row")!,id)!;
            using var database=(Seamless.Database.DatabaseContainer)Activator.CreateInstance(loaded.Assembly.GetType("Relations.Db")!,Options())!;dynamic db=database;
            dynamic tx=await db.BeginTransactionAsync();try{tx.As.Add(row(1,2));tx.Bs.Add(row(2,1));await tx.CommitAsync();}finally{await tx.DisposeAsync();}
            dynamic invalid=await db.BeginTransactionAsync();try{invalid.Bs.Remove(key(2));await Assert.ThrowsAsync<Seamless.Database.ForeignKeyViolationException>(async()=>{await invalid.CommitAsync();});}finally{await invalid.DisposeAsync();}
            dynamic read=await db.BeginReadOnlyTransactionAsync();try{Assert.Single(((System.Collections.IEnumerable)read.As.Rows).Cast<object>());Assert.Single(((System.Collections.IEnumerable)read.Bs.Rows).Cast<object>());}finally{await read.DisposeAsync();}
        }finally{loaded.Context.Unload();}
    }
    private static Seamless.Database.DatabaseContainerOptions Options()=>Seamless.Database.DatabaseContainerOptions.InMemory with{IndexSize=1024,PageSize=1<<20,MemorySize=1<<24,SegmentSize=1<<24,TemporaryDirectory=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,Guid.NewGuid().ToString("N"))};
    [Fact]public void EmptyDatabaseCompiles(){
        var source="using Seamless.Database;namespace Empty;internal static partial class DatabaseGenerator;public sealed partial class Db(DatabaseContainerOptions options):DatabaseContainer(options);";
        Assert.NotEmpty(new RuntimeCompiler().Compile(new("1",[new("empty.cs",source)],[])).AssemblyBytes);
    }
}
