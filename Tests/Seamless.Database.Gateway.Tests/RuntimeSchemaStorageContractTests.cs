using MemoryPack;
using Seamless.Database;

namespace Seamless.Database.Gateway.Tests;

[MemoryPackable]public partial record struct RuntimeSchemaKey(int ObjectId);
[MemoryPackable]public partial record struct RuntimeSchemaRecord(RuntimeSchemaKey Key,string Name,int Version);
[MemoryPackable]public partial record struct RuntimeDataKey(int ObjectId,int RowId);
[MemoryPackable]public partial record struct RuntimeDataRecord(RuntimeDataKey Key,byte[] Payload);

/// <summary>論理カタログと可変行を、同一の既存 SharedV2 取引へ事前登録する試験専用のコンテナ。</summary>
internal sealed class RuntimeSchemaFixture(DatabaseContainerOptions options):DatabaseContainer(options){
    public Table<RuntimeSchemaKey,RuntimeSchemaRecord> Catalog=>this.Table<RuntimeSchemaKey,RuntimeSchemaRecord>(static item=>item.Key,"runtime.catalog");
    public Table<RuntimeDataKey,RuntimeDataRecord> Rows=>this.Table<RuntimeDataKey,RuntimeDataRecord>(static item=>item.Key,"runtime.rows");
    protected override void EnsureTransactionSchema(){_=this.Catalog;_=this.Rows;}
}

[MemoryPackable]public partial record struct RuntimeTypedKey(int ObjectId);
[MemoryPackable]public partial record struct RuntimeTypedRecord(int ObjectId,string Name,int Version);

internal static class DatabaseGenerator;

internal abstract class RuntimePreRegisteredContainer:DatabaseContainer{
    private readonly Table<RuntimeSchemaKey,RuntimeSchemaRecord> catalog;
    private readonly Table<RuntimeDataKey,RuntimeDataRecord> rows;
    protected RuntimePreRegisteredContainer(DatabaseContainerOptions options):base(options){
        this.RootReadyDuringIdentity=this is RuntimeGeneratedFixture root&&root.RootReady;
        this.IdentityInBaseConstructor=this.TransactionCatalogIdentity;
        this.catalog=this.Table<RuntimeSchemaKey,RuntimeSchemaRecord>(static item=>item.Key,"~runtime.catalog.v1");
        this.rows=this.Table<RuntimeDataKey,RuntimeDataRecord>(static item=>item.Key,"~runtime.rows.v1");
    }
    public bool RootReadyDuringIdentity{get;}
    public string IdentityInBaseConstructor{get;}
    public string CurrentIdentity()=>this.TransactionCatalogIdentity;
    public Table<RuntimeSchemaKey,RuntimeSchemaRecord> RuntimeCatalog()=>this.catalog;
    public Table<RuntimeDataKey,RuntimeDataRecord> RuntimeRows()=>this.rows;
}

internal sealed partial class RuntimeGeneratedFixture:RuntimePreRegisteredContainer{
    public RuntimeGeneratedFixture(DatabaseContainerOptions options):base(options){this.RootReady=true;}
    public bool RootReady{get;}
    public Table<RuntimeTypedKey,RuntimeTypedRecord> TypedOrders=>this.Table<RuntimeTypedKey,RuntimeTypedRecord>(static item=>new(item.ObjectId),"TypedOrders");
}

internal sealed class RuntimeGeneratedImport(RuntimeTypedRecord record):IDatabaseImportSource{
    public IEnumerable<TRecord> ReadTable<TRecord>(global::Seamless.Database.System.Keys.DatabaseObject key){
        if(key.Name!="TypedOrders"||typeof(TRecord)!=typeof(RuntimeTypedRecord)){throw new InvalidOperationException("Unexpected import table.");}
        yield return (TRecord)(object)record;
    }
}


