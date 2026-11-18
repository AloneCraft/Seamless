using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Seamless.Management;
using Seamless.Management.Components;

namespace Management.Tests;

public sealed class DatabaseDisplayOrder{
    private static CatalogDatabase DB(string ID,string name)=>new("db:"+ID,name,ID,1,"artifact",false);
    [Fact]public void MovesUseDatabaseIdentityAndNeverChangeTheCatalogOrInferIdentityFromAName(){
        var source=new[]{DB("a","Same"),DB("b","Same"),DB("c","Third")};var original=source.ToArray();var order=new DB表示順序();
        Assert.True(order.移動(source,"db:b",-1));Assert.Equal(new[]{"db:b","db:a","db:c"},order.適用(source).Select(DB=>DB.Id));Assert.Equal(original,source);
        var renamed=source.Select(DB=>DB with{Name="Renamed"}).ToArray();Assert.Equal("db:b",order.適用(renamed)[0].Id);
        var recreated=new[]{DB("a","Same"),DB("new-b","Same"),DB("c","Third")};Assert.Equal(new[]{"db:a","db:c","db:new-b"},order.適用(recreated).Select(DB=>DB.Id));
    }
    [Fact]public void ReorderingLoadedPagesPreservesThePositionsOfNotYetLoadedIdentities(){
        var order=new DB表示順序();order.復元(["db:a","db:later","db:b"]);var firstPage=new[]{DB("a","A"),DB("b","B")};
        Assert.True(order.移動(firstPage,"db:b",-1));Assert.Equal(new[]{"db:b","db:later","db:a"},order.保存値);
        Assert.Equal(new[]{"db:b","db:later","db:a"},order.適用([..firstPage,DB("later","Later")]).Select(DB=>DB.Id));
    }
    [Fact]public void DropMovesBeforeOrAfterTheActualDatabaseAndKeepsUnloadedIdentities(){
        var order=new DB表示順序();order.復元(["db:a","db:later","db:b","db:c"]);var loaded=new[]{DB("a","A"),DB("b","B"),DB("c","C")};
        Assert.True(order.対象位置へ移動(loaded,"db:c","db:a",true));Assert.Equal(new[]{"db:c","db:later","db:a","db:b"},order.保存値);
        Assert.True(order.対象位置へ移動(loaded,"db:c","db:b",false));Assert.Equal(new[]{"db:a","db:later","db:b","db:c"},order.保存値);
        foreach(var target in new[]{"db:c","db:missing","Items"}){Assert.False(order.対象位置へ移動(loaded,"db:c",target,true));}
        Assert.False(order.対象位置へ移動(loaded,"db:a","db:b",true));Assert.False(order.対象位置へ移動(loaded,"db:missing","db:b",false));Assert.Equal(new[]{"db:a","db:later","db:b","db:c"},order.保存値);
    }
    [Fact]public void InvalidOrOversizedStateIsRejectedAtomicallyAndResetRestoresTheCatalogOrder(){
        var order=new DB表示順序();order.復元(["db:b","db:a"]);
        foreach(var invalid in new[]{new[]{"Items"},new[]{"db:a","db:a"},Enumerable.Range(0,257).Select(index=>"db:"+index).ToArray()}){Assert.Throws<InvalidOperationException>(()=>order.復元(invalid));Assert.Equal(new[]{"db:b","db:a"},order.保存値);}
        var source=new[]{DB("a","A"),DB("b","B")};Assert.False(order.移動(source,"db:b",-1));Assert.False(order.移動(source,"db:missing",1));order.既定順に戻す();Assert.Equal(source,order.適用(source));
    }
    [Fact]public void FormatThreeRoundTripsDisplayOrderAndOlderFormatsRejectTheNewField(){
        var value=new ブラウザー表示状態(3,["db:a"],true,50){SidebarWidth=300,SidebarCollapsed=false,DatabaseOrder=["db:b","db:a"]};value.検証();
        var restored=System.Text.Json.JsonSerializer.Deserialize<ブラウザー表示状態>(System.Text.Json.JsonSerializer.Serialize(value))!;restored.検証();Assert.Equal(value.DatabaseOrder,restored.DatabaseOrder);
        Assert.Throws<InvalidOperationException>(()=>(value with{Format=2}).検証());Assert.Throws<InvalidOperationException>(()=>(value with{DatabaseOrder=["dbo.Items"]}).検証());Assert.Throws<InvalidOperationException>(()=>(value with{DatabaseOrder=null}).検証());
    }
    [Fact]public async Task ReorderingDatabaseRowsPreservesTheirOpenTreeAndUnsavedColumnEditor(){
        var operations=DispatchProxy.Create<IManagementOperations,WorkspaceStartup.EmptyOperations>();
        using var provider=new ServiceCollection().AddLogging().AddSingleton(operations).AddSingleton<IJSRuntime,StaticRenderScripts>().BuildServiceProvider();
        await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            WorkspaceStartup.TestWorkspace? workspace=null;
            var output=await renderer.RenderComponentAsync<WorkspaceStartup.TestWorkspace>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"Capture",(Action<WorkspaceStartup.TestWorkspace>)(value=>workspace=value)},{"利用者",new UserInfo("owner",[new("*",ManagementPermission.All)],false)}}));
            var databases=new[]{DB("a","A"),DB("b","B")};Set(workspace!,"DB一覧",databases);
            var documents=Field<Dictionary<string,Seamless.Management.Components.文書状態>>(workspace!,"文書一覧");
            var tabs=Field<文書タブ順序>(workspace!,"タブ順序");
            var openDocuments=Field<Dictionary<string,対象文書状態>>(workspace!,"対象文書一覧");
            foreach(var database in databases){
                var definition=global::Seamless.Management.DatabaseDesigner.Apply(global::Seamless.Management.DatabaseDesigner.Empty(database.DefinitionId,database.Name),new("Items","",[new("Id","int",true)],[]));
                documents.Add(database.Id,new(database,new(definition,1,"owner",DateTimeOffset.UtcNow),null));
                openDocuments.Add(database.Id,new(database.Id,database.DefinitionId,ID:database.Id));tabs.追加(database.Id);
                var state=new ツリー展開状態{展開中か=true};state.分類を開閉("","Table",true);state.テーブルを開閉("Items",true);Field<Dictionary<string,ツリー展開状態>>(workspace!,"DBツリー一覧").Add(database.Id,state);
            }
            workspace!.Refresh();
            Assert.DoesNotContain("class=\"文書タブ帯\"",System.Net.WebUtility.HtmlDecode(output.ToHtmlString()));
            var trees=Field<Dictionary<string,DBオブジェクトツリー>>(workspace,"DBツリー画面");var originalTree=trees["db:a"];
            await originalTree.列追加を開始Async("Items");
            var membership=Field<Dictionary<string,所属オブジェクト一覧>>(originalTree,"所属一覧")[""];
            var editor=Field<Dictionary<string,ツリー列一覧>>(membership,"列画面")["Items"];Set(editor,"列名","PendingA");workspace.Refresh();
            Assert.True(editor.未保存か);Field<DB表示順序>(workspace,"DBの表示順").移動(databases,"db:b",-1);workspace.Refresh();
            Assert.Same(originalTree,trees["db:a"]);Assert.Same(editor,Field<Dictionary<string,ツリー列一覧>>(Field<Dictionary<string,所属オブジェクト一覧>>(trees["db:a"],"所属一覧")[""],"列画面")["Items"]);
            Assert.Equal("PendingA",Field<string>(editor,"列名"));Assert.True(editor.未保存か);
            var html=System.Net.WebUtility.HtmlDecode(output.ToHtmlString());Assert.True(html.IndexOf("data-db-tree=\"db:b\"",StringComparison.Ordinal)<html.IndexOf("data-db-tree=\"db:a\"",StringComparison.Ordinal));
            Assert.Matches("class=\"対象名\">A</span>\\s*<span class=\"変更印\"",html);
            Assert.DoesNotMatch("class=\"対象名\">B</span>\\s*<span class=\"変更印\"",html);
        });
    }
    [Fact]public async Task DragRequestsUseTheSameBrowserOrderAndRejectUnavailableOrUnauthorizedTargets(){
        var operations=DispatchProxy.Create<IManagementOperations,WorkspaceStartup.EmptyOperations>();
        using var provider=new ServiceCollection().AddLogging().AddSingleton(operations).AddSingleton<IJSRuntime,StaticRenderScripts>().BuildServiceProvider();
        await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            WorkspaceStartup.TestWorkspace? workspace=null;
            var output=await renderer.RenderComponentAsync<WorkspaceStartup.TestWorkspace>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"Capture",(Action<WorkspaceStartup.TestWorkspace>)(value=>workspace=value)},{"利用者",new UserInfo("viewer",[new("*",ManagementPermission.View)],false)}}));
            var source=new[]{DB("a","A"),DB("b","B"),new CatalogDatabase("db:system","system","system",1,"",true)};Set(workspace!,"DB一覧",source);Set(workspace!,"操作受付中か",true);Set(workspace!,"表示状態操作",new NoStorageIO());workspace!.Refresh();
            var html=output.ToHtmlString();Assert.Contains("data-db-order-row=\"db:a\"",html);Assert.Contains("data-db-order-enabled=\"true\"",html);
            await workspace.DB表示順ドロップAsync("db:b","db:a",true);var order=Field<DB表示順序>(workspace,"DBの表示順");Assert.Equal(new[]{"db:b","db:a"},order.保存値);Assert.Equal(source,Field<CatalogDatabase[]>(workspace,"DB一覧"));
            foreach(var target in new[]{"db:system","db:missing","tab:db:a","Items"}){await workspace.DB表示順ドロップAsync("db:a",target,true);Assert.Equal(new[]{"db:b","db:a"},order.保存値);}
            Set(workspace,"表示状態診断","Unavailable");await workspace.DB表示順ドロップAsync("db:a","db:b",true);Assert.Equal(new[]{"db:b","db:a"},order.保存値);Set(workspace,"表示状態診断",null!);
            Set(workspace,"操作受付中か",false);await workspace.DB表示順ドロップAsync("db:a","db:b",true);Assert.Equal(new[]{"db:b","db:a"},order.保存値);Set(workspace,"操作受付中か",true);
            typeof(管理ワークスペース).GetProperty("利用者")!.SetValue(workspace,new UserInfo("viewer",[],false));await workspace.DB表示順ドロップAsync("db:a","db:b",true);Assert.Equal(new[]{"db:b","db:a"},order.保存値);
        });
    }
    private sealed class NoStorageIO:IJSObjectReference{
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier,object?[]? args){if(identifier=="dispose"){return ValueTask.FromResult(default(TValue)!);}throw new InvalidOperationException("Drag must not call storage or management IO directly.");}
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier,CancellationToken cancellationToken,object?[]? args)=>this.InvokeAsync<TValue>(identifier,args);
        public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
    }
    private static FieldInfo Member(object target,string name){
        for(Type? type=target.GetType();type is not null;type=type.BaseType){if(type.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic) is{} field){return field;}}
        throw new InvalidOperationException(name);
    }
    private static T Field<T>(object target,string name)=>(T)Member(target,name).GetValue(target)!;
    private static void Set(object target,string name,object value)=>Member(target,name).SetValue(target,value);
}
