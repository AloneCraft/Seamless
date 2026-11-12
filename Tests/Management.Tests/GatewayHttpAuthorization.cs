using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Seamless.Authentication;
using Seamless.Management;
using Seamless.Database.Gateway;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Http;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Gateway.Tds;
using Seamless.Database.Rdbms;
using Seamless.Database.Server;
using Seamless.Management.Server;
using Seamless.Security;
using Policy=Seamless.Security.AuthorizationPolicySnapshot;

namespace Management.Tests;

[Collection("AuditStorage")]
public sealed class GatewayHttpAuthorization{
    [Fact]
    public Task GatewayCommandUsesPersistedIssuerAndWalBoundResources()=>ManagementTransport.WithServerCertificate(async certificate=>{
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();
        var directory=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,Guid.NewGuid().ToString("N"));
        var managementRoot=Path.Combine(directory,"m");var storage=Path.Combine(directory,"db");
        var databaseId=Guid.NewGuid();var schemaId=Guid.NewGuid();var ordersId=Guid.NewGuid();var payrollId=Guid.NewGuid();var roleId=Guid.NewGuid();
        var viewId=Guid.NewGuid();var restrictedViewId=Guid.NewGuid();var scoreId=Guid.NewGuid();var wrapperId=Guid.NewGuid();var restrictedScoreId=Guid.NewGuid();
        var outerViewId=Guid.NewGuid();var delegatedCountId=Guid.NewGuid();var procedureId=Guid.NewGuid();
        var actor=new SecuritySubjectId(issuer.Id,"admin");var reader=new SecuritySubjectId(issuer.Id,"reader");var discoverRole=Guid.NewGuid();var now=DateTimeOffset.UtcNow;
        var writer=new SecuritySubjectId(issuer.Id,"writer");var writerRole=Guid.NewGuid();
        Guid instance;
        using(var store=ManagementStore.Initialize(managementRoot,"admin","TestPassword-123")){
            instance=store.InstanceId;
            var policy=new Policy(1,
                [new(instance,null,SecurityResourceKind.Server,"gateway"),new(databaseId,instance,SecurityResourceKind.Database,"sales"),
                    new(schemaId,databaseId,SecurityResourceKind.Schema,"dbo"),new(ordersId,schemaId,SecurityResourceKind.Table,"Orders"),new(payrollId,schemaId,SecurityResourceKind.Table,"Payroll"),
                    new(viewId,schemaId,SecurityResourceKind.View,"OrderView"),new(restrictedViewId,schemaId,SecurityResourceKind.View,"PayrollView"),
                    new(outerViewId,schemaId,SecurityResourceKind.View,"OuterView"),new(delegatedCountId,schemaId,SecurityResourceKind.ScalarFunction,"DelegatedCount"),
                    new(scoreId,schemaId,SecurityResourceKind.ScalarFunction,"ApprovedScore"),new(wrapperId,schemaId,SecurityResourceKind.ScalarFunction,"ScoreWrapper"),
                    new(restrictedScoreId,schemaId,SecurityResourceKind.ScalarFunction,"RestrictedScore"),
                    new(procedureId,schemaId,SecurityResourceKind.Procedure,"ReadOrder")],
                [new(roleId,"operator",[new(instance,SecurityPermission.ManageServer),new(databaseId,SecurityPermission.Discover|SecurityPermission.ManagePermissions),
                    new(ordersId,SecurityPermission.Discover|SecurityPermission.Read),new(payrollId,SecurityPermission.None,SecurityPermission.Discover|SecurityPermission.Read),
                    new(viewId,SecurityPermission.Read),new(restrictedViewId,SecurityPermission.Read),new(scoreId,SecurityPermission.Execute),new(wrapperId,SecurityPermission.Execute)]),
                    new(discoverRole,"catalogue reader",[new(databaseId,SecurityPermission.Discover),new(viewId,SecurityPermission.Read),
                        new(restrictedViewId,SecurityPermission.Read),new(outerViewId,SecurityPermission.Read),new(delegatedCountId,SecurityPermission.Execute),
                        new(procedureId,SecurityPermission.Execute)]),
                    new(writerRole,"orders writer",[new(databaseId,SecurityPermission.Discover),new(ordersId,SecurityPermission.Read|SecurityPermission.Insert|SecurityPermission.Update|SecurityPermission.Delete)])],
                [new(actor,SecurityPrincipalKind.User,roleId),new(reader,SecurityPrincipalKind.User,discoverRole),new(writer,SecurityPrincipalKind.User,writerRole)]);
            using var authorization=StoredAuthorization.Initialize(store,policy,new(actor,actor.Value,[],now.AddMinutes(5),now),issuer.Settings);
        }
        var number=new DatabaseType(DatabaseTypeKind.Int32,false,SqlTypeName:"int");
        var catalog=new DatabaseCatalog("sales",[new("dbo",[Table("Orders"),Table("Payroll")],
            [View("OrderView","Orders"),View("PayrollView","Payroll"),View("OuterView","OrderView")],
            [Scalar("ApprovedScore","7"),Scalar("ScoreWrapper","dbo.RestrictedScore()"),Scalar("RestrictedScore","9"),
                new("dbo","DelegatedCount",RoutineKind.ScalarFunction,number,[new(1,"@bonus",number,false)],[],
                    "CREATE FUNCTION dbo.DelegatedCount(@bonus int) RETURNS int AS BEGIN DECLARE @n int; SELECT @n=COUNT(*) FROM dbo.Payroll; RETURN @n+@bonus; END",false,0,1),
                new("dbo","ReadOrder",RoutineKind.StoredProcedure,null,[new(1,"@id",number,false)],[],
                    "CREATE PROCEDURE dbo.ReadOrder @id int AS BEGIN SELECT Id FROM dbo.Orders WHERE Id=@id; END",false,1,0)])],"Japanese_CI_AS");
        TableCatalog Table(string name)=>new(name,[new(0,"Id",number,false)],new("PK_"+name,[new("Id",false)],1));
        ViewCatalog View(string name,string table)=>new(name,[new(0,"Id",number,false)],null,$"CREATE VIEW dbo.{name} AS SELECT Id FROM dbo.{table}");
        RoutineCatalog Scalar(string name,string expression)=>new("dbo",name,RoutineKind.ScalarFunction,number,[],[],$"CREATE FUNCTION dbo.{name}() RETURNS int AS BEGIN RETURN {expression}; END",false,0,1);
        var certificatePassword=Guid.NewGuid().ToString("N");
        await File.WriteAllBytesAsync(Path.Combine(directory,"gateway.pfx"),certificate.Export(X509ContentType.Pfx,certificatePassword));
        var tdsPin=Path.Combine(directory,"gateway.cer");
        await File.WriteAllBytesAsync(tdsPin,certificate.Export(X509ContentType.Cert));
        var configuration=Path.Combine(directory,"gateway.json");
        var ready=Path.Combine(directory,"ready.json");var stop=Path.Combine(directory,"stop");

        Guid wal=default,generation=default;
        await RunAsync(null,true,true,record=>{
            Assert.InRange(record.GetProperty("TdsPort").GetInt32(),1,65535);
            Assert.False(record.GetProperty("TdsAuthenticated").GetBoolean());
            Assert.InRange(record.GetProperty("NativePort").GetInt32(),1,65535);
            wal=record.GetProperty("GatewayWalId").GetGuid();
            generation=Assert.Single(record.GetProperty("Databases").EnumerateArray()).GetProperty("GenerationId").GetGuid();
            return Task.CompletedTask;
        });
        Assert.NotEqual(Guid.Empty,wal);
        Assert.NotEqual(Guid.Empty,generation);
        var mapped=new GatewayHttpDatabaseResource(1,databaseId,[new(1000,ordersId),new(1001,payrollId)]){
            SqlObjects=[new(generation,SecurityResourceKind.View,"dbo","OrderView",viewId),new(generation,SecurityResourceKind.View,"dbo","PayrollView",restrictedViewId){DelegatedReadResources=[payrollId]},
                new(generation,SecurityResourceKind.View,"dbo","OuterView",outerViewId){DelegatedReadResources=[ordersId]},
                new(generation,SecurityResourceKind.ScalarFunction,"dbo","DelegatedCount",delegatedCountId){DelegatedReadResources=[payrollId]},
                new(generation,SecurityResourceKind.ScalarFunction,"dbo","ApprovedScore",scoreId),new(generation,SecurityResourceKind.ScalarFunction,"dbo","ScoreWrapper",wrapperId),
                new(generation,SecurityResourceKind.ScalarFunction,"dbo","RestrictedScore",restrictedScoreId),
                new(generation,SecurityResourceKind.Procedure,"dbo","ReadOrder",procedureId){DelegatedReadResources=[ordersId]}]
        };
        var settings=new GatewayHttpAuthorizationConfiguration("m",instance,wal,[mapped]){ManagementEndpoint=new("127.0.0.1",0)};
        var revokedOperation=Guid.NewGuid();
        await RunAsync(settings,false,false);
        await RunAsync(settings with{ManagementInstanceId=Guid.NewGuid()},true,false);
        await RunAsync(settings with{GatewayWalId=Guid.NewGuid()},true,false);
        await RunAsync(settings with{ManagementEndpoint=new("not-an-address",0)},true,false);
        var invalid=mapped with{SqlObjects=mapped.SqlObjects!.Select(item=>item.ResourceId==restrictedViewId?item with{DelegatedReadResources=[viewId]}:item).ToArray()};
        await RunAsync(settings with{Databases=[invalid]},true,false);
        using(var locked=new ManagementStore(managementRoot)){await RunAsync(settings,true,false);}
        await RunAsync(settings,true,true,async record=>{
            Assert.Equal(JsonValueKind.Null,record.GetProperty("TdsPort").ValueKind);
            Assert.False(record.GetProperty("TdsAuthenticated").GetBoolean());
            Assert.Equal(JsonValueKind.Null,record.GetProperty("NativePort").ValueKind);
            using var client=CreateClient(record.GetProperty("HttpPort").GetInt32(),certificate);
            using(var anonymous=await client.GetAsync("/api/databases")){Assert.Equal(HttpStatusCode.Unauthorized,anonymous.StatusCode);}
            client.DefaultRequestHeaders.Authorization=new("Bearer",issuer.CreateToken(OidcDevelopmentIssuer.Audience,actor.Value,scope:"seamless.database"));
            using(var databases=JsonDocument.Parse(await client.GetStringAsync("/api/databases"))){Assert.Equal("sales",Assert.Single(databases.RootElement.EnumerateArray()).GetProperty("Name").GetString());}
            using(var tables=JsonDocument.Parse(await client.GetStringAsync("/api/tables?database=sales"))){Assert.Equal("Orders",Assert.Single(tables.RootElement.EnumerateArray()).GetProperty("name").GetString());}
            using(var rows=await client.GetAsync("/api/tables/dbo.Orders?database=sales")){Assert.Equal(HttpStatusCode.OK,rows.StatusCode);}
            using(var denied=await client.GetAsync("/api/tables/dbo.Payroll?database=sales")){Assert.Equal(HttpStatusCode.Forbidden,denied.StatusCode);}
            const string allowed="WITH OrderRows AS (SELECT Id FROM sales.dbo.Orders) SELECT COUNT(*) AS Total FROM OrderRows";
            using(var sql=await SqlAsync(allowed)){Assert.Equal(HttpStatusCode.OK,sql.StatusCode);}
            using(var view=await SqlAsync("SELECT Id FROM dbo.OrderView")){Assert.Equal(HttpStatusCode.OK,view.StatusCode);}
            using(var scalar=await SqlAsync("SELECT dbo.ApprovedScore() AS Score")){
                var response=await scalar.Content.ReadAsStringAsync();Assert.True(scalar.IsSuccessStatusCode,response);
                using var result=JsonDocument.Parse(response);Assert.Equal(7,Assert.Single(result.RootElement.GetProperty("rows").EnumerateArray()).GetProperty("Score").GetInt32());
            }
            foreach(var query in new[]{"SELECT * FROM dbo.Payroll","SELECT * FROM dbo.Orders CROSS JOIN dbo.Payroll",
                "SELECT * FROM dbo.PayrollView","SELECT dbo.RestrictedScore()","SELECT dbo.ScoreWrapper()",
                "SELECT * FROM master.sys.databases","SELECT * FROM sales.sys.tables","SELECT dbo.UnregisteredFunction()",
                "SELECT Id INTO dbo.Copy FROM dbo.Orders","INSERT INTO dbo.Orders VALUES (1)","USE sales"}){
                using var deniedSql=await SqlAsync(query);Assert.Equal(HttpStatusCode.Forbidden,deniedSql.StatusCode);
            }
            using(var batch=await SqlAsync("SELECT * FROM dbo.Orders; DELETE FROM dbo.Orders")){Assert.Equal(HttpStatusCode.BadRequest,batch.StatusCode);}
            using(var large=await SqlAsync("SELECT 1 --"+new string('x',2048))){
                Assert.Equal(HttpStatusCode.RequestEntityTooLarge,large.StatusCode);Assert.True(large.Headers.ConnectionClose);
            }
            client.DefaultRequestHeaders.Authorization=new("Bearer",issuer.CreateToken(OidcDevelopmentIssuer.Audience,reader.Value,scope:"seamless.database"));
            using(var other=await SqlAsync(allowed)){Assert.Equal(HttpStatusCode.Forbidden,other.StatusCode);}
            using(var view=await SqlAsync("SELECT Id FROM dbo.PayrollView")){Assert.Equal(HttpStatusCode.OK,view.StatusCode);}
            using(var scalar=await SqlAsync("SELECT dbo.DelegatedCount(5) AS Total")){
                var response=await scalar.Content.ReadAsStringAsync();Assert.True(scalar.IsSuccessStatusCode,response);
                using var result=JsonDocument.Parse(response);Assert.Equal(5,Assert.Single(result.RootElement.GetProperty("rows").EnumerateArray()).GetProperty("Total").GetInt32());
            }
            foreach(var query in new[]{"SELECT Id FROM dbo.Payroll","SELECT * FROM dbo.PayrollView CROSS JOIN dbo.Payroll",
                "SELECT Id FROM dbo.OuterView","SELECT dbo.DelegatedCount((SELECT COUNT(*) FROM dbo.Payroll))"}){
                using var refused=await SqlAsync(query);Assert.Equal(HttpStatusCode.Forbidden,refused.StatusCode);
            }
            client.DefaultRequestHeaders.Authorization=new("Bearer",issuer.CreateToken(OidcDevelopmentIssuer.Audience,writer.Value,scope:"seamless.database"));
            foreach(var query in new[]{"INSERT INTO dbo.Orders VALUES (1),(2)","UPDATE sales.dbo.Orders SET Id=Id+2 WHERE Id=1","DELETE FROM dbo.Orders WHERE Id=2"}){
                using var changed=await SqlAsync(query);Assert.True(changed.IsSuccessStatusCode,await changed.Content.ReadAsStringAsync());
            }
            foreach(var query in new[]{"INSERT INTO dbo.Orders VALUES (8),(dbo.RestrictedScore())","UPDATE dbo.Orders SET Id=dbo.RestrictedScore()",
                "DELETE FROM dbo.Orders WHERE dbo.RestrictedScore()=1","INSERT INTO dbo.Payroll VALUES (9)",
                "WITH Orders AS (SELECT Id FROM dbo.Payroll) DELETE FROM Orders","INSERT INTO master.dbo.Orders VALUES (7)"}){
                using var deniedWrite=await SqlAsync(query);Assert.Equal(HttpStatusCode.Forbidden,deniedWrite.StatusCode);
            }
            using(var remaining=await SqlAsync("SELECT Id FROM dbo.Orders")){
                using var result=JsonDocument.Parse(await remaining.Content.ReadAsStringAsync());
                Assert.Equal(3,Assert.Single(result.RootElement.GetProperty("rows").EnumerateArray()).GetProperty("Id").GetInt32());
            }
            using(var opened=await client.PostAsync("/api/session",null)){
                Assert.Equal(HttpStatusCode.Created,opened.StatusCode);
                using var info=JsonDocument.Parse(await opened.Content.ReadAsStringAsync());
                client.DefaultRequestHeaders.Add("X-Seamless-Session",info.RootElement.GetProperty("id").GetString());
            }
            var original=issuer.CreateToken(OidcDevelopmentIssuer.Audience,writer.Value,scope:"seamless.database",lifetime:TimeSpan.FromSeconds(4));
            client.DefaultRequestHeaders.Authorization=new("Bearer",original);
            await BeginAsync();
            using(var pending=await SqlAsync("INSERT INTO dbo.Orders VALUES (4)")){Assert.Equal(HttpStatusCode.OK,pending.StatusCode);}
            client.DefaultRequestHeaders.Authorization=new("Bearer",issuer.CreateToken(OidcDevelopmentIssuer.Audience,writer.Value,scope:"seamless.database",nonce:"different"));
            using(var deniedCommit=await SqlAsync("COMMIT TRANSACTION")){Assert.Equal(HttpStatusCode.Conflict,deniedCommit.StatusCode);}
            client.DefaultRequestHeaders.Authorization=new("Bearer",original);
            await Task.Delay(TimeSpan.FromSeconds(5));
            using(var committed=await SqlAsync("COMMIT TRANSACTION")){Assert.True(committed.IsSuccessStatusCode,await committed.Content.ReadAsStringAsync());}
            client.DefaultRequestHeaders.Remove("X-Seamless-Transaction");
            using(var expired=await SqlAsync("SELECT Id FROM dbo.Orders")){Assert.Equal(HttpStatusCode.Unauthorized,expired.StatusCode);}
            client.DefaultRequestHeaders.Authorization=new("Bearer",issuer.CreateToken(OidcDevelopmentIssuer.Audience,writer.Value,scope:"seamless.database"));
            using(var committedRow=await SqlAsync("SELECT Id FROM dbo.Orders WHERE Id=4")){
                using var result=JsonDocument.Parse(await committedRow.Content.ReadAsStringAsync());
                Assert.Equal(4,Assert.Single(result.RootElement.GetProperty("rows").EnumerateArray()).GetProperty("Id").GetInt32());
            }
            await BeginAsync();
            using(var pending=await SqlAsync("DELETE FROM dbo.Orders WHERE Id=3")){Assert.Equal(HttpStatusCode.OK,pending.StatusCode);}
            using(var rolledBack=await SqlAsync("ROLLBACK TRANSACTION")){Assert.Equal(HttpStatusCode.OK,rolledBack.StatusCode);}
            client.DefaultRequestHeaders.Remove("X-Seamless-Transaction");
            using(var delete=await SqlAsync("DELETE FROM dbo.Orders WHERE Id=4")){Assert.Equal(HttpStatusCode.OK,delete.StatusCode);}
            using(var closed=await client.DeleteAsync("/api/session")){Assert.Equal(HttpStatusCode.OK,closed.StatusCode);}

            var management=new ManagementClient("localhost",record.GetProperty("AuthorizationManagementPort").GetInt32(),certificate);
            client.DefaultRequestHeaders.Remove("X-Seamless-Session");
            var access=issuer.CreateToken(OidcDevelopmentIssuer.Audience,actor.Value,scope:"seamless.database");
            var before=await management.DatabaseAuthorizationAsync(access,databaseId);
            var change=new DatabaseAuthorizationEdit(revokedOperation,databaseId,before.Version){Unassign=[new(reader,SecurityPrincipalKind.User,discoverRole)]};
            var revoked=await management.ChangeDatabaseAuthorizationAsync(access,change);
            Assert.Equal(before.Version+1,revoked.Version);
            Assert.Equal(revoked,await management.AuthorizationChangeResultAsync(access,revokedOperation));
            client.DefaultRequestHeaders.Authorization=new("Bearer",issuer.CreateToken(OidcDevelopmentIssuer.Audience,reader.Value,scope:"seamless.database"));
            using(var hidden=JsonDocument.Parse(await client.GetStringAsync("/api/databases"))){Assert.Empty(hidden.RootElement.EnumerateArray());}
            await management.ChangeDatabaseAuthorizationAsync(access,new(Guid.NewGuid(),databaseId,revoked.Version){Assign=[new(reader,SecurityPrincipalKind.User,discoverRole)]});
            using(var restored=JsonDocument.Parse(await client.GetStringAsync("/api/databases"))){Assert.Single(restored.RootElement.EnumerateArray());}

            async Task BeginAsync(){
                using var response=await SqlAsync("BEGIN TRANSACTION");
                var body=await response.Content.ReadAsStringAsync();Assert.True(response.IsSuccessStatusCode,body);
                using var state=JsonDocument.Parse(body);
                client.DefaultRequestHeaders.Add("X-Seamless-Transaction",state.RootElement.GetProperty("session").GetProperty("transactionId").GetGuid().ToString());
            }
            Task<HttpResponseMessage> SqlAsync(string query)=>client.PostAsync("/api/sql",new StringContent(JsonSerializer.Serialize(new{query})));
        });
        var stale=mapped with{SqlObjects=mapped.SqlObjects!.Select(item=>item with{GenerationId=Guid.NewGuid()}).ToArray()};
        await RunAsync(settings with{Databases=[stale],MaximumTransactionDuration=TimeSpan.FromSeconds(2)},true,true,async record=>{
            var management=new ManagementClient("localhost",record.GetProperty("AuthorizationManagementPort").GetInt32(),certificate);
            var access=issuer.CreateToken(OidcDevelopmentIssuer.Audience,actor.Value,scope:"seamless.database");
            Assert.Equal(revokedOperation,(await management.AuthorizationChangeResultAsync(access,revokedOperation)).OperationId);
            using var client=CreateClient(record.GetProperty("HttpPort").GetInt32(),certificate);
            client.DefaultRequestHeaders.Authorization=new("Bearer",issuer.CreateToken(OidcDevelopmentIssuer.Audience,actor.Value,scope:"seamless.database"));
            foreach(var query in new[]{"SELECT Id FROM dbo.OrderView","SELECT dbo.ApprovedScore()"}){
                using var response=await client.PostAsync("/api/sql",new StringContent(JsonSerializer.Serialize(new{query})));Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode);
            }
            using(var persisted=await client.PostAsync("/api/sql",new StringContent("{\"query\":\"SELECT Id FROM dbo.Orders\"}"))){
                using var result=JsonDocument.Parse(await persisted.Content.ReadAsStringAsync());
                Assert.Equal(3,Assert.Single(result.RootElement.GetProperty("rows").EnumerateArray()).GetProperty("Id").GetInt32());
            }
            var oversized="SELECT 1 --"+new string('x',1048576);
            using var rejected=await client.PostAsync("/api/sql",new StringContent(JsonSerializer.Serialize(new{query=oversized})));
            Assert.Equal(HttpStatusCode.BadRequest,rejected.StatusCode);
            using var message=JsonDocument.Parse(await rejected.Content.ReadAsStringAsync());
            Assert.Equal("SQL text exceeds the query limit.",message.RootElement.GetProperty("error").GetString());
            client.DefaultRequestHeaders.Authorization=new("Bearer",issuer.CreateToken(OidcDevelopmentIssuer.Audience,writer.Value,scope:"seamless.database"));
            using(var opened=await client.PostAsync("/api/session",null)){
                using var info=JsonDocument.Parse(await opened.Content.ReadAsStringAsync());
                client.DefaultRequestHeaders.Add("X-Seamless-Session",info.RootElement.GetProperty("id").GetString());
            }
            using(var begin=await client.PostAsync("/api/sql",new StringContent("{\"query\":\"BEGIN TRANSACTION\"}"))){
                using var state=JsonDocument.Parse(await begin.Content.ReadAsStringAsync());
                client.DefaultRequestHeaders.Add("X-Seamless-Transaction",state.RootElement.GetProperty("session").GetProperty("transactionId").GetGuid().ToString());
            }
            using(var pending=await client.PostAsync("/api/sql",new StringContent("{\"query\":\"INSERT INTO dbo.Orders VALUES (8)\"}"))){Assert.Equal(HttpStatusCode.OK,pending.StatusCode);}
            await Task.Delay(TimeSpan.FromSeconds(3));
            using(var expired=await client.PostAsync("/api/sql",new StringContent("{\"query\":\"COMMIT TRANSACTION\"}"))){Assert.Contains(expired.StatusCode,new[]{HttpStatusCode.NotFound,HttpStatusCode.Conflict});}
            client.DefaultRequestHeaders.Remove("X-Seamless-Transaction");client.DefaultRequestHeaders.Remove("X-Seamless-Session");
            using(var rolledBack=await client.PostAsync("/api/sql",new StringContent("{\"query\":\"SELECT Id FROM dbo.Orders\"}"))){
                using var result=JsonDocument.Parse(await rolledBack.Content.ReadAsStringAsync());
                Assert.Equal(3,Assert.Single(result.RootElement.GetProperty("rows").EnumerateArray()).GetProperty("Id").GetInt32());
            }
        },maximumRequestBodyBytes:2*1024*1024);

