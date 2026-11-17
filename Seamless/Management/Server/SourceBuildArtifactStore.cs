using System.Security.Cryptography;
using System.Text.Json;
using Seamless.Execution;

namespace Seamless.Management.Server;

/// <summary>検査済みバッファを内容アドレスで保管する。下書きへの束縛は管理取引で行う。</summary>
internal sealed partial class SourceBuildArtifactStore(string root,object gate,long maximumStorageBytes=1L<<30,int maximumPackages=1024){
    private const long MaximumFileBytes=64L<<20;
    private readonly string directory=Path.Combine(Path.GetFullPath(root),"source-builds");
    private readonly ResourceBudget budget=new(128L<<20,maximumStorageBytes);
    private sealed record PackageFile(string File,string Sha256,string Name="",string Version="",bool Shared=false);
    private sealed record PackageManifest(int Format,PackageFile[] Files);

    internal static string BundleHash(SourceProjectBuild build)=>Convert.ToHexString(SHA256.HashData(ManifestBytes(build)));
    private static PackageFile[] Files(SourceProjectBuild build)=>[
        new("module.dll",build.AssemblySha256),new("module.pdb",build.PdbSha256),
        ..build.Dependencies.OrderBy(item=>item.Name,StringComparer.Ordinal).Select((item,index)=>
            new PackageFile("dependency-"+index.ToString("D3",System.Globalization.CultureInfo.InvariantCulture)+".dll",item.Sha256,item.Name,item.Version,item.Shared))];
    private static byte[] ManifestBytes(SourceProjectBuild build,int format)=>JsonSerializer.SerializeToUtf8Bytes(new PackageManifest(format,Files(build)),ManagementJson.Options);
    private static int ManifestFormat(SourceProjectBuild build){
        var legacy=ManifestBytes(build,1);
        if(build.BundleHash==Convert.ToHexString(SHA256.HashData(legacy))){return 1;}
        return 2;
    }
    private static byte[] ManifestBytes(SourceProjectBuild build)=>ManifestBytes(build,ManifestFormat(build));
    private static PackageFile[] StoredFiles(SourceProjectBuild build){
        var files=Files(build);
        if(ManifestFormat(build)==1){return files;}
        return files.Where(file=>!file.Shared).ToArray();
    }
    private static IEnumerable<SourceBuildDependency> StoredDependencies(SourceProjectBuild build){
        if(ManifestFormat(build)==1){return build.Dependencies.OrderBy(item=>item.Name,StringComparer.Ordinal);}
        return build.Dependencies.Where(item=>!item.Shared).OrderBy(item=>item.Name,StringComparer.Ordinal);
    }
    private static string FilePath(SourceProjectBuild build,PackageFile file,string directory){
        if(ManifestFormat(build)==1||!file.Shared){return Path.Combine(directory,file.File);}
        return CompilationReferences.Paths.FirstOrDefault(path=>string.Equals(Path.GetFileNameWithoutExtension(path),file.Name,StringComparison.OrdinalIgnoreCase))
            ??throw new ManagementException("BuildMismatch","共有依存DLLがホストにありません: "+file.Name);
    }

    internal SourceProjectBuild Capture(SourceBuildInspectionContext context,string path,string expectedBundleHash,CancellationToken cancellationToken)=>
        SourceBuildArtifactInspector.Inspect(context,path,cancellationToken,(build,captured)=>{
            if(build.BundleHash!=expectedBundleHash){throw new ManagementException("Conflict","検査後に成果物が変更されています。再検査してください。");}
            this.Store(build,captured,cancellationToken);
        });

