using System.Data;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Channels;
using Microsoft.Data.SqlClient;
using Seamless.Authentication;
using Seamless.Database;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Http;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Gateway.Tds;
using Seamless.Database.Rdbms;
using Seamless.Execution;
using Seamless.Security;
using Seamless.Transport.Kestrel;

namespace Management.Tests;

[Collection("AuditStorage")]
public sealed class GatewayTdsTransactionAuthorization{
    private const string ColumnCatalogStatement="""
        DECLARE @cols TABLE (
            TABLE_QUALIFIER sysname,TABLE_OWNER sysname,TABLE_NAME sysname,COLUMN_NAME sysname,
            DATA_TYPE smallint,TYPE_NAME sysname,PRECISION int,LENGTH int,SCALE smallint,RADIX smallint,
            NULLABLE smallint,REMARKS varchar(254),COLUMN_DEF nvarchar(4000),SQL_DATA_TYPE smallint,
            SQL_DATETIME_SUB smallint,CHAR_OCTET_LENGTH int,ORDINAL_POSITION int,IS_NULLABLE varchar(254),
            SS_IS_SPARSE smallint,SS_IS_COLUMN_SET smallint,SS_IS_COMPUTED smallint,SS_IS_IDENTITY smallint,
            SS_UDT_CATALOG_NAME sysname,SS_UDT_SCHEMA_NAME sysname,SS_UDT_ASSEMBLY_TYPE_NAME nvarchar(max),
            SS_XML_SCHEMACOLLECTION_CATALOG_NAME sysname,SS_XML_SCHEMACOLLECTION_SCHEMA_NAME sysname,
            SS_XML_SCHEMACOLLECTION_NAME sysname,SS_DATA_TYPE tinyint
        );
        INSERT INTO @cols EXEC sys.sp_columns_170 @table,N'dbo',N'sales',NULL,2,3;
        """;

