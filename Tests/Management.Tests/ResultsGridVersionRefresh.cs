using System.Collections;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Seamless.Management;
using Grid=Seamless.Management.Components.結果グリッド;

namespace Management.Tests;

public sealed class ResultsGridVersionRefresh{
    [Fact]public async Task PublishedVersionChangeRefreshesColumnsAndRowsOnlyOnce(){
        await using var fixture=new Fixture();await fixture.Run(async()=>{var grid=await fixture.Render();Assert.Equal(1,fixture.Reads);await grid.SetParametersAsync(Parameters(2));Assert.Equal(2,fixture.Reads);Assert.Equal(2L,Get(grid,"版"));Assert.Equal(3,((IList)Get(grid,"列一覧")!).Count);await grid.SetParametersAsync(Parameters(2));Assert.Equal(2,fixture.Reads);});
    }
    [Fact]public async Task NewVersionPreservesPendingInsertUpdateAndDeleteUntilExplicitDiscard(){
        await using var fixture=new Fixture();await fixture.Run(async()=>{
            var grid=await fixture.Render();var row=((IList)Get(grid,"行一覧")!)[0]!;var edits=(Dictionary<string,string>)row.GetType().GetProperty("Values")!.GetValue(row)!;edits["Value"]="99";row.GetType().GetProperty("削除予定か")!.SetValue(row,true);Invoke(grid,"行追加開始");
            await grid.SetParametersAsync(Parameters(2));Assert.Equal(1,fixture.Reads);Assert.Equal("99",edits["Value"]);Assert.True((bool)row.GetType().GetProperty("削除予定か")!.GetValue(row)!);Assert.Single((IList)Get(grid,"新規行")!);
            await grid.保存実行Async();await grid.再読込Async();Assert.Equal(0,fixture.Writes);Assert.Equal(1,fixture.Reads);Assert.Equal("99",edits["Value"]);
            await(Task)Invoke(grid,"破棄再読込Async")!;Assert.Equal(2,fixture.Reads);Assert.Equal(2L,Get(grid,"版"));Assert.Empty((IList)Get(grid,"新規行")!);Assert.False((bool)Get(grid,"版更新待ちか")!);
        });
    }
    [Fact]public async Task HiddenGridLoadsTheLatestVersionWhenItBecomesVisible(){
        await using var fixture=new Fixture();await fixture.Run(async()=>{var grid=await fixture.Render();await grid.SetParametersAsync(Parameters(2,false));Assert.Equal(1,fixture.Reads);await grid.SetParametersAsync(Parameters(2));Assert.Equal(2,fixture.Reads);Assert.Equal(2L,Get(grid,"版"));});
    }
    [Fact]public async Task LateOldVersionResponseCannotReplaceTheNewerPublishedPage(){
        await using var fixture=new Fixture();await fixture.Run(async()=>{
            var grid=await fixture.Render();var pending=new TaskCompletionSource<TableRows>(TaskCreationOptions.RunContinuationsAsynchronously);CancellationToken old=default;
            fixture.Operations.Read=(request,token)=>{if(request.ExpectedVersion==2){old=token;return pending.Task;}return Task.FromResult(Page(request.ExpectedVersion??1));};
            var loading=grid.SetParametersAsync(Parameters(2));await grid.SetParametersAsync(Parameters(3));Assert.True(old.IsCancellationRequested);pending.SetResult(Page(2));await loading;Assert.Equal(3L,Get(grid,"版"));
        });
    }
    [Fact]public async Task VersionChangeDuringAConfirmedSaveFinishesTheSaveAndThenRefreshes(){
        await using var fixture=new Fixture();await fixture.Run(async()=>{
            var grid=await fixture.Render();var row=((IList)Get(grid,"行一覧")!)[0]!;((Dictionary<string,string>)row.GetType().GetProperty("Values")!.GetValue(row)!)["Value"]="99";
            var pending=new TaskCompletionSource<TableRowCommit>(TaskCreationOptions.RunContinuationsAsynchronously);TableRowBatch? batch=null;fixture.Operations.Commit=value=>{batch=value;return pending.Task;};
            var saving=grid.保存実行Async();await grid.SetParametersAsync(Parameters(2));Assert.True((bool)Get(grid,"保存中か")!);pending.SetResult(new(1,batch!.OperationId,1));await saving;Assert.False((bool)Get(grid,"保存中か")!);Assert.Equal(2L,Get(grid,"版"));Assert.False((bool)Get(grid,"版更新待ちか")!);
        });
    }
    private static ParameterView Parameters(long version,bool visible=true)=>ParameterView.FromDictionary(new Dictionary<string,object?>{{"DB名","DB"},{"テーブル名","Rows"},{"公開版",version},{"表示中か",visible}});
    private static TableRows Page(long version){var columns=new List<CatalogField>{new("Id","int"),new("Value","int")};if(version>1){columns.Add(new("Note","string?"));}return new(version,[new(ManagementJson.Element(new{Id=1}),ManagementJson.Element(new{Id=1,Value=10,Note=(string?)null}),new('C',64))],false,columns.ToArray()){KeyMembers=[new("Id","int")]};}
    private static object? Get(Grid grid,string name)=>typeof(Grid).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(grid);
    private static object? Invoke(Grid grid,string name)=>typeof(Grid).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(grid,null);
    private sealed class Fixture:IAsyncDisposable{
        private readonly ServiceProvider services;
        private readonly HtmlRenderer renderer;
        private readonly Capture capture=new();
        internal readonly OperationsProxy Operations;
        internal int Reads,Writes;
        internal Fixture(){var operations=DispatchProxy.Create<IManagementOperations,OperationsProxy>();this.Operations=(OperationsProxy)(object)operations;this.Operations.Read=(request,_)=>{this.Reads++;return Task.FromResult(Page(request.ExpectedVersion??1));};this.Operations.Commit=batch=>{this.Writes++;return Task.FromResult(new TableRowCommit(batch.ExpectedVersion,batch.OperationId,batch.Changes.Length));};this.services=new ServiceCollection().AddLogging().AddSingleton<IManagementOperations>(operations).AddSingleton(this.capture).AddSingleton<IJSRuntime,StaticRenderScripts>().BuildServiceProvider();this.renderer=new(this.services,this.services.GetRequiredService<ILoggerFactory>());}
        internal Task Run(Func<Task> action)=>this.renderer.Dispatcher.InvokeAsync(action);
        internal async Task<Grid> Render(){await this.renderer.RenderComponentAsync<TestGrid>(Parameters(1));return this.capture.Grid!;}
        public async ValueTask DisposeAsync(){await this.renderer.DisposeAsync();await this.services.DisposeAsync();}
    }
    public sealed class Capture{public Grid? Grid;}
    public sealed class TestGrid:Grid{[Inject]public Capture Capture{get;set;}=default!;protected override void OnInitialized(){this.Capture.Grid=this;}}
    public class OperationsProxy:DispatchProxy{
        internal Func<TableRowsRequest,CancellationToken,Task<TableRows>> Read=default!;
        internal Func<TableRowBatch,Task<TableRowCommit>> Commit=default!;
        protected override object? Invoke(MethodInfo? method,object?[]? args)=>method!.Name switch{nameof(IManagementOperations.ReadRowsAsync)=>this.Read((TableRowsRequest)args![0]!, (CancellationToken)args[1]!),nameof(IManagementOperations.CommitRowsAsync)=>this.Commit((TableRowBatch)args![0]!),_=>throw new NotSupportedException(method.Name)};
    }
}