        var tdsOnly=settings with{ProtectHttp=false,ProtectTds=true};
        await RunAsync(null,false,false,useTdsCertificate:true);
        await RunAsync(tdsOnly,false,false);
        await RunAsync(tdsOnly,false,false,useTdsCertificate:true,tdsEnabled:false);
        var requestLog=Path.Combine(directory,"protected-tds.jsonl");
        await RunAsync(tdsOnly,false,false,useTdsCertificate:true,requestLog:requestLog);
        Assert.False(File.Exists(requestLog));
        await RunAsync(tdsOnly,false,true,async record=>{
            Assert.True(record.GetProperty("TdsAuthenticated").GetBoolean());
            Assert.Equal(JsonValueKind.Null,record.GetProperty("HttpPort").ValueKind);
            Assert.Equal(JsonValueKind.Null,record.GetProperty("NativePort").ValueKind);
            await using(var denied=ConnectTds(record,issuer.CreateToken(OidcDevelopmentIssuer.ClientId,writer.Value))){
                var rejected=await Assert.ThrowsAsync<SqlException>(()=>denied.OpenAsync());
                Assert.True(rejected.Number==18456,rejected.ToString());
            }
            await using var connection=ConnectTds(record,Token(writer));
            await connection.OpenAsync();
            using var select=new SqlCommand("SELECT Id FROM dbo.Orders WHERE Id=@id",connection);
            select.Parameters.AddWithValue("@id",3);
            Assert.Equal(3,await select.ExecuteScalarAsync());
            await using var caller=ConnectTds(record,Token(reader));
            await caller.OpenAsync();
            using var invoke=ReadOrder(caller);
            Assert.Equal(3,await invoke.ExecuteScalarAsync());
            using var direct=new SqlCommand("SELECT Id FROM dbo.Orders",caller);
            Assert.Equal(229,(await Assert.ThrowsAsync<SqlException>(()=>direct.ExecuteScalarAsync())).Number);
        },useTdsCertificate:true);

