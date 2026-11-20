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
    [Fact]public async Task CompoundAssignmentDefersSelfReferenceAndMultiTableCycles(){
        const string actions="""
            namespace Relations{
                public static class CompoundWrites{
                    public static void AddToDatabase(Db db,Tables.Row[] rows)=>db.As += rows;
                    public static void AddToTransaction(Db.TypedTransaction tx,Tables.Row[] a,Tables.Row[] b){
                        tx.As += a;
                        tx.Bs += b;
                    }
                }
            }
            """;
        var writable=NativeRelations.Source.Replace(" As=>this.Table<Keys.Row,Tables.Row>(row=>new(row.Id));",
            " As{get=>this.Table<Keys.Row,Tables.Row>(row=>new(row.Id));set=>this.As.Assign(value);}",StringComparison.Ordinal);
        var self=Load(writable.Replace("\"AB\",\"Bs\"","\"AB\",\"As\"",StringComparison.Ordinal)+actions);
        var cycle=Load(writable+actions);
        try{
            using(var database=(DatabaseContainer)Activator.CreateInstance(self.Assembly.GetType("Relations.Db")!,Options())!){
                var rows=Rows(self.Assembly,(1,2),(2,1));
                self.Assembly.GetType("Relations.CompoundWrites")!.GetMethod("AddToDatabase")!.Invoke(null,[database,rows]);
                Assert.Equal(2,(int)((dynamic)database).As.Count);
            }
            using var explicitDatabase=(DatabaseContainer)Activator.CreateInstance(cycle.Assembly.GetType("Relations.Db")!,Options())!;
            dynamic db=explicitDatabase;
            var apply=cycle.Assembly.GetType("Relations.CompoundWrites")!.GetMethod("AddToTransaction")!;
            dynamic successful=await db.BeginTransactionAsync();
            try{
                apply.Invoke(null,[(object)successful,Rows(cycle.Assembly,(1,2)),Rows(cycle.Assembly,(2,1))]);
                await successful.CommitAsync();
            }finally{await successful.DisposeAsync();}
            dynamic failing=await db.BeginTransactionAsync();
            try{
                apply.Invoke(null,[(object)failing,Rows(cycle.Assembly,(3,4)),Rows(cycle.Assembly,(4,99))]);
                await Assert.ThrowsAsync<ForeignKeyViolationException>(async()=>await failing.CommitAsync());
            }finally{await failing.DisposeAsync();}
            Assert.Equal(1,(int)db.As.Count);
            Assert.Equal(1,(int)db.Bs.Count);
        }finally{self.Context.Unload();cycle.Context.Unload();}

        static Array Rows(global::System.Reflection.Assembly assembly,params (int Id,int Other)[] input){
            var row=assembly.GetType("Relations.Tables.Row")!;var key=assembly.GetType("Relations.Keys.Row")!;
            var result=Array.CreateInstance(row,input.Length);
            for(var index=0;index<input.Length;index++){
                result.SetValue(Activator.CreateInstance(row,Activator.CreateInstance(key,input[index].Id),input[index].Other),index);
            }
            return result;
        }
    }
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
    [Fact]public async Task OrderedTableLocksKeepDeferredValidationAndGeneratedNavigationInOneTransaction(){
        var source=NativeRelations.Source+"""
            namespace Relations{
                public static class NavigationProbe{
                    public static int ParentId(Tables.Row row,Db.IReader q)=>row.AB_Parent(q).Id;
                    public static int ChildCount(Tables.Row row,Db.IReader q)=>row.AB_Children(q).Count;
                }
            }
            """;
        var loaded=Load(source);
        try{
            using var database=(DatabaseContainer)Activator.CreateInstance(loaded.Assembly.GetType("Relations.Db")!,Options())!;
            dynamic db=database;
            dynamic Key(int id)=>Activator.CreateInstance(loaded.Assembly.GetType("Relations.Keys.Row")!,id)!;
            dynamic Row(int id,int other)=>Activator.CreateInstance(loaded.Assembly.GetType("Relations.Tables.Row")!,Key(id),other)!;
            var probe=loaded.Assembly.GetType("Relations.NavigationProbe")!;
            var seed=await db.BeginTransactionAsync(new string[0],new[]{"Bs","As"});
            try{
                seed.As.Add(Row(1,2));
                var missing=Assert.Throws<global::System.Reflection.TargetInvocationException>(()=>probe.GetMethod("ParentId")!.Invoke(null,[(object)Row(1,2),(object)seed]));
                Assert.IsType<KeyNotFoundException>(missing.InnerException);
                seed.Bs.Add(Row(2,1));
                Assert.Equal(2,probe.GetMethod("ParentId")!.Invoke(null,[(object)Row(1,2),(object)seed]));
                Assert.Equal(1,probe.GetMethod("ChildCount")!.Invoke(null,[(object)Row(2,1),(object)seed]));
                await seed.CommitAsync();
            }finally{await seed.DisposeAsync();}
            dynamic keep=await db.BeginTransactionAsync(new[]{"Bs"},new[]{"As"});
            try{
                keep.As.Add(Row(3,99));
                keep.As.Remove(Key(3));
                using var cancel=new CancellationTokenSource();
                Task waiter;var started=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                using(ExecutionContext.SuppressFlow()){
                    waiter=Task.Run(async()=>{
                        started.SetResult();
                        dynamic other=await db.BeginTransactionAsync(new string[0],new[]{"Bs"},cancel.Token);
                        await other.DisposeAsync();
                    });
                }
                await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
                cancel.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(async()=>await waiter.WaitAsync(TimeSpan.FromSeconds(10)));
                await keep.CommitAsync();
            }finally{await keep.DisposeAsync();}
            dynamic invalid=await db.BeginTransactionAsync(new string[0],new[]{"Bs"});
            try{
                invalid.Bs.Remove(Key(2));
                await Assert.ThrowsAsync<ForeignKeyViolationException>(async()=>await invalid.CommitAsync());
            }finally{await invalid.DisposeAsync();}
            Assert.True((bool)db.Bs.ContainsKey(Key(2)));
            Assert.False((bool)db.As.ContainsKey(Key(3)));
        }finally{loaded.Context.Unload();}
    }
    [Fact]public void UnpublishedSnapshotCannotBypassRelations(){
        var loaded=Load(NativeRelations.Source);try{
            using var database=(DatabaseContainer)Activator.CreateInstance(loaded.Assembly.GetType("Relations.Db")!,Options() with{RecordSerialization=DatabaseRecordSerialization.CanonicalV1})!;dynamic db=database;dynamic row=Activator.CreateInstance(loaded.Assembly.GetType("Relations.Tables.Row")!,Activator.CreateInstance(loaded.Assembly.GetType("Relations.Keys.Row")!,1),99)!;
            dynamic snapshot=db.BeginUnpublishedSnapshot();try{snapshot.Insert(db.As,row);Assert.Throws<ForeignKeyViolationException>(()=>{snapshot.Complete();});}finally{snapshot.Dispose();}
        }finally{loaded.Context.Unload();}
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ParentDeletionAndChildInsertionSerializeInEitherOrder(bool childFirst){
        var source=NativeRelations.Source.Replace("[ForeignKey(\"BA\",\"As\",new[]{\"Other\"},new[]{\"Id\"})]","",StringComparison.Ordinal);
        var loaded=Load(source);
        try{
            using var database=(DatabaseContainer)Activator.CreateInstance(loaded.Assembly.GetType("Relations.Db")!,Options())!;
            dynamic db=database;
            dynamic Key(int id)=>Activator.CreateInstance(loaded.Assembly.GetType("Relations.Keys.Row")!,id)!;
            dynamic Row(int id,int other)=>Activator.CreateInstance(loaded.Assembly.GetType("Relations.Tables.Row")!,Key(id),other)!;
            db.Bs.Add(Row(2,0));
            dynamic first=await db.BeginTransactionAsync(new string[0],new[]{childFirst?"As":"Bs"});
            try{
                Change(first,childFirst);
                var waiting=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                Task second;
                using(ExecutionContext.SuppressFlow()){
                    second=Task.Run(async()=>{
                        dynamic pending=db.BeginTransactionAsync(new string[0],new[]{childFirst?"Bs":"As"});
                        waiting.SetResult((bool)pending.IsCompleted);
                        dynamic tx=await pending;
                        try{
                            Change(tx,!childFirst);
                            await tx.CommitAsync();
                        }finally{await tx.DisposeAsync();}
                    });
                }
                Assert.False(await waiting.Task.WaitAsync(TimeSpan.FromSeconds(10)));
                await first.CommitAsync();
                await Assert.ThrowsAsync<ForeignKeyViolationException>(async()=>await second.WaitAsync(TimeSpan.FromSeconds(10)));
                Assert.Equal(childFirst,(bool)db.As.ContainsKey(Key(1)));
                Assert.Equal(childFirst,(bool)db.Bs.ContainsKey(Key(2)));
            }finally{await first.DisposeAsync();}

            void Change(dynamic tx,bool insert){
                if(insert){Assert.True((bool)tx.As.Add(Row(1,2)));}
                else{Assert.True((bool)tx.Bs.Remove(Key(2)));}
            }
        }finally{loaded.Context.Unload();}
    }
    [Fact]public void HundredThousandChangesCheckDistinctParentKeysAndScanChildOnce(){
        const string source="""
            using Seamless.Database;
            namespace BulkRelations{
                internal static partial class DatabaseGenerator;
                public sealed partial class Db(DatabaseContainerOptions options):DatabaseContainer(options){
                    public Table<Keys.Parent,Tables.Parent> Parents=>this.Table<Keys.Parent,Tables.Parent>(row=>row.Key);
                    [ForeignKey("FK_Child_Parent","Parents",new[]{"ParentId"},new[]{"Id"})]
                    public Table<Keys.Child,Tables.Child> Children=>this.Table<Keys.Child,Tables.Child>(row=>row.Key);
                    public void Change(int step){
                        if(step==3){this.Parents.Remove(new(1));this.Parents.Remove(new(3));}
                        else{
                            for(var id=0;id<100000;id++){
                                var key=new Keys.Child(id);
                                this.Children[key]=new Tables.Child(key,step==2?2:1,step);
                            }
                            if(step==0){for(var key=1;key<=3;key++){this.Parents.Add(new(new Keys.Parent(key)));}}
                        }
                    }
                }
                namespace Tables{
                    public partial struct Parent{[Key]public partial int Id{get;}}
                    public partial struct Child{[Key]public partial int Id{get;}public partial int ParentId{get;}public partial int Value{get;}}
                }
            }
            """;
        var loaded=Load(source);
        try{
            using var database=(DatabaseContainer)Activator.CreateInstance(loaded.Assembly.GetType("BulkRelations.Db")!,Options() with{MemorySize=64<<20,SegmentSize=64<<20})!;
            var change=database.GetType().GetMethod("Change")!.CreateDelegate<Action<int>>(database);
            void CommitStep(int step){using var transaction=database.BeginTransaction();change(step);transaction.Commit();}
            var counts=new List<RelationValidationWork>();
            database.ObserveRelationValidation=counts.Add;
            CommitStep(0);
            Assert.Equal([RelationValidationWork.ParentLookup],counts);
            counts.Clear();
            CommitStep(1);
            Assert.Empty(counts);
            CommitStep(2);
            Assert.Equal([RelationValidationWork.ParentLookup],counts);
            counts.Clear();
            CommitStep(3);
            Assert.Equal([RelationValidationWork.ChildScan],counts);
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
