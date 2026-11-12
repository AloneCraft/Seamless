using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Seamless.Management;
using Seamless.Management.Components;
using Document=Seamless.Management.Components.文書状態;
using Designer=Seamless.Management.DatabaseDesigner;

namespace Management.Tests;

public sealed class TreeExpansion{
    [Fact]public async Task SelectingADatabaseKeepsItsCurrentExpansionState(){
        var operations=DispatchProxy.Create<IManagementOperations,TreeOperations>();((TreeOperations)(object)operations).Read=id=>Task.FromResult(Version(id));await using var workspace=Workspace(operations);
        await(Task)Invoke(workspace,"DBを開くAsync","first")!;var state=(ツリー展開状態)Invoke(workspace,"DBツリー状態","first")!;Assert.False(state.展開中か);Assert.Equal("first",Get<string>(workspace,"選択DBID"));
        await(Task)Invoke(workspace,"DBツリーを開閉Async","first",true)!;await(Task)Invoke(workspace,"DBを開くAsync","first")!;Assert.True(state.展開中か);
    }
    [Fact]public async Task LeafSelectionUsesTheCatalogIdentityAndDoesNotAlsoSelectItsSchema(){
        var objects=new CatalogObject[]{new("Items","Items","","Table","Item"),new("dbo0.Items","Items","dbo0","Table","Item"),new("dbo1.Items","Items","dbo1","Table","Item"),new("dbo0.ItemList","ItemList","dbo0","View","Item")};
        var parameters=new Dictionary<string,object?>{{"スキーマ",new[]{"dbo0","dbo1"}},{"オブジェクト",objects},{"選択スキーマ","dbo0"},{"選択オブジェクト",objects[1]}};
        var html=await Render<DBオブジェクトツリー>(parameters);
        Assert.Equal(1,html.Split("data-選択中=\"true\"",StringSplitOptions.None).Length-1);
        Assert.Contains("data-対象-id=\"table:dbo0.Items\" data-選択中=\"true\"",html);
        Assert.Contains("data-対象-id=\"table:Items\" data-選択中=\"false\"",html);
        Assert.Contains("data-対象-id=\"table:dbo1.Items\" data-選択中=\"false\"",html);
        Assert.Contains("data-対象-id=\"schema:dbo0\" data-選択中=\"false\"",html);
        parameters["選択オブジェクト"]=null;
        html=await Render<DBオブジェクトツリー>(parameters);
        Assert.Equal(1,html.Split("data-選択中=\"true\"",StringSplitOptions.None).Length-1);
        Assert.Contains("data-対象-id=\"schema:dbo0\" data-選択中=\"true\"",html);
    }
    [Fact]public async Task TableSelectionSurvivesDocumentTabsAndIsScopedToTheActiveDatabase(){
        await using var workspace=Workspace(DispatchProxy.Create<IManagementOperations,TreeOperations>());
        var definition=Designer.Apply(Designer.Empty("first","first"),new("Items","dbo0",[new("Id","int",true)],[]));
        var document=new Document(new("first","first","first",1,"",false),new(definition,1,"owner",DateTimeOffset.UtcNow),null);
        Get<Dictionary<string,Document>>(workspace,"文書一覧").Add("first",document);
        Invoke(workspace,"文書を選ぶ","first");Invoke(workspace,"表データ表示時","dbo0.Items");
        foreach(var display in new[]{"データ","構造","ソース","履歴"}){
            var field=typeof(管理ワークスペース).GetField("表示",BindingFlags.Instance|BindingFlags.NonPublic)!;
            field.SetValue(workspace,Enum.Parse(field.FieldType,display));
            var selected=(CatalogObject)Invoke(workspace,"ツリー選択オブジェクト","first")!;
            Assert.Equal("dbo0.Items",selected.Id);Assert.Equal("Table",selected.Kind);
            Assert.Null(Invoke(workspace,"ツリー選択オブジェクト","second"));
        }
        Invoke(workspace,"スキーマを選ぶ","dbo0");
        Assert.Null(Invoke(workspace,"ツリー選択オブジェクト","first"));
        Assert.Equal("dbo0.Items",Get<string>(workspace,"選択表名"));
        Invoke(workspace,"表データ表示時","dbo0.Items");
        Assert.NotNull(Invoke(workspace,"ツリー選択オブジェクト","first"));
        Invoke(workspace,"文書を選ぶ","first");
        Assert.Null(Invoke(workspace,"ツリー選択オブジェクト","first"));
    }
    [Fact]public async Task ViewAndPlanSelectionUseTheDefinitionAndQualifiedObjectIdentity(){
        await using var workspace=Workspace(DispatchProxy.Create<IManagementOperations,TreeOperations>());
        var definition=Designer.ApplyQuery(Designer.AddSchema(Designer.Empty("first","first"),"dbo0"),new("ItemList","dbo0",DatabaseQueryKind.View,[new("Id","int",false)],[],"int","return [];"));
        var document=new Document(new("first","first","first",1,"",false),new(definition,1,"owner",DateTimeOffset.UtcNow),null);
        Get<Dictionary<string,Document>>(workspace,"文書一覧").Add("first",document);
        Invoke(workspace,"文書を選ぶ","first");Invoke(workspace,"問い合わせ表示時",document,"dbo0.ItemList");
        var selected=(CatalogObject)Invoke(workspace,"ツリー選択オブジェクト","first")!;
        Assert.Equal("dbo0.ItemList",selected.Id);Assert.Equal("View",selected.Kind);
        Invoke(workspace,"文書を選ぶ","first");Set(workspace,"ツリー所属選択中か",false);Set(workspace,"実行問い合わせ",null);Set(workspace,"計画対象",new QueryPlanRequest("first","dbo0.ItemList",1));
        Assert.Equal("dbo0.ItemList",((CatalogObject)Invoke(workspace,"ツリー選択オブジェクト","first")!).Id);
        Set(workspace,"計画対象",new QueryPlanRequest("other-definition","dbo0.ItemList",1));
        Assert.Null(Invoke(workspace,"ツリー選択オブジェクト","first"));
        Invoke(workspace,"クエリ一覧表示時");Assert.Null(Invoke(workspace,"ツリー選択オブジェクト","first"));
    }
    [Fact]public async Task SchemaAndKindCollapseKeepTheirOwnStateWhenTheirParentIsReopened(){
        var state=new ツリー展開状態();
        state.スキーマを開閉("dbo0",true);state.分類を開閉("","Table",true);state.分類を開閉("dbo0","Table",true);state.分類を開閉("dbo0","View",true);state.分類を開閉("dbo0","Function",true);
        var objects=new CatalogObject[]{new("Items","Items","","Table","Item"),new("dbo0.Items","Items","dbo0","Table","Item"),new("dbo0.List","List","dbo0","View","Item"),new("dbo0.Find","Find","dbo0","TableFunction","Item")};
        var parameters=new Dictionary<string,object?>{{"スキーマ",new[]{"dbo0"}},{"オブジェクト",objects},{"展開状態",state}};
        var expanded=await Render<DBオブジェクトツリー>(parameters);
        Assert.Contains("data-対象-id=\"table:Items\"",expanded);Assert.Contains("data-対象-id=\"table:dbo0.Items\"",expanded);
        Assert.Contains("data-対象-id=\"query:dbo0.Find\"",expanded);
        state.分類を開閉("dbo0","Table",false);
        var kindClosed=System.Net.WebUtility.HtmlDecode(await Render<DBオブジェクトツリー>(parameters));
        Assert.Contains("data-対象-id=\"table:dbo0.Items\"",kindClosed);Assert.Contains("class=\"オブジェクト子一覧\" hidden",kindClosed);
        Assert.Contains("data-対象-id=\"table:Items\"",kindClosed);Assert.Contains("data-対象-id=\"query:dbo0.List\"",kindClosed);
        state.スキーマを開閉("dbo0",false);
        var schemaClosed=System.Net.WebUtility.HtmlDecode(await Render<DBオブジェクトツリー>(parameters));
        Assert.Contains("data-対象-id=\"group:View:dbo0\"",schemaClosed);Assert.Contains("class=\"スキーマ子一覧\" hidden",schemaClosed);Assert.Contains("aria-expanded=\"false\"",schemaClosed);
        state.スキーマを開閉("dbo0",true);
        var reopened=System.Net.WebUtility.HtmlDecode(await Render<DBオブジェクトツリー>(parameters));
        Assert.Contains("class=\"オブジェクト子一覧\" hidden",reopened);Assert.False(state.分類展開中か("dbo0","Table"));Assert.Contains("data-対象-id=\"query:dbo0.List\"",reopened);
        state.分類を開閉("dbo0","Table",true);
        Assert.Contains("data-対象-id=\"table:dbo0.Items\"",await Render<DBオブジェクトツリー>(parameters));
    }