public sealed class RuntimeSchemaStorageContractTests{
    [Fact,Trait("Category","Integration")]
    public void GeneratedRoot_PreRegisteredBackingAndTypedImportShareCommitRollbackAndRecovery(){
        var testRoot=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT");
        if(string.IsNullOrWhiteSpace(testRoot)||!Path.IsPathFullyQualified(testRoot)){throw new InvalidOperationException("SEAMLESS_TEST_DATA_ROOT must be an absolute D: or F: test directory.");}
        var root=Path.GetFullPath(testRoot);if(!root.StartsWith("D:\\",StringComparison.OrdinalIgnoreCase)&&!root.StartsWith("F:\\",StringComparison.OrdinalIgnoreCase)){throw new InvalidOperationException("The test directory must be on D: or F:.");}
        var path=Path.Combine(root,"runtime-generated-contract-"+Guid.NewGuid().ToString("N"));
        var options=DatabaseContainerOptions.OnStorage(path) with{Transactions=DatabaseTransactionMode.SharedV2,IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20,TemporaryDirectory=Path.Combine(path,"scratch")};
        var typedKey=new RuntimeTypedKey(5);var catalogKey=new RuntimeSchemaKey(17);var rowKey=new RuntimeDataKey(17,1);
        var typed=new RuntimeTypedRecord(typedKey.ObjectId,"typed",1);var catalog=new RuntimeSchemaRecord(catalogKey,"dynamic",1);var row=new RuntimeDataRecord(rowKey,[1,2,3]);
        using(var database=new RuntimeGeneratedFixture(options)){
            Assert.False(database.RootReadyDuringIdentity);Assert.True(database.RootReady);Assert.Contains("RuntimeGeneratedFixture|typed-v2",database.IdentityInBaseConstructor);Assert.Equal(database.IdentityInBaseConstructor,database.CurrentIdentity());
            Assert.Equal(typeof(RuntimeGeneratedFixture),typeof(RuntimeGeneratedFixture).GetMethod("EnsureTransactionSchema",global::System.Reflection.BindingFlags.Instance|global::System.Reflection.BindingFlags.NonPublic)!.DeclaringType);
            Assert.True(database.DatabaseObjects.ContainsKey(new("TypedOrders")));Assert.False(database.DatabaseObjects.ContainsKey(new("~runtime.catalog.v1")));Assert.False(database.DatabaseObjects.ContainsKey(new("~runtime.rows.v1")));
            var tables=new Seamless.Database.Gateway.Hosting.InProcessDatabaseAdapter(database).GetTables();Assert.Single(tables);Assert.Equal("TypedOrders",tables[0].TableName);
            database.EnsureTablesCreated();Assert.Throws<InvalidOperationException>(()=>database.ImportTables(new RuntimeGeneratedImport(typed)));
            using(var transaction=database.BeginTransaction()){database.ImportTables(new RuntimeGeneratedImport(typed));Assert.True(database.RuntimeCatalog().Add(catalog));Assert.True(database.RuntimeRows().Add(row));transaction.Commit();}
            using(var transaction=database.BeginTransaction()){database.TypedOrders[typedKey]=new(typedKey.ObjectId,"typed-rollback",2);database.RuntimeCatalog()[catalogKey]=new(catalogKey,"dynamic-rollback",2);database.RuntimeRows()[rowKey]=new(rowKey,[9]);transaction.Rollback();}
            using var read=database.BeginReadSession();Assert.Equal(typed,database.TypedOrders[typedKey]);Assert.Equal(catalog,database.RuntimeCatalog()[catalogKey]);Assert.Equal(row.Payload,database.RuntimeRows()[rowKey].Payload);
        }
        using(var reopened=new RuntimeGeneratedFixture(options)){
            Assert.Equal(reopened.IdentityInBaseConstructor,reopened.CurrentIdentity());using var read=reopened.BeginReadSession();
            Assert.Equal(typed,reopened.TypedOrders[typedKey]);Assert.Equal(catalog,reopened.RuntimeCatalog()[catalogKey]);Assert.Equal(row.Payload,reopened.RuntimeRows()[rowKey].Payload);
            Assert.Single(reopened.TypedOrders);Assert.Single(reopened.RuntimeCatalog());Assert.Single(reopened.RuntimeRows());
        }
    }

