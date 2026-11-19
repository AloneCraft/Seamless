using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace Seamless.Management.Server;

internal interface IManagedDatabaseSourceOperations{
    bool CanOpenManagedDatabaseSource{get;}
    Task<string> OpenManagedDatabaseSourceAsync(string id,CancellationToken cancellationToken);
}

public sealed partial class ManagementService{
    private sealed record ManagedSource(string Owner,string Name,string TextHash);

    private static bool IsManagedSourceOwner(UserAccount user)=>user.PasswordHash.Length==0&&Can(user,"*",ManagementPermission.Manage);
    private static string ManagedTextHash(string text)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    internal string ManagedSourcePath(UserAccount user,string id){
        if(!IsManagedSourceOwner(user)){throw new ManagementException("Permission","管理ソースを開くにはホスト所有者の権限が必要です。");}
        lock(this.store.Gate){
            var draft=this.FindDraft(user,id);
            var active=this.store.Read<DefinitionVersion>("active",id);
            var definition=draft?.Definition??active?.Definition??throw new ManagementException("NotFound","DBの定義がありません。");
            Require(user,this.store.CurrentDatabase(definition),ManagementPermission.Edit);
            if(draft?.DatabaseChange is{Delete:true}){throw new ManagementException("Conflict","削除予定のDBはソースを開けません。");}
            var binding=this.WriteManagedSource(user,draft??new(definition,user.Name,0,active!.Version,active.CreatedAt),active,open:true)
                ??throw new ManagementException("Unsupported","管理ソースを開くには、DBのC#定義が1ファイルである必要があります。");
            this.store.Write(()=>this.store.Put("managedSource",id,binding));
            return this.ManagedFilePath(id,binding.Name);
        }
    }

    internal string OpenManagedDatabaseSource(UserAccount user,string id){
        var path=this.ManagedSourcePath(user,id);
        var executable="code";
        if(OperatingSystem.IsWindows()){
            using var registration=Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(@"vscode\shell\open\command");
            var command=registration?.GetValue(null) as string;
            var end=command is not null&&command.StartsWith('"')?command.IndexOf('"',1):-1;
            if(end<=1){throw new ManagementException("SourceEditor","VS Codeのインストールを確認してください。");}
            executable=command![1..end];
            if(!Path.IsPathFullyQualified(executable)||!Path.GetFileName(executable).Equals("Code.exe",StringComparison.OrdinalIgnoreCase)||!File.Exists(executable)){
                throw new ManagementException("SourceEditor","登録されたVS Codeの実行ファイルを確認できません。");
            }
        }
        var start=new ProcessStartInfo(executable){UseShellExecute=false};
        start.ArgumentList.Add("--new-window");
        start.ArgumentList.Add("--");
        start.ArgumentList.Add(path);
        try{using var process=Process.Start(start);}
        catch(System.ComponentModel.Win32Exception error){throw new ManagementException("SourceEditor","VS Codeで開けませんでした。"+error.Message);}
        return path;
    }

    private ManagedSource? WriteManagedSource(UserAccount actor,Draft draft,DefinitionVersion? active,bool open=false){
        var definition=draft.Definition;
        if(!IsManagedSourceOwner(actor)||definition.Kind!=DefinitionKind.Database||draft.DatabaseChange is{Delete:true}){return null;}
        var existing=this.store.Read<ManagedSource>("managedSource",definition.Id);
        var name=existing?.Name??active?.Database??definition.Database;
        var named=DatabaseDesigner.IsManagedDatabaseName(name);
        if(existing is not null&&existing.Owner!=actor.Name||definition.Source.Files is not [var source]||
            !named&&!name.All(char.IsAsciiDigit)||
            !open&&existing is null&&(!named||source.Path!=name+".cs"||definition.EntryType!=name+".Database")){return null;}
        var path=this.ManagedFilePath(definition.Id,name);
        if(File.Exists(path)){
            var hash=ManagedTextHash(ReadManagedText(path));
            if(hash!=existing?.TextHash&&hash!=ManagedTextHash(source.Text)){throw new ManagementException("Conflict","VS Codeで保存した変更があります。DBを開き直してください。");}
            if(hash==ManagedTextHash(source.Text)){return new(actor.Name,name,hash);}
        }
        else if(existing is not null){throw new ManagementException("NotFound","管理ソースのファイルが見つかりません。元の場所へ戻してください。");}
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        SourceWorkspace.Atomic(path,source.Text);
        return new(actor.Name,name,ManagedTextHash(source.Text));
    }

    private Draft? ReadManagedSource(UserAccount actor,string id,string key,Draft? draft){
        var binding=this.store.Read<ManagedSource>("managedSource",id);
        if(binding is null||binding.Owner!=actor.Name||draft?.DatabaseChange is{Delete:true}){return draft;}
        var active=this.store.Read<DefinitionVersion>("active",id);
        var definition=draft?.Definition??active?.Definition;
        if(definition?.Kind!=DefinitionKind.Database||definition.Source.Files is not [var source]){return draft;}
        Require(actor,this.store.CurrentDatabase(definition),ManagementPermission.Edit);
        var path=this.ManagedFilePath(id,binding.Name);
        if(!File.Exists(path)){throw new ManagementException("NotFound","管理ソースのファイルが見つかりません。元の場所へ戻してください。");}
        var text=ReadManagedText(path);
        var hash=ManagedTextHash(text);
        if(hash==binding.TextHash){return draft;}
        if(ManagedTextHash(source.Text)!=binding.TextHash&&source.Text!=text){throw new ManagementException("Conflict","管理ソースとManagerの下書きの両方が更新されています。");}
        var changed=definition with{Source=definition.Source with{Files=[source with{Text=text}]}};
        ValidateDefinition(changed);
        var baseVersion=active is not null&&active.Definition.Hash()==definition.Hash()?active.Version:draft?.BaseVersion??active!.Version;
        var updated=new Draft(changed,actor.Name,checked((draft?.Revision??0)+1),baseVersion,DateTimeOffset.UtcNow){
            DatabaseChange=draft?.DatabaseChange,QueryRenames=draft?.QueryRenames
        };
        this.store.Write(()=>{
            this.store.Put("draft",key,updated);
            this.store.Put("managedSource",id,binding with{TextHash=hash});
            this.store.AddHistory(actor.Name,this.store.CurrentDatabase(definition),"DraftSaved",id,updated.Revision);
        });
        return updated;
    }

    private string ManagedFilePath(string id,string name){
        if(string.IsNullOrEmpty(name)||!DatabaseDesigner.IsManagedDatabaseName(name)&&!name.All(char.IsAsciiDigit)){throw new InvalidDataException("管理ソースのDB名が不正です。");}
        var path=Path.Combine(this.store.Root,"sources",ManagedTextHash(id).ToLowerInvariant(),name+".cs");
        SourceWorkspace.CheckLinks(path);
        return path;
    }

    private static string ReadManagedText(string path){
        using var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read|FileShare.Delete);
        if(file.Length>16L<<20){throw new ManagementException("Limit","管理ソースが大きすぎます。");}
        using var reader=new StreamReader(file,new UTF8Encoding(false,true));
        try{return reader.ReadToEnd();}
        catch(DecoderFallbackException){throw new ManagementException("Validation","管理ソースをUTF-8で保存してください。");}
    }
}
