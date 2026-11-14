using System.Text.Json;
using Seamless.Management;
using Seamless.Management.Server;
using Designer=Seamless.Management.DatabaseDesigner;

namespace Management.Tests;

public sealed class SourceObjectIdentities{
    [Theory][InlineData(false,"None")][InlineData(false,"Key")][InlineData(false,"Name")][InlineData(true,"None")][InlineData(true,"Key")][InlineData(true,"Name")]
    public void PositionalTableStructureChangesPreserveMarkersAndOriginalComments(bool assign,string change){
        var definition=new Definition("position","Position",DefinitionKind.Database,"Example.Db","",new("1",[new("Database.cs","""
            using Seamless.Database;
            namespace Example{
            public sealed class Db(DatabaseContainerOptions options):DatabaseContainer(options){public Table<Row> Items=>this.Table<Row>();}
            public readonly record struct Row(
                // identity argument
                [property:global::Seamless.Database.Key] int Id,
                // title argument
                string Title);
            }
            """)],[]));
        if(assign){definition=Designer.AssignStableIdentities(definition);}
        var ids=Designer.ReadStableIdentities(definition);var baseline=Designer.ReadObjectDefinition(definition,Assert.Single(Designer.Objects(definition)));
        var columns=baseline.Columns.Select(column=>change=="Key"?column with{PrimaryKey=column.Name=="Title"}:change=="Name"&&column.Name=="Title"?column with{Name="Label",Type="string?"}:column).ToArray();
        var result=Designer.ApplyObjectStructure(definition,baseline,columns,baseline.Parameters,baseline.ReturnType);
        Assert.True(result.Succeeded,string.Join("\n",result.Diagnostics.Select(item=>item.Message)));
        var updated=result.Definition!;var next=Designer.ReadStableIdentities(updated);
        Assert.Equal(ids.Object("Items"),next.Object("Items"));Assert.Equal(ids.Column("Items","Id"),next.Column("Items","Id"));
        Assert.Equal(ids.Column("Items","Title"),next.Column("Items",change=="Name"?"Label":"Title"));
        Assert.Contains("// identity argument",Text(updated));Assert.Contains("// title argument",Text(updated));
        if(assign){Assert.NotNull(next.Column("Items","Id"));}else{Assert.Empty(next.Entries);}
        var read=Designer.ReadObjectDefinition(updated,Assert.Single(Designer.Objects(updated)));
        Assert.Equal(change=="Key"?"Title":"Id",Assert.Single(read.Columns,column=>column.PrimaryKey).Name);
        using var compiled=new RuntimeCompiler().Compile(updated.Source);
        Assert.True(compiled.AssemblyBytes.Length>0,string.Join("\n",compiled.Result.Diagnostics.Select(item=>item.Code+": "+item.Message)));
    }
    [Fact]public void LegacyDefinitionsAndJsonKeepTheirExistingContract(){
        var definition=Generated();var hash=definition.Hash();
        Assert.Empty(Designer.ReadStableIdentities(definition).Entries);
        Assert.All(Designer.Objects(definition),item=>Assert.Null(item.StableId));
        Assert.Equal(hash,definition.Hash());
        var old="{\"id\":\"dbo.Items\",\"name\":\"Items\",\"schema\":\"dbo\",\"kind\":\"Table\",\"recordType\":\"Example.Tables.dbo.Items\",\"keyType\":null}";
        var item=JsonSerializer.Deserialize<CatalogObject>(old,ManagementJson.Options)!;
        Assert.Null(item.StableId);Assert.Equal(old,JsonSerializer.Serialize(item,ManagementJson.Options));
        Assert.Equal("{\"name\":\"dbo\"}",JsonSerializer.Serialize(new CatalogSchema("dbo"),ManagementJson.Options));
        Assert.Equal("{\"name\":\"Id\",\"type\":\"int\"}",JsonSerializer.Serialize(new CatalogField("Id","int"),ManagementJson.Options));
    }

