using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Seamless.Database;
using Seamless.Database.Compilation;
using Seamless.Database.Rdbms;
using Seamless.Database.SqlServer;
using TransferAdapter=Seamless.Database.Transfer.DatabaseTransferAdapter;

namespace Database.Verification.Tests;

public sealed class SqlServerRoundtrip{
    [SqlServerFact,Trait("Category","LiveSqlServer")]public async Task VerifyDataAsync(){
        var run=Guid.NewGuid().ToString("N");
        var sourceName="Seamless_Roundtrip_Source_"+run;
        var targetName="Seamless_Roundtrip_Target_"+run;
        var root=Path.Combine(Path.GetTempPath(),"SqlServerRoundtrip",run);Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root,"owner.json"),JsonSerializer.Serialize(new{Owner=run,SourceDatabase=sourceName,TargetDatabase=targetName,CreatedUtc=DateTime.UtcNow},new JsonSerializerOptions{WriteIndented=true}));
        var connection=new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ConnectionStrings__Source")??"Server=localhost;Database=master;Integrated Security=True;Encrypt=False;TrustServerCertificate=True;Connect Timeout=15"){InitialCatalog="master"};
        await using var master=new SqlConnection(connection.ConnectionString);await master.OpenAsync();
        string dataDirectory;
        await using(var command=master.CreateCommand()){
            command.CommandText="SELECT TOP(1) physical_name FROM sys.master_files WHERE database_id>4 AND type=0 AND (physical_name LIKE 'D:%' OR physical_name LIKE 'G:%') ORDER BY CASE WHEN physical_name LIKE 'G:%' THEN 0 ELSE 1 END,database_id";
            dataDirectory=Path.GetDirectoryName((string?)await command.ExecuteScalarAsync()??throw new InvalidOperationException("A non-C SQL Server data directory is required."))!;
        }
        async Task Execute(SqlConnection target,string sql){await using var command=target.CreateCommand();command.CommandText=sql;await command.ExecuteNonQueryAsync();}
        static string Literal(string text)=>"N'"+text.Replace("'","''",StringComparison.Ordinal)+"'";
        foreach(var name in new[]{sourceName,targetName}){
            var data=Path.Combine(dataDirectory,name+".mdf");var log=Path.Combine(dataDirectory,name+".ldf");
            await Execute(master,"CREATE DATABASE ["+name+"] ON PRIMARY (NAME="+Literal(name+"_data")+",FILENAME="+Literal(data)+",SIZE=8MB,FILEGROWTH=8MB) LOG ON (NAME="+Literal(name+"_log")+",FILENAME="+Literal(log)+",SIZE=8MB,FILEGROWTH=8MB)");
        }
        var sourceConnection=new SqlConnectionStringBuilder(connection.ConnectionString){InitialCatalog=sourceName}.ConnectionString;
        var targetConnection=new SqlConnectionStringBuilder(connection.ConnectionString){InitialCatalog=targetName}.ConnectionString;
        await using var source=new SqlConnection(sourceConnection);await source.OpenAsync();
        await Execute(source,"CREATE TABLE dbo.Items(Id int NOT NULL PRIMARY KEY,Name nvarchar(20) NULL,Amount bigint NOT NULL); CREATE TABLE dbo.Bag(Value int NOT NULL,Name nvarchar(20) NULL); INSERT dbo.Items VALUES(1,N'ABC',10),(2,N'abc',20),(3,NULL,30),(4,N'日本😀',40); INSERT dbo.Bag VALUES(7,N'duplicate'),(7,N'duplicate'),(8,NULL)");
        var catalog=await new SqlServerCatalogReader(sourceConnection,analyzeRoutines:false).ReadAsync();
        var generated=CSharpDatabaseGenerator.GenerateNative(catalog);
        var sources=generated.Files.Select(f=>new NativeSourceFile(f.RelativePath,f.Content)).ToList();
        sources.Add(new("Queries.cs",$$"""
            using System.Collections.Generic;
            using System.Linq;
            namespace {{generated.TypeNamespace}};
            public sealed partial class {{generated.DatabaseName}}{
                public IEnumerable<global::{{generated.TypeNamespace}}.Records.Items>Find(long minimum)=>this.dbo.Items.Where(row=>row.Amount>=minimum).OrderBy(row=>row.Id).Take(5);
                public long Total()=>this.dbo.Items.Sum(row=>row.Amount);
            }
            """));
        var snapshot=NativeCompilationSnapshot.Create(run,sources,[typeof(DatabaseCatalog).Assembly.Location]);
        var compiler=new NativeDatabaseCompiler();var analysis=compiler.Analyze(snapshot,generated.TypeNamespace+"."+generated.DatabaseName);
        Assert.Empty(analysis.Diagnostics);
        var artifact=compiler.Emit(snapshot);Assert.Empty(artifact.Diagnostics);Assert.NotEmpty(artifact.AssemblyBytes);
        await File.WriteAllBytesAsync(Path.Combine(root,"native.dll"),artifact.AssemblyBytes.ToArray());
        var context=new AssemblyLoadContext("Roundtrip_"+run,isCollectible:true);
        try{
            using var bytes=new MemoryStream(artifact.AssemblyBytes.ToArray());var assembly=context.LoadFromStream(bytes);
            var type=assembly.GetType(generated.TypeNamespace+"."+generated.DatabaseName,true)!;
            var options=DatabaseContainerOptions.OnStorage(Path.Combine(root,"native")) with{IndexSize=64<<10,PageSize=64<<10,SegmentSize=1<<20,MemorySize=1<<20,ManagedMemoryLimitBytes=16<<20,TransactionMemoryLimit=4<<20,MaximumQueryWorkers=8};
            using var native=(DatabaseContainer)Activator.CreateInstance(type,[options])!;
            type.GetMethod("EnsureTablesCreated")!.Invoke(native,[]);
            using(var import=new SqlServerImportSource(sourceConnection,generated.Tables,consistentRead:true)){
                type.GetMethod("ImportTables")!.Invoke(native,[import]);import.Complete();
            }
            var exported=new VendorSqlGenerator().Generate(analysis.Database,SqlVendor.SqlServer);Assert.Empty(exported.Diagnostics);
            await File.WriteAllLinesAsync(Path.Combine(root,"generated.sql"),exported.Statements);
            await using var target=new SqlConnection(targetConnection);await target.OpenAsync();
            var adapter=new TransferAdapter();await adapter.ApplyToEmptyDatabaseAsync(target,exported);
            using(var read=native.BeginTransaction()){
                foreach(var table in analysis.Database.Tables){await adapter.WriteRowsAsync(target,SqlVendor.SqlServer,table,TransferAdapter.ReadRowsAsync(native,table));}
                var verifier=new global::Database.Verification.DatabaseConsistencyVerifier();
                var report=await verifier.VerifyDataAsync(source,SqlVendor.SqlServer,target,SqlVendor.SqlServer,analysis.Database,table=>TransferAdapter.ReadRowsAsync(native,table),root);
                Assert.True(report.Equal,JsonSerializer.Serialize(report));Assert.Equal(2,report.Tables.Length);
                var query=Assert.Single(exported.Queries.Where(q=>q.Name=="Find"));
                var result=await verifier.VerifyQueryAsync(source,"SELECT TOP(5) Id,Name,Amount FROM dbo.Items WHERE Amount>=@minimum ORDER BY Id",target,query.Sql,new Dictionary<string,object?>{{"minimum",20L}},root,ordered:true);
                Assert.True(result.Equal);
                var nativeResult=(System.Collections.IEnumerable)type.GetMethod("Find")!.Invoke(native,[20L])!;
                var ids=new List<int>();foreach(var row in nativeResult){Assert.NotNull(row);ids.Add((int)row.GetType().GetProperty("Id")!.GetValue(row)!);}Assert.Equal([2,3,4],ids);
                Assert.Equal(100L,type.GetMethod("Total")!.Invoke(native,[]));
                await File.WriteAllTextAsync(Path.Combine(root,"verification.json"),JsonSerializer.Serialize(new{Owner=run,SourceDatabase=sourceName,TargetDatabase=targetName,Snapshot=snapshot.Fingerprint,Data=report,Query=result},new JsonSerializerOptions{WriteIndented=true}));
            }
        }finally{context.Unload();}
    }
}
