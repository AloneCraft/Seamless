using System.Runtime.Loader;
using Seamless.Database;
using Seamless.Management;
using Seamless.Management.Server;
using Seamless.Transactions;

namespace Management.Tests;

public sealed class NativeRelationScenarios{
    internal const string Composite="""
        using Seamless.Database;
        namespace Shapes{
        internal static partial class DatabaseGenerator;
        public sealed partial class Db(DatabaseContainerOptions options):DatabaseContainer(options){
          [ForeignKey("AB","Bs",new[]{"Tenant","Other"},new[]{"Tenant","Id"})]
          public Table<Keys.Row,Tables.Row> As=>this.Table<Keys.Row,Tables.Row>(row=>new(row.Tenant,row.Id));
          [ForeignKey("BC","Nested.Cs",new[]{"Tenant","Other"},new[]{"Tenant","Id"})]
          public Table<Keys.Row,Tables.Row> Bs=>this.Table<Keys.Row,Tables.Row>(row=>new(row.Tenant,row.Id));
          public Schema Nested=>new(this);
        }
        public sealed partial class Schema(DatabaseContainer db):DatabaseSchema(db,"Nested"){
          [ForeignKey("CA","As",new[]{"Tenant","Other"},new[]{"Tenant","Id"})]
          public Table<Keys.Row,Tables.Row> Cs=>this.Table<Keys.Row,Tables.Row>(row=>new(row.Tenant,row.Id));
        }
        namespace Tables{public partial struct Row{[Key] public partial int Tenant{get;}[Key] public partial int Id{get;}public partial int Other{get;}}}}
        """;
    private static DatabaseContainerOptions Options()=>new(){IndexSize=1024,PageSize=1<<20,SegmentSize=1<<24,MemorySize=1<<24,TemporaryDirectory=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,Guid.NewGuid().ToString("N"))};
    private static (AssemblyLoadContext Context,System.Reflection.Assembly Assembly) Load(string source){var compiled=new RuntimeCompiler().Compile(new("1",[new("shape.cs",source)],[]));Assert.DoesNotContain(compiled.Result.Diagnostics,item=>item.Severity=="Error");var context=new AssemblyLoadContext("Shape_"+Guid.NewGuid(),true);return(context,context.LoadFromStream(new MemoryStream(compiled.AssemblyBytes)));}
    [Fact]public void CompositeThreeCycleAndSavepoint(){
        var loaded=Load(Composite);try{
            var options=Options();
            using var database=(DatabaseContainer)Activator.CreateInstance(loaded.Assembly.GetType("Shapes.Db")!,options)!;dynamic db=database;
            dynamic row(int id,int other,int tenant=3)=>Activator.CreateInstance(loaded.Assembly.GetType("Shapes.Tables.Row")!,Activator.CreateInstance(loaded.Assembly.GetType("Shapes.Keys.Row")!,tenant,id),other)!;
            dynamic key(int id)=>Activator.CreateInstance(loaded.Assembly.GetType("Shapes.Keys.Row")!,3,id)!;
            using(var tx=database.BeginTransaction()){db.As.Add(row(1,2));db.Bs.Add(row(2,3));db.Nested.Cs.Add(row(3,1));tx.Commit();}
            using(var tx=database.BeginTransaction()){tx.CreateSavepoint("unchanged");db.Nested.Cs.Remove(key(3));tx.RollbackToSavepoint("unchanged");tx.Commit();}
            Assert.Throws<ForeignKeyViolationException>(()=>db.As.Add(row(8,2,4)));
            using(var tx=database.BeginTransaction()){Assert.True((bool)db.As.Replace(key(1),row(7,2)));Assert.True((bool)db.Nested.Cs.Replace(key(3),row(3,7)));tx.Commit();}
            using(var tx=database.BeginTransaction()){db.As.Remove(key(7));db.Bs.Remove(key(2));db.Nested.Cs.Remove(key(3));tx.Commit();}
            Assert.Equal(0,(int)db.As.Count+(int)db.Bs.Count+(int)db.Nested.Cs.Count);
        }finally{loaded.Context.Unload();}
    }
    [Fact]public async Task ConcurrentChildInsertPreventsParentDelete(){
        var loaded=Load(NativeRelations.Source.Replace("\"AB\",\"Bs\"","\"AB\",\"As\""));try{
            var options=Options() with{TransactionConcurrency=TransactionConcurrencyMode.Optimistic,TransactionHistory=new(){MaxRetainedMemoryBytes=4<<20,MaxRetainedStorageBytes=4<<20,MaxMetadataBytes=4<<20,MaintenanceReserveMemoryBytes=1<<20,MaintenanceReserveStorageBytes=4<<20,MaintenanceWaitTimeout=TimeSpan.FromSeconds(5)}};
            using var database=(DatabaseContainer)Activator.CreateInstance(loaded.Assembly.GetType("Relations.Db")!,options)!;dynamic db=database;
            dynamic row(int id,int other)=>Activator.CreateInstance(loaded.Assembly.GetType("Relations.Tables.Row")!,key(id),other)!;
            dynamic key(int id)=>Activator.CreateInstance(loaded.Assembly.GetType("Relations.Keys.Row")!,id)!;
            dynamic seed=await db.BeginTransactionAsync();try{seed.As.Add(row(1,1));await seed.CommitAsync();}finally{await seed.DisposeAsync();}
            dynamic delete=await db.BeginTransactionAsync();try{
                delete.As.Remove(key(1));Task writer;using(ExecutionContext.SuppressFlow()){writer=Task.Run(async()=>{dynamic insert=await db.BeginTransactionAsync();try{insert.As.Add(row(2,1));await insert.CommitAsync();}finally{await insert.DisposeAsync();}});}await writer.WaitAsync(TimeSpan.FromSeconds(10));
                await Assert.ThrowsAsync<TransactionAbortedException>(async()=>{await delete.CommitAsync();});
            }finally{await delete.DisposeAsync();}
            dynamic read=await db.BeginReadOnlyTransactionAsync();try{Assert.True((bool)read.As.ContainsKey(key(1)));Assert.True((bool)read.As.ContainsKey(key(2)));}finally{await read.DisposeAsync();}
        }finally{loaded.Context.Unload();}
    }
    [Fact]public void UnpublishedSnapshotCannotBypassRelations(){
        var loaded=Load(NativeRelations.Source);try{
            using var database=(DatabaseContainer)Activator.CreateInstance(loaded.Assembly.GetType("Relations.Db")!,Options() with{RecordSerialization=DatabaseRecordSerialization.CanonicalV1})!;dynamic db=database;dynamic row=Activator.CreateInstance(loaded.Assembly.GetType("Relations.Tables.Row")!,Activator.CreateInstance(loaded.Assembly.GetType("Relations.Keys.Row")!,1),99)!;
            dynamic snapshot=db.BeginUnpublishedSnapshot();try{snapshot.Insert(db.As,row);Assert.Throws<ForeignKeyViolationException>(()=>{snapshot.Complete();});}finally{snapshot.Dispose();}
        }finally{loaded.Context.Unload();}
    }
    private sealed class ImportSource(Dictionary<string,object[]> rows,bool fail=false):IDatabaseImportSource{
        public IEnumerable<TRecord> ReadTable<TRecord>(Seamless.Database.System.Keys.DatabaseObject key){if(fail&&key.Name=="Bs"){throw new InvalidOperationException("injected import failure");}if(rows.TryGetValue(key.Name,out var values)){return values.Cast<TRecord>();}return [];}
    }
    [Fact]public void GeneratedImportDefersRelationsAcrossAllTables(){
        var loaded=Load(NativeRelations.Source);try{
            var options=Options();using var database=(DatabaseContainer)Activator.CreateInstance(loaded.Assembly.GetType("Relations.Db")!,options)!;dynamic db=database;var type=loaded.Assembly.GetType("Relations.Tables.Row")!;var keyType=loaded.Assembly.GetType("Relations.Keys.Row")!;
            var source=new Dictionary<string,object[]>{{"As",[Activator.CreateInstance(type,Activator.CreateInstance(keyType,1),2)!]},{"Bs",[Activator.CreateInstance(type,Activator.CreateInstance(keyType,2),1)!]}};
            Assert.Throws<InvalidOperationException>(()=>{db.ImportTables(new ImportSource(source,true));});Assert.Equal(0,(int)db.As.Count+(int)db.Bs.Count);
            db.ImportTables(new ImportSource(source));Assert.Equal(1,(int)db.As.Count);Assert.Equal(1,(int)db.Bs.Count);
        }finally{loaded.Context.Unload();}
    }
}