    [Fact]public void ExplicitAssignmentKeepsCommentsAndUsesNewIdsForUnmarkedRecreation(){
        var original=Generated();var identified=Designer.AssignStableIdentities(original);var ids=Designer.ReadStableIdentities(identified);
        Assert.NotEmpty(ids.Entries);Assert.Equal(original.Hash(),Generated().Hash());
        Assert.Equal(identified,Designer.AssignStableIdentities(identified));
        Assert.Contains("// keep record comment",Text(identified));
        Assert.NotNull(ids.Schema("dbo"));Assert.NotNull(ids.Object("dbo.Items"));Assert.NotNull(ids.Column("dbo.Items","Title"));
        Assert.Contains(ids.Diagnostics,item=>item.Code=="IdentityDeclaration");
        var unrelated=identified with{Source=identified.Source with{Files=[..identified.Source.Files,new("Helper.cs","namespace Example{\n/* seamless-id:"+Guid.NewGuid().ToString("N")+" */ class Helper{}}") ]}};
        Assert.Contains(Designer.ReadStableIdentities(unrelated).Diagnostics,item=>item.Code=="IdentityUnbound");
        var markerless=identified with{Source=original.Source};
        Assert.Empty(Designer.ReadStableIdentities(markerless).Entries);
        var recreated=Designer.ReadStableIdentities(Designer.AssignStableIdentities(markerless));
        Assert.NotEqual(ids.Object("dbo.Items"),recreated.Object("dbo.Items"));
        Assert.NotEqual(ids.Column("dbo.Items","Title"),recreated.Column("dbo.Items","Title"));
    }

    [Fact]public void DeclaredNamespaceRecordAndParameterRenamesKeepTheirOwnIds(){
        var identified=Designer.AssignStableIdentities(Generated());var before=Designer.ReadStableIdentities(identified);
        var renamed=identified with{Source=identified.Source with{Files=identified.Source.Files.Select(file=>file with{Text=file.Text.Replace("Example.Tables.dbo","Example.Tables.sales",StringComparison.Ordinal).Replace("struct Items{","struct Products{",StringComparison.Ordinal).Replace("string Title","string Label",StringComparison.Ordinal)}).ToArray()}};
        var after=Designer.ReadStableIdentities(renamed);
        Assert.Equal(before.Schema("dbo"),after.Schema("sales"));
        Assert.Equal(before.Object("dbo.Items"),after.Object("sales.Products"));
        Assert.Equal(before.Column("dbo.Items","Title"),after.Column("sales.Products","Label"));
        Assert.Null(after.Object("dbo.Items"));Assert.Contains("// keep record comment",Text(renamed));
    }

    [Fact]public void CopiedOrMisplacedMarkersAreRejectedAndOrdinaryDocumentationIsAllowed(){
        var identified=Designer.AssignStableIdentities(Generated());var row=identified.Source.Files.Single(file=>file.Path=="Items.cs");
        var copied=identified.Source with{Files=[..identified.Source.Files,new("Copy.cs",row.Text.Replace("struct Items{","struct Copied{",StringComparison.Ordinal))]};
        Assert.Equal("Conflict",Assert.Throws<ManagementException>(()=>Seamless.Management.SourceEditor.Validate(copied)).Code);
        var id=Guid.NewGuid().ToString("N");
        foreach(var text in new[]{"/* seamless-id:bad */ class C{}","/* seamless-id:"+id+" */ using System; class C{}","/* seamless-id:"+id+" */ /* seamless-id:"+Guid.NewGuid().ToString("N")+" */ class C{}"}){
            Assert.Throws<ManagementException>(()=>Seamless.Management.SourceEditor.Validate(new("1",[new("Bad.cs",text)],[])));
        }
        Seamless.Management.SourceEditor.Validate(new("1",[new("Documentation.cs","// a seamless-id: marker is explicit metadata\nclass C{string Text=\"seamless-id:literal\";}")],[]));
    }

    [Fact]public void PositionalViewColumnsKeepTheDeclarationMarkerThroughRenames(){
        var definition=Generated();
        definition=definition with{Source=definition.Source with{Files=[..definition.Source.Files,new("View.cs","namespace Example.Views{public readonly record struct ItemList(int Id,string Title);}namespace Example{public static class DbQueries{extension(Db.IReader q){public global::Seamless.Collections.ISet<Views.ItemList> ItemList=>throw new System.NotImplementedException();}}}")]}};
        var identified=Designer.AssignStableIdentities(definition);var before=Designer.ReadStableIdentities(identified);
        var updated=identified with{Source=identified.Source with{Files=identified.Source.Files.Select(file=>file.Path=="View.cs"?file with{Text=file.Text.Replace("string Title","string Label",StringComparison.Ordinal)}:file).ToArray()}};
        Assert.NotNull(before.Column("ItemList","Title"));
        Assert.Equal(before.Column("ItemList","Title"),Designer.ReadStableIdentities(updated).Column("ItemList","Label"));
    }

