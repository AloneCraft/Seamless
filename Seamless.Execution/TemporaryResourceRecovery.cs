using System.Text.Json;
namespace Seamless.Execution;
public static class TemporaryResourceRecovery{
    public static IReadOnlyList<string> RecoverAbandoned(string temporaryDirectory){
        var root=Path.GetFullPath(temporaryDirectory);
        var recovered=new List<string>();
        if(!Directory.Exists(root)){return recovered;}
        foreach(var directory in Directory.EnumerateDirectories(root,"scope-*")){
            if((File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0){continue;}
            var name=Path.GetFileName(directory);
            if(name.Length!=38||!Guid.TryParseExact(name[6..],"N",out _)){continue;}
            var metadata=Path.Combine(directory,"owner.json");
            if(!File.Exists(metadata)){continue;}
            JsonDocument document;
            try{document=JsonDocument.Parse(File.ReadAllText(metadata));}
            catch(Exception error) when(error is JsonException or IOException or UnauthorizedAccessException){continue;}
            using(document){
                if(!document.RootElement.TryGetProperty("Format",out var format)||format.GetString()!="seamless-execution-v1"
                    ||!document.RootElement.TryGetProperty("Id",out var id)||id.GetString()!=name){continue;}
            }
            FileStream lease;
            try{lease=new(Path.Combine(directory,"owner.lock"),FileMode.Open,FileAccess.ReadWrite,FileShare.None);}
            catch(IOException){continue;}
            using(lease){
                foreach(var file in Directory.EnumerateFiles(directory)){
                    if(Path.GetFileName(file)=="owner.lock"){continue;}
                    File.Delete(file);
                }
            }
            File.Delete(Path.Combine(directory,"owner.lock"));
            Directory.Delete(directory);
            recovered.Add(directory);
        }
        return recovered;
    }
}
