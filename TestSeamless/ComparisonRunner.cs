using System.Diagnostics;
using System.Data.Common;
using System.Text;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using MemoryPack;
using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using Seamless.Database.Client;
using Seamless.Database.Gateway;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Query;
using Seamless.Database.Rdbms;
using TestSeamless.Queries;

namespace TestSeamless;

public sealed record ComparisonCase(string Id,string Database,string Sql,string? NativeQuery,bool Ordered=false,bool ComputedFloatingPoint=false,int[]? OrderKeyOrdinals=null,bool TopOrderUnique=false,int[]? ComputedFloatingPointOrdinals=null);
public static class ComparisonRunner{
    public static async Task ValidateAsync(string directory,string source){
        source=new SqlConnectionStringBuilder(source){ColumnEncryptionSetting=SqlConnectionColumnEncryptionSetting.Disabled}.ConnectionString;
        var generation=ComparisonGeneration.Read(directory);if(generation.State!="Imported"||generation.ModuleManifest is null){throw new InvalidOperationException("Validation requires all imported tables/views and a deployed query module.");}
        var checks=new List<object>();var copy=CopyData(directory,"direct");
        try{
            using var direct=new ComparisonHost(copy,false);using var candidate=new ComparisonHost(directory,false);using var gateway=new GatewayServer(candidate.Databases,new(){TdsPort=14343,HttpPort=8098,NativePort=14345});gateway.Native.Load(Path.Combine(directory,generation.ModuleManifest),generation.ProductVersion);gateway.Start();
            var module=LoadModule(directory);var codec=new NativeComparisonCodec(module);await using var native=new NativeQueryClient(port:14345);
            await GeneratedDatabaseAcceptance.ValidateAsync(directory,generation,source,direct);
            await ValidateWireAsync(directory,generation,source,new SqlQueryEngine(candidate.Databases));
            foreach(var test in ReadCases(directory)){
                var sourceConnection=new SqlConnectionStringBuilder(source){InitialCatalog=test.Database}.ConnectionString;var targetConnection=Target(test.Database,14343);CompareSchema(sourceConnection,targetConnection,test.Sql);var expected=ReadSql(sourceConnection,test.Sql);var target=ReadSql(targetConnection,test.Sql);var comparison=CompareResults(test,expected,target,Path.Combine(directory,"verification-work"));if(!comparison.Equal){throw new InvalidDataException($"TDS result differs: {test.Id}");}
                var status="NotImplemented";
                if(test.NativeQuery is not null){var registration=direct.Databases.Resolve(test.Database);var context=new NativeContext(((InProcessDatabaseAdapter)registration.Adapter).Database,test.Database,CancellationToken.None);var local=module.Execute(test.NativeQuery,codec.Arguments(test.NativeQuery),context).Select(bytes=>codec.Project(test.NativeQuery,bytes));var remote=ReadNative(codec,native,generation.Id,test.NativeQuery,test.Database,registration.SchemaFingerprint);var localResult=CompareResults(test,ReadSql(new SqlConnectionStringBuilder(source){InitialCatalog=test.Database}.ConnectionString,test.Sql),local,Path.Combine(directory,"verification-work"));var remoteResult=CompareResults(test,ReadSql(new SqlConnectionStringBuilder(source){InitialCatalog=test.Database}.ConnectionString,test.Sql),remote,Path.Combine(directory,"verification-work"));if(!localResult.Equal||!remoteResult.Equal){throw new InvalidDataException($"Native result differs: {test.Id}");}status="Passed";}
                checks.Add(new{test.Id,test.Database,Sql="Passed",Native=status});await WriteReportAsync(directory,"validation",new{Generation=generation.Id,Checks=checks,FloatingTolerance=FloatingTolerance.FromEnvironment(),Status="InProgress"});
            }
            await VerifyCommonCrudAsync(new SqlConnectionStringBuilder(source){InitialCatalog="Seamless_Comparison_Fixture"}.ConnectionString);await VerifyCommonCrudAsync(Target("Seamless_Comparison_Fixture",14343));await VerifyNativeFixtureAsync(generation,direct,module,native);await VerifySqlClientAsync(Target("Seamless_Comparison_Fixture",14343));await gateway.StopAsync();
            var proof=Path.Combine(directory,"package-provenance.json");if(!File.Exists(proof)){throw new InvalidDataException("Package provenance has not been verified.");}if(!File.Exists(Path.Combine(directory,"flinq-proof.json"))){throw new InvalidDataException("Build-time FLinq application has not been verified.");}
            await WriteReportAsync(directory,"validation",new{Generation=generation.Id,Checks=checks,FloatingTolerance=FloatingTolerance.FromEnvironment(),Status="Passed"});(generation with{State="Validated"}).Write(directory);
        }catch(Exception ex){await WriteReportAsync(directory,"validation",new{Generation=generation.Id,Checks=checks,Status="Failed",Reason=ex.Message});throw;}
    }
    private static async Task ValidateWireAsync(string directory,ComparisonGeneration generation,string source,SqlQueryEngine engine){
        var checks=new List<object>();
        foreach(var database in generation.Databases){
            var sourceConnection=new SqlConnectionStringBuilder(source){InitialCatalog=database.Name}.ConnectionString;
            var targetConnection=Target(database.Name,14343);
            foreach(var schema in database.Catalog.Schemas){
                var objects=schema.Tables.Select(table=>(Name:table.Name,Kind:"Table")).Concat(schema.Views.Select(view=>(Name:view.Name,Kind:"View")));
                foreach(var item in objects){
                    var sql="SELECT * FROM "+QuoteSqlName(schema.Name)+"."+QuoteSqlName(item.Name);
                    try{
                        CompareSchema(sourceConnection,targetConnection,sql);
                        var floatingColumns=engine.Prepare(sql,database.Name).Columns.Where(column=>column.IsComputedFloatingPoint).Select(column=>column.Ordinal).ToHashSet();var comparison=ResultBag.Compare(ReadSql(sourceConnection,sql),ReadSql(targetConnection,sql),Path.Combine(directory,"verification-work"),floatingColumns.Count>0,computedFloatingPointOrdinals:floatingColumns);
                        checks.Add(new{Database=database.Name,Schema=schema.Name,Object=item.Name,item.Kind,ComputedFloatingPointOrdinals=floatingColumns.Order().ToArray(),comparison.Rows,comparison.Equal,comparison.Reason});
                        await WriteReportAsync(directory,"wire-validation",new{Generation=generation.Id,Checks=checks,Status="InProgress"});
                        if(!comparison.Equal){throw new InvalidDataException($"TDS values differ: {database.Name}.{schema.Name}.{item.Name}: {comparison.Reason}");}
                    }catch(Exception exception){
                        await WriteReportAsync(directory,"wire-validation",new{Generation=generation.Id,Checks=checks,Status="Failed",Object=database.Name+"."+schema.Name+"."+item.Name,Reason=exception.Message});throw;
                    }
                }
            }
        }
        await WriteReportAsync(directory,"wire-validation",new{Generation=generation.Id,Checks=checks,Status="Passed"});
    }
    internal static string QuoteSqlName(string value)=>"["+value.Replace("]","]]")+"]";
    private static async Task VerifyCommonCrudAsync(string connectionString){
        await using var connection=new SqlConnection(connectionString);await connection.OpenAsync();using var command=connection.CreateCommand();command.CommandText="SELECT COUNT_BIG(*) FROM dbo.FixtureParent WHERE Id=-2000000101";if(Convert.ToInt64(await command.ExecuteScalarAsync())!=0){throw new InvalidDataException("Common CRUD fixture sentinel is already present.");}
        var path=Path.Combine(AppContext.BaseDirectory,"Cases","Common","Fixture-CRUD-CommitRollback.sql");var lines=await File.ReadAllLinesAsync(path);command.CommandText=string.Join(Environment.NewLine,lines.Where(line=>!line.Trim().Equals("GO",StringComparison.OrdinalIgnoreCase)&&!line.TrimStart().StartsWith("USE ",StringComparison.OrdinalIgnoreCase)));
        using var reader=await command.ExecuteReaderAsync();var expected=new[]{"updated","comparison fixture"};var index=0;do{if(reader.FieldCount==0){continue;}if(index>=expected.Length||!await reader.ReadAsync()||reader.GetInt32(0)!=-2000000101||reader.GetString(1)!=expected[index]||await reader.ReadAsync()){throw new InvalidDataException("Common CRUD result differs.");}index++;}while(await reader.NextResultAsync());if(index!=expected.Length){throw new InvalidDataException("Common CRUD result sets are missing.");}
    }
    private static async Task VerifyNativeFixtureAsync(ComparisonGeneration generation,ComparisonHost direct,INativeQueryModule module,NativeQueryClient client){
        const string databaseName="Seamless_Comparison_Fixture";const int id=-2000000201;var registration=direct.Databases.Resolve(databaseName);var fingerprint=registration.SchemaFingerprint;var context=new NativeContext(((InProcessDatabaseAdapter)registration.Adapter).Database,databaseName,CancellationToken.None);var original=new NativeFixtureCommand(id,"native fixture");var updated=new NativeFixtureCommand(id,"updated");
        long Direct(string operation,NativeFixtureCommand value){var rows=module.Execute("fixture/"+operation,MemoryPackSerializer.Serialize(value),context).Select(bytes=>MemoryPackSerializer.Deserialize<long>(bytes)).ToArray();if(rows.Length!=1){throw new InvalidDataException("Native direct mutation did not return exactly one count.");}return rows[0];}
        NativeFixtureRow[] DirectRead()=>module.Execute("fixture/read-parent",MemoryPackSerializer.Serialize(original),context).Select(bytes=>MemoryPackSerializer.Deserialize<NativeFixtureRow>(bytes)!).ToArray()!;
        if(DirectRead().Length!=0){throw new InvalidDataException("Native direct sentinel already exists.");}if(Direct("insert-parent",original)!=1||DirectRead().Single().Name!=original.Name||Direct("update-parent",updated)!=1||DirectRead().Single().Name!=updated.Name||Direct("delete-parent",original)!=1||DirectRead().Length!=0){throw new InvalidDataException("Native direct CRUD failed.");}
        async Task<NativeFixtureRow[]> Read(){var rows=new List<NativeFixtureRow>();await foreach(var row in client.QueryAsync<NativeFixtureCommand,NativeFixtureRow>(generation.Id,"fixture/read-parent",databaseName,fingerprint,original)){rows.Add(row);}return rows.ToArray();}
        async Task<long> Mutate(NativeTransaction transaction,string operation,NativeFixtureCommand value){var rows=new List<long>();await foreach(var row in transaction.QueryAsync<NativeFixtureCommand,long>(generation.Id,"fixture/"+operation,fingerprint,value)){rows.Add(row);}if(rows.Count!=1){throw new InvalidDataException("Native mutation did not return exactly one count.");}return rows[0];}
        if((await Read()).Length!=0){throw new InvalidDataException("Native remote sentinel already exists.");}
        await using(var transaction=await client.BeginTransactionAsync(databaseName)){if(await Mutate(transaction,"insert-parent",original)!=1){throw new InvalidDataException("Native insert failed.");}await transaction.CommitAsync();}
        if((await Read()).Single().Name!=original.Name){throw new InvalidDataException("Native commit failed.");}
        await using(var transaction=await client.BeginTransactionAsync(databaseName)){if(await Mutate(transaction,"update-parent",updated)!=1){throw new InvalidDataException("Native update failed.");}await transaction.RollbackAsync();}
        if((await Read()).Single().Name!=original.Name){throw new InvalidDataException("Native rollback failed.");}
        await using(var transaction=await client.BeginTransactionAsync(databaseName)){if(await Mutate(transaction,"delete-parent",original)!=1){throw new InvalidDataException("Native delete failed.");}await transaction.CommitAsync();}if((await Read()).Length!=0){throw new InvalidDataException("Native delete was not persisted.");}
    }
    private static async Task VerifySqlClientAsync(string connectionString){
        await using var connection=new SqlConnection(connectionString);await connection.OpenAsync();using var command=connection.CreateCommand();command.CommandText="SELECT @value AS [Value], CAST(NULL AS decimal(18,4)) AS [Empty]; SELECT DB_NAME()";command.Parameters.Add("@value",System.Data.SqlDbType.Int).Value=42;await command.PrepareAsync();using(var reader=await command.ExecuteReaderAsync()){if(!await reader.ReadAsync()||reader.GetInt32(0)!=42||!reader.IsDBNull(1)||!await reader.NextResultAsync()||!await reader.ReadAsync()||reader.GetString(0)!="Seamless_Comparison_Fixture"){throw new InvalidDataException("Prepared RPC, NULL, multiple result sets or DB routing failed.");}}
        command.Parameters.Clear();using(var transaction=(SqlTransaction)await connection.BeginTransactionAsync()){command.Transaction=transaction;command.CommandText="INSERT INTO dbo.FixtureParent(Id,Name) VALUES (1900000001,N'rollback');";await command.ExecuteNonQueryAsync();await transaction.RollbackAsync();command.Transaction=null;}
        command.CommandText="SELECT COUNT_BIG(*) FROM dbo.FixtureParent WHERE Id=1900000001";if(Convert.ToInt64(await command.ExecuteScalarAsync())!=0){throw new InvalidDataException("SqlClient rollback did not restore the fixture.");}
        using(var transaction=(SqlTransaction)await connection.BeginTransactionAsync()){command.Transaction=transaction;command.CommandText="INSERT INTO dbo.FixtureParent(Id,Name) VALUES (1900000002,N'commit');";await command.ExecuteNonQueryAsync();await transaction.CommitAsync();command.Transaction=null;}
        command.CommandText="DELETE FROM dbo.FixtureParent WHERE Id=1900000002";if(await command.ExecuteNonQueryAsync()!=1){throw new InvalidDataException("Committed fixture row could not be removed.");}
    }
    public static async Task MeasureAsync(string directory,string source){
#if DEBUG
        throw new InvalidOperationException("Performance measurements require a Release build.");
#else
        source=new SqlConnectionStringBuilder(source){ColumnEncryptionSetting=SqlConnectionColumnEncryptionSetting.Disabled}.ConnectionString;
        var generation=ComparisonGeneration.Read(directory);if(generation.State is not ("Validated" or "Active")){throw new InvalidOperationException("Only a validated generation can be measured.");}
        var copy=Path.Combine(directory,"direct");if(!Directory.Exists(copy)){throw new InvalidOperationException("The offline validation copy is required for direct measurement.");}using var direct=new ComparisonHost(copy,false);var module=LoadModule(directory);var codec=new NativeComparisonCodec(module);var results=new List<object>();var cases=ReadCases(directory);
        Task WritePerformanceAsync(string status,string? reason=null)=>WriteReportAsync(directory,"performance",new{Generation=generation.Id,PackageVersion=generation.ProductVersion,Module=generation.ModuleManifest,Cases=cases.Select(item=>new{Case=item,NativeArgumentsBase64=item.NativeQuery is null?null:Convert.ToBase64String(codec.Arguments(item.NativeQuery))}).ToArray(),Warmup=2,Measurements=7,SqlExecutionMemoryBytes=new Seamless.Execution.ExecutionOptions().MemoryLimitBytes,SqlExecutionTemporaryBytes=new Seamless.Execution.ExecutionOptions().TemporaryStorageLimitBytes,MemoryBytes=ResultBag.MemoryBytes,TemporaryBytes=ResultBag.TemporaryBytes,FloatingTolerance=FloatingTolerance.FromEnvironment(),SqlServerResourceCaveat="Other SQL Server connections and background tasks can contribute to process counters.",Results=results,Status=status,Reason=reason});
        foreach(var test in cases){
            try{
            var sourceConnection=new SqlConnectionStringBuilder(source){InitialCatalog=test.Database}.ConnectionString;var targetConnection=Target(test.Database,14333);CompareSchema(sourceConnection,targetConnection,test.Sql);var check=CompareResults(test,ReadSql(sourceConnection,test.Sql),ReadSql(targetConnection,test.Sql),Path.Combine(directory,"verification-work"));if(!check.Equal){throw new InvalidDataException($"Results changed for {test.Id}; timing was skipped: {check.Reason}");}
            if(test.NativeQuery is null){foreach(var route in new[]{"NativeDirect","NativeRemote"}){results.Add(new{test.Id,Route=route,Status="NotImplemented",Reason="No typed query is registered for this SQL case."});}}
            foreach(var concurrency in new[]{1,4,16}){
                var routes=new List<string>{"SQLServer","SeamlessSQL"};if(test.NativeQuery is not null){routes.Add("NativeDirect");routes.Add("NativeRemote");}
                for(var iteration=-2;iteration<7;iteration++){
                    var ordered=routes.ToArray();if(iteration%2!=0){(ordered[0],ordered[1])=(ordered[1],ordered[0]);}
                    foreach(var route in ordered){
                        var identity=await IdentityAsync(route,source,directory);ProcessMetrics? metrics=null;if(identity is not null){metrics=new(identity.Value.Pid,identity.Value.Created);}
                        try{var samples=await ComparisonConcurrency.RunAsync(concurrency,TimeAsync);var resources=metrics is null?new ResourceMeasurement("Unavailable",0,null,null,null,null,null,null,"Target process identity is unavailable."):await metrics.FinishAsync();if(iteration>=0){results.Add(new{test.Id,Route=route,Concurrency=concurrency,Iteration=iteration,ElapsedMilliseconds=samples,Resources=resources,Status="Passed"});}}finally{if(metrics is not null){await metrics.DisposeAsync();}}
                        async Task<double> TimeAsync(){
                            if(route is "SQLServer" or "SeamlessSQL"){var connectionString=Target(test.Database,14333);if(route=="SQLServer"){connectionString=new SqlConnectionStringBuilder(source){InitialCatalog=test.Database}.ConnectionString;}using var connection=new SqlConnection(connectionString);await connection.OpenAsync();using var command=connection.CreateCommand();command.CommandTimeout=0;command.CommandText=test.Sql;var clock=Stopwatch.StartNew();using var reader=await command.ExecuteReaderAsync();do{while(await reader.ReadAsync()){for(var i=0;i<reader.FieldCount;i++){_ = reader.GetValue(i);}}}while(await reader.NextResultAsync());clock.Stop();return clock.Elapsed.TotalMilliseconds;}
                            var database=direct.Databases.Resolve(test.Database);if(route=="NativeRemote"){await using var client=new NativeQueryClient();await client.ConnectAsync();var clock=Stopwatch.StartNew();await foreach(var row in codec.Remote(client,generation.Id,test.NativeQuery!,test.Database,database.SchemaFingerprint,CancellationToken.None)){_ = row;}clock.Stop();return clock.Elapsed.TotalMilliseconds;}
                            var timer=Stopwatch.StartNew();foreach(var row in module.Execute(test.NativeQuery!,codec.Arguments(test.NativeQuery!),new NativeContext(((InProcessDatabaseAdapter)database.Adapter).Database,test.Database,CancellationToken.None))){_ = row;}timer.Stop();return timer.Elapsed.TotalMilliseconds;
                        }
                    }
                }
            }
            await WritePerformanceAsync("InProgress");
            }catch(Exception exception){results.Add(new{test.Id,Status="Failed",Reason=exception.Message});await WritePerformanceAsync("Failed",exception.Message);throw;}
        }
        await WritePerformanceAsync("Passed");
#endif
    }
    private static async Task<(int Pid,DateTime Created)?> IdentityAsync(string route,string source,string directory){try{if(route=="NativeDirect"){using var current=Process.GetCurrentProcess();return(current.Id,current.StartTime.ToUniversalTime());}if(route=="SQLServer"){using var connection=new SqlConnection(source);await connection.OpenAsync();using var command=connection.CreateCommand();command.CommandText="SELECT CAST(SERVERPROPERTY('ProcessID') AS int), CAST(SERVERPROPERTY('ComputerNamePhysicalNetBIOS') AS nvarchar(128))";using var reader=await command.ExecuteReaderAsync();if(!await reader.ReadAsync()||reader.IsDBNull(0)||reader.IsDBNull(1)||!string.Equals(reader.GetString(1),Environment.MachineName,StringComparison.OrdinalIgnoreCase)){return null;}var pid=reader.GetInt32(0);using var process=Process.GetProcessById(pid);return(pid,process.StartTime.ToUniversalTime());}using var identity=JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,"server-ready.json")));if(identity.RootElement.GetProperty("Generation").GetString()!=ComparisonGeneration.Read(directory).Id){return null;}return(identity.RootElement.GetProperty("Pid").GetInt32(),identity.RootElement.GetProperty("CreatedUtc").GetDateTime());}catch{return null;}}
    public static IEnumerable<object?[]> ReadSql(string connectionString,string sql){using var connection=new SqlConnection(connectionString);connection.Open();using var command=connection.CreateCommand();command.CommandText=sql;command.CommandTimeout=0;using var reader=command.ExecuteReader(System.Data.CommandBehavior.SequentialAccess);while(reader.Read()){var values=new object?[reader.FieldCount];for(var i=0;i<values.Length;i++){if(!reader.IsDBNull(i)){values[i]=ReadSqlValue(reader,i);}}yield return values;}}
    private static object ReadSqlValue(SqlDataReader reader,int ordinal){
        var typeName=reader.GetDataTypeName(ordinal).Split('.').Last();
        if(typeName.Equals("hierarchyid",StringComparison.OrdinalIgnoreCase)||typeName.Equals("geography",StringComparison.OrdinalIgnoreCase)||typeName.Equals("geometry",StringComparison.OrdinalIgnoreCase)){
            var length=reader.GetBytes(ordinal,0,null,0,0);
            if(length>NativeFrame.MaximumBytes){throw new InvalidDataException("SQL UDT value exceeds the configured row budget.");}
            var bytes=new byte[checked((int)length)];long offset=0;
            while(offset<length){var read=reader.GetBytes(ordinal,offset,bytes,checked((int)offset),checked((int)(length-offset)));if(read==0){throw new EndOfStreamException("SQL UDT value was truncated.");}offset+=read;}
            return bytes;
        }
        return reader.GetValue(ordinal);
    }
    private static IEnumerable<object?[]> ReadNative(NativeComparisonCodec codec,NativeQueryClient client,string generation,string query,string database,string fingerprint){
        var enumerator=codec.Remote(client,generation,query,database,fingerprint,CancellationToken.None).GetAsyncEnumerator();
        try{while(enumerator.MoveNextAsync().AsTask().GetAwaiter().GetResult()){yield return enumerator.Current;}}finally{enumerator.DisposeAsync().AsTask().GetAwaiter().GetResult();}
    }
    public static BagComparison CompareResults(ComparisonCase test,IEnumerable<object?[]> expected,IEnumerable<object?[]> actual,string temporaryRoot){
        var comparison=ResultBag.Compare(expected,actual,temporaryRoot,test.ComputedFloatingPoint||test.ComputedFloatingPointOrdinals is{Length:>0},computedFloatingPointOrdinals:test.ComputedFloatingPointOrdinals?.ToHashSet());
        if(comparison.Equal){VerifyOrder(test,comparison,expected,actual);}return comparison;
    }
    private static void VerifyOrder(ComparisonCase test,BagComparison comparison,IEnumerable<object?[]> expected,IEnumerable<object?[]> actual){
        var parser=new TSql180Parser(true);using var sqlReader=new StringReader(test.Sql);var fragment=parser.Parse(sqlReader,out var errors);if(errors.Count!=0){throw new InvalidDataException($"Cannot parse case {test.Id} to verify its ordering.");}
        var visitor=new TopVisitor();fragment.Accept(visitor);if(visitor.HasTop&&!test.TopOrderUnique){throw new InvalidDataException($"TOP case {test.Id} does not declare a unique deterministic order.");}
        if(!test.Ordered||comparison.Rows<=1){return;}var ordinals=test.OrderKeyOrdinals??DeriveOrderKeys(fragment);if(ordinals is null||ordinals.Length==0){throw new InvalidDataException($"Ordered case {test.Id} has no resolvable order-key ordinals.");}
        if(!ResultBag.CompareOrder(expected,actual,ordinals,test.ComputedFloatingPoint||test.ComputedFloatingPointOrdinals is{Length:>0},test.ComputedFloatingPointOrdinals?.ToHashSet())){throw new InvalidDataException($"Order keys differ: {test.Id}");}
    }
    private sealed class TopVisitor:TSqlFragmentVisitor{public bool HasTop{get;private set;}public override void ExplicitVisit(TopRowFilter node){this.HasTop=true;base.ExplicitVisit(node);}}
    private static int[]? DeriveOrderKeys(TSqlFragment fragment){
        if(fragment is not TSqlScript script||script.Batches.Count!=1||script.Batches[0].Statements.Count!=1||script.Batches[0].Statements[0] is not SelectStatement select||select.QueryExpression is not QuerySpecification query||query.OrderByClause is null){return null;}
        var projections=query.SelectElements.OfType<SelectScalarExpression>().ToArray();if(projections.Length!=query.SelectElements.Count){return null;}var result=new List<int>();
        foreach(var term in query.OrderByClause.OrderByElements){
            if(term.Expression is IntegerLiteral number&&int.TryParse(number.Value,NumberStyles.None,CultureInfo.InvariantCulture,out var position)&&position>0&&position<=projections.Length){result.Add(position-1);continue;}
            if(term.Expression is not ColumnReferenceExpression column){return null;}var parts=column.MultiPartIdentifier.Identifiers.Select(x=>x.Value).ToArray();if(parts.Length==0){return null;}
            var candidates=new List<int>();for(var i=0;i<projections.Length;i++){
                var projection=projections[i];if(parts.Length==1&&projection.ColumnName is not null&&string.Equals(parts[0],projection.ColumnName.Value,StringComparison.OrdinalIgnoreCase)){candidates.Add(i);continue;}
                if(projection.Expression is ColumnReferenceExpression projected){var name=projected.MultiPartIdentifier.Identifiers.Select(x=>x.Value).ToArray();if(parts.Length<=name.Length&&name.Skip(name.Length-parts.Length).SequenceEqual(parts,StringComparer.OrdinalIgnoreCase)){candidates.Add(i);}}
            }
            if(candidates.Count!=1){return null;}result.Add(candidates[0]);
        }return result.ToArray();
    }
    internal static void CompareSchema(string expected,string actual,string sql){
        var left=ReadSchema(expected,sql);var right=ReadSchema(actual,sql);
        if(left.Count!=right.Count){throw new InvalidDataException("Result column count differs.");}
        for(var i=0;i<left.Count;i++){if(left[i]!=right[i]){throw new InvalidDataException($"Result column metadata differs at ordinal {i}: {left[i]} / {right[i]}.");}}
    }
    private static List<ColumnContract> ReadSchema(string connectionString,string sql){
        using var connection=new SqlConnection(connectionString);connection.Open();using var command=connection.CreateCommand();command.CommandTimeout=0;command.CommandText=sql;using var reader=command.ExecuteReader(System.Data.CommandBehavior.SchemaOnly);
        return reader.GetColumnSchema().Select(c=>new ColumnContract(c.ColumnName??throw new InvalidDataException("Missing column name."),c.DataTypeName?.ToLowerInvariant()??throw new InvalidDataException("Missing SQL type."),c.ColumnSize,c.NumericPrecision,c.NumericScale,c.AllowDBNull)).ToList();
    }
    private sealed record ColumnContract(string Name,string SqlType,int? Size,int? Precision,int? Scale,bool? AllowDBNull);
    private static void EnsureServerStopped(string directory){
        var path=Path.Combine(directory,"server-ready.json");if(!File.Exists(path)){return;}
        try{using var identity=JsonDocument.Parse(File.ReadAllText(path));var pid=identity.RootElement.GetProperty("Pid").GetInt32();var created=identity.RootElement.GetProperty("CreatedUtc").GetDateTime().ToUniversalTime();using var process=Process.GetProcessById(pid);if(process.StartTime.ToUniversalTime()==created){throw new InvalidOperationException("The generation server is running; a consistent offline copy cannot be created.");}}catch(ArgumentException){}catch(System.ComponentModel.Win32Exception ex){throw new InvalidOperationException("Server identity cannot be verified; refusing to copy live data.",ex);}
    }
    internal static string Target(string database,int port)=>new SqlConnectionStringBuilder(Program.TargetConnection(database)){DataSource="127.0.0.1,"+port}.ConnectionString;
    private static IReadOnlyList<ComparisonCase> ReadCases(string directory){
        var cases=JsonSerializer.Deserialize<ComparisonCase[]>(File.ReadAllText(Path.Combine(directory,"generated-cases.json")))?.ToList()??throw new InvalidDataException("Invalid generated case manifest.");
        var common=Path.Combine(AppContext.BaseDirectory,"Cases","Common");var manifest=JsonSerializer.Deserialize<CommonCase[]>(File.ReadAllText(Path.Combine(common,"manifest.json")))??throw new InvalidDataException("Invalid common case manifest.");
        foreach(var item in manifest){if(Path.GetFileName(item.File)!=item.File){throw new InvalidDataException("Invalid common case path.");}var lines=File.ReadAllLines(Path.Combine(common,item.File));var sql=string.Join(Environment.NewLine,lines.Where(line=>!line.Trim().Equals("GO",StringComparison.OrdinalIgnoreCase)&&!line.TrimStart().StartsWith("USE ",StringComparison.OrdinalIgnoreCase)));cases.Add(new(item.Id,item.Database,sql,item.NativeQuery,item.Ordered,item.ComputedFloatingPoint,item.OrderKeyOrdinals,item.TopOrderUnique,item.ComputedFloatingPointOrdinals));}
        foreach(var test in cases){SqlConsole.ValidateSourceSql(test.Database,test.Sql);}
        if(cases.Select(c=>c.Id).Distinct(StringComparer.Ordinal).Count()!=cases.Count){throw new InvalidDataException("Duplicate case ID.");}return cases;
    }
    private sealed record CommonCase(string Id,string Database,string File,string? NativeQuery=null,bool Ordered=false,bool ComputedFloatingPoint=false,int[]? OrderKeyOrdinals=null,bool TopOrderUnique=false,int[]? ComputedFloatingPointOrdinals=null);
    private static INativeQueryModule LoadModule(string directory){var manifest=JsonSerializer.Deserialize<NativeModuleManifest>(File.ReadAllText(Path.Combine(directory,"modules/native-module.json")))!;var assembly=Assembly.LoadFrom(Path.Combine(directory,"modules",manifest.AssemblyPath));return (INativeQueryModule)Activator.CreateInstance(assembly.GetType(manifest.EntryType,true)!)!;}
    private static string CopyData(string directory,string name){using var sourceLock=new FileStream(Path.Combine(directory,"data",".seamless-open.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);EnsureServerStopped(directory);var copy=Path.Combine(directory,name);if(Directory.Exists(copy)){throw new IOException("The direct-test copy already exists; use a new generation or remove it through the managed cleanup.");}Directory.CreateDirectory(copy);foreach(var sub in new[]{"data","schema","modules"}){var source=Path.Combine(directory,sub);foreach(var file in Directory.EnumerateFiles(source,"*",SearchOption.AllDirectories)){if(sub=="data"&&Path.GetFileName(file)==".seamless-open.lock"){continue;}var target=Path.Combine(copy,sub,Path.GetRelativePath(source,file));Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(file,target);}}File.Copy(Path.Combine(directory,ComparisonGeneration.FileName),Path.Combine(copy,ComparisonGeneration.FileName));return copy;}
    private static async Task WriteReportAsync(string directory,string name,object report){
        var text=JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true});await File.WriteAllTextAsync(Path.Combine(directory,name+".json"),text);await File.WriteAllTextAsync(Path.Combine(directory,name+".html"),"<!doctype html><meta charset=utf-8><title>Seamless comparison</title><pre>"+System.Net.WebUtility.HtmlEncode(text)+"</pre>");
        using var document=JsonDocument.Parse(text);var root=document.RootElement;var property=name=="performance"?"Results":"Checks";var csv=new StringBuilder();
        if(root.TryGetProperty(property,out var rows)&&rows.ValueKind==JsonValueKind.Array){
            var headers=rows.EnumerateArray().Where(row=>row.ValueKind==JsonValueKind.Object).SelectMany(row=>row.EnumerateObject().Select(p=>p.Name)).Distinct(StringComparer.Ordinal).ToArray();csv.AppendLine(string.Join(",",headers.Select(Csv)));
            foreach(var row in rows.EnumerateArray()){csv.AppendLine(string.Join(",",headers.Select(header=>Csv(row.TryGetProperty(header,out var value)?value.ToString():""))));}
        }
        await File.WriteAllTextAsync(Path.Combine(directory,name+".csv"),csv.ToString());
        if(name=="performance"&&root.TryGetProperty("Results",out var measurements)){
            var distributions=measurements.EnumerateArray().Where(x=>x.TryGetProperty("ElapsedMilliseconds",out _)).GroupBy(x=>(Id:x.GetProperty("Id").GetString(),Route:x.GetProperty("Route").GetString(),Concurrency:x.GetProperty("Concurrency").GetInt32())).Select(group=>{var values=group.SelectMany(x=>x.GetProperty("ElapsedMilliseconds").EnumerateArray().Select(v=>v.GetDouble())).Order().ToArray();return new{group.Key.Id,group.Key.Route,group.Key.Concurrency,Count=values.Length,Minimum=values[0],Median=Percentile(values,0.5),P95=Percentile(values,0.95),Maximum=values[^1]};}).ToArray();
            var summary=distributions.Select(item=>{var baseline=distributions.FirstOrDefault(x=>x.Id==item.Id&&x.Concurrency==item.Concurrency&&x.Route=="SQLServer");return new{item.Id,item.Route,item.Concurrency,item.Count,item.Minimum,item.Median,item.P95,item.Maximum,MedianToSqlServer=baseline is null||baseline.Median==0?(double?)null:item.Median/baseline.Median};}).ToArray();
            await File.WriteAllTextAsync(Path.Combine(directory,"performance-summary.json"),JsonSerializer.Serialize(summary,new JsonSerializerOptions{WriteIndented=true}));
            var summaryCsv=new StringBuilder("Id,Route,Concurrency,Count,Minimum,Median,P95,Maximum,MedianToSqlServer\n");foreach(var item in summary){summaryCsv.AppendLine(string.Join(",",new[]{Csv(item.Id??""),Csv(item.Route??""),item.Concurrency.ToString(CultureInfo.InvariantCulture),item.Count.ToString(CultureInfo.InvariantCulture),item.Minimum.ToString("R",CultureInfo.InvariantCulture),item.Median.ToString("R",CultureInfo.InvariantCulture),item.P95.ToString("R",CultureInfo.InvariantCulture),item.Maximum.ToString("R",CultureInfo.InvariantCulture),item.MedianToSqlServer?.ToString("R",CultureInfo.InvariantCulture)??""}));}await File.WriteAllTextAsync(Path.Combine(directory,"performance-summary.csv"),summaryCsv.ToString());
        }
    }
    private static double Percentile(double[] values,double percentile){var position=(values.Length-1)*percentile;var lower=(int)position;var upper=(int)Math.Ceiling(position);return values[lower]+(values[upper]-values[lower])*(position-lower);}
    private static string Csv(string value)=>"\""+value.Replace("\"","\"\"")+"\"";
    private sealed class NativeComparisonCodec{
        public Func<string,byte[]> Arguments{get;}
        public Func<string,ReadOnlyMemory<byte>,object?[]> Project{get;}
        public Func<NativeQueryClient,string,string,string,string,CancellationToken,IAsyncEnumerable<object?[]>> Remote{get;}
        public NativeComparisonCodec(INativeQueryModule module){
            var type=module.GetType();
            this.Arguments=(type.GetMethod("CreateComparisonArguments")??throw new InvalidDataException("Native comparison argument codec is missing.")).CreateDelegate<Func<string,byte[]>>(module);
            this.Project=(type.GetMethod("ProjectComparisonRow")??throw new InvalidDataException("Native comparison result codec is missing.")).CreateDelegate<Func<string,ReadOnlyMemory<byte>,object?[]>>(module);
            this.Remote=(type.GetMethod("ExecuteComparisonRemoteAsync")??throw new InvalidDataException("Native comparison remote entry is missing.")).CreateDelegate<Func<NativeQueryClient,string,string,string,string,CancellationToken,IAsyncEnumerable<object?[]>>>(module);
        }
    }
    private sealed record NativeContext(object Database,string DatabaseName,CancellationToken CancellationToken):INativeQueryContext;
}