        var staleProcedure=mapped with{SqlObjects=mapped.SqlObjects!.Select(item=>item.ResourceId==procedureId?item with{GenerationId=Guid.NewGuid()}:item).ToArray()};
        await RunAsync(tdsOnly with{Databases=[staleProcedure]},false,true,async record=>{
            await using var caller=ConnectTds(record,Token(reader));
            await caller.OpenAsync();
            using var invoke=ReadOrder(caller);
            Assert.Equal(229,(await Assert.ThrowsAsync<SqlException>(()=>invoke.ExecuteScalarAsync())).Number);
        },useTdsCertificate:true);

        await RunAsync(settings with{ProtectTds=true},true,true,async record=>{
            Assert.True(record.GetProperty("TdsAuthenticated").GetBoolean());
            Assert.Equal(JsonValueKind.Null,record.GetProperty("NativePort").ValueKind);
            using var client=CreateClient(record.GetProperty("HttpPort").GetInt32(),certificate);
            using(var anonymous=await client.GetAsync("/api/databases")){Assert.Equal(HttpStatusCode.Unauthorized,anonymous.StatusCode);}
            var writerToken=Token(writer);
            var management=new ManagementClient("localhost",record.GetProperty("AuthorizationManagementPort").GetInt32(),certificate);
            await using var connection=ConnectTds(record,writerToken);await connection.OpenAsync();
            await using var pending=(SqlTransaction)await connection.BeginTransactionAsync();
            using(var insert=new SqlCommand("INSERT INTO dbo.Orders VALUES (10)",connection,pending)){Assert.Equal(1,await insert.ExecuteNonQueryAsync());}
            var change=new AuthorizationSubjectChange(Guid.NewGuid(),writer,true){ExpectedInstanceId=instance};
            var stopped=await management.ChangeAuthorizationSubjectAsync(Token(actor),change);
            Assert.True(stopped.Disabled);
            client.DefaultRequestHeaders.Authorization=new("Bearer",writerToken);
            using(var forbidden=await client.GetAsync("/api/databases")){Assert.Equal(HttpStatusCode.Forbidden,forbidden.StatusCode);}
            var deniedCommit=await Record.ExceptionAsync(()=>pending.CommitAsync());
            Assert.True(deniedCommit is SqlException or InvalidOperationException,deniedCommit?.Message);
            var enabled=await management.ChangeAuthorizationSubjectAsync(Token(actor),change with{OperationId=Guid.NewGuid(),Disabled=false});
            Assert.False(enabled.Disabled);
            await using var reopened=ConnectTds(record,writerToken);await reopened.OpenAsync();
            await using var rollback=(SqlTransaction)await reopened.BeginTransactionAsync();
            using(var insert=new SqlCommand("INSERT INTO dbo.Orders VALUES (10)",reopened,rollback)){Assert.Equal(1,await insert.ExecuteNonQueryAsync());}
            await rollback.RollbackAsync();
        },useTdsCertificate:true);

