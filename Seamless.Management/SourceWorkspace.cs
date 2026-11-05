using System.Text.Json;
using System.Xml.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Seamless.Management;

public sealed record WorkspaceState(int Format,DefinitionExport Current,SourceSnapshot Published,SourceSnapshot Synchronized,SourceSnapshot WithoutManager,string? SavedDraftHash,bool UndoConflict=false);
internal sealed record WorkspaceWrite(string Path,string? Before,string? After);
internal sealed record WorkspaceJournal(WorkspaceState State,WorkspaceWrite[] Writes);

/// <summary>通常のC#プロジェクトと個人下書きの同期。ファイル保存・復旧はビルドもDB公開も行わない。</summary>
public sealed class SourceWorkspace{
    public string DirectoryPath{get;}
    public WorkspaceState State{get;private set;}
    public DefinitionExport Current=>this.State.Current;
    public bool HasUnappliedChanges=>!Same(this.State.Published,this.Current.Definition.Source)||this.Current.Definition.Migration is not null;
    public bool NeedsDraftSave=>this.State.SavedDraftHash!=this.Current.Definition.Hash();
    private string Metadata=>Path.Combine(this.DirectoryPath,".seamless");
    private SourceWorkspace(string directory,WorkspaceState state){this.DirectoryPath=directory;this.State=state;}
    public static SourceWorkspace Create(string directory,DefinitionExport current,SourceSnapshot published,string repository){
        SourceEditor.Validate(current.Definition.Source);SourceEditor.Validate(published);repository=Path.GetFullPath(repository);
        if(!File.Exists(Path.Combine(repository,"Seamless.Database","Seamless.Database.csproj"))){throw new ManagementException("Unsupported","ビルドに使うSeamlessリポジトリを指定してください。");}
        directory=Path.GetFullPath(directory);CheckLinks(directory);
        if(Directory.Exists(directory)&&Directory.EnumerateFileSystemEntries(directory).Any()){throw new IOException("空のプロジェクト保存先を指定してください。");}
        Directory.CreateDirectory(directory);Directory.CreateDirectory(Path.Combine(directory,".seamless"));
        var workspace=new SourceWorkspace(directory,new(1,current,published,current.Definition.Source,current.Definition.Source,null));
        using var lease=workspace.Lock();
        var references=new XElement("ItemGroup");
        references.Add(new XElement("ProjectReference",new XAttribute("Include",Path.Combine(repository,"Seamless.Database","Seamless.Database.csproj"))));
        foreach(var generator in new[]{"Seamless.Database.Generator","Seamless.FLinq.Generator"}){references.Add(new XElement("ProjectReference",new XAttribute("Include",Path.Combine(repository,generator,generator+".csproj")),new XAttribute("OutputItemType","Analyzer"),new XAttribute("ReferenceOutputAssembly","false")));}
        var project=new XElement("Project",new XAttribute("Sdk","Microsoft.NET.Sdk"),new XElement("PropertyGroup",new XElement("TargetFramework","net10.0"),new XElement("LangVersion","14"),new XElement("Nullable","enable"),new XElement("AllowUnsafeBlocks","true"),new XElement("EnableDefaultCompileItems","false"),new XElement("InterceptorsNamespaces","$(InterceptorsNamespaces);Intercept"),new XElement("DefineConstants",string.Join(";",current.Definition.Source.Defines??[]))),references);
        project.Add(new XElement("ItemGroup",new XAttribute("Label","SeamlessSources"),current.Definition.Source.Files.Select(file=>new XElement("Compile",new XAttribute("Include",file.Path)))));
        File.WriteAllText(Path.Combine(directory,"Schema.csproj"),project.ToString());
        Directory.CreateDirectory(Path.Combine(directory,".vscode"));
        File.WriteAllText(Path.Combine(directory,".vscode","tasks.json"),JsonSerializer.Serialize(new{version="2.0.0",tasks=new[]{new{label="build",type="process",command="dotnet",args=new[]{"build","Schema.csproj"},group=new{kind="build",isDefault=true},problemMatcher=new[]{"$msCompile"}}}},ManagementJson.Options));
        File.WriteAllText(Path.Combine(directory,".gitignore"),"bin/\nobj/\n.seamless/\n");
        workspace.Commit(workspace.State,current.Definition.Source.Files.Select(file=>new WorkspaceWrite(file.Path,null,file.Text)).ToArray());return workspace;
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
    public DefinitionExport Synchronize(Definition manager){
        using var lease=this.Lock();this.AssertCurrent();
        if(manager.Id!=this.Current.Definition.Id){throw new ManagementException("Conflict","別のDB定義を同期できません。");}SourceEditor.Validate(manager.Source);
        var disk=this.ReadDisk();SourceSnapshot merged;var external=this.State.WithoutManager;var undoConflict=this.State.UndoConflict;
        try{merged=Merge(this.State.Synchronized,manager.Source,disk);}
        catch(ManagementException){Atomic(Path.Combine(this.Metadata,"conflict.json"),JsonSerializer.Serialize(new{baseline=this.State.Synchronized,manager,disk},ManagementJson.Options));throw;}
        // 通常の同期と「Manager編集だけの取消」は別の判定。新規宣言への外部編集は同期できても、安全な取消には依存解消が必要。
        if(!undoConflict){try{external=Merge(this.State.Synchronized,this.State.WithoutManager,disk);}catch(ManagementException){undoConflict=true;}}
        var state=this.State with{Current=this.Current with{Definition=manager with{Source=merged}},Synchronized=merged,WithoutManager=external,UndoConflict=undoConflict};
        if(state==this.State||JsonSerializer.Serialize(state,ManagementJson.Options)==JsonSerializer.Serialize(this.State,ManagementJson.Options)){return this.Current;}
        this.Commit(state,Writes(disk,merged));return this.Current;
    }
    public DefinitionExport UndoManager(){
        // 直前の外部保存も取り込み、Manager由来の変更だけを除く。重なりはSynchronizeで拒否する。
        this.Synchronize(this.Current.Definition);using var lease=this.Lock();this.AssertCurrent();
        if(this.State.UndoConflict){throw new ManagementException("Conflict","外部編集がManagerの変更に依存しています。差分を確認して取り消してください。");}
        var source=this.State.WithoutManager;var current=this.Current with{Definition=this.Current.Definition with{Source=source}};
        if(this.Current.Definition.Migration is not null){throw new ManagementException("Conflict","移行定義を伴う変更は差分を確認して取り消してください。");}
        this.Commit(this.State with{Current=current,Synchronized=source},Writes(this.State.Synchronized,source));return this.Current;
    }
    public void MarkDraftSaved(long revision,long baseVersion){
        using var lease=this.Lock();this.AssertCurrent();this.Commit(this.State with{Current=this.Current with{DraftRevision=revision,BaseVersion=baseVersion},SavedDraftHash=this.Current.Definition.Hash()},[]);
    }
    public void MarkPublished(Definition definition,long version){
        using var lease=this.Lock();this.AssertCurrent();if(!Same(definition.Source,this.State.Synchronized)){throw new ManagementException("Conflict","適用版と作業ソースが一致しません。");}
        this.Commit(this.State with{Published=definition.Source,Current=this.Current with{Definition=definition,BaseVersion=version},WithoutManager=definition.Source,SavedDraftHash=null,UndoConflict=false},[]);
    }
    private SourceSnapshot ReadDisk(){
        var files=new List<SourceFile>();foreach(var file in this.State.Synchronized.Files){var path=this.FilePath(file.Path);if(!File.Exists(path)){throw new ManagementException("Conflict","外部でファイルが削除されています。内容を保全しました: "+file.Path);}if(new FileInfo(path).Length>8*1024*1024){throw new ManagementException("InvalidRequest","ソースが大きすぎます。");}files.Add(new(file.Path,File.ReadAllText(path)));}
        var result=this.State.Synchronized with{Files=files.ToArray()};SourceEditor.Validate(result);return result;
    }
    public static SourceSnapshot Merge(SourceSnapshot baseline,SourceSnapshot manager,SourceSnapshot disk){
        var files=new List<SourceFile>();foreach(var path in baseline.Files.Concat(manager.Files).Concat(disk.Files).Select(file=>file.Path).Distinct(StringComparer.OrdinalIgnoreCase)){
            string? Text(SourceSnapshot source)=>source.Files.SingleOrDefault(file=>string.Equals(file.Path,path,StringComparison.OrdinalIgnoreCase))?.Text;
            var old=Text(baseline);var left=Text(manager);var right=Text(disk);string? merged;
            if(left==right){merged=left;}else if(left==old){merged=right;}else if(right==old){merged=left;}else{
                if(old is null||left is null||right is null){throw new ManagementException("Conflict","ファイルの追加・削除が競合しています: "+path);}
                var tree=CSharpSyntaxTree.ParseText(old);var a=CSharpSyntaxTree.ParseText(left).GetChanges(tree).ToArray();var b=CSharpSyntaxTree.ParseText(right).GetChanges(tree).ToArray();
                foreach(var x in a){foreach(var y in b){if(x==y){continue;}if(x.Span.Start<=y.Span.End&&y.Span.Start<=x.Span.End){throw new ManagementException("Conflict","同じ領域に変更があります: "+path);}}}
                merged=SourceText.From(old).WithChanges(a.Concat(b).Distinct().OrderBy(change=>change.Span.Start)).ToString();
            }if(merged is not null){files.Add(new(path,merged));}
        }
        var result=manager with{Files=files.ToArray()};SourceEditor.Validate(result);return result;
    }
    private static bool Same(SourceSnapshot first,SourceSnapshot second)=>(first with{Revision=""}).Hash()==(second with{Revision=""}).Hash();
    private static WorkspaceWrite[] Writes(SourceSnapshot before,SourceSnapshot after)=>before.Files.Concat(after.Files).Select(file=>file.Path).Distinct(StringComparer.OrdinalIgnoreCase).Select(path=>new WorkspaceWrite(path,before.Files.SingleOrDefault(file=>file.Path==path)?.Text,after.Files.SingleOrDefault(file=>file.Path==path)?.Text)).Where(write=>write.Before!=write.After).ToArray();
    private FileStream Lock(){CheckLinks(this.Metadata);return new(Path.Combine(this.Metadata,"workspace.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}
    private void AssertCurrent(){
        var path=Path.Combine(this.Metadata,"workspace.json");if(File.Exists(Path.Combine(this.Metadata,"pending.json"))){throw new ManagementException("Conflict","中断した同期を開き直して復旧してください。");}
        if(File.Exists(path)&&JsonSerializer.Serialize(Read<WorkspaceState>(path),ManagementJson.Options)!=JsonSerializer.Serialize(this.State,ManagementJson.Options)){throw new ManagementException("Conflict","別のManagerが同期記録を更新しました。開き直してください。");}
    }
    private void Commit(WorkspaceState state,WorkspaceWrite[] writes){
        var path=Path.Combine(this.DirectoryPath,"Schema.csproj");CheckLinks(path);var before=File.ReadAllText(path);var project=XElement.Parse(before);var group=project.Elements("ItemGroup").Single(element=>(string?)element.Attribute("Label")=="SeamlessSources");
        if(!group.Elements("Compile").Select(element=>(string?)element.Attribute("Include")).SequenceEqual(this.State.Synchronized.Files.Select(file=>file.Path))){throw new ManagementException("Conflict","プロジェクトのソース一覧が外部で変更されています。連携を確認してください。");}
        group.ReplaceNodes(state.Current.Definition.Source.Files.Select(file=>new XElement("Compile",new XAttribute("Include",file.Path))));var after=project.ToString();if(before!=after){writes=[..writes,new("Schema.csproj",before,after)];}
        var journal=new WorkspaceJournal(state,writes);Atomic(Path.Combine(this.Metadata,"pending.json"),JsonSerializer.Serialize(journal,ManagementJson.Options));this.Recover(journal);
    }
    private void Recover(WorkspaceJournal journal){
        foreach(var write in journal.Writes){var path=this.FilePath(write.Path);var current=ReadOptional(path);if(current!=write.Before&&current!=write.After){throw new ManagementException("Conflict","同期中に外部保存がありました。ファイルと復旧記録を保持しています: "+write.Path);}}
        foreach(var write in journal.Writes){var path=this.FilePath(write.Path);var current=ReadOptional(path);if(current==write.After){continue;}if(current!=write.Before){throw new ManagementException("Conflict","同期中に外部保存がありました: "+write.Path);}if(write.After is null){File.Delete(path);}else{Directory.CreateDirectory(Path.GetDirectoryName(path)!);Atomic(path,write.After);}}
        Atomic(Path.Combine(this.Metadata,"workspace.json"),JsonSerializer.Serialize(journal.State,ManagementJson.Options));this.State=journal.State;File.Delete(Path.Combine(this.Metadata,"pending.json"));
    }
    private static string? ReadOptional(string path){if(File.Exists(path)){return File.ReadAllText(path);}return null;}
    private string FilePath(string relative){if(relative!="Schema.csproj"){SourceEditor.Validate(this.Current.Definition.Source with{Files=[new(relative,"")]});}var path=Path.GetFullPath(Path.Combine(this.DirectoryPath,relative.Replace('\\',Path.DirectorySeparatorChar).Replace('/',Path.DirectorySeparatorChar)));CheckLinks(path);return path;}
    private static void CheckLinks(string path){var full=Path.GetFullPath(path);FileSystemInfo? item=new FileInfo(full);if(Directory.Exists(full)){item=new DirectoryInfo(full);}while(item is not null){if(item.LinkTarget is not null||item.Exists&&(item.Attributes&FileAttributes.ReparsePoint)!=0){throw new IOException("同期対象にリンクは使用できません: "+item.FullName);}if(item is FileInfo file){item=file.Directory;}else{item=((DirectoryInfo)item).Parent;}}}
    private static T Read<T>(string path){CheckLinks(path);if(new FileInfo(path).Length>64*1024*1024){throw new InvalidDataException("同期記録が大きすぎます。");}return JsonSerializer.Deserialize<T>(File.ReadAllText(path),ManagementJson.Options)??throw new InvalidDataException("同期記録がありません。");}
    private static void Atomic(string path,string text){CheckLinks(path);var temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";try{using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){using var writer=new StreamWriter(stream,leaveOpen:true);writer.Write(text);writer.Flush();stream.Flush(flushToDisk:true);}File.Move(temporary,path,overwrite:true);}finally{if(File.Exists(temporary)){File.Delete(temporary);}}}
}
