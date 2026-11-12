using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Seamless.Management;
using Seamless.Management.Components;
using Seamless.Management.Server;

namespace Management.Tests;

public sealed class プロジェクト作成接続{
    [Fact]public async Task DB作成後の編集を専用プロジェクトへ含める(){
        var 保存先=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"created-edited-"+Guid.NewGuid().ToString("N"));
        using var ホスト=InProcessManagementHost.Create(Path.Combine(保存先,"store"));
        var 初期=Seamless.Management.DatabaseDesigner.Empty("created","Sales");
        var 下書き=await ホスト.SaveDraftAsync(new(初期,0,0));
        var 確認=await ホスト.PreviewAsync(new(初期.Id,下書き.Revision));
        Assert.True(確認.CanApply);
        Assert.Equal(OperationState.Succeeded,(await ホスト.ApplyAsync(new(初期.Id,下書き.Revision,確認.Token,Guid.NewGuid()))).State);
        var 公開=await ホスト.GetDefinitionAsync(初期.Id);
        var 文書=new Seamless.Management.Components.文書状態(new("db","Sales",初期.Id,1,"",false),公開,下書き);
        文書.定義=Seamless.Management.DatabaseDesigner.Apply(文書.定義,new("Items","",[new("Id","int",true),new("Name","string",false)],[]));
        await using var 画面=new 管理ワークスペース();
        var フラグ=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        var 型=typeof(管理ワークスペース);
        型.GetProperty("管理操作",フラグ)!.SetValue(画面,ホスト);
        型.GetField("作成完了定義",フラグ)!.SetValue(画面,初期);
        型.GetField("作成完了DB名",フラグ)!.SetValue(画面,"Sales");
        型.GetField("新プロジェクト保存先",フラグ)!.SetValue(画面,Path.Combine(保存先,"project"));
        var 文書一覧=(Dictionary<string,Seamless.Management.Components.文書状態>)型.GetField("文書一覧",フラグ)!.GetValue(画面)!;
        文書一覧.Add("db",文書);
        await (Task)型.GetMethod("プロジェクト生成実行Async",フラグ)!.Invoke(画面,null)!;
        Assert.False((bool)型.GetField("エラーか",フラグ)!.GetValue(画面)!);
        var プロジェクト=Assert.IsType<SourceProjectDocument>(型.GetField("作成済みプロジェクト",フラグ)!.GetValue(画面));
        Assert.Contains("Items",string.Join("\n",プロジェクト.Source!.Files.Select(ファイル=>ファイル.Text)));
        var 作業領域=SourceWorkspace.Open(Path.Combine(保存先,"project"));
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(文書.定義.Migration,ManagementJson.Options),
            System.Text.Json.JsonSerializer.Serialize(作業領域.Current.Definition.Migration,ManagementJson.Options));
        Assert.Equal(文書.基準版,作業領域.Current.BaseVersion);
        Assert.False(文書.未保管か);
        Assert.Equal(文書.定義.Hash(),(await ホスト.GetDraftAsync(初期.Id)).Definition.Hash());
        Assert.Equal(文書.定義.Hash(),作業領域.State.SavedDraftHash);
    }
    [Fact]public async Task 下書き反映のエラーをファイル同期で消さない(){
        var 保存先=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"draft-message-"+Guid.NewGuid().ToString("N"));
        using var ホスト=InProcessManagementHost.Create(保存先);
        var ソース=Seamless.Management.DatabaseDesigner.Empty("db","Sales").Source;
        var プロジェクト=new SourceProjectDocument("unknown",Path.Combine(保存先,"Sales.csproj"),["net10.0"],"net10.0",["UserCode"],ソース){Database=new("db","Sales",1,0)};
        await using var 画面=new プロジェクト作業領域();
        var フラグ=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        var 型=typeof(プロジェクト作業領域);
        型.GetProperty("作成済みプロジェクト")!.SetValue(画面,プロジェクト);
        型.GetProperty("管理操作",フラグ)!.SetValue(画面,ホスト);
        型.GetMethod("OnParametersSet",フラグ)!.Invoke(画面,null);
        var 文書=型.GetField("現在",フラグ)!.GetValue(画面)!;
        await (Task)型.GetMethod("DB下書きへ反映時Async",フラグ)!.Invoke(画面,[文書])!;
        var 案内=文書.GetType().GetField("下書き案内")!;
        Assert.Contains("ソースプロジェクト",Assert.IsType<string>(案内.GetValue(文書)));
        Assert.True((bool)文書.GetType().GetField("下書きエラーか")!.GetValue(文書)!);
        var 反映結果=案内.GetValue(文書);
        await (Task)型.GetMethod("実行Async",フラグ)!.Invoke(画面,[new Func<Task>(()=>Task.CompletedTask),false,false])!;
        Assert.Equal(反映結果,案内.GetValue(文書));
    }
    [Theory][InlineData(null)][InlineData("参照DLLの型補完は、SDKで復元・ビルド後に同期すると利用できます。")]
    public async Task 作成応答から場所と名前空間とコードを表示する(string? 型補完案内){
        var 保存先=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"created-view-"+Guid.NewGuid().ToString("N"));
        using var ホスト=InProcessManagementHost.Create(保存先);
        var 登録=new ServiceCollection().AddLogging().AddSingleton<IManagementOperations>(ホスト).AddSingleton<IJSRuntime,試験スクリプト>();
        await using var サービス=登録.BuildServiceProvider();
        await using var 描画=new HtmlRenderer(サービス,サービス.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>());
        var ソース=Seamless.Management.DatabaseDesigner.Empty("created","Sales").Source;
        var プロジェクト=new SourceProjectDocument("created",Path.Combine(保存先,"Sales.csproj"),["net10.0"],"net10.0",["UserCode"],ソース){Database=new("created","Sales",1,3),TypeCompletionDiagnostic=型補完案内};
        await 描画.Dispatcher.InvokeAsync(async()=>{
            var 初期=await 描画.RenderComponentAsync<プロジェクト作業領域>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"表示中か",true}}));
            Assert.DoesNotContain("Sales.csproj",初期.ToHtmlString());
            var 未対応=await 描画.RenderComponentAsync<プロジェクト作業領域>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"表示中か",true},{"作成済みプロジェクト",プロジェクト}}));
            Assert.DoesNotContain("DB下書きへ反映",未対応.ToHtmlString());
            var 画面=await 描画.RenderComponentAsync<プロジェクト作業領域>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"表示中か",true},{"作成済みプロジェクト",プロジェクト},{"下書き反映可能か",true}}));
            var HTML=System.Net.WebUtility.HtmlDecode(画面.ToHtmlString());
            Assert.Contains("Sales.csproj",HTML);
            Assert.Contains("UserCode",HTML);
            Assert.Contains("DatabaseGenerator",HTML);
            Assert.Contains("DB下書きへ反映",HTML);
            Assert.Contains("保管記録 r3",HTML);
            if(型補完案内 is not null){Assert.Contains(型補完案内,HTML);Assert.Contains("aria-label=\"型補完の状態\"",HTML);}
            else{Assert.DoesNotContain("aria-label=\"型補完の状態\"",HTML);}
        });
    }
    private sealed class 試験スクリプト:IJSRuntime{
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier,object?[]? args)=>throw new InvalidOperationException(identifier);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier,CancellationToken cancellationToken,object?[]? args)=>throw new InvalidOperationException(identifier);
    }
}
