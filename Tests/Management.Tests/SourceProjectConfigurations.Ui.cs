using System.Collections;
using System.Reflection;
using Seamless.Management;
using Seamless.Management.Components;

namespace Management.Tests;

public sealed partial class SourceProjectConfigurations{
    [Fact]public async Task UiSwitchSavesInputBeforeChangingCompileFiles(){
        var project=CreateProject();using var service=new SourceProjectService();var opened=await service.OpenSourceProjectAsync(new(project));
        await using var component=Component(service,opened,out var document);Edit(document,"Debug.cs","Value=1","Value=2");
        await Switch(component,document,"Release");
        Assert.Equal("Release",Info(document).Configuration);Assert.DoesNotContain(Current(document).Files,file=>file.Path=="Debug.cs");
        Assert.Contains("Value=2",File.ReadAllText(Path.Combine(Path.GetDirectoryName(project)!,"Debug.cs")));Assert.False((bool)Get(component,"構成変更中か")!);
    }
    [Fact]public async Task UiSwitchKeepsAdditionalInputAndWaitsForAnotherSynchronization(){
        var project=CreateProject();using var service=new SourceProjectService();var opened=await service.OpenSourceProjectAsync(new(project));
        var ready=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var proxy=new ConfigurationOperations(service){Synchronize=async(request,cancellation)=>{var response=await service.SynchronizeSourceProjectAsync(request,cancellation);ready.SetResult();await release.Task;return response;}};
        await using var component=Component(proxy,opened,out var document);Edit(document,"Debug.cs","Value=1","Value=2");
        var work=Switch(component,document,"Release");await ready.Task;Edit(document,"Debug.cs","Value=2","Value=9");release.SetResult();await work;
        Assert.Equal(0,proxy.Opens);Assert.Equal("Debug",Info(document).Configuration);Assert.Contains("Value=9",Current(document).Files.Single(file=>file.Path=="Debug.cs").Text);
        Assert.True((bool)Get(document,"同期停止か")!);Assert.False((bool)Get(component,"構成変更中か")!);
    }
    [Fact]public async Task UiSynchronizationFailurePreservesTheInputAndConfiguration(){
        var project=CreateProject();using var service=new SourceProjectService();var opened=await service.OpenSourceProjectAsync(new(project));
        var proxy=new ConfigurationOperations(service){Synchronize=(_,_)=>Task.FromException<SourceProjectDocument>(new ManagementException("SourceConflict","conflict"))};
        await using var component=Component(proxy,opened,out var document);Edit(document,"Debug.cs","Value=1","Value=9");await Switch(component,document,"Release");
        Assert.Equal(0,proxy.Opens);Assert.Equal("Debug",Info(document).Configuration);Assert.Contains("Value=9",Current(document).Files.Single(file=>file.Path=="Debug.cs").Text);Assert.True((bool)Get(document,"競合ありか")!);
    }
    [Fact]public async Task UiLateInputToAnExcludedFileCanBeRecoveredByReevaluation(){
        var project=CreateProject();using var service=new SourceProjectService();var opened=await service.OpenSourceProjectAsync(new(project));
        var ready=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var proxy=new ConfigurationOperations(service){Open=async(request,cancellation)=>{var response=await service.OpenSourceProjectAsync(request,cancellation);ready.TrySetResult();await release.Task;return response;}};
        await using var component=Component(proxy,opened,out var document);
        var work=Switch(component,document,"Release");await ready.Task;Assert.True((bool)Get(component,"構成変更中か")!);Edit(document,"Debug.cs","Value=1","Value=9");release.SetResult();await work;
        Assert.Equal("Debug",Info(document).Configuration);Assert.Contains("Value=9",Current(document).Files.Single(file=>file.Path=="Debug.cs").Text);Assert.True((bool)Get(document,"同期停止か")!);
        await Invoke(component,"再評価時Async",document);Assert.Equal("Debug",Info(document).Configuration);
        Assert.Contains("Value=9",Current(document).Files.Single(file=>file.Path=="Debug.cs").Text);
    }
    private static プロジェクト作業領域 Component(IManagementSourceOperations operations,SourceProjectDocument opened,out object document){
        var component=new プロジェクト作業領域();var management=operations as IManagementOperations??new ConfigurationOperations(operations);
        typeof(プロジェクト作業領域).GetProperty("管理操作",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!.SetValue(component,management);
        var type=typeof(プロジェクト作業領域).GetNestedType("ソース文書",BindingFlags.NonPublic)!;document=Activator.CreateInstance(type,[opened])!;
        Set(component,"現在",document);((IDictionary)Get(component,"文書一覧")!).Add(opened.Id,document);return component;
    }
    private static Task Switch(プロジェクト作業領域 component,object document,string configuration)=>Invoke(component,"構成変更時Async",document,configuration);
    private static Task Invoke(object component,string method,params object[] arguments)=>(Task)component.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(component,arguments)!;
    private static object? Get(object target,string field)=>target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!.GetValue(target);
    private static void Set(object target,string field,object value)=>target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!.SetValue(target,value);
    private static SourceSnapshot Current(object document)=>(SourceSnapshot)Get(document,"現在")!;
    private static SourceProjectDocument Info(object document)=>(SourceProjectDocument)Get(document,"情報")!;
    private static void Edit(object document,string path,string before,string after){var source=Current(document);Set(document,"現在",source with{Files=source.Files.Select(file=>{if(file.Path==path){return file with{Text=file.Text.Replace(before,after)};}return file;}).ToArray()});}
    private sealed class ConfigurationOperations(IManagementSourceOperations inner):IManagementSourceOperations,IManagementOperations{
        internal int Opens;
        internal Func<SourceProjectSynchronization,CancellationToken,Task<SourceProjectDocument>>? Synchronize;
        internal Func<SourceProjectOpenRequest,CancellationToken,Task<SourceProjectDocument>>? Open;
        public Task<SourceDirectory> BrowseSourceDirectoryAsync(string path,CancellationToken cancellation=default)=>inner.BrowseSourceDirectoryAsync(path,cancellation);
        public Task<SourceProjectLocation> OpenSourceLocationAsync(string path,CancellationToken cancellation=default)=>inner.OpenSourceLocationAsync(path,cancellation);
        public Task<SourceProjectDocument> OpenSourceProjectAsync(SourceProjectOpenRequest request,CancellationToken cancellation=default){this.Opens++;if(this.Open is not null){return this.Open(request,cancellation);}return inner.OpenSourceProjectAsync(request,cancellation);}
        public Task<SourceProjectDocument> SynchronizeSourceProjectAsync(SourceProjectSynchronization request,CancellationToken cancellation=default){if(this.Synchronize is not null){return this.Synchronize(request,cancellation);}return inner.SynchronizeSourceProjectAsync(request,cancellation);}
        public Task<SourceProjectDocument> CreateSourceProjectAsync(SourceProjectCreateRequest request,CancellationToken cancellation=default)=>inner.CreateSourceProjectAsync(request,cancellation);
        public Task<ServerInfo> InfoAsync(CancellationToken cancellation=default)=>throw new NotSupportedException();
        public Task<CatalogPage<CatalogDatabase>> DatabasesAsync(long cursor=0,string versionToken="",CancellationToken cancellation=default)=>throw new NotSupportedException();
        public Task<CatalogPage<CatalogObject>> ObjectsAsync(CatalogRequest request,CancellationToken cancellation=default)=>throw new NotSupportedException();
        public Task<Draft> GetDraftAsync(string id,CancellationToken cancellation=default)=>throw new NotSupportedException();
        public Task<DefinitionVersion> GetDefinitionAsync(string id,CancellationToken cancellation=default)=>throw new NotSupportedException();
        public Task<Draft> SaveDraftAsync(SaveDraftRequest request,CancellationToken cancellation=default)=>throw new NotSupportedException();
        public Task<BuildResult> BuildAsync(DraftKey key,CancellationToken cancellation=default)=>throw new NotSupportedException();
        public Task<ValidationDatabase> CreateValidationDraftAsync(DraftKey key,CancellationToken cancellation=default)=>throw new NotSupportedException();
        public Task DropValidationAsync(string id,CancellationToken cancellation=default)=>throw new NotSupportedException();
        public Task<ChangePreview> PreviewAsync(DraftKey key,CancellationToken cancellation=default)=>throw new NotSupportedException();
        public Task<OperationInfo> ApplyAsync(ApplyRequest request,CancellationToken cancellation=default)=>throw new NotSupportedException();
        public Task<OperationInfo> GetOperationAsync(Guid id,CancellationToken cancellation=default)=>throw new NotSupportedException();
        public Task<TableRows> ReadRowsAsync(TableRowsRequest request,CancellationToken cancellation=default)=>throw new NotSupportedException();
        public Task<TableRowCommit> CommitRowsAsync(TableRowBatch batch,CancellationToken cancellation=default)=>throw new NotSupportedException();
        public Task<Seamless.Management.ExecutionPlanDocument[]> PlansAsync(string id,CancellationToken cancellation=default)=>throw new NotSupportedException();
    }
}
