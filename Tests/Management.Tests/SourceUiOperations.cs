using System.Reflection;
using Seamless.Management.Components;

namespace Management.Tests;

public sealed class SourceUiOperations{
    [Fact]public async Task UserActionWaitsForAnInFlightSynchronization(){
        var component=new プロジェクト作業領域();
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var order=new List<int>();
        var first=Run(component,async()=>{order.Add(1);await release.Task;},false);
        var second=Run(component,()=>{order.Add(2);return Task.CompletedTask;},false);
        try{Assert.False(second.IsCompleted);Assert.Equal(new[]{1},order);}
        finally{release.TrySetResult();await Task.WhenAll(first,second);await component.DisposeAsync();}
        Assert.Equal(new[]{1,2},order);
    }
    [Fact]public async Task AutomaticSynchronizationPreservesTheUserOperationMessage(){
        var component=new プロジェクト作業領域();
        Set(component,"案内","explicit operation failed");Set(component,"エラーか",true);
        try{
            await Run(component,()=>{Set(component,"案内","automatic synchronization succeeded");return Task.CompletedTask;},true);
            Assert.Equal("explicit operation failed",Get(component,"案内"));Assert.Equal(true,Get(component,"エラーか"));
        }finally{await component.DisposeAsync();}
    }
    [Fact]public async Task DisposalCancelsQueuedUserActions(){
        var component=new プロジェクト作業領域();
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first=Run(component,()=>release.Task,false);
        var executed=false;
        var queued=Run(component,()=>{executed=true;return Task.CompletedTask;},false);
        try{
            await component.DisposeAsync();
            await queued.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.False(executed);
        }finally{release.TrySetResult();await Task.WhenAll(first,queued);}
    }
    private static Task Run(プロジェクト作業領域 component,Func<Task> action,bool preserve){
        var method=typeof(プロジェクト作業領域).GetMethod("実行Async",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var arguments=new object?[method.GetParameters().Length];arguments[0]=action;arguments[1]=false;
        if(arguments.Length>2){arguments[2]=preserve;}
        return(Task)method.Invoke(component,arguments)!;
    }
    private static void Set(object component,string field,object value)=>component.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(component,value);
    private static object? Get(object component,string field)=>component.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(component);
}
