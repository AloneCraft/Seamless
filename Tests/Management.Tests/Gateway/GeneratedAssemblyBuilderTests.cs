using System.Runtime.CompilerServices;
using Seamless.Database;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Rdbms;
using Seamless.Database.Query;

namespace Seamless.Database.Gateway.Tests;

public sealed class GeneratedAssemblyBuilderTests{
    [Fact,Trait("Category","Integration")]
    public async Task BuildLoadExecuteAndUnloadWithoutGlobalFormatterRoots(){
        var data=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??throw new InvalidOperationException("Missing isolated test root.");
        var cache=Environment.GetEnvironmentVariable("SEAMLESS_TEST_CACHE_ROOT")??throw new InvalidOperationException("Missing isolated test cache root.");
        var compiler=Compiler(data,cache);
        var catalog=new DatabaseCatalog("typed",[new SchemaCatalog("dbo",[new TableCatalog("Customer",[new ColumnCatalog(0,"Id",new DatabaseType(DatabaseTypeKind.Int32,false,SqlTypeName:"int"),false),new ColumnCatalog(1,"Name",new DatabaseType(DatabaseTypeKind.String,false,MaximumLength:40,SqlTypeName:"nvarchar"),false)],new PrimaryKeyCatalog("pk",[new KeyColumnCatalog("Id",false)]))])]);
        var artifact=await compiler.BuildAsync(catalog);Assert.True(artifact.Generated.IsCollectible);
        if(OperatingSystem.IsWindows()){Assert.DoesNotContain(Path.Combine(cache,"generated","cli"),Environment.GetEnvironmentVariable("Path",EnvironmentVariableTarget.User)??"",StringComparison.OrdinalIgnoreCase);}
        Assert.True(File.Exists(Path.Combine(artifact.LogDirectory,"obj","project.assets.json")));Assert.DoesNotContain("MSB3539",await File.ReadAllTextAsync(Path.Combine(artifact.LogDirectory,"build.stdout.log")));
        var reference=Exercise(artifact,data);
        for(var attempt=0;attempt<10&&reference.IsAlive;attempt++){GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();await Task.Delay(20);}
        Assert.False(reference.IsAlive,"A disposed generation must not remain in a global formatter/type cache.");
    }