    [Fact]public async Task SharedCaretUsesAbsoluteArrowKeyStateWithoutSelectingTheRow(){
        var component=new 対象操作ホスト();var state=false;var selections=0;var changes=0;
        Parameter(component,"展開中か",false);
        Parameter(component,"選択時",EventCallback.Factory.Create(this,()=>selections++));
        Parameter(component,"展開変更時",EventCallback.Factory.Create<bool>(this,value=>{state=value;changes++;Parameter(component,"展開中か",value);}));
        await (Task)Invoke(component,"キー押下時Async",new KeyboardEventArgs{Key="ArrowRight"})!;
        Assert.True(state);Assert.Equal(1,changes);
        await (Task)Invoke(component,"キー押下時Async",new KeyboardEventArgs{Key="ArrowRight"})!;
        Assert.Equal(1,changes);
        await (Task)Invoke(component,"枝キー押下時Async",new KeyboardEventArgs{Key="ArrowLeft"})!;
        Assert.False(state);Assert.Equal(2,changes);Assert.Equal(0,selections);
        var html=System.Net.WebUtility.HtmlDecode(await Render<対象操作ホスト>(new(){{"ID","db"},{"名前","Example"},{"展開中か",false}}));
        Assert.Contains("type=\"button\" class=\"枝の開閉\"",html);
        Assert.Contains("aria-expanded=\"false\"",html);
    }

