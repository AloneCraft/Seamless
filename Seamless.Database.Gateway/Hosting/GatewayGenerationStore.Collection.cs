using System.Globalization;

namespace Seamless.Database.Gateway.Hosting;

public sealed record GatewayGenerationCollectionDiagnostic(string Path,string Message);
public sealed record GatewayGenerationCollectionResult(IReadOnlyList<string> Removed,IReadOnlyList<GatewayGenerationCollectionDiagnostic> Deferred);

public sealed partial class GatewayGenerationStore{
    // Called with every registered database exclusively leased, after the common checkpoint has pruned its WAL prefix.
    // Only this captured set is collected later; a concurrently built generation can never enter it.
    internal CollectionPlan PlanCollection(IEnumerable<GatewayGenerationReference> retained){
        var keep=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var reference in retained){
            if(reference.DatabaseId<=0||!Guid.TryParseExact(reference.GenerationId,"N",out _)){throw new InvalidDataException("Invalid retained generation identity.");}
            var expected=this.Resolve(Path.Combine(reference.DatabaseId.ToString(CultureInfo.InvariantCulture),reference.GenerationId,"manifest.json"));
            if(!expected.Equals(this.Resolve(reference.Manifest),StringComparison.OrdinalIgnoreCase)){throw new InvalidDataException("Invalid retained generation path.");}
            keep.Add(Path.GetDirectoryName(expected)!);
        }
        var candidates=new List<string>();var deferred=new List<GatewayGenerationCollectionDiagnostic>();
        try{
            foreach(var database in Directory.EnumerateDirectories(this.directory)){
                if(!int.TryParse(Path.GetFileName(database),NumberStyles.None,CultureInfo.InvariantCulture,out var id)||id<=0){continue;}
                try{
                    RejectLink(database);
                    foreach(var generation in Directory.EnumerateDirectories(database)){
                        if(!Guid.TryParseExact(Path.GetFileName(generation),"N",out _)||keep.Contains(generation)){continue;}
                        // Resolve the complete deletion target while the reference set is stable.
                        var target=this.Resolve(Path.GetRelativePath(this.directory,generation));
                        if(Path.GetDirectoryName(target)!=database){throw new InvalidDataException("Invalid generation collection target.");}
                        candidates.Add(target);
                    }
                }catch(Exception error)when(error is IOException or UnauthorizedAccessException){deferred.Add(new(database,error.Message));}
            }
        }catch(Exception error)when(error is IOException or UnauthorizedAccessException){deferred.Add(new(this.directory,error.Message));}
        return new(candidates,deferred);
    }
    private static void RejectLink(string path){if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0){throw new IOException("Generation collection does not traverse reparse points: "+path);}}
    internal sealed class CollectionPlan(List<string> candidates,List<GatewayGenerationCollectionDiagnostic> deferred){
        internal GatewayGenerationCollectionResult Collect(){
            var removed=new List<string>();
            foreach(var candidate in candidates){
                try{
                    if(!Directory.Exists(candidate)){continue;}
                    var pending=new Stack<string>();pending.Push(candidate);
                    while(pending.TryPop(out var directory)){
                        RejectLink(directory);
                        foreach(var entry in Directory.EnumerateFileSystemEntries(directory)){
                            RejectLink(entry);if(Directory.Exists(entry)){pending.Push(entry);}
                        }
                    }
                    Directory.Delete(candidate,true);removed.Add(candidate);
                }catch(Exception error)when(error is IOException or UnauthorizedAccessException){deferred.Add(new(candidate,error.Message));}
            }
            return new(removed.AsReadOnly(),deferred.AsReadOnly());
        }
    }
}