        string Token(SecuritySubjectId subject)=>issuer.CreateToken(OidcDevelopmentIssuer.Audience,subject.Value,scope:"seamless.database");
        static SqlCommand ReadOrder(SqlConnection connection){
            var command=new SqlCommand("dbo.ReadOrder",connection){CommandType=global::System.Data.CommandType.StoredProcedure};
            command.Parameters.Add("@id",global::System.Data.SqlDbType.Int).Value=3;
            return command;
        }
        SqlConnection ConnectTds(JsonElement record,string token){
            var builder=new SqlConnectionStringBuilder{DataSource=$"tcp:127.0.0.1,{record.GetProperty("TdsPort").GetInt32()}",InitialCatalog="sales",
                Encrypt=SqlConnectionEncryptOption.Strict,Pooling=false,ConnectTimeout=5,ConnectRetryCount=0};
            builder["Server Certificate"]=tdsPin;
            return new(builder.ConnectionString){AccessToken=token};
        }

        async Task RunAsync(GatewayHttpAuthorizationConfiguration? protection,bool useCertificate,bool expectReady,Func<JsonElement,Task>? inspect=null,int maximumRequestBodyBytes=1024,
            bool useTdsCertificate=false,bool tdsEnabled=true,string? requestLog=null){
            File.Delete(ready);File.Delete(stop);
            var dotnet=Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"dotnet","dotnet.exe");
            var config=new{
                StorageDirectory=storage,WorkingDirectory=Path.Combine(directory,"w"),CacheDirectory=Path.Combine(directory,"c"),DotNetExecutable=dotnet,
                GeneratorAssemblyPath=typeof(global::Seamless.Database.Generator.DatabaseSchemaGenerator).Assembly.Location,
                InitialCatalogs=new[]{catalog},HttpCertificatePath=useCertificate?"gateway.pfx":null,
                TdsCertificatePath=useTdsCertificate?"gateway.pfx":null,Authorization=protection?.ProtectTds==true?protection:null,
                HttpAuthorization=protection?.ProtectTds==true?null:protection,
                DatabaseOptions=new{IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20},
                Endpoints=new{TdsPort=0,TdsEnabled=tdsEnabled,HttpPort=0,NativePort=0,RequestLogPath=requestLog,Http=new{ListenAddress="127.0.0.1",MaximumRequestBodyBytes=maximumRequestBodyBytes,MaximumBufferedRequestBodyBytes=maximumRequestBodyBytes}}
            };
            await File.WriteAllTextAsync(configuration,JsonSerializer.Serialize(config));
            var start=new ProcessStartInfo(dotnet){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardOutput=true,RedirectStandardError=true};
            start.Environment["SEAMLESS_GATEWAY_HTTP_CERT_PASSWORD"]=certificatePassword;
            start.Environment["SEAMLESS_GATEWAY_TDS_CERT_PASSWORD"]=certificatePassword;
            if(Environment.GetEnvironmentVariable("SEAMLESS_TEST_PACKAGED_GATEWAY") is{} packaged){
                Assert.True(Path.IsPathFullyQualified(packaged)&&File.Exists(packaged),"The specified Gateway package entry point is missing.");
                start.ArgumentList.Add(packaged);
            }else{
                start.ArgumentList.Add(typeof(CrashProbe).Assembly.Location);start.ArgumentList.Add("--gateway-cli");
            }
            foreach(var argument in new[]{"--gateway",configuration,"--ready-file",ready,"--stop-file",stop}){start.ArgumentList.Add(argument);}
            using var process=Process.Start(start)!;var created=process.StartTime.ToUniversalTime();
            var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(110));
            try{
                if(expectReady){
                    while(!File.Exists(ready)&&!process.HasExited){await Task.Delay(50,timeout.Token);}
                    if(process.HasExited){Assert.Fail((await output)+(await error));}
                    using var record=JsonDocument.Parse(await File.ReadAllTextAsync(ready,timeout.Token));
                    Assert.Equal(process.Id,record.RootElement.GetProperty("Pid").GetInt32());
                    Assert.Equal(created,record.RootElement.GetProperty("CreatedUtc").GetDateTime());
                    await inspect!(record.RootElement);await File.WriteAllTextAsync(stop,"stop",timeout.Token);
                }
                await process.WaitForExitAsync(timeout.Token);
                Assert.True(process.ExitCode==(expectReady?0:1),(await output)+(await error));
                Assert.Equal(expectReady,File.Exists(ready));
            }finally{
                if(!process.HasExited&&process.StartTime.ToUniversalTime()==created){process.Kill(entireProcessTree:true);await process.WaitForExitAsync();}
            }
        }
    },includeLoopbackAddress:true);

    [Fact]
    public Task AccessTokensFilterDatabasesAndBindSessionsToTheirSubject()=>ManagementTransport.WithServerCertificate(async certificate=>{
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();
        using var verifier=new OidcTokenVerifier(issuer.Settings);
        var serverId=Guid.NewGuid();
        var visibleId=Guid.NewGuid();
        var hiddenId=Guid.NewGuid();
        var role=new SecurityRole(Guid.NewGuid(),"discover",[new(visibleId,SecurityPermission.Discover)]);
        SecurityResource[] resources=[new(serverId,null,SecurityResourceKind.Server,"gateway"),new(visibleId,serverId,SecurityResourceKind.Database,"visible"),new(hiddenId,serverId,SecurityResourceKind.Database,"hidden")];
        using var policy=new AuthorizationManager(new(1,resources,[role],[new(new(issuer.Id,"developers"),SecurityPrincipalKind.Group,role.Id)]));
        var visible=new CatalogueAdapter("visible",[new("dbo","payroll",[],null,1000)]);
        var registry=new DatabaseRegistry();
        registry.Register("visible",visible);
        registry.Register("hidden",new CatalogueAdapter("hidden"));
        var ids=new Dictionary<string,Guid>(StringComparer.OrdinalIgnoreCase){{"visible",visibleId},{"hidden",hiddenId}};
        var policyAvailable=1;
        var wrongSubject=0;
        var wrongDatabase=0;
        var protection=new HttpGatewayAuthorization(verifier,BeginOperationAsync,ResolveDatabase);
        var endpoints=new GatewayServerOptions{TdsEnabled=false,NativePort=0,HttpPort=0,Http=new(){ListenAddress=IPAddress.Loopback,Certificate=certificate}};
        Assert.Contains("unauthenticated TDS or native",Assert.Throws<ArgumentException>(()=>new GatewayServer(registry,endpoints,protection)).Message);
        endpoints.NativeEnabled=false;
        endpoints.TdsEnabled=true;
        Assert.Contains("unauthenticated TDS or native",Assert.Throws<ArgumentException>(()=>new GatewayServer(registry,endpoints,protection)).Message);
        using var server=new HttpServer(0,visible,new SqlExecutionEngine(registry),new(){ListenAddress=IPAddress.Loopback,Certificate=certificate},protection);
        server.Start();
        using var client=CreateClient(server.Port,certificate);
        var owner=issuer.CreateToken(OidcDevelopmentIssuer.Audience,"alice",scope:"seamless.database");
        var other=issuer.CreateToken(OidcDevelopmentIssuer.Audience,"bob",scope:"seamless.database");
        var idToken=issuer.CreateToken(OidcDevelopmentIssuer.ClientId,"alice");
        using(var preflight=await Send(HttpMethod.Options,"/api/databases",null)){
            Assert.Equal(HttpStatusCode.NoContent,preflight.StatusCode);
            Assert.Contains("Authorization",Assert.Single(preflight.Headers.GetValues("Access-Control-Allow-Headers")),StringComparison.OrdinalIgnoreCase);
        }
        using(var rejected=await Send(HttpMethod.Get,"/api/databases",idToken)){
            Assert.Equal(HttpStatusCode.Unauthorized,rejected.StatusCode);
            Assert.Equal("*",Assert.Single(rejected.Headers.GetValues("Access-Control-Allow-Origin")));
            Assert.Equal("Bearer",Assert.Single(rejected.Headers.WwwAuthenticate).Scheme);
        }
        using(var response=await Send(HttpMethod.Get,"/api/databases?database=hidden",owner)){
            Assert.Equal(HttpStatusCode.OK,response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore);
            using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("visible",Assert.Single(json.RootElement.EnumerateArray()).GetProperty("Name").GetString());
        }
        using(var denied=await Send(HttpMethod.Post,"/api/session?database=hidden",owner)){Assert.Equal(HttpStatusCode.Forbidden,denied.StatusCode);}
        using var created=await Send(HttpMethod.Post,"/api/session?database=visible",owner);
        Assert.Equal(HttpStatusCode.Created,created.StatusCode);
        using var session=JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var token=session.RootElement.GetProperty("id").GetString();
        using(var foreign=await Send(HttpMethod.Get,"/api/session",other,token)){Assert.Equal(HttpStatusCode.NotFound,foreign.StatusCode);}
        using(var foreign=await Send(HttpMethod.Delete,"/api/session",other,token)){Assert.Equal(HttpStatusCode.NotFound,foreign.StatusCode);}
        // A database can be visible before the host supplies an object-to-policy binding.
        using(var blocked=await Send(HttpMethod.Get,"/api/tables",owner,token)){Assert.Equal(HttpStatusCode.Forbidden,blocked.StatusCode);}
        using(var blocked=await Send(HttpMethod.Get,"/api/tables/dbo.payroll",owner,token)){Assert.Equal(HttpStatusCode.Forbidden,blocked.StatusCode);}
        using(var blocked=await Send(HttpMethod.Post,"/api/sql",owner,token)){Assert.Equal(HttpStatusCode.Forbidden,blocked.StatusCode);}
        using(var own=await Send(HttpMethod.Get,"/api/session",owner,token)){Assert.Equal(HttpStatusCode.OK,own.StatusCode);}

        Volatile.Write(ref wrongDatabase,1);
        using(var mismatched=await Send(HttpMethod.Post,"/api/session?database=hidden",owner)){Assert.Equal(HttpStatusCode.Forbidden,mismatched.StatusCode);}
        Volatile.Write(ref wrongDatabase,0);
        Volatile.Write(ref wrongSubject,1);
        using(var mismatched=await Send(HttpMethod.Get,"/api/session",other,token)){Assert.Equal(HttpStatusCode.ServiceUnavailable,mismatched.StatusCode);}
        Volatile.Write(ref wrongSubject,0);

        Volatile.Write(ref policyAvailable,0);
        using(var unavailable=await Send(HttpMethod.Get,"/api/databases",owner)){
            Assert.Equal(HttpStatusCode.ServiceUnavailable,unavailable.StatusCode);
            Assert.Equal("*",Assert.Single(unavailable.Headers.GetValues("Access-Control-Allow-Origin")));
            Assert.True(unavailable.Headers.CacheControl?.NoStore);
        }
        using(var unavailable=await Send(HttpMethod.Delete,"/api/session",owner,token)){Assert.Equal(HttpStatusCode.ServiceUnavailable,unavailable.StatusCode);}
        Volatile.Write(ref policyAvailable,1);
        using(var restored=await Send(HttpMethod.Get,"/api/session",owner,token)){Assert.Equal(HttpStatusCode.OK,restored.StatusCode);}

        policy.PublishPolicy(new(2,resources,[],[]));
        using(var revoked=await Send(HttpMethod.Get,"/api/session",owner,token)){Assert.Equal(HttpStatusCode.Forbidden,revoked.StatusCode);}
        using(var closed=await Send(HttpMethod.Delete,"/api/session",owner,token)){Assert.Equal(HttpStatusCode.OK,closed.StatusCode);}
        policy.SetDisabled(new(issuer.Id,"alice"),true);
        using(var disabled=await Send(HttpMethod.Get,"/api/databases",owner)){Assert.Equal(HttpStatusCode.Forbidden,disabled.StatusCode);}
        issuer.MembershipUnavailable=true;
        using(var unavailable=await Send(HttpMethod.Get,"/api/databases",other)){Assert.Equal(HttpStatusCode.ServiceUnavailable,unavailable.StatusCode);}

        async ValueTask<AuthorizationLease?> BeginOperationAsync(AuthenticatedIdentity identity,CancellationToken cancellation){
            await Task.Yield();
            if(Volatile.Read(ref policyAvailable)==0){return null;}
            if(Volatile.Read(ref wrongSubject)==1){
                identity=new(new(issuer.Id,"alice"),identity.DisplayName,identity.Groups,identity.ExpiresAt,identity.MembershipVerifiedAt);
            }
            return policy.CreateSession(identity).BeginOperation(cancellation);
        }

        Guid? ResolveDatabase(string name){
            if(name=="hidden"&&Volatile.Read(ref wrongDatabase)==1){return visibleId;}
            if(ids.TryGetValue(name,out var id)){return id;}
            return null;
        }

        async Task<HttpResponseMessage> Send(HttpMethod method,string path,string? accessToken,string? sessionToken=null){
            using var request=new HttpRequestMessage(method,path);
            request.Headers.Add("Origin","https://app.example");
            if(method==HttpMethod.Options){
                request.Headers.Add("Access-Control-Request-Method","GET");
                request.Headers.Add("Access-Control-Request-Headers","authorization");
            }
            if(accessToken is not null){request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",accessToken);}
            if(sessionToken is not null){request.Headers.Add("X-Seamless-Session",sessionToken);}
            return await client.SendAsync(request);
        }
    });

    [Theory]
    [InlineData(8192,0,1,true,false)]
    [InlineData(64,8192,1,true,true)]
    [InlineData(8192,16384,96,true,true)]
    [InlineData(8192,0,96,false,false)]
    public async Task DatabaseListBuffersWithinBudgetBeforeSending(int memoryLimit,int storageLimit,int count,bool success,bool spills){
        var directory=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,Guid.NewGuid().ToString("N"));
        var budget=new global::Seamless.Execution.ResourceBudget(memoryLimit,storageLimit);
        var adapter=new CatalogueAdapter("visible");
        var registry=new DatabaseRegistry();
        var names=Enumerable.Range(0,count).Select(index=>$"db_{index:D3}_"+new string('x',64)).ToArray();
        for(var index=0;index<names.Length;index++){
            registry.Register(names[index],adapter,readOnly:index%2==0);
        }
        using var server=new HttpServer(0,adapter,new SqlExecutionEngine(registry),new(){
            ListenAddress=IPAddress.Loopback,
            MaximumPendingWriteBytes=128,
            Execution=new(){ParentBudget=budget,MemoryLimitBytes=memoryLimit,TemporaryStorageLimitBytes=storageLimit,TemporaryDirectory=directory}
        });
        server.Start();
        using var client=new HttpClient(new SocketsHttpHandler{UseProxy=false}){
            BaseAddress=new($"http://127.0.0.1:{server.Port}")
        };
        using var response=await client.GetAsync("/api/databases");
        var bytes=await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(bytes.LongLength,response.Content.Headers.ContentLength);
        using var result=JsonDocument.Parse(bytes);
        if(success){
            Assert.Equal(HttpStatusCode.OK,response.StatusCode);
            Assert.Equal(names,result.RootElement.EnumerateArray().Select(value=>value.GetProperty("Name").GetString()));
            Assert.Equal(Enumerable.Range(0,count).Select(index=>index%2==0),result.RootElement.EnumerateArray().Select(value=>value.GetProperty("IsReadOnly").GetBoolean()));
        }else{
            Assert.Equal(HttpStatusCode.InternalServerError,response.StatusCode);
            Assert.Contains("TemporaryStorage",result.RootElement.GetProperty("error").GetString());
        }
        await server.StopAsync();
        Assert.Equal(0,budget.MemoryBytes);
        Assert.Equal(0,budget.TemporaryStorageBytes);
        Assert.Equal(spills,budget.PeakTemporaryStorageBytes>0);
        if(!spills&&success){
            Assert.InRange(budget.PeakMemoryBytes,1,memoryLimit);
            Assert.False(Directory.Exists(directory));
        }
        if(Directory.Exists(directory)){
            Assert.Empty(Directory.EnumerateFileSystemEntries(directory));
        }
    }

    [Fact]
    public Task NextHttpRequestUsesCommittedPolicyFromManagementStore()=>ManagementTransport.WithServerCertificate(async certificate=>{
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();
        using var verifier=new OidcTokenVerifier(issuer.Settings);
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,Guid.NewGuid().ToString("N"));
        using var store=ManagementStore.Initialize(root,"admin","TestPassword-123");
        var databaseId=Guid.NewGuid();
        var admin=new SecuritySubjectId(issuer.Id,"policy-admin");
        var group=new SecuritySubjectId(issuer.Id,"developers");
        var serverRole=new SecurityRole(Guid.NewGuid(),"admin",[new(store.InstanceId,SecurityPermission.All)]);
        var readerRole=new SecurityRole(Guid.NewGuid(),"reader",[new(databaseId,SecurityPermission.Discover)]);
        var readerBinding=new RoleBinding(group,SecurityPrincipalKind.Group,readerRole.Id);
        var policy=new Policy(1,
            [new(store.InstanceId,null,SecurityResourceKind.Server,"gateway"),new(databaseId,store.InstanceId,SecurityResourceKind.Database,"visible")],
            [serverRole,readerRole],[new(admin,SecurityPrincipalKind.User,serverRole.Id),readerBinding]);
        var now=DateTimeOffset.UtcNow;
        var administrator=new AuthenticatedIdentity(admin,admin.Value,[],now.AddHours(1),now);
        using var writer=StoredAuthorization.Initialize(store,policy,administrator);
        using var reader=StoredAuthorization.Open(store);
        var adapter=new CatalogueAdapter("visible");
        var registry=new DatabaseRegistry();
        registry.Register("visible",adapter);
        var protection=new HttpGatewayAuthorization(verifier,
            (identity,cancellation)=>reader.CreateSession(identity).BeginOperation(cancellation),
            name=>string.Equals(name,"visible",StringComparison.OrdinalIgnoreCase)?databaseId:null);
        using var server=new HttpServer(0,adapter,new SqlExecutionEngine(registry),new(){ListenAddress=IPAddress.Loopback,Certificate=certificate},protection);
        server.Start();
        using var client=CreateClient(server.Port,certificate);
        client.DefaultRequestHeaders.Authorization=new("Bearer",issuer.CreateToken(OidcDevelopmentIssuer.Audience,"alice",scope:"seamless.database"));
        using(var before=await client.GetAsync("/api/databases")){
            before.EnsureSuccessStatusCode();
            using var result=JsonDocument.Parse(await before.Content.ReadAsStringAsync());
            Assert.Equal("visible",Assert.Single(result.RootElement.EnumerateArray()).GetProperty("Name").GetString());
        }
        using(var operation=writer.CreateSession(administrator).BeginOperation()){
            Assert.Equal(2,writer.ApplyDatabaseChange(operation,new(databaseId,1,unassign:[readerBinding])).Version);
        }
        using var after=await client.GetAsync("/api/databases");
        after.EnsureSuccessStatusCode();
        using var resultAfter=JsonDocument.Parse(await after.Content.ReadAsStringAsync());
        Assert.Empty(resultAfter.RootElement.EnumerateArray());
    });

    [Fact]
    public Task TableEndpointsUseStableIdentityAndEnforceObjectPermissions()=>ManagementTransport.WithServerCertificate(async certificate=>{
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();
        using var verifier=new OidcTokenVerifier(issuer.Settings);
        var serverId=Guid.NewGuid();var databaseId=Guid.NewGuid();var schemaId=Guid.NewGuid();var otherSchema=Guid.NewGuid();
        var visibleId=Guid.NewGuid();var deniedId=Guid.NewGuid();var wrongSchemaId=Guid.NewGuid();
        SecurityResource[] resources=[new(serverId,null,SecurityResourceKind.Server,"gateway"),new(databaseId,serverId,SecurityResourceKind.Database,"sales"),
            new(schemaId,databaseId,SecurityResourceKind.Schema,"dbo"),new(otherSchema,databaseId,SecurityResourceKind.Schema,"other"),
            new(visibleId,schemaId,SecurityResourceKind.Table,"orders"),new(deniedId,schemaId,SecurityResourceKind.Table,"payroll"),new(wrongSchemaId,otherSchema,SecurityResourceKind.Table,"archive")];
        var rights=SecurityPermission.Discover|SecurityPermission.Read;
        var role=new SecurityRole(Guid.NewGuid(),"reader",[new(databaseId,rights),new(deniedId,SecurityPermission.None,rights)]);
        using var policy=new AuthorizationManager(new(1,resources,[role],[new(new(issuer.Id,"developers"),SecurityPrincipalKind.Group,role.Id)]));
        var tables=new List<TableMetadata>{Table("orders",11),Table("payroll",12),Table("archive",13),Table("unregistered",null)};
        var adapter=new CatalogueAdapter("sales",tables);
        var registry=new DatabaseRegistry();registry.Register("sales",adapter);
        var mapping=new Dictionary<int,Guid>{{11,visibleId},{12,deniedId},{13,wrongSchemaId}};
        var protection=new HttpGatewayAuthorization(verifier,(identity,cancellation)=>new ValueTask<AuthorizationLease?>(policy.CreateSession(identity).BeginOperation(cancellation)),
            name=>name=="sales"?databaseId:null,(target,id)=>ReferenceEquals(target,adapter)&&mapping.TryGetValue(id,out var resource)?resource:null);
        using var server=new HttpServer(0,adapter,new SqlExecutionEngine(registry),new(){ListenAddress=IPAddress.Loopback,Certificate=certificate},protection);
        server.Start();using var client=CreateClient(server.Port,certificate);
        client.DefaultRequestHeaders.Authorization=new("Bearer",issuer.CreateToken(OidcDevelopmentIssuer.Audience,"alice",scope:"seamless.database"));
        using(var list=await client.GetAsync("/api/tables")){
            list.EnsureSuccessStatusCode();using var result=JsonDocument.Parse(await list.Content.ReadAsStringAsync());
            Assert.Equal("orders",Assert.Single(result.RootElement.EnumerateArray()).GetProperty("name").GetString());
        }
        using(var read=await client.GetAsync("/api/tables/dbo.orders")){
            read.EnsureSuccessStatusCode();Assert.Contains("42",await read.Content.ReadAsStringAsync());
        }
        foreach(var path in new[]{"dbo.payroll","dbo.archive","dbo.unregistered"}){
            using var denied=await client.GetAsync("/api/tables/"+path);Assert.Equal(HttpStatusCode.Forbidden,denied.StatusCode);
        }
        using(var metadata=await client.PostAsync("/api/sql",new StringContent(
            JsonSerializer.Serialize(new{query="SET FMTONLY ON SELECT * FROM dbo.orders\nSET FMTONLY OFF"})))){
            Assert.Equal(HttpStatusCode.BadRequest,metadata.StatusCode);
        }
        tables[0]=Table("orders",14);
        using(var recreated=await client.GetAsync("/api/tables/dbo.orders")){Assert.Equal(HttpStatusCode.Forbidden,recreated.StatusCode);}
        using(var sql=await client.PostAsync("/api/sql",new StringContent("{\"query\":\"SELECT * FROM dbo.orders\"}"))){Assert.Equal(HttpStatusCode.Forbidden,sql.StatusCode);}
        Assert.Equal(1,adapter.SelectCalls);

        static TableMetadata Table(string name,int? id)=>new("dbo",name,[new("id",typeof(int),"int",false,true,0)],null,id);
    });

    private static HttpClient CreateClient(int port,X509Certificate2 certificate){
        var trust=new X509ChainPolicy{TrustMode=X509ChainTrustMode.CustomRootTrust,RevocationMode=X509RevocationMode.NoCheck};
        trust.CustomTrustStore.Add(certificate);
        return new(new SocketsHttpHandler{UseProxy=false,SslOptions=new SslClientAuthenticationOptions{CertificateChainPolicy=trust}}){
            BaseAddress=new($"https://localhost:{port}"),Timeout=TimeSpan.FromSeconds(15)
        };
    }

    [Fact]
    public Task StrictTdsValidatesOidcAndChecksEachSqlOperation()=>ManagementTransport.WithServerCertificate(async certificate=>{
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();
        using var verifier=new OidcTokenVerifier(issuer.Settings);
        var serverId=Guid.NewGuid();var databaseId=Guid.NewGuid();var schemaId=Guid.NewGuid();var orders=Guid.NewGuid();var payroll=Guid.NewGuid();
        SecurityResource[] resources=[new(serverId,null,SecurityResourceKind.Server,"gateway"),new(databaseId,serverId,SecurityResourceKind.Database,"sales"),
            new(schemaId,databaseId,SecurityResourceKind.Schema,"dbo"),new(orders,schemaId,SecurityResourceKind.Table,"orders"),new(payroll,schemaId,SecurityResourceKind.Table,"payroll")];
        var role=new SecurityRole(Guid.NewGuid(),"orders-reader",[new(databaseId,SecurityPermission.Discover|SecurityPermission.Read),new(payroll,SecurityPermission.None,SecurityPermission.Read)]);
        using var policy=new AuthorizationManager(new(1,resources,[role],[new(new(issuer.Id,"developers"),SecurityPrincipalKind.Group,role.Id)]));
        var column=new ColumnMetadata("Id",typeof(int),"int",false,false,0);
        var adapter=new CatalogueAdapter("sales",[new("dbo","orders",[column],null,11),new("dbo","payroll",[column],null,12)],allowMutationPreparation:true);
        var registry=new DatabaseRegistry();registry.Register("sales",adapter);
        var protection=new HttpGatewayAuthorization(verifier,(identity,cancellation)=>{
            using var session=policy.CreateSession(identity);
            return ValueTask.FromResult<AuthorizationLease?>(session.BeginOperation(cancellation));
        },name=>name=="sales"?databaseId:null,(target,id)=>ReferenceEquals(target,adapter)?id switch{11=>orders,12=>payroll,_=>(Guid?)null}:null);
        using var server=TdsServer.CreateAuthenticated(0,new(registry),certificate,protection,
            (identity,_)=>ValueTask.FromResult<IAuthorizationSession>(policy.CreateSession(identity)));
        server.Start();
        var directory=Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!;
        var pin=Path.Combine(directory,Guid.NewGuid().ToString("N")+".cer");
        await File.WriteAllBytesAsync(pin,certificate.Export(X509ContentType.Cert));
        try{
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var access=issuer.CreateToken(OidcDevelopmentIssuer.Audience,"alice",scope:"seamless.database");
            var idToken=issuer.CreateToken(OidcDevelopmentIssuer.ClientId,"alice");
            await using(var invalid=Connect(idToken,"sales")){
                Assert.Equal(18456,(await Assert.ThrowsAsync<SqlException>(()=>invalid.OpenAsync(timeout.Token))).Number);
            }
            await using(var unknown=Connect(access,"unknown")){
                Assert.Equal(18456,(await Assert.ThrowsAsync<SqlException>(()=>unknown.OpenAsync(timeout.Token))).Number);
            }
            await using(var password=Connect(null,"sales")){
                Assert.Equal(18456,(await Assert.ThrowsAsync<SqlException>(()=>password.OpenAsync(timeout.Token))).Number);
            }
            await using var accepted=Connect(access,"sales");
            await accepted.OpenAsync(timeout.Token);
            using(var metadata=new SqlCommand("SET FMTONLY ON SELECT Id AS ItemId FROM dbo.orders\nSET FMTONLY OFF",accepted)){
                using var columns=await metadata.ExecuteReaderAsync(timeout.Token);
                Assert.Equal("ItemId",columns.GetName(0));
                Assert.Equal(typeof(int),columns.GetFieldType(0));
                Assert.False(await columns.ReadAsync(timeout.Token));
            }
            using(var metadata=new SqlCommand("sp_prepare",accepted){CommandType=global::System.Data.CommandType.StoredProcedure}){
                var handle=metadata.Parameters.Add("@handle",global::System.Data.SqlDbType.Int);
                handle.Direction=global::System.Data.ParameterDirection.Output;
                metadata.Parameters.Add("@params",global::System.Data.SqlDbType.NVarChar,80).Value="@id int";
                metadata.Parameters.Add("@stmt",global::System.Data.SqlDbType.NVarChar,160).Value="SELECT Id FROM dbo.orders WHERE Id=@id";
                metadata.Parameters.Add("@options",global::System.Data.SqlDbType.Int).Value=1;
                using(var columns=await metadata.ExecuteReaderAsync(timeout.Token)){
                    Assert.Equal("Id",columns.GetName(0));
                    Assert.Equal(typeof(int),columns.GetFieldType(0));
                    Assert.False(await columns.ReadAsync(timeout.Token));
                }
                Assert.IsType<int>(handle.Value);
                metadata.Parameters["@stmt"].Value="SELECT Id FROM dbo.payroll";
                Assert.Equal(229,(await Assert.ThrowsAsync<SqlException>(()=>metadata.ExecuteNonQueryAsync(timeout.Token))).Number);
                metadata.Parameters["@stmt"].Value="INSERT INTO dbo.orders VALUES (3)";
                Assert.Equal(40515,(await Assert.ThrowsAsync<SqlException>(()=>metadata.ExecuteNonQueryAsync(timeout.Token))).Number);
                metadata.Parameters["@stmt"].Value="SELECT Id FROM dbo.orders";
                metadata.Parameters["@options"].Value=2;
                Assert.Equal(40515,(await Assert.ThrowsAsync<SqlException>(()=>metadata.ExecuteNonQueryAsync(timeout.Token))).Number);
            }
            using(var denied=new SqlCommand("SET FMTONLY ON SELECT Id FROM dbo.payroll\nSET FMTONLY OFF",accepted)){
                Assert.Equal(229,(await Assert.ThrowsAsync<SqlException>(()=>denied.ExecuteNonQueryAsync(timeout.Token))).Number);
            }
            using(var malformed=new SqlCommand("SET FMTONLY ON SELECT Id FROM dbo.orders; DELETE FROM dbo.orders; SET FMTONLY OFF",accepted)){
                Assert.Equal(102,(await Assert.ThrowsAsync<SqlException>(()=>malformed.ExecuteNonQueryAsync(timeout.Token))).Number);
            }
            Assert.Equal(0,adapter.SelectCalls);
            using var command=accepted.CreateCommand();
            command.CommandText="SELECT Id FROM dbo.orders";
            Assert.Equal(42,await command.ExecuteScalarAsync(timeout.Token));
            command.CommandText="SELECT Id FROM dbo.payroll";
            Assert.Equal(229,(await Assert.ThrowsAsync<SqlException>(()=>command.ExecuteScalarAsync(timeout.Token))).Number);
            command.CommandText="INSERT INTO dbo.orders VALUES (3)";
            Assert.Equal(229,(await Assert.ThrowsAsync<SqlException>(()=>command.ExecuteNonQueryAsync(timeout.Token))).Number);
            command.CommandText="BEGIN TRANSACTION";
            Assert.Equal(40515,(await Assert.ThrowsAsync<SqlException>(()=>command.ExecuteNonQueryAsync(timeout.Token))).Number);
            policy.SetDisabled(new(issuer.Id,"alice"),true);
            command.CommandText="SELECT Id FROM dbo.orders";
            Assert.Equal(229,(await Assert.ThrowsAsync<SqlException>(()=>command.ExecuteScalarAsync(timeout.Token))).Number);
            Assert.Equal(1,adapter.SelectCalls);

            SqlConnection Connect(string? token,string database){
                var builder=new SqlConnectionStringBuilder{DataSource=$"tcp:127.0.0.1,{server.Port}",InitialCatalog=database,
                    Encrypt=SqlConnectionEncryptOption.Strict,Pooling=false,ConnectTimeout=5,ConnectRetryCount=0};
                builder["Server Certificate"]=pin;
                if(token is null){builder.UserID="local-test";builder.Password="local-test";}
                return new(builder.ConnectionString){AccessToken=token};
            }
        }finally{File.Delete(pin);}
    },includeLoopbackAddress:true);

    [Fact]
    public Task ProtectedTdsProcedureRequiresExecuteAndLimitsDelegatedRead()=>ManagementTransport.WithServerCertificate(async certificate=>{
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();
        using var verifier=new OidcTokenVerifier(issuer.Settings);
        var serverId=Guid.NewGuid();var database=Guid.NewGuid();var schema=Guid.NewGuid();var ledger=Guid.NewGuid();var payroll=Guid.NewGuid();
        var allowed=Guid.NewGuid();var denied=Guid.NewGuid();var restricted=Guid.NewGuid();var multi=Guid.NewGuid();
        SecurityResource[] resources=[
            new(serverId,null,SecurityResourceKind.Server,"gateway"),new(database,serverId,SecurityResourceKind.Database,"sales"),
            new(schema,database,SecurityResourceKind.Schema,"dbo"),new(ledger,schema,SecurityResourceKind.Table,"ledger"),
            new(payroll,schema,SecurityResourceKind.Table,"payroll"),new(allowed,schema,SecurityResourceKind.Procedure,"read_ledger"),
            new(denied,schema,SecurityResourceKind.Procedure,"denied"),new(restricted,schema,SecurityResourceKind.Procedure,"read_payroll"),
            new(multi,schema,SecurityResourceKind.Procedure,"multi")];
        var role=new SecurityRole(Guid.NewGuid(),"procedure-user",[
            new(database,SecurityPermission.Discover),new(allowed,SecurityPermission.Execute),new(restricted,SecurityPermission.Execute),
            new(multi,SecurityPermission.Execute),new(payroll,SecurityPermission.None,SecurityPermission.Read)]);
        using var policy=new AuthorizationManager(new(1,resources,[role],[new(new(issuer.Id,"developers"),SecurityPrincipalKind.Group,role.Id)]));
        var column=new ColumnMetadata("Id",typeof(int),"int",false,false,0);
        var adapter=new CatalogueAdapter("sales",[new("dbo","ledger",[column],null,11),new("dbo","payroll",[column],null,12)]);
        var registry=new DatabaseRegistry();
        registry.Register("sales",adapter,routines:[
            Procedure("read_ledger","SELECT Id FROM dbo.ledger WHERE Id=@id"),Procedure("denied","SELECT Id FROM dbo.ledger"),
            Procedure("read_payroll","SELECT Id FROM dbo.payroll"),Procedure("multi","SELECT Id FROM dbo.ledger; SELECT Id FROM dbo.payroll")]);
        var access=new HttpGatewayAuthorization(verifier,(identity,cancellation)=>{
            using var session=policy.CreateSession(identity);
            return ValueTask.FromResult<AuthorizationLease?>(session.BeginOperation(cancellation));
        },name=>name=="sales"?database:null,(target,id)=>ReferenceEquals(target,adapter)?id switch{11=>ledger,12=>payroll,_=>(Guid?)null}:null,
            (target,kind,_,name)=>ReferenceEquals(target,adapter)&&kind==SecurityResourceKind.Procedure?name switch{
                "read_ledger"=>new(allowed,[ledger]),"denied"=>new(denied,[ledger]),"read_payroll"=>new(restricted,[payroll]),"multi"=>new(multi,[ledger,payroll]),_=>null}:null);
        using var server=TdsServer.CreateAuthenticated(0,new(registry),certificate,access,(identity,_)=>ValueTask.FromResult<IAuthorizationSession>(policy.CreateSession(identity)));
        server.Start();
        var pin=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,Guid.NewGuid().ToString("N")+".cer");
        await File.WriteAllBytesAsync(pin,certificate.Export(X509ContentType.Cert));
        try{
            var builder=new SqlConnectionStringBuilder{DataSource=$"tcp:127.0.0.1,{server.Port}",InitialCatalog="sales",
                Encrypt=SqlConnectionEncryptOption.Strict,Pooling=false,ConnectTimeout=5,ConnectRetryCount=0};
            builder["Server Certificate"]=pin;
            await using var connection=new SqlConnection(builder.ConnectionString){AccessToken=issuer.CreateToken(OidcDevelopmentIssuer.Audience,"alice",scope:"seamless.database")};
            await connection.OpenAsync();
            using(var invoke=new SqlCommand("[dbo].[read_ledger]",connection){CommandType=global::System.Data.CommandType.StoredProcedure}){
                invoke.Parameters.Add("@id",global::System.Data.SqlDbType.Int).Value=42;
                Assert.Equal(42,await invoke.ExecuteScalarAsync());
                invoke.Parameters.Clear();
                Assert.Equal(201,(await Assert.ThrowsAsync<SqlException>(()=>invoke.ExecuteScalarAsync())).Number);
            }
            foreach(var name in new[]{"dbo.denied","dbo.read_payroll","dbo.missing"}){
                using var invoke=new SqlCommand(name,connection){CommandType=global::System.Data.CommandType.StoredProcedure};
                invoke.Parameters.Add("@id",global::System.Data.SqlDbType.Int).Value=42;
                Assert.Equal(229,(await Assert.ThrowsAsync<SqlException>(()=>invoke.ExecuteScalarAsync())).Number);
            }
            using(var invoke=new SqlCommand("dbo.multi",connection){CommandType=global::System.Data.CommandType.StoredProcedure}){
                invoke.Parameters.Add("@id",global::System.Data.SqlDbType.Int).Value=42;
                Assert.Equal(229,(await Assert.ThrowsAsync<SqlException>(()=>invoke.ExecuteReaderAsync())).Number);
                Assert.Equal(1,adapter.SelectCalls);
            }
            using(var direct=new SqlCommand("SELECT Id FROM dbo.ledger",connection)){
                Assert.Equal(229,(await Assert.ThrowsAsync<SqlException>(()=>direct.ExecuteScalarAsync())).Number);
            }
            using(var execute=new SqlCommand("EXEC dbo.read_ledger @id=42",connection)){
                Assert.Equal(42,await execute.ExecuteScalarAsync());
            }
            using(var dynamicSql=new SqlCommand("EXEC ('SELECT Id FROM dbo.ledger')",connection)){
                Assert.Equal(40515,(await Assert.ThrowsAsync<SqlException>(()=>dynamicSql.ExecuteScalarAsync())).Number);
            }
            policy.PublishPolicy(new(2,resources,[new(role.Id,role.Name,[new(database,SecurityPermission.Discover)])],
                [new(new(issuer.Id,"developers"),SecurityPrincipalKind.Group,role.Id)]));
            using(var revoked=new SqlCommand("dbo.read_ledger",connection){CommandType=global::System.Data.CommandType.StoredProcedure}){
                revoked.Parameters.Add("@id",global::System.Data.SqlDbType.Int).Value=42;
                Assert.Equal(229,(await Assert.ThrowsAsync<SqlException>(()=>revoked.ExecuteScalarAsync())).Number);
            }
            Assert.Equal(2,adapter.SelectCalls);
        }finally{File.Delete(pin);}
        static DatabaseRoutineDefinition Procedure(string name,string body){
            var definition=$"CREATE PROCEDURE dbo.{name} @id int AS {body}";
            var parameter=new ParameterCatalog(1,"@id",new(DatabaseTypeKind.Int32,false,SqlTypeName:"int"),false);
            return new("dbo",name,definition,new("dbo",name,RoutineKind.StoredProcedure,null,[parameter],[],definition,false,1,0));
        }
    },includeLoopbackAddress:true);

    private sealed class CatalogueAdapter(string name,IReadOnlyList<TableMetadata>? tables=null,bool allowMutationPreparation=false):IDatabaseAdapter{
        private int selectCalls;
        public int SelectCalls=>Volatile.Read(ref this.selectCalls);
        public string DatabaseName=>name;
        public IReadOnlyList<TableMetadata> GetTables()=>tables??[];
        public TableMetadata? FindTable(string name)=>this.GetTables().SingleOrDefault(table=>table.FullName==name||table.IdentifierKey==name);
        public QueryResult ExecuteSelect(string tableName,IReadOnlyList<string>? columns,Func<object,bool>? predicate,SessionContext session){
            Interlocked.Increment(ref this.selectCalls);
            var table=this.FindTable(tableName)??throw new InvalidOperationException("Unexpected table lookup.");
            return new(table.Columns,[[42]],-1);
        }
        public int ExecuteInsert(string tableName,IReadOnlyDictionary<string,object?>values,SessionContext session)=>throw new NotSupportedException();
        public int ExecuteUpdate(string tableName,IReadOnlyDictionary<string,object?>values,Func<object,bool>? predicate,SessionContext session)=>throw new NotSupportedException();
        public int ExecuteDelete(string tableName,Func<object,bool>? predicate,SessionContext session)=>throw new NotSupportedException();
        public void BeginTransaction(SessionContext session){
            if(!allowMutationPreparation){throw new NotSupportedException();}
            // このアダプターは認可検査前の準備だけを許し、行更新の呼び出しは拒否する。
        }
        public void CommitTransaction(SessionContext session)=>throw new NotSupportedException();
        public void RollbackTransaction(SessionContext session)=>throw new NotSupportedException();
    }
}
