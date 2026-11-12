using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Seamless.Management;
using Seamless.Management.Components;
using Designer=Seamless.Management.DatabaseDesigner;
using Document=Seamless.Management.Components.文書状態;

namespace Management.Tests;

public sealed class BrowserDocumentRestoration{
    private static readonly string Hash=new('A',64);
    private static Definition Definition(string id){var value=Designer.AddSchema(Designer.Apply(Designer.Empty(id,id),new("Items","",[new("Id","int",true)],[])),"dbo0");return value with{Source=value.Source with{Revision="fixture"},Migration=null};}
    private static CatalogDatabase DB(string id)=>new("db:"+id,id,id,1,Hash,false);
    private static ブラウザー保存文書 Reference(string id,string kind="Table",string objectId="Items",string schema="",string view="構造")=>new("db:"+id,1,Hash,id,1,Hash,Definition(id).Hash(),kind,objectId,schema,view);
    private static ブラウザー表示状態 State(ブラウザー保存文書[] documents,int? selected)=>new(4,[],false,0){SidebarWidth=254,SidebarCollapsed=false,DatabaseOrder=[],Documents=documents,SelectedDocument=selected};
    [Fact]public void FormatFourRequiresBoundedExactMetadataAndKeepsAllEarlierFormatsReadable(){
        var value=State([Reference("a")],0);value.検証();var restored=JsonSerializer.Deserialize<ブラウザー表示状態>(JsonSerializer.Serialize(value))!;restored.検証();Assert.Equal(value.Documents,restored.Documents);Assert.Equal(0,restored.SelectedDocument);
        foreach(var invalid in new[]{value with{Format=3},value with{Documents=null},value with{SelectedDocument=1},value with{Documents=[Reference("a"),Reference("a")]},value with{Documents=Enumerable.Repeat(Reference("a"),33).ToArray()}}){Assert.Throws<InvalidOperationException>(invalid.検証);}
        foreach(var invalid in new[]{Reference("a") with{Version=9007199254740992},Reference("a") with{ArtifactHash="unknown"},Reference("a") with{ObjectId=""},Reference("a") with{View="実行計画"}}){Assert.Throws<InvalidOperationException>(invalid.検証);}
        new ブラウザー表示状態(1,[],false,0).検証();new ブラウザー表示状態(2,[],false,0){SidebarWidth=254,SidebarCollapsed=false}.検証();new ブラウザー表示状態(3,[],false,0){SidebarWidth=254,SidebarCollapsed=false,DatabaseOrder=[]}.検証();
    }
    [Fact]public Task ExactReferencesRestoreOrderedDocumentsAndSelectionWithoutPersistingSourceOrData()=>WithWorkspace(async workspace=>{
        Cache(workspace,"a");Prepare(workspace,[Reference("a","Database","","","DB情報"),Reference("a"),Reference("a","Schema","","dbo0","DB情報")],1);
        Assert.True(await Restore(workspace));Assert.Empty(Get<HashSet<string>>(workspace,"文書復元案内"));var tabs=Get<文書タブ順序>(workspace,"タブ順序").順序;var documents=Get<Dictionary<string,対象文書状態>>(workspace,"対象文書一覧");
        Assert.Equal(new[]{"","Items","dbo0"},tabs.Select(id=>documents[id].名前));Assert.Equal(tabs[1],Get<string>(workspace,"選択作業文書ID"));Assert.Equal("構造",documents[tabs[1]].表示);
        var saved=((ブラウザー保存文書[] 文書,int? 選択))Invoke(workspace,"保存文書を取得")!;Assert.Equal(3,saved.文書.Length);Assert.Equal(1,saved.選択);
        var json=JsonSerializer.Serialize(State(saved.文書,saved.選択));Assert.DoesNotContain("Database.cs",json);Assert.DoesNotContain("public partial",json);Assert.DoesNotContain("Arguments",json);Assert.DoesNotContain("Source",json);Assert.DoesNotContain("ExecutionId",json);
    });
    [Theory][InlineData("version")][InlineData("artifact")][InlineData("definition")][InlineData("missing")][InlineData("permission")]
    public Task ChangedOrMissingReferencesAndPermissionLossNeverFallBackToMatchingNames(string change)=>WithWorkspace(async workspace=>{
        Cache(workspace,"a");var saved=Reference("a");
        if(change=="version"){saved=saved with{DatabaseVersion=2};}if(change=="artifact"){saved=saved with{ArtifactHash=new('B',64)};}if(change=="definition"){saved=saved with{DefinitionHash=new('C',64)};}if(change=="missing"){saved=saved with{ObjectId="RenamedItems"};}if(change=="permission"){SetUser(workspace,new("viewer",[new("*",ManagementPermission.View)],false));}
        Prepare(workspace,[saved],0);Assert.True(await Restore(workspace));Assert.Empty(Get<文書タブ順序>(workspace,"タブ順序").順序);Assert.NotEmpty(Get<HashSet<string>>(workspace,"文書復元案内"));
    });
    [Fact]public Task UnreadPagesRemainPendingAndEmptyDocumentStateClosesOnlyTheInitialTabs()=>WithWorkspace(async workspace=>{
        Cache(workspace,"a");Set(workspace,"次ページがあるか",true);Prepare(workspace,[Reference("b")],0);Assert.False(await Restore(workspace));Assert.Null(Invoke(workspace,"保存文書を取得"));
        Cache(workspace,"b");Set(workspace,"次ページがあるか",false);Assert.True(await Restore(workspace));var selected=Get<string>(workspace,"選択作業文書ID");Assert.Equal("db:b",Get<Dictionary<string,対象文書状態>>(workspace,"対象文書一覧")[selected].DBID);
        Prepare(workspace,[],null);Assert.True(await Restore(workspace));Assert.Empty(Get<文書タブ順序>(workspace,"タブ順序").順序);Assert.Null(Get<string?>(workspace,"選択作業文書ID"));
    });
    [Fact]public Task UnsavedCodeRowsAndDefinitionChangesAreExcludedAndNeverCopiedIntoStorage()=>WithWorkspace(workspace=>{
        Cache(workspace,"a");Invoke(workspace,"文書を選ぶ","db:a");var target=(対象文書状態)Invoke(workspace,"対象文書を取得","db:a",Designer.Objects(Definition("a")).Single(item=>item.Id=="Items"),null!,null!)!;
        Get<文書タブ順序>(workspace,"タブ順序").追加(target.ID);target.編集中か=true;
        Assert.Empty((((ブラウザー保存文書[] 文書,int? 選択))Invoke(workspace,"保存文書を取得")!).文書);Assert.NotNull(Get<string>(workspace,"文書保存診断"));
        target.編集中か=false;target.データ編集中か=true;Assert.Empty((((ブラウザー保存文書[] 文書,int? 選択))Invoke(workspace,"保存文書を取得")!).文書);target.データ編集中か=false;
        var definition=Get<Dictionary<string,Document>>(workspace,"文書一覧")["db:a"];definition.定義=definition.定義 with{WritesData=true};Assert.Empty((((ブラウザー保存文書[] 文書,int? 選択))Invoke(workspace,"保存文書を取得")!).文書);return Task.CompletedTask;
    });
    [Theory][InlineData(false)][InlineData(true)]public Task SlowResponsesCannotReplaceNewSelectionOrUseRevokedPermission(bool revoke)=>WithWorkspace(async workspace=>{
        Cache(workspace,"a");Set(workspace,"DB一覧",new[]{DB("a"),DB("b")});var operations=(RestoreOperations)Get<IManagementOperations>(workspace,"管理操作");operations.Delayed=new(TaskCreationOptions.RunContinuationsAsynchronously);
        Prepare(workspace,[Reference("b")],0);var restore=Restore(workspace);Assert.False(restore.IsCompleted);
        if(revoke){SetUser(workspace,new("owner",[],false));}else{Invoke(workspace,"文書を選ぶ","db:a");}
        operations.Delayed.SetResult(new(Definition("b"),1,"owner",DateTimeOffset.UtcNow,Hash));await restore;
        Assert.DoesNotContain(Get<Dictionary<string,対象文書状態>>(workspace,"対象文書一覧").Values,item=>item.DBID=="db:b");Assert.NotEmpty(Get<HashSet<string>>(workspace,"文書復元案内"));
        if(!revoke){Assert.Equal("db:a",Get<string>(workspace,"選択DBID"));}
    });
    [Fact]public Task RegisteredFunctionsUseTheirOwnDefinitionVersionAndArtifactWithinTheParentDatabase()=>WithWorkspace(async workspace=>{
        Cache(workspace,"a");var definition=new Definition("registered-query","a",DefinitionKind.Query,"Functions","Run",new("fixture",[new("Function.cs","public static class Functions { public static int Run()=>3; }")],[]));var hash=new string('C',64);
        var function=new FunctionSummary(new(FunctionOrigin.RegisteredEntry,"db:a",definition.Id,definition.Id),"Registered","",3,hash,false);Get<Dictionary<string,FunctionSummary[]>>(workspace,"登録関数一覧")["db:a"]=[function];
        Get<Dictionary<string,Document>>(workspace,"関数定義文書")[definition.Id]=new(DB("a"),new(definition,3,"owner",DateTimeOffset.UtcNow,hash),null);
        var reference=new ブラウザー保存文書("db:a",1,Hash,definition.Id,3,hash,definition.Hash(),"RegisteredFunction","registered:"+definition.Id,"","構造");Prepare(workspace,[reference],0);Assert.True(await Restore(workspace));Assert.Empty(Get<HashSet<string>>(workspace,"文書復元案内"));
        var selected=Get<Dictionary<string,対象文書状態>>(workspace,"対象文書一覧")[Get<string>(workspace,"選択作業文書ID")];Assert.True(selected.登録関数か);Assert.Equal(definition.Id,selected.定義ID);
        var saved=((ブラウザー保存文書[] 文書,int? 選択))Invoke(workspace,"保存文書を取得")!;Assert.Equal(reference,Assert.Single(saved.文書));
    });
    [Fact]public Task SeparateDatabaseViewAndWildcardEditGrantsRestoreTheSameSourceAuthorizedDocument()=>WithWorkspace(async workspace=>{
        Cache(workspace,"a");SetUser(workspace,new("owner",[new("a",ManagementPermission.View),new("*",ManagementPermission.Edit)],false));
        var reference=Reference("a");Prepare(workspace,[reference],0);Assert.True(await Restore(workspace));Assert.Empty(Get<HashSet<string>>(workspace,"文書復元案内"));
        var selected=Get<Dictionary<string,対象文書状態>>(workspace,"対象文書一覧")[Get<string>(workspace,"選択作業文書ID")];Assert.Equal("Items",selected.対象!.Id);
        var saved=((ブラウザー保存文書[] 文書,int? 選択))Invoke(workspace,"保存文書を取得")!;Assert.Equal(reference,Assert.Single(saved.文書));Assert.Equal(0,saved.選択);
    });
    [Theory][InlineData(false)][InlineData(true)]public Task PublishedReferencesRestoreForViewOnlyAndRejectChangedArtifacts(bool changed)=>WithWorkspace(async workspace=>{
        Cache(workspace,"a");SetUser(workspace,new("owner",[new("a",ManagementPermission.View)],false));
        Get<Dictionary<string,公開DB表示状態>>(workspace,"公開DB一覧")["db:a"]=new(DB("a"),"v",["dbo0"],Designer.Objects(Definition("a")));
        var reference=Reference("a") with{PublishedOnly=true,DefinitionHash="",View="データ"};if(changed){reference=reference with{ArtifactHash=new('B',64)};}
        var state=State([reference],0) with{Format=5};state.検証();Invoke(workspace,"文書復元を準備",state);Assert.True(await Restore(workspace));
        if(changed){Assert.Empty(Get<文書タブ順序>(workspace,"タブ順序").順序);Assert.NotEmpty(Get<HashSet<string>>(workspace,"文書復元案内"));}
        else{Assert.Empty(Get<HashSet<string>>(workspace,"文書復元案内"));var selected=Get<Dictionary<string,対象文書状態>>(workspace,"対象文書一覧")[Get<string>(workspace,"選択作業文書ID")];Assert.Equal("Items",selected.対象!.Id);var saved=((ブラウザー保存文書[] 文書,int? 選択))Invoke(workspace,"保存文書を取得")!;Assert.Equal(reference,Assert.Single(saved.文書));}
    });
    [Fact]public Task PublishedDatabaseAndTableRestoreInOrderWithoutTreatingTheirDisplayAsUserNavigation()=>WithWorkspace(async workspace=>{
        Cache(workspace,"a");SetUser(workspace,new("owner",[new("a",ManagementPermission.View)],false));
        Get<Dictionary<string,公開DB表示状態>>(workspace,"公開DB一覧")["db:a"]=new(DB("a"),"v",["dbo0"],Designer.Objects(Definition("a")));Invoke(workspace,"文書を選ぶ","db:a");
        var references=new[]{Reference("a","Database","","","DB情報") with{PublishedOnly=true,DefinitionHash=""},Reference("a") with{PublishedOnly=true,DefinitionHash="",View="データ"}};
        var state=State(references,1) with{Format=5};state.検証();Invoke(workspace,"文書復元を準備",state);Assert.True(await Restore(workspace));Assert.Empty(Get<HashSet<string>>(workspace,"文書復元案内"));
        var tabs=Get<文書タブ順序>(workspace,"タブ順序").順序;var documents=Get<Dictionary<string,対象文書状態>>(workspace,"対象文書一覧");Assert.Equal(new[]{"DB情報","データ"},tabs.Select(id=>documents[id].表示));Assert.Equal("Items",documents[tabs[1]].対象!.Id);Assert.Equal(tabs[1],Get<string>(workspace,"選択作業文書ID"));
        var saved=((ブラウザー保存文書[] 文書,int? 選択))Invoke(workspace,"保存文書を取得")!;Assert.Equal(references,saved.文書);Assert.Equal(1,saved.選択);
    });
    [Theory][InlineData(false)][InlineData(true)]public Task SlowPublishedMetadataCannotReplaceUserSelectionOrUseRevokedPermission(bool revoke)=>WithWorkspace(async workspace=>{
        Cache(workspace,"a");Cache(workspace,"b");SetUser(workspace,new("owner",[new("*",ManagementPermission.View)],false));Invoke(workspace,"文書を選ぶ","db:b");
        var operations=(RestoreOperations)Get<IManagementOperations>(workspace,"管理操作");operations.DelayedSchemas=new(TaskCreationOptions.RunContinuationsAsynchronously);
        var reference=Reference("a") with{PublishedOnly=true,DefinitionHash="",View="データ"};Invoke(workspace,"文書復元を準備",State([reference],0) with{Format=5});var restore=Restore(workspace);Assert.False(restore.IsCompleted);
        if(revoke){SetUser(workspace,new("owner",[],false));}else{Invoke(workspace,"文書を選ぶ","db:b");}
        operations.DelayedSchemas.SetResult(new([],0,false,"v"));await restore;Assert.NotEmpty(Get<HashSet<string>>(workspace,"文書復元案内"));Assert.DoesNotContain(Get<Dictionary<string,対象文書状態>>(workspace,"対象文書一覧").Values,item=>item.DBID=="db:a"&&item.対象 is not null);Assert.Equal("db:b",Get<string>(workspace,"選択DBID"));
    });
    private static async Task WithWorkspace(Func<WorkspaceStartup.TestWorkspace,Task> action){
        var operations=DispatchProxy.Create<IManagementOperations,RestoreOperations>();using var provider=new ServiceCollection().AddLogging().AddSingleton(operations).AddSingleton<IJSRuntime,StaticRenderScripts>().BuildServiceProvider();await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            WorkspaceStartup.TestWorkspace? workspace=null;await renderer.RenderComponentAsync<WorkspaceStartup.TestWorkspace>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"Capture",(Action<WorkspaceStartup.TestWorkspace>)(value=>workspace=value)},{"利用者",new UserInfo("owner",[new("*",ManagementPermission.All)],false)}}));
            var instance=Guid.NewGuid();Set(workspace!,"表示状態インスタンスID",instance);Set(workspace!,"表示状態キー",ブラウザー表示状態.保存キー(instance,workspace!.利用者));await action(workspace);
        });
    }
    private static void Cache(管理ワークスペース workspace,string id){
        var db=DB(id);var databases=Get<CatalogDatabase[]>(workspace,"DB一覧");Set(workspace,"DB一覧",databases.Where(item=>item.Id!=db.Id).Append(db).ToArray());Get<Dictionary<string,Document>>(workspace,"文書一覧")[db.Id]=new(db,new(Definition(id),1,"owner",DateTimeOffset.UtcNow,Hash),null);Get<Dictionary<string,FunctionSummary[]>>(workspace,"登録関数一覧")[db.Id]=[];
    }
    private static void Prepare(object workspace,ブラウザー保存文書[] documents,int? selected){var value=State(documents,selected);value.検証();Invoke(workspace,"文書復元を準備",value);}
    private static Task<bool> Restore(object workspace)=>(Task<bool>)Invoke(workspace,"保存文書を復元Async")!;
    private static void SetUser(object workspace,UserInfo user)=>typeof(管理ワークスペース).GetProperty("利用者")!.SetValue(workspace,user);
    private static FieldInfo Field(string name)=>typeof(管理ワークスペース).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!;
    private static T Get<T>(object workspace,string name){if(name=="管理操作"){return(T)typeof(管理ワークスペース).GetProperty(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(workspace)!;}return(T)Field(name).GetValue(workspace)!;}
    private static void Set(object workspace,string name,object value)=>Field(name).SetValue(workspace,value);
    private static object? Invoke(object workspace,string name,params object?[] args)=>typeof(管理ワークスペース).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(workspace,args);
    public class RestoreOperations:DispatchProxy{
        public TaskCompletionSource<DefinitionVersion>? Delayed{get;set;}
        public TaskCompletionSource<CatalogPage<CatalogSchema>>? DelayedSchemas{get;set;}
        protected override object? Invoke(MethodInfo? method,object?[]? args)=>method!.Name switch{
            nameof(IManagementOperations.InfoAsync)=>Task.FromResult(new ServerInfo(Guid.NewGuid(),"fixture","fixture",[])),
            nameof(IManagementOperations.DatabasesAsync)=>Task.FromResult(new CatalogPage<CatalogDatabase>([],0,false,"v")),
            nameof(IManagementOperations.GetDefinitionAsync)=>this.Delayed!.Task,
            nameof(IManagementOperations.FindDraftAsync)=>Task.FromResult<Draft?>(null),
            nameof(IManagementOperations.SchemasAsync)=>this.DelayedSchemas!.Task,
            nameof(IManagementOperations.ObjectsAsync)=>Task.FromResult(new CatalogPage<CatalogObject>(Designer.Objects(Definition("a")),0,false,"v")),
            nameof(IManagementOperations.FunctionsAsync)=>Task.FromResult(new CatalogPage<FunctionSummary>([],0,false,"v")),
            _=>throw new InvalidOperationException("Unexpected operation "+method.Name)
        };
    }
}
