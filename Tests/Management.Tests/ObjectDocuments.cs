using System.Reflection;
using Seamless.Management;
using Seamless.Management.Components;
using Designer=Seamless.Management.DatabaseDesigner;
using Document=Seamless.Management.Components.文書状態;

namespace Management.Tests;

public sealed class ObjectDocuments{
    [Fact]public void IdentityIncludesDatabaseDefinitionKindAndQualifiedObject(){
        var item=new CatalogObject("dbo0.Items","Items","dbo0","Table","Row");
        var document=new 対象文書状態("db:first","first",item);
        Assert.True(document.一致するか("db:first","first",item,null));
        Assert.False(document.一致するか("db:second","first",item,null));
        Assert.False(document.一致するか("db:first","second",item,null));
        Assert.False(document.一致するか("db:first","first",item with{Id="dbo1.Items",Schema="dbo1"},null));
        Assert.False(document.一致するか("db:first","first",item with{Kind="View"},null));
    }
    [Fact]public async Task OpeningAndRevisitingObjectsPreservesTheirOwnTabsAndEdits(){
        var definition=Designer.Empty("first","First");
        foreach(var name in new[]{"Items","Orders"}){definition=Designer.Apply(definition,new(name,"",[new("Id","int",true)],[]));}
        await using var workspace=new 管理ワークスペース();
        var owner=new Document(new("db:first","First","first",1,"",false),new(definition,1,"owner",DateTimeOffset.UtcNow),null);
        Get<Dictionary<string,Document>>(workspace,"文書一覧").Add("db:first",owner);
        Invoke(workspace,"文書を選ぶ","db:first");
        Invoke(workspace,"表データ表示時","Items");
        var documents=Get<Dictionary<string,対象文書状態>>(workspace,"対象文書一覧");
        var items=Assert.Single(documents.Values,item=>item.対象?.Id=="Items");
        items.表示="C#";items.編集中か=true;items.トレース実行ID=Guid.NewGuid();
        Invoke(workspace,"表データ表示時","Orders");
        var orders=Assert.Single(documents.Values,item=>item.対象?.Id=="Orders");
        Assert.Equal("データ",orders.表示);Assert.False(orders.編集中か);Assert.Null(orders.トレース実行ID);
        Invoke(workspace,"表データ表示時","Items");
        Assert.Equal(items.ID,Get<string>(workspace,"選択作業文書ID"));
        Assert.Equal("C#",items.表示);Assert.True(items.編集中か);Assert.NotNull(items.トレース実行ID);
        Assert.Single(documents.Values,item=>item.対象?.Id=="Items");
        Assert.Equal("Items",((CatalogObject)Invoke(workspace,"ツリー選択オブジェクト","db:first")!).Id);
        Invoke(workspace,"作業文書を選ぶ",orders.ID);
        Assert.Equal("Orders",((CatalogObject)Invoke(workspace,"ツリー選択オブジェクト","db:first")!).Id);
    }
    [Fact]public async Task ClosingAnObjectKeepsItsDraftAndSchemaSelectionOpensInformation(){
        var definition=Designer.Apply(Designer.Empty("first","First"),new("Items","dbo0",[new("Id","int",true)],[]));
        await using var workspace=new 管理ワークスペース();
        Get<Dictionary<string,Document>>(workspace,"文書一覧").Add("db:first",new(new("db:first","First","first",1,"",false),new(definition,1,"owner",DateTimeOffset.UtcNow),null));
        Invoke(workspace,"文書を選ぶ","db:first");Get<文書タブ順序>(workspace,"タブ順序").追加("db:first");Invoke(workspace,"表データ表示時","dbo0.Items");
        var documents=Get<Dictionary<string,対象文書状態>>(workspace,"対象文書一覧");
        var items=Assert.Single(documents.Values,item=>item.対象 is not null);items.編集中か=true;
        Invoke(workspace,"作業文書を閉じる",items.ID);
        Assert.True(items.編集中か);Assert.Same(items,documents[items.ID]);
        Invoke(workspace,"スキーマを選ぶ","dbo0");
        var schema=documents[Get<string>(workspace,"選択作業文書ID")];
        Assert.True(schema.スキーマか);Assert.Equal("dbo0",schema.名前);
        Assert.Null(Invoke(workspace,"ツリー選択オブジェクト","db:first"));
    }
    private static object? Invoke(object target,string name,params object[] args)=>target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(target,args);
    [Fact]public async Task DelayedRegisteredFunctionLoadCannotReplaceTheNewerTableSelection(){
        var definition=Designer.Apply(Designer.Empty("first","First"),new("Items","",[new("Id","int",true)],[]));
        var pending=new TaskCompletionSource<DefinitionVersion>(TaskCreationOptions.RunContinuationsAsynchronously);
        var operations=DispatchProxy.Create<IManagementOperations,DelayedFunctions>();
        ((DelayedFunctions)(object)operations).Pending=pending.Task;
        await using var workspace=new 管理ワークスペース();
        typeof(管理ワークスペース).GetProperty("管理操作",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!.SetValue(workspace,operations);
        Get<Dictionary<string,Document>>(workspace,"文書一覧").Add("db:first",new(new("db:first","First","first",1,"",false),new(definition,1,"owner",DateTimeOffset.UtcNow),null));
        Get<Dictionary<string,FunctionSummary[]>>(workspace,"登録関数一覧").Add("db:first",[new(new(FunctionOrigin.RegisteredEntry,"db:first","query","query"),"Query","",1,new('A',64),false)]);
        Invoke(workspace,"文書を選ぶ","db:first");
        var loading=(Task)Invoke(workspace,"登録関数を開くAsync","db:first",new CatalogObject("registered:query","Query","","RegisteredFunction",""))!;
        Invoke(workspace,"表データ表示時","Items");
        pending.SetResult(new(definition with{Id="query",Kind=DefinitionKind.Query},1,"owner",DateTimeOffset.UtcNow));
        await loading;
        Assert.Equal("Items",((CatalogObject)Invoke(workspace,"ツリー選択オブジェクト","db:first")!).Id);
        Assert.Empty(Get<Dictionary<string,Document>>(workspace,"関数定義文書"));
    }
    public class DelayedFunctions:DispatchProxy{
        public Task<DefinitionVersion> Pending{get;set;}=default!;
        protected override object? Invoke(MethodInfo? method,object?[]? arguments)=>method!.Name switch{
            nameof(IManagementOperations.GetDefinitionAsync)=>this.Pending,
            nameof(IManagementOperations.FindDraftAsync)=>Task.FromResult<Draft?>(null),
            _=>throw new NotSupportedException(method.Name)
        };
    }
    private static T Get<T>(object target,string name)=>(T)target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(target)!;
}
