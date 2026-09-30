using System.Text.Json;
namespace Seamless.Execution;
public static class TemporaryResourceRecovery{
    /// <summary>
    /// 異常終了で残った <see cref="ExecutionScope"/>(scope-*) と一時 <see cref="Store"/>(store-*) のフォルダを回収する。
    /// 所有者メタデータが正しく、所有者ロックを取れた(=使用中でない)フォルダだけを削除する。
    /// </summary>
    public static IReadOnlyList<string> RecoverAbandoned(string temporaryDirectory){
        var root=Path.GetFullPath(temporaryDirectory);
        var recovered=new List<string>();
        if(!Directory.Exists(root)){return recovered;}
        Recover(root,"scope-","seamless-execution-v1",recovered);
        Recover(root,"store-",Store.FormatName,recovered);
        return recovered;
    }
    private static void Recover(string root,string prefix,string format,List<string> recovered){
        foreach(var directory in Directory.EnumerateDirectories(root,prefix+"*")){
            try{
                RecoverDirectory(directory,prefix,format,recovered);
            }catch(Exception error) when(error is IOException or UnauthorizedAccessException){
                // 回収は最善努力。所有者が同時に破棄している、ほかの回収と競合している、などで失敗したら、次回に回収する。
            }
        }
    }
    private static void RecoverDirectory(string directory,string prefix,string format,List<string> recovered){
        if((File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0){return;}
        var name=Path.GetFileName(directory);
        if(name.Length!=prefix.Length+32||!Guid.TryParseExact(name[prefix.Length..],"N",out _)){return;}
        // 使用中のスコープ・ストアの owner.json には触れない。先に所有者ロックを取り、取れなければ(使用中なので)飛ばす。
        // 先に owner.json を読むと、使用中のスコープが破棄で owner.json を削除するのと競合して、破棄の側が共有違反で失敗する。
        var ownerLock=Path.Combine(directory,"owner.lock");
        if(!File.Exists(ownerLock)){return;}
        FileStream lease;
        try{lease=new(ownerLock,FileMode.Open,FileAccess.ReadWrite,FileShare.None);}
        catch(IOException){return;}
        using(lease){
            var metadata=Path.Combine(directory,"owner.json");
            if(!File.Exists(metadata)){return;}
            JsonDocument document;
            try{document=JsonDocument.Parse(File.ReadAllText(metadata));}
            catch(Exception error) when(error is JsonException or IOException or UnauthorizedAccessException){return;}
            using(document){
                if(!document.RootElement.TryGetProperty("Format",out var formatElement)||formatElement.GetString()!=format
                    ||!document.RootElement.TryGetProperty("Id",out var id)||id.GetString()!=name){return;}
            }
            foreach(var child in Directory.EnumerateDirectories(directory)){
                Directory.Delete(child,true);
            }
            foreach(var file in Directory.EnumerateFiles(directory)){
                if(Path.GetFileName(file)=="owner.lock"){continue;}
                File.Delete(file);
            }
        }
        File.Delete(Path.Combine(directory,"owner.lock"));
        Directory.Delete(directory);
        recovered.Add(directory);
    }
}
