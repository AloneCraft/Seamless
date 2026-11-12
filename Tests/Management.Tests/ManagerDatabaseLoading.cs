using System.Reflection;
using Seamless.Management;
using Seamless.Management.Components;
using Document=Seamless.Management.Components.文書状態;
using Designer=Seamless.Management.DatabaseDesigner;

namespace Management.Tests;

public sealed class ManagerDatabaseLoading{
    [Fact]public async Task RepeatedDatabaseOpenKeepsTheFirstLoadedDocumentAndLaterEdits(){
        var first=new TaskCompletionSource<DefinitionVersion>(TaskCreationOptions.RunContinuationsAsynchronously);
        var second=new TaskCompletionSource<DefinitionVersion>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reads=0;
        var operations=Operations((_,_)=>++reads==1?first.Task:second.Task);
        await using var workspace=Workspace(operations,"first");
        var opening=Open(workspace,"first");var repeated=Open(workspace,"first");
        Assert.Equal(2,reads);
        first.SetResult(Version("first"));await opening;
        var document=Get<Dictionary<string,Document>>(workspace,"文書一覧")["first"];
        document.定義=Designer.AddSchema(document.定義,"pending_schema");
        Display(workspace,"クエリ一覧");
        second.SetResult(Version("first"));await repeated;
        Assert.Same(document,Get<Dictionary<string,Document>>(workspace,"文書一覧")["first"]);
        Assert.Equal(new[]{"pending_schema"},Designer.Schemas(document.定義));
        Assert.True(document.未保管か);
        Assert.Equal("first",Get<string>(workspace,"選択DBID"));
        Assert.Equal("クエリ一覧",Get<object>(workspace,"表示").ToString());
        Assert.Equal(new[]{"first"},Get<文書タブ順序>(workspace,"タブ順序").順序);
        Assert.False(Get<bool>(workspace,"エラーか"));
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task EarlierDatabaseCompletionPreservesTheCurrentSelectionDisplayAndNotice(bool firstCompletesLast){
        var pending=new TaskCompletionSource<DefinitionVersion>(TaskCreationOptions.RunContinuationsAsynchronously);
        var operations=Operations((id,_)=>id=="first"?pending.Task:Task.FromResult(Version(id)));
        await using var workspace=Workspace(operations,"first","second");
        var opening=Open(workspace,"first");
        if(!firstCompletesLast){pending.SetResult(Version("first"));await opening;}
        await Open(workspace,"second");Display(workspace,"クエリ一覧");
        var notice=Get<string>(workspace,"案内");
        if(firstCompletesLast){pending.SetResult(Version("first"));await opening;}
        Assert.Equal("second",Get<string>(workspace,"選択DBID"));
        Assert.Equal("クエリ一覧",Get<object>(workspace,"表示").ToString());
        Assert.Equal(notice,Get<string>(workspace,"案内"));
        Assert.Equal(2,Get<Dictionary<string,Document>>(workspace,"文書一覧").Count);
        Assert.False(Get<bool>(workspace,"エラーか"));
    }

    private static IManagementOperations Operations(Func<string,CancellationToken,Task<DefinitionVersion>> read){
        var operations=DispatchProxy.Create<IManagementOperations,LoadingOperations>();
        ((LoadingOperations)(object)operations).Read=read;return operations;
    }
    private static 管理ワークスペース Workspace(IManagementOperations operations,params string[] ids){
        var workspace=new 管理ワークスペース();
        typeof(管理ワークスペース).GetProperty("管理操作",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!.SetValue(workspace,operations);
        Set(workspace,"DB一覧",ids.Select(id=>new CatalogDatabase(id,id,id,1,"",false)).ToArray());return workspace;
    }
    private static DefinitionVersion Version(string id)=>new(Designer.Empty(id,id),1,"owner",DateTimeOffset.UtcNow);
    private static Task Open(管理ワークスペース workspace,string id)=>(Task)typeof(管理ワークスペース).GetMethod("DBを開くAsync",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(workspace,[id])!;
    private static T Get<T>(object target,string name)=>(T)target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(target)!;
    private static void Set(object target,string name,object value)=>target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(target,value);
    private static void Display(管理ワークスペース workspace,string value){
        var field=typeof(管理ワークスペース).GetField("表示",BindingFlags.Instance|BindingFlags.NonPublic)!;field.SetValue(workspace,Enum.Parse(field.FieldType,value));
    }
    public class LoadingOperations:DispatchProxy{
        public Func<string,CancellationToken,Task<DefinitionVersion>> Read=null!;
        protected override object? Invoke(MethodInfo? method,object?[]? args)=>method!.Name switch{
            nameof(IManagementOperations.GetDefinitionAsync)=>this.Read((string)args![0]!, (CancellationToken)args[1]!),
            nameof(IManagementOperations.FindDraftAsync)=>Task.FromResult<Draft?>(null),
            nameof(IManagementOperations.FunctionsAsync)=>Task.FromResult(new CatalogPage<FunctionSummary>([],0,false,"")),
            _=>throw new InvalidOperationException(method.Name)
        };
    }
}
