using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Seamless.Management;
using Seamless.Management.Components;
using Designer=Seamless.Management.DatabaseDesigner;

namespace Management.Tests;

public sealed class TreeColumnEditing{
    [Fact]public async Task AddingAColumnUsesTheDesignerDraftAndClearsOnlyCommittedInput(){
        await using var fixture=await Fixture.Create();await fixture.Open();
        var editing=await fixture.Html();Assert.Contains("data-column-editor",editing);Assert.Contains("data-column-name-input",editing);
        Assert.True(fixture.Editor.未保存か);fixture.Set("列名","Notes");fixture.Set("列型","string?");fixture.Set("列式","\"initial\"");
        await fixture.Invoke("確定Async");Assert.False(fixture.Editor.未保存か);
        Assert.Contains(Designer.ReadObjectDefinition(fixture.Latest,Target()).Columns,column=>column.Name=="Notes"&&column.Type=="string?"&&!column.PrimaryKey);
        Assert.Contains(fixture.Latest.Migration!.Tables.Single(table=>table.Target=="Item").Columns,column=>column.Target=="Notes"&&column.Expression=="\"initial\"");
        var committed=await fixture.Html();Assert.Contains("data-列名=\"Notes\"",committed);Assert.DoesNotContain("data-column-editor",committed);Assert.True(fixture.Notifications>=2);
    }
    [Fact]public async Task EmptyNameCancelsWithoutChangingTheDraft(){
        await using var fixture=await Fixture.Create();var original=fixture.Latest;await fixture.Open();fixture.Set("列名","  ");await fixture.Invoke("確定Async");
        Assert.Same(original,fixture.Latest);Assert.False(fixture.Editor.未保存か);Assert.Equal(0,fixture.Writes);
    }
    [Fact]public async Task EditingReplacesTheSelectedRowAndCancelRestoresIt(){
        await using var fixture=await Fixture.Create();
        var column=Designer.ReadObjectDefinition(fixture.Latest,Target()).Columns.Single(item=>item.Name=="Title");
        await fixture.Edit(column);
        var editing=await fixture.Html();Assert.Contains("data-column-editor",editing);Assert.DoesNotContain("data-列名=\"Title\"",editing);Assert.Contains("data-列名=\"Id\"",editing);
        fixture.Set("列名","Changed");await fixture.Invoke("取消Async");
        Assert.Contains("data-列名=\"Title\"",await fixture.Html());Assert.Equal(0,fixture.Writes);
    }
    [Fact]public async Task ReorderingOnlyChangesTheDisplayedOrder(){
        await using var fixture=await Fixture.Create();var before=fixture.Latest;
        Assert.Contains("draggable=\"true\"",await fixture.Html());
        fixture.Set("移動元","Title");await fixture.Move("Id");
        var html=await fixture.Html();Assert.True(html.IndexOf("data-列名=\"Title\"",StringComparison.Ordinal)<html.IndexOf("data-列名=\"Id\"",StringComparison.Ordinal));
        Assert.Same(before,fixture.Latest);Assert.Equal(0,fixture.Writes);
    }
    [Fact]public async Task DuplicateNameKeepsAllEnteredValuesAndShowsTheDiagnostic(){
        await using var fixture=await Fixture.Create();await fixture.Open();fixture.Set("列名","Id");fixture.Set("列型","long");fixture.Set("主キーか",true);await fixture.Invoke("確定Async");
        Assert.True(fixture.Editor.未保存か);Assert.Equal("Id",fixture.Get<string>("列名"));Assert.Equal("long",fixture.Get<string>("列型"));Assert.True(fixture.Get<bool>("主キーか"));Assert.Contains("重複",fixture.Get<string>("案内"));
    }
    [Fact]public async Task ConcurrentEditsToTheSameObjectKeepPendingInputAndRejectTheStaleBaseline(){
        await using var fixture=await Fixture.Create();await fixture.Open();fixture.Set("列名","Pending");
        var baseline=Designer.ReadObjectDefinition(fixture.Latest,Target());fixture.Latest=Designer.ApplyObjectStructure(fixture.Latest,baseline,baseline.Columns.Select(column=>column with{Type="long"}).ToArray(),baseline.Parameters,baseline.ReturnType).Definition!;
        await fixture.Invoke("確定Async");Assert.True(fixture.Editor.未保存か);Assert.Equal("Pending",fixture.Get<string>("列名"));Assert.DoesNotContain(Designer.ReadObjectDefinition(fixture.Latest,Target()).Columns,column=>column.Name=="Pending");
        var message=fixture.Get<string>("案内");Assert.Contains("Item",message);Assert.Contains("Pending",message);Assert.Contains("int",message);Assert.Contains("Esc",message);
    }
    private static CatalogObject Target()=>new("Item","Item","","Table","global::Sample.Tables.Item");
    private static Definition Definition()=>new("db","DB",DefinitionKind.Database,"Sample.Container","",new("1",[new("Definition.cs","namespace Sample{internal static partial class DatabaseGenerator;namespace Tables{public partial record struct Item(int Id,string Title);}}")],[]));
    private sealed class Fixture:IAsyncDisposable{
        private readonly ServiceProvider services;
        private readonly HtmlRenderer renderer;
        private Microsoft.AspNetCore.Components.Web.HtmlRendering.HtmlRootComponent root;
        internal ツリー列一覧 Editor=null!;
        internal Definition Latest=Definition();
        internal int Notifications,Writes;
        private Fixture(){this.services=new ServiceCollection().AddLogging().AddSingleton<IJSRuntime,NoScripts>().BuildServiceProvider();this.renderer=new(this.services,this.services.GetRequiredService<ILoggerFactory>());}
        internal static async Task<Fixture> Create(){
            var fixture=new Fixture();await fixture.renderer.Dispatcher.InvokeAsync(async()=>{fixture.root=await fixture.renderer.RenderComponentAsync<Host>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"Definition",fixture.Latest},{"Capture",(Action<ツリー列一覧>)(editor=>fixture.Editor=editor)},{"Commit",(Func<ツリー列編集要求,Task<ObjectDefinitionEdit>>)(request=>{fixture.Writes++;var result=Designer.ApplyObjectStructure(fixture.Latest,request.基準,request.列一覧,request.基準.Parameters,request.基準.ReturnType);if(result.Succeeded){fixture.Latest=result.Definition!;}return Task.FromResult(result);})},{"Changed",EventCallback.Factory.Create(fixture,()=>fixture.Notifications++)}}));});return fixture;
        }
        internal Task Open()=>this.renderer.Dispatcher.InvokeAsync(()=>this.Editor.追加開始Async());
        internal Task Edit(ColumnDesign column)=>this.renderer.Dispatcher.InvokeAsync(async()=>{await(Task)typeof(ツリー列一覧).GetMethod("編集開始",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(this.Editor,[column])!;typeof(ComponentBase).GetMethod("StateHasChanged",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(this.Editor,null);});
        internal Task Move(string target)=>this.renderer.Dispatcher.InvokeAsync(()=>{typeof(ツリー列一覧).GetMethod("列を移動",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(this.Editor,[target]);typeof(ComponentBase).GetMethod("StateHasChanged",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(this.Editor,null);});
        internal Task<string> Html()=>this.renderer.Dispatcher.InvokeAsync(()=>this.root.ToHtmlString());
        internal Task Invoke(string name)=>this.renderer.Dispatcher.InvokeAsync(async()=>{await(Task)typeof(ツリー列一覧).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(this.Editor,null)!;typeof(ComponentBase).GetMethod("StateHasChanged",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(this.Editor,null);});
        internal void Set(string name,object value)=>typeof(ツリー列一覧).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(this.Editor,value);
        internal T Get<T>(string name)=>(T)typeof(ツリー列一覧).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(this.Editor)!;
        public async ValueTask DisposeAsync(){await this.renderer.DisposeAsync();await this.services.DisposeAsync();}
    }
    public sealed class Host:ComponentBase{
        [Parameter]public Definition Definition{get;set;}=default!;
        [Parameter]public Action<ツリー列一覧> Capture{get;set;}=default!;
        [Parameter]public Func<ツリー列編集要求,Task<ObjectDefinitionEdit>> Commit{get;set;}=default!;
        [Parameter]public EventCallback Changed{get;set;}
        protected override void BuildRenderTree(RenderTreeBuilder builder){builder.OpenComponent<ツリー列一覧>(0);builder.AddAttribute(1,"定義",this.Definition);builder.AddAttribute(2,"対象",Target());builder.AddAttribute(3,"表示中か",true);builder.AddAttribute(4,"編集可能か",true);builder.AddAttribute(5,"確定時",this.Commit);builder.AddAttribute(6,"未保存変更時",this.Changed);builder.AddComponentReferenceCapture(7,editor=>this.Capture((ツリー列一覧)editor));builder.CloseComponent();}
    }
    private sealed class NoScripts:IJSRuntime{
        public NoScripts(){}
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier,object?[]? args)=>throw new InvalidOperationException("Static rendering must not run browser scripts.");
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier,CancellationToken cancellationToken,object?[]? args)=>this.InvokeAsync<TValue>(identifier,args);
    }
}