    [Fact]public void HandwrittenMembersPreserveTheirOwnIdsDuringStructureEditing(){
        var definition=Legacy();var identified=Designer.AssignStableIdentities(definition);var ids=Designer.ReadStableIdentities(identified);
        Assert.NotNull(ids.Schema("sales"));Assert.NotNull(ids.Schema("Empty"));
        Assert.NotNull(ids.Column("Items","Name"));
        var baseline=Designer.ReadObjectDefinition(identified,Designer.Objects(identified).Single(item=>item.Id=="Items"));
        var changed=Designer.ApplyObjectStructure(identified,baseline,baseline.Columns.Select(column=>column.Name=="Name"?column with{Name="Label"}:column).ToArray(),baseline.Parameters,baseline.ReturnType);
        Assert.True(changed.Succeeded);var next=Designer.ReadStableIdentities(changed.Definition!);
        Assert.Equal(ids.Object("Items"),next.Object("Items"));Assert.Equal(ids.Column("Items","Name"),next.Column("Items","Label"));
        Assert.Contains("// kept field comment",Text(changed.Definition!));
    }

    [Fact]public void SharedDeclarationsNeverBecomeTwoObjectsWithTheSameIdentity(){
        var definition=Legacy();
        definition=definition with{Source=definition.Source with{Files=definition.Source.Files.Select(file=>file with{Text=file.Text.Replace("public Table<Key,Row> Items=>","public Table<Key,Row> Other=>this.Table<Key,Row>(row=>new(row.Id));public Table<Key,Row> Items=>",StringComparison.Ordinal)}).ToArray()}};
        Assert.Equal("Unsupported",Assert.Throws<ManagementException>(()=>Designer.AssignStableIdentities(definition)).Code);
        var id=Guid.NewGuid().ToString("N");
        var annotated=definition with{Source=definition.Source with{Files=definition.Source.Files.Select(file=>file with{Text=file.Text.Replace("public string Name", "/* seamless-id:"+id+" */ public string Name",StringComparison.Ordinal)}).ToArray()}};
        var read=Designer.ReadStableIdentities(annotated);
        Assert.Null(read.Column("Items","Name"));Assert.Null(read.Column("Other","Name"));
        Assert.Equal("AmbiguousDeclaration",read.UnavailableReason("Column","Items","Name"));Assert.NotEmpty(read.Diagnostics);
    }

    [Fact]public void GeneratedAssemblyCatalogKeepsTheSourceIdsWithoutInvokingUserMembers(){
        var definition=Designer.AssignStableIdentities(Generated());var ids=Designer.ReadStableIdentities(definition);
        using var compiled=new RuntimeCompiler().Compile(definition.Source);
        Assert.True(compiled.AssemblyBytes.Length>0,string.Join("\n",compiled.Result.Diagnostics.Select(item=>item.Code+": "+item.Message)));Assert.DoesNotContain(compiled.Result.Diagnostics,item=>item.Severity=="Error");
        var context=new ManagedLoadContext();
        try{
            using var stream=new MemoryStream(compiled.AssemblyBytes);var type=context.LoadFromStream(stream).GetType(definition.EntryType,true)!;
            var version=new DefinitionVersion(definition,1,"owner",DateTimeOffset.UtcNow,compiled.Result.ArtifactHash);
            var actual=Assert.Single(Seamless.Management.Server.NativeCatalog.Read(type,version),item=>item.Details.Object.Id=="dbo.Items");
            Assert.Equal("dbo.Items",actual.Details.Object.Id);Assert.Equal(ids.Object("dbo.Items"),actual.Details.Object.StableId);
            Assert.Equal(ids.Column("dbo.Items","Title"),Assert.Single(actual.Details.Columns,column=>column.Name=="Title").StableId);
        }finally{context.Unload();}
    }