    internal void Store(SourceProjectBuild build,IReadOnlyDictionary<string,byte[]> captured,CancellationToken cancellationToken){
        lock(gate){
            cancellationToken.ThrowIfCancellationRequested();
            var destination=this.PathFor(build);
            if(Directory.Exists(destination)){this.VerifyDirectory(build,destination,cancellationToken);return;}
            SourceWorkspace.CheckLinks(this.directory);
            var entries=this.StorageUsage(cancellationToken);
            using var previous=this.budget.ReserveTemporaryStorage(entries.Bytes);
            if(entries.Packages>=maximumPackages){throw new ManagementException("Limit","保管済み成果物の件数上限です。");}
            var files=StoredFiles(build);var manifest=ManifestBytes(build);
            var sources=new[]{build.AssemblyPath,Path.ChangeExtension(build.AssemblyPath,".pdb")}
                .Concat(StoredDependencies(build).Select(item=>item.Path)).ToArray();
            var buffers=sources.Select(path=>captured[Path.GetFullPath(path)]).ToArray();
            using var addition=this.budget.ReserveTemporaryStorage(buffers.Sum(bytes=>(long)bytes.Length)+manifest.Length);
            Directory.CreateDirectory(this.directory);
            var staging=Path.Combine(this.directory,".incoming-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);
            try{
                for(var index=0;index<files.Length;index++){
                    if(Convert.ToHexString(SHA256.HashData(buffers[index]))!=files[index].Sha256){throw new ManagementException("BuildMismatch","検査済みバッファと成果物のハッシュが異なります。");}
                    Write(Path.Combine(staging,files[index].File),buffers[index],cancellationToken);
                }
                Write(Path.Combine(staging,"manifest.json"),manifest,cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                Directory.Move(staging,destination);
            }finally{
                if(Directory.Exists(staging)){
                    // stagingはこの呼び出しが生成した直下のGUID名。リンクへの差し替えは削除前に拒否する。
                    SourceWorkspace.CheckLinks(staging);
                    if(Path.GetDirectoryName(Path.GetFullPath(staging))!=this.directory){throw new IOException("成果物の一時領域が管理領域外です。");}
                    Directory.Delete(staging,true);
                }
            }
        }
    }
    internal void Verify(SourceProjectBuild build,CancellationToken cancellationToken=default){
        lock(gate){this.VerifyDirectory(build,this.PathFor(build),cancellationToken);}
    }
    private string PathFor(SourceProjectBuild build){
        if(build.BundleHash.Length!=64||build.BundleHash.Any(character=>!char.IsAsciiHexDigit(character))||build.BundleHash!=BundleHash(build)){
            throw new ManagementException("BuildMismatch","成果物の識別情報が一致しません。");
        }
        var path=Path.Combine(this.directory,build.BundleHash);
        SourceWorkspace.CheckLinks(path);return path;
    }
    private void VerifyDirectory(SourceProjectBuild build,string path,CancellationToken cancellationToken){
        var manifest=ManifestBytes(build);
        using var reservation=this.budget.ReserveMemory(manifest.Length+65536);
        var manifestPath=Path.Combine(path,"manifest.json");SourceWorkspace.CheckLinks(manifestPath);
        using(var stream=new FileStream(manifestPath,FileMode.Open,FileAccess.Read,FileShare.Read)){
            if(stream.Length!=manifest.Length){throw new ManagementException("BuildMismatch","保管済み成果物のマニフェストが異なります。");}
            var bytes=new byte[manifest.Length];stream.ReadExactly(bytes);
            if(!bytes.AsSpan().SequenceEqual(manifest)){throw new ManagementException("BuildMismatch","保管済み成果物のマニフェストが異なります。");}
        }
        var buffer=new byte[65536];
        foreach(var file in Files(build)){
            cancellationToken.ThrowIfCancellationRequested();
            var filePath=FilePath(build,file,path);SourceWorkspace.CheckLinks(filePath);
            using var stream=new FileStream(filePath,FileMode.Open,FileAccess.Read,FileShare.Read);
            if(stream.Length is <=0 or >MaximumFileBytes){throw new ManagementException("BuildMismatch","保管済み成果物のサイズが不正です。");}
            using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            int count;
            while((count=stream.Read(buffer))>0){cancellationToken.ThrowIfCancellationRequested();hash.AppendData(buffer,0,count);}
            if(Convert.ToHexString(hash.GetHashAndReset())!=file.Sha256){throw new ManagementException("BuildMismatch","保管済み成果物が変更されています: "+file.File);}
        }
    }
    private (long Bytes,int Packages) StorageUsage(CancellationToken cancellationToken){
        long bytes=0;var packages=0;
        if(!Directory.Exists(this.directory)){return(0,0);}
        foreach(var entry in Directory.EnumerateFileSystemEntries(this.directory)){
            cancellationToken.ThrowIfCancellationRequested();SourceWorkspace.CheckLinks(entry);
            if(!Directory.Exists(entry)||++packages>maximumPackages){throw new ManagementException("Limit","成果物保管領域の件数が上限外です。");}
            var count=0;
            foreach(var file in Directory.EnumerateFileSystemEntries(entry)){
                SourceWorkspace.CheckLinks(file);
                if(!File.Exists(file)||++count>256){throw new ManagementException("Limit","成果物保管領域の構成が上限外です。");}
                bytes=checked(bytes+new FileInfo(file).Length);
                if(bytes>maximumStorageBytes){throw new ManagementException("Limit","成果物保管領域のサイズ上限です。");}
            }
        }
        return(bytes,packages);
    }
    private static void Write(string path,byte[] bytes,CancellationToken cancellationToken){
        using var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);
        for(var offset=0;offset<bytes.Length;offset+=65536){
            cancellationToken.ThrowIfCancellationRequested();stream.Write(bytes,offset,Math.Min(65536,bytes.Length-offset));
        }
        stream.Flush(true);
    }
}
