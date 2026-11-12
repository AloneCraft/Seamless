using System.Reflection;
using Microsoft.JSInterop;
using Seamless.Management.Components;

namespace Management.Tests;

public sealed class BrowserComponentDisposal{
    [Fact]public async Task ConnectedAndUninitializedComponentsCanBeDisposedRepeatedly(){
        var component=new 管理ワークスペース();
        var handler=new ScriptObject(false,false);var module=new ScriptObject(false,false);
        Set(component,"相互操作",handler);Set(component,"モジュール",module);
        await component.DisposeAsync();await component.DisposeAsync();
        Assert.Equal(1,handler.Disposals);Assert.Equal(1,module.Disposals);
        await new 管理ワークスペース().DisposeAsync();
        await new 操作メニュー<string>().DisposeAsync();
    }
    [Theory]
    [InlineData(false,false)][InlineData(false,true)]
    [InlineData(true,false)][InlineData(true,true)]
    public async Task DisconnectedModulesDoNotEscapeDisposal(bool menu,bool disconnectHandler){
        IAsyncDisposable component=new 管理ワークスペース();
        var handlerField="相互操作";
        if(menu){component=new 操作メニュー<string>();handlerField="表示";}
        var handler=new ScriptObject(disconnectHandler,disconnectHandler);
        var module=new ScriptObject(false,true);
        Set(component,handlerField,handler);Set(component,"モジュール",module);
        await component.DisposeAsync();
        Assert.Equal(1,module.Disposals);
        await component.DisposeAsync();
        Assert.Equal(1,module.Disposals);
        if(!menu){Assert.Throws<ObjectDisposedException>(()=>Get<CancellationTokenSource>(component,"寿命").Cancel());}
    }
    [Fact]public async Task UnexpectedScriptErrorsRemainVisibleAndManagedCleanupCompletes(){
        var component=new 管理ワークスペース();
        Set(component,"モジュール",new ScriptObject(false,false,new JSException("unexpected script error")));
        await Assert.ThrowsAsync<JSException>(()=>component.DisposeAsync().AsTask());
        Assert.Throws<ObjectDisposedException>(()=>Get<CancellationTokenSource>(component,"寿命").Cancel());
    }
    [Theory]
    [InlineData(typeof(コードエディター),false)][InlineData(typeof(コードエディター),true)]
    [InlineData(typeof(編集グリッド),false)][InlineData(typeof(編集グリッド),true)]
    [InlineData(typeof(型入力),false)][InlineData(typeof(型入力),true)]
    [InlineData(typeof(管理ワークスペース),false)][InlineData(typeof(管理ワークスペース),true)]
    public async Task CancelledBrowserCleanupReleasesEveryReferenceOnce(Type componentType,bool cancelHandler){
        var component=(IAsyncDisposable)Activator.CreateInstance(componentType)!;
        Exception? invokeError=null;if(cancelHandler){invokeError=new TaskCanceledException("browser closed during handler cleanup");}
        var handler=new ScriptObject(false,false,invokeError:invokeError);
        var module=new ScriptObject(false,false,new TaskCanceledException("browser closed during module cleanup"));
        var handlerField="操作";if(component is 管理ワークスペース){handlerField="相互操作";}
        Set(component,handlerField,handler);Set(component,"モジュール",module);
        await component.DisposeAsync();await component.DisposeAsync();
        Assert.Equal(1,handler.Disposals);Assert.Equal(1,module.Disposals);
    }
    private static T Get<T>(object target,string name)=>(T)target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(target)!;
    private static void Set(object target,string name,object value)=>target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(target,value);
    private sealed class ScriptObject(bool disconnectInvoke,bool disconnectDispose,Exception? disposalError=null,Exception? invokeError=null):IJSObjectReference{
        internal int Disposals{get;private set;}
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier,object?[]? args)=>this.InvokeAsync<TValue>(identifier,CancellationToken.None,args);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier,CancellationToken cancellationToken,object?[]? args){
            if(invokeError is not null){return ValueTask.FromException<TValue>(invokeError);}
            if(disconnectInvoke){return ValueTask.FromException<TValue>(new JSDisconnectedException("circuit disconnected"));}
            return ValueTask.FromResult(default(TValue)!);
        }
        public ValueTask DisposeAsync(){
            this.Disposals++;
            if(disposalError is not null){return ValueTask.FromException(disposalError);}
            if(disconnectDispose){return ValueTask.FromException(new JSDisconnectedException("circuit disconnected"));}
            return ValueTask.CompletedTask;
        }
    }
}
