using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using Seamless.Execution;

namespace Seamless.Management.Server;

internal sealed partial class SourceBuildArtifactStore{
    internal CapturedSourceBuild Read(SourceProjectBuild build,CancellationToken cancellationToken=default){
        lock(gate){return ReadDirectory(build,this.PathFor(build),"manifest.json",cancellationToken);}
    }
    internal static CapturedSourceBuild ReadGeneration(SourceProjectBuild build,string path,string? assemblyName=null)=>
        ReadDirectory(build,path,assemblyName is null?"source-build.json":null,CancellationToken.None,assemblyName??"module.dll");

    private static CapturedSourceBuild ReadDirectory(SourceProjectBuild build,string path,string? manifestName,CancellationToken cancellationToken,string moduleName="module.dll"){
        var capture=SourceBuildArtifactInspector.CaptureFiles(cancellationToken);
        try{
            if(build.BundleHash!=BundleHash(build)){throw new ManagementException("BuildMismatch","保管済み成果物の識別情報が一致しません。");}
            if(manifestName is not null){
                var manifest=capture.Read(Path.Combine(path,manifestName));
                if(!manifest.AsSpan().SequenceEqual(ManifestBytes(build))){throw new ManagementException("BuildMismatch","保管済み成果物のマニフェストが異なります。");}
            }
            var files=Files(build);var hostShared=ManifestFormat(build)==2;
            foreach(var file in files){
                var filePath=file.File=="module.dll"?Path.Combine(path,moduleName):FilePath(build,file,path);
                if(hostShared&&file.Shared){
                    if(capture.HashFile(filePath)!=file.Sha256){throw new ManagementException("BuildMismatch","現在のホストの共有依存DLLが成果物のビルド時と一致しません: "+file.Name);}
                }else{
                    if(Convert.ToHexString(SHA256.HashData(capture.Read(filePath)))!=file.Sha256){throw new ManagementException("BuildMismatch","保管済み成果物が変更されています: "+file.File);}
                }
            }
            var module=Path.Combine(path,moduleName);
            using(var pe=new PEReader(new MemoryStream(capture.Read(module),false))){
                if(!pe.HasMetadata||pe.GetMetadataReader().GetGuid(pe.GetMetadataReader().GetModuleDefinition().Mvid)!=build.ModuleVersionId){
                    throw new ManagementException("BuildMismatch","保管済みDLLのモジュール識別情報が異なります。");
                }
                if(build.PdbSha256.Length==0&&!pe.ReadDebugDirectory().Any(entry=>entry.Type==DebugDirectoryEntryType.EmbeddedPortablePdb)){
                    throw new ManagementException("BuildMismatch","DLLに埋め込みPDBがありません。");
                }
            }
            var dependencyPaths=files.Where(file=>file.Name.Length>0).ToDictionary(file=>file.Name,file=>FilePath(build,file,path),StringComparer.OrdinalIgnoreCase);
            var dependencies=SourceBuildArtifactInspector.CheckDependencies(module,capture.Read(module),capture,dependencyPaths);
            if(dependencies.Length!=build.Dependencies.Length||dependencies.Any(actual=>!build.Dependencies.Any(expected=>
                expected.Name==actual.Name&&expected.Version==actual.Version&&expected.Sha256==actual.Sha256&&expected.Shared==actual.Shared))){
                throw new ManagementException("BuildMismatch","現在のホストと成果物の依存関係が異なります。");
            }
            return new(capture,build with{AssemblyPath=module,Dependencies=dependencies});
        }catch(BadImageFormatException error){capture.Dispose();throw new ManagementException("BuildMismatch","保管済みDLLが不正です。 "+error.Message);}
        catch(IOException error){capture.Dispose();throw new ManagementException("BuildMismatch","保管済み成果物を読み取れません。 "+error.Message);}
        catch(ResourceLimitException error){capture.Dispose();throw new ManagementException("Limit","成果物の読み出し予算を超えています。 "+error.Message);}
        catch{capture.Dispose();throw;}
    }
    internal static void WriteGeneration(CapturedSourceBuild captured,string directory,string? moduleName=null){
        var build=captured.Build;var files=StoredFiles(build);
        var paths=StoredPaths(build).ToArray();
        for(var index=0;index<files.Length;index++){
            var bytes=captured.Bytes(paths[index]);
            if(Convert.ToHexString(SHA256.HashData(bytes))!=files[index].Sha256){throw new ManagementException("BuildMismatch","捕捉済み成果物が変更されています。");}
            var name=files[index].File;
            if(moduleName is not null&&name=="module.dll"){name=moduleName;}
            Write(Path.Combine(directory,name),bytes,CancellationToken.None);
        }
        if(moduleName is null){Write(Path.Combine(directory,"source-build.json"),ManifestBytes(build),CancellationToken.None);}
    }
}

/// <summary>検査とロードの間で同じバッファを所有し、破棄時に全予約を返却する。</summary>
internal sealed class CapturedSourceBuild:IDisposable{
    private sealed class SharedFiles(SourceBuildArtifactInspector.Capture capture){
        internal readonly object Gate=new();
        internal readonly SourceBuildArtifactInspector.Capture Capture=capture;
        internal int References=1;
    }
    private readonly SharedFiles files;
    private int disposed;
    internal SourceProjectBuild Build{get;}
    internal CapturedSourceBuild(SourceBuildArtifactInspector.Capture capture,SourceProjectBuild build):this(new SharedFiles(capture),build){}
    private CapturedSourceBuild(SharedFiles files,SourceProjectBuild build){this.files=files;this.Build=build;}
    internal CapturedSourceBuild Retain(){
        lock(this.files.Gate){
            ObjectDisposedException.ThrowIf(this.disposed!=0,this);
            this.files.References++;
            return new(this.files,this.Build);
        }
    }
    internal byte[] AssemblyBytes=>this.Bytes(this.Build.AssemblyPath);
    internal byte[] PdbBytes=>this.Bytes(Path.ChangeExtension(this.Build.AssemblyPath,".pdb"));
    internal byte[] Bytes(string path){ObjectDisposedException.ThrowIf(this.disposed!=0,this);return this.files.Capture.Files[Path.GetFullPath(path)];}
    internal byte[]? PrivateDependency(string name){
        var dependency=this.Build.Dependencies.SingleOrDefault(item=>!item.Shared&&string.Equals(item.Name,name,StringComparison.OrdinalIgnoreCase));
        if(dependency is null){return null;}
        return this.Bytes(dependency.Path);
    }
    public void Dispose(){
        lock(this.files.Gate){
            if(Interlocked.Exchange(ref this.disposed,1)!=0){return;}
            if(--this.files.References==0){this.files.Capture.Dispose();}
        }
    }
}