    [Fact]public async Task ExplicitPublicationKeepsIdsThroughOwnerAndAuthenticatedAdaptersAndIpc(){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,Guid.NewGuid().ToString("N"));
        var definition=Designer.AssignStableIdentities(Generated());var ids=Designer.ReadStableIdentities(definition);
        using var store=ManagementStore.Initialize(root,"admin","TestPassword-123");
        using var service=new Seamless.Management.Server.ManagementService(store,null,startAuditWorker:false);
        var user=service.Authenticate(service.Login(new("admin","TestPassword-123")).Token);
        var draft=service.SaveDraft(user,new(definition,0,0));var preview=service.Preview(user,new(definition.Id,draft.Revision));
        Assert.True(preview.CanApply,string.Join("\n",preview.Diagnostics.Select(item=>item.Message)));
        Assert.Equal(OperationState.Succeeded,service.Apply(user,new(definition.Id,draft.Revision,preview.Token,Guid.NewGuid())).State);
        await using var concurrent=new ConcurrentManagementOperations(service);
        using var ownerSessions=new LocalBrowserSessions(store.InstanceId);ownerSessions.BindOrigin(new("http://127.0.0.1:25001"));
        var ownerSession=ownerSessions.Redeem(new Uri(ownerSessions.CreateLaunch().Url).Fragment[1..],"http://127.0.0.1:25001");
        var browser=new BrowserManagementOperations(ownerSessions,concurrent,()=>Task.FromResult<string?>(ownerSession.Id));
        service.ChangeUser(user,new("viewer","ViewerPassword-123",[new(definition.Database,ManagementPermission.View)]));
        using var externalSessions=new UserBrowserSessions(service);var externalSession=externalSessions.SignIn(new("viewer","ViewerPassword-123"));
        var authenticated=new AuthenticatedManagementOperations(concurrent,externalSessions,()=>Task.FromResult<string?>(externalSession.Id));
        await using var server=Seamless.Management.Server.ManagementServer.ForLocalOwner(service);server.Start();
        using var client=ManagementClient.ForLocalPipe(server.PipeName!);
        foreach(IManagementOperations operations in new IManagementOperations[]{concurrent,browser,authenticated,client}){await Check(operations);}
        using var embedded=InProcessManagementHost.Create(root+"-embedded");
        var embeddedDraft=await embedded.SaveDraftAsync(new(definition,0,0));var embeddedPreview=await embedded.PreviewAsync(new(definition.Id,embeddedDraft.Revision));
        Assert.True(embeddedPreview.CanApply);Assert.Equal(OperationState.Succeeded,(await embedded.ApplyAsync(new(definition.Id,embeddedDraft.Revision,embeddedPreview.Token,Guid.NewGuid()))).State);
        await Check(embedded);

        async Task Check(IManagementOperations operations){
            var databases=await operations.DatabasesAsync();var database=Assert.Single(databases.Items,item=>!item.IsSystem);
            var request=new CatalogRequest(database.Id,databases.VersionToken);
            var schema=Assert.Single((await operations.SchemasAsync(request)).Items,item=>item.Name=="dbo");
            Assert.Equal(ids.Schema("dbo"),schema.StableId);
            var table=Assert.Single((await operations.ObjectsAsync(request)).Items,item=>item.Id=="dbo.Items");
            Assert.Equal(ids.Object(table.Id),table.StableId);
            var details=await operations.GetObjectDetailsAsync(request with{ObjectId=table.Id});
            Assert.Equal(ids.Column(table.Id,"Title"),Assert.Single(details.Columns,item=>item.Name=="Title").StableId);
            Assert.False(details.Source.Available);Assert.Null(details.Source.Path);
            var json=JsonSerializer.Serialize(details,ManagementJson.Options);
            var roundtrip=JsonSerializer.Deserialize<CatalogObjectDetails>(json,ManagementJson.Options)!;
            Assert.Equal(details.Object,roundtrip.Object);Assert.Equal(details.Columns,roundtrip.Columns);
        }
    }

    private static Definition Generated()=>new("identities","IdentityDb",DefinitionKind.Database,"Example.Db","",new("1",[
        new("Database.cs","using Seamless.Database;namespace Example{internal static partial class DatabaseGenerator;public sealed partial class Db(DatabaseContainerOptions options):DatabaseContainer(options){}}"),
        new("Items.cs","// keep namespace comment\nnamespace Example.Tables.dbo{\n// keep record comment\npublic partial struct Items{[global::Seamless.Database.Key] public partial int Id{get;}public partial string Title{get;}}}\n")],[]));
    private static Definition Legacy()=>new("manual","Manual",DefinitionKind.Database,"Example.Db","",new("1",[new("Database.cs","""
        using Seamless.Database;
        namespace Example{
        public sealed partial class Db(DatabaseContainerOptions options):DatabaseContainer(options){
            public Schema Sales=>throw new System.InvalidOperationException();
            public Schema Empty=>throw new System.InvalidOperationException();
            public Schema sales=>throw new System.InvalidOperationException();
            public Table<Key,Row> Items=>this.Table<Key,Row>(row=>new(row.Id));
        }
        public sealed class Schema(DatabaseContainer db):DatabaseSchema(db,"sales"){
        }
        public readonly record struct Key(int Id);
        public partial record struct Row{
            public int Id{get;init;}
            // kept field comment
            public string Name{get;init;}
        }
        }
        """)],[]));
    private static string Text(Definition definition)=>string.Join("\n",definition.Source.Files.Select(file=>file.Text));
}
