using System.Collections;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Seamless.Management;
using Seamless.Management.Components;
using Document=Seamless.Management.Components.文書状態;
using Designer=Seamless.Management.DatabaseDesigner;

namespace Management.Tests;

public sealed class CreationDrafts{
    [Fact]public async Task ATableCreationReturnsToItsOriginalSchemaAndOnlyReflectionChangesTheDraft(){
        await using var workspace=Workspace();var first=Get<Dictionary<string,Document>>(workspace,"文書一覧")["db:first"];var second=Get<Dictionary<string,Document>>(workspace,"文書一覧")["db:second"];var original=first.定義;
        Invoke(workspace,"表作成開始時",first,"dbo0");Set(workspace,"表名","PendingTable");var columns=Get<IList>(workspace,"入力列");columns[0]!.GetType().GetProperty("名前")!.SetValue(columns[0],"KeyId");columns[0]!.GetType().GetProperty("型")!.SetValue(columns[0],"long");
        var location=Get<string>(workspace,"選択作業文書ID");var session=Get<object>(workspace,"新規作成入力");Assert.True(Property<bool>(workspace,"作成入力表示中か"));
        Invoke(workspace,"表データ表示時","dbo0.Items");Assert.False(Property<bool>(workspace,"作成入力表示中か"));
        Invoke(workspace,"文書を選ぶ","db:second");Assert.False(Property<bool>(workspace,"作成入力表示中か"));Assert.Same(session,Get<object>(workspace,"新規作成入力"));
        var busy=Assert.IsType<ManagementException>(Assert.Throws<TargetInvocationException>(()=>Invoke(workspace,"表作成開始時",second,"")).InnerException);Assert.Equal("Busy",busy.Code);
        Assert.Same(original,first.定義);Assert.DoesNotContain(second.オブジェクト,item=>item.Name=="PendingTable");
        Invoke(workspace,"作業文書を選ぶ",location);Assert.True(Property<bool>(workspace,"作成入力表示中か"));Assert.Equal("PendingTable",Get<string>(workspace,"表名"));Assert.Equal("dbo0",Get<string>(workspace,"スキーマ名"));
        Invoke(workspace,"表作成開始時",first,"dbo0");Assert.Same(session,Get<object>(workspace,"新規作成入力"));Assert.Equal("PendingTable",Get<string>(workspace,"表名"));
        Invoke(workspace,"表を下書きに追加時",first);Assert.Null(Get<object?>(workspace,"新規作成入力"));Assert.False(Get<bool>(workspace,"表入力中か"));
        var table=Designer.Read(first.定義,"dbo0.PendingTable");Assert.Equal("KeyId",table.Columns[0].Name);Assert.Equal("long",table.Columns[0].Type);Assert.Contains(first.オブジェクト,item=>item.Id=="dbo0.Items");
        Assert.DoesNotContain(second.オブジェクト,item=>item.Name=="PendingTable");
    }
    [Theory][InlineData(DatabaseQueryKind.View)][InlineData(DatabaseQueryKind.ScalarFunction)]
    public async Task QueryCreationPreservesItsBodyAcrossTargetsAndCancellationCannotBeUndoneByAnOldCallback(DatabaseQueryKind kind){
        await using var workspace=Workspace();var target=Target("db:first","dbo1");Invoke(workspace,"問い合わせ作成開始時",target,kind);
        var session=Get<object>(workspace,"新規作成入力");var id=(Guid)session.GetType().GetProperty("ID")!.GetValue(session)!;var location=Get<string>(workspace,"選択作業文書ID");
        var input=Get<QueryDesign>(workspace,"問い合わせ入力") with{Name="PendingQuery",Body="// retain this comment\nreturn [];",Columns=[new("Amount","decimal",false)],Parameters=[new("minimum","decimal")]};
        Invoke(workspace,"問い合わせ入力変更時",id,input);Invoke(workspace,"文書を選ぶ","db:second");Assert.False(Property<bool>(workspace,"作成入力表示中か"));Assert.Same(input,Get<QueryDesign>(workspace,"問い合わせ入力"));
        Invoke(workspace,"作業文書を選ぶ",location);Assert.True(Property<bool>(workspace,"作成入力表示中か"));Invoke(workspace,"問い合わせ作成開始時",target,kind);Assert.Same(input,Get<QueryDesign>(workspace,"問い合わせ入力"));
        var first=Get<Dictionary<string,Document>>(workspace,"文書一覧")["db:first"];Assert.DoesNotContain(first.オブジェクト,item=>item.Name=="PendingQuery");
        Invoke(workspace,"作成入力を取消す");Assert.Null(Get<object?>(workspace,"新規作成入力"));Assert.Null(Get<QueryDesign?>(workspace,"問い合わせ入力"));
        Invoke(workspace,"問い合わせ入力変更時",id,input);Assert.Null(Get<QueryDesign?>(workspace,"問い合わせ入力"));
        Invoke(workspace,"問い合わせ作成開始時",target,kind);var next=Get<object>(workspace,"新規作成入力");Assert.NotEqual(id,(Guid)next.GetType().GetProperty("ID")!.GetValue(next)!);
    }
    [Fact]public async Task QueryEditorPublishesAllEditableFieldsWithoutResettingCurrentRowsAndRestoresANewInstance(){
        var original=new QueryDesign("NewFunction","dbo1",DatabaseQueryKind.ScalarFunction,[new("Id","int",false)],[new("id","int")],"int","return default;");
        var editor=new 問い合わせ設計();Parameter(editor,"定義",original);QueryDesign? saved=null;
        Parameter(editor,"入力変更時",EventCallback.Factory.Create<QueryDesign>(new object(),(QueryDesign value)=>saved=value));Invoke(editor,"OnParametersSet");
        Set(editor,"名前","FindAmounts");Set(editor,"戻り型","ISet<Result>");Set(editor,"本体","// keep whitespace and comments\nreturn q.Items.Select(row=>new Result(row.Id));\n");
        var parameters=Get<IList>(editor,"引数一覧");parameters[0]!.GetType().GetProperty("名前")!.SetValue(parameters[0],"minimum");parameters[0]!.GetType().GetProperty("型")!.SetValue(parameters[0],"decimal");var existing=parameters[0];
        var columns=Get<IList>(editor,"列一覧");columns[0]!.GetType().GetProperty("名前")!.SetValue(columns[0],"Amount");columns[0]!.GetType().GetProperty("型")!.SetValue(columns[0],"decimal");
        await (Task)Invoke(editor,"入力を通知Async")!;Assert.NotNull(saved);Assert.Equal(DatabaseQueryKind.TableFunction,saved.Kind);Assert.Equal("FindAmounts",saved.Name);Assert.Equal("minimum",saved.Parameters[0].Name);Assert.Equal("decimal",saved.Columns[0].Type);
        Parameter(editor,"定義",saved);Invoke(editor,"OnParametersSet");Assert.Same(existing,Get<IList>(editor,"引数一覧")[0]);
        var restored=new 問い合わせ設計();Parameter(restored,"定義",saved);Invoke(restored,"OnParametersSet");Assert.Equal(saved.Body,Get<string>(restored,"本体"));Assert.Equal(saved.Name,Get<string>(restored,"名前"));
        var restoredValue=Assert.IsType<QueryDesign>(Invoke(restored,"入力内容"));Assert.Equal(saved.Body,restoredValue.Body);Assert.Equal(saved.Parameters,restoredValue.Parameters);Assert.Equal(saved.Columns,restoredValue.Columns);
    }
    private static 管理ワークスペース Workspace(){
        var workspace=new 管理ワークスペース();Parameter(workspace,"利用者",new UserInfo("owner",[new("*",ManagementPermission.All)],false));
        var documents=Get<Dictionary<string,Document>>(workspace,"文書一覧");
        foreach(var id in new[]{"first","second"}){
            var definition=Designer.Empty(id,id);definition=Designer.AddSchema(definition,"dbo1");definition=Designer.Apply(definition,new("Items","dbo0",[new("Id","int",true)],[]));
            documents.Add("db:"+id,new(new("db:"+id,id,id,1,"",false),new(definition,1,"owner",DateTimeOffset.UtcNow),null));
        }
        Invoke(workspace,"文書を選ぶ","db:first");return workspace;
    }
    private static object Target(string db,string schema)=>Activator.CreateInstance(typeof(管理ワークスペース).GetNestedType("管理対象",BindingFlags.NonPublic)!,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,["作成",db,null,schema,null],null)!;
    private static object? Invoke(object target,string name,params object[] values)=>target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(target,values);
    private static T Get<T>(object target,string name)=>(T)target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(target)!;
    private static T Property<T>(object target,string name)=>(T)target.GetType().GetProperty(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(target)!;
    private static void Set(object target,string name,object value)=>target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(target,value);
    private static void Parameter(object target,string name,object value)=>target.GetType().GetProperty(name)!.SetValue(target,value);
}
