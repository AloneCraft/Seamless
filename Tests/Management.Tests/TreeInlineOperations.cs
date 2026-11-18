using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Seamless.Management;
using Seamless.Management.Components;
using Designer=Seamless.Management.DatabaseDesigner;

namespace Management.Tests;

// 左ツリーの「＋…を追加」行と、テーブル・ビュー・関数の右端の名前変更・コピー・削除(2026-11-11の利用者指示)。
public sealed class TreeInlineOperations{
    [Fact]public async Task CreatesTablesViewsFunctionsAndSchemasInTheDraft(){
        await using var workspace=await Workspace(ManagementPermission.All);
        var operations=Operations(workspace);
        {
            //テーブルは主キーIdを持って作成され、ツリーで開いた状態になる
            Assert.Null((await operations.作成("","Table","Orders")).案内);
            var table=Designer.Read(Draft(workspace),"Orders");
            Assert.Equal(new[]{("Id","int",true)},table.Columns.Select(column=>(column.Name,column.Type,column.PrimaryKey)));
            Assert.True(((ツリー展開状態)Invoke(workspace,"DBツリー状態","db")!).テーブル展開中か("Orders"));
        }
        {
            //名前付きスキーマへのビューと関数
            Assert.Null((await operations.作成("dbo0","View","Recent")).案内);
            Assert.Null((await operations.作成("dbo0","Function","Total")).案内);
            var objects=Designer.Objects(Draft(workspace));
            Assert.Contains(objects,item=>item.Schema=="dbo0"&&item.Name=="Recent"&&item.Kind=="View");
            Assert.Contains(objects,item=>item.Schema=="dbo0"&&item.Name=="Total"&&item.Kind=="ScalarFunction");
        }
        {
            //スキーマ
            Assert.Null((await operations.スキーマ作成("Sales")).案内);
            Assert.Contains("Sales",Designer.Schemas(Draft(workspace)));
        }
        {
            //同じ所属に同名があれば下書きを変えずに理由を返す
            var before=Draft(workspace);
            Assert.NotNull((await operations.作成("","Table","Orders")).案内);
            Assert.Same(before,Draft(workspace));
        }
    }
    [Fact]public async Task RenamesAndCopiesTablesAndViews(){
        await using var workspace=await Workspace(ManagementPermission.All);
        var operations=Operations(workspace);
        var items=Designer.Objects(Draft(workspace)).Single(item=>item.Name=="Items");
        {
            //テーブルのコピーは構造だけを複製し、名前が重なれば番号を付ける
            Assert.Null((await operations.コピー(items)).案内);
            Assert.Null((await operations.コピー(items)).案内);
            Assert.Equal(new[]{("Id","int",true)},Designer.Read(Draft(workspace),"ItemsCopy").Columns.Select(column=>(column.Name,column.Type,column.PrimaryKey)));
            Assert.Contains(Designer.Objects(Draft(workspace)),item=>item.Name=="ItemsCopy2");
        }
        {
            //テーブルの名前変更
            Assert.Null((await operations.名前変更(items,"Products")).案内);
            var names=Designer.Objects(Draft(workspace)).Select(item=>item.Name).ToArray();
            Assert.Contains("Products",names);Assert.DoesNotContain("Items",names);
        }
        {
            Assert.Null((await operations.作成("","View","Recent")).案内);
            var view=Designer.Objects(Draft(workspace)).Single(item=>item.Name=="Recent");
            var rename=await operations.名前変更(view,"Latest");
            Assert.Null(rename.案内);
            Assert.DoesNotContain(Designer.Objects(Draft(workspace)),item=>item.Name=="Recent");
            Assert.Null((await operations.作成("","View","Recent")).案内);
        }
        {
            //ビューのコピーは定義ごと別名で作る
            var view=Designer.Objects(Draft(workspace)).Single(item=>item.Name=="Recent");
            Assert.Null((await operations.コピー(view)).案内);
            Assert.Contains(Designer.Objects(Draft(workspace)),item=>item.Name=="RecentCopy"&&item.Kind=="View");
        }
    }
    [Fact]public async Task RemovesTablesViewsAndFunctionsFromTheDraftOnly(){
        await using var workspace=await Workspace(ManagementPermission.All);
        var operations=Operations(workspace);
        Assert.Null((await operations.作成("","Table","DraftOnly")).案内);
        Assert.Null((await operations.作成("dbo0","View","Recent")).案内);
        Assert.Null((await operations.作成("dbo0","Function","Total")).案内);
        foreach(var name in new[]{"Items","DraftOnly","Recent","Total"}){
            var item=Designer.Objects(Draft(workspace)).Single(item=>item.Name==name);
            Assert.Null((await operations.削除(item)).案内);
            Assert.DoesNotContain(Designer.Objects(Draft(workspace)),candidate=>candidate.Id==item.Id);
        }
        Assert.Equal("Items",Assert.Single(Draft(workspace).Migration!.DroppedTables));
        Assert.Null((await operations.作成("dbo0","View","Recent")).案内);
        Assert.Null((await operations.作成("dbo0","Function","Total")).案内);
    }
    [Fact]public async Task ViewUsersCannotChangeTheDraftFromTheTree(){
        await using var workspace=await Workspace(ManagementPermission.View);
        var before=Draft(workspace);
        var result=await Operations(workspace).作成("","Table","Orders");
        Assert.NotNull(result.案内);Assert.Same(before,Draft(workspace));
    }
    [Fact]public async Task TreeChangesViewColumnsAndFunctionParametersInTheDatabaseDraft(){
        await using var workspace=await Workspace(ManagementPermission.All);
        var operations=Operations(workspace);
        Assert.Null((await operations.作成("dbo0","View","Recent")).案内);
        Assert.Null((await operations.作成("dbo0","Function","Total")).案内);
        var view=Designer.Objects(Draft(workspace)).Single(item=>item.Kind=="View");
        var recent=Designer.ReadObjectDefinition(Draft(workspace),view);
        var columns=new ツリー列編集要求(recent,[..recent.Columns,new("Label","string",false)]);
        Assert.True((await (Task<ObjectDefinitionEdit>)Invoke(workspace,"ツリー列確定時Async","db",columns)!).Succeeded);
        Assert.Equal(new[]{"Id","Label"},Designer.ReadQuery(Draft(workspace),"dbo0.Recent").Columns.Select(column=>column.Name));
        var function=Designer.Objects(Draft(workspace)).Single(item=>item.Kind=="ScalarFunction");
        var total=Designer.ReadObjectDefinition(Draft(workspace),function);
        var argument=new ツリー列編集要求(total,total.Columns){引数一覧=[new("quantity","long")],戻り型="long"};
        Assert.True((await (Task<ObjectDefinitionEdit>)Invoke(workspace,"ツリー列確定時Async","db",argument)!).Succeeded);
        var updated=Designer.ReadQuery(Draft(workspace),"dbo0.Total");
        Assert.Equal(new ParameterDesign("quantity","long"),Assert.Single(updated.Parameters));
        Assert.Equal("long",updated.ReturnType);
        var columnsBefore=Designer.ReadObjectDefinition(Draft(workspace),function);
        var tableRequest=new ツリー列編集要求(columnsBefore,[new("Amount","long",false)]);
        Assert.True((await (Task<ObjectDefinitionEdit>)Invoke(workspace,"ツリー列確定時Async","db",tableRequest)!).Succeeded);
        var table=Designer.ReadQuery(Draft(workspace),"dbo0.Total");
        Assert.Equal(DatabaseQueryKind.TableFunction,table.Kind);
        Assert.Equal("Amount",Assert.Single(table.Columns).Name);
        Assert.Equal(updated.Parameters,table.Parameters);
        Assert.Contains("Set.Empty",table.Body);
        var shape=Designer.Objects(Draft(workspace)).Single(item=>item.Name=="Total");
        var scalarRequest=new ツリー列編集要求(Designer.ReadObjectDefinition(Draft(workspace),shape),[]);
        Assert.True((await (Task<ObjectDefinitionEdit>)Invoke(workspace,"ツリー列確定時Async","db",scalarRequest)!).Succeeded);
        var scalar=Designer.ReadQuery(Draft(workspace),"dbo0.Total");
        Assert.Equal(DatabaseQueryKind.ScalarFunction,scalar.Kind);
        Assert.Equal("int",scalar.ReturnType);
        Assert.Equal(updated.Parameters,scalar.Parameters);
        Assert.Contains("return default;",scalar.Body);
    }
    [Fact]public async Task CategoryListShowsAddRowsAndRowButtonsButNoCategoryMenu(){
        var calls=new List<string>();
        var operations=new ツリー対象操作{
            作成=(scope,kind,name)=>{calls.Add("create:"+scope+":"+kind+":"+name);return Task.FromResult(ツリー操作結果.成功);},
            スキーマ作成=name=>Task.FromResult(ツリー操作結果.成功),
            名前変更=(item,name)=>{calls.Add("rename:"+item.Name+":"+name);return Task.FromResult(ツリー操作結果.成功);},
            コピー=item=>Task.FromResult(ツリー操作結果.成功),
            削除=item=>Task.FromResult(new ツリー操作結果("unsupported",true)),
        };
        var item=new CatalogObject("Items","Items","","Table","Items");
        using var services=new ServiceCollection().AddLogging().AddSingleton<IJSRuntime,StaticRenderScripts>().BuildServiceProvider();
        await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            var parameters=new Dictionary<string,object?>{{"オブジェクト",new[]{item}},{"編集可能か",true},{"対象操作",operations},{"DBID","db"}};
            var html=System.Net.WebUtility.HtmlDecode((await renderer.RenderComponentAsync<所属オブジェクト一覧>(ParameterView.FromDictionary(parameters))).ToHtmlString());
            //まだ1件もないビュー・関数の分類も、追加行の置き場所として表示する
            Assert.Contains("テーブルを追加",html);Assert.Contains("ビューを追加",html);Assert.Contains("関数を追加",html);
            //分類の行にも操作メニューのボタンを出す(生成コードの表示)。追加の「＋」ボタンは出さない
            Assert.Contains("data-メニュー起点=\"group:",html);
            //作成したテーブルの右端に名前変更・コピー・削除
            Assert.Contains("aria-label=\"Itemsの名前を変更\"",html);Assert.Contains("aria-label=\"Itemsをコピー\"",html);Assert.Contains("aria-label=\"Itemsを削除\"",html);
            //閲覧だけなら追加行も右端のボタンも出さない
            parameters["編集可能か"]=false;
            var readOnly=System.Net.WebUtility.HtmlDecode((await renderer.RenderComponentAsync<所属オブジェクト一覧>(ParameterView.FromDictionary(parameters))).ToHtmlString());
            Assert.DoesNotContain("テーブルを追加",readOnly);Assert.DoesNotContain("Itemsを削除",readOnly);
        });
    }
    private static ツリー対象操作 Operations(管理ワークスペース workspace)=>(ツリー対象操作)Invoke(workspace,"ツリー操作","db")!;
    private static Definition Draft(管理ワークスペース workspace)=>Get<Dictionary<string,Seamless.Management.Components.文書状態>>(workspace,"文書一覧")["db"].定義;
    private static async Task<管理ワークスペース> Workspace(ManagementPermission permission){
        var workspace=new 管理ワークスペース();Property(workspace,"管理操作",DispatchProxy.Create<IManagementOperations,UnifiedTreeMenus.InitializationOperations>());await(Task)Invoke(workspace,"OnInitializedAsync")!;Property(workspace,"利用者",new UserInfo("owner",[new("DB",permission)],false));
        Set(workspace,"DB一覧",new[]{new CatalogDatabase("db","DB","db",1,"",false)});var definition=Designer.AddSchema(Designer.Apply(Designer.Empty("db","DB"),new("Items","",[new("Id","int",true)],[])),"dbo0");Get<Dictionary<string,Seamless.Management.Components.文書状態>>(workspace,"文書一覧").Add("db",new(new("db","DB","db",1,"",false),new(definition,1,"owner",DateTimeOffset.UtcNow),null));Set(workspace,"選択DBID","db");return workspace;
    }
    private static object? Invoke(object target,string name,params object[] args)=>target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public)!.Invoke(target,args);
    private static T Get<T>(object target,string name)=>(T)target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(target)!;
    private static void Set(object target,string name,object? value)=>target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(target,value);
    private static void Property(object target,string name,object? value)=>target.GetType().GetProperty(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!.SetValue(target,value);
}