    // MemoryPack の生成器は、この試験が読み込んだ MemoryPack と同じ版をパッケージの置き場(NUGET_PACKAGES か利用者の既定)から使う。
    private static string MemoryPackGenerator(){
        var version=global::System.Reflection.CustomAttributeExtensions.GetCustomAttribute<global::System.Reflection.AssemblyInformationalVersionAttribute>(typeof(global::MemoryPack.MemoryPackSerializer).Assembly)?.InformationalVersion.Split('+')[0]??throw new InvalidOperationException("The MemoryPack version is unknown.");
        var packages=Environment.GetEnvironmentVariable("NUGET_PACKAGES")??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".nuget","packages");
        var path=Path.Combine(packages,"memorypack.generator",version,"analyzers","dotnet","cs","MemoryPack.Generator.dll");
        return File.Exists(path)?path:throw new InvalidOperationException("The MemoryPack generator of the loaded version is missing: "+path);
    }
    internal static string[] RuntimeReferences()=>Directory.EnumerateFiles(AppContext.BaseDirectory,"*.dll").Where(path=>{
        var name=Path.GetFileName(path);
        return name is "Runtime.dll" or "Database.dll" or "Client.dll" or "Server.dll" or "Tsavorite.core.dll" or "Garnet.common.dll" or "Tsavorite.devices.AzureStorageDevice.dll"||name.StartsWith("MemoryPack",StringComparison.Ordinal);
    }).ToArray();
    internal static GeneratedAssemblyBuilder Compiler(string data,string cache,bool nativeSerialization=false,bool nativeFLinq=false){
        var configuration=new DirectoryInfo(AppContext.BaseDirectory).Name;
        var generator=Environment.GetEnvironmentVariable("SEAMLESS_TEST_GENERATOR_ASSEMBLY")??Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","Generator",configuration,"Seamless.Database.Generator.dll"));
        var references=RuntimeReferences();
        var analyzers=new List<string>();
        if(nativeSerialization){analyzers.Add(Environment.GetEnvironmentVariable("SEAMLESS_TEST_MEMORYPACK_GENERATOR_ASSEMBLY")??MemoryPackGenerator());}
        if(nativeFLinq){analyzers.Add(Environment.GetEnvironmentVariable("SEAMLESS_TEST_FLINQ_GENERATOR_ASSEMBLY")??Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","Compiler",configuration,"Seamless.FLinq.Generator.dll")));}
        return new(new(Path.Combine(data,"generated"),Path.Combine(cache,"generated"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"dotnet","dotnet.exe"),generator,references){NativeAnalyzerPaths=analyzers});
    }

    [Fact,Trait("Category","Integration")]
    public async Task MigrateTypedRowsIncludesOwnUpdatesAndReopensNewStorage(){
        var data=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??throw new InvalidOperationException("Missing isolated test root.");var cache=Environment.GetEnvironmentVariable("SEAMLESS_TEST_CACHE_ROOT")??throw new InvalidOperationException("Missing isolated test cache root.");var compiler=Compiler(data,cache);
        var oldCatalog=new DatabaseCatalog("migrating",[new SchemaCatalog("dbo",[new TableCatalog("Customer",[new ColumnCatalog(0,"Id",new(DatabaseTypeKind.Int32,false,SqlTypeName:"int"),false),new ColumnCatalog(1,"Name",new(DatabaseTypeKind.String,false,MaximumLength:40,SqlTypeName:"nvarchar"),false),new ColumnCatalog(2,"Removed",new(DatabaseTypeKind.Int32,false,SqlTypeName:"int"),false)],new PrimaryKeyCatalog("pk",[new("Id",false)]))])]);
        var newCatalog=new DatabaseCatalog("migrating",[new SchemaCatalog("dbo",[new TableCatalog("Customer",[new ColumnCatalog(0,"Id",new(DatabaseTypeKind.Int64,false,SqlTypeName:"bigint"),false),new ColumnCatalog(1,"Name",new(DatabaseTypeKind.String,false,MaximumLength:40,SqlTypeName:"nvarchar"),false),new ColumnCatalog(2,"Extra",new(DatabaseTypeKind.Int32,true,SqlTypeName:"int"),false)],new PrimaryKeyCatalog("pk",[new("Id",false)]))])]);
        var oldArtifact=await compiler.BuildAsync(oldCatalog);var newArtifact=await compiler.BuildAsync(newCatalog);
        var path=Path.Combine(data,"typed-migration-"+Guid.NewGuid().ToString("N"));var references=ExerciseMigration(oldArtifact,newArtifact,path);
        for(var attempt=0;attempt<10&&references.Any(static reference=>reference.IsAlive);attempt++){GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();await Task.Delay(20);}
        Assert.All(references,static reference=>Assert.False(reference.IsAlive,"A migration converter must not keep either generation alive."));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static IReadOnlyList<WeakReference> ExerciseMigration(GeneratedAssemblyArtifact oldArtifact,GeneratedAssemblyArtifact newArtifact,string path){
        var sourceOptions=Options(Path.Combine(path,"old"));var targetOptions=Options(Path.Combine(path,"new"));var references=new List<WeakReference>();
        using(var source=oldArtifact.Load(sourceOptions)){
            references.Add(source.UnloadReference);
            using(var target=newArtifact.Load(targetOptions)){
                references.Add(target.UnloadReference);
                var engine=new SqlExecutionEngine(source.Adapter);using var session=new SessionContext(10,source.Adapter.DatabaseName);
                engine.Execute("INSERT dbo.Customer(Id,Name,Removed) VALUES(1,N'original',5)",session);engine.Execute("BEGIN TRANSACTION",session);engine.Execute("UPDATE dbo.Customer SET Name=N'pending' WHERE Id=1",session);
                var plan=new MigrationPlan(1,[new(1,"Id",SqlType.Int,false),new(2,"Name",new("nvarchar",typeof(string),40),false),new(3,"Removed",SqlType.Int,false)],[new(1,"Id",SqlType.BigInt,false),new(2,"Name",new("nvarchar",typeof(string),40),false),new(4,"Extra",SqlType.Int,true)]);
                var tablePath=Assert.Single(source.Generated.Tables).ObjectName;Assert.Equal(1,session.Run(()=>TypedGenerationMigrator.Prepare(source,target,[new(tablePath,tablePath,plan)],new MigrationContext(_=>99))));
                engine.Execute("ROLLBACK TRANSACTION",session);Assert.Equal("original",Assert.Single(engine.Execute("SELECT Name FROM dbo.Customer",session).Rows)[0]);
                var targetEngine=new SqlExecutionEngine(target.Adapter);using var targetSession=new SessionContext(11,target.Adapter.DatabaseName);Assert.Equal(new object?[]{1L,"pending",99},Assert.Single(targetEngine.Execute("SELECT Id,Name,Extra FROM dbo.Customer",targetSession).Rows));
                Assert.Throws<InvalidOperationException>(()=>target.Database.BeginUnpublishedSnapshot());
            }
            using var reopened=newArtifact.Load(targetOptions);references.Add(reopened.UnloadReference);var verify=new SqlExecutionEngine(reopened.Adapter);using var verification=new SessionContext(12,reopened.Adapter.DatabaseName);Assert.Equal(new object?[]{1L,"pending",99},Assert.Single(verify.Execute("SELECT Id,Name,Extra FROM dbo.Customer",verification).Rows));
        }
        return references;
    }

    private static DatabaseContainerOptions Options(string path)=>DatabaseContainerOptions.OnStorage(path) with{IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20,TemporaryDirectory=Path.Combine(path,"tmp")};

    [Fact,Trait("Category","Integration")]
    public async Task WholeDatabaseCopyPreservesKeylessDuplicatesAndInternalRowIds(){
        var data=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??throw new InvalidOperationException("Missing isolated test root.");var cache=Environment.GetEnvironmentVariable("SEAMLESS_TEST_CACHE_ROOT")??throw new InvalidOperationException("Missing isolated test cache root.");var compiler=Compiler(data,cache);
        var catalog=new DatabaseCatalog("keyless",[new SchemaCatalog("dbo",[new TableCatalog("Items",[new ColumnCatalog(0,"Value",new(DatabaseTypeKind.Int32,true,SqlTypeName:"int"),false)],null),new TableCatalog("Unchanged",[new ColumnCatalog(0,"Id",new(DatabaseTypeKind.Int32,false,SqlTypeName:"int"),false)],new PrimaryKeyCatalog("pk",[new("Id",false)]))])]);
        var artifact=await compiler.BuildAsync(catalog);var path=Path.Combine(data,"keyless-migration-"+Guid.NewGuid().ToString("N"));
        using var source=artifact.Load(Options(Path.Combine(path,"old")));using var target=artifact.Load(Options(Path.Combine(path,"new")));var engine=new SqlExecutionEngine(source.Adapter);using var session=new SessionContext(20,source.Adapter.DatabaseName);
        engine.Execute("INSERT dbo.Items(Value) VALUES(7),(7),(NULL)",session);engine.Execute("DELETE dbo.Items WHERE Value IS NULL",session);engine.Execute("INSERT dbo.Unchanged(Id) VALUES(42)",session);
        var migrations=artifact.Generated.Tables.Select((table,index)=>{
            var columns=table.Columns.Select((column,ordinal)=>new MigrationColumn(ordinal+1,column.PropertyName,column.IsSynthetic?SqlType.BigInt:SqlType.Int,!column.IsSynthetic&&table.SourceTable=="Items")).ToArray();return new GeneratedTableMigration(table.ObjectName,table.ObjectName,new(index+1,columns,columns));
        }).ToArray();
        Assert.Equal(3,TypedGenerationMigrator.Prepare(source,target,migrations,new()));
        var verify=new SqlExecutionEngine(target.Adapter);using var verification=new SessionContext(21,target.Adapter.DatabaseName);Assert.Equal(2,Assert.Single(verify.Execute("SELECT COUNT(*) FROM dbo.Items",verification).Rows)[0]);Assert.Equal(42,Assert.Single(verify.Execute("SELECT Id FROM dbo.Unchanged",verification).Rows)[0]);
        var keyless=artifact.Generated.Tables.Single(table=>table.SourceTable=="Items");var rowId=keyless.Columns.Single(column=>column.IsSynthetic).PropertyName;
        long[] Ids(LoadedDatabaseGeneration generation){object table=generation.Database;foreach(var part in keyless.ObjectName.Split('.')){table=table.GetType().GetProperty(part)!.GetValue(table)!;}return ((global::System.Collections.IEnumerable)table).Cast<object>().Select(row=>(long)row.GetType().GetProperty(rowId)!.GetValue(row)!).Order().ToArray();}
        Assert.Equal(new long[]{1,2},Ids(source));Assert.Equal(Ids(source),Ids(target));verify.Execute("INSERT dbo.Items(Value) VALUES(7)",verification);Assert.Equal(new long[]{1,2,3},Ids(target));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference Exercise(GeneratedAssemblyArtifact artifact,string data){
        using var generation=artifact.Load(DatabaseContainerOptions.InMemory with{IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20,TemporaryDirectory=Path.Combine(data,"generation-tmp")});
        Assert.DoesNotContain(generation.Database.GetType().Assembly.GetTypes().SelectMany(static type=>type.GetInterfaces()),static type=>type.FullName?.StartsWith("MemoryPack.IMemoryPackable",StringComparison.Ordinal)==true);
        var registry=new DatabaseRegistry();registry.Register("typed",generation.Adapter);var engine=new SqlExecutionEngine(registry);using var session=new SessionContext(1,"typed");
        engine.Execute("INSERT dbo.Customer(Id,Name) VALUES(1,N'first')",session);
        engine.Execute("UPDATE dbo.Customer SET Name=N'changed' WHERE Id=1",session);
        Assert.Equal(new object?[]{1,"changed"},Assert.Single(engine.Execute("SELECT Id,Name FROM dbo.Customer",session).Rows));
        engine.Execute("DELETE dbo.Customer WHERE Id=1",session);Assert.Empty(engine.Execute("SELECT * FROM dbo.Customer",session).Rows);
        engine.Execute("BEGIN TRANSACTION",session);engine.Execute("INSERT dbo.Customer(Id,Name) VALUES(1,N'pending')",session);engine.Execute("SET XACT_ABORT ON",session);
        var errors=engine.ExecuteBatch("BEGIN TRY INSERT dbo.Customer(Id,Name) VALUES(1,N'duplicate'); END TRY BEGIN CATCH SELECT XACT_STATE() AS state; BEGIN TRY COMMIT TRANSACTION; END TRY BEGIN CATCH SELECT ERROR_NUMBER() AS error; END CATCH; ROLLBACK TRANSACTION; END CATCH",session);Assert.Equal((short)-1,Assert.Single(errors[0].Rows)[0]);Assert.Equal(3930,Assert.Single(errors[1].Rows)[0]);Assert.False(session.IsInTransaction);Assert.Empty(engine.Execute("SELECT * FROM dbo.Customer",session).Rows);
        return generation.UnloadReference;
    }
}
