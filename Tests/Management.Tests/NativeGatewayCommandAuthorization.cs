using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Text.Json;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using Microsoft.Data.SqlClient;
using Seamless.Authentication;
using Seamless.Database;
using Seamless.Database.Client;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Native;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Rdbms;
using Seamless.Database.Server;
using Seamless.Management;
using Seamless.Management.Server;
using Seamless.Security;

namespace Management.Tests;

[Collection("AuditStorage")]
public sealed class NativeGatewayCommandAuthorization{
    [Fact]public Task NativeOnlyCommandUsesPersistedPolicyAndExactPublicationApprovals()=>ManagementTransport.WithServerCertificate(async certificate=>{
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();var directory=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        var storage=Path.Combine(directory,"db");var managementRoot=Path.Combine(directory,"m");var databaseId=Guid.NewGuid();var schemaId=Guid.NewGuid();var tableId=Guid.NewGuid();var outsideDatabaseId=Guid.NewGuid();var outsideTableId=Guid.NewGuid();var queryId=Guid.NewGuid();var adminRole=Guid.NewGuid();var queryRole=Guid.NewGuid();var readerRole=Guid.NewGuid();var dbManagerRole=Guid.NewGuid();var actor=new SecuritySubjectId(issuer.Id,"admin");Guid instance;
        Seamless.Security.AuthorizationPolicySnapshot policy;
        using(var store=ManagementStore.Initialize(managementRoot,"admin","TestPassword-123")){
            instance=store.InstanceId;policy=new(1,[new(instance,null,SecurityResourceKind.Server,"server"),new(databaseId,instance,SecurityResourceKind.Database,"lab"),new(schemaId,databaseId,SecurityResourceKind.Schema,"dbo"),new(tableId,schemaId,SecurityResourceKind.Table,"Rows"),new(queryId,schemaId,SecurityResourceKind.Query,"value"),new(outsideDatabaseId,instance,SecurityResourceKind.Database,"outside"),new(outsideTableId,outsideDatabaseId,SecurityResourceKind.Table,"Rows")],
                [new(adminRole,"admin",[new(instance,SecurityPermission.ManageServer),new(databaseId,SecurityPermission.Discover|SecurityPermission.ManagePermissions),new(tableId,SecurityPermission.None,SecurityPermission.Read)]),new(queryRole,"executor",[new(queryId,SecurityPermission.Execute)]),new(readerRole,"reader",[new(databaseId,SecurityPermission.Discover),new(tableId,SecurityPermission.Read)]),new(dbManagerRole,"db-manager",[new(databaseId,SecurityPermission.ManagePermissions)])],
                [new(actor,SecurityPrincipalKind.User,adminRole),new(actor,SecurityPrincipalKind.User,queryRole),new(new(issuer.Id,"dev-user"),SecurityPrincipalKind.User,adminRole),new(new(issuer.Id,"other-user"),SecurityPrincipalKind.User,readerRole),new(new(issuer.Id,"other-user"),SecurityPrincipalKind.User,queryRole),new(new(issuer.Id,"db-manager"),SecurityPrincipalKind.User,dbManagerRole)]);
            var now=DateTimeOffset.UtcNow;using var initialized=StoredAuthorization.Initialize(store,policy,new(actor,"admin",[],now.AddMinutes(30),now),issuer.Settings);
        }
        var dotnet=Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"dotnet","dotnet.exe");
        var references=global::Seamless.Database.Gateway.Tests.GeneratedAssemblyBuilderTests.RuntimeReferences();
        var compiler=new GeneratedAssemblyBuilder(new(Path.Combine(directory,"w"),Path.Combine(directory,"c"),dotnet,typeof(Seamless.Database.Generator.DatabaseSchemaGenerator).Assembly.Location,references));
        NativeQueryExecution? captured=null;var outsideProfileId=0;
        var number=new DatabaseType(DatabaseTypeKind.Int32,false,SqlTypeName:"int");
        using(var host=await GatewayDatabaseHost.OpenAsync(storage,compiler,new(){IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20},[new("lab",[new("dbo",[new TableCatalog("Rows",[new(0,"Id",number,false)],new("PK_Rows",[new("Id",false)],1))])]),new("outside",[new SchemaCatalog("dbo",[])])])){
            outsideProfileId=((InProcessDatabaseAdapter)host.Databases.Resolve("outside").Adapter).Database.Options.GatewayProfile!.DatabaseId;
            using var administration=new SessionContext(998,"lab");foreach(var row in new SqlExecutionEngine(host.Databases).Execute("INSERT INTO dbo.Rows VALUES(1)",administration).Rows){}
            var type=((InProcessDatabaseAdapter)host.Databases.Resolve("lab").Adapter).Database.GetType().FullName!.Replace('+','.');
            var source=$$"""
                using MemoryPack;
                using Seamless.Database.Client;
                using Seamless.Database.Gateway.Native;
                public sealed class Query : INativeQueryModule {
                    public IEnumerable<byte[]> Execute(string id, ReadOnlyMemory<byte> args, INativeQueryContext context) {
                        var db = (global::{{type}})context.Database;
                        var count = 0;
                        foreach (var row in db.dbo.Rows) { count++; }
                        yield return MemoryPackSerializer.Serialize(MemoryPackSerializer.Deserialize<int>(args.Span) + count);
                    }
                }
                """;
            await host.Transactions.InstallNativeQueryAsync(new("secured","Query",[new("Query.cs",source)],[new("value2","lab","","System.Int32","System.Int32"),new("value","lab","","System.Int32","System.Int32")],["lab"]));
            await host.Transactions.InstallNativeQueryAsync(new("cross","Query",[new("Query.cs",source)],[new("value","lab","","System.Int32","System.Int32")],["lab","outside"]));
            using var verifier=new OidcTokenVerifier(issuer.Settings);using var manager=new AuthorizationManager(policy);
            var protection=NativeGatewayAuthorization.ForPublishedQueries(verifier,(identity,_)=>ValueTask.FromResult<IAuthorizationSession>(manager.CreateSession(identity)),(lease,_)=>lease.Demand(databaseId,SecurityPermission.Discover),(lease,_,execution)=>{lease.Demand(queryId,SecurityPermission.Execute);Assert.Equal("ExplicitDeny",lease.Snapshot.Evaluate(tableId,SecurityPermission.Read).Code);captured=execution;});
            using var server=NativeQueryServer.CreateAuthenticated(host.Databases,certificate,protection,new(),0);server.Start();await using var client=NativeQueryClient.ForAuthenticatedTls(_=>ValueTask.FromResult(issuer.CreateToken(OidcDevelopmentIssuer.Audience,"admin",scope:"seamless.database")),port:server.Port,trustedRoot:certificate);
            Assert.Equal(8,await client.ExecuteScalarAsync<int,int>("secured","value","lab",host.Databases.Resolve("lab").SchemaFingerprint!,7));
        }
        Assert.NotNull(captured);var binding=Assert.Single(captured.Databases);
        var approval=new GatewayNativeQueryResource(binding.DatabaseId,"secured","value",captured.QueryGenerationId,captured.ManifestSha256,captured.AssemblySha256,queryId,[new(binding.DatabaseId,binding.GenerationId,binding.SchemaFingerprint,binding.AssemblySha256)],[new(tableId,SecurityPermission.Read,Delegated:true)]);
        var settings=new GatewayAuthorizationConfiguration("m",instance,captured.WalId,[new(binding.DatabaseId,databaseId),new(outsideProfileId,outsideDatabaseId)]){ProtectHttp=false,Native=new([approval]),ManagementEndpoint=new("127.0.0.1",0)};
        var outcomeKey=RandomNumberGenerator.GetBytes(32);
        var outcomeKeyText=Convert.ToBase64String(outcomeKey);
        var outcomeSettings=settings with{Native=settings.Native! with{TransactionOutcomeKeySha256=Convert.ToHexString(SHA256.HashData(outcomeKey))}};
        CryptographicOperations.ZeroMemory(outcomeKey);
        var password=Guid.NewGuid().ToString("N");var pfx=Path.Combine(directory,"native.pfx");await File.WriteAllBytesAsync(pfx,certificate.Export(X509ContentType.Pfx,password));
        try{
            await Run(settings,false,false);
            await Run(outcomeSettings,true,false);
            await Run(outcomeSettings,true,false,outcomeKey:Convert.ToBase64String(new byte[32]));
            await Run(settings with{GatewayWalId=Guid.NewGuid()},true,false);
            await Run(settings with{Native=settings.Native! with{Queries=[approval with{AssemblySha256=new string('0',64)}]}},true,true,async record=>{
                await using var client=Client(record,"other-user");
                var error=await Assert.ThrowsAsync<NativeQueryException>(()=>Query(client));
                Assert.Contains("not approved",error.Message,StringComparison.Ordinal);
            });
            foreach(var invalid in new[]{approval with{Access=[new(tableId,SecurityPermission.ManagePermissions,Delegated:true)]},approval with{Access=[new(outsideTableId,SecurityPermission.Read,Delegated:true)]},approval with{Access=[approval.Access[0],approval.Access[0]]}}){
                await Run(settings with{Native=new([invalid])},true,false);
            }
            await Run(settings with{Native=new([approval with{Access=[new(tableId,SecurityPermission.Read)]}])},true,true,async record=>{await using var client=Client(record);await Assert.ThrowsAsync<NativeQueryException>(()=>Query(client));});
            var approvalPath=Path.Combine(directory,"native-approvals.json");
            var fileSettings=settings with{Native=settings.Native! with{ApprovalFile="native-approvals.json",Queries=[approval with{QueryGenerationId=Guid.NewGuid()}]}};
            await SaveApprovals([]);
            await Run(fileSettings,true,true,async record=>{
                await using var reader=Client(record,"other-user");
                var beforeApproval=await Assert.ThrowsAsync<NativeQueryException>(()=>Query(reader));
                Assert.Contains("not approved",beforeApproval.Message,StringComparison.Ordinal);
                var management=new ManagementClient("localhost",record.GetProperty("AuthorizationManagementPort").GetInt32(),certificate);
                var token=issuer.CreateToken(OidcDevelopmentIssuer.Audience,"admin",scope:"seamless.database");
                var current=await management.GatewayPublicationAsync(token,new(databaseId,"secured","value"));
                Assert.Equal(approval.QueryGenerationId,current.QueryGenerationId);
                Assert.Equal(approval.AssemblySha256,current.AssemblySha256);
                var unapproved=await management.GatewayPublicationsAsync(token,new());
                Assert.Equal(new[]{"value","value2"},unapproved.Items.Select(static item=>item.QueryId));
                Assert.All(unapproved.Items,static item=>Assert.Equal("secured",item.ModuleId));
                var capabilities=(await management.InfoAsync()).Capabilities;
                Assert.Contains("GatewayApprovalExportV1",capabilities);
                Assert.DoesNotContain("GatewayApprovalRegistrationV1",capabilities);
                var request=ApprovalRequest();
                var exporter=issuer.CreateToken(OidcDevelopmentIssuer.Audience,"db-manager",scope:"seamless.database");
                var exported=await management.ExportGatewayApprovalAsync(exporter,request);
                Assert.False(exported.Registered);
                using var document=JsonDocument.Parse(exported.Document!);
                Assert.Equal(instance,document.RootElement.GetProperty("ManagementInstanceId").GetGuid());
                Assert.Equal(approval.QueryGenerationId,document.RootElement.GetProperty("Queries")[0].GetProperty("QueryGenerationId").GetGuid());
                Assert.Equal("Unsupported",(await Assert.ThrowsAsync<ManagementException>(()=>management.RegisterGatewayApprovalAsync(token,request))).Code);
                Assert.Contains("not approved",(await Assert.ThrowsAsync<NativeQueryException>(()=>Query(reader))).Message,StringComparison.Ordinal);

                // 有効な行が先頭でも、後続行の実成果物が一致しなければ文書全体を拒否する。
                await SaveApprovals([approval,approval with{QueryGenerationId=Guid.NewGuid(),AssemblySha256=new string('0',64)}]);
                await Task.Delay(TimeSpan.FromSeconds(3));
                Assert.Contains("not approved",(await Assert.ThrowsAsync<NativeQueryException>(()=>Query(reader))).Message,StringComparison.Ordinal);

                await SaveApprovals([approval]);
                using var wait=new CancellationTokenSource(TimeSpan.FromSeconds(15));
                while(true){
                    try{await Query(reader,wait.Token);break;}
                    catch(NativeQueryException error)when(error.Message.Contains("not approved",StringComparison.Ordinal)){await Task.Delay(100,wait.Token);}
                }
                Assert.Equal(8,await Query(reader,wait.Token));
                await using var transaction=await reader.BeginTransactionAsync("lab",wait.Token);
                Assert.Equal(8,await QueryTransaction(transaction,wait.Token));
                await SaveApprovals([]);
                await Task.Delay(TimeSpan.FromSeconds(3),wait.Token);
                Assert.Equal(8,await QueryTransaction(transaction,wait.Token));
                await transaction.RollbackAsync(wait.Token);
                Assert.Equal(8,await Query(reader,wait.Token));
                await SaveApprovals([approval]);
            },diagnostics:["A new approval does not match the current native publication.","The native approval file cannot remove an existing approval."]);
            await Run(fileSettings,true,true,async record=>{
                await using var reader=Client(record,"other-user");
                Assert.Equal(8,await Query(reader));
            });
            var managedPath=Path.Combine(directory,"managed-approvals.json");
            var empty=JsonSerializer.SerializeToUtf8Bytes(new GatewayNativeApprovalDocument(instance,captured.WalId,[]));
            await File.WriteAllBytesAsync(managedPath,empty);
            var managedSettings=fileSettings with{Native=fileSettings.Native! with{ApprovalFile=Path.GetFileName(managedPath),ManageApprovalFile=true}};
            var registeredId=Guid.Empty;
            await Run(managedSettings,true,true,async record=>{
                var management=new ManagementClient("localhost",record.GetProperty("AuthorizationManagementPort").GetInt32(),certificate);
                Assert.Contains("GatewayApprovalRegistrationV1",(await management.InfoAsync()).Capabilities);
                var token=issuer.CreateToken(OidcDevelopmentIssuer.Audience,"admin",scope:"seamless.database");
                var manager=issuer.CreateToken(OidcDevelopmentIssuer.Audience,"db-manager",scope:"seamless.database");
                var request=ApprovalRequest();
                Assert.Equal("NotGranted",(await Assert.ThrowsAsync<ManagementException>(()=>management.RegisterGatewayApprovalAsync(manager,request))).Code);
                Assert.Equal("CatalogChanged",(await Assert.ThrowsAsync<ManagementException>(()=>management.RegisterGatewayApprovalAsync(token,request with{
                    Publication=request.Publication with{ExpectedGenerationId=Guid.NewGuid()}}))).Code);
                Assert.Equal("UnapprovedExecution",(await Assert.ThrowsAsync<ManagementException>(()=>management.ExportGatewayApprovalAsync(token,request with{
                    Access=[new(outsideTableId,SecurityPermission.Read,true)]}))).Code);
                await using var reader=Client(record,"other-user");
                Assert.Contains("not approved",(await Assert.ThrowsAsync<NativeQueryException>(()=>Query(reader))).Message,StringComparison.Ordinal);
                await File.AppendAllTextAsync(managedPath," ");
                Assert.Equal("Conflict",(await Assert.ThrowsAsync<ManagementException>(()=>management.RegisterGatewayApprovalAsync(token,request))).Code);
                await File.WriteAllBytesAsync(managedPath,empty);
                var registered=await management.RegisterGatewayApprovalAsync(token,request);
                Assert.True(registered.Registered);Assert.False(registered.AlreadyRegistered);
                registeredId=registered.RegistrationId!.Value;Assert.NotEqual(Guid.Empty,registeredId);
                var persisted=JsonSerializer.Deserialize<GatewayNativeApprovalDocument>(await File.ReadAllBytesAsync(managedPath),
                    new JsonSerializerOptions{Converters={new System.Text.Json.Serialization.JsonStringEnumConverter()}})!;
                var saved=Assert.Single(persisted.Queries);
                Assert.Equal(registeredId,saved.Registration!.Id);
                Assert.Equal(actor,saved.Registration.Subject);
                Assert.Equal(approval.QueryGenerationId,saved.QueryGenerationId);
                Assert.Equal(8,await Query(reader));
                var duplicate=await management.RegisterGatewayApprovalAsync(token,request);
                Assert.True(duplicate.AlreadyRegistered);Assert.Equal(registeredId,duplicate.RegistrationId);
                Assert.Equal("Conflict",(await Assert.ThrowsAsync<ManagementException>(()=>management.RegisterGatewayApprovalAsync(token,request with{Access=[]}))).Code);
            });
            await Run(managedSettings,true,true,async record=>{
                await using var reader=Client(record,"other-user");
                Assert.Equal(8,await Query(reader));
                var management=new ManagementClient("localhost",record.GetProperty("AuthorizationManagementPort").GetInt32(),certificate);
                var token=issuer.CreateToken(OidcDevelopmentIssuer.Audience,"admin",scope:"seamless.database");
                var duplicate=await management.RegisterGatewayApprovalAsync(token,ApprovalRequest());
                Assert.True(duplicate.AlreadyRegistered);Assert.Equal(registeredId,duplicate.RegistrationId);
            });
            Guid outcomeId=default;
            await Run(outcomeSettings,true,true,async record=>{
                Assert.Equal("127.0.0.1",record.GetProperty("NativeListenAddress").GetString());
                Assert.True(record.GetProperty("NativeAuthenticated").GetBoolean());Assert.Equal(JsonValueKind.Null,record.GetProperty("HttpPort").ValueKind);Assert.Equal(JsonValueKind.Null,record.GetProperty("TdsPort").ValueKind);
                await using(var unauthenticated=NativeQueryClient.ForTls(port:record.GetProperty("NativePort").GetInt32(),trustedRoot:certificate)){await Assert.ThrowsAnyAsync<Exception>(()=>unauthenticated.ExecuteScalarAsync<int,int>("secured","value","lab",binding.SchemaFingerprint,7));}
                await using var client=Client(record);Assert.Equal(8,await Query(client));Assert.Equal(8,await client.ExecuteScalarAsync<int,int>("secured","value","LAB",binding.SchemaFingerprint,7));
                await using(var reader=Client(record,"other-user")){
                    Assert.Equal(8,await Query(reader));
                }
                var management=new ManagementClient("localhost",record.GetProperty("AuthorizationManagementPort").GetInt32(),certificate);var token=issuer.CreateToken(OidcDevelopmentIssuer.Audience,"admin",scope:"seamless.database");
                var info=await management.InfoAsync();
                Assert.Contains("GatewayPublicationIdentityV1",info.Capabilities);
                Assert.Contains("GatewayPublicationListV1",info.Capabilities);
                var selector=new GatewayPublicationRequest(databaseId,"secured","value");
                using var browserDeadline=new CancellationTokenSource(TimeSpan.FromSeconds(25));
                using var browser=new HttpClient(new HttpClientHandler{UseProxy=false,AllowAutoRedirect=false});
                Task? navigation=null;
                var signIn=await Seamless.Authentication.OidcBrowserSignIn.SignInAsync(issuer.Settings,(url,cancellation)=>{
                    navigation=FollowCallbackAsync(url,cancellation);
                    return Task.CompletedTask;
                },cancellationToken:browserDeadline.Token);
                await navigation!;
                var firstPage=await management.GatewayPublicationsAsync(signIn.AccessToken,new(PageSize:1));
                var selected=Assert.Single(firstPage.Items);
                Assert.Equal(new GatewayPublicationItem(databaseId,"lab","secured","value",approval.QueryGenerationId,true),selected);
                Assert.NotNull(firstPage.Next);
                var lastPage=await management.GatewayPublicationsAsync(signIn.AccessToken,new(firstPage.Next,1));
                Assert.Equal("value2",Assert.Single(lastPage.Items).QueryId);
                Assert.Null(lastPage.Next);
                var publication=await management.GatewayPublicationAsync(signIn.AccessToken,selector with{ExpectedGenerationId=selected.GenerationId});
                Assert.Equal(instance,publication.ManagementInstanceId);
                Assert.Equal(captured.WalId,publication.GatewayWalId);
                Assert.Equal(approval.QueryGenerationId,publication.QueryGenerationId);
                Assert.Equal(approval.ManifestSha256,publication.ManifestSha256);
                Assert.Equal(approval.AssemblySha256,publication.AssemblySha256);
                Assert.Equal(new GatewayPublicationDatabase(databaseId,binding.DatabaseId,binding.Name,binding.GenerationId,binding.SchemaFingerprint,binding.AssemblySha256),Assert.Single(publication.Databases));
                var userToken=issuer.CreateToken(OidcDevelopmentIssuer.Audience,"other-user",scope:"seamless.database");
                Assert.Empty((await management.GatewayPublicationsAsync(userToken,new())).Items);
                Assert.Equal("CatalogChanged",(await Assert.ThrowsAsync<ManagementException>(()=>management.GatewayPublicationsAsync(token,new(firstPage.Next,1)))).Code);
                Assert.Equal("Validation",(await Assert.ThrowsAsync<ManagementException>(()=>management.GatewayPublicationsAsync(token,new(PageSize:101)))).Code);
                Assert.Equal("CatalogChanged",(await Assert.ThrowsAsync<ManagementException>(()=>management.GatewayPublicationAsync(token,selector with{ExpectedGenerationId=Guid.NewGuid()}))).Code);
                Assert.Equal("NotGranted",(await Assert.ThrowsAsync<ManagementException>(()=>management.GatewayPublicationAsync(userToken,selector))).Code);
                Assert.Equal("NotGranted",(await Assert.ThrowsAsync<ManagementException>(()=>management.GatewayPublicationAsync(token,selector with{ModuleId="cross"}))).Code);
                Assert.Equal("NotFound",(await Assert.ThrowsAsync<ManagementException>(()=>management.GatewayPublicationAsync(token,selector with{QueryId="absent"}))).Code);
                Assert.Equal("Validation",(await Assert.ThrowsAsync<ManagementException>(()=>management.GatewayPublicationAsync(token,selector with{ModuleId=new string('x',1025)}))).Code);
                var before=await management.DatabaseAuthorizationAsync(token,databaseId);var changed=await management.ChangeDatabaseAuthorizationAsync(token,new(Guid.NewGuid(),databaseId,before.Version){Unassign=[new(actor,SecurityPrincipalKind.User,queryRole)]});
                Assert.Equal("CatalogChanged",(await Assert.ThrowsAsync<ManagementException>(()=>management.GatewayPublicationsAsync(signIn.AccessToken,new(firstPage.Next,1)))).Code);
                await Assert.ThrowsAsync<NativeQueryException>(()=>Query(client));await management.ChangeDatabaseAuthorizationAsync(token,new(Guid.NewGuid(),databaseId,changed.Version){Assign=[new(actor,SecurityPrincipalKind.User,queryRole)]});Assert.Equal(8,await Query(client));
                await using var transaction=await client.BeginTransactionAsync("lab");
                outcomeId=transaction.Id;
                await transaction.CommitAsync();
                Assert.Equal(NativeTransactionOutcomeKind.Committed,(await transaction.GetOutcomeAsync()).Kind);

                async Task FollowCallbackAsync(Uri url,CancellationToken cancellation){
                    var callback=await OidcSignInRequest.BrowserAsync(url);
                    using var response=await browser.GetAsync(callback,cancellation);
                    Assert.Equal(System.Net.HttpStatusCode.OK,response.StatusCode);
                }
            },outcomeKey:outcomeKeyText);
            await Run(outcomeSettings,true,true,async record=>{
                Assert.Equal("::1",record.GetProperty("NativeListenAddress").GetString());Assert.True(record.GetProperty("NativeAuthenticated").GetBoolean());
                await using var client=Client(record);Assert.Equal(8,await Query(client));
                await using var reader=Client(record,"other-user");
                await using(var transaction=await reader.BeginTransactionAsync("lab")){
                    Assert.Equal(8,await QueryTransaction(transaction));
                    await transaction.RollbackAsync();
                }
                Assert.NotEqual(Guid.Empty,outcomeId);
                Assert.Equal(NativeTransactionOutcomeKind.Committed,(await client.GetTransactionOutcomeAsync("lab",outcomeId)).Kind);
            },address:"::1",outcomeKey:outcomeKeyText);
            await Run(settings with{ProtectHttp=true,ProtectTds=true,MaximumConcurrentIdentityVerifications=1},true,true,async record=>{
                Assert.True(record.GetProperty("TdsAuthenticated").GetBoolean());
                var trust=new X509ChainPolicy{TrustMode=X509ChainTrustMode.CustomRootTrust,RevocationMode=X509RevocationMode.NoCheck};trust.CustomTrustStore.Add(certificate);
                using var http=new HttpClient(new SocketsHttpHandler{UseProxy=false,SslOptions=new SslClientAuthenticationOptions{CertificateChainPolicy=trust}}){
                    BaseAddress=new($"https://127.0.0.1:{record.GetProperty("HttpPort").GetInt32()}")
                };
                var token=issuer.CreateToken(OidcDevelopmentIssuer.Audience,"admin",scope:"seamless.database");
                http.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",token);
                using var management=new ManagementClient("127.0.0.1",record.GetProperty("AuthorizationManagementPort").GetInt32(),certificate);
                using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(25));await using var client=Client(record,hostnameOverride:"127.0.0.1");
                var pin=Path.Combine(directory,"identity-tds.cer");
                await File.WriteAllBytesAsync(pin,certificate.Export(X509ContentType.Cert),timeout.Token);
                var connection=new SqlConnectionStringBuilder{
                    DataSource=$"tcp:127.0.0.1,{record.GetProperty("TdsPort").GetInt32()}",InitialCatalog="lab",
                    Encrypt=SqlConnectionEncryptOption.Strict,Pooling=false,ConnectTimeout=5,ConnectRetryCount=0
                };
                connection["Server Certificate"]=pin;
                try{
                    await WhileMembershipCheckIsPendingAsync("Native",()=>client.ConnectAsync(timeout.Token),async()=>{
                        await AssertHttpAndManagementBusyAsync();
                        await using var tds=new SqlConnection(connection.ConnectionString){AccessToken=token};
                        var error=await Assert.ThrowsAsync<SqlException>(()=>tds.OpenAsync(timeout.Token));
                        Assert.Equal(18456,error.Number);
                    });
                    await using var accepted=new SqlConnection(connection.ConnectionString){AccessToken=token};
                    await WhileMembershipCheckIsPendingAsync("TDS",()=>accepted.OpenAsync(timeout.Token),async()=>{
                        await AssertHttpAndManagementBusyAsync();
                        await using var newcomer=Client(record,hostnameOverride:"127.0.0.1");
                        var error=await Assert.ThrowsAsync<NativeAuthenticationException>(()=>newcomer.ConnectAsync(timeout.Token));
                        Assert.Equal(NativeAuthenticationError.Busy,error.Error);
                        Assert.Equal(8,await Query(client));
                    });
                    using var sql=new SqlCommand("SELECT 1",accepted);
                    Assert.Equal(1,(int)(await sql.ExecuteScalarAsync(timeout.Token))!);
                    using var allowed=await http.GetAsync("/api/databases",timeout.Token);Assert.Equal(HttpStatusCode.OK,allowed.StatusCode);
                    Assert.True((await management.DatabaseAuthorizationAsync(token,databaseId,cancellationToken:timeout.Token)).Version>=1);
                    await client.ReauthenticateAsync(timeout.Token);
                    Assert.Equal(8,await Query(client));
                }finally{File.Delete(pin);}

                async Task AssertHttpAndManagementBusyAsync(){
                    using var busy=await http.GetAsync("/api/databases",timeout.Token);Assert.Equal(HttpStatusCode.ServiceUnavailable,busy.StatusCode);
                    var rejected=await Assert.ThrowsAsync<ManagementException>(()=>management.DatabaseAuthorizationAsync(token,databaseId,cancellationToken:timeout.Token));
                    Assert.Equal("AuthenticationBusy",rejected.Code);
                }
                async Task WhileMembershipCheckIsPendingAsync(string endpoint,Func<Task> hold,Func<Task> verify){
                    var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    issuer.MembershipHook=cancel=>{entered.TrySetResult();return release.Task.WaitAsync(cancel);};
                    var before=issuer.MembershipChecks;
                    var clock=Stopwatch.StartNew();
                    var pending=hold();
                    var complete=false;
                    try{
                        var first=await Task.WhenAny(entered.Task,pending).WaitAsync(timeout.Token);
                        if(first==pending){await pending;throw new InvalidOperationException("Authentication completed before reaching the membership gate.");}
                        await verify();
                        Assert.Equal(before+1,issuer.MembershipChecks);
                        complete=true;
                    }catch(Exception error){
                        throw new InvalidOperationException($"{endpoint} identity-capacity check failed after {clock.Elapsed.TotalMilliseconds:F0}ms.",error);
                    }finally{
                        issuer.MembershipHook=null;
                        release.TrySetResult();
                        if(!complete){try{await pending;}catch{/* The failure above identifies the stage that stopped the probe. */}}
                    }
                    await pending;
                }
            });
            foreach(var wrong in new[]{approval with{QueryGenerationId=Guid.NewGuid()},approval with{ManifestSha256=new string('0',64)},approval with{AssemblySha256=new string('0',64)},approval with{Databases=[approval.Databases[0] with{GenerationId=Guid.NewGuid()}]},approval with{Databases=[approval.Databases[0] with{AssemblySha256=new string('0',64)}]},approval with{Databases=[approval.Databases[0] with{SchemaFingerprint="unapproved"}]}}){
                await Run(settings with{Native=new([wrong])},true,true,async record=>{await using var client=Client(record);await Assert.ThrowsAsync<NativeQueryException>(()=>Query(client));});
            }
            async Task SaveApprovals(IReadOnlyList<GatewayNativeQueryResource> entries){
                var temporary=approvalPath+".new";
                await File.WriteAllTextAsync(temporary,JsonSerializer.Serialize(new GatewayNativeApprovalDocument(instance,captured.WalId,entries)));
                File.Move(temporary,approvalPath,overwrite:true);
            }
        }finally{File.Delete(pfx);}
        GatewayPublicationApprovalRequest ApprovalRequest()=>new(new(databaseId,approval.ModuleId,approval.QueryId){
            ExpectedGenerationId=approval.QueryGenerationId},queryId,[new(tableId,SecurityPermission.Read,true)]);
        NativeQueryClient Client(JsonElement record,string subject="admin",string? hostnameOverride=null){
            var hostname=hostnameOverride??(record.GetProperty("NativeListenAddress").GetString()=="::1"?"::1":"localhost");
            return NativeQueryClient.ForAuthenticatedTls(_=>ValueTask.FromResult(issuer.CreateToken(OidcDevelopmentIssuer.Audience,subject,scope:"seamless.database")),host:hostname,port:record.GetProperty("NativePort").GetInt32(),trustedRoot:certificate);
        }
        Task<int> Query(NativeQueryClient client,CancellationToken cancellationToken=default)=>client.ExecuteScalarAsync<int,int>("secured","value","lab",binding.SchemaFingerprint,7,cancellationToken);
        async Task<int> QueryTransaction(NativeTransaction transaction,CancellationToken cancellationToken=default){
            var result=0;var count=0;
            await foreach(var row in transaction.QueryAsync<int,int>("secured","value",binding.SchemaFingerprint,7,cancellationToken)){result=row;count++;}
            Assert.Equal(1,count);return result;
        }
        async Task Run(GatewayAuthorizationConfiguration configuration,bool certificateEnabled,bool readyExpected,Func<JsonElement,Task>? inspect=null,string address="127.0.0.1",string? outcomeKey=null,string[]? diagnostics=null){
            var id=Guid.NewGuid().ToString("N");var path=Path.Combine(directory,id+".json");var ready=Path.Combine(directory,id+".ready");var stop=Path.Combine(directory,id+".stop");
            await File.WriteAllTextAsync(path,JsonSerializer.Serialize(new{StorageDirectory=storage,WorkingDirectory=Path.Combine(directory,"w"),CacheDirectory=Path.Combine(directory,"c"),DotNetExecutable=dotnet,GeneratorAssemblyPath=typeof(Seamless.Database.Generator.DatabaseSchemaGenerator).Assembly.Location,
                Authorization=configuration,NativeCertificatePath=certificateEnabled?"native.pfx":null,HttpCertificatePath=configuration.ProtectHttp?"native.pfx":null,TdsCertificatePath=configuration.ProtectTds?"native.pfx":null,DatabaseOptions=new{IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20},Endpoints=new{TdsPort=0,HttpPort=0,NativePort=0,NativeListenAddress=address,Http=new{ListenAddress="127.0.0.1"}}}));
            var start=new ProcessStartInfo(dotnet){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardOutput=true,RedirectStandardError=true};start.Environment["SEAMLESS_GATEWAY_NATIVE_CERT_PASSWORD"]=password;start.Environment["SEAMLESS_GATEWAY_HTTP_CERT_PASSWORD"]=password;start.Environment["SEAMLESS_GATEWAY_TDS_CERT_PASSWORD"]=password;
            if(outcomeKey is null){start.Environment.Remove("SEAMLESS_GATEWAY_NATIVE_OUTCOME_KEY");}else{start.Environment["SEAMLESS_GATEWAY_NATIVE_OUTCOME_KEY"]=outcomeKey;}
            if(Environment.GetEnvironmentVariable("SEAMLESS_TEST_PACKAGED_GATEWAY") is{} packaged){Assert.True(Path.IsPathFullyQualified(packaged)&&File.Exists(packaged));start.ArgumentList.Add(packaged);}else{start.ArgumentList.Add(typeof(CrashProbe).Assembly.Location);start.ArgumentList.Add("--gateway-cli");}
            foreach(var argument in new[]{"--gateway",path,"--ready-file",ready,"--stop-file",stop}){start.ArgumentList.Add(argument);}
            using var process=Process.Start(start)!;var created=process.StartTime.ToUniversalTime();var output=process.StandardOutput.ReadToEndAsync();var errors=process.StandardError.ReadToEndAsync();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(90));
            try{
                if(readyExpected){while(!File.Exists(ready)&&!process.HasExited){await Task.Delay(50,timeout.Token);}if(process.HasExited){Assert.Fail((await output)+(await errors));}using var record=JsonDocument.Parse(await File.ReadAllTextAsync(ready,timeout.Token));Assert.Equal(process.Id,record.RootElement.GetProperty("Pid").GetInt32());Assert.Equal(created,record.RootElement.GetProperty("CreatedUtc").GetDateTime());await inspect!(record.RootElement);await File.WriteAllTextAsync(stop,"stop",timeout.Token);}
                await process.WaitForExitAsync(timeout.Token);Assert.Equal(readyExpected?0:1,process.ExitCode);Assert.Equal(readyExpected,File.Exists(ready));
                if(diagnostics is not null){var written=await errors;foreach(var message in diagnostics){Assert.Contains(message,written,StringComparison.Ordinal);}}
            }finally{if(!process.HasExited&&process.StartTime.ToUniversalTime()==created){process.Kill(entireProcessTree:true);await process.WaitForExitAsync();}}
        }
    },includeLoopbackAddress:true,includeIPv6LoopbackAddress:true);
}
