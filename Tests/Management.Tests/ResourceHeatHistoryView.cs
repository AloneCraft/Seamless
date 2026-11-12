using System.Reflection;
using System.Text.Json;
using Seamless.Management;
using Seamless.Management.Components;

namespace Management.Tests;

public sealed class ResourceHeatHistoryView{
    [Fact]public async Task HiddenReadIsCancelledAndReopeningRequestsTheSameExecutionAgain(){
        using var view=Create(out var operations,out var execution);
        var pending=new TaskCompletionSource<ResourceHeatReadResult>(TaskCreationOptions.RunContinuationsAsynchronously);operations.Read=_=>pending.Task;
        var first=Read(view);Assert.Equal(1,operations.Calls);Invoke(view,"読取を停止");await first;
        operations.Read=_=>Task.FromResult(Result(Trace(execution)));await Read(view);
        pending.SetResult(Result(Trace(execution)));
        Assert.Equal(2,operations.Calls);Assert.Equal(execution,Get<ResourceHeatTrace>(view,"保存資源記録").ExecutionId);
    }
    [Fact]public async Task AnotherExecutionCannotReplaceTheCurrentlySelectedRecording(){
        using var view=Create(out var operations,out var execution);
        operations.Read=_=>Task.FromResult(Result(Trace(Guid.NewGuid())));await Read(view);
        Assert.Null(Get<ResourceHeatTrace?>(view,"保存資源記録"));
        Assert.Contains("一致しません",Get<string>(view,"資源履歴案内"));
        Assert.NotEqual(Guid.Empty,execution);
    }
    [Fact]public async Task PermissionLossHidesPreviouslyAvailableResourceData(){
        using var view=Create(out var operations,out var execution);Parameter(view,"資源記録",Trace(execution));
        operations.Read=_=>Task.FromException<ResourceHeatReadResult>(new ManagementException("Permission","Denied"));await Read(view);
        Assert.True(Get<bool>(view,"資源閲覧拒否か"));
        Assert.Null(view.GetType().GetProperty("適合資源",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(view));
    }
    private static 実行計画ビュー Create(out ResourceOperations operations,out Guid execution){
        var api=DispatchProxy.Create<IManagementOperations,ResourceOperations>();operations=(ResourceOperations)(object)api;execution=Guid.NewGuid();
        var view=new 実行計画ビュー();Parameter(view,"表示中か",true);Parameter(view,"トレース実行ID",execution);
        view.GetType().GetProperty("管理操作",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public)!.SetValue(view,api);
        var plan=new Seamless.Management.ExecutionPlanDocument(2,"Method","test",new('B',64),new('C',64),[],JsonSerializer.SerializeToElement<object?>(null));
        Set(view,"取得結果",new QueryPlanResult("definition",1,new('A',64),new('B',64),false,new("Query",[],[plan])));
        var mode=view.GetType().GetField("描画方式",BindingFlags.Instance|BindingFlags.NonPublic)!;mode.SetValue(view,Enum.Parse(mode.FieldType,"Heat"));return view;
    }
    private static ResourceHeatTrace Trace(Guid execution)=>new(execution,"definition","Query",1,new('A',64),true,new("clock",1000,0,DateTimeOffset.UnixEpoch),[],[],TimeSpan.FromMilliseconds(100),0,[]);
    private static ResourceHeatReadResult Result(ResourceHeatTrace trace)=>new(trace,new(Guid.NewGuid(),1,1,0,0,0,0,null,false),0,false,null);
    private static Task Read(実行計画ビュー view)=>(Task)Invoke(view,"資源履歴を読むAsync",false)!;
    private static object? Invoke(object value,string name,params object[] arguments)=>value.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(value,arguments);
    private static T Get<T>(object value,string name)=>(T)value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(value)!;
    private static void Set(object value,string name,object content)=>value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(value,content);
    private static void Parameter(object value,string name,object content)=>value.GetType().GetProperty(name)!.SetValue(value,content);
    public class ResourceOperations:DispatchProxy{
        internal Func<ResourceHeatQuery,Task<ResourceHeatReadResult>> Read=default!;
        internal int Calls;
        protected override object? Invoke(MethodInfo? method,object?[]? arguments){
            if(method?.Name!=nameof(IManagementOperations.QueryResourceHeatAsync)){throw new InvalidOperationException(method?.Name);}
            this.Calls++;return this.Read((ResourceHeatQuery)arguments![0]!);
        }
    }
}
