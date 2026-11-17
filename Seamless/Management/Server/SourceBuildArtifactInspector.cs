using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using Seamless.Database;
using Seamless.Execution;

namespace Seamless.Management.Server;

internal static class SourceBuildArtifactInspector{
    private const int MaximumFileBytes=64<<20;
    private const int MaximumFiles=128;
    private static readonly ResourceBudget SharedBudget=new(256L<<20,0);
    private static readonly Guid CompilationOptionsKind=new("b5feec05-8cd0-4a83-96da-466284bb4bd8");
    private static StringComparer PathComparer{get{if(OperatingSystem.IsWindows()){return StringComparer.OrdinalIgnoreCase;}return StringComparer.Ordinal;}}

    internal static SourceProjectBuild Inspect(SourceBuildInspectionContext context,string assemblyPath,CancellationToken cancellationToken)=>
        Inspect(context,assemblyPath,SharedBudget,cancellationToken);

    internal static SourceProjectBuild Inspect(SourceBuildInspectionContext context,string assemblyPath,CancellationToken cancellationToken,
        Action<SourceProjectBuild,IReadOnlyDictionary<string,byte[]>> captured)=>Inspect(context,assemblyPath,SharedBudget,cancellationToken,captured);

    internal static SourceProjectBuild Inspect(SourceBuildInspectionContext context,string assemblyPath,ResourceBudget budget,CancellationToken cancellationToken,
        Action<SourceProjectBuild,IReadOnlyDictionary<string,byte[]>>? captured=null){
        cancellationToken.ThrowIfCancellationRequested();
        try{
            using var capture=new Capture(budget.CreateChild(128L<<20,0),cancellationToken);
            var path=Path.GetFullPath(assemblyPath);
            var image=capture.Read(path);
            var pdb=capture.Read(Path.ChangeExtension(path,".pdb"));
            using var pe=new PEReader(new MemoryStream(image,false));
            if(!pe.HasMetadata||pe.PEHeaders.CorHeader is not{} cor||(cor.Flags&CorFlags.Requires32Bit)!=0){throw Mismatch("対応するマネージドDLLが必要です。");}
            var metadata=pe.GetMetadataReader();
            if(!metadata.IsAssembly||!metadata.TypeDefinitions.Any(handle=>TypeName(metadata,handle)==context.Current.Definition.EntryType)){
                throw Mismatch("DB定義の入口型がDLLにありません。");
            }
            using var provider=MetadataReaderProvider.FromPortablePdbStream(new MemoryStream(pdb,false));
            var symbols=provider.GetMetadataReader();
            var id=symbols.DebugMetadataHeader?.Id.ToArray()??[];
            if(id.Length!=20||!pe.ReadDebugDirectory().Any(entry=>{
                if(entry.Type!=DebugDirectoryEntryType.CodeView){return false;}
                var codeView=pe.ReadCodeViewDebugDirectoryData(entry);
                return codeView.Guid==new Guid(id.AsSpan(0,16))&&codeView.Age==1&&entry.Stamp==BitConverter.ToUInt32(id,16);
            })){throw Mismatch("DLLとPDBの識別情報が一致しません。");}
            CheckSources(context,symbols,capture);
            var dependencies=CheckDependencies(path,image,capture);
            var result=new SourceProjectBuild(context.ProjectId,context.Current.Definition.Id,context.Current.DraftRevision,context.Current.BaseVersion,
                context.Current.Definition.Hash(),context.Current.Definition.Source.Hash(),path,Hash(image),Hash(pdb),
                metadata.GetGuid(metadata.GetModuleDefinition().Mvid),dependencies);
            result=result with{BundleHash=SourceBuildArtifactStore.BundleHash(result)};
            captured?.Invoke(result,capture.Files);
            return result;
        }catch(BadImageFormatException error){throw Mismatch("DLLまたはPortable PDBが不正です。 "+error.Message);}
        catch(IOException error){throw Mismatch("成果物を読み取れません。 "+error.Message);}
        catch(ResourceLimitException error){throw new ManagementException("Limit","成果物の検査予算を超えています。 "+error.Message);}
    }