    [Fact]public async Task ExpandingAnotherDatabaseReadsItsDefinitionWithoutChangingTheEditor(){
        var reads=0;
        var operations=DispatchProxy.Create<IManagementOperations,TreeOperations>();
        ((TreeOperations)(object)operations).Read=id=>{reads++;return Task.FromResult(Version(id));};
        await using var workspace=Workspace(operations);
        await (Task)Invoke(workspace,"DBを開くAsync","first")!;
        Set(workspace,"選択表名","UnsavedSelection");
        var first=Get<Dictionary<string,Document>>(workspace,"文書一覧")["first"];
        first.定義=Designer.AddSchema(first.定義,"pending_schema");
        await (Task)Invoke(workspace,"DBツリーを開閉Async","second",true)!;
        Assert.Equal("first",Get<string>(workspace,"選択DBID"));Assert.Equal("UnsavedSelection",Get<string>(workspace,"選択表名"));
        Assert.True(first.未保管か);Assert.Equal(2,reads);
        Assert.Equal(new[]{"first"},Get<文書タブ順序>(workspace,"タブ順序").順序);
        var secondState=(ツリー展開状態)Invoke(workspace,"DBツリー状態","second")!;
        secondState.分類を開閉("","View",false);
        await (Task)Invoke(workspace,"DBツリーを開閉Async","second",false)!;
        await (Task)Invoke(workspace,"DBツリーを開閉Async","second",true)!;
        Assert.Same(secondState,Invoke(workspace,"DBツリー状態","second"));Assert.False(secondState.分類展開中か("","View"));Assert.Equal(2,reads);
        Invoke(workspace,"ツリースキーマ選択時","second","dbo0");
        Assert.Equal("second",Get<string>(workspace,"選択DBID"));Assert.Equal("dbo0",Get<string>(workspace,"選択スキーマ名"));
    }

