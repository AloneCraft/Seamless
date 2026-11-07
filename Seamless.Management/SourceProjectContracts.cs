namespace Seamless.Management;

public sealed record SourceProjectLocation(string Path,string[] Projects);
public sealed record SourceProjectOpenRequest(string Path,string TargetFramework="");
public sealed record SourceProjectDocument(string Id,string ProjectPath,string[] TargetFrameworks,string TargetFramework,string[] Namespaces,SourceSnapshot? Source);
public sealed record SourceProjectSynchronization(string Id,SourceSnapshot Baseline,SourceSnapshot Source);
public sealed record SourceProjectFile(string Path,string FullPath);
public sealed record SourceProjectBinding(string ProjectPath,SourceProjectFile[] Files);

/// <summary>サーバー所有者の開発ソース操作。DBの公開・ビルドとは独立する。</summary>
public interface IManagementSourceOperations{
    Task<SourceProjectLocation> OpenSourceLocationAsync(string path,CancellationToken cancellationToken=default);
    Task<SourceProjectDocument> OpenSourceProjectAsync(SourceProjectOpenRequest request,CancellationToken cancellationToken=default);
    Task<SourceProjectDocument> SynchronizeSourceProjectAsync(SourceProjectSynchronization request,CancellationToken cancellationToken=default);
}
