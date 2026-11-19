using System.Collections;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Seamless.Management;
using Seamless.Management.Components;
using Designer=Seamless.Management.DatabaseDesigner;

namespace Management.Tests;

public sealed class UnifiedTreeMenus{
    [Fact]public async Task RightClickKeyboardAndEllipsisShareOneMenuWithoutSelectingOrExpandingTheRow(){
        var host=new 対象操作ホスト();var requests=new List<対象操作要求>();var selected=0;var expanded=0;
        Parameter(host,"ID","table:Items");Parameter(host,"展開中か",false);Parameter(host,"選択時",EventCallback.Factory.Create(this,()=>selected++));Parameter(host,"展開変更時",EventCallback.Factory.Create<bool>(this,_=>expanded++));Parameter(host,"メニュー要求時",EventCallback.Factory.Create<対象操作要求>(this,requests.Add));
        await(Task)Invoke(host,"ハンドル押下時Async",new MouseEventArgs())!;await(Task)Invoke(host,"右クリック時Async",new MouseEventArgs{ClientX=11,ClientY=22})!;await(Task)Invoke(host,"キー押下時Async",new KeyboardEventArgs{Key="F10",ShiftKey=true})!;await(Task)Invoke(host,"キー押下時Async",new KeyboardEventArgs{Key="ContextMenu"})!;
        Assert.Equal(4,requests.Count);Assert.All(requests,request=>Assert.Equal("table:Items",request.ID));Assert.Equal(11,requests[1].X);Assert.Equal(22,requests[1].Y);Assert.Equal(0,selected);Assert.Equal(0,expanded);
    }
    [Fact]public async Task TreeModeRemovesOnlyThePlusAndKeepsTheExistingMenuAnchor(){
        using var services=new ServiceCollection().AddLogging().BuildServiceProvider();await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{var output=await renderer.RenderComponentAsync<対象操作ホスト>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"ID","databases"},{"名前","Databases"},{"追加ボタンを表示するか",false},{"追加時",EventCallback.Factory.Create(this,()=>{})}}));var html=System.Net.WebUtility.HtmlDecode(output.ToHtmlString());Assert.DoesNotContain("data-操作アイコン=\"追加\"",html);Assert.Contains("data-メニュー起点=\"databases\"",html);Assert.Contains("data-操作アイコン=\"その他\"",html);});
    }
    // 名前編集はツリーのダブルクリックとF2で開始する。生成コードの表示・コピー・削除は対象別のメニューで使える。
    [Theory]
    [InlineData("db","全ソース")]
    [InlineData("schema:dbo0","生成コード,スキーマ削除")]
    [InlineData("group:View:dbo0","生成コード")]
    [InlineData("group:Schema:","生成コード")]
    [InlineData("databases","全ソース")]
    [InlineData("table:Items","生成コード,対象コピー,対象削除")]
    public async Task TreeMenusShowOnlyTheRowOwnActions(string id,string expected){
        await using var workspace=await Workspace(ManagementPermission.All);
        await(Task)Invoke(workspace,"メニュー要求時Async",new 対象操作要求(id,default,0,0))!;
        var ids=Commands(workspace,Get<object>(workspace,"メニュー対象")).Select(command=>(string)command.GetType().GetProperty("ID")!.GetValue(command)!).ToArray();
        Assert.Equal(expected.Split(',',StringSplitOptions.RemoveEmptyEntries),ids);
        Assert.False(((ツリー展開状態)Invoke(workspace,"DBツリー状態","db")!).展開中か);
    }
    [Fact]public async Task CommandPaletteStillFindsCreationCommands(){
        await using var workspace=await Workspace(ManagementPermission.All);
        await(Task)Invoke(workspace,"メニュー要求時Async",new 対象操作要求("table:Items",default,0,0))!;
        var target=Get<object>(workspace,"メニュー対象");
        Set(workspace,"パレットか",true);
        var ids=Commands(workspace,target).Select(command=>(string)command.GetType().GetProperty("ID")!.GetValue(command)!).ToArray();
        Assert.Contains("表データ",ids);Assert.Contains("表の列追加",ids);Assert.DoesNotContain("対象名前変更",ids);
    }
    [Fact]public async Task DatabaseDisplayOrderChangesOnlyTheBrowserOrderForAViewUser(){
        await using var workspace=await Workspace(ManagementPermission.View);
        var databases=new[]{new CatalogDatabase("db:a","A","a",1,"",false),new("db:b","B","b",1,"",false)};
        Set(workspace,"DB一覧",databases);Parameter(workspace,"利用者",new UserInfo("owner",[new("*",ManagementPermission.View)],false));Set(workspace,"表示状態操作",new BrowserStorage());
        await(Task)Invoke(workspace,"メニュー要求時Async",new 対象操作要求("db:b",default,0,0))!;
        var target=Get<object>(workspace,"メニュー対象");
        // 表示順はドラッグで変更する。メニューには出さない。
        Assert.DoesNotContain(Commands(workspace,target),item=>Equals(item.GetType().GetProperty("ID")!.GetValue(item),"DB表示上"));
        var selected=Get<string>(workspace,"選択DBID");await(Task)Invoke(workspace,"DB表示順を変更Async",target,-1)!;
        var order=Get<DB表示順序>(workspace,"DBの表示順");Assert.Equal(new[]{"db:b","db:a"},order.適用(databases).Select(DB=>DB.Id));Assert.Equal(databases,Get<CatalogDatabase[]>(workspace,"DB一覧"));Assert.Equal(selected,Get<string>(workspace,"選択DBID"));
        await(Task)Invoke(workspace,"DB表示順を戻すAsync")!;Assert.Equal(databases,order.適用(databases));
    }
    private static async Task<管理ワークスペース> Workspace(ManagementPermission permission){
        var workspace=new 管理ワークスペース();Parameter(workspace,"管理操作",DispatchProxy.Create<IManagementOperations,InitializationOperations>());await(Task)Invoke(workspace,"OnInitializedAsync")!;Parameter(workspace,"利用者",new UserInfo("owner",[new("DB",permission)],false));
        Set(workspace,"DB一覧",new[]{new CatalogDatabase("db","DB","db",1,"",false)});var definition=Designer.AddSchema(Designer.Apply(Designer.Empty("db","DB"),new("Items","",[new("Id","int",true)],[])),"dbo0");Get<Dictionary<string,Seamless.Management.Components.文書状態>>(workspace,"文書一覧").Add("db",new(new("db","DB","db",1,"",false),new(definition,1,"owner",DateTimeOffset.UtcNow),null));Set(workspace,"選択DBID","db");Set(workspace,"選択表名","before");return workspace;
    }
    private static object[] Commands(object workspace,object target){var registry=Get<object>(workspace,"コマンド");return((IEnumerable)registry.GetType().GetMethod("検索")!.Invoke(registry,[target,""])!).Cast<object>().ToArray();}
    private static object? Invoke(object target,string name,params object[] args)=>target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public)!.Invoke(target,args);
    private static T Get<T>(object target,string name)=>(T)target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(target)!;
    private static void Set(object target,string name,object? value)=>target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(target,value);
    private static void Parameter(object target,string name,object? value)=>target.GetType().GetProperty(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!.SetValue(target,value);
    public class InitializationOperations:DispatchProxy{protected override object? Invoke(MethodInfo? method,object?[]? args)=>method!.Name switch{nameof(IManagementOperations.InfoAsync)=>Task.FromException<ServerInfo>(new ManagementException("Unsupported","Fixture stops after registering commands.")),_=>throw new NotSupportedException(method.Name)};}
    private sealed class BrowserStorage:Microsoft.JSInterop.IJSObjectReference{
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier,object?[]? args){if(identifier=="dispose"){return ValueTask.FromResult(default(TValue)!);}throw new InvalidOperationException("Menu execution must not perform storage IO directly.");}
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier,CancellationToken cancellationToken,object?[]? args)=>this.InvokeAsync<TValue>(identifier,args);
        public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
    }
}
