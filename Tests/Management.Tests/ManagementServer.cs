using System.Net;
using System.Diagnostics;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Seamless.Management;
using Seamless.Management.Server;

namespace Management.Tests;

public sealed class ManagementServer{
    [Theory][InlineData(false)][InlineData(true)]public async Task Connect(bool collectReads){
        var path=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,Guid.NewGuid().ToString("N"));
        using var key=RSA.Create(2048);
        var request=new CertificateRequest("CN=localhost",key,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
        var san=new SubjectAlternativeNameBuilder();san.AddDnsName("localhost");request.CertificateExtensions.Add(san.Build());
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true,false,0,true));
        using var ephemeral=request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1),DateTimeOffset.UtcNow.AddDays(1));
        // 利用者が2026-11-02に許可した、一時的なWindowsユーザーキー。証明書ストアには登録しない。
        var cert=X509CertificateLoader.LoadPkcs12(ephemeral.Export(X509ContentType.Pfx),null,X509KeyStorageFlags.Exportable);
        string? keyName=null;CngProvider? provider=null;
        if(OperatingSystem.IsWindows()){using var privateKey=cert.GetRSAPrivateKey();if(privateKey is RSACng cng){keyName=cng.Key.KeyName;provider=cng.Key.Provider;}}
        try{
            using var store=ManagementStore.Initialize(path,"admin","TestPassword-123");
            ReadAuditOptions? readOptions=null;if(collectReads){readOptions=new(TimeSpan.FromMinutes(1),TimeSpan.FromDays(1),MaximumKeys:1,MaximumBatch:1);}using var service=new Seamless.Management.Server.ManagementService(store,readOptions);
            await using var server=new Seamless.Management.Server.ManagementServer(service,cert,new IPEndPoint(IPAddress.Loopback,0),"admin");
            var failures=new System.Collections.Concurrent.ConcurrentQueue<Exception>();server.ConnectionFailed+=failures.Enqueue;server.Start();
            var client=new ManagementClient("localhost",server.Port,cert);
            ServerInfo info;try{info=await client.InfoAsync();}catch(Exception error){throw new InvalidOperationException(string.Join("\n",failures.Select(item=>item.ToString())),error);}
            Assert.Equal(store.InstanceId,info.InstanceId);
            var localClient=ManagementClient.ForLocalPipe(server.PipeName!);Assert.Equal("admin",(await localClient.LocalIdentityAsync()).Name);Assert.Null(localClient.Token);Assert.Single((await localClient.CallAsync<CatalogPage<CatalogDatabase>>("catalogDatabases","",new{})).Items);
            await Assert.ThrowsAsync<ManagementException>(()=>client.CallAsync<Page<DefinitionSummary>>("definitions","",new{}));
            await client.LoginAsync("admin","TestPassword-123");
            Assert.Equal("Unsupported",(await Assert.ThrowsAsync<ManagementException>(()=>client.LocalIdentityAsync())).Code);
            Assert.Empty((await client.CallAsync<Page<DefinitionSummary>>("definitions","",new{})).Items);
            Assert.Contains("NativeCatalogV1",info.Capabilities);
            Assert.Contains("AuditSearchV1",info.Capabilities);
            Assert.Contains("AuditReadMethodsV1",info.Capabilities);
            Assert.Contains("AuditDeliveryV1",info.Capabilities);Assert.Empty((await client.AuditDeliveryStatusAsync()).Items);Assert.Empty((await localClient.AuditDeliveryStatusAsync()).Items);
            Assert.Contains("AuditReadBufferV1",info.Capabilities);Assert.Equal(collectReads,(await client.ReadAuditStatusAsync()).Enabled);Assert.Equal(collectReads,(await localClient.ReadAuditStatusAsync()).Enabled);
            var at=DateTimeOffset.UtcNow;store.AppendAudit(new(Guid.NewGuid(),null,at,at,store.InstanceId,"admin","Native","orders",AuditAction.Read,AuditOutcome.Succeeded,null,null,null,"Read",Namespace:"Company.Sales",TypeName:"OrderQueries",MethodName:"Save"));
            Assert.Single((await client.AuditEventsAsync(new(Text:"Company.Sales.OrderQueries.Save"))).Items);Assert.Empty((await client.ReadSummariesAsync(new())).Items);
            service.ChangeUser(service.Authenticate(client.Token),new("auditreader","ReaderPassword-123",[new("*",ManagementPermission.View|ManagementPermission.Execute)]));var readerClient=new ManagementClient("localhost",server.Port,cert);await readerClient.LoginAsync("auditreader","ReaderPassword-123");await Assert.ThrowsAsync<ManagementException>(()=>readerClient.AuditEventsAsync(new()));await Assert.ThrowsAsync<ManagementException>(()=>readerClient.AuditDeliveryStatusAsync());await Assert.ThrowsAsync<ManagementException>(()=>readerClient.ReadAuditStatusAsync());
            var catalog=await client.CallAsync<CatalogPage<CatalogDatabase>>("catalogDatabases","",new{});Assert.True(Assert.Single(catalog.Items).IsSystem);
            var system=await client.CallAsync<CatalogPage<CatalogObject>>("catalogObjects","",new CatalogRequest("system",catalog.VersionToken));Assert.Contains(system.Items,item=>item.Name=="Entries");
            await Assert.ThrowsAsync<ManagementException>(()=>new ManagementClient("localhost",server.Port,cert).LoginAsync("admin","WrongPassword-123"));
            await Assert.ThrowsAsync<AuthenticationException>(()=>new ManagementClient("localhost",server.Port).InfoAsync());
            await Assert.ThrowsAsync<AuthenticationException>(()=>new ManagementClient("127.0.0.1",server.Port,cert).InfoAsync());
            if(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_UI_TEST_EXE") is{} executable){
                var actor=service.Authenticate((await client.LoginAsync("admin","TestPassword-123")).Token);NativeWorkflow.Publish(service,actor,NativeWorkflow.Database());
                var query=new Definition("ui-query","Native",DefinitionKind.Query,"Model.Query","Run",new("1",[new("query.cs","""
                    using System.Linq;using Seamless.FLinq;using Seamless.Management.Server;
                    namespace Model;
                    public sealed class Query{
                        // preserved
                        [LinqIntercept(true)] public static int Run(ManagementExecutionContext context)=>context.GetDatabase<Db>().Rows.Where(row=>row.Value>0).Sum(row=>row.Value);
                    }
                    """)],[]));NativeWorkflow.Publish(service,actor,query);
                var rebase=query with{Id="ui-rebase-query"};
                NativeWorkflow.Publish(service,actor,rebase);
                var rebaseRevision=service.GetDraft(actor,rebase.Id).Revision;
                var updated=rebase with{Source=rebase.Source with{Files=[new("query.cs",rebase.Source.Files[0].Text+"\n// current published version\n")]}};
                NativeWorkflow.Publish(service,actor,updated,rebaseRevision,1);
                service.SaveDraft(actor,new(rebase with{Source=rebase.Source with{
                    Files=[new("query.cs",rebase.Source.Files[0].Text.Replace("row.Value>0","row.Value>2",StringComparison.Ordinal))]
                }},service.GetDraft(actor,rebase.Id).Revision,1));
                Assert.Contains("ProtectedSourceV1",info.Capabilities);Assert.Contains("DefinitionInfoV1",info.Capabilities);var safe=await readerClient.CallAsync<PublishedDefinitionInfo>("definitionInfo","Native",new{id="ui-query"});Assert.Null(safe.Method!.QueryTemplate);Assert.Equal("Run",safe.Definition.EntryMember);foreach(var operation in new[]{"definition","version","publishedAnalysis","plans"}){Assert.Equal("Permission",(await Assert.ThrowsAsync<ManagementException>(()=>readerClient.CallAsync<System.Text.Json.JsonElement>(operation,"Native",new{id="ui-query",version=1}))).Code);}Assert.Equal("Permission",(await Assert.ThrowsAsync<ManagementException>(()=>readerClient.CallAsync<CatalogObjectSource>("catalogSource","Native",new CatalogRequest("db:db",ObjectId:"Rows")))).Code);Assert.False((await readerClient.CallAsync<CatalogObjectDetails>("catalogDetails","Native",new CatalogRequest("db:db",ObjectId:"Rows"))).Source.Available);
                if(collectReads){NativeWorkflow.Publish(service,actor,query with{Id="ui-other"});_=service.Execute(actor,new("ui-query",null,ManagementJson.Element(new{})),CancellationToken.None).ToArray();_=service.Execute(actor,new("ui-other",null,ManagementJson.Element(new{})),CancellationToken.None).ToArray();Assert.True(service.ReadAuditStatus(actor).DroppedCount>0);}
                var auditTime=DateTimeOffset.UtcNow;for(var first=0;first<4303;first+=64){var begin=first;store.Write(()=>{for(var index=begin;index<Math.Min(begin+64,4303);index++){var message="paging fixture";var action=AuditAction.Read;var outcome=AuditOutcome.Succeeded;if(index<103){message="UI監査テスト 注文 失敗";action=AuditAction.DataChange;outcome=AuditOutcome.Failed;}store.AppendAuditInside(new(Guid.NewGuid(),null,auditTime,auditTime,store.InstanceId,"ui-audit","Native","orders",action,outcome,1,"ui-v1",null,"Test",message,"Company.UIAudit","Queries","Execute","Execute(int)","Rows.Where(row=>row.Id==@p0)"));}});}
                store.ApplyReadSummaryBatch(Guid.NewGuid(),[new(new(auditTime.AddMinutes(-1),auditTime,store.InstanceId,"ui-audit","Native","UI監査集計",service.GetDefinition(actor,"ui-query").ArtifactHash,1),42)]);
                var methodRow=Assert.Single((await client.ReadSummariesAsync(new(Text:"Model.Query.Run UI監査集計"))).Items);Assert.Equal("Model.Query.Run",methodRow.Method!.QualifiedName);Assert.NotNull(methodRow.Method.QueryTemplate);Assert.Single((await localClient.ReadSummariesAsync(new(Text:"Model.Query.Run UI監査集計"))).Items);
                service.ChangeUser(actor,new("summary-manager","SummaryPassword-123",[new("*",ManagementPermission.Manage)]));var summaryClient=new ManagementClient("localhost",server.Port,cert);await summaryClient.LoginAsync("summary-manager","SummaryPassword-123");Assert.Null(Assert.Single((await summaryClient.ReadSummariesAsync(new(Text:"Model.Query.Run UI監査集計"))).Items).Method!.QueryTemplate);Assert.Empty((await summaryClient.ReadSummariesAsync(new(Text:"Where UI監査集計"))).Items);Assert.Single((await client.ReadSummariesAsync(new(Text:"Where UI監査集計"))).Items);
                Seamless.Management.LocalServerConnection.Publish(path,store.InstanceId,server.Port,cert,server.PipeName);var publicCertificate=Path.Combine(path,Seamless.Management.LocalServerConnection.CertificateFileName);
                var config=Path.Combine(path,"ui-test.json");File.WriteAllText(config,System.Text.Json.JsonSerializer.Serialize(new{ホスト="localhost",ポート=server.Port,利用者="admin",証明書=publicCertificate,定義ID="ui-query",自動接続か=true},ManagementJson.Options));
                var outputDirectory=Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_LOG")!;if(collectReads){outputDirectory=Path.Combine(outputDirectory,"configured");}Directory.CreateDirectory(outputDirectory);var output=Path.Combine(outputDirectory,"ui-plan.png");
                async Task RunGui(string settings,string image,string password){
                    var start=new System.Diagnostics.ProcessStartInfo(executable){UseShellExecute=false,CreateNoWindow=true,WindowStyle=System.Diagnostics.ProcessWindowStyle.Hidden};start.ArgumentList.Add("--smoke");start.ArgumentList.Add(settings);start.ArgumentList.Add(image);start.Environment["SEAMLESS_MANAGEMENT_UI_TEST_PASSWORD"]=password;start.Environment["SEAMLESS_MANAGEMENT_HOME"]=Path.Combine(path,"ui-settings");start.Environment["SEAMLESS_MANAGEMENT_ROOT"]=path;
                    using var process=Process.Start(start)!;
                    var pid=process.Id;
                    var createdUtc=process.StartTime.ToUniversalTime();
                    var clock=Stopwatch.StartNew();
                    using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(60));
                    double? deadlineSeconds=null;
                    GuiProgress? last=null;
                    try{
                        await process.WaitForExitAsync(timeout.Token);
                    }catch(OperationCanceledException)when(timeout.IsCancellationRequested){
                        deadlineSeconds=clock.Elapsed.TotalSeconds;
                        if(!process.HasExited){process.Kill(entireProcessTree:true);}
                        await process.WaitForExitAsync();
                    }catch{
                        if(!process.HasExited){process.Kill(entireProcessTree:true);await process.WaitForExitAsync();}
                        throw;
                    }finally{
                        var progressPath=image+".progress.jsonl";
                        if(File.Exists(progressPath)){last=LastGuiProgress(File.ReadLines(progressPath),pid);}
                        File.WriteAllText(image+".process.json",JsonSerializer.Serialize(new{
                            Pid=pid,CreatedUtc=createdUtc,ElapsedSeconds=clock.Elapsed.TotalSeconds,DeadlineSeconds=deadlineSeconds,
                            ExitCode=process.HasExited?(int?)process.ExitCode:null,LastProgress=last
                        },ManagementJson.Options));
                    }
                    Assert.True(deadlineSeconds is null,$"GUI exceeded 60 seconds. Last stage: {last?.Stage??"(none)"}. Evidence: {image}.process.json");
                    var failure=image+".error.txt";var message="GUI exit="+process.ExitCode;if(File.Exists(failure)){message=File.ReadAllText(failure);}Assert.True(process.ExitCode==0,message);Assert.True(File.Exists(image+".result.json"));
                    Assert.Equal("完了",last?.Stage);
                }
                await RunGui(config,output,"TestPassword-123");
                var recovery=NativeWorkflow.Database() with{Id="ui-rebase-recovery",Database="Recovery"};
                NativeWorkflow.Publish(service,actor,recovery);
                var activeRecovery=recovery with{Source=recovery.Source with{
                    Files=[..recovery.Source.Files,new("PublishedChange.cs","// change published while the draft was open\n")]
                }};
                NativeWorkflow.Publish(service,actor,activeRecovery,service.GetDraft(actor,recovery.Id).Revision,1);
                service.SaveDraft(actor,new(recovery,service.GetDraft(actor,recovery.Id).Revision,1));
                var mergeConfig=Path.Combine(path,"ui-draft-merge-test.json");
                File.WriteAllText(mergeConfig,System.Text.Json.JsonSerializer.Serialize(new{ホスト="localhost",ポート=server.Port,利用者="admin",
                    証明書=publicCertificate,定義ID="ui-query",自動接続か=true,下書き統合だけか=true},ManagementJson.Options));
                await RunGui(mergeConfig,Path.Combine(outputDirectory,"ui-draft-merge.png"),"TestPassword-123");
                var tableConfig=Path.Combine(path,"ui-table-test.json");File.WriteAllText(tableConfig,System.Text.Json.JsonSerializer.Serialize(new{ホスト="localhost",ポート=server.Port,利用者="admin",証明書=publicCertificate,定義ID="ui-query",自動接続か=true,表編集だけか=true},ManagementJson.Options));await RunGui(tableConfig,Path.Combine(outputDirectory,"ui-table.png"),"TestPassword-123");
                var auditConfig=Path.Combine(path,"ui-audit-test.json");File.WriteAllText(auditConfig,System.Text.Json.JsonSerializer.Serialize(new{ホスト="localhost",ポート=server.Port,利用者="admin",証明書=publicCertificate,定義ID="ui-query",自動接続か=true,監査だけか=true},ManagementJson.Options));await RunGui(auditConfig,Path.Combine(outputDirectory,"ui-audit.png"),"TestPassword-123");
                var viewerConfig=Path.Combine(path,"ui-viewer-test.json");File.WriteAllText(viewerConfig,System.Text.Json.JsonSerializer.Serialize(new{ホスト="localhost",ポート=server.Port,利用者="auditreader",証明書=publicCertificate,定義ID="ui-query",ソース閲覧不可か=true},ManagementJson.Options));await RunGui(viewerConfig,Path.Combine(outputDirectory,"ui-viewer.png"),"ReaderPassword-123");
                var rowDefinition=new Definition("ui-rows","RowEditor",DefinitionKind.Database,"Relations.Db","",
                    new("1",[new("relations.cs",NativeRelations.Source)],[]));
                rowDefinition=Seamless.Management.DatabaseDesigner.Apply(rowDefinition,new("Nodes","Graph",[new("Id","int",true),new("Parent","int",false)],
                    [new("Self","Graph.Nodes",["Parent"],["Id"])]));
                NativeWorkflow.Publish(service,actor,rowDefinition);
                var rowConfig=Path.Combine(path,"ui-rows-test.json");
                File.WriteAllText(rowConfig,System.Text.Json.JsonSerializer.Serialize(new{ホスト="localhost",ポート=server.Port,利用者="admin",
                    証明書=publicCertificate,定義ID=rowDefinition.Id,自動接続か=true,行編集だけか=true},ManagementJson.Options));
                await RunGui(rowConfig,Path.Combine(outputDirectory,"ui-rows.png"),"TestPassword-123");
            }
            Assert.Contains("TableRowsV1",info.Capabilities);
            var administrator=service.Authenticate(client.Token);
            NativeWorkflow.Publish(service,administrator,NativeWorkflow.Database() with{Id="rpc-rows",Database="RpcRows"});
            var rowRequest=new TableRowsRequest("RpcRows","Rows");
            Assert.Equal("Permission",(await Assert.ThrowsAsync<ManagementException>(()=>readerClient.CallAsync<TableRows>("readRows","RpcRows",rowRequest))).Code);
            service.ChangeUser(administrator,new("auditreader",null,[new("RpcRows",ManagementPermission.View|ManagementPermission.ReadRows)]));
            Assert.Empty((await readerClient.CallAsync<TableRows>("readRows","RpcRows",rowRequest)).Rows);
            var rowBatch=new TableRowBatch("RpcRows",1,Guid.NewGuid(),[
                new("Rows",TableRowChangeKind.Insert,Values:ManagementJson.Element(new{Id=10,Value=11}))
            ]);
            Assert.Equal("Permission",(await Assert.ThrowsAsync<ManagementException>(()=>readerClient.CallAsync<TableRowCommit>("commitRows","RpcRows",rowBatch))).Code);
            Assert.Equal(rowBatch.OperationId,(await client.CallAsync<TableRowCommit>("commitRows","RpcRows",rowBatch)).OperationId);
            var row=Assert.Single((await localClient.CallAsync<TableRows>("readRows","RpcRows",rowRequest)).Rows);
            Assert.Equal(11,row.Values.GetProperty("value").GetInt32());
            Assert.Equal(1,(await localClient.CallAsync<TableRowCommit>("commitRows","RpcRows",
                rowBatch with{OperationId=Guid.NewGuid(),Changes=[new("Rows",TableRowChangeKind.Delete,row.Key,row.Stamp)]})).ChangedRows);
            Assert.Empty((await client.CallAsync<TableRows>("readRows","RpcRows",rowRequest)).Rows);
        }finally{
            cert.Dispose();
            if(OperatingSystem.IsWindows()&&keyName is not null&&provider is not null){
                if(CngKey.Exists(keyName,provider)){using var owned=CngKey.Open(keyName,provider);owned.Delete();}
                Assert.False(CngKey.Exists(keyName,provider));
            }
        }
    }

    [Fact]public void GuiProgressUsesTheOwnedProcessAndSurvivesAnIncompleteTail(){
        var last=LastGuiProgress([
            """{"時刻":"2026-11-06T12:00:00Z","工程":"開始","PID":12}""",
            """{"時刻":"2026-11-06T12:00:05Z","工程":"一括保存開始","PID":12}""",
            """{"時刻":"2026-11-06T12:00:10Z","工程":"完了","PID":13}""",
            """{"時刻":"2026-11-06T12:00:12Z","工程":"""
        ],12);
        Assert.Equal("一括保存開始",last?.Stage);
        Assert.Equal(new DateTimeOffset(2026,11,6,12,0,5,TimeSpan.Zero),last?.Utc);
        Assert.Null(LastGuiProgress(["""{"時刻":"2026-11-06T12:00:00Z","工程":"開始","PID":13}"""],12));
    }

    private static GuiProgress? LastGuiProgress(IEnumerable<string> lines,int pid){
        GuiProgress? last=null;
        foreach(var line in lines){
            try{
                using var entry=JsonDocument.Parse(line);
                var root=entry.RootElement;
                if(root.ValueKind==JsonValueKind.Object&&root.TryGetProperty("PID",out var process)
                    &&process.ValueKind==JsonValueKind.Number&&process.TryGetInt32(out var owner)&&owner==pid
                    &&root.TryGetProperty("時刻",out var time)&&time.ValueKind==JsonValueKind.String&&time.TryGetDateTimeOffset(out var utc)
                    &&root.TryGetProperty("工程",out var stage)&&stage.ValueKind==JsonValueKind.String&&stage.GetString() is{} text){
                    last=new(text,utc);
                }
            }catch(JsonException){
                // 子の強制終了と書き込みが重なると、末尾の1行は途中で終わる。
            }
        }
        return last;
    }
    private sealed record GuiProgress(string Stage,DateTimeOffset Utc);
}
