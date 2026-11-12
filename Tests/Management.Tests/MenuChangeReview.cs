using System.Reflection;
using Seamless.Management;
using Seamless.Management.Components;
using Designer=Seamless.Management.DatabaseDesigner;
using Document=Seamless.Management.Components.文書状態;

namespace Management.Tests;

public sealed class MenuChangeReview{
    [Theory][InlineData("Success")][InlineData("OtherDocument")][InlineData("Failure")]
    public async Task ReviewSelectsTheDatabaseOnlyAfterSuccessAndWhenTheUserHasNotSelectedAnotherDocument(string outcome){
        var source=Designer.Empty("definition","ReviewDb");
        var db=new CatalogDatabase("db:first","ReviewDb",source.Id,1,new('A',64),false);
        var document=new Document(db,new(source,1,"owner",DateTimeOffset.UtcNow,db.ArtifactHash),new(source,"owner",1,1,DateTimeOffset.UtcNow));
        var pending=new TaskCompletionSource<ChangePreview>(TaskCreationOptions.RunContinuationsAsynchronously);
        var operations=DispatchProxy.Create<IManagementOperations,ReviewOperations>();var fake=(ReviewOperations)(object)operations;fake.Preview=pending.Task;
        await using var workspace=new 管理ワークスペース();Property(workspace,"管理操作",operations);Property(workspace,"利用者",new UserInfo("owner",[new("*",ManagementPermission.All)],false));
        Field(workspace,"DB一覧",new[]{db});Get<Dictionary<string,Document>>(workspace,"文書一覧").Add(db.Id,document);
        Invoke(workspace,"文書を選ぶ",db.Id);Invoke(workspace,"対象を文書で開く",new CatalogObject("Items","Items","","Table","Row"),"構造");
        var firstSelection=Get<string>(workspace,"選択作業文書ID");Assert.NotEqual(db.Id,firstSelection);
        var review=(Task)Invoke(workspace,"メニューから変更を確認Async",document)!;Assert.False(review.IsCompleted);
        var expected=db.Id;
        if(outcome=="OtherDocument"){
            Invoke(workspace,"対象を文書で開く",new CatalogObject("Orders","Orders","","Table","Order"),"データ");expected=Get<string>(workspace,"選択作業文書ID");Assert.NotEqual(firstSelection,expected);
        }
        if(outcome=="Failure"){pending.SetException(new ManagementException("Validation","Expected rejection"));expected=firstSelection;}
        else{pending.SetResult(new("preview",source.Id,1,1,[],[],true,[]));}
        await review;Assert.Equal(expected,Get<string>(workspace,"選択作業文書ID"));Assert.Equal(1,fake.Drops);
        if(outcome=="Failure"){Assert.Null(document.プレビュー);}else{Assert.NotNull(document.プレビュー);}
    }
    private static object? Invoke(object target,string name,params object?[] args)=>typeof(管理ワークスペース).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(target,args);
    private static T Get<T>(object target,string name)=>(T)typeof(管理ワークスペース).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(target)!;
    private static void Field(object target,string name,object value)=>typeof(管理ワークスペース).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(target,value);
    private static void Property(object target,string name,object value)=>typeof(管理ワークスペース).GetProperty(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!.SetValue(target,value);
    public class ReviewOperations:DispatchProxy{
        public Task<ChangePreview> Preview=null!;
        public int Drops;
        protected override object? Invoke(MethodInfo? method,object?[]? args){
            if(method!.Name==nameof(IManagementOperations.CreateValidationDraftAsync)){return Task.FromResult(new ValidationDatabase("validation","ReviewDb","definition",1,"owner"));}
            if(method.Name==nameof(IManagementOperations.PreviewAsync)){return this.Preview;}
            if(method.Name==nameof(IManagementOperations.DropValidationAsync)){this.Drops++;return Task.CompletedTask;}
            throw new InvalidOperationException(method.Name);
        }
    }
}