    [Fact,Trait("Category","Integration")]
    public async Task SharedV2_CatalogAndRowsCommitRollbackAndReopenTogether(){
        var testRoot=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT");
        if(string.IsNullOrWhiteSpace(testRoot)||!Path.IsPathFullyQualified(testRoot)){throw new InvalidOperationException("SEAMLESS_TEST_DATA_ROOT must be an absolute D: or F: test directory.");}
        var root=Path.GetFullPath(testRoot);
        if(!root.StartsWith("D:\\",StringComparison.OrdinalIgnoreCase)&&!root.StartsWith("F:\\",StringComparison.OrdinalIgnoreCase)){throw new InvalidOperationException("The test directory must be on D: or F:.");}
        var path=Path.Combine(root,"runtime-schema-contract-"+Guid.NewGuid().ToString("N"));
        var options=DatabaseContainerOptions.OnStorage(path) with{Transactions=DatabaseTransactionMode.SharedV2,IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20,TemporaryDirectory=Path.Combine(path,"scratch")};
        var objectKey=new RuntimeSchemaKey(17);var rowKey=new RuntimeDataKey(17,1);
        var firstSchema=new RuntimeSchemaRecord(objectKey,"orders",1);var firstRow=new RuntimeDataRecord(rowKey,[1,2,3]);
        using(var database=new RuntimeSchemaFixture(options)){
            Task<(RuntimeSchemaRecord Schema,RuntimeDataRecord Row)> reader;
            {
                var started=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                using(var transaction=database.BeginTransaction()){
                    Assert.True(database.Catalog.Add(firstSchema));Assert.True(database.Rows.Add(firstRow));
                    Assert.Equal(firstSchema,database.Catalog[objectKey]);Assert.Equal(firstRow.Payload,database.Rows[rowKey].Payload);
                    reader=StartRead(database,objectKey,rowKey,started);
                    await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
                    Assert.NotSame(reader,await Task.WhenAny(reader,Task.Delay(200)));
                    transaction.Commit();
                }
                var observed=await reader.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.Equal(firstSchema,observed.Schema);Assert.Equal(firstRow.Payload,observed.Row.Payload);
            }
            {
                var started=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                using(var transaction=database.BeginTransaction()){
                    database.Catalog[objectKey]=new(objectKey,"orders_changed",2);database.Rows[rowKey]=new(rowKey,[9,9]);
                    Assert.Equal(2,database.Catalog[objectKey].Version);Assert.Equal(new byte[]{9,9},database.Rows[rowKey].Payload);
                    reader=StartRead(database,objectKey,rowKey,started);
                    await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
                    Assert.NotSame(reader,await Task.WhenAny(reader,Task.Delay(200)));
                    transaction.Rollback();
                }
                var observed=await reader.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.Equal(firstSchema,observed.Schema);Assert.Equal(firstRow.Payload,observed.Row.Payload);
            }
        }
        using(var reopened=new RuntimeSchemaFixture(options)){
            using var read=reopened.BeginReadSession();
            Assert.Equal(firstSchema,reopened.Catalog[objectKey]);Assert.Equal(firstRow.Payload,reopened.Rows[rowKey].Payload);
            Assert.Single(reopened.Catalog);Assert.Single(reopened.Rows);
        }
    }
    private static Task<(RuntimeSchemaRecord Schema,RuntimeDataRecord Row)>StartRead(RuntimeSchemaFixture database,RuntimeSchemaKey objectKey,RuntimeDataKey rowKey,TaskCompletionSource started){
        using(ExecutionContext.SuppressFlow()){
            return Task.Run(()=>{started.SetResult();using var read=database.BeginReadSession();return(database.Catalog[objectKey],database.Rows[rowKey]);});
        }
    }
}
