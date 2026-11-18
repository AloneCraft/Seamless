using System.Collections;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Seamless.Management;
using Grid=Seamless.Management.Components.結果グリッド;

namespace Management.Tests;

public sealed class ResultsGridOperations{
    [Theory][InlineData(false)][InlineData(true)]
    public async Task RowActionsUseAccessibleSvgIcons(bool newRow){
        var operations=new GridOperations{Read=(_,_)=>Task.FromResult(Page([Row(1,10)]))};
        using var provider=Services(operations);
        await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            var rendered=await renderer.RenderComponentAsync<TestGrid>(Parameters("First"));
            var label="削除";
            var glyph="🗑";
            if(newRow){
                Invoke(operations.Component!,"行追加開始");
                ((TestGrid)operations.Component!).Refresh();
                label="取消";
                glyph="✕";
            }
            var html=System.Net.WebUtility.HtmlDecode(rendered.ToHtmlString());
            var button=System.Text.RegularExpressions.Regex.Match(html,"<button\\b[^>]*\\btitle=\""+label+"\"[^>]*>(.*?)</button>",System.Text.RegularExpressions.RegexOptions.Singleline);
            Assert.True(button.Success);
            Assert.Contains("<svg",button.Value);
            Assert.Contains("aria-label=\""+label+"\"",button.Value);
            Assert.Contains("data-操作アイコン=\""+label+"\"",button.Value);
            Assert.DoesNotContain(glyph,button.Value);
        });
    }
    [Fact]public async Task ReadonlyRowsStayVisibleAndCannotStartOrSaveEdits(){
        var operations=new GridOperations{Read=(_,_)=>Task.FromResult(Page([Row(1,10)]))};using var provider=Services(operations);await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            var parameters=ParameterView.FromDictionary(new Dictionary<string,object?>{{"DB名","First"},{"テーブル名","Rows"},{"編集可能か",false}});var rendered=await renderer.RenderComponentAsync<TestGrid>(parameters);var grid=operations.Component!;var html=System.Net.WebUtility.HtmlDecode(rendered.ToHtmlString());Assert.Contains("readonly",html);Assert.DoesNotContain("行を追加</",html);
            Invoke(grid,"行追加開始");Assert.Empty((IList)Get(grid,"新規行")!);await grid.保存実行Async();((TestGrid)grid).Refresh();Assert.Null(operations.Saved);Assert.Contains("行を変更する権限がありません。",System.Net.WebUtility.HtmlDecode(rendered.ToHtmlString()));
        });
    }
    [Fact]public async Task InitialReadFailureShowsRetryAndOnlySuccessfulEmptyMetadataShowsMissingColumns(){
        var operations=new GridOperations{Read=(_,_)=>Task.FromException<TableRows>(new ManagementException("BuildMismatch","Database DLL mismatch"))};
        using var provider=Services(operations);
        await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            var rendered=await renderer.RenderComponentAsync<TestGrid>(Parameters("First"));
            var html=System.Net.WebUtility.HtmlDecode(rendered.ToHtmlString());
            Assert.Contains("Database DLL mismatch",html);
            Assert.Contains("再読込",html);
            Assert.DoesNotContain("列が定義されていません",html);
            operations.Read=(_,_)=>Task.FromResult(Page([]) with{Columns=[]});
            await operations.Component!.再読込Async();
            html=System.Net.WebUtility.HtmlDecode(rendered.ToHtmlString());
            Assert.DoesNotContain("Database DLL mismatch",html);
            Assert.Contains("列が定義されていません",html);
        });
    }
    [Fact]public async Task TypingInTheLastRowCreatesAnEditAndKeepsOneBlankRowOutsideTheSaveBatch(){
        var operations=new GridOperations{Read=(_,_)=>Task.FromResult(Page([]))};
        using var provider=Services(operations);
        await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            var rendered=await renderer.RenderComponentAsync<TestGrid>(Parameters("First"));
            var grid=operations.Component!;
            Assert.Contains("data-grid-placeholder",rendered.ToHtmlString());
            await grid.保存実行Async();
            Assert.Null(operations.Saved);
            var first=Get(grid,"次の新規行");
            Invoke(grid,"新規行セル変更",first,"id","1");
            Invoke(grid,"新規行セル変更",first,"id","");
            await grid.保存実行Async();
            Assert.Null(operations.Saved);
            first=Get(grid,"次の新規行");
            Invoke(grid,"新規行セル変更",first,"id","1");
            Invoke(grid,"新規行セル変更",first,"value","10");
            var second=Get(grid,"次の新規行");
            Assert.NotSame(first,second);
            Invoke(grid,"新規行セル変更",second,"id","2");
            Invoke(grid,"新規行セル変更",second,"value","20");
            await grid.再読込Async();
            await grid.保存実行Async();
            Assert.Collection(operations.Saved!.Changes,
                row=>Assert.Equal(10,row.Values!.Value.GetProperty("value").GetInt32()),
                row=>Assert.Equal(20,row.Values!.Value.GetProperty("value").GetInt32()));
            Assert.Contains("data-grid-placeholder",rendered.ToHtmlString());
        });
    }
    [Fact]public async Task EmptyTableSupportsInsertionAndKeyReplacementWithVisibleConfirmation(){
        var operations=new GridOperations();var empty=Page([]);operations.Read=(_,_)=>Task.FromResult(empty);
        using var provider=Services(operations);await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            var rendered=await renderer.RenderComponentAsync<TestGrid>(Parameters("First"));var grid=operations.Component!;
            Assert.Contains("PK",rendered.ToHtmlString());Assert.Contains("0 行",System.Net.WebUtility.HtmlDecode(rendered.ToHtmlString()));Assert.Contains("role=\"grid\"",rendered.ToHtmlString());Assert.Contains("class=\"データ表\"",System.Net.WebUtility.HtmlDecode(rendered.ToHtmlString()));
            Invoke(grid,"行追加開始");var added=((IList)Get(grid,"新規行")!)[0]!;
            var values=(Dictionary<string,string>)added.GetType().GetProperty("Values")!.GetValue(added)!;values["id"]="1";values["value"]="10";
            operations.Read=(_,_)=>Task.FromResult(Page([Row(1,10)]));await grid.保存実行Async();
            Assert.Equal(TableRowChangeKind.Insert,Assert.Single(operations.Saved!.Changes).Kind);Assert.Contains("1 行の変更を保存しました",System.Net.WebUtility.HtmlDecode(rendered.ToHtmlString()));
            var row=((IList)Get(grid,"行一覧")!)[0]!;((Dictionary<string,string>)row.GetType().GetProperty("Values")!.GetValue(row)!)["id"]="2";
            operations.Read=(_,_)=>Task.FromResult(Page([Row(2,10)]));await grid.保存実行Async();
            var replacement=Assert.Single(operations.Saved!.Changes);Assert.Equal(TableRowChangeKind.Replace,replacement.Kind);
            Assert.Equal(1,replacement.Key!.Value.GetProperty("id").GetInt32());Assert.Equal(2,replacement.Values!.Value.GetProperty("id").GetInt32());
            Assert.Contains("aria-label=\"&#x884C;1 id\"",rendered.ToHtmlString());
        });
    }
    [Fact]public async Task RestoringOriginalCellValueRemovesThatEditWithoutLosingOtherChanges(){
        var operations=new GridOperations{Read=(_,_)=>Task.FromResult(Page([Row(1,10)]))};
        using var provider=Services(operations);await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            await renderer.RenderComponentAsync<TestGrid>(Parameters("First"));var grid=operations.Component!;var row=((IList)Get(grid,"行一覧")!)[0]!;
            var change=typeof(Grid).GetMethod("行セル変更",BindingFlags.Instance|BindingFlags.NonPublic)!;
            change.Invoke(grid,[row,"id","2"]);change.Invoke(grid,[row,"value","20"]);change.Invoke(grid,[row,"value","10"]);
            var edits=(Dictionary<string,string>)row.GetType().GetProperty("Values")!.GetValue(row)!;Assert.Single(edits);Assert.Equal("2",edits["id"]);
            change.Invoke(grid,[row,"id","1"]);Assert.Empty(edits);
        });
    }
    [Fact]public async Task NextPageUsesTheCursorAndLateResponsesCannotCrossDatabases(){
        var cursor=new TableRowCursor(1,new string('A',64));
        var operations=new GridOperations{Read=(_,_)=>Task.FromResult(Page([Row(1,10)]) with{HasMore=true,NextCursor=cursor})};
        using var provider=Services(operations);await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            var rendered=await renderer.RenderComponentAsync<TestGrid>(Parameters("First"));var grid=operations.Component!;
            operations.Read=(request,_)=>{Assert.Equal(cursor,request.Cursor);Assert.Null(request.Key);Assert.Equal(1,request.ExpectedVersion);return Task.FromResult(Page([Row(2,20)]) with{HasMore=true,NextCursor=new(2,new string('B',64))});};
            await grid.次ページ読込Async();Assert.Equal(2,((IList)Get(grid,"行一覧")!).Count);
            var pending=new TaskCompletionSource<TableRows>(TaskCreationOptions.RunContinuationsAsynchronously);CancellationToken oldToken=default;
            operations.Read=(_,token)=>{oldToken=token;return pending.Task;};var loading=grid.次ページ読込Async();
            operations.Read=(request,_)=>{Assert.Equal("Second",request.Database);Assert.Null(request.Cursor);return Task.FromResult(Page([Row(9,90)]));};
            await grid.SetParametersAsync(Parameters("Second"));Assert.True(oldToken.IsCancellationRequested);
            pending.SetResult(Page([Row(3,30)]));await loading;
            Assert.Single((IList)Get(grid,"行一覧")!);Assert.Contains("value=\"90\"",rendered.ToHtmlString());Assert.DoesNotContain("value=\"30\"",rendered.ToHtmlString());
        });
    }
    [Fact]public async Task FailedNextPagePreservesEditsAndCanBeRetried(){
        var cursor=new TableRowCursor(1,new string('A',64));
        var operations=new GridOperations{Read=(_,_)=>Task.FromResult(Page([Row(1,10)]) with{HasMore=true,NextCursor=cursor})};
        using var provider=Services(operations);await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            var rendered=await renderer.RenderComponentAsync<TestGrid>(Parameters("First"));var grid=operations.Component!;
            var row=((IList)Get(grid,"行一覧")!)[0]!;var edits=(Dictionary<string,string>)row.GetType().GetProperty("Values")!.GetValue(row)!;edits["value"]="99";
            operations.Read=(_,_)=>Task.FromException<TableRows>(new ManagementException("RowConflict","prefix changed"));await grid.次ページ読込Async();
            Assert.Equal("99",edits["value"]);Assert.Contains("prefix changed",rendered.ToHtmlString());
            operations.Read=(request,_)=>{Assert.Equal(cursor,request.Cursor);return Task.FromResult(Page([Row(2,20)]));};await grid.次ページ読込Async();
            Assert.Equal("99",edits["value"]);Assert.Equal(2,((IList)Get(grid,"行一覧")!).Count);Assert.DoesNotContain("prefix changed",rendered.ToHtmlString());
        });
    }
    [Fact]public async Task ReloadMergesOtherCellsAndKeepsTheLatestRowStampForSaving(){
        var original=Row(1,10);
        var updated=Row(1,20) with{Stamp=new string('D',64)};
        var operations=new GridOperations{Read=(_,_)=>Task.FromResult(Page([original]))};
        using var provider=Services(operations);await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            var rendered=await renderer.RenderComponentAsync<TestGrid>(Parameters("First"));var grid=operations.Component!;
            Invoke(grid,"行追加開始");
            var row=((IList)Get(grid,"行一覧")!)[0]!;
            Invoke(grid,"行セル変更",row,"id","2");
            operations.Read=(_,_)=>Task.FromException<TableRows>(new IOException("read failed"));
            await grid.再読込Async();
            Assert.Contains("value=\"2\"",rendered.ToHtmlString());
            Assert.Single((IList)Get(grid,"新規行")!);
            operations.Read=(_,_)=>Task.FromResult(Page([updated]));
            await grid.再読込Async();
            Assert.Contains("value=\"2\"",rendered.ToHtmlString());
            Assert.Contains("value=\"20\"",rendered.ToHtmlString());
            Assert.Single((IList)Get(grid,"新規行")!);
            ((IList)Get(grid,"新規行")!).Clear();
            await grid.保存実行Async();
            var saved=Assert.Single(operations.Saved!.Changes);
            Assert.Equal(updated.Stamp,saved.ExpectedStamp);
            Assert.Equal(2,saved.Values!.Value.GetProperty("id").GetInt32());
            Assert.Equal(20,saved.Values!.Value.GetProperty("value").GetInt32());
        });
    }
    [Theory][InlineData(true)][InlineData(false)]
    public async Task ReloadRequiresExplicitResolutionOfCellAndDeleteConflicts(bool keepMine){
        var original=Row(1,10);var deleted=Row(2,20);
        var operations=new GridOperations{Read=(_,_)=>Task.FromResult(Page([original,deleted]))};
        using var provider=Services(operations);await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            var rendered=await renderer.RenderComponentAsync<TestGrid>(Parameters("First"));var grid=operations.Component!;
            var rows=(IList)Get(grid,"行一覧")!;
            Invoke(grid,"行セル変更",rows[0],"value","11");
            rows[1]!.GetType().GetProperty("削除予定か")!.SetValue(rows[1],true);
            var remote=Row(1,12) with{Stamp=new string('D',64)};
            var remoteDeleted=Row(2,22) with{Stamp=new string('E',64)};
            operations.Read=(_,_)=>Task.FromResult(Page([remote,remoteDeleted]));
            await grid.再読込Async();await grid.再読込Async();
            Assert.Contains("aria-invalid=\"true\"",rendered.ToHtmlString());
            Assert.Contains("この行を削除する",System.Net.WebUtility.HtmlDecode(rendered.ToHtmlString()));
            await grid.保存実行Async();Assert.Null(operations.Saved);
            Invoke(grid,"セル競合を解決",rows[0],"value",keepMine);
            Invoke(grid,"行競合を解決",rows[1],true);
            await grid.保存実行Async();
            var changes=operations.Saved!.Changes;
            Assert.Equal(remoteDeleted.Stamp,changes.Single(change=>change.Kind==TableRowChangeKind.Delete).ExpectedStamp);
            if(keepMine){Assert.Equal(11,changes.Single(change=>change.Kind==TableRowChangeKind.Replace).Values!.Value.GetProperty("value").GetInt32());}
            else{Assert.Single(changes);}
        });
    }
    [Fact]public async Task ReloadLooksUpEditedRowsOutsideTheFirstPageAndDistinguishesDeletion(){
        var cursor=new TableRowCursor(1,new string('A',64));
        var operations=new GridOperations{Read=(_,_)=>Task.FromResult(Page([Row(1,10)]))};
        using var provider=Services(operations);await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            var rendered=await renderer.RenderComponentAsync<TestGrid>(Parameters("First"));var grid=operations.Component!;
            var rows=(IList)Get(grid,"行一覧")!;
            Invoke(grid,"行セル変更",rows[0],"value","15");
            operations.Read=(request,_)=>Task.FromResult(request.Key is{} key?
                Page(key.GetProperty("id").GetInt32()==1?[Row(1,10)]:throw new InvalidOperationException()):
                Page([Row(2,20)]) with{HasMore=true,NextCursor=cursor});
            await grid.再読込Async();
            Assert.Equal(2,rows.Count);
            Assert.Contains("value=\"15\"",rendered.ToHtmlString());
            operations.Read=(request,_)=>Task.FromResult(request.Key is not null?Page([]):Page([Row(2,20)]) with{HasMore=true,NextCursor=cursor});
            await grid.再読込Async();
            Assert.Contains("新規行として残す",System.Net.WebUtility.HtmlDecode(rendered.ToHtmlString()));
            Invoke(grid,"行競合を解決",rows[1],true);
            await grid.保存実行Async();
            var added=Assert.Single(operations.Saved!.Changes);Assert.Equal(TableRowChangeKind.Insert,added.Kind);
            Assert.Equal(1,added.Values!.Value.GetProperty("id").GetInt32());Assert.Equal(15,added.Values!.Value.GetProperty("value").GetInt32());
        });
    }
    public sealed class TestGrid:Grid{
        public void Refresh()=>this.StateHasChanged();
        [Inject]public GridOperations Operations{get;set;}=null!;
        protected override void OnInitialized(){this.Operations.Component=this;}
    }
    private static ServiceProvider Services(GridOperations operations)=>new ServiceCollection().AddLogging().AddSingleton(operations).AddSingleton<IManagementOperations>(operations).AddSingleton<Microsoft.JSInterop.IJSRuntime,StaticRenderScripts>().BuildServiceProvider();
    private static ParameterView Parameters(string database)=>ParameterView.FromDictionary(new Dictionary<string,object?>{{"DB名",database},{"テーブル名","Rows"}});
    private static TableRows Page(TableRowSnapshot[] rows)=>new(1,rows,false,[new("id","System.Int32"),new("value","System.Int32")]){KeyMembers=[new("id","System.Int32")]};
    private static TableRowSnapshot Row(int id,int value)=>new(ManagementJson.Element(new{id}),ManagementJson.Element(new{id,value}),new string('C',64));
    private static object? Get(Grid target,string name)=>typeof(Grid).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(target);
    private static void Invoke(Grid target,string name,params object?[] args)=>typeof(Grid).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(target,args);
    public sealed class GridOperations:IManagementOperations{
        internal Grid? Component;
        internal TableRowBatch? Saved;
        internal Func<TableRowsRequest,CancellationToken,Task<TableRows>> Read=null!;
        public Task<TableRows> ReadRowsAsync(TableRowsRequest request,CancellationToken cancellationToken=default)=>this.Read(request,cancellationToken);
        public Task<TableRowCommit> CommitRowsAsync(TableRowBatch batch,CancellationToken cancellationToken=default){this.Saved=batch;return Task.FromResult(new TableRowCommit(1,batch.OperationId,batch.Changes.Length));}
        public Task<ServerInfo> InfoAsync(CancellationToken cancellationToken=default)=>throw new NotSupportedException();
        public Task<CatalogPage<CatalogDatabase>> DatabasesAsync(long cursor=0,string versionToken="",CancellationToken cancellationToken=default)=>throw new NotSupportedException();
        public Task<CatalogPage<CatalogObject>> ObjectsAsync(CatalogRequest request,CancellationToken cancellationToken=default)=>throw new NotSupportedException();
        public Task<Draft> GetDraftAsync(string id,CancellationToken cancellationToken=default)=>throw new NotSupportedException();
        public Task<DefinitionVersion> GetDefinitionAsync(string id,CancellationToken cancellationToken=default)=>throw new NotSupportedException();
        public Task<Draft> SaveDraftAsync(SaveDraftRequest request,CancellationToken cancellationToken=default)=>throw new NotSupportedException();
        public Task<BuildResult> BuildAsync(DraftKey key,CancellationToken cancellationToken=default)=>throw new NotSupportedException();
        public Task<ValidationDatabase> CreateValidationDraftAsync(DraftKey key,CancellationToken cancellationToken=default)=>throw new NotSupportedException();
        public Task DropValidationAsync(string id,CancellationToken cancellationToken=default)=>throw new NotSupportedException();
        public Task<ChangePreview> PreviewAsync(DraftKey key,CancellationToken cancellationToken=default)=>throw new NotSupportedException();
        public Task<OperationInfo> ApplyAsync(ApplyRequest request,CancellationToken cancellationToken=default)=>throw new NotSupportedException();
        public Task<OperationInfo> GetOperationAsync(Guid id,CancellationToken cancellationToken=default)=>throw new NotSupportedException();
        public Task<Seamless.Management.ExecutionPlanDocument[]> PlansAsync(string id,CancellationToken cancellationToken=default)=>throw new NotSupportedException();
    }
}
