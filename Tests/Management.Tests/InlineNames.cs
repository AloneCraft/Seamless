using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Seamless.Management;
using Seamless.Management.Components;
using Document=Seamless.Management.Components.文書状態;
using Designer=Seamless.Management.DatabaseDesigner;

namespace Management.Tests;

public sealed class InlineNames{
    [Theory][InlineData(true)][InlineData(false)]
    public async Task EnterAndBlurCommitTheTrimmedName(bool enter){
        var input=new インライン名前入力();string? actual=null;
        Parameter(input,"確定時",EventCallback.Factory.Create<string>(new object(),(string name)=>actual=name));
        Set(input,"編集値","  dbo0  ");
        if(enter){await Key(input,"Enter");}else{await Blur(input);}
        Assert.Equal("dbo0",actual);
    }
    [Theory][InlineData(true)][InlineData(false)]
    public async Task ExplicitCreationKeepsTheDraftOnBlurAndSubmitsOnlyOnEnterOrClick(bool enter){
        var input=new インライン名前入力();var committed=new List<string>();
        Parameter(input,"明示作成か",true);
        Parameter(input,"確定時",EventCallback.Factory.Create<string>(new object(),(string name)=>committed.Add(name)));
        Set(input,"編集値"," 222 ");
        await Blur(input);
        Assert.Equal(" 222 ",Get<string>(input,"編集値"));Assert.Empty(committed);
        if(enter){await Key(input,"Enter");}else{await Confirm(input);}
        Assert.Equal(new[]{"222"},committed);
    }
    [Fact]public async Task EscapeCancelsBeforeTheBlurEvent(){
        var input=new インライン名前入力();var committed=0;var cancelled=0;
        Parameter(input,"確定時",EventCallback.Factory.Create<string>(new object(),(string _)=>committed++));
        Parameter(input,"取消時",EventCallback.Factory.Create(new object(),()=>cancelled++));
        Set(input,"編集値","dbo0");await Key(input,"Escape");await Blur(input);
        Assert.Equal(0,committed);Assert.Equal(1,cancelled);
    }
    [Theory][InlineData("",true)][InlineData("",false)][InlineData(" \t ",true)][InlineData(" \t ",false)]
    public async Task EmptyNamesCancel(string name,bool enter){
        var input=new インライン名前入力();var committed=0;var cancelled=0;
        Parameter(input,"確定時",EventCallback.Factory.Create<string>(new object(),(string _)=>committed++));
        Parameter(input,"取消時",EventCallback.Factory.Create(new object(),()=>cancelled++));
        Set(input,"編集値",name);
        if(enter){await Key(input,"Enter");}else{await Blur(input);}
        Assert.Equal(0,committed);Assert.Equal(1,cancelled);
    }
    [Fact]public async Task BlurDuringEnterDoesNotCommitTwice(){
        var input=new インライン名前入力();var entered=0;var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Parameter(input,"確定時",EventCallback.Factory.Create<string>(new object(),async(string _)=>{entered++;await release.Task;}));
        Set(input,"編集値","dbo0");var pending=Key(input,"Enter");
        try{await Blur(input);await Key(input,"Escape");Assert.Equal(1,entered);}
        finally{release.TrySetResult();await pending;}
    }
    [Fact]public async Task ARejectedNameCanBeCorrectedAndRetried(){
        var input=new インライン名前入力();string? actual=null;
        Parameter(input,"確定時",EventCallback.Factory.Create<string>(new object(),(string _)=>Task.FromException(new ManagementException("Conflict","duplicate"))));
        Set(input,"編集値","dbo0");await Assert.ThrowsAsync<ManagementException>(()=>Confirm(input));
        Parameter(input,"確定時",EventCallback.Factory.Create<string>(new object(),(string name)=>actual=name));
        Set(input,"編集値","dbo1");await Key(input,"Enter");Assert.Equal("dbo1",actual);
    }
    [Fact]public void SchemaCreationChangesOnlyTheCapturedDatabaseDraft(){
        var first=CreateDocument("first");var second=CreateDocument("second");var component=Workspace(first,second);
        Set(component,"選択DBID","second");Set(component,"スキーマ入力中か",true);Set(component,"スキーマ作成DBID","first");
        Invoke(component,"スキーマ名確定時","dbo0");
        Assert.Equal(new[]{"dbo0"},Designer.Schemas(first.定義));Assert.Empty(Designer.Schemas(second.定義));
        Assert.True(first.未保管か);Assert.False(Get<bool>(component,"スキーマ入力中か"));
        Invoke(component,"スキーマ名確定時","dbo0");Assert.Equal(new[]{"dbo0"},Designer.Schemas(first.定義));
    }
    [Fact]public void DuplicateSchemaNamesRemainEditableWithoutChangingTheDraft(){
        var document=CreateDocument("first");document.定義=Designer.AddSchema(document.定義,"dbo0");
        var original=document.定義;var component=Workspace(document);
        Set(component,"スキーマ入力中か",true);Set(component,"スキーマ作成DBID","first");Invoke(component,"スキーマ名確定時","dbo0");
        Assert.Same(original,document.定義);Assert.True(Get<bool>(component,"スキーマ入力中か"));Assert.True(Get<bool>(component,"エラーか"));
        Invoke(component,"スキーマ名確定時","dbo1");Assert.Equal(new[]{"dbo0","dbo1"},Designer.Schemas(document.定義));
    }
    private static Document CreateDocument(string id){
        var definition=Designer.Empty(id,id);
        return new(new(id,id,id,1,"",false),new(definition,1,"owner",DateTimeOffset.UtcNow),null);
    }
    private static 管理ワークスペース Workspace(params Document[] documents){
        var component=new 管理ワークスペース();
        Parameter(component,"利用者",new UserInfo("owner",[new("*",ManagementPermission.All)],false));
        var open=Get<Dictionary<string,Document>>(component,"文書一覧");foreach(var document in documents){open.Add(document.DB.Id,document);}
        return component;
    }
    private static Task Key(インライン名前入力 input,string key)=>(Task)Invoke(input,"キー時Async",new KeyboardEventArgs{Key=key})!;
    private static Task Blur(インライン名前入力 input)=>(Task)Invoke(input,"フォーカス離脱時Async")!;
    private static Task Confirm(インライン名前入力 input)=>(Task)Invoke(input,"確定Async")!;
    private static object? Invoke(object target,string name,params object?[] args)=>target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(target,args);
    private static T Get<T>(object target,string name)=>(T)target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(target)!;
    private static void Set(object target,string name,object value)=>target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(target,value);
    private static void Parameter(object target,string name,object value)=>target.GetType().GetProperty(name)!.SetValue(target,value);
}