    [Fact]public async Task CollapseWhileADefinitionIsLoadingStaysCollapsedAfterTheReadCompletes(){
        var pending=new TaskCompletionSource<DefinitionVersion>(TaskCreationOptions.RunContinuationsAsynchronously);
        var operations=DispatchProxy.Create<IManagementOperations,TreeOperations>();
        ((TreeOperations)(object)operations).Read=_=>pending.Task;
        await using var workspace=Workspace(operations);
        var opening=(Task)Invoke(workspace,"DBツリーを開閉Async","second",true)!;
        await (Task)Invoke(workspace,"DBツリーを開閉Async","second",false)!;
        pending.SetResult(Version("second"));await opening;
        Assert.False(((ツリー展開状態)Invoke(workspace,"DBツリー状態","second")!).展開中か);
        Assert.Null(Get<string?>(workspace,"選択DBID"));
        Assert.True(Get<Dictionary<string,Document>>(workspace,"文書一覧").ContainsKey("second"));
    }

    [Fact]public async Task DatabaseCaretRemainsActiveWhileSelectionIsLoading(){
        var pending=new TaskCompletionSource<DefinitionVersion>(TaskCreationOptions.RunContinuationsAsynchronously);var reads=0;
        var operations=DispatchProxy.Create<IManagementOperations,TreeOperations>();
        ((TreeOperations)(object)operations).Read=id=>{reads++;if(reads==1){return pending.Task;}return Task.FromResult(Version(id));};
        await using var workspace=Workspace(operations);
        var selecting=(Task)Invoke(workspace,"DBを開く時Async","second")!;
        Assert.False(selecting.IsCompleted);Assert.True(Get<int>(workspace,"処理中数")>0);
        await(Task)Invoke(workspace,"DBツリー開閉時Async","second",true)!;
        var state=(ツリー展開状態)Invoke(workspace,"DBツリー状態","second")!;Assert.True(state.展開中か);
        await(Task)Invoke(workspace,"DBツリー開閉時Async","second",false)!;Assert.False(state.展開中か);
        pending.SetResult(Version("second"));await selecting;
        Assert.False(state.展開中か);Assert.Equal("second",Get<string>(workspace,"選択DBID"));
    }

    private static 管理ワークスペース Workspace(IManagementOperations operations){
        var workspace=new 管理ワークスペース();Parameter(workspace,"管理操作",operations);Parameter(workspace,"利用者",new UserInfo("owner",[new("*",ManagementPermission.All)],false));
        Set(workspace,"DB一覧",new[]{new CatalogDatabase("first","first","first",1,"",false),new("second","second","second",1,"",false)});return workspace;
    }
    private static DefinitionVersion Version(string id)=>new(Designer.Empty(id,id),1,"owner",DateTimeOffset.UtcNow);
    private static async Task<string> Render<T>(Dictionary<string,object?> parameters)where T:IComponent{
        using var provider=new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async()=>{
            var output=await renderer.RenderComponentAsync<T>(ParameterView.FromDictionary(parameters));return output.ToHtmlString();
        });
    }
    private static object? Invoke(object target,string name,params object[] args)=>target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(target,args);
    private static T Get<T>(object target,string name)=>(T)target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(target)!;
    private static void Set(object target,string name,object? value)=>target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(target,value);
    private static void Parameter(object target,string name,object? value)=>target.GetType().GetProperty(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!.SetValue(target,value);
    public class TreeOperations:DispatchProxy{
        public Func<string,Task<DefinitionVersion>> Read=null!;
        protected override object? Invoke(MethodInfo? method,object?[]? args)=>method!.Name switch{
            nameof(IManagementOperations.GetDefinitionAsync)=>this.Read((string)args![0]!),
            nameof(IManagementOperations.FindDraftAsync)=>Task.FromResult<Draft?>(null),
            nameof(IManagementOperations.FunctionsAsync)=>Task.FromResult(new CatalogPage<FunctionSummary>([],0,false,"")),
            _=>throw new InvalidOperationException(method.Name)
        };
    }
}
