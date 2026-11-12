using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace Seamless.Management;

/// <summary>所有者が開いた既存プロジェクトを、評価済みCompile対象と既存の同期基盤へ接続する。</summary>
public sealed partial class SourceProjectService:IManagementSourceOperations,IDisposable{
    private sealed record OpenProject(string Path,string TargetFramework,string[] TargetFrameworks,SourceWorkspace Workspace,string[] Configurations){
        internal string Configuration=>this.Workspace.State.ProjectBinding!.Configuration;
        internal string[] ReferenceTypes{get;set;}=[];
        internal string? TypeCompletionDiagnostic{get;set;}
        internal string? ReferenceStamp{get;set;}
    }
    private readonly SemaphoreSlim gate=new(1,1);
    private readonly CancellationTokenSource stopping=new();
    private readonly Dictionary<string,OpenProject> projects=new(StringComparer.Ordinal);
    private int disposalStarted;
    private readonly TaskCompletionSource disposed=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static StringComparer PathComparer{get{if(OperatingSystem.IsWindows()){return StringComparer.OrdinalIgnoreCase;}return StringComparer.Ordinal;}}
    public async Task<SourceDirectory> BrowseSourceDirectoryAsync(string path,CancellationToken cancellationToken=default){
        using var cancellation=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,this.stopping.Token);
        await this.gate.WaitAsync(cancellation.Token);
        try{
            return await Task.Run(()=>ReadDirectory(path,cancellation.Token),cancellation.Token).ConfigureAwait(false);
        }finally{this.gate.Release();}
    }
    private static SourceDirectory ReadDirectory(string path,CancellationToken cancellationToken){
        if(string.IsNullOrWhiteSpace(path)){
            if(OperatingSystem.IsWindows()){
                var drives=DriveInfo.GetDrives().Where(drive=>drive.IsReady).Select(drive=>new SourceDirectoryEntry(drive.Name,drive.Name,true)).ToArray();
                return new("",null,drives);
            }
            path=Path.DirectorySeparatorChar.ToString();
        }
        path=FullPath(path);
        if(File.Exists(path)){path=Path.GetDirectoryName(path)!;}
        if(!Directory.Exists(path)){throw new ManagementException("NotFound","サーバー上のフォルダーが見つかりません。");}
        var entries=new List<SourceDirectoryEntry>();
        var scanned=0;
        foreach(var item in new DirectoryInfo(path).EnumerateFileSystemInfos("*",new EnumerationOptions{AttributesToSkip=FileAttributes.ReparsePoint,IgnoreInaccessible=false})){
            cancellationToken.ThrowIfCancellationRequested();
            if(++scanned>20000){throw new ManagementException("Limit","項目が多すぎます。サーバー側のパスを直接指定してください。");}
            var directory=(item.Attributes&FileAttributes.Directory)!=0;
            if(directory){
                if(item.Name is "bin" or "obj" or ".git" or ".vs" or ".seamless" or "node_modules"){continue;}
            }else if(!item.Extension.Equals(".slnx",StringComparison.OrdinalIgnoreCase)&&!item.Extension.Equals(".csproj",StringComparison.OrdinalIgnoreCase)){continue;}
            entries.Add(new(item.Name,item.FullName,directory));
            if(entries.Count>4096){throw new ManagementException("Limit","表示対象が多すぎます。サーバー側のパスを直接指定してください。");}
        }
        var parent=Directory.GetParent(path)?.FullName;
        if(parent is null&&OperatingSystem.IsWindows()){parent="";}
        return new(path,parent,entries.OrderByDescending(entry=>entry.IsDirectory).ThenBy(entry=>entry.Name,PathComparer).ToArray());
    }
    public async Task<SourceProjectLocation> OpenSourceLocationAsync(string path,CancellationToken cancellationToken=default){
        using var cancellation=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,this.stopping.Token);
        await this.gate.WaitAsync(cancellation.Token);
        try{
            return await Task.Run(()=>ReadLocation(path,cancellation.Token),cancellation.Token).ConfigureAwait(false);
        }finally{this.gate.Release();}
    }
    private static SourceProjectLocation ReadLocation(string path,CancellationToken cancellationToken){
            path=FullPath(path);
            if(File.Exists(path)&&Path.GetExtension(path).Equals(".csproj",StringComparison.OrdinalIgnoreCase)){return new(path,[path]);}
            var result=new List<string>();
            if(File.Exists(path)&&Path.GetExtension(path).Equals(".slnx",StringComparison.OrdinalIgnoreCase)){
                using var reader=XmlReader.Create(path,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=4*1024*1024});
                var solution=XDocument.Load(reader);
                foreach(var item in solution.Descendants("Project")){
                    cancellationToken.ThrowIfCancellationRequested();
                    var relative=(string?)item.Attribute("Path");
                    if(relative is null||!relative.EndsWith(".csproj",StringComparison.OrdinalIgnoreCase)){continue;}
                    var project=FullPath(Path.Combine(Path.GetDirectoryName(path)!,relative.Replace('\\',Path.DirectorySeparatorChar)));
                    if(!File.Exists(project)){throw new ManagementException("NotFound","プロジェクトがありません: "+project);}
                    result.Add(project);if(result.Count>512){throw new ManagementException("Limit","一度に開けるプロジェクトは512件までです。");}
                }
            }else if(Directory.Exists(path)){
                var pending=new Stack<string>();pending.Push(path);var directories=0;
                while(pending.TryPop(out var directory)){
                    if(++directories>20000){throw new ManagementException("Limit","検索対象のフォルダーが多すぎます。");}
                    foreach(var file in Directory.EnumerateFiles(directory,"*.csproj")){result.Add(FullPath(file));if(result.Count>512){throw new ManagementException("Limit","一度に開けるプロジェクトは512件までです。");}}
                    foreach(var child in Directory.EnumerateDirectories(directory)){
                        cancellationToken.ThrowIfCancellationRequested();
                        if(Path.GetFileName(child) is "bin" or "obj" or ".git" or ".vs" or ".seamless" or "node_modules"){continue;}
                        if((File.GetAttributes(child)&FileAttributes.ReparsePoint)!=0){continue;}
                        pending.Push(child);
                    }
                }
            }else{throw new ManagementException("InvalidRequest","サーバー側の.slnx・フォルダー・.csprojを指定してください。");}
            return new(path,result.Distinct(PathComparer).Order(PathComparer).ToArray());
    }
    public async Task<SourceProjectDocument> OpenSourceProjectAsync(SourceProjectOpenRequest request,CancellationToken cancellationToken=default){
        using var cancellation=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,this.stopping.Token);
        await this.gate.WaitAsync(cancellation.Token);
        try{
            var path=FullPath(request.Path);
            if(!path.EndsWith(".csproj",StringComparison.OrdinalIgnoreCase)||!File.Exists(path)){throw new ManagementException("InvalidRequest","既存の.csprojが必要です。");}
            ValidateConfiguration(request.Configuration);
            var evaluation=await EvaluateAsync(path,request.TargetFramework,cancellation.Token,request.Configuration).ConfigureAwait(false);
            var properties=evaluation.GetProperty("Properties");
            var configurations=Configurations(properties,request.Configuration);
            var frameworks=(properties.GetProperty("TargetFrameworks").GetString()??"").Split(';',StringSplitOptions.RemoveEmptyEntries);
            var target=properties.GetProperty("TargetFramework").GetString()??"";
            if(frameworks.Length==0){frameworks=[target];}
            var id=ProjectId(path);
            if(frameworks.Length>1&&request.TargetFramework.Length==0){return new(id,path,frameworks,"",[],null){Configuration=request.Configuration,Configurations=configurations};}
            if(!frameworks.Contains(target,StringComparer.Ordinal)||request.TargetFramework.Length>0&&target!=request.TargetFramework){throw new ManagementException("InvalidRequest","プロジェクトのターゲットが一致しません。");}
            if(target!="net10.0"){throw new ManagementException("Unsupported","現在のDBソース同期はnet10.0を対象にしています。");}
            var files=ReadFiles(path,evaluation);
            var source=new SourceSnapshot("project",files.Select(file=>new SourceFile(file.Path,File.ReadAllText(file.FullPath))).ToArray(),[],Defines:Defines(evaluation));
            SourceEditor.Validate(source);
            if(this.projects.Count>=16&&!this.projects.ContainsKey(id)){throw new ManagementException("Limit","同時に開けるソースプロジェクトは16件までです。");}
            var workspace=SourceWorkspace.ImportProject(new(path,files){Configuration=request.Configuration},source);
            var pending=workspace.PendingConflictSource();
            var current=workspace.Current;
            if(pending is null){current=workspace.Synchronize(current.Definition);}
            var opened=new OpenProject(path,target,frameworks,workspace,configurations);
            await RefreshReferenceTypesAsync(opened,evaluation,cancellation.Token).ConfigureAwait(false);
            this.projects[id]=opened;
            return Document(id,opened,pending??current.Definition.Source) with{HasConflict=pending is not null};
        }finally{this.gate.Release();}
    }
    public async Task<SourceProjectDocument> SynchronizeSourceProjectAsync(SourceProjectSynchronization request,CancellationToken cancellationToken=default){
        var baseline=request.Baseline.Freeze();var source=request.Source.Freeze();
        SourceEditor.Validate(baseline);SourceEditor.Validate(source);
        using var cancellation=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,this.stopping.Token);
        await this.gate.WaitAsync(cancellation.Token);
        try{
            if(!this.projects.TryGetValue(request.Id,out var project)){throw new ManagementException("NotFound","ソースプロジェクトを開き直してください。");}
            var workspace=project.Workspace;
            await this.CheckProjectAsync(project,cancellation.Token).ConfigureAwait(false);
            var defines=workspace.Current.Definition.Source.Defines??[];
            if(!(baseline.Defines??[]).SequenceEqual(defines)||!(source.Defines??[]).SequenceEqual(defines)){
                throw new ManagementException("Conflict","表示中の条件定義が選択構成と異なります。編集を保持してプロジェクトを再評価してください。");
            }
            var paths=workspace.State.ProjectBinding!.Files.Select(file=>file.Path).Order(StringComparer.Ordinal).ToArray();
            if(!paths.SequenceEqual(source.Files.Select(file=>file.Path).Order(StringComparer.Ordinal))||!paths.SequenceEqual(baseline.Files.Select(file=>file.Path).Order(StringComparer.Ordinal))){throw new ManagementException("InvalidRequest","評価済みCompile対象以外は同期できません。");}
            var definition=workspace.Current.Definition;
            var result=workspace.Synchronize(definition with{Source=source},definition with{Source=baseline});
            return Document(request.Id,project,result.Definition.Source);
        }finally{this.gate.Release();}
    }
    public Task<SourceProjectDocument> CreateSourceProjectAsync(SourceProjectCreateRequest request,CancellationToken cancellationToken=default)=>
        this.CreateSourceProjectAsync(request,null,cancellationToken);
    internal async Task<SourceProjectDocument> CreateSourceProjectAsync(SourceProjectCreateRequest request,
        Func<string,CancellationToken,Task<Draft>>? getDraft,CancellationToken cancellationToken){
        var current=request.Current with{Definition=request.Current.Definition.Freeze()};
        using var cancellation=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,this.stopping.Token);
        await this.gate.WaitAsync(cancellation.Token);
        try{
            Draft? saved=null;
            if(getDraft is not null&&current.DraftRevision>0){
                saved=await getDraft(current.Definition.Id,cancellation.Token).ConfigureAwait(false);
                saved=saved with{Definition=saved.Definition.Freeze()};
                if(saved.Revision!=current.DraftRevision||saved.BaseVersion!=current.BaseVersion||saved.Definition.Hash()!=current.Definition.Hash()){
                    throw new ManagementException("Conflict","保管済みのDB下書きが変わっています。内容を確認してからプロジェクトを保存してください。");
                }
            }
            var directory=FullPath(request.DirectoryPath);
            if(Directory.Exists(directory)&&Directory.EnumerateFileSystemEntries(directory).Any()){
                throw new ManagementException("Conflict","空のプロジェクト保存先を指定してください。");
            }
            var (initialWorkspace,projectPath)=SourceWorkspace.CreatePackageProject(directory,current,current.Definition.Source,request.DatabaseName,request.ProjectName);
            if(saved is not null){initialWorkspace.MarkDraftSaved(saved);}
            var id=ProjectId(projectPath);
            var evaluation=await EvaluateAsync(projectPath,"net10.0",cancellation.Token).ConfigureAwait(false);
            var properties=evaluation.GetProperty("Properties");
            var frameworks=(properties.GetProperty("TargetFrameworks").GetString()??"").Split(';',StringSplitOptions.RemoveEmptyEntries);
            var target=properties.GetProperty("TargetFramework").GetString()??"";
            if(frameworks.Length==0){frameworks=[target];}
            var files=ReadFiles(projectPath,evaluation);
            var source=new SourceSnapshot("project",files.Select(file=>new SourceFile(file.Path,File.ReadAllText(file.FullPath))).ToArray(),[],Defines:Defines(evaluation));
            SourceEditor.Validate(source);
            if(this.projects.Count>=16&&!this.projects.ContainsKey(id)){throw new ManagementException("Limit","同時に開けるソースプロジェクトは16件までです。");}
            var workspace=SourceWorkspace.ImportProject(new(projectPath,files),source);
            var synchronized=workspace.Synchronize(workspace.Current.Definition);
            var opened=new OpenProject(projectPath,target,frameworks,workspace,Configurations(properties,"Debug"));
            await RefreshReferenceTypesAsync(opened,evaluation,cancellation.Token).ConfigureAwait(false);
            this.projects[id]=opened;
            return Document(id,opened,synchronized.Definition.Source);
        }finally{this.gate.Release();}
    }
    private static SourceProjectDocument Document(string id,OpenProject project,SourceSnapshot source){
        var result=new SourceProjectDocument(id,project.Path,project.TargetFrameworks,project.TargetFramework,Namespaces(source),source.Freeze()){
            Configuration=project.Configuration,Configurations=project.Configurations.ToArray(),ReferenceTypes=project.ReferenceTypes.ToArray(),TypeCompletionDiagnostic=project.TypeCompletionDiagnostic
        };
        if(project.Workspace.IsDatabaseProject){
            var current=project.Workspace.Current;
            result=result with{Database=new(current.Definition.Id,current.Definition.Database,current.BaseVersion,current.DraftRevision)};
        }
        return result;
    }
    public static string[] Namespaces(SourceSnapshot source)=>SourceStructure.Read(source).Namespaces;
    private static SourceProjectFile[] ReadFiles(string project,JsonElement evaluation){
        var root=Path.GetDirectoryName(project)!;var result=new List<SourceProjectFile>();var physical=new HashSet<string>(PathComparer);long length=0;
        foreach(var item in evaluation.GetProperty("Items").GetProperty("Compile").EnumerateArray()){
            var path=FullPath(item.GetProperty("FullPath").GetString()!);
            if(!physical.Add(path)){continue;}
            if(!File.Exists(path)||!path.EndsWith(".cs",StringComparison.OrdinalIgnoreCase)){throw new ManagementException("InvalidRequest","Compile対象に存在しないC#ファイルがあります: "+path);}
            length+=new FileInfo(path).Length;if(length>4*1024*1024||result.Count>=256){throw new ManagementException("Limit","ソースの容量・件数上限を超えています。");}
            var relative=Path.GetRelativePath(root,path).Replace('\\','/');
            if(relative.StartsWith("../",StringComparison.Ordinal)||Path.IsPathRooted(relative)){relative="Linked/"+ProjectId(path)+"_"+Path.GetFileName(path);}
            result.Add(new(relative,path));
        }
        return result.OrderBy(file=>file.Path,StringComparer.Ordinal).ToArray();
    }
    private static string[] Defines(JsonElement evaluation){
        var properties=evaluation.GetProperty("Properties");
        var result=(properties.GetProperty("DefineConstants").GetString()??"").Split([';',','],StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).ToHashSet(StringComparer.Ordinal);
        // SDKのBeforeCommon.targetsと同じ規則を、評価済みの版一覧へ適用する。Buildターゲットは実行しない。
        if(!string.Equals(properties.GetProperty("DisableImplicitFrameworkDefines").GetString(),"true",StringComparison.OrdinalIgnoreCase)&&properties.GetProperty("TargetFrameworkIdentifier").GetString()==".NETCoreApp"){
            var target=Version.Parse(properties.GetProperty("TargetFrameworkVersion").GetString()!.TrimStart('v','V'));
            result.Add("NET");result.Add("NETCOREAPP");result.Add("NET"+target.ToString().Replace('.','_'));
            foreach(var item in evaluation.GetProperty("Items").GetProperty("_NETCoreAppVersionsForDefines").EnumerateArray()){
                var identity=item.GetProperty("Identity").GetString()!;
                var version=Version.Parse(identity[(identity.LastIndexOf("=v",StringComparison.Ordinal)+2)..]);
                if(version>target){continue;}
                var prefix="NETCOREAPP";if(version.Major>=5){prefix="NET";}
                result.Add(prefix+version.ToString().Replace('.','_')+"_OR_GREATER");
            }
        }
        return result.Order(StringComparer.Ordinal).ToArray();
    }
    private static async Task<JsonElement> EvaluateAsync(string path,string target,CancellationToken cancellationToken,string configuration="Debug",bool resolveReferences=false){
        ValidateConfiguration(configuration);
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);deadline.CancelAfter(TimeSpan.FromSeconds(30));
        using var process=new Process{StartInfo=new(DotnetHost()){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=Path.GetDirectoryName(path)!,RedirectStandardOutput=true,RedirectStandardError=true}};
        foreach(var argument in new[]{"msbuild",path,"-nologo","-v:quiet","-getItem:Compile,_NETCoreAppVersionsForDefines,ReferencePath","-getProperty:DefineConstants,TargetFramework,TargetFrameworks,TargetFrameworkIdentifier,TargetFrameworkVersion,DisableImplicitFrameworkDefines,Configuration,Configurations,TargetPath,ProjectAssetsFile","-m:1","-nodeReuse:false","-p:Configuration="+configuration}){process.StartInfo.ArgumentList.Add(argument);}
        if(resolveReferences){process.StartInfo.ArgumentList.Add("-target:ResolveReferences");process.StartInfo.ArgumentList.Add("-p:BuildProjectReferences=false");}
        if(target.Length>0){if(target.Length>128||target.Any(character=>!char.IsAsciiLetterOrDigit(character)&&character is not '.' and not '-')){throw new ManagementException("InvalidRequest","ターゲット名が不正です。");}process.StartInfo.ArgumentList.Add("-p:TargetFramework="+target);}
        if(!process.Start()){throw new ManagementException("Unavailable","SDKを起動できません。");}
        try{
            var output=ReadOutputAsync(process.StandardOutput,deadline);var error=ReadOutputAsync(process.StandardError,deadline);
            await Task.WhenAll(output,error,process.WaitForExitAsync(deadline.Token));
            if(process.ExitCode!=0){throw new ManagementException("ProjectEvaluation",await error+await output);}
            using var json=JsonDocument.Parse(await output);return json.RootElement.Clone();
        }finally{
            if(!process.HasExited){process.Kill(entireProcessTree:true);await process.WaitForExitAsync();}
        }
    }
    private static async Task<string> ReadOutputAsync(StreamReader reader,CancellationTokenSource cancellation){
        var text=new StringBuilder();var buffer=new char[4096];
        while(true){
            var count=await reader.ReadAsync(buffer.AsMemory(),cancellation.Token);if(count==0){return text.ToString();}
            if(text.Length+count>4*1024*1024){cancellation.Cancel();throw new ManagementException("Limit","SDKの出力上限を超えました。");}text.Append(buffer,0,count);
        }
    }
    private static string FullPath(string path){
        ArgumentException.ThrowIfNullOrWhiteSpace(path);path=Path.GetFullPath(path);
        FileSystemInfo? item=new FileInfo(path);if(Directory.Exists(path)){item=new DirectoryInfo(path);}
        while(item is not null){
            if(item.LinkTarget is not null||item.Exists&&(item.Attributes&FileAttributes.ReparsePoint)!=0){throw new ManagementException("InvalidRequest","同期対象にシンボリックリンクは使えません。");}
            if(item is FileInfo file){item=file.Directory;}else{item=((DirectoryInfo)item).Parent;}
        }
        return path;
    }
    private static string ProjectId(string path){if(OperatingSystem.IsWindows()){path=path.ToUpperInvariant();}return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(path))).ToLowerInvariant();}
    private static string DotnetHost(){
        var configured=Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if(!string.IsNullOrWhiteSpace(configured)){
            if(Path.IsPathRooted(configured)&&File.Exists(configured)){return configured;}
            throw new ManagementException("Unavailable","DOTNET_HOST_PATHに既存のdotnetホストを指定してください。");
        }
        var name="dotnet";if(OperatingSystem.IsWindows()){name="dotnet.exe";}
        var runtime=new DirectoryInfo(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory()).Parent?.Parent?.Parent;
        if(runtime is not null&&File.Exists(Path.Combine(runtime.FullName,name))){return Path.Combine(runtime.FullName,name);}
        if(OperatingSystem.IsWindows()){
            var installed=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"dotnet",name);if(File.Exists(installed)){return installed;}
        }
        foreach(var directory in (Environment.GetEnvironmentVariable("PATH")??"").Split(Path.PathSeparator,StringSplitOptions.RemoveEmptyEntries)){
            var candidate=Path.Combine(directory,name);if(Path.IsPathRooted(candidate)&&File.Exists(candidate)){return candidate;}
        }
        throw new ManagementException("Unavailable","開発用SDKが必要です。DOTNET_HOST_PATHにdotnetホストを指定してください。");
    }
    public void Dispose(){
        if(Interlocked.Exchange(ref this.disposalStarted,1)!=0){this.disposed.Task.GetAwaiter().GetResult();return;}
        try{
            try{this.stopping.Cancel();}catch(ObjectDisposedException){}
            this.gate.Wait();
            try{this.projects.Clear();}finally{this.gate.Release();}
            this.disposed.TrySetResult();
        }catch(Exception error){this.disposed.TrySetException(error);throw;}
    }
}
