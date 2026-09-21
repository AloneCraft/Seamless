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
            if((File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0){continue;}
            var name=Path.GetFileName(directory);
            if(name.Length!=prefix.Length+32||!Guid.TryParseExact(name[prefix.Length..],"N",out _)){continue;}
            var metadata=Path.Combine(directory,"owner.json");
            if(!File.Exists(metadata)){continue;}
            JsonDocument document;
            try{document=JsonDocument.Parse(File.ReadAllText(metadata));}
            catch(Exception error) when(error is JsonException or IOException or UnauthorizedAccessException){continue;}
            using(document){
                if(!document.RootElement.TryGetProperty("Format",out var formatElement)||formatElement.GetString()!=format
                    ||!document.RootElement.TryGetProperty("Id",out var id)||id.GetString()!=name){continue;}
            }
            FileStream lease;
            try{lease=new(Path.Combine(directory,"owner.lock"),FileMode.Open,FileAccess.ReadWrite,FileShare.None);}
            catch(IOException){continue;}
            using(lease){
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
}
