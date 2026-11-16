using System.Text.Json;
using System.Xml.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Seamless.Management;

public sealed record PendingTablePublication(Guid Id,long DraftRevision,long BaseVersion,string DefinitionHash);
public sealed record WorkspaceState(int Format,DefinitionExport Current,SourceSnapshot Published,SourceSnapshot Synchronized,string? SavedDraftHash,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]PendingTablePublication? PendingTablePublication=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]Guid? ResolvedConflictId=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]Definition? SavedDraftDefinition=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]SourceProjectBinding? ProjectBinding=null){
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? ImportedProjectStateHash{get;init;}
    /// <summary>作業領域を作ったときのソース。下書きの保存を確認した記録が無いときに、サーバーの下書きが作ったときの中身のままかを見分けるためだけに使う。同期では変えない。</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public SourceSnapshot? CreatedSource{get;init;}
}
internal sealed record WorkspaceWrite(string Path,string? Before,string? After);
internal sealed record WorkspaceJournal(WorkspaceState State,WorkspaceWrite[] Writes);

/// <summary>通常のC#プロジェクトと個人下書きの同期。ファイル保存・復旧はビルドもDB公開も行わない。</summary>
public sealed partial class SourceWorkspace{
    public string DirectoryPath{get;}
    public WorkspaceState State{get;private set;}
    public DefinitionExport Current=>this.State.Current;
    public bool HasUnappliedChanges=>!Same(this.State.Published,this.Current.Definition.Source)||this.Current.Definition.Migration is not null;
    public bool NeedsDraftSave=>this.State.SavedDraftHash!=this.Current.Definition.Hash();
    public SourceWatcher? ActiveWatcher{get;set;}
    public SourceWatcher CreateWatcher(Func<IReadOnlyList<string>,Task> onChanged,int debounceMilliseconds=SourceWatcher.DefaultDebounceMilliseconds){
        var watcher=new SourceWatcher(this.DirectoryPath,onChanged,debounceMilliseconds);
        this.ActiveWatcher=watcher;
        return watcher;
    }
    private readonly string? importedMetadata;
    private string Metadata=>this.importedMetadata??Path.Combine(this.DirectoryPath,".seamless");
    internal bool IsDatabaseProject=>this.State.ProjectBinding is not null&&this.Metadata==Path.Combine(this.DirectoryPath,".seamless");
    private SourceWorkspace(string directory,WorkspaceState state,string? importedMetadata=null){this.DirectoryPath=directory;this.State=state;this.importedMetadata=importedMetadata;}
    public static SourceWorkspace Create(string directory,DefinitionExport current,SourceSnapshot published,string repository){
        SourceEditor.Validate(current.Definition.Source);SourceEditor.Validate(published);repository=Path.GetFullPath(repository);
        if(!File.Exists(Path.Combine(repository,"Seamless.Database","Seamless.Database.csproj"))){throw new ManagementException("Unsupported","ビルドに使うSeamlessリポジトリを指定してください。");}
        directory=Path.GetFullPath(directory);CheckLinks(directory);
        if(Directory.Exists(directory)&&Directory.EnumerateFileSystemEntries(directory).Any()){throw new IOException("空のプロジェクト保存先を指定してください。");}
        Directory.CreateDirectory(directory);Directory.CreateDirectory(Path.Combine(directory,".seamless"));
        var workspace=new SourceWorkspace(directory,new(1,current,published,current.Definition.Source,null){CreatedSource=current.Definition.Source.Freeze()});
        using var lease=workspace.Lock();
        var references=new XElement("ItemGroup");
        references.Add(new XElement("ProjectReference",new XAttribute("Include",Path.Combine(repository,"Seamless.Database","Seamless.Database.csproj"))));
        foreach(var generator in new[]{"Seamless.Database.Generator","Seamless.FLinq.Generator"}){references.Add(new XElement("ProjectReference",new XAttribute("Include",Path.Combine(repository,generator,generator+".csproj")),new XAttribute("OutputItemType","Analyzer"),new XAttribute("ReferenceOutputAssembly","false")));}
        // Manager が管理する DB の印を、DB 生成器の解析器(使用制限)と生成器へ渡す。
        references.Add(new XElement("CompilerVisibleProperty",new XAttribute("Include","SeamlessManagedDatabase")));
        var project=new XElement("Project",new XAttribute("Sdk","Microsoft.NET.Sdk"),new XElement("PropertyGroup",new XElement("TargetFramework","net10.0"),new XElement("LangVersion","14"),new XElement("Nullable","enable"),new XElement("AllowUnsafeBlocks","true"),new XElement("SeamlessManagedDatabase","true"),new XElement("EnableDefaultCompileItems","false"),new XElement("InterceptorsNamespaces","$(InterceptorsNamespaces);Intercept"),new XElement("DefineConstants",string.Join(";",current.Definition.Source.Defines??[]))),references);
        project.Add(new XElement("ItemGroup",new XAttribute("Label","SeamlessSources"),current.Definition.Source.Files.Select(file=>new XElement("Compile",new XAttribute("Include",file.Path)))));
        File.WriteAllText(Path.Combine(directory,"Schema.csproj"),project.ToString());
        var sdkPolicy=Path.Combine(repository,"global.json");
        if(File.Exists(sdkPolicy)){
            File.Copy(sdkPolicy,Path.Combine(directory,"global.json"));
        }
        Directory.CreateDirectory(Path.Combine(directory,".vscode"));
        File.WriteAllText(Path.Combine(directory,".vscode","tasks.json"),JsonSerializer.Serialize(new{version="2.0.0",tasks=new[]{new{label="build",type="process",command="dotnet",args=new[]{"build","Schema.csproj"},group=new{kind="build",isDefault=true},problemMatcher=new[]{"$msCompile"}}}},ManagementJson.Options));
        File.WriteAllText(Path.Combine(directory,".gitignore"),"bin/\nobj/\n.seamless/\n");
        workspace.Commit(workspace.State,current.Definition.Source.Files.Select(file=>new WorkspaceWrite(file.Path,null,file.Text)).ToArray());return workspace;
    }
    internal static string ResolvePackageFeed(string? overrideFeed=null){
        var configured=overrideFeed;
        if(string.IsNullOrWhiteSpace(configured)){configured=Environment.GetEnvironmentVariable("SEAMLESS_PACKAGE_FEED");}
        if(!string.IsNullOrWhiteSpace(configured)){
            if(!Directory.Exists(configured)){throw new ManagementException("PackageFeed","指定したSeamlessパッケージのフィードがありません。パスを確認してください。");}
            return Path.GetFullPath(configured);
        }
        var bundled=Path.Combine(AppContext.BaseDirectory,"packages");
        if(Directory.Exists(bundled)){return bundled;}
        var candidate=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","..","Work","VirtualDbFeed"));
        if(Directory.Exists(candidate)){return candidate;}
        var repository=Environment.GetEnvironmentVariable("SEAMLESS_SOURCE_REPOSITORY");
        if(!string.IsNullOrWhiteSpace(repository)&&File.Exists(Path.Combine(repository,"Seamless.slnx"))){
            var feed=Path.GetFullPath(Path.Combine(repository,"Work","VirtualDbFeed"));
            if(Directory.Exists(feed)){return feed;}
        }
        for(var directory=new DirectoryInfo(Environment.CurrentDirectory);directory is not null;directory=directory.Parent){
            var feed=Path.Combine(directory.FullName,"Work","VirtualDbFeed");
            if(File.Exists(Path.Combine(directory.FullName,"Seamless.slnx"))&&Directory.Exists(feed)){return feed;}
        }
        throw new ManagementException("PackageFeed","Seamlessの開発版パッケージが見つかりません。SEAMLESS_PACKAGE_FEEDへフィードのパスを指定してください。");
    }
    public static (SourceWorkspace Workspace,string ProjectPath) CreatePackageProject(string directory,DefinitionExport current,SourceSnapshot published,string databaseName,string? projectName=null,string? packageFeed=null,string? packageVersion=null){
        SourceEditor.Validate(current.Definition.Source);SourceEditor.Validate(published);directory=Path.GetFullPath(directory);CheckLinks(directory);
        if(Directory.Exists(directory)&&Directory.EnumerateFileSystemEntries(directory).Any()){throw new IOException("空のプロジェクト保存先を指定してください。");}
        var feed=ResolvePackageFeed(packageFeed);
        packageVersion??=ResolvePackageReferenceVersion(feed);
        Directory.CreateDirectory(directory);Directory.CreateDirectory(Path.Combine(directory,".seamless"));Directory.CreateDirectory(Path.Combine(directory,".vscode"));
        var name=string.IsNullOrWhiteSpace(projectName)?(string.IsNullOrWhiteSpace(databaseName)?"Schema":databaseName):projectName;
        foreach(var c in Path.GetInvalidFileNameChars()){name=name.Replace(c,'_');}
        var projectFile=Path.Combine(directory,$"{name}.csproj");
        var nugetConfig=new XDocument(new XElement("configuration",
            new XElement("packageSources",new XElement("clear"),
                new XElement("add",new XAttribute("key","SeamlessLocal"),new XAttribute("value",feed)),
                new XElement("add",new XAttribute("key","nuget.org"),new XAttribute("value","https://api.nuget.org/v3/index.json"))),
            new XElement("packageSourceMapping",
                new XElement("packageSource",new XAttribute("key","SeamlessLocal"),new XElement("package",new XAttribute("pattern","Seamless")),new XElement("package",new XAttribute("pattern","Seamless.*"))),
                new XElement("packageSource",new XAttribute("key","nuget.org"),new XElement("package",new XAttribute("pattern","*"))))));
        nugetConfig.Save(Path.Combine(directory,"NuGet.Config"));
        var globalJson=@"{
  ""sdk"": {
    ""version"": ""10.0.100"",
    ""rollForward"": ""latestFeature"",
    ""allowPrerelease"": false
  }
}";
        File.WriteAllText(Path.Combine(directory,"global.json"),globalJson);
        var tasksJson=JsonSerializer.Serialize(new{version="2.0.0",tasks=new[]{new{label="build",type="process",command="dotnet",args=new[]{"build",$"{name}.csproj"},group=new{kind="build",isDefault=true},problemMatcher=new[]{"$msCompile"}}}},ManagementJson.Options);
        File.WriteAllText(Path.Combine(directory,".vscode","tasks.json"),tasksJson);
        File.WriteAllText(Path.Combine(directory,".gitignore"),"bin/\nobj/\n.seamless/\n");
        var references=new XElement("ItemGroup");
        references.Add(new XElement("PackageReference",new XAttribute("Include","Seamless"),new XAttribute("Version",packageVersion)));
        references.Add(new XElement("PackageReference",new XAttribute("Include","Seamless.Database.Client"),new XAttribute("Version",packageVersion)));
        references.Add(new XElement("PackageReference",new XAttribute("Include","Seamless.Database.Generator"),new XAttribute("Version",packageVersion),new XAttribute("OutputItemType","Analyzer"),new XAttribute("ReferenceOutputAssembly","false")));
        references.Add(new XElement("PackageReference",new XAttribute("Include","Seamless.FLinq.Generator"),new XAttribute("Version",packageVersion),new XAttribute("OutputItemType","Analyzer"),new XAttribute("ReferenceOutputAssembly","false")));
        // Manager が管理する DB の印を、DB 生成器の解析器(使用制限)と生成器へ渡す。
        references.Add(new XElement("CompilerVisibleProperty",new XAttribute("Include","SeamlessManagedDatabase")));
        var project=new XElement("Project",new XAttribute("Sdk","Microsoft.NET.Sdk"),
            new XElement("PropertyGroup",
                new XElement("TargetFramework","net10.0"),
                new XElement("LangVersion","14"),
                new XElement("Nullable","enable"),
                new XElement("AllowUnsafeBlocks","true"),new XElement("SeamlessManagedDatabase","true"),
                new XElement("EnableDefaultCompileItems","true"),
                new XElement("CopyLocalLockFileAssemblies","true"),
                new XElement("InterceptorsNamespaces","$(InterceptorsNamespaces);Intercept"),
                new XElement("DefineConstants",string.Join(";",current.Definition.Source.Defines??[]))),
            references);
        File.WriteAllText(projectFile,project.ToString());
        var files=current.Definition.Source.Files;
        if(files.Length==0){
            files=[new("Tables.cs",$"using Seamless.Database;\nnamespace {name};\ninternal static partial class DatabaseGenerator;\npublic sealed partial class Container(DatabaseContainerOptions options):DatabaseContainer(options){{\n}}\n")];
            current=current with{Definition=current.Definition with{Source=current.Definition.Source with{Files=files}}};
            published=current.Definition.Source;
        }
        var projectFiles=files.Select(file=>new SourceProjectFile(file.Path,Path.Combine(directory,file.Path.Replace('/',Path.DirectorySeparatorChar)))).ToArray();
        var binding=new SourceProjectBinding(projectFile,projectFiles);
        var workspace=new SourceWorkspace(directory,new(1,current,published,current.Definition.Source,null,ProjectBinding:binding){CreatedSource=current.Definition.Source.Freeze()});
        using var lease=workspace.Lock();
        workspace.Commit(workspace.State,files.Select(file=>new WorkspaceWrite(file.Path,null,file.Text)).ToArray());
        return (workspace,projectFile);
    }
    public static SourceWorkspace Open(string directory){
        directory=Path.GetFullPath(directory);CheckLinks(directory);
        var metadata=Path.Combine(directory,".seamless");var pending=Path.Combine(metadata,"pending.json");
        var stateFile=Path.Combine(metadata,"workspace.json");var input=stateFile;
        if(!File.Exists(input)){input=pending;}
        WorkspaceState state;if(input==pending){state=Read<WorkspaceJournal>(input).State;}else{state=Read<WorkspaceState>(input);}
        if(state.Format!=1){throw new ManagementException("Unsupported","対応しない同期記録です。");}SourceEditor.Validate(state.Current.Definition.Source);
        var workspace=new SourceWorkspace(directory,state);using var lease=workspace.Lock();
        if(File.Exists(pending)){workspace.Recover(Read<WorkspaceJournal>(pending));}return workspace;
    }
    public DefinitionExport Synchronize(Definition manager,Definition? managerBase=null){
        using var lease=this.Lock();this.AssertCurrent();
        if(this.State.PendingTablePublication is not null){throw new ManagementException("Busy","単独保存の結果を確認してから作業を同期してください。");}
        if(manager.Id!=this.Current.Definition.Id){throw new ManagementException("Conflict","別のDB定義を同期できません。");}SourceEditor.Validate(manager.Source);
        if(this.ActiveConflict() is not null){throw ConflictPending();}
        var disk=this.ReadDisk();SourceSnapshot merged;
        var input=manager;
        try{if(managerBase is not null){manager=MergeDefinitions(managerBase,manager,this.Current.Definition);}merged=Merge(this.State.Synchronized,manager.Source,disk);}
        catch(ManagementException error)when(error.Code=="Conflict"){
            this.RecordConflict(new(Guid.NewGuid(),this.State.Synchronized,managerBase,input,disk));
            throw ConflictPending();
        }
        var state=this.State with{Current=this.Current with{Definition=manager with{Source=merged}},Synchronized=merged};
        if(state==this.State||JsonSerializer.Serialize(state,ManagementJson.Options)==JsonSerializer.Serialize(this.State,ManagementJson.Options)){return this.Current;}
        this.Commit(state,Writes(disk,merged));return this.Current;
    }
    public void MarkDraftSaved(long revision,long baseVersion,string? savedHash=null){
        using var lease=this.Lock();this.AssertCurrent();
        Definition? definition=this.Current.Definition;
        var currentHash=definition.Hash();
        savedHash??=currentHash;
        if(savedHash!=currentHash){definition=null;}
        this.MarkDraftSaved(revision,baseVersion,savedHash,definition);
    }
    public void MarkDraftSaved(Draft saved){
        using var lease=this.Lock();this.AssertCurrent();
        if(saved.Definition.Id!=this.Current.Definition.Id){throw new ManagementException("Conflict","保存応答の定義が作業領域と一致しません。");}
        this.MarkDraftSaved(saved.Revision,saved.BaseVersion,saved.Definition.Hash(),saved.Definition);
    }
    private void MarkDraftSaved(long revision,long baseVersion,string hash,Definition? definition){
        if(revision<this.Current.DraftRevision){return;}
        this.Commit(this.State with{Current=this.Current with{DraftRevision=revision,BaseVersion=baseVersion},SavedDraftHash=hash,SavedDraftDefinition=definition?.Freeze()},[]);
    }
    public void MarkPublished(Definition definition,long version){
        using var lease=this.Lock();this.AssertCurrent();if(!Same(definition.Source,this.State.Synchronized)){throw new ManagementException("Conflict","適用版と作業ソースが一致しません。");}
        this.Commit(this.State with{Published=definition.Source,Current=this.Current with{Definition=definition,BaseVersion=version},SavedDraftHash=null,SavedDraftDefinition=null},[]);
    }
    public void BeginTablePublication(Guid id,long revision,string hash){
        using var lease=this.Lock();
        this.AssertCurrent();
        if(this.ActiveConflict() is not null){throw ConflictPending();}
        if(this.State.PendingTablePublication is not null||this.Current.DraftRevision!=revision||this.Current.Definition.Hash()!=hash||this.NeedsDraftSave){
            throw new ManagementException("Conflict","公開結果の照会と個人下書きの同期を先に行ってください。");
        }
        this.Commit(this.State with{PendingTablePublication=new(id,revision,this.Current.BaseVersion,hash)},[]);
    }
    public void FinishTablePublication(OperationInfo operation,DefinitionVersion? published=null,Draft? remaining=null){
        using var lease=this.Lock();
        this.AssertCurrent();
        var pending=this.State.PendingTablePublication;
        if(pending is null||pending.Id!=operation.Id||operation.DefinitionId!=this.Current.Definition.Id){
            throw new ManagementException("Conflict","操作IDまたは対象DBが作業記録と一致しません。");
        }
        if(operation.State is OperationState.Failed or OperationState.Cancelled){
            this.Commit(this.State with{PendingTablePublication=null},[]);
            return;
        }
        if(operation.State!=OperationState.Succeeded||published is null||remaining is null||published.Version!=operation.PublishedVersion||
            published.Version!=pending.BaseVersion+1||remaining.BaseVersion!=published.Version||remaining.Revision!=pending.DraftRevision+1||
            published.Definition.Id!=operation.DefinitionId||remaining.Definition.Id!=operation.DefinitionId||
            this.Current.DraftRevision!=pending.DraftRevision||this.Current.Definition.Hash()!=pending.DefinitionHash||
            !Same(remaining.Definition.Source,this.Current.Definition.Source)){
            throw new ManagementException("Conflict","公開版と個人下書きが作業記録と一致しません。内容を保持しています。");
        }
        this.Commit(this.State with{Published=published.Definition.Source,Current=new(remaining.Definition,remaining.BaseVersion,remaining.Revision),
            SavedDraftHash=remaining.Definition.Hash(),SavedDraftDefinition=remaining.Definition.Freeze(),PendingTablePublication=null},[]);
    }
    private SourceSnapshot ReadDisk(){
        var files=new List<SourceFile>();foreach(var file in this.State.Synchronized.Files){var path=this.FilePath(file.Path);if(!File.Exists(path)){throw new ManagementException("Conflict","外部でファイルが削除されています。内容を保全しました: "+file.Path);}if(new FileInfo(path).Length>8*1024*1024){throw new ManagementException("InvalidRequest","ソースが大きすぎます。");}files.Add(new(file.Path,File.ReadAllText(path)));}
        var result=this.State.Synchronized with{Files=files.ToArray()};SourceEditor.Validate(result);return result;
    }
    public static SourceSnapshot Merge(SourceSnapshot baseline,SourceSnapshot manager,SourceSnapshot disk){
        var files=new List<SourceFile>();foreach(var path in baseline.Files.Concat(manager.Files).Concat(disk.Files).Select(file=>file.Path).Distinct(StringComparer.OrdinalIgnoreCase)){
            string? Text(SourceSnapshot source)=>source.Files.SingleOrDefault(file=>string.Equals(file.Path,path,StringComparison.OrdinalIgnoreCase))?.Text;
            var merged=MergeFile(path,Text(baseline),Text(manager),Text(disk));
            if(merged is not null){files.Add(new(path,merged));}
        }
        var result=manager with{Files=files.ToArray()};SourceEditor.Validate(result);return result;
    }
    private static string? MergeFile(string path,string? baseline,string? manager,string? disk){
        if(manager==disk||disk==baseline){return manager;}
        if(manager==baseline){return disk;}
        if(baseline is null||manager is null||disk is null){throw new ManagementException("Conflict","ファイルの追加・削除が競合しています: "+path);}
        var (a,b)=FileChanges(baseline,manager,disk);
        if(a.Any(x=>b.Any(y=>ChangesOverlap(x,y)))){throw new ManagementException("Conflict","同じ領域に変更があります: "+path);}
        return ApplyFileChanges(baseline,a.Concat(b));
    }
    private static (TextChange[] Manager,TextChange[] Disk) FileChanges(string baseline,string manager,string disk){
        var tree=CSharpSyntaxTree.ParseText(baseline);
        return (CSharpSyntaxTree.ParseText(manager).GetChanges(tree).ToArray(),CSharpSyntaxTree.ParseText(disk).GetChanges(tree).ToArray());
    }
    private static bool ChangesOverlap(TextChange x,TextChange y){
        if(x==y){return false;}
        // An insertion at a shared boundary can depend on the text the other edit replaces.
        if(x.Span.IsEmpty||y.Span.IsEmpty){return x.Span.IntersectsWith(y.Span);}
        return x.Span.OverlapsWith(y.Span);
    }
    private static string ApplyFileChanges(string baseline,IEnumerable<TextChange> changes)=>
        SourceText.From(baseline).WithChanges(changes.Distinct().OrderBy(change=>change.Span.Start)).ToString();
    public static Definition MergeDefinitions(Definition baseline,Definition left,Definition right){
        if(baseline.Id!=left.Id||baseline.Id!=right.Id||baseline.Database!=left.Database||baseline.Database!=right.Database){throw new ManagementException("Conflict","異なるDBの編集を統合できません。");}
        var header=MergeValue(baseline with{Source=baseline.Source,Migration=null},left with{Source=baseline.Source,Migration=null},right with{Source=baseline.Source,Migration=null},"定義の属性")!;
        var sourceHeader=MergeValue(baseline.Source with{Files=[],Revision=""},left.Source with{Files=[],Revision=""},right.Source with{Files=[],Revision=""},"ソースの参照・コンパイル条件")!;
        var source=Merge(baseline.Source,left.Source,right.Source);source=sourceHeader with{Files=source.Files,Revision=left.Source.Revision};
        var migration=MergeMigrations(baseline.Migration,left.Migration,right.Migration);return header with{Source=source,Migration=migration};
    }
    private static T? MergeValue<T>(T? baseline,T? left,T? right,string label){
        var old=JsonSerializer.Serialize(baseline,ManagementJson.Options);var a=JsonSerializer.Serialize(left,ManagementJson.Options);var b=JsonSerializer.Serialize(right,ManagementJson.Options);
        if(a==b||b==old){return left;}if(a==old){return right;}throw new ManagementException("Conflict",label+"の変更が競合しています。");
    }
    private static MigrationDefinition? MergeMigrations(MigrationDefinition? baseline,MigrationDefinition? left,MigrationDefinition? right){
        if(JsonSerializer.Serialize(left,ManagementJson.Options)==JsonSerializer.Serialize(baseline,ManagementJson.Options)){return right;}
        if(JsonSerializer.Serialize(right,ManagementJson.Options)==JsonSerializer.Serialize(baseline,ManagementJson.Options)||JsonSerializer.Serialize(left,ManagementJson.Options)==JsonSerializer.Serialize(right,ManagementJson.Options)){return left;}
        baseline??=new(1,[],[]);left??=new(1,[],[]);right??=new(1,[],[]);baseline=baseline.Freeze();left=left.Freeze();right=right.Freeze();
        var tables=baseline.Tables.Concat(left.Tables).Concat(right.Tables).Select(table=>table.Source).Distinct().Select(key=>MergeValue(baseline.Tables.SingleOrDefault(table=>table.Source==key),left.Tables.SingleOrDefault(table=>table.Source==key),right.Tables.SingleOrDefault(table=>table.Source==key),"表「"+key+"」の移行定義")).OfType<TableMigration>().ToArray();
        string[] Set(string[] before,string[] a,string[] b)=>before.Concat(a).Concat(b).Distinct().Where(key=>MergeValue(before.Contains(key),a.Contains(key),b.Contains(key),key)).ToArray();
        var dropped=Set(baseline.DroppedTables,left.DroppedTables,right.DroppedTables);var added=Set(baseline.NewTables??[],left.NewTables??[],right.NewTables??[]);
        if(tables.Length==0&&dropped.Length==0&&added.Length==0){return null;}return new(1,tables,dropped,added);
    }
    private static bool Same(SourceSnapshot first,SourceSnapshot second)=>(first with{Revision=""}).Hash()==(second with{Revision=""}).Hash();
    private static WorkspaceWrite[] Writes(SourceSnapshot before,SourceSnapshot after)=>before.Files.Concat(after.Files).Select(file=>file.Path).Distinct(StringComparer.OrdinalIgnoreCase).Select(path=>new WorkspaceWrite(path,before.Files.SingleOrDefault(file=>file.Path==path)?.Text,after.Files.SingleOrDefault(file=>file.Path==path)?.Text)).Where(write=>write.Before!=write.After).ToArray();
    private FileStream Lock(){CheckLinks(this.Metadata);return new(Path.Combine(this.Metadata,"workspace.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}
    private void AssertCurrent(){
        var path=Path.Combine(this.Metadata,"workspace.json");if(File.Exists(Path.Combine(this.Metadata,"pending.json"))){throw new ManagementException("Conflict","中断した同期を開き直して復旧してください。");}
        if(File.Exists(path)&&JsonSerializer.Serialize(Read<WorkspaceState>(path),ManagementJson.Options)!=JsonSerializer.Serialize(this.State,ManagementJson.Options)){throw new ManagementException("Conflict","別のManagerが同期記録を更新しました。開き直してください。");}
    }
    private void Commit(WorkspaceState state,WorkspaceWrite[] writes){
        if(state.ProjectBinding is not null){
            if(!state.Current.Definition.Source.Files.Select(file=>file.Path).Order(StringComparer.Ordinal).SequenceEqual(state.ProjectBinding.Files.Select(file=>file.Path).Order(StringComparer.Ordinal))){throw new ManagementException("Conflict","Compile対象を変更する場合はプロジェクトを開き直してください。");}
            this.CommitJournal(state,writes);return;
        }
        var path=Path.Combine(this.DirectoryPath,"Schema.csproj");CheckLinks(path);var before=File.ReadAllText(path);var project=XElement.Parse(before);var group=project.Elements("ItemGroup").Single(element=>(string?)element.Attribute("Label")=="SeamlessSources");
        if(!group.Elements("Compile").Select(element=>(string?)element.Attribute("Include")).SequenceEqual(this.State.Synchronized.Files.Select(file=>file.Path))){throw new ManagementException("Conflict","プロジェクトのソース一覧が外部で変更されています。連携を確認してください。");}
        group.ReplaceNodes(state.Current.Definition.Source.Files.Select(file=>new XElement("Compile",new XAttribute("Include",file.Path))));var after=project.ToString();if(before!=after){writes=[..writes,new("Schema.csproj",before,after)];}
        this.CommitJournal(state,writes);
    }
    private void CommitJournal(WorkspaceState state,WorkspaceWrite[] writes){var journal=new WorkspaceJournal(state,writes);Atomic(Path.Combine(this.Metadata,"pending.json"),JsonSerializer.Serialize(journal,ManagementJson.Options));this.Recover(journal);}
    private void Recover(WorkspaceJournal journal){
        foreach(var write in journal.Writes){var path=this.FilePath(write.Path);var current=ReadOptional(path);if(current!=write.Before&&current!=write.After){throw new ManagementException("Conflict","同期中に外部保存がありました。ファイルと復旧記録を保持しています: "+write.Path);}}
        foreach(var write in journal.Writes){
            var path=this.FilePath(write.Path);
            var current=ReadOptional(path);
            if(current==write.After){continue;}
            if(current!=write.Before){throw new ManagementException("Conflict","同期中に外部保存がありました: "+write.Path);}
            this.ActiveWatcher?.RecordSelfWritten(path,write.After);
            if(write.After is null){File.Delete(path);}else{Directory.CreateDirectory(Path.GetDirectoryName(path)!);Atomic(path,write.After);}
        }
        Atomic(Path.Combine(this.Metadata,"workspace.json"),JsonSerializer.Serialize(journal.State,ManagementJson.Options));this.State=journal.State;File.Delete(Path.Combine(this.Metadata,"pending.json"));
    }
    private static string? ReadOptional(string path){if(File.Exists(path)){return File.ReadAllText(path);}return null;}
    private string FilePath(string relative){
        if(this.State.ProjectBinding is{} binding){
            var file=binding.Files.SingleOrDefault(file=>file.Path==relative)??throw new ManagementException("InvalidRequest","Compile対象以外のファイルは編集できません。");
            CheckLinks(file.FullPath);return file.FullPath;
        }
        if(relative!="Schema.csproj"){SourceEditor.Validate(this.Current.Definition.Source with{Files=[new(relative,"")]});}var path=Path.GetFullPath(Path.Combine(this.DirectoryPath,relative.Replace('\\',Path.DirectorySeparatorChar).Replace('/',Path.DirectorySeparatorChar)));CheckLinks(path);return path;
    }
    internal static void CheckLinks(string path){var full=Path.GetFullPath(path);FileSystemInfo? item=new FileInfo(full);if(Directory.Exists(full)){item=new DirectoryInfo(full);}while(item is not null){if(item.LinkTarget is not null||item.Exists&&(item.Attributes&FileAttributes.ReparsePoint)!=0){throw new IOException("同期対象にリンクは使用できません: "+item.FullName);}if(item is FileInfo file){item=file.Directory;}else{item=((DirectoryInfo)item).Parent;}}}
    private static T Read<T>(string path){CheckLinks(path);if(new FileInfo(path).Length>64*1024*1024){throw new InvalidDataException("同期記録が大きすぎます。");}return JsonSerializer.Deserialize<T>(File.ReadAllText(path),ManagementJson.Options)??throw new InvalidDataException("同期記録がありません。");}
    private static void Atomic(string path,string text){CheckLinks(path);var temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";try{using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){using var writer=new StreamWriter(stream,leaveOpen:true);writer.Write(text);writer.Flush();stream.Flush(flushToDisk:true);}File.Move(temporary,path,overwrite:true);}finally{if(File.Exists(temporary)){File.Delete(temporary);}}}
}
