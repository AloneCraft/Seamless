using System.Text.Json;
using Seamless.Management;
using Seamless.Management.Server;

namespace Management.Tests;

public sealed class ManagementService{
    [Fact]public void PasswordChangeInvalidatesSessions(){
        using var store=ManagementStore.Initialize(Root(),"admin","TestPassword-123");using var service=new Seamless.Management.Server.ManagementService(store);
        var token=service.Login(new("admin","TestPassword-123")).Token;var admin=service.Authenticate(token);
        service.ChangePassword(admin,new("admin","Replacement-12345"));
        Assert.Throws<ManagementException>(()=>service.Authenticate(token));
        Assert.Throws<ManagementException>(()=>service.Login(new("admin","TestPassword-123")));
        Assert.Equal(admin.Grants,service.Authenticate(service.Login(new("admin","Replacement-12345")).Token).Grants);
    }
    private static string Root()=>Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")??throw new InvalidOperationException("SEAMLESS_MANAGEMENT_TEST_ROOTが必要です。"),Guid.NewGuid().ToString("N"));
    private static Definition Schema()=>new("database","Shop",DefinitionKind.Database,"Sample.Shop","",new("1",[new("schema.cs","""
        using Seamless.Database;
        namespace Sample{
        internal static partial class DatabaseGenerator;
        public sealed partial class Shop(DatabaseContainerOptions options):DatabaseContainer(options){
            public Table<Keys.Item,Tables.Item> Items=>this.Table<Keys.Item,Tables.Item>(row=>new(row.Id));
        }
        namespace Tables{public partial struct Item{[Key] public partial int Id{get;}public partial int Amount{get;}}}
        }
        """)],[]));
    [Fact]public void SaveDraft(){
        var path=Root();using(var store=ManagementStore.Initialize(path,"admin","TestPassword-123"))using(var service=new Seamless.Management.Server.ManagementService(store)){
            var login=service.Login(new("admin","TestPassword-123"));var user=service.Authenticate(login.Token);var draft=service.SaveDraft(user,new(Schema(),0,0));Assert.Equal(1,draft.Revision);Assert.Single(service.Drafts(user).Items);
            Assert.Throws<ManagementException>(()=>service.SaveDraft(user,new(Schema(),0,0)));
            service.ChangeUser(user,new("reader","ReaderPassword-123",[new("Shop",ManagementPermission.View|ManagementPermission.Execute)]));
            var reader=service.Authenticate(service.Login(new("reader","ReaderPassword-123")).Token);
            Assert.Empty(service.Drafts(reader).Items);
            Assert.Throws<ManagementException>(()=>service.SaveDraft(reader,new(Schema(),0,0)));
            Assert.Throws<ManagementException>(()=>ManagementStore.Initialize(path,"another","TestPassword-123"));
        }
        using(var store=new ManagementStore(path))using(var service=new Seamless.Management.Server.ManagementService(store)){
            var user=service.Authenticate(service.Login(new("admin","TestPassword-123")).Token);Assert.Equal(1,service.GetDraft(user,"database").Revision);Assert.NotEmpty(service.History(user).Items);
        }
    }
    [Fact]public void Apply(){
        var path=Root();using var store=ManagementStore.Initialize(path,"admin","TestPassword-123");using var service=new Seamless.Management.Server.ManagementService(store);var user=service.Authenticate(service.Login(new("admin","TestPassword-123")).Token);
        var schema=service.SaveDraft(user,new(Schema(),0,0));var preview=service.Preview(user,new("database",schema.Revision));Assert.True(preview.CanApply,string.Join("\n",preview.Diagnostics.Select(item=>item.Message)));
        var operation=service.Apply(user,new("database",schema.Revision,preview.Token,Guid.NewGuid()));Assert.Equal(OperationState.Succeeded,operation.State);
        var validation=service.CreateValidation(user,"database");service.Seed(user,new(validation.Id,"Items",[JsonSerializer.SerializeToElement(new{Id=1,Amount=7})]));
        var query=new Definition("query","Shop",DefinitionKind.Query,"Sample.Query","Run",new("q1",[new("query.cs","""
            using System.Linq;
            using Seamless.Management.Server;
            namespace Sample;
            public static class Query{public static int Run(ManagementExecutionContext context)=>context.GetDatabase<Shop>().Items.Sum(item=>item.Amount);}
            """)],[]));
        var draft=service.SaveDraft(user,new(query,0,0));var request=new ExecuteRequest("query",validation.Id,JsonSerializer.SerializeToElement(new{}),DraftRevision:draft.Revision);
        Assert.Equal(7,Assert.Single(service.Execute(user,request,CancellationToken.None)).Value.GetInt32());
        Assert.Throws<ManagementException>(()=>service.Execute(user,request with{ValidationDatabaseId=null},CancellationToken.None).ToArray());
        var qp=service.Preview(user,new("query",draft.Revision));Assert.True(qp.CanApply,string.Join("\n",qp.Diagnostics.Select(item=>item.Message)));
        var id=Guid.NewGuid();var applied=service.Apply(user,new("query",draft.Revision,qp.Token,id));Assert.Equal(OperationState.Succeeded,applied.State);
        Assert.Equal(applied,service.Apply(user,new("query",draft.Revision,qp.Token,id)));
        Assert.Equal(0,Assert.Single(service.Execute(user,new("query",null,JsonSerializer.SerializeToElement(new{})),CancellationToken.None)).Value.GetInt32());
    }
    [Fact]public void SaveDraftChecksPublishedVersion(){
        using var store=ManagementStore.Initialize(Root(),"admin","TestPassword-123");
        using var service=new Seamless.Management.Server.ManagementService(store);
        var user=service.Authenticate(service.Login(new("admin","TestPassword-123")).Token);
        var definition=Schema();
        var version=new DefinitionVersion(definition,3,user.Name,DateTimeOffset.UtcNow);
        store.Write(()=>store.Put("active",definition.Id,version));
        var original=service.SaveDraft(user,new(definition,0,2));
        Assert.Equal("Conflict",Assert.Throws<ManagementException>(()=>service.SaveDraft(user,new(definition,original.Revision,2,2))).Code);
        var unchanged=service.GetDraft(user,definition.Id);
        Assert.Equal(original.Revision,unchanged.Revision);
        Assert.Equal(original.Definition.Hash(),unchanged.Definition.Hash());
        var restored=service.SaveDraft(user,new(definition,original.Revision,3,3));
        Assert.Equal(3,restored.BaseVersion);
        Assert.Equal(original.Revision+1,restored.Revision);
        Assert.Equal(version.Version,service.GetDefinition(user,definition.Id).Version);
    }
    [Fact]public void StablePages(){
        using var store=ManagementStore.Initialize(Root(),"admin","TestPassword-123");store.Write(()=>{for(var index=0;index<205;index++){store.Put("fixture",index.ToString(),new UserInfo("user"+index,[],false));}});
        var first=store.List<UserInfo>("fixture");Assert.Equal(100,first.Items.Length);Assert.True(first.HasMore);store.Write(()=>{store.Put("fixture","0",new UserInfo("changed",[],false));store.Put("fixture","205",new UserInfo("user205",[],false));});
        var second=store.List<UserInfo>("fixture",first.NextCursor);var third=store.List<UserInfo>("fixture",second.NextCursor);Assert.Equal(100,second.Items.Length);Assert.Equal(6,third.Items.Length);Assert.False(third.HasMore);Assert.Equal(206,first.Items.Concat(second.Items).Concat(third.Items).Select(item=>item.Name).Distinct().Count());
    }
    [Fact]public void DefinitionListDoesNotReturnSource(){
        using var store=ManagementStore.Initialize(Root(),"admin","TestPassword-123");using var service=new Seamless.Management.Server.ManagementService(store);var user=service.Authenticate(service.Login(new("admin","TestPassword-123")).Token);var definition=Schema() with{Source=new("1",[new("large.cs",new string(' ',1<<20))],[])};
        store.Write(()=>store.Put("active",definition.Id,new DefinitionVersion(definition,1,user.Name,DateTimeOffset.UtcNow)));var page=service.Definitions(user);Assert.Single(page.Items);Assert.True(JsonSerializer.SerializeToUtf8Bytes(page,ManagementJson.Options).Length<2048);Assert.Equal(1<<20,service.GetDefinition(user,definition.Id).Definition.Source.Files[0].Text.Length);
    }
    [Fact]public void ChunkedValueSurvivesRollbackAndReopen(){
        var path=Root();var large=new string('あ',200000);using(var store=ManagementStore.Initialize(path,"admin","TestPassword-123")){
            store.Write(()=>store.Put("fixture","large",large));Assert.Equal(large,store.Read<string>("fixture","large"));
            Assert.Throws<InvalidOperationException>(()=>store.Write(()=>{store.Put("fixture","large","short");throw new InvalidOperationException("rollback");}));Assert.Equal(large,store.Read<string>("fixture","large"));
        }
        using(var store=new ManagementStore(path)){Assert.Equal(large,store.Read<string>("fixture","large"));store.Write(()=>store.Put("fixture","large","short"));Assert.Equal("short",store.Read<string>("fixture","large"));}
    }
}