    private static void CheckSources(SourceBuildInspectionContext context,MetadataReader symbols,Capture capture){
        if(symbols.Documents.Count>4096||symbols.CustomDebugInformation.Count>16384){throw Mismatch("PDBの文書・設定件数が上限を超えています。");}
        var options=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var handle in symbols.CustomDebugInformation){
            var data=symbols.GetCustomDebugInformation(handle);
            if(symbols.GetGuid(data.Kind)!=CompilationOptionsKind){continue;}
            var parts=Encoding.UTF8.GetString(symbols.GetBlobBytes(data.Value)).Split('\0');
            for(var index=0;index+1<parts.Length;index+=2){
                if(!options.TryAdd(parts[index],parts[index+1])){throw Mismatch("PDBのコンパイル設定が重複しています。");}
            }
        }
        if(!options.TryGetValue("language",out var language)||language!="C#"||
            !options.TryGetValue("language-version",out var languageVersion)||!Version.TryParse(languageVersion,out var version)||version.Major!=14){
            throw Mismatch("PDBにC# 14のコンパイル設定が必要です。");
        }
        options.TryGetValue("define",out var defines);
        var actual=(defines??"").Split([',',';'],StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
        if(!actual.SequenceEqual((context.Current.Definition.Source.Defines??[]).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))){
            throw Mismatch("ビルド時の条件定義とDB下書きが異なります。");
        }
        var documents=new Dictionary<string,Document>(PathComparer);
        foreach(var handle in symbols.Documents){
            var document=symbols.GetDocument(handle);
            var name=symbols.GetString(document.Name);
            if(Path.IsPathFullyQualified(name)){documents.TryAdd(Path.GetFullPath(name),document);}
        }
        foreach(var source in context.Current.Definition.Source.Files){
            var file=context.Binding.Files.Single(item=>item.Path==source.Path);
            var bytes=capture.Read(file.FullPath);
            using var text=new StreamReader(new MemoryStream(bytes,false),Encoding.UTF8,true);
            if(text.ReadToEnd()!=source.Text){throw Mismatch("ビルド確認中にソースが変更されました: "+source.Path);}
            if(!documents.TryGetValue(Path.GetFullPath(file.FullPath),out var document)||document.Hash.IsNil){
                throw Mismatch("PDBに元ソースのチェックサムがありません: "+source.Path);
            }
            var algorithm=symbols.GetGuid(document.HashAlgorithm);
            byte[] expected;
            if(algorithm==new Guid("8829d00f-11b8-4213-878b-770e8597ac16")){expected=SHA256.HashData(bytes);}
            else if(algorithm==new Guid("ff1816ec-aa5e-4d10-87f7-6f4963833460")){expected=SHA1.HashData(bytes);}
            else{throw Mismatch("PDBのソースチェックサム形式に対応していません。");}
            if(!expected.AsSpan().SequenceEqual(symbols.GetBlobBytes(document.Hash))){throw Mismatch("DLLの元ソースと下書きが異なります: "+source.Path);}
        }
    }

    internal static Capture CaptureFiles(CancellationToken cancellationToken)=>new(SharedBudget.CreateChild(128L<<20,0),cancellationToken);
    internal static SourceBuildDependency[] CheckDependencies(string path,byte[] image,Capture capture,IReadOnlyDictionary<string,string>? storedPaths=null){
        var platformPaths=global::Seamless.Execution.CompilationReferences.Paths
            .GroupBy(Path.GetFileNameWithoutExtension,StringComparer.OrdinalIgnoreCase).ToDictionary(group=>group.Key!,group=>group.First(),StringComparer.OrdinalIgnoreCase);
        platformPaths[typeof(DatabaseContainer).Assembly.GetName().Name!]=global::Seamless.Execution.CompilationReferences.PathFor(typeof(DatabaseContainer).Assembly);

        var result=new Dictionary<string,SourceBuildDependency>(StringComparer.OrdinalIgnoreCase);
        var pending=new Queue<(byte[]? Bytes,string? Path,string? Hash)>();pending.Enqueue((image,null,null));
        var directory=Path.GetDirectoryName(path)!.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
        System.Runtime.Loader.AssemblyDependencyResolver? resolver=null;
        var definition=Path.ChangeExtension(path,".deps.json");
        if(storedPaths is null&&File.Exists(definition)){
            var definitionHash=capture.HashFile(definition);
            try{resolver=new(path);}catch(InvalidOperationException error){throw Mismatch("依存定義を解決できません。 "+error.Message);}
            if(capture.HashFile(definition)!=definitionHash){throw Mismatch("依存定義が検査中に変更されています。");}
        }
        while(pending.Count>0){
            var current=pending.Dequeue();
            Stream input;
            if(current.Bytes is not null){input=new MemoryStream(current.Bytes,false);}
            else{
                SourceWorkspace.CheckLinks(current.Path!);
                input=new FileStream(current.Path!,FileMode.Open,FileAccess.Read,FileShare.Read);
            }
            using var ownedInput=input;
            using var readBuffer=capture.ReserveMemory(65536);
            if(current.Bytes is null){
                if(Convert.ToHexString(SHA256.HashData(input))!=current.Hash){throw Mismatch("共有依存DLLが検査中に変更されています。");}
                input.Position=0;
            }
            using var pe=new PEReader(input,PEStreamOptions.LeaveOpen);
            using var metadataMemory=capture.ReserveMemory(pe.PEHeaders.CorHeader?.MetadataDirectory.Size??throw Mismatch("依存DLLに管理メタデータがありません。"));
            var reader=pe.GetMetadataReader();
            foreach(var handle in reader.AssemblyReferences){
                var reference=reader.GetAssemblyReference(handle);
                var name=Name(reader,reference);
                if(platformPaths.TryGetValue(name.Name!,out var runtime)&&global::Seamless.Execution.CompilationReferences.IsFramework(Assembly.Load(name))){
                    if(!CanBindReference(name,AssemblyName.GetAssemblyName(runtime))){throw Mismatch("フレームワークの版が異なります: "+name.Name);}
                    continue;
                }
                var dependencyPath=Path.Combine(directory,name.Name+".dll");
                if(storedPaths is not null&&!storedPaths.TryGetValue(name.Name!,out dependencyPath)){
                    throw Mismatch("保管済み成果物に依存DLLがありません: "+name.Name);
                }
                if(storedPaths is null&&resolver is not null){
                    dependencyPath=resolver.ResolveAssemblyToPath(name)??throw Mismatch("実行時の依存DLLがありません: "+name.Name);
                    dependencyPath=Path.GetFullPath(dependencyPath);
                    if(!dependencyPath.StartsWith(directory,PathComparison)){
                        throw Mismatch("依存DLLが成果物の出力先にありません: "+name.Name);
                    }
                }
                var isShared=runtime is not null;
                byte[]? bytes=null;AssemblyName identity;string hash;
                if(isShared){
                    hash=capture.HashFile(dependencyPath);
                    if(capture.HashFile(runtime!)!=hash){throw Mismatch("サーバーと異なるビルドの依存DLLです: "+name.Name);}
                    identity=AssemblyName.GetAssemblyName(runtime!);
                }else{
                    bytes=capture.Read(dependencyPath);
                    using var dependency=new PEReader(new MemoryStream(bytes,false));
                    var metadata=dependency.GetMetadataReader();
                    identity=Name(metadata,metadata.GetAssemblyDefinition());hash=Hash(bytes);
                }
                if(!CanBindReference(name,identity)){throw Mismatch("依存DLLの名前・版が一致しません: "+name.FullName+" / "+identity.FullName);}
                if(result.TryGetValue(name.Name!,out var previous)){
                    if(previous.Sha256!=hash){throw Mismatch("依存DLLの内容が重複しています: "+name.Name);}
                    continue;
                }
                if(result.Count>=MaximumFiles){throw Mismatch("依存DLLの件数上限を超えています。");}
                result.Add(name.Name!,new(name.Name!,identity.Version!.ToString(),dependencyPath,hash,isShared));
                pending.Enqueue((bytes,runtime,hash));
            }
        }
        return result.Values.OrderBy(item=>item.Name,StringComparer.Ordinal).ToArray();
    }
    private static StringComparison PathComparison{get{if(OperatingSystem.IsWindows()){return StringComparison.OrdinalIgnoreCase;}return StringComparison.Ordinal;}}
    private static AssemblyName Name(MetadataReader reader,AssemblyReference reference){
        var result=new AssemblyName(reader.GetString(reference.Name)){Version=reference.Version};
        if(!reference.Culture.IsNil){result.CultureName=reader.GetString(reference.Culture);}
        var key=reader.GetBlobBytes(reference.PublicKeyOrToken);
        if((reference.Flags&AssemblyFlags.PublicKey)!=0){result.SetPublicKey(key);}else{result.SetPublicKeyToken(key);}
        return result;
    }
    private static AssemblyName Name(MetadataReader reader,AssemblyDefinition definition){
        var result=new AssemblyName(reader.GetString(definition.Name)){Version=definition.Version};
        if(!definition.Culture.IsNil){result.CultureName=reader.GetString(definition.Culture);}
        result.SetPublicKey(reader.GetBlobBytes(definition.PublicKey));return result;
    }
    // CLRの参照解決は要求以上の版を許す。サーバー共有DLLの同一ビルドは別途ハッシュで検査する。
    internal static bool CanBindReference(AssemblyName requested,AssemblyName loaded)=>
        SameNameAndKey(requested,loaded)&&requested.Version is not null&&loaded.Version>=requested.Version;
    private static bool SameNameAndKey(AssemblyName left,AssemblyName right)=>
        string.Equals(left.Name,right.Name,StringComparison.OrdinalIgnoreCase)&&
        string.Equals(left.CultureName??"",right.CultureName??"",StringComparison.OrdinalIgnoreCase)&&
        (left.GetPublicKeyToken()??[]).AsSpan().SequenceEqual(right.GetPublicKeyToken()??[]);
    private static string TypeName(MetadataReader reader,TypeDefinitionHandle handle){
        var type=reader.GetTypeDefinition(handle);var name=reader.GetString(type.Name);
        if(!type.GetDeclaringType().IsNil){return TypeName(reader,type.GetDeclaringType())+"+"+name;}
        var ns=reader.GetString(type.Namespace);if(ns.Length==0){return name;}return ns+"."+name;
    }
    private static string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes));
    private static ManagementException Mismatch(string message)=>new("BuildMismatch",message);

    internal sealed class Capture(ResourceBudget budget,CancellationToken cancellationToken):IDisposable{
        private readonly Dictionary<string,byte[]> files=new(PathComparer);
        private readonly List<ResourceReservation> reservations=[];
        private bool disposed;
        internal IReadOnlyDictionary<string,byte[]> Files=>this.files;
        public byte[] Read(string path){
            ObjectDisposedException.ThrowIf(this.disposed,this);
            cancellationToken.ThrowIfCancellationRequested();path=Path.GetFullPath(path);
            if(this.files.TryGetValue(path,out var existing)){return existing;}
            if(this.files.Count>=MaximumFiles){throw Mismatch("成果物のファイル件数上限を超えています。");}
            SourceWorkspace.CheckLinks(path);
            using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
            if(stream.Length is <=0 or >MaximumFileBytes){throw Mismatch("成果物ファイルのサイズが上限外です。");}
            var reservation=budget.ReserveMemory(checked(stream.Length*2+65536));
            this.reservations.Add(reservation);
            var bytes=new byte[checked((int)stream.Length)];
            var offset=0;
            while(offset<bytes.Length){
                cancellationToken.ThrowIfCancellationRequested();var count=stream.Read(bytes,offset,Math.Min(bytes.Length-offset,65536));
                if(count==0){throw Mismatch("読み取り中に成果物の長さが変わりました。");}offset+=count;
            }
            if(stream.ReadByte()!=-1){throw Mismatch("読み取り中に成果物が変更されました。");}
            this.files.Add(path,bytes);return bytes;
        }
        internal ResourceReservation ReserveMemory(long bytes)=>budget.ReserveMemory(bytes);
        internal string HashFile(string path){
            ObjectDisposedException.ThrowIf(this.disposed,this);
            cancellationToken.ThrowIfCancellationRequested();
            SourceWorkspace.CheckLinks(path);
            using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
            if(stream.Length is <=0 or >MaximumFileBytes){throw Mismatch("成果物ファイルのサイズが上限外です。");}
            using var reservation=budget.ReserveMemory(65536);
            var hash=Convert.ToHexString(SHA256.HashData(stream));
            cancellationToken.ThrowIfCancellationRequested();
            return hash;
        }
        public void Dispose(){this.disposed=true;this.files.Clear();foreach(var reservation in this.reservations){reservation.Dispose();}this.reservations.Clear();}
    }
}
