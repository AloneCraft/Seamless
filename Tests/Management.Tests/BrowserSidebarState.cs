using Seamless.Management;
using Seamless.Management.Components;

namespace Management.Tests;

public sealed class BrowserSidebarState{
    [Fact]public void StorageKeyUsesTheActualInstanceAndAuthenticatedName(){
        var instance=Guid.NewGuid();var user=new UserInfo("owner",[],false);
        var key=ブラウザー表示状態.保存キー(instance,user);
        Assert.Equal(key,ブラウザー表示状態.保存キー(instance,user));
        Assert.NotEqual(key,ブラウザー表示状態.保存キー(Guid.NewGuid(),user));
        Assert.NotEqual(key,ブラウザー表示状態.保存キー(instance,new("other",[],false)));
        Assert.Contains(instance.ToString("N"),key);
    }
    [Fact]public void MissingOrDisabledIdentityCannotProduceAnAnonymousFallbackKey(){
        Assert.Throws<InvalidOperationException>(()=>ブラウザー表示状態.保存キー(Guid.Empty,new("owner",[],false)));
        Assert.Throws<InvalidOperationException>(()=>ブラウザー表示状態.保存キー(Guid.NewGuid(),null));
        Assert.Throws<InvalidOperationException>(()=>ブラウザー表示状態.保存キー(Guid.NewGuid(),new("owner",[],true)));
    }
    [Fact]public void OnlyDatabaseIdentitiesAndBoundedSidebarValuesAreAccepted(){
        new ブラウザー表示状態(1,["db:definition-1","db:definition-2"],true,123).検証();
        Assert.Throws<InvalidOperationException>(()=>new ブラウザー表示状態(1,["dbo0.Items"],false,0).検証());
        Assert.Throws<InvalidOperationException>(()=>new ブラウザー表示状態(1,["db:x","db:x"],false,0).検証());
        Assert.Throws<InvalidOperationException>(()=>new ブラウザー表示状態(1,Enumerable.Range(0,257).Select(number=>"db:"+number).ToArray(),false,0).検証());
        Assert.Throws<InvalidOperationException>(()=>new ブラウザー表示状態(1,[],false,double.NaN).検証());
        Assert.Throws<InvalidOperationException>(()=>new ブラウザー表示状態(2,[],false,0).検証());
    }
    [Fact]public void LegacyStateRemainsReadableAndLayoutRequiresBoundedFormatTwoFields(){
        var legacy=System.Text.Json.JsonSerializer.Deserialize<ブラウザー表示状態>("{\"Format\":1,\"OpenDatabaseIds\":[\"db:one\"],\"SystemExpanded\":true,\"ScrollTop\":100}")!;
        legacy.検証();Assert.Null(legacy.SidebarWidth);Assert.Null(legacy.SidebarCollapsed);
        new ブラウザー表示状態(2,["db:one"],true,100){SidebarWidth=254,SidebarCollapsed=false}.検証();
        foreach(var width in new[]{199d,601d,double.NaN,double.PositiveInfinity}){Assert.Throws<InvalidOperationException>(()=>new ブラウザー表示状態(2,[],false,0){SidebarWidth=width,SidebarCollapsed=false}.検証());}
        Assert.Throws<InvalidOperationException>(()=>(legacy with{SidebarWidth=254}).検証());
        Assert.Throws<InvalidOperationException>(()=>new ブラウザー表示状態(3,[],false,0).検証());
    }
}