    [Fact]
    public Task LargeProtectedTableCatalogRespectsBudgetAndDiscoveryPolicy()=>ManagementTransport.WithServerCertificate(async certificate=>{
        const int tableCount=16,viewCount=112,objectCount=tableCount+viewCount;
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,Guid.NewGuid().ToString("N"));
        var number=new DatabaseType(DatabaseTypeKind.Int32,false,SqlTypeName:"int");
        ColumnCatalog[] columnTypes=[
            new(0,"Id",number,false),
            new(1,"Caption",new(DatabaseTypeKind.String,true,MaximumLength:20,SqlTypeName:"nvarchar"),true),
            new(2,"Price",new(DatabaseTypeKind.Decimal,true,Precision:12,Scale:3,SqlTypeName:"decimal"),true),
            new(3,"ClockTime",new(DatabaseTypeKind.Time,true,Scale:0,SqlTypeName:"time"),true),
            new(4,"LocalTime",new(DatabaseTypeKind.DateTime,true,Scale:3,SqlTypeName:"datetime2"),true),
            new(5,"OffsetTime",new(DatabaseTypeKind.DateTimeOffset,true,Scale:7,SqlTypeName:"datetimeoffset"),true),
            new(6,"UnicodeBody",new(DatabaseTypeKind.String,true,MaximumLength:-1,SqlTypeName:"nvarchar"),true),
            new(7,"Body",new(DatabaseTypeKind.String,true,MaximumLength:-1,SqlTypeName:"varchar"),true),
            new(8,"Payload",new(DatabaseTypeKind.Binary,true,MaximumLength:-1,SqlTypeName:"varbinary"),true),
            new(9,"ShortBody",new(DatabaseTypeKind.String,true,MaximumLength:30,SqlTypeName:"varchar"),true),
            new(10,"ShortPayload",new(DatabaseTypeKind.Binary,true,MaximumLength:16,SqlTypeName:"varbinary"),true)
        ];
        var tables=Enumerable.Range(0,tableCount).Select(index=>new TableCatalog($"Catalog_{index:D3}",index==1?columnTypes:[new(0,"Id",number,false)],
            new($"PK_Catalog_{index:D3}",[new("Id",false)],1))).ToArray();
        var viewSchemas=Enumerable.Range(0,viewCount).Select(index=>new SchemaCatalog($"CatalogSchema_{index:D3}",[],
            [new ViewCatalog($"CatalogView_{index:D3}",[new(0,"Id",number,false)],null,
                $"CREATE VIEW CatalogSchema_{index:D3}.CatalogView_{index:D3} AS SELECT Id FROM dbo.Catalog_000")])).ToArray();
        var definition=new DatabaseCatalog("sales",[new SchemaCatalog("dbo",tables),..viewSchemas],"Japanese_CI_AS");
        var options=new DatabaseContainerOptions{IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20};
        var budget=new ResourceBudget(256L<<20,16L<<30);
        using var host=await GatewayDatabaseHost.OpenAsync(Path.Combine(root,"db"),CreateCompiler(root),options,[definition],budget:budget);
        var adapter=host.Databases.Resolve("sales").Adapter;
        var all=adapter.GetTables().OrderBy(table=>table.TableName,StringComparer.Ordinal).ToArray();
        Assert.Equal(tableCount,all.Length);
        Assert.Equal(viewCount,host.Databases.Resolve("sales").Views.Count);
        var serverId=Guid.NewGuid();var databaseId=Guid.NewGuid();var roleId=Guid.NewGuid();
        var schemaIds=definition.Schemas.ToDictionary(schema=>schema.Name,_=>Guid.NewGuid(),StringComparer.Ordinal);
        var tableIds=all.ToDictionary(table=>table.StableObjectId!.Value,_=>Guid.NewGuid());
        var viewIds=viewSchemas.ToDictionary(schema=>(schema.Name,schema.Views.Single().Name),_=>Guid.NewGuid());
        var resources=all.Select(table=>new SecurityResource(tableIds[table.StableObjectId!.Value],schemaIds["dbo"],SecurityResourceKind.Table,table.TableName))
            .Concat(viewIds.Select(view=>new SecurityResource(view.Value,schemaIds[view.Key.Item1],SecurityResourceKind.View,view.Key.Item2))).ToArray();
        var denied=resources.Where((_,index)=>index%4==0).Select(resource=>resource.Id).ToHashSet();
        var expected=resources.Where(resource=>!denied.Contains(resource.Id)).Select(resource=>resource.Name).Order(StringComparer.Ordinal).ToArray();
        var visibleSchemaIds=resources.Where(resource=>!denied.Contains(resource.Id)).Select(resource=>resource.ParentId).ToHashSet();
        var expectedSchemas=schemaIds.Where(schema=>visibleSchemaIds.Contains(schema.Value)).Select(schema=>schema.Key).Order(StringComparer.Ordinal).ToArray();
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();
        using var verifier=new OidcTokenVerifier(issuer.Settings);
        var subject=new SecuritySubjectId(issuer.Id,"catalog-reader");
        using var policy=new AuthorizationManager(new Seamless.Security.AuthorizationPolicySnapshot(1,
            [new(serverId,null,SecurityResourceKind.Server,"gateway"),new(databaseId,serverId,SecurityResourceKind.Database,"sales"),
                ..schemaIds.Select(schema=>new SecurityResource(schema.Value,databaseId,SecurityResourceKind.Schema,schema.Key)),..resources],
            [new(roleId,"catalog-reader",[new(databaseId,SecurityPermission.Discover),
                ..denied.Select(id=>new PermissionRule(id,SecurityPermission.None,SecurityPermission.Discover))])],
            [new(subject,SecurityPrincipalKind.User,roleId)]));
        var inspected=0;
        var authorization=new HttpGatewayAuthorization(verifier,(identity,cancellation)=>{
            using var session=policy.CreateSession(identity);
            return ValueTask.FromResult<AuthorizationLease?>(session.BeginOperation(cancellation));
        },name=>name=="sales"?databaseId:null,(target,id)=>{
            if(!ReferenceEquals(target,adapter)||!tableIds.TryGetValue(id,out var resource)){return null;}
            Interlocked.Increment(ref inspected);
            return resource;
        },(target,kind,schema,name)=>{
            if(!ReferenceEquals(target,adapter)||kind!=SecurityResourceKind.View||!viewIds.TryGetValue((schema,name),out var resource)){return null;}
            Interlocked.Increment(ref inspected);
            return resource;
        });
        using var server=TdsServer.CreateAuthenticated(0,new(host.Databases),certificate,authorization,
            (identity,_)=>ValueTask.FromResult<IAuthorizationSession>(policy.CreateSession(identity)));
        server.Start();
        using var limit=new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var cancel=limit.Token;
        var pin=Path.Combine(root,"catalog.cer");
        await File.WriteAllBytesAsync(pin,certificate.Export(X509ContentType.Cert),cancel);
        try{
            var builder=new SqlConnectionStringBuilder{DataSource=$"tcp:127.0.0.1,{server.Port}",InitialCatalog="sales",
                Encrypt=SqlConnectionEncryptOption.Strict,Pooling=false,ConnectTimeout=5,ConnectRetryCount=0};
            builder["Server Certificate"]=pin;
            await using var client=new SqlConnection(builder.ConnectionString){
                AccessToken=issuer.CreateToken(OidcDevelopmentIssuer.Audience,subject.Value,scope:"seamless.database")
            };
            await client.OpenAsync(cancel);
            using var command=CatalogCommand(client);
            Volatile.Write(ref inspected,0);
            using(var held=budget.ReserveMemory(budget.AvailableMemoryBytes-8192)){
                var error=await Assert.ThrowsAsync<SqlException>(async()=>{
                    await using var unexpected=await command.ExecuteReaderAsync(cancel);
                    await unexpected.ReadAsync(cancel);
                });
                Assert.Equal(50000,error.Number);
                Assert.Contains("Resource limit exceeded: Memory",error.Message);
                Assert.InRange(Volatile.Read(ref inspected),1,objectCount-1);
            }
            Volatile.Write(ref inspected,0);
            await AssertCatalog(command);
            Assert.Equal(objectCount,Volatile.Read(ref inspected));
            using(var fields=new SqlCommand(ColumnCatalogStatement+"SELECT COLUMN_NAME,PRECISION,SCALE,CHAR_OCTET_LENGTH,DATA_TYPE,LENGTH FROM @cols ORDER BY ORDINAL_POSITION",client)){
                fields.Parameters.Add("@table",SqlDbType.NVarChar,128).Value="Catalog_001";
                await using var rows=await fields.ExecuteReaderAsync(cancel);
                (string Name,int Size,short? Scale,int? Octets,short Type)[] expectedFields=[
                    ("Id",10,null,null,4),("Caption",20,null,40,-9),("Price",12,3,null,3),
                    ("ClockTime",8,0,null,-154),("LocalTime",23,3,null,93),("OffsetTime",34,7,null,-155),
                    ("UnicodeBody",0,null,0,-9),("Body",0,null,0,12),("Payload",0,null,0,-3),
                    ("ShortBody",30,null,30,12),("ShortPayload",16,null,16,-3)
                ];
                foreach(var (name,size,scale,octets,type) in expectedFields){
                    Assert.True(await rows.ReadAsync(cancel));
                    Assert.Equal(name,rows.GetString(0));
                    Assert.Equal<int?>(size,rows.IsDBNull(1)?null:rows.GetInt32(1));
                    Assert.Equal(scale,rows.IsDBNull(2)?null:rows.GetInt16(2));
                    Assert.Equal(octets,rows.IsDBNull(3)?null:rows.GetInt32(3));
                    Assert.Equal(type,rows.GetInt16(4));
                    if(octets.HasValue){Assert.Equal(octets.Value,rows.GetInt32(5));}
                }
                Assert.False(await rows.ReadAsync(cancel));
            }
            using var schemas=new SqlCommand("SELECT name FROM sys.schemas",client);
            Volatile.Write(ref inspected,0);
            using(var held=budget.ReserveMemory(budget.AvailableMemoryBytes-8192)){
                var error=await Assert.ThrowsAsync<SqlException>(async()=>{
                    await using var rows=await schemas.ExecuteReaderAsync(cancel);
                    while(await rows.ReadAsync(cancel)){}
                });
                Assert.Equal(50000,error.Number);
                Assert.Contains("Resource limit exceeded: Memory",error.Message);
                Assert.InRange(Volatile.Read(ref inspected),1,objectCount-1);
            }
            using(var held=budget.ReserveMemory(budget.AvailableMemoryBytes-16384)){
                using var first=new SqlCommand("SELECT TOP (1) name FROM sys.schemas",client);
                var firstSchema=Assert.IsType<string>(await first.ExecuteScalarAsync(cancel));
                Assert.Contains(firstSchema,expectedSchemas);
                var observedSchemas=new List<string>();
                await using(var rows=await schemas.ExecuteReaderAsync(cancel)){
                    while(await rows.ReadAsync(cancel)){observedSchemas.Add(rows.GetString(0));}
                }
                Assert.Equal(expectedSchemas,observedSchemas.Order(StringComparer.Ordinal));
            }
            var connections=Channel.CreateUnbounded<(CatalogWriteGate Gate,Action Abort,Task Completed,CancellationToken Closed)>();
            CancellationToken operationToken=default;
            var engine=new SqlExecutionEngine(host.Databases);
            var protectedAccess=new TdsProtectedAccess(authorization,(identity,_)=>
                ValueTask.FromResult<IAuthorizationSession>(new ObservedCatalogSession(policy.CreateSession(identity),token=>operationToken=token)));
            await using var gatedServer=new KestrelTcpHost(new(IPAddress.Loopback,0),async connection=>{
                var gate=new CatalogWriteGate(connection.Transport.Output);
                var completed=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                connections.Writer.TryWrite((gate,()=>connection.Abort(),completed.Task,connection.ConnectionClosed));
                try{
                    await new TdsSession(1,adapter,engine){Protection=protectedAccess}.ProcessConnectionAsync(
                        connection.Transport.Input,gate,connection.ConnectionClosed,TimeSpan.FromSeconds(10),TimeSpan.FromSeconds(10));
                }catch(OperationCanceledException)when(connection.ConnectionClosed.IsCancellationRequested){}
                catch(IOException)when(connection.ConnectionClosed.IsCancellationRequested){}
                finally{completed.TrySetResult();}
            },certificate);
            await gatedServer.StartAsync(cancel);
            await using var gatedClient=GatedClient(gatedServer.Port);
            await gatedClient.OpenAsync(cancel);
            var (writeGate,abort,disconnected,_)=await connections.Reader.ReadAsync(cancel);
            using var interrupted=CatalogCommand(gatedClient);
            var memoryBefore=budget.MemoryBytes;
            var paused=writeGate.PauseNextRow();
            var interruptedRead=ReadInterruptedCatalog(interrupted);
            await paused.WaitAsync(cancel);
            Assert.True(budget.MemoryBytes-memoryBefore>=24*1024);
            var attention=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using(var cancellation=operationToken.Register(()=>attention.TrySetResult())){
                interrupted.Cancel();
                await attention.Task.WaitAsync(cancel);
            }
            writeGate.Resume();
            var cancelled=await interruptedRead.WaitAsync(cancel);
            Assert.True(cancelled is SqlException or OperationCanceledException,cancelled?.Message);
            using(var held=budget.ReserveMemory(budget.AvailableMemoryBytes-32768)){
                await AssertCatalog(interrupted);
            }

            paused=writeGate.PauseNextRow();
            interruptedRead=ReadInterruptedCatalog(interrupted);
            await paused.WaitAsync(cancel);
            abort();
            await disconnected.WaitAsync(cancel);
            Assert.IsType<SqlException>(await interruptedRead.WaitAsync(cancel));
            using(var held=budget.ReserveMemory(budget.AvailableMemoryBytes-32768)){
                await AssertCatalog(command);
            }

            using var relay=new TcpListener(IPAddress.Loopback,0);
            relay.Start();
            using var upstream=new TcpClient();
            await upstream.ConnectAsync(IPAddress.Loopback,gatedServer.Port,cancel);
            await using var resetClient=GatedClient(((IPEndPoint)relay.LocalEndpoint).Port);
            var opening=resetClient.OpenAsync(cancel);
            using var downstream=await relay.AcceptTcpClientAsync(cancel);
            var forwarded=Task.WhenAll(Forward(downstream,upstream),Forward(upstream,downstream));
            try{
                await opening;
                var reset=await connections.Reader.ReadAsync(cancel);
                using var resetCommand=CatalogCommand(resetClient);
                var blocked=reset.Gate.PauseNextRow();
                var resetRead=ReadInterruptedCatalog(resetCommand);
                await blocked.WaitAsync(cancel);
                Assert.True(budget.MemoryBytes-memoryBefore>=24*1024);
                upstream.LingerState=new LingerOption(true,0);
                upstream.Client.Dispose();
                downstream.Dispose();
                await reset.Completed.WaitAsync(TimeSpan.FromSeconds(5),cancel);
                Assert.True(reset.Closed.IsCancellationRequested);
                Assert.IsType<SqlException>(await resetRead.WaitAsync(cancel));
                using var held=budget.ReserveMemory(budget.AvailableMemoryBytes-32768);
                await AssertCatalog(command);
            }finally{
                upstream.Dispose();
                downstream.Dispose();
                try{await forwarded;}catch(IOException){}catch(ObjectDisposedException){}
            }

            SqlConnection GatedClient(int port){
                var settings=new SqlConnectionStringBuilder(builder.ConnectionString){DataSource=$"tcp:127.0.0.1,{port}"};
                return new(settings.ConnectionString){
                    AccessToken=issuer.CreateToken(OidcDevelopmentIssuer.Audience,subject.Value,scope:"seamless.database")
                };
            }
            async Task Forward(TcpClient from,TcpClient to)=>await from.GetStream().CopyToAsync(to.GetStream(),cancel);
            async Task<Exception?> ReadInterruptedCatalog(SqlCommand query)=>await Record.ExceptionAsync(async()=>{
                await using var rows=await query.ExecuteReaderAsync(cancel);
                while(await rows.ReadAsync(cancel)){}
            });
            static SqlCommand CatalogCommand(SqlConnection connection){
                var query=new SqlCommand("sys.sp_tables",connection){CommandType=CommandType.StoredProcedure};
                query.Parameters.Add("@table_name",SqlDbType.NVarChar,128).Value="Catalog%";
                query.Parameters.Add("@table_owner",SqlDbType.NVarChar,128).Value=DBNull.Value;
                query.Parameters.Add("@table_qualifier",SqlDbType.NVarChar,128).Value="sales";
                query.Parameters.Add("@table_type",SqlDbType.NVarChar,128).Value="TABLE,VIEW";
                return query;
            }
            async Task AssertCatalog(SqlCommand query){
                var names=new List<string>();
                await using var rows=await query.ExecuteReaderAsync(cancel);
                var name=rows.GetOrdinal("TABLE_NAME");
                while(await rows.ReadAsync(cancel)){names.Add(rows.GetString(name));}
                Assert.Equal(expected,names.Order(StringComparer.Ordinal));
            }
        }finally{File.Delete(pin);}
    },includeLoopbackAddress:true);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task TransactionUsesStartPolicyAndReleasesWritesOnRollbackDisconnectAndDisable(bool versioned)=>ManagementTransport.WithServerCertificate(async certificate=>{
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,Guid.NewGuid().ToString("N"));
        var compiler=CreateCompiler(root);
        var number=new DatabaseType(DatabaseTypeKind.Int32,false,SqlTypeName:"int");
        var wide=new DatabaseType(DatabaseTypeKind.Int64,true,SqlTypeName:"bigint");
        var label=new DatabaseType(DatabaseTypeKind.String,true,MaximumLength:20,SqlTypeName:"nvarchar");
        RoutineCatalog Procedure(string name,string body,bool mutation=true)=>new("dbo",name,RoutineKind.StoredProcedure,null,[new(1,"@id",number,false)],[],
            $"CREATE PROCEDURE dbo.{name} @id int AS {body}",mutation,mutation?0:2,0);
        var definition=new DatabaseCatalog("sales",[new("dbo",[new TableCatalog("Rows",[new(0,"Id",number,false)],new("PK_Rows",[new("Id",false)],1))],[],
            [Procedure("AddRow","INSERT INTO dbo.Rows VALUES (@id);"),Procedure("MoveRow","UPDATE dbo.Rows SET Id=9 WHERE Id=@id;"),
                Procedure("RemoveRow","DELETE FROM dbo.Rows WHERE Id=@id;"),
                Procedure("AddPair","BEGIN INSERT INTO dbo.Rows VALUES (@id); INSERT INTO dbo.Rows VALUES (@id+1); END"),
                Procedure("FailPair","INSERT INTO dbo.Rows VALUES (@id); INSERT INTO dbo.Rows VALUES (1);"),
                Procedure("DeleteAfterInsert","INSERT INTO dbo.Rows VALUES (@id); DELETE FROM dbo.Rows WHERE Id=1;"),
                Procedure("ReadMutateRead","SELECT COUNT(*) FROM dbo.Rows; INSERT INTO dbo.Rows VALUES (@id); SELECT Id FROM dbo.Rows WHERE Id=@id;"),
                Procedure("FailMixed","INSERT INTO dbo.Rows VALUES (@id); SELECT Id FROM dbo.Rows WHERE Id=@id; INSERT INTO dbo.Rows VALUES (1);"),
                Procedure("SpoolMixed","INSERT INTO dbo.Rows VALUES (@id); SELECT Id FROM dbo.Rows WHERE Id=@id; SELECT REPLICATE(CAST(N'x' AS nvarchar(max)),65536);"),
                Procedure("StreamMixed","INSERT INTO dbo.Rows VALUES (@id); SELECT Id FROM dbo.Rows WHERE Id=@id; SELECT REPLICATE(CAST(N'x' AS nvarchar(max)),4194304);"),
                Procedure("UnsupportedPair","INSERT INTO dbo.Rows VALUES (@id); PRINT @id;"),
                Procedure("ReadTwice","SELECT Id FROM dbo.Rows WHERE Id=@id; SELECT COUNT(*) FROM dbo.Rows;",mutation:false),
                Procedure("ReadFault","SELECT Id FROM dbo.Rows WHERE Id=@id; SELECT 1/(Id-@id) FROM dbo.Rows WHERE Id=@id;",mutation:false),
                new("dbo","SetOutputs",RoutineKind.StoredProcedure,null,[new(1,"@id",number,false),new(2,"@total",wide,true),new(3,"@label",label,true)],[],
                    "CREATE PROCEDURE dbo.SetOutputs @id int, @total bigint OUTPUT, @label nvarchar(20) OUTPUT AS BEGIN SET @total=@total+(SELECT COUNT(*) FROM dbo.Rows); SET @label=CASE WHEN @id=2 THEN N'集計' ELSE NULL END; END",false,0,1),
                MixedOutput("WriteOutput","SET @work=@id+1; INSERT INTO dbo.Rows VALUES (@work); SELECT Id FROM dbo.Rows WHERE Id=@work; SET @work=@work+100;"),
                MixedOutput("FailOutput","SET @work=@id+1; INSERT INTO dbo.Rows VALUES (@work); SELECT Id FROM dbo.Rows WHERE Id=@work; SET @work=1/(@id-@id);"),
                MixedOutput("ReadOutput","SELECT @work AS BeforeValue; SET @work=@id+1; SELECT @work AS AfterValue;",mutation:false)])],"Japanese_CI_AS");
        RoutineCatalog MixedOutput(string name,string body,bool mutation=true)=>new("dbo",name,RoutineKind.StoredProcedure,null,
            [new(1,"@id",number,false),new(2,"@work",number,true)],[],$"CREATE PROCEDURE dbo.{name} @id int, @work int OUTPUT AS BEGIN {body} END",mutation,0,1);
        var options=new DatabaseContainerOptions{IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20};
        if(versioned){
            options=options with{TransactionConcurrency=global::Seamless.Transactions.TransactionConcurrencyMode.Optimistic,
                TransactionHistory=new(){MaxRetainedMemoryBytes=1<<20,MaxRetainedStorageBytes=8<<20,MaxMetadataBytes=2<<20,MaintenanceReserveMemoryBytes=64<<10,
                    MaintenanceReserveStorageBytes=1<<20,MaintenanceWaitTimeout=TimeSpan.FromSeconds(5)}};
        }
        var budget=new ResourceBudget(256L<<20,16L<<30);
        using var host=await GatewayDatabaseHost.OpenAsync(Path.Combine(root,"db"),compiler,options,[definition],budget:budget);
        var adapter=(InProcessDatabaseAdapter)host.Databases.Resolve("sales").Adapter;
        var table=Assert.Single(adapter.GetTables());
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();
        using var verifier=new OidcTokenVerifier(issuer.Settings);
        var serverId=Guid.NewGuid();var databaseId=Guid.NewGuid();var schemaId=Guid.NewGuid();var tableId=Guid.NewGuid();
        var procedures=definition.Schemas[0].Routines.ToDictionary(routine=>routine.Name,_=>Guid.NewGuid(),StringComparer.Ordinal);
        var writerRole=Guid.NewGuid();var observerRole=Guid.NewGuid();
        var writer=new SecuritySubjectId(issuer.Id,"writer");var observer=new SecuritySubjectId(issuer.Id,"observer");
        var clock=new AuthorizationClock();
        using var policy=new AuthorizationManager(Policy(1,true),time:clock,limits:new(){MaximumTransactionDuration=TimeSpan.FromSeconds(10)});
        var access=new HttpGatewayAuthorization(verifier,(identity,cancellation)=>{
            using var session=policy.CreateSession(identity);
            return ValueTask.FromResult<AuthorizationLease?>(session.BeginOperation(cancellation));
        },name=>name=="sales"?databaseId:null,(target,id)=>ReferenceEquals(target,adapter)&&id==table.StableObjectId?tableId:null,
            (target,kind,schema,name)=>ReferenceEquals(target,adapter)&&kind==SecurityResourceKind.Procedure&&schema=="dbo"&&procedures.TryGetValue(name,out var id)?id:(Guid?)null);
        var pendingWrites=new FrameBufferQuota(32L<<20);
        using var server=TdsServer.CreateAuthenticated(new(){TdsPort=0,TdsPacketWriteTimeout=TimeSpan.FromSeconds(3)},new(host.Databases),
            new(certificate,access,(identity,_)=>ValueTask.FromResult<IAuthorizationSession>(policy.CreateSession(identity))),
            null,null,pendingWrites,null,null);
        server.Start();
        var pin=Path.Combine(root,"tds.cer");
        await File.WriteAllBytesAsync(pin,certificate.Export(X509ContentType.Cert));
        try{
            using var limit=new CancellationTokenSource(TimeSpan.FromSeconds(55));var cancel=limit.Token;
            await using var client=Connect(server.Port,writer);
            await using var reader=Connect(server.Port,observer);
            await client.OpenAsync(cancel);await reader.OpenAsync(cancel);

            // SqlClientの取引管理要求と、SQL文のセーブポイントを同じ認可リースで実行する。
            await using(var tx=(SqlTransaction)await client.BeginTransactionAsync(cancel)){
                using var insert=new SqlCommand("INSERT INTO dbo.Rows VALUES (@id)",client,tx);
                var id=insert.Parameters.Add("@id",SqlDbType.Int);
                id.Value=1;insert.Prepare();
                Assert.Equal(1,await insert.ExecuteNonQueryAsync(cancel));
                await AssertMetadata(client,tx);
                await Sql(client,"SAVE TRANSACTION before_extra",tx);
                await Sql(client,"INSERT INTO dbo.Rows VALUES (99)",tx);
                await Sql(client,"ROLLBACK TRANSACTION before_extra",tx);
                policy.PublishPolicy(Policy(2,false));
                Assert.Equal(1,await Sql(client,"EXEC dbo.AddRow @id=2",tx));
                await tx.CommitAsync(cancel);
                insert.Transaction=null;
                id.Value=3;
                Assert.Equal(229,(await Assert.ThrowsAsync<SqlException>(()=>insert.ExecuteNonQueryAsync(cancel))).Number);
            }
            Assert.Equal(2,await Count(reader));
            await AssertOutputs(client,reader);
            await AssertReadTwice(client,2,2);
            Assert.Equal(8134,(await Assert.ThrowsAsync<SqlException>(()=>Sql(client,"EXEC dbo.ReadFault @id=2"))).Number);
            await AssertReadTwice(client,2,2);
            Assert.Equal(229,(await Assert.ThrowsAsync<SqlException>(()=>Sql(client,"EXEC dbo.AddRow @id=3"))).Number);
            Assert.Equal(229,(await Assert.ThrowsAsync<SqlException>(()=>Sql(reader,"EXEC dbo.AddRow @id=3"))).Number);
            await AssertMetadata(reader);
            await AssertColumnMetadataBudget(client);
            await AssertTableAndKeyMetadataBudget(client);
            Assert.Equal(2,await Count(reader));
            await using(var tx=(SqlTransaction)await client.BeginTransactionAsync(cancel)){
                Assert.Equal(229,(await Assert.ThrowsAsync<SqlException>(()=>Sql(client,"INSERT INTO dbo.Rows VALUES (3)",tx))).Number);
                await tx.RollbackAsync(cancel);
            }
            policy.PublishPolicy(Policy(3,true));
            await Sql(client,"BEGIN TRANSACTION");
            using(var insert=new SqlCommand("dbo.AddRow",client){CommandType=CommandType.StoredProcedure}){
                insert.Parameters.Add("@id",SqlDbType.Int).Value=3;
                Assert.Equal(1,await insert.ExecuteNonQueryAsync(cancel));
            }
            await Sql(client,"ROLLBACK TRANSACTION");
            await AssertRolledBack(reader,3);
            Assert.Equal(1,await Sql(client,"EXEC dbo.AddRow @id=8"));
            Assert.Equal(3,await Count(reader));
            Assert.Equal(1,await Sql(client,"EXEC dbo.MoveRow @id=8"));
            Assert.Equal(0,await Sql(client,"EXEC dbo.RemoveRow @id=8"));
            Assert.Equal(1,await Sql(client,"EXEC dbo.RemoveRow @id=9"));
            Assert.Equal(2,await Count(reader));

            Assert.Equal(2,await Sql(client,"EXEC dbo.AddPair @id=10"));
            Assert.Equal(4,await Count(reader));
            Assert.Equal(2,await Sql(reader,"DELETE FROM dbo.Rows WHERE Id IN (10,11)"));
            Assert.Equal(2627,(await Assert.ThrowsAsync<SqlException>(()=>Sql(client,"EXEC dbo.FailPair @id=12"))).Number);
            await AssertRolledBack(reader,12);
            await using(var tx=(SqlTransaction)await client.BeginTransactionAsync(cancel)){
                Assert.Equal(1,await Sql(client,"INSERT INTO dbo.Rows VALUES (13)",tx));
                await AssertReadTwice(client,13,3,tx);
                using var fail=new SqlCommand("dbo.FailPair",client,tx){CommandType=CommandType.StoredProcedure};
                fail.Parameters.Add("@id",SqlDbType.Int).Value=14;
                Assert.Equal(2627,(await Assert.ThrowsAsync<SqlException>(()=>fail.ExecuteNonQueryAsync(cancel))).Number);
                Assert.Equal(1,await Sql(client,"INSERT INTO dbo.Rows VALUES (15)",tx));
                await tx.CommitAsync(cancel);
            }
            await AssertRolledBack(reader,14);
            Assert.Equal(2,await Sql(reader,"DELETE FROM dbo.Rows WHERE Id IN (13,15)"));
            await AssertMixed(client,16,2);
            Assert.Equal(3,await Count(reader));
            Assert.Equal(1,await Sql(reader,"DELETE FROM dbo.Rows WHERE Id=16"));
            using(var failing=new SqlCommand("EXEC dbo.FailMixed @id=16",client)){
                Assert.Equal(2627,(await Assert.ThrowsAsync<SqlException>(()=>failing.ExecuteReaderAsync(cancel))).Number);
            }
            await AssertRolledBack(reader,16);
            await using(var tx=(SqlTransaction)await client.BeginTransactionAsync(cancel)){
                await AssertMixed(client,16,2,tx);
                using var failing=new SqlCommand("EXEC dbo.FailMixed @id=18",client,tx);
                Assert.Equal(2627,(await Assert.ThrowsAsync<SqlException>(()=>failing.ExecuteReaderAsync(cancel))).Number);
                Assert.Equal(1,await Sql(client,"INSERT INTO dbo.Rows VALUES (19)",tx));
                await tx.CommitAsync(cancel);
            }
            await AssertRolledBack(reader,18);
            Assert.Equal(2,await Sql(reader,"DELETE FROM dbo.Rows WHERE Id IN (16,19)"));
            Assert.Equal(2,await Count(client));
            var procedureSpool=Path.Combine(root,"db","transactions","query-spool");
            Assert.True(Directory.Exists(procedureSpool));
            Assert.Empty(Directory.EnumerateFileSystemEntries(procedureSpool,"scope-*"));
            using(var storage=budget.ReserveTemporaryStorage(budget.TemporaryStorageLimitBytes-budget.TemporaryStorageBytes-(16<<10))){
                using var limited=new SqlCommand("EXEC dbo.SpoolMixed @id=20",client);
                var failure=await Assert.ThrowsAsync<SqlException>(()=>limited.ExecuteReaderAsync(cancel));
                Assert.Contains("Resource limit exceeded: TemporaryStorage",failure.Message);
                Assert.Equal(2,await Count(client));
                Assert.Empty(Directory.EnumerateFileSystemEntries(procedureSpool,"scope-*"));
                await using var tx=(SqlTransaction)await client.BeginTransactionAsync(cancel);
                limited.Transaction=tx;
                var inside=await Assert.ThrowsAsync<SqlException>(()=>limited.ExecuteReaderAsync(cancel));
                Assert.Contains("Resource limit exceeded: TemporaryStorage",inside.Message);
                using var inspect=new SqlCommand("SELECT Id FROM dbo.Rows WHERE Id=20",client,tx);
                Assert.Null(await inspect.ExecuteScalarAsync(cancel));
                await tx.RollbackAsync(cancel);
            }
            await AssertRolledBack(reader,20);
            using(var retry=new SqlCommand("EXEC dbo.SpoolMixed @id=20",client)){
                using var results=await retry.ExecuteReaderAsync(cancel);
                Assert.True(await results.ReadAsync(cancel));
                Assert.Equal(20,results.GetInt32(0));
                Assert.True(await results.NextResultAsync(cancel));
                Assert.True(await results.ReadAsync(cancel));
                Assert.Equal(new string('x',65536),results.GetString(0));
                Assert.False(await results.NextResultAsync(cancel));
            }
            Assert.Equal(1,await Sql(client,"DELETE FROM dbo.Rows WHERE Id=20"));
            Assert.Empty(Directory.EnumerateFileSystemEntries(procedureSpool,"scope-*"));
            await AssertResultSendStops(procedureSpool,disconnect:true);
            await AssertResultSendStops(procedureSpool,disconnect:false);
            await AssertMixedOutputs();
            Assert.Equal(40515,(await Assert.ThrowsAsync<SqlException>(()=>Sql(client,"EXEC dbo.UnsupportedPair @id=16"))).Number);
            await AssertRolledBack(reader,16);
            policy.PublishPolicy(Policy(4,true,delete:false));
            Assert.Equal(229,(await Assert.ThrowsAsync<SqlException>(()=>Sql(client,"EXEC dbo.DeleteAfterInsert @id=17"))).Number);
            await AssertRolledBack(reader,17);
            Assert.Equal(2,await Count(reader));

            using(var text=new SqlCommand("SELECT @value",reader)){
                const string value="'; DELETE FROM dbo.Rows; --";
                var input=text.Parameters.Add("@value",SqlDbType.NVarChar,80);
                input.Value=value;
                Assert.Equal(value,await text.ExecuteScalarAsync(cancel));
                input.Value=DBNull.Value;
                Assert.Equal(DBNull.Value,await text.ExecuteScalarAsync(cancel));
            }
            using(var unsupported=new SqlCommand("dbo.unknown",reader){CommandType=CommandType.StoredProcedure}){
                Assert.Equal(229,(await Assert.ThrowsAsync<SqlException>(()=>unsupported.ExecuteNonQueryAsync(cancel))).Number);
            }
            // 明示の準備と破棄を通し、破棄後に同じハンドルが再利用できないことを確認する。
            using(var prepare=new SqlCommand("sp_prepare",reader){CommandType=CommandType.StoredProcedure}){
                var handle=prepare.Parameters.Add("@handle",SqlDbType.Int);
                handle.Direction=ParameterDirection.Output;
                prepare.Parameters.Add("@params",SqlDbType.NVarChar,80).Value="@value int";
                prepare.Parameters.Add("@stmt",SqlDbType.NVarChar,80).Value="SELECT @value";
                prepare.Parameters.Add("@options",SqlDbType.Int).Value=0;
                await prepare.ExecuteNonQueryAsync(cancel);
                var preparedHandle=Assert.IsType<int>(handle.Value);
                using var execute=new SqlCommand("sp_execute",reader){CommandType=CommandType.StoredProcedure};
                execute.Parameters.Add("@handle",SqlDbType.Int).Value=preparedHandle;
                execute.Parameters.Add("@value",SqlDbType.Int).Value=37;
                Assert.Equal(37,await execute.ExecuteScalarAsync(cancel));
                using(var release=new SqlCommand("sp_unprepare",reader){CommandType=CommandType.StoredProcedure}){
                    release.Parameters.Add("@handle",SqlDbType.Int).Value=preparedHandle;
                    await release.ExecuteNonQueryAsync(cancel);
                }
                Assert.Equal(8179,(await Assert.ThrowsAsync<SqlException>(()=>execute.ExecuteScalarAsync(cancel))).Number);
            }

            // プロキシのサーバー側TCPを先に閉じ、SqlClientの取消要求を経由しない切断を再現する。
            using(var relay=new TcpListener(IPAddress.Loopback,0)){
                relay.Start();
                using var upstream=new TcpClient();await upstream.ConnectAsync(IPAddress.Loopback,server.Port,cancel);
                await using var cut=Connect(((IPEndPoint)relay.LocalEndpoint).Port,writer);
                var opening=cut.OpenAsync(cancel);
                using var downstream=await relay.AcceptTcpClientAsync(cancel);
                var forwarded=Task.WhenAll(downstream.GetStream().CopyToAsync(upstream.GetStream(),cancel),upstream.GetStream().CopyToAsync(downstream.GetStream(),cancel));
                try{
                    await opening;
                    await using var tx=(SqlTransaction)await cut.BeginTransactionAsync(cancel);
                    await Sql(cut,"INSERT INTO dbo.Rows VALUES (4)",tx);
                    upstream.Dispose();downstream.Dispose();
                    await AssertRolledBack(reader,4);
                }finally{
                    upstream.Dispose();downstream.Dispose();
                    try{await forwarded;}catch(IOException){}catch(ObjectDisposedException){}
                }
            }

            // 操作者が無効化された時点で、本人が確定や切断を送る前に書き込みを解放する。
            await using(var tx=(SqlTransaction)await client.BeginTransactionAsync(cancel)){
                await Sql(client,"INSERT INTO dbo.Rows VALUES (5)",tx);
                policy.SetDisabled(writer,true);
                await AssertRolledBack(reader,5);
                var rejected=await Record.ExceptionAsync(()=>tx.CommitAsync(cancel));
                Assert.True(rejected is SqlException or InvalidOperationException,rejected?.Message);
            }
            policy.SetDisabled(writer,false);

            // 期限切れの取引も、次の要求を待たずにロールバックする。
            await using var expires=Connect(server.Port,writer);await expires.OpenAsync(cancel);
            await using(var tx=(SqlTransaction)await expires.BeginTransactionAsync(cancel)){
                await Sql(expires,"INSERT INTO dbo.Rows VALUES (6)",tx);
                await AssertRolledBack(reader,6);
            }
            Assert.Equal(2,await Count(reader));

            // 取引期限内ならサインイン期限切れ後も開始済み取引を確定できる。
            await using var shortSignIn=Connect(server.Port,writer,TimeSpan.FromMinutes(2));
            await shortSignIn.OpenAsync(cancel);
            clock.Freeze();
            clock.Advance(TimeSpan.FromSeconds(114));
            await using(var tx=(SqlTransaction)await shortSignIn.BeginTransactionAsync(cancel)){
                await Sql(shortSignIn,"INSERT INTO dbo.Rows VALUES (7)",tx);
                clock.Advance(TimeSpan.FromSeconds(8));
                await tx.CommitAsync(cancel);
            }
            Assert.Equal(229,(await Assert.ThrowsAsync<SqlException>(()=>Count(shortSignIn))).Number);
            Assert.Equal(3,await Count(reader));

            SqlConnection Connect(int port,SecuritySubjectId subject,TimeSpan? lifetime=null){
                var builder=new SqlConnectionStringBuilder{DataSource=$"tcp:127.0.0.1,{port}",InitialCatalog="sales",
                    Encrypt=SqlConnectionEncryptOption.Strict,Pooling=false,ConnectTimeout=5,ConnectRetryCount=0};
                builder["Server Certificate"]=pin;
                return new(builder.ConnectionString){AccessToken=issuer.CreateToken(OidcDevelopmentIssuer.Audience,subject.Value,scope:"seamless.database",lifetime:lifetime)};
            }
            async Task<int> Sql(SqlConnection connection,string text,SqlTransaction? transaction=null){
                using var command=new SqlCommand(text,connection,transaction){CommandTimeout=20};
                return await command.ExecuteNonQueryAsync(cancel);
            }
            async Task<int> Count(SqlConnection connection){
                using var command=new SqlCommand("SELECT COUNT(*) FROM dbo.Rows",connection){CommandTimeout=20};
                return (int)(await command.ExecuteScalarAsync(cancel))!;
            }
            async Task AssertMetadata(SqlConnection connection,SqlTransaction? transaction=null){
                using var command=new SqlCommand("SET FMTONLY ON SELECT Id FROM dbo.Rows\nSET FMTONLY OFF",connection,transaction);
                using var columns=await command.ExecuteReaderAsync(cancel);
                Assert.Equal("Id",columns.GetName(0));
                Assert.False(await columns.ReadAsync(cancel));
            }
            async Task AssertColumnMetadataBudget(SqlConnection connection){
                using var command=new SqlCommand(ColumnCatalogStatement+"SELECT COLUMN_NAME FROM @cols;",connection);
                command.Parameters.Add("@table",SqlDbType.NVarChar,128).Value="Rows";
                using(var held=budget.ReserveMemory(budget.AvailableMemoryBytes-512)){
                    var error=await Assert.ThrowsAsync<SqlException>(()=>command.ExecuteScalarAsync(cancel));
                    Assert.Equal(50000,error.Number);
                    Assert.Contains("Resource limit exceeded: Memory",error.Message);
                }
                Assert.Equal("Id",await command.ExecuteScalarAsync(cancel));
            }
            async Task AssertTableAndKeyMetadataBudget(SqlConnection connection){
                var errors=new List<Exception?>();
                foreach(var (procedure,column,value) in new[]{("sys.sp_tables","TABLE_NAME","Rows"),("sys.sp_pkeys","COLUMN_NAME","Id")}){
                    using var command=new SqlCommand(procedure,connection){CommandType=CommandType.StoredProcedure};
                    command.Parameters.Add("@table_name",SqlDbType.NVarChar,128).Value="Rows";
                    command.Parameters.Add("@table_owner",SqlDbType.NVarChar,128).Value="dbo";
                    command.Parameters.Add("@table_qualifier",SqlDbType.NVarChar,128).Value="sales";
                    if(procedure=="sys.sp_tables"){
                        command.Parameters.Add("@table_type",SqlDbType.NVarChar,128).Value="TABLE";
                    }
                    using(var held=budget.ReserveMemory(budget.AvailableMemoryBytes-64)){
                        errors.Add(await Record.ExceptionAsync(async()=>{
                            await using var unexpected=await command.ExecuteReaderAsync(cancel);
                            await unexpected.ReadAsync(cancel);
                        }));
                    }
                    await using var rows=await command.ExecuteReaderAsync(cancel);
                    Assert.True(await rows.ReadAsync(cancel));
                    Assert.Equal(value,rows.GetString(rows.GetOrdinal(column)));
                    Assert.False(await rows.ReadAsync(cancel));
                }
                Assert.All(errors,error=>{
                    var sqlError=Assert.IsType<SqlException>(error);
                    Assert.Equal(50000,sqlError.Number);
                    Assert.Contains("Resource limit exceeded: Memory",sqlError.Message);
                });
            }
            async Task AssertReadTwice(SqlConnection connection,int id,int count,SqlTransaction? transaction=null){
                using var command=new SqlCommand("dbo.ReadTwice",connection,transaction){CommandType=CommandType.StoredProcedure};
                command.Parameters.Add("@id",SqlDbType.Int).Value=id;
                using var rows=await command.ExecuteReaderAsync(cancel);
                Assert.Equal("Id",rows.GetName(0));
                Assert.True(await rows.ReadAsync(cancel));
                Assert.Equal(id,rows.GetInt32(0));
                Assert.False(await rows.ReadAsync(cancel));
                Assert.True(await rows.NextResultAsync(cancel));
                Assert.Equal(1,rows.FieldCount);
                Assert.True(await rows.ReadAsync(cancel));
                Assert.Equal(count,rows.GetInt32(0));
                Assert.False(await rows.ReadAsync(cancel));
                Assert.False(await rows.NextResultAsync(cancel));
            }
            async Task AssertOutputs(SqlConnection connection,SqlConnection forbidden){
                using var command=new SqlCommand("dbo.SetOutputs",connection){CommandType=CommandType.StoredProcedure};
                var description=command.Parameters.Add("@label",SqlDbType.NVarChar,20);description.Direction=ParameterDirection.Output;
                var id=command.Parameters.Add("@id",SqlDbType.Int);id.Value=2;
                var total=command.Parameters.Add("@total",SqlDbType.BigInt);total.Direction=ParameterDirection.InputOutput;total.Value=4294967296L;
                await command.ExecuteNonQueryAsync(cancel);
                Assert.Equal("集計",description.Value);
                Assert.Equal(4294967298L,total.Value);
                id.Value=3;
                await command.ExecuteNonQueryAsync(cancel);
                Assert.Equal(DBNull.Value,description.Value);
                Assert.Equal(4294967300L,total.Value);
                id.Direction=ParameterDirection.InputOutput;
                Assert.Equal(201,(await Assert.ThrowsAsync<SqlException>(()=>command.ExecuteNonQueryAsync(cancel))).Number);
                id.Direction=ParameterDirection.Input;
                command.Connection=forbidden;
                Assert.Equal(229,(await Assert.ThrowsAsync<SqlException>(()=>command.ExecuteNonQueryAsync(cancel))).Number);
                using var wrapper=new SqlCommand("EXEC dbo.SetOutputs @id, @count OUTPUT, @name OUTPUT",connection);
                wrapper.Parameters.Add("@id",SqlDbType.Int).Value=2;
                var count=wrapper.Parameters.Add("@count",SqlDbType.BigInt);count.Direction=ParameterDirection.InputOutput;count.Value=4294967296L;
                var name=wrapper.Parameters.Add("@name",SqlDbType.NVarChar,20);name.Direction=ParameterDirection.Output;
                await wrapper.ExecuteNonQueryAsync(cancel);
                Assert.Equal(4294967298L,count.Value);
                Assert.Equal("集計",name.Value);
                wrapper.CommandText="SELECT @count";
                Assert.Equal(40515,(await Assert.ThrowsAsync<SqlException>(()=>wrapper.ExecuteNonQueryAsync(cancel))).Number);
                wrapper.CommandText="EXEC dbo.SetOutputs @id, @count OUTPUT, @count OUTPUT";
                Assert.Equal(201,(await Assert.ThrowsAsync<SqlException>(()=>wrapper.ExecuteNonQueryAsync(cancel))).Number);
                const string localBatch="DECLARE @id int=2,@total bigint=4294967296; DECLARE @label nvarchar(20); "+
                    "EXEC dbo.SetOutputs @id, @total OUTPUT, @label OUTPUT; SELECT @total AS Total, @label AS Label;";
                using(var batch=new SqlCommand(localBatch,connection)){
                    using var rows=await batch.ExecuteReaderAsync(cancel);
                    Assert.Equal("Total",rows.GetName(0));
                    Assert.Equal("Label",rows.GetName(1));
                    Assert.True(await rows.ReadAsync(cancel));
                    Assert.Equal(4294967298L,rows.GetInt64(0));
                    Assert.Equal("集計",rows.GetString(1));
                    Assert.False(await rows.ReadAsync(cancel));
                    Assert.False(await rows.NextResultAsync(cancel));
                }
                using(var denied=new SqlCommand(localBatch,forbidden)){
                    Assert.Equal(229,(await Assert.ThrowsAsync<SqlException>(()=>denied.ExecuteReaderAsync(cancel))).Number);
                }
                using(var nullOutput=new SqlCommand(localBatch.Replace("@id int=2","@id int=3",StringComparison.Ordinal),connection)){
                    using var rows=await nullOutput.ExecuteReaderAsync(cancel);
                    Assert.True(await rows.ReadAsync(cancel));
                    Assert.Equal(4294967298L,rows.GetInt64(0));
                    Assert.True(rows.IsDBNull(1));
                }
                Assert.Equal(137,(await Assert.ThrowsAsync<SqlException>(()=>Sql(connection,"SELECT @total"))).Number);
                using(var unsupported=new SqlCommand(localBatch.Replace("SELECT @total AS Total, @label AS Label","SELECT Id FROM dbo.Rows",StringComparison.Ordinal),connection)){
                    Assert.Equal(40515,(await Assert.ThrowsAsync<SqlException>(()=>unsupported.ExecuteReaderAsync(cancel))).Number);
                }
                using(var invalid=new SqlCommand(localBatch.Replace("@label OUTPUT","@total OUTPUT",StringComparison.Ordinal),connection)){
                    Assert.Equal(201,(await Assert.ThrowsAsync<SqlException>(()=>invalid.ExecuteReaderAsync(cancel))).Number);
                }
            }
            async Task AssertMixed(SqlConnection connection,int id,int count,SqlTransaction? transaction=null){
                using var command=new SqlCommand("dbo.ReadMutateRead",connection,transaction){CommandType=CommandType.StoredProcedure};
                command.Parameters.Add("@id",SqlDbType.Int).Value=id;
                using var rows=await command.ExecuteReaderAsync(cancel);
                Assert.True(await rows.ReadAsync(cancel));
                Assert.Equal(count,rows.GetInt32(0));
                Assert.False(await rows.ReadAsync(cancel));
                Assert.True(await rows.NextResultAsync(cancel));
                Assert.True(await rows.ReadAsync(cancel));
                Assert.Equal(id,rows.GetInt32(0));
                Assert.False(await rows.ReadAsync(cancel));
                Assert.False(await rows.NextResultAsync(cancel));
                Assert.Equal(1,rows.RecordsAffected);
            }
            async Task AssertMixedOutputs(){
                using(var write=new SqlCommand("dbo.WriteOutput",client){CommandType=CommandType.StoredProcedure}){
                    write.Parameters.Add("@id",SqlDbType.Int).Value=22;
                    var work=write.Parameters.Add("@work",SqlDbType.Int);work.Direction=ParameterDirection.Output;
                    using(var rows=await write.ExecuteReaderAsync(cancel)){
                        Assert.True(await rows.ReadAsync(cancel));
                        Assert.Equal(23,rows.GetInt32(0));
                        Assert.False(await rows.NextResultAsync(cancel));
                        Assert.Equal(1,rows.RecordsAffected);
                    }
                    Assert.Equal(123,work.Value);
                }
                Assert.Equal(1,await Sql(reader,"DELETE FROM dbo.Rows WHERE Id=23"));
                using(var fail=new SqlCommand("dbo.FailOutput",client){CommandType=CommandType.StoredProcedure}){
                    fail.Parameters.Add("@id",SqlDbType.Int).Value=22;
                    var work=fail.Parameters.Add("@work",SqlDbType.Int);work.Direction=ParameterDirection.InputOutput;work.Value=-1;
                    Assert.Equal(8134,(await Assert.ThrowsAsync<SqlException>(()=>fail.ExecuteReaderAsync(cancel))).Number);
                    Assert.Equal(-1,work.Value);
                    await using var tx=(SqlTransaction)await client.BeginTransactionAsync(cancel);
                    fail.Transaction=tx;
                    Assert.Equal(8134,(await Assert.ThrowsAsync<SqlException>(()=>fail.ExecuteReaderAsync(cancel))).Number);
                    Assert.Equal(1,await Sql(client,"INSERT INTO dbo.Rows VALUES (23)",tx));
                    await tx.CommitAsync(cancel);
                }
                Assert.Equal(1,await Sql(reader,"DELETE FROM dbo.Rows WHERE Id=23"));
                const string overflow="DECLARE @work tinyint; EXEC dbo.WriteOutput 200, @work OUTPUT; SELECT @work AS Value;";
                Assert.Equal(8115,(await Assert.ThrowsAsync<SqlException>(()=>Sql(client,overflow))).Number);
                await AssertRolledBack(reader,201);
                const string read="DECLARE @work int=9; EXEC dbo.ReadOutput 22, @work OUTPUT; SELECT @work AS Value;";
                using var command=new SqlCommand(read,client);
                using var result=await command.ExecuteReaderAsync(cancel);
                foreach(var expected in new[]{("BeforeValue",9),("AfterValue",23),("Value",23)}){
                    Assert.Equal(expected.Item1,result.GetName(0));
                    Assert.True(await result.ReadAsync(cancel));
                    Assert.Equal(expected.Item2,result.GetInt32(0));
                    if(expected.Item1!="Value"){Assert.True(await result.NextResultAsync(cancel));}
                }
                Assert.False(await result.NextResultAsync(cancel));
            }
            async Task AssertResultSendStops(string spool,bool disconnect){
                using var relay=new TcpListener(IPAddress.Loopback,0);
                relay.Start();
                using var upstream=new TcpClient{ReceiveBufferSize=4096};
                await upstream.ConnectAsync(IPAddress.Loopback,server.Port,cancel);
                await using var receiving=Connect(((IPEndPoint)relay.LocalEndpoint).Port,writer);
                var opening=receiving.OpenAsync(cancel);
                using var downstream=await relay.AcceptTcpClientAsync(cancel);
                var paused=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                long sent=0,stopAt=long.MaxValue;
                var forwarded=Task.WhenAll(downstream.GetStream().CopyToAsync(upstream.GetStream(),cancel),ForwardResponse());
                SqlDataReader? result=null;
                try{
                    await opening;
                    Volatile.Write(ref stopAt,Volatile.Read(ref sent)+(32<<10));
                    using var command=new SqlCommand("EXEC dbo.StreamMixed @id=21",receiving);
                    result=await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess,cancel);
                    Assert.True(await result.ReadAsync(cancel));
                    Assert.Equal(21,result.GetInt32(0));
                    await paused.Task.WaitAsync(TimeSpan.FromSeconds(8),cancel);
                    Assert.Single(Directory.EnumerateFileSystemEntries(spool,"scope-*"));
                    if(!disconnect){
                        using var writing=CancellationTokenSource.CreateLinkedTokenSource(cancel);
                        writing.CancelAfter(TimeSpan.FromSeconds(1));
                        while(pendingWrites.UsedBytes==0){await Task.Delay(10,writing.Token);}
                    }
                    using var inspect=new SqlCommand("SELECT Id FROM dbo.Rows WHERE Id=21",reader);
                    Assert.Equal(21,await inspect.ExecuteScalarAsync(cancel));
                    if(disconnect){
                        upstream.LingerState=new LingerOption(true,0);
                        upstream.Dispose();
                        downstream.Dispose();
                        await Assert.ThrowsAsync<SqlException>(async()=>{
                            Assert.True(await result.NextResultAsync(cancel));
                            Assert.True(await result.ReadAsync(cancel));
                            _=await result.GetFieldValueAsync<string>(0,cancel);
                        });
                    }
                    using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancel);
                    deadline.CancelAfter(TimeSpan.FromSeconds(8));
                    while(Directory.EnumerateFileSystemEntries(spool,"scope-*").Any()||pendingWrites.UsedBytes!=0){
                        await Task.Delay(20,deadline.Token);
                    }
                    if(!disconnect){
                        // 読み取りを再開する前に資源が戻り、サーバーが結果の途中で接続を閉じたことを確認する。
                        var buffer=new byte[4096];
                        long received=0;
                        try{
                            var source=upstream.GetStream();
                            int length;
                            while((length=await source.ReadAsync(buffer,deadline.Token))!=0){received+=length;}
                        }catch(IOException error)when(error.InnerException is SocketException{SocketErrorCode:SocketError.ConnectionReset or SocketError.ConnectionAborted}){}
                        Assert.InRange(Volatile.Read(ref sent)+received,1,(4L<<20)-1);
                    }
                    Assert.Equal(1,await Sql(reader,"DELETE FROM dbo.Rows WHERE Id=21"));
                }finally{
                    upstream.Dispose();downstream.Dispose();
                    result?.Dispose();
                    try{await forwarded;}catch(IOException){}catch(ObjectDisposedException){}
                }
                async Task ForwardResponse(){
                    var source=upstream.GetStream();
                    var target=downstream.GetStream();
                    var buffer=new byte[4096];
                    while(true){
                        var remaining=Volatile.Read(ref stopAt)-sent;
                        if(remaining<=0){paused.TrySetResult();return;}
                        var length=await source.ReadAsync(buffer.AsMemory(0,(int)Math.Min(buffer.Length,remaining)),cancel);
                        if(length==0){return;}
                        await target.WriteAsync(buffer.AsMemory(0,length),cancel);
                        Volatile.Write(ref sent,sent+length);
                    }
                }
            }
            async Task AssertRolledBack(SqlConnection connection,int key){
                Assert.Equal(1,await Sql(connection,$"INSERT INTO dbo.Rows VALUES ({key})"));
                Assert.Equal(1,await Sql(connection,$"DELETE FROM dbo.Rows WHERE Id={key}"));
            }
        }finally{File.Delete(pin);}

        Seamless.Security.AuthorizationPolicySnapshot Policy(long version,bool insert,bool delete=true)=>new(version,
            [new(serverId,null,SecurityResourceKind.Server,"gateway"),new(databaseId,serverId,SecurityResourceKind.Database,"sales"),
                new(schemaId,databaseId,SecurityResourceKind.Schema,"dbo"),new(tableId,schemaId,SecurityResourceKind.Table,"Rows"),
                ..procedures.Select(pair=>new SecurityResource(pair.Value,schemaId,SecurityResourceKind.Procedure,pair.Key))],
            [new(writerRole,"writer",[new(databaseId,SecurityPermission.Discover),
                ..procedures.Values.Select(id=>new PermissionRule(id,SecurityPermission.Execute)),
                new(tableId,SecurityPermission.Read|(insert?SecurityPermission.Insert|SecurityPermission.Update:SecurityPermission.None)
                    |(insert&&delete?SecurityPermission.Delete:SecurityPermission.None),delete?SecurityPermission.None:SecurityPermission.Delete)]),
                new(observerRole,"observer",[new(databaseId,SecurityPermission.Discover),new(tableId,SecurityPermission.Read|SecurityPermission.Insert|SecurityPermission.Delete,SecurityPermission.Discover)])],
            [new(writer,SecurityPrincipalKind.User,writerRole),new(observer,SecurityPrincipalKind.User,observerRole)]);
    },includeLoopbackAddress:true);

    private static GeneratedAssemblyBuilder CreateCompiler(string root){
        var references=Directory.EnumerateFiles(AppContext.BaseDirectory,"*.dll").Where(static path=>{
            var name=Path.GetFileName(path);
            return name.StartsWith("MemoryPack",StringComparison.Ordinal)||name.StartsWith("Seamless.",StringComparison.Ordinal)
                &&!name.Contains("Generator",StringComparison.Ordinal)&&!name.StartsWith("Seamless.Management",StringComparison.Ordinal);
        }).ToArray();
        var dotnet=Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"dotnet","dotnet.exe");
        return new(new(Path.Combine(root,"w"),Path.Combine(root,"c"),dotnet,
            typeof(global::Seamless.Database.Generator.DatabaseSchemaGenerator).Assembly.Location,references));
    }

    private sealed class ObservedCatalogSession(IAuthorizationSession inner,Action<CancellationToken> observe):IAuthorizationSession{
        public SecuritySubjectId Subject=>inner.Subject;
        public AuthorizationLease BeginOperation(CancellationToken token=default){observe(token);return inner.BeginOperation(token);}
        public AuthorizationLease BeginTransaction(CancellationToken token=default)=>inner.BeginTransaction(token);
        public void UpdateValidatedIdentity(AuthenticatedIdentity identity)=>inner.UpdateValidatedIdentity(identity);
        public void MarkMembershipUnavailable()=>inner.MarkMembershipUnavailable();
        public void Dispose()=>inner.Dispose();
    }

    private sealed class CatalogWriteGate(PipeWriter inner):PipeWriter{
        private sealed record Pause(TaskCompletionSource Reached,TaskCompletionSource Released);
        private Pause? pause;
        private Memory<byte> pending;
        private bool row;
        public Task PauseNextRow(){
            var next=new Pause(new(TaskCreationOptions.RunContinuationsAsynchronously),new(TaskCreationOptions.RunContinuationsAsynchronously));
            if(Interlocked.CompareExchange(ref this.pause,next,null) is not null){throw new InvalidOperationException("The previous catalog write is still paused.");}
            return next.Reached.Task;
        }
        public void Resume()=>Volatile.Read(ref this.pause)?.Released.TrySetResult();
        public override Memory<byte> GetMemory(int sizeHint=0)=>this.pending=inner.GetMemory(sizeHint);
        public override Span<byte> GetSpan(int sizeHint=0)=>this.GetMemory(sizeHint).Span;
        public override void Advance(int bytes){
            this.row=bytes>8&&this.pending.Span[0]==TdsConstants.PacketTypeTabularResult&&this.pending.Span[8]==TdsConstants.TokenRow;
            this.pending=default;
            inner.Advance(bytes);
        }
        public override void Complete(Exception? exception=null)=>inner.Complete(exception);
        public override void CancelPendingFlush()=>inner.CancelPendingFlush();
        public override async ValueTask<FlushResult> FlushAsync(CancellationToken cancellationToken=default){
            var flushed=await inner.FlushAsync(cancellationToken);
            if(this.row&&!flushed.IsCanceled&&!flushed.IsCompleted&&Volatile.Read(ref this.pause) is{} current){
                current.Reached.TrySetResult();
                try{await current.Released.Task.WaitAsync(cancellationToken);}
                finally{Interlocked.CompareExchange(ref this.pause,null,current);}
            }
            return flushed;
        }
    }

    private sealed class AuthorizationClock:TimeProvider{
        private long utcTicks;
        public override DateTimeOffset GetUtcNow(){
            var ticks=Interlocked.Read(ref this.utcTicks);
            return ticks==0?DateTimeOffset.UtcNow:new(ticks,TimeSpan.Zero);
        }
        public void Freeze()=>Interlocked.Exchange(ref this.utcTicks,DateTime.UtcNow.Ticks);
        public void Advance(TimeSpan duration)=>Interlocked.Add(ref this.utcTicks,duration.Ticks);
    }
}
