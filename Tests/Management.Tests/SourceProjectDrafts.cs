using Seamless.Management;
using Seamless.Management.Server;

namespace Management.Tests;

public sealed class SourceProjectDrafts{
    private static string Root()=>Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"project-draft-"+Guid.NewGuid().ToString("N"));
    [Theory][InlineData(false)][InlineData(true)]
    public async Task SourceChangesBecomeTheSameDatabaseDraftAndSurviveReopen(bool pipe){
        var root=Root();var storage=Path.Combine(root,"store");var projectPath=Path.Combine(root,"project");
        if(pipe){
            using var store=ManagementStore.Initialize(storage,"admin","TestPassword-123");
            using var service=new Seamless.Management.Server.ManagementService(store,null,startAuditWorker:false);
            await using var server=Seamless.Management.Server.ManagementServer.ForLocalOwner(service);server.Start();
            using var client=ManagementClient.ForLocalPipe(server.PipeName!);
            await Verify(client,client,projectPath);
        }else{
            using(var host=InProcessManagementHost.Create(storage)){await Verify(host,host,projectPath);}
            using var reopened=InProcessManagementHost.Open(storage);
            var document=await reopened.OpenSourceProjectAsync(new(Path.Combine(projectPath,"Sales.csproj")));
            var saved=await ((IManagementSourceOperations)reopened).SaveSourceProjectDraftAsync(document.Id);
            Assert.Equal(reopened.GetDraft("sales").Revision,saved.Revision);
        }
    }
    [Fact]public async Task ExistingProjectWithoutDraftAcknowledgementCanConfirmItsUnchangedDraft(){
        var root=Root();using var host=InProcessManagementHost.Create(Path.Combine(root,"store"));
        await Verify(host,host,Path.Combine(root,"project"),true);
    }
    private static async Task Verify(IManagementOperations operations,IManagementSourceOperations sources,string projectPath,bool legacy=false){
        var definition=Seamless.Management.DatabaseDesigner.Empty("sales","Sales");
        var draft=await operations.SaveDraftAsync(new(definition,0,0));
        var key=new DraftKey(definition.Id,draft.Revision);
        var validation=await operations.CreateValidationDraftAsync(key);
        try{
            var preview=await operations.PreviewAsync(key);
            Assert.True(preview.CanApply,string.Join("\n",preview.Diagnostics.Select(item=>item.Message)));
            Assert.Equal(OperationState.Succeeded,(await operations.ApplyAsync(new(definition.Id,draft.Revision,preview.Token,Guid.NewGuid()))).State);
        }finally{await operations.DropValidationAsync(validation.Id);}
        var published=await operations.GetDefinitionAsync(definition.Id);
        var designed=Seamless.Management.DatabaseDesigner.Apply(published.Definition,new("Items","",[new("Id","int",true),new("Name","string",false)],[]));
        var projectDraft=await operations.SaveDraftAsync(new(designed,draft.Revision,published.Version,published.Version));
        var stalePath=projectPath+"-stale";
        var stale=Change(projectDraft.Definition,text=>text+"\n// unacknowledged edit\n");
        Assert.Equal("Conflict",(await Assert.ThrowsAsync<ManagementException>(()=>sources.CreateSourceProjectAsync(new(stalePath,"Sales",new(stale,projectDraft.BaseVersion,projectDraft.Revision))))).Code);
        Assert.False(Directory.Exists(stalePath));
        SourceProjectDocument document;
        var creation=new SourceProjectCreateRequest(projectPath,"Sales",new(projectDraft.Definition,projectDraft.BaseVersion,projectDraft.Revision));
        if(legacy){
            using var filesystem=new SourceProjectService();
            document=await filesystem.CreateSourceProjectAsync(creation);
            Assert.Null(SourceWorkspace.Open(projectPath).State.SavedDraftHash);
            document=await sources.OpenSourceProjectAsync(new(document.ProjectPath));
        }else{document=await sources.CreateSourceProjectAsync(creation);}
        Assert.Equal(definition.Id,document.Database?.DefinitionId);
        var edited=document.Source! with{Files=document.Source!.Files.Select(file=>file with{Text=file.Text+"\n// project edit\n"}).ToArray()};
        await sources.SynchronizeSourceProjectAsync(new(document.Id,document.Source,edited));
        if(legacy){
            var conflicting=await operations.SaveDraftAsync(new(Change(projectDraft.Definition,text=>"// competing server edit\n"+text),projectDraft.Revision,projectDraft.BaseVersion,published.Version));
            Assert.Equal("Conflict",(await Assert.ThrowsAsync<ManagementException>(()=>sources.SaveSourceProjectDraftAsync(document.Id))).Code);
            Assert.Equal(conflicting.Definition.Hash(),(await operations.GetDraftAsync(definition.Id)).Definition.Hash());
            Assert.Contains("// project edit",File.ReadAllText(Path.Combine(projectPath,"Tables","Items.cs")));
            await operations.SaveDraftAsync(new(projectDraft.Definition,conflicting.Revision,projectDraft.BaseVersion,published.Version));
        }
        var result=await sources.SaveSourceProjectDraftAsync(document.Id);
        Assert.Equal(definition.Id,result.Definition.Id);
        Assert.Equal(published.Version,result.BaseVersion);
        Assert.Contains("// project edit",result.Definition.Source.Files[0].Text);
        Assert.Equal(result.Definition.Hash(),(await operations.GetDraftAsync(definition.Id)).Definition.Hash());
        Assert.Equal(published.Definition.Hash(),(await operations.GetDefinitionAsync(definition.Id)).Definition.Hash());
        Assert.Equal(result.Revision,(await sources.SaveSourceProjectDraftAsync(document.Id)).Revision);
        Assert.False(SourceWorkspace.Open(projectPath).NeedsDraftSave);
        if(legacy){return;}

        var server=await operations.GetDraftAsync(definition.Id);
        var serverDefinition=Change(server.Definition,text=>"// server edit\n"+text);
        await operations.SaveDraftAsync(new(serverDefinition,server.Revision,server.BaseVersion,published.Version));
        document=await Edit(sources,document.ProjectPath,text=>text+"\n// second file edit\n");
        result=await sources.SaveSourceProjectDraftAsync(document.Id);
        Assert.Contains("// server edit",result.Definition.Source.Files[0].Text);
        Assert.Contains("// second file edit",File.ReadAllText(Path.Combine(projectPath,"Database.cs")));

        server=await operations.GetDraftAsync(definition.Id);
        serverDefinition=Change(server.Definition,text=>text.Replace("// project edit","// server replacement",StringComparison.Ordinal));
        var competing=await operations.SaveDraftAsync(new(serverDefinition,server.Revision,server.BaseVersion,published.Version));
        document=await Edit(sources,document.ProjectPath,text=>text.Replace("// project edit","// file replacement",StringComparison.Ordinal));
        Assert.Equal("Conflict",(await Assert.ThrowsAsync<ManagementException>(()=>sources.SaveSourceProjectDraftAsync(document.Id))).Code);
        Assert.Equal(competing.Definition.Hash(),(await operations.GetDraftAsync(definition.Id)).Definition.Hash());
        Assert.Contains("// file replacement",File.ReadAllText(Path.Combine(projectPath,"Database.cs")));
        document=await Edit(sources,document.ProjectPath,text=>text.Replace("// file replacement","// server replacement",StringComparison.Ordinal));
        result=await sources.SaveSourceProjectDraftAsync(document.Id);
        Assert.Equal(competing.Revision,result.Revision);

        key=new(definition.Id,result.Revision);
        validation=await operations.CreateValidationDraftAsync(key);
        try{
            var preview=await operations.PreviewAsync(key);
            Assert.True(preview.CanApply,string.Join("\n",preview.Diagnostics.Select(item=>item.Message)));
            Assert.Equal(OperationState.Succeeded,(await operations.ApplyAsync(new(definition.Id,result.Revision,preview.Token,Guid.NewGuid()))).State);
        }finally{await operations.DropValidationAsync(validation.Id);}
        await File.AppendAllTextAsync(Path.Combine(projectPath,"Database.cs"),"\n// external after publication\n");
        result=await sources.SaveSourceProjectDraftAsync(document.Id);
        Assert.Equal(2,result.BaseVersion);
        document=await Edit(sources,document.ProjectPath,text=>text.Replace("// server replacement","// manager after publication",StringComparison.Ordinal));
        result=await sources.SaveSourceProjectDraftAsync(document.Id);
        var working=SourceWorkspace.Open(projectPath);
        working.UndoManager();
        var undone=File.ReadAllText(Path.Combine(projectPath,"Database.cs"));
        Assert.Contains("// server replacement",undone);
        Assert.Contains("// external after publication",undone);
        Assert.DoesNotContain("// manager after publication",undone);
        document=await sources.OpenSourceProjectAsync(new(document.ProjectPath));
        result=await sources.SaveSourceProjectDraftAsync(document.Id);
        var active=await operations.GetDefinitionAsync(definition.Id);
        document=await Edit(sources,document.ProjectPath,_=>"namespace {");
        result=await sources.SaveSourceProjectDraftAsync(document.Id);
        Assert.Equal("namespace {",result.Definition.Source.Files[0].Text);
        Assert.Equal(active.ArtifactHash,(await operations.GetDefinitionAsync(definition.Id)).ArtifactHash);
    }
    private static Definition Change(Definition definition,Func<string,string> change)=>definition with{
        Source=definition.Source with{Files=definition.Source.Files.Select(file=>file with{Text=change(file.Text)}).ToArray()}
    };
    private static async Task<SourceProjectDocument> Edit(IManagementSourceOperations sources,string path,Func<string,string> change){
        var document=await sources.OpenSourceProjectAsync(new(path));
        var source=document.Source! with{Files=document.Source!.Files.Select(file=>file with{Text=change(file.Text)}).ToArray()};
        return await sources.SynchronizeSourceProjectAsync(new(document.Id,document.Source,source));
    }
}
