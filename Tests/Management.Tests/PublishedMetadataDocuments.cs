using System.Reflection;
using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Seamless.Management;
using Seamless.Management.Components;

namespace Management.Tests;

public sealed class PublishedMetadataDocuments{
    [Fact]public async Task RootCatalogSchemaDoesNotCreateAnEmptySchemaBranchOrDuplicateRootObjects(){
        var operations=DispatchProxy.Create<IManagementOperations,MetadataOperations>();
        using var provider=new ServiceCollection().AddLogging().AddSingleton(operations).AddSingleton<IJSRuntime,StaticRenderScripts>().BuildServiceProvider();await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            WorkspaceStartup.TestWorkspace? workspace=null;
            await renderer.RenderComponentAsync<WorkspaceStartup.TestWorkspace>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"Capture",(Action<WorkspaceStartup.TestWorkspace>)(value=>workspace=value)},{"利用者",new UserInfo("viewer",[new("Native",ManagementPermission.View)],false)}}));
            var documents=(Dictionary<string,公開DB表示状態>)typeof(管理ワークスペース).GetField("公開DB一覧",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(workspace)!;
            var document=documents["db:db"];Assert.Equal("dbo0",Assert.Single(document.スキーマ));
            var tree=await renderer.RenderComponentAsync<DBオブジェクトツリー>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"スキーマ",document.スキーマ},{"オブジェクト",document.オブジェクト}}));
            var html=WebUtility.HtmlDecode(tree.ToHtmlString());Assert.Single(System.Text.RegularExpressions.Regex.Matches(html,"data-対象-id=\"group:Table:\"").Cast<System.Text.RegularExpressions.Match>());Assert.DoesNotContain("data-対象-id=\"schema:\"",html);Assert.Contains("data-対象-id=\"schema:dbo0\"",html);
        });
    }
    [Fact]public async Task ViewerUsesOnlyPublishedMetadataAndReaderReusesTheReadonlyGrid(){
        var operations=DispatchProxy.Create<IManagementOperations,MetadataOperations>();var calls=(MetadataOperations)operations;
        using var provider=new ServiceCollection().AddLogging().AddSingleton(operations).AddSingleton<IJSRuntime,StaticRenderScripts>().BuildServiceProvider();await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            WorkspaceStartup.TestWorkspace? workspace=null;
            var rendered=await renderer.RenderComponentAsync<WorkspaceStartup.TestWorkspace>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"Capture",(Action<WorkspaceStartup.TestWorkspace>)(value=>workspace=value)},{"利用者",new UserInfo("viewer",[new("Native",ManagementPermission.View)],false)}}));
            typeof(管理ワークスペース).GetMethod("公開対象を開く",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(workspace,["db:db",MetadataOperations.Table]);workspace!.Refresh();
            var html=WebUtility.HtmlDecode(rendered.ToHtmlString());Assert.Contains("公開メタデータ・読取専用",html);Assert.Contains("ReadRows",html);Assert.DoesNotContain("対象のC#",html);Assert.DoesNotContain("role=\"tab\" aria-selected=\"false\"",html);Assert.Equal(0,calls.Reads);Assert.Equal(0,calls.SourceCalls);
            var commands=typeof(管理ワークスペース).GetField("コマンド",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(workspace)!;
            var target=typeof(管理ワークスペース).GetNestedType("管理対象",BindingFlags.NonPublic)!.GetConstructors().Single(constructor=>constructor.GetParameters().Length==5).Invoke(["表","db:db","Rows","",null]);
            var matches=(System.Collections.IEnumerable)commands.GetType().GetMethod("検索")!.Invoke(commands,[target,""])!;var ids=matches.Cast<object>().Select(command=>(string)command.GetType().GetProperty("ID")!.GetValue(command)!).ToArray();Assert.Empty(ids);Assert.DoesNotContain("表ソース",ids);Assert.DoesNotContain("表を編集",ids);
            await Assert.ThrowsAsync<InvalidOperationException>(()=>(Task)commands.GetType().GetMethod("実行Async")!.Invoke(commands,["表ソース",target])!);
            typeof(管理ワークスペース).GetProperty("利用者")!.SetValue(workspace,new UserInfo("viewer",[new("Native",ManagementPermission.View|ManagementPermission.ReadRows)],false));workspace.Refresh();
            html=WebUtility.HtmlDecode(rendered.ToHtmlString());Assert.Equal(1,calls.Reads);Assert.Contains("class=\"データ表\"",html);Assert.Contains("readonly",html);Assert.DoesNotContain("行を追加</",html);Assert.Equal(0,calls.SourceCalls);
            var saved=((ブラウザー保存文書[] 文書,int? 選択))typeof(管理ワークスペース).GetMethod("保存文書を取得",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(workspace,null)!;var document=Assert.Single(saved.文書,item=>item.Kind=="Table");Assert.True(document.PublishedOnly);Assert.Empty(document.DefinitionHash);
            typeof(管理ワークスペース).GetMethod("公開対象を開く",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(workspace,["db:db",MetadataOperations.View]);workspace.Refresh();html=WebUtility.HtmlDecode(rendered.ToHtmlString());Assert.Contains("ビューと関数の実行にはExecute権限が必要です。",html);
            typeof(管理ワークスペース).GetProperty("利用者")!.SetValue(workspace,new UserInfo("viewer",[new("Native",ManagementPermission.View|ManagementPermission.ReadRows|ManagementPermission.Execute)],false));workspace.Refresh();html=WebUtility.HtmlDecode(rendered.ToHtmlString());Assert.Contains("問い合わせの実行",html);Assert.DoesNotContain("実行時の計測",html);Assert.DoesNotContain("対象のC#",html);Assert.Equal(0,calls.SourceCalls);
        });
    }
    [Fact]public void PublishedReferencesRequireFormatFiveAndNeverUseASyntheticSourceHash(){
        var reference=new ブラウザー保存文書("db:db",1,MetadataOperations.Hash,"db",1,MetadataOperations.Hash,"","Table","Rows","","データ"){PublishedOnly=true};reference.検証();
        var state=new ブラウザー表示状態(5,[],false,0){SidebarWidth=254,SidebarCollapsed=false,DatabaseOrder=[],Documents=[reference],SelectedDocument=0};state.検証();Assert.Throws<InvalidOperationException>(()=>(state with{Format=4}).検証());Assert.Throws<InvalidOperationException>(()=>(reference with{View="C#"}).検証());Assert.Throws<InvalidOperationException>(()=>(reference with{DefinitionHash=MetadataOperations.Hash}).検証());
    }
    public class MetadataOperations:DispatchProxy{
        internal static readonly string Hash=new('A',64);
        internal static readonly CatalogObject Table=new("Rows","Rows","","Table","Model.Row");
        internal static readonly CatalogObject View=new("All","All","","View","Model.Row");
        internal int Reads,SourceCalls;
        protected override object? Invoke(MethodInfo? method,object?[]? args){
            switch(method!.Name){
                case nameof(IManagementOperations.InfoAsync):return Task.FromResult(new ServerInfo(Guid.NewGuid(),"fixture","fixture",[]));
                case nameof(IManagementOperations.DatabasesAsync):return Task.FromResult(new CatalogPage<CatalogDatabase>([new("db:db","Native","db",1,Hash,false)],0,false,"v1"));
                case nameof(IManagementOperations.SchemasAsync):return Task.FromResult(new CatalogPage<CatalogSchema>([new(""),new("dbo0")],0,false,"v1"));
                case nameof(IManagementOperations.ObjectsAsync):return Task.FromResult(new CatalogPage<CatalogObject>([Table,View],0,false,"v1"));
                case nameof(IManagementOperations.FunctionsAsync):return Task.FromResult(new CatalogPage<FunctionSummary>([],0,false,"v1"));
                case nameof(IManagementOperations.GetObjectDetailsAsync):var target=Table;if(((CatalogRequest)args![0]!).ObjectId==View.Id){target=View;}return Task.FromResult(new CatalogObjectDetails(target,[new("Value","int")],[new("Id","int")],[],null,new("db",1,Hash,null,0,0,false)));
                case nameof(IManagementOperations.ReadRowsAsync):this.Reads++;return Task.FromResult(new TableRows(1,[new(ManagementJson.Element(new{Id=1}),ManagementJson.Element(new{Value=7}),Hash)],false,[new("Value","int")]){KeyMembers=[new("Id","int")]});
                case nameof(IManagementOperations.GetDefinitionAsync):case nameof(IManagementOperations.FindDraftAsync):case nameof(IManagementOperations.QueryPlansAsync):this.SourceCalls++;throw new InvalidOperationException("Source APIs must never be called for published metadata.");
                default:throw new InvalidOperationException(method.Name);
            }
        }
    }
}
