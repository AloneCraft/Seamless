using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Seamless.Authentication;
using Seamless.Database;
using Seamless.Database.Client;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Rdbms;
using Seamless.Database.Server;
using Seamless.Management;
using Seamless.Management.Server;
using Seamless.Security;

namespace Management.Tests;

[Collection("AuditStorage")]
public sealed class GatewaySubjectStatus{
    [Fact]
    public Task OneManagementDisableRollsBackHttpAndNativeWritesBeforeTheirClientsClose()=>ManagementTransport.WithServerCertificate(async certificate=>{
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var storage=Path.Combine(root,"db");
        var managementRoot=Path.Combine(root,"m");
        var working=Path.Combine(root,"w");
        var cache=Path.Combine(root,"c");
        var generator=typeof(Seamless.Database.Generator.DatabaseSchemaGenerator).Assembly.Location;
        var dotnet=Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"dotnet","dotnet.exe");
        var references=global::Seamless.Database.Gateway.Tests.GeneratedAssemblyBuilderTests.RuntimeReferences();
        var compiler=new GeneratedAssemblyBuilder(new(working,cache,dotnet,generator,references));
        var options=new DatabaseContainerOptions{IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20};
        var number=new DatabaseType(DatabaseTypeKind.Int32,false,SqlTypeName:"int");
        DatabaseCatalog Catalog(string name)=>new(name,[new("dbo",[new TableCatalog("Rows",[new(0,"Id",number,false)],new("PK_Rows",[new("Id",false)],1))])]);

        await using var issuer=await OidcDevelopmentIssuer.StartAsync();
        var admin=new SecuritySubjectId(issuer.Id,"administrator");
        var writer=new SecuritySubjectId(issuer.Id,"writer");
        var adminToken=Access(admin);
        var writerToken=Access(writer);
        var salesId=Guid.NewGuid();var ledgerId=Guid.NewGuid();
        var salesSchema=Guid.NewGuid();var ledgerSchema=Guid.NewGuid();
        var salesTable=Guid.NewGuid();var ledgerTable=Guid.NewGuid();
        var queryId=Guid.NewGuid();var adminRole=Guid.NewGuid();var writerRole=Guid.NewGuid();
        Guid instance;
        using(var store=ManagementStore.Initialize(managementRoot,"admin","TestPassword-123")){
            instance=store.InstanceId;
            var policy=new Seamless.Security.AuthorizationPolicySnapshot(1,[
                new(instance,null,SecurityResourceKind.Server,"gateway"),new(salesId,instance,SecurityResourceKind.Database,"sales"),
                new(ledgerId,instance,SecurityResourceKind.Database,"ledger"),new(salesSchema,salesId,SecurityResourceKind.Schema,"dbo"),
                new(ledgerSchema,ledgerId,SecurityResourceKind.Schema,"dbo"),new(salesTable,salesSchema,SecurityResourceKind.Table,"Rows"),
                new(ledgerTable,ledgerSchema,SecurityResourceKind.Table,"Rows"),new(queryId,ledgerId,SecurityResourceKind.Query,"insert")
            ],[
                new(adminRole,"admin",[new(instance,SecurityPermission.All)]),
                new(writerRole,"writer",[new(salesId,SecurityPermission.Discover),new(ledgerId,SecurityPermission.Discover),
                    new(salesTable,SecurityPermission.Read|SecurityPermission.Insert),new(ledgerTable,SecurityPermission.Insert),
                    new(queryId,SecurityPermission.Execute)])
            ],[new(admin,SecurityPrincipalKind.User,adminRole),new(writer,SecurityPrincipalKind.User,writerRole)]);
            var now=DateTimeOffset.UtcNow;
            using var initialized=StoredAuthorization.Initialize(store,policy,new(admin,admin.Value,[],now.AddHours(1),now),issuer.Settings);
        }

        GatewayAuthorizationConfiguration settings;
        string fingerprint;
        using(var host=await GatewayDatabaseHost.OpenAsync(storage,compiler,options,[Catalog("sales"),Catalog("ledger")])){
            var sql=new SqlExecutionEngine(host.Databases);
            foreach(var name in new[]{"sales","ledger"}){
                using var session=new SessionContext(1,name);
                foreach(var _ in sql.Execute("INSERT INTO dbo.Rows VALUES (1)",session).Rows){}
            }
            var ledger=(InProcessDatabaseAdapter)host.Databases.Resolve("ledger").Adapter;
            var database=ledger.Database.GetType();
            var table=database.GetProperty("dbo")!.PropertyType.GetProperty("Rows")!.PropertyType;
            var rowType=table.GetGenericArguments()[1].FullName!.Replace('+','.');
            var databaseType=database.FullName!.Replace('+','.');
            var code=$$"""
                using MemoryPack;
                using Seamless.Database.Client;
                public sealed class SubjectStatusWriter : INativeQueryModule {
                    public IEnumerable<byte[]> Execute(string id, ReadOnlyMemory<byte> args, INativeQueryContext context) {
                        var db = (global::{{databaseType}})context.Database;
                        var key = MemoryPackSerializer.Deserialize<int>(args.Span);
                        db.dbo.Rows.Add(new global::{{rowType}}(new(key)));
                        yield return MemoryPackSerializer.Serialize(key);
                    }
                }
                """;
            await host.Transactions.InstallNativeQueryAsync(new("writer","SubjectStatusWriter",[new("Writer.cs",code)],
                [new("insert","ledger","","System.Int32","System.Int32",false)],["ledger"]));
            var publication=host.Transactions.InspectNativePublication("ledger","writer","insert");
            var binding=Assert.Single(publication.Databases);
            fingerprint=binding.SchemaFingerprint;
            var approval=new GatewayNativeQueryResource(binding.DatabaseId,"writer","insert",publication.QueryGenerationId,publication.ManifestSha256,
                publication.AssemblySha256,queryId,[new(binding.DatabaseId,binding.GenerationId,binding.SchemaFingerprint,binding.AssemblySha256)],
                [new(ledgerTable,SecurityPermission.Insert)]);
            settings=new GatewayAuthorizationConfiguration(managementRoot,instance,host.Transactions.WalId,
                [Mapping("sales",salesId,salesTable),Mapping("ledger",ledgerId,ledgerTable)]){
                Native=new([approval]),ManagementEndpoint=new("127.0.0.1",0)
            };

            GatewayHttpDatabaseResource Mapping(string name,Guid databaseId,Guid tableId){
                var adapter=(InProcessDatabaseAdapter)host.Databases.Resolve(name).Adapter;
                return new(adapter.Database.Options.GatewayProfile!.DatabaseId,databaseId,[new(Assert.Single(adapter.GetTables()).StableObjectId!.Value,tableId)]);
            }
        }

        var password=Guid.NewGuid().ToString("N");
        var pfx=Path.Combine(root,"tls.pfx");
        await File.WriteAllBytesAsync(pfx,certificate.Export(X509ContentType.Pfx,password));
        var change=new AuthorizationSubjectChange(Guid.NewGuid(),writer,true){ExpectedInstanceId=instance};
        AuthorizationSubjectChangeResult? receipt=null;
        try{
            await RunGatewayAsync("before",async (record,token)=>{
                using var http=Http(record,writerToken);
                using var observer=Http(record,adminToken);
                await using var native=Native(record,writerToken);
                var management=Management(record);
                await BeginHttpAsync(http,token);
                await CheckSqlAsync(http,"sales","INSERT INTO dbo.Rows VALUES (2)",token);
                Assert.Equal(new[]{1,2},await RowsAsync(http,"sales",token));
                await using var transaction=await native.BeginTransactionAsync("ledger",token);
                Assert.Equal(2,await InsertNativeAsync(transaction,2,token));
                Assert.False(transaction.IsCompleted);

                var wrongInstance=await Assert.ThrowsAsync<ManagementException>(()=>management.ChangeAuthorizationSubjectAsync(
                    adminToken,change with{ExpectedInstanceId=Guid.NewGuid()},token));
                Assert.Equal("Connection",wrongInstance.Code);
                receipt=await management.ChangeAuthorizationSubjectAsync(adminToken,change,token);
                Assert.True(receipt.Disabled);
                await CheckDisabledAsync(record,token);
                foreach(var name in new[]{"sales","ledger"}){
                    Assert.Equal(new[]{1},await RowsAsync(observer,name,token));
                    await CheckSqlAsync(observer,name,"INSERT INTO dbo.Rows VALUES (2)",token);
                    await CheckSqlAsync(observer,name,"DELETE FROM dbo.Rows WHERE Id=2",token);
                }

                using var rejectedHttp=await SqlAsync(http,"sales","COMMIT TRANSACTION",token);
                Assert.Contains(rejectedHttp.StatusCode,new[]{HttpStatusCode.Conflict,HttpStatusCode.Forbidden});
                var rejectedNative=await Record.ExceptionAsync(()=>transaction.CommitAsync(token));
                Assert.True(rejectedNative is IOException or NativeQueryException,rejectedNative?.ToString());
                Assert.True(transaction.IsCompleted);
                Assert.Equal(receipt,await management.AuthorizationSubjectChangeResultAsync(adminToken,change.OperationId,token));
            });
            await RunGatewayAsync("after",async (record,token)=>{
                await CheckDisabledAsync(record,token);
                var management=Management(record);
                Assert.NotNull(receipt);
                Assert.Equal(receipt,await management.AuthorizationSubjectChangeResultAsync(adminToken,change.OperationId,token));
                using var observer=Http(record,adminToken);
                Assert.Equal(new[]{1},await RowsAsync(observer,"sales",token));
                Assert.Equal(new[]{1},await RowsAsync(observer,"ledger",token));
                var enabled=await management.ChangeAuthorizationSubjectAsync(adminToken,change with{OperationId=Guid.NewGuid(),Disabled=false},token);
                Assert.False(enabled.Disabled);
                using var http=Http(record,writerToken);
                Assert.Equal(new[]{1},await RowsAsync(http,"sales",token));
                await using var native=Native(record,writerToken);
                await using var transaction=await native.BeginTransactionAsync("ledger",token);
                Assert.Equal(3,await InsertNativeAsync(transaction,3,token));
                await transaction.RollbackAsync(token);
                Assert.Equal(new[]{1},await RowsAsync(observer,"ledger",token));
                if(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_UI_TEST_EXE") is{} manager){
                    await VerifyManagerAsync(record,manager,token);
                    Assert.Equal(new[]{1},await RowsAsync(http,"sales",token));
                }
            });
        }finally{File.Delete(pfx);}

        string Access(SecuritySubjectId subject)=>issuer.CreateToken(OidcDevelopmentIssuer.Audience,subject.Value,scope:"seamless.database");
        ManagementClient Management(JsonElement record)=>new("localhost",record.GetProperty("AuthorizationManagementPort").GetInt32(),certificate);
        NativeQueryClient Native(JsonElement record,string access)=>NativeQueryClient.ForAuthenticatedTls(
            _=>ValueTask.FromResult(access),port:record.GetProperty("NativePort").GetInt32(),trustedRoot:certificate);
        HttpClient Http(JsonElement record,string access){
            var trust=new X509ChainPolicy{TrustMode=X509ChainTrustMode.CustomRootTrust,RevocationMode=X509RevocationMode.NoCheck};
            trust.CustomTrustStore.Add(certificate);
            var client=new HttpClient(new SocketsHttpHandler{UseProxy=false,SslOptions=new SslClientAuthenticationOptions{CertificateChainPolicy=trust}}){
                BaseAddress=new($"https://localhost:{record.GetProperty("HttpPort").GetInt32()}"),Timeout=TimeSpan.FromSeconds(15)
            };
            client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",access);
            return client;
        }
        async Task CheckDisabledAsync(JsonElement record,CancellationToken token){
            using var http=Http(record,writerToken);
            using var refused=await http.GetAsync("/api/databases",token);
            Assert.Equal(HttpStatusCode.Forbidden,refused.StatusCode);
            await using var native=Native(record,writerToken);
            var error=await Assert.ThrowsAsync<NativeAuthenticationException>(()=>native.ConnectAsync(token));
            Assert.Equal(NativeAuthenticationError.Rejected,error.Error);
        }
        async Task<int> InsertNativeAsync(NativeTransaction transaction,int key,CancellationToken token){
            var results=new List<int>();
            await foreach(var value in transaction.QueryAsync<int,int>("writer","insert",fingerprint,key,token)){results.Add(value);}
            return Assert.Single(results);
        }
        static async Task<HttpResponseMessage> SqlAsync(HttpClient client,string database,string sql,CancellationToken token){
            using var content=new StringContent(JsonSerializer.Serialize(new{query=sql}),Encoding.UTF8,"application/json");
            return await client.PostAsync("/api/sql?database="+database,content,token);
        }
        static async Task CheckSqlAsync(HttpClient client,string database,string sql,CancellationToken token){
            using var response=await SqlAsync(client,database,sql,token);
            Assert.True(response.IsSuccessStatusCode,await response.Content.ReadAsStringAsync(token));
        }
        static async Task<int[]> RowsAsync(HttpClient client,string database,CancellationToken token){
            using var response=await SqlAsync(client,database,"SELECT Id FROM dbo.Rows ORDER BY Id",token);
            var text=await response.Content.ReadAsStringAsync(token);
            Assert.True(response.IsSuccessStatusCode,text);
            using var document=JsonDocument.Parse(text);
            return document.RootElement.GetProperty("rows").EnumerateArray().Select(static row=>row.GetProperty("Id").GetInt32()).ToArray();
        }
        static async Task BeginHttpAsync(HttpClient client,CancellationToken token){
            using var opened=await client.PostAsync("/api/session?database=sales",null,token);
            Assert.Equal(HttpStatusCode.Created,opened.StatusCode);
            using var info=JsonDocument.Parse(await opened.Content.ReadAsStringAsync(token));
            client.DefaultRequestHeaders.Add("X-Seamless-Session",info.RootElement.GetProperty("id").GetString());
            using var began=await SqlAsync(client,"sales","BEGIN TRANSACTION",token);
            var text=await began.Content.ReadAsStringAsync(token);
            Assert.True(began.IsSuccessStatusCode,text);
            using var transaction=JsonDocument.Parse(text);
            client.DefaultRequestHeaders.Add("X-Seamless-Transaction",transaction.RootElement.GetProperty("session").GetProperty("transactionId").GetGuid().ToString());
        }
        async Task VerifyManagerAsync(JsonElement record,string executable,CancellationToken token){
            var publicCertificate=Path.Combine(root,"public.cer");
            var config=Path.Combine(root,"ui.json");
            var logs=Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_LOG")??throw new InvalidOperationException("Missing Manager evidence directory.");
            var image=Path.Combine(logs,"gateway-subject-status.png");
            await File.WriteAllBytesAsync(publicCertificate,certificate.Export(X509ContentType.Cert),token);
            await File.WriteAllTextAsync(config,JsonSerializer.Serialize(new{
                ホスト="127.0.0.1",ポート=record.GetProperty("AuthorizationManagementPort").GetInt32(),利用者=admin.Value,
                証明書=publicCertificate,定義ID="",試験認証元=issuer.Id,試験対象主体=writer.Value
            },ManagementJson.Options),token);
            var start=new ProcessStartInfo(executable){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};
            start.Environment["SEAMLESS_MANAGEMENT_UI_TEST_BEARER"]=adminToken;
            start.Environment["SEAMLESS_MANAGEMENT_HOME"]=Path.Combine(root,"ui");
            start.Environment["SEAMLESS_MANAGEMENT_ROOT"]=managementRoot;
            foreach(var argument in new[]{"--smoke",config,image}){start.ArgumentList.Add(argument);}
            using var process=Process.Start(start)!;
            try{await process.WaitForExitAsync(token);}
            finally{if(!process.HasExited){process.Kill(entireProcessTree:true);await process.WaitForExitAsync();}}
            Assert.True(process.ExitCode==0,File.Exists(image+".error.txt")?File.ReadAllText(image+".error.txt"):"Manager exit="+process.ExitCode);
            Assert.True(new FileInfo(image).Length>0);
            using var result=JsonDocument.Parse(await File.ReadAllTextAsync(image+".result.json",token));
            Assert.True(result.RootElement.GetProperty("無効化").GetBoolean());
            Assert.True(result.RootElement.GetProperty("照会").GetBoolean());
            Assert.True(result.RootElement.GetProperty("再有効化").GetBoolean());
        }
        async Task RunGatewayAsync(string label,Func<JsonElement,CancellationToken,Task> verify){
            var config=Path.Combine(root,label+".json");
            var ready=Path.Combine(root,label+".ready");
            var stop=Path.Combine(root,label+".stop");
            await File.WriteAllTextAsync(config,JsonSerializer.Serialize(new{
                StorageDirectory=storage,WorkingDirectory=working,CacheDirectory=cache,GeneratorAssemblyPath=generator,DotNetExecutable=dotnet,
                Authorization=settings,NativeCertificatePath=pfx,HttpCertificatePath=pfx,DatabaseOptions=options,
                Endpoints=new{NativePort=0,HttpPort=0,TdsPort=0,Http=new{ListenAddress="127.0.0.1"}}
            }));
            var start=new ProcessStartInfo(dotnet){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
            start.Environment["SEAMLESS_GATEWAY_NATIVE_CERT_PASSWORD"]=password;
            start.Environment["SEAMLESS_GATEWAY_HTTP_CERT_PASSWORD"]=password;
            foreach(var argument in new[]{typeof(CrashProbe).Assembly.Location,"--gateway-cli","--gateway",config,"--ready-file",ready,"--stop-file",stop}){
                start.ArgumentList.Add(argument);
            }
            using var process=Process.Start(start)!;
            var created=process.StartTime.ToUniversalTime();
            var output=process.StandardOutput.ReadToEndAsync();
            var errors=process.StandardError.ReadToEndAsync();
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(90));
            var token=timeout.Token;
            try{
                while(!File.Exists(ready)&&!process.HasExited){await Task.Delay(50,token);}
                if(process.HasExited){Assert.Fail((await output)+(await errors));}
                using var document=JsonDocument.Parse(await File.ReadAllTextAsync(ready,token));
                var record=document.RootElement;
                Assert.Equal(process.Id,record.GetProperty("Pid").GetInt32());
                Assert.Equal(created,record.GetProperty("CreatedUtc").GetDateTime());
                Assert.True(record.GetProperty("NativeAuthenticated").GetBoolean());
                Assert.Equal(JsonValueKind.Null,record.GetProperty("TdsPort").ValueKind);
                await verify(record,token);
                await File.WriteAllTextAsync(stop,"stop",token);
                await process.WaitForExitAsync(token);
                var waitReturned=Stopwatch.GetTimestamp();var nativeSignal=RecoveryLockDiagnostic.ProcessSignal(process);
                await File.WriteAllTextAsync(Path.Combine(root,label+".handoff.json"),JsonSerializer.Serialize(new{Stage=label,ChildPid=process.Id,ChildCreatedUtc=created,Runtime=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,process.ExitCode,NativeSignalAfterAsync=nativeSignal,StopwatchFrequency=Stopwatch.Frequency,WaitReturned=waitReturned,GatewayWalId=record.GetProperty("GatewayWalId").GetGuid()}),token);
                Assert.True(process.ExitCode==0,(await output)+(await errors));
            }finally{
                if(!process.HasExited&&process.StartTime.ToUniversalTime()==created){
                    process.Kill(entireProcessTree:true);
                    await process.WaitForExitAsync();
                }
            }
        }
    },includeLoopbackAddress:true);
}
