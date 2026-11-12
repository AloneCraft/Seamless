using Seamless.Management.Components;

namespace Management.Tests;

public sealed class ContextualInteractionTests{
    [Fact]public async Task SearchAndExecutionUseTheSameVisibilityAndPermissionRules(){
        var allowed=true;var calls=0;
        var commands=new コマンド登録<string>([
            new("open","開く",target=>target=="table",_=>allowed,_=>{calls++;return Task.CompletedTask;},["表示","open"]),
            new("delete","削除",_=>false,_=>true,_=>throw new InvalidOperationException(),["delete"],破壊的か:true)
        ]);
        Assert.Equal("open",Assert.Single(commands.検索("table","OPEN")).ID);
        Assert.Empty(commands.検索("view",""));
        Assert.Empty(commands.検索("table","delete"));
        allowed=false;
        await Assert.ThrowsAsync<InvalidOperationException>(()=>commands.実行Async("open","table"));
        Assert.Equal(0,calls);
        allowed=true;await commands.実行Async("open","table");Assert.Equal(1,calls);
    }

    [Fact]public async Task AnExecutingCommandCannotBeSubmittedAgain(){
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var commands=new コマンド登録<string>([new("run","実行",_=>true,_=>true,_=>release.Task,[])]);
        var first=commands.実行Async("run","target");
        Assert.True(commands.実行中か);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>commands.実行Async("run","target"));
        release.SetResult();await first;
        Assert.False(commands.実行中か);
    }

    [Theory]
    [InlineData("a","c",false,"b,c,a")]
    [InlineData("c","a",true,"c,a,b")]
    [InlineData("a","b",true,"a,b,c")]
    [InlineData("c","b",false,"a,b,c")]
    public void DropUsesStableIdsAndTheRequestedSide(string source,string target,bool before,string expected){
        var tabs=new 文書タブ順序(["a","b","c"]);
        tabs.ドラッグ開始(source);tabs.移動先を設定(target,before);tabs.ドロップ();
        Assert.Equal(expected,string.Join(",",tabs.順序));
        Assert.Null(tabs.移動元ID);
    }

    [Fact]public void CancellationAndATargetRemovedDuringDraggingPreserveTheRemainingOrder(){
        var tabs=new 文書タブ順序(["a","b","c"]);
        tabs.ドラッグ開始("a");tabs.移動先を設定("c",false);tabs.取消();
        Assert.Equal(["a","b","c"],tabs.順序);
        tabs.ドラッグ開始("a");tabs.移動先を設定("c",false);tabs.削除("c");
        Assert.False(tabs.ドロップ());Assert.Equal(["a","b"],tabs.順序);
        Assert.False(tabs.追加("a"));Assert.True(tabs.追加("d"));
    }
}
