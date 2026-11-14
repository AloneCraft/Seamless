using System.Net;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json;
using Seamless.Authentication;
using Seamless.Management;
using Seamless.Management.Server;
using Seamless.Security;
using Xunit.Abstractions;
using Policy=Seamless.Security.AuthorizationPolicySnapshot;

namespace Management.Tests;

[Collection("AuditStorage")]
public sealed class ManagementAuthorizationTransport(ITestOutputHelper output){
    [Fact]
    public Task AuthorizationCatalogListsOnlyManageableDatabasesAndRejectsStalePages()=>ManagementTransport.WithServerCertificate(async certificate=>{
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,Guid.NewGuid().ToString("N"));
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();
        using var store=ManagementStore.Initialize(root,"admin","TestPassword-123");
        var serverId=store.InstanceId;
        var databases=Enumerable.Range(0,103)
            .Select(index=>new SecurityResource(Guid.NewGuid(),serverId,SecurityResourceKind.Database,$"db-{index}")).ToArray();
        var hidden=databases[0];
        var delegated=databases[1];
        var schema=new SecurityResource(Guid.NewGuid(),delegated.Id,SecurityResourceKind.Schema,"schema");
        var administrator=new SecuritySubjectId(issuer.Id,"administrator");
        var developer=new SecuritySubjectId(issuer.Id,"developer");
        var reader=new SecuritySubjectId(issuer.Id,"reader");
        var rootRole=Guid.NewGuid();
        var developerRole=Guid.NewGuid();
        var readerRole=Guid.NewGuid();
        var hiddenGrant=Guid.NewGuid();
        var hiddenDeny=Guid.NewGuid();
        var initial=new Policy(1,[new(serverId,null,SecurityResourceKind.Server,"server"),schema,..databases],
            [new(rootRole,"root",[new(serverId,SecurityPermission.All)]),
                new(developerRole,"db-admin",[new(delegated.Id,SecurityPermission.ManagePermissions)]),
                new(readerRole,"reader",[new(delegated.Id,SecurityPermission.Discover|SecurityPermission.Read)]),
                new(Guid.NewGuid(),"wide-role",Enumerable.Repeat(new PermissionRule(delegated.Id,SecurityPermission.Read),125)),
                new(hiddenGrant,"hidden-grant",[new(hidden.Id,SecurityPermission.ManagePermissions)]),
                new(hiddenDeny,"hidden-deny",[new(hidden.Id,SecurityPermission.None,SecurityPermission.ManagePermissions)])],
            [new(administrator,SecurityPrincipalKind.User,rootRole),new(developer,SecurityPrincipalKind.User,developerRole),
                new(developer,SecurityPrincipalKind.User,hiddenGrant),new(developer,SecurityPrincipalKind.User,hiddenDeny),
                new(reader,SecurityPrincipalKind.User,readerRole)]);
        var now=DateTimeOffset.UtcNow;
        using var policy=StoredAuthorization.Initialize(store,initial,new(administrator,"administrator",[],now.AddMinutes(5),now),issuer.Settings);
        using var tokens=new OidcTokenVerifier(issuer.Settings);
        await using var server=Seamless.Management.Server.ManagementServer.ForAuthorization(policy,tokens,certificate,new(IPAddress.Loopback,0));
        server.Start();
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var token=deadline.Token;
        using var client=new ManagementClient("localhost",server.Port,certificate);
        Assert.Contains("AuthorizationDatabaseListV1",(await client.InfoAsync(token)).Capabilities);
        string Access(SecuritySubjectId subject)=>issuer.CreateToken(OidcDevelopmentIssuer.Audience,subject.Value,scope:"seamless.database");
        var adminToken=Access(administrator);
        var developerToken=Access(developer);
        var first=await client.AuthorizationDatabasesAsync(adminToken,new(),token);
        Assert.Equal(1,first.Version);
        Assert.Equal(100,first.Items.Length);
        Assert.True(first.HasMore);
        var nextQuery=new DatabaseAuthorizationListQuery(first.Version,first.NextCursor);
        var second=await client.AuthorizationDatabasesAsync(adminToken,nextQuery,token);
        Assert.False(second.HasMore);
        Assert.Equal(databases.OrderBy(static item=>item.Id),first.Items.Concat(second.Items));
        Assert.Equal(delegated,Assert.Single((await client.AuthorizationDatabasesAsync(developerToken,new(),token)).Items));
        Assert.Empty((await client.AuthorizationDatabasesAsync(Access(reader),new(),token)).Items);
        await RejectAsync("Validation",()=>client.AuthorizationDatabasesAsync(adminToken,new(Cursor:1),token));
        await RejectAsync("Validation",()=>client.AuthorizationDatabasesAsync(developerToken,nextQuery,token));
        await client.ChangeDatabaseAuthorizationAsync(adminToken,new(Guid.NewGuid(),delegated.Id,1){Unassign=[new(developer,SecurityPrincipalKind.User,developerRole)]},token);
        await RejectAsync("Conflict",()=>client.AuthorizationDatabasesAsync(adminToken,nextQuery,token));
        var refreshed=await client.AuthorizationDatabasesAsync(developerToken,new(),token);
        Assert.Equal(2,refreshed.Version);
        Assert.Empty(refreshed.Items);
        if(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_UI_TEST_EXE") is{} manager){
            using var uiDeadline=new CancellationTokenSource(TimeSpan.FromSeconds(110));
            var uiToken=uiDeadline.Token;
            var pin=Path.Combine(root,"catalog.cer");
            var config=Path.Combine(root,"catalog-ui.json");
            var logs=Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_LOG")??throw new InvalidOperationException("Missing Manager evidence directory.");
            var image=Path.Combine(logs,"gateway-database-authorization.png");
            await File.WriteAllBytesAsync(pin,certificate.Export(System.Security.Cryptography.X509Certificates.X509ContentType.Cert),uiToken);
            await File.WriteAllTextAsync(config,JsonSerializer.Serialize(new{
                ホスト="127.0.0.1",ポート=server.Port,利用者=administrator.Value,証明書=pin,定義ID="",
                DB権限だけか=true,試験認証元=issuer.Id,試験対象主体="gui-assignment",試験DB名=delegated.Name
            },ManagementJson.Options),uiToken);
            var start=new ProcessStartInfo(manager){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};
            start.Environment["SEAMLESS_MANAGEMENT_UI_TEST_BEARER"]=Access(administrator);
            start.Environment["SEAMLESS_MANAGEMENT_HOME"]=Path.Combine(root,"ui");
            start.Environment["SEAMLESS_MANAGEMENT_ROOT"]=root;
            foreach(var argument in new[]{"--smoke",config,image}){start.ArgumentList.Add(argument);}
            using var process=Process.Start(start)!;
            try{await process.WaitForExitAsync(uiToken);}
            finally{if(!process.HasExited){process.Kill(entireProcessTree:true);await process.WaitForExitAsync();}}
            Assert.True(process.ExitCode==0,File.Exists(image+".error.txt")?File.ReadAllText(image+".error.txt"):"Manager exit="+process.ExitCode);
            Assert.True(new FileInfo(image).Length>0);
            using var evidence=JsonDocument.Parse(await File.ReadAllTextAsync(image+".result.json",uiToken));
            var result=evidence.RootElement;
            Assert.Equal(process.Id,result.GetProperty("pid").GetInt32());
            Assert.Equal(Path.GetFullPath(manager),result.GetProperty("実行ファイル").GetString(),ignoreCase:true);
            Assert.Equal(delegated.Id,result.GetProperty("対象DB").GetGuid());
            foreach(var action in new[]{"割当","照会","解除","ロール作成","ロール編集","ロール削除","複数ページの規則"}){Assert.True(result.GetProperty(action).GetBoolean());}
            Assert.True(new FileInfo(image+".role.png").Length>0);
            Assert.Equal(7,policy.Policy.Version);
            var finalPolicy=AuthorizationPolicyDocument.Capture(policy.Policy);
            Assert.DoesNotContain(finalPolicy.Roles,role=>role.Name.StartsWith("gui-",StringComparison.Ordinal));
            Assert.DoesNotContain(finalPolicy.Bindings,binding=>binding.Subject.Value=="gui-assignment");
        }

        static async Task RejectAsync(string code,Func<Task> action){
            Assert.Equal(code,(await Assert.ThrowsAsync<ManagementException>(action)).Code);
        }
    },includeLoopbackAddress:true);

    [Fact]
    public Task SubjectStatusSurvivesRestartAndCancelsLeasesOnEveryStoredHost()=>ManagementTransport.WithServerCertificate(async certificate=>{
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,Guid.NewGuid().ToString("N"));
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();
        using var tokens=new OidcTokenVerifier(issuer.Settings);
        var elapsed=Stopwatch.StartNew();
        var administrator=new SecuritySubjectId(issuer.Id,"root");
        var developer=new SecuritySubjectId(issuer.Id,"db-admin");
        var database=Guid.NewGuid();var adminRole=Guid.NewGuid();var dbRole=Guid.NewGuid();
        var administratorToken=Access(administrator);var developerToken=Access(developer);
        var disable=new AuthorizationSubjectChange(Guid.NewGuid(),developer,true);
        AuthorizationSubjectChangeResult committed;
        using(var store=ManagementStore.Initialize(root,"admin","TestPassword-123")){
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var token=timeout.Token;
            var initial=new Policy(1,[new(store.InstanceId,null,SecurityResourceKind.Server,"server"),new(database,store.InstanceId,SecurityResourceKind.Database,"sales")],
                [new(adminRole,"root",[new(store.InstanceId,SecurityPermission.All)]),new(dbRole,"db-admin",[new(database,SecurityPermission.ManagePermissions)])],
                [new(administrator,SecurityPrincipalKind.User,adminRole),new(developer,SecurityPrincipalKind.User,dbRole)]);
            using var writer=StoredAuthorization.Initialize(store,initial,Identity(administrator),issuer.Settings);
            using var observer=StoredAuthorization.Open(store);
            await using var server=Seamless.Management.Server.ManagementServer.ForAuthorization(writer,tokens,certificate,new(IPAddress.Loopback,0));
            server.Start();
            var client=new ManagementClient("localhost",server.Port,certificate);
            Assert.Contains("AuthorizationSubjectStatusV1",(await client.InfoAsync(token)).Capabilities);
            await RejectAsync("NotGranted",()=>client.ChangeAuthorizationSubjectAsync(developerToken,disable,token));
            await RejectAsync("Validation",()=>client.ChangeAuthorizationSubjectAsync(administratorToken,disable with{Subject=administrator},token));
            using var target=observer.CreateSession(Identity(developer));
            using var active=target.BeginTransaction(token);
            using var differentAuthority=observer.CreateSession(Identity(new(Guid.NewGuid(),developer.Value)));
            using var unrelated=differentAuthority.BeginOperation(token);
            writer.Checkpoint=phase=>{if(phase=="SubjectPrepared"){throw new IOException("before commit");}};
            await RejectAsync("OperationFailed",()=>client.ChangeAuthorizationSubjectAsync(administratorToken,disable,token));
            Assert.False(active.CancellationToken.IsCancellationRequested);
            await RejectAsync("NotFound",()=>client.AuthorizationSubjectChangeResultAsync(administratorToken,disable.OperationId,token));

            writer.Checkpoint=phase=>{if(phase=="SubjectCommitted"){throw new IOException("response lost after commit");}};
            await RejectAsync("OperationFailed",()=>client.ChangeAuthorizationSubjectAsync(administratorToken,disable,token));
            Assert.True(active.CancellationToken.IsCancellationRequested);
            Assert.False(unrelated.CancellationToken.IsCancellationRequested);
            Assert.Equal("AccountDisabled",Assert.Throws<AuthorizationException>(()=>target.BeginOperation(token)).Code);
            await RejectAsync("AccountDisabled",()=>client.DatabaseAuthorizationAsync(developerToken,database,cancellationToken:token));
            committed=await client.AuthorizationSubjectChangeResultAsync(administratorToken,disable.OperationId,token);
            Assert.True(committed.Disabled);
            Assert.Equal(committed,await client.ChangeAuthorizationSubjectAsync(administratorToken,disable,token));
            await RejectAsync("Conflict",()=>client.ChangeAuthorizationSubjectAsync(administratorToken,disable with{Disabled=false},token));
            var audit=Assert.Single(store.AuditEvents(new(Action:AuditAction.AccountChange)).Items,item=>item.OperationId==disable.OperationId);
            Assert.Equal("SubjectDisabled",audit.ResultCode);
            Assert.Equal(administrator.Value,audit.SubjectId);
            output.WriteLine($"Disabled and audited at {elapsed.Elapsed.TotalSeconds:F2}s.");
        }
        output.WriteLine($"Stopped and closed the first store at {elapsed.Elapsed.TotalSeconds:F2}s.");
        using(var reopened=new ManagementStore(root))using(var host=StoredAuthorization.Open(reopened)){
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var token=timeout.Token;
            await using var server=Seamless.Management.Server.ManagementServer.ForAuthorization(host,tokens,certificate,new(IPAddress.Loopback,0));
            server.Start();
            var client=new ManagementClient("localhost",server.Port,certificate);
            await RejectAsync("AccountDisabled",()=>client.DatabaseAuthorizationAsync(developerToken,database,cancellationToken:token));
            Assert.Equal(committed,await client.AuthorizationSubjectChangeResultAsync(administratorToken,disable.OperationId,token));
            var enabled=await client.ChangeAuthorizationSubjectAsync(administratorToken,disable with{OperationId=Guid.NewGuid(),Disabled=false},token);
            Assert.False(enabled.Disabled);
            Assert.Equal(1,(await client.DatabaseAuthorizationAsync(developerToken,database,cancellationToken:token)).Version);
            await RejectAsync("NotFound",()=>client.AuthorizationSubjectChangeResultAsync(developerToken,disable.OperationId,token));
            Assert.Equal(committed,await client.ChangeAuthorizationSubjectAsync(administratorToken,disable,token));
            Assert.Equal(1,(await client.DatabaseAuthorizationAsync(developerToken,database,cancellationToken:token)).Version);
            output.WriteLine($"Reopened, re-enabled and verified at {elapsed.Elapsed.TotalSeconds:F2}s.");
        }
        string Access(SecuritySubjectId subject)=>issuer.CreateToken(OidcDevelopmentIssuer.Audience,subject.Value,scope:"seamless.database");
        static AuthenticatedIdentity Identity(SecuritySubjectId subject)=>new(subject,subject.Value,[],DateTimeOffset.UtcNow.AddHours(1),DateTimeOffset.UtcNow);
        static async Task RejectAsync(string code,Func<Task> action)=>Assert.Equal(code,(await Assert.ThrowsAsync<ManagementException>(action)).Code);
    });

    [Fact]
    public void OversizedAuthorizationChangeIsAbortedWithoutFaultingTheStore(){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,Guid.NewGuid().ToString("N"));
        var database=Guid.NewGuid();var role=Guid.NewGuid();var administrator=new SecuritySubjectId(Guid.NewGuid(),"administrator");
        using(var store=ManagementStore.Initialize(root,"admin","TestPassword-123")){
            var initial=new Policy(1,[new(store.InstanceId,null,SecurityResourceKind.Server,"server"),new(database,store.InstanceId,SecurityResourceKind.Database,"sales")],
                [new(role,"root",[new(store.InstanceId,SecurityPermission.All)])],[new(administrator,SecurityPrincipalKind.User,role)]);
            var now=DateTimeOffset.UtcNow;
            using var policy=StoredAuthorization.Initialize(store,initial,new(administrator,"administrator",[],now.AddMinutes(5),now));
            // 大きすぎる変更は、確定の前に資源の上限(管理DBの作業メモリ)で止まる。取り消され、ストアは壊れない。
            var roles=Enumerable.Range(0,1).Select(index=>new SecurityRole(Guid.NewGuid(),"検索\n\""+index+new string('中',4_000_000),[new(database,SecurityPermission.Read)])).ToArray();
            using var operation=policy.CreateSession(new(administrator,"administrator",[],now.AddMinutes(5),now)).BeginOperation();
            Assert.Throws<Seamless.Execution.ResourceLimitException>(()=>policy.ApplyDatabaseChange(operation,new(database,1,upsertRoles:roles)));
            Assert.Equal(1,policy.Policy.Version);
            policy.ApplyDatabaseChange(operation,new(database,1,upsertRoles:[new(Guid.NewGuid(),"small",[new(database,SecurityPermission.Read)])]));
            Assert.Equal(2,policy.Policy.Version);
        }
        using var reopened=new ManagementStore(root);using var restored=StoredAuthorization.Open(reopened);Assert.Equal(2,restored.Policy.Version);
    }
    [Fact]
    public Task TlsEditsUseVerifiedIdentityDatabaseScopeAndDurableOwnedReceipts()=>ManagementTransport.WithServerCertificate(async certificate=>{
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,Guid.NewGuid().ToString("N"));
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();
        var database=Guid.NewGuid();var otherDatabase=Guid.NewGuid();var rootRole=Guid.NewGuid();var adminRole=Guid.NewGuid();var readerRole=Guid.NewGuid();var bulkRole=Guid.NewGuid();
        var administrator=new SecuritySubjectId(issuer.Id,"administrator");var developer=new SecuritySubjectId(issuer.Id,"developer");var reader=new SecuritySubjectId(issuer.Id,"reader");
        var id=Guid.NewGuid();
        var administratorToken=issuer.CreateToken(OidcDevelopmentIssuer.Audience,administrator.Value,scope:"seamless.database");
        var developerToken=issuer.CreateToken(OidcDevelopmentIssuer.Audience,developer.Value,scope:"seamless.database");
        var readerToken=issuer.CreateToken(OidcDevelopmentIssuer.Audience,reader.Value,scope:"seamless.database");
        using(var store=ManagementStore.Initialize(root,"admin","TestPassword-123")){
            var initial=new Policy(1,[new(store.InstanceId,null,SecurityResourceKind.Server,"server"),new(database,store.InstanceId,SecurityResourceKind.Database,"sales"),new(otherDatabase,store.InstanceId,SecurityResourceKind.Database,"private")],
                [new(rootRole,"root",[new(store.InstanceId,SecurityPermission.All)]),new(adminRole,"db-admin",[new(database,SecurityPermission.ManagePermissions)]),new(readerRole,"reader",[new(database,SecurityPermission.Discover|SecurityPermission.Read)]),new(bulkRole,"large role",Enumerable.Repeat(new PermissionRule(database,SecurityPermission.Read),205))],
                [new(administrator,SecurityPrincipalKind.User,rootRole),new(developer,SecurityPrincipalKind.User,adminRole),new(reader,SecurityPrincipalKind.User,readerRole)]);
            var now=DateTimeOffset.UtcNow;
            using var policy=StoredAuthorization.Initialize(store,initial,new(administrator,"administrator",[],now.AddMinutes(5),now),issuer.Settings);
            using var tokens=new OidcTokenVerifier(issuer.Settings);
            await using var server=Seamless.Management.Server.ManagementServer.ForAuthorization(policy,tokens,certificate,new(IPAddress.Loopback,0));server.Start();
            var client=new ManagementClient("localhost",server.Port,certificate);
            Assert.Contains("AuthorizationManagementV1",(await client.InfoAsync()).Capabilities);
            Assert.DoesNotContain("GatewayPublicationIdentityV1",(await client.InfoAsync()).Capabilities);
            Assert.DoesNotContain("GatewayPublicationListV1",(await client.InfoAsync()).Capabilities);
            await RejectAsync("Unsupported",()=>client.GatewayPublicationAsync(developerToken,new(database,"module","query")));
            await RejectAsync("Unsupported",()=>client.GatewayPublicationsAsync(developerToken,new()));
            var info=await client.DatabaseAuthorizationAsync(developerToken,database);
            Assert.Equal(1,info.Version);Assert.Equal(database,Assert.Single(info.Items,item=>item.Resource is not null).Resource!.Id);
            Assert.DoesNotContain(info.Items,item=>item.Role?.Id==rootRole);
            Assert.DoesNotContain(info.Items,item=>item.Binding?.Subject==administrator);
            var entries=new List<AuthorizationMetadataEntry>(info.Items);var page=info;
            while(page.HasMore){
                var previous=page.NextCursor;page=await client.DatabaseAuthorizationAsync(developerToken,database,page.Version,page.NextCursor);
                Assert.InRange(page.Items.Length,1,100);Assert.True(page.NextCursor>previous);entries.AddRange(page.Items);
            }
            Assert.Equal(205,entries.Count(item=>item.Rule?.RoleId==bulkRole));
            Assert.Single(entries,item=>item.Role?.Id==bulkRole);
            Assert.Equal(entries.Count,page.NextCursor);
            Assert.DoesNotContain(entries,item=>item.Role?.Id==rootRole||item.Binding?.Subject==administrator);
            await RejectAsync("Validation",()=>client.DatabaseAuthorizationAsync(developerToken,database,info.Version,-1));
            await RejectAsync("NotGranted",()=>client.DatabaseAuthorizationAsync(developerToken,otherDatabase));
            await RejectAsync("TokenRejected",()=>client.DatabaseAuthorizationAsync(issuer.CreateToken(OidcDevelopmentIssuer.ClientId,developer.Value),database));
            await RejectAsync("Unsupported",()=>client.LoginAsync("admin","TestPassword-123"));
            await RejectAsync("Validation",()=>client.ChangeDatabaseAuthorizationAsync(developerToken,new(Guid.NewGuid(),database,1){Assign=[null!]}));
            var redelegation=new DatabaseAuthorizationEdit(Guid.NewGuid(),database,1){Assign=[new(reader,SecurityPrincipalKind.User,adminRole)]};
            await RejectAsync("NotGranted",()=>client.ChangeDatabaseAuthorizationAsync(developerToken,redelegation));
            var edit=new DatabaseAuthorizationEdit(id,database,1){Unassign=[new(reader,SecurityPrincipalKind.User,readerRole)]};
            var result=await client.ChangeDatabaseAuthorizationAsync(developerToken,edit);
            Assert.Equal(id,result.OperationId);Assert.Equal(2,result.Version);Assert.Equal(database,result.DatabaseId);
            Assert.Equal(result,await client.AuthorizationChangeResultAsync(developerToken,id));
            await RejectAsync("NotFound",()=>client.AuthorizationChangeResultAsync(readerToken,id));
            await RejectAsync("Conflict",()=>client.ChangeDatabaseAuthorizationAsync(developerToken,edit with{OperationId=Guid.NewGuid()}));
            await RejectAsync("Conflict",()=>client.ChangeDatabaseAuthorizationAsync(developerToken,edit with{ExpectedVersion=2}));
            Assert.Equal(2,policy.Policy.Version);
            await RejectAsync("Conflict",()=>client.DatabaseAuthorizationAsync(developerToken,database,info.Version,info.NextCursor));
            await client.ChangeDatabaseAuthorizationAsync(administratorToken,new(Guid.NewGuid(),database,2){Unassign=[new(developer,SecurityPrincipalKind.User,adminRole)]});
            await RejectAsync("NotGranted",()=>client.DatabaseAuthorizationAsync(developerToken,database));
            Assert.Equal(result,await client.AuthorizationChangeResultAsync(developerToken,id));
            var audit=Assert.Single(store.AuditEvents(new(Action:AuditAction.PermissionChange)).Items,item=>item.OperationId==id);
            Assert.Equal(developer.Value,audit.SubjectId);Assert.Equal(1,audit.PolicyVersion);
            var names=Enumerable.Range(0,3).Select(index=>new SecurityRoleDocument(Guid.NewGuid(),"検索\n\""+index+new string('中',50000),ImmutableArray.Create(new PermissionRule(database,SecurityPermission.Read)))).ToArray();
            await client.ChangeDatabaseAuthorizationAsync(administratorToken,new(Guid.NewGuid(),database,3){UpsertRoles=names});
            var found=0;var pages=0;var limitedByBytes=false;page=await client.DatabaseAuthorizationAsync(administratorToken,database);
            while(true){
                pages++;Assert.InRange(JsonSerializer.SerializeToUtf8Bytes(page,ManagementJson.Options).Length,1,530000);
                found+=page.Items.Count(item=>item.Role?.Name.StartsWith("検索",StringComparison.Ordinal)==true);
                if(page.HasMore&&page.Items.Length<100){limitedByBytes=true;}
                if(!page.HasMore){break;}
                page=await client.DatabaseAuthorizationAsync(administratorToken,database,page.Version,page.NextCursor);
            }
            Assert.Equal(3,found);Assert.True(limitedByBytes);Assert.True(pages>3);
            var oversized=new SecurityRoleDocument(Guid.NewGuid(),new string('x',600000),ImmutableArray.Create(new PermissionRule(database,SecurityPermission.Read)));
            await client.ChangeDatabaseAuthorizationAsync(administratorToken,new(Guid.NewGuid(),database,4){UpsertRoles=[oversized],RemoveRoles=names.Select(static role=>role.Id).ToArray()});
            var tooLarge=await Assert.ThrowsAsync<ManagementException>(async()=>{
                var next=await client.DatabaseAuthorizationAsync(administratorToken,database);
                while(next.HasMore){next=await client.DatabaseAuthorizationAsync(administratorToken,database,next.Version,next.NextCursor);}
            });
            Assert.Equal("Limit",tooLarge.Code);
        }
        using(var reopened=new ManagementStore(root))using(var policy=StoredAuthorization.Open(reopened)){
            Assert.Equal(5,policy.Policy.Version);Assert.Equal(developer,policy.FindOperation(id)!.AppliedBy.Subject);
            Assert.Equal(2,policy.FindOperation(id)!.Version);
        }
        static async Task RejectAsync(string code,Func<Task> action){var error=await Assert.ThrowsAsync<ManagementException>(action);Assert.Equal(code,error.Code);}
    });
}
