using System.Collections;
using System.Reflection;
using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Seamless.Management;
using Seamless.Management.Components;
using Designer=Seamless.Management.DatabaseDesigner;

namespace Management.Tests;

public sealed class IdentityAssignmentUi{
    [Fact]public async Task CommandAddsOnlyAnUnsavedDraftAndPreservesExistingUserChanges(){
        await using var fixture=await Fixture.Create();var published=fixture.Document.定義;
        fixture.Document.定義=published with{Source=published.Source with{Files=published.Source.Files.Select(file=>file with{Text=file.Text+"\n// user change retained\n"}).ToArray()}};
        await fixture.Assign();Assert.True(fixture.Document.未保管か);Assert.True(fixture.Document.未適用か);
        Assert.NotEmpty(Designer.ReadStableIdentities(fixture.Document.定義).Entries);Assert.Contains("// user change retained",fixture.Document.コード);
        Assert.DoesNotContain("seamless-id:",string.Join("\n",published.Source.Files.Select(file=>file.Text)));Assert.Equal(1,fixture.Operations.Reads);Assert.Equal(0,fixture.Operations.Writes);
        var assigned=fixture.Document.定義;await fixture.Assign();Assert.Same(assigned,fixture.Document.定義);Assert.Equal(0,fixture.Operations.Writes);
        Assert.Contains("付与済み",fixture.Get<string>("案内"));
    }

    [Theory][InlineData("Source")][InlineData("Rows")][InlineData("Creation")][InlineData("Project")]
    public async Task PendingInputIsKeptAndNoAuthorityReadOrMutationOccurs(string kind){
        await using var fixture=await Fixture.Create();var before=fixture.Document.定義;
        if(kind is "Source" or "Rows"){
            var target=new 対象文書状態("db","db",new("Items","Items","","Table","Example.Tables.Items")){編集中か=kind=="Source",データ編集中か=kind=="Rows"};
            fixture.Get<Dictionary<string,対象文書状態>>("対象文書一覧").Add(target.ID,target);
        }else if(kind=="Project"){fixture.Set("プロジェクト未保存か",true);}
        else{
            var state=typeof(管理ワークスペース).GetNestedType("作成入力状態",BindingFlags.NonPublic)!;
            fixture.Set("新規作成入力",Activator.CreateInstance(state,Guid.NewGuid(),"db","db","","document","Table"));
        }
        Assert.Equal("Validation",(await Assert.ThrowsAsync<ManagementException>(fixture.Assign)).Code);
        Assert.Same(before,fixture.Document.定義);Assert.Equal(0,fixture.Operations.Reads);Assert.Equal(0,fixture.Operations.Writes);
        if(kind=="Project"){Assert.True(fixture.Get<bool>("プロジェクト未保存か"));}
        if(kind=="Creation"){Assert.NotNull(fixture.Get<object>("新規作成入力"));}
    }

