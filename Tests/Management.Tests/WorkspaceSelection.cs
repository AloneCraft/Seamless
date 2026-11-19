using System.Reflection;
using Seamless.Management;
using Seamless.Management.Components;

namespace Management.Tests;

public sealed class WorkspaceSelection{
    [Theory][InlineData(true,false)][InlineData(false,false)][InlineData(true,true)]
    public async Task NewDatabaseInputRequiresDraftSupportAndWaitsForNameEditing(bool draftSupported,bool renaming){
        await using var component=new 管理ワークスペース();
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        component.GetType().GetProperty("利用者",flags)!.SetValue(component,new UserInfo("owner",[new("*",ManagementPermission.All)],false));
        component.GetType().GetField("DB一括適用対応か",flags)!.SetValue(component,draftSupported);
        if(renaming){component.GetType().GetField("DB名前変更ID",flags)!.SetValue(component,"db:db");}
        component.GetType().GetMethod("新規DB開始時",flags)!.Invoke(component,null);
        Assert.Equal(draftSupported&&!renaming,component.GetType().GetField("DB名編集中か",flags)!.GetValue(component));
    }
    [Theory][InlineData(true)][InlineData(false)]
    public async Task ChangingDatabaseClearsThePreviousExecutionContext(bool changeDatabase){
        await using var component=new 管理ワークスペース();
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        FieldInfo Field(string name)=>component.GetType().GetField(name,flags)!;
        var query=new QueryDesign("OldView","",DatabaseQueryKind.View,[],[],"int","return [];");
        Field("選択DBID").SetValue(component,"first");Field("実行問い合わせ").SetValue(component,query);Field("選択表名").SetValue(component,"OldRows");
        Field("保持計画").SetValue(component,Array.CreateInstance(Field("保持計画").FieldType.GetElementType()!,0));
        Field("表示").SetValue(component,Enum.Parse(Field("表示").FieldType,"問い合わせ"));
        var id="first";if(changeDatabase){id="second";}
        component.GetType().GetMethod("文書を選ぶ",flags)!.Invoke(component,[id]);
        if(changeDatabase){
            Assert.Null(Field("実行問い合わせ").GetValue(component));Assert.Null(Field("選択表名").GetValue(component));Assert.Null(Field("保持計画").GetValue(component));Assert.Equal("構造",Field("表示").GetValue(component)!.ToString());
        }else{
            Assert.Same(query,Field("実行問い合わせ").GetValue(component));Assert.Equal("OldRows",Field("選択表名").GetValue(component));Assert.Equal("問い合わせ",Field("表示").GetValue(component)!.ToString());
        }
    }
}
