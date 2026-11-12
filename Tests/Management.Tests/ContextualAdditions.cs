using System.Collections;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using System.Text.RegularExpressions;
using Seamless.Management;
using Seamless.Management.Components;
using Designer=Seamless.Management.DatabaseDesigner;

namespace Management.Tests;

public sealed class ContextualAdditions{
    [Fact]public async Task TableColumnCommandRequiresItsRenderedTreeAndKeepsTheDraftOnFailure(){
        var definition=Designer.Apply(Designer.Empty("db","Plans"),new("Items","",[new("Id","int",true),new("Column","int",false)],[]));
        var document=new global::Seamless.Management.Components.文書状態(new("db","Plans","db",1,"",false),new(definition,1,"owner",DateTimeOffset.UtcNow),null);
        var original=document.定義;
        var workspace=new 管理ワークスペース();
        Parameter(workspace,"利用者",new UserInfo("owner",[new("Plans",ManagementPermission.Edit)],false));
        Get<Dictionary<string,global::Seamless.Management.Components.文書状態>>(workspace,"文書一覧").Add("db",document);Set(workspace,"選択DBID","db");
        var target=Activator.CreateInstance(typeof(管理ワークスペース).GetNestedType("管理対象",BindingFlags.NonPublic)!,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,["表追加","db","Items","",null],null)!;
        var error=await Assert.ThrowsAsync<ManagementException>(async()=>await(Task)Invoke(workspace,"表追加編集Async",target,true)!);
        Assert.Equal("Busy",error.Code);
        Assert.Same(original,document.定義);
        var documents=Get<Dictionary<string,対象文書状態>>(workspace,"対象文書一覧");
        Assert.Empty(documents);
        Assert.Same(original,document.定義);
        Assert.Equal(2,Designer.Read(document.定義,"Items").Columns.Length);
    }

