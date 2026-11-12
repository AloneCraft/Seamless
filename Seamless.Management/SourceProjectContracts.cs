namespace Seamless.Management;

public sealed record SourceProjectLocation(string Path,string[] Projects);
public sealed record SourceDirectoryEntry(string Name,string Path,bool IsDirectory);
public sealed record SourceDirectory(string Path,string? Parent,SourceDirectoryEntry[] Entries);
public sealed record SourceProjectOpenRequest(string Path,string TargetFramework=""){
    public string Configuration{get;init;}="Debug";
}
public sealed record SourceProjectDocument(string Id,string ProjectPath,string[] TargetFrameworks,string TargetFramework,string[] Namespaces,SourceSnapshot? Source){
    public string[] ReferenceTypes{get;init;}=[];
    public string? TypeCompletionDiagnostic{get;init;}
    public SourceProjectDatabase? Database{get;init;}
    public bool HasConflict{get;init;}
    public string Configuration{get;init;}="Debug";
    public string[] Configurations{get;init;}=[];
}
public sealed record SourceProjectDatabase(string DefinitionId,string Name,long BaseVersion,long DraftRevision);
public sealed record SourceProjectSynchronization(string Id,SourceSnapshot Baseline,SourceSnapshot Source);
public sealed record SourceProjectConflictRequest(string Id,SourceSnapshot Editor);
public sealed record SourceProjectConflict(SourceConflict? Conflict,bool AdditionalEditorChanges);
public sealed record SourceProjectConflictResolution(string Id,Guid ConflictId,string DiskHash,string ManagerHash,SourceSnapshot Editor,Dictionary<string,string?> Files);
public sealed record SourceProjectConflictResult(SourceProjectDocument Document,string? EditorBackup);
public sealed record SourceProjectFile(string Path,string FullPath);
public sealed record SourceProjectBinding(string ProjectPath,SourceProjectFile[] Files){
    public string Configuration{get;init;}="Debug";
}
public sealed record SourceProjectCreateRequest(string DirectoryPath,string DatabaseName,DefinitionExport Current,string? ProjectName=null);
public sealed record SourceProjectBuildRequest(string Id,string AssemblyPath,string ExpectedSourceHash);
public sealed record SourceBuildDependency(string Name,string Version,string Path,string Sha256,bool Shared);
public sealed record SourceProjectBuild(string ProjectId,string DefinitionId,long DraftRevision,long BaseVersion,string DefinitionHash,string SourceHash,
    string AssemblyPath,string AssemblySha256,string PdbSha256,Guid ModuleVersionId,SourceBuildDependency[] Dependencies){
    public string BundleHash{get;init;}="";
}
public sealed record SourceProjectBuildImportRequest(SourceProjectBuildRequest Build,string ExpectedBundleHash);
public sealed record ImportedSourceBuild(SourceProjectBuild Build,DateTimeOffset ImportedAt);
public sealed record SourceBuildLookup(ImportedSourceBuild? Build);

/// <summary>サーバー所有者の開発ソース操作。DBの公開・ビルドとは独立する。</summary>
public interface IManagementSourceOperations{
    Task<SourceProjectConflict> InspectSourceConflictAsync(SourceProjectConflictRequest request,CancellationToken cancellationToken=default)=>
        Task.FromException<SourceProjectConflict>(new ManagementException("Unsupported","この接続はC#同期の競合解消に対応していません。"));
    Task<SourceProjectConflictResult> ResolveSourceConflictAsync(SourceProjectConflictResolution request,CancellationToken cancellationToken=default)=>
        Task.FromException<SourceProjectConflictResult>(new ManagementException("Unsupported","この接続はC#同期の競合解消に対応していません。"));
    Task<SourceDirectory> BrowseSourceDirectoryAsync(string path,CancellationToken cancellationToken=default);
    Task<SourceProjectLocation> OpenSourceLocationAsync(string path,CancellationToken cancellationToken=default);
    Task<SourceProjectDocument> OpenSourceProjectAsync(SourceProjectOpenRequest request,CancellationToken cancellationToken=default);
    Task<SourceProjectDocument> SynchronizeSourceProjectAsync(SourceProjectSynchronization request,CancellationToken cancellationToken=default);
    Task<SourceProjectDocument> CreateSourceProjectAsync(SourceProjectCreateRequest request,CancellationToken cancellationToken=default);
    Task<Draft> SaveSourceProjectDraftAsync(string id,CancellationToken cancellationToken=default)=>
        Task.FromException<Draft>(new ManagementException("Unsupported","この接続はソースからのDB下書き反映に対応していません。"));
    Task<SourceProjectBuild> InspectSourceProjectBuildAsync(SourceProjectBuildRequest request,CancellationToken cancellationToken=default)=>
        Task.FromException<SourceProjectBuild>(new ManagementException("Unsupported","この接続は開発者ビルド成果物の検査に対応していません。"));
    Task<ImportedSourceBuild> ImportSourceProjectBuildAsync(SourceProjectBuildImportRequest request,CancellationToken cancellationToken=default)=>
        Task.FromException<ImportedSourceBuild>(new ManagementException("Unsupported","この接続は開発者ビルド成果物の取り込みに対応していません。"));
    Task<ImportedSourceBuild> GetImportedSourceBuildAsync(DraftKey key,CancellationToken cancellationToken=default)=>
        Task.FromException<ImportedSourceBuild>(new ManagementException("Unsupported","この接続は取り込み済み成果物の照会に対応していません。"));
    Task<ImportedSourceBuild?> FindImportedSourceBuildAsync(DraftKey key,CancellationToken cancellationToken=default)=>
        Task.FromException<ImportedSourceBuild?>(new ManagementException("Unsupported","この接続は取り込み済み成果物の任意照会に対応していません。"));
}
