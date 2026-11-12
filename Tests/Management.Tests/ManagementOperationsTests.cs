using Seamless.Management;
using Seamless.Management.Server;

namespace Management.Tests;

public sealed class ManagementOperationsTests{
    private static string Root()=>Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,Guid.NewGuid().ToString("N"));

    [Fact]public async Task DirectOperationsKeepDraftValidationAndPublicationSeparate(){
        using var host=InProcessManagementHost.Create(Root());
        await VerifyCreation(host);
        Assert.Equal("Validation",Assert.Throws<ManagementException>(()=>host.SaveDraft(new(Seamless.Management.DatabaseDesigner.Empty("reserved","system"),0,0))).Code);
    }

    [Fact]public async Task OwnerPipeUsesTheSameCreationContract(){
        using var store=ManagementStore.Initialize(Root(),"admin","TestPassword-123");
        using var service=new Seamless.Management.Server.ManagementService(store);
        await using var server=Seamless.Management.Server.ManagementServer.ForLocalOwner(service);
        server.Start();
        using var client=ManagementClient.ForLocalPipe(server.PipeName!);
        await VerifyCreation(client);
    }

    [Fact]public async Task AQueuedSaveFreezesTheCallersMutableSource(){
        using var host=InProcessManagementHost.Create(Root());
        using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
        var running=host.InvokeAsync((_,_)=>{entered.Set();Assert.True(release.Wait(TimeSpan.FromSeconds(15)));return 1;}).AsTask();
        Assert.True(entered.Wait(TimeSpan.FromSeconds(15)));
        var definition=Seamless.Management.DatabaseDesigner.Empty("frozen","FrozenDb");
        var expected=definition.Hash();
        var saving=host.SaveDraftAsync(new(definition,0,0));
        definition.Source.Files[0]=new("changed.cs","invalid input after enqueue");
        release.Set();
        Assert.Equal(1,await running);
        var saved=await saving;
        Assert.Equal(expected,saved.Definition.Hash());
        saved.Definition.Source.Files[0]=new("changed-return.cs","invalid input after return");
        Assert.Equal(expected,host.GetDraft("frozen").Definition.Hash());
    }

    [Fact]public Task TlsKeepsAuthenticationAndWriteAuthorization()=>ManagementTransport.WithServerCertificate(async certificate=>{
        using var store=ManagementStore.Initialize(Root(),"admin","TestPassword-123");
        using var service=new Seamless.Management.Server.ManagementService(store);
        var admin=service.Authenticate(service.Login(new("admin","TestPassword-123")).Token);
        service.ChangeUser(admin,new("viewer","ViewerPassword-123",[new("CommonDb",ManagementPermission.View)]));
        await using var server=new Seamless.Management.Server.ManagementServer(service,certificate,new(System.Net.IPAddress.Loopback,0));
        server.Start();
        using var client=new ManagementClient("localhost",server.Port,certificate);
        IManagementOperations operations=client;
        var request=new SaveDraftRequest(Seamless.Management.DatabaseDesigner.Empty("protected","CommonDb"),0,0);
        Assert.Equal("Authentication",(await Assert.ThrowsAsync<ManagementException>(()=>operations.FindDraftAsync(request.Definition.Id))).Code);
        Assert.Equal("Authentication",(await Assert.ThrowsAsync<ManagementException>(()=>operations.SaveDraftAsync(request))).Code);
        await client.LoginAsync("viewer","ViewerPassword-123");
        Assert.Equal("Permission",(await Assert.ThrowsAsync<ManagementException>(()=>operations.SaveDraftAsync(request))).Code);
        await client.LoginAsync("admin","TestPassword-123");
        Assert.Equal(1,(await operations.SaveDraftAsync(request)).Revision);
        await client.LoginAsync("viewer","ViewerPassword-123");
        Assert.Null(await operations.FindDraftAsync(request.Definition.Id));
    });

    private static async Task VerifyCreation(IManagementOperations operations){
        var definition=Seamless.Management.DatabaseDesigner.Empty("common-create","CommonDb");
        Assert.Null(await operations.FindDraftAsync(definition.Id));
        var draft=await operations.SaveDraftAsync(new(definition,0,0));
        Assert.Equal(1,draft.Revision);
        Assert.Equal(definition.Hash(),(await operations.GetDraftAsync(definition.Id)).Definition.Hash());
        Assert.Equal(definition.Hash(),(await operations.FindDraftAsync(definition.Id))!.Definition.Hash());
        Assert.Null(await ((IManagementSourceOperations)operations).FindImportedSourceBuildAsync(new(definition.Id,draft.Revision)));
        var key=new DraftKey(definition.Id,draft.Revision);
        var build=await operations.BuildAsync(key);
        Assert.DoesNotContain(build.Diagnostics,item=>item.Severity=="Error");
        Assert.DoesNotContain((await operations.DatabasesAsync()).Items,item=>item.Name==definition.Database);
        var validation=await operations.CreateValidationDraftAsync(key);
        Assert.True(validation.FromDraft);
        Assert.Equal(definition.Source.Hash(),validation.SourceHash);
        var preview=await operations.PreviewAsync(key);
        Assert.True(preview.CanApply,string.Join("\n",preview.Diagnostics.Select(item=>item.Message)));
        Assert.True(preview.Validation?.Passed);
        Assert.DoesNotContain((await operations.DatabasesAsync()).Items,item=>item.Name==definition.Database);
        var apply=new ApplyRequest(definition.Id,draft.Revision,preview.Token,Guid.NewGuid());
        var result=await operations.ApplyAsync(apply);
        Assert.Equal(OperationState.Succeeded,result.State);
        Assert.Equal(1,result.PublishedVersion);
        Assert.Equal(result,await operations.ApplyAsync(apply));
        Assert.Equal(result,await operations.GetOperationAsync(apply.OperationId));
        Assert.Equal(definition.Hash(),(await operations.GetDefinitionAsync(definition.Id)).Definition.Hash());
        var published=Assert.Single((await operations.DatabasesAsync()).Items,item=>item.Name==definition.Database);
        Assert.Empty((await operations.ObjectsAsync(new(published.Id,IncludeSystemObjects:false))).Items);
        await operations.DropValidationAsync(validation.Id);
    }
}