    [Fact]public async Task UserChildPlusAddsAnUnsavedPermissionAndBlocksWhileSaving(){
        var view=new 利用者管理ビュー();Set(view,"編集中か",true);Set(view,"新規か",true);
        var commands=Commands(view);
        await commands.実行Async("grant","editing");
        Assert.Single(Get<IList>(view,"権限一覧").Cast<object>());
        Set(view,"保存中か",true);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>commands.実行Async("grant","editing"));
        Assert.Single(Get<IList>(view,"権限一覧").Cast<object>());
    }

    [Fact]public async Task RoleChildPlusUsesTheSelectedRoleAndPreservesUnsavedEditing(){
        var view=new ロール管理ビュー();var scope=Guid.NewGuid();var role=Guid.NewGuid();
        Set(view,"DBID",scope);Set(view,"ロールID",role);
        var commands=Commands(view);
        await commands.実行Async("rule",role.ToString());
        Assert.Single(Get<IList>(view,"規則").Cast<object>());
        await commands.実行Async("binding",role.ToString());
        Assert.True(Get<bool>(view,"割当展開か"));
        Assert.Empty(Get<IList>(view,"割当").Cast<object>());
        await Assert.ThrowsAsync<InvalidOperationException>(()=>commands.実行Async("rule",Guid.NewGuid().ToString()));
        Assert.Single(Get<IList>(view,"規則").Cast<object>());
        Set(view,"編集ありか",false);Set(view,"処理中か",true);
        await (Task)Invoke(view,"ロール行選択Async",new AuthorizationRoleHeader(Guid.NewGuid(),"other"))!;
        Assert.Equal(role,Get<Guid?>(view,"ロールID"));Assert.Single(Get<IList>(view,"規則").Cast<object>());
    }

    [Fact]public async Task SettingsPlusOpensExistingOptionalInputsAndRespectsFixedProperties(){
        var view=new サーバー設定ビュー();var settings=new ManagementServerSettings();
        var snapshot=new ServerSettingsSnapshot(1,settings,settings,false,[nameof(ManagementServerSettings.ResponseWriteTimeout)],"isolated","",null,1,0,"http://127.0.0.1/");
        Set(view,"情報",snapshot);Set(view,"編集値",settings);
        var commands=Commands(view);
        Assert.DoesNotContain(commands.検索("settings",""),command=>command.ID=="timeout");
        await commands.実行Async("request","settings");
        Assert.True(Get<bool>(view,"通信予算展開か"));Assert.Same(settings,Get<ManagementServerSettings>(view,"編集値"));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>commands.実行Async("timeout","settings"));
    }

    [Fact]public async Task EmbeddedSettingsWithoutAvailableCommandsDoNotOpenAnEmptyMenu(){
        var view=new サーバー設定ビュー();var settings=new ManagementServerSettings();
        var snapshot=new ServerSettingsSnapshot(1,settings,settings,false,[],"isolated","",null,1,0,"http://127.0.0.1/"){
            ExecutionMode=ManagementExecutionMode.Embedded,
            UnavailableProperties=[nameof(ManagementServerSettings.MaximumBufferedRequestBytes),nameof(ManagementServerSettings.MaximumPendingWriteBytes),nameof(ManagementServerSettings.ResponseWriteTimeout),nameof(ManagementServerSettings.ExternalManagerOrigin)]
        };
        Set(view,"情報",snapshot);Set(view,"編集値",settings);
        var commands=Commands(view);Assert.Empty(commands.検索("settings",""));
        var host=new 追加メニューホスト();Parameter(host,"ID","settings");Parameter(host,"登録",commands);
        var request=new 対象操作要求("settings",default,0,0);
        Invoke(host,"開く時",request);Assert.Null(Get<対象操作要求?>(host,"要求"));
        using var provider=new ServiceCollection().AddLogging().AddSingleton<IJSRuntime,NoRenderScripts>().BuildServiceProvider();
        await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            var rendered=await renderer.RenderComponentAsync<追加メニューホスト>(ParameterView.FromDictionary(new Dictionary<string,object?>{
                ["ID"]="settings",["名前"]="Settings",["登録"]=commands,["実行時"]=EventCallback.Factory.Create<string>(new object(),(string ID)=>commands.実行Async(ID,"settings"))
            }));
            var html=rendered.ToHtmlString();var plus=Regex.Match(html,"<button\\b[^>]*>");
            Assert.True(plus.Success);Assert.Contains("disabled",plus.Value);Assert.DoesNotContain("role=\"dialog\"",html);
        });
        Set(view,"情報",snapshot with{ExecutionMode=ManagementExecutionMode.Server,UnavailableProperties=[]});
        Invoke(host,"開く時",request);Assert.Equal(request,Get<対象操作要求?>(host,"要求"));
        Set(view,"情報",snapshot);Invoke(host,"OnParametersSet");Assert.Null(Get<対象操作要求?>(host,"要求"));
        Assert.Same(settings,Get<ManagementServerSettings>(view,"編集値"));
    }

    [Fact]public async Task AuditPlusAddsSearchFieldsAndKeepsExistingConditionsVisible(){
        var view=new 監査ビュー();var commands=Commands(view);
        Assert.False((bool)Invoke(view,"条件表示か","database")!);
        await commands.実行Async("database","audit");
        Assert.True((bool)Invoke(view,"条件表示か","database")!);
        Assert.Empty(Get<string>(view,"DB条件"));
        Assert.DoesNotContain(commands.検索("audit",""),command=>command.ID=="database");
        Set(view,"利用者条件","existing-owner");
        Assert.True((bool)Invoke(view,"条件表示か","user")!);
        Set(view,"読込中か",true);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>commands.実行Async("period","audit"));
        Assert.False((bool)Invoke(view,"条件表示か","period")!);
    }

    [Fact]public async Task SharedHandlesAndMenuItemsHaveNonSubmittingButtonTypes(){
        using var provider=new ServiceCollection().AddLogging().AddSingleton<IJSRuntime,NoRenderScripts>().BuildServiceProvider();
        await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            var handles=await renderer.RenderComponentAsync<対象操作ホスト>(ParameterView.FromDictionary(new Dictionary<string,object?>{
                ["ID"]="test",["名前"]="test",["追加時"]=EventCallback.Factory.Create(new object(),()=>{})
            }));
            var commands=new コマンド登録<string>([
                new("direct","Add",_=>true,_=>true,_=>Task.CompletedTask,[]),
                new("nested","Nested",_=>true,_=>true,_=>Task.CompletedTask,[],グループ:"More")
            ]);
            var menu=await renderer.RenderComponentAsync<操作メニュー<string>>(ParameterView.FromDictionary(new Dictionary<string,object?>{["登録"]=commands,["対象"]="test"}));
            foreach(var html in new[]{handles.ToHtmlString(),menu.ToHtmlString()}){
                var buttons=Regex.Matches(html,"<button\\b[^>]*>");
                Assert.NotEmpty(buttons);
                Assert.All(buttons.Cast<Match>(),button=>Assert.Contains("type=\"button\"",button.Value));
            }
        });
    }

    [Fact]public async Task AContextChangeOrCancellationKeepsTheMenuTargetAndAllowsRetry(){
        var available=false;var calls=0;
        var commands=new コマンド登録<string>([new("add","Add",_=>true,_=>available,_=>{calls++;return Task.CompletedTask;},[])]);
        var host=new 追加メニューホスト();var subject=new 対象操作要求("editing",default,0,0);
        Parameter(host,"実行時",EventCallback.Factory.Create<string>(new object(),(string command)=>commands.実行Async(command,"editing")));
        Set(host,"要求",subject);
        await (Task)Invoke(host,"実行Async","add")!;
        Assert.Equal(subject,Get<対象操作要求?>(host,"要求"));Assert.True(Get<bool>(host,"エラーか"));Assert.NotEmpty(Get<string>(host,"案内"));Assert.Equal(0,calls);
        available=true;
        await (Task)Invoke(host,"実行Async","add")!;
        Assert.Null(Get<対象操作要求?>(host,"要求"));Assert.Empty(Get<string>(host,"案内"));Assert.Equal(1,calls);
        Set(host,"要求",subject);
        Parameter(host,"実行時",EventCallback.Factory.Create<string>(new object(),(string _)=>Task.FromCanceled(new CancellationToken(true))));
        await (Task)Invoke(host,"実行Async","add")!;
        Assert.Equal(subject,Get<対象操作要求?>(host,"要求"));Assert.False(Get<bool>(host,"エラーか"));Assert.NotEmpty(Get<string>(host,"案内"));
        Parameter(host,"実行時",EventCallback.Factory.Create<string>(new object(),(string _)=>Task.FromException(new IOException("unexpected"))));
        await Assert.ThrowsAsync<IOException>(()=>(Task)Invoke(host,"実行Async","add")!);
        Assert.Equal(subject,Get<対象操作要求?>(host,"要求"));
    }

    private sealed class NoRenderScripts:IJSRuntime{
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier,object?[]? args)=>throw new InvalidOperationException("Interactive script ran during static rendering.");
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier,CancellationToken cancellationToken,object?[]? args)=>this.InvokeAsync<TValue>(identifier,args);
    }

    private static コマンド登録<string> Commands(object target)=>(コマンド登録<string>)target.GetType().GetProperty("追加コマンド",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(target)!;
    private static object? Invoke(object target,string name,params object?[] args)=>target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(target,args);
    private static T Get<T>(object target,string name)=>(T)target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(target)!;
    private static void Set(object target,string name,object? value)=>target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(target,value);
    private static void Parameter(object target,string name,object value)=>target.GetType().GetProperty(name)!.SetValue(target,value);
}