    [Fact]public async Task ViewOnlyAndExpiredAuthorityCannotChangeTheDraft(){
        await using var fixture=await Fixture.Create();var before=fixture.Document.定義;
        fixture.Set("表示用ID対応か",false);Assert.False(fixture.CommandVisible());
        Assert.Equal("Unsupported",(await Assert.ThrowsAsync<ManagementException>(fixture.Assign)).Code);Assert.Same(before,fixture.Document.定義);
        fixture.Set("表示用ID対応か",true);
        fixture.User(ManagementPermission.View);Assert.False(fixture.CommandVisible());
        Assert.Equal("Permission",(await Assert.ThrowsAsync<ManagementException>(fixture.Assign)).Code);Assert.Equal(0,fixture.Operations.Reads);
        fixture.User(ManagementPermission.View|ManagementPermission.Edit);Assert.True(fixture.CommandVisible());
        fixture.Operations.Read=()=>Task.FromException<DefinitionVersion>(new ManagementException("Permission","revoked"));
        Assert.Equal("Permission",(await Assert.ThrowsAsync<ManagementException>(fixture.Assign)).Code);
        Assert.Same(before,fixture.Document.定義);Assert.False(fixture.Get<bool>("表示用ID処理中か"));Assert.Equal(0,fixture.Operations.Writes);
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task AChangedDraftOrPublishedVersionDiscardsTheComputedResult(bool version){
        await using var fixture=await Fixture.Create();var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var response=new TaskCompletionSource<DefinitionVersion>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Operations.Read=()=>{entered.SetResult();return response.Task;};var operation=fixture.Assign();await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var latest=fixture.Document.定義;if(!version){latest=latest with{Source=latest.Source with{Files=latest.Source.Files.Select(file=>file with{Text=file.Text+"\n// concurrent edit\n"}).ToArray()}};fixture.Document.定義=latest;}
        response.SetResult(fixture.Published with{Version=version?2:1});
        Assert.Equal("Conflict",(await Assert.ThrowsAsync<ManagementException>(()=>operation)).Code);Assert.Same(latest,fixture.Document.定義);Assert.Equal(0,fixture.Operations.Writes);
    }

    [Fact]public async Task DuplicateAndSharedDeclarationsLeaveTheDraftIntact(){
        await using var fixture=await Fixture.Create();var identified=Designer.AssignStableIdentities(fixture.Document.定義);
        var file=identified.Source.Files.Single(item=>item.Path=="Items.cs");
        var duplicate=identified with{Source=identified.Source with{Files=[..identified.Source.Files,new("Copied.cs",file.Text.Replace("struct Items","struct Other",StringComparison.Ordinal))]}};
        fixture.Document.定義=duplicate;Assert.Equal("Conflict",(await Assert.ThrowsAsync<ManagementException>(fixture.Assign)).Code);Assert.Same(duplicate,fixture.Document.定義);
        var shared=new Definition("db","DB",DefinitionKind.Database,"Example.Db","",new("1",[new("Database.cs","using Seamless.Database;namespace Example{public sealed class Db(DatabaseContainerOptions o):DatabaseContainer(o){public Table<Row> A=>this.Table<Row>();public Table<Row> B=>this.Table<Row>();}public readonly record struct Row(int Id);}")],[]));
        fixture.Document.定義=shared;Assert.Equal("Unsupported",(await Assert.ThrowsAsync<ManagementException>(fixture.Assign)).Code);Assert.Same(shared,fixture.Document.定義);Assert.Equal(0,fixture.Operations.Reads);
    }

    [Fact]public async Task StatusShowsTheDbDraftAndPendingRestorationInEnglish(){
        var previous=CultureInfo.CurrentUICulture;CultureInfo.CurrentUICulture=CultureInfo.GetCultureInfo("en");
        try{
            await using var services=new ServiceCollection().AddLogging().BuildServiceProvider();await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
            var html=await renderer.Dispatcher.InvokeAsync(async()=>{
                var component=await renderer.RenderComponentAsync<表示用ID診断>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"DB名","DB"},{"件数",3},{"保存状態","未保管の変更"},{"診断一覧",new[]{表示文言.取得("表示用IDは元宣言のマーカーです。IDの転用や別版との同一性には別途検証が必要です。")}}}));return component.ToHtmlString();
            });
            Assert.Contains("Source identities for DB",html);Assert.Contains("3 identifiers",html);Assert.Contains("Unsaved",html,StringComparison.OrdinalIgnoreCase);Assert.Contains("restoration across renames is pending",html);Assert.Contains("additional verification",html);
        }finally{CultureInfo.CurrentUICulture=previous;}
    }

    private sealed class Fixture:IAsyncDisposable{
        internal readonly 管理ワークスペース Workspace=new();
        internal Operations Operations=null!;internal Seamless.Management.Components.文書状態 Document=null!;internal DefinitionVersion Published=null!;
        internal static async Task<Fixture> Create(){
            var fixture=new Fixture();var proxy=DispatchProxy.Create<IManagementOperations,Operations>();fixture.Operations=(Operations)(object)proxy;
            Parameter(fixture.Workspace,"管理操作",proxy);await(Task)Call(fixture.Workspace,"OnInitializedAsync")!;
            var definition=new Definition("db","DB",DefinitionKind.Database,"Example.Db","",new("1",[new("Database.cs","using Seamless.Database;namespace Example{internal static partial class DatabaseGenerator;public sealed partial class Db(DatabaseContainerOptions options):DatabaseContainer(options){}}"),new("Items.cs","namespace Example.Tables{public partial struct Items{public partial int Id{get;}public partial string Title{get;}}}")],[]));
            fixture.Published=new(definition,1,"owner",DateTimeOffset.UtcNow,new string('A',64));fixture.Operations.Read=()=>Task.FromResult(fixture.Published);
            var database=new CatalogDatabase("db","DB","db",1,new string('A',64),false);fixture.Document=new(database,fixture.Published,null);
            fixture.Set("DB一覧",new[]{database});fixture.Get<Dictionary<string,Seamless.Management.Components.文書状態>>("文書一覧").Add("db",fixture.Document);fixture.Set("表示用ID対応か",true);fixture.Set("選択DBID","db");fixture.User(ManagementPermission.View|ManagementPermission.Edit);return fixture;
        }
        internal Task Assign()=>(Task)Call(this.Workspace,"表示用ID付与時Async",Target())!;
        internal void User(ManagementPermission permissions)=>Parameter(this.Workspace,"利用者",new UserInfo("owner",[new("DB",permissions)],false));
        internal bool CommandVisible(){
            var registry=this.Get<object>("コマンド");var commands=((IEnumerable)registry.GetType().GetMethod("検索")!.Invoke(registry,[Target(),""])!).Cast<object>();
            return commands.Any(command=>(string)command.GetType().GetProperty("ID")!.GetValue(command)! =="DB表示用ID");
        }
        private static object Target()=>Activator.CreateInstance(typeof(管理ワークスペース).GetNestedType("管理対象",BindingFlags.NonPublic)!,"DB","db",null,"",null)!;
        internal T Get<T>(string name)=>(T)typeof(管理ワークスペース).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(this.Workspace)!;
        internal void Set(string name,object? value)=>typeof(管理ワークスペース).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(this.Workspace,value);
        private static object? Call(object target,string name,params object[] args)=>target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(target,args);
        private static void Parameter(object target,string name,object? value)=>target.GetType().GetProperty(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!.SetValue(target,value);
        public ValueTask DisposeAsync()=>this.Workspace.DisposeAsync();
    }
    public class Operations:DispatchProxy{
        internal Func<Task<DefinitionVersion>> Read=()=>throw new InvalidOperationException();internal int Reads,Writes;
        protected override object? Invoke(MethodInfo? method,object?[]? args){
            if(method!.Name==nameof(IManagementOperations.InfoAsync)){return Task.FromException<ServerInfo>(new ManagementException("Unsupported","fixture initialization stops here"));}
            if(method.Name==nameof(IManagementOperations.GetDefinitionAsync)){this.Reads++;return this.Read();}
            this.Writes++;throw new InvalidOperationException("Unexpected operation "+method.Name);
        }
    }
}
